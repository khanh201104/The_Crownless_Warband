using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Roguelite;
using CrownlessWarband.Presentation.BattleView;
using UnityEngine;

namespace CrownlessWarband.Presentation.UI
{
    public class CampaignWorldMapView : MonoBehaviour
    {
        private const int MaxMiniRosterPills = 5;
        private const int RoadSegmentCount = 14;
        private const float LeftPadding = 220f;
        private const float FloorSpacing = 310f;
        private const float RightPadding = 240f;
        private const float NormalTravelDuration = 0.55f;
        private const float ShortHopDuration = 0.16f;
        private const int PanoramaWidth = 768;
        private const int PanoramaHeight = 320;

        private sealed class MiniRosterPill
        {
            public GameObject Root;
            public UnityEngine.UI.Text NameText;
            public RectTransform HpFillRect;
        }

        private static readonly Dictionary<int, Sprite> _panoramaCache = new Dictionary<int, Sprite>(4);

        [SerializeField] private BattleSandboxController sandboxController;
        [SerializeField] private BattleModalsView modalsView;
        [SerializeField] private MainMenuAndSettingsView mainMenuAndSettingsView;

        private Canvas _worldMapCanvas;
        private RectTransform _canvasRect;

        // Top Header Bar
        private UnityEngine.UI.Text _headerSummaryText;
        private UnityEngine.UI.Button _policyButton;
        private UnityEngine.UI.Button _saveButton;
        private UnityEngine.UI.Button _unlockEndlessButton;
        private UnityEngine.UI.Button _settingsButton;

        // Panoramic Scroll Map
        private UnityEngine.UI.ScrollRect _scrollRect;
        private RectTransform _mapContent;
        private UnityEngine.UI.Image _panoramaBackdrop;
        private RectTransform _roadBordersLayer;
        private RectTransform _roadSurfaceLayer;
        private RectTransform _markersLayer;
        private RectTransform _avatarLayer;

        // Player Avatar & Toast
        private RectTransform _avatarRoot;
        private RectTransform _avatarBodyRt;
        private UnityEngine.UI.Image _avatarImage;
        private GameObject _travelToastRoot;
        private RectTransform _travelToastRt;
        private UnityEngine.UI.Text _travelToastText;
        private float _travelToastTimer;

        // Mini Roster Strip Bottom-Left
        private readonly MiniRosterPill[] _miniRosterPills = new MiniRosterPill[MaxMiniRosterPills];
        private UnityEngine.UI.Text _miniStatusText;

        // Floating Hover Tooltip Card
        private GameObject _tooltipRoot;
        private RectTransform _tooltipRt;
        private UnityEngine.UI.Image _tooltipOutline;
        private UnityEngine.UI.Text _tooltipTitleText;
        private UnityEngine.UI.Text _tooltipMetaText;
        private UnityEngine.UI.Text _tooltipStatusText;

        // Pooled / Spawned map objects cleaned up on rebuild & OnDestroy
        private readonly List<GameObject> _spawnedRoadObjects = new List<GameObject>(256);
        private readonly List<MapNodeMarkerView> _spawnedMarkers = new List<MapNodeMarkerView>(24);
        private readonly Dictionary<string, Vector2> _nodePositions = new Dictionary<string, Vector2>(24);
        private readonly Dictionary<int, int> _floorNodeCounts = new Dictionary<int, int>(12);

        // Travel interpolation state
        private bool _isTraveling;
        private float _travelElapsed;
        private float _activeTravelDuration = NormalTravelDuration;
        private Vector2 _avatarBasePos;
        private Vector2 _travelStartPos;
        private Vector2 _travelEndPos;
        private CampaignNode _pendingTravelNode;

        // Dirty-tracking state (zero-GC in Update)
        private bool _mapDirty = true;
        private RunState _lastRun;
        private string _lastCurrentNodeId;
        private int _lastFloor = -1;
        private int _lastBattlesWon = -1;
        private int _lastEndlessDepth = -1;
        private bool _lastRunCompleted;
        private int _lastRelicCount = -1;
        private string _lastStatusMessage;
        private readonly Vector3[] _worldCornersBuffer = new Vector3[4];

        private static readonly Color BtnActiveColor = new Color(0.14f, 0.52f, 0.62f, 1f);
        private static readonly Color BtnGreenColor = new Color(0.14f, 0.48f, 0.32f, 1f);
        private static readonly Color BtnGoldColor = new Color(0.58f, 0.40f, 0.08f, 1f);

        private void Awake()
        {
            EnsureReferences();
            BuildWorldMapCanvas();
            _worldMapCanvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            EnsureReferences();

            bool shouldShow = sandboxController != null &&
                              sandboxController.ScreenMode == SandboxScreenMode.CampaignMap &&
                              !sandboxController.IsDebugMode;

            if (_worldMapCanvas != null && _worldMapCanvas.gameObject.activeSelf != shouldShow)
            {
                _worldMapCanvas.gameObject.SetActive(shouldShow);
                if (shouldShow)
                {
                    _mapDirty = true;
                    HideTooltip();
                }
                else
                {
                    CancelPendingTravel();
                    HideTooltip();
                    HideTravelToast();
                }
            }

            if (!shouldShow) return;

            if (sandboxController.IsAnyModalOpen)
            {
                HideTooltip();
            }

            RefreshIfDirty();
            UpdateAvatarMotion();
            UpdateTravelToast();
        }

        private void OnDestroy()
        {
            UnbindButton(_policyButton);
            UnbindButton(_saveButton);
            UnbindButton(_unlockEndlessButton);
            UnbindButton(_settingsButton);

            ClearMapElements();

            if (_worldMapCanvas != null)
            {
                Destroy(_worldMapCanvas.gameObject);
                _worldMapCanvas = null;
            }
        }

