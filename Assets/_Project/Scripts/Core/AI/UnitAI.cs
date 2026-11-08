using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Tầng 2: Unit AI (Utility Scoring System & Execution).
    /// Chấm điểm chọn mục tiêu và kỹ năng tối ưu, đồng thời lưu trữ AIDecisionRecord cho F3 Debug (GDD Mục 14, 17, 19).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class UnitAI
    {
        /// <summary>
        /// Thực thi trọn vẹn lượt hành động của một Unit theo Utility AI:
        /// Observe -> Evaluate Targets & Actions -> Choose Best Action -> Execute Skill.
        /// </summary>
        public static void ExecuteUnitTurn(BattleState state, UnitState actor)
        {
            if (state == null || actor == null || !actor.IsAlive || actor.IsStunned)
            {
                return;
            }

            SquadStrategyType activeSquadStrategy = actor.Team == TeamSide.Player
                ? state.PlayerSquadStrategy
                : state.EnemySquadStrategy;

            var decisionLog = new UnitAIDecisionLog
            {
                TurnNumber = state.CurrentTurn,
                UnitId = actor.InstanceId,
                UnitName = actor.Name,
                ActiveSquadStrategy = activeSquadStrategy
            };

            // 1. Phân loại quân số còn sống của cả 2 phe (Không dùng LINQ)
            var aliveAllies = new List<UnitState>();
            var aliveEnemies = new List<UnitState>();

            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u != null && u.IsAlive)
                {
                    if (u.Team == actor.Team)
                    {
                        aliveAllies.Add(u);
                    }
                    else
                    {
                        aliveEnemies.Add(u);
                    }
                }
            }

            bool allAlliesHighHp = true;
            for (int i = 0; i < aliveAllies.Count; i++)
            {
                if (aliveAllies[i].HPPercentage <= 0.85f)
                {
                    allAlliesHighHp = false;
                    break;
                }
            }

            // 2. Duyệt qua toàn bộ các kỹ năng đã sẵn sàng (Ready Skills)
            SkillInstance chosenSkill = null;
            var chosenTargets = new List<UnitState>();
            float bestActionScore = float.NegativeInfinity;

            foreach (var skill in actor.GetReadySkills())
            {
                if (skill == null) continue;

                // Bỏ qua kỹ năng ở chế độ Manual (chỉ người chơi được bấm kích hoạt ở Phase Planning)
                if (skill.ControlMode == SkillControlMode.Manual) continue;

                bool isOffensive = skill.TargetType == SkillTargetType.SingleEnemy ||
                                   skill.TargetType == SkillTargetType.AllEnemies ||
                                   skill.TargetType == SkillTargetType.EnemyRow;

                if (isOffensive)
                {
                    // Lọc kẻ địch hợp lệ theo luật tầm đánh và hàng Front/Back
                    var validEnemies = new List<UnitState>();
                    for (int i = 0; i < aliveEnemies.Count; i++)
                    {
                        if (CombatResolver.IsValidTarget(state, actor, skill, aliveEnemies[i]))
                        {
                            validEnemies.Add(aliveEnemies[i]);
                        }
                    }

                    if (validEnemies.Count == 0) continue;

                    UnitState bestTargetForSkill = null;
                    TargetScoreBreakdown bestTargetScore = null;
                    float bestTargetTotal = float.NegativeInfinity;

                    for (int i = 0; i < validEnemies.Count; i++)
                    {
                        var enemy = validEnemies[i];
                        var tb = TargetEvaluator.EvaluateEnemyTarget(state, actor, skill, enemy);
                        AddOrUpdateEvaluatedTarget(decisionLog.EvaluatedTargets, tb);

                        if (tb.TotalScore > bestTargetTotal)
                        {
                            bestTargetTotal = tb.TotalScore;
                            bestTargetForSkill = enemy;
                            bestTargetScore = tb;
                        }
                    }

                    if (bestTargetForSkill != null)
                    {
                        // Kiểm tra điều kiện thi triển kỹ năng do người chơi cấu hình (Player-Configurable AI)
                        if (skill.Slot != SkillSlotType.BasicAttack && actor.AIPolicy != null &&
                            !actor.AIPolicy.IsSkillConditionMet(state, actor, skill, bestTargetForSkill, aliveEnemies.Count))
                        {
                            continue;
                        }

                        var actionScore = ActionEvaluator.EvaluateOffensiveAction(
                            state, actor, skill, bestTargetForSkill, bestTargetScore, aliveEnemies.Count);
                        decisionLog.EvaluatedActions.Add(actionScore);

                        if (actionScore.TotalScore > bestActionScore)
                        {
                            bestActionScore = actionScore.TotalScore;
                            chosenSkill = skill;
                            chosenTargets.Clear();

                            if (skill.TargetType == SkillTargetType.SingleEnemy)
                            {
                                chosenTargets.Add(bestTargetForSkill);
                            }
                            else if (skill.TargetType == SkillTargetType.AllEnemies)
                            {
                                for (int i = 0; i < aliveEnemies.Count; i++)
                                {
                                    chosenTargets.Add(aliveEnemies[i]);
                                }
                            }
                            else if (skill.TargetType == SkillTargetType.EnemyRow)
                            {
                                for (int i = 0; i < aliveEnemies.Count; i++)
                                {
                                    if (aliveEnemies[i].Position.Row == bestTargetForSkill.Position.Row)
                                    {
                                        chosenTargets.Add(aliveEnemies[i]);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Kỹ năng hỗ trợ (Heal, Shield, ProtectAlly, Self Buff)
                    UnitState targetAlly = null;

                    if (skill.TargetType == SkillTargetType.Self)
                    {
                        targetAlly = actor;
                    }
                    else if (skill.EffectType == SkillEffectType.ProtectAlly)
                    {
                        // Guardian chọn bảo vệ đồng minh yếu hoặc hàng sau (không tự bảo vệ mình)
                        UnitState bestProtected = null;
                        float lowestProtectScore = float.MaxValue;
                        for (int i = 0; i < aliveAllies.Count; i++)
                        {
                            var ally = aliveAllies[i];
                            if (ally.InstanceId == actor.InstanceId) continue;

                            bool isBack = ally.Position.Row == RowType.Back ||
                                          ally.Role == UnitRole.Ranger ||
                                          ally.Role == UnitRole.Medic;

                            // Hàng sau được ưu tiên hơn hàng trước
                            float priority = ally.HPPercentage + (isBack ? 0f : 1.0f);
                            if (priority < lowestProtectScore)
                            {
                                lowestProtectScore = priority;
                                bestProtected = ally;
                            }
                        }
                        targetAlly = bestProtected;
                    }
                    else
                    {
                        // LowestHpAlly hoặc SingleAlly: Tìm đồng minh có % HP thấp nhất
                        UnitState lowestHpAlly = null;
                        float minHpPct = float.MaxValue;
                        for (int i = 0; i < aliveAllies.Count; i++)
                        {
                            var ally = aliveAllies[i];
                            if (ally.HPPercentage < minHpPct)
                            {
                                minHpPct = ally.HPPercentage;
                                lowestHpAlly = ally;
                            }
                        }
                        targetAlly = lowestHpAlly ?? actor;
                    }

                    if (targetAlly != null)
                    {
                        // Kiểm tra điều kiện thi triển kỹ năng do người chơi cấu hình (Player-Configurable AI)
                        if (skill.Slot != SkillSlotType.BasicAttack && actor.AIPolicy != null &&
                            !actor.AIPolicy.IsSkillConditionMet(state, actor, skill, targetAlly, aliveEnemies.Count))
                        {
                            continue;
                        }

                        var actionScore = ActionEvaluator.EvaluateSupportAction(
                            state, actor, skill, targetAlly, allAlliesHighHp);
                        decisionLog.EvaluatedActions.Add(actionScore);

                        if (actionScore.TotalScore > bestActionScore)
                        {
                            bestActionScore = actionScore.TotalScore;
                            chosenSkill = skill;
                            chosenTargets.Clear();
                            chosenTargets.Add(targetAlly);
                        }
                    }
                }
            }

            // 3. Fallback: Nếu không chọn được kỹ năng nào (hoặc không đủ năng lượng/cooldown), dùng đòn đánh cơ bản
            if (chosenSkill == null || chosenTargets.Count == 0)
            {
                SkillInstance basicAttack = actor.GetBasicAttack();
                if (basicAttack != null)
                {
                    var validEnemies = new List<UnitState>();
                    for (int i = 0; i < aliveEnemies.Count; i++)
                    {
                        if (CombatResolver.IsValidTarget(state, actor, basicAttack, aliveEnemies[i]))
                        {
                            validEnemies.Add(aliveEnemies[i]);
                        }
                    }

                    UnitState bestEnemy = null;
                    TargetScoreBreakdown bestTargetScore = null;
                    float bestScoreVal = float.NegativeInfinity;

                    for (int i = 0; i < validEnemies.Count; i++)
                    {
                        var enemy = validEnemies[i];
                        var tb = TargetEvaluator.EvaluateEnemyTarget(state, actor, basicAttack, enemy);
                        AddOrUpdateEvaluatedTarget(decisionLog.EvaluatedTargets, tb);

                        if (tb.TotalScore > bestScoreVal)
                        {
                            bestScoreVal = tb.TotalScore;
                            bestEnemy = enemy;
                            bestTargetScore = tb;
                        }
                    }

                    if (bestEnemy != null)
                    {
                        chosenSkill = basicAttack;
                        chosenTargets.Clear();
                        chosenTargets.Add(bestEnemy);

                        var basicAction = ActionEvaluator.EvaluateOffensiveAction(
                            state, actor, basicAttack, bestEnemy, bestTargetScore, aliveEnemies.Count);
                        decisionLog.EvaluatedActions.Add(basicAction);
                    }
                }
            }

            // 4. Lưu vết tư duy vào Decision Log để phục vụ F3 Explainable AI
            decisionLog.FinalChosenSkillName = chosenSkill != null ? chosenSkill.Name : "None";
            decisionLog.FinalChosenTargetName = (chosenTargets.Count > 0 && chosenTargets[0] != null) ? chosenTargets[0].Name : "None";
            state.LastUnitDecisions[actor.InstanceId] = decisionLog;

            // 5. Thực thi kỹ năng qua CombatResolver
            if (chosenSkill != null && chosenTargets.Count > 0)
            {
                CombatResolver.ExecuteSkill(state, actor, chosenSkill, chosenTargets, decisionLog.FormatForDebugUI());
            }
        }

        private static void AddOrUpdateEvaluatedTarget(List<TargetScoreBreakdown> list, TargetScoreBreakdown tb)
        {
            if (list == null || tb == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].TargetUnitId == tb.TargetUnitId)
                {
                    if (tb.TotalScore > list[i].TotalScore)
                    {
                        list[i] = tb;
                    }
                    return;
                }
            }
            list.Add(tb);
        }
    }
}
