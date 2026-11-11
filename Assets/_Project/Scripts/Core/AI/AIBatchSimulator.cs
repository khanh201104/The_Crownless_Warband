using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Events;
using CrownlessWarband.Core.Roguelite;

namespace CrownlessWarband.Core.AI
{
    /// <summary>
    /// Kết quả thống kê sau khi chạy giả lập hàng loạt trận đấu tự động không đồ họa (Headless Simulation).
    /// </summary>
    [Serializable]
    public class BatchSimulationResult
    {
        public int TotalBattles;
        public int PlayerWins;
        public int EnemyWins;
        public int Draws;
        public float PlayerWinRate => TotalBattles > 0 ? (float)PlayerWins / TotalBattles * 100f : 0f;
        public float AverageTurns;
        public int TotalStrategyAdaptations;
        public float ElapsedMilliseconds;
        public string SummaryReport;

        public override string ToString() => SummaryReport;
    }

    /// <summary>
    /// Bộ giả lập AI Headless chạy hàng loạt trận đấu trong Pure C# với tốc độ mili-giây (GDD Section 25 & 49).
    /// Phục vụ phân tích cân bằng game, kiểm thử hồi quy và đánh giá học thuật.
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class AIBatchSimulator
    {
        /// <summary>
        /// Chạy mô phỏng hàng loạt matchCount trận đấu tự động giữa 2 phe.
        /// </summary>
        public static BatchSimulationResult RunBatchSimulation(
            int matchCount,
            int baseSeed,
            WorldModifierType modifier = WorldModifierType.None,
            UnitBehaviorPreset teamPresetOverride = UnitBehaviorPreset.Balanced)
        {
            var stopwatch = Stopwatch.StartNew();

            int playerWins = 0;
            int enemyWins = 0;
            int draws = 0;
            int totalTurns = 0;
            int totalAdaptations = 0;

            for (int match = 0; match < matchCount; match++)
            {
                int matchSeed = baseSeed + (match * 79) + 13;

                // 1. Tạo đội hình người chơi với Preset chỉ định
                // Trong AIBatchSimulator.cs (thay thế đoạn tạo playerUnits ở đầu vòng lặp for):
                var playerUnits = CampaignGenerator.CreateDefaultPlayerWarband();
                for (int p = 0; p < playerUnits.Count; p++)
                {
                    playerUnits[p].AIPolicy.BehaviorPreset = teamPresetOverride;

                    // Cho phép các kỹ năng Manual (như Ultimate Meteor Storm của Mage) tự động kích hoạt khi chạy giả lập Headless
                    for (int s = 0; s < playerUnits[p].Skills.Count; s++)
                    {
                        if (playerUnits[p].Skills[s].ControlMode == SkillControlMode.Manual)
                        {
                            playerUnits[p].Skills[s].ControlMode = SkillControlMode.Auto;
                        }
                    }
                }

                // 2. Tạo trạng thái trận đấu
                var state = new BattleState
                {
                    RunSeed = matchSeed,
                    CurrentTurn = 1,
                    CurrentPhase = TurnPhase.Planning,
                    PlayerSquadStrategy = SquadStrategyType.Balanced,
                    EnemySquadStrategy = SquadStrategyType.DirectAssault,
                    ActiveModifier = modifier
                };

                for (int p = 0; p < playerUnits.Count; p++)
                {
                    state.Units.Add(playerUnits[p]);
                }

                // 3. Sinh kẻ địch (5v5 chuẩn GDD 53)
                SpawnSimulatorEnemyTeam(state, matchSeed);

                // 4. Chạy vòng lặp trận đấu (Turn loop) cho đến khi có kết quả hoặc chạm trần 30 lượt
                TurnManager.StartPlanningPhase(state);

                while (!TurnManager.IsBattleOver(state, out var winner) && state.CurrentTurn < 30)
                {
                    TurnManager.ExecuteAutoCombatPhase(state);
                }

                totalTurns += state.CurrentTurn;

                // 5. Đếm số lần Squad AI thích nghi chiến thuật
                for (int e = 0; e < state.CombatLog.Count; e++)
                {
                    if (state.CombatLog[e].Contains("Squad AI adapted strategy"))
                    {
                        totalAdaptations++;
                    }
                }

                // 6. Ghi nhận kết quả
                if (TurnManager.IsBattleOver(state, out var finalWinner))
                {
                    if (finalWinner == TeamSide.Player)
                    {
                        playerWins++;
                    }
                    else if (finalWinner == TeamSide.Enemy)
                    {
                        enemyWins++;
                    }
                    else
                    {
                        draws++;
                    }
                }
                else
                {
                    draws++;
                }
            }

            stopwatch.Stop();

            float avgTurns = matchCount > 0 ? (float)totalTurns / matchCount : 0f;
            float elapsedMs = (float)stopwatch.Elapsed.TotalMilliseconds;

            var sb = new StringBuilder();
            sb.AppendLine("================ HEADLESS AI BATCH SIMULATION REPORT ================");
            sb.AppendLine($"Matches Simulated: {matchCount} | Base Seed: {baseSeed} | Modifier: {modifier}");
            sb.AppendLine($"Team Behavior Preset: [{teamPresetOverride}]");
            sb.AppendLine($"Player Wins: {playerWins} ({(matchCount > 0 ? (float)playerWins / matchCount * 100f : 0f):F1}%) | Enemy Wins: {enemyWins} | Draws: {draws}");
            sb.AppendLine($"Average Turns per Battle: {avgTurns:F1}");
            sb.AppendLine($"Total Squad Strategy Adaptations: {totalAdaptations} ({(matchCount > 0 ? (float)totalAdaptations / matchCount : 0f):F1} / match)");
            sb.AppendLine($"Execution Time: {elapsedMs:F1} ms ({(matchCount > 0 ? elapsedMs / matchCount : 0f):F2} ms/match)");
            sb.AppendLine("=====================================================================");

            return new BatchSimulationResult
            {
                TotalBattles = matchCount,
                PlayerWins = playerWins,
                EnemyWins = enemyWins,
                Draws = draws,
                AverageTurns = avgTurns,
                TotalStrategyAdaptations = totalAdaptations,
                ElapsedMilliseconds = elapsedMs,
                SummaryReport = sb.ToString()
            };
        }

        private static void SpawnSimulatorEnemyTeam(BattleState state, int seed)
        {
            var rng = new System.Random(seed);
            int Vary(int baseVal, int pct)
            {
                int delta = (baseVal * pct) / 100;
                return Math.Max(1, baseVal + rng.Next(-delta, delta + 1));
            }

            int VarySpeed(int baseSpeed)
            {
                return Math.Max(1, baseSpeed + rng.Next(-2, 3));
            }

            // Enemy Tank (Front[1])
            int tankHp = Vary(260, 15);
            var eTank = new UnitState("e_tank", "Enemy Tank", TeamSide.Enemy, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 1), tankHp, Vary(26, 15), 22, VarySpeed(10));
            eTank.BaseThreat = 15f;
            eTank.Skills.Add(new SkillInstance("e_t_basic", "Mace Smash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            eTank.Skills.Add(new SkillInstance("e_t_s1", "Iron Guard", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 1.8f));

            // Enemy Assassin (Front[0]) - Backline Diver
            int assHp = Vary(135, 15);
            var eAssassin = new UnitState("e_assassin", "Enemy Assassin", TeamSide.Enemy, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 0), assHp, Vary(48, 15), 8, VarySpeed(18));
            eAssassin.BaseThreat = 45f;
            eAssassin.Skills.Add(new SkillInstance("e_a_basic", "Shadow Stab", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.1f, true));
            eAssassin.Skills.Add(new SkillInstance("e_a_s1", "Backline Ambush", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 0, 2, 1.7f, true));

            // Enemy Sniper (Back[0])
            int snipHp = Vary(130, 15);
            var eSniper = new UnitState("e_sniper", "Enemy Sniper", TeamSide.Enemy, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0), snipHp, Vary(42, 15), 8, VarySpeed(15));
            eSniper.BaseThreat = 35f;
            eSniper.Skills.Add(new SkillInstance("e_s_basic", "Piercing Shot", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));

            // Enemy Mage (Mid[1])
            int mageHp = Vary(145, 15);
            var eMage = new UnitState("e_mage", "Enemy Mage", TeamSide.Enemy, UnitRole.Mage,
                new FormationPosition(RowType.Mid, 1), mageHp, Vary(40, 15), 10, VarySpeed(12));
            eMage.BaseThreat = 35f;
            eMage.Skills.Add(new SkillInstance("e_m_basic", "Dark Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            eMage.Skills.Add(new SkillInstance("e_m_s1", "Shockwave", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 20, 2, 1.3f, true));

            // Enemy Support (Back[2])
            int supHp = Vary(140, 15);
            var eSupport = new UnitState("e_support", "Enemy Support", TeamSide.Enemy, UnitRole.Medic,
                new FormationPosition(RowType.Back, 2), supHp, Vary(32, 15), 10, VarySpeed(14));
            eSupport.BaseThreat = 35f;
            eSupport.Skills.Add(new SkillInstance("e_sup_basic", "Zap", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 0.8f, true));
            eSupport.Skills.Add(new SkillInstance("e_sup_s1", "Dark Mend", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.LowestHpAlly, SkillEffectType.Heal, 0, 1, 1.6f));

            var enemies = new List<UnitState> { eTank, eAssassin, eSniper, eMage, eSupport };

            // Gán ngẫu nhiên 2 Affixes cho 2 unit địch khác nhau trong mỗi trận
            var affixTypes = new[] { EnemyAffixType.Armored, EnemyAffixType.Berserker, EnemyAffixType.Regenerator };
            int firstIdx = rng.Next(enemies.Count);
            int secondIdx = (firstIdx + 1 + rng.Next(enemies.Count - 1)) % enemies.Count;

            void ApplyAffix(UnitState u, EnemyAffixType affix)
            {
                u.Affix = affix;
                if (affix == EnemyAffixType.Armored)
                {
                    u.Defense += 8;
                    u.Shield = 40;
                    u.Name = "[Armored] " + u.Name;
                }
                else if (affix == EnemyAffixType.Berserker)
                {
                    u.Name = "[Berserker] " + u.Name;
                }
                else if (affix == EnemyAffixType.Regenerator)
                {
                    u.Name = "[Regenerator] " + u.Name;
                }
            }

            ApplyAffix(enemies[firstIdx], affixTypes[rng.Next(affixTypes.Length)]);
            // ApplyAffix(enemies[secondIdx], affixTypes[rng.Next(affixTypes.Length)]);

            for (int i = 0; i < enemies.Count; i++)
            {
                state.Units.Add(enemies[i]);
            }
        }
    }
}

