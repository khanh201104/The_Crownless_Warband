using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Roguelite;
using CrownlessWarband.Presentation.BattleView;
using UnityEngine;

namespace CrownlessWarband.Presentation.UI
{
    public class BattleHudView : MonoBehaviour
    {
        private const int MaxAllyCards = 5;
        private const int MaxSkillOrbs = 4;

        private sealed class AllyCardSlot
        {
            public GameObject Root;
            public UnityEngine.UI.Button SelectButton;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image BorderOutline;
            public UnityEngine.UI.Image PortraitBg;
            public UnityEngine.UI.Text NameText;
            public UnityEngine.UI.Text RoleCodeText;
            public UnityEngine.UI.Text StatusBadgeText;
            public GameObject StatusBadgeRoot;
            public RectTransform HpFillRect;
            public RectTransform ShieldFillRect;
            public RectTransform EnergyFillRect;
            public UnityEngine.UI.Text HpValueText;

            public string BoundUnitId;
            public int CachedHp = -1;
            public int CachedMaxHp = -1;
            public int CachedShield = -1;
            public int CachedEnergy = -1;
            public int CachedMaxEnergy = -1;
            public bool CachedAlive = true;
            public int CachedHighlightState = -1; // 0=normal, 1=focused, 2=acting, 3=dead
            public int CachedBadgeState = -1;     // 0=none, 1=stun, 2=prot
            public UnitRole CachedRole = (UnitRole)(-1);
            public string CachedName;
        }

        private sealed class SkillOrbSlot
        {
            public GameObject Root;
            public GameObject ModePillRoot;
            public UnityEngine.UI.Button ModePillButton;
            public UnityEngine.UI.Text ModePillText;
            public GameObject ManualReadyRing;
            public UnityEngine.UI.Button OrbButton;
            public UnityEngine.UI.Text OrbCodeText;
            public UnityEngine.UI.Text OrbCostText;

            public string BoundUnitId;
            public SkillInstance BoundSkill;
            public SkillControlMode CachedMode = (SkillControlMode)(-1);
            public int CachedCost = -1;
            public int CachedCooldown = -1;
            public bool CachedReady = true;
            public bool CachedManualReady;
            public SkillSlotType CachedSlotType = (SkillSlotType)(-1);
            public SkillEffectType CachedEffectType = (SkillEffectType)(-1);
        }

        [SerializeField] private BattleSandboxController sandboxController;
        [SerializeField] private BattlefieldGridView gridView;
        [SerializeField] private CombatEventPresenter eventPresenter;
        [SerializeField] private MainMenuAndSettingsView mainMenuAndSettingsView;
        [SerializeField] private BattleModalsView modalsView;

        private Canvas _hudCanvas;

        // Top-Left Status Badge
        private UnityEngine.UI.Text _statusLine1Text;
        private UnityEngine.UI.Text _statusLine2Text;

        // Top-Center Banner & Boss HP
        private UnityEngine.UI.Image _turnBannerOutline;
        private UnityEngine.UI.Text _turnBannerText;
        private GameObject _bossBarRoot;
        private RectTransform _bossHpFillRect;
        private UnityEngine.UI.Text _bossHpText;

        // Top-Right Controls
        private UnityEngine.UI.Button _speedButton;
        private UnityEngine.UI.Text _speedButtonText;
        private UnityEngine.UI.Button _pauseButton;
        private UnityEngine.UI.Text _pauseButtonText;
        private UnityEngine.UI.Button _stepButton;
        private UnityEngine.UI.Button _settingsButton;

        // Bottom-Left Ally Cards
        private readonly AllyCardSlot[] _allyCards = new AllyCardSlot[MaxAllyCards];
        private readonly List<UnitState> _playerUnitsBuffer = new List<UnitState>(8);

        // Bottom-Right Skill Orbs & Turn Action
        private UnityEngine.UI.Text _focusedAllyLabel;
        private UnityEngine.UI.Button _policyButton;
        private UnityEngine.UI.Text _orbStatusHintText;
        private readonly SkillOrbSlot[] _skillOrbs = new SkillOrbSlot[MaxSkillOrbs];
        private UnityEngine.UI.Button _runTurnButton;
        private UnityEngine.UI.Text _runTurnButtonText;
        private UnityEngine.UI.Image _runTurnOutline;

        // Center Victory & Defeat Modals (separated)
        private GameObject _victoryModalRoot;
        private UnityEngine.UI.Text _victoryTitleText;
        private GameObject _defeatModalRoot;
        private UnityEngine.UI.Button _victoryClaimButton;
        private UnityEngine.UI.Text _victoryClaimButtonText;
        private UnityEngine.UI.Button _endlessButton;
        private UnityEngine.UI.Button _defeatReturnButton;

        // Final Campaign Victory / Defeat Game Over Summary Modal
        private GameObject _finalGameOverModalRoot;
        private UnityEngine.UI.Image _finalGameOverCardOutline;
        private UnityEngine.UI.Text _finalGameOverTitleText;
        private UnityEngine.UI.Text _finalGameOverDescText;
        private UnityEngine.UI.Button _finalPlayAgainButton;
        private UnityEngine.UI.Button _finalMainMenuButton;
        private BattleState _lastObservedBattleState;
        private int _finalEndModalState; // 0 = Không mở, 1 = Modal Chiến Thắng Trò Chơi, 2 = Modal Thua Trò Chơi

        // Selection & Status state
        private string _focusedAllyId;
        private string _lastSeenGridSelectedId;
        private string _transientHint;
        private float _transientHintTimer;

        // Cached values for zero-GC Update()
        private MissionType _cachedMission = (MissionType)(-1);
        private int _cachedStatusTurn = -1;
        private int _cachedSurvivalTarget = -1;
        private int _cachedFloor = -1;
        private BiomeType _cachedBiome = (BiomeType)(-1);
        private WorldModifierType _cachedModifier = (WorldModifierType)(-1);
        private int _cachedRelicCount = -1;
        private SquadStrategyType _cachedPlayerStrat = (SquadStrategyType)(-1);
        private SquadStrategyType _cachedEnemyStrat = (SquadStrategyType)(-1);

        private bool _bannerInitialized;
        private int _cachedBannerTurn = -1;
        private bool _cachedBannerShowOutcome;
        private bool _cachedBannerPaused;
        private bool _cachedBannerInitialPrep;
        private TeamSide? _cachedWinner;

        private bool _cachedBossVisible;
        private int _cachedBossHp = -1;
        private int _cachedBossMaxHp = -1;
        private int _cachedBossPhase = -1;
        private string _cachedBossName;

        private PlaybackSpeedMode _cachedSpeedMode = (PlaybackSpeedMode)(-1);
        private bool _cachedPaused;
        private bool _topControlsInitialized;
        private bool _cachedPauseInteractable = true;
        private bool _cachedStepInteractable;
        private bool _actionBtnInitialized;
        private bool _cachedActionBtnPaused;
        private bool _cachedActionBtnInitialPrep;
        private bool _cachedRunTurnInteractable = true;
        private string _cachedFocusedLabelName;

        private static readonly Color GuardianColor = new Color(0.145f, 0.388f, 0.922f, 1f);
        private static readonly Color VanguardColor = new Color(0.918f, 0.345f, 0.047f, 1f);
        private static readonly Color RangerColor = new Color(0.086f, 0.639f, 0.290f, 1f);
        private static readonly Color MageColor = new Color(0.576f, 0.200f, 0.918f, 1f);
        private static readonly Color MedicColor = new Color(0.051f, 0.580f, 0.533f, 1f);

        private static readonly Color OrbDamageColor = new Color(0.86f, 0.30f, 0.16f, 1f);
        private static readonly Color OrbHealColor = new Color(0.14f, 0.65f, 0.36f, 1f);
        private static readonly Color OrbShieldColor = new Color(0.18f, 0.48f, 0.85f, 1f);
        private static readonly Color OrbStunColor = new Color(0.56f, 0.24f, 0.78f, 1f);
        private static readonly Color OrbMarkColor = new Color(0.76f, 0.54f, 0.12f, 1f);

        private static readonly Color ActionNormalColor = new Color(0.11f, 0.46f, 0.58f, 1f);
        private static readonly Color PauseActiveColor = new Color(0.68f, 0.46f, 0.06f, 1f);

        private void Awake()
        {
            EnsureReferences();
            BuildHudCanvas();
            _hudCanvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            EnsureReferences();

            bool shouldShow = sandboxController != null &&
                              sandboxController.ScreenMode == SandboxScreenMode.ActiveBattle &&
                              !sandboxController.IsDebugMode &&
                              sandboxController.CurrentState != null;

            if (_hudCanvas != null && _hudCanvas.gameObject.activeSelf != shouldShow)
            {
                _hudCanvas.gameObject.SetActive(shouldShow);
            }

            if (!shouldShow)
            {
                if (_finalEndModalState != 0)
                {
                    _finalEndModalState = 0;
                    if (_finalGameOverModalRoot != null)
                    {
                        _finalGameOverModalRoot.SetActive(false);
                    }
                    if (sandboxController != null)
                    {
                        sandboxController.IsFinalGameOverModalOpen = false;
                    }
                }
                return;
            }

            if (_transientHintTimer > 0f)
            {
                _transientHintTimer -= Time.unscaledDeltaTime;
                if (_transientHintTimer <= 0f)
                {
                    _transientHint = null;
                    if (_orbStatusHintText != null)
                    {
                        _orbStatusHintText.text = string.Empty;
                        _orbStatusHintText.gameObject.SetActive(false);
                    }
                }
            }

            BattleState state = sandboxController.CurrentState;
            SyncGridSelectionToHud(state);

            bool isBattleOver = TurnManager.IsBattleOver(state, out TeamSide? winner);
            bool isPlayingEvents = eventPresenter != null && eventPresenter.IsPlayingEvents;

            if (_lastObservedBattleState != state || !isBattleOver)
            {
                _lastObservedBattleState = state;
                if (_finalEndModalState != 0)
                {
                    _finalEndModalState = 0;
                    if (_finalGameOverModalRoot != null)
                    {
                        _finalGameOverModalRoot.SetActive(false);
                    }
                    sandboxController.IsFinalGameOverModalOpen = false;
                }
            }

            UpdateTopLeftStatus(state);
            UpdateTopCenterBannerAndBoss(state, isBattleOver, winner, isPlayingEvents);
            UpdateTopRightControls(isBattleOver, isPlayingEvents);
            UpdateBottomLeftAllyCards(state);
            UnitState focusedAlly = ResolveFocusedAlly(state);
            UpdateBottomRightSkillOrbs(state, focusedAlly, isBattleOver, isPlayingEvents);
            UpdateEndBattleOverlay(isBattleOver, winner, isPlayingEvents);
        }

