using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Roguelite;
using CrownlessWarband.Presentation.BattleView;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrownlessWarband.Presentation.UI
{
    public class CampaignWorldMapView : MonoBehaviour
    {
        private const int MaxMiniRosterPills = 5;
        private const float NormalTravelStepDuration = 0.085f;
        private const float ShortHopDuration = 0.16f;

        // Unity 2D Tilemap Fixed Viewport Grid (Method A: Sector Paging, 26 x 14 tiles)
        private const float TileCellWorldSize = 0.8f;
        private const float TilePixelsPerUnit = 80f;
        private const int GridMinX = -13;
        private const int GridMaxX = 12;
        private const int GridMinY = -7;
        private const int GridMaxY = 6;
        private const int GridWidth = GridMaxX - GridMinX + 1;
        private const int GridHeight = GridMaxY - GridMinY + 1;

        private const string TileResourcesRoot = "UI/CampaignMap/Tiles/";
        private const string TrailheadNodeKey = "__TRAILHEAD__";

        private enum TerrainCellKind
        {
            Grass = 0,
            FlowerStone = 1,
            Water = 2,
            WaterShore = 3,
            Road = 4
        }

        private enum DecorPropKind
        {
            None = 0,
            Tree = 1,
            Rock = 2,
            BushSign = 3,
            Pillar = 4,
            Stump = 5,
            Campfire = 6,
            WarBanner = 7,
            SupplyCart = 8,
            BrokenCrown = 9,
            RuneStone = 10,
            RuinWall = 11,
            Reeds = 12
        }

        private enum MapStructureTemplate
        {
            FrontierForestValley = 0,
            TwinRiversCrossing = 1,
            SunkenAncientRuins = 2,
            CitadelCausewayIsles = 3
        }

        private enum RoadVisualState
        {
            Locked = 0,
            Traveled = 1,
            Available = 2
        }

        private sealed class MiniRosterPill
        {
            public GameObject Root;
            public UnityEngine.UI.Image RoleBadge;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI HpValueText;
            public RectTransform HpFillRect;
        }

        private sealed class MarkerCellBinding
        {
            public MapNodeMarkerView Marker;
            public Vector2Int Cell;
        }

        // Shared pixel-art tiles loaded from Resources/UI/CampaignMap/Tiles
        private static Tile _tileGrass;
        private static Tile _tileFlowerStone;
        private static Tile _tileCobbleRoad;
        private static readonly Tile[] _tileCobbleRoadVariants = new Tile[4];
        private static Tile _tileWater;
        private static Tile _tileWaterShore;
        private static Tile _tilePropTree;
        private static Tile _tilePropRock;
        private static Tile _tilePropBushSign;
        private static Tile _tilePropPillar;
        private static Tile _tilePropStump;
        private static Tile _tilePropCampfire;
        private static Tile _tilePropWarBanner;
        private static Tile _tilePropSupplyCart;
        private static Tile _tilePropBrokenCrown;
        private static Tile _tilePropRuneStone;
        private static Tile _tilePropRuinWall;
        private static Tile _tilePropReeds;
        private static bool _tilesInitialized;

        // Runtime cached autotile overhang, shoreline & wooden bridge overlays (Top-Down Glades style)
        private static readonly Dictionary<int, Tile> _roadFringeTileCache = new Dictionary<int, Tile>(64);
        private static readonly Dictionary<int, Tile> _shorelineTileCache = new Dictionary<int, Tile>(128);
        private static readonly Dictionary<int, Tile> _bridgeTileCache = new Dictionary<int, Tile>(32);
        private static Color32 _sampledGrassBaseColor = new Color32(167, 202, 76, 255);

        [SerializeField] private BattleSandboxController sandboxController;
        [SerializeField] private BattleModalsView modalsView;
        [SerializeField] private MainMenuAndSettingsView mainMenuAndSettingsView;

        private Canvas _worldMapCanvas;
        private RectTransform _canvasRect;

        // World-Space Unity 2D Tilemap Hierarchy
        private GameObject _tilemapGridRoot;
        private Grid _worldGrid;
        private Tilemap _groundTilemap;
        private Tilemap _roadTilemap;
        private Tilemap _fringeTilemap;
        private Tilemap _decorTilemap;

        // Top Header Bar
        private TextMeshProUGUI _headerSummaryText;
        private UnityEngine.UI.Button _policyButton;
        private UnityEngine.UI.Button _saveButton;
        private UnityEngine.UI.Button _unlockEndlessButton;
        private UnityEngine.UI.Button _settingsButton;

        // Screen-space Node Overlay Layer above Tilemap
        private RectTransform _mapContent;
        private RectTransform _markersLayer;
        private RectTransform _avatarLayer;

        // Player Avatar & Toast
        private RectTransform _avatarRoot;
        private RectTransform _avatarBodyRt;
        private UnityEngine.UI.Image _avatarImage;
        private GameObject _travelToastRoot;
        private RectTransform _travelToastRt;
        private TextMeshProUGUI _travelToastText;
        private float _travelToastTimer;

        // Mini Roster Strip Bottom-Left (Footer without right-side status box)
        private readonly MiniRosterPill[] _miniRosterPills = new MiniRosterPill[MaxMiniRosterPills];

        // Floating Hover Tooltip Card
        private GameObject _tooltipRoot;
        private RectTransform _tooltipRt;
        private UnityEngine.UI.Image _tooltipOutline;
        private TextMeshProUGUI _tooltipTitleText;
        private TextMeshProUGUI _tooltipMetaText;
        private TextMeshProUGUI _tooltipStatusText;

        // Sector Paging & Tile Grid Buffers (Zero-GC)
        private readonly TerrainCellKind[,] _terrainGrid = new TerrainCellKind[GridWidth, GridHeight];
        private readonly TerrainCellKind[,] _underRoadTerrainGrid = new TerrainCellKind[GridWidth, GridHeight];
        private readonly DecorPropKind[,] _decorGrid = new DecorPropKind[GridWidth, GridHeight];
        private readonly RoadVisualState[,] _roadStateGrid = new RoadVisualState[GridWidth, GridHeight];
        private readonly bool[,] _nodeReservedMask = new bool[GridWidth, GridHeight];
        private readonly bool[,] _roadTurnCells = new bool[GridWidth, GridHeight];

        private readonly List<MapNodeMarkerView> _spawnedMarkers = new List<MapNodeMarkerView>(16);
        private readonly List<MarkerCellBinding> _markerBindings = new List<MarkerCellBinding>(16);
        private readonly List<CampaignNode> _sectorNodesBuffer = new List<CampaignNode>(16);
        private readonly Dictionary<string, Vector2Int> _nodeGridCells = new Dictionary<string, Vector2Int>(24);
        private readonly Dictionary<string, List<Vector2Int>> _roadPathsByEdgeKey = new Dictionary<string, List<Vector2Int>>(32);
        private readonly List<Vector2Int> _activeTravelCells = new List<Vector2Int>(32);
        private readonly Dictionary<int, int> _floorNodeCounts = new Dictionary<int, int>(8);

        // Travel interpolation state
        private bool _isTraveling;
        private float _travelElapsed;
        private float _activeTravelDuration = 0.5f;
        private Vector2Int _avatarCurrentCell;
        private Vector2 _avatarBasePos;
        private Vector2Int _travelTargetCell;
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
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private readonly Vector3[] _worldCornersBuffer = new Vector3[4];

        private static readonly Color BtnActiveColor = new Color(0.14f, 0.52f, 0.62f, 1f);
        private static readonly Color BtnGreenColor = new Color(0.14f, 0.48f, 0.32f, 1f);
        private static readonly Color BtnGoldColor = new Color(0.58f, 0.40f, 0.08f, 1f);

        private static readonly Color RoadLockedTint = new Color(0.94f, 0.94f, 0.94f, 1f);
        private static readonly Color RoadTraveledTint = Color.white;
        private static readonly Color RoadAvailableTint = new Color(1.0f, 0.98f, 0.84f, 1f);

        private void Awake()
        {
            EnsureReferences();
            EnsureTilesLoaded();
            BuildWorldTilemapGrid();
            BuildWorldMapCanvas();
            SetMapActive(false);
        }

        private void Update()
        {
            EnsureReferences();

            bool shouldShow = sandboxController != null &&
                              sandboxController.ScreenMode == SandboxScreenMode.CampaignMap &&
                              !sandboxController.IsDebugMode;

            bool canvasActive = _worldMapCanvas != null && _worldMapCanvas.gameObject.activeSelf;
            bool gridActive = _tilemapGridRoot != null && _tilemapGridRoot.activeSelf;
            if (canvasActive != shouldShow || gridActive != shouldShow)
            {
                SetMapActive(shouldShow);
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
            SyncCanvasPositionsIfScreenChanged();
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

            if (_tilemapGridRoot != null)
            {
                Destroy(_tilemapGridRoot);
                _tilemapGridRoot = null;
            }
            _worldGrid = null;
            _groundTilemap = null;
            _roadTilemap = null;
            _fringeTilemap = null;
            _decorTilemap = null;

            if (_worldMapCanvas != null)
            {
                Destroy(_worldMapCanvas.gameObject);
                _worldMapCanvas = null;
            }
        }

        private void SetMapActive(bool active)
        {
            if (_tilemapGridRoot != null && _tilemapGridRoot.activeSelf != active)
            {
                _tilemapGridRoot.SetActive(active);
            }
            if (_worldMapCanvas != null && _worldMapCanvas.gameObject.activeSelf != active)
            {
                _worldMapCanvas.gameObject.SetActive(active);
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

        private static void EnsureTilesLoaded()
        {
            if (_tilesInitialized && _tileGrass != null && _tileCobbleRoad != null)
            {
                return;
            }

            _tileGrass = LoadPixelTile("Tile_Grass", new Vector2(0.5f, 0.5f));
            _tileFlowerStone = LoadPixelTile("Tile_FlowerStoneGrass", new Vector2(0.5f, 0.5f));
            _tileCobbleRoad = LoadPixelTile("Tile_CobbleRoad", new Vector2(0.5f, 0.5f));
            _tileCobbleRoadVariants[0] = _tileCobbleRoad;
            _tileCobbleRoadVariants[1] = LoadPixelTileOptional("Tile_CobbleRoad_1", new Vector2(0.5f, 0.5f)) ?? _tileCobbleRoad;
            _tileCobbleRoadVariants[2] = LoadPixelTileOptional("Tile_CobbleRoad_2", new Vector2(0.5f, 0.5f)) ?? _tileCobbleRoad;
            _tileCobbleRoadVariants[3] = LoadPixelTileOptional("Tile_CobbleRoad_3", new Vector2(0.5f, 0.5f)) ?? _tileCobbleRoad;

            if (_tileGrass != null && _tileGrass.sprite != null && _tileGrass.sprite.texture != null && _tileGrass.sprite.texture.isReadable)
            {
                Texture2D gTex = _tileGrass.sprite.texture;
                Color c = gTex.GetPixel(gTex.width / 2, gTex.height / 2);
                _sampledGrassBaseColor = new Color32(
                    (byte)Mathf.RoundToInt(c.r * 255f),
                    (byte)Mathf.RoundToInt(c.g * 255f),
                    (byte)Mathf.RoundToInt(c.b * 255f),
                    255);
            }

            _tileWater = LoadPixelTile("Tile_Water", new Vector2(0.5f, 0.5f));
            _tileWaterShore = LoadPixelTile("Tile_WaterShore", new Vector2(0.5f, 0.5f));
            _tilePropTree = LoadPixelTile("Prop_Tree", new Vector2(0.5f, 0.35f));
            _tilePropRock = LoadPixelTile("Prop_Rock", new Vector2(0.5f, 0.5f));
            _tilePropBushSign = LoadPixelTile("Prop_BushSign", new Vector2(0.5f, 0.5f));
            _tilePropPillar = LoadPixelTile("Prop_Pillar", new Vector2(0.5f, 0.35f));
            _tilePropStump = LoadPixelTile("Prop_Stump", new Vector2(0.5f, 0.50f));
            _tilePropCampfire = LoadPixelTile("Prop_Campfire", new Vector2(0.5f, 0.45f));
            _tilePropWarBanner = LoadPixelTile("Prop_WarBanner", new Vector2(0.5f, 0.38f));
            _tilePropSupplyCart = LoadPixelTile("Prop_SupplyCart", new Vector2(0.5f, 0.45f));
            _tilePropBrokenCrown = LoadPixelTile("Prop_BrokenCrown", new Vector2(0.5f, 0.42f));
            _tilePropRuneStone = LoadPixelTile("Prop_RuneStone", new Vector2(0.5f, 0.36f));
            _tilePropRuinWall = LoadPixelTile("Prop_RuinWall", new Vector2(0.5f, 0.42f));
            _tilePropReeds = LoadPixelTile("Prop_Reeds", new Vector2(0.5f, 0.40f));
            _tilesInitialized = true;
        }

        private static Tile LoadPixelTileOptional(string resourceName, Vector2 pivot)
        {
            Texture2D tex = Resources.Load<Texture2D>(TileResourcesRoot + resourceName);
            if (tex == null) return null;

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, TilePixelsPerUnit);
            sprite.name = $"TileSprite_{resourceName}";

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"Tile_{resourceName}";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private static Tile LoadPixelTile(string resourceName, Vector2 pivot)
        {
            Texture2D tex = Resources.Load<Texture2D>(TileResourcesRoot + resourceName);
            if (tex == null)
            {
                tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                tex.name = $"Fallback_{resourceName}";
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, TilePixelsPerUnit);
            sprite.name = $"TileSprite_{resourceName}";

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"Tile_{resourceName}";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private void BuildWorldTilemapGrid()
        {
            _tilemapGridRoot = new GameObject("CampaignWorldTilemapGrid");
            _tilemapGridRoot.transform.SetParent(transform, false);
            _tilemapGridRoot.transform.localPosition = Vector3.zero;

            _worldGrid = _tilemapGridRoot.AddComponent<Grid>();
            _worldGrid.cellLayout = GridLayout.CellLayout.Rectangle;
            _worldGrid.cellSize = new Vector3(TileCellWorldSize, TileCellWorldSize, 0f);
            _worldGrid.cellGap = Vector3.zero;

            _groundTilemap = CreateTilemapLayer("EnvironmentGroundTilemap", _tilemapGridRoot.transform, -15);
            _roadTilemap = CreateTilemapLayer("RoadPathTilemap", _tilemapGridRoot.transform, -10);
            _fringeTilemap = CreateTilemapLayer("EdgeFringeTilemap", _tilemapGridRoot.transform, -8);
            _decorTilemap = CreateTilemapLayer("EnvironmentDecorTilemap", _tilemapGridRoot.transform, -5, true);
        }

        private static Tilemap CreateTilemapLayer(
            string layerName,
            Transform parent,
            int sortingOrder,
            bool enableTopDownYSort = false)
        {
            GameObject layerObj = new GameObject(layerName);
            layerObj.transform.SetParent(parent, false);
            layerObj.transform.localPosition = Vector3.zero;

            Tilemap tilemap = layerObj.AddComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);

            TilemapRenderer renderer = layerObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (enableTopDownYSort)
            {
                renderer.mode = TilemapRenderer.Mode.Individual;
                renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
            }
            else
            {
                renderer.mode = TilemapRenderer.Mode.Chunk;
            }
            return tilemap;
        }

        private void BuildWorldMapCanvas()
        {
            _worldMapCanvas = UguiThemeFactory.CreateOverlayCanvas("CampaignWorldMapCanvas", 16);
            _canvasRect = _worldMapCanvas.GetComponent<RectTransform>();

            // Transparent overlay area over the world-space 2D Tilemap
            _mapContent = UguiThemeFactory.CreateRect("TilemapOverlayLayer", _canvasRect);
            StretchFull(_mapContent, 0f, 0f);

            _markersLayer = UguiThemeFactory.CreateRect("MarkersContainer", _mapContent);
            _markersLayer.anchorMin = new Vector2(0.5f, 0.5f);
            _markersLayer.anchorMax = new Vector2(0.5f, 0.5f);
            _markersLayer.pivot = new Vector2(0.5f, 0.5f);
            _markersLayer.sizeDelta = Vector2.zero;
            _markersLayer.anchoredPosition = Vector2.zero;

            _avatarLayer = UguiThemeFactory.CreateRect("AvatarContainer", _mapContent);
            _avatarLayer.anchorMin = new Vector2(0.5f, 0.5f);
            _avatarLayer.anchorMax = new Vector2(0.5f, 0.5f);
            _avatarLayer.pivot = new Vector2(0.5f, 0.5f);
            _avatarLayer.sizeDelta = Vector2.zero;
            _avatarLayer.anchoredPosition = Vector2.zero;

            BuildPlayerAvatar(_avatarLayer);
            BuildTopStatusHeader(_canvasRect);
            BuildBottomMiniRosterStrip(_canvasRect);
            BuildTooltipCard(_canvasRect);
        }

        private void BuildTopStatusHeader(RectTransform root)
        {
            UnityEngine.UI.Image headerBg = UguiThemeFactory.CreatePanel(
                "TopStatusHeader",
                root,
                new Color(0.06f, 0.08f, 0.12f, 0.94f),
                false);
            RectTransform hdrRt = headerBg.rectTransform;
            hdrRt.anchorMin = new Vector2(0f, 1f);
            hdrRt.anchorMax = new Vector2(1f, 1f);
            hdrRt.pivot = new Vector2(0.5f, 1f);
            hdrRt.offsetMin = new Vector2(0f, -72f);
            hdrRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image bottomBorder = UguiThemeFactory.CreatePanel(
                "HeaderBottomLine",
                hdrRt,
                new Color(0.85f, 0.65f, 0.24f, 0.60f),
                false);
            bottomBorder.raycastTarget = false;
            RectTransform lineRt = bottomBorder.rectTransform;
            lineRt.anchorMin = new Vector2(0f, 0f);
            lineRt.anchorMax = new Vector2(1f, 0f);
            lineRt.pivot = new Vector2(0.5f, 0f);
            lineRt.sizeDelta = new Vector2(0f, 2f);
            lineRt.anchoredPosition = Vector2.zero;

            _headerSummaryText = UguiThemeFactory.CreateTmpText(
                "HeaderSummaryText",
                hdrRt,
                string.Empty,
                22f,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _headerSummaryText.enableAutoSizing = true;
            _headerSummaryText.fontSizeMin = 15f;
            _headerSummaryText.fontSizeMax = 22f;
            _headerSummaryText.overflowMode = TextOverflowModes.Truncate;
            RectTransform sumRt = _headerSummaryText.rectTransform;
            sumRt.anchorMin = new Vector2(0f, 0f);
            sumRt.anchorMax = new Vector2(1f, 1f);
            sumRt.offsetMin = new Vector2(24f, 4f);
            sumRt.offsetMax = new Vector2(-560f, -4f);

            RectTransform btnsRt = UguiThemeFactory.CreateRect("HeaderButtonsRow", hdrRt);
            btnsRt.anchorMin = new Vector2(1f, 0.5f);
            btnsRt.anchorMax = new Vector2(1f, 0.5f);
            btnsRt.pivot = new Vector2(1f, 0.5f);
            btnsRt.sizeDelta = new Vector2(540f, 46f);
            btnsRt.anchoredPosition = new Vector2(-18f, 0f);

            UnityEngine.UI.HorizontalLayoutGroup layout = btnsRt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            _policyButton = CreateHeaderButton("PolicyBtn", btnsRt, "AI Policy", 112f, BtnActiveColor, UguiThemeFactory.AllyCyan);
            _policyButton.onClick.AddListener(OnClickOpenPolicy);

            _saveButton = CreateHeaderButton("SaveBtn", btnsRt, "Lưu Run", 104f, BtnGreenColor, UguiThemeFactory.HpGreen);
            _saveButton.onClick.AddListener(OnClickSaveJson);

            _unlockEndlessButton = CreateHeaderButton("UnlockEndlessBtn", btnsRt, "Mở Tầng Endless", 172f, BtnGoldColor, UguiThemeFactory.AccentGold);
            _unlockEndlessButton.onClick.AddListener(OnClickUnlockEndless);
            _unlockEndlessButton.gameObject.SetActive(false);

            _settingsButton = CreateHeaderButton("SettingsBtn", btnsRt, "Cài Đặt", 104f, UguiThemeFactory.PanelBgSoft, new Color(0.55f, 0.65f, 0.82f, 0.85f));
            _settingsButton.onClick.AddListener(OnClickOpenSettings);
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

            // Tactical Warband Pawn Pedestal beneath leader sprite
            RectTransform pedestalOuterRt = UguiThemeFactory.CreateRect("PawnPedestalRing", _avatarRoot);
            pedestalOuterRt.anchorMin = new Vector2(0.5f, 0.5f);
            pedestalOuterRt.anchorMax = new Vector2(0.5f, 0.5f);
            pedestalOuterRt.sizeDelta = new Vector2(72f, 30f);
            pedestalOuterRt.anchoredPosition = new Vector2(0f, -12f);
            UnityEngine.UI.Image pedestalOuterImg = pedestalOuterRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            pedestalOuterImg.sprite = UguiThemeFactory.GetCircleSprite();
            pedestalOuterImg.color = new Color(0.96f, 0.72f, 0.18f, 0.92f);
            pedestalOuterImg.raycastTarget = false;

            RectTransform pedestalInnerRt = UguiThemeFactory.CreateRect("PawnPedestalInner", _avatarRoot);
            pedestalInnerRt.anchorMin = new Vector2(0.5f, 0.5f);
            pedestalInnerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pedestalInnerRt.sizeDelta = new Vector2(58f, 22f);
            pedestalInnerRt.anchoredPosition = new Vector2(0f, -12f);
            UnityEngine.UI.Image pedestalInnerImg = pedestalInnerRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            pedestalInnerImg.sprite = UguiThemeFactory.GetCircleSprite();
            pedestalInnerImg.color = new Color(0.13f, 0.83f, 0.93f, 0.85f);
            pedestalInnerImg.raycastTarget = false;

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

            _travelToastText = UguiThemeFactory.CreateTmpText(
                "ToastText",
                _travelToastRt,
                string.Empty,
                13f,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _travelToastText.enableAutoSizing = true;
            _travelToastText.fontSizeMin = 10f;
            _travelToastText.fontSizeMax = 13f;
            _travelToastText.overflowMode = TextOverflowModes.Truncate;
            StretchFull(_travelToastText.rectTransform, 12f, 6f);
            _travelToastRoot.SetActive(false);
        }

        private void BuildBottomMiniRosterStrip(RectTransform root)
        {
            // Compact dock strictly anchored at bottom-left; no right-side status box
            UnityEngine.UI.Image bottomBackdrop = UguiThemeFactory.CreatePanel(
                "BottomBarBackdrop",
                root,
                new Color(0.05f, 0.07f, 0.11f, 0.90f),
                true);
            bottomBackdrop.raycastTarget = false;
            RectTransform bdRt = bottomBackdrop.rectTransform;
            bdRt.anchorMin = new Vector2(0f, 0f);
            bdRt.anchorMax = new Vector2(0f, 0f);
            bdRt.pivot = new Vector2(0f, 0f);
            bdRt.anchoredPosition = new Vector2(14f, 8f);
            bdRt.sizeDelta = new Vector2(1142f, 72f);
            UguiThemeFactory.CreateOutlineFrame("Outline", bdRt, new Color(0.85f, 0.65f, 0.24f, 0.45f));

            RectTransform stripRt = UguiThemeFactory.CreateRect("BottomLeftMiniRosterStrip", bdRt);
            stripRt.anchorMin = new Vector2(0f, 0.5f);
            stripRt.anchorMax = new Vector2(0f, 0.5f);
            stripRt.pivot = new Vector2(0f, 0.5f);
            stripRt.sizeDelta = new Vector2(1120f, 58f);
            stripRt.anchoredPosition = new Vector2(12f, 0f);

            for (int i = 0; i < MaxMiniRosterPills; i++)
            {
                MiniRosterPill pill = new MiniRosterPill();
                UnityEngine.UI.Image bg = UguiThemeFactory.CreatePanel(
                    $"MiniPill_{i}",
                    stripRt,
                    new Color(0.08f, 0.11f, 0.17f, 0.96f),
                    true);
                bg.raycastTarget = false;
                RectTransform pRt = bg.rectTransform;
                pRt.anchorMin = new Vector2(0f, 0.5f);
                pRt.anchorMax = new Vector2(0f, 0.5f);
                pRt.pivot = new Vector2(0f, 0.5f);
                pRt.sizeDelta = new Vector2(214f, 56f);
                pRt.anchoredPosition = new Vector2(i * 224f, 0f);
                UguiThemeFactory.CreateOutlineFrame("Outline", pRt, new Color(0.13f, 0.83f, 0.93f, 0.55f));

                UnityEngine.UI.Image roleBadge = UguiThemeFactory.CreatePanel(
                    "RoleBadge",
                    pRt,
                    UguiThemeFactory.AllyCyan,
                    false);
                roleBadge.raycastTarget = false;
                RectTransform rbRt = roleBadge.rectTransform;
                rbRt.anchorMin = new Vector2(0f, 0f);
                rbRt.anchorMax = new Vector2(0f, 1f);
                rbRt.pivot = new Vector2(0f, 0.5f);
                rbRt.offsetMin = new Vector2(3f, 4f);
                rbRt.offsetMax = new Vector2(11f, -4f);

                pill.Root = bg.gameObject;
                pill.RoleBadge = roleBadge;
                pill.NameText = UguiThemeFactory.CreateTmpText(
                    "Name",
                    pRt,
                    string.Empty,
                    15f,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold);
                pill.NameText.enableAutoSizing = true;
                pill.NameText.fontSizeMin = 11f;
                pill.NameText.fontSizeMax = 15f;
                pill.NameText.overflowMode = TextOverflowModes.Truncate;
                RectTransform nameRt = pill.NameText.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0.48f);
                nameRt.anchorMax = new Vector2(1f, 1f);
                nameRt.offsetMin = new Vector2(16f, 0f);
                nameRt.offsetMax = new Vector2(-8f, -2f);

                UnityEngine.UI.Image hpTrack = UguiThemeFactory.CreatePanel("HpTrack", pRt, new Color(0.04f, 0.06f, 0.10f, 1f), false);
                hpTrack.raycastTarget = false;
                RectTransform trackRt = hpTrack.rectTransform;
                trackRt.anchorMin = new Vector2(0f, 0f);
                trackRt.anchorMax = new Vector2(1f, 0f);
                trackRt.pivot = new Vector2(0.5f, 0f);
                trackRt.offsetMin = new Vector2(16f, 6f);
                trackRt.offsetMax = new Vector2(-10f, 24f);

                UnityEngine.UI.Image hpFill = UguiThemeFactory.CreatePanel("HpFill", trackRt, UguiThemeFactory.HpGreen, false);
                hpFill.raycastTarget = false;
                pill.HpFillRect = hpFill.rectTransform;
                StretchFull(pill.HpFillRect, 0f, 0f);

                pill.HpValueText = UguiThemeFactory.CreateTmpText(
                    "HpValueText",
                    trackRt,
                    string.Empty,
                    12f,
                    Color.white,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                StretchFull(pill.HpValueText.rectTransform, 2f, 0f);

                pill.Root.SetActive(false);
                _miniRosterPills[i] = pill;
            }
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
            _tooltipRt.sizeDelta = new Vector2(390f, 152f);

            _tooltipOutline = UguiThemeFactory.CreateOutlineFrame("Outline", _tooltipRt, UguiThemeFactory.AllyCyan);

            _tooltipTitleText = UguiThemeFactory.CreateTmpText(
                "Title",
                _tooltipRt,
                string.Empty,
                17f,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _tooltipTitleText.enableAutoSizing = true;
            _tooltipTitleText.fontSizeMin = 13f;
            _tooltipTitleText.fontSizeMax = 17f;
            _tooltipTitleText.overflowMode = TextOverflowModes.Truncate;
            RectTransform titleRt = _tooltipTitleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.offsetMin = new Vector2(14f, -38f);
            titleRt.offsetMax = new Vector2(-14f, -8f);

            _tooltipMetaText = UguiThemeFactory.CreateTmpText(
                "Meta",
                _tooltipRt,
                string.Empty,
                13.5f,
                UguiThemeFactory.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            _tooltipMetaText.enableAutoSizing = true;
            _tooltipMetaText.fontSizeMin = 11f;
            _tooltipMetaText.fontSizeMax = 14f;
            _tooltipMetaText.overflowMode = TextOverflowModes.Truncate;
            RectTransform metaRt = _tooltipMetaText.rectTransform;
            metaRt.anchorMin = new Vector2(0f, 0f);
            metaRt.anchorMax = new Vector2(1f, 1f);
            metaRt.offsetMin = new Vector2(14f, 36f);
            metaRt.offsetMax = new Vector2(-14f, -40f);

            _tooltipStatusText = UguiThemeFactory.CreateTmpText(
                "Status",
                _tooltipRt,
                string.Empty,
                13.5f,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            RectTransform statusRt = _tooltipStatusText.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.offsetMin = new Vector2(14f, 8f);
            statusRt.offsetMax = new Vector2(-14f, 32f);

            _tooltipRoot.SetActive(false);
        }

        private void RefreshIfDirty()
        {
            RunState run = sandboxController != null ? sandboxController.CurrentRun : null;

            if (run == null)
            {
                if (_mapDirty || _lastRun != null)
                {
                    _mapDirty = false;
                    _lastRun = null;
                    ClearMapElements();
                    ClearTilemaps();
                    _headerSummaryText.text = "CHƯA CÓ DỮ LIỆU BẢN ĐỒ";
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

                RebuildWorldMap(run);
            }
        }

        private void RebuildWorldMap(RunState run)
        {
            EnsureTilesLoaded();
            if (_worldGrid == null)
            {
                BuildWorldTilemapGrid();
            }

            ClearMapElements();
            ResetGridBuffers();

            int sectorIndex;
            int startFloor;
            int endFloor;
            ResolveCurrentSectorRange(run, out sectorIndex, out startFloor, out endFloor);

            if (sectorIndex >= 1)
            {
                EnsureEndlessSectorPopulated(run, endFloor);
                _lastEndlessDepth = run.EndlessFloorDepth;
            }

            int displayFloor = run.CurrentFloor + 1;
            _headerSummaryText.text = $"TẦNG HIỆN TẠI: {displayFloor}";

            CampaignNode currentNode = run.GetCurrentNode();
            bool canUnlockEndless = run.IsRunCompleted ||
                                    (currentNode != null && currentNode.IsCompleted && currentNode.NextNodeIds.Count == 0);
            _unlockEndlessButton.gameObject.SetActive(canUnlockEndless);

            // Collect only the nodes belonging to the active Sector (Method A: Sector Paging)
            _sectorNodesBuffer.Clear();
            _floorNodeCounts.Clear();
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode n = run.Nodes[i];
                if (n == null || n.FloorIndex < startFloor || n.FloorIndex > endFloor) continue;

                _sectorNodesBuffer.Add(n);
                int existingCount;
                _floorNodeCounts.TryGetValue(n.FloorIndex, out existingCount);
                _floorNodeCounts[n.FloorIndex] = existingCount + 1;
            }

            int sectorSeed = unchecked(run.Seed * 7919 + sectorIndex * 6271 + 37);

            // 1. Compute asymmetric, organic tile coordinates for all visible sector nodes and trailhead per sectorSeed
            int entryY = (CellHash(sectorIndex, -99, sectorSeed) % 3) - 1; // -1, 0, or +1
            int exitY = (CellHash(sectorIndex, 99, sectorSeed) % 3) - 1;   // -1, 0, or +1
            Vector2Int trailheadCell = sectorIndex == 0
                ? new Vector2Int(-11, entryY)
                : new Vector2Int(-10, entryY);

            for (int i = 0; i < _sectorNodesBuffer.Count; i++)
            {
                CampaignNode n = _sectorNodesBuffer[i];

                int floorCount = 1;
                _floorNodeCounts.TryGetValue(n.FloorIndex, out floorCount);

                Vector2Int nodeCell = ResolveAsymmetricNodeCell(
                    n.FloorIndex,
                    n.LaneIndex,
                    floorCount,
                    sectorIndex,
                    startFloor,
                    endFloor,
                    sectorSeed,
                    entryY,
                    exitY);
                _nodeGridCells[n.NodeId] = nodeCell;
                ReserveNodeArea(nodeCell);
            }
            ReserveNodeArea(trailheadCell);

            // 2. Carve clean 1-tile winding trail network with shared hubs (zero thick 2x2 blobs, zero 1x1 grass holes)
            CarveCleanSectorRoadNetwork(run, currentNode, sectorIndex, startFloor, endFloor, sectorSeed, trailheadCell, entryY, exitY);

            // 3. Generate Biome structure & decor props using 4 non-repeating templates
            MapStructureTemplate template;
            BiomeType sectorBiome;
            SelectSectorTemplateAndBiome(run, sectorIndex, _sectorNodesBuffer, out template, out sectorBiome);

            ApplyBiomeStructureTemplate(template, sectorSeed, endFloor, sectorIndex, startFloor);
            ApplyWaterShoreTransitions();
            PlaceDecorProps(template, sectorBiome, sectorSeed, trailheadCell);

            // 4. Push data to Unity 2D Tilemaps
            PaintTilemaps(sectorBiome);

            // 5. Spawn UI node markers positioned cleanly over their respective tile centers
            for (int i = 0; i < _sectorNodesBuffer.Count; i++)
            {
                CampaignNode node = _sectorNodesBuffer[i];
                if (!_nodeGridCells.TryGetValue(node.NodeId, out Vector2Int markerCell)) continue;

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
                markerRt.anchoredPosition = CellToCanvasPosition(markerCell);

                MapNodeMarkerView marker = markerObj.AddComponent<MapNodeMarkerView>();
                marker.Bind(node, isCurrent, isAvailableHighlight, canSelect);
                marker.SetTravelingLock(_isTraveling);
                marker.OnNodeClicked += HandleMarkerClicked;
                marker.OnNodeHoverChanged += HandleMarkerHoverChanged;
                _spawnedMarkers.Add(marker);

                MarkerCellBinding binding = new MarkerCellBinding();
                binding.Marker = marker;
                binding.Cell = markerCell;
                _markerBindings.Add(binding);
            }

            // 6. Sync Player Avatar resting cell & sprite
            UnitRole leaderRole = ResolveLeaderRole(run);
            _avatarImage.sprite = ProceduralUnitArtFactory.GetUnitSprite(leaderRole, TeamSide.Player, false);

            if (!_isTraveling)
            {
                bool standAtTrailhead = currentNode == null ||
                                        currentNode.FloorIndex < startFloor ||
                                        (sectorIndex == 0 && currentNode.FloorIndex == 0 && !currentNode.IsCompleted);

                if (standAtTrailhead)
                {
                    _avatarCurrentCell = trailheadCell;
                }
                else if (currentNode != null && _nodeGridCells.TryGetValue(currentNode.NodeId, out Vector2Int currCell))
                {
                    _avatarCurrentCell = currCell;
                }
                else
                {
                    _avatarCurrentCell = trailheadCell;
                }

                _avatarBasePos = CellToCanvasPosition(_avatarCurrentCell);
                _avatarRoot.anchoredPosition = _avatarBasePos;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            RefreshMiniRosterPills(run);
        }

        private static void ResolveCurrentSectorRange(RunState run, out int sectorIndex, out int startFloor, out int endFloor)
        {
            int maxFloor = GetMaxFloor(run);
            if (run.EndlessFloorDepth <= 0 || maxFloor <= 4)
            {
                sectorIndex = 0;
                startFloor = 0;
                endFloor = 4;
                return;
            }

            int activeEndlessFloor = -1;
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode n = run.Nodes[i];
                if (n == null || n.FloorIndex < 5 || n.IsCompleted) continue;
                if (activeEndlessFloor < 0 || n.FloorIndex < activeEndlessFloor)
                {
                    activeEndlessFloor = n.FloorIndex;
                }
            }

            if (activeEndlessFloor < 0)
            {
                activeEndlessFloor = maxFloor;
            }

            sectorIndex = 1 + ((activeEndlessFloor - 5) / 3);
            startFloor = 5 + (sectorIndex - 1) * 3;
            endFloor = startFloor + 2;
        }

        private static int GetMaxFloor(RunState run)
        {
            int maxFloor = 0;
            if (run == null || run.Nodes == null) return 0;
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                CampaignNode n = run.Nodes[i];
                if (n != null && n.FloorIndex > maxFloor)
                {
                    maxFloor = n.FloorIndex;
                }
            }
            return maxFloor;
        }

        private static void EnsureEndlessSectorPopulated(RunState run, int targetEndFloor)
        {
            if (run == null) return;
            int safety = 0;
            while (GetMaxFloor(run) < targetEndFloor && safety < 6)
            {
                int savedCorruption = run.CorruptionLevel;
                CampaignGenerator.AdvanceToNextEndlessFloor(run);
                run.CorruptionLevel = savedCorruption;
                safety++;
            }
        }

        private static int ResolveNodeTileX(int floorIndex, int sectorIndex, int startFloor)
        {
            if (sectorIndex == 0)
            {
                switch (floorIndex)
                {
                    case 0:  return -8;
                    case 1:  return -4;
                    case 2:  return 0;
                    case 3:  return 4;
                    default: return 9;
                }
            }

            int localCol = floorIndex - startFloor;
            if (localCol <= 0) return -4;
            if (localCol == 1) return 2;
            return 8;
        }

        private static Vector2Int ResolveAsymmetricNodeCell(
            int floorIndex,
            int laneIndex,
            int floorCount,
            int sectorIndex,
            int startFloor,
            int endFloor,
            int sectorSeed,
            int entryY,
            int exitY)
        {
            int baseX = ResolveNodeTileX(floorIndex, sectorIndex, startFloor);
            int h = CellHash(floorIndex * 19 + laneIndex * 7, sectorIndex, sectorSeed);

            if (floorCount <= 1)
            {
                // Single node on floor (e.g., Floor 0 start or Boss at endFloor): organic vertical offset in [-2..+2]
                int singleY;
                if (floorIndex == startFloor)
                {
                    int delta = (h % 3) - 1; // -1, 0, +1 relative to entryY
                    singleY = Mathf.Clamp(entryY + delta, -2, 2);
                }
                else if (floorIndex == endFloor)
                {
                    int delta = (h % 3) - 1;
                    singleY = Mathf.Clamp(exitY + delta, -2, 2);
                }
                else
                {
                    singleY = (h % 5) - 2;
                }
                return new Vector2Int(baseX, singleY);
            }

            // Two-lane floor: stagger X slightly (-1 or 0 or +1) and vary Y asymmetrically per lane & floor
            int xShift = 0;
            if (sectorIndex > 0 || (floorIndex >= 1 && floorIndex <= 3))
            {
                // Stagger top vs bottom lane by up to 1 tile horizontally so columns don't look rigidly vertical
                int laneStagger = ((h >> 3) % 3) - 1; // -1, 0, +1
                // Ensure adequate horizontal room from neighbors
                if (baseX + laneStagger >= -6 && baseX + laneStagger <= 6)
                {
                    xShift = laneStagger;
                }
            }

            int cellY;
            if (laneIndex == 0)
            {
                // Top lane varies between +1 and +4 (avoiding constant +3 across all floors)
                int[] topChoices = { 2, 3, 4, 1, 3, 4, 2 };
                int idx = (h + floorIndex * 3 + sectorIndex * 5) % topChoices.Length;
                cellY = topChoices[idx];
            }
            else
            {
                // Bottom lane varies between -4 and -1 (avoiding constant -3 across all floors)
                int[] botChoices = { -3, -2, -4, -2, -3, -1, -4 };
                int idx = (h + floorIndex * 5 + sectorIndex * 2) % botChoices.Length;
                cellY = botChoices[idx];
            }

            return new Vector2Int(baseX + xShift, cellY);
        }

        private void ResetGridBuffers()
        {
            for (int gx = 0; gx < GridWidth; gx++)
            {
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    _terrainGrid[gx, gy] = TerrainCellKind.Grass;
                    _underRoadTerrainGrid[gx, gy] = TerrainCellKind.Grass;
                    _decorGrid[gx, gy] = DecorPropKind.None;
                    _roadStateGrid[gx, gy] = RoadVisualState.Locked;
                    _nodeReservedMask[gx, gy] = false;
                    _roadTurnCells[gx, gy] = false;
                }
            }
        }

        private static bool TryToGridIndex(int cellX, int cellY, out int gx, out int gy)
        {
            gx = cellX - GridMinX;
            gy = cellY - GridMinY;
            return gx >= 0 && gx < GridWidth && gy >= 0 && gy < GridHeight;
        }

        private void ReserveNodeArea(Vector2Int centerCell)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int gx, gy;
                    if (TryToGridIndex(centerCell.x + dx, centerCell.y + dy, out gx, out gy))
                    {
                        _nodeReservedMask[gx, gy] = true;
                    }
                }
            }
        }

        private void CarveCleanSectorRoadNetwork(
            RunState run,
            CampaignNode currentNode,
            int sectorIndex,
            int startFloor,
            int endFloor,
            int sectorSeed,
            Vector2Int trailheadCell,
            int entryY,
            int exitY)
        {
            // 1. Entry road from far-left map border (GridMinX, entryY) -> trailheadCell
            List<Vector2Int> entryPath = new List<Vector2Int>(8);
            entryPath.Add(new Vector2Int(GridMinX, entryY));
            AppendManhattanSegment(trailheadCell, entryPath, true);
            StampRoadPathCells(entryPath, RoadVisualState.Traveled, false);

            // 2. Trailhead -> startFloor node(s) using a single clean fork hub if 2 lanes exist
            List<CampaignNode> startNodes = CollectFloorNodes(startFloor);
            if (startNodes.Count == 1)
            {
                CampaignNode entryNode = startNodes[0];
                if (_nodeGridCells.TryGetValue(entryNode.NodeId, out Vector2Int f0Cell))
                {
                    RoadVisualState st = ResolveEntryRoadState(run, currentNode, sectorIndex, startFloor, entryNode);
                    List<Vector2Int> path = new List<Vector2Int>(12);
                    path.Add(trailheadCell);
                    int turnX = (trailheadCell.x + f0Cell.x) / 2;
                    AppendManhattanSegment(new Vector2Int(turnX, trailheadCell.y), path, true);
                    AppendManhattanSegment(new Vector2Int(turnX, f0Cell.y), path, false);
                    AppendManhattanSegment(f0Cell, path, true);
                    StampRoadPathCells(path, st, true);
                    _roadPathsByEdgeKey[GetEdgeKey(TrailheadNodeKey, entryNode.NodeId)] = path;
                }
            }
            else if (startNodes.Count >= 2)
            {
                int minStartX = int.MaxValue;
                RoadVisualState trunkState = RoadVisualState.Locked;
                for (int i = 0; i < startNodes.Count; i++)
                {
                    if (_nodeGridCells.TryGetValue(startNodes[i].NodeId, out Vector2Int c) && c.x < minStartX)
                    {
                        minStartX = c.x;
                    }
                    RoadVisualState st = ResolveEntryRoadState(run, currentNode, sectorIndex, startFloor, startNodes[i]);
                    if ((int)st > (int)trunkState) trunkState = st;
                }

                int forkX = (trailheadCell.x + minStartX) / 2;
                forkX = Mathf.Clamp(forkX, trailheadCell.x + 1, minStartX - 1);
                Vector2Int forkHub = new Vector2Int(forkX, trailheadCell.y);

                List<Vector2Int> trunk = new List<Vector2Int>(8);
                trunk.Add(trailheadCell);
                AppendManhattanSegment(forkHub, trunk, true);
                StampRoadPathCells(trunk, trunkState, false);

                for (int i = 0; i < startNodes.Count; i++)
                {
                    CampaignNode entryNode = startNodes[i];
                    if (!_nodeGridCells.TryGetValue(entryNode.NodeId, out Vector2Int targetCell)) continue;
                    RoadVisualState st = ResolveEntryRoadState(run, currentNode, sectorIndex, startFloor, entryNode);

                    List<Vector2Int> fullPath = new List<Vector2Int>(16);
                    for (int t = 0; t < trunk.Count; t++) fullPath.Add(trunk[t]);
                    AppendManhattanSegment(new Vector2Int(forkX, targetCell.y), fullPath, false);
                    AppendManhattanSegment(targetCell, fullPath, true);
                    StampRoadPathCells(fullPath, st, true);
                    _roadPathsByEdgeKey[GetEdgeKey(TrailheadNodeKey, entryNode.NodeId)] = fullPath;
                }
            }

            // 3. Floor-to-floor links: single shared crossroads column per floor transition (prevents thick 2x2 blobs and 1x1 grass holes)
            for (int floor = startFloor; floor < endFloor; floor++)
            {
                List<CampaignNode> nodesA = CollectFloorNodes(floor);
                List<CampaignNode> nodesB = CollectFloorNodes(floor + 1);
                if (nodesA.Count == 0 || nodesB.Count == 0) continue;

                int maxAx = int.MinValue;
                for (int i = 0; i < nodesA.Count; i++)
                {
                    if (_nodeGridCells.TryGetValue(nodesA[i].NodeId, out Vector2Int ca) && ca.x > maxAx) maxAx = ca.x;
                }

                int minBx = int.MaxValue;
                for (int i = 0; i < nodesB.Count; i++)
                {
                    if (_nodeGridCells.TryGetValue(nodesB[i].NodeId, out Vector2Int cb) && cb.x < minBx) minBx = cb.x;
                }

                int crossX = (maxAx + minBx) / 2;
                if (minBx - maxAx >= 4)
                {
                    int h = CellHash(floor * 23, sectorSeed, 511);
                    int shift = (h % 3) - 1;
                    crossX = Mathf.Clamp(crossX + shift, maxAx + 2, minBx - 2);
                }
                else if (minBx > maxAx + 1)
                {
                    crossX = Mathf.Clamp(crossX, maxAx + 1, minBx - 1);
                }

                for (int i = 0; i < nodesA.Count; i++)
                {
                    CampaignNode fromNode = nodesA[i];
                    if (fromNode.NextNodeIds == null || !_nodeGridCells.TryGetValue(fromNode.NodeId, out Vector2Int fromCell)) continue;

                    for (int k = 0; k < fromNode.NextNodeIds.Count; k++)
                    {
                        string nextId = fromNode.NextNodeIds[k];
                        if (string.IsNullOrEmpty(nextId) || !_nodeGridCells.TryGetValue(nextId, out Vector2Int toCell)) continue;
                        CampaignNode toNode = run.GetNode(nextId);

                        RoadVisualState edgeState = ResolveEdgeVisualState(run, currentNode, fromNode, toNode);
                        List<Vector2Int> edgePath = new List<Vector2Int>(20);
                        edgePath.Add(fromCell);
                        AppendManhattanSegment(new Vector2Int(crossX, fromCell.y), edgePath, true);
                        AppendManhattanSegment(new Vector2Int(crossX, toCell.y), edgePath, false);
                        AppendManhattanSegment(toCell, edgePath, true);

                        StampRoadPathCells(edgePath, edgeState, true);
                        _roadPathsByEdgeKey[GetEdgeKey(fromNode.NodeId, nextId)] = edgePath;
                    }
                }
            }

            // 4. Continuous winding exit road from final floor node(s) to far-right border (GridMaxX, exitY)
            List<CampaignNode> endNodes = CollectFloorNodes(endFloor);
            Vector2Int rightBorderExitCell = new Vector2Int(GridMaxX, exitY);
            for (int i = 0; i < endNodes.Count; i++)
            {
                CampaignNode exitNode = endNodes[i];
                if (!_nodeGridCells.TryGetValue(exitNode.NodeId, out Vector2Int exitNodeCell)) continue;

                RoadVisualState exitState = exitNode.IsCompleted ? RoadVisualState.Available : RoadVisualState.Traveled;
                List<Vector2Int> exitPath = new List<Vector2Int>(12);
                exitPath.Add(exitNodeCell);

                int turnExitX = Mathf.Clamp(exitNodeCell.x + 1 + (Math.Abs(sectorSeed + i) % 2), exitNodeCell.x + 1, GridMaxX - 1);
                if (exitNodeCell.y != exitY)
                {
                    AppendManhattanSegment(new Vector2Int(turnExitX, exitNodeCell.y), exitPath, true);
                    AppendManhattanSegment(new Vector2Int(turnExitX, exitY), exitPath, false);
                    AppendManhattanSegment(rightBorderExitCell, exitPath, true);
                }
                else
                {
                    int detourY = Mathf.Clamp(exitY == 0 ? 1 : 0, -2, 2);
                    int detourX1 = Mathf.Clamp(exitNodeCell.x + 1, exitNodeCell.x + 1, GridMaxX - 2);
                    int detourX2 = Mathf.Clamp(GridMaxX - 1, detourX1 + 1, GridMaxX - 1);
                    AppendManhattanSegment(new Vector2Int(detourX1, detourY), exitPath, true);
                    AppendManhattanSegment(new Vector2Int(detourX2, detourY), exitPath, true);
                    AppendManhattanSegment(rightBorderExitCell, exitPath, false);
                }

                StampRoadPathCells(exitPath, exitState, false);
            }
        }

        private List<CampaignNode> CollectFloorNodes(int floorIndex)
        {
            List<CampaignNode> list = new List<CampaignNode>(2);
            for (int i = 0; i < _sectorNodesBuffer.Count; i++)
            {
                CampaignNode n = _sectorNodesBuffer[i];
                if (n != null && n.FloorIndex == floorIndex)
                {
                    list.Add(n);
                }
            }
            return list;
        }

        private static RoadVisualState ResolveEntryRoadState(
            RunState run,
            CampaignNode currentNode,
            int sectorIndex,
            int startFloor,
            CampaignNode entryNode)
        {
            bool entryTraveled = entryNode.IsCompleted || entryNode.NodeId == run.CurrentNodeId ||
                                 (currentNode != null && currentNode.FloorIndex > startFloor);
            bool entryAvailable = !entryNode.IsCompleted &&
                                  (sectorIndex == 0
                                      ? (entryNode.FloorIndex == 0)
                                      : (currentNode != null && currentNode.IsCompleted &&
                                         currentNode.NextNodeIds != null &&
                                         currentNode.NextNodeIds.Contains(entryNode.NodeId)));

            return entryAvailable
                ? RoadVisualState.Available
                : (entryTraveled ? RoadVisualState.Traveled : RoadVisualState.Locked);
        }

        private static RoadVisualState ResolveEdgeVisualState(
            RunState run,
            CampaignNode currentNode,
            CampaignNode fromNode,
            CampaignNode toNode)
        {
            bool isTraveled = fromNode.IsCompleted && toNode != null &&
                              (toNode.IsCompleted || toNode.NodeId == run.CurrentNodeId ||
                               (currentNode != null && currentNode.FloorIndex >= toNode.FloorIndex &&
                                fromNode.NodeId == run.CurrentNodeId && toNode.IsCompleted));
            bool isAvailable = fromNode.IsCompleted && fromNode.NodeId == run.CurrentNodeId && toNode != null;

            return isAvailable
                ? RoadVisualState.Available
                : (isTraveled ? RoadVisualState.Traveled : RoadVisualState.Locked);
        }

        private void StampRoadPathCells(List<Vector2Int> path, RoadVisualState roadState, bool markTurnCells)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int c = path[i];
                int gx, gy;
                if (TryToGridIndex(c.x, c.y, out gx, out gy))
                {
                    _terrainGrid[gx, gy] = TerrainCellKind.Road;
                    if ((int)roadState > (int)_roadStateGrid[gx, gy])
                    {
                        _roadStateGrid[gx, gy] = roadState;
                    }
                }

                if (markTurnCells && i > 0 && i < path.Count - 1)
                {
                    Vector2Int prev = path[i - 1];
                    Vector2Int next = path[i + 1];
                    if (prev.x != next.x && prev.y != next.y)
                    {
                        int tgx, tgy;
                        if (TryToGridIndex(c.x, c.y, out tgx, out tgy))
                        {
                            _roadTurnCells[tgx, tgy] = true;
                        }
                    }
                }
            }
        }

        private static void AppendManhattanSegment(Vector2Int target, List<Vector2Int> output, bool horizontalFirst)
        {
            if (output.Count == 0)
            {
                output.Add(target);
                return;
            }

            Vector2Int cur = output[output.Count - 1];
            if (horizontalFirst)
            {
                while (cur.x != target.x)
                {
                    cur.x += (target.x > cur.x) ? 1 : -1;
                    output.Add(cur);
                }
                while (cur.y != target.y)
                {
                    cur.y += (target.y > cur.y) ? 1 : -1;
                    output.Add(cur);
                }
            }
            else
            {
                while (cur.y != target.y)
                {
                    cur.y += (target.y > cur.y) ? 1 : -1;
                    output.Add(cur);
                }
                while (cur.x != target.x)
                {
                    cur.x += (target.x > cur.x) ? 1 : -1;
                    output.Add(cur);
                }
            }
        }

        private static void BuildWindingCellPath(
            Vector2Int fromCell,
            Vector2Int toCell,
            int sectorSeed,
            int edgeSalt,
            List<Vector2Int> output)
        {
            output.Clear();
            output.Add(fromCell);
            int turnX = (fromCell.x + toCell.x) / 2;
            AppendManhattanSegment(new Vector2Int(turnX, fromCell.y), output, true);
            AppendManhattanSegment(new Vector2Int(turnX, toCell.y), output, false);
            AppendManhattanSegment(toCell, output, true);
        }

        private static string GetEdgeKey(string fromNodeId, string toNodeId)
        {
            return fromNodeId + "->" + toNodeId;
        }

        private static void SelectSectorTemplateAndBiome(
            RunState run,
            int sectorIndex,
            List<CampaignNode> sectorNodes,
            out MapStructureTemplate template,
            out BiomeType sectorBiome)
        {
            int startTemplate = Math.Abs(run.Seed) % 4;
            if (sectorIndex == 0)
            {
                template = (MapStructureTemplate)startTemplate;
            }
            else
            {
                int prevTemplate = startTemplate;
                int curTemplate = startTemplate;
                for (int s = 1; s <= sectorIndex; s++)
                {
                    int stepOffset = 1 + (Math.Abs(run.Seed + s * 131) % 3); // 1..3 ensures != prevTemplate
                    curTemplate = (prevTemplate + stepOffset) % 4;
                    prevTemplate = curTemplate;
                }
                template = (MapStructureTemplate)curTemplate;
            }

            if (sectorIndex == 0)
            {
                sectorBiome = BiomeType.RuinedCity;
                return;
            }

            if (sectorNodes != null && sectorNodes.Count > 0)
            {
                sectorBiome = sectorNodes[0].Biome;
                return;
            }

            sectorBiome = (BiomeType)(sectorIndex % 3);
        }

        private void ApplyBiomeStructureTemplate(
            MapStructureTemplate template,
            int sectorSeed,
            int endFloor,
            int sectorIndex,
            int startFloor)
        {
            int bossCellX = ResolveNodeTileX(endFloor, sectorIndex, startFloor);
            int bossMoatStartX = bossCellX - 2;
            int bossMoatEndX = bossCellX - 1;

            // Step 1: Assign base terrain & water candidate zones
            for (int gx = 0; gx < GridWidth; gx++)
            {
                int cellX = GridMinX + gx;
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    int cellY = GridMinY + gy;
                    bool isRoad = _terrainGrid[gx, gy] == TerrainCellKind.Road;

                    int h = CellHash(cellX, cellY, sectorSeed);
                    bool defaultFlower = (h % 100) < 25;

                    TerrainCellKind baseTerrain = defaultFlower ? TerrainCellKind.FlowerStone : TerrainCellKind.Grass;
                    bool forceWater = false;
                    bool allowBridgeCrossing = false;

                    switch (template)
                    {
                        case MapStructureTemplate.FrontierForestValley:
                            int pondShift = (sectorSeed % 3) - 1;
                            if ((cellX <= -8 + pondShift && cellY >= 3) || (cellX >= 6 + pondShift && cellY <= -4))
                            {
                                forceWater = true;
                            }
                            break;

                        case MapStructureTemplate.TwinRiversCrossing:
                            float riverPhase = (sectorSeed & 0x3F) * 0.17f;
                            int meander1 = Mathf.RoundToInt(Mathf.Sin(cellY * 0.65f + riverPhase));
                            int meander2 = Mathf.RoundToInt(Mathf.Cos(cellY * 0.60f + riverPhase));
                            int r1 = -3 + meander1;
                            int r2 = 3 + meander2;
                            bool inRiver1 = (cellX == r1 || cellX == r1 - 1) && Mathf.Abs(cellY) <= 6;
                            bool inRiver2 = (cellX == r2 || cellX == r2 + 1) && Mathf.Abs(cellY) <= 6;
                            if (inRiver1 || inRiver2)
                            {
                                forceWater = true;
                                allowBridgeCrossing = true;
                            }
                            else if (Mathf.Abs(cellX - r1) <= 1 || Mathf.Abs(cellX - r2) <= 1)
                            {
                                baseTerrain = (h % 100) < 55 ? TerrainCellKind.FlowerStone : TerrainCellKind.Grass;
                            }
                            break;

                        case MapStructureTemplate.SunkenAncientRuins:
                            int poolShiftX = ((sectorSeed >> 4) % 3) - 1;
                            bool outerMoat = (cellY >= 5 || cellY <= -6 || cellX <= -12 || cellX >= 11);
                            bool centerPool = (cellX >= -1 + poolShiftX && cellX <= 1 + poolShiftX && cellY >= -1 && cellY <= 1);
                            if (outerMoat || centerPool)
                            {
                                forceWater = true;
                            }
                            else
                            {
                                baseTerrain = (h % 100) < 45 ? TerrainCellKind.FlowerStone : TerrainCellKind.Grass;
                            }
                            break;

                        case MapStructureTemplate.CitadelCausewayIsles:
                            bool bossMoat = (cellX >= bossMoatStartX && cellX <= bossMoatEndX);
                            bool northBay = (cellY >= 5 && cellX >= -7 && cellX <= bossCellX);
                            bool southBay = (cellY <= -6 && cellX >= -7 && cellX <= bossCellX);
                            bool westCove = (cellX <= -11 && Mathf.Abs(cellY) >= 4);
                            if (bossMoat)
                            {
                                forceWater = true;
                                allowBridgeCrossing = true;
                            }
                            else if (northBay || southBay || westCove)
                            {
                                forceWater = true;
                            }
                            break;
                    }

                    // Keep a solid land clearing directly under every Node marker
                    if (_nodeReservedMask[gx, gy])
                    {
                        forceWater = false;
                    }

                    if (isRoad)
                    {
                        // Only erect a bridge over water where the road crosses cleanly (not down a parallel N-S spine)
                        bool hasVerticalRoad = IsTerrainAt(gx, gy + 1, TerrainCellKind.Road) ||
                                               IsTerrainAt(gx, gy - 1, TerrainCellKind.Road);
                        bool hasHorizRoad = IsTerrainAt(gx - 1, gy, TerrainCellKind.Road) ||
                                            IsTerrainAt(gx + 1, gy, TerrainCellKind.Road);
                        bool isCleanHorizontalCrossing = allowBridgeCrossing && forceWater && hasHorizRoad && !hasVerticalRoad;
                        _underRoadTerrainGrid[gx, gy] = isCleanHorizontalCrossing ? TerrainCellKind.Water : TerrainCellKind.Grass;
                    }
                    else
                    {
                        _terrainGrid[gx, gy] = forceWater ? TerrainCellKind.Water : baseTerrain;
                    }
                }
            }

            // Step 2 (Method A): Enforce at least a 1-tile Grass bank buffer between non-bridge Road cells and Water
            // so lakes, moats & rivers never slice flush along parallel roads
            for (int gx = 0; gx < GridWidth; gx++)
            {
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    if (_terrainGrid[gx, gy] != TerrainCellKind.Water) continue;

                    if (TouchesNonBridgeRoadOrthogonally(gx, gy))
                    {
                        int h = CellHash(GridMinX + gx, GridMinY + gy, sectorSeed + 19);
                        _terrainGrid[gx, gy] = (h % 100) < 30 ? TerrainCellKind.FlowerStone : TerrainCellKind.Grass;
                    }
                }
            }
        }

        private bool TouchesNonBridgeRoadOrthogonally(int gx, int gy)
        {
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = gx + (dir == 0 ? 1 : (dir == 1 ? -1 : 0));
                int ny = gy + (dir == 2 ? 1 : (dir == 3 ? -1 : 0));
                if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) continue;
                if (_terrainGrid[nx, ny] == TerrainCellKind.Road &&
                    _underRoadTerrainGrid[nx, ny] != TerrainCellKind.Water)
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyWaterShoreTransitions()
        {
            for (int gx = 0; gx < GridWidth; gx++)
            {
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    if (_terrainGrid[gx, gy] != TerrainCellKind.Water) continue;

                    bool touchesLand = false;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        int nx = gx + (dir == 0 ? 1 : (dir == 1 ? -1 : 0));
                        int ny = gy + (dir == 2 ? 1 : (dir == 3 ? -1 : 0));
                        if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) continue;

                        TerrainCellKind neighbor = _terrainGrid[nx, ny];
                        if (neighbor == TerrainCellKind.Grass ||
                            neighbor == TerrainCellKind.FlowerStone ||
                            neighbor == TerrainCellKind.Road)
                        {
                            touchesLand = true;
                            break;
                        }
                    }

                    if (touchesLand)
                    {
                        _terrainGrid[gx, gy] = TerrainCellKind.WaterShore;
                    }
                }
            }
        }

        private static readonly int[] RuinPillarOffsetsX = { 1, -1, 2, -2, 1, -1, 1, -1 };
        private static readonly int[] RuinPillarOffsetsY = { 0, 0, 0, 0, 1, 1, -1, -1 };

        private void PlaceDecorProps(
            MapStructureTemplate template,
            BiomeType sectorBiome,
            int sectorSeed,
            Vector2Int trailheadCell)
        {
            // Pass 1: Contextual Warband & Landmark storytelling props anchored meaningfully near Trailhead & Nodes
            TryPlacePropNearCell(trailheadCell, DecorPropKind.Campfire, 2, 3, sectorSeed + 11, false, 4);
            TryPlacePropNearCell(trailheadCell, DecorPropKind.SupplyCart, 2, 3, sectorSeed + 17, true, 4);

            int bannersPlaced = 0;
            bool bossCrownPlaced = false;
            for (int i = 0; i < _sectorNodesBuffer.Count; i++)
            {
                CampaignNode node = _sectorNodesBuffer[i];
                if (!_nodeGridCells.TryGetValue(node.NodeId, out Vector2Int nodeCell)) continue;
                int nSalt = sectorSeed + i * 53 + node.FloorIndex * 19;

                switch (node.NodeType)
                {
                    case CampaignNodeType.Boss:
                        bool crownInCluster;
                        TryPlaceRuinClusterNearCell(nodeCell, 2, 4, nSalt + 3, true, out crownInCluster);
                        bossCrownPlaced = crownInCluster;
                        if (!bossCrownPlaced)
                        {
                            bossCrownPlaced = TryPlacePropNearCell(nodeCell, DecorPropKind.BrokenCrown, 2, 3, nSalt + 9, false, 5);
                        }
                        break;

                    case CampaignNodeType.Recovery:
                        TryPlacePropNearCell(nodeCell, DecorPropKind.Campfire, 2, 3, nSalt + 5, false, 4);
                        TryPlacePropNearCell(nodeCell, DecorPropKind.RuneStone, 2, 3, nSalt + 13, false, 4);
                        break;

                    case CampaignNodeType.Shop:
                        TryPlacePropNearCell(nodeCell, DecorPropKind.SupplyCart, 2, 3, nSalt + 7, true, 4);
                        break;

                    case CampaignNodeType.Event:
                        TryPlacePropNearCell(nodeCell, DecorPropKind.RuneStone, 2, 3, nSalt + 11, false, 4);
                        break;

                    case CampaignNodeType.EliteCombat:
                        if (bannersPlaced < 3 &&
                            TryPlacePropNearCell(nodeCell, DecorPropKind.WarBanner, 2, 2, nSalt + 15, true, 4))
                        {
                            bannersPlaced++;
                        }
                        break;
                }
            }

            if (bannersPlaced < 3)
            {
                for (int i = 0; i < _sectorNodesBuffer.Count && bannersPlaced < 3; i++)
                {
                    CampaignNode node = _sectorNodesBuffer[i];
                    if (node.NodeType != CampaignNodeType.Combat) continue;
                    if (!_nodeGridCells.TryGetValue(node.NodeId, out Vector2Int nodeCell)) continue;
                    int nSalt = sectorSeed + i * 53 + node.FloorIndex * 19;
                    if (TryPlacePropNearCell(nodeCell, DecorPropKind.WarBanner, 2, 2, nSalt + 21, true, 4))
                    {
                        bannersPlaced++;
                    }
                }
            }

            // Pass 2: Anti-spam BushSign placement cleanly beside road forks (max 3 across entire map, min spacing 5)
            int bushSignsPlaced = 0;
            for (int gx = 1; gx < GridWidth - 1 && bushSignsPlaced < 3; gx++)
            {
                for (int gy = 1; gy < GridHeight - 1 && bushSignsPlaced < 3; gy++)
                {
                    if (!_roadTurnCells[gx, gy]) continue;

                    for (int dir = 0; dir < 4; dir++)
                    {
                        int nx = gx + (dir == 2 ? -1 : (dir == 3 ? 1 : 0));
                        int ny = gy + (dir == 0 ? 1 : (dir == 1 ? -1 : 0));
                        if (!CanPlaceDecorAtGrid(nx, ny)) continue;
                        if (HasNearbyDecor(nx, ny, DecorPropKind.BushSign, 5)) continue;
                        if (HasAnyDecorWithin(nx, ny, 1)) continue;

                        _decorGrid[nx, ny] = DecorPropKind.BushSign;
                        bushSignsPlaced++;
                        break;
                    }
                }
            }

            // Pass 3: Waterside Ecology (Cattail Reeds & Mossy Rocks only; Pillars belong strictly inside Ruin Clusters)
            for (int gx = 0; gx < GridWidth; gx++)
            {
                int cellX = GridMinX + gx;
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    int cellY = GridMinY + gy;
                    if (!CanPlaceDecorAtGrid(gx, gy)) continue;

                    bool nearWater = IsAnyWater(gx - 1, gy) || IsAnyWater(gx + 1, gy) ||
                                     IsAnyWater(gx, gy - 1) || IsAnyWater(gx, gy + 1);
                    if (!nearWater) continue;
                    if (HasAnyDecorWithin(gx, gy, 1)) continue;

                    int h = CellHash(cellX, cellY, sectorSeed + 31);
                    int roll = h % 100;
                    if (roll < 34 && !HasNearbyDecor(gx, gy, DecorPropKind.Reeds, 1))
                    {
                        _decorGrid[gx, gy] = DecorPropKind.Reeds;
                    }
                    else if (roll < 50 && !HasNearbyDecor(gx, gy, DecorPropKind.Rock, 2))
                    {
                        _decorGrid[gx, gy] = DecorPropKind.Rock;
                    }
                }
            }

            // Pass 4: Cohesive Ancient Ruin Clusters (RuinWall + Pillar paired together) & LabComplex RuneStones
            bool isRuinTemplate = template == MapStructureTemplate.SunkenAncientRuins ||
                                  template == MapStructureTemplate.CitadelCausewayIsles;
            int targetClusters = isRuinTemplate ? 3 : 2;
            int clustersPlaced = 0;
            int extraCrowns = bossCrownPlaced ? 0 : 0;

            for (int pass = 0; pass < 2 && clustersPlaced < targetClusters; pass++)
            {
                int threshold = pass == 0 ? 18 : 55;
                int startGx = 2 + (Math.Abs(sectorSeed + pass * 13) % Mathf.Max(1, GridWidth - 5));
                for (int stepX = 0; stepX < GridWidth - 4 && clustersPlaced < targetClusters; stepX++)
                {
                    int gx = 2 + ((startGx - 2 + stepX) % (GridWidth - 4));
                    int cellX = GridMinX + gx;
                    for (int gy = 2; gy < GridHeight - 2 && clustersPlaced < targetClusters; gy++)
                    {
                        int cellY = GridMinY + gy;
                        int h = CellHash(cellX, cellY, sectorSeed + 71) % 100;
                        if (h >= threshold) continue;

                        bool tryCrown = isRuinTemplate && extraCrowns < 1;
                        bool placedCrownHere;
                        if (TryPlaceRuinClusterAt(gx, gy, sectorSeed + 71 + gx * 7 + gy, tryCrown, out placedCrownHere))
                        {
                            clustersPlaced++;
                            if (placedCrownHere) extraCrowns++;
                        }
                    }
                }
            }

            if (sectorBiome == BiomeType.LabComplex)
            {
                int extraRuneStones = 0;
                for (int gx = 2; gx < GridWidth - 2 && extraRuneStones < 2; gx++)
                {
                    int cellX = GridMinX + gx;
                    for (int gy = 2; gy < GridHeight - 2 && extraRuneStones < 2; gy++)
                    {
                        int cellY = GridMinY + gy;
                        if (!CanPlaceDecorAtGrid(gx, gy)) continue;
                        if (HasAnyDecorWithin(gx, gy, 2)) continue;
                        if (HasNearbyDecor(gx, gy, DecorPropKind.RuneStone, 5)) continue;

                        int h = CellHash(cellX, cellY, sectorSeed + 109) % 100;
                        if (h < 12)
                        {
                            _decorGrid[gx, gy] = DecorPropKind.RuneStone;
                            extraRuneStones++;
                        }
                    }
                }
            }

            // Pass 5: Natural Forest Framing (strictly forbids placing Trees/Stumps adjacent to ANY existing prop so zero overlap!)
            for (int gx = 0; gx < GridWidth; gx++)
            {
                int cellX = GridMinX + gx;
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    int cellY = GridMinY + gy;
                    if (!CanPlaceDecorAtGrid(gx, gy)) continue;
                    if (HasAnyDecorWithin(gx, gy, 1)) continue;

                    bool isBorderForest = (cellY >= 5 || cellY <= -5 || cellX <= -12 || cellX >= 11);
                    int h = CellHash(cellX, cellY, sectorSeed + 97) % 100;

                    if (isBorderForest)
                    {
                        int treeChance = template == MapStructureTemplate.FrontierForestValley ? 46 : 40;
                        if (h < treeChance)
                        {
                            _decorGrid[gx, gy] = DecorPropKind.Tree;
                        }
                        else if (h < treeChance + 14)
                        {
                            _decorGrid[gx, gy] = DecorPropKind.Stump;
                        }
                    }
                    else
                    {
                        if (HasNearbyDecor(gx, gy, DecorPropKind.Tree, 2)) continue;

                        int innerRoll = CellHash(cellX, cellY, sectorSeed + 149) % 100;
                        int innerTreeChance = template == MapStructureTemplate.FrontierForestValley ? 18 : 14;
                        if (innerRoll < innerTreeChance)
                        {
                            _decorGrid[gx, gy] = DecorPropKind.Tree;
                        }
                        else if (innerRoll < innerTreeChance + 9)
                        {
                            _decorGrid[gx, gy] = DecorPropKind.Stump;
                        }
                    }
                }
            }
        }

        private bool TryPlaceRuinClusterNearCell(
            Vector2Int anchorCell,
            int minDist,
            int maxDist,
            int seedSalt,
            bool includeBrokenCrown,
            out bool placedCrown)
        {
            placedCrown = false;
            int bestGx = -1;
            int bestGy = -1;
            int bestScore = int.MinValue;

            for (int dy = -maxDist; dy <= maxDist; dy++)
            {
                for (int dx = -maxDist; dx <= maxDist; dx++)
                {
                    int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    if (dist < minDist || dist > maxDist) continue;

                    int gx, gy;
                    if (!TryToGridIndex(anchorCell.x + dx, anchorCell.y + dy, out gx, out gy)) continue;
                    if (!CanPlaceDecorAtGrid(gx, gy)) continue;
                    if (HasAnyDecorWithin(gx, gy, 1)) continue;
                    if (HasNearbyDecor(gx, gy, DecorPropKind.RuinWall, 4)) continue;
                    if (HasNearbyDecor(gx, gy, DecorPropKind.Pillar, 4)) continue;

                    int dummyPx, dummyPy;
                    if (!TryFindRuinPillarSlot(gx, gy, seedSalt, out dummyPx, out dummyPy)) continue;

                    int score = (CellHash(anchorCell.x + dx, anchorCell.y + dy, seedSalt) & 0xFF);
                    if (dy >= 0) score += 180;
                    if (dx != 0) score += 90;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestGx = gx;
                        bestGy = gy;
                    }
                }
            }

            if (bestGx < 0) return false;
            return TryPlaceRuinClusterAt(bestGx, bestGy, seedSalt, includeBrokenCrown, out placedCrown);
        }

        private bool TryPlaceRuinClusterAt(
            int wallGx,
            int wallGy,
            int seedSalt,
            bool tryAddBrokenCrown,
            out bool placedCrown)
        {
            placedCrown = false;
            if (!CanPlaceDecorAtGrid(wallGx, wallGy)) return false;
            if (HasAnyDecorWithin(wallGx, wallGy, 1)) return false;
            if (HasNearbyDecor(wallGx, wallGy, DecorPropKind.RuinWall, 4)) return false;
            if (HasNearbyDecor(wallGx, wallGy, DecorPropKind.Pillar, 4)) return false;

            int pillarGx, pillarGy;
            if (!TryFindRuinPillarSlot(wallGx, wallGy, seedSalt, out pillarGx, out pillarGy))
            {
                return false;
            }

            _decorGrid[wallGx, wallGy] = DecorPropKind.RuinWall;
            _decorGrid[pillarGx, pillarGy] = DecorPropKind.Pillar;

            int pillar2Gx = -1;
            int pillar2Gy = -1;
            int dx1 = pillarGx - wallGx;
            int dy1 = pillarGy - wallGy;
            if ((Math.Abs(seedSalt) % 100) < 45)
            {
                int symGx = wallGx - dx1;
                int symGy = wallGy - dy1;
                if (CanPlaceDecorAtGrid(symGx, symGy) &&
                    !HasOtherDecorWithin(symGx, symGy, 1, wallGx, wallGy, pillarGx, pillarGy))
                {
                    _decorGrid[symGx, symGy] = DecorPropKind.Pillar;
                    pillar2Gx = symGx;
                    pillar2Gy = symGy;
                }
            }

            if (tryAddBrokenCrown)
            {
                for (int dy = -1; dy <= 1 && !placedCrown; dy++)
                {
                    for (int dx = -2; dx <= 2 && !placedCrown; dx++)
                    {
                        if (dx == 0 && Math.Abs(dy) == 1) continue; // avoid vertically stacking on RuinWall
                        int cgx = wallGx + dx;
                        int cgy = wallGy + dy;
                        if (cgx == pillarGx && Math.Abs(cgy - pillarGy) == 1) continue;
                        if (pillar2Gx >= 0 && cgx == pillar2Gx && Math.Abs(cgy - pillar2Gy) == 1) continue;
                        if (!CanPlaceDecorAtGrid(cgx, cgy)) continue;
                        if (HasNearbyDecor(cgx, cgy, DecorPropKind.BrokenCrown, 5)) continue;
                        if (HasOtherDecorWithin3(cgx, cgy, 1, wallGx, wallGy, pillarGx, pillarGy, pillar2Gx, pillar2Gy)) continue;

                        _decorGrid[cgx, cgy] = DecorPropKind.BrokenCrown;
                        placedCrown = true;
                    }
                }
            }

            return true;
        }

        private bool TryFindRuinPillarSlot(int wallGx, int wallGy, int seedSalt, out int pillarGx, out int pillarGy)
        {
            int startIdx = ((seedSalt & 1) == 0) ? 0 : 1;
            for (int step = 0; step < RuinPillarOffsetsX.Length; step++)
            {
                int idx = step;
                if (step < 2) idx = (step + startIdx) % 2;
                else if (step < 4) idx = 2 + ((step - 2 + startIdx) % 2);

                int px = wallGx + RuinPillarOffsetsX[idx];
                int py = wallGy + RuinPillarOffsetsY[idx];
                if (!CanPlaceDecorAtGrid(px, py)) continue;
                if (HasOtherDecorWithin(px, py, 1, wallGx, wallGy, -1, -1)) continue;

                pillarGx = px;
                pillarGy = py;
                return true;
            }

            pillarGx = -1;
            pillarGy = -1;
            return false;
        }

        private bool TryPlacePropNearCell(
            Vector2Int anchorCell,
            DecorPropKind prop,
            int minDist,
            int maxDist,
            int seedSalt,
            bool preferRoadAdjacent,
            int samePropMinSpacing)
        {
            int bestGx = -1;
            int bestGy = -1;
            int bestScore = int.MinValue;

            for (int dy = -maxDist; dy <= maxDist; dy++)
            {
                for (int dx = -maxDist; dx <= maxDist; dx++)
                {
                    int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    if (dist < minDist || dist > maxDist) continue;

                    int gx, gy;
                    if (!TryToGridIndex(anchorCell.x + dx, anchorCell.y + dy, out gx, out gy)) continue;
                    if (!CanPlaceDecorAtGrid(gx, gy)) continue;
                    if (HasAnyDecorWithin(gx, gy, 1)) continue;
                    if (samePropMinSpacing > 0 && HasNearbyDecor(gx, gy, prop, samePropMinSpacing)) continue;

                    int score = (CellHash(anchorCell.x + dx, anchorCell.y + dy, seedSalt) & 0xFF);
                    // Prefer side/upper positions so props never tuck right behind the nameplate below the node
                    if (dy >= 0) score += 180;
                    if (dx != 0) score += 90;

                    bool adjRoad = IsTerrainAt(gx - 1, gy, TerrainCellKind.Road) ||
                                   IsTerrainAt(gx + 1, gy, TerrainCellKind.Road) ||
                                   IsTerrainAt(gx, gy - 1, TerrainCellKind.Road);
                    if (preferRoadAdjacent && adjRoad)
                    {
                        score += 320;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestGx = gx;
                        bestGy = gy;
                    }
                }
            }

            if (bestGx >= 0)
            {
                _decorGrid[bestGx, bestGy] = prop;
                return true;
            }
            return false;
        }

        private bool HasOtherDecorWithin(int gx, int gy, int radius, int allowX1, int allowY1, int allowX2, int allowY2)
        {
            return HasOtherDecorWithin3(gx, gy, radius, allowX1, allowY1, allowX2, allowY2, -1, -1);
        }

        private bool HasOtherDecorWithin3(
            int gx,
            int gy,
            int radius,
            int allowX1,
            int allowY1,
            int allowX2,
            int allowY2,
            int allowX3,
            int allowY3)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = gx + dx;
                    int ny = gy + dy;
                    if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) continue;
                    if ((nx == allowX1 && ny == allowY1) ||
                        (nx == allowX2 && ny == allowY2) ||
                        (nx == allowX3 && ny == allowY3))
                    {
                        continue;
                    }
                    if (_decorGrid[nx, ny] != DecorPropKind.None) return true;
                }
            }
            return false;
        }

        private bool HasAnyDecorWithin(int gx, int gy, int radius)
        {
            if (radius <= 0) return _decorGrid[gx, gy] != DecorPropKind.None;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = gx + dx;
                    int ny = gy + dy;
                    if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) continue;
                    if (_decorGrid[nx, ny] != DecorPropKind.None) return true;
                }
            }
            return false;
        }

        private bool CanPlaceDecorAtGrid(int gx, int gy)
        {
            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) return false;
            if (_nodeReservedMask[gx, gy]) return false;
            if (_decorGrid[gx, gy] != DecorPropKind.None) return false;

            TerrainCellKind kind = _terrainGrid[gx, gy];
            if (kind != TerrainCellKind.Grass && kind != TerrainCellKind.FlowerStone)
            {
                return false;
            }

            // Never allow any prop directly below a Road cell so tall sprites (Tree/Pillar) never overlap the path
            if (IsTerrainAt(gx, gy + 1, TerrainCellKind.Road))
            {
                return false;
            }

            return true;
        }

        private bool HasNearbyDecor(int gx, int gy, DecorPropKind prop, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = gx + dx;
                    int ny = gy + dy;
                    if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) continue;
                    if (_decorGrid[nx, ny] == prop) return true;
                }
            }
            return false;
        }

        private bool IsTerrainAt(int gx, int gy, TerrainCellKind expected)
        {
            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) return false;
            return _terrainGrid[gx, gy] == expected;
        }

        private bool IsAnyWater(int gx, int gy)
        {
            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) return false;
            TerrainCellKind k = _terrainGrid[gx, gy];
            return k == TerrainCellKind.Water || k == TerrainCellKind.WaterShore;
        }

        private void ClearTilemaps()
        {
            if (_groundTilemap != null) _groundTilemap.ClearAllTiles();
            if (_roadTilemap != null) _roadTilemap.ClearAllTiles();
            if (_fringeTilemap != null) _fringeTilemap.ClearAllTiles();
            if (_decorTilemap != null) _decorTilemap.ClearAllTiles();
        }

        private void PaintTilemaps(BiomeType sectorBiome)
        {
            ClearTilemaps();
            if (_groundTilemap == null || _roadTilemap == null || _decorTilemap == null) return;

            Color biomeGroundTint = ResolveBiomeGroundTint(sectorBiome);

            for (int gx = 0; gx < GridWidth; gx++)
            {
                int cellX = GridMinX + gx;
                for (int gy = 0; gy < GridHeight; gy++)
                {
                    int cellY = GridMinY + gy;
                    Vector3Int pos = new Vector3Int(cellX, cellY, 0);
                    int variant = ((gx * 13 + gy * 29) & 3);

                    TerrainCellKind cellKind = _terrainGrid[gx, gy];
                    if (cellKind == TerrainCellKind.Road)
                    {
                        bool isBridgeOverWater = _underRoadTerrainGrid[gx, gy] == TerrainCellKind.Water;
                        if (isBridgeOverWater)
                        {
                            _groundTilemap.SetTile(pos, _tileWater);
                            _groundTilemap.SetTileFlags(pos, TileFlags.None);
                            _groundTilemap.SetColor(pos, biomeGroundTint);

                            bool horizontal = !IsTerrainAt(gx, gy + 1, TerrainCellKind.Road) &&
                                              !IsTerrainAt(gx, gy - 1, TerrainCellKind.Road);
                            bool westLand = IsGrassOrLandRoadAt(gx - 1, gy);
                            bool eastLand = IsGrassOrLandRoadAt(gx + 1, gy);
                            bool northLand = IsGrassOrLandRoadAt(gx, gy + 1);
                            bool southLand = IsGrassOrLandRoadAt(gx, gy - 1);

                            Tile bridgeTile = GetOrCreateWoodenBridgeTile(horizontal, westLand, eastLand, northLand, southLand);
                            _roadTilemap.SetTile(pos, bridgeTile);
                            _roadTilemap.SetTileFlags(pos, TileFlags.None);
                            _roadTilemap.SetColor(pos, ResolveRoadTint(_roadStateGrid[gx, gy]));
                        }
                        else
                        {
                            _groundTilemap.SetTile(pos, _tileGrass);
                            _groundTilemap.SetTileFlags(pos, TileFlags.None);
                            _groundTilemap.SetColor(pos, biomeGroundTint);

                            Tile roadVariant = _tileCobbleRoadVariants[(gx * 7 + gy * 11) & 3] ?? _tileCobbleRoad;
                            _roadTilemap.SetTile(pos, roadVariant);
                            _roadTilemap.SetTileFlags(pos, TileFlags.None);
                            _roadTilemap.SetColor(pos, ResolveRoadTint(_roadStateGrid[gx, gy]));

                            if (_fringeTilemap != null)
                            {
                                int grassMask = ComputeGrassNeighborMask(gx, gy);
                                Tile fringeTile = GetOrCreateRoadFringeTile(grassMask, variant);
                                if (fringeTile != null)
                                {
                                    _fringeTilemap.SetTile(pos, fringeTile);
                                    _fringeTilemap.SetTileFlags(pos, TileFlags.None);
                                    _fringeTilemap.SetColor(pos, biomeGroundTint);
                                }
                            }
                        }
                    }
                    else if (cellKind == TerrainCellKind.Water || cellKind == TerrainCellKind.WaterShore)
                    {
                        _groundTilemap.SetTile(pos, _tileWater);
                        _groundTilemap.SetTileFlags(pos, TileFlags.None);
                        _groundTilemap.SetColor(pos, biomeGroundTint);

                        if (_fringeTilemap != null)
                        {
                            int grassMask;
                            int roadMask;
                            ComputeGrassAndRoadMasksForShoreline(gx, gy, out grassMask, out roadMask);
                            Tile shoreTile = GetOrCreateSmartShorelineTile(grassMask, roadMask, variant);
                            if (shoreTile != null)
                            {
                                _fringeTilemap.SetTile(pos, shoreTile);
                                _fringeTilemap.SetTileFlags(pos, TileFlags.None);
                                _fringeTilemap.SetColor(pos, biomeGroundTint);
                            }
                        }
                    }
                    else
                    {
                        Tile groundTile = cellKind == TerrainCellKind.FlowerStone ? _tileFlowerStone : _tileGrass;
                        _groundTilemap.SetTile(pos, groundTile);
                        _groundTilemap.SetTileFlags(pos, TileFlags.None);
                        _groundTilemap.SetColor(pos, biomeGroundTint);
                    }

                    DecorPropKind prop = _decorGrid[gx, gy];
                    if (prop != DecorPropKind.None)
                    {
                        Tile propTile;
                        switch (prop)
                        {
                            case DecorPropKind.Tree:        propTile = _tilePropTree; break;
                            case DecorPropKind.Rock:        propTile = _tilePropRock; break;
                            case DecorPropKind.BushSign:    propTile = _tilePropBushSign; break;
                            case DecorPropKind.Stump:       propTile = _tilePropStump; break;
                            case DecorPropKind.Campfire:    propTile = _tilePropCampfire; break;
                            case DecorPropKind.WarBanner:   propTile = _tilePropWarBanner; break;
                            case DecorPropKind.SupplyCart:  propTile = _tilePropSupplyCart; break;
                            case DecorPropKind.BrokenCrown: propTile = _tilePropBrokenCrown; break;
                            case DecorPropKind.RuneStone:   propTile = _tilePropRuneStone; break;
                            case DecorPropKind.RuinWall:    propTile = _tilePropRuinWall; break;
                            case DecorPropKind.Reeds:       propTile = _tilePropReeds; break;
                            default:                        propTile = _tilePropPillar; break;
                        }

                        _decorTilemap.SetTile(pos, propTile);
                        _decorTilemap.SetTileFlags(pos, TileFlags.None);
                        _decorTilemap.SetColor(pos, biomeGroundTint);
                    }
                }
            }
        }

        private bool IsGrassForFringe(int gx, int gy)
        {
            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) return false;
            TerrainCellKind k = _terrainGrid[gx, gy];
            return k == TerrainCellKind.Grass || k == TerrainCellKind.FlowerStone;
        }

        private bool IsGrassOrLandRoadAt(int gx, int gy)
        {
            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) return false;
            TerrainCellKind k = _terrainGrid[gx, gy];
            if (k == TerrainCellKind.Grass || k == TerrainCellKind.FlowerStone) return true;
            return k == TerrainCellKind.Road && _underRoadTerrainGrid[gx, gy] != TerrainCellKind.Water;
        }

        private int GetShorelineNeighborKind(int gx, int gy)
        {
            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) return 0;
            TerrainCellKind k = _terrainGrid[gx, gy];
            if (k == TerrainCellKind.Grass || k == TerrainCellKind.FlowerStone) return 1;
            if (k == TerrainCellKind.Road && _underRoadTerrainGrid[gx, gy] != TerrainCellKind.Water) return 2;
            return 0;
        }

        private int ComputeGrassNeighborMask(int gx, int gy)
        {
            int mask = 0;
            if (IsGrassForFringe(gx, gy + 1)) mask |= 1 << 0;     // North (top)
            if (IsGrassForFringe(gx, gy - 1)) mask |= 1 << 1;     // South (bottom)
            if (IsGrassForFringe(gx - 1, gy)) mask |= 1 << 2;     // West (left)
            if (IsGrassForFringe(gx + 1, gy)) mask |= 1 << 3;     // East (right)
            if (IsGrassForFringe(gx - 1, gy + 1)) mask |= 1 << 4; // NW
            if (IsGrassForFringe(gx + 1, gy + 1)) mask |= 1 << 5; // NE
            if (IsGrassForFringe(gx - 1, gy - 1)) mask |= 1 << 6; // SW
            if (IsGrassForFringe(gx + 1, gy - 1)) mask |= 1 << 7; // SE
            return mask;
        }

        private void ComputeGrassAndRoadMasksForShoreline(int gx, int gy, out int grassMask, out int roadMask)
        {
            grassMask = 0;
            roadMask = 0;

            int k;
            k = GetShorelineNeighborKind(gx, gy + 1);     if (k == 1) grassMask |= 1 << 0; else if (k == 2) roadMask |= 1 << 0;
            k = GetShorelineNeighborKind(gx, gy - 1);     if (k == 1) grassMask |= 1 << 1; else if (k == 2) roadMask |= 1 << 1;
            k = GetShorelineNeighborKind(gx - 1, gy);     if (k == 1) grassMask |= 1 << 2; else if (k == 2) roadMask |= 1 << 2;
            k = GetShorelineNeighborKind(gx + 1, gy);     if (k == 1) grassMask |= 1 << 3; else if (k == 2) roadMask |= 1 << 3;
            k = GetShorelineNeighborKind(gx - 1, gy + 1); if (k == 1) grassMask |= 1 << 4; else if (k == 2) roadMask |= 1 << 4;
            k = GetShorelineNeighborKind(gx + 1, gy + 1); if (k == 1) grassMask |= 1 << 5; else if (k == 2) roadMask |= 1 << 5;
            k = GetShorelineNeighborKind(gx - 1, gy - 1); if (k == 1) grassMask |= 1 << 6; else if (k == 2) roadMask |= 1 << 6;
            k = GetShorelineNeighborKind(gx + 1, gy - 1); if (k == 1) grassMask |= 1 << 7; else if (k == 2) roadMask |= 1 << 7;
        }

        private static readonly Color32[] _fringePixelBuffer = new Color32[64 * 64];

        private static Tile GetOrCreateRoadFringeTile(int mask, int variant)
        {
            if (mask == 0) return null;
            int cacheKey = (mask << 2) | (variant & 3);
            Tile cached;
            if (_roadFringeTileCache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }

            const int LP = 32;
            const int TilePx = 64;
            bool nGrass = (mask & (1 << 0)) != 0;
            bool sGrass = (mask & (1 << 1)) != 0;
            bool wGrass = (mask & (1 << 2)) != 0;
            bool eGrass = (mask & (1 << 3)) != 0;
            bool nwGrass = (mask & (1 << 4)) != 0;
            bool neGrass = (mask & (1 << 5)) != 0;
            bool swGrass = (mask & (1 << 6)) != 0;
            bool seGrass = (mask & (1 << 7)) != 0;

            Color32 colGrassBase = _sampledGrassBaseColor;
            Color32 colGrassLight = new Color32(186, 222, 102, 255);
            Color32 colGrassMid = new Color32(132, 180, 62, 255);
            Color32 colDirtEdge = new Color32(178, 122, 56, 255);
            Color32 colClear = new Color32(0, 0, 0, 0);

            float ph = (variant & 3) * 1.57f;

            for (int ly = 0; ly < LP; ly++)
            {
                // Texture2D y=0 is bottom (South); pyFromTop=0 is top (North)
                int pyFromTop = LP - 1 - ly;
                for (int lx = 0; lx < LP; lx++)
                {
                    float waveN = nGrass ? (4.2f + 1.6f * Mathf.Sin(lx * 0.55f + ph) + 0.9f * Mathf.Cos(lx * 1.15f - ph)) : -10f;
                    float waveS = sGrass ? (4.0f + 1.5f * Mathf.Sin(lx * 0.52f - ph) + 0.9f * Mathf.Cos(lx * 1.10f + ph)) : -10f;
                    float waveW = wGrass ? (4.2f + 1.6f * Mathf.Sin(pyFromTop * 0.55f + ph) + 0.9f * Mathf.Cos(pyFromTop * 1.15f + ph)) : -10f;
                    float waveE = eGrass ? (4.2f + 1.6f * Mathf.Sin(pyFromTop * 0.55f - ph) + 0.9f * Mathf.Cos(pyFromTop * 1.15f - ph)) : -10f;

                    float dN = waveN - pyFromTop;
                    float dS = waveS - ly;
                    float dW = waveW - lx;
                    float dE = waveE - (LP - 1 - lx);

                    float cornerBoost = -10f;
                    if (nGrass && wGrass) cornerBoost = Mathf.Max(cornerBoost, 10.5f - Mathf.Sqrt(lx * lx + pyFromTop * pyFromTop));
                    if (nGrass && eGrass) cornerBoost = Mathf.Max(cornerBoost, 10.5f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + pyFromTop * pyFromTop));
                    if (sGrass && wGrass) cornerBoost = Mathf.Max(cornerBoost, 10.5f - Mathf.Sqrt(lx * lx + ly * ly));
                    if (sGrass && eGrass) cornerBoost = Mathf.Max(cornerBoost, 10.5f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + ly * ly));

                    float diagTuft = -10f;
                    if (nwGrass && !nGrass && !wGrass) diagTuft = Mathf.Max(diagTuft, 5.5f - Mathf.Sqrt(lx * lx + pyFromTop * pyFromTop));
                    if (neGrass && !nGrass && !eGrass) diagTuft = Mathf.Max(diagTuft, 5.5f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + pyFromTop * pyFromTop));
                    if (swGrass && !sGrass && !wGrass) diagTuft = Mathf.Max(diagTuft, 5.5f - Mathf.Sqrt(lx * lx + ly * ly));
                    if (seGrass && !sGrass && !eGrass) diagTuft = Mathf.Max(diagTuft, 5.5f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + ly * ly));

                    float pen = Mathf.Max(Mathf.Max(dN, dS), Mathf.Max(Mathf.Max(dW, dE), Mathf.Max(cornerBoost, diagTuft)));
                    Color32 col = colClear;
                    if (pen >= -0.8f)
                    {
                        if (pen < 0.25f)
                        {
                            col = colDirtEdge;
                        }
                        else if (pen < 1.35f)
                        {
                            bool isTip = ((lx + pyFromTop + variant) % 3) == 0;
                            col = isTip ? colGrassLight : colGrassMid;
                        }
                        else if (pen < 2.4f)
                        {
                            col = ((lx * 3 + pyFromTop) % 4 == 0) ? colGrassMid : colGrassBase;
                        }
                        else
                        {
                            col = colGrassBase;
                        }
                    }

                    int row0 = (ly * 2) * TilePx + (lx * 2);
                    int row1 = row0 + TilePx;
                    _fringePixelBuffer[row0] = col;
                    _fringePixelBuffer[row0 + 1] = col;
                    _fringePixelBuffer[row1] = col;
                    _fringePixelBuffer[row1 + 1] = col;
                }
            }

            Texture2D tex = new Texture2D(TilePx, TilePx, TextureFormat.RGBA32, false);
            tex.name = $"RoadFringeTex_{mask}_{variant}";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(_fringePixelBuffer);
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, TilePx, TilePx), new Vector2(0.5f, 0.5f), TilePixelsPerUnit);
            sprite.name = $"RoadFringeSprite_{mask}_{variant}";
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"RoadFringeTile_{mask}_{variant}";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            _roadFringeTileCache[cacheKey] = tile;
            return tile;
        }

        private static Tile GetOrCreateWoodenBridgeTile(
            bool horizontal,
            bool westLand,
            bool eastLand,
            bool northLand,
            bool southLand)
        {
            int key = (horizontal ? 1 : 0) |
                      (westLand ? 2 : 0) |
                      (eastLand ? 4 : 0) |
                      (northLand ? 8 : 0) |
                      (southLand ? 16 : 0);
            Tile cached;
            if (_bridgeTileCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            const int LP = 32;
            const int TilePx = 64;
            Color32 colWoodHi = new Color32(208, 154, 94, 255);
            Color32 colWoodBase = new Color32(172, 118, 66, 255);
            Color32 colWoodDark = new Color32(128, 82, 44, 255);
            Color32 colWoodOut = new Color32(84, 54, 32, 255);
            Color32 colCliffTop = new Color32(184, 124, 82, 255);
            Color32 colCliffDark = new Color32(114, 70, 46, 255);
            Color32 colGrassMid = new Color32(132, 180, 62, 255);
            Color32 colClear = new Color32(0, 0, 0, 0);

            for (int ly = 0; ly < LP; ly++)
            {
                int pyFromTop = LP - 1 - ly;
                for (int lx = 0; lx < LP; lx++)
                {
                    Color32 col = colClear;
                    if (horizontal)
                    {
                        if ((westLand && lx < 5) || (eastLand && lx >= LP - 5))
                        {
                            if (pyFromTop < 4 || pyFromTop >= LP - 4)
                            {
                                int distE = (westLand && lx < 5) ? lx : (LP - 1 - lx);
                                col = (distE == 4) ? colCliffDark : (pyFromTop >= LP - 4 ? colCliffTop : colGrassMid);
                            }
                        }

                        if (pyFromTop >= 4 && pyFromTop < 28)
                        {
                            if (pyFromTop == 4 || pyFromTop == 27)
                            {
                                col = colWoodOut;
                            }
                            else if (pyFromTop == 5 || pyFromTop == 6 || pyFromTop == 25 || pyFromTop == 26)
                            {
                                bool isPost = (lx % 8) < 2;
                                bool isTopSide = (pyFromTop == 5 || pyFromTop == 25);
                                col = isPost ? (isTopSide ? colWoodHi : colWoodDark) : (isTopSide ? colWoodBase : colWoodOut);
                            }
                            else if (pyFromTop == 7 || pyFromTop == 24)
                            {
                                col = colWoodDark;
                            }
                            else
                            {
                                int mod = lx % 4;
                                col = (mod == 0) ? colWoodOut : (mod == 1 ? colWoodHi : colWoodBase);
                            }
                        }
                    }
                    else
                    {
                        if ((northLand && pyFromTop < 5) || (southLand && pyFromTop >= LP - 5))
                        {
                            if (lx < 4 || lx >= LP - 4)
                            {
                                int distE = (northLand && pyFromTop < 5) ? pyFromTop : (LP - 1 - pyFromTop);
                                col = (distE == 4) ? colCliffDark : colGrassMid;
                            }
                        }

                        if (lx >= 4 && lx < 28)
                        {
                            if (lx == 4 || lx == 27)
                            {
                                col = colWoodOut;
                            }
                            else if (lx == 5 || lx == 6 || lx == 25 || lx == 26)
                            {
                                bool isPost = (pyFromTop % 8) < 2;
                                bool isLeftSide = (lx == 5 || lx == 25);
                                col = isPost ? (isLeftSide ? colWoodHi : colWoodDark) : (isLeftSide ? colWoodBase : colWoodOut);
                            }
                            else if (lx == 7 || lx == 24)
                            {
                                col = colWoodDark;
                            }
                            else
                            {
                                int mod = pyFromTop % 4;
                                col = (mod == 0) ? colWoodOut : (mod == 1 ? colWoodHi : colWoodBase);
                            }
                        }
                    }

                    int row0 = (ly * 2) * TilePx + (lx * 2);
                    int row1 = row0 + TilePx;
                    _fringePixelBuffer[row0] = col;
                    _fringePixelBuffer[row0 + 1] = col;
                    _fringePixelBuffer[row1] = col;
                    _fringePixelBuffer[row1 + 1] = col;
                }
            }

            Texture2D tex = new Texture2D(TilePx, TilePx, TextureFormat.RGBA32, false);
            tex.name = $"BridgeTex_{key}";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(_fringePixelBuffer);
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, TilePx, TilePx), new Vector2(0.5f, 0.5f), TilePixelsPerUnit);
            sprite.name = $"BridgeSprite_{key}";
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"BridgeTile_{key}";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            _bridgeTileCache[key] = tile;
            return tile;
        }

        private static Tile GetOrCreateSmartShorelineTile(int grassMask, int roadMask, int variant)
        {
            if (grassMask == 0 && roadMask == 0) return null;
            int cacheKey = ((grassMask & 0xFF) << 10) | ((roadMask & 0xFF) << 2) | (variant & 3);
            Tile cached;
            if (_shorelineTileCache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }

            const int LP = 32;
            const int TilePx = 64;

            int nKind = ((grassMask & (1 << 0)) != 0) ? 1 : (((roadMask & (1 << 0)) != 0) ? 2 : 0);
            int sKind = ((grassMask & (1 << 1)) != 0) ? 1 : (((roadMask & (1 << 1)) != 0) ? 2 : 0);
            int wKind = ((grassMask & (1 << 2)) != 0) ? 1 : (((roadMask & (1 << 2)) != 0) ? 2 : 0);
            int eKind = ((grassMask & (1 << 3)) != 0) ? 1 : (((roadMask & (1 << 3)) != 0) ? 2 : 0);
            int nwKind = ((grassMask & (1 << 4)) != 0) ? 1 : (((roadMask & (1 << 4)) != 0) ? 2 : 0);
            int neKind = ((grassMask & (1 << 5)) != 0) ? 1 : (((roadMask & (1 << 5)) != 0) ? 2 : 0);
            int swKind = ((grassMask & (1 << 6)) != 0) ? 1 : (((roadMask & (1 << 6)) != 0) ? 2 : 0);
            int seKind = ((grassMask & (1 << 7)) != 0) ? 1 : (((roadMask & (1 << 7)) != 0) ? 2 : 0);

            bool nLand = nKind > 0;
            bool sLand = sKind > 0;
            bool wLand = wKind > 0;
            bool eLand = eKind > 0;
            bool nwLand = nwKind > 0;
            bool neLand = neKind > 0;
            bool swLand = swKind > 0;
            bool seLand = seKind > 0;

            Color32 colGrassBase = _sampledGrassBaseColor;
            Color32 colGrassMid = new Color32(132, 180, 62, 255);
            Color32 colDirtBase = new Color32(206, 150, 74, 255);
            Color32 colDirtEdge = new Color32(178, 122, 56, 255);
            Color32 colStoneFill = new Color32(234, 198, 138, 255);
            Color32 colCliffTop = new Color32(184, 124, 82, 255);
            Color32 colCliffMid = new Color32(154, 98, 64, 255);
            Color32 colCliffDark = new Color32(114, 70, 46, 255);
            Color32 colWaterFoam = new Color32(186, 224, 252, 255);
            Color32 colWaterShallow = new Color32(132, 186, 244, 255);
            Color32 colClear = new Color32(0, 0, 0, 0);

            float ph = (variant & 3) * 1.3f;

            for (int ly = 0; ly < LP; ly++)
            {
                int pyFromTop = LP - 1 - ly;
                for (int lx = 0; lx < LP; lx++)
                {
                    float waveN = nLand ? (((nKind == 1) ? 7.5f : 6.8f) + 1.4f * Mathf.Sin(lx * 0.50f + ph)) : -10f;
                    float waveS = sLand ? (((sKind == 1) ? 3.8f : 4.2f) + 1.2f * Mathf.Sin(lx * 0.55f - ph)) : -10f;
                    float waveW = wLand ? (((wKind == 1) ? 4.2f : 5.2f) + 1.5f * Mathf.Sin(pyFromTop * 0.52f + ph) + 0.6f * Mathf.Cos(pyFromTop * 1.05f)) : -10f;
                    float waveE = eLand ? (((eKind == 1) ? 4.2f : 5.2f) + 1.5f * Mathf.Sin(pyFromTop * 0.52f - ph) + 0.6f * Mathf.Cos(pyFromTop * 1.05f)) : -10f;

                    float dN = waveN - pyFromTop;
                    float dS = waveS - ly;
                    float dW = waveW - lx;
                    float dE = waveE - (LP - 1 - lx);

                    float cBoost = -10f;
                    if (nLand && wLand) cBoost = Mathf.Max(cBoost, 12.0f - Mathf.Sqrt(lx * lx + pyFromTop * pyFromTop));
                    if (nLand && eLand) cBoost = Mathf.Max(cBoost, 12.0f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + pyFromTop * pyFromTop));
                    if (sLand && wLand) cBoost = Mathf.Max(cBoost, 9.5f - Mathf.Sqrt(lx * lx + ly * ly));
                    if (sLand && eLand) cBoost = Mathf.Max(cBoost, 9.5f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + ly * ly));

                    if (nwLand && !nLand && !wLand) cBoost = Mathf.Max(cBoost, 6.0f - Mathf.Sqrt(lx * lx + pyFromTop * pyFromTop));
                    if (neLand && !nLand && !eLand) cBoost = Mathf.Max(cBoost, 6.0f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + pyFromTop * pyFromTop));
                    if (swLand && !sLand && !wLand) cBoost = Mathf.Max(cBoost, 5.0f - Mathf.Sqrt(lx * lx + ly * ly));
                    if (seLand && !sLand && !eLand) cBoost = Mathf.Max(cBoost, 5.0f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + ly * ly));

                    float pen = Mathf.Max(Mathf.Max(dN, dS), Mathf.Max(Mathf.Max(dW, dE), cBoost));
                    if (pen < -1.8f)
                    {
                        int clrRow0 = (ly * 2) * TilePx + (lx * 2);
                        _fringePixelBuffer[clrRow0] = colClear;
                        _fringePixelBuffer[clrRow0 + 1] = colClear;
                        _fringePixelBuffer[clrRow0 + TilePx] = colClear;
                        _fringePixelBuffer[clrRow0 + TilePx + 1] = colClear;
                        continue;
                    }

                    // Determine dominant shoreline material (Grass=1 vs Dirt Road=2) cleanly across corners
                    float grassInf = -10f;
                    if (nKind == 1) grassInf = Mathf.Max(grassInf, dN);
                    if (sKind == 1) grassInf = Mathf.Max(grassInf, dS);
                    if (wKind == 1) grassInf = Mathf.Max(grassInf, dW);
                    if (eKind == 1) grassInf = Mathf.Max(grassInf, dE);
                    if (nwKind == 1 && (nKind == 1 || wKind == 1)) grassInf = Mathf.Max(grassInf, 12.0f - Mathf.Sqrt(lx * lx + pyFromTop * pyFromTop));
                    if (neKind == 1 && (nKind == 1 || eKind == 1)) grassInf = Mathf.Max(grassInf, 12.0f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + pyFromTop * pyFromTop));
                    if (swKind == 1 && (sKind == 1 || wKind == 1)) grassInf = Mathf.Max(grassInf, 9.5f - Mathf.Sqrt(lx * lx + ly * ly));
                    if (seKind == 1 && (sKind == 1 || eKind == 1)) grassInf = Mathf.Max(grassInf, 9.5f - Mathf.Sqrt((LP - 1 - lx) * (LP - 1 - lx) + ly * ly));

                    float roadInf = -10f;
                    if (nKind == 2) roadInf = Mathf.Max(roadInf, dN);
                    if (sKind == 2) roadInf = Mathf.Max(roadInf, dS);
                    if (wKind == 2) roadInf = Mathf.Max(roadInf, dW);
                    if (eKind == 2) roadInf = Mathf.Max(roadInf, dE);

                    bool isRoadBank = roadInf > grassInf;
                    float maxOtherDir = Mathf.Max(dS, Mathf.Max(dW, dE));

                    Color32 col;
                    if (pen < -0.6f)
                    {
                        col = (((lx + pyFromTop + variant) % 5) != 0) ? colWaterFoam : colWaterShallow;
                    }
                    else if (pen < 0.4f)
                    {
                        col = colCliffDark;
                    }
                    else if (dN >= maxOtherDir && pen < (isRoadBank ? 3.2f : 4.2f))
                    {
                        col = (pen > 2.0f) ? colCliffTop : colCliffMid;
                    }
                    else if (isRoadBank)
                    {
                        if (pen < 1.6f)
                        {
                            col = (((lx + pyFromTop + variant) % 3) != 0) ? colCliffTop : colDirtEdge;
                        }
                        else if (((lx * 5 + pyFromTop * 7 + variant) % 19) == 0 && pen > 2.2f)
                        {
                            col = colStoneFill;
                        }
                        else
                        {
                            col = colDirtBase;
                        }
                    }
                    else
                    {
                        col = (pen < 1.8f) ? colGrassMid : colGrassBase;
                    }

                    int row0 = (ly * 2) * TilePx + (lx * 2);
                    int row1 = row0 + TilePx;
                    _fringePixelBuffer[row0] = col;
                    _fringePixelBuffer[row0 + 1] = col;
                    _fringePixelBuffer[row1] = col;
                    _fringePixelBuffer[row1 + 1] = col;
                }
            }

            Texture2D tex = new Texture2D(TilePx, TilePx, TextureFormat.RGBA32, false);
            tex.name = $"SmartShorelineTex_{grassMask}_{roadMask}_{variant}";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(_fringePixelBuffer);
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, TilePx, TilePx), new Vector2(0.5f, 0.5f), TilePixelsPerUnit);
            sprite.name = $"SmartShorelineSprite_{grassMask}_{roadMask}_{variant}";
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"SmartShorelineTile_{grassMask}_{roadMask}_{variant}";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            _shorelineTileCache[cacheKey] = tile;
            return tile;
        }

        private static Color ResolveBiomeGroundTint(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.IndustrialZone: return new Color(1.0f, 0.92f, 0.80f, 1f);
                case BiomeType.LabComplex:     return new Color(0.86f, 0.98f, 1.0f, 1f);
                default:                       return Color.white;
            }
        }

        private static Color ResolveRoadTint(RoadVisualState state)
        {
            switch (state)
            {
                case RoadVisualState.Available: return RoadAvailableTint;
                case RoadVisualState.Traveled:  return RoadTraveledTint;
                default:                        return RoadLockedTint;
            }
        }

        private static int CellHash(int cellX, int cellY, int seed)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ (cellX * 73856093);
                h = (h * 397) ^ (cellY * 19349663);
                h ^= h >> 16;
                h *= 0x45d9f3b;
                h ^= h >> 16;
                return h & 0x7FFFFFFF;
            }
        }

        private Vector2 CellToCanvasPosition(Vector2Int cell)
        {
            Vector3 worldCenter = _groundTilemap != null
                ? _groundTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0))
                : new Vector3((cell.x + 0.5f) * TileCellWorldSize, (cell.y + 0.5f) * TileCellWorldSize, 0f);

            Camera cam = Camera.main;
            if (cam != null && _canvasRect != null && _canvasRect.rect.width > 1f)
            {
                Vector3 screenPt = cam.WorldToScreenPoint(worldCenter);
                Vector2 localPt;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    new Vector2(screenPt.x, screenPt.y),
                    null,
                    out localPt))
                {
                    return localPt;
                }
            }

            return new Vector2(worldCenter.x * 100f, worldCenter.y * 100f);
        }

        private void SyncCanvasPositionsIfScreenChanged()
        {
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            for (int i = 0; i < _markerBindings.Count; i++)
            {
                MarkerCellBinding binding = _markerBindings[i];
                if (binding.Marker != null)
                {
                    binding.Marker.GetComponent<RectTransform>().anchoredPosition = CellToCanvasPosition(binding.Cell);
                }
            }

            if (!_isTraveling && _avatarRoot != null)
            {
                _avatarBasePos = CellToCanvasPosition(_avatarCurrentCell);
                _avatarRoot.anchoredPosition = _avatarBasePos;
            }
        }

        private void HandleMarkerClicked(CampaignNode targetNode)
        {
            if (_isTraveling || targetNode == null || sandboxController == null || sandboxController.CurrentRun == null)
            {
                return;
            }

            Vector2Int targetCell;
            if (!_nodeGridCells.TryGetValue(targetNode.NodeId, out targetCell))
            {
                return;
            }

            PlayClickSfx();
            HideTooltip();

            _activeTravelCells.Clear();
            bool sameCell = _avatarCurrentCell == targetCell;

            if (!sameCell)
            {
                RunState run = sandboxController.CurrentRun;
                string fromKey = (!string.IsNullOrEmpty(run.CurrentNodeId) &&
                                  _nodeGridCells.TryGetValue(run.CurrentNodeId, out Vector2Int curNodeCell) &&
                                  curNodeCell == _avatarCurrentCell)
                    ? run.CurrentNodeId
                    : TrailheadNodeKey;

                List<Vector2Int> cachedPath;
                if (_roadPathsByEdgeKey.TryGetValue(GetEdgeKey(fromKey, targetNode.NodeId), out cachedPath) &&
                    cachedPath != null && cachedPath.Count >= 2)
                {
                    for (int i = 0; i < cachedPath.Count; i++)
                    {
                        _activeTravelCells.Add(cachedPath[i]);
                    }
                }
                else
                {
                    BuildWindingCellPath(_avatarCurrentCell, targetCell, run.Seed, 77, _activeTravelCells);
                }
            }
            else
            {
                _activeTravelCells.Add(targetCell);
            }

            _isTraveling = true;
            _travelElapsed = 0f;
            _travelTargetCell = targetCell;
            _activeTravelDuration = sameCell
                ? ShortHopDuration
                : Mathf.Clamp((_activeTravelCells.Count - 1) * NormalTravelStepDuration, 0.35f, 0.85f);
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

                int cellCount = _activeTravelCells.Count;
                if (cellCount >= 2)
                {
                    float exactStep = progress * (cellCount - 1);
                    int stepIdx = Mathf.Clamp(Mathf.FloorToInt(exactStep), 0, cellCount - 2);
                    float localStepT = exactStep - stepIdx;

                    Vector2 posA = CellToCanvasPosition(_activeTravelCells[stepIdx]);
                    Vector2 posB = CellToCanvasPosition(_activeTravelCells[stepIdx + 1]);
                    Vector2 basePos = Vector2.Lerp(posA, posB, localStepT);
                    float stepHop = Mathf.Sin(localStepT * Mathf.PI) * 11f;
                    float tiltDeg = Mathf.Sin(exactStep * Mathf.PI) * 6f;

                    _avatarRoot.anchoredPosition = new Vector2(basePos.x, basePos.y + stepHop);
                    if (_avatarBodyRt != null)
                    {
                        _avatarBodyRt.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
                    }
                }
                else
                {
                    Vector2 stayPos = CellToCanvasPosition(_travelTargetCell);
                    float hop = Mathf.Sin(progress * Mathf.PI) * 10f;
                    _avatarRoot.anchoredPosition = new Vector2(stayPos.x, stayPos.y + hop);
                }

                if (progress >= 1f)
                {
                    CompleteTravel();
                }
            }
            else
            {
                _avatarBasePos = CellToCanvasPosition(_avatarCurrentCell);
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
            _avatarCurrentCell = _travelTargetCell;
            _avatarBasePos = CellToCanvasPosition(_avatarCurrentCell);
            _avatarRoot.anchoredPosition = _avatarBasePos;
            if (_avatarBodyRt != null)
            {
                _avatarBodyRt.localRotation = Quaternion.identity;
            }

            _isTraveling = false;
            _pendingTravelNode = null;
            _activeTravelCells.Clear();
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
            _activeTravelCells.Clear();
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

            string typeDisplay = GetNodeTypeDisplayName(node.NodeType);
            _tooltipTitleText.text = $"{node.Title}  •  Tầng {node.FloorIndex + 1}";
            string modPart = node.Modifier != WorldModifierType.None ? $"\nBiến số: {node.Modifier}" : string.Empty;
            _tooltipMetaText.text = $"Loại: {typeDisplay}   •   Khu vực: {node.Biome}\nNhiệm vụ: {node.Mission}{modPart}";
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
            float clampedX = Mathf.Clamp(localPoint.x, -halfCanvasW + 210f, halfCanvasW - 210f);
            float clampedY = Mathf.Clamp(localPoint.y + 12f, -halfCanvasH + 96f, halfCanvasH - 232f);

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
                pill.NameText.text = u.Name;
                if (pill.RoleBadge != null)
                {
                    pill.RoleBadge.color = GetRoleAccentColor(u.Role);
                }
                if (pill.HpValueText != null)
                {
                    pill.HpValueText.text = $"{u.CurrentHP}/{u.MaxHP} HP";
                }
                float ratio = u.MaxHP > 0 ? Mathf.Clamp01((float)u.CurrentHP / u.MaxHP) : 0f;
                pill.HpFillRect.anchorMax = new Vector2(ratio, 1f);
            }
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
            _markerBindings.Clear();
            _nodeGridCells.Clear();
            _roadPathsByEdgeKey.Clear();
            _sectorNodesBuffer.Clear();
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

        private static Color GetRoleAccentColor(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Guardian: return new Color32(0x33, 0xBF, 0xFF, 255);
                case UnitRole.Vanguard: return new Color32(0x33, 0xD9, 0x8C, 255);
                case UnitRole.Ranger:   return new Color32(0xA6, 0xE6, 0x40, 255);
                case UnitRole.Mage:     return new Color32(0x99, 0x66, 0xF2, 255);
                case UnitRole.Medic:    return new Color32(0x59, 0xF2, 0xA6, 255);
                default:                return UguiThemeFactory.AllyCyan;
            }
        }

        private static string GetNodeTypeDisplayName(CampaignNodeType nodeType)
        {
            switch (nodeType)
            {
                case CampaignNodeType.Combat:      return "Giao tranh";
                case CampaignNodeType.EliteCombat: return "Tinh nhuệ";
                case CampaignNodeType.Recovery:    return "Suối hồi phục";
                case CampaignNodeType.Shop:        return "Kho vũ khí";
                case CampaignNodeType.Event:       return "Sự kiện";
                case CampaignNodeType.Boss:        return "Pháo đài Trùm";
                default:                           return nodeType.ToString();
            }
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
            UnityEngine.UI.Button btn = UguiThemeFactory.CreateTmpButton(name, parent, label, bgColor, 15f);
            UguiThemeFactory.CreateOutlineFrame("Outline", btn.transform, borderColor);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 42f);
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
