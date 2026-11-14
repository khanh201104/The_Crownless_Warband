using System;
using System.Collections.Generic;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Battle;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    [Serializable]
    public struct RoleSpriteEntry
    {
        public TeamSide Team;
        public UnitRole Role;
        public Sprite Sprite;
    }

    /// <summary>
    /// Bộ điều khiển chính của Sàn đấu 2D 3x3 (GDD Sections 8, 9, 11).
    /// Sinh 18 ô lưới FormationSlotView, đồng bộ các UnitVisualView theo BattleState,
    /// hỗ trợ fallback Sprite vuông 1x1 bằng code và cho phép click đổi vị trí ở Turn 1 Planning.
    /// </summary>
    public class BattlefieldGridView : MonoBehaviour
    {
        private static readonly Color SwapHighlightColor = new Color(0.20f, 0.95f, 0.85f, 0.95f);
        private static Sprite _sharedSquareSprite;

        [Header("References")]
        [SerializeField] private BattleSandboxController sandboxController;

        [Header("Optional Custom Sprites")]
        [SerializeField] private List<RoleSpriteEntry> roleSprites = new List<RoleSpriteEntry>();
        [SerializeField] private Sprite bossWardenSprite = null;

        public string SelectedUnitId { get; private set; }
        public event Action<string> OnUnitSelected;

        private readonly List<FormationSlotView> _slotViews = new List<FormationSlotView>(18);
        private readonly Dictionary<string, UnitVisualView> _unitVisuals = new Dictionary<string, UnitVisualView>(16);
        private readonly HashSet<string> _activeUnitIds = new HashSet<string>();
        private readonly List<string> _staleUnitIds = new List<string>(16);

        private GameObject _slotsContainer;
        private GameObject _unitsContainer;
        private BattleState _lastBattleState;
        private bool _isGridVisible = true;

        private void Awake()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }

            GetOrCreateDefaultSprite();
            EnsureContainers();
            SpawnGridSlots();
        }

        private void Update()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
                if (sandboxController == null)
                {
                    SetGridVisible(false);
                    return;
                }
            }

            bool shouldShow = sandboxController.ScreenMode == SandboxScreenMode.ActiveBattle &&
                              sandboxController.CurrentState != null;

            SetGridVisible(shouldShow);

            if (shouldShow)
            {
                RefreshBattlefield(sandboxController.CurrentState);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slotViews.Count; i++)
            {
                if (_slotViews[i] != null)
                {
                    _slotViews[i].OnSlotClicked -= HandleSlotClicked;
                }
            }

            foreach (KeyValuePair<string, UnitVisualView> kvp in _unitVisuals)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.OnUnitClicked -= HandleUnitClicked;
                }
            }
        }

        public static Sprite GetOrCreateDefaultSprite()
        {
            if (_sharedSquareSprite != null) return _sharedSquareSprite;

            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.name = "ProceduralWhite1x1";
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            _sharedSquareSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f
            );
            _sharedSquareSprite.name = "ProceduralWhiteSquareSprite";
            return _sharedSquareSprite;
        }

        public Vector3 GetSlotWorldPosition(TeamSide team, FormationPosition pos)
        {
            float x;
            if (team == TeamSide.Player)
            {
                switch (pos.Row)
                {
                    case RowType.Front: x = -1.20f; break;
                    case RowType.Mid:   x = -2.35f; break;
                    default:            x = -3.50f; break;
                }
            }
            else
            {
                switch (pos.Row)
                {
                    case RowType.Front: x = 1.20f; break;
                    case RowType.Mid:   x = 2.35f; break;
                    default:            x = 3.50f; break;
                }
            }

            float y;
            switch (pos.Column)
            {
                case 0:  y = 1.60f;  break;
                case 1:  y = 0.0f;   break;
                default: y = -1.60f; break;
            }

            return new Vector3(x, y, 0f);
        }

        public UnitVisualView GetUnitVisual(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            UnitVisualView view;
            return _unitVisuals.TryGetValue(instanceId, out view) ? view : null;
        }

        public void RefreshBattlefield(BattleState state)
        {
            if (state == null) return;

            if (_lastBattleState != state)
            {
                _lastBattleState = state;
                SelectedUnitId = null;
            }

            Sprite defaultSprite = GetOrCreateDefaultSprite();
            _activeUnitIds.Clear();

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState unit = state.Units[i];
                if (unit == null || string.IsNullOrEmpty(unit.InstanceId)) continue;

                _activeUnitIds.Add(unit.InstanceId);

                UnitVisualView view;
                if (!_unitVisuals.TryGetValue(unit.InstanceId, out view) || view == null)
                {
                    GameObject unitObj = new GameObject($"UnitVisual_{unit.InstanceId}");
                    unitObj.transform.SetParent(_unitsContainer.transform, false);
                    view = unitObj.AddComponent<UnitVisualView>();
                    view.Initialize(unit, defaultSprite, ResolveCustomSprite(unit));
                    view.OnUnitClicked += HandleUnitClicked;
                    _unitVisuals[unit.InstanceId] = view;
                }

                view.IsVisibleOnBattlefield = _isGridVisible;
                Vector3 slotWorldPos = GetSlotWorldPosition(unit.Team, unit.Position);
                bool isSelected = !string.IsNullOrEmpty(SelectedUnitId) && unit.InstanceId == SelectedUnitId;
                view.SyncFromState(unit, slotWorldPos, isSelected);
            }

            // Dọn dẹp các UnitVisualView không còn trong BattleState hiện tại
            _staleUnitIds.Clear();
            foreach (KeyValuePair<string, UnitVisualView> kvp in _unitVisuals)
            {
                if (!_activeUnitIds.Contains(kvp.Key))
                {
                    _staleUnitIds.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _staleUnitIds.Count; i++)
            {
                string staleId = _staleUnitIds[i];
                UnitVisualView staleView = _unitVisuals[staleId];
                if (staleView != null)
                {
                    staleView.OnUnitClicked -= HandleUnitClicked;
                    Destroy(staleView.gameObject);
                }
                _unitVisuals.Remove(staleId);
            }

            UpdateSlotHighlights(state);
        }

        private void EnsureContainers()
        {
            if (_slotsContainer == null)
            {
                _slotsContainer = new GameObject("GridSlots");
                _slotsContainer.transform.SetParent(transform, false);
            }

            if (_unitsContainer == null)
            {
                _unitsContainer = new GameObject("UnitVisuals");
                _unitsContainer.transform.SetParent(transform, false);
            }
        }

        private void SpawnGridSlots()
        {
            if (_slotViews.Count > 0) return;

            Sprite squareSprite = GetOrCreateDefaultSprite();
            TeamSide[] teams = new TeamSide[] { TeamSide.Player, TeamSide.Enemy };
            RowType[] rows = new RowType[] { RowType.Front, RowType.Mid, RowType.Back };

            for (int t = 0; t < teams.Length; t++)
            {
                TeamSide team = teams[t];
                for (int r = 0; r < rows.Length; r++)
                {
                    RowType row = rows[r];
                    for (int col = 0; col < 3; col++)
                    {
                        FormationPosition pos = new FormationPosition(row, col);
                        Vector3 worldPos = GetSlotWorldPosition(team, pos);
                        worldPos.z = 0.1f; // Đặt ô hơi lùi ra sau Unit để Unit nhận click trước

                        GameObject slotObj = new GameObject($"Slot_{team}_{row}_{col}");
                        slotObj.transform.SetParent(_slotsContainer.transform, false);
                        slotObj.transform.position = worldPos;

                        FormationSlotView slotView = slotObj.AddComponent<FormationSlotView>();
                        slotView.Initialize(team, pos, squareSprite);
                        slotView.OnSlotClicked += HandleSlotClicked;

                        _slotViews.Add(slotView);
                    }
                }
            }
        }

        private void SetGridVisible(bool visible)
        {
            if (_isGridVisible == visible) return;
            _isGridVisible = visible;

            if (_slotsContainer != null)
            {
                _slotsContainer.SetActive(visible);
            }

            if (_unitsContainer != null)
            {
                _unitsContainer.SetActive(visible);
            }

            foreach (KeyValuePair<string, UnitVisualView> kvp in _unitVisuals)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.IsVisibleOnBattlefield = visible;
                }
            }
        }

        private void HandleUnitClicked(string clickedInstanceId)
        {
            if (sandboxController == null || sandboxController.CurrentState == null) return;
            BattleState state = sandboxController.CurrentState;

            UnitState clickedUnit = FindUnitById(state, clickedInstanceId);
            if (clickedUnit == null) return;

            bool isTurn1Planning = state.CurrentPhase == TurnPhase.Planning && state.CurrentTurn == 1;
            UnitState currentSelected = FindUnitById(state, SelectedUnitId);

            // Nếu đang chọn một unit phe Player ở Turn 1 Planning và click sang unit phe Player khác -> Hoán đổi vị trí
            if (isTurn1Planning &&
                currentSelected != null &&
                currentSelected.Team == TeamSide.Player &&
                currentSelected.IsAlive &&
                clickedUnit.Team == TeamSide.Player &&
                clickedUnit.IsAlive)
            {
                if (clickedUnit.InstanceId == currentSelected.InstanceId)
                {
                    SelectedUnitId = null;
                    UpdateSlotHighlights(state);
                    RefreshBattlefield(state);
                    return;
                }

                SwapUnitPositions(state, currentSelected, clickedUnit);
                SelectedUnitId = null;
                UpdateSlotHighlights(state);
                RefreshBattlefield(state);
                return;
            }

            SelectedUnitId = clickedInstanceId;
            OnUnitSelected?.Invoke(SelectedUnitId);
            UpdateSlotHighlights(state);
            RefreshBattlefield(state);
        }

        private void HandleSlotClicked(FormationSlotView clickedSlot)
        {
            if (clickedSlot == null || sandboxController == null || sandboxController.CurrentState == null) return;
            BattleState state = sandboxController.CurrentState;

            if (state.CurrentPhase != TurnPhase.Planning || state.CurrentTurn != 1) return;
            if (clickedSlot.Team != TeamSide.Player) return;

            UnitState selectedUnit = FindUnitById(state, SelectedUnitId);
            if (selectedUnit == null || selectedUnit.Team != TeamSide.Player || !selectedUnit.IsAlive)
            {
                // Nếu chưa chọn unit nào, click vào ô có unit Player đứng sẽ chọn unit đó
                UnitState occupantAtSlot = FindUnitAtSlot(state, TeamSide.Player, clickedSlot.Position);
                if (occupantAtSlot != null && occupantAtSlot.IsAlive)
                {
                    HandleUnitClicked(occupantAtSlot.InstanceId);
                }
                return;
            }

            if (selectedUnit.Position.Equals(clickedSlot.Position))
            {
                SelectedUnitId = null;
                UpdateSlotHighlights(state);
                RefreshBattlefield(state);
                return;
            }

            UnitState occupant = FindUnitAtSlot(state, TeamSide.Player, clickedSlot.Position);
            if (occupant != null && occupant.InstanceId != selectedUnit.InstanceId)
            {
                SwapUnitPositions(state, selectedUnit, occupant);
            }
            else
            {
                FormationPosition oldPos = selectedUnit.Position;
                selectedUnit.Position = clickedSlot.Position;
                state.AppendLog($"Formation Move: {selectedUnit.Name} moved from {oldPos} to {clickedSlot.Position}");
                NotifyFormationChanged(state);
            }

            SelectedUnitId = null;
            UpdateSlotHighlights(state);
            RefreshBattlefield(state);
        }

        private void UpdateSlotHighlights(BattleState state)
        {
            bool canSwapPlayerSlots = false;
            if (state != null && state.CurrentPhase == TurnPhase.Planning && state.CurrentTurn == 1)
            {
                UnitState selectedUnit = FindUnitById(state, SelectedUnitId);
                if (selectedUnit != null && selectedUnit.Team == TeamSide.Player && selectedUnit.IsAlive)
                {
                    canSwapPlayerSlots = true;
                }
            }

            for (int i = 0; i < _slotViews.Count; i++)
            {
                FormationSlotView slot = _slotViews[i];
                if (slot == null) continue;

                bool highlight = canSwapPlayerSlots && slot.Team == TeamSide.Player;
                slot.SetHighlighted(highlight, SwapHighlightColor);
            }
        }

        private void SwapUnitPositions(BattleState state, UnitState unitA, UnitState unitB)
        {
            FormationPosition posA = unitA.Position;
            FormationPosition posB = unitB.Position;

            unitA.Position = posB;
            unitB.Position = posA;

            state.AppendLog($"Formation Swap: {unitA.Name} ({posA}) <-> {unitB.Name} ({posB})");
            NotifyFormationChanged(state);
        }

        private void NotifyFormationChanged(BattleState state)
        {
            if (state == null) return;
            state.LastPlayerStrategyChangeTurn = -1;
            state.LastEnemyStrategyChangeTurn = -1;
            SquadAI.EvaluateAndUpdateStrategy(state, TeamSide.Player);
            SquadAI.EvaluateAndUpdateStrategy(state, TeamSide.Enemy);
        }

        private static UnitState FindUnitById(BattleState state, string instanceId)
        {
            if (state == null || string.IsNullOrEmpty(instanceId)) return null;

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.InstanceId == instanceId)
                {
                    return u;
                }
            }
            return null;
        }

        private static UnitState FindUnitAtSlot(BattleState state, TeamSide team, FormationPosition pos)
        {
            if (state == null) return null;

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.Team == team && u.IsAlive && u.Position.Equals(pos))
                {
                    return u;
                }
            }

            // Nếu không có unit sống, kiểm tra xem có unit đã gục ở ô đó không để tránh đè vị trí
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.Team == team && u.Position.Equals(pos))
                {
                    return u;
                }
            }
            return null;
        }

        private Sprite ResolveCustomSprite(UnitState unit)
        {
            if (unit == null) return null;

            if ((unit.BossPhase > 0 || unit.InstanceId == "e_boss_warden") && bossWardenSprite != null)
            {
                return bossWardenSprite;
            }

            for (int i = 0; i < roleSprites.Count; i++)
            {
                RoleSpriteEntry entry = roleSprites[i];
                if (entry.Team == unit.Team && entry.Role == unit.Role && entry.Sprite != null)
                {
                    return entry.Sprite;
                }
            }

            return null;
        }
    }
}