        private void OnDestroy()
        {
            UnbindButton(_speedButton);
            UnbindButton(_pauseButton);
            UnbindButton(_stepButton);
            UnbindButton(_settingsButton);
            UnbindButton(_policyButton);
            UnbindButton(_runTurnButton);
            UnbindButton(_victoryClaimButton);
            UnbindButton(_endlessButton);
            UnbindButton(_defeatReturnButton);
            UnbindButton(_finalPlayAgainButton);
            UnbindButton(_finalMainMenuButton);

            for (int i = 0; i < _allyCards.Length; i++)
            {
                if (_allyCards[i] != null)
                {
                    UnbindButton(_allyCards[i].SelectButton);
                }
            }

            for (int i = 0; i < _skillOrbs.Length; i++)
            {
                if (_skillOrbs[i] != null)
                {
                    UnbindButton(_skillOrbs[i].ModePillButton);
                    UnbindButton(_skillOrbs[i].OrbButton);
                }
            }

            if (_hudCanvas != null)
            {
                Destroy(_hudCanvas.gameObject);
                _hudCanvas = null;
            }
        }

        private void EnsureReferences()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }
            if (gridView == null)
            {
                gridView = FindFirstObjectByType<BattlefieldGridView>();
            }
            if (eventPresenter == null)
            {
                eventPresenter = FindFirstObjectByType<CombatEventPresenter>();
            }
            if (mainMenuAndSettingsView == null)
            {
                mainMenuAndSettingsView = FindFirstObjectByType<MainMenuAndSettingsView>();
            }
            if (modalsView == null)
            {
                modalsView = FindFirstObjectByType<BattleModalsView>();
            }
        }

        private void SyncGridSelectionToHud(BattleState state)
        {
            if (gridView == null) return;

            string gridSelected = gridView.SelectedUnitId;
            if (gridSelected == _lastSeenGridSelectedId) return;

            _lastSeenGridSelectedId = gridSelected;
            if (string.IsNullOrEmpty(gridSelected)) return;

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.InstanceId == gridSelected && u.Team == TeamSide.Player && u.IsAlive)
                {
                    _focusedAllyId = gridSelected;
                    break;
                }
            }
        }

        private void BuildHudCanvas()
        {
            _hudCanvas = UguiThemeFactory.CreateOverlayCanvas("BattleHudCanvas", 10);
            RectTransform root = _hudCanvas.GetComponent<RectTransform>();

            BuildTopLeftStatus(root);
            BuildTopCenterBanner(root);
            BuildTopRightControls(root);
            BuildBottomLeftAllyCards(root);
            BuildBottomRightSkillOrbs(root);
            BuildEndBattleOverlay(root);
        }

        private void BuildTopLeftStatus(RectTransform root)
        {
            UnityEngine.UI.Image badgeBg = UguiThemeFactory.CreatePanel(
                "TopLeftStatusBadge",
                root,
                UguiThemeFactory.PanelBgSoft,
                true);
            badgeBg.raycastTarget = true;

            RectTransform rt = badgeBg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(430f, 82f);
            rt.anchoredPosition = new Vector2(18f, -14f);

            UguiThemeFactory.CreateOutlineFrame("Outline", rt, new Color(0.13f, 0.83f, 0.93f, 0.55f));

            _statusLine1Text = UguiThemeFactory.CreateText(
                "StatusLine1",
                rt,
                string.Empty,
                15,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _statusLine1Text.resizeTextForBestFit = true;
            _statusLine1Text.resizeTextMinSize = 10;
            _statusLine1Text.resizeTextMaxSize = 15;
            _statusLine1Text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform line1Rt = _statusLine1Text.rectTransform;
            line1Rt.anchorMin = new Vector2(0f, 0.5f);
            line1Rt.anchorMax = new Vector2(1f, 1f);
            line1Rt.offsetMin = new Vector2(14f, 0f);
            line1Rt.offsetMax = new Vector2(-12f, -6f);

            _statusLine2Text = UguiThemeFactory.CreateText(
                "StatusLine2",
                rt,
                string.Empty,
                13,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _statusLine2Text.resizeTextForBestFit = true;
            _statusLine2Text.resizeTextMinSize = 10;
            _statusLine2Text.resizeTextMaxSize = 13;
            _statusLine2Text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform line2Rt = _statusLine2Text.rectTransform;
            line2Rt.anchorMin = new Vector2(0f, 0f);
            line2Rt.anchorMax = new Vector2(1f, 0.5f);
            line2Rt.offsetMin = new Vector2(14f, 6f);
            line2Rt.offsetMax = new Vector2(-12f, 0f);
        }

        private void BuildTopCenterBanner(RectTransform root)
        {
            UnityEngine.UI.Image bannerBg = UguiThemeFactory.CreatePanel(
                "TopCenterTurnBanner",
                root,
                UguiThemeFactory.PanelBg,
                true);
            bannerBg.raycastTarget = true;

            RectTransform bannerRt = bannerBg.rectTransform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(460f, 48f);
            bannerRt.anchoredPosition = new Vector2(0f, -12f);

            _turnBannerOutline = UguiThemeFactory.CreateOutlineFrame("Outline", bannerRt, UguiThemeFactory.AccentGold);

            _turnBannerText = UguiThemeFactory.CreateText(
                "TurnBannerText",
                bannerRt,
                string.Empty,
                20,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _turnBannerText.resizeTextForBestFit = true;
            _turnBannerText.resizeTextMinSize = 13;
            _turnBannerText.resizeTextMaxSize = 20;
            _turnBannerText.verticalOverflow = VerticalWrapMode.Truncate;
            StretchFull(_turnBannerText.rectTransform, 10f, 4f);

            // Boss HP Bar right below banner
            UnityEngine.UI.Image bossBg = UguiThemeFactory.CreatePanel(
                "BossHpBar",
                root,
                UguiThemeFactory.PanelBg,
                true);
            bossBg.raycastTarget = true;
            _bossBarRoot = bossBg.gameObject;

            RectTransform bossRt = bossBg.rectTransform;
            bossRt.anchorMin = new Vector2(0.5f, 1f);
            bossRt.anchorMax = new Vector2(0.5f, 1f);
            bossRt.pivot = new Vector2(0.5f, 1f);
            bossRt.sizeDelta = new Vector2(420f, 32f);
            bossRt.anchoredPosition = new Vector2(0f, -66f);

            UguiThemeFactory.CreateOutlineFrame("Outline", bossRt, UguiThemeFactory.EnemyCrimson);

            RectTransform trackRt = UguiThemeFactory.CreateRect("HpTrack", bossRt);
            StretchFull(trackRt, 4f, 4f);

            UnityEngine.UI.Image fillImg = UguiThemeFactory.CreatePanel("HpFill", trackRt, UguiThemeFactory.EnemyCrimson, false);
            fillImg.raycastTarget = false;
            _bossHpFillRect = fillImg.rectTransform;
            _bossHpFillRect.anchorMin = Vector2.zero;
            _bossHpFillRect.anchorMax = Vector2.one;
            _bossHpFillRect.offsetMin = Vector2.zero;
            _bossHpFillRect.offsetMax = Vector2.zero;

            // Phase threshold tick lines at 65% & 35%
            CreateThresholdMarker(trackRt, "Threshold35", 0.35f);
            CreateThresholdMarker(trackRt, "Threshold65", 0.65f);

            _bossHpText = UguiThemeFactory.CreateText(
                "BossHpLabel",
                bossRt,
                string.Empty,
                13,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _bossHpText.resizeTextForBestFit = true;
            _bossHpText.resizeTextMinSize = 10;
            _bossHpText.resizeTextMaxSize = 13;
            _bossHpText.verticalOverflow = VerticalWrapMode.Truncate;
            StretchFull(_bossHpText.rectTransform, 6f, 2f);

            _bossBarRoot.SetActive(false);
        }

        private void BuildTopRightControls(RectTransform root)
        {
            RectTransform rowRt = UguiThemeFactory.CreateRect("TopRightControls", root);
            rowRt.anchorMin = new Vector2(1f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(1f, 1f);
            rowRt.sizeDelta = new Vector2(280f, 44f);
            rowRt.anchoredPosition = new Vector2(-18f, -14f);

            UnityEngine.UI.HorizontalLayoutGroup layout = rowRt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            _speedButton = CreateControlButton("SpeedButton", rowRt, "1x", 62f, 14);
            _speedButtonText = _speedButton.GetComponentInChildren<UnityEngine.UI.Text>();
            _speedButton.onClick.AddListener(OnClickCycleSpeed);

            _pauseButton = CreateControlButton("PauseButton", rowRt, "PAUSE", 72f, 13);
            _pauseButtonText = _pauseButton.GetComponentInChildren<UnityEngine.UI.Text>();
            _pauseButton.onClick.AddListener(OnClickTogglePause);

            _stepButton = CreateControlButton("StepButton", rowRt, "STEP", 60f, 13);
            _stepButton.onClick.AddListener(OnClickStepEvent);

            _settingsButton = CreateControlButton("SettingsButton", rowRt, "SET", 56f, 13);
            _settingsButton.onClick.AddListener(OnClickOpenSettings);
        }

        private void BuildBottomLeftAllyCards(RectTransform root)
        {
            RectTransform containerRt = UguiThemeFactory.CreateRect("BottomLeftAllyCards", root);
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.sizeDelta = new Vector2(MaxAllyCards * 88f + (MaxAllyCards - 1) * 10f, 126f);
            containerRt.anchoredPosition = new Vector2(18f, 16f);

            for (int i = 0; i < MaxAllyCards; i++)
            {
                int capturedIndex = i;
                AllyCardSlot slot = new AllyCardSlot();

                UnityEngine.UI.Button btn = UguiThemeFactory.CreateButton(
                    $"AllyCard_{i}",
                    containerRt,
                    string.Empty,
                    UguiThemeFactory.PanelBg,
                    12);
                RectTransform cardRt = btn.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0f, 0f);
                cardRt.anchorMax = new Vector2(0f, 0f);
                cardRt.pivot = new Vector2(0f, 0f);
                cardRt.sizeDelta = new Vector2(88f, 126f);
                cardRt.anchoredPosition = new Vector2(i * 98f, 0f);

                slot.Root = btn.gameObject;
                slot.SelectButton = btn;
                slot.Background = btn.GetComponent<UnityEngine.UI.Image>();
                slot.BorderOutline = UguiThemeFactory.CreateOutlineFrame(
                    "BorderOutline",
                    cardRt,
                    new Color(0.30f, 0.36f, 0.46f, 0.75f));

                slot.NameText = UguiThemeFactory.CreateText(
                    "UnitName",
                    cardRt,
                    string.Empty,
                    11,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                slot.NameText.resizeTextForBestFit = true;
                slot.NameText.resizeTextMinSize = 8;
                slot.NameText.resizeTextMaxSize = 11;
                slot.NameText.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform nameRt = slot.NameText.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 1f);
                nameRt.anchorMax = new Vector2(1f, 1f);
                nameRt.pivot = new Vector2(0.5f, 1f);
                nameRt.offsetMin = new Vector2(4f, -22f);
                nameRt.offsetMax = new Vector2(-4f, -4f);

                slot.PortraitBg = UguiThemeFactory.CreatePanel("PortraitBox", cardRt, GuardianColor, true);
                slot.PortraitBg.raycastTarget = false;
                RectTransform portRt = slot.PortraitBg.rectTransform;
                portRt.anchorMin = new Vector2(0.5f, 1f);
                portRt.anchorMax = new Vector2(0.5f, 1f);
                portRt.pivot = new Vector2(0.5f, 1f);
                portRt.sizeDelta = new Vector2(56f, 44f);
                portRt.anchoredPosition = new Vector2(0f, -25f);

                slot.RoleCodeText = UguiThemeFactory.CreateText(
                    "RoleCode",
                    portRt,
                    "GRD",
                    16,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                StretchFull(slot.RoleCodeText.rectTransform, 2f, 2f);

                UnityEngine.UI.Image badgeBg = UguiThemeFactory.CreatePanel(
                    "StatusBadge",
                    portRt,
                    new Color(0.05f, 0.08f, 0.12f, 0.9f),
                    true);
                badgeBg.raycastTarget = false;
                RectTransform badgeRt = badgeBg.rectTransform;
                badgeRt.anchorMin = new Vector2(0f, 1f);
                badgeRt.anchorMax = new Vector2(1f, 1f);
                badgeRt.pivot = new Vector2(0.5f, 1f);
                badgeRt.sizeDelta = new Vector2(0f, 14f);
                badgeRt.anchoredPosition = new Vector2(0f, 1f);
                slot.StatusBadgeRoot = badgeBg.gameObject;

                slot.StatusBadgeText = UguiThemeFactory.CreateText(
                    "BadgeText",
                    badgeRt,
                    string.Empty,
                    10,
                    UguiThemeFactory.AccentGold,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                StretchFull(slot.StatusBadgeText.rectTransform, 1f, 0f);
                slot.StatusBadgeRoot.SetActive(false);

                // HP Track + Fill + Shield Overlay
                UnityEngine.UI.Image hpTrack = UguiThemeFactory.CreatePanel(
                    "HpTrack",
                    cardRt,
                    new Color(0.06f, 0.08f, 0.12f, 1f),
                    false);
                hpTrack.raycastTarget = false;
                RectTransform hpTrackRt = hpTrack.rectTransform;
                hpTrackRt.anchorMin = new Vector2(0f, 0f);
                hpTrackRt.anchorMax = new Vector2(1f, 0f);
                hpTrackRt.pivot = new Vector2(0.5f, 0f);
                hpTrackRt.offsetMin = new Vector2(8f, 36f);
                hpTrackRt.offsetMax = new Vector2(-8f, 46f);

                UnityEngine.UI.Image hpFill = UguiThemeFactory.CreatePanel("HpFill", hpTrackRt, UguiThemeFactory.HpGreen, false);
                hpFill.raycastTarget = false;
                slot.HpFillRect = hpFill.rectTransform;
                slot.HpFillRect.anchorMin = Vector2.zero;
                slot.HpFillRect.anchorMax = Vector2.one;
                slot.HpFillRect.offsetMin = Vector2.zero;
                slot.HpFillRect.offsetMax = Vector2.zero;

                UnityEngine.UI.Image shieldFill = UguiThemeFactory.CreatePanel("ShieldFill", hpTrackRt, UguiThemeFactory.ShieldBlue, false);
                shieldFill.raycastTarget = false;
                slot.ShieldFillRect = shieldFill.rectTransform;
                slot.ShieldFillRect.anchorMin = new Vector2(0f, 0f);
                slot.ShieldFillRect.anchorMax = new Vector2(0f, 0.45f);
                slot.ShieldFillRect.offsetMin = Vector2.zero;
                slot.ShieldFillRect.offsetMax = Vector2.zero;

                // Energy Track + Fill
                UnityEngine.UI.Image energyTrack = UguiThemeFactory.CreatePanel(
                    "EnergyTrack",
                    cardRt,
                    new Color(0.06f, 0.08f, 0.12f, 1f),
                    false);
                energyTrack.raycastTarget = false;
                RectTransform energyTrackRt = energyTrack.rectTransform;
                energyTrackRt.anchorMin = new Vector2(0f, 0f);
                energyTrackRt.anchorMax = new Vector2(1f, 0f);
                energyTrackRt.pivot = new Vector2(0.5f, 0f);
                energyTrackRt.offsetMin = new Vector2(8f, 26f);
                energyTrackRt.offsetMax = new Vector2(-8f, 32f);

                UnityEngine.UI.Image energyFill = UguiThemeFactory.CreatePanel("EnergyFill", energyTrackRt, UguiThemeFactory.EnergyGold, false);
                energyFill.raycastTarget = false;
                slot.EnergyFillRect = energyFill.rectTransform;
                slot.EnergyFillRect.anchorMin = Vector2.zero;
                slot.EnergyFillRect.anchorMax = Vector2.one;
                slot.EnergyFillRect.offsetMin = Vector2.zero;
                slot.EnergyFillRect.offsetMax = Vector2.zero;

                slot.HpValueText = UguiThemeFactory.CreateText(
                    "HpValueText",
                    cardRt,
                    string.Empty,
                    11,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                slot.HpValueText.resizeTextForBestFit = true;
                slot.HpValueText.resizeTextMinSize = 8;
                slot.HpValueText.resizeTextMaxSize = 11;
                slot.HpValueText.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform hpTxtRt = slot.HpValueText.rectTransform;
                hpTxtRt.anchorMin = new Vector2(0f, 0f);
                hpTxtRt.anchorMax = new Vector2(1f, 0f);
                hpTxtRt.pivot = new Vector2(0.5f, 0f);
                hpTxtRt.offsetMin = new Vector2(4f, 4f);
                hpTxtRt.offsetMax = new Vector2(-4f, 24f);

                btn.onClick.AddListener(() => OnClickAllyCard(capturedIndex));
                slot.Root.SetActive(false);
                _allyCards[i] = slot;
            }
        }

        private void BuildBottomRightSkillOrbs(RectTransform root)
        {
            RectTransform clusterRt = UguiThemeFactory.CreateRect("BottomRightSkillCluster", root);
            clusterRt.anchorMin = new Vector2(1f, 0f);
            clusterRt.anchorMax = new Vector2(1f, 0f);
            clusterRt.pivot = new Vector2(1f, 0f);
            clusterRt.sizeDelta = new Vector2(470f, 134f);
            clusterRt.anchoredPosition = new Vector2(-18f, 16f);

            _focusedAllyLabel = UguiThemeFactory.CreateText(
                "FocusedAllyLabel",
                clusterRt,
                string.Empty,
                14,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _focusedAllyLabel.resizeTextForBestFit = true;
            _focusedAllyLabel.resizeTextMinSize = 10;
            _focusedAllyLabel.resizeTextMaxSize = 14;
            _focusedAllyLabel.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform focusRt = _focusedAllyLabel.rectTransform;
            focusRt.anchorMin = new Vector2(0f, 1f);
            focusRt.anchorMax = new Vector2(0f, 1f);
            focusRt.pivot = new Vector2(0f, 1f);
            focusRt.sizeDelta = new Vector2(190f, 24f);
            focusRt.anchoredPosition = new Vector2(6f, 0f);

            _policyButton = UguiThemeFactory.CreateButton(
                "PolicyButton",
                clusterRt,
                "POLICY",
                UguiThemeFactory.PanelBgSoft,
                11);
            UguiThemeFactory.CreateOutlineFrame("Outline", _policyButton.transform, UguiThemeFactory.AllyCyan);
            RectTransform policyRt = _policyButton.GetComponent<RectTransform>();
            policyRt.anchorMin = new Vector2(0f, 1f);
            policyRt.anchorMax = new Vector2(0f, 1f);
            policyRt.pivot = new Vector2(0f, 1f);
            policyRt.sizeDelta = new Vector2(76f, 22f);
            policyRt.anchoredPosition = new Vector2(202f, -1f);
            _policyButton.onClick.AddListener(OnClickOpenPolicy);

            _orbStatusHintText = UguiThemeFactory.CreateText(
                "OrbHintText",
                clusterRt,
                string.Empty,
                12,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            RectTransform hintRt = _orbStatusHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.pivot = new Vector2(1f, 0f);
            hintRt.offsetMin = new Vector2(0f, 4f);
            hintRt.offsetMax = new Vector2(-4f, 26f);
            _orbStatusHintText.gameObject.SetActive(false);

            // 4 Skill Orbs on the left side of Bottom-Right cluster
            for (int i = 0; i < MaxSkillOrbs; i++)
            {
                int capturedIndex = i;
                SkillOrbSlot orbSlot = new SkillOrbSlot();

                RectTransform slotRt = UguiThemeFactory.CreateRect($"SkillOrbSlot_{i}", clusterRt);
                slotRt.anchorMin = new Vector2(0f, 0f);
                slotRt.anchorMax = new Vector2(0f, 0f);
                slotRt.pivot = new Vector2(0f, 0f);
                slotRt.sizeDelta = new Vector2(68f, 98f);
                slotRt.anchoredPosition = new Vector2(i * 80f, 0f);
                orbSlot.Root = slotRt.gameObject;

                // Mode Pill above Orb (68 x 22)
                UnityEngine.UI.Button pillBtn = UguiThemeFactory.CreateButton(
                    "ModePill",
                    slotRt,
                    "AUTO",
                    UguiThemeFactory.PanelBgSoft,
                    11);
                UguiThemeFactory.CreateOutlineFrame("Outline", pillBtn.transform, new Color(1f, 1f, 1f, 0.25f));
                RectTransform pillRt = pillBtn.GetComponent<RectTransform>();
                pillRt.anchorMin = new Vector2(0.5f, 1f);
                pillRt.anchorMax = new Vector2(0.5f, 1f);
                pillRt.pivot = new Vector2(0.5f, 1f);
                pillRt.sizeDelta = new Vector2(68f, 22f);
                pillRt.anchoredPosition = Vector2.zero;

                orbSlot.ModePillRoot = pillBtn.gameObject;
                orbSlot.ModePillButton = pillBtn;
                orbSlot.ModePillText = pillBtn.GetComponentInChildren<UnityEngine.UI.Text>();
                pillBtn.onClick.AddListener(() => OnClickSkillModePill(capturedIndex));

                // Circular Manual Ready Glow Ring behind Orb (76 x 76)
                RectTransform readyRingRt = UguiThemeFactory.CreateRect("ManualReadyRing", slotRt);
                readyRingRt.anchorMin = new Vector2(0.5f, 0f);
                readyRingRt.anchorMax = new Vector2(0.5f, 0f);
                readyRingRt.pivot = new Vector2(0.5f, 0f);
                readyRingRt.sizeDelta = new Vector2(76f, 76f);
                readyRingRt.anchoredPosition = new Vector2(0f, -4f);
                UnityEngine.UI.Image readyRingImg = readyRingRt.gameObject.AddComponent<UnityEngine.UI.Image>();
                readyRingImg.sprite = UguiThemeFactory.GetCircleSprite();
                readyRingImg.color = UguiThemeFactory.AccentGold;
                readyRingImg.raycastTarget = false;
                orbSlot.ManualReadyRing = readyRingRt.gameObject;
                orbSlot.ManualReadyRing.SetActive(false);

                // Circular Orb Button (68 x 68)
                UnityEngine.UI.Button orbBtn = UguiThemeFactory.CreateButton(
                    "SkillOrb",
                    slotRt,
                    string.Empty,
                    OrbDamageColor,
                    14,
                    true);
                RectTransform orbRt = orbBtn.GetComponent<RectTransform>();
                orbRt.anchorMin = new Vector2(0.5f, 0f);
                orbRt.anchorMax = new Vector2(0.5f, 0f);
                orbRt.pivot = new Vector2(0.5f, 0f);
                orbRt.sizeDelta = new Vector2(68f, 68f);
                orbRt.anchoredPosition = Vector2.zero;

                orbSlot.OrbButton = orbBtn;

                // Two crisp texts inside Orb (skill code & cost/CD)
                UnityEngine.UI.Text defaultLabel = orbBtn.GetComponentInChildren<UnityEngine.UI.Text>();
                if (defaultLabel != null)
                {
                    defaultLabel.gameObject.SetActive(false);
                }

                orbSlot.OrbCodeText = UguiThemeFactory.CreateText(
                    "CodeText",
                    orbRt,
                    "ATK",
                    14,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                RectTransform codeRt = orbSlot.OrbCodeText.rectTransform;
                codeRt.anchorMin = new Vector2(0f, 0.42f);
                codeRt.anchorMax = new Vector2(1f, 0.92f);
                codeRt.offsetMin = Vector2.zero;
                codeRt.offsetMax = Vector2.zero;

                orbSlot.OrbCostText = UguiThemeFactory.CreateText(
                    "CostText",
                    orbRt,
                    "E:0",
                    12,
                    UguiThemeFactory.EnergyGold,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                RectTransform costRt = orbSlot.OrbCostText.rectTransform;
                costRt.anchorMin = new Vector2(0f, 0.08f);
                costRt.anchorMax = new Vector2(1f, 0.46f);
                costRt.offsetMin = Vector2.zero;
                costRt.offsetMax = Vector2.zero;

                orbBtn.onClick.AddListener(() => OnClickSkillOrb(capturedIndex));
                orbSlot.Root.SetActive(false);
                _skillOrbs[i] = orbSlot;
            }

            // Main Action Button on the far right (132 x 76): Pause / Resume [SPACE]
            _runTurnButton = UguiThemeFactory.CreateButton(
                "RunTurnButton",
                clusterRt,
                "TẠM DỪNG\n[SPACE]",
                ActionNormalColor,
                14);
            _runTurnOutline = UguiThemeFactory.CreateOutlineFrame("Outline", _runTurnButton.transform, UguiThemeFactory.AllyCyan);
            _runTurnButtonText = _runTurnButton.GetComponentInChildren<UnityEngine.UI.Text>();
            RectTransform runRt = _runTurnButton.GetComponent<RectTransform>();
            runRt.anchorMin = new Vector2(1f, 0f);
            runRt.anchorMax = new Vector2(1f, 0f);
            runRt.pivot = new Vector2(1f, 0f);
            runRt.sizeDelta = new Vector2(132f, 76f);
            runRt.anchoredPosition = Vector2.zero;
            _runTurnButton.onClick.AddListener(OnClickRunTurn);
        }

        private void BuildEndBattleOverlay(RectTransform root)
        {
            // Khối 1: Victory Modal Overlay (540 x 142)
            UnityEngine.UI.Image victoryPanel = UguiThemeFactory.CreatePanel(
                "VictoryModalOverlay",
                root,
                UguiThemeFactory.PanelBg,
                true);
            victoryPanel.raycastTarget = true;
            _victoryModalRoot = victoryPanel.gameObject;

            RectTransform vRt = victoryPanel.rectTransform;
            vRt.anchorMin = new Vector2(0.5f, 1f);
            vRt.anchorMax = new Vector2(0.5f, 1f);
            vRt.pivot = new Vector2(0.5f, 1f);
            vRt.sizeDelta = new Vector2(540f, 142f);
            vRt.anchoredPosition = new Vector2(0f, -108f);
            UguiThemeFactory.CreateOutlineFrame("Outline", vRt, UguiThemeFactory.AccentGold);

            _victoryTitleText = UguiThemeFactory.CreateText(
                "VictoryTitle",
                vRt,
                "CHIẾN THẮNG TRẬN ĐẤU!",
                20,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            RectTransform vTitleRt = _victoryTitleText.rectTransform;
            vTitleRt.anchorMin = new Vector2(0f, 1f);
            vTitleRt.anchorMax = new Vector2(1f, 1f);
            vTitleRt.pivot = new Vector2(0.5f, 1f);
            vTitleRt.offsetMin = new Vector2(16f, -46f);
            vTitleRt.offsetMax = new Vector2(-16f, -12f);

            RectTransform vBtnRow = UguiThemeFactory.CreateRect("ButtonRow", vRt);
            vBtnRow.anchorMin = new Vector2(0f, 0f);
            vBtnRow.anchorMax = new Vector2(1f, 0f);
            vBtnRow.pivot = new Vector2(0.5f, 0f);
            vBtnRow.offsetMin = new Vector2(18f, 18f);
            vBtnRow.offsetMax = new Vector2(-18f, 70f);

            UnityEngine.UI.HorizontalLayoutGroup vLayout = vBtnRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            vLayout.spacing = 12f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = true;

            _victoryClaimButton = UguiThemeFactory.CreateButton(
                "ClaimVictoryButton",
                vBtnRow,
                "CHIẾN THẮNG! NHẬN THƯỞNG ->",
                new Color(0.14f, 0.52f, 0.34f, 1f),
                14);
            _victoryClaimButtonText = _victoryClaimButton.GetComponentInChildren<UnityEngine.UI.Text>();
            UguiThemeFactory.CreateOutlineFrame("Outline", _victoryClaimButton.transform, UguiThemeFactory.HpGreen);
            _victoryClaimButton.onClick.AddListener(OnClickClaimVictory);

            _endlessButton = UguiThemeFactory.CreateButton(
                "EnterEndlessButton",
                vBtnRow,
                "VÀO ENDLESS MODE (FLOOR 5+) ->",
                new Color(0.66f, 0.42f, 0.08f, 1f),
                13);
            UguiThemeFactory.CreateOutlineFrame("Outline", _endlessButton.transform, UguiThemeFactory.AccentGold);
            _endlessButton.onClick.AddListener(OnClickEnterEndless);
            _victoryModalRoot.SetActive(false);

            // Khối 2: Defeat Modal Overlay (460 x 142) — nút kết thúc trò chơi
            UnityEngine.UI.Image defeatPanel = UguiThemeFactory.CreatePanel(
                "DefeatModalOverlay",
                root,
                UguiThemeFactory.PanelBg,
                true);
            defeatPanel.raycastTarget = true;
            _defeatModalRoot = defeatPanel.gameObject;

            RectTransform dRt = defeatPanel.rectTransform;
            dRt.anchorMin = new Vector2(0.5f, 1f);
            dRt.anchorMax = new Vector2(0.5f, 1f);
            dRt.pivot = new Vector2(0.5f, 1f);
            dRt.sizeDelta = new Vector2(460f, 142f);
            dRt.anchoredPosition = new Vector2(0f, -108f);
            UguiThemeFactory.CreateOutlineFrame("Outline", dRt, UguiThemeFactory.EnemyCrimson);

            UnityEngine.UI.Text defeatTitle = UguiThemeFactory.CreateText(
                "DefeatTitle",
                dRt,
                "THẤT BẠI — ĐỘI HÌNH GỤC NGÃ",
                20,
                UguiThemeFactory.EnemyCrimson,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            RectTransform dTitleRt = defeatTitle.rectTransform;
            dTitleRt.anchorMin = new Vector2(0f, 1f);
            dTitleRt.anchorMax = new Vector2(1f, 1f);
            dTitleRt.pivot = new Vector2(0.5f, 1f);
            dTitleRt.offsetMin = new Vector2(16f, -46f);
            dTitleRt.offsetMax = new Vector2(-16f, -12f);

            RectTransform dBtnRow = UguiThemeFactory.CreateRect("ButtonRow", dRt);
            dBtnRow.anchorMin = new Vector2(0f, 0f);
            dBtnRow.anchorMax = new Vector2(1f, 0f);
            dBtnRow.pivot = new Vector2(0.5f, 0f);
            dBtnRow.offsetMin = new Vector2(28f, 18f);
            dBtnRow.offsetMax = new Vector2(-28f, 70f);

            UnityEngine.UI.HorizontalLayoutGroup dLayout = dBtnRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            dLayout.spacing = 12f;
            dLayout.childAlignment = TextAnchor.MiddleCenter;
            dLayout.childControlWidth = true;
            dLayout.childControlHeight = true;
            dLayout.childForceExpandWidth = true;
            dLayout.childForceExpandHeight = true;

            _defeatReturnButton = UguiThemeFactory.CreateButton(
                "DefeatReturnButton",
                dBtnRow,
                "KẾT THÚC TRÒ CHƠI",
                new Color(0.56f, 0.18f, 0.20f, 1f),
                14);
            UguiThemeFactory.CreateOutlineFrame("Outline", _defeatReturnButton.transform, UguiThemeFactory.EnemyCrimson);
            _defeatReturnButton.onClick.AddListener(OnClickReturnOnDefeat);
            _defeatModalRoot.SetActive(false);

            // Khối 3: Final Campaign Victory / Defeat Game Over Summary Modal (Bước 2)
            RectTransform finalOverlayRt = UguiThemeFactory.CreateRect("FinalGameOverModalOverlay", root);
            StretchFull(finalOverlayRt, 0f, 0f);
            _finalGameOverModalRoot = finalOverlayRt.gameObject;

            UnityEngine.UI.Image dimmer = UguiThemeFactory.CreatePanel(
                "DimmerBackdrop",
                finalOverlayRt,
                new Color(0f, 0f, 0f, 0.78f),
                false);
            dimmer.raycastTarget = true;
            StretchFull(dimmer.rectTransform, 0f, 0f);

            UnityEngine.UI.Image finalCard = UguiThemeFactory.CreatePanel(
                "FinalSummaryCard",
                finalOverlayRt,
                UguiThemeFactory.PanelBg,
                true);
            finalCard.raycastTarget = true;
            RectTransform fRt = finalCard.rectTransform;
            fRt.anchorMin = new Vector2(0.5f, 0.5f);
            fRt.anchorMax = new Vector2(0.5f, 0.5f);
            fRt.pivot = new Vector2(0.5f, 0.5f);
            fRt.sizeDelta = new Vector2(580f, 300f);
            fRt.anchoredPosition = Vector2.zero;
            _finalGameOverCardOutline = UguiThemeFactory.CreateOutlineFrame("Outline", fRt, UguiThemeFactory.AccentGold);

            _finalGameOverTitleText = UguiThemeFactory.CreateText(
                "FinalGameOverTitle",
                fRt,
                "CHIẾN THẮNG TRÒ CHƠI!",
                24,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            RectTransform fTitleRt = _finalGameOverTitleText.rectTransform;
            fTitleRt.anchorMin = new Vector2(0f, 1f);
            fTitleRt.anchorMax = new Vector2(1f, 1f);
            fTitleRt.pivot = new Vector2(0.5f, 1f);
            fTitleRt.offsetMin = new Vector2(20f, -64f);
            fTitleRt.offsetMax = new Vector2(-20f, -18f);

            _finalGameOverDescText = UguiThemeFactory.CreateText(
                "FinalGameOverDesc",
                fRt,
                string.Empty,
                15,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            RectTransform fDescRt = _finalGameOverDescText.rectTransform;
            fDescRt.anchorMin = new Vector2(0f, 0f);
            fDescRt.anchorMax = new Vector2(1f, 1f);
            fDescRt.offsetMin = new Vector2(28f, 90f);
            fDescRt.offsetMax = new Vector2(-28f, -72f);

            RectTransform fBtnRow = UguiThemeFactory.CreateRect("ButtonRow", fRt);
            fBtnRow.anchorMin = new Vector2(0f, 0f);
            fBtnRow.anchorMax = new Vector2(1f, 0f);
            fBtnRow.pivot = new Vector2(0.5f, 0f);
            fBtnRow.offsetMin = new Vector2(28f, 22f);
            fBtnRow.offsetMax = new Vector2(-28f, 74f);

            UnityEngine.UI.HorizontalLayoutGroup fLayout = fBtnRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            fLayout.spacing = 16f;
            fLayout.childAlignment = TextAnchor.MiddleCenter;
            fLayout.childControlWidth = true;
            fLayout.childControlHeight = true;
            fLayout.childForceExpandWidth = true;
            fLayout.childForceExpandHeight = true;

            _finalPlayAgainButton = UguiThemeFactory.CreateButton(
                "FinalPlayAgainButton",
                fBtnRow,
                "CHƠI LẠI",
                new Color(0.14f, 0.52f, 0.34f, 1f),
                15);
            UguiThemeFactory.CreateOutlineFrame("Outline", _finalPlayAgainButton.transform, UguiThemeFactory.HpGreen);
            _finalPlayAgainButton.onClick.AddListener(OnClickFinalPlayAgain);

            _finalMainMenuButton = UguiThemeFactory.CreateButton(
                "FinalMainMenuButton",
                fBtnRow,
                "VỀ MAIN MENU",
                new Color(0.16f, 0.24f, 0.36f, 1f),
                15);
            UguiThemeFactory.CreateOutlineFrame("Outline", _finalMainMenuButton.transform, UguiThemeFactory.AllyCyan);
            _finalMainMenuButton.onClick.AddListener(OnClickFinalReturnMainMenu);
            _finalGameOverModalRoot.SetActive(false);
        }

        private void UpdateTopLeftStatus(BattleState state)
        {
            RunState run = sandboxController.CurrentRun;
            int floor = run != null ? run.CurrentFloor : 1;
            int relicCount = state.ActiveRelics != null ? state.ActiveRelics.Count : 0;

            bool line1Dirty = _cachedMission != state.Mission ||
                              _cachedFloor != floor ||
                              _cachedBiome != state.ActiveBiome ||
                              (state.Mission == MissionType.Survival &&
                               (_cachedStatusTurn != state.CurrentTurn || _cachedSurvivalTarget != state.SurvivalTargetTurns));

            if (line1Dirty && _statusLine1Text != null)
            {
                _cachedMission = state.Mission;
                _cachedFloor = floor;
                _cachedBiome = state.ActiveBiome;
                _cachedStatusTurn = state.CurrentTurn;
                _cachedSurvivalTarget = state.SurvivalTargetTurns;

                string missionLabel;
                switch (state.Mission)
                {
                    case MissionType.Survival:
                        missionLabel = $"SURVIVAL {state.CurrentTurn}/{state.SurvivalTargetTurns}";
                        break;
                    case MissionType.Elimination:
                        missionLabel = "ELIMINATION VIP";
                        break;
                    default:
                        missionLabel = "ASSAULT";
                        break;
                }

                _statusLine1Text.text = $"{missionLabel}  |  Floor {floor}  •  {state.ActiveBiome}";
            }

            bool line2Dirty = _cachedModifier != state.ActiveModifier ||
                              _cachedRelicCount != relicCount ||
                              _cachedPlayerStrat != state.PlayerSquadStrategy ||
                              _cachedEnemyStrat != state.EnemySquadStrategy;

            if (line2Dirty && _statusLine2Text != null)
            {
                _cachedModifier = state.ActiveModifier;
                _cachedRelicCount = relicCount;
                _cachedPlayerStrat = state.PlayerSquadStrategy;
                _cachedEnemyStrat = state.EnemySquadStrategy;

                string modPrefix = state.ActiveModifier != WorldModifierType.None
                    ? $"Mod: {state.ActiveModifier}  |  "
                    : string.Empty;
                _statusLine2Text.text = $"{modPrefix}Relics: {relicCount}  |  AI: {state.PlayerSquadStrategy} / {state.EnemySquadStrategy}";
            }
        }

        private void UpdateTopCenterBannerAndBoss(BattleState state, bool isBattleOver, TeamSide? winner, bool isPlayingEvents)
        {
            bool showFinalOutcome = isBattleOver && !isPlayingEvents;
            bool isPaused = eventPresenter != null && eventPresenter.IsPaused;
            bool isInitialPrep = !isBattleOver && !isPlayingEvents && isPaused &&
                                 state.CurrentTurn == 1 && state.CurrentPhase == TurnPhase.Planning;
            int displayTurn = (!isBattleOver && isPlayingEvents && state.CurrentTurn > 1)
                ? state.CurrentTurn - 1
                : state.CurrentTurn;

            bool bannerDirty = !_bannerInitialized ||
                               _cachedBannerTurn != displayTurn ||
                               _cachedBannerShowOutcome != showFinalOutcome ||
                               _cachedBannerPaused != isPaused ||
                               _cachedBannerInitialPrep != isInitialPrep ||
                               _cachedWinner != winner;

            if (bannerDirty && _turnBannerText != null)
            {
                _bannerInitialized = true;
                _cachedBannerTurn = displayTurn;
                _cachedBannerShowOutcome = showFinalOutcome;
                _cachedBannerPaused = isPaused;
                _cachedBannerInitialPrep = isInitialPrep;
                _cachedWinner = winner;

                if (showFinalOutcome)
                {
                    if (winner == TeamSide.Player)
                    {
                        _turnBannerText.text = "VICTORY!";
                        _turnBannerText.color = UguiThemeFactory.HpGreen;
                        if (_turnBannerOutline != null) _turnBannerOutline.color = UguiThemeFactory.HpGreen;
                    }
                    else
                    {
                        _turnBannerText.text = "DEFEAT";
                        _turnBannerText.color = UguiThemeFactory.EnemyCrimson;
                        if (_turnBannerOutline != null) _turnBannerOutline.color = UguiThemeFactory.EnemyCrimson;
                    }
                }
                else
                {
                    _turnBannerText.text = isInitialPrep
                        ? "TURN 1 — XẾP ĐỘI HÌNH"
                        : (isPaused
                            ? $"TURN {displayTurn} — TẠM DỪNG"
                            : $"TURN {displayTurn} — ĐANG GIAO TRANH");
                    _turnBannerText.color = isInitialPrep ? UguiThemeFactory.AllyCyan : UguiThemeFactory.TextPrimary;
                    if (_turnBannerOutline != null)
                    {
                        _turnBannerOutline.color = isInitialPrep ? UguiThemeFactory.AllyCyan : UguiThemeFactory.AccentGold;
                    }
                }
            }

            // Boss Bar lookup
            UnitState boss = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.Team == TeamSide.Enemy && u.BossPhase > 0)
                {
                    boss = u;
                    break;
                }
            }

            bool bossVisible = boss != null;
            if (_cachedBossVisible != bossVisible && _bossBarRoot != null)
            {
                _cachedBossVisible = bossVisible;
                _bossBarRoot.SetActive(bossVisible);
            }

            if (boss != null)
            {
                bool bossDirty = _cachedBossHp != boss.CurrentHP ||
                                 _cachedBossMaxHp != boss.MaxHP ||
                                 _cachedBossPhase != boss.BossPhase ||
                                 _cachedBossName != boss.Name;
                if (bossDirty)
                {
                    _cachedBossHp = boss.CurrentHP;
                    _cachedBossMaxHp = boss.MaxHP;
                    _cachedBossPhase = boss.BossPhase;
                    _cachedBossName = boss.Name;

                    float ratio = boss.MaxHP > 0 ? Mathf.Clamp01((float)boss.CurrentHP / boss.MaxHP) : 0f;
                    if (_bossHpFillRect != null)
                    {
                        _bossHpFillRect.anchorMax = new Vector2(ratio, 1f);
                    }
                    if (_bossHpText != null)
                    {
                        _bossHpText.text = $"{boss.Name}  |  PHASE {boss.BossPhase} ({boss.CurrentHP}/{boss.MaxHP})";
                    }
                }
            }
        }

        private void UpdateTopRightControls(bool isBattleOver, bool isPlayingEvents)
        {
            PlaybackSpeedMode speed = eventPresenter != null ? eventPresenter.SpeedMode : PlaybackSpeedMode.Speed1x;
            if (_cachedSpeedMode != speed && _speedButtonText != null)
            {
                _cachedSpeedMode = speed;
                switch (speed)
                {
                    case PlaybackSpeedMode.Speed2x:
                        _speedButtonText.text = "2x";
                        break;
                    case PlaybackSpeedMode.Speed4x:
                        _speedButtonText.text = "4x";
                        break;
                    default:
                        _speedButtonText.text = "1x";
                        break;
                }
            }

            bool isPaused = eventPresenter != null && eventPresenter.IsPaused;
            bool battleSettled = isBattleOver && !isPlayingEvents;
            bool pauseInteractable = !battleSettled;
            bool stepInteractable = isPlayingEvents && !battleSettled;

            if ((!_topControlsInitialized || _cachedPaused != isPaused) && _pauseButton != null && _pauseButtonText != null)
            {
                _cachedPaused = isPaused;
                _pauseButtonText.text = isPaused ? "PLAY" : "PAUSE";
                SetButtonColor(_pauseButton, isPaused ? PauseActiveColor : UguiThemeFactory.PanelBgSoft);
            }

            if ((!_topControlsInitialized || _cachedPauseInteractable != pauseInteractable) && _pauseButton != null)
            {
                _cachedPauseInteractable = pauseInteractable;
                _pauseButton.interactable = pauseInteractable;
            }

            if ((!_topControlsInitialized || _cachedStepInteractable != stepInteractable) && _stepButton != null)
            {
                _cachedStepInteractable = stepInteractable;
                _stepButton.interactable = stepInteractable;
            }

            _topControlsInitialized = true;
        }

        private void UpdateBottomLeftAllyCards(BattleState state)
        {
            _playerUnitsBuffer.Clear();
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.Team == TeamSide.Player)
                {
                    _playerUnitsBuffer.Add(u);
                }
            }

            string actingId = eventPresenter != null ? eventPresenter.CurrentActingUnitId : null;
            UnitState focused = ResolveFocusedAlly(state);
            string focusedId = focused != null ? focused.InstanceId : null;

            int count = Mathf.Min(_playerUnitsBuffer.Count, MaxAllyCards);
            for (int i = 0; i < MaxAllyCards; i++)
            {
                AllyCardSlot slot = _allyCards[i];
                bool active = i < count;
                if (slot.Root.activeSelf != active)
                {
                    slot.Root.SetActive(active);
                }
                if (!active)
                {
                    slot.BoundUnitId = null;
                    continue;
                }

                UnitState u = _playerUnitsBuffer[i];
                slot.BoundUnitId = u.InstanceId;

                if (slot.CachedName != u.Name)
                {
                    slot.CachedName = u.Name;
                    slot.NameText.text = u.Name ?? string.Empty;
                }

                if (slot.CachedRole != u.Role || slot.CachedAlive != u.IsAlive)
                {
                    slot.CachedRole = u.Role;
                    slot.PortraitBg.color = u.IsAlive
                        ? GetRoleColor(u.Role)
                        : new Color(0.28f, 0.30f, 0.34f, 0.7f);
                    slot.RoleCodeText.text = GetRoleCode(u.Role);
                }

                int badgeState = u.IsStunned ? 1 : (!string.IsNullOrEmpty(u.ProtectingAllyId) ? 2 : 0);
                if (slot.CachedBadgeState != badgeState)
                {
                    slot.CachedBadgeState = badgeState;
                    if (badgeState == 1)
                    {
                        slot.StatusBadgeRoot.SetActive(true);
                        slot.StatusBadgeText.text = "[STUN]";
                        slot.StatusBadgeText.color = UguiThemeFactory.EnemyCrimson;
                    }
                    else if (badgeState == 2)
                    {
                        slot.StatusBadgeRoot.SetActive(true);
                        slot.StatusBadgeText.text = "[PROT]";
                        slot.StatusBadgeText.color = UguiThemeFactory.AllyCyan;
                    }
                    else
                    {
                        slot.StatusBadgeRoot.SetActive(false);
                    }
                }

                int highlightState;
                if (!u.IsAlive)
                {
                    highlightState = 3;
                }
                else if (!string.IsNullOrEmpty(actingId) && u.InstanceId == actingId)
                {
                    highlightState = 2;
                }
                else if (!string.IsNullOrEmpty(focusedId) && u.InstanceId == focusedId)
                {
                    highlightState = 1;
                }
                else
                {
                    highlightState = 0;
                }

                if (slot.CachedHighlightState != highlightState || slot.CachedAlive != u.IsAlive)
                {
                    slot.CachedHighlightState = highlightState;
                    slot.CachedAlive = u.IsAlive;

                    switch (highlightState)
                    {
                        case 3:
                            slot.BorderOutline.color = new Color(0.35f, 0.38f, 0.42f, 0.6f);
                            SetButtonColor(slot.SelectButton, new Color(0.10f, 0.11f, 0.14f, 0.72f));
                            break;
                        case 2:
                            slot.BorderOutline.color = UguiThemeFactory.AccentGold;
                            SetButtonColor(slot.SelectButton, new Color(0.14f, 0.16f, 0.22f, 0.96f));
                            break;
                        case 1:
                            slot.BorderOutline.color = UguiThemeFactory.AllyCyan;
                            SetButtonColor(slot.SelectButton, new Color(0.12f, 0.17f, 0.25f, 0.96f));
                            break;
                        default:
                            slot.BorderOutline.color = new Color(0.30f, 0.36f, 0.46f, 0.75f);
                            SetButtonColor(slot.SelectButton, UguiThemeFactory.PanelBg);
                            break;
                    }
                }

                bool barsDirty = slot.CachedHp != u.CurrentHP ||
                                 slot.CachedMaxHp != u.MaxHP ||
                                 slot.CachedShield != u.Shield;
                if (barsDirty)
                {
                    slot.CachedHp = u.CurrentHP;
                    slot.CachedMaxHp = u.MaxHP;
                    slot.CachedShield = u.Shield;

                    float hpRatio = u.MaxHP > 0 ? Mathf.Clamp01((float)u.CurrentHP / u.MaxHP) : 0f;
                    float shieldRatio = (u.MaxHP > 0 && u.Shield > 0) ? Mathf.Clamp01((float)u.Shield / u.MaxHP) : 0f;

                    slot.HpFillRect.anchorMax = new Vector2(hpRatio, 1f);
                    slot.ShieldFillRect.anchorMax = new Vector2(shieldRatio, 0.45f);
                    slot.HpValueText.text = u.Shield > 0
                        ? $"{u.CurrentHP}/{u.MaxHP} (+{u.Shield})"
                        : $"{u.CurrentHP}/{u.MaxHP}";
                }

                if (slot.CachedEnergy != u.CurrentEnergy || slot.CachedMaxEnergy != u.MaxEnergy)
                {
                    slot.CachedEnergy = u.CurrentEnergy;
                    slot.CachedMaxEnergy = u.MaxEnergy;
                    float energyRatio = u.MaxEnergy > 0 ? Mathf.Clamp01((float)u.CurrentEnergy / u.MaxEnergy) : 0f;
                    slot.EnergyFillRect.anchorMax = new Vector2(energyRatio, 1f);
                }
            }
        }

        private UnitState ResolveFocusedAlly(BattleState state)
        {
            if (state == null || state.Units == null) return null;

            if (!string.IsNullOrEmpty(_focusedAllyId))
            {
                for (int i = 0; i < state.Units.Count; i++)
                {
                    UnitState u = state.Units[i];
                    if (u != null && u.InstanceId == _focusedAllyId && u.Team == TeamSide.Player && u.IsAlive)
                    {
                        return u;
                    }
                }
            }

            if (gridView != null && !string.IsNullOrEmpty(gridView.SelectedUnitId))
            {
                for (int i = 0; i < state.Units.Count; i++)
                {
                    UnitState u = state.Units[i];
                    if (u != null && u.InstanceId == gridView.SelectedUnitId && u.Team == TeamSide.Player && u.IsAlive)
                    {
                        _focusedAllyId = u.InstanceId;
                        return u;
                    }
                }
            }

            UnitState fallbackDead = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.Team == TeamSide.Player)
                {
                    if (u.IsAlive)
                    {
                        _focusedAllyId = u.InstanceId;
                        return u;
                    }
                    if (fallbackDead == null)
                    {
                        fallbackDead = u;
                    }
                }
            }

            return fallbackDead;
        }

        private void UpdateBottomRightSkillOrbs(BattleState state, UnitState unit, bool isBattleOver, bool isPlayingEvents)
        {
            string unitName = unit != null ? unit.Name : "Không có";
            if (_cachedFocusedLabelName != unitName && _focusedAllyLabel != null)
            {
                _cachedFocusedLabelName = unitName;
                _focusedAllyLabel.text = $"Đang xem: {unitName}";
            }

            int skillCount = (unit != null && unit.Skills != null) ? Mathf.Min(unit.Skills.Count, MaxSkillOrbs) : 0;
            for (int i = 0; i < MaxSkillOrbs; i++)
            {
                SkillOrbSlot orbSlot = _skillOrbs[i];
                bool active = i < skillCount;
                if (orbSlot.Root.activeSelf != active)
                {
                    orbSlot.Root.SetActive(active);
                }

                if (!active)
                {
                    orbSlot.BoundUnitId = null;
                    orbSlot.BoundSkill = null;
                    continue;
                }

                SkillInstance sk = unit.Skills[i];
                orbSlot.BoundUnitId = unit.InstanceId;
                orbSlot.BoundSkill = sk;

                bool showPill = sk.Slot != SkillSlotType.BasicAttack;
                if (orbSlot.ModePillRoot.activeSelf != showPill)
                {
                    orbSlot.ModePillRoot.SetActive(showPill);
                }

                if (showPill && orbSlot.CachedMode != sk.ControlMode)
                {
                    orbSlot.CachedMode = sk.ControlMode;
                    switch (sk.ControlMode)
                    {
                        case SkillControlMode.SemiAuto:
                            orbSlot.ModePillText.text = "SEMI";
                            SetButtonColor(orbSlot.ModePillButton, new Color(0.22f, 0.36f, 0.56f, 1f));
                            break;
                        case SkillControlMode.Manual:
                            orbSlot.ModePillText.text = "MAN";
                            SetButtonColor(orbSlot.ModePillButton, new Color(0.68f, 0.44f, 0.06f, 1f));
                            break;
                        default:
                            orbSlot.ModePillText.text = "AUTO";
                            SetButtonColor(orbSlot.ModePillButton, UguiThemeFactory.PanelBgSoft);
                            break;
                    }
                }

                if (orbSlot.CachedSlotType != sk.Slot)
                {
                    orbSlot.CachedSlotType = sk.Slot;
                    orbSlot.OrbCodeText.text = GetSkillSlotCode(sk.Slot);
                }

                if (orbSlot.CachedCooldown != sk.CurrentCooldown || orbSlot.CachedCost != sk.EnergyCost)
                {
                    orbSlot.CachedCooldown = sk.CurrentCooldown;
                    orbSlot.CachedCost = sk.EnergyCost;
                    orbSlot.OrbCostText.text = sk.CurrentCooldown > 0 ? $"CD:{sk.CurrentCooldown}" : $"E:{sk.EnergyCost}";
                    orbSlot.OrbCostText.color = sk.CurrentCooldown > 0
                        ? UguiThemeFactory.EnemyCrimson
                        : UguiThemeFactory.EnergyGold;
                }

                bool ready = unit.IsAlive && !unit.IsStunned && sk.IsReady(unit.CurrentEnergy);
                bool isManualReady = !isBattleOver &&
                                     !isPlayingEvents &&
                                     state.CurrentPhase == TurnPhase.Planning &&
                                     sk.ControlMode == SkillControlMode.Manual &&
                                     ready;

                if (orbSlot.ManualReadyRing != null && orbSlot.CachedManualReady != isManualReady)
                {
                    orbSlot.CachedManualReady = isManualReady;
                    orbSlot.ManualReadyRing.SetActive(isManualReady);
                }

                if (orbSlot.CachedEffectType != sk.EffectType || orbSlot.CachedReady != ready)
                {
                    orbSlot.CachedEffectType = sk.EffectType;
                    orbSlot.CachedReady = ready;
                    Color baseColor = GetOrbEffectColor(sk.EffectType);
                    if (!ready)
                    {
                        baseColor = new Color(baseColor.r * 0.42f, baseColor.g * 0.42f, baseColor.b * 0.42f, 0.85f);
                    }
                    SetButtonColor(orbSlot.OrbButton, baseColor);
                }
            }

            bool isPaused = eventPresenter != null && eventPresenter.IsPaused;
            bool battleEndedAndSettled = isBattleOver && !isPlayingEvents;
            bool pauseInteractable = !battleEndedAndSettled;
            bool isInitialPrep = !isBattleOver && !isPlayingEvents && isPaused &&
                                 state.CurrentTurn == 1 && state.CurrentPhase == TurnPhase.Planning;

            if ((!_actionBtnInitialized || _cachedActionBtnPaused != isPaused || _cachedActionBtnInitialPrep != isInitialPrep) &&
                _runTurnButton != null && _runTurnButtonText != null)
            {
                _cachedActionBtnPaused = isPaused;
                _cachedActionBtnInitialPrep = isInitialPrep;
                _runTurnButtonText.text = isInitialPrep
                    ? "BẮT ĐẦU\n[SPACE]"
                    : (isPaused ? "TIẾP TỤC\n[SPACE]" : "TẠM DỪNG\n[SPACE]");
                SetButtonColor(_runTurnButton, isPaused ? PauseActiveColor : ActionNormalColor);
                if (_runTurnOutline != null)
                {
                    _runTurnOutline.color = isPaused ? UguiThemeFactory.AccentGold : UguiThemeFactory.AllyCyan;
                }
            }

            if ((!_actionBtnInitialized || _cachedRunTurnInteractable != pauseInteractable))
            {
                _cachedRunTurnInteractable = pauseInteractable;
                if (_runTurnButton != null)
                {
                    _runTurnButton.interactable = pauseInteractable;
                }
                if (_policyButton != null)
                {
                    _policyButton.interactable = pauseInteractable;
                }
            }

            _actionBtnInitialized = true;
        }

        private void UpdateEndBattleOverlay(bool isBattleOver, TeamSide? winner, bool isPlayingEvents)
        {
            if (_finalEndModalState != 0)
            {
                if (_victoryModalRoot != null && _victoryModalRoot.activeSelf)
                {
                    _victoryModalRoot.SetActive(false);
                }
                if (_defeatModalRoot != null && _defeatModalRoot.activeSelf)
                {
                    _defeatModalRoot.SetActive(false);
                }
                if (_finalGameOverModalRoot != null && !_finalGameOverModalRoot.activeSelf)
                {
                    _finalGameOverModalRoot.SetActive(true);
                }
                return;
            }

            if (_finalGameOverModalRoot != null && _finalGameOverModalRoot.activeSelf)
            {
                _finalGameOverModalRoot.SetActive(false);
            }

            bool showOverlay = isBattleOver && !isPlayingEvents;
            bool showVictory = showOverlay && winner == TeamSide.Player;
            bool showDefeat = showOverlay && winner != TeamSide.Player;

            if (_victoryModalRoot != null && _victoryModalRoot.activeSelf != showVictory)
            {
                _victoryModalRoot.SetActive(showVictory);
            }

            if (_defeatModalRoot != null && _defeatModalRoot.activeSelf != showDefeat)
            {
                _defeatModalRoot.SetActive(showDefeat);
            }

            if (showVictory)
            {
                bool canEndless = sandboxController != null && sandboxController.CanEnterEndlessMode();
                if (_endlessButton != null && _endlessButton.gameObject.activeSelf != canEndless)
                {
                    _endlessButton.gameObject.SetActive(canEndless);
                }
                if (_victoryTitleText != null)
                {
                    _victoryTitleText.text = canEndless
                        ? "ĐẠI THẮNG BOSS! CHỌN BƯỚC TIẾP THEO"
                        : "CHIẾN THẮNG TRẬN ĐẤU!";
                }
                if (_victoryClaimButtonText != null)
                {
                    _victoryClaimButtonText.text = canEndless
                        ? "CHIẾN THẮNG, KẾT THÚC TRÒ CHƠI"
                        : "CHIẾN THẮNG! NHẬN THƯỞNG ->";
                }
            }
        }

        private void OnClickCycleSpeed()
        {
            PlayClickSfx();
            if (eventPresenter == null) return;

            int next = ((int)eventPresenter.SpeedMode + 1) % 3;
            eventPresenter.SpeedMode = (PlaybackSpeedMode)next;
        }

        private void OnClickTogglePause()
        {
            PlayClickSfx();
            EnsureReferences();
            if (sandboxController != null && sandboxController.CurrentState != null && !TurnManager.IsBattleOver(sandboxController.CurrentState))
            {
                sandboxController.ToggleBattlePause();
            }
            else if (eventPresenter != null)
            {
                eventPresenter.IsPaused = !eventPresenter.IsPaused;
            }
        }

        private void OnClickStepEvent()
        {
            PlayClickSfx();
            if (eventPresenter == null) return;
            eventPresenter.IsPaused = true;
            eventPresenter.StepSingleEvent();
        }

        private void OnClickOpenSettings()
        {
            EnsureReferences();
            mainMenuAndSettingsView?.OpenSettings();
        }

        private void OnClickOpenPolicy()
        {
            PlayClickSfx();
            EnsureReferences();
            if (modalsView == null)
            {
                modalsView = FindFirstObjectByType<BattleModalsView>();
            }
            modalsView?.OpenPolicyModal(_focusedAllyId);
        }

        private void OnClickAllyCard(int index)
        {
            if (index < 0 || index >= MaxAllyCards) return;
            string unitId = _allyCards[index].BoundUnitId;
            if (string.IsNullOrEmpty(unitId)) return;

            PlayClickSfx();
            _focusedAllyId = unitId;
            gridView?.SelectUnitFromHud(unitId);
        }

        private void OnClickSkillModePill(int orbIndex)
        {
            if (orbIndex < 0 || orbIndex >= MaxSkillOrbs) return;
            SkillInstance skill = _skillOrbs[orbIndex].BoundSkill;
            if (skill == null || skill.Slot == SkillSlotType.BasicAttack) return;

            PlayClickSfx();
            int next = ((int)skill.ControlMode + 1) % 3;
            skill.ControlMode = (SkillControlMode)next;
            ShowOrbHint($"{skill.Name}: {skill.ControlMode}");
        }

        private void OnClickSkillOrb(int orbIndex)
        {
            if (orbIndex < 0 || orbIndex >= MaxSkillOrbs || sandboxController == null || sandboxController.CurrentState == null) return;

            SkillOrbSlot orbSlot = _skillOrbs[orbIndex];
            SkillInstance skill = orbSlot.BoundSkill;
            if (skill == null) return;

            BattleState state = sandboxController.CurrentState;
            UnitState caster = FindUnitById(state, orbSlot.BoundUnitId);
            if (caster == null) return;

            bool isPlaying = eventPresenter != null && eventPresenter.IsPlayingEvents;
            bool isBattleOver = TurnManager.IsBattleOver(state);
            if (!isBattleOver &&
                caster.IsAlive &&
                !caster.IsStunned &&
                skill.ControlMode == SkillControlMode.Manual &&
                state.CurrentPhase == TurnPhase.Planning &&
                skill.IsReady(caster.CurrentEnergy) &&
                !isPlaying)
            {
                PlayClickSfx();
                if (sandboxController.ExecutePlayerManualSkill(caster, skill))
                {
                    ShowOrbHint($"Đã dùng {skill.Name}!");
                }
                else
                {
                    ShowOrbHint($"Chưa thể dùng {skill.Name} lúc này");
                }
            }
            else if (skill.Slot != SkillSlotType.BasicAttack)
            {
                PlayClickSfx();
                if (isBattleOver)
                {
                    ShowOrbHint("Trận đấu đã kết thúc");
                }
                else if (caster.IsStunned)
                {
                    ShowOrbHint($"{caster.Name} đang bị STUN!");
                }
                else if (skill.ControlMode != SkillControlMode.Manual)
                {
                    ShowOrbHint($"{skill.Name} (E:{skill.EnergyCost}) — Chuyển sang MAN để dùng tay");
                }
                else
                {
                    ShowOrbHint($"{skill.Name} — E:{skill.EnergyCost} | CD:{skill.CurrentCooldown}");
                }
            }
        }

        private void OnClickRunTurn()
        {
            PlayClickSfx();
            EnsureReferences();
            sandboxController?.ToggleBattlePause();
        }

        private void OnClickClaimVictory()
        {
            PlayClickSfx();
            bool canEndless = sandboxController != null && sandboxController.CanEnterEndlessMode();
            if (canEndless)
            {
                sandboxController.FinalizeCampaignVictoryGameOver();
                OpenFinalGameOverModal(true);
            }
            else
            {
                sandboxController?.ClaimBattleVictoryAndAdvance();
            }
        }

        private void OnClickEnterEndless()
        {
            PlayClickSfx();
            sandboxController?.EnterEndlessModeFromVictory();
        }

        private void OnClickReturnOnDefeat()
        {
            PlayClickSfx();
            OpenFinalGameOverModal(false);
        }

        private void OpenFinalGameOverModal(bool isCampaignVictory)
        {
            _finalEndModalState = isCampaignVictory ? 1 : 2;
            if (sandboxController != null)
            {
                sandboxController.IsFinalGameOverModalOpen = true;
            }
            Color accentColor = isCampaignVictory ? UguiThemeFactory.AccentGold : UguiThemeFactory.EnemyCrimson;

            if (_finalGameOverCardOutline != null)
            {
                _finalGameOverCardOutline.color = accentColor;
            }

            if (_finalGameOverTitleText != null)
            {
                _finalGameOverTitleText.text = isCampaignVictory
                    ? "CHIẾN THẮNG TRÒ CHƠI!"
                    : "THẤT BẠI — TRÒ CHƠI KẾT THÚC";
                _finalGameOverTitleText.color = accentColor;
            }

            if (_finalGameOverDescText != null)
            {
                RunState run = sandboxController != null ? sandboxController.CurrentRun : null;
                int seed = run != null ? run.Seed : 0;
                int floor = run != null ? run.CurrentFloor + 1 : 1;
                int wins = run != null ? run.BattlesWon : 0;
                int relics = run != null && run.CollectedRelics != null ? run.CollectedRelics.Count : 0;

                string headline = isCampaignVictory
                    ? "Chúc mừng! Bạn đã đánh bại Trùm cuối The Warden và hoàn thành chiến dịch."
                    : "Đội hình đã gục ngã trên chiến trường. Chiến dịch lần này đã khép lại.";
                string floorLabel = isCampaignVictory ? "Tầng hoàn thành" : "Tầng đạt tới";

                _finalGameOverDescText.text =
                    $"{headline}\n\n" +
                    $"Seed: #{seed}   |   {floorLabel}: {floor}\n" +
                    $"Số trận thắng: {wins}   |   Di vật thu thập: {relics}";
            }

            if (_victoryModalRoot != null)
            {
                _victoryModalRoot.SetActive(false);
            }
            if (_defeatModalRoot != null)
            {
                _defeatModalRoot.SetActive(false);
            }
            if (_finalGameOverModalRoot != null)
            {
                _finalGameOverModalRoot.SetActive(true);
            }
        }

        private void OnClickFinalPlayAgain()
        {
            PlayClickSfx();
            _finalEndModalState = 0;
            if (_finalGameOverModalRoot != null)
            {
                _finalGameOverModalRoot.SetActive(false);
            }
            if (sandboxController != null)
            {
                sandboxController.IsFinalGameOverModalOpen = false;
                sandboxController.StartNewRun();
            }
        }

        private void OnClickFinalReturnMainMenu()
        {
            PlayClickSfx();
            _finalEndModalState = 0;
            if (_finalGameOverModalRoot != null)
            {
                _finalGameOverModalRoot.SetActive(false);
            }
            if (sandboxController != null)
            {
                sandboxController.IsFinalGameOverModalOpen = false;
                sandboxController.ReturnToMainMenu();
            }
        }

        private void ShowOrbHint(string message)
        {
            _transientHint = message;
            _transientHintTimer = 2.6f;
            if (_orbStatusHintText != null)
            {
                _orbStatusHintText.text = _transientHint;
                _orbStatusHintText.gameObject.SetActive(true);
            }
        }

        private void PlayClickSfx()
        {
            if (sandboxController != null && sandboxController.AudioManager != null)
            {
                sandboxController.AudioManager.PlayUiClick();
            }
        }

        private static UnitState FindUnitById(BattleState state, string id)
        {
            if (state == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.InstanceId == id)
                {
                    return u;
                }
            }
            return null;
        }

        private static UnityEngine.UI.Button CreateControlButton(string name, Transform parent, string label, float width, int fontSize)
        {
            UnityEngine.UI.Button btn = UguiThemeFactory.CreateButton(name, parent, label, UguiThemeFactory.PanelBgSoft, fontSize);
            UguiThemeFactory.CreateOutlineFrame("Outline", btn.transform, new Color(0.45f, 0.55f, 0.72f, 0.6f));
            RectTransform rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 40f);
            return btn;
        }

        private static void CreateThresholdMarker(RectTransform parent, string name, float normalizedX)
        {
            UnityEngine.UI.Image line = UguiThemeFactory.CreatePanel(name, parent, UguiThemeFactory.AccentGold, false);
            line.raycastTarget = false;
            RectTransform rt = line.rectTransform;
            rt.anchorMin = new Vector2(normalizedX, 0f);
            rt.anchorMax = new Vector2(normalizedX, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2f, 0f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static Color GetRoleColor(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Guardian: return GuardianColor;
                case UnitRole.Vanguard: return VanguardColor;
                case UnitRole.Ranger: return RangerColor;
                case UnitRole.Mage: return MageColor;
                case UnitRole.Medic: return MedicColor;
                default: return GuardianColor;
            }
        }

        private static string GetRoleCode(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Guardian: return "GRD";
                case UnitRole.Vanguard: return "VNG";
                case UnitRole.Ranger: return "RNG";
                case UnitRole.Mage: return "MAG";
                case UnitRole.Medic: return "MED";
                default: return "UNT";
            }
        }

        private static string GetSkillSlotCode(SkillSlotType slot)
        {
            switch (slot)
            {
                case SkillSlotType.BasicAttack: return "ATK";
                case SkillSlotType.Skill1: return "S1";
                case SkillSlotType.Skill2: return "S2";
                case SkillSlotType.Ultimate: return "ULT";
                default: return "SKL";
            }
        }

        private static Color GetOrbEffectColor(SkillEffectType effectType)
        {
            switch (effectType)
            {
                case SkillEffectType.Heal:
                    return OrbHealColor;
                case SkillEffectType.Shield:
                case SkillEffectType.ProtectAlly:
                    return OrbShieldColor;
                case SkillEffectType.Stun:
                    return OrbStunColor;
                case SkillEffectType.Mark:
                    return OrbMarkColor;
                default:
                    return OrbDamageColor;
            }
        }

        private static void SetButtonColor(UnityEngine.UI.Button button, Color normalColor)
        {
            if (button == null) return;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.22f);
            colors.selectedColor = normalColor;
            colors.disabledColor = new Color(
                normalColor.r * 0.45f,
                normalColor.g * 0.45f,
                normalColor.b * 0.45f,
                normalColor.a * 0.6f);
            button.colors = colors;
        }

        private static void UnbindButton(UnityEngine.UI.Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private static void StretchFull(RectTransform rt, float horizontalPadding, float verticalPadding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rt.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }
    }
}
