using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Events;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Tầng 1: Squad AI (Rule-Based Strategy & Coordination).
    /// Đánh giá tình hình toàn đội và chỉ đạo chiến lược đội hình (GDD Mục 20-23, 53).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class SquadAI
    {
        /// <summary>
        /// Đánh giá và cập nhật chiến lược đội hình (SquadStrategyType) dựa trên trạng thái chiến trường.
        /// Được gọi ở đầu Phase 1 (Planning) và trước mỗi lượt đi cá nhân (Mid-battle Adaptation).
        /// </summary>
        public static void EvaluateAndUpdateStrategy(BattleState state, TeamSide team)
        {
            if (state == null) return;

            var aliveAllies = new List<UnitState>();
            var aliveEnemies = new List<UnitState>();

            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u != null && u.IsAlive)
                {
                    if (u.Team == team)
                    {
                        aliveAllies.Add(u);
                    }
                    else
                    {
                        aliveEnemies.Add(u);
                    }
                }
            }

            if (aliveAllies.Count == 0 || aliveEnemies.Count == 0) return;

            // 1. Phân tích chỉ số phe ta (Allies)
            float totalAllyHpPct = 0f;
            bool hasAliveGuardian = false;
            bool allyBacklineInDanger = false;
            bool teamHasBacklineDiver = false;

            for (int i = 0; i < aliveAllies.Count; i++)
            {
                var ally = aliveAllies[i];
                totalAllyHpPct += ally.HPPercentage;

                if (ally.Role == UnitRole.Guardian)
                {
                    hasAliveGuardian = true;
                }

                bool isBacklineAlly = ally.Position.Row == RowType.Back || ally.Role == UnitRole.Ranger || ally.Role == UnitRole.Medic;
                if (isBacklineAlly && ally.HPPercentage < 0.45f)
                {
                    allyBacklineInDanger = true;
                }

                for (int s = 0; s < ally.Skills.Count; s++)
                {
                    // Chỉ tính là chủ lực nhảy hàng sau (Diver) nếu có kỹ năng chuyên biệt đánh hàng sau (như Backline Ambush của Assassin)
                    if (ally.Skills[s].Slot != SkillSlotType.BasicAttack && ally.Skills[s].CanTargetBackline && ally.BaseThreat >= 40f)
                    {
                        teamHasBacklineDiver = true;
                        break;
                    }
                }
            }

            float avgAllyHpPct = totalAllyHpPct / aliveAllies.Count;

            // 2. Phân tích chỉ số phe địch (Enemies)
            bool enemyHasBacklineDiver = false;
            bool anyEnemyLowHp = false;
            bool enemyHasAliveBackline = false;

            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                var enemy = aliveEnemies[i];

                if (enemy.HPPercentage < 0.35f)
                {
                    anyEnemyLowHp = true;
                }

                if (enemy.Position.Row == RowType.Back || enemy.Role == UnitRole.Ranger || enemy.Role == UnitRole.Medic)
                {
                    enemyHasAliveBackline = true;
                }

                for (int s = 0; s < enemy.Skills.Count; s++)
                {
                    if (enemy.Skills[s].Slot != SkillSlotType.BasicAttack && enemy.Skills[s].CanTargetBackline && enemy.BaseThreat >= 40f)
                    {
                        enemyHasBacklineDiver = true;
                        break;
                    }
                }
            }

            // 3. Logic thích nghi chiến thuật (Mid-Battle AI Adaptation - GDD Mục 23 & 53)
            SquadStrategyType oldStrategy = team == TeamSide.Player ? state.PlayerSquadStrategy : state.EnemySquadStrategy;
            SquadStrategyType newStrategy = oldStrategy;

            // Ưu tiên 1: Đội hình nguy kịch (< 40% HP trung bình) -> Chuyển sang phòng thủ
            if (avgAllyHpPct < 0.40f)
            {
                newStrategy = SquadStrategyType.Defensive;
            }
            // Ưu tiên 2: Có kẻ địch sắp chết (< 35% HP) -> Tập trung dồn sát thương kết liễu
            else if (anyEnemyLowHp)
            {
                newStrategy = SquadStrategyType.FocusFire;
            }
            // Ưu tiên 3: Logic chiến lược riêng theo từng phe
            else if (team == TeamSide.Enemy)
            {
                // Phe Địch: Nếu có quân nhảy hàng sau và người chơi còn hàng sau -> BacklineDive
                if (teamHasBacklineDiver && enemyHasAliveBackline)
                {
                    newStrategy = SquadStrategyType.BacklineDive;
                }
                else
                {
                    // Khi diver đã tử trận hoặc người chơi không còn hàng sau -> chuyển sang DirectAssault
                    newStrategy = SquadStrategyType.DirectAssault;
                }
            }
            else // TeamSide.Player
            {
                // Phe Người chơi: Nếu hàng sau bị đe dọa (HP < 45% hoặc địch có sát thủ) và còn Tank Guardian -> Bảo vệ hàng sau
                if (hasAliveGuardian && (allyBacklineInDanger || enemyHasBacklineDiver))
                {
                    newStrategy = SquadStrategyType.ProtectBackline;
                }
                else
                {
                    // Nếu điều kiện nguy cấp qua đi, quay lại cân bằng
                    if (oldStrategy == SquadStrategyType.ProtectBackline || oldStrategy == SquadStrategyType.Defensive)
                    {
                        newStrategy = SquadStrategyType.Balanced;
                    }
                }
            }

            // 4. Nếu chiến lược thay đổi, cập nhật BattleState và phát CombatEvent
            if (newStrategy != oldStrategy)
            {
                if (team == TeamSide.Player)
                {
                    state.PlayerSquadStrategy = newStrategy;
                }
                else
                {
                    state.EnemySquadStrategy = newStrategy;
                }

                state.EnqueueEvent(new CombatEvent
                {
                    EventType = CombatEventType.SquadStrategyChanged,
                    TurnNumber = state.CurrentTurn,
                    Description = $"{team} Squad AI adapted strategy: {oldStrategy} -> {newStrategy}"
                });
            }
        }

        /// <summary>
        /// Tính điểm cộng chỉ đạo của Squad AI (SquadDirectiveBonus) cho một mục tiêu candidateTarget (GDD Mục 22).
        /// </summary>
        public static float GetSquadTargetBonus(BattleState state, UnitState actor, UnitState candidateTarget)
        {
            if (state == null || actor == null || candidateTarget == null) return 0f;

            SquadStrategyType strategy = actor.Team == TeamSide.Player ? state.PlayerSquadStrategy : state.EnemySquadStrategy;
            float bonus = 0f;

            switch (strategy)
            {
                case SquadStrategyType.BacklineDive:
                    if (candidateTarget.Position.Row == RowType.Back)
                    {
                        bonus += 35f;
                    }
                    break;

                case SquadStrategyType.FocusFire:
                    if (candidateTarget.IsMarked || candidateTarget.HPPercentage < 0.4f)
                    {
                        bonus += 30f;
                    }
                    break;

                case SquadStrategyType.AntiHeal:
                    if (candidateTarget.Role == UnitRole.Medic)
                    {
                        bonus += 35f;
                    }
                    break;

                case SquadStrategyType.DirectAssault:
                    if (candidateTarget.Position.Row == RowType.Front)
                    {
                        bonus += 20f;
                    }
                    break;

                case SquadStrategyType.ProtectBackline:
                    bool hasBacklineSkill = false;
                    for (int i = 0; i < candidateTarget.Skills.Count; i++)
                    {
                        if (candidateTarget.Skills[i].CanTargetBackline)
                        {
                            hasBacklineSkill = true;
                            break;
                        }
                    }
                    if (hasBacklineSkill)
                    {
                        bonus += 25f;
                    }
                    break;
            }

            return bonus;
        }
    }
}
