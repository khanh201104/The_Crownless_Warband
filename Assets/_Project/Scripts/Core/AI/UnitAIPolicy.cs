using System;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.AI
{
    public enum UnitBehaviorPreset
    {
        Balanced,
        Aggressive,
        Defensive,
        Support,
        Tactical
    }

    public enum TargetPriorityRule
    {
        DefaultUtility,
        MarkedFirst,
        LowestHP,
        HighestThreat,
        SupportFirst,
        FrontlineFirst
    }

    public enum SkillConditionType
    {
        Always,
        TargetHpBelow40,
        AllyHpBelow50,
        EnemiesAliveAtLeast3,
        TargetNotStunned,
        TargetNotMarked
    }

    /// <summary>
    /// Player-Configurable AI Policy (GDD Section 13, 18, 19).
    /// Cho phép người chơi cấu hình ưu tiên mục tiêu, phong cách hành vi và điều kiện thi triển kỹ năng cho từng unit.
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    [Serializable]
    public class UnitAIPolicy
    {
        public UnitBehaviorPreset BehaviorPreset = UnitBehaviorPreset.Balanced;
        public TargetPriorityRule PrimaryTargetRule = TargetPriorityRule.DefaultUtility;
        public SkillConditionType SpecialSkillCondition = SkillConditionType.Always;

        public float GetTargetRuleBonus(UnitState actor, UnitState target)
        {
            if (target == null) return 0f;

            switch (PrimaryTargetRule)
            {
                case TargetPriorityRule.MarkedFirst:
                    return target.IsMarked ? 45f : 0f;

                case TargetPriorityRule.LowestHP:
                    return (1f - target.HPPercentage) * 40f;

                case TargetPriorityRule.HighestThreat:
                    return target.BaseThreat * 0.6f;

                case TargetPriorityRule.SupportFirst:
                    return (target.Role == UnitRole.Medic || target.Role == UnitRole.Mage) ? 40f : 0f;

                case TargetPriorityRule.FrontlineFirst:
                    return (target.Position.Row == RowType.Front) ? 30f : 0f;

                case TargetPriorityRule.DefaultUtility:
                default:
                    return 0f;
            }
        }

        public float GetActionPresetBonus(SkillInstance skill)
        {
            if (skill == null) return 0f;

            switch (BehaviorPreset)
            {
                case UnitBehaviorPreset.Aggressive:
                    return (skill.EffectType == SkillEffectType.Damage || skill.EffectType == SkillEffectType.ExecuteDamage) ? 20f : 0f;

                case UnitBehaviorPreset.Defensive:
                    return (skill.EffectType == SkillEffectType.Shield || skill.EffectType == SkillEffectType.ProtectAlly) ? 25f : 0f;

                case UnitBehaviorPreset.Support:
                    return (skill.EffectType == SkillEffectType.Heal || skill.EffectType == SkillEffectType.ProtectAlly) ? 25f : 0f;

                case UnitBehaviorPreset.Tactical:
                    return (skill.EffectType == SkillEffectType.Stun || skill.EffectType == SkillEffectType.Mark) ? 25f : 0f;

                case UnitBehaviorPreset.Balanced:
                default:
                    return 0f;
            }
        }

        public bool IsSkillConditionMet(BattleState state, UnitState actor, SkillInstance skill, UnitState candidateTarget, int aliveEnemyCount)
        {
            if (skill == null || skill.Slot == SkillSlotType.BasicAttack)
            {
                return true;
            }

            if (skill.ControlMode != SkillControlMode.SemiAuto && SpecialSkillCondition == SkillConditionType.Always)
            {
                return true;
            }

            switch (SpecialSkillCondition)
            {
                case SkillConditionType.TargetHpBelow40:
                    return candidateTarget != null && candidateTarget.HPPercentage < 0.40f;

                case SkillConditionType.AllyHpBelow50:
                    return candidateTarget != null && candidateTarget.HPPercentage < 0.50f;

                case SkillConditionType.EnemiesAliveAtLeast3:
                    return aliveEnemyCount >= 3;

                case SkillConditionType.TargetNotStunned:
                    return candidateTarget != null && !candidateTarget.IsStunned;

                case SkillConditionType.TargetNotMarked:
                    return candidateTarget != null && !candidateTarget.IsMarked;

                case SkillConditionType.Always:
                default:
                    return true;
            }
        }
    }
}

