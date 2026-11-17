using System;
using System.Collections.Generic;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Roguelite;
using CrownlessWarband.Presentation.BattleView;
using UnityEngine;

namespace CrownlessWarband.Presentation.UI
{
    public class BattleModalsView : MonoBehaviour
    {
        private const int MaxRewardCards = 3;
        private const int MaxPolicyTabs = 5;

        private sealed class RewardCardSlot
        {
            public GameObject Root;
            public UnityEngine.UI.Image BorderOutline;
            public UnityEngine.UI.Image BadgeBg;
            public UnityEngine.UI.Text BadgeText;
            public UnityEngine.UI.Text TitleText;
            public UnityEngine.UI.Text DescText;
            public UnityEngine.UI.Button SelectButton;
        }

        private sealed class PolicyTabSlot
        {
            public GameObject Root;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Outline;
            public UnityEngine.UI.Text Label;
            public string BoundUnitId;
        }

        [SerializeField] private BattleSandboxController sandboxController;

        private Canvas _rewardCanvas;
        private Canvas _policyCanvas;

        // 1. Reward Canvas controls
        private UnityEngine.UI.Text _rewardRelicsSubtitle;
        private GameObject _rewardCardsContainer;
        private readonly RewardCardSlot[] _rewardCards = new RewardCardSlot[MaxRewardCards];
        private GameObject _rewardEmptyFallback;
        private UnityEngine.UI.Button _rewardEmptyReturnButton;

        // 2. AI Policy Modal controls
        private UnityEngine.UI.Text _policyScopeText;
        private readonly PolicyTabSlot[] _policyTabs = new PolicyTabSlot[MaxPolicyTabs];
        private UnityEngine.UI.Text _policyUnitSummaryText;
        private UnityEngine.UI.Text _behaviorValueText;
        private UnityEngine.UI.Text _behaviorDescText;
        private UnityEngine.UI.Button _behaviorNextButton;
        private UnityEngine.UI.Text _targetValueText;
        private UnityEngine.UI.Text _targetDescText;
        private UnityEngine.UI.Button _targetNextButton;
        private UnityEngine.UI.Text _conditionValueText;
        private UnityEngine.UI.Text _conditionDescText;
        private UnityEngine.UI.Button _conditionNextButton;
        private UnityEngine.UI.Button _policySaveCloseButton;

        private readonly List<UnitState> _policyUnitsBuffer = new List<UnitState>(8);
        private string _selectedPolicyUnitId;
        private bool _isPolicyOpen;

        // Dirty-tracking state to avoid per-frame allocations
        private bool _rewardDirty = true;
        private int _lastRewardCount = -1;
        private RunState _lastRewardRun;

        private static readonly Color OrbDamageColor = new Color(0.86f, 0.30f, 0.16f, 1f);
        private static readonly Color OrbStunColor = new Color(0.56f, 0.24f, 0.78f, 1f);
        private static readonly Color BtnActiveColor = new Color(0.14f, 0.52f, 0.62f, 1f);
        private static readonly Color BtnGreenColor = new Color(0.14f, 0.48f, 0.32f, 1f);

        private void Awake()
        {
            EnsureReferences();
            BuildRewardCanvas();
            BuildPolicyModalCanvas();

            _rewardCanvas.gameObject.SetActive(false);
            _policyCanvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            EnsureReferences();

            bool hasController = sandboxController != null;
            bool debugActive = hasController && sandboxController.IsDebugMode;
            SandboxScreenMode mode = hasController ? sandboxController.ScreenMode : SandboxScreenMode.MainMenu;

            if (debugActive || mode == SandboxScreenMode.MainMenu)
            {
                if (_isPolicyOpen)
                {
                    ClosePolicyModal();
                }
            }

            if (hasController)
            {
                sandboxController.IsPolicyModalOpen = _isPolicyOpen;
            }

            bool showReward = hasController && !debugActive && mode == SandboxScreenMode.PostBattleReward;
            if (_rewardCanvas.gameObject.activeSelf != showReward)
            {
                _rewardCanvas.gameObject.SetActive(showReward);
                if (showReward)
                {
                    _rewardDirty = true;
                }
            }
            if (showReward)
            {
                RefreshRewardIfDirty();
            }

            bool showPolicy = hasController && !debugActive && mode != SandboxScreenMode.MainMenu && _isPolicyOpen;
            if (_policyCanvas.gameObject.activeSelf != showPolicy)
            {
                _policyCanvas.gameObject.SetActive(showPolicy);
            }
        }

