using System;
using System.Collections.Generic;
using System.IO;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Events;
using CrownlessWarband.Core.Roguelite;
using CrownlessWarband.Core.Save;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    public enum SandboxScreenMode
    {
        CampaignMap,
        ActiveBattle,
        PostBattleReward,
        BatchSimulator
    }

    /// <summary>
    /// Sandbox điều khiển kịch bản trận đấu và vòng lặp Roguelite Campaign (GDD Section 5, 19, 50, 53).
    /// Cung cấp giao diện OnGUI & phím tắt để kiểm thử toàn diện chu trình 3 pha,
    /// Player AI Policy Editor, Campaign Map Navigation, 3-Choice Post-Battle Rewards và JSON Save/Load.
    /// </summary>
    public class BattleSandboxController : MonoBehaviour
    {
        public BattleState CurrentState { get; private set; }
        public RunState CurrentRun { get; private set; }
        public SandboxScreenMode ScreenMode = SandboxScreenMode.ActiveBattle;

        private Vector2 _combatLogScrollPos = Vector2.zero;
        private Vector2 _mapScrollPos = Vector2.zero;
        private Vector2 _policyScrollPos = Vector2.zero;
        private Vector2 _simReportScrollPos = Vector2.zero;

        // Trạng thái cho Headless AI Batch Simulator
        private BatchSimulationResult _lastSimResult;
        private WorldModifierType _simModifier = WorldModifierType.None;
        private UnitBehaviorPreset _simPreset = UnitBehaviorPreset.Balanced;

        private string _saveStatusMsg = "Save System Ready";
        private string SaveFilePath => Path.Combine(Application.persistentDataPath, "crownless_run.save.json");

        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _unitCardStyle;
        private GUIStyle _logStyle;
        private GUIStyle _nodeStyle;
        private GUIStyle _rewardCardStyle;
        private bool _stylesInitialized;

        private void Start()
        {
            if (CurrentRun == null)
            {
                CurrentRun = CampaignGenerator.CreateNewRun(847291, CampaignGenerator.CreateDefaultPlayerWarband());
            }

            ResetScenario();
        }

        private void Update()
        {
            if (ScreenMode == SandboxScreenMode.ActiveBattle)
            {
                // Phím SPACE: Thực thi Auto Combat Phase khi đang ở Planning
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    RunAutoCombat();
                }

                // Phím R: Reset lại trận đấu về đầu Turn 1
                if (Input.GetKeyDown(KeyCode.R))
                {
                    ResetScenario();
                }
            }
        }

        public void ResetScenario()
        {
            SetupGDDSection53Scenario();
            TurnManager.StartPlanningPhase(CurrentState);
        }

        public void RunAutoCombat()
        {
            if (CurrentState == null || TurnManager.IsBattleOver(CurrentState)) return;

            if (CurrentState.CurrentPhase == TurnPhase.Planning)
            {
                TurnManager.ExecuteAutoCombatPhase(CurrentState);
            }
        }

        public void TriggerManualMageMeteor()
        {
            if (CurrentState == null || CurrentState.CurrentPhase != TurnPhase.Planning) return;

            UnitState mage = null;
            for (int i = 0; i < CurrentState.Units.Count; i++)
            {
                if (CurrentState.Units[i].InstanceId == "p_mage" && CurrentState.Units[i].IsAlive)
                {
                    mage = CurrentState.Units[i];
                    break;
                }
            }

            if (mage != null)
            {
                // Cung cấp đủ 40 Energy để test kích hoạt chiêu cuối thủ công
                if (mage.CurrentEnergy < 40)
                {
                    mage.CurrentEnergy = 40;
                }

                TurnManager.TryExecuteManualSkill(CurrentState, "p_mage", "p_m_ult", null);
            }
        }

        public void KillEnemyAssassinForAdaptationTest()
        {
            if (CurrentState == null) return;

            for (int i = 0; i < CurrentState.Units.Count; i++)
            {
                var u = CurrentState.Units[i];
                if (u.InstanceId == "e_assassin" && u.IsAlive)
                {
                    u.CurrentHP = 0;
                    u.Shield = 0;
                    CurrentState.EnqueueEvent(new CombatEvent
                    {
                        EventType = CombatEventType.UnitDied,
                        TurnNumber = CurrentState.CurrentTurn,
                        SourceUnitId = "SANDBOX_DEBUG",
                        TargetUnitId = u.InstanceId,
                        SkillName = "Insta-Kill Debug",
                        Description = $"{u.Name} was eliminated by Sandbox Debug Command! Testing AI Adaptation..."
                    });

                    // Kích hoạt đánh giá lại chiến lược phe địch ngay lập tức
                    SquadAI.EvaluateAndUpdateStrategy(CurrentState, TeamSide.Enemy);
                    break;
                }
            }
        }

        /// <summary>
        /// Khởi tạo kịch bản trận đấu chuẩn GDD Mục 53 (5v5).
        /// </summary>
        public void SetupGDDSection53Scenario()
        {
            CurrentState = new BattleState
            {
                RunSeed = CurrentRun != null ? CurrentRun.Seed : 12345,
                CurrentTurn = 1,
                CurrentPhase = TurnPhase.Planning,
                PlayerSquadStrategy = SquadStrategyType.Balanced,
                EnemySquadStrategy = SquadStrategyType.DirectAssault
            };

            // 1. PHE NGƯỜI CHƠI (Player Team)
            if (CurrentRun != null && CurrentRun.PlayerRoster != null && CurrentRun.PlayerRoster.Count > 0)
            {
                for (int i = 0; i < CurrentRun.PlayerRoster.Count; i++)
                {
                    CurrentState.Units.Add(CurrentRun.PlayerRoster[i]);
                }
                for (int i = 0; i < CurrentRun.CollectedRelics.Count; i++)
                {
                    CurrentState.ActiveRelics.Add(CurrentRun.CollectedRelics[i]);
                }
            }
            else
            {
                var defaultUnits = CampaignGenerator.CreateDefaultPlayerWarband();
                for (int i = 0; i < defaultUnits.Count; i++)
                {
                    CurrentState.Units.Add(defaultUnits[i]);
                }
            }

            // 2. PHE ĐỊCH (Enemy Team chuẩn GDD 53)
            var eTank = new UnitState("e_tank", "Enemy Tank", TeamSide.Enemy, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 1), 260, 26, 22, 10);
            eTank.BaseThreat = 15f;
            eTank.Skills.Add(new SkillInstance("e_t_basic", "Mace Smash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            eTank.Skills.Add(new SkillInstance("e_t_s1", "Iron Guard", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 1.8f));
            CurrentState.Units.Add(eTank);

            var eAssassin = new UnitState("e_assassin", "Enemy Assassin", TeamSide.Enemy, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 0), 135, 48, 8, 18);
            eAssassin.BaseThreat = 45f;
            eAssassin.Skills.Add(new SkillInstance("e_a_basic", "Shadow Stab", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.1f, true));
            eAssassin.Skills.Add(new SkillInstance("e_a_s1", "Backline Ambush", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 0, 2, 1.7f, true));
            CurrentState.Units.Add(eAssassin);

            var eSniper = new UnitState("e_sniper", "Enemy Sniper", TeamSide.Enemy, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0), 130, 42, 8, 15);
            eSniper.BaseThreat = 35f;
            eSniper.Skills.Add(new SkillInstance("e_s_basic", "Piercing Shot", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            CurrentState.Units.Add(eSniper);

            var eMage = new UnitState("e_mage", "Enemy Mage", TeamSide.Enemy, UnitRole.Mage,
                new FormationPosition(RowType.Mid, 1), 145, 40, 10, 12);
            eMage.BaseThreat = 35f;
            eMage.Skills.Add(new SkillInstance("e_m_basic", "Dark Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            eMage.Skills.Add(new SkillInstance("e_m_s1", "Shockwave", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 20, 2, 1.3f, true));
            CurrentState.Units.Add(eMage);

            var eSupport = new UnitState("e_support", "Enemy Support", TeamSide.Enemy, UnitRole.Medic,
                new FormationPosition(RowType.Back, 2), 140, 32, 10, 14);
            eSupport.BaseThreat = 35f;
            eSupport.Skills.Add(new SkillInstance("e_sup_basic", "Zap", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 0.8f, true));
            eSupport.Skills.Add(new SkillInstance("e_sup_s1", "Dark Mend", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.LowestHpAlly, SkillEffectType.Heal, 0, 1, 1.6f));
            CurrentState.Units.Add(eSupport);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };

            _unitCardStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                padding = new RectOffset(6, 6, 6, 6)
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            _nodeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            _rewardCardStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            float panelWidth = Mathf.Min(740, Screen.width * 0.60f);
            GUILayout.BeginArea(new Rect(10, 10, panelWidth, Screen.height - 20), _boxStyle);

            // 1. Top Global Bar (Thông tin chung và Chuyển đổi màn hình)
            DrawTopGlobalBar();

            GUILayout.Space(6);

            // 2. Chuyển đổi nội dung theo ScreenMode
            switch (ScreenMode)
            {
                case SandboxScreenMode.CampaignMap:
                    DrawCampaignMapScreen(panelWidth);
                    break;

                case SandboxScreenMode.ActiveBattle:
                    DrawActiveBattleScreen(panelWidth);
                    break;

                case SandboxScreenMode.PostBattleReward:
                    DrawPostBattleRewardScreen();
                    break;

                case SandboxScreenMode.BatchSimulator:
                    DrawBatchSimulatorScreen(panelWidth);
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawTopGlobalBar()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // Dòng 1: Thông tin Run, Relics & Modifier
            GUILayout.BeginHorizontal();
            string relicsText = (CurrentRun != null && CurrentRun.CollectedRelics.Count > 0)
                ? string.Join(", ", CurrentRun.CollectedRelics)
                : "None";

            GUI.color = Color.yellow;
            GUILayout.Label($"<b>SEED:</b> {CurrentRun?.Seed} | <b>Floor:</b> {CurrentRun?.CurrentFloor}/4 | <b>Corruption:</b> {CurrentRun?.CorruptionLevel}% | <b>Wins:</b> {CurrentRun?.BattlesWon} | <b>Modifier:</b> [{CurrentState?.ActiveModifier}]");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.4f, 1f, 0.4f);
            GUILayout.Label($"<b>Relics Active:</b> [{relicsText}]");
            GUI.color = Color.gray;
            GUILayout.Label($"<i>{_saveStatusMsg}</i>", GUILayout.Width(280));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Dòng 2: Nút chuyển màn hình & Lưu/Nạp JSON
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = ScreenMode == SandboxScreenMode.CampaignMap ? Color.cyan : Color.white;
            if (GUILayout.Button("[1] Map & AI", GUILayout.Height(26)))
            {
                ScreenMode = SandboxScreenMode.CampaignMap;
            }

            GUI.backgroundColor = ScreenMode == SandboxScreenMode.ActiveBattle ? Color.green : Color.white;
            if (GUILayout.Button("[2] 5v5 Battle", GUILayout.Height(26)))
            {
                ScreenMode = SandboxScreenMode.ActiveBattle;
            }

            GUI.backgroundColor = ScreenMode == SandboxScreenMode.BatchSimulator ? new Color(1f, 0.4f, 1f) : Color.white;
            if (GUILayout.Button("[3] AI Batch Simulator", GUILayout.Height(26)))
            {
                ScreenMode = SandboxScreenMode.BatchSimulator;
            }

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("[Save JSON]", GUILayout.Height(26)))
            {
                SaveRunToJson();
            }

            if (GUILayout.Button("[Load JSON]", GUILayout.Height(26)))
            {
                LoadRunFromJson();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawCampaignMapScreen(float panelWidth)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("=== PROCEDURAL CAMPAIGN MAP (5-FLOORS) ===", _headerStyle);
            if (GUILayout.Button("[New Run (847291)]", GUILayout.Width(130)))
            {
                CurrentRun = CampaignGenerator.CreateNewRun(847291, CampaignGenerator.CreateDefaultPlayerWarband());
                _saveStatusMsg = "New Run Initialized (Seed: 847291)";
            }
            if (GUILayout.Button("[Random Seed]", GUILayout.Width(100)))
            {
                int rSeed = UnityEngine.Random.Range(100000, 999999);
                CurrentRun = CampaignGenerator.CreateNewRun(rSeed, CampaignGenerator.CreateDefaultPlayerWarband());
                _saveStatusMsg = $"New Run Initialized (Seed: {rSeed})";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            _mapScrollPos = GUILayout.BeginScrollView(_mapScrollPos, GUILayout.Height(210));

            var currentNode = CurrentRun?.GetCurrentNode();

            // Hiển thị danh sách 5 tầng
            for (int f = 0; f <= 4; f++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Floor {f}:</b>", GUILayout.Width(65));

                for (int i = 0; i < CurrentRun.Nodes.Count; i++)
                {
                    var node = CurrentRun.Nodes[i];
                    if (node.FloorIndex != f) continue;

                    bool isCurrent = node.NodeId == CurrentRun.CurrentNodeId;
                    bool isNextAvailable = currentNode != null && currentNode.IsCompleted && currentNode.NextNodeIds.Contains(node.NodeId);
                    bool isStartSelectable = (f == 0 && !node.IsCompleted);

                    string modText = node.Modifier != WorldModifierType.None ? $" | {node.Modifier}" : "";
                    string nodeLabel = $"{node.NodeId}: {node.Title}\n[{node.NodeType}{modText}]";
                    if (node.IsCompleted) nodeLabel += " (DONE)";

                    if (isCurrent)
                    {
                        GUI.backgroundColor = Color.yellow;
                    }
                    else if (isNextAvailable || isStartSelectable)
                    {
                        GUI.backgroundColor = Color.green;
                    }
                    else if (node.IsCompleted)
                    {
                        GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                    }
                    else
                    {
                        GUI.backgroundColor = Color.white;
                    }

                    GUI.enabled = (isCurrent && !node.IsCompleted) || isNextAvailable || isStartSelectable;
                    if (GUILayout.Button(nodeLabel, _nodeStyle, GUILayout.Width(240), GUILayout.Height(36)))
                    {
                        SelectCampaignNode(node);
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;
                    GUILayout.Space(6);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(3);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Bảng cấu hình Player AI Policy (GDD Section 19)
            GUILayout.Label("=== PLAYER-CONFIGURABLE AI POLICY EDITOR (GDD Section 19) ===", _headerStyle);
            GUILayout.Label("<i>Click buttons below to cycle Behavior Presets, Target Rules, or Skill Activation Conditions per unit:</i>", _logStyle);

            _policyScrollPos = GUILayout.BeginScrollView(_policyScrollPos, GUILayout.Height(190));

            for (int i = 0; i < CurrentRun.PlayerRoster.Count; i++)
            {
                var unit = CurrentRun.PlayerRoster[i];
                var policy = unit.AIPolicy ?? (unit.AIPolicy = new UnitAIPolicy());

                GUILayout.BeginVertical(_unitCardStyle);
                GUILayout.BeginHorizontal();

                GUI.color = Color.cyan;
                GUILayout.Label($"<b>{unit.Name}</b> ({unit.Role})", GUILayout.Width(140));
                GUI.color = Color.white;

                // 1. Behavior Preset Button (Click to cycle)
                string behaviorTxt = $"Behavior: [{policy.BehaviorPreset}]";
                if (GUILayout.Button(behaviorTxt, GUILayout.Width(150)))
                {
                    policy.BehaviorPreset = (UnitBehaviorPreset)(((int)policy.BehaviorPreset + 1) % 5);
                }

                // 2. Target Priority Rule Button (Click to cycle)
                string targetRuleTxt = $"Target: [{policy.PrimaryTargetRule}]";
                if (GUILayout.Button(targetRuleTxt, GUILayout.Width(180)))
                {
                    policy.PrimaryTargetRule = (TargetPriorityRule)(((int)policy.PrimaryTargetRule + 1) % 6);
                }

                // 3. Skill Activation Condition Button (Click to cycle)
                string skillCondTxt = $"Skill: [{policy.SpecialSkillCondition}]";
                if (GUILayout.Button(skillCondTxt, GUILayout.Width(190)))
                {
                    policy.SpecialSkillCondition = (SkillConditionType)(((int)policy.SpecialSkillCondition + 1) % 6);
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(2);
            }

            GUILayout.EndScrollView();
        }

        private void SelectCampaignNode(CampaignNode node)
        {
            CurrentRun.CurrentNodeId = node.NodeId;
            CurrentRun.CurrentFloor = node.FloorIndex;

            if (node.NodeType == CampaignNodeType.Recovery || node.NodeType == CampaignNodeType.Event)
            {
                string outcome = CampaignGenerator.ResolveNonCombatNode(CurrentRun, node);
                _saveStatusMsg = outcome;

                if (CurrentRun.PendingRewards != null && CurrentRun.PendingRewards.Count > 0)
                {
                    ScreenMode = SandboxScreenMode.PostBattleReward;
                }
            }
            else
            {
                // Combat, EliteCombat, hoặc Boss
                CurrentState = CampaignGenerator.CreateBattleForNode(CurrentRun, node);
                TurnManager.StartPlanningPhase(CurrentState);
                ScreenMode = SandboxScreenMode.ActiveBattle;
                _saveStatusMsg = $"Entered {node.Title} ({node.NodeType})";
            }
        }

        private void DrawActiveBattleScreen(float panelWidth)
        {
            // Header: Thông tin Turn, Phase, Squad Strategies
            GUILayout.BeginHorizontal();
            GUILayout.Label($"TURN {CurrentState.CurrentTurn} | Phase: {CurrentState.CurrentPhase}", _headerStyle);
            if (TurnManager.IsBattleOver(CurrentState, out var winner))
            {
                if (winner == TeamSide.Player)
                {
                    GUI.color = Color.green;
                    GUILayout.Label($"VICTORY! All enemies eliminated!", _headerStyle);
                }
                else
                {
                    GUI.color = Color.red;
                    GUILayout.Label($"DEFEAT! Warband has fallen!", _headerStyle);
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            GUI.color = Color.cyan;
            GUILayout.Label($"Player: [{CurrentState.PlayerSquadStrategy}]");
            GUI.color = new Color(1f, 0.5f, 0.5f);
            GUILayout.Label($"Enemy: [{CurrentState.EnemySquadStrategy}]");
            GUI.color = new Color(1f, 0.8f, 0.3f);
            GUILayout.Label($"Mod: [{CurrentState.ActiveModifier}]");
            GUI.color = Color.white;

            if (CurrentState.CurrentPhase == TurnPhase.Planning && !TurnManager.IsBattleOver(CurrentState))
            {
                if (GUILayout.Button($"[Cycle Mod]", GUILayout.Width(100)))
                {
                    CurrentState.ActiveModifier = (WorldModifierType)(((int)CurrentState.ActiveModifier + 1) % 5);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Nút bấm tương tác chiến đấu
            GUILayout.BeginHorizontal();
            GUI.enabled = (CurrentState.CurrentPhase == TurnPhase.Planning && !TurnManager.IsBattleOver(CurrentState));
            if (GUILayout.Button("[SPACE] Run Auto-Combat Turn", GUILayout.Height(30)))
            {
                RunAutoCombat();
            }

            if (GUILayout.Button("[Manual Ult: Mage Meteor]", GUILayout.Height(30)))
            {
                TriggerManualMageMeteor();
            }
            GUI.enabled = true;

            if (GUILayout.Button("[Test Boss Warden]", GUILayout.Height(30), GUILayout.Width(135)))
            {
                var bossNode = CurrentRun != null ? CurrentRun.Nodes.Find(n => n.NodeType == CampaignNodeType.Boss) : null;
                if (bossNode == null) bossNode = new CampaignNode(4, 0, "F4_L0", CampaignNodeType.Boss, "The Warden's Stronghold", 100);
                CurrentState = CampaignGenerator.CreateBattleForNode(CurrentRun, bossNode);
                TurnManager.StartPlanningPhase(CurrentState);
                _saveStatusMsg = "Spawned Boss: The Warden (Multi-Phase Test)";
            }

            if (GUILayout.Button("[Kill Assassin]", GUILayout.Height(30), GUILayout.Width(100)))
            {
                KillEnemyAssassinForAdaptationTest();
            }

            if (GUILayout.Button("[R] Reset", GUILayout.Height(30), GUILayout.Width(65)))
            {
                ResetScenario();
            }
            GUILayout.EndHorizontal();

            // Nếu trận đấu kết thúc thắng lợi: Hiện nút Claim Rewards
            if (TurnManager.IsBattleOver(CurrentState, out var battleWinner))
            {
                GUILayout.Space(4);
                if (battleWinner == TeamSide.Player)
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("<b>VICTORY! Claim Rewards & Advance Campaign -></b>", GUILayout.Height(34)))
                    {
                        var node = CurrentRun?.GetCurrentNode() ?? (CurrentRun.Nodes.Count > 0 ? CurrentRun.Nodes[0] : null);
                        if (node != null)
                        {
                            CampaignGenerator.CompleteNodeVictory(CurrentRun, node);
                        }

                        if (CurrentRun.PendingRewards != null && CurrentRun.PendingRewards.Count > 0)
                        {
                            ScreenMode = SandboxScreenMode.PostBattleReward;
                        }
                        else
                        {
                            ScreenMode = SandboxScreenMode.CampaignMap;
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                    if (GUILayout.Button("<b>Warband Fallen. Return to Campaign Map</b>", GUILayout.Height(34)))
                    {
                        ScreenMode = SandboxScreenMode.CampaignMap;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            GUILayout.Space(8);

            // Danh sách 2 phe
            GUILayout.BeginHorizontal();

            // Phe Người chơi
            GUILayout.BeginVertical(GUILayout.Width((panelWidth - 30) * 0.5f));
            GUI.color = Color.cyan;
            GUILayout.Label("--- PLAYER WARBAND ---", _headerStyle);
            GUI.color = Color.white;
            DrawTeamList(TeamSide.Player);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Phe Địch
            GUILayout.BeginVertical(GUILayout.Width((panelWidth - 30) * 0.5f));
            GUI.color = new Color(1f, 0.5f, 0.5f);
            GUILayout.Label("--- ENEMY FORCES ---", _headerStyle);
            GUI.color = Color.white;
            DrawTeamList(TeamSide.Enemy);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Nhật ký chiến đấu (Combat Log)
            GUILayout.Label("=== RECENT COMBAT EVENTS ===", _headerStyle);
            _combatLogScrollPos = GUILayout.BeginScrollView(_combatLogScrollPos, GUILayout.Height(130));
            int logCount = CurrentState.CombatLog.Count;
            int startIdx = Mathf.Max(0, logCount - 15);
            for (int i = startIdx; i < logCount; i++)
            {
                GUILayout.Label(CurrentState.CombatLog[i], _logStyle);
            }
            GUILayout.EndScrollView();
        }

        private void DrawPostBattleRewardScreen()
        {
            GUILayout.Label("=== POST-BATTLE REWARDS (Choose 1 of 3) ===", _headerStyle);
            GUILayout.Label("<i>Select one upgrade or tactical relic to bolster your warband for subsequent floors:</i>", _logStyle);

            GUILayout.Space(10);

            if (CurrentRun.PendingRewards == null || CurrentRun.PendingRewards.Count == 0)
            {
                GUILayout.Label("No pending rewards found.");
                if (GUILayout.Button("Return to Campaign Map", GUILayout.Height(30)))
                {
                    ScreenMode = SandboxScreenMode.CampaignMap;
                }
                return;
            }

            for (int i = 0; i < CurrentRun.PendingRewards.Count; i++)
            {
                var opt = CurrentRun.PendingRewards[i];

                GUILayout.BeginVertical(_rewardCardStyle);
                GUI.color = Color.yellow;
                GUILayout.Label($"<b>{opt.Title}</b>  <color=cyan>[{opt.RewardType}]</color>", _headerStyle);
                GUI.color = Color.white;
                GUILayout.Label(opt.Description, _logStyle);

                GUILayout.Space(6);
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button($"Select {opt.Title}", GUILayout.Height(30)))
                {
                    CampaignGenerator.ApplyReward(CurrentRun, opt);
                    CurrentRun.PendingRewards.Clear();
                    ScreenMode = SandboxScreenMode.CampaignMap;
                    _saveStatusMsg = $"Applied Reward: {opt.Title}";
                    GUILayout.EndVertical();
                    break;
                }
                GUI.backgroundColor = Color.white;

                GUILayout.EndVertical();
                GUILayout.Space(8);
            }
        }

        private void DrawTeamList(TeamSide team)
        {
            for (int i = 0; i < CurrentState.Units.Count; i++)
            {
                var u = CurrentState.Units[i];
                if (u.Team != team) continue;

                if (!u.IsAlive)
                {
                    GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }
                else
                {
                    GUI.backgroundColor = (team == TeamSide.Player)
                        ? new Color(0.1f, 0.25f, 0.35f, 0.9f)
                        : new Color(0.35f, 0.15f, 0.15f, 0.9f);
                }

                GUILayout.BeginVertical(_unitCardStyle);

                string statusText = "";
                if (u.IsStunned) statusText += " <color=yellow>[STUN]</color>";
                if (u.IsMarked) statusText += " <color=red>[MARK]</color>";
                if (u.BossPhase > 0) statusText += $" <color=orange>[BOSS PHASE {u.BossPhase}]</color>";
                if (u.Affix != EnemyAffixType.None && !u.Name.Contains(u.Affix.ToString())) statusText += $" <color=magenta>[{u.Affix}]</color>";
                if (!string.IsNullOrEmpty(u.ProtectingAllyId)) statusText += $" <color=green>[PROT by {u.ProtectingAllyId}]</color>";

                string stateStatus = u.IsAlive ? $"HP: {u.CurrentHP}/{u.MaxHP}" : "<color=gray>[DEAD]</color>";
                if (u.Shield > 0) stateStatus += $" (+{u.Shield} Shd)";

                GUILayout.Label($"<b>{u.Name}</b> ({u.Role} | {u.Position.Row}[{u.Position.Column}]) {statusText}");
                GUILayout.Label($"{stateStatus} | Năng lượng: {u.CurrentEnergy}/{u.MaxEnergy} | Tốc: {u.Speed}");

                // Hiển thị và cho phép chỉnh nhanh Policy trực tiếp trong Planning Phase cho Unit người chơi
                if (team == TeamSide.Player && u.AIPolicy != null && CurrentState.CurrentPhase == TurnPhase.Planning)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button($"P:[{u.AIPolicy.BehaviorPreset}]", GUILayout.Height(18)))
                    {
                        u.AIPolicy.BehaviorPreset = (UnitBehaviorPreset)(((int)u.AIPolicy.BehaviorPreset + 1) % 5);
                    }
                    if (GUILayout.Button($"T:[{u.AIPolicy.PrimaryTargetRule}]", GUILayout.Height(18)))
                    {
                        u.AIPolicy.PrimaryTargetRule = (TargetPriorityRule)(((int)u.AIPolicy.PrimaryTargetRule + 1) % 6);
                    }
                    GUILayout.EndHorizontal();
                }

                // Liệt kê Cooldown kỹ năng
                string skillsCd = "Chiêu: ";
                for (int s = 0; s < u.Skills.Count; s++)
                {
                    var sk = u.Skills[s];
                    string cdStr = sk.CurrentCooldown > 0 ? $"[CD:{sk.CurrentCooldown}]" : "[Ready]";
                    skillsCd += $"{sk.Name}{cdStr} ";
                }
                GUILayout.Label(skillsCd);

                GUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
                GUILayout.Space(2);
            }
        }

        private void DrawBatchSimulatorScreen(float panelWidth)
        {
            GUILayout.Label("=== HEADLESS AI BATCH SIMULATOR (GDD Section 25 & 49) ===", _headerStyle);
            GUILayout.Label("<i>Runs headless automated batch simulations of N full battles instantaneously in pure C# with randomized seed variance to evaluate AI performance and win rates.</i>", _logStyle);

            GUILayout.Space(8);

            // Hàng tùy chỉnh tham số giả lập
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUI.color = Color.yellow;
            GUILayout.Label("<b>Simulation Parameters:</b>", GUILayout.Width(170));
            GUI.color = Color.white;

            if (GUILayout.Button($"World Modifier: [{_simModifier}]", GUILayout.Height(28)))
            {
                _simModifier = (WorldModifierType)(((int)_simModifier + 1) % 5);
            }

            if (GUILayout.Button($"Team AI Preset: [{_simPreset}]", GUILayout.Height(28)))
            {
                _simPreset = (UnitBehaviorPreset)(((int)_simPreset + 1) % 5);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Nút bấm thực thi giả lập
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("[Run 50 Matches (Fast)]", GUILayout.Height(34)))
            {
                int baseSeed = CurrentRun != null ? CurrentRun.Seed : 12345;
                _lastSimResult = AIBatchSimulator.RunBatchSimulation(50, baseSeed, _simModifier, _simPreset);
                _saveStatusMsg = $"Batch Sim Completed: 50 matches ({_lastSimResult.ElapsedMilliseconds:F1}ms)";
            }

            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("[Run 100 Matches (Academic Benchmark)]", GUILayout.Height(34)))
            {
                int baseSeed = CurrentRun != null ? CurrentRun.Seed : 12345;
                _lastSimResult = AIBatchSimulator.RunBatchSimulation(100, baseSeed, _simModifier, _simPreset);
                _saveStatusMsg = $"Batch Sim Completed: 100 matches ({_lastSimResult.ElapsedMilliseconds:F1}ms)";
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Hiển thị kết quả giả lập
            if (_lastSimResult != null)
            {
                GUILayout.Label("=== SIMULATION METRICS BREAKDOWN ===", _subHeaderStyle);

                GUILayout.BeginHorizontal();

                // Thẻ Win Rate
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(panelWidth * 0.28f));
                GUI.color = _lastSimResult.PlayerWinRate >= 50f ? Color.green : Color.yellow;
                GUILayout.Label($"<b>Win Rate: {_lastSimResult.PlayerWinRate:F1}%</b>", _headerStyle);
                GUI.color = Color.white;
                GUILayout.Label($"Wins: {_lastSimResult.PlayerWins} | Losses: {_lastSimResult.EnemyWins} | Draws: {_lastSimResult.Draws}");
                GUILayout.EndVertical();

                // Thẻ Avg Turns & Strategy Adaptations
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(panelWidth * 0.36f));
                GUI.color = Color.cyan;
                GUILayout.Label($"<b>Avg Turns: {_lastSimResult.AverageTurns:F1}</b>", _headerStyle);
                GUI.color = Color.white;
                GUILayout.Label($"Strategy Adaptations: {_lastSimResult.TotalStrategyAdaptations} ({(float)_lastSimResult.TotalStrategyAdaptations / _lastSimResult.TotalBattles:F1}/match)");
                GUILayout.EndVertical();

                // Thẻ Hiệu năng Thời gian
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.color = Color.magenta;
                GUILayout.Label($"<b>Speed: {_lastSimResult.ElapsedMilliseconds:F1} ms</b>", _headerStyle);
                GUI.color = Color.white;
                GUILayout.Label($"Avg: {_lastSimResult.ElapsedMilliseconds / _lastSimResult.TotalBattles:F2} ms / full battle");
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                GUILayout.Space(6);

                // Hộp hiển thị chi tiết báo cáo dạng text
                GUILayout.Label("=== FULL SIMULATION REPORT (Academic Output) ===", _subHeaderStyle);
                _simReportScrollPos = GUILayout.BeginScrollView(_simReportScrollPos, GUI.skin.box, GUILayout.Height(150));
                GUILayout.TextArea(_lastSimResult.SummaryReport, _logStyle);
                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("No simulation has been run yet. Click one of the buttons above to execute headless matches instantaneously.", _logStyle);
                GUILayout.EndVertical();
            }
        }

        private void SaveRunToJson()
        {
            if (CurrentRun == null) return;
            try
            {
                SaveSystem.SaveRunToFile(CurrentRun, SaveFilePath);
                _saveStatusMsg = $"Saved Run to: {SaveFilePath}";
            }
            catch (Exception ex)
            {
                _saveStatusMsg = $"Save Failed: {ex.Message}";
            }
        }

        private void LoadRunFromJson()
        {
            try
            {
                var loaded = SaveSystem.LoadRunFromFile(SaveFilePath);
                if (loaded != null)
                {
                    CurrentRun = loaded;
                    _saveStatusMsg = $"Loaded Run (Seed: {loaded.Seed}, Floor: {loaded.CurrentFloor})";
                }
                else
                {
                    _saveStatusMsg = "File not found or invalid JSON.";
                }
            }
            catch (Exception ex)
            {
                _saveStatusMsg = $"Load Failed: {ex.Message}";
            }
        }
    }
}
