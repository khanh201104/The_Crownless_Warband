using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    public class BattleArenaBackdropView : MonoBehaviour
    {
        private const int TextureWidth = 512;
        private const int TextureHeight = 288;
        private const float BackdropPixelsPerUnit = 23f;

        private static readonly Dictionary<BiomeType, Sprite> _biomeSprites = new Dictionary<BiomeType, Sprite>(4);

        private GameObject _backdropContainer;
        private SpriteRenderer _backdropRenderer;
        private SpriteRenderer _dividerRenderer;
        private Canvas _floorLabelsCanvas;

        private bool _isBuilt;
        private bool _hasInitializedVisibility;
        private bool _lastVisible;
        private BiomeType _lastBiome;

        private void Awake()
        {
            EnsureBuilt();
            SetBiomeAndVisible(BiomeType.RuinedCity, false);
        }

        private void OnDestroy()
        {
            if (_backdropContainer != null)
            {
                Destroy(_backdropContainer);
                _backdropContainer = null;
            }
            _backdropRenderer = null;
            _dividerRenderer = null;
            _floorLabelsCanvas = null;
            _isBuilt = false;
        }

        public void SetBiomeAndVisible(BiomeType biome, bool visible)
        {
            EnsureBuilt();

            if (_hasInitializedVisibility && _lastVisible == visible && (!visible || _lastBiome == biome))
            {
                return;
            }

            _hasInitializedVisibility = true;
            _lastVisible = visible;

            if (_backdropContainer != null && _backdropContainer.activeSelf != visible)
            {
                _backdropContainer.SetActive(visible);
            }

            if (visible && (_lastBiome != biome || _backdropRenderer.sprite == null))
            {
                _lastBiome = biome;
                _backdropRenderer.sprite = GetOrCreateBiomeSprite(biome);
                ApplyDividerTintForBiome(biome);
            }
            else if (visible)
            {
                _lastBiome = biome;
            }
        }

        private void EnsureBuilt()
        {
            if (_isBuilt && _backdropContainer != null) return;

            _backdropContainer = new GameObject("ArenaBackdropContainer");
            _backdropContainer.transform.SetParent(transform, false);

            // 1. Phông nền 2D toàn màn hình (sortingOrder = -20)
            GameObject bgObj = new GameObject("ArenaBackdropSprite");
            bgObj.transform.SetParent(_backdropContainer.transform, false);
            bgObj.transform.localPosition = new Vector3(0f, 0f, 1f);
            _backdropRenderer = bgObj.AddComponent<SpriteRenderer>();
            _backdropRenderer.sortingOrder = -20;
            _backdropRenderer.sprite = GetOrCreateBiomeSprite(BiomeType.RuinedCity);

            // 2. Vạch chia chiến tuyến giữa sân (sortingOrder = -5)
            GameObject divObj = new GameObject("CenterDividerLine");
            divObj.transform.SetParent(_backdropContainer.transform, false);
            divObj.transform.localPosition = new Vector3(0f, 0.05f, 0.5f);
            divObj.transform.localScale = new Vector3(0.04f, 4.25f, 1f);
            _dividerRenderer = divObj.AddComponent<SpriteRenderer>();
            _dividerRenderer.sortingOrder = -5;
            _dividerRenderer.sprite = ProceduralUnitArtFactory.GetRoundedBarSprite();
            ApplyDividerTintForBiome(BiomeType.RuinedCity);

            // 3. Nhãn hàng chiến thuật dưới sàn (WorldSpace Canvas, sortingOrder = -4)
            GameObject canvasObj = new GameObject("FloorZoneLabelsCanvas");
            canvasObj.transform.SetParent(_backdropContainer.transform, false);
            canvasObj.transform.localPosition = Vector3.zero;
            canvasObj.transform.localScale = new Vector3(0.01f, 0.01f, 1f);

            _floorLabelsCanvas = canvasObj.AddComponent<Canvas>();
            _floorLabelsCanvas.renderMode = RenderMode.WorldSpace;
            _floorLabelsCanvas.sortingOrder = -4;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            Color playerZoneColor = new Color(0.490f, 0.827f, 0.988f, 0.55f); // #7DD3FC @ 0.55
            Color enemyZoneColor = new Color(0.988f, 0.647f, 0.647f, 0.55f);  // #FCA5A5 @ 0.55

            CreateFloorZoneLabel(canvasObj.transform, font, "PlayerBackLabel", "BACK", -3.75f, -2.66f, playerZoneColor);
            CreateFloorZoneLabel(canvasObj.transform, font, "PlayerMidLabel", "MID", -2.55f, -2.66f, playerZoneColor);
            CreateFloorZoneLabel(canvasObj.transform, font, "PlayerFrontLabel", "FRONT", -1.35f, -2.66f, playerZoneColor);

            CreateFloorZoneLabel(canvasObj.transform, font, "EnemyFrontLabel", "FRONT", 1.35f, -2.66f, enemyZoneColor);
            CreateFloorZoneLabel(canvasObj.transform, font, "EnemyMidLabel", "MID", 2.55f, -2.66f, enemyZoneColor);
            CreateFloorZoneLabel(canvasObj.transform, font, "EnemyBackLabel", "BACK", 3.75f, -2.66f, enemyZoneColor);

            _isBuilt = true;
        }

        private void ApplyDividerTintForBiome(BiomeType biome)
        {
            if (_dividerRenderer == null) return;

            switch (biome)
            {
                case BiomeType.IndustrialZone:
                    _dividerRenderer.color = new Color(0.96f, 0.65f, 0.25f, 0.35f);
                    break;
                case BiomeType.LabComplex:
                    _dividerRenderer.color = new Color(0.28f, 0.92f, 0.88f, 0.35f);
                    break;
                default:
                    _dividerRenderer.color = new Color(0.75f, 0.88f, 1.00f, 0.35f);
                    break;
            }
        }

        private static void CreateFloorZoneLabel(
            Transform parent,
            Font font,
            string objName,
            string label,
            float worldX,
            float worldY,
            Color color)
        {
            GameObject go = new GameObject(objName, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(180f, 48f);
            rt.anchoredPosition = new Vector2(worldX * 100f, worldY * 100f);

            UnityEngine.UI.Text uiText = go.AddComponent<UnityEngine.UI.Text>();
            uiText.font = font;
            uiText.text = label;
            uiText.fontSize = 32;
            uiText.fontStyle = FontStyle.Bold;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = color;
            uiText.raycastTarget = false;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static Sprite GetOrCreateBiomeSprite(BiomeType biome)
        {
            if (_biomeSprites.TryGetValue(biome, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
            tex.name = $"ArenaBiomeTex_{biome}";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[TextureWidth * TextureHeight];
            ResolveBiomePalette(
                biome,
                out Color skyTop,
                out Color skyHorizon,
                out Color silhouetteColor,
                out Color silhouetteGlow,
                out Color groundNear,
                out Color groundFar,
                out Color daisFill,
                out Color daisRing);

            const float horizonY = 0.72f;

            for (int y = 0; y < TextureHeight; y++)
            {
                float ny = (y + 0.5f) / TextureHeight; // 0 bottom .. 1 top
                for (int x = 0; x < TextureWidth; x++)
                {
                    float nx = (x + 0.5f) / TextureWidth; // 0 left .. 1 right
                    Color pxColor;

                    if (ny >= horizonY)
                    {
                        float skyT = (ny - horizonY) / (1f - horizonY);
                        pxColor = Color.Lerp(skyHorizon, skyTop, Mathf.SmoothStep(0f, 1f, skyT));

                        // Distant horizon skyline silhouette
                        float skylineHeight = SampleBiomeSkylineHeight(biome, nx);
                        if (ny <= skylineHeight)
                        {
                            float blendEdge = Mathf.Clamp01((skylineHeight - ny) * 140f);
                            pxColor = Color.Lerp(pxColor, silhouetteColor, blendEdge);

                            // Subtle window/furnace/bio-glow accents inside skyline structures
                            float glowMask = SampleSkylineGlowMask(biome, nx, ny, skylineHeight);
                            if (glowMask > 0f)
                            {
                                pxColor = Color.Lerp(pxColor, silhouetteGlow, glowMask * 0.72f);
                            }
                        }
                    }
                    else
                    {
                        float depthT = ny / horizonY; // 0 front edge .. 1 horizon
                        pxColor = Color.Lerp(groundNear, groundFar, Mathf.SmoothStep(0f, 1f, depthT));

                        // Subtle horizon rim transition
                        if (ny > horizonY - 0.025f)
                        {
                            float rimT = (ny - (horizonY - 0.025f)) / 0.025f;
                            pxColor = Color.Lerp(pxColor, Color.Lerp(groundFar, skyHorizon, 0.45f), rimT);
                        }

                        // Subtle tiled ground grid texture variation
                        pxColor = ApplyFloorPattern(biome, nx, ny, pxColor, daisRing);

                        // Central elliptical stone Arena Dais (centered around combat frontlines)
                        pxColor = ApplyCentralArenaDais(nx, ny, pxColor, daisFill, daisRing);
                    }

                    // Smooth corner vignette so unit sprites & 4-corner HUD stand out sharply
                    float vx = (nx - 0.5f) * 2f;
                    float vy = (ny - 0.48f) * 2f;
                    float cornerDist = Mathf.Clamp01((vx * vx * 0.55f + vy * vy * 0.65f) - 0.32f);
                    float vignette = 1f - cornerDist * 0.52f;
                    pxColor.r *= vignette;
                    pxColor.g *= vignette;
                    pxColor.b *= vignette;
                    pxColor.a = 1f;

                    pixels[y * TextureWidth + x] = pxColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, TextureWidth, TextureHeight),
                new Vector2(0.5f, 0.5f),
                BackdropPixelsPerUnit);
            sprite.name = $"ArenaBiomeSprite_{biome}";
            _biomeSprites[biome] = sprite;
            return sprite;
        }

        private static void ResolveBiomePalette(
            BiomeType biome,
            out Color skyTop,
            out Color skyHorizon,
            out Color silhouetteColor,
            out Color silhouetteGlow,
            out Color groundNear,
            out Color groundFar,
            out Color daisFill,
            out Color daisRing)
        {
            switch (biome)
            {
                case BiomeType.IndustrialZone:
                    skyTop = new Color32(0x18, 0x11, 0x0E, 255);
                    skyHorizon = new Color32(0x4A, 0x2B, 0x18, 255);
                    silhouetteColor = new Color32(0x1E, 0x14, 0x10, 255);
                    silhouetteGlow = new Color(0.98f, 0.52f, 0.14f, 1f);
                    groundNear = new Color32(0x16, 0x11, 0x0F, 255);
                    groundFar = new Color32(0x27, 0x1C, 0x16, 255);
                    daisFill = new Color(0.28f, 0.21f, 0.16f, 1f);
                    daisRing = new Color(0.86f, 0.54f, 0.22f, 1f);
                    break;

                case BiomeType.LabComplex:
                    skyTop = new Color32(0x0C, 0x16, 0x18, 255);
                    skyHorizon = new Color32(0x1D, 0x42, 0x40, 255);
                    silhouetteColor = new Color32(0x0E, 0x1E, 0x20, 255);
                    silhouetteGlow = new Color(0.24f, 0.88f, 0.84f, 1f);
                    groundNear = new Color32(0x0E, 0x18, 0x1A, 255);
                    groundFar = new Color32(0x16, 0x29, 0x2A, 255);
                    daisFill = new Color(0.14f, 0.27f, 0.29f, 1f);
                    daisRing = new Color(0.26f, 0.84f, 0.82f, 1f);
                    break;

                default: // RuinedCity
                    skyTop = new Color32(0x12, 0x16, 0x1F, 255);
                    skyHorizon = new Color32(0x26, 0x32, 0x44, 255);
                    silhouetteColor = new Color32(0x11, 0x16, 0x20, 255);
                    silhouetteGlow = new Color(0.45f, 0.72f, 0.92f, 1f);
                    groundNear = new Color32(0x11, 0x15, 0x1D, 255);
                    groundFar = new Color32(0x1E, 0x26, 0x34, 255);
                    daisFill = new Color(0.20f, 0.25f, 0.33f, 1f);
                    daisRing = new Color(0.56f, 0.68f, 0.82f, 1f);
                    break;
            }
        }

        private static float SampleBiomeSkylineHeight(BiomeType biome, float nx)
        {
            float baseH = 0.745f;
            switch (biome)
            {
                case BiomeType.IndustrialZone:
                {
                    float ridge = 0.025f * Mathf.Sin(nx * 24f) + 0.020f * Mathf.Cos(nx * 53f);
                    bool stack1 = Mathf.Abs(nx - 0.16f) < 0.018f || Mathf.Abs(nx - 0.21f) < 0.015f;
                    bool stack2 = Mathf.Abs(nx - 0.78f) < 0.020f || Mathf.Abs(nx - 0.84f) < 0.016f;
                    bool craneBeam = nx > 0.56f && nx < 0.74f && Mathf.Abs(nx - 0.64f) < 0.014f;
                    float factory = (nx > 0.08f && nx < 0.30f) || (nx > 0.70f && nx < 0.92f) ? 0.045f : 0f;
                    float stacks = (stack1 || stack2) ? 0.135f : (craneBeam ? 0.105f : 0f);
                    return baseH + Mathf.Max(factory + Mathf.Abs(ridge), stacks);
                }

                case BiomeType.LabComplex:
                {
                    float sideVatsL = Mathf.Abs(nx - 0.14f) < 0.045f || Mathf.Abs(nx - 0.26f) < 0.038f ? 0.110f : 0f;
                    float sideVatsR = Mathf.Abs(nx - 0.74f) < 0.038f || Mathf.Abs(nx - 0.86f) < 0.045f ? 0.110f : 0f;
                    float domeDx = (nx - 0.50f) / 0.20f;
                    float dome = Mathf.Abs(domeDx) < 1f ? Mathf.Sqrt(1f - domeDx * domeDx) * 0.085f : 0f;
                    return baseH + Mathf.Max(dome, Mathf.Max(sideVatsL, sideVatsR));
                }

                default: // RuinedCity
                {
                    int block = Mathf.FloorToInt(nx * 18f);
                    float seedH = Mathf.Abs(Mathf.Sin(block * 12.9898f + 1.7f));
                    float slant = Mathf.Sin(nx * 46f) * 0.016f;
                    bool spireL = Mathf.Abs(nx - 0.18f) < 0.022f;
                    bool spireR = Mathf.Abs(nx - 0.82f) < 0.025f;
                    float tower = (spireL || spireR) ? 0.125f : seedH * 0.090f;
                    return baseH + Mathf.Max(0.015f, tower + slant);
                }
            }
        }

        private static float SampleSkylineGlowMask(BiomeType biome, float nx, float ny, float skylineHeight)
        {
            if (ny < 0.725f || ny > skylineHeight - 0.015f) return 0f;

            switch (biome)
            {
                case BiomeType.IndustrialZone:
                    // Furnace embers near factory base
                    if (ny < 0.775f && ((nx > 0.11f && nx < 0.28f) || (nx > 0.72f && nx < 0.89f)))
                    {
                        float cell = Mathf.Sin(nx * 160f) * Mathf.Cos(ny * 140f);
                        return cell > 0.55f ? 1f : 0f;
                    }
                    return 0f;

                case BiomeType.LabComplex:
                    // Vertical containment core strips
                    if ((Mathf.Abs(nx - 0.14f) < 0.008f || Mathf.Abs(nx - 0.26f) < 0.008f ||
                         Mathf.Abs(nx - 0.74f) < 0.008f || Mathf.Abs(nx - 0.86f) < 0.008f) &&
                        ny < 0.84f)
                    {
                        return 0.85f;
                    }
                    return 0f;

                default:
                    // Faint moonlight slits in ruined towers
                    if ((Mathf.Abs(nx - 0.18f) < 0.005f || Mathf.Abs(nx - 0.82f) < 0.005f) && ny < 0.86f)
                    {
                        return 0.45f;
                    }
                    return 0f;
            }
        }

        private static Color ApplyFloorPattern(BiomeType biome, float nx, float ny, Color baseColor, Color accentColor)
        {
            float tileGridX = Mathf.Abs(Mathf.Sin(nx * Mathf.PI * 16f));
            float tileGridY = Mathf.Abs(Mathf.Sin(ny * Mathf.PI * 14f));
            bool onGroove = tileGridX < 0.045f || tileGridY < 0.055f;

            if (biome == BiomeType.LabComplex)
            {
                bool onConduit = Mathf.Abs(ny - 0.34f) < 0.004f || Mathf.Abs(ny - 0.60f) < 0.004f;
                if (onConduit)
                {
                    return Color.Lerp(baseColor, accentColor, 0.26f);
                }
                if (onGroove)
                {
                    return Color.Lerp(baseColor, accentColor, 0.12f);
                }
            }
            else if (onGroove)
            {
                return new Color(baseColor.r * 0.85f, baseColor.g * 0.85f, baseColor.b * 0.85f, 1f);
            }

            return baseColor;
        }

        private static Color ApplyCentralArenaDais(float nx, float ny, Color baseColor, Color daisFill, Color daisRing)
        {
            // Elliptical central dais spanning Player Front (-1.35) to Enemy Front (+1.35) and vertical battle zone
            const float centerX = 0.50f;
            const float centerY = 0.504f;
            const float radiusX = 0.255f;
            const float radiusY = 0.19f;

            float dx = (nx - centerX) / radiusX;
            float dy = (ny - centerY) / radiusY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist > 1.03f) return baseColor;

            float fillMask = Mathf.Clamp01((1.0f - dist) * 28f);
            float radialBright = Mathf.Lerp(1.06f, 0.88f, dist);
            Color stoneFill = new Color(daisFill.r * radialBright, daisFill.g * radialBright, daisFill.b * radialBright, 1f);
            Color col = Color.Lerp(baseColor, stoneFill, fillMask * 0.75f);

            // Outer ring & Inner concentric ring
            float outerRing = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.985f) * 58f);
            float innerRing = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.72f) * 68f);
            float crossHair = (Mathf.Abs(dx) < 0.012f && dist < 0.70f) || (Mathf.Abs(dy) < 0.016f && dist < 0.70f)
                ? 0.25f
                : 0f;

            float ringStrength = Mathf.Max(outerRing * 0.65f, Mathf.Max(innerRing * 0.42f, crossHair));
            if (ringStrength > 0f)
            {
                col = Color.Lerp(col, daisRing, ringStrength);
            }

            return col;
        }
    }
}