        private void LateUpdate()
        {
            // Đóng AI Policy bằng Esc ở LateUpdate để MainMenuAndSettingsView.Update() không mở Settings đè lên cùng frame
            if (_isPolicyOpen && Input.GetKeyDown(KeyCode.Escape) &&
                (sandboxController == null || !sandboxController.IsSettingsOpen))
            {
                ClosePolicyModal();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _rewardCards.Length; i++)
            {
                if (_rewardCards[i] != null)
                {
                    UnbindButton(_rewardCards[i].SelectButton);
                }
            }
            UnbindButton(_rewardEmptyReturnButton);

            for (int i = 0; i < _policyTabs.Length; i++)
            {
                if (_policyTabs[i] != null)
                {
                    UnbindButton(_policyTabs[i].Button);
                }
            }
            UnbindButton(_behaviorNextButton);
            UnbindButton(_targetNextButton);
            UnbindButton(_conditionNextButton);
            UnbindButton(_policySaveCloseButton);

            if (_rewardCanvas != null)
            {
                Destroy(_rewardCanvas.gameObject);
                _rewardCanvas = null;
            }
            if (_policyCanvas != null)
            {
                Destroy(_policyCanvas.gameObject);
                _policyCanvas = null;
            }
        }

        public void OpenPolicyModal(string preferredUnitId = null)
        {
            EnsureReferences();
            if (sandboxController == null) return;

            _selectedPolicyUnitId = preferredUnitId;
            _isPolicyOpen = true;
            sandboxController.IsPolicyModalOpen = true;
            RefreshPolicyModalContents();
            if (_policyCanvas != null)
            {
                _policyCanvas.gameObject.SetActive(true);
            }
        }

        public void ClosePolicyModal()
        {
            _isPolicyOpen = false;
            if (sandboxController != null)
            {
                sandboxController.IsPolicyModalOpen = false;
            }
            if (_policyCanvas != null)
            {
                _policyCanvas.gameObject.SetActive(false);
            }
        }

