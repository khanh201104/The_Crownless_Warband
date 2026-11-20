using System;
using System.Collections.Generic;
using CrownlessWarband.Presentation.BattleView;
using TMPro;
using UnityEngine;

namespace CrownlessWarband.Presentation.UI
{
    public class MainMenuAndSettingsView : MonoBehaviour
    {
        private const string PrefSfxMuted = "CW_SfxMuted";
        private const string PrefSfxVolume = "CW_SfxVolume";
        private const string PrefDefaultSpeed = "CW_DefaultSpeed";
        private const string PrefFullscreen = "CW_Fullscreen";
        private const string PrefResIndex = "CW_ResIndex";

        [SerializeField] private BattleSandboxController sandboxController;

        [Header("Optional Main Menu Art Overrides (Auto-loaded from Resources/UI/MainMenu if null)")]
        [SerializeField] private Sprite backdropKeyArtSprite = null;
        [SerializeField] private Sprite brokenCrownCrestSprite = null;
        [SerializeField] private Sprite tacticalDividerSprite = null;
        [SerializeField] private Sprite cardFrameSprite = null;
        [SerializeField] private Sprite buttonFrameSprite = null;
        [SerializeField] private Sprite iconNewRunSprite = null;
        [SerializeField] private Sprite iconContinueSprite = null;
        [SerializeField] private Sprite iconSettingsSprite = null;
        [SerializeField] private Sprite iconQuitSprite = null;

        private static Sprite _cachedBackdropSprite;
        private static Sprite _cachedCrestSprite;
        private static Sprite _cachedDividerSprite;
        private static Sprite _cachedCardFrameSprite;
        private static Sprite _cachedButtonFrameSprite;
        private static Sprite _cachedIconNewRunSprite;
        private static Sprite _cachedIconContinueSprite;
        private static Sprite _cachedIconSettingsSprite;
        private static Sprite _cachedIconQuitSprite;
        private static Sprite _cachedHorizontalGradientSprite;

        private struct AmbientEmber
        {
            public RectTransform Rt;
            public UnityEngine.UI.Image Img;
            public float BaseX;
            public float Y;
            public float SpeedY;
            public float SwayAmp;
            public float SwayFreq;
            public float Phase;
            public Color BaseColor;
        }

        private readonly AmbientEmber[] _embers = new AmbientEmber[22];
        private RectTransform _crestRect;
        private float _crestBaseY;
        private UnityEngine.UI.Image _continueIconImage;

        private Canvas _mainMenuCanvas;
        private Canvas _settingsCanvas;

        // Main Menu controls
        private TMP_InputField _seedInput;
        private UnityEngine.UI.Button _randomSeedButton;
        private UnityEngine.UI.Button _newRunButton;
        private UnityEngine.UI.Button _continueButton;
        private TextMeshProUGUI _continueLabel;
        private UnityEngine.UI.Button _openSettingsButton;
        private UnityEngine.UI.Button _quitButton;
        private TextMeshProUGUI _menuErrorText;

        // Settings controls
        private UnityEngine.UI.Button _sfxToggleButton;
        private TextMeshProUGUI _sfxToggleLabel;
        private UnityEngine.UI.Slider _sfxVolumeSlider;
        private TextMeshProUGUI _sfxVolumeValueText;
        private readonly UnityEngine.UI.Button[] _speedButtons = new UnityEngine.UI.Button[3];
        private UnityEngine.UI.Button _fullscreenToggleButton;
        private TextMeshProUGUI _fullscreenToggleLabel;
        private UnityEngine.UI.Button _res1080Button;
        private UnityEngine.UI.Button _res720Button;
        private UnityEngine.UI.Button _returnMainMenuButton;
        private UnityEngine.UI.Button _saveCloseButton;

        // Cached state & settings
        private bool _isMuted;
        private float _sfxVolume = 0.50f;
        private PlaybackSpeedMode _defaultSpeed = PlaybackSpeedMode.Speed1x;
        private bool _isFullscreen = true;
        private int _resIndex;

        private bool _isSettingsOpen;
        private bool _hasObservedScreenMode;
        private SandboxScreenMode _lastScreenMode;
        private bool _audioSynced;
        private bool _presenterSynced;

        private static readonly Color BtnNewRunColor = new Color(0.11f, 0.44f, 0.56f, 1f);
        private static readonly Color BtnContinueColor = new Color(0.16f, 0.36f, 0.28f, 1f);
        private static readonly Color BtnQuitColor = new Color(0.32f, 0.14f, 0.16f, 1f);
        private static readonly Color BtnActiveColor = new Color(0.14f, 0.52f, 0.62f, 1f);
        private static readonly Color BtnPositiveColor = new Color(0.16f, 0.48f, 0.34f, 1f);
        private static readonly Color BtnMutedColor = new Color(0.24f, 0.27f, 0.33f, 1f);
        private static readonly Color ErrorTextColor = new Color(0.98f, 0.42f, 0.28f, 1f);

        private void Awake()
        {
            EnsureSandboxController();
            LoadPreferences();
            BuildMainMenuCanvas();
            BuildSettingsCanvas();
            SyncSettingsModalControls();
        }

        private void Start()
        {
            EnsureSandboxController();
            ApplyPreferencesToServices();
            SyncScreenVisibility(true);
            SetSettingsOpen(false);
        }

        private void Update()
        {
            EnsureSandboxController();
            ApplyPreferencesToServices();

            if (sandboxController != null && sandboxController.IsDebugMode && _isSettingsOpen)
            {
                SetSettingsOpen(false);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isSettingsOpen)
                {
                    CloseSettings();
                }
                else if (sandboxController != null &&
                         !sandboxController.IsDebugMode &&
                         !sandboxController.IsPolicyModalOpen &&
                         !sandboxController.IsFinalGameOverModalOpen &&
                         sandboxController.ScreenMode != SandboxScreenMode.MainMenu)
                {
                    OpenSettings();
                }
            }

            if (sandboxController != null)
            {
                sandboxController.IsSettingsOpen = _isSettingsOpen;
            }

            SyncScreenVisibility(false);

            if (_mainMenuCanvas != null && _mainMenuCanvas.gameObject.activeSelf)
            {
                UpdateMenuAmbience();
            }
        }

