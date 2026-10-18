using System;

namespace CrownlessWarband.Core.Events
{
    public enum CombatEventType
    {
        TurnStarted,
        PhaseChanged,
        SquadStrategyChanged,
        UnitTurnStarted,
        SkillUsed,
        DamageDealt,
        HealApplied,
        ShieldApplied,
        StatusApplied,
        InterceptionTriggered,
        UnitDied,
        BattleEnded
    }

    [Serializable]
    public class CombatEvent
    {
        public CombatEventType EventType;
        public int TurnNumber;
        public string SourceUnitId;
        public string TargetUnitId;
        public string SkillName;
        public int Value;
        public string Description;
        public string AIDebugSummary;

        public CombatEvent() { }

        public CombatEvent(
            CombatEventType eventType,
            int turnNumber,
            string sourceUnitId = null,
            string targetUnitId = null,
            string skillName = null,
            int value = 0,
            string description = null,
            string aiDebugSummary = null)
        {
            EventType = eventType;
            TurnNumber = turnNumber;
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            SkillName = skillName;
            Value = value;
            Description = description;
            AIDebugSummary = aiDebugSummary;
        }

        public override string ToString()
        {
            return $"[Turn {TurnNumber}] {EventType}: {Description}";
        }
    }
}
