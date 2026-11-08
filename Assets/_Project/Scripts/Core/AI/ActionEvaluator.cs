using System;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Bước B: Chấm điểm chọn Hành động / Kỹ năng (Action Selection - GDD Mục 17 & 44).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class ActionEvaluator
    {
        /// <summary>
        /// Tính toán điểm số Action cho kỹ năng tấn công/gây hiệu ứng lên mục tiêu địch tốt nhất.
        /// </summary>
        public static ActionScoreBreakdown EvaluateOffensiveAction(
            BattleState state,
            UnitState actor,
            SkillInstance skill,
            UnitState target,
            TargetScoreBreakdown targetScore,
            int aliveEnemyCount)
        {
            if (skill == null || target == null || targetScore == null)
            {
                return new ActionScoreBreakdown();
            }

            // 1. BaseUtilityScore: Sát thương cơ bản và diện rộng (AoE)
            float baseUtilityScore = skill.PowerMultiplier * 25f;
            if (skill.TargetType == SkillTargetType.AllEnemies && aliveEnemyCount >= 3)
            {
                baseUtilityScore += 20f;
            }

            // 2. TacticalFitScore: Độ phù hợp chiến thuật với mục tiêu
            float tacticalFitScore = targetScore.TotalScore * 0.5f;

            if (skill.EffectType == SkillEffectType.Stun && !target.IsStunned)
            {
                tacticalFitScore += 25f;
            }

            if (skill.EffectType == SkillEffectType.Mark && !target.IsMarked)
            {
                tacticalFitScore += 25f;
            }

            if (skill.EffectType == SkillEffectType.ExecuteDamage && target.HPPercentage < 0.40f)
            {
                tacticalFitScore += 35f;
            }

            // Điểm cộng từ Khuynh hướng hành vi (BehaviorPreset) của Player AI Policy (GDD Section 18)
            if (actor != null && actor.AIPolicy != null)
            {
                tacticalFitScore += actor.AIPolicy.GetActionPresetBonus(skill);
            }

            // 3. UrgencyScore: Điểm ưu tiên kỹ năng Ultimate
            float urgencyScore = (skill.Slot == SkillSlotType.Ultimate) ? 20f : 0f;

            return new ActionScoreBreakdown
            {
                SkillId = skill.SkillId,
                SkillName = skill.Name,
                ChosenTargetId = target.InstanceId,
                ChosenTargetName = target.Name,
                BaseUtilityScore = baseUtilityScore,
                TacticalFitScore = tacticalFitScore,
                UrgencyScore = urgencyScore
            };
        }

        /// <summary>
        /// Tính toán điểm số Action cho kỹ năng hỗ trợ (Heal, Shield, ProtectAlly, Self Buff).
        /// </summary>
        public static ActionScoreBreakdown EvaluateSupportAction(
            BattleState state,
            UnitState actor,
            SkillInstance skill,
            UnitState targetAlly,
            bool allAlliesHighHp)
        {
            if (state == null || actor == null || skill == null || targetAlly == null)
            {
                return new ActionScoreBreakdown();
            }

            // 1. BaseUtilityScore
            float baseUtilityScore = skill.PowerMultiplier * 25f;

            // 2. TacticalFitScore
            float tacticalFitScore = 10f;
            if (actor != null && actor.AIPolicy != null)
            {
                tacticalFitScore += actor.AIPolicy.GetActionPresetBonus(skill);
            }

            // 3. UrgencyScore: Tính theo mức độ cấp bách của đồng minh
            float urgencyScore = (skill.Slot == SkillSlotType.Ultimate) ? 20f : 0f;

            if (skill.EffectType == SkillEffectType.Heal)
            {
                if (targetAlly.HPPercentage < 0.65f)
                {
                    urgencyScore += (1f - targetAlly.HPPercentage) * 90f;
                }
                else if (allAlliesHighHp)
                {
                    // Toàn đội còn đầy máu (> 85% HP) -> phạt nặng để Medic đánh thường thay vì lãng phí chiêu hồi máu
                    urgencyScore -= 50f;
                }
            }
            else if (skill.EffectType == SkillEffectType.ProtectAlly)
            {
                SquadStrategyType squadStrategy = actor.Team == TeamSide.Player ? state.PlayerSquadStrategy : state.EnemySquadStrategy;
                bool isBacklineAlly = targetAlly.Position.Row == RowType.Back ||
                                      targetAlly.Role == UnitRole.Ranger ||
                                      targetAlly.Role == UnitRole.Medic;

                if (squadStrategy == SquadStrategyType.ProtectBackline || targetAlly.HPPercentage < 0.60f)
                {
                    urgencyScore += isBacklineAlly ? 65f : 40f;
                }
                else
                {
                    urgencyScore += isBacklineAlly ? 30f : 10f;
                }
            }
            else if (skill.EffectType == SkillEffectType.Shield)
            {
                if (targetAlly.HPPercentage < 0.70f)
                {
                    urgencyScore += (1f - targetAlly.HPPercentage) * 50f;
                }
                else
                {
                    urgencyScore += 15f;
                }
            }

            return new ActionScoreBreakdown
            {
                SkillId = skill.SkillId,
                SkillName = skill.Name,
                ChosenTargetId = targetAlly.InstanceId,
                ChosenTargetName = targetAlly.Name,
                BaseUtilityScore = baseUtilityScore,
                TacticalFitScore = tacticalFitScore,
                UrgencyScore = urgencyScore
            };
        }
    }
}