        private void OnDestroy()
        {
            UnbindButton(_randomSeedButton);
            UnbindButton(_newRunButton);
            UnbindButton(_continueButton);
            UnbindButton(_openSettingsButton);
            UnbindButton(_quitButton);

            UnbindButton(_sfxToggleButton);
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            }

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                UnbindButton(_speedButtons[i]);
            }

            UnbindButton(_fullscreenToggleButton);
            UnbindButton(_res1080Button);
            UnbindButton(_res720Button);
            UnbindButton(_returnMainMenuButton);
            UnbindButton(_saveCloseButton);

            if (_settingsCanvas != null)
            {
                Destroy(_settingsCanvas.gameObject);
                _settingsCanvas = null;
            }
            if (_mainMenuCanvas != null)
            {
                Destroy(_mainMenuCanvas.gameObject);
                _mainMenuCanvas = null;
            }
        }

        public void OpenSettings()
        {
            EnsureSandboxController();
            PlayClickSfx();
            SyncSettingsModalControls();
            UpdateReturnMenuButtonVisibility();
            SetSettingsOpen(true);
        }

        public void CloseSettings()
        {
            SavePreferences();
            PlayClickSfx();
            SetSettingsOpen(false);
        }

        public void ToggleSettings()
        {
            if (_isSettingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        public void RefreshMainMenuState()
        {
            if (_continueButton == null || _continueLabel == null) return;

            bool hasSave = sandboxController != null && sandboxController.HasSaveFile();
            _continueButton.interactable = hasSave;
            _continueLabel.text = hasSave ? "TIẾP TỤC CHIẾN DỊCH" : "TIẾP TỤC (Chưa có Save)";
            if (_continueIconImage != null)
            {
                _continueIconImage.color = hasSave
                    ? Color.white
                    : new Color(0.55f, 0.55f, 0.55f, 0.50f);
            }

            if (_menuErrorText != null)
            {
                _menuErrorText.text = string.Empty;
                _menuErrorText.gameObject.SetActive(false);
            }
        }

        private void EnsureSandboxController()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }
        }

        private void LoadPreferences()
        {
            _isMuted = PlayerPrefs.GetInt(PrefSfxMuted, 0) != 0;
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfxVolume, 0.50f));

            int speedRaw = PlayerPrefs.GetInt(PrefDefaultSpeed, 0);
            if (speedRaw < (int)PlaybackSpeedMode.Speed1x || speedRaw > (int)PlaybackSpeedMode.Speed4x)
            {
                speedRaw = (int)PlaybackSpeedMode.Speed1x;
            }
            _defaultSpeed = (PlaybackSpeedMode)speedRaw;

            _isFullscreen = PlayerPrefs.GetInt(PrefFullscreen, 1) != 0;
            _resIndex = PlayerPrefs.GetInt(PrefResIndex, 0) == 1 ? 1 : 0;
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetInt(PrefSfxMuted, _isMuted ? 1 : 0);
            PlayerPrefs.SetFloat(PrefSfxVolume, _sfxVolume);
            PlayerPrefs.SetInt(PrefDefaultSpeed, (int)_defaultSpeed);
            PlayerPrefs.SetInt(PrefFullscreen, _isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PrefResIndex, _resIndex);
            PlayerPrefs.Save();
        }

        private void ApplyPreferencesToServices()
        {
            if (sandboxController == null) return;

            if (!_audioSynced && sandboxController.AudioManager != null)
            {
                sandboxController.AudioManager.IsMuted = _isMuted;
                sandboxController.AudioManager.MasterVolume = _sfxVolume;
                _audioSynced = true;
            }

            if (!_presenterSynced && sandboxController.EventPresenter != null)
            {
                sandboxController.EventPresenter.SpeedMode = _defaultSpeed;
                _presenterSynced = true;
            }
        }

        private void SyncScreenVisibility(bool force)
        {
            if (_mainMenuCanvas == null) return;

            bool hasController = sandboxController != null;
            SandboxScreenMode currentMode = hasController ? sandboxController.ScreenMode : SandboxScreenMode.MainMenu;
            bool showMainMenu = hasController && currentMode == SandboxScreenMode.MainMenu;

            if (_mainMenuCanvas.gameObject.activeSelf != showMainMenu)
            {
                _mainMenuCanvas.gameObject.SetActive(showMainMenu);
            }

            bool modeChanged = !_hasObservedScreenMode || currentMode != _lastScreenMode;
            if (force || modeChanged)
            {
                _hasObservedScreenMode = true;
                _lastScreenMode = currentMode;

                if (showMainMenu)
                {
                    RefreshMainMenuState();
                }

                if (_isSettingsOpen)
                {
                    UpdateReturnMenuButtonVisibility();
                }
            }
        }

        private void SetSettingsOpen(bool open)
        {
            _isSettingsOpen = open;
            if (_settingsCanvas != null && _settingsCanvas.gameObject.activeSelf != open)
            {
                _settingsCanvas.gameObject.SetActive(open);
            }

            if (sandboxController != null)
            {
                sandboxController.IsSettingsOpen = open;
            }
        }

        private void UpdateReturnMenuButtonVisibility()
        {
            if (_returnMainMenuButton == null) return;
            bool showReturn = sandboxController != null && sandboxController.ScreenMode != SandboxScreenMode.MainMenu;
            _returnMainMenuButton.gameObject.SetActive(showReturn);
        }

        private void BuildMainMenuCanvas()
        {
            _mainMenuCanvas = UguiThemeFactory.CreateOverlayCanvas("MainMenuCanvas", 20);
            RectTransform canvasRt = _mainMenuCanvas.GetComponent<RectTransform>();

            // Full-Screen Key Art Backdrop
            UnityEngine.UI.Image backdrop = UguiThemeFactory.CreatePanel(
                "Backdrop",
                canvasRt,
                new Color(0.05f, 0.07f, 0.11f, 1f),
                false);
            StretchFull(backdrop.rectTransform);

            Sprite backdropSprite = LoadMenuSprite(
                backdropKeyArtSprite,
                ref _cachedBackdropSprite,
                "UI/MainMenu/Menu_KeyArt_Backdrop",
                new Vector2(0.5f, 0.5f));
            if (backdropSprite != null)
            {
                backdrop.sprite = backdropSprite;
                backdrop.type = UnityEngine.UI.Image.Type.Simple;
                backdrop.color = Color.white;
            }

            // Smooth Horizontal Fade Gradient Scrim (no harsh vertical seam)
            UnityEngine.UI.Image leftScrim = UguiThemeFactory.CreatePanel(
                "LeftAtmosphereScrim",
                backdrop.rectTransform,
                new Color(0.03f, 0.05f, 0.08f, 0.72f),
                false);
            leftScrim.sprite = GetHorizontalFadeGradientSprite();
            leftScrim.type = UnityEngine.UI.Image.Type.Simple;
            leftScrim.raycastTarget = false;
            leftScrim.rectTransform.anchorMin = new Vector2(0f, 0f);
            leftScrim.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            leftScrim.rectTransform.offsetMin = Vector2.zero;
            leftScrim.rectTransform.offsetMax = Vector2.zero;

            // Zero-GC Ambient Embers Layer behind MenuCard
            BuildAmbientEmbersLayer(backdrop.rectTransform);

            // Enlarged Ornate Left-Dock Tactical Command Card (600 x 884) with 9-sliced half-width border
            UnityEngine.UI.Image card = UguiThemeFactory.CreatePanel(
                "MenuCard",
                backdrop.rectTransform,
                new Color(0.06f, 0.08f, 0.12f, 0.92f),
                true);
            RectTransform cardRt = card.rectTransform;
            cardRt.anchorMin = new Vector2(0f, 0.5f);
            cardRt.anchorMax = new Vector2(0f, 0.5f);
            cardRt.pivot = new Vector2(0f, 0.5f);
            cardRt.sizeDelta = new Vector2(600f, 884f);
            cardRt.anchoredPosition = new Vector2(48f, 20f);

            Sprite cardFrame = LoadSlicedMenuSprite(
                cardFrameSprite,
                ref _cachedCardFrameSprite,
                "UI/MainMenu/Menu_CardFrame",
                new Vector4(165f, 165f, 165f, 165f));
            if (cardFrame != null)
            {
                card.sprite = cardFrame;
                card.type = UnityEngine.UI.Image.Type.Sliced;
                card.pixelsPerUnitMultiplier = 2.0f;
                card.color = Color.white;
            }
            else
            {
                UguiThemeFactory.CreateOutlineFrame("CardBorder", cardRt, new Color(0.96f, 0.62f, 0.04f, 0.78f));
                UnityEngine.UI.Image cardLeftGoldStrip = UguiThemeFactory.CreatePanel(
                    "CardLeftGoldStrip",
                    cardRt,
                    UguiThemeFactory.AccentGold,
                    false);
                cardLeftGoldStrip.raycastTarget = false;
                RectTransform stripRt = cardLeftGoldStrip.rectTransform;
                stripRt.anchorMin = new Vector2(0f, 0f);
                stripRt.anchorMax = new Vector2(0f, 1f);
                stripRt.pivot = new Vector2(0f, 0.5f);
                stripRt.offsetMin = new Vector2(1f, 8f);
                stripRt.offsetMax = new Vector2(5f, -8f);
            }

            // 1. Broken Crown Crest Emblem (168 x 168 centered top)
            _crestRect = null;
            _crestBaseY = -36f;
            Sprite crestSprite = LoadMenuSprite(
                brokenCrownCrestSprite,
                ref _cachedCrestSprite,
                "UI/MainMenu/Menu_BrokenCrown_Crest",
                new Vector2(0.5f, 0.5f));
            if (crestSprite != null)
            {
                RectTransform crestRt = UguiThemeFactory.CreateRect("CrestEmblem", cardRt);
                UnityEngine.UI.Image crestImg = crestRt.gameObject.AddComponent<UnityEngine.UI.Image>();
                crestImg.sprite = crestSprite;
                crestImg.preserveAspect = true;
                crestImg.raycastTarget = false;
                crestRt.anchorMin = new Vector2(0.5f, 1f);
                crestRt.anchorMax = new Vector2(0.5f, 1f);
                crestRt.pivot = new Vector2(0.5f, 1f);
                crestRt.sizeDelta = new Vector2(168f, 168f);
                crestRt.anchoredPosition = new Vector2(0f, _crestBaseY);
                _crestRect = crestRt;
            }

            // 2. Title & Subtitle (TMP Lilita One + Gold Gradient)
            TextMeshProUGUI titleText = UguiThemeFactory.CreateTmpText(
                "Title",
                cardRt,
                "THE CROWNLESS WARBAND",
                35f,
                Color.white,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 24f;
            titleText.fontSizeMax = 36f;
            titleText.characterSpacing = 2.0f;
            UguiThemeFactory.ApplyTitleGradient(
                titleText,
                new Color(1.00f, 0.94f, 0.65f, 1f),
                new Color(0.92f, 0.54f, 0.04f, 1f));
            SetAnchoredTopBox(titleText.rectTransform, 208f, 48f, 40f);

            TextMeshProUGUI subtitleText = UguiThemeFactory.CreateTmpText(
                "Subtitle",
                cardRt,
                "TÀN THÀNH  •  AI-DRIVEN TACTICAL ROGUELITE",
                15f,
                new Color(0.56f, 0.88f, 1.00f, 1f),
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            subtitleText.characterSpacing = 1.2f;
            SetAnchoredTopBox(subtitleText.rectTransform, 258f, 24f, 40f);

            // 3. Ornate Tactical Divider
            Sprite dividerSprite = LoadMenuSprite(
                tacticalDividerSprite,
                ref _cachedDividerSprite,
                "UI/MainMenu/Menu_TacticalDivider",
                new Vector2(0.5f, 0.5f));
            if (dividerSprite != null)
            {
                RectTransform divRt = UguiThemeFactory.CreateRect("HeaderDivider", cardRt);
                UnityEngine.UI.Image divImg = divRt.gameObject.AddComponent<UnityEngine.UI.Image>();
                divImg.sprite = dividerSprite;
                divImg.preserveAspect = true;
                divImg.color = Color.white;
                divImg.raycastTarget = false;
                SetAnchoredTopBox(divRt, 286f, 40f, 48f);
            }
            else
            {
                UnityEngine.UI.Image divider = UguiThemeFactory.CreatePanel(
                    "HeaderDivider",
                    cardRt,
                    new Color(0.13f, 0.83f, 0.93f, 0.45f),
                    false);
                divider.raycastTarget = false;
                SetAnchoredTopBox(divider.rectTransform, 305f, 2f, 48f);
            }

            // Load Group 2 9-Sliced Button Frame & 4 PNG Icons
            Sprite btnFrame = LoadSlicedMenuSprite(
                buttonFrameSprite,
                ref _cachedButtonFrameSprite,
                "UI/MainMenu/Menu_ButtonFrame",
                new Vector4(86f, 28f, 44f, 28f));
            Sprite iconNewRun = LoadMenuSprite(iconNewRunSprite, ref _cachedIconNewRunSprite, "UI/MainMenu/Icon_NewRun", new Vector2(0.5f, 0.5f));
            Sprite iconContinue = LoadMenuSprite(iconContinueSprite, ref _cachedIconContinueSprite, "UI/MainMenu/Icon_Continue", new Vector2(0.5f, 0.5f));
            Sprite iconSettings = LoadMenuSprite(iconSettingsSprite, ref _cachedIconSettingsSprite, "UI/MainMenu/Icon_Settings", new Vector2(0.5f, 0.5f));
            Sprite iconQuit = LoadMenuSprite(iconQuitSprite, ref _cachedIconQuitSprite, "UI/MainMenu/Icon_Quit", new Vector2(0.5f, 0.5f));

            // 4. Primary Action: CHƠI MỚI
            _newRunButton = UguiThemeFactory.CreateTmpButton(
                "NewRunButton",
                cardRt,
                "CHƠI MỚI",
                new Color(0.10f, 0.28f, 0.38f, 0.98f),
                24f);
            StyleOrnateMenuButton(
                _newRunButton,
                btnFrame,
                iconNewRun,
                42f,
                1.5f,
                new Color(1.00f, 0.98f, 0.90f, 1f),
                new Color(0.10f, 0.28f, 0.38f, 0.98f),
                UguiThemeFactory.AllyCyan,
                UguiThemeFactory.AccentGold);
            SetAnchoredTopBox(_newRunButton.GetComponent<RectTransform>(), 342f, 76f, 46f);
            _newRunButton.onClick.AddListener(OnClickNewRun);

            // 5. Integrated Seed Sub-Bar directly beneath CHƠI MỚI
            UnityEngine.UI.Image seedBg = UguiThemeFactory.CreatePanel(
                "SeedRow",
                cardRt,
                new Color(0.04f, 0.06f, 0.10f, 0.95f),
                true);
            RectTransform seedRow = seedBg.rectTransform;
            UguiThemeFactory.CreateOutlineFrame("SeedRowOutline", seedRow, new Color(0.13f, 0.83f, 0.93f, 0.35f));
            SetAnchoredTopBox(seedRow, 426f, 48f, 52f);

            TextMeshProUGUI seedLabel = UguiThemeFactory.CreateTmpText(
                "SeedLabel",
                seedRow,
                "SEED:",
                15f,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            RectTransform seedLabelRt = seedLabel.rectTransform;
            seedLabelRt.anchorMin = new Vector2(0f, 0f);
            seedLabelRt.anchorMax = new Vector2(0f, 1f);
            seedLabelRt.pivot = new Vector2(0f, 0.5f);
            seedLabelRt.sizeDelta = new Vector2(60f, 0f);
            seedLabelRt.anchoredPosition = new Vector2(14f, 0f);

            _seedInput = UguiThemeFactory.CreateTmpInputField("SeedInput", seedRow, "Ngẫu nhiên (100000–999999)", 15f);
            _seedInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _seedInput.characterLimit = 9;
            RectTransform seedInputRt = _seedInput.GetComponent<RectTransform>();
            seedInputRt.anchorMin = new Vector2(0f, 0f);
            seedInputRt.anchorMax = new Vector2(1f, 1f);
            seedInputRt.offsetMin = new Vector2(76f, 5f);
            seedInputRt.offsetMax = new Vector2(-132f, -5f);

            _randomSeedButton = UguiThemeFactory.CreateTmpButton("RandomSeedButton", seedRow, "Ngẫu nhiên", UguiThemeFactory.PanelBgSoft, 14f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _randomSeedButton.transform, new Color(0.45f, 0.55f, 0.72f, 0.55f));
            RectTransform randRt = _randomSeedButton.GetComponent<RectTransform>();
            randRt.anchorMin = new Vector2(1f, 0f);
            randRt.anchorMax = new Vector2(1f, 1f);
            randRt.pivot = new Vector2(1f, 0.5f);
            randRt.sizeDelta = new Vector2(122f, -10f);
            randRt.anchoredPosition = new Vector2(-5f, 0f);
            _randomSeedButton.onClick.AddListener(OnClickRandomSeed);

            // 6. Secondary Action: TIẾP TỤC
            _continueButton = UguiThemeFactory.CreateTmpButton(
                "ContinueButton",
                cardRt,
                "TIẾP TỤC CHIẾN DỊCH",
                new Color(0.10f, 0.23f, 0.19f, 0.96f),
                22f);
            _continueIconImage = StyleOrnateMenuButton(
                _continueButton,
                btnFrame,
                iconContinue,
                40f,
                1.0f,
                new Color(0.84f, 0.98f, 0.90f, 1f),
                new Color(0.10f, 0.23f, 0.19f, 0.96f),
                new Color(0.24f, 0.66f, 0.50f, 0.70f),
                UguiThemeFactory.HpGreen);
            _continueLabel = _continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (_continueLabel != null)
            {
                _continueLabel.enableAutoSizing = true;
                _continueLabel.fontSizeMin = 16f;
                _continueLabel.fontSizeMax = 22f;
            }
            SetAnchoredTopBox(_continueButton.GetComponent<RectTransform>(), 492f, 72f, 46f);
            _continueButton.onClick.AddListener(OnClickContinue);

            // 7. Utility Action: CÀI ĐẶT
            _openSettingsButton = UguiThemeFactory.CreateTmpButton(
                "SettingsButton",
                cardRt,
                "CÀI ĐẶT HỆ THỐNG",
                new Color(0.10f, 0.13f, 0.19f, 0.95f),
                22f);
            StyleOrnateMenuButton(
                _openSettingsButton,
                btnFrame,
                iconSettings,
                40f,
                1.0f,
                new Color(0.88f, 0.93f, 1.00f, 1f),
                new Color(0.10f, 0.13f, 0.19f, 0.95f),
                new Color(0.45f, 0.55f, 0.72f, 0.55f),
                UguiThemeFactory.AllyCyan);
            SetAnchoredTopBox(_openSettingsButton.GetComponent<RectTransform>(), 578f, 70f, 46f);
            _openSettingsButton.onClick.AddListener(OpenSettings);

            // 8. Exit Action: THOÁT GAME
            _quitButton = UguiThemeFactory.CreateTmpButton(
                "QuitButton",
                cardRt,
                "THOÁT GAME",
                new Color(0.17f, 0.09f, 0.11f, 0.95f),
                22f);
            StyleOrnateMenuButton(
                _quitButton,
                btnFrame,
                iconQuit,
                40f,
                1.2f,
                new Color(1.00f, 0.80f, 0.80f, 1f),
                new Color(0.17f, 0.09f, 0.11f, 0.95f),
                new Color(0.75f, 0.28f, 0.30f, 0.65f),
                UguiThemeFactory.EnemyCrimson);
            SetAnchoredTopBox(_quitButton.GetComponent<RectTransform>(), 662f, 70f, 46f);
            _quitButton.onClick.AddListener(OnClickQuit);

            // Inline error feedback message
            _menuErrorText = UguiThemeFactory.CreateTmpText(
                "MenuErrorText",
                cardRt,
                string.Empty,
                14f,
                ErrorTextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            SetAnchoredTopBox(_menuErrorText.rectTransform, 740f, 28f, 46f);
            _menuErrorText.gameObject.SetActive(false);

            // Card Bottom Tactical Tag
            TextMeshProUGUI cardTag = UguiThemeFactory.CreateTmpText(
                "CardTacticalTag",
                cardRt,
                "SQUAD AI   •   EXPLAINABLE UTILITY AI   •   3x3 FORMATION",
                12f,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            cardTag.characterSpacing = 1.0f;
            SetAnchoredBottomBox(cardTag.rectTransform, 44f, 24f, 46f);

            // Full-Width Bottom Footer Bar
            UnityEngine.UI.Image footerBar = UguiThemeFactory.CreatePanel(
                "BottomFooterBar",
                backdrop.rectTransform,
                new Color(0.03f, 0.05f, 0.08f, 0.92f),
                false);
            footerBar.raycastTarget = false;
            RectTransform footerRt = footerBar.rectTransform;
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0f);
            footerRt.pivot = new Vector2(0.5f, 0f);
            footerRt.sizeDelta = new Vector2(0f, 42f);
            footerRt.anchoredPosition = Vector2.zero;

            UnityEngine.UI.Image footerTopLine = UguiThemeFactory.CreatePanel(
                "FooterTopLine",
                footerRt,
                new Color(0.96f, 0.62f, 0.04f, 0.40f),
                false);
            footerTopLine.raycastTarget = false;
            RectTransform topLineRt = footerTopLine.rectTransform;
            topLineRt.anchorMin = new Vector2(0f, 1f);
            topLineRt.anchorMax = new Vector2(1f, 1f);
            topLineRt.pivot = new Vector2(0.5f, 1f);
            topLineRt.sizeDelta = new Vector2(0f, 1f);
            topLineRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI creditText = UguiThemeFactory.CreateTmpText(
                "CreditText",
                footerRt,
                "Đồ án Tốt nghiệp ĐHBK Hà Nội (HUST) — Phạm Quốc Khánh (20225138)",
                14f,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            RectTransform creditRt = creditText.rectTransform;
            creditRt.anchorMin = new Vector2(0f, 0f);
            creditRt.anchorMax = new Vector2(0.52f, 1f);
            creditRt.offsetMin = new Vector2(28f, 0f);
            creditRt.offsetMax = Vector2.zero;

            TextMeshProUGUI shortcutsText = UguiThemeFactory.CreateTmpText(
                "FooterShortcutsText",
                footerRt,
                "[F1–F7] Kịch bản Demo AI   •   [F3] Explainable AI   •   [F8] Debug Sandbox   •   v2.0",
                13f,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            RectTransform shortcutsRt = shortcutsText.rectTransform;
            shortcutsRt.anchorMin = new Vector2(0.48f, 0f);
            shortcutsRt.anchorMax = new Vector2(1f, 1f);
            shortcutsRt.offsetMin = Vector2.zero;
            shortcutsRt.offsetMax = new Vector2(-28f, 0f);
        }

        private void BuildSettingsCanvas()
        {
            _settingsCanvas = UguiThemeFactory.CreateOverlayCanvas("SettingsCanvas", 50);
            RectTransform canvasRt = _settingsCanvas.GetComponent<RectTransform>();

            // Dimmed click-blocking backdrop
            UnityEngine.UI.Image dimmer = UguiThemeFactory.CreatePanel(
                "DimmerBackdrop",
                canvasRt,
                new Color(0f, 0f, 0f, 0.72f),
                false);
            dimmer.raycastTarget = true;
            StretchFull(dimmer.rectTransform);

            // Centered Modal (560 x 540)
            UnityEngine.UI.Image modal = UguiThemeFactory.CreatePanel(
                "SettingsModal",
                dimmer.rectTransform,
                UguiThemeFactory.PanelBg,
                true);
            RectTransform modalRt = modal.rectTransform;
            modalRt.anchorMin = new Vector2(0.5f, 0.5f);
            modalRt.anchorMax = new Vector2(0.5f, 0.5f);
            modalRt.pivot = new Vector2(0.5f, 0.5f);
            modalRt.sizeDelta = new Vector2(560f, 540f);
            modalRt.anchoredPosition = Vector2.zero;
            UguiThemeFactory.CreateOutlineFrame("ModalBorder", modalRt, UguiThemeFactory.AllyCyan);

            TextMeshProUGUI header = UguiThemeFactory.CreateTmpText(
                "Header",
                modalRt,
                "CÀI ĐẶT HỆ THỐNG",
                26f,
                Color.white,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            UguiThemeFactory.ApplyTitleGradient(
                header,
                new Color(1.00f, 0.94f, 0.65f, 1f),
                new Color(0.92f, 0.54f, 0.04f, 1f));
            SetAnchoredTopBox(header.rectTransform, 22f, 38f, 28f);

            UnityEngine.UI.Image headerLine = UguiThemeFactory.CreatePanel(
                "HeaderDivider",
                modalRt,
                new Color(0.13f, 0.83f, 0.93f, 0.35f),
                false);
            headerLine.raycastTarget = false;
            SetAnchoredTopBox(headerLine.rectTransform, 68f, 2f, 28f);

            // Section 1: SFX
            TextMeshProUGUI sfxSection = UguiThemeFactory.CreateTmpText(
                "SfxSectionTitle",
                modalRt,
                "1. ÂM THANH (SFX)",
                14f,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            SetAnchoredTopBox(sfxSection.rectTransform, 84f, 24f, 30f);

            RectTransform sfxRow1 = UguiThemeFactory.CreateRect("SfxToggleRow", modalRt);
            SetAnchoredTopBox(sfxRow1, 112f, 38f, 30f);

            TextMeshProUGUI sfxLabel = UguiThemeFactory.CreateTmpText(
                "SfxLabel",
                sfxRow1,
                "Hiệu ứng Âm thanh (SFX)",
                16f,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            SetLeftAnchoredBox(sfxLabel.rectTransform, 0f, 260f, 38f);

            _sfxToggleButton = UguiThemeFactory.CreateTmpButton("SfxToggleButton", sfxRow1, "SFX: BẬT", BtnPositiveColor, 15f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _sfxToggleButton.transform, new Color(1f, 1f, 1f, 0.22f));
            _sfxToggleLabel = _sfxToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            SetRightAnchoredBox(_sfxToggleButton.GetComponent<RectTransform>(), 0f, 160f, 36f);
            _sfxToggleButton.onClick.AddListener(OnClickToggleSfx);

            RectTransform sfxRow2 = UguiThemeFactory.CreateRect("SfxVolumeRow", modalRt);
            SetAnchoredTopBox(sfxRow2, 160f, 34f, 30f);

            TextMeshProUGUI volLabel = UguiThemeFactory.CreateTmpText(
                "VolumeLabel",
                sfxRow2,
                "Âm lượng",
                16f,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            SetLeftAnchoredBox(volLabel.rectTransform, 0f, 120f, 34f);

            _sfxVolumeSlider = UguiThemeFactory.CreateSlider("SfxVolumeSlider", sfxRow2, 0f, 1f, _sfxVolume);
            RectTransform sliderRt = _sfxVolumeSlider.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0f, 0.5f);
            sliderRt.anchorMax = new Vector2(1f, 0.5f);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.offsetMin = new Vector2(130f, -14f);
            sliderRt.offsetMax = new Vector2(-68f, 14f);
            _sfxVolumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);

            _sfxVolumeValueText = UguiThemeFactory.CreateTmpText(
                "VolumePercentText",
                sfxRow2,
                "50%",
                15f,
                UguiThemeFactory.EnergyGold,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            SetRightAnchoredBox(_sfxVolumeValueText.rectTransform, 0f, 58f, 34f);

            // Section 2: Default Combat Playback Speed
            TextMeshProUGUI speedSection = UguiThemeFactory.CreateTmpText(
                "SpeedSectionTitle",
                modalRt,
                "2. TỐC ĐỘ DIỄN HOẠT MẶC ĐỊNH",
                14f,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            SetAnchoredTopBox(speedSection.rectTransform, 214f, 24f, 30f);

            RectTransform speedRow = UguiThemeFactory.CreateRect("SpeedButtonsRow", modalRt);
            SetAnchoredTopBox(speedRow, 244f, 42f, 30f);

            string[] speedLabels = new string[] { "1x", "2x", "4x" };
            for (int i = 0; i < 3; i++)
            {
                int capturedIndex = i;
                UnityEngine.UI.Button speedBtn = UguiThemeFactory.CreateTmpButton(
                    $"Speed_{speedLabels[i]}",
                    speedRow,
                    speedLabels[i],
                    UguiThemeFactory.PanelBgSoft,
                    15f);
                UguiThemeFactory.CreateOutlineFrame("Outline", speedBtn.transform, new Color(1f, 1f, 1f, 0.22f));
                RectTransform btnRt = speedBtn.GetComponent<RectTransform>();
                float minX = i / 3f;
                float maxX = (i + 1) / 3f;
                btnRt.anchorMin = new Vector2(minX, 0f);
                btnRt.anchorMax = new Vector2(maxX, 1f);
                btnRt.offsetMin = new Vector2(i == 0 ? 0f : 5f, 0f);
                btnRt.offsetMax = new Vector2(i == 2 ? 0f : -5f, 0f);

                speedBtn.onClick.AddListener(() => OnClickSpeedPreset((PlaybackSpeedMode)capturedIndex));
                _speedButtons[i] = speedBtn;
            }

            // Section 3: Display (PC Build)
            TextMeshProUGUI displaySection = UguiThemeFactory.CreateTmpText(
                "DisplaySectionTitle",
                modalRt,
                "3. HIỂN THỊ (PC BUILD)",
                14f,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            SetAnchoredTopBox(displaySection.rectTransform, 306f, 24f, 30f);

            _fullscreenToggleButton = UguiThemeFactory.CreateTmpButton(
                "FullscreenToggleButton",
                modalRt,
                "Toàn màn hình (Fullscreen Window)",
                UguiThemeFactory.PanelBgSoft,
                15f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _fullscreenToggleButton.transform, new Color(1f, 1f, 1f, 0.22f));
            _fullscreenToggleLabel = _fullscreenToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            SetAnchoredTopBox(_fullscreenToggleButton.GetComponent<RectTransform>(), 336f, 40f, 30f);
            _fullscreenToggleButton.onClick.AddListener(OnClickToggleFullscreen);

            RectTransform resRow = UguiThemeFactory.CreateRect("ResolutionRow", modalRt);
            SetAnchoredTopBox(resRow, 386f, 40f, 30f);

            _res1080Button = UguiThemeFactory.CreateTmpButton("Res1080Button", resRow, "1920 x 1080", UguiThemeFactory.PanelBgSoft, 15f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _res1080Button.transform, new Color(1f, 1f, 1f, 0.22f));
            RectTransform res1080Rt = _res1080Button.GetComponent<RectTransform>();
            res1080Rt.anchorMin = new Vector2(0f, 0f);
            res1080Rt.anchorMax = new Vector2(0.5f, 1f);
            res1080Rt.offsetMin = Vector2.zero;
            res1080Rt.offsetMax = new Vector2(-6f, 0f);
            _res1080Button.onClick.AddListener(() => OnClickResolution(0));

            _res720Button = UguiThemeFactory.CreateTmpButton("Res720Button", resRow, "1280 x 720", UguiThemeFactory.PanelBgSoft, 15f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _res720Button.transform, new Color(1f, 1f, 1f, 0.22f));
            RectTransform res720Rt = _res720Button.GetComponent<RectTransform>();
            res720Rt.anchorMin = new Vector2(0.5f, 0f);
            res720Rt.anchorMax = new Vector2(1f, 1f);
            res720Rt.offsetMin = new Vector2(6f, 0f);
            res720Rt.offsetMax = Vector2.zero;
            _res720Button.onClick.AddListener(() => OnClickResolution(1));

            // Bottom Action Row
            UnityEngine.UI.Image footerLine = UguiThemeFactory.CreatePanel(
                "FooterDivider",
                modalRt,
                new Color(1f, 1f, 1f, 0.12f),
                false);
            footerLine.raycastTarget = false;
            SetAnchoredBottomBox(footerLine.rectTransform, 84f, 1f, 30f);

            RectTransform bottomRow = UguiThemeFactory.CreateRect("BottomActionsRow", modalRt);
            SetAnchoredBottomBox(bottomRow, 24f, 46f, 30f);

            _returnMainMenuButton = UguiThemeFactory.CreateTmpButton(
                "ReturnMainMenuButton",
                bottomRow,
                "Quay về Main Menu",
                BtnQuitColor,
                15f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _returnMainMenuButton.transform, new Color(0.75f, 0.28f, 0.30f, 0.7f));
            RectTransform returnRt = _returnMainMenuButton.GetComponent<RectTransform>();
            returnRt.anchorMin = new Vector2(0f, 0f);
            returnRt.anchorMax = new Vector2(0.48f, 1f);
            returnRt.offsetMin = Vector2.zero;
            returnRt.offsetMax = Vector2.zero;
            _returnMainMenuButton.onClick.AddListener(OnClickReturnToMainMenu);

            _saveCloseButton = UguiThemeFactory.CreateTmpButton(
                "SaveCloseButton",
                bottomRow,
                "Lưu & Đóng",
                BtnNewRunColor,
                16f);
            UguiThemeFactory.CreateOutlineFrame("Outline", _saveCloseButton.transform, UguiThemeFactory.AllyCyan);
            RectTransform saveCloseRt = _saveCloseButton.GetComponent<RectTransform>();
            saveCloseRt.anchorMin = new Vector2(0.52f, 0f);
            saveCloseRt.anchorMax = new Vector2(1f, 1f);
            saveCloseRt.offsetMin = Vector2.zero;
            saveCloseRt.offsetMax = Vector2.zero;
            _saveCloseButton.onClick.AddListener(OnClickSaveAndClose);
        }

        private void SyncSettingsModalControls()
        {
            if (_sfxToggleButton != null && _sfxToggleLabel != null)
            {
                _sfxToggleLabel.text = _isMuted ? "SFX: TẮT" : "SFX: BẬT";
                SetButtonColor(_sfxToggleButton, _isMuted ? BtnMutedColor : BtnPositiveColor);
            }

            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.SetValueWithoutNotify(_sfxVolume);
            }

            if (_sfxVolumeValueText != null)
            {
                int pct = Mathf.RoundToInt(_sfxVolume * 100f);
                _sfxVolumeValueText.text = $"{pct}%";
            }

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null) continue;
                bool selected = (int)_defaultSpeed == i;
                SetButtonColor(_speedButtons[i], selected ? BtnActiveColor : UguiThemeFactory.PanelBgSoft);
            }

            if (_fullscreenToggleButton != null && _fullscreenToggleLabel != null)
            {
                _fullscreenToggleLabel.text = _isFullscreen
                    ? "Toàn màn hình (Fullscreen Window)"
                    : "Cửa sổ (Windowed)";
                SetButtonColor(_fullscreenToggleButton, _isFullscreen ? BtnActiveColor : UguiThemeFactory.PanelBgSoft);
            }

            if (_res1080Button != null)
            {
                SetButtonColor(_res1080Button, _resIndex == 0 ? BtnActiveColor : UguiThemeFactory.PanelBgSoft);
            }

            if (_res720Button != null)
            {
                SetButtonColor(_res720Button, _resIndex == 1 ? BtnActiveColor : UguiThemeFactory.PanelBgSoft);
            }
        }

        private void OnClickRandomSeed()
        {
            PlayClickSfx();
            if (_seedInput != null)
            {
                int randSeed = UnityEngine.Random.Range(100000, 1000000);
                _seedInput.text = randSeed.ToString();
            }
        }

        private void OnClickNewRun()
        {
            EnsureSandboxController();
            PlayClickSfx();
            if (sandboxController == null) return;

            int parsedSeed = 0;
            if (_seedInput != null && !string.IsNullOrWhiteSpace(_seedInput.text))
            {
                if (!int.TryParse(_seedInput.text.Trim(), out parsedSeed))
                {
                    parsedSeed = 0;
                }
            }

            if (_menuErrorText != null)
            {
                _menuErrorText.text = string.Empty;
                _menuErrorText.gameObject.SetActive(false);
            }

            sandboxController.StartNewRun(parsedSeed);
        }

        private void OnClickContinue()
        {
            EnsureSandboxController();
            PlayClickSfx();
            if (sandboxController == null) return;

            bool loaded = sandboxController.ContinueSavedRun();
            if (!loaded && _menuErrorText != null)
            {
                _menuErrorText.text = string.IsNullOrEmpty(sandboxController.LastStatusMessage)
                    ? "Không thể nạp file save."
                    : sandboxController.LastStatusMessage;
                _menuErrorText.gameObject.SetActive(true);
            }
        }

        private void OnClickQuit()
        {
            PlayClickSfx();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnClickToggleSfx()
        {
            _isMuted = !_isMuted;
            EnsureSandboxController();
            if (sandboxController != null && sandboxController.AudioManager != null)
            {
                sandboxController.AudioManager.IsMuted = _isMuted;
            }
            if (!_isMuted)
            {
                PlayClickSfx();
            }
            SyncSettingsModalControls();
        }

        private void OnVolumeSliderChanged(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);
            EnsureSandboxController();
            if (sandboxController != null && sandboxController.AudioManager != null)
            {
                sandboxController.AudioManager.MasterVolume = _sfxVolume;
            }

            if (_sfxVolumeValueText != null)
            {
                int pct = Mathf.RoundToInt(_sfxVolume * 100f);
                _sfxVolumeValueText.text = $"{pct}%";
            }
        }

        private void OnClickSpeedPreset(PlaybackSpeedMode mode)
        {
            PlayClickSfx();
            _defaultSpeed = mode;
            EnsureSandboxController();
            if (sandboxController != null && sandboxController.EventPresenter != null)
            {
                sandboxController.EventPresenter.SpeedMode = _defaultSpeed;
            }
            SyncSettingsModalControls();
        }

        private void OnClickToggleFullscreen()
        {
            PlayClickSfx();
            _isFullscreen = !_isFullscreen;
            ApplyResolutionSettings();
            SyncSettingsModalControls();
        }

        private void OnClickResolution(int index)
        {
            PlayClickSfx();
            _resIndex = index == 1 ? 1 : 0;
            ApplyResolutionSettings();
            SyncSettingsModalControls();
        }

        private void OnClickReturnToMainMenu()
        {
            EnsureSandboxController();
            PlayClickSfx();
            SetSettingsOpen(false);
            sandboxController?.ReturnToMainMenu();
        }

        private void OnClickSaveAndClose()
        {
            SavePreferences();
            CloseSettings();
        }

        private void ApplyResolutionSettings()
        {
            int width = _resIndex == 1 ? 1280 : 1920;
            int height = _resIndex == 1 ? 720 : 1080;
            FullScreenMode mode = _isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, mode);
        }

        private void PlayClickSfx()
        {
            if (sandboxController != null && sandboxController.AudioManager != null)
            {
                sandboxController.AudioManager.PlayUiClick();
            }
        }

        private void BuildAmbientEmbersLayer(RectTransform parent)
        {
            RectTransform layerRt = UguiThemeFactory.CreateRect("AmbientEmbersLayer", parent);
            StretchFull(layerRt);

            Sprite dotSprite = UguiThemeFactory.GetCircleSprite();
            for (int i = 0; i < _embers.Length; i++)
            {
                float frac = (float)i / _embers.Length;
                bool isCyanMote = i >= _embers.Length - 4;

                float baseX = isCyanMote
                    ? 840f + (i - (_embers.Length - 4)) * 260f
                    : 680f + ((i * 193) % 1180);
                float startY = ((i * 137) % 1080);
                float size = isCyanMote ? 5f + (i % 2) * 2f : 4f + (i % 4) * 1.5f;
                float speedY = isCyanMote ? 18f + (i % 3) * 6f : 26f + (i % 5) * 9f;
                float swayAmp = 10f + (i % 4) * 5f;
                float swayFreq = 0.7f + (i % 5) * 0.25f;
                float phase = frac * Mathf.PI * 2f;

                Color baseCol = isCyanMote
                    ? new Color(0.25f, 0.88f, 0.98f, 0.65f)
                    : Color.Lerp(
                        new Color(0.98f, 0.45f, 0.10f, 0.75f),
                        new Color(1.00f, 0.78f, 0.22f, 0.82f),
                        (i % 4) / 3f);

                RectTransform rt = UguiThemeFactory.CreateRect($"Ember_{i}", layerRt);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(baseX, startY);

                UnityEngine.UI.Image img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.sprite = dotSprite;
                img.color = baseCol;
                img.raycastTarget = false;

                _embers[i] = new AmbientEmber
                {
                    Rt = rt,
                    Img = img,
                    BaseX = baseX,
                    Y = startY,
                    SpeedY = speedY,
                    SwayAmp = swayAmp,
                    SwayFreq = swayFreq,
                    Phase = phase,
                    BaseColor = baseCol
                };
            }
        }

        private void UpdateMenuAmbience()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            if (_crestRect != null)
            {
                Vector2 pos = _crestRect.anchoredPosition;
                pos.y = _crestBaseY + Mathf.Sin(now * 1.4f) * 2.5f;
                _crestRect.anchoredPosition = pos;
            }

            if (dt <= 0f) return;

            for (int i = 0; i < _embers.Length; i++)
            {
                AmbientEmber e = _embers[i];
                if (e.Rt == null || e.Img == null) continue;

                e.Y += e.SpeedY * dt;
                if (e.Y > 1120f)
                {
                    e.Y = -20f;
                }

                float x = e.BaseX + Mathf.Sin(now * e.SwayFreq + e.Phase) * e.SwayAmp;
                e.Rt.anchoredPosition = new Vector2(x, e.Y);

                float pulse = 0.55f + 0.45f * Mathf.Sin(now * 2.2f + e.Phase);
                float heightFade = e.Y < 80f
                    ? Mathf.Clamp01(e.Y / 80f)
                    : (e.Y > 920f ? Mathf.Clamp01((1120f - e.Y) / 200f) : 1f);

                Color c = e.BaseColor;
                c.a = e.BaseColor.a * pulse * heightFade;
                e.Img.color = c;

                _embers[i] = e;
            }
        }

        private static Sprite LoadSlicedMenuSprite(Sprite overrideSprite, ref Sprite cache, string resourcePath, Vector4 border)
        {
            if (overrideSprite != null)
            {
                return overrideSprite;
            }
            if (cache != null)
            {
                return cache;
            }

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                cache = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0u,
                    SpriteMeshType.FullRect,
                    border);
                cache.name = resourcePath;
                return cache;
            }

            Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
            if (loadedSprite != null)
            {
                cache = loadedSprite;
                return loadedSprite;
            }

            return null;
        }

        private static Sprite GetHorizontalFadeGradientSprite()
        {
            if (_cachedHorizontalGradientSprite != null)
            {
                return _cachedHorizontalGradientSprite;
            }

            const int width = 64;
            Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            tex.name = "MainMenuHorizontalFadeTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width];
            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (width - 1);
                float alpha = (1f - t) * (1f - t) * (1f - t * 0.35f);
                pixels[x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
            tex.SetPixels(pixels);
            tex.Apply();

            _cachedHorizontalGradientSprite = Sprite.Create(tex, new Rect(0f, 0f, width, 1f), new Vector2(0.5f, 0.5f), 100f);
            _cachedHorizontalGradientSprite.name = "MainMenuHorizontalFadeSprite";
            return _cachedHorizontalGradientSprite;
        }

        private static UnityEngine.UI.Image StyleOrnateMenuButton(
            UnityEngine.UI.Button button,
            Sprite buttonFrame,
            Sprite iconSprite,
            float iconSize,
            float charSpacing,
            Color tintColor,
            Color fallbackBaseColor,
            Color fallbackOutlineColor,
            Color fallbackAccentColor)
        {
            if (button == null) return null;

            UnityEngine.UI.Image bgImage = button.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image iconImage = null;

            if (buttonFrame != null && bgImage != null)
            {
                bgImage.sprite = buttonFrame;
                bgImage.type = UnityEngine.UI.Image.Type.Sliced;
                bgImage.pixelsPerUnitMultiplier = 2.0f;

                UnityEngine.UI.ColorBlock colors = button.colors;
                colors.normalColor = tintColor;
                colors.highlightedColor = Color.Lerp(tintColor, Color.white, 0.25f);
                colors.pressedColor = Color.Lerp(tintColor, Color.black, 0.18f);
                colors.selectedColor = tintColor;
                colors.disabledColor = new Color(tintColor.r * 0.45f, tintColor.g * 0.45f, tintColor.b * 0.45f, 0.55f);
                button.colors = colors;

                if (iconSprite != null)
                {
                    RectTransform iconRt = UguiThemeFactory.CreateRect("ButtonIcon", button.transform);
                    iconImage = iconRt.gameObject.AddComponent<UnityEngine.UI.Image>();
                    iconImage.sprite = iconSprite;
                    iconImage.preserveAspect = true;
                    iconImage.raycastTarget = false;
                    iconRt.anchorMin = new Vector2(0f, 0.5f);
                    iconRt.anchorMax = new Vector2(0f, 0.5f);
                    iconRt.pivot = new Vector2(0f, 0.5f);
                    iconRt.sizeDelta = new Vector2(iconSize, iconSize);
                    iconRt.anchoredPosition = new Vector2(52f, 0f);
                }

                TextMeshProUGUI labelText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (labelText != null)
                {
                    labelText.alignment = TextAlignmentOptions.Left;
                    labelText.characterSpacing = charSpacing;
                    RectTransform labelRt = labelText.rectTransform;
                    labelRt.offsetMin = new Vector2(iconSprite != null ? 106f : 56f, 6f);
                    labelRt.offsetMax = new Vector2(-28f, -6f);
                }
            }
            else
            {
                StyleTacticalMenuButton(button, fallbackBaseColor, fallbackOutlineColor, fallbackAccentColor);
            }

            return iconImage;
        }

        private static Sprite LoadMenuSprite(Sprite overrideSprite, ref Sprite cache, string resourcePath, Vector2 pivot)
        {
            if (overrideSprite != null)
            {
                return overrideSprite;
            }
            if (cache != null)
            {
                return cache;
            }

            Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
            if (loadedSprite != null)
            {
                cache = loadedSprite;
                return loadedSprite;
            }

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                cache = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, 100f);
                cache.name = resourcePath;
                return cache;
            }

            return null;
        }

        private static void StyleTacticalMenuButton(
            UnityEngine.UI.Button button,
            Color baseColor,
            Color outlineColor,
            Color accentBarColor)
        {
            if (button == null) return;

            SetButtonColor(button, baseColor);
            UguiThemeFactory.CreateOutlineFrame("Outline", button.transform, outlineColor);

            UnityEngine.UI.Image accentBar = UguiThemeFactory.CreatePanel("LeftAccentBar", button.transform, accentBarColor, false);
            accentBar.raycastTarget = false;
            RectTransform barRt = accentBar.rectTransform;
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(0f, 1f);
            barRt.pivot = new Vector2(0f, 0.5f);
            barRt.offsetMin = new Vector2(2f, 2f);
            barRt.offsetMax = new Vector2(8f, -2f);

            TextMeshProUGUI labelText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (labelText != null)
            {
                RectTransform labelRt = labelText.rectTransform;
                labelRt.offsetMin = new Vector2(16f, 4f);
                labelRt.offsetMax = new Vector2(-10f, -4f);
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

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchoredTopBox(RectTransform rt, float topOffset, float height, float horizontalPadding)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(horizontalPadding, -(topOffset + height));
            rt.offsetMax = new Vector2(-horizontalPadding, -topOffset);
        }

        private static void SetAnchoredBottomBox(RectTransform rt, float bottomOffset, float height, float horizontalPadding)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(horizontalPadding, bottomOffset);
            rt.offsetMax = new Vector2(-horizontalPadding, bottomOffset + height);
        }

        private static void SetLeftAnchoredBox(RectTransform rt, float leftOffset, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(leftOffset, 0f);
        }

        private static void SetRightAnchoredBox(RectTransform rt, float rightOffset, float width, float height)
        {
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-rightOffset, 0f);
        }
    }
}
