using System;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Bước A: Chấm điểm chọn Mục tiêu (Target Selection - GDD Mục 16).
    /// Tính toán bảng phân rã điểm số Utility cho mọi mục tiêu hợp lệ để AI chọn mục tiêu tối ưu.
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class TargetEvaluator
    {
        /// <summary>
        /// Tính toán chi tiết điểm số của một mục tiêu địch đối với hành động tấn công của actor bằng skill.
        /// </summary>
        public static TargetScoreBreakdown EvaluateEnemyTarget(BattleState state, UnitState actor, SkillInstance skill, UnitState target)
        {
            if (state == null || actor == null || skill == null || target == null)
            {
                return new TargetScoreBreakdown();
            }

            // 1. ThreatScore: Mức độ đe dọa từ mục tiêu (Sát thương và Threat cơ bản)
            float threatScore = target.BaseThreat + (target.Attack * 0.8f);

            // 2. StrategicValueScore: Giá trị chiến lược (Healer hoặc DPS chủ lực)
            float strategicValueScore = (target.Role == UnitRole.Medic || target.Role == UnitRole.Mage || target.Role == UnitRole.Ranger) ? 25f : 10f;

            // 3. KillProbabilityScore: Khả năng dứt điểm mục tiêu
            int estimatedDamage = CombatResolver.CalculateDamage(state, actor, skill, target);
            int effectiveHp = target.CurrentHP + target.Shield;
            float killProbabilityScore;
            if (estimatedDamage >= effectiveHp)
            {
                killProbabilityScore = 50f; // Dứt điểm chắc chắn
            }
            else
            {
                killProbabilityScore = Math.Min(40f, ((float)estimatedDamage / Math.Max(1, effectiveHp)) * 40f);
            }

            // 4. VulnerabilityScore: Tình trạng suy yếu (Thấp máu, bị Marked, bị Stun)
            float vulnerabilityScore = (1f - target.HPPercentage) * 30f +
                                       (target.IsMarked ? 20f : 0f) +
                                       (target.IsStunned ? 10f : 0f);

            // 5. RolePriorityScore: Ưu tiên mục tiêu theo vai trò của actor
            float rolePriorityScore = 0f;
            switch (actor.Role)
            {
                case UnitRole.Ranger:
                    if (target.Role == UnitRole.Medic || target.Role == UnitRole.Mage || target.BaseThreat >= 30f)
                    {
                        rolePriorityScore += 20f;
                    }
                    break;

                case UnitRole.Vanguard:
                    if (target.Position.Row == RowType.Front)
                    {
                        rolePriorityScore += 15f;
                    }
                    break;

                case UnitRole.Guardian:
                    if (target.Position.Row == RowType.Front)
                    {
                        rolePriorityScore += 10f;
                    }
                    break;

                case UnitRole.Mage:
                    if (target.Position.Row == RowType.Front || target.Role == UnitRole.Guardian)
                    {
                        rolePriorityScore += 10f;
                    }
                    break;
            }

            // Cộng thêm điểm ưu tiên mục tiêu do người chơi cấu hình qua UnitAIPolicy (GDD Section 19)
            if (actor.AIPolicy != null)
            {
                rolePriorityScore += actor.AIPolicy.GetTargetRuleBonus(actor, target);
            }

            // 6. SquadDirectiveBonus: Điểm cộng từ mệnh lệnh chiến lược của Squad AI
            float squadDirectiveBonus = SquadAI.GetSquadTargetBonus(state, actor, target);

            // 7. CounterRiskPenalty: Rủi ro khi tấn công (Ví dụ khiên địch quá dày hấp thụ toàn bộ đòn đánh)
            float counterRiskPenalty = (target.Shield > estimatedDamage) ? 15f : 0f;

            return new TargetScoreBreakdown
            {
                TargetUnitId = target.InstanceId,
                TargetName = target.Name,
                ThreatScore = threatScore,
                StrategicValueScore = strategicValueScore,
                KillProbabilityScore = killProbabilityScore,
                VulnerabilityScore = vulnerabilityScore,
                RolePriorityScore = rolePriorityScore,
                SquadDirectiveBonus = squadDirectiveBonus,
                CounterRiskPenalty = counterRiskPenalty
            };
        }
    }
}
