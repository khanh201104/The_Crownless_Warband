using System;
using System.Collections.Generic;

namespace CrownlessWarband.Core.Battle
{
    [Serializable]
    public struct FormationPosition : IEquatable<FormationPosition>
    {
        public RowType Row;
        public int Column; // 0, 1, 2 (Tối đa 3 slot mỗi hàng)

        public FormationPosition(RowType row, int column)
        {
            Row = row;
            Column = column;
        }

        public bool Equals(FormationPosition other) => Row == other.Row && Column == other.Column;
        public override string ToString() => $"{Row}[{Column}]";
    }

    [Serializable]
    public class UnitState
    {
        public string InstanceId;
        public string CharacterDataId;
        public string Name;
        public TeamSide Team;
        public UnitRole Role;
        public FormationPosition Position;

        // Base & Current Stats
        public int MaxHP;
        public int CurrentHP;
        public int MaxEnergy;
        public int CurrentEnergy;
        public int Attack;
        public int Defense;
        public int Speed;
        public int Shield;

        // AI & Threat Metrics (Phục vụ F3 AI Debug Mode)
        public float BaseThreat;
        public bool IsMarked;
        public bool IsStunned;
        public string ProtectingAllyId; // ID đồng minh mà Guardian đang bảo vệ (Interception)

        // Skills
        public List<SkillInstance> Skills = new List<SkillInstance>();

        public bool IsAlive => CurrentHP > 0;
        public float HPPercentage => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;

        public SkillInstance GetBasicAttack()
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].Slot == SkillSlotType.BasicAttack)
                {
                    return Skills[i];
                }
            }
            return null;
        }

        public IEnumerable<SkillInstance> GetReadySkills()
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].IsReady(CurrentEnergy))
                {
                    yield return Skills[i];
                }
            }
        }

        public UnitState() { }

        public UnitState(string instanceId, string name, TeamSide team, UnitRole role, FormationPosition pos,
                         int maxHp, int atk, int def, int spd)
        {
            InstanceId = instanceId;
            Name = name;
            Team = team;
            Role = role;
            Position = pos;
            MaxHP = maxHp;
            CurrentHP = maxHp;
            MaxEnergy = 100;
            CurrentEnergy = 0;
            Attack = atk;
            Defense = def;
            Speed = spd;
        }
    }
}