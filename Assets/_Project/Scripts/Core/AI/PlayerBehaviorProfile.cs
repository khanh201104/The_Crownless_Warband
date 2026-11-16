using System;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Hồ sơ hành vi chiến thuật của người chơi qua các trận đấu (GDD Sections 41 & 42).
    /// Thu thập dữ liệu sử dụng kỹ năng Ranged, AoE, Support/Heal, can thiệp Manual và Policy,
    /// từ đó đề xuất chiến thuật & tiền tố khắc chế tự động (Adaptive Encounter).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    [Serializable]
    public class PlayerBehaviorProfile
    {
        public int TotalBattlesRecorded;
        public int TotalTurnsRecorded;
        public int TotalPlayerSkillEvents;
        public int RangedSkillEvents;
        public int AoESkillEvents;
        public int HealAndSupportEvents;
        public int ManualInterventions;
        public int AggressivePolicySelections;
        public int DefensivePolicySelections;

        public float AverageCombatTurns => TotalBattlesRecorded > 0 ? (float)TotalTurnsRecorded / TotalBattlesRecorded : 0f;
        public float RangedPreferencePct => TotalPlayerSkillEvents > 0 ? (RangedSkillEvents * 100f) / TotalPlayerSkillEvents : 0f;
        public float AoEPreferencePct => TotalPlayerSkillEvents > 0 ? (AoESkillEvents * 100f) / TotalPlayerSkillEvents : 0f;
        public float SupportPreferencePct => TotalPlayerSkillEvents > 0 ? (HealAndSupportEvents * 100f) / TotalPlayerSkillEvents : 0f;

        public void RecordBattleTelemetry(BattleState battle)
        {
            if (battle == null) return;

            TotalBattlesRecorded++;
            TotalTurnsRecorded += Math.Max(1, battle.CurrentTurn);

            if (battle.Units != null)
            {
                for (int i = 0; i < battle.Units.Count; i++)
                {
                    var u = battle.Units[i];
                    if (u == null || u.Team != TeamSide.Player || u.AIPolicy == null) continue;

                    if (u.AIPolicy.BehaviorPreset == UnitBehaviorPreset.Aggressive)
                    {
                        AggressivePolicySelections++;
                    }
                    else if (u.AIPolicy.BehaviorPreset == UnitBehaviorPreset.Defensive ||
                             u.AIPolicy.BehaviorPreset == UnitBehaviorPreset.Support)
                    {
                        DefensivePolicySelections++;
                    }
                }
            }

            if (battle.CombatLog != null)
            {
                for (int i = 0; i < battle.CombatLog.Count; i++)
                {
                    string log = battle.CombatLog[i];
                    if (string.IsNullOrEmpty(log)) continue;

                    if (log.IndexOf("Manual Skill Execution", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ManualInterventions++;
                    }

                    if (log.IndexOf("Quick Shot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        log.IndexOf("Hunter's Mark", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        log.IndexOf("Snipe Execute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        log.IndexOf("Arcane Bolt", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RangedSkillEvents++;
                        TotalPlayerSkillEvents++;
                    }
                    else if (log.IndexOf("Meteor Storm", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AoESkillEvents++;
                        TotalPlayerSkillEvents++;
                    }
                    else if (log.IndexOf("Emergency Heal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             log.IndexOf("Protect Ally", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             log.IndexOf("Bastion Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        HealAndSupportEvents++;
                        TotalPlayerSkillEvents++;
                    }
                    else if (log.IndexOf("Shield Strike", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             log.IndexOf("Heavy Slash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             log.IndexOf("Stunning Blow", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        TotalPlayerSkillEvents++;
                    }
                }
            }
        }

        public SquadStrategyType GetRecommendedCounterStrategy(out EnemyAffixType counterAffix, out string adaptationReason)
        {
            if (TotalBattlesRecorded <= 0)
            {
                counterAffix = EnemyAffixType.None;
                adaptationReason = "Baseline Encounter (No prior player telemetry)";
                return SquadStrategyType.Balanced;
            }

            if (RangedPreferencePct >= 42f)
            {
                counterAffix = EnemyAffixType.Berserker;
                adaptationReason = $"Adaptive Counter: High Ranged usage ({RangedPreferencePct:F0}%) -> Enemy deploys Backline Dive + Berserker";
                return SquadStrategyType.BacklineDive;
            }

            if (SupportPreferencePct >= 28f || DefensivePolicySelections > AggressivePolicySelections)
            {
                counterAffix = EnemyAffixType.Regenerator;
                adaptationReason = $"Adaptive Counter: High Sustain/Defense ({SupportPreferencePct:F0}%) -> Enemy deploys Anti-Heal + Regenerator";
                return SquadStrategyType.AntiHeal;
            }

            counterAffix = EnemyAffixType.Armored;
            adaptationReason = $"Adaptive Counter: High Aggression/AoE ({AoEPreferencePct:F0}%) -> Enemy deploys Armored Frontline";
            return SquadStrategyType.DirectAssault;
        }

        public string GetProfileSummary()
        {
            return $"Battles: {TotalBattlesRecorded} (Avg {AverageCombatTurns:F1}T) | Ranged: {RangedPreferencePct:F0}% | AoE: {AoEPreferencePct:F0}% | Support: {SupportPreferencePct:F0}% | Manual Casts: {ManualInterventions}";
        }
    }
}
