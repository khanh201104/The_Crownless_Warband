using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    public static class ProceduralUnitArtFactory
    {
        private static Sprite _roundedTileFloorSprite;
        private static Sprite _roundedTileOutlineSprite;
        private static Sprite _roundedBarSprite;
        private static Sprite _dropShadowSprite;
        private static Sprite _groundRingSprite;
        private static readonly Dictionary<int, Sprite> _unitSpriteCache = new Dictionary<int, Sprite>(16);

        private static readonly Color OutlineColor = new Color(0.05f, 0.07f, 0.10f, 1f);
        private static readonly Color BossCrownGold = new Color(0.961f, 0.620f, 0.043f, 1f);   // #F59E0B
        private static readonly Color BossCoreRed = new Color(0.937f, 0.267f, 0.267f, 1f);     // #EF4444

        public static Sprite GetRoundedTileFloorSprite()
        {
            if (_roundedTileFloorSprite != null) return _roundedTileFloorSprite;

            const int size = 64;
            const float radius = 10f;
            Texture2D tex = CreateBlankTexture("TileFloorTex", size, size);
            Color[] pixels = new Color[size * size];

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dist = SdfRoundedBox(px - half, py - half, half - 2f, half - 2f, radius);
                    float edgeAlpha = Mathf.Clamp01(0.5f - dist);
                    if (edgeAlpha <= 0f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    // Soft radial dimming in center so tile blends gently into stone battlefield floor
                    float nx = (px - half) / half;
                    float ny = (py - half) / half;
                    float radial = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
                    float centerSoftness = Mathf.Lerp(0.68f, 1.0f, radial * radial);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, edgeAlpha * centerSoftness);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _roundedTileFloorSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
            _roundedTileFloorSprite.name = "RoundedTileFloorSprite";
            return _roundedTileFloorSprite;
        }

        public static Sprite GetRoundedTileOutlineSprite()
        {
            if (_roundedTileOutlineSprite != null) return _roundedTileOutlineSprite;

            const int size = 64;
            const float radius = 10f;
            const float border = 3f;
            Texture2D tex = CreateBlankTexture("TileOutlineTex", size, size);
            Color[] pixels = new Color[size * size];

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float outerDist = SdfRoundedBox(px - half, py - half, half - 1.5f, half - 1.5f, radius);
                    float innerDist = SdfRoundedBox(px - half, py - half, half - 1.5f - border, half - 1.5f - border, Mathf.Max(2f, radius - border));
                    float outerAlpha = Mathf.Clamp01(0.5f - outerDist);
                    float innerAlpha = Mathf.Clamp01(0.5f - innerDist);
                    float alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _roundedTileOutlineSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
            _roundedTileOutlineSprite.name = "RoundedTileOutlineSprite";
            return _roundedTileOutlineSprite;
        }

        public static Sprite GetRoundedBarSprite()
        {
            if (_roundedBarSprite != null) return _roundedBarSprite;

            const int width = 32;
            const int height = 32;
            const float radius = 5f;
            Texture2D tex = CreateBlankTexture("RoundedBarTex", width, height);
            Color[] pixels = new Color[width * height];

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = SdfRoundedBox((x + 0.5f) - halfW, (y + 0.5f) - halfH, halfW - 1f, halfH - 1f, radius);
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    float verticalHighlight = Mathf.Lerp(0.88f, 1.0f, (float)y / (height - 1));
                    pixels[y * width + x] = new Color(verticalHighlight, verticalHighlight, verticalHighlight, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _roundedBarSprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 32f);
            _roundedBarSprite.name = "RoundedBarSprite";
            return _roundedBarSprite;
        }

        public static Sprite GetDropShadowSprite()
        {
            if (_dropShadowSprite != null) return _dropShadowSprite;

            const int width = 96;
            const int height = 48;
            Texture2D tex = CreateBlankTexture("DropShadowTex", width, height);
            Color[] pixels = new Color[width * height];

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = ((x + 0.5f) - halfW) / (halfW - 2f);
                    float ny = ((y + 0.5f) - halfH) / (halfH - 2f);
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float falloff = Mathf.Clamp01(1f - r);
                    float alpha = falloff * falloff * (3f - 2f * falloff);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _dropShadowSprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 96f);
            _dropShadowSprite.name = "DropShadowSprite";
            return _dropShadowSprite;
        }

        public static Sprite GetGroundRingSprite()
        {
            if (_groundRingSprite != null) return _groundRingSprite;

            const int size = 96;
            Texture2D tex = CreateBlankTexture("GroundRingTex", size, size);
            Color[] pixels = new Color[size * size];

            float center = size * 0.5f;
            float ringRadius = 39f;
            float ringHalfThickness = 4.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ringDist = Mathf.Abs(dist - ringRadius) - ringHalfThickness;
                    float alpha = Mathf.Clamp01(0.8f - ringDist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _groundRingSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 96f);
            _groundRingSprite.name = "GroundRingSprite";
            return _groundRingSprite;
        }

        public static Sprite GetUnitSprite(UnitRole role, TeamSide team, bool isBoss)
        {
            int key = ((int)team << 8) | ((int)role << 1) | (isBoss ? 1 : 0);
            if (_unitSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            const int size = 128;
            Texture2D tex = CreateBlankTexture($"UnitSprite_{team}_{role}_{(isBoss ? "Boss" : "Std")}", size, size);
            Color[] pixels = new Color[size * size];

            Color primary = GetRoleArtBibleColor(role, team, isBoss);
            float facing = team == TeamSide.Player ? 1f : -1f;

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size; // 0 bottom .. 1 top
                for (int x = 0; x < size; x++)
                {
                    // Normalized coordinates centered horizontally (-0.5..0.5), mirrored by team facing
                    float px = (((x + 0.5f) / size) - 0.5f) * facing;
                    float py = ny - 0.5f;

                    Color pixel = isBoss
                        ? SampleBossPixel(px, py, primary)
                        : SampleClassPixel(role, team, px, py, primary);

                    pixels[y * size + x] = pixel;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 128f);
            sprite.name = $"Sprite_{team}_{role}_{(isBoss ? "Boss" : "Std")}";
            _unitSpriteCache[key] = sprite;
            return sprite;
        }

        private static Color SampleBossPixel(float px, float py, Color primary)
        {
            // Heavy armored torso & spiked pauldrons
            float torsoDist = SdfRoundedBox(px, py + 0.04f, 0.25f, 0.25f, 0.07f);
            float leftPauldron = SdfRoundedBox(px + 0.23f, py - 0.09f, 0.12f, 0.10f, 0.03f);
            float rightPauldron = SdfRoundedBox(px - 0.23f, py - 0.09f, 0.12f, 0.10f, 0.03f);
            float helmDist = SdfRoundedBox(px, py - 0.23f, 0.14f, 0.12f, 0.05f);
            float leftLeg = SdfRoundedBox(px + 0.11f, py + 0.34f, 0.08f, 0.11f, 0.03f);
            float rightLeg = SdfRoundedBox(px - 0.11f, py + 0.34f, 0.08f, 0.11f, 0.03f);

            // 3-prong crown on head
            float crownCenter = SdfTriangle(px, py - 0.39f, 0.055f, 0.09f);
            float crownLeft = SdfTriangle(px + 0.09f, py - 0.36f, 0.045f, 0.075f);
            float crownRight = SdfTriangle(px - 0.09f, py - 0.36f, 0.045f, 0.075f);
            float crownDist = Mathf.Min(crownCenter, Mathf.Min(crownLeft, crownRight));

            // Pauldron spikes
            float spikeL = SdfTriangle(px + 0.32f, py - 0.20f, 0.045f, 0.07f);
            float spikeR = SdfTriangle(px - 0.32f, py - 0.20f, 0.045f, 0.07f);

            float bodyDist = Mathf.Min(torsoDist, Mathf.Min(Mathf.Min(leftPauldron, rightPauldron), Mathf.Min(helmDist, Mathf.Min(leftLeg, rightLeg))));
            float fullDist = Mathf.Min(bodyDist, Mathf.Min(crownDist, Mathf.Min(spikeL, spikeR)));

            const float outlineThickness = 0.017f;
            float outerAlpha = AlphaFromSdf(fullDist - outlineThickness);
            if (outerAlpha <= 0f) return Color.clear;

            Color result = OutlineColor;
            float innerAlpha = AlphaFromSdf(fullDist);
            if (innerAlpha > 0f)
            {
                float shade = Mathf.Lerp(0.76f, 1.18f, Mathf.Clamp01(py + 0.5f));
                Color armorColor = ScaleColorRgb(primary, shade);

                // Shoulder gold trim
                bool onShoulderTrim = (leftPauldron < 0.002f && py > 0.13f) || (rightPauldron < 0.002f && py > 0.13f);
                if (onShoulderTrim)
                {
                    armorColor = BossCrownGold;
                }

                // Helm visor slit
                float visorDist = SdfRoundedBox(px - 0.015f, py - 0.22f, 0.09f, 0.022f, 0.008f);
                if (visorDist < 0f)
                {
                    armorColor = Color.Lerp(BossCoreRed, Color.white, 0.25f);
                }

                // Golden crown fill
                if (crownDist < 0.002f && crownDist <= bodyDist)
                {
                    armorColor = Color.Lerp(BossCrownGold, Color.white, Mathf.Clamp01((py - 0.33f) * 3f));
                }

                // Glowing Crimson Chest Core + outer golden ring
                float coreOuter = SdfCircle(px, py + 0.01f, 0.115f);
                float coreInner = SdfCircle(px, py + 0.01f, 0.075f);
                if (coreInner < 0f)
                {
                    float glow = Mathf.Clamp01(1f - (-coreInner / 0.075f));
                    armorColor = Color.Lerp(new Color(1f, 0.85f, 0.55f, 1f), BossCoreRed, glow * 0.7f);
                }
                else if (coreOuter < 0f)
                {
                    armorColor = BossCrownGold;
                }

                result = Color.Lerp(OutlineColor, armorColor, innerAlpha);
            }

            result.a = outerAlpha;
            return result;
        }

        private static Color SampleClassPixel(UnitRole role, TeamSide team, float px, float py, Color primary)
        {
            Color accentLight = team == TeamSide.Player
                ? new Color(0.90f, 0.96f, 1.00f, 1f)
                : new Color(1.00f, 0.84f, 0.78f, 1f);
            Color darkTrim = ScaleColorRgb(primary, 0.42f);

            // Base body SDF varies slightly by Role silhouette
            float bodyDist;
            float emblemMask = -1f; // < 0 means inside special foreground emblem/weapon
            Color emblemColor = accentLight;

            switch (role)
            {
                case UnitRole.Guardian:
                {
                    // Broad armored body + rounded square helm + hexagonal front shield
                    float torso = SdfRoundedBox(px, py + 0.06f, 0.20f, 0.21f, 0.05f);
                    float shoulders = SdfRoundedBox(px, py - 0.09f, 0.27f, 0.09f, 0.04f);
                    float helm = SdfRoundedBox(px, py - 0.24f, 0.125f, 0.11f, 0.045f);
                    float legs = SdfRoundedBox(px, py + 0.32f, 0.14f, 0.10f, 0.03f);
                    float shield = SdfHexagon(px - 0.13f, py + 0.04f, 0.185f);
                    bodyDist = Mathf.Min(Mathf.Min(torso, shoulders), Mathf.Min(Mathf.Min(helm, legs), shield));

                    float shieldInner = SdfHexagon(px - 0.13f, py + 0.04f, 0.145f);
                    if (shield <= 0.002f)
                    {
                        emblemMask = shield;
                        float crossV = SdfRoundedBox(px - 0.13f, py + 0.04f, 0.025f, 0.095f, 0.008f);
                        float crossH = SdfRoundedBox(px - 0.13f, py + 0.04f, 0.080f, 0.025f, 0.008f);
                        bool onShieldEmblem = crossV < 0f || crossH < 0f;
                        if (shieldInner >= 0f)
                        {
                            emblemColor = accentLight;
                        }
                        else if (onShieldEmblem)
                        {
                            emblemColor = BossCrownGold;
                        }
                        else
                        {
                            emblemColor = ScaleColorRgb(primary, 0.78f);
                        }
                    }
                    break;
                }

                case UnitRole.Vanguard:
                {
                    // Sleek warrior + crested helm + diagonal bright spear/halberd across front
                    float torso = SdfRoundedBox(px, py + 0.05f, 0.165f, 0.20f, 0.05f);
                    float shoulders = SdfRoundedBox(px, py - 0.09f, 0.22f, 0.07f, 0.03f);
                    float head = SdfCircle(px, py - 0.24f, 0.11f);
                    float crest = SdfTriangle(px + 0.02f, py - 0.36f, 0.06f, 0.07f);
                    float legs = Mathf.Min(
                        SdfRoundedBox(px + 0.075f, py + 0.32f, 0.055f, 0.10f, 0.025f),
                        SdfRoundedBox(px - 0.075f, py + 0.32f, 0.055f, 0.10f, 0.025f));
                    float shaft = SdfSegment(px, py, -0.22f, -0.22f, 0.22f, 0.20f) - 0.022f;
                    float spearTip = SdfTriangleRotated(px - 0.27f, py - 0.25f, 0.075f, 0.12f, 0.785f);
                    float weaponDist = Mathf.Min(shaft, spearTip);
                    bodyDist = Mathf.Min(Mathf.Min(torso, shoulders), Mathf.Min(Mathf.Min(head, crest), Mathf.Min(legs, weaponDist)));

                    if (weaponDist <= 0.002f)
                    {
                        emblemMask = weaponDist;
                        emblemColor = spearTip <= 0.002f
                            ? accentLight
                            : new Color(0.55f, 0.36f, 0.22f, 1f);
                    }
                    break;
                }

                case UnitRole.Ranger:
                {
                    // Hooded cloak + agile torso + curved Longbow & Arrow on front
                    float cloak = SdfTriangle(px + 0.04f, py + 0.04f, 0.19f, 0.32f);
                    float torso = SdfRoundedBox(px, py + 0.06f, 0.135f, 0.18f, 0.045f);
                    float hood = SdfCircle(px - 0.01f, py - 0.23f, 0.115f);
                    float hoodTip = SdfTriangle(px + 0.10f, py - 0.26f, 0.06f, 0.07f);
                    float bowArc = Mathf.Abs(SdfCircle(px - 0.05f, py - 0.01f, 0.24f)) - 0.024f;
                    // Clip bow arc to front right half (px > 0.10)
                    bowArc = Mathf.Max(bowArc, 0.11f - px);
                    float bowString = SdfSegment(px, py, 0.12f, -0.21f, 0.12f, 0.21f) - 0.010f;
                    float arrowShaft = SdfSegment(px, py, 0.04f, 0.00f, 0.34f, 0.00f) - 0.014f;
                    float arrowTip = SdfTriangleRotated(px - 0.35f, py, 0.045f, 0.07f, 1.5708f);
                    float bowDist = Mathf.Min(Mathf.Min(bowArc, bowString), Mathf.Min(arrowShaft, arrowTip));
                    bodyDist = Mathf.Min(Mathf.Min(cloak, torso), Mathf.Min(Mathf.Min(hood, hoodTip), bowDist));

                    if (bowDist <= 0.002f)
                    {
                        emblemMask = bowDist;
                        if (arrowTip <= 0.002f) emblemColor = accentLight;
                        else if (bowArc <= 0.002f) emblemColor = BossCrownGold;
                        else emblemColor = new Color(0.85f, 0.90f, 0.95f, 1f);
                    }
                    break;
                }

                case UnitRole.Mage:
                {
                    // Pointed wizard hat + wide flowing robe + glowing floating Arcane Diamond & rune ring
                    float robe = SdfTriangle(px, py + 0.11f, 0.22f, 0.34f);
                    float collar = SdfRoundedBox(px, py - 0.07f, 0.21f, 0.07f, 0.03f);
                    float head = SdfCircle(px, py - 0.20f, 0.095f);
                    float hatBrim = SdfRoundedBox(px, py - 0.27f, 0.18f, 0.028f, 0.012f);
                    float hatCone = SdfTriangle(px + 0.02f, py - 0.37f, 0.105f, 0.14f);
                    float orbHalo = Mathf.Abs(SdfCircle(px - 0.24f, py - 0.05f, 0.115f)) - 0.015f;
                    float diamondGem = SdfDiamond(px - 0.24f, py - 0.05f, 0.085f);
                    float magicDist = Mathf.Min(orbHalo, diamondGem);
                    bodyDist = Mathf.Min(Mathf.Min(robe, collar), Mathf.Min(Mathf.Min(head, hatBrim), Mathf.Min(hatCone, magicDist)));

                    if (magicDist <= 0.002f)
                    {
                        emblemMask = magicDist;
                        emblemColor = diamondGem <= 0.002f
                            ? Color.Lerp(Color.white, new Color(0.60f, 0.95f, 1.0f, 1f), 0.4f)
                            : new Color(0.98f, 0.78f, 0.25f, 1f);
                    }
                    break;
                }

                case UnitRole.Medic:
                default:
                {
                    // Clean healer cowl/cloak + rounded tabard + bright Sacred Cross on chest
                    float robe = SdfRoundedBox(px, py + 0.10f, 0.175f, 0.25f, 0.06f);
                    float shoulders = SdfRoundedBox(px, py - 0.07f, 0.225f, 0.08f, 0.04f);
                    float head = SdfCircle(px, py - 0.22f, 0.11f);
                    float halo = Mathf.Abs(SdfCircle(px, py - 0.25f, 0.145f)) - 0.014f;
                    halo = Mathf.Max(halo, 0.22f - py); // upper arc halo
                    bodyDist = Mathf.Min(Mathf.Min(robe, shoulders), Mathf.Min(head, halo));

                    float crossV = SdfRoundedBox(px - 0.03f, py + 0.04f, 0.032f, 0.105f, 0.01f);
                    float crossH = SdfRoundedBox(px - 0.03f, py + 0.04f, 0.105f, 0.032f, 0.01f);
                    float crossDist = Mathf.Min(crossV, crossH);
                    if (crossDist <= 0.002f)
                    {
                        emblemMask = crossDist;
                        emblemColor = new Color(0.95f, 1.00f, 0.97f, 1f);
                    }
                    else if (halo <= 0.002f)
                    {
                        emblemMask = halo;
                        emblemColor = BossCrownGold;
                    }
                    break;
                }
            }

            const float outlineThickness = 0.017f;
            float outerAlpha = AlphaFromSdf(bodyDist - outlineThickness);
            if (outerAlpha <= 0f) return Color.clear;

            Color result = OutlineColor;
            float innerAlpha = AlphaFromSdf(bodyDist);
            if (innerAlpha > 0f)
            {
                float verticalShade = Mathf.Lerp(0.76f, 1.15f, Mathf.Clamp01(py + 0.5f));
                Color shadedPrimary = ScaleColorRgb(primary, verticalShade);

                // Subtle visor/face tone on head area
                float faceArea = SdfRoundedBox(px - 0.03f, py - 0.22f, 0.06f, 0.045f, 0.02f);
                if (faceArea < 0f && role != UnitRole.Mage)
                {
                    shadedPrimary = Color.Lerp(shadedPrimary, darkTrim, 0.55f);
                    float eye = SdfCircle(px - 0.055f, py - 0.215f, 0.016f);
                    if (eye < 0f)
                    {
                        shadedPrimary = accentLight;
                    }
                }

                // Belt / waist trim for clear torso-to-leg break
                if (Mathf.Abs(py + 0.16f) < 0.022f && Mathf.Abs(px) < 0.17f)
                {
                    shadedPrimary = darkTrim;
                }

                if (emblemMask <= 0.002f)
                {
                    shadedPrimary = emblemColor;
                }

                result = Color.Lerp(OutlineColor, shadedPrimary, innerAlpha);
            }

            result.a = outerAlpha;
            return result;
        }

        private static Color GetRoleArtBibleColor(UnitRole role, TeamSide team, bool isBoss)
        {
            if (isBoss)
            {
                return new Color(0.22f, 0.18f, 0.24f, 1f);
            }

            if (team == TeamSide.Player)
            {
                switch (role)
                {
                    case UnitRole.Guardian: return new Color32(0x33, 0xBF, 0xFF, 255); // #33BFFF
                    case UnitRole.Vanguard: return new Color32(0x33, 0xD9, 0x8C, 255); // #33D98C
                    case UnitRole.Ranger:   return new Color32(0xA6, 0xE6, 0x40, 255); // #A6E640
                    case UnitRole.Mage:     return new Color32(0x99, 0x66, 0xF2, 255); // #9966F2
                    case UnitRole.Medic:    return new Color32(0x59, 0xF2, 0xA6, 255); // #59F2A6
                }
            }
            else
            {
                switch (role)
                {
                    case UnitRole.Guardian: return new Color32(0xC0, 0x40, 0x40, 255); // #C04040
                    case UnitRole.Vanguard: return new Color32(0xE6, 0x59, 0x33, 255); // #E65933
                    case UnitRole.Ranger:   return new Color32(0xD9, 0x33, 0x8C, 255); // #D9338C
                    case UnitRole.Mage:     return new Color32(0xBF, 0x33, 0xD9, 255); // #BF33D9
                    case UnitRole.Medic:    return new Color32(0xE6, 0x73, 0x8C, 255); // #E6738C
                }
            }

            return Color.white;
        }

        private static Texture2D CreateBlankTexture(string texName, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = texName;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private static Color ScaleColorRgb(Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a);
        }

        private static float AlphaFromSdf(float dist)
        {
            return Mathf.Clamp01((0.008f - dist) / 0.016f);
        }

        private static float SdfCircle(float x, float y, float radius)
        {
            return Mathf.Sqrt(x * x + y * y) - radius;
        }

        private static float SdfDiamond(float x, float y, float radius)
        {
            return (Mathf.Abs(x) + Mathf.Abs(y)) * 0.70710678f - radius * 0.70710678f;
        }

        private static float SdfHexagon(float x, float y, float radius)
        {
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float d = Mathf.Max(ax * 0.8660254f + ay * 0.5f, ay);
            return d - radius * 0.8660254f;
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

        private static float SdfTriangleRotated(float x, float y, float halfBase, float halfHeight, float angleRad)
        {
            float c = Mathf.Cos(angleRad);
            float s = Mathf.Sin(angleRad);
            float rx = x * c - y * s;
            float ry = x * s + y * c;
            return SdfTriangle(rx, ry, halfBase, halfHeight);
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
