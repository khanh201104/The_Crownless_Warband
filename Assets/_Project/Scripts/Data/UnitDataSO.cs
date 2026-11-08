using System;
using System.Collections.Generic;
using UnityEngine;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Data
{
    /// <summary>
    /// Định nghĩa Nhân vật / Kẻ địch thông qua ScriptableObject trên Unity Inspector (GDD Section 34 & 47).
    /// Chuyển đổi thành cấu trúc Pure C# UnitState tại thời điểm Runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUnitData", menuName = "CrownlessWarband/Unit Data")]
    public class UnitDataSO : ScriptableObject
    {
        public string CharacterId;
        public string UnitName;
        public TeamSide DefaultTeam = TeamSide.Player;
        public UnitRole Role = UnitRole.Guardian;
        public RowType DefaultRow = RowType.Front;
        public int DefaultColumn = 0;

        [Header("Base Stats")]
        public int MaxHP = 200;
        public int Attack = 30;
        public int Defense = 15;
        public int Speed = 12;
        public float BaseThreat = 25f;
        public EnemyAffixType DefaultAffix = EnemyAffixType.None;

        [Header("Skills")]
        public List<SkillDataSO> Skills = new List<SkillDataSO>();

        public UnitState ToUnitState(string instanceId)
        {
            var unit = new UnitState(
                instanceId,
                UnitName,
                DefaultTeam,
                Role,
                new FormationPosition(DefaultRow, DefaultColumn),
                MaxHP,
                Attack,
                Defense,
                Speed
            );

            unit.CharacterDataId = CharacterId;
            unit.BaseThreat = BaseThreat;
            unit.Affix = DefaultAffix;

            if (Skills != null)
            {
                for (int i = 0; i < Skills.Count; i++)
                {
                    if (Skills[i] != null)
                    {
                        unit.Skills.Add(Skills[i].ToSkillInstance());
                    }
                }
            }

            return unit;
        }
    }
}

