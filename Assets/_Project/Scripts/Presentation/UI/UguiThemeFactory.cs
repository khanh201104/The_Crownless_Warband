using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CrownlessWarband.Presentation.UI
{
    public static class UguiThemeFactory
    {
        public static readonly Color PanelBg = new Color(0.08f, 0.10f, 0.14f, 0.92f);
        public static readonly Color PanelBgSoft = new Color(0.11f, 0.14f, 0.20f, 0.85f);
        public static readonly Color AccentGold = new Color(0.96f, 0.62f, 0.04f, 1f);
        public static readonly Color AllyCyan = new Color(0.13f, 0.83f, 0.93f, 1f);
        public static readonly Color EnemyCrimson = new Color(0.94f, 0.27f, 0.27f, 1f);
        public static readonly Color HpGreen = new Color(0.18f, 0.82f, 0.45f, 1f);
        public static readonly Color ShieldBlue = new Color(0.24f, 0.65f, 0.98f, 1f);
        public static readonly Color EnergyGold = new Color(0.98f, 0.78f, 0.18f, 1f);
        public static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f, 1f);
        public static readonly Color TextMuted = new Color(0.64f, 0.69f, 0.78f, 1f);

        private static Font _cachedFont;
        private static TMP_FontAsset _cachedTmpFont;
        private static TMP_FontAsset _cachedTmpFallbackFont;
        private static Sprite _roundedPanelSprite;
        private static Sprite _roundedOutlineSprite;
        private static Sprite _circleSprite;

        public static Font GetDefaultFont()
        {
            if (_cachedFont != null)
            {
                return _cachedFont;
            }

            _cachedFont = Resources.Load<Font>("Fonts/PaytoneOne-Regular");
            if (_cachedFont == null)
            {
                _cachedFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            }
            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return _cachedFont;
        }

        public static TMP_FontAsset GetTmpFontAsset()
        {
            if (_cachedTmpFont != null)
            {
                return _cachedTmpFont;
            }

            Font lilitaFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            Font paytoneFont = Resources.Load<Font>("Fonts/PaytoneOne-Regular");

            if (paytoneFont != null)
            {
                _cachedTmpFallbackFont = TMP_FontAsset.CreateFontAsset(
                    paytoneFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (_cachedTmpFallbackFont != null)
                {
                    _cachedTmpFallbackFont.name = "PaytoneOne-Regular SDF Fallback";
                    ConfigureTmpMaterial(_cachedTmpFallbackFont.material);
                }
            }

            if (lilitaFont != null)
            {
                _cachedTmpFont = TMP_FontAsset.CreateFontAsset(
                    lilitaFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (_cachedTmpFont != null)
                {
                    _cachedTmpFont.name = "LilitaOne-Regular SDF";
                    ConfigureTmpMaterial(_cachedTmpFont.material);
                    if (_cachedTmpFallbackFont != null)
                    {
                        if (_cachedTmpFont.fallbackFontAssetTable == null)
                        {
                            _cachedTmpFont.fallbackFontAssetTable = new List<TMP_FontAsset>(2);
                        }
                        _cachedTmpFont.fallbackFontAssetTable.Add(_cachedTmpFallbackFont);
                    }
                }
            }

            if (_cachedTmpFont == null)
            {
                _cachedTmpFont = _cachedTmpFallbackFont != null ? _cachedTmpFallbackFont : TMP_Settings.defaultFontAsset;
            }

            return _cachedTmpFont;
        }

        private static void ConfigureTmpMaterial(Material mat)
        {
            if (mat == null) return;

            ShaderUtilities.GetShaderPropertyIDs();
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            {
                mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(6, 10, 18, 235));
            }
            if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.14f);
            }

            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color32(0, 0, 0, 175));
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayDilate))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.12f);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            }
        }

        public static UnityEngine.EventSystems.EventSystem EnsureEventSystem()
        {
            UnityEngine.EventSystems.EventSystem existing = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject("EventSystem");
            UnityEngine.EventSystems.EventSystem eventSystem = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            return eventSystem;
        }

        public static Canvas CreateOverlayCanvas(string name, int sortingOrder = 10)
        {
            EnsureEventSystem();

            GameObject canvasGo = new GameObject(string.IsNullOrEmpty(name) ? "UIOverlayCanvas" : name);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            UnityEngine.UI.CanvasScaler scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvas;
        }

        public static Sprite GetRoundedPanelSprite()
        {
            if (_roundedPanelSprite != null)
            {
                return _roundedPanelSprite;
            }

            const int size = 32;
            const float radius = 6f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "UguiRoundedPanelTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = ComputeRoundedRectAlpha(x + 0.5f, y + 0.5f, size, size, radius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _roundedPanelSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(6f, 6f, 6f, 6f));
            _roundedPanelSprite.name = "UguiRoundedPanelSprite";
            return _roundedPanelSprite;
        }

        public static Sprite GetRoundedOutlineSprite()
        {
            if (_roundedOutlineSprite != null)
            {
                return _roundedOutlineSprite;
            }

            const int size = 32;
            const float radius = 6f;
            const float borderWidth = 2f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "UguiRoundedOutlineTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float outerAlpha = ComputeRoundedRectAlpha(px, py, size, size, radius);
                    float innerAlpha = ComputeRoundedRectAlpha(
                        px - borderWidth,
                        py - borderWidth,
                        size - borderWidth * 2f,
                        size - borderWidth * 2f,
                        Mathf.Max(1f, radius - borderWidth));
                    float alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _roundedOutlineSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(6f, 6f, 6f, 6f));
            _roundedOutlineSprite.name = "UguiRoundedOutlineSprite";
            return _roundedOutlineSprite;
        }

        public static Sprite GetCircleSprite()
        {
            if (_circleSprite != null)
            {
                return _circleSprite;
            }

            const int size = 64;
            float center = size * 0.5f;
            float radius = center - 1f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "UguiCircleTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _circleSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            _circleSprite.name = "UguiCircleSprite";
            return _circleSprite;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(string.IsNullOrEmpty(name) ? "Rect" : name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            if (parent != null)
            {
                rt.SetParent(parent, false);
            }
            rt.localScale = Vector3.one;
            return rt;
        }

        public static UnityEngine.UI.Image CreatePanel(string name, Transform parent, Color color, bool rounded = true)
        {
            RectTransform rt = CreateRect(name, parent);
            UnityEngine.UI.Image image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = GetRoundedPanelSprite();
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return image;
        }

        public static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:  return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:   return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:   return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:  return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:  return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:   return TextAlignmentOptions.BottomRight;
                default:                      return TextAlignmentOptions.Left;
            }
        }

        public static void ApplyTitleGradient(TextMeshProUGUI tmp, Color topColor, Color bottomColor)
        {
            if (tmp == null) return;
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(topColor, topColor, bottomColor, bottomColor);
        }

        public static TextMeshProUGUI CreateTmpText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle fontStyle = FontStyle.Normal)
        {
            RectTransform rt = CreateRect(name, parent);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset fontAsset = GetTmpFontAsset();
            if (fontAsset != null)
            {
                tmp.font = fontAsset;
                if (fontAsset.material != null)
                {
                    tmp.fontSharedMaterial = fontAsset.material;
                }
            }
            tmp.text = text ?? string.Empty;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = ConvertAlignment(alignment);
            tmp.fontStyle = (fontStyle == FontStyle.Italic || fontStyle == FontStyle.BoldAndItalic)
                ? FontStyles.Italic
                : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.extraPadding = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static UnityEngine.UI.Button CreateTmpButton(
            string name,
            Transform parent,
            string label,
            Color bgColor,
            float fontSize = 16f,
            bool isCircle = false)
        {
            RectTransform rt = CreateRect(name, parent);
            UnityEngine.UI.Image bgImage = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = Color.white;
            if (isCircle)
            {
                bgImage.sprite = GetCircleSprite();
                bgImage.type = UnityEngine.UI.Image.Type.Simple;
            }
            else
            {
                bgImage.sprite = GetRoundedPanelSprite();
                bgImage.type = UnityEngine.UI.Image.Type.Sliced;
            }

            UnityEngine.UI.Button button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bgImage;
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;

            // Tắt keyboard/Space navigation mặc định để phím [SPACE] không vô tình bấm lại nút vừa click
            UnityEngine.UI.Navigation noNav = button.navigation;
            noNav.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = noNav;

            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.22f);
            colors.selectedColor = bgColor;
            colors.disabledColor = new Color(bgColor.r * 0.45f, bgColor.g * 0.45f, bgColor.b * 0.45f, bgColor.a * 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TextMeshProUGUI labelText = CreateTmpText(
                "Label",
                rt,
                label,
                fontSize,
                TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            RectTransform labelRt = labelText.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 4f);
            labelRt.offsetMax = new Vector2(-8f, -4f);

            return button;
        }

        public static TMP_InputField CreateTmpInputField(
            string name,
            Transform parent,
            string placeholderText,
            float fontSize = 16f)
        {
            UnityEngine.UI.Image bgImage = CreatePanel(
                string.IsNullOrEmpty(name) ? "InputField" : name,
                parent,
                new Color(0.06f, 0.08f, 0.12f, 0.96f),
                true);
            RectTransform bgRt = bgImage.rectTransform;

            CreateOutlineFrame("Outline", bgRt, new Color(0.28f, 0.36f, 0.48f, 0.9f));

            TMP_InputField inputField = bgRt.gameObject.AddComponent<TMP_InputField>();
            inputField.targetGraphic = bgImage;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            RectTransform textAreaRt = CreateRect("Text Area", bgRt);
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(12f, 4f);
            textAreaRt.offsetMax = new Vector2(-12f, -4f);
            UnityEngine.UI.RectMask2D mask = textAreaRt.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            mask.padding = new Vector4(-2f, -2f, -2f, -2f);

            TextMeshProUGUI placeholder = CreateTmpText(
                "Placeholder",
                textAreaRt,
                placeholderText,
                fontSize,
                TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Italic);
            placeholder.raycastTarget = false;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Truncate;
            RectTransform placeholderRt = placeholder.rectTransform;
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = Vector2.zero;
            placeholderRt.offsetMax = Vector2.zero;

            TextMeshProUGUI inputText = CreateTmpText(
                "Text",
                textAreaRt,
                string.Empty,
                fontSize,
                TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            inputText.richText = false;
            inputText.raycastTarget = false;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            inputText.overflowMode = TextOverflowModes.Overflow;
            RectTransform textRt = inputText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            inputField.textViewport = textAreaRt;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            TMP_FontAsset fontAsset = GetTmpFontAsset();
            if (fontAsset != null)
            {
                inputField.fontAsset = fontAsset;
            }
            inputField.pointSize = fontSize;
            inputField.customCaretColor = true;
            inputField.caretColor = AccentGold;
            inputField.selectionColor = new Color(0.13f, 0.83f, 0.93f, 0.35f);
            return inputField;
        }

        public static UnityEngine.UI.Slider CreateSlider(
            string name,
            Transform parent,
            float minValue,
            float maxValue,
            float value)
        {
            RectTransform rootRt = CreateRect(name, parent);
            UnityEngine.UI.Slider slider = rootRt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            UnityEngine.UI.Navigation sliderNoNav = slider.navigation;
            sliderNoNav.mode = UnityEngine.UI.Navigation.Mode.None;
            slider.navigation = sliderNoNav;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = value;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            // Background
            UnityEngine.UI.Image bgImage = CreatePanel("Background", rootRt, new Color(0.16f, 0.20f, 0.28f, 1f), true);
            RectTransform bgRt = bgImage.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill Area
            RectTransform fillAreaRt = CreateRect("Fill Area", rootRt);
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(4f, 0f);
            fillAreaRt.offsetMax = new Vector2(-4f, 0f);

            UnityEngine.UI.Image fillImage = CreatePanel("Fill", fillAreaRt, AccentGold, true);
            RectTransform fillRt = fillImage.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // Handle Slide Area
            RectTransform handleAreaRt = CreateRect("Handle Slide Area", rootRt);
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(8f, 0f);
            handleAreaRt.offsetMax = new Vector2(-8f, 0f);

            RectTransform handleRt = CreateRect("Handle", handleAreaRt);
            handleRt.sizeDelta = new Vector2(20f, 0f);
            UnityEngine.UI.Image handleImage = handleRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            handleImage.sprite = GetCircleSprite();
            handleImage.color = TextPrimary;
            handleImage.preserveAspect = true;

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, minValue, maxValue));

            return slider;
        }

        public static UnityEngine.UI.Image CreateOutlineFrame(string name, Transform parent, Color borderColor)
        {
            RectTransform rt = CreateRect(string.IsNullOrEmpty(name) ? "Outline" : name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image outlineImage = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            outlineImage.sprite = GetRoundedOutlineSprite();
            outlineImage.type = UnityEngine.UI.Image.Type.Sliced;
            outlineImage.color = borderColor;
            outlineImage.raycastTarget = false;
            return outlineImage;
        }

        private static float ComputeRoundedRectAlpha(float px, float py, float width, float height, float radius)
        {
            if (width <= 0f || height <= 0f)
            {
                return 0f;
            }

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(halfW, halfH));
            float qx = Mathf.Abs(px - halfW) - (halfW - r);
            float qy = Mathf.Abs(py - halfH) - (halfH - r);
            float outsideDist = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float insideDist = Mathf.Min(Mathf.Max(qx, qy), 0f);
            float signedDist = outsideDist + insideDist - r;
            return Mathf.Clamp01(0.5f - signedDist);
        }
    }
}
