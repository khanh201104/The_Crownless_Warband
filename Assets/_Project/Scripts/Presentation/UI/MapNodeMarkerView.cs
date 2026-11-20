using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Roguelite;
using TMPro;
using UnityEngine;

namespace CrownlessWarband.Presentation.UI
{
    public class MapNodeMarkerView : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private static readonly Dictionary<CampaignNodeType, Sprite> _iconSpriteCache = new Dictionary<CampaignNodeType, Sprite>(8);

        public event Action<CampaignNode> OnNodeClicked;
        public event Action<CampaignNode, RectTransform, bool> OnNodeHoverChanged;

        private RectTransform _rootRect;
        private CanvasGroup _canvasGroup;
        private UnityEngine.UI.Image _haloRing;
        private UnityEngine.UI.Image _borderCircle;
        private UnityEngine.UI.Button _iconButton;
        private UnityEngine.UI.Image _iconArt;
        private GameObject _doneBadgeRoot;
        private UnityEngine.UI.Image _plateOutline;
        private TextMeshProUGUI _plateText;

        private CampaignNode _boundNode;
        private bool _isBuilt;
        private bool _canSelect;
        private bool _isTravelingLocked;
        private bool _isHovered;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            if (_haloRing != null)
            {
                bool showHalo = _canSelect && !_isTravelingLocked;
                if (_haloRing.gameObject.activeSelf != showHalo)
                {
                    _haloRing.gameObject.SetActive(showHalo);
                }

                if (showHalo)
                {
                    float pulse = 1.0f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
                    _haloRing.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                }
            }

