using System;
using System.Collections.Generic;
using System.Text;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Bảng phân rã điểm số chi tiết khi chấm điểm một mục tiêu hợp lệ (GDD Mục 16 & 44).
    /// Phục vụ hiển thị F3 Explainable AI Debug Mode và Combat Log.
    /// </summary>
    [Serializable]
    public class TargetScoreBreakdown
    {
        public string TargetUnitId;
        public string TargetName;
        public float ThreatScore;
        public float StrategicValueScore;
        public float KillProbabilityScore;
        public float VulnerabilityScore;
        public float RolePriorityScore;
        public float SquadDirectiveBonus;
        public float CounterRiskPenalty;

        public float TotalScore => ThreatScore + StrategicValueScore + KillProbabilityScore + VulnerabilityScore + RolePriorityScore + SquadDirectiveBonus - CounterRiskPenalty;

        public override string ToString()
        {
            return $"{TargetName}: {TotalScore:F1} (Thr:{ThreatScore:F0} Str:{StrategicValueScore:F0} Kill:{KillProbabilityScore:F0} Vul:{VulnerabilityScore:F0} Role:{RolePriorityScore:F0} Squad:{SquadDirectiveBonus:F0} -Risk:{CounterRiskPenalty:F0})";
        }
    }

    /// <summary>
    /// Bảng phân rã điểm số chi tiết khi chấm điểm một kỹ năng trên một mục tiêu (GDD Mục 17 & 44).
    /// </summary>
    [Serializable]
    public class ActionScoreBreakdown
    {
        public string SkillId;
        public string SkillName;
        public string ChosenTargetId;
        public string ChosenTargetName;
        public float BaseUtilityScore;
        public float TacticalFitScore;
        public float UrgencyScore;

        public float TotalScore => BaseUtilityScore + TacticalFitScore + UrgencyScore;

        public override string ToString()
        {
            return $"{SkillName} -> {ChosenTargetName}: {TotalScore:F1} (Base:{BaseUtilityScore:F0} Tac:{TacticalFitScore:F0} Urg:{UrgencyScore:F0})";
        }
    }

    /// <summary>
    /// Bản ghi toàn bộ quá trình tư duy của một Unit trong một lượt (GDD Mục 16, 17, 44).
    /// Lưu vào BattleState.LastUnitDecisions để UI F3 Debug Overlay đọc và hiển thị.
    /// </summary>
    [Serializable]
    public class UnitAIDecisionLog
    {
        public int TurnNumber;
        public string UnitId;
        public string UnitName;
        public SquadStrategyType ActiveSquadStrategy;

        public List<TargetScoreBreakdown> EvaluatedTargets = new List<TargetScoreBreakdown>();
        public List<ActionScoreBreakdown> EvaluatedActions = new List<ActionScoreBreakdown>();

        public string FinalChosenSkillName;
        public string FinalChosenTargetName;

        public string FormatForDebugUI()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Unit: {UnitName} (Turn {TurnNumber})] | Strategy: {ActiveSquadStrategy}");
            sb.AppendLine("--- Target Evaluation Breakdown ---");
            if (EvaluatedTargets.Count == 0)
            {
                sb.AppendLine("  (No valid enemy targets)");
            }
            else
            {
                for (int i = 0; i < EvaluatedTargets.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. {EvaluatedTargets[i]}");
                }
            }

            sb.AppendLine("--- Action Evaluation Breakdown ---");
            if (EvaluatedActions.Count == 0)
            {
                sb.AppendLine("  (No actions evaluated)");
            }
            else
            {
                for (int i = 0; i < EvaluatedActions.Count; i++)
                {
                    var act = EvaluatedActions[i];
                    string prefix = (act.SkillName == FinalChosenSkillName && act.ChosenTargetName == FinalChosenTargetName) ? "-> [CHOSEN] " : "   ";
                    sb.AppendLine($"{prefix}{act}");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
