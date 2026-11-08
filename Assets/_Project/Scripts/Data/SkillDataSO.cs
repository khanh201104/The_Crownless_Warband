using System;
using UnityEngine;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Data
{
    /// <summary>
    /// Định nghĩa Kỹ năng thông qua ScriptableObject trên Unity Inspector (GDD Section 34 & 47).
    /// Chuyển đổi thành cấu trúc Pure C# SkillInstance tại thời điểm Runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "CrownlessWarband/Skill Data")]
    public class SkillDataSO : ScriptableObject
    {
        public string SkillId;
        public string SkillName;
        public SkillSlotType Slot = SkillSlotType.Skill1;
        public SkillControlMode DefaultControlMode = SkillControlMode.Auto;
        public SkillTargetType TargetType = SkillTargetType.SingleEnemy;
        public SkillEffectType EffectType = SkillEffectType.Damage;
        public int EnergyCost = 0;
        public int CooldownTurns = 0;
        public float PowerMultiplier = 1.0f;
        public bool CanTargetBackline = false;

        public SkillInstance ToSkillInstance()
        {
            return new SkillInstance(
                SkillId,
                SkillName,
                Slot,
                DefaultControlMode,
                TargetType,
                EffectType,
                EnergyCost,
                CooldownTurns,
                PowerMultiplier,
                CanTargetBackline
            );
        }
    }
}