        private void EnsureReferences()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }
        }

        // =========================================================================
        // 1. POST-BATTLE REWARD CANVAS (sortingOrder = 30)
        // =========================================================================

        private void BuildRewardCanvas()
        {
            _rewardCanvas = UguiThemeFactory.CreateOverlayCanvas("PostBattleRewardCanvas", 30);
            RectTransform root = _rewardCanvas.GetComponent<RectTransform>();

            UnityEngine.UI.Image dimmer = UguiThemeFactory.CreatePanel(
                "DimmerBackdrop",
                root,
                new Color(0f, 0f, 0f, 0.78f),
                false);
            dimmer.raycastTarget = true;
            StretchFull(dimmer.rectTransform, 0f, 0f);

            UnityEngine.UI.Image card = UguiThemeFactory.CreatePanel(
                "RewardModalCard",
                dimmer.rectTransform,
                UguiThemeFactory.PanelBg,
                true);
            RectTransform cardRt = card.rectTransform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(1120f, 560f);
            cardRt.anchoredPosition = Vector2.zero;
            UguiThemeFactory.CreateOutlineFrame("Outline", cardRt, UguiThemeFactory.AccentGold);

            UnityEngine.UI.Text title = UguiThemeFactory.CreateText(
                "RewardTitle",
                cardRt,
                "PHẦN THƯỞNG CHIẾN THẮNG — CHỌN 1 TRONG 3",
                26,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetTopAnchored(title.rectTransform, 24f, 38f, 32f);

            _rewardRelicsSubtitle = UguiThemeFactory.CreateText(
                "RelicsSubtitle",
                cardRt,
                string.Empty,
                15,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            _rewardRelicsSubtitle.resizeTextForBestFit = true;
            _rewardRelicsSubtitle.resizeTextMinSize = 11;
            _rewardRelicsSubtitle.resizeTextMaxSize = 15;
            _rewardRelicsSubtitle.verticalOverflow = VerticalWrapMode.Truncate;
            SetTopAnchored(_rewardRelicsSubtitle.rectTransform, 66f, 26f, 32f);

            RectTransform cardsRowRt = UguiThemeFactory.CreateRect("RewardCardsRow", cardRt);
            _rewardCardsContainer = cardsRowRt.gameObject;
            cardsRowRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardsRowRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardsRowRt.pivot = new Vector2(0.5f, 0.5f);
            cardsRowRt.sizeDelta = new Vector2(1020f, 368f);
            cardsRowRt.anchoredPosition = new Vector2(0f, -32f);

            UnityEngine.UI.HorizontalLayoutGroup rowLayout = cardsRowRt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            rowLayout.spacing = 30f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            for (int i = 0; i < MaxRewardCards; i++)
            {
                int capturedIndex = i;
                RewardCardSlot slot = new RewardCardSlot();

                UnityEngine.UI.Image slotBg = UguiThemeFactory.CreatePanel(
                    $"RewardCard_{i}",
                    cardsRowRt,
                    UguiThemeFactory.PanelBgSoft,
                    true);
                RectTransform slotRt = slotBg.rectTransform;
                slotRt.sizeDelta = new Vector2(320f, 360f);

                slot.Root = slotBg.gameObject;
                slot.BorderOutline = UguiThemeFactory.CreateOutlineFrame("Outline", slotRt, UguiThemeFactory.AccentGold);

                slot.BadgeBg = UguiThemeFactory.CreatePanel("TypeBadge", slotRt, UguiThemeFactory.AccentGold, true);
                slot.BadgeBg.raycastTarget = false;
                RectTransform badgeRt = slot.BadgeBg.rectTransform;
                badgeRt.anchorMin = new Vector2(0.5f, 1f);
                badgeRt.anchorMax = new Vector2(0.5f, 1f);
                badgeRt.pivot = new Vector2(0.5f, 1f);
                badgeRt.sizeDelta = new Vector2(240f, 30f);
                badgeRt.anchoredPosition = new Vector2(0f, -20f);

                slot.BadgeText = UguiThemeFactory.CreateText(
                    "BadgeLabel",
                    badgeRt,
                    string.Empty,
                    13,
                    new Color(0.05f, 0.07f, 0.11f, 1f),
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                StretchFull(slot.BadgeText.rectTransform, 6f, 2f);

                slot.TitleText = UguiThemeFactory.CreateText(
                    "CardTitle",
                    slotRt,
                    string.Empty,
                    18,
                    UguiThemeFactory.TextPrimary,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                slot.TitleText.resizeTextForBestFit = true;
                slot.TitleText.resizeTextMinSize = 12;
                slot.TitleText.resizeTextMaxSize = 18;
                slot.TitleText.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform titleRt = slot.TitleText.rectTransform;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.offsetMin = new Vector2(18f, -124f);
                titleRt.offsetMax = new Vector2(-18f, -64f);

                slot.DescText = UguiThemeFactory.CreateText(
                    "CardDesc",
                    slotRt,
                    string.Empty,
                    14,
                    UguiThemeFactory.TextMuted,
                    TextAnchor.UpperCenter,
                    FontStyle.Normal);
                slot.DescText.resizeTextForBestFit = true;
                slot.DescText.resizeTextMinSize = 11;
                slot.DescText.resizeTextMaxSize = 14;
                slot.DescText.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform descRt = slot.DescText.rectTransform;
                descRt.anchorMin = new Vector2(0f, 0f);
                descRt.anchorMax = new Vector2(1f, 1f);
                descRt.offsetMin = new Vector2(20f, 84f);
                descRt.offsetMax = new Vector2(-20f, -134f);

                slot.SelectButton = UguiThemeFactory.CreateButton(
                    "SelectRewardButton",
                    slotRt,
                    "CHỌN NÂNG CẤP",
                    BtnGreenColor,
                    15);
                UguiThemeFactory.CreateOutlineFrame("Outline", slot.SelectButton.transform, UguiThemeFactory.HpGreen);
                RectTransform btnRt = slot.SelectButton.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0f, 0f);
                btnRt.anchorMax = new Vector2(1f, 0f);
                btnRt.pivot = new Vector2(0.5f, 0f);
                btnRt.offsetMin = new Vector2(22f, 20f);
                btnRt.offsetMax = new Vector2(-22f, 68f);
                slot.SelectButton.onClick.AddListener(() => OnClickSelectReward(capturedIndex));

                _rewardCards[i] = slot;
            }

            // Fallback container when PendingRewards is empty
            RectTransform emptyRt = UguiThemeFactory.CreateRect("RewardEmptyFallback", cardRt);
            _rewardEmptyFallback = emptyRt.gameObject;
            emptyRt.anchorMin = new Vector2(0.5f, 0.5f);
            emptyRt.anchorMax = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta = new Vector2(420f, 160f);
            emptyRt.anchoredPosition = new Vector2(0f, -20f);

            UnityEngine.UI.Text emptyMsg = UguiThemeFactory.CreateText(
                "EmptyMsg",
                emptyRt,
                "Không có phần thưởng chờ lựa chọn.",
                18,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            SetTopAnchored(emptyMsg.rectTransform, 10f, 38f, 10f);

            _rewardEmptyReturnButton = UguiThemeFactory.CreateButton(
                "ReturnToMapButton",
                emptyRt,
                "Quay về Bản đồ",
                BtnActiveColor,
                16);
            UguiThemeFactory.CreateOutlineFrame("Outline", _rewardEmptyReturnButton.transform, UguiThemeFactory.AllyCyan);
            RectTransform retRt = _rewardEmptyReturnButton.GetComponent<RectTransform>();
            retRt.anchorMin = new Vector2(0.5f, 0f);
            retRt.anchorMax = new Vector2(0.5f, 0f);
            retRt.pivot = new Vector2(0.5f, 0f);
            retRt.sizeDelta = new Vector2(240f, 52f);
            retRt.anchoredPosition = new Vector2(0f, 20f);
            _rewardEmptyReturnButton.onClick.AddListener(OnClickRewardEmptyReturn);
            _rewardEmptyFallback.SetActive(false);
        }

        private void RefreshRewardIfDirty()
        {
            RunState run = sandboxController.CurrentRun;
            int pendingCount = (run != null && run.PendingRewards != null) ? run.PendingRewards.Count : 0;

            if (!_rewardDirty && _lastRewardRun == run && _lastRewardCount == pendingCount)
            {
                return;
            }

            _rewardDirty = false;
            _lastRewardRun = run;
            _lastRewardCount = pendingCount;

            if (_rewardRelicsSubtitle != null)
            {
                _rewardRelicsSubtitle.text = BuildRelicsSummary(run);
            }

            bool hasRewards = pendingCount > 0;
            _rewardCardsContainer.SetActive(hasRewards);
            _rewardEmptyFallback.SetActive(!hasRewards);

            if (!hasRewards) return;

            for (int i = 0; i < MaxRewardCards; i++)
            {
                RewardCardSlot slot = _rewardCards[i];
                bool active = i < pendingCount;
                slot.Root.SetActive(active);
                if (!active) continue;

                RewardOption opt = run.PendingRewards[i];
                Color accent = GetRewardAccentColor(opt.RewardType);
                slot.BorderOutline.color = accent;
                slot.BadgeBg.color = accent;
                slot.BadgeText.text = GetRewardBadgeLabel(opt.RewardType);
                slot.TitleText.text = opt.Title ?? string.Empty;
                slot.DescText.text = opt.Description ?? string.Empty;
            }
        }

        private void OnClickSelectReward(int index)
        {
            PlayClickSfx();
            if (sandboxController == null) return;
            sandboxController.SelectRewardOption(index);
            _rewardDirty = true;
        }

        private void OnClickRewardEmptyReturn()
        {
            PlayClickSfx();
            if (sandboxController == null) return;
            sandboxController.ScreenMode = SandboxScreenMode.CampaignMap;
        }

        // =========================================================================
        // 2. AI POLICY MODAL CANVAS (sortingOrder = 40)
        // =========================================================================

        private void BuildPolicyModalCanvas()
        {
            _policyCanvas = UguiThemeFactory.CreateOverlayCanvas("AiPolicyModalCanvas", 40);
            RectTransform root = _policyCanvas.GetComponent<RectTransform>();

            UnityEngine.UI.Image dimmer = UguiThemeFactory.CreatePanel(
                "DimmerBackdrop",
                root,
                new Color(0f, 0f, 0f, 0.72f),
                false);
            dimmer.raycastTarget = true;
            StretchFull(dimmer.rectTransform, 0f, 0f);

            UnityEngine.UI.Image modal = UguiThemeFactory.CreatePanel(
                "AiPolicyModal",
                dimmer.rectTransform,
                UguiThemeFactory.PanelBg,
                true);
            RectTransform modalRt = modal.rectTransform;
            modalRt.anchorMin = new Vector2(0.5f, 0.5f);
            modalRt.anchorMax = new Vector2(0.5f, 0.5f);
            modalRt.pivot = new Vector2(0.5f, 0.5f);
            modalRt.sizeDelta = new Vector2(740f, 560f);
            modalRt.anchoredPosition = Vector2.zero;
            UguiThemeFactory.CreateOutlineFrame("Outline", modalRt, UguiThemeFactory.AllyCyan);

            UnityEngine.UI.Text title = UguiThemeFactory.CreateText(
                "PolicyTitle",
                modalRt,
                "CẤU HÌNH CHIẾN THUẬT AI (UNIT AI POLICY)",
                22,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetTopAnchored(title.rectTransform, 18f, 32f, 24f);

            _policyScopeText = UguiThemeFactory.CreateText(
                "PolicyScopeNote",
                modalRt,
                string.Empty,
                13,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Italic);
            SetTopAnchored(_policyScopeText.rectTransform, 50f, 22f, 24f);

            // Top row: 5 Unit Tabs
            RectTransform tabsRowRt = UguiThemeFactory.CreateRect("UnitTabsRow", modalRt);
            SetTopAnchored(tabsRowRt, 82f, 40f, 24f);
            UnityEngine.UI.HorizontalLayoutGroup tabsLayout = tabsRowRt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            tabsLayout.spacing = 8f;
            tabsLayout.childAlignment = TextAnchor.MiddleCenter;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            for (int i = 0; i < MaxPolicyTabs; i++)
            {
                int capturedIndex = i;
                PolicyTabSlot tab = new PolicyTabSlot();
                UnityEngine.UI.Button tabBtn = UguiThemeFactory.CreateButton(
                    $"PolicyTab_{i}",
                    tabsRowRt,
                    string.Empty,
                    UguiThemeFactory.PanelBgSoft,
                    13);
                tab.Root = tabBtn.gameObject;
                tab.Button = tabBtn;
                tab.Outline = UguiThemeFactory.CreateOutlineFrame("Outline", tabBtn.transform, new Color(0.4f, 0.48f, 0.60f, 0.6f));
                tab.Label = tabBtn.GetComponentInChildren<UnityEngine.UI.Text>();
                tabBtn.onClick.AddListener(() => OnClickSelectPolicyTab(capturedIndex));
                tab.Root.SetActive(false);
                _policyTabs[i] = tab;
            }

            _policyUnitSummaryText = UguiThemeFactory.CreateText(
                "UnitSummary",
                modalRt,
                string.Empty,
                14,
                UguiThemeFactory.AllyCyan,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetTopAnchored(_policyUnitSummaryText.rectTransform, 130f, 26f, 24f);

            // 3 Policy Setting Groups
            BuildPolicyRow(
                modalRt,
                166f,
                "1. BEHAVIOR PRESET (Phong cách hành vi)",
                out _behaviorValueText,
                out _behaviorDescText,
                out _behaviorNextButton);
            _behaviorNextButton.onClick.AddListener(OnClickCycleBehaviorPreset);

            BuildPolicyRow(
                modalRt,
                268f,
                "2. TARGET PRIORITY (Ưu tiên mục tiêu)",
                out _targetValueText,
                out _targetDescText,
                out _targetNextButton);
            _targetNextButton.onClick.AddListener(OnClickCycleTargetPriority);

            BuildPolicyRow(
                modalRt,
                370f,
                "3. SKILL CONDITION (Điều kiện dùng kỹ năng)",
                out _conditionValueText,
                out _conditionDescText,
                out _conditionNextButton);
            _conditionNextButton.onClick.AddListener(OnClickCycleSkillCondition);

            _policySaveCloseButton = UguiThemeFactory.CreateButton(
                "SaveClosePolicyButton",
                modalRt,
                "Lưu & Đóng",
                BtnActiveColor,
                16);
            UguiThemeFactory.CreateOutlineFrame("Outline", _policySaveCloseButton.transform, UguiThemeFactory.AllyCyan);
            RectTransform closeRt = _policySaveCloseButton.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.sizeDelta = new Vector2(220f, 44f);
            closeRt.anchoredPosition = new Vector2(0f, 20f);
            _policySaveCloseButton.onClick.AddListener(OnClickClosePolicyModal);
        }

        private static void BuildPolicyRow(
            RectTransform parent,
            float topOffset,
            string sectionTitle,
            out UnityEngine.UI.Text valueText,
            out UnityEngine.UI.Text descText,
            out UnityEngine.UI.Button nextButton)
        {
            UnityEngine.UI.Image rowBg = UguiThemeFactory.CreatePanel("PolicyRow", parent, UguiThemeFactory.PanelBgSoft, true);
            RectTransform rowRt = rowBg.rectTransform;
            SetTopAnchored(rowRt, topOffset, 90f, 24f);
            UguiThemeFactory.CreateOutlineFrame("Outline", rowRt, new Color(0.32f, 0.40f, 0.54f, 0.5f));

            UnityEngine.UI.Text label = UguiThemeFactory.CreateText(
                "GroupTitle",
                rowRt,
                sectionTitle,
                13,
                UguiThemeFactory.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            RectTransform lblRt = label.rectTransform;
            lblRt.anchorMin = new Vector2(0f, 1f);
            lblRt.anchorMax = new Vector2(0.7f, 1f);
            lblRt.pivot = new Vector2(0f, 1f);
            lblRt.offsetMin = new Vector2(14f, -26f);
            lblRt.offsetMax = new Vector2(0f, -6f);

            valueText = UguiThemeFactory.CreateText(
                "CurrentValue",
                rowRt,
                string.Empty,
                16,
                UguiThemeFactory.AccentGold,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 12;
            valueText.resizeTextMaxSize = 16;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform valRt = valueText.rectTransform;
            valRt.anchorMin = new Vector2(0f, 0.44f);
            valRt.anchorMax = new Vector2(0.70f, 0.72f);
            valRt.offsetMin = new Vector2(14f, 0f);
            valRt.offsetMax = Vector2.zero;

            descText = UguiThemeFactory.CreateText(
                "PresetDesc",
                rowRt,
                string.Empty,
                12,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            descText.resizeTextForBestFit = true;
            descText.resizeTextMinSize = 10;
            descText.resizeTextMaxSize = 12;
            descText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform descRt = descText.rectTransform;
            descRt.anchorMin = new Vector2(0f, 0.04f);
            descRt.anchorMax = new Vector2(0.70f, 0.43f);
            descRt.offsetMin = new Vector2(14f, 4f);
            descRt.offsetMax = Vector2.zero;

            nextButton = UguiThemeFactory.CreateButton(
                "NextOptionButton",
                rowRt,
                "Đổi / Tiếp theo ->",
                new Color(0.16f, 0.24f, 0.36f, 1f),
                14);
            UguiThemeFactory.CreateOutlineFrame("Outline", nextButton.transform, UguiThemeFactory.AllyCyan);
            RectTransform btnRt = nextButton.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0.5f);
            btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.sizeDelta = new Vector2(180f, 44f);
            btnRt.anchoredPosition = new Vector2(-14f, 0f);
        }

        private void RefreshPolicyModalContents()
        {
            _policyUnitsBuffer.Clear();
            bool inActiveBattle = sandboxController != null &&
                                  sandboxController.ScreenMode == SandboxScreenMode.ActiveBattle &&
                                  sandboxController.CurrentState != null;

            if (inActiveBattle)
            {
                List<UnitState> battleUnits = sandboxController.CurrentState.Units;
                for (int i = 0; i < battleUnits.Count; i++)
                {
                    UnitState u = battleUnits[i];
                    if (u != null && u.Team == TeamSide.Player)
                    {
                        _policyUnitsBuffer.Add(u);
                    }
                }
                _policyScopeText.text = "Chỉnh trực tiếp AI Policy cho trận đấu hiện tại (lượt AI kế tiếp áp dụng ngay).";
            }
            else if (sandboxController != null &&
                     sandboxController.CurrentRun != null &&
                     sandboxController.CurrentRun.PlayerRoster != null)
            {
                List<UnitState> roster = sandboxController.CurrentRun.PlayerRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    if (roster[i] != null)
                    {
                        _policyUnitsBuffer.Add(roster[i]);
                    }
                }
                _policyScopeText.text = "Chỉnh AI Policy mặc định cho đội hình Campaign trước khi bước vào trận mới.";
            }
            else
            {
                _policyScopeText.text = "Chưa có dữ liệu đội hình.";
            }

            UnitState selectedUnit = ResolveSelectedPolicyUnit();
            int tabCount = Mathf.Min(_policyUnitsBuffer.Count, MaxPolicyTabs);
            for (int i = 0; i < MaxPolicyTabs; i++)
            {
                PolicyTabSlot tab = _policyTabs[i];
                bool active = i < tabCount;
                tab.Root.SetActive(active);
                if (!active)
                {
                    tab.BoundUnitId = null;
                    continue;
                }

                UnitState u = _policyUnitsBuffer[i];
                tab.BoundUnitId = u.InstanceId;
                string unitName = u.Name ?? u.Role.ToString();
                tab.Label.text = u.IsAlive ? unitName : $"{unitName} (KO)";
                tab.Label.color = u.IsAlive ? UguiThemeFactory.TextPrimary : UguiThemeFactory.TextMuted;
                bool isSelected = selectedUnit != null && u.InstanceId == selectedUnit.InstanceId;
                tab.Outline.color = isSelected ? UguiThemeFactory.AllyCyan : new Color(0.4f, 0.48f, 0.60f, 0.55f);
                Color tabBg = isSelected ? BtnActiveColor : (u.IsAlive ? UguiThemeFactory.PanelBgSoft : new Color(0.08f, 0.10f, 0.14f, 0.72f));
                SetButtonColor(tab.Button, tabBg);
            }

            if (selectedUnit == null)
            {
                _policyUnitSummaryText.text = "Không có tướng khả dụng";
                _behaviorValueText.text = "-";
                _behaviorDescText.text = string.Empty;
                _targetValueText.text = "-";
                _targetDescText.text = string.Empty;
                _conditionValueText.text = "-";
                _conditionDescText.text = string.Empty;
                return;
            }

            if (selectedUnit.AIPolicy == null)
            {
                selectedUnit.AIPolicy = new UnitAIPolicy();
            }

            UnitAIPolicy policy = selectedUnit.AIPolicy;
            _policyUnitSummaryText.text =
                $"{selectedUnit.Name} [{selectedUnit.Role}]  •  HP: {selectedUnit.CurrentHP}/{selectedUnit.MaxHP}  •  ATK {selectedUnit.Attack} / DEF {selectedUnit.Defense} / SPD {selectedUnit.Speed}";

            _behaviorValueText.text = policy.BehaviorPreset.ToString();
            _behaviorDescText.text = GetBehaviorPresetDescription(policy.BehaviorPreset);

            _targetValueText.text = policy.PrimaryTargetRule.ToString();
            _targetDescText.text = GetTargetRuleDescription(policy.PrimaryTargetRule);

            _conditionValueText.text = policy.SpecialSkillCondition.ToString();
            _conditionDescText.text = GetSkillConditionDescription(policy.SpecialSkillCondition);
        }

        private UnitState ResolveSelectedPolicyUnit()
        {
            if (_policyUnitsBuffer.Count == 0) return null;

            if (!string.IsNullOrEmpty(_selectedPolicyUnitId))
            {
                for (int i = 0; i < _policyUnitsBuffer.Count; i++)
                {
                    if (_policyUnitsBuffer[i].InstanceId == _selectedPolicyUnitId)
                    {
                        return _policyUnitsBuffer[i];
                    }
                }
            }

            _selectedPolicyUnitId = _policyUnitsBuffer[0].InstanceId;
            return _policyUnitsBuffer[0];
        }

        private void OnClickSelectPolicyTab(int index)
        {
            if (index < 0 || index >= MaxPolicyTabs) return;
            string unitId = _policyTabs[index].BoundUnitId;
            if (string.IsNullOrEmpty(unitId)) return;

            PlayClickSfx();
            _selectedPolicyUnitId = unitId;
            RefreshPolicyModalContents();
        }

        private void OnClickCycleBehaviorPreset()
        {
            PlayClickSfx();
            UnitState unit = ResolveSelectedPolicyUnit();
            if (unit == null) return;
            if (unit.AIPolicy == null) unit.AIPolicy = new UnitAIPolicy();

            int next = ((int)unit.AIPolicy.BehaviorPreset + 1) % 5;
            unit.AIPolicy.BehaviorPreset = (UnitBehaviorPreset)next;
            RefreshPolicyModalContents();
        }

        private void OnClickCycleTargetPriority()
        {
            PlayClickSfx();
            UnitState unit = ResolveSelectedPolicyUnit();
            if (unit == null) return;
            if (unit.AIPolicy == null) unit.AIPolicy = new UnitAIPolicy();

            int next = ((int)unit.AIPolicy.PrimaryTargetRule + 1) % 6;
            unit.AIPolicy.PrimaryTargetRule = (TargetPriorityRule)next;
            RefreshPolicyModalContents();
        }

        private void OnClickCycleSkillCondition()
        {
            PlayClickSfx();
            UnitState unit = ResolveSelectedPolicyUnit();
            if (unit == null) return;
            if (unit.AIPolicy == null) unit.AIPolicy = new UnitAIPolicy();

            int next = ((int)unit.AIPolicy.SpecialSkillCondition + 1) % 6;
            unit.AIPolicy.SpecialSkillCondition = (SkillConditionType)next;
            RefreshPolicyModalContents();
        }

        private void OnClickClosePolicyModal()
        {
            PlayClickSfx();
            ClosePolicyModal();
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private string BuildRelicsSummary(RunState run)
        {
            if (run == null || run.CollectedRelics == null || run.CollectedRelics.Count == 0)
            {
                return "Di vật hiện có: Chưa thu thập";
            }

            string text = "Di vật hiện có: ";
            for (int i = 0; i < run.CollectedRelics.Count; i++)
            {
                if (i > 0) text += ", ";
                text += run.CollectedRelics[i].ToString();
            }
            return text;
        }

        private static string GetRewardBadgeLabel(UpgradeRewardType rewardType)
        {
            switch (rewardType)
            {
                case UpgradeRewardType.GrantRelic: return "DI VẬT CHIẾN THUẬT";
                case UpgradeRewardType.TeamHeal30Percent: return "HỒI PHỤC ĐỘI HÌNH";
                case UpgradeRewardType.BoostGuardianDefense: return "PHÒNG THỦ GUARDIAN";
                case UpgradeRewardType.BoostRangerCritMark: return "SÁT THƯƠNG RANGER";
                case UpgradeRewardType.BoostMageAoE: return "CƯỜNG HÓA MAGE";
                default: return rewardType.ToString().ToUpperInvariant();
            }
        }

        private static Color GetRewardAccentColor(UpgradeRewardType rewardType)
        {
            switch (rewardType)
            {
                case UpgradeRewardType.GrantRelic: return UguiThemeFactory.AccentGold;
                case UpgradeRewardType.TeamHeal30Percent: return UguiThemeFactory.HpGreen;
                case UpgradeRewardType.BoostGuardianDefense: return UguiThemeFactory.ShieldBlue;
                case UpgradeRewardType.BoostRangerCritMark: return OrbDamageColor;
                case UpgradeRewardType.BoostMageAoE: return OrbStunColor;
                default: return UguiThemeFactory.AllyCyan;
            }
        }

        private static string GetBehaviorPresetDescription(UnitBehaviorPreset preset)
        {
            switch (preset)
            {
                case UnitBehaviorPreset.Aggressive: return "Ưu tiên tối đa kỹ năng gây sát thương và kết liễu kẻ địch.";
                case UnitBehaviorPreset.Defensive: return "Ưu tiên tạo khiên, bảo vệ đồng minh và giảm rủi ro phản công.";
                case UnitBehaviorPreset.Support: return "Ưu tiên hồi máu, buff lá chắn và giữ nhịp sống sót cho đồng đội.";
                case UnitBehaviorPreset.Tactical: return "Ưu tiên khống chế (Stun/Mark) và phối hợp hiệu ứng chiến thuật.";
                default: return "Cân bằng giữa tấn công, phòng ngự và hỗ trợ theo Utility AI.";
            }
        }

        private static string GetTargetRuleDescription(TargetPriorityRule rule)
        {
            switch (rule)
            {
                case TargetPriorityRule.MarkedFirst: return "Cộng điểm ưu tiên cao cho mục tiêu đang bị gắn dấu Marked.";
                case TargetPriorityRule.LowestHP: return "Tập trung dồn sát thương vào kẻ địch có phần trăm HP thấp nhất.";
                case TargetPriorityRule.HighestThreat: return "Nhắm vào đơn vị có chỉ số đe dọa (BaseThreat) lớn nhất.";
                case TargetPriorityRule.SupportFirst: return "Ưu tiên truy sát Medic và Mage hỗ trợ của đối phương.";
                case TargetPriorityRule.FrontlineFirst: return "Ưu tiên đánh phá hàng Frontline trước để mở đường.";
                default: return "Sử dụng công thức Utility chuẩn của AI để chấm điểm mục tiêu.";
            }
        }

        private static string GetSkillConditionDescription(SkillConditionType condition)
        {
            switch (condition)
            {
                case SkillConditionType.TargetHpBelow40: return "Chỉ ưu tiên tung kỹ năng đặc biệt khi mục tiêu dưới 40% HP.";
                case SkillConditionType.AllyHpBelow50: return "Kích hoạt kỹ năng khi có đồng minh xuống dưới 50% HP.";
                case SkillConditionType.EnemiesAliveAtLeast3: return "Ưu tiên tung kỹ năng khi phe địch còn ít nhất 3 tướng sống.";
                case SkillConditionType.TargetNotStunned: return "Chỉ dùng kỹ năng khi mục tiêu chưa bị Stun để tránh phí khống chế.";
                case SkillConditionType.TargetNotMarked: return "Ưu tiên dùng kỹ năng lên mục tiêu chưa mang trạng thái Marked.";
                default: return "Luôn cho phép sử dụng kỹ năng đặc biệt ngay khi đủ Energy & Hồi chiêu.";
            }
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

        private static void StretchFull(RectTransform rt, float horizontalPadding, float verticalPadding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rt.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }

        private static void SetTopAnchored(RectTransform rt, float topOffset, float height, float horizontalPadding)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(horizontalPadding, -(topOffset + height));
            rt.offsetMax = new Vector2(-horizontalPadding, -topOffset);
        }
    }
}
