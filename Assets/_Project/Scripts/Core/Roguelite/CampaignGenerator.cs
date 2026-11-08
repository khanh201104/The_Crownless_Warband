using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.Roguelite
{
    /// <summary>
    /// Bộ sinh chiến dịch theo Seed (Seed-based Procedural Generation - GDD Section 4, 30, 31, 35, 36).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class CampaignGenerator
    {
        /// <summary>
        /// Khởi tạo một RunState mới với cây bản đồ 5 tầng phân nhánh cố định theo Seed (GDD Section 31).
        /// </summary>
        public static RunState CreateNewRun(int seed, List<UnitState> initialRoster)
        {
            var rng = new System.Random(seed);

            var run = new RunState
            {
                Seed = seed,
                CurrentFloor = 0,
                CorruptionLevel = 0,
                BattlesWon = 0,
                IsRunCompleted = false,
                IsRunFailed = false,
                CurrentNodeId = "F0_L0"
            };

            // Copy đội hình người chơi vào RunState
            if (initialRoster != null)
            {
                for (int i = 0; i < initialRoster.Count; i++)
                {
                    run.PlayerRoster.Add(initialRoster[i]);
                }
            }

            // Sinh cây bản đồ 5 tầng (Floor 0 -> Floor 4)
            // Floor 0: 1 node Combat
            var f0_l0 = new CampaignNode(0, 0, "F0_L0", CampaignNodeType.Combat, "Frontier Skirmish", 10);
            f0_l0.NextNodeIds.Add("F1_L0");
            f0_l0.NextNodeIds.Add("F1_L1");
            run.Nodes.Add(f0_l0);

            // Floor 1: 2 nodes (L0 Combat, L1 Event hoặc Recovery theo seed)
            CampaignNodeType f1l1Type = (rng.Next(2) == 0) ? CampaignNodeType.Event : CampaignNodeType.Recovery;
            string f1l1Title = f1l1Type == CampaignNodeType.Event ? "Deserted Cache" : "Camp Haven";

            var f1_l0 = new CampaignNode(1, 0, "F1_L0", CampaignNodeType.Combat, "Outpost Ambush", 25);
            f1_l0.Modifier = GetRandomWorldModifier(rng);
            f1_l0.NextNodeIds.Add("F2_L0");
            f1_l0.NextNodeIds.Add("F2_L1");
            run.Nodes.Add(f1_l0);

            var f1_l1 = new CampaignNode(1, 1, "F1_L1", f1l1Type, f1l1Title, 15);
            f1_l1.NextNodeIds.Add("F2_L0");
            f1_l1.NextNodeIds.Add("F2_L1");
            run.Nodes.Add(f1_l1);

            // Floor 2: 2 nodes (L0 EliteCombat, L1 Combat)
            var f2_l0 = new CampaignNode(2, 0, "F2_L0", CampaignNodeType.EliteCombat, "Heavy Guard Patrol", 50);
            f2_l0.Modifier = GetRandomWorldModifier(rng);
            f2_l0.NextNodeIds.Add("F3_L0");
            f2_l0.NextNodeIds.Add("F3_L1");
            run.Nodes.Add(f2_l0);

            var f2_l1 = new CampaignNode(2, 1, "F2_L1", CampaignNodeType.Combat, "Barricade Assault", 35);
            f2_l1.Modifier = GetRandomWorldModifier(rng);
            f2_l1.NextNodeIds.Add("F3_L0");
            f2_l1.NextNodeIds.Add("F3_L1");
            run.Nodes.Add(f2_l1);

            // Floor 3: 2 nodes (L0 Recovery, L1 EliteCombat)
            var f3_l0 = new CampaignNode(3, 0, "F3_L0", CampaignNodeType.Recovery, "Sanctuary Springs", 20);
            f3_l0.NextNodeIds.Add("F4_L0");
            run.Nodes.Add(f3_l0);

            var f3_l1 = new CampaignNode(3, 1, "F3_L1", CampaignNodeType.EliteCombat, "Warden's Vanguard", 65);
            f3_l1.Modifier = GetRandomWorldModifier(rng);
            f3_l1.NextNodeIds.Add("F4_L0");
            run.Nodes.Add(f3_l1);

            // Floor 4: 1 Boss node ("The Warden" GDD Section 38)
            var f4_l0 = new CampaignNode(4, 0, "F4_L0", CampaignNodeType.Boss, "The Warden's Stronghold", 100);
            f4_l0.Modifier = GetRandomWorldModifier(rng);
            run.Nodes.Add(f4_l0);

            return run;
        }

        private static WorldModifierType GetRandomWorldModifier(System.Random rng)
        {
            var modifiers = new[]
            {
                WorldModifierType.AcidRain,
                WorldModifierType.BloodMoon,
                WorldModifierType.Overcharge,
                WorldModifierType.NoHealing
            };
            return modifiers[rng.Next(modifiers.Length)];
        }

        /// <summary>
        /// Tạo danh sách 3 phần thưởng ngẫu nhiên khác nhau sau trận đấu (GDD Section 27, 28, 29).
        /// </summary>
        public static List<RewardOption> GeneratePostBattleRewards(int seed, int floor, List<RelicType> ownedRelics)
        {
            var rng = new System.Random(seed + (floor * 101) + 17);
            var candidatePool = new List<RewardOption>();

            // 1. Các gói nâng cấp chỉ số & kỹ năng
            candidatePool.Add(new RewardOption(
                "upg_heal",
                "Field Medic Relief",
                "Restore 30% Max HP to all surviving warband members.",
                UpgradeRewardType.TeamHeal30Percent));

            candidatePool.Add(new RewardOption(
                "upg_guardian_def",
                "Iron Bastion Reinforcement",
                "All Guardian units gain +8 Defense permanently.",
                UpgradeRewardType.BoostGuardianDefense));

            candidatePool.Add(new RewardOption(
                "upg_ranger_atk",
                "Predator Focus",
                "All Ranger units gain +15 Attack permanently.",
                UpgradeRewardType.BoostRangerCritMark));

            candidatePool.Add(new RewardOption(
                "upg_mage_atk",
                "Arcane Amplification",
                "All Mage units gain +12 Attack permanently.",
                UpgradeRewardType.BoostMageAoE));

            // 2. Các Di vật chiến thuật (chỉ đưa vào pool nếu chưa sở hữu)
            bool HasOwned(RelicType r)
            {
                if (ownedRelics == null) return false;
                for (int i = 0; i < ownedRelics.Count; i++)
                {
                    if (ownedRelics[i] == r) return true;
                }
                return false;
            }

            if (!HasOwned(RelicType.BloodContract))
            {
                candidatePool.Add(new RewardOption(
                    "relic_blood",
                    "Blood Contract",
                    "Deal +30% damage, but Max HP is reduced by 10%.",
                    UpgradeRewardType.GrantRelic,
                    RelicType.BloodContract));
            }

            if (!HasOwned(RelicType.TacticalCore))
            {
                candidatePool.Add(new RewardOption(
                    "relic_tactical",
                    "Tactical Core",
                    "Whenever Squad AI adapts strategy, all allies gain +10 Energy.",
                    UpgradeRewardType.GrantRelic,
                    RelicType.TacticalCore));
            }

            if (!HasOwned(RelicType.EmergencyProtocol))
            {
                candidatePool.Add(new RewardOption(
                    "relic_emergency",
                    "Emergency Protocol",
                    "Healing an ally below 35% HP is increased by +50%.",
                    UpgradeRewardType.GrantRelic,
                    RelicType.EmergencyProtocol));
            }

            if (!HasOwned(RelicType.ChainRelay))
            {
                candidatePool.Add(new RewardOption(
                    "relic_chain",
                    "Chain Relay",
                    "When a Marked enemy dies, the Mark transfers to another alive enemy.",
                    UpgradeRewardType.GrantRelic,
                    RelicType.ChainRelay));
            }

            // Chọn 3 phần thưởng phân biệt từ candidatePool
            var results = new List<RewardOption>();
            int countToPick = Math.Min(3, candidatePool.Count);

            for (int i = 0; i < countToPick; i++)
            {
                int index = rng.Next(candidatePool.Count);
                results.Add(candidatePool[index]);
                candidatePool.RemoveAt(index);
            }

            return results;
        }

        /// <summary>
        /// Áp dụng phần thưởng đã chọn vào RunState (nâng cấp chỉ số, hồi máu, hoặc thêm di vật).
        /// </summary>
        public static void ApplyReward(RunState run, RewardOption reward)
        {
            if (run == null || reward == null) return;

            switch (reward.RewardType)
            {
                case UpgradeRewardType.GrantRelic:
                    if (reward.GrantedRelic != RelicType.None && !run.HasRelic(reward.GrantedRelic))
                    {
                        run.CollectedRelics.Add(reward.GrantedRelic);

                        // Blood Contract: Giảm 10% Max HP của toàn đội
                        if (reward.GrantedRelic == RelicType.BloodContract)
                        {
                            for (int i = 0; i < run.PlayerRoster.Count; i++)
                            {
                                var unit = run.PlayerRoster[i];
                                unit.MaxHP = Math.Max(1, (int)(unit.MaxHP * 0.90f));
                                unit.CurrentHP = Math.Min(unit.CurrentHP, unit.MaxHP);
                            }
                        }
                    }
                    break;

                case UpgradeRewardType.TeamHeal30Percent:
                    for (int i = 0; i < run.PlayerRoster.Count; i++)
                    {
                        var unit = run.PlayerRoster[i];
                        if (unit.IsAlive)
                        {
                            int heal = Math.Max(1, (int)(unit.MaxHP * 0.30f));
                            unit.CurrentHP = Math.Min(unit.MaxHP, unit.CurrentHP + heal);
                        }
                    }
                    break;

                case UpgradeRewardType.BoostGuardianDefense:
                    for (int i = 0; i < run.PlayerRoster.Count; i++)
                    {
                        var unit = run.PlayerRoster[i];
                        if (unit.Role == UnitRole.Guardian)
                        {
                            unit.Defense += 8;
                        }
                    }
                    break;

                case UpgradeRewardType.BoostRangerCritMark:
                    for (int i = 0; i < run.PlayerRoster.Count; i++)
                    {
                        var unit = run.PlayerRoster[i];
                        if (unit.Role == UnitRole.Ranger)
                        {
                            unit.Attack += 15;
                        }
                    }
                    break;

                case UpgradeRewardType.BoostMageAoE:
                    for (int i = 0; i < run.PlayerRoster.Count; i++)
                    {
                        var unit = run.PlayerRoster[i];
                        if (unit.Role == UnitRole.Mage)
                        {
                            unit.Attack += 12;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Khởi tạo một trận đấu (BattleState) cho một Node cụ thể, nạp đội hình người chơi và sinh kẻ địch theo cấp độ Node.
        /// </summary>
        public static BattleState CreateBattleForNode(RunState run, CampaignNode node)
        {
            if (run == null || node == null) return null;

            var battle = new BattleState
            {
                RunSeed = run.Seed + (node.FloorIndex * 1000) + (node.LaneIndex * 10),
                CurrentTurn = 1,
                CurrentPhase = TurnPhase.Planning,
                PlayerSquadStrategy = SquadStrategyType.Balanced,
                EnemySquadStrategy = (node.NodeType == CampaignNodeType.Boss) ? SquadStrategyType.DirectAssault : SquadStrategyType.Balanced,
                ActiveModifier = node.Modifier
            };

            // Kiểm tra xem có đồng minh nào còn sống không
            bool anyAlive = false;
            for (int i = 0; i < run.PlayerRoster.Count; i++)
            {
                if (run.PlayerRoster[i].IsAlive)
                {
                    anyAlive = true;
                    break;
                }
            }

            // Cơ chế an toàn (Safety fallback): Nếu toàn đội tử trận, hồi sinh Guardian với 50% HP
            if (!anyAlive && run.PlayerRoster.Count > 0)
            {
                var fallbackUnit = run.PlayerRoster[0];
                fallbackUnit.CurrentHP = Math.Max(1, (int)(fallbackUnit.MaxHP * 0.50f));
            }

            // Nạp các unit còn sống và reset các trạng thái tạm thời giữa các trận
            for (int i = 0; i < run.PlayerRoster.Count; i++)
            {
                var unit = run.PlayerRoster[i];
                if (!unit.IsAlive) continue;

                unit.Shield = 0;
                unit.CurrentEnergy = (unit.Role == UnitRole.Mage) ? 20 : 0;
                unit.IsStunned = false;
                unit.IsMarked = false;
                unit.ProtectingAllyId = null;

                for (int s = 0; s < unit.Skills.Count; s++)
                {
                    unit.Skills[s].CurrentCooldown = 0;
                }

                battle.Units.Add(unit);
            }

            // Nạp các di vật đã thu thập
            for (int i = 0; i < run.CollectedRelics.Count; i++)
            {
                battle.ActiveRelics.Add(run.CollectedRelics[i]);
            }

            // Sinh kẻ địch theo loại Node
            if (node.NodeType == CampaignNodeType.Boss)
            {
                SpawnBossWardenEncounter(battle, run.CorruptionLevel);
            }
            else if (node.NodeType == CampaignNodeType.EliteCombat)
            {
                SpawnEliteEncounter(battle, run.CorruptionLevel);
            }
            else
            {
                SpawnStandardCombatEncounter(battle, run.CorruptionLevel);
            }

            return battle;
        }

        /// <summary>
        /// Tạo đội hình khởi đầu chuẩn 5 nhân vật theo GDD Section 53.
        /// </summary>
        public static List<UnitState> CreateDefaultPlayerWarband()
        {
            var roster = new List<UnitState>();

            // Guardian (Front[0])
            var guardian = new UnitState("p_guardian", "Guardian", TeamSide.Player, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 0), 280, 28, 24, 11);
            guardian.BaseThreat = 20f;
            guardian.Skills.Add(new SkillInstance("p_g_basic", "Shield Strike", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            guardian.Skills.Add(new SkillInstance("p_g_s1", "Protect Ally", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleAlly, SkillEffectType.ProtectAlly, 0, 2, 1.0f));
            guardian.Skills.Add(new SkillInstance("p_g_s2", "Bastion Shield", SkillSlotType.Skill2, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 3, 2.0f));
            roster.Add(guardian);

            // Vanguard (Front[1])
            var vanguard = new UnitState("p_vanguard", "Vanguard", TeamSide.Player, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 1), 220, 34, 18, 14);
            vanguard.BaseThreat = 25f;
            vanguard.Skills.Add(new SkillInstance("p_v_basic", "Heavy Slash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            vanguard.Skills.Add(new SkillInstance("p_v_s1", "Stunning Blow", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.2f));
            roster.Add(vanguard);

            // Mage (Mid[1])
            var mage = new UnitState("p_mage", "Mage", TeamSide.Player, UnitRole.Mage,
                new FormationPosition(RowType.Mid, 1), 150, 42, 10, 13);
            mage.BaseThreat = 35f;
            mage.CurrentEnergy = 20;
            mage.Skills.Add(new SkillInstance("p_m_basic", "Arcane Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            mage.Skills.Add(new SkillInstance("p_m_s1", "Disable Frost", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.3f, true));
            mage.Skills.Add(new SkillInstance("p_m_ult", "Meteor Storm", SkillSlotType.Ultimate, SkillControlMode.Manual, SkillTargetType.AllEnemies, SkillEffectType.Damage, 40, 3, 1.8f, true));
            roster.Add(mage);

            // Ranger (Back[0])
            var ranger = new UnitState("p_ranger", "Ranger", TeamSide.Player, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0), 140, 46, 8, 16);
            ranger.BaseThreat = 40f;
            ranger.Skills.Add(new SkillInstance("p_r_basic", "Quick Shot", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            ranger.Skills.Add(new SkillInstance("p_r_s1", "Hunter's Mark", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Mark, 0, 2, 1.1f, true));
            ranger.Skills.Add(new SkillInstance("p_r_ult", "Snipe Execute", SkillSlotType.Ultimate, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 30, 3, 2.2f, true));
            roster.Add(ranger);

            // Medic (Back[2])
            var medic = new UnitState("p_medic", "Medic", TeamSide.Player, UnitRole.Medic,
                new FormationPosition(RowType.Back, 2), 145, 35, 10, 15);
            medic.BaseThreat = 35f;
            medic.Skills.Add(new SkillInstance("p_med_basic", "Light Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 0.9f, true));
            medic.Skills.Add(new SkillInstance("p_med_s1", "Emergency Heal", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.LowestHpAlly, SkillEffectType.Heal, 0, 1, 1.8f));
            roster.Add(medic);

            return roster;
        }

        /// <summary>
        /// Xử lý các node phi giao chiến (Recovery, Event) trong Roguelite Run.
        /// </summary>
        public static string ResolveNonCombatNode(RunState run, CampaignNode node)
        {
            if (run == null || node == null) return string.Empty;

            if (node.NodeType == CampaignNodeType.Recovery)
            {
                for (int i = 0; i < run.PlayerRoster.Count; i++)
                {
                    var unit = run.PlayerRoster[i];
                    if (unit.IsAlive)
                    {
                        int heal = Math.Max(1, (int)(unit.MaxHP * 0.40f));
                        unit.CurrentHP = Math.Min(unit.MaxHP, unit.CurrentHP + heal);
                    }
                    else
                    {
                        // Hồi sinh đồng minh tử trận với 30% HP
                        unit.CurrentHP = Math.Max(1, (int)(unit.MaxHP * 0.30f));
                        unit.Shield = 0;
                        unit.CurrentEnergy = 0;
                        unit.IsStunned = false;
                        unit.IsMarked = false;
                    }
                }

                run.CorruptionLevel = Math.Max(0, run.CorruptionLevel - 10);
                node.IsCompleted = true;
                return "Rested at Sanctuary: All allies restored +40% HP (fallen allies revived at 30% HP), Corruption -10%.";
            }
            else if (node.NodeType == CampaignNodeType.Event)
            {
                run.CorruptionLevel = Math.Min(100, run.CorruptionLevel + 15);
                run.PendingRewards = GeneratePostBattleRewards(run.Seed, node.FloorIndex, run.CollectedRelics);
                node.IsCompleted = true;
                return "Explored Deserted Cache: Corruption +15%, discovered a Tactical Relic / Upgrade cache!";
            }

            return string.Empty;
        }

        /// <summary>
        /// Ghi nhận chiến thắng một Node chiến đấu, cập nhật tiến trình và tạo phần thưởng sau trận.
        /// </summary>
        public static void CompleteNodeVictory(RunState run, CampaignNode node)
        {
            if (run == null || node == null) return;

            node.IsCompleted = true;
            run.BattlesWon++;
            run.CorruptionLevel = Math.Min(100, run.CorruptionLevel + 15);

            if (node.NodeType == CampaignNodeType.Boss)
            {
                run.IsRunCompleted = true;
            }
            else
            {
                run.PendingRewards = GeneratePostBattleRewards(run.Seed, node.FloorIndex, run.CollectedRelics);
            }
        }

        private static void SpawnStandardCombatEncounter(BattleState state, int corruptionLevel)
        {
            float statScale = 1.0f + (corruptionLevel * 0.005f);

            // 1. Enemy Vanguard (Front[1])
            var vanguard = new UnitState("e_van", "Outpost Guard", TeamSide.Enemy, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 1),
                (int)(170 * statScale), (int)(28 * statScale), 14, 12);
            vanguard.BaseThreat = 25f;
            vanguard.Skills.Add(new SkillInstance("e_v_atk", "Heavy Cleave", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            vanguard.Skills.Add(new SkillInstance("e_v_stun", "Concussive Strike", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.2f));

            // Gán Affix nếu Corruption >= 15
            if (corruptionLevel >= 15)
            {
                int affixIdx = (corruptionLevel / 15) % 3;
                if (affixIdx == 0)
                {
                    vanguard.Affix = EnemyAffixType.Armored;
                    vanguard.Defense += 8;
                    vanguard.Shield = 40;
                    vanguard.Name = "[Armored] " + vanguard.Name;
                }
                else if (affixIdx == 1)
                {
                    vanguard.Affix = EnemyAffixType.Berserker;
                    vanguard.Name = "[Berserker] " + vanguard.Name;
                }
                else
                {
                    vanguard.Affix = EnemyAffixType.Regenerator;
                    vanguard.Name = "[Regenerator] " + vanguard.Name;
                }
            }

            state.Units.Add(vanguard);

            // 2. Enemy Scout Ranger (Back[0])
            var scout = new UnitState("e_scout", "Outpost Scout", TeamSide.Enemy, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0),
                (int)(120 * statScale), (int)(34 * statScale), 8, 15);
            scout.BaseThreat = 35f;
            scout.Skills.Add(new SkillInstance("e_s_atk", "Quick Arrow", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            scout.Skills.Add(new SkillInstance("e_s_mark", "Target Sighting", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Mark, 0, 2, 1.1f, true));
            state.Units.Add(scout);

            // 3. Enemy Cultist Medic (Back[2])
            var cultist = new UnitState("e_cult", "Rogue Cultist", TeamSide.Enemy, UnitRole.Medic,
                new FormationPosition(RowType.Back, 2),
                (int)(130 * statScale), (int)(26 * statScale), 10, 13);
            cultist.BaseThreat = 30f;
            cultist.Skills.Add(new SkillInstance("e_c_atk", "Shadow Blast", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 0.9f, true));
            cultist.Skills.Add(new SkillInstance("e_c_heal", "Dark Mend", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.LowestHpAlly, SkillEffectType.Heal, 0, 1, 1.6f));
            state.Units.Add(cultist);
        }

        private static void SpawnEliteEncounter(BattleState state, int corruptionLevel)
        {
            float statScale = 1.15f + (corruptionLevel * 0.007f);

            // 1. Elite Guardian (Front[1]) - Armored
            var tank = new UnitState("e_elite_tank", "[Armored] Ironclad Sentinel", TeamSide.Enemy, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 1),
                (int)(270 * statScale), (int)(28 * statScale), 24 + 8, 11);
            tank.BaseThreat = 20f;
            tank.Affix = EnemyAffixType.Armored;
            tank.Shield = 40;
            tank.Skills.Add(new SkillInstance("e_et_atk", "Warhammer Bash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            tank.Skills.Add(new SkillInstance("e_et_shield", "Bulwark Fortify", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 2.0f));
            state.Units.Add(tank);

            // 2. Elite Assassin (Front[0]) - Berserker, có chiêu nhảy hàng sau (CanTargetBackline)
            var assassin = new UnitState("e_elite_assassin", "[Berserker] Shadow Infiltrator", TeamSide.Enemy, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 0),
                (int)(150 * statScale), (int)(46 * statScale), 10, 18);
            assassin.BaseThreat = 50f;
            assassin.Affix = EnemyAffixType.Berserker;
            assassin.Skills.Add(new SkillInstance("e_ea_atk", "Twin Daggers", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.1f, true));
            assassin.Skills.Add(new SkillInstance("e_ea_dive", "Throat Slit", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 0, 2, 1.8f, true));
            state.Units.Add(assassin);

            // 3. Elite Sniper (Back[0])
            var sniper = new UnitState("e_elite_sniper", "Canyon Marksman", TeamSide.Enemy, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0),
                (int)(140 * statScale), (int)(42 * statScale), 8, 16);
            sniper.BaseThreat = 40f;
            sniper.Skills.Add(new SkillInstance("e_es_atk", "Heavy Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            sniper.Skills.Add(new SkillInstance("e_es_crit", "Armor Piercer", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 20, 2, 1.9f, true));
            state.Units.Add(sniper);

            // 4. Elite Mage (Mid[1]) - Regenerator
            var mage = new UnitState("e_elite_mage", "[Regenerator] Blight Sorcerer", TeamSide.Enemy, UnitRole.Mage,
                new FormationPosition(RowType.Mid, 1),
                (int)(150 * statScale), (int)(40 * statScale), 12, 13);
            mage.BaseThreat = 40f;
            mage.Affix = EnemyAffixType.Regenerator;
            mage.Skills.Add(new SkillInstance("e_em_atk", "Void Ray", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            mage.Skills.Add(new SkillInstance("e_em_aoe", "Abyssal Shockwave", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 20, 2, 1.4f, true));
            state.Units.Add(mage);
        }

        private static void SpawnBossWardenEncounter(BattleState state, int corruptionLevel)
        {
            float statScale = 1.25f + (corruptionLevel * 0.01f);

            // Boss: "The Warden" (GDD Section 38 - Multi-Phase Boss)
            var warden = new UnitState("boss_warden", "The Warden", TeamSide.Enemy, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 1),
                (int)(520 * statScale), (int)(48 * statScale), 26, 12);
            warden.BaseThreat = 100f;
            warden.Shield = 80;
            warden.BossPhase = 1; // Khởi đầu ở Phase 1
            warden.Skills.Add(new SkillInstance("bw_basic", "Crushing Greathammer", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.2f));
            warden.Skills.Add(new SkillInstance("bw_shield", "Fortress Barrier", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 2.5f));
            warden.Skills.Add(new SkillInstance("bw_sweep", "Earthshatter Sweep", SkillSlotType.Ultimate, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 30, 3, 1.6f, true));
            warden.Skills.Add(new SkillInstance("bw_stun", "Oppressive Aura", SkillSlotType.Skill2, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.0f));
            state.Units.Add(warden);

            // Boss Minion 1: Warden's Enforcer (Front[0])
            var minion1 = new UnitState("boss_m_enforcer", "Warden's Enforcer", TeamSide.Enemy, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 0),
                (int)(190 * statScale), (int)(32 * statScale), 16, 13);
            minion1.BaseThreat = 30f;
            minion1.Skills.Add(new SkillInstance("be_atk", "Heavy Mace", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            minion1.Skills.Add(new SkillInstance("be_stun", "Shield Bash", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.2f));
            state.Units.Add(minion1);

            // Boss Minion 2: Warden's Arcanist (Back[1])
            var minion2 = new UnitState("boss_m_arcanist", "Warden's Arcanist", TeamSide.Enemy, UnitRole.Mage,
                new FormationPosition(RowType.Back, 1),
                (int)(160 * statScale), (int)(38 * statScale), 12, 14);
            minion2.BaseThreat = 35f;
            minion2.Skills.Add(new SkillInstance("ba_atk", "Force Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            minion2.Skills.Add(new SkillInstance("ba_aoe", "Shatter Nova", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 20, 2, 1.3f, true));
            state.Units.Add(minion2);
        }
    }
}

