using System;
using System.Collections.Generic;
using UnityEngine;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Presentation.BattleView;

namespace CrownlessWarband.Presentation.UI
{
    /// <summary>
    /// Giao diện F3 Explainable AI Debug Overlay (GDD Mục 44).
    /// Hiển thị bảng phân rã điểm số chi tiết Utility AI (Threat, KillProbability, Vulnerability, StrategicValue)
    /// và quyết định chiến lược Squad AI trong thời gian thực.
    /// </summary>
    public class AIDebugOverlayUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleSandboxController sandboxController;

        [Header("Debug Settings")]
        public bool isVisible = true; // Mặc định mở để kiểm thử tiện lợi

        private string _selectedUnitId = "p_vanguard";
        private Vector2 _decisionScrollPos = Vector2.zero;
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _bodyStyle;
        private bool _stylesInitialized;

        private void Awake()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }
        }

        private void Update()
        {
            // Bấm F3 để bật/tắt bảng AI Debug
            if (Input.GetKeyDown(KeyCode.F3))
            {
                isVisible = !isVisible;
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _windowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 12, 12)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 1f, 0.4f) }
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }

            if (sandboxController == null || sandboxController.CurrentState == null) return;

            InitStyles();

            var state = sandboxController.CurrentState;
            float panelWidth = Mathf.Min(560, Screen.width * 0.42f);
            float panelX = Screen.width - panelWidth - 10;
            Rect panelRect = new Rect(panelX, 10, panelWidth, Screen.height - 20);

            // Nền tối bán trong suốt cho F3 Debug Panel
            GUI.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            GUILayout.BeginArea(panelRect, _windowStyle);

            // Tiêu đề & Hướng dẫn phím tắt
            GUILayout.BeginHorizontal();
            GUILayout.Label("=== [F3] EXPLAINABLE AI DEBUGGER ===", _headerStyle);
            if (GUILayout.Button("Close [F3]", GUILayout.Width(75), GUILayout.Height(22)))
            {
                isVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.Label($"Squad Strategies -> Player: <color=cyan>[{state.PlayerSquadStrategy}]</color> | Enemy: <color=#ff6666>[{state.EnemySquadStrategy}]</color>", _bodyStyle);

            GUILayout.Space(6);
            GUILayout.Label("--- SELECT UNIT BRAIN TO INSPECT ---", _subHeaderStyle);

            // Hàng chọn Unit phe Player
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player: ", GUILayout.Width(50));
            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.Team != TeamSide.Player) continue;

                bool isSelected = (u.InstanceId == _selectedUnitId);
                GUI.backgroundColor = isSelected ? Color.cyan : (u.IsAlive ? new Color(0.2f, 0.4f, 0.6f) : Color.gray);
                if (GUILayout.Button(u.Name, GUILayout.Height(24)))
                {
                    _selectedUnitId = u.InstanceId;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Hàng chọn Unit phe Enemy
            GUILayout.BeginHorizontal();
            GUILayout.Label("Enemy: ", GUILayout.Width(50));
            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.Team != TeamSide.Enemy) continue;

                bool isSelected = (u.InstanceId == _selectedUnitId);
                GUI.backgroundColor = isSelected ? Color.red : (u.IsAlive ? new Color(0.6f, 0.2f, 0.2f) : Color.gray);
                if (GUILayout.Button(u.Name, GUILayout.Height(24)))
                {
                    _selectedUnitId = u.InstanceId;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Hiển thị chi tiết quyết định Utility AI của Unit đã chọn
            UnitState selectedUnit = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                if (state.Units[i].InstanceId == _selectedUnitId)
                {
                    selectedUnit = state.Units[i];
                    break;
                }
            }

            if (selectedUnit != null)
            {
                string aliveText = selectedUnit.IsAlive ? $"<color=green>Alive</color> (HP: {selectedUnit.CurrentHP}/{selectedUnit.MaxHP})" : "<color=red>DEAD</color>";
                GUILayout.Label($"Inspecting: <b>{selectedUnit.Name}</b> ({selectedUnit.Team} {selectedUnit.Role}) - {aliveText}", _subHeaderStyle);

                GUILayout.Space(4);

                _decisionScrollPos = GUILayout.BeginScrollView(_decisionScrollPos, GUI.skin.box);

                if (state.LastUnitDecisions.TryGetValue(selectedUnit.InstanceId, out var decisionLog))
                {
                    GUILayout.Label(decisionLog.FormatForDebugUI(), _bodyStyle);
                }
                else
                {
                    GUILayout.Space(20);
                    GUI.color = Color.yellow;
                    GUILayout.Label($"Chưa có bản ghi tư duy AI cho '{selectedUnit.Name}'.\n\nNhấn [SPACE] (Run Auto-Combat Turn) để Unit thực hiện lượt đi và sinh ra bảng điểm số Utility AI!", _bodyStyle);
                    GUI.color = Color.white;
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
