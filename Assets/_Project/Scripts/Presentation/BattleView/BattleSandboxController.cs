using System;
using System.Collections.Generic;
using UnityEngine;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Events;

namespace CrownlessWarband.Presentation.BattleView
{
    /// <summary>
    /// Sandbox điều khiển trận đấu giả lập kịch bản GDD Mục 53 (5v5).
    /// Cung cấp giao diện OnGUI & phím tắt để kiểm thử toàn diện chu trình 3 pha,
    /// kích hoạt kỹ năng thủ công (Manual Skill) và chế độ F3 Explainable AI.
    /// </summary>
    public class BattleSandboxController : MonoBehaviour
    {
        public BattleState CurrentState { get; private set; }

        private Vector2 _combatLogScrollPos = Vector2.zero;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _unitCardStyle;
        private GUIStyle _logStyle;
        private bool _stylesInitialized;

        private void Start()
        {
            ResetScenario();
        }

        private void Update()
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
                RunSeed = 12345,
                CurrentTurn = 1,
                CurrentPhase = TurnPhase.Planning,
                PlayerSquadStrategy = SquadStrategyType.Balanced,
                EnemySquadStrategy = SquadStrategyType.DirectAssault
            };

            // -------------------------------------------------------------
            // 1. PHE NGƯỜI CHƠI (Player Team)
            // -------------------------------------------------------------
            // Guardian (Front[0])
            var pGuardian = new UnitState("p_guardian", "Guardian", TeamSide.Player, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 0), 280, 28, 24, 11);
            pGuardian.BaseThreat = 20f;
            pGuardian.Skills.Add(new SkillInstance("p_g_basic", "Shield Strike", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            pGuardian.Skills.Add(new SkillInstance("p_g_s1", "Protect Ally", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleAlly, SkillEffectType.ProtectAlly, 0, 2, 1.0f));
            pGuardian.Skills.Add(new SkillInstance("p_g_s2", "Bastion Shield", SkillSlotType.Skill2, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 3, 2.0f));
            CurrentState.Units.Add(pGuardian);

            // Vanguard (Front[1])
            var pVanguard = new UnitState("p_vanguard", "Vanguard", TeamSide.Player, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 1), 220, 34, 18, 14);
            pVanguard.BaseThreat = 25f;
            pVanguard.Skills.Add(new SkillInstance("p_v_basic", "Heavy Slash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            pVanguard.Skills.Add(new SkillInstance("p_v_s1", "Stunning Blow", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.2f));
            CurrentState.Units.Add(pVanguard);

            // Mage (Mid[1])
            var pMage = new UnitState("p_mage", "Mage", TeamSide.Player, UnitRole.Mage,
                new FormationPosition(RowType.Mid, 1), 150, 42, 10, 13);
            pMage.BaseThreat = 35f;
            pMage.CurrentEnergy = 20; // Sẵn sàng test Ultimate sau vài lượt
            pMage.Skills.Add(new SkillInstance("p_m_basic", "Arcane Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            pMage.Skills.Add(new SkillInstance("p_m_s1", "Disable Frost", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.3f, true));
            pMage.Skills.Add(new SkillInstance("p_m_ult", "Meteor Storm", SkillSlotType.Ultimate, SkillControlMode.Manual, SkillTargetType.AllEnemies, SkillEffectType.Damage, 40, 3, 1.8f, true));
            CurrentState.Units.Add(pMage);

            // Ranger (Back[0])
            var pRanger = new UnitState("p_ranger", "Ranger", TeamSide.Player, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0), 140, 46, 8, 16);
            pRanger.BaseThreat = 40f;
            pRanger.Skills.Add(new SkillInstance("p_r_basic", "Quick Shot", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            pRanger.Skills.Add(new SkillInstance("p_r_s1", "Hunter's Mark", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Mark, 0, 2, 1.1f, true));
            pRanger.Skills.Add(new SkillInstance("p_r_ult", "Snipe Execute", SkillSlotType.Ultimate, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 30, 3, 2.2f, true));
            CurrentState.Units.Add(pRanger);

            // Medic (Back[2])
            var pMedic = new UnitState("p_medic", "Medic", TeamSide.Player, UnitRole.Medic,
                new FormationPosition(RowType.Back, 2), 145, 35, 10, 15);
            pMedic.BaseThreat = 35f;
            pMedic.Skills.Add(new SkillInstance("p_med_basic", "Light Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 0.9f, true));
            pMedic.Skills.Add(new SkillInstance("p_med_s1", "Emergency Heal", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.LowestHpAlly, SkillEffectType.Heal, 0, 1, 1.8f));
            CurrentState.Units.Add(pMedic);

            // -------------------------------------------------------------
            // 2. PHE ĐỊCH (Enemy Team)
            // -------------------------------------------------------------
            // Enemy Tank (Front[1])
            var eTank = new UnitState("e_tank", "Enemy Tank", TeamSide.Enemy, UnitRole.Guardian,
                new FormationPosition(RowType.Front, 1), 260, 26, 22, 10);
            eTank.BaseThreat = 15f;
            eTank.Skills.Add(new SkillInstance("e_t_basic", "Mace Smash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f));
            eTank.Skills.Add(new SkillInstance("e_t_s1", "Iron Guard", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 1.8f));
            CurrentState.Units.Add(eTank);

            // Enemy Assassin (Front[0]) - Có chiêu nhảy hàng sau (CanTargetBackline)
            var eAssassin = new UnitState("e_assassin", "Enemy Assassin", TeamSide.Enemy, UnitRole.Vanguard,
                new FormationPosition(RowType.Front, 0), 135, 48, 8, 18);
            eAssassin.BaseThreat = 45f;
            eAssassin.Skills.Add(new SkillInstance("e_a_basic", "Shadow Stab", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.1f, true));
            eAssassin.Skills.Add(new SkillInstance("e_a_s1", "Backline Ambush", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 0, 2, 1.7f, true));
            CurrentState.Units.Add(eAssassin);

            // Enemy Sniper (Back[0])
            var eSniper = new UnitState("e_sniper", "Enemy Sniper", TeamSide.Enemy, UnitRole.Ranger,
                new FormationPosition(RowType.Back, 0), 130, 42, 8, 15);
            eSniper.BaseThreat = 35f;
            eSniper.Skills.Add(new SkillInstance("e_s_basic", "Piercing Shot", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            CurrentState.Units.Add(eSniper);

            // Enemy Mage (Mid[1])
            var eMage = new UnitState("e_mage", "Enemy Mage", TeamSide.Enemy, UnitRole.Mage,
                new FormationPosition(RowType.Mid, 1), 145, 40, 10, 12);
            eMage.BaseThreat = 35f;
            eMage.Skills.Add(new SkillInstance("e_m_basic", "Dark Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true));
            eMage.Skills.Add(new SkillInstance("e_m_s1", "Shockwave", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 20, 2, 1.3f, true));
            CurrentState.Units.Add(eMage);

            // Enemy Support (Back[2])
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

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // Vùng bảng điều khiển chính bên trái màn hình
            float panelWidth = Mathf.Min(650, Screen.width * 0.55f);
            GUILayout.BeginArea(new Rect(10, 10, panelWidth, Screen.height - 20), _boxStyle);

            // Header: Thông tin Turn, Phase, Squad Strategies
            GUILayout.BeginHorizontal();
            GUILayout.Label($"TURN {CurrentState.CurrentTurn} | Phase: {CurrentState.CurrentPhase}", _headerStyle);
            if (TurnManager.IsBattleOver(CurrentState, out var winner))
            {
                GUI.color = Color.green;
                GUILayout.Label($"BATTLE OVER! Winner: {winner}", _headerStyle);
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.color = Color.cyan;
            GUILayout.Label($"Player Squad: [{CurrentState.PlayerSquadStrategy}]");
            GUI.color = new Color(1f, 0.5f, 0.5f);
            GUILayout.Label($"Enemy Squad: [{CurrentState.EnemySquadStrategy}]");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Nút bấm tương tác
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

            if (GUILayout.Button("[Kill Enemy Assassin (Test Dive->Assault)]", GUILayout.Height(30)))
            {
                KillEnemyAssassinForAdaptationTest();
            }

            if (GUILayout.Button("[R] Reset", GUILayout.Height(30), GUILayout.Width(70)))
            {
                ResetScenario();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Hiển thị danh sách 2 phe
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

            GUILayout.Space(10);

            // Nhật ký chiến đấu (Combat Log)
            GUILayout.Label("=== RECENT COMBAT EVENTS ===", _headerStyle);
            _combatLogScrollPos = GUILayout.BeginScrollView(_combatLogScrollPos, GUILayout.Height(150));
            int logCount = CurrentState.CombatLog.Count;
            int startIdx = Mathf.Max(0, logCount - 15);
            for (int i = startIdx; i < logCount; i++)
            {
                GUILayout.Label(CurrentState.CombatLog[i], _logStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
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
                if (!string.IsNullOrEmpty(u.ProtectingAllyId)) statusText += $" <color=green>[PROT by {u.ProtectingAllyId}]</color>";

                string stateStatus = u.IsAlive ? $"HP: {u.CurrentHP}/{u.MaxHP}" : "<color=gray>[DEAD]</color>";
                if (u.Shield > 0) stateStatus += $" (+{u.Shield} Shd)";

                GUILayout.Label($"<b>{u.Name}</b> ({u.Role} | {u.Position.Row}[{u.Position.Column}]) {statusText}");
                GUILayout.Label($"{stateStatus} | Năng lượng: {u.CurrentEnergy}/{u.MaxEnergy} | Tốc: {u.Speed}");

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
    }
}
