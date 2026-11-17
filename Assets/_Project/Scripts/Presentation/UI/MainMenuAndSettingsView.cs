using System;
using System.Collections.Generic;
using CrownlessWarband.Presentation.BattleView;
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

        private Canvas _mainMenuCanvas;
        private Canvas _settingsCanvas;

        // Main Menu controls
        private UnityEngine.UI.InputField _seedInput;
        private UnityEngine.UI.Button _randomSeedButton;
        private UnityEngine.UI.Button _newRunButton;
        private UnityEngine.UI.Button _continueButton;
        private UnityEngine.UI.Text _continueLabel;
        private UnityEngine.UI.Button _openSettingsButton;
        private UnityEngine.UI.Button _quitButton;
        private UnityEngine.UI.Text _menuErrorText;

        // Settings controls
        private UnityEngine.UI.Button _sfxToggleButton;
        private UnityEngine.UI.Text _sfxToggleLabel;
        private UnityEngine.UI.Slider _sfxVolumeSlider;
        private UnityEngine.UI.Text _sfxVolumeValueText;
        private readonly UnityEngine.UI.Button[] _speedButtons = new UnityEngine.UI.Button[3];
        private UnityEngine.UI.Button _fullscreenToggleButton;
        private UnityEngine.UI.Text _fullscreenToggleLabel;
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
            _continueLabel.text = hasSave ? "TIẾP TỤC" : "TIẾP TỤC (Chưa có Save)";

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

            // Tactical dark backdrop
            UnityEngine.UI.Image backdrop = UguiThemeFactory.CreatePanel(
                "Backdrop",
                canvasRt,
                new Color(0.05f, 0.07f, 0.11f, 1f),
                false);
            StretchFull(backdrop.rectTransform);

            // Subtle decorative atmospheric bands
            UnityEngine.UI.Image upperBand = UguiThemeFactory.CreatePanel(
                "DecorUpperBand",
                backdrop.rectTransform,
                new Color(0.13f, 0.83f, 0.93f, 0.05f),
                false);
            upperBand.raycastTarget = false;
            upperBand.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            upperBand.rectTransform.anchorMax = new Vector2(1f, 0.78f);
            upperBand.rectTransform.offsetMin = Vector2.zero;
            upperBand.rectTransform.offsetMax = Vector2.zero;

            UnityEngine.UI.Image lowerBand = UguiThemeFactory.CreatePanel(
                "DecorLowerBand",
                backdrop.rectTransform,
                new Color(0.96f, 0.62f, 0.04f, 0.04f),
                false);
            lowerBand.raycastTarget = false;
            lowerBand.rectTransform.anchorMin = new Vector2(0f, 0.18f);
            lowerBand.rectTransform.anchorMax = new Vector2(1f, 0.34f);
            lowerBand.rectTransform.offsetMin = Vector2.zero;
            lowerBand.rectTransform.offsetMax = Vector2.zero;

            // Center Card (width 520px, height 530px)
            UnityEngine.UI.Image card = UguiThemeFactory.CreatePanel("MenuCard", backdrop.rectTransform, UguiThemeFactory.PanelBg, true);
            RectTransform cardRt = card.rectTransform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(520f, 530f);
            cardRt.anchoredPosition = Vector2.zero;
            UguiThemeFactory.CreateOutlineFrame("CardBorder", cardRt, UguiThemeFactory.AccentGold);

            // Title & Subtitle
            UnityEngine.UI.Text titleText = UguiThemeFactory.CreateText(
                "Title",
                cardRt,
                "THE CROWNLESS WARBAND",
                28,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 20;
            titleText.resizeTextMaxSize = 28;
            SetAnchoredTopBox(titleText.rectTransform, 28f, 44f, 24f);

            UnityEngine.UI.Text subtitleText = UguiThemeFactory.CreateText(
                "Subtitle",
                cardRt,
                "AI-DRIVEN TACTICAL ROGUELITE",
                14,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetAnchoredTopBox(subtitleText.rectTransform, 78f, 24f, 24f);

            UnityEngine.UI.Image divider = UguiThemeFactory.CreatePanel(
                "HeaderDivider",
                cardRt,
                new Color(0.13f, 0.83f, 0.93f, 0.35f),
                false);
            divider.raycastTarget = false;
            SetAnchoredTopBox(divider.rectTransform, 118f, 2f, 36f);

            // Seed Row
            RectTransform seedRow = UguiThemeFactory.CreateRect("SeedRow", cardRt);
            SetAnchoredTopBox(seedRow, 136f, 44f, 36f);

            UnityEngine.UI.Text seedLabel = UguiThemeFactory.CreateText(
                "SeedLabel",
                seedRow,
                "Seed:",
                16,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            RectTransform seedLabelRt = seedLabel.rectTransform;
            seedLabelRt.anchorMin = new Vector2(0f, 0f);
            seedLabelRt.anchorMax = new Vector2(0f, 1f);
            seedLabelRt.pivot = new Vector2(0f, 0.5f);
            seedLabelRt.sizeDelta = new Vector2(58f, 0f);
            seedLabelRt.anchoredPosition = Vector2.zero;

            _seedInput = UguiThemeFactory.CreateInputField("SeedInput", seedRow, "Ngẫu nhiên (100000–999999)", 15);
            _seedInput.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber;
            _seedInput.characterLimit = 9;
            RectTransform seedInputRt = _seedInput.GetComponent<RectTransform>();
            seedInputRt.anchorMin = new Vector2(0f, 0f);
            seedInputRt.anchorMax = new Vector2(1f, 1f);
            seedInputRt.offsetMin = new Vector2(62f, 0f);
            seedInputRt.offsetMax = new Vector2(-124f, 0f);

            _randomSeedButton = UguiThemeFactory.CreateButton("RandomSeedButton", seedRow, "Ngẫu nhiên", UguiThemeFactory.PanelBgSoft, 14);
            UguiThemeFactory.CreateOutlineFrame("Outline", _randomSeedButton.transform, new Color(0.45f, 0.55f, 0.72f, 0.6f));
            RectTransform randRt = _randomSeedButton.GetComponent<RectTransform>();
            randRt.anchorMin = new Vector2(1f, 0f);
            randRt.anchorMax = new Vector2(1f, 1f);
            randRt.pivot = new Vector2(1f, 0.5f);
            randRt.sizeDelta = new Vector2(116f, 0f);
            randRt.anchoredPosition = Vector2.zero;
            _randomSeedButton.onClick.AddListener(OnClickRandomSeed);

            // 4 Main Vertical Buttons (height 52px, spacing 12px)
            float startY = 200f;
            float btnHeight = 52f;
            float spacing = 12f;

            _newRunButton = UguiThemeFactory.CreateButton("NewRunButton", cardRt, "CHƠI MỚI", BtnNewRunColor, 18);
            UguiThemeFactory.CreateOutlineFrame("Outline", _newRunButton.transform, UguiThemeFactory.AllyCyan);
            SetAnchoredTopBox(_newRunButton.GetComponent<RectTransform>(), startY, btnHeight, 36f);
            _newRunButton.onClick.AddListener(OnClickNewRun);

            _continueButton = UguiThemeFactory.CreateButton("ContinueButton", cardRt, "TIẾP TỤC", BtnContinueColor, 18);
            UguiThemeFactory.CreateOutlineFrame("Outline", _continueButton.transform, new Color(0.24f, 0.66f, 0.50f, 0.75f));
            _continueLabel = _continueButton.GetComponentInChildren<UnityEngine.UI.Text>();
            SetAnchoredTopBox(_continueButton.GetComponent<RectTransform>(), startY + (btnHeight + spacing), btnHeight, 36f);
            _continueButton.onClick.AddListener(OnClickContinue);

            _openSettingsButton = UguiThemeFactory.CreateButton("SettingsButton", cardRt, "CÀI ĐẶT", UguiThemeFactory.PanelBgSoft, 18);
            UguiThemeFactory.CreateOutlineFrame("Outline", _openSettingsButton.transform, new Color(0.45f, 0.55f, 0.72f, 0.65f));
            SetAnchoredTopBox(_openSettingsButton.GetComponent<RectTransform>(), startY + (btnHeight + spacing) * 2f, btnHeight, 36f);
            _openSettingsButton.onClick.AddListener(OpenSettings);

            _quitButton = UguiThemeFactory.CreateButton("QuitButton", cardRt, "THOÁT GAME", BtnQuitColor, 18);
            UguiThemeFactory.CreateOutlineFrame("Outline", _quitButton.transform, new Color(0.75f, 0.28f, 0.30f, 0.7f));
            SetAnchoredTopBox(_quitButton.GetComponent<RectTransform>(), startY + (btnHeight + spacing) * 3f, btnHeight, 36f);
            _quitButton.onClick.AddListener(OnClickQuit);

            // Inline error feedback message
            _menuErrorText = UguiThemeFactory.CreateText(
                "MenuErrorText",
                cardRt,
                string.Empty,
                14,
                ErrorTextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetAnchoredTopBox(_menuErrorText.rectTransform, startY + (btnHeight + spacing) * 4f + 4f, 28f, 36f);
            _menuErrorText.gameObject.SetActive(false);

            // Footer: credit line
            UnityEngine.UI.Text creditText = UguiThemeFactory.CreateText(
                "CreditText",
                cardRt,
                "Đồ án Tốt nghiệp HUST — Phạm Quốc Khánh (20225138)",
                12,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            SetAnchoredBottomBox(creditText.rectTransform, 14f, 24f, 24f);
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

            UnityEngine.UI.Text header = UguiThemeFactory.CreateText(
                "Header",
                modalRt,
                "CÀI ĐẶT HỆ THỐNG",
                24,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetAnchoredTopBox(header.rectTransform, 22f, 38f, 28f);

            UnityEngine.UI.Image headerLine = UguiThemeFactory.CreatePanel(
                "HeaderDivider",
                modalRt,
                new Color(0.13f, 0.83f, 0.93f, 0.35f),
                false);
            headerLine.raycastTarget = false;
            SetAnchoredTopBox(headerLine.rectTransform, 68f, 2f, 28f);

            // Section 1: SFX
            UnityEngine.UI.Text sfxSection = UguiThemeFactory.CreateText(
                "SfxSectionTitle",
                modalRt,
                "1. ÂM THANH (SFX)",
                14,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            SetAnchoredTopBox(sfxSection.rectTransform, 84f, 24f, 30f);

            RectTransform sfxRow1 = UguiThemeFactory.CreateRect("SfxToggleRow", modalRt);
            SetAnchoredTopBox(sfxRow1, 112f, 38f, 30f);

            UnityEngine.UI.Text sfxLabel = UguiThemeFactory.CreateText(
                "SfxLabel",
                sfxRow1,
                "Hiệu ứng Âm thanh (SFX)",
                16,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            SetLeftAnchoredBox(sfxLabel.rectTransform, 0f, 260f, 38f);

            _sfxToggleButton = UguiThemeFactory.CreateButton("SfxToggleButton", sfxRow1, "SFX: BẬT", BtnPositiveColor, 15);
            UguiThemeFactory.CreateOutlineFrame("Outline", _sfxToggleButton.transform, new Color(1f, 1f, 1f, 0.22f));
            _sfxToggleLabel = _sfxToggleButton.GetComponentInChildren<UnityEngine.UI.Text>();
            SetRightAnchoredBox(_sfxToggleButton.GetComponent<RectTransform>(), 0f, 160f, 36f);
            _sfxToggleButton.onClick.AddListener(OnClickToggleSfx);

            RectTransform sfxRow2 = UguiThemeFactory.CreateRect("SfxVolumeRow", modalRt);
            SetAnchoredTopBox(sfxRow2, 160f, 34f, 30f);

            UnityEngine.UI.Text volLabel = UguiThemeFactory.CreateText(
                "VolumeLabel",
                sfxRow2,
                "Âm lượng",
                16,
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

            _sfxVolumeValueText = UguiThemeFactory.CreateText(
                "VolumePercentText",
                sfxRow2,
                "50%",
                15,
                UguiThemeFactory.EnergyGold,
                TextAnchor.MiddleRight,
                FontStyle.Bold);
            SetRightAnchoredBox(_sfxVolumeValueText.rectTransform, 0f, 58f, 34f);

            // Section 2: Default Combat Playback Speed
            UnityEngine.UI.Text speedSection = UguiThemeFactory.CreateText(
                "SpeedSectionTitle",
                modalRt,
                "2. TỐC ĐỘ DIỄN HOẠT MẶC ĐỊNH",
                14,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            SetAnchoredTopBox(speedSection.rectTransform, 214f, 24f, 30f);

            RectTransform speedRow = UguiThemeFactory.CreateRect("SpeedButtonsRow", modalRt);
            SetAnchoredTopBox(speedRow, 244f, 42f, 30f);

            string[] speedLabels = new string[] { "1x", "2x", "4x" };
            for (int i = 0; i < 3; i++)
            {
                int capturedIndex = i;
                UnityEngine.UI.Button speedBtn = UguiThemeFactory.CreateButton(
                    $"Speed_{speedLabels[i]}",
                    speedRow,
                    speedLabels[i],
                    UguiThemeFactory.PanelBgSoft,
                    15);
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
            UnityEngine.UI.Text displaySection = UguiThemeFactory.CreateText(
                "DisplaySectionTitle",
                modalRt,
                "3. HIỂN THỊ (PC BUILD)",
                14,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            SetAnchoredTopBox(displaySection.rectTransform, 306f, 24f, 30f);

            _fullscreenToggleButton = UguiThemeFactory.CreateButton(
                "FullscreenToggleButton",
                modalRt,
                "Toàn màn hình (Fullscreen Window)",
                UguiThemeFactory.PanelBgSoft,
                15);
            UguiThemeFactory.CreateOutlineFrame("Outline", _fullscreenToggleButton.transform, new Color(1f, 1f, 1f, 0.22f));
            _fullscreenToggleLabel = _fullscreenToggleButton.GetComponentInChildren<UnityEngine.UI.Text>();
            SetAnchoredTopBox(_fullscreenToggleButton.GetComponent<RectTransform>(), 336f, 40f, 30f);
            _fullscreenToggleButton.onClick.AddListener(OnClickToggleFullscreen);

            RectTransform resRow = UguiThemeFactory.CreateRect("ResolutionRow", modalRt);
            SetAnchoredTopBox(resRow, 386f, 40f, 30f);

            _res1080Button = UguiThemeFactory.CreateButton("Res1080Button", resRow, "1920 x 1080", UguiThemeFactory.PanelBgSoft, 15);
            UguiThemeFactory.CreateOutlineFrame("Outline", _res1080Button.transform, new Color(1f, 1f, 1f, 0.22f));
            RectTransform res1080Rt = _res1080Button.GetComponent<RectTransform>();
            res1080Rt.anchorMin = new Vector2(0f, 0f);
            res1080Rt.anchorMax = new Vector2(0.5f, 1f);
            res1080Rt.offsetMin = Vector2.zero;
            res1080Rt.offsetMax = new Vector2(-6f, 0f);
            _res1080Button.onClick.AddListener(() => OnClickResolution(0));

            _res720Button = UguiThemeFactory.CreateButton("Res720Button", resRow, "1280 x 720", UguiThemeFactory.PanelBgSoft, 15);
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

            _returnMainMenuButton = UguiThemeFactory.CreateButton(
                "ReturnMainMenuButton",
                bottomRow,
                "Quay về Main Menu",
                BtnQuitColor,
                15);
            UguiThemeFactory.CreateOutlineFrame("Outline", _returnMainMenuButton.transform, new Color(0.75f, 0.28f, 0.30f, 0.7f));
            RectTransform returnRt = _returnMainMenuButton.GetComponent<RectTransform>();
            returnRt.anchorMin = new Vector2(0f, 0f);
            returnRt.anchorMax = new Vector2(0.48f, 1f);
            returnRt.offsetMin = Vector2.zero;
            returnRt.offsetMax = Vector2.zero;
            _returnMainMenuButton.onClick.AddListener(OnClickReturnToMainMenu);

            _saveCloseButton = UguiThemeFactory.CreateButton(
                "SaveCloseButton",
                bottomRow,
                "Lưu & Đóng",
                BtnNewRunColor,
                16);
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