            if (_iconButton != null)
            {
                float targetScale = (_isHovered && _canSelect && !_isTravelingLocked) ? 1.08f : 1.0f;
                float currentScale = _iconButton.transform.localScale.x;
                float nextScale = Mathf.Lerp(currentScale, targetScale, Time.unscaledDeltaTime * 14f);
                _iconButton.transform.localScale = new Vector3(nextScale, nextScale, 1f);
            }
        }

        private void OnDestroy()
        {
            if (_iconButton != null)
            {
                _iconButton.onClick.RemoveAllListeners();
            }
            OnNodeClicked = null;
            OnNodeHoverChanged = null;
        }

        public void Bind(CampaignNode node, bool isCurrent, bool isAvailable, bool isInteractable)
        {
            EnsureBuilt();
            _boundNode = node;
            if (node == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            bool isBoss = node.NodeType == CampaignNodeType.Boss;
            bool isElite = node.NodeType == CampaignNodeType.EliteCombat;
            float btnSize = isBoss ? 116f : (isElite ? 96f : 88f);
            _iconButton.GetComponent<RectTransform>().sizeDelta = new Vector2(btnSize, btnSize);
            _borderCircle.rectTransform.sizeDelta = new Vector2(btnSize + 12f, btnSize + 12f);
            _haloRing.rectTransform.sizeDelta = new Vector2(btnSize + 28f, btnSize + 28f);
            _iconArt.rectTransform.sizeDelta = new Vector2(btnSize * 0.76f, btnSize * 0.76f);
            _iconArt.sprite = GetOrCreateNodeIconSprite(node.NodeType);

            _canSelect = isAvailable && isInteractable;
            _iconButton.interactable = isInteractable && !_isTravelingLocked;

            Color bgColor;
            Color borderColor;
            Color haloColor;
            float groupAlpha;

            if (isCurrent)
            {
                bgColor = new Color(0.28f, 0.20f, 0.07f, 0.98f);
                borderColor = UguiThemeFactory.AccentGold;
                haloColor = new Color(0.98f, 0.72f, 0.12f, 0.46f);
                groupAlpha = 1f;
            }
            else if (isAvailable)
            {
                bgColor = new Color(0.10f, 0.28f, 0.36f, 0.98f);
                borderColor = UguiThemeFactory.AllyCyan;
                haloColor = new Color(0.13f, 0.83f, 0.93f, 0.45f);
                groupAlpha = 1f;
            }
            else if (node.IsCompleted)
            {
                bgColor = new Color(0.11f, 0.22f, 0.17f, 0.95f);
                borderColor = new Color(0.28f, 0.78f, 0.48f, 0.88f);
                haloColor = Color.clear;
                groupAlpha = 0.95f;
            }
            else
            {
                bgColor = new Color(0.11f, 0.14f, 0.20f, 0.92f);
                borderColor = new Color(0.45f, 0.52f, 0.64f, 0.75f);
                haloColor = Color.clear;
                groupAlpha = 0.78f;
            }

            SetCircleButtonColor(_iconButton, bgColor);
            _borderCircle.color = borderColor;
            _haloRing.color = haloColor;
            _canvasGroup.alpha = groupAlpha;

            if (_plateOutline != null)
            {
                _plateOutline.color = isCurrent ? UguiThemeFactory.AccentGold : (isAvailable ? UguiThemeFactory.AllyCyan : borderColor);
            }

            _doneBadgeRoot.SetActive(node.IsCompleted);

            string shortTitle = string.IsNullOrEmpty(node.Title) ? node.NodeType.ToString() : node.Title;
            if (shortTitle.Length > 26)
            {
                shortTitle = shortTitle.Substring(0, 24) + "..";
            }
            _plateText.text = isBoss ? $"[TRÙM] {shortTitle}" : shortTitle;
            _plateText.color = isBoss ? UguiThemeFactory.AccentGold : (isAvailable || isCurrent ? Color.white : new Color(0.86f, 0.90f, 0.96f, 1f));
        }

        public void SetTravelingLock(bool locked)
        {
            _isTravelingLocked = locked;
            if (_iconButton != null)
            {
                _iconButton.interactable = _canSelect && !locked;
            }
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isHovered = true;
            if (_boundNode != null)
            {
                OnNodeHoverChanged?.Invoke(_boundNode, _rootRect, true);
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isHovered = false;
            if (_boundNode != null)
            {
                OnNodeHoverChanged?.Invoke(_boundNode, _rootRect, false);
            }
        }

        private void EnsureBuilt()
        {
            if (_isBuilt) return;
            _isBuilt = true;

            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null)
            {
                _rootRect = gameObject.AddComponent<RectTransform>();
            }
            _rootRect.sizeDelta = new Vector2(204f, 156f);

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Halo pulse ring behind button
            RectTransform haloRt = UguiThemeFactory.CreateRect("HaloRing", _rootRect);
            haloRt.anchorMin = new Vector2(0.5f, 0.5f);
            haloRt.anchorMax = new Vector2(0.5f, 0.5f);
            haloRt.anchoredPosition = new Vector2(0f, 16f);
            haloRt.sizeDelta = new Vector2(116f, 116f);
            _haloRing = haloRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            _haloRing.sprite = UguiThemeFactory.GetCircleSprite();
            _haloRing.raycastTarget = false;
            _haloRing.gameObject.SetActive(false);

            // Crisp circular outline ring
            RectTransform borderRt = UguiThemeFactory.CreateRect("BorderCircle", _rootRect);
            borderRt.anchorMin = new Vector2(0.5f, 0.5f);
            borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.anchoredPosition = new Vector2(0f, 16f);
            borderRt.sizeDelta = new Vector2(100f, 100f);
            _borderCircle = borderRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            _borderCircle.sprite = UguiThemeFactory.GetCircleSprite();
            _borderCircle.raycastTarget = false;

            // Circular clickable button
            _iconButton = UguiThemeFactory.CreateTmpButton("NodeIconButton", _rootRect, string.Empty, UguiThemeFactory.PanelBg, 12f, true);
            RectTransform btnRt = _iconButton.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0f, 16f);
            btnRt.sizeDelta = new Vector2(88f, 88f);

            TextMeshProUGUI defaultBtnTxt = _iconButton.GetComponentInChildren<TextMeshProUGUI>();
            if (defaultBtnTxt != null) defaultBtnTxt.gameObject.SetActive(false);

            _iconButton.onClick.AddListener(HandleClicked);

            // Node Icon Sprite centered inside button
            RectTransform artRt = UguiThemeFactory.CreateRect("IconArt", btnRt);
            artRt.anchorMin = new Vector2(0.5f, 0.5f);
            artRt.anchorMax = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(67f, 67f);
            _iconArt = artRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            _iconArt.preserveAspect = true;
            _iconArt.raycastTarget = false;

            // Done Badge (top-right XONG pill)
            UnityEngine.UI.Image doneBg = UguiThemeFactory.CreatePanel(
                "DoneBadge",
                _rootRect,
                new Color(0.11f, 0.54f, 0.31f, 1f),
                true);
            doneBg.raycastTarget = false;
            RectTransform doneRt = doneBg.rectTransform;
            doneRt.anchorMin = new Vector2(0.5f, 0.5f);
            doneRt.anchorMax = new Vector2(0.5f, 0.5f);
            doneRt.anchoredPosition = new Vector2(38f, 50f);
            doneRt.sizeDelta = new Vector2(44f, 22f);
            _doneBadgeRoot = doneBg.gameObject;

            TextMeshProUGUI doneText = UguiThemeFactory.CreateTmpText(
                "OkText",
                doneRt,
                "XONG",
                11f,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            doneText.rectTransform.anchorMin = Vector2.zero;
            doneText.rectTransform.anchorMax = Vector2.one;
            doneText.rectTransform.offsetMin = Vector2.zero;
            doneText.rectTransform.offsetMax = Vector2.zero;
            _doneBadgeRoot.SetActive(false);

            // Nameplate beneath marker (198 x 34)
            UnityEngine.UI.Image plateBg = UguiThemeFactory.CreatePanel(
                "CompactPlate",
                _rootRect,
                new Color(0.06f, 0.09f, 0.14f, 0.96f),
                true);
            plateBg.raycastTarget = false;
            RectTransform plateRt = plateBg.rectTransform;
            plateRt.anchorMin = new Vector2(0.5f, 0f);
            plateRt.anchorMax = new Vector2(0.5f, 0f);
            plateRt.pivot = new Vector2(0.5f, 0f);
            plateRt.sizeDelta = new Vector2(198f, 34f);
            plateRt.anchoredPosition = new Vector2(0f, -6f);
            _plateOutline = UguiThemeFactory.CreateOutlineFrame("Outline", plateRt, new Color(0.38f, 0.48f, 0.62f, 0.85f));

            _plateText = UguiThemeFactory.CreateTmpText(
                "PlateText",
                plateRt,
                string.Empty,
                14f,
                UguiThemeFactory.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _plateText.enableAutoSizing = true;
            _plateText.fontSizeMin = 11f;
            _plateText.fontSizeMax = 15f;
            _plateText.overflowMode = TextOverflowModes.Truncate;
            _plateText.rectTransform.anchorMin = Vector2.zero;
            _plateText.rectTransform.anchorMax = Vector2.one;
            _plateText.rectTransform.offsetMin = new Vector2(8f, 2f);
            _plateText.rectTransform.offsetMax = new Vector2(-8f, -2f);
        }

        private void HandleClicked()
        {
            if (_boundNode != null && _canSelect && !_isTravelingLocked)
            {
                OnNodeClicked?.Invoke(_boundNode);
            }
        }

        private static void SetCircleButtonColor(UnityEngine.UI.Button button, Color normalColor)
        {
            if (button == null) return;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.20f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = normalColor;
            button.colors = colors;
        }

        private static Sprite GetOrCreateNodeIconSprite(CampaignNodeType nodeType)
        {
            if (_iconSpriteCache.TryGetValue(nodeType, out Sprite cached) && cached != null)
            {
                return cached;
            }

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = $"MapNodeIcon_{nodeType}";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float py = ((y + 0.5f) / size) - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float px = ((x + 0.5f) / size) - 0.5f;
                    pixels[y * size + x] = SampleNodeIconPixel(nodeType, px, py);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 128f);
            sprite.name = $"NodeIconSprite_{nodeType}";
            _iconSpriteCache[nodeType] = sprite;
            return sprite;
        }

        private static Color SampleNodeIconPixel(CampaignNodeType nodeType, float px, float py)
        {
            // Medieval medallion base with bronze/gold rim
            float dist = Mathf.Sqrt(px * px + py * py);
            float baseDist = dist - 0.44f;
            float baseAlpha = Mathf.Clamp01((0.01f - baseDist) / 0.02f);
            if (baseAlpha <= 0f) return Color.clear;

            Color centerBg = new Color(0.10f, 0.14f, 0.20f, 0.95f);
            Color rimBronze = new Color(0.72f, 0.56f, 0.28f, 0.95f);
            float rimMask = Mathf.Clamp01((dist - 0.375f) / 0.015f);
            Color bg = Color.Lerp(centerBg, rimBronze, rimMask);
            Color fg = Color.clear;

            switch (nodeType)
            {
                case CampaignNodeType.Combat:
                {
                    float blade1 = SdfSegment(px, py, -0.22f, -0.22f, 0.24f, 0.24f) - 0.032f;
                    float blade2 = SdfSegment(px, py, 0.22f, -0.22f, -0.24f, 0.24f) - 0.032f;
                    float guard1 = SdfSegment(px, py, -0.18f, -0.06f, -0.06f, -0.18f) - 0.024f;
                    float guard2 = SdfSegment(px, py, 0.18f, -0.06f, 0.06f, -0.18f) - 0.024f;
                    float hilts = Mathf.Min(guard1, guard2);
                    float blades = Mathf.Min(blade1, blade2);
                    float d = Mathf.Min(blades, hilts);
                    if (d < 0.01f)
                    {
                        float a = Mathf.Clamp01((0.01f - d) / 0.02f);
                        Color col = hilts <= blades ? new Color(0.96f, 0.55f, 0.18f, 1f) : new Color(0.92f, 0.95f, 1.0f, 1f);
                        fg = new Color(col.r, col.g, col.b, a);
                    }
                    break;
                }

                case CampaignNodeType.EliteCombat:
                {
                    float shieldBody = SdfRoundedBox(px, py + 0.03f, 0.16f, 0.18f, 0.06f);
                    float hornL = SdfTriangle(px + 0.15f, py - 0.17f, 0.07f, 0.11f);
                    float hornR = SdfTriangle(px - 0.15f, py - 0.17f, 0.07f, 0.11f);
                    float eyeL = SdfCircle(px + 0.07f, py - 0.01f, 0.035f);
                    float eyeR = SdfCircle(px - 0.07f, py - 0.01f, 0.035f);
                    float d = Mathf.Min(shieldBody, Mathf.Min(hornL, hornR));
                    if (d < 0.01f)
                    {
                        float a = Mathf.Clamp01((0.01f - d) / 0.02f);
                        Color col = (eyeL < 0f || eyeR < 0f)
                            ? new Color(1.0f, 0.85f, 0.25f, 1f)
                            : new Color(0.96f, 0.36f, 0.18f, 1f);
                        fg = new Color(col.r, col.g, col.b, a);
                    }
                    break;
                }

                case CampaignNodeType.Boss:
                {
                    float crownBase = SdfRoundedBox(px, py + 0.11f, 0.25f, 0.075f, 0.02f);
                    float p1 = SdfTriangle(px, py - 0.09f, 0.08f, 0.17f);
                    float p2 = SdfTriangle(px + 0.17f, py - 0.05f, 0.07f, 0.13f);
                    float p3 = SdfTriangle(px - 0.17f, py - 0.05f, 0.07f, 0.13f);
                    float gem = SdfCircle(px, py + 0.11f, 0.055f);
                    float d = Mathf.Min(crownBase, Mathf.Min(p1, Mathf.Min(p2, p3)));
                    if (d < 0.01f)
                    {
                        float a = Mathf.Clamp01((0.01f - d) / 0.02f);
                        Color col = gem < 0f
                            ? new Color(0.94f, 0.27f, 0.27f, 1f)
                            : new Color(0.96f, 0.62f, 0.04f, 1f);
                        fg = new Color(col.r, col.g, col.b, a);
                    }
                    break;
                }

                case CampaignNodeType.Shop:
                {
                    float coinOuter = SdfCircle(px, py, 0.24f);
                    float coinInner = SdfCircle(px, py, 0.17f);
                    float diamond = SdfDiamond(px, py, 0.10f);
                    if (coinOuter < 0.01f)
                    {
                        float a = Mathf.Clamp01((0.01f - coinOuter) / 0.02f);
                        Color col = diamond < 0f
                            ? new Color(1.0f, 0.96f, 0.72f, 1f)
                            : (coinInner < 0f ? new Color(0.85f, 0.56f, 0.08f, 1f) : new Color(0.98f, 0.78f, 0.20f, 1f));
                        fg = new Color(col.r, col.g, col.b, a);
                    }
                    break;
                }

                case CampaignNodeType.Recovery:
                {
                    float crossV = SdfRoundedBox(px, py, 0.065f, 0.22f, 0.02f);
                    float crossH = SdfRoundedBox(px, py, 0.22f, 0.065f, 0.02f);
                    float ring = Mathf.Abs(SdfCircle(px, py, 0.30f)) - 0.025f;
                    float d = Mathf.Min(Mathf.Min(crossV, crossH), ring);
                    if (d < 0.01f)
                    {
                        float a = Mathf.Clamp01((0.01f - d) / 0.02f);
                        Color col = new Color(0.24f, 0.88f, 0.58f, 1f);
                        fg = new Color(col.r, col.g, col.b, a);
                    }
                    break;
                }

                case CampaignNodeType.Event:
                default:
                {
                    float d1 = SdfRoundedBox(px, py, 0.04f, 0.26f, 0.015f);
                    float d2 = SdfRoundedBox(px, py, 0.26f, 0.04f, 0.015f);
                    float core = SdfDiamond(px, py, 0.13f);
                    float d = Mathf.Min(Mathf.Min(d1, d2), core);
                    if (d < 0.01f)
                    {
                        float a = Mathf.Clamp01((0.01f - d) / 0.02f);
                        Color col = core < 0f
                            ? new Color(0.35f, 0.88f, 0.98f, 1f)
                            : new Color(0.68f, 0.42f, 0.96f, 1f);
                        fg = new Color(col.r, col.g, col.b, a);
                    }
                    break;
                }
            }

            Color finalCol = Color.Lerp(bg, fg, fg.a);
            finalCol.a = baseAlpha;
            return finalCol;
        }

        private static float SdfCircle(float x, float y, float radius)
        {
            return Mathf.Sqrt(x * x + y * y) - radius;
        }

        private static float SdfDiamond(float x, float y, float radius)
        {
            return (Mathf.Abs(x) + Mathf.Abs(y)) * 0.70710678f - radius * 0.70710678f;
        }

        private static float SdfRoundedBox(float x, float y, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x) - Mathf.Max(0f, halfW - radius);
            float qy = Mathf.Abs(y) - Mathf.Max(0f, halfH - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        private static float SdfTriangle(float x, float y, float halfBase, float halfHeight)
        {
            float ax = Mathf.Abs(x);
            float ny = (y + halfHeight) / Mathf.Max(0.0001f, halfHeight * 2f);
            float allowedWidth = halfBase * (1f - Mathf.Clamp01(ny));
            float horizDist = ax - allowedWidth;
            float vertDist = Mathf.Abs(y) - halfHeight;
            return Mathf.Max(horizDist * 0.85f, vertDist);
        }

        private static float SdfSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax;
            float pay = py - ay;
            float bax = bx - ax;
            float bay = by - ay;
            float lenSq = bax * bax + bay * bay;
            float h = lenSq > 0.00001f ? Mathf.Clamp01((pax * bax + pay * bay) / lenSq) : 0f;
            float dx = pax - bax * h;
            float dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