        private void EnsureReferences()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }
            if (modalsView == null)
            {
                modalsView = FindFirstObjectByType<BattleModalsView>();
            }
            if (mainMenuAndSettingsView == null)
            {
                mainMenuAndSettingsView = FindFirstObjectByType<MainMenuAndSettingsView>();
            }
        }

        private void BuildWorldMapCanvas()
        {
            _worldMapCanvas = UguiThemeFactory.CreateOverlayCanvas("CampaignWorldMapCanvas", 16);
            _canvasRect = _worldMapCanvas.GetComponent<RectTransform>();

            // Fullscreen dark slate backing behind scroll viewport
            UnityEngine.UI.Image rootBg = UguiThemeFactory.CreatePanel(
                "WorldMapRootBg",
                _canvasRect,
                new Color(0.05f, 0.07f, 0.11f, 1f),
                false);
            rootBg.raycastTarget = true;
            StretchFull(rootBg.rectTransform, 0f, 0f);

            BuildTopStatusHeader(_canvasRect);
            BuildCenterPanoramicScroll(_canvasRect);
            BuildBottomMiniRosterStrip(_canvasRect);
            BuildTooltipCard(_canvasRect);
        }

        private void BuildTopStatusHeader(RectTransform root)
        {
            UnityEngine.UI.Image headerBg = UguiThemeFactory.CreatePanel(
                "TopStatusHeader",
                root,
                new Color(0.06f, 0.08f, 0.12f, 0.96f),
                false);
            RectTransform hdrRt = headerBg.rectTransform;
            hdrRt.anchorMin = new Vector2(0f, 1f);
            hdrRt.anchorMax = new Vector2(1f, 1f);
            hdrRt.pivot = new Vector2(0.5f, 1f);
            hdrRt.offsetMin = new Vector2(0f, -64f);
            hdrRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image bottomBorder = UguiThemeFactory.CreatePanel(
                "HeaderBottomLine",
                hdrRt,
                new Color(0.13f, 0.83f, 0.93f, 0.45f),
                false);
            bottomBorder.raycastTarget = false;
            RectTransform lineRt = bottomBorder.rectTransform;
            lineRt.anchorMin = new Vector2(0f, 0f);
            lineRt.anchorMax = new Vector2(1f, 0f);
            lineRt.pivot = new Vector2(0.5f, 0f);
            lineRt.sizeDelta = new Vector2(0f, 2f);
            lineRt.anchoredPosition = Vector2.zero;

            _headerSummaryText = UguiThemeFactory.CreateText(
                "HeaderSummaryText",
                hdrRt,
                string.Empty,
                16,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _headerSummaryText.resizeTextForBestFit = true;
            _headerSummaryText.resizeTextMinSize = 11;
            _headerSummaryText.resizeTextMaxSize = 16;
            _headerSummaryText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform sumRt = _headerSummaryText.rectTransform;
            sumRt.anchorMin = new Vector2(0f, 0f);
            sumRt.anchorMax = new Vector2(1f, 1f);
            sumRt.offsetMin = new Vector2(20f, 4f);
            sumRt.offsetMax = new Vector2(-536f, -4f);

            RectTransform btnsRt = UguiThemeFactory.CreateRect("HeaderButtonsRow", hdrRt);
            btnsRt.anchorMin = new Vector2(1f, 0.5f);
            btnsRt.anchorMax = new Vector2(1f, 0.5f);
            btnsRt.pivot = new Vector2(1f, 0.5f);
            btnsRt.sizeDelta = new Vector2(512f, 42f);
            btnsRt.anchoredPosition = new Vector2(-16f, 0f);

            UnityEngine.UI.HorizontalLayoutGroup layout = btnsRt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            _policyButton = CreateHeaderButton("PolicyBtn", btnsRt, "AI Policy", 98f, BtnActiveColor, UguiThemeFactory.AllyCyan);
            _policyButton.onClick.AddListener(OnClickOpenPolicy);

            _saveButton = CreateHeaderButton("SaveBtn", btnsRt, "Lưu JSON", 98f, BtnGreenColor, UguiThemeFactory.HpGreen);
            _saveButton.onClick.AddListener(OnClickSaveJson);

            _unlockEndlessButton = CreateHeaderButton("UnlockEndlessBtn", btnsRt, "Mở Endless Floor", 168f, BtnGoldColor, UguiThemeFactory.AccentGold);
            _unlockEndlessButton.onClick.AddListener(OnClickUnlockEndless);
            _unlockEndlessButton.gameObject.SetActive(false);

            _settingsButton = CreateHeaderButton("SettingsBtn", btnsRt, "Cài đặt", 92f, UguiThemeFactory.PanelBgSoft, new Color(0.45f, 0.55f, 0.72f, 0.7f));
            _settingsButton.onClick.AddListener(OnClickOpenSettings);
        }

        private void BuildCenterPanoramicScroll(RectTransform root)
        {
            RectTransform scrollRt = UguiThemeFactory.CreateRect("PanoramicMapScrollView", root);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(0f, 0f);
            scrollRt.offsetMax = new Vector2(0f, -64f);

            _scrollRect = scrollRt.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            _scrollRect.horizontal = true;
            _scrollRect.vertical = false;
            _scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 45f;

            RectTransform viewportRt = UguiThemeFactory.CreateRect("Viewport", scrollRt);
            StretchFull(viewportRt, 0f, 0f);
            UnityEngine.UI.Image vpImg = viewportRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.01f);
            vpImg.raycastTarget = true;
            viewportRt.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            _mapContent = UguiThemeFactory.CreateRect("MapContent", viewportRt);
            _mapContent.anchorMin = new Vector2(0f, 0f);
            _mapContent.anchorMax = new Vector2(0f, 1f);
            _mapContent.pivot = new Vector2(0f, 0.5f);
            _mapContent.sizeDelta = new Vector2(1920f, 0f);
            _mapContent.anchoredPosition = Vector2.zero;

            _scrollRect.viewport = viewportRt;
            _scrollRect.content = _mapContent;

            // Layer 1: Panorama Backdrop Texture
            RectTransform bgRt = UguiThemeFactory.CreateRect("PanoramaBackdropImage", _mapContent);
            StretchFull(bgRt, 0f, 0f);
            _panoramaBackdrop = bgRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            _panoramaBackdrop.raycastTarget = false;
            _panoramaBackdrop.preserveAspect = false;

            // Layer 2: Road Segments Container (Borders behind Surfaces)
            RectTransform roadsContainer = UguiThemeFactory.CreateRect("RoadSegmentsContainer", _mapContent);
            roadsContainer.anchorMin = new Vector2(0f, 0.5f);
            roadsContainer.anchorMax = new Vector2(0f, 0.5f);
            roadsContainer.pivot = new Vector2(0f, 0.5f);
            roadsContainer.sizeDelta = Vector2.zero;
            roadsContainer.anchoredPosition = Vector2.zero;

            _roadBordersLayer = UguiThemeFactory.CreateRect("RoadBordersLayer", roadsContainer);
            _roadSurfaceLayer = UguiThemeFactory.CreateRect("RoadSurfaceLayer", roadsContainer);

            // Layer 3: Node Markers
            _markersLayer = UguiThemeFactory.CreateRect("MarkersContainer", _mapContent);
            _markersLayer.anchorMin = new Vector2(0f, 0.5f);
            _markersLayer.anchorMax = new Vector2(0f, 0.5f);
            _markersLayer.pivot = new Vector2(0f, 0.5f);
            _markersLayer.sizeDelta = Vector2.zero;
            _markersLayer.anchoredPosition = Vector2.zero;

            // Layer 4: Player Map Avatar (always rendered on top of Nodes)
            _avatarLayer = UguiThemeFactory.CreateRect("AvatarContainer", _mapContent);
            _avatarLayer.anchorMin = new Vector2(0f, 0.5f);
            _avatarLayer.anchorMax = new Vector2(0f, 0.5f);
            _avatarLayer.pivot = new Vector2(0f, 0.5f);
            _avatarLayer.sizeDelta = Vector2.zero;
            _avatarLayer.anchoredPosition = Vector2.zero;

            BuildPlayerAvatar(_avatarLayer);
        }

        private void BuildPlayerAvatar(RectTransform parent)
        {
            _avatarRoot = UguiThemeFactory.CreateRect("PlayerMapAvatar", parent);
            _avatarRoot.sizeDelta = new Vector2(88f, 96f);

            RectTransform shadowRt = UguiThemeFactory.CreateRect("DropShadow", _avatarRoot);
            shadowRt.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRt.sizeDelta = new Vector2(64f, 26f);
            shadowRt.anchoredPosition = new Vector2(0f, -16f);
            UnityEngine.UI.Image shadowImg = shadowRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            shadowImg.sprite = ProceduralUnitArtFactory.GetDropShadowSprite();
            shadowImg.color = new Color(0f, 0f, 0f, 0.62f);
            shadowImg.raycastTarget = false;

            _avatarBodyRt = UguiThemeFactory.CreateRect("AvatarSprite", _avatarRoot);
            _avatarBodyRt.anchorMin = new Vector2(0.5f, 0.5f);
            _avatarBodyRt.anchorMax = new Vector2(0.5f, 0.5f);
            _avatarBodyRt.sizeDelta = new Vector2(82f, 82f);
            _avatarBodyRt.anchoredPosition = new Vector2(0f, 18f);
            _avatarImage = _avatarBodyRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            _avatarImage.sprite = ProceduralUnitArtFactory.GetUnitSprite(UnitRole.Guardian, TeamSide.Player, false);
            _avatarImage.preserveAspect = true;
            _avatarImage.raycastTarget = false;

            // Floating Toast near avatar after resolving non-combat nodes
            UnityEngine.UI.Image toastBg = UguiThemeFactory.CreatePanel(
                "AvatarToast",
                parent,
                new Color(0.07f, 0.11f, 0.17f, 0.96f),
                true);
            toastBg.raycastTarget = false;
            _travelToastRoot = toastBg.gameObject;
            _travelToastRt = toastBg.rectTransform;
            _travelToastRt.sizeDelta = new Vector2(380f, 58f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _travelToastRt, UguiThemeFactory.AccentGold);

            _travelToastText = UguiThemeFactory.CreateText(
                "ToastText",
                _travelToastRt,
                string.Empty,
                13,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _travelToastText.resizeTextForBestFit = true;
            _travelToastText.resizeTextMinSize = 10;
            _travelToastText.resizeTextMaxSize = 13;
            _travelToastText.verticalOverflow = VerticalWrapMode.Truncate;
            StretchFull(_travelToastText.rectTransform, 12f, 6f);
            _travelToastRoot.SetActive(false);
        }

        private void BuildBottomMiniRosterStrip(RectTransform root)
        {
            RectTransform stripRt = UguiThemeFactory.CreateRect("BottomLeftMiniRosterStrip", root);
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(0f, 0f);
            stripRt.pivot = new Vector2(0f, 0f);
            stripRt.sizeDelta = new Vector2(1040f, 54f);
            stripRt.anchoredPosition = new Vector2(18f, 14f);

            for (int i = 0; i < MaxMiniRosterPills; i++)
            {
                MiniRosterPill pill = new MiniRosterPill();
                UnityEngine.UI.Image bg = UguiThemeFactory.CreatePanel(
                    $"MiniPill_{i}",
                    stripRt,
                    new Color(0.07f, 0.10f, 0.15f, 0.92f),
                    true);
                bg.raycastTarget = false;
                RectTransform pRt = bg.rectTransform;
                pRt.anchorMin = new Vector2(0f, 0f);
                pRt.anchorMax = new Vector2(0f, 0f);
                pRt.pivot = new Vector2(0f, 0f);
                pRt.sizeDelta = new Vector2(112f, 44f);
                pRt.anchoredPosition = new Vector2(i * 120f, 0f);
                UguiThemeFactory.CreateOutlineFrame("Outline", pRt, new Color(0.13f, 0.83f, 0.93f, 0.45f));

                pill.Root = bg.gameObject;
                pill.NameText = UguiThemeFactory.CreateText(
                    "Name",
                    pRt,
                    string.Empty,
                    12,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                pill.NameText.resizeTextForBestFit = true;
                pill.NameText.resizeTextMinSize = 9;
                pill.NameText.resizeTextMaxSize = 12;
                pill.NameText.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform nameRt = pill.NameText.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0.42f);
                nameRt.anchorMax = new Vector2(1f, 1f);
                nameRt.offsetMin = new Vector2(6f, 0f);
                nameRt.offsetMax = new Vector2(-6f, -3f);

                UnityEngine.UI.Image hpTrack = UguiThemeFactory.CreatePanel("HpTrack", pRt, new Color(0.04f, 0.06f, 0.10f, 1f), false);
                hpTrack.raycastTarget = false;
                RectTransform trackRt = hpTrack.rectTransform;
                trackRt.anchorMin = new Vector2(0f, 0f);
                trackRt.anchorMax = new Vector2(1f, 0f);
                trackRt.pivot = new Vector2(0.5f, 0f);
                trackRt.offsetMin = new Vector2(8f, 6f);
                trackRt.offsetMax = new Vector2(-8f, 14f);

                UnityEngine.UI.Image hpFill = UguiThemeFactory.CreatePanel("HpFill", trackRt, UguiThemeFactory.HpGreen, false);
                hpFill.raycastTarget = false;
                pill.HpFillRect = hpFill.rectTransform;
                StretchFull(pill.HpFillRect, 0f, 0f);

                pill.Root.SetActive(false);
                _miniRosterPills[i] = pill;
            }

            UnityEngine.UI.Image statusBg = UguiThemeFactory.CreatePanel(
                "MiniStatusBox",
                stripRt,
                new Color(0.07f, 0.10f, 0.15f, 0.92f),
                true);
            statusBg.raycastTarget = false;
            RectTransform stRt = statusBg.rectTransform;
            stRt.anchorMin = new Vector2(0f, 0f);
            stRt.anchorMax = new Vector2(0f, 0f);
            stRt.pivot = new Vector2(0f, 0f);
            stRt.sizeDelta = new Vector2(420f, 44f);
            stRt.anchoredPosition = new Vector2(MaxMiniRosterPills * 120f + 4f, 0f);
            UguiThemeFactory.CreateOutlineFrame("Outline", stRt, new Color(0.96f, 0.62f, 0.04f, 0.42f));

            _miniStatusText = UguiThemeFactory.CreateText(
                "MiniStatusText",
                stRt,
                string.Empty,
                12,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _miniStatusText.resizeTextForBestFit = true;
            _miniStatusText.resizeTextMinSize = 10;
            _miniStatusText.resizeTextMaxSize = 12;
            _miniStatusText.verticalOverflow = VerticalWrapMode.Truncate;
            StretchFull(_miniStatusText.rectTransform, 10f, 4f);
        }

        private void BuildTooltipCard(RectTransform root)
        {
            UnityEngine.UI.Image panel = UguiThemeFactory.CreatePanel(
                "FloatingTooltipCard",
                root,
                new Color(0.06f, 0.08f, 0.13f, 0.97f),
                true);
            panel.raycastTarget = false;
            _tooltipRoot = panel.gameObject;
            _tooltipRt = panel.rectTransform;
            _tooltipRt.anchorMin = new Vector2(0.5f, 0.5f);
            _tooltipRt.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRt.pivot = new Vector2(0.5f, 0f);
            _tooltipRt.sizeDelta = new Vector2(350f, 134f);

            _tooltipOutline = UguiThemeFactory.CreateOutlineFrame("Outline", _tooltipRt, UguiThemeFactory.AllyCyan);

            _tooltipTitleText = UguiThemeFactory.CreateText(
                "Title",
                _tooltipRt,
                string.Empty,
                15,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _tooltipTitleText.resizeTextForBestFit = true;
            _tooltipTitleText.resizeTextMinSize = 11;
            _tooltipTitleText.resizeTextMaxSize = 15;
            _tooltipTitleText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform titleRt = _tooltipTitleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.offsetMin = new Vector2(12f, -34f);
            titleRt.offsetMax = new Vector2(-12f, -8f);

            _tooltipMetaText = UguiThemeFactory.CreateText(
                "Meta",
                _tooltipRt,
                string.Empty,
                12,
                UguiThemeFactory.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            _tooltipMetaText.resizeTextForBestFit = true;
            _tooltipMetaText.resizeTextMinSize = 10;
            _tooltipMetaText.resizeTextMaxSize = 12;
            _tooltipMetaText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform metaRt = _tooltipMetaText.rectTransform;
            metaRt.anchorMin = new Vector2(0f, 0f);
            metaRt.anchorMax = new Vector2(1f, 1f);
            metaRt.offsetMin = new Vector2(12f, 34f);
            metaRt.offsetMax = new Vector2(-12f, -36f);

            _tooltipStatusText = UguiThemeFactory.CreateText(
                "Status",
                _tooltipRt,
                string.Empty,
                12,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            RectTransform statusRt = _tooltipStatusText.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.offsetMin = new Vector2(12f, 8f);
            statusRt.offsetMax = new Vector2(-12f, 30f);

            _tooltipRoot.SetActive(false);
        }

        private void RefreshIfDirty()
        {
            RunState run = sandboxController != null ? sandboxController.CurrentRun : null;
            string statusMsg = sandboxController != null ? sandboxController.LastStatusMessage : string.Empty;

            if (run == null)
            {
                if (_mapDirty || _lastRun != null)
                {
                    _mapDirty = false;
                    _lastRun = null;
                    ClearMapElements();
                    _headerSummaryText.text = "Chưa có dữ liệu Campaign Run";
                    _unlockEndlessButton.gameObject.SetActive(false);
                }
                return;
            }

            int relicCount = run.CollectedRelics != null ? run.CollectedRelics.Count : 0;
            bool structuralDirty = _mapDirty ||
                                   _lastRun != run ||
                                   _lastCurrentNodeId != run.CurrentNodeId ||
                                   _lastFloor != run.CurrentFloor ||
                                   _lastBattlesWon != run.BattlesWon ||
                                   _lastEndlessDepth != run.EndlessFloorDepth ||
                                   _lastRunCompleted != run.IsRunCompleted ||
                                   _lastRelicCount != relicCount;

            if (structuralDirty)
            {
                if (_lastRun != run)
                {
                    HideTravelToast();
                }

                _mapDirty = false;
                _lastRun = run;
                _lastCurrentNodeId = run.CurrentNodeId;
                _lastFloor = run.CurrentFloor;
                _lastBattlesWon = run.BattlesWon;
                _lastEndlessDepth = run.EndlessFloorDepth;
                _lastRunCompleted = run.IsRunCompleted;
                _lastRelicCount = relicCount;
                _lastStatusMessage = statusMsg;

                RebuildWorldMap(run);
            }
            else if (_lastStatusMessage != statusMsg)
            {
                _lastStatusMessage = statusMsg;
                RefreshMiniStripStatus(run, statusMsg);
            }
        }

        private void RebuildWorldMap(RunState run)
        {
            ClearMapElements();

            int relicCount = run.CollectedRelics != null ? run.CollectedRelics.Count : 0;
            _headerSummaryText.text =
                $"Seed: {run.Seed}   •   Tầng: {run.CurrentFloor}   •   Corruption: {run.CorruptionLevel}%   •   Thắng: {run.BattlesWon}   •   Relics: {relicCount}";

            CampaignNode currentNode = run.GetCurrentNode();
            bool canUnlockEndless = run.IsRunCompleted ||
                                    (currentNode != null && currentNode.IsCompleted && currentNode.NextNodeIds.Count == 0);
            _unlockEndlessButton.gameObject.SetActive(canUnlockEndless);

            int maxFloor = 4 + run.EndlessFloorDepth;
            float contentWidth = Mathf.Max(1920f, LeftPadding + maxFloor * FloorSpacing + RightPadding);
            _mapContent.sizeDelta = new Vector2(contentWidth, 0f);
            _panoramaBackdrop.sprite = GetOrCreatePanoramaSprite(Mathf.Abs(run.Seed) % 3);

            // Count nodes per floor to compute clean vertical lane centering
            _floorNodeCounts.Clear();
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode n = run.Nodes[i];
                if (n == null) continue;
                int existingCount;
                _floorNodeCounts.TryGetValue(n.FloorIndex, out existingCount);
                _floorNodeCounts[n.FloorIndex] = existingCount + 1;
            }

            // Compute coordinates for every node
            CampaignNode f0Node = null;
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode n = run.Nodes[i];
                if (n == null) continue;

                if (n.FloorIndex == 0 && f0Node == null)
                {
                    f0Node = n;
                }

                int floorCount = 1;
                _floorNodeCounts.TryGetValue(n.FloorIndex, out floorCount);

                float x = LeftPadding + n.FloorIndex * FloorSpacing;
                float y;
                if (floorCount <= 1)
                {
                    y = Mathf.Sin(n.FloorIndex * 1.4f) * 32f;
                }
                else
                {
                    float wave = Mathf.Sin(n.FloorIndex * 1.1f) * 14f;
                    y = n.LaneIndex == 0 ? 142f + wave : -142f - wave;
                }

                _nodePositions[n.NodeId] = new Vector2(x, y);
            }

            // Always draw Trailhead entrance road before Floor 0
            Vector2? trailheadPos = null;
            if (f0Node != null && _nodePositions.TryGetValue(f0Node.NodeId, out Vector2 f0Pos))
            {
                Vector2 thPos = new Vector2(f0Pos.x - 150f, f0Pos.y - 16f);
                trailheadPos = thPos;
                DrawCurvedRoad(thPos, f0Pos, true, !f0Node.IsCompleted);
            }

            // Draw road curves connecting every node -> NextNodeIds
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode fromNode = run.Nodes[i];
                if (fromNode == null || fromNode.NextNodeIds == null) continue;
                if (!_nodePositions.TryGetValue(fromNode.NodeId, out Vector2 fromPos)) continue;

                for (int k = 0; k < fromNode.NextNodeIds.Count; k++)
                {
                    string nextId = fromNode.NextNodeIds[k];
                    if (string.IsNullOrEmpty(nextId) || !_nodePositions.TryGetValue(nextId, out Vector2 toPos)) continue;

                    CampaignNode toNode = run.GetNode(nextId);
                    bool isTraveled = fromNode.IsCompleted && toNode != null && (toNode.IsCompleted || toNode.NodeId == run.CurrentNodeId);
                    bool isAvailable = fromNode.IsCompleted && fromNode.NodeId == run.CurrentNodeId && toNode != null;
                    DrawCurvedRoad(fromPos, toPos, isTraveled, isAvailable);
                }
            }

            // Spawn landmark markers
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode node = run.Nodes[i];
                if (node == null || !_nodePositions.TryGetValue(node.NodeId, out Vector2 markerPos)) continue;

                bool isCurrent = node.NodeId == run.CurrentNodeId;
                bool isNextAvailable = currentNode != null &&
                                       currentNode.IsCompleted &&
                                       currentNode.NextNodeIds != null &&
                                       currentNode.NextNodeIds.Contains(node.NodeId);
                bool isStartSelectable = node.FloorIndex == 0 && !node.IsCompleted;
                bool canSelect = (isCurrent && !node.IsCompleted) || isNextAvailable || isStartSelectable;
                bool isAvailableHighlight = isNextAvailable || isStartSelectable || (isCurrent && !node.IsCompleted);

                GameObject markerObj = new GameObject($"MapMarker_{node.NodeId}", typeof(RectTransform));
                RectTransform markerRt = markerObj.GetComponent<RectTransform>();
                markerRt.SetParent(_markersLayer, false);
                markerRt.anchorMin = new Vector2(0.5f, 0.5f);
                markerRt.anchorMax = new Vector2(0.5f, 0.5f);
                markerRt.pivot = new Vector2(0.5f, 0.5f);
                markerRt.anchoredPosition = markerPos;

                MapNodeMarkerView marker = markerObj.AddComponent<MapNodeMarkerView>();
                marker.Bind(node, isCurrent, isAvailableHighlight, canSelect);
                marker.SetTravelingLock(_isTraveling);
                marker.OnNodeClicked += HandleMarkerClicked;
                marker.OnNodeHoverChanged += HandleMarkerHoverChanged;
                _spawnedMarkers.Add(marker);
            }

            // Sync PlayerMapAvatar sprite & resting position
            UnitRole leaderRole = ResolveLeaderRole(run);
            _avatarImage.sprite = ProceduralUnitArtFactory.GetUnitSprite(leaderRole, TeamSide.Player, false);

            if (!_isTraveling)
            {
                bool startAtTrailhead = trailheadPos.HasValue &&
                                        currentNode != null &&
                                        currentNode.FloorIndex == 0 &&
                                        !currentNode.IsCompleted;

                if (startAtTrailhead)
                {
                    _avatarBasePos = trailheadPos.Value;
                }
                else if (currentNode != null && _nodePositions.TryGetValue(currentNode.NodeId, out Vector2 currPos))
                {
                    _avatarBasePos = currPos;
                }
                else if (trailheadPos.HasValue)
                {
                    _avatarBasePos = trailheadPos.Value;
                }
                else if (f0Node != null && _nodePositions.TryGetValue(f0Node.NodeId, out Vector2 fallbackF0))
                {
                    _avatarBasePos = fallbackF0;
                }
                else
                {
                    _avatarBasePos = new Vector2(LeftPadding, 0f);
                }

                _avatarRoot.anchoredPosition = _avatarBasePos;
            }

            // Auto-scroll horizontally to keep player / latest endless floors in view
            if (contentWidth > 1920f)
            {
                float norm = Mathf.Clamp01((_avatarBasePos.x - 420f) / Mathf.Max(1f, contentWidth - 1920f));
                _scrollRect.horizontalNormalizedPosition = norm;
            }
            else
            {
                _scrollRect.horizontalNormalizedPosition = 0f;
            }

            RefreshMiniRosterPills(run);
            RefreshMiniStripStatus(run, sandboxController.LastStatusMessage);
        }

        private void DrawCurvedRoad(Vector2 a, Vector2 b, bool isTraveled, bool isAvailable)
        {
            Color borderColor = new Color(0.08f, 0.07f, 0.09f, 0.90f);
            Color surfaceColor = isTraveled
                ? new Color(0.60f, 0.53f, 0.41f, 0.95f)
                : (isAvailable
                    ? new Color(0.45f, 0.64f, 0.66f, 0.95f)
                    : new Color(0.24f, 0.27f, 0.32f, 0.78f));

            Vector2 prev = EvaluateRoadCurve(a, b, 0f);
            for (int s = 1; s <= RoadSegmentCount; s++)
            {
                float t = (float)s / RoadSegmentCount;
                Vector2 next = EvaluateRoadCurve(a, b, t);
                Vector2 mid = (prev + next) * 0.5f;
                Vector2 delta = next - prev;
                float length = delta.magnitude;
                float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                // Dark earth border segment
                UnityEngine.UI.Image borderSeg = UguiThemeFactory.CreatePanel("RoadBorderSeg", _roadBordersLayer, borderColor, true);
                borderSeg.raycastTarget = false;
                RectTransform bRt = borderSeg.rectTransform;
                bRt.anchorMin = new Vector2(0.5f, 0.5f);
                bRt.anchorMax = new Vector2(0.5f, 0.5f);
                bRt.sizeDelta = new Vector2(length + 10f, 34f);
                bRt.anchoredPosition = mid;
                bRt.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                _spawnedRoadObjects.Add(borderSeg.gameObject);

                // Warm stone surface segment
                UnityEngine.UI.Image surfSeg = UguiThemeFactory.CreatePanel("RoadSurfSeg", _roadSurfaceLayer, surfaceColor, true);
                surfSeg.raycastTarget = false;
                RectTransform sRt = surfSeg.rectTransform;
                sRt.anchorMin = new Vector2(0.5f, 0.5f);
                sRt.anchorMax = new Vector2(0.5f, 0.5f);
                sRt.sizeDelta = new Vector2(length + 6f, 22f);
                sRt.anchoredPosition = mid;
                sRt.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                _spawnedRoadObjects.Add(surfSeg.gameObject);

                prev = next;
            }
        }

        private static Vector2 EvaluateRoadCurve(Vector2 a, Vector2 b, float t)
        {
            return new Vector2(
                Mathf.Lerp(a.x, b.x, t),
                Mathf.Lerp(a.y, b.y, Mathf.SmoothStep(0f, 1f, t)));
        }

        private void HandleMarkerClicked(CampaignNode targetNode)
        {
            if (_isTraveling || targetNode == null || sandboxController == null || sandboxController.CurrentRun == null)
            {
                return;
            }
            if (!_nodePositions.TryGetValue(targetNode.NodeId, out Vector2 targetPos))
            {
                return;
            }

            PlayClickSfx();
            HideTooltip();

            bool isShortHop = Vector2.Distance(_avatarBasePos, targetPos) < 12f;

            _isTraveling = true;
            _travelElapsed = 0f;
            _activeTravelDuration = isShortHop ? ShortHopDuration : NormalTravelDuration;
            _travelStartPos = _avatarBasePos;
            _travelEndPos = targetPos;
            _pendingTravelNode = targetNode;

            SetAllMarkersTravelingLock(true);
        }

        private void UpdateAvatarMotion()
        {
            if (_avatarRoot == null) return;

            if (_isTraveling)
            {
                _travelElapsed += Time.unscaledDeltaTime;
                float progress = _activeTravelDuration > 0.001f
                    ? Mathf.Clamp01(_travelElapsed / _activeTravelDuration)
                    : 1f;

                Vector2 curvePos = EvaluateRoadCurve(_travelStartPos, _travelEndPos, progress);
                float walkBob = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 5f)) * 9f;
                float tiltDeg = Mathf.Sin(progress * Mathf.PI * 5f) * 6f;

                _avatarRoot.anchoredPosition = new Vector2(curvePos.x, curvePos.y + walkBob);
                if (_avatarBodyRt != null)
                {
                    _avatarBodyRt.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
                }

                if (progress >= 1f)
                {
                    CompleteTravel();
                }
            }
            else
            {
                float breathY = Mathf.Sin(Time.unscaledTime * 3.2f) * 3.5f;
                _avatarRoot.anchoredPosition = new Vector2(_avatarBasePos.x, _avatarBasePos.y + breathY);
                if (_avatarBodyRt != null)
                {
                    _avatarBodyRt.localRotation = Quaternion.identity;
                }
            }
        }

        private void CompleteTravel()
        {
            CampaignNode arrivedNode = _pendingTravelNode;
            _avatarBasePos = _travelEndPos;
            _avatarRoot.anchoredPosition = _avatarBasePos;
            if (_avatarBodyRt != null)
            {
                _avatarBodyRt.localRotation = Quaternion.identity;
            }

            _isTraveling = false;
            _pendingTravelNode = null;
            SetAllMarkersTravelingLock(false);

            if (arrivedNode == null || sandboxController == null) return;

            bool isNonCombat = arrivedNode.NodeType == CampaignNodeType.Recovery ||
                               arrivedNode.NodeType == CampaignNodeType.Shop ||
                               arrivedNode.NodeType == CampaignNodeType.Event;

            sandboxController.SelectCampaignNode(arrivedNode);
            _mapDirty = true;

            if (isNonCombat && !string.IsNullOrEmpty(sandboxController.LastStatusMessage))
            {
                ShowTravelToast(sandboxController.LastStatusMessage, _avatarBasePos);
            }
        }

        private void CancelPendingTravel()
        {
            _isTraveling = false;
            _pendingTravelNode = null;
            SetAllMarkersTravelingLock(false);
            if (_avatarBodyRt != null)
            {
                _avatarBodyRt.localRotation = Quaternion.identity;
            }
        }

        private void ShowTravelToast(string message, Vector2 avatarPos)
        {
            if (_travelToastRoot == null || _travelToastText == null) return;
            _travelToastText.text = message;
            _travelToastRt.anchoredPosition = new Vector2(avatarPos.x, avatarPos.y + 92f);
            _travelToastRoot.SetActive(true);
            _travelToastTimer = 3.0f;
        }

        private void HideTravelToast()
        {
            _travelToastTimer = 0f;
            if (_travelToastRoot != null && _travelToastRoot.activeSelf)
            {
                _travelToastRoot.SetActive(false);
            }
        }

        private void UpdateTravelToast()
        {
            if (_travelToastTimer <= 0f) return;
            _travelToastTimer -= Time.unscaledDeltaTime;
            if (_travelToastTimer <= 0f && _travelToastRoot != null)
            {
                _travelToastRoot.SetActive(false);
            }
        }

        private void SetAllMarkersTravelingLock(bool locked)
        {
            for (int i = 0; i < _spawnedMarkers.Count; i++)
            {
                if (_spawnedMarkers[i] != null)
                {
                    _spawnedMarkers[i].SetTravelingLock(locked);
                }
            }
        }

        private void HandleMarkerHoverChanged(CampaignNode node, RectTransform markerRect, bool isEntering)
        {
            if (!isEntering ||
                node == null ||
                markerRect == null ||
                _isTraveling ||
                (sandboxController != null && sandboxController.IsAnyModalOpen))
            {
                HideTooltip();
                return;
            }

            RunState run = sandboxController != null ? sandboxController.CurrentRun : null;
            CampaignNode currentNode = run != null ? run.GetCurrentNode() : null;
            bool isCurrent = run != null && node.NodeId == run.CurrentNodeId;
            bool isNextAvailable = currentNode != null &&
                                   currentNode.IsCompleted &&
                                   currentNode.NextNodeIds != null &&
                                   currentNode.NextNodeIds.Contains(node.NodeId);
            bool isStartSelectable = node.FloorIndex == 0 && !node.IsCompleted;

            string statusLabel;
            Color statusColor;
            if (isCurrent && !node.IsCompleted)
            {
                statusLabel = "Đang đứng tại đây — Click để thực hiện";
                statusColor = UguiThemeFactory.AccentGold;
            }
            else if (node.IsCompleted)
            {
                statusLabel = "Đã hoàn thành";
                statusColor = UguiThemeFactory.HpGreen;
            }
            else if (isNextAvailable || isStartSelectable)
            {
                statusLabel = "Có thể tiến tới";
                statusColor = UguiThemeFactory.AllyCyan;
            }
            else
            {
                statusLabel = "Chưa mở khóa";
                statusColor = UguiThemeFactory.TextMuted;
            }

            _tooltipTitleText.text = $"{node.Title}  [{node.NodeId}]";
            string modPart = node.Modifier != WorldModifierType.None ? $"\nBiến số: {node.Modifier}" : string.Empty;
            _tooltipMetaText.text = $"Loại: {node.NodeType}   •   Biome: {node.Biome}\nNhiệm vụ: {node.Mission}{modPart}";
            _tooltipStatusText.text = statusLabel;
            _tooltipStatusText.color = statusColor;
            _tooltipOutline.color = statusColor;

            // Compute position above hovered marker in Canvas coordinates & clamp within screen bounds
            markerRect.GetWorldCorners(_worldCornersBuffer);
            Vector3 topCenterWorld = (_worldCornersBuffer[1] + _worldCornersBuffer[2]) * 0.5f;
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                RectTransformUtility.WorldToScreenPoint(null, topCenterWorld),
                null,
                out localPoint);

            float halfCanvasW = _canvasRect.rect.width * 0.5f;
            float halfCanvasH = _canvasRect.rect.height * 0.5f;
            float clampedX = Mathf.Clamp(localPoint.x, -halfCanvasW + 190f, halfCanvasW - 190f);
            float clampedY = Mathf.Clamp(localPoint.y + 12f, -halfCanvasH + 90f, halfCanvasH - 216f);

            _tooltipRt.anchoredPosition = new Vector2(clampedX, clampedY);
            _tooltipRoot.SetActive(true);
        }

        private void HideTooltip()
        {
            if (_tooltipRoot != null && _tooltipRoot.activeSelf)
            {
                _tooltipRoot.SetActive(false);
            }
        }

        private void RefreshMiniRosterPills(RunState run)
        {
            int count = (run != null && run.PlayerRoster != null) ? Mathf.Min(run.PlayerRoster.Count, MaxMiniRosterPills) : 0;
            for (int i = 0; i < MaxMiniRosterPills; i++)
            {
                MiniRosterPill pill = _miniRosterPills[i];
                bool active = i < count;
                pill.Root.SetActive(active);
                if (!active) continue;

                UnitState u = run.PlayerRoster[i];
                pill.NameText.text = $"{u.Name} ({u.CurrentHP})";
                float ratio = u.MaxHP > 0 ? Mathf.Clamp01((float)u.CurrentHP / u.MaxHP) : 0f;
                pill.HpFillRect.anchorMax = new Vector2(ratio, 1f);
            }
        }

        private void RefreshMiniStripStatus(RunState run, string statusMsg)
        {
            if (_miniStatusText == null) return;
            int relicCount = (run != null && run.CollectedRelics != null) ? run.CollectedRelics.Count : 0;
            string cleanMsg = string.IsNullOrEmpty(statusMsg) ? "Click vào Node phát sáng để tiến quân" : statusMsg;
            _miniStatusText.text = $"Relics: {relicCount}  |  {cleanMsg}";
        }

        private void ClearMapElements()
        {
            for (int i = 0; i < _spawnedMarkers.Count; i++)
            {
                MapNodeMarkerView marker = _spawnedMarkers[i];
                if (marker != null)
                {
                    marker.OnNodeClicked -= HandleMarkerClicked;
                    marker.OnNodeHoverChanged -= HandleMarkerHoverChanged;
                    Destroy(marker.gameObject);
                }
            }
            _spawnedMarkers.Clear();

            for (int i = 0; i < _spawnedRoadObjects.Count; i++)
            {
                if (_spawnedRoadObjects[i] != null)
                {
                    Destroy(_spawnedRoadObjects[i]);
                }
            }
            _spawnedRoadObjects.Clear();
            _nodePositions.Clear();
        }

        private static UnitRole ResolveLeaderRole(RunState run)
        {
            if (run != null && run.PlayerRoster != null)
            {
                for (int i = 0; i < run.PlayerRoster.Count; i++)
                {
                    UnitState u = run.PlayerRoster[i];
                    if (u != null && u.IsAlive) return u.Role;
                }
                if (run.PlayerRoster.Count > 0 && run.PlayerRoster[0] != null)
                {
                    return run.PlayerRoster[0].Role;
                }
            }
            return UnitRole.Guardian;
        }

        private static Sprite GetOrCreatePanoramaSprite(int variant)
        {
            if (_panoramaCache.TryGetValue(variant, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = new Texture2D(PanoramaWidth, PanoramaHeight, TextureFormat.RGBA32, false);
            tex.name = $"WorldMapPanoramaTex_{variant}";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[PanoramaWidth * PanoramaHeight];
            Color citySkyTop = new Color32(0x11, 0x16, 0x20, 255);
            Color cityHorizon = new Color32(0x28, 0x35, 0x48, 255);
            Color indSkyTop = new Color32(0x1E, 0x14, 0x10, 255);
            Color indHorizon = new Color32(0x4E, 0x2D, 0x19, 255);
            Color labSkyTop = new Color32(0x0E, 0x1A, 0x1C, 255);
            Color labHorizon = new Color32(0x22, 0x46, 0x48, 255);

            Color cityGround = new Color32(0x16, 0x1E, 0x25, 255);
            Color indGround = new Color32(0x21, 0x18, 0x13, 255);
            Color labGround = new Color32(0x13, 0x22, 0x22, 255);

            float phaseOffset = variant * 1.15f;
            const float horizonY = 0.68f; // Upper ~32% sky, lower ~68% terrain

            for (int y = 0; y < PanoramaHeight; y++)
            {
                float ny = (y + 0.5f) / PanoramaHeight;
                for (int x = 0; x < PanoramaWidth; x++)
                {
                    float nx = (x + 0.5f) / PanoramaWidth;
                    Color skyTop = SampleRegionTripletColor(nx, citySkyTop, indSkyTop, labSkyTop);
                    Color skyHor = SampleRegionTripletColor(nx, cityHorizon, indHorizon, labHorizon);
                    Color gndBase = SampleRegionTripletColor(nx, cityGround, indGround, labGround);

                    Color px;
                    if (ny >= horizonY)
                    {
                        float skyT = (ny - horizonY) / (1f - horizonY);
                        px = Color.Lerp(skyHor, skyTop, Mathf.SmoothStep(0f, 1f, skyT));

                        // Mountains & biome structures along horizon
                        float mountain = horizonY + 0.06f + 0.045f * Mathf.Sin(nx * 14f + phaseOffset) + 0.025f * Mathf.Cos(nx * 31f);
                        bool landmarkSpire = (nx > 0.12f && nx < 0.16f && ny < 0.80f) ||
                                             (nx > 0.48f && nx < 0.52f && ny < 0.82f) ||
                                             (nx > 0.84f && nx < 0.89f && ny < 0.81f);
                        if (ny <= mountain || landmarkSpire)
                        {
                            Color sil = Color.Lerp(skyTop, new Color(0.06f, 0.08f, 0.12f, 1f), 0.65f);
                            px = Color.Lerp(px, sil, 0.88f);
                        }
                    }
                    else
                    {
                        float depthT = ny / horizonY;
                        px = Color.Lerp(new Color(gndBase.r * 0.72f, gndBase.g * 0.72f, gndBase.b * 0.72f, 1f), gndBase, depthT);

                        // Gentle terrain contour patches & mossy/stone clusters
                        float terrainWave = Mathf.Sin(nx * 22f + phaseOffset) * Mathf.Cos(ny * 16f);
                        if (terrainWave > 0.35f)
                        {
                            px = Color.Lerp(px, new Color(gndBase.r * 1.22f, gndBase.g * 1.25f, gndBase.b * 1.20f, 1f), 0.30f);
                        }
                        else if (terrainWave < -0.55f)
                        {
                            px = Color.Lerp(px, new Color(0.06f, 0.08f, 0.10f, 1f), 0.25f);
                        }
                    }

                    // Soft edge vignette top & bottom
                    float vertEdge = Mathf.Abs(ny - 0.50f) * 2f;
                    float vig = 1f - Mathf.Clamp01(vertEdge - 0.68f) * 0.45f;
                    px.r *= vig;
                    px.g *= vig;
                    px.b *= vig;
                    px.a = 1f;
                    pixels[y * PanoramaWidth + x] = px;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, PanoramaWidth, PanoramaHeight), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"WorldMapPanoramaSprite_{variant}";
            _panoramaCache[variant] = sprite;
            return sprite;
        }

        private static Color SampleRegionTripletColor(float nx, Color leftCol, Color midCol, Color rightCol)
        {
            if (nx < 0.5f)
            {
                return Color.Lerp(leftCol, midCol, Mathf.SmoothStep(0f, 1f, nx * 2f));
            }
            return Color.Lerp(midCol, rightCol, Mathf.SmoothStep(0f, 1f, (nx - 0.5f) * 2f));
        }

        private void OnClickOpenPolicy()
        {
            PlayClickSfx();
            EnsureReferences();
            modalsView?.OpenPolicyModal(null);
        }

        private void OnClickSaveJson()
        {
            PlayClickSfx();
            EnsureReferences();
            sandboxController?.SaveRunToJson();
        }

        private void OnClickUnlockEndless()
        {
            PlayClickSfx();
            EnsureReferences();
            sandboxController?.UnlockNextEndlessFloor();
            _mapDirty = true;
        }

        private void OnClickOpenSettings()
        {
            EnsureReferences();
            mainMenuAndSettingsView?.OpenSettings();
        }

        private void PlayClickSfx()
        {
            if (sandboxController != null && sandboxController.AudioManager != null)
            {
                sandboxController.AudioManager.PlayUiClick();
            }
        }

        private static UnityEngine.UI.Button CreateHeaderButton(
            string name,
            Transform parent,
            string label,
            float width,
            Color bgColor,
            Color borderColor)
        {
            UnityEngine.UI.Button btn = UguiThemeFactory.CreateButton(name, parent, label, bgColor, 14);
            UguiThemeFactory.CreateOutlineFrame("Outline", btn.transform, borderColor);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 38f);
            return btn;
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
