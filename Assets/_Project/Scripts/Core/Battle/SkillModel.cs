using System;

namespace CrownlessWarband.Core.Battle
{
    public enum SkillSlotType
    {
        BasicAttack,
        Skill1,
        Skill2,
        Ultimate
    }

    public enum SkillTargetType
    {
        SingleEnemy,
        AllEnemies,
        EnemyRow,
        SingleAlly,
        Self,
        LowestHpAlly
    }

    public enum SkillEffectType
    {
        Damage,
        Heal,
        Shield,
        Stun,
        Mark,
        ProtectAlly,
        ExecuteDamage
    }

    [Serializable]
    public class SkillInstance
    {
        public string SkillId;
        public string Name;
        public SkillSlotType Slot;
        public SkillControlMode ControlMode;
        public SkillTargetType TargetType;
        public SkillEffectType EffectType;
        public int EnergyCost;
        public int CooldownTurns;
        public int CurrentCooldown;
        public float PowerMultiplier;
        public bool CanTargetBackline;

        public SkillInstance() { }

        public SkillInstance(
            string skillId,
            string name,
            SkillSlotType slot,
            SkillControlMode controlMode,
            SkillTargetType targetType,
            SkillEffectType effectType,
            int energyCost,
            int cooldownTurns,
            float powerMultiplier,
            bool canTargetBackline = false)
        {
            SkillId = skillId;
            Name = name;
            Slot = slot;
            ControlMode = controlMode;
            TargetType = targetType;
            EffectType = effectType;
            EnergyCost = energyCost;
            CooldownTurns = cooldownTurns;
            CurrentCooldown = 0;
            PowerMultiplier = powerMultiplier;
            CanTargetBackline = canTargetBackline;
        }

        public bool IsReady(int currentUnitEnergy)
        {
            return CurrentCooldown <= 0 && currentUnitEnergy >= EnergyCost;
        }
    }
}
