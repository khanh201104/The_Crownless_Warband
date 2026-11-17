using System;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    /// <summary>
    /// Quản lý Object Pool 32 phần tử hiệu ứng chiến đấu 2D (Procedural Texture + Zero-GC).
    /// Hỗ trợ vệt chém cận chiến, tia đạn tầm xa, vòng sóng xung kích, khiên chắn, hạt sáng và bùng nổ Phase Boss.
    /// </summary>
    public class CombatVfxManager : MonoBehaviour
    {
        private const int PoolSize = 32;
        private const int VfxSortingOrder = 3;

        private enum VfxMotionType
        {
            ExpandAndFade,
            ProjectileLinear,
            SlashSweep
        }

        private struct VfxInstance
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Transform Tr;
            public bool Active;
            public VfxMotionType Motion;
            public float Elapsed;
            public float Duration;
            public Vector3 StartPos;
            public Vector3 EndPos;
            public Vector3 StartScale;
            public Vector3 EndScale;
            public float StartAngleDeg;
            public float EndAngleDeg;
            public float RiseSpeed;
            public Color BaseColor;
        }

        private static Sprite _slashArcSprite;
        private static Sprite _projectileBoltSprite;
        private static Sprite _shockwaveRingSprite;
        private static Sprite _sparkBurstSprite;

        private readonly VfxInstance[] _pool = new VfxInstance[PoolSize];
        private GameObject _poolContainer;
        private bool _poolInitialized;
        private int _nextCursor;

        private void Awake()
        {
            EnsurePoolInitialized();
        }

        private void Update()
        {
            if (!_poolInitialized) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < PoolSize; i++)
            {
                if (!_pool[i].Active) continue;

                _pool[i].Elapsed += dt;
                float duration = _pool[i].Duration;
                float t = duration > 0.0001f ? Mathf.Clamp01(_pool[i].Elapsed / duration) : 1f;

                if (_pool[i].Elapsed >= duration)
                {
                    _pool[i].Active = false;
                    if (_pool[i].Renderer != null)
                    {
                        _pool[i].Renderer.enabled = false;
                    }
                    continue;
                }

                Transform tr = _pool[i].Tr;
                SpriteRenderer sr = _pool[i].Renderer;
                if (tr == null || sr == null)
                {
                    _pool[i].Active = false;
                    continue;
                }

                Color col = _pool[i].BaseColor;

                switch (_pool[i].Motion)
                {
                    case VfxMotionType.ProjectileLinear:
                    {
                        float moveT = Mathf.Clamp01(t / 0.88f);
                        tr.position = Vector3.Lerp(_pool[i].StartPos, _pool[i].EndPos, moveT);
                        tr.localRotation = Quaternion.Euler(0f, 0f, _pool[i].StartAngleDeg);
                        tr.localScale = Vector3.Lerp(_pool[i].StartScale, _pool[i].EndScale, t);
                        float fade = t > 0.78f ? Mathf.Clamp01((1f - t) / 0.22f) : 1f;
                        col.a *= fade;
                        sr.color = col;
                        break;
                    }

                    case VfxMotionType.SlashSweep:
                    {
                        float easeOut = 1f - (1f - t) * (1f - t);
                        float angle = Mathf.LerpAngle(_pool[i].StartAngleDeg, _pool[i].EndAngleDeg, easeOut);
                        tr.position = _pool[i].StartPos;
                        tr.localRotation = Quaternion.Euler(0f, 0f, angle);
                        tr.localScale = Vector3.LerpUnclamped(_pool[i].StartScale, _pool[i].EndScale, easeOut);
                        float fade = Mathf.Clamp01(1f - t * t);
                        col.a *= fade;
                        sr.color = col;
                        break;
                    }

                    default: // ExpandAndFade
                    {
                        float easeOut = 1f - (1f - t) * (1f - t);
                        Vector3 pos = _pool[i].StartPos;
                        pos.y += _pool[i].RiseSpeed * _pool[i].Elapsed;
                        tr.position = pos;
                        tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_pool[i].StartAngleDeg, _pool[i].EndAngleDeg, t));
                        tr.localScale = Vector3.LerpUnclamped(_pool[i].StartScale, _pool[i].EndScale, easeOut);
                        float fade = Mathf.Clamp01(1f - t * t);
                        col.a *= fade;
                        sr.color = col;
                        break;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_poolContainer != null)
            {
                Destroy(_poolContainer);
                _poolContainer = null;
            }
            _poolInitialized = false;
        }

        public void ClearAll()
        {
            if (!_poolInitialized) return;

            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i].Active = false;
                if (_pool[i].Renderer != null)
                {
                    _pool[i].Renderer.enabled = false;
                }
            }
        }

        public void SpawnSlashArc(Vector3 fromPos, Vector3 targetPos, Color color, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);

            Vector3 dir = targetPos - fromPos;
            dir.z = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
            }
            else
            {
                dir = Vector3.right;
            }

            Vector3 spawnPos = targetPos - dir * 0.15f;
            spawnPos.z = -0.15f;

            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            Color slashColor = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.95f));
            int idx = AcquireSlot();
            StartSlot(
                idx,
                GetSlashArcSprite(),
                VfxMotionType.SlashSweep,
                spawnPos,
                spawnPos,
                new Vector3(0.85f, 0.85f, 1f),
                new Vector3(1.65f, 1.65f, 1f),
                baseAngle - 24f,
                baseAngle + 24f,
                0f,
                0.26f / speed,
                slashColor);
        }

        public void SpawnProjectile(Vector3 fromPos, Vector3 toPos, Color color, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);

            Vector3 startPos = new Vector3(fromPos.x, fromPos.y, -0.15f);
            Vector3 endPos = new Vector3(toPos.x, toPos.y, -0.15f);
            Vector3 delta = endPos - startPos;
            float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Color boltColor = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.95f));
            int boltIdx = AcquireSlot();
            StartSlot(
                boltIdx,
                GetProjectileBoltSprite(),
                VfxMotionType.ProjectileLinear,
                startPos,
                endPos,
                new Vector3(1.18f, 0.78f, 1f),
                new Vector3(1.18f, 0.78f, 1f),
                angleDeg,
                angleDeg,
                0f,
                0.20f / speed,
                boltColor);

            // Vòng sáng nhỏ tại điểm bắn (muzzle flare)
            Color muzzleColor = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.92f));
            int muzzleIdx = AcquireSlot();
            StartSlot(
                muzzleIdx,
                GetShockwaveRingSprite(),
                VfxMotionType.ExpandAndFade,
                startPos,
                startPos,
                new Vector3(0.32f, 0.32f, 1f),
                new Vector3(1.05f, 1.05f, 1f),
                0f,
                0f,
                0f,
                0.18f / speed,
                muzzleColor);
        }

        public void SpawnHitSpark(Vector3 targetPos, Color color, bool isHeavyHit, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);

            Vector3 pos = new Vector3(targetPos.x, targetPos.y, -0.16f);
            float endScale = isHeavyHit ? 1.45f : 1.05f;
            float rot = UnityEngine.Random.Range(0f, 360f);

            int sparkIdx = AcquireSlot();
            StartSlot(
                sparkIdx,
                GetSparkBurstSprite(),
                VfxMotionType.ExpandAndFade,
                pos,
                pos,
                new Vector3(0.45f, 0.45f, 1f),
                new Vector3(endScale, endScale, 1f),
                rot,
                rot + 25f,
                0f,
                0.25f / speed,
                color);

            if (isHeavyHit)
            {
                Color ringColor = new Color(color.r, color.g, color.b, color.a * 0.85f);
                int ringIdx = AcquireSlot();
                StartSlot(
                    ringIdx,
                    GetShockwaveRingSprite(),
                    VfxMotionType.ExpandAndFade,
                    pos,
                    pos,
                    new Vector3(0.35f, 0.35f, 1f),
                    new Vector3(1.55f, 1.55f, 1f),
                    0f,
                    0f,
                    0f,
                    0.26f / speed,
                    ringColor);
            }
        }

        public void SpawnShockwave(Vector3 centerPos, Color color, float maxScale = 2.2f, float duration = 0.38f, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);
            Vector3 pos = new Vector3(centerPos.x, centerPos.y, -0.14f);

            int idx = AcquireSlot();
            StartSlot(
                idx,
                GetShockwaveRingSprite(),
                VfxMotionType.ExpandAndFade,
                pos,
                pos,
                new Vector3(0.35f, 0.35f, 1f),
                new Vector3(maxScale, maxScale, 1f),
                0f,
                0f,
                0f,
                Mathf.Max(0.05f, duration) / speed,
                color);
        }

        public void SpawnHealSparkle(Vector3 targetPos, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);

            Color sparkColor = new Color(0.302f, 0.949f, 0.549f, 1f); // #4DF28C
            Vector3 centerPos = new Vector3(targetPos.x, targetPos.y, -0.16f);

            int sparkIdx = AcquireSlot();
            StartSlot(
                sparkIdx,
                GetSparkBurstSprite(),
                VfxMotionType.ExpandAndFade,
                centerPos,
                centerPos,
                new Vector3(0.62f, 0.62f, 1f),
                new Vector3(1.32f, 1.32f, 1f),
                0f,
                40f,
                0.65f,
                0.38f / speed,
                sparkColor);

            Color mintRingColor = new Color(0.56f, 1.0f, 0.80f, 0.92f);
            Vector3 feetPos = new Vector3(targetPos.x, targetPos.y - 0.32f, -0.12f);

            int ringIdx = AcquireSlot();
            StartSlot(
                ringIdx,
                GetShockwaveRingSprite(),
                VfxMotionType.ExpandAndFade,
                feetPos,
                feetPos,
                new Vector3(0.55f, 0.30f, 1f),
                new Vector3(1.50f, 0.80f, 1f),
                0f,
                0f,
                0f,
                0.34f / speed,
                mintRingColor);
        }

        public void SpawnShieldDome(Vector3 targetPos, bool isIntercept, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);

            Color domeColor = new Color(0.302f, 0.851f, 1.0f, 0.95f); // #4DD9FF
            Vector3 pos = new Vector3(targetPos.x, targetPos.y, -0.15f);
            float endScale = isIntercept ? 1.80f : 1.45f;

            int idx = AcquireSlot();
            StartSlot(
                idx,
                GetShockwaveRingSprite(),
                VfxMotionType.ExpandAndFade,
                pos,
                pos,
                new Vector3(0.68f, 0.68f, 1f),
                new Vector3(endScale, endScale, 1f),
                0f,
                0f,
                0f,
                0.36f / speed,
                domeColor);
        }

        public void SpawnBossPhaseBurst(Vector3 bossPos, int phase, float speedMultiplier = 1f)
        {
            EnsurePoolInitialized();
            float speed = Mathf.Max(0.25f, speedMultiplier);

            Color phaseColor = phase >= 3
                ? new Color(0.937f, 0.267f, 0.267f, 1f)  // #EF4444 Phase 3
                : new Color(0.961f, 0.620f, 0.043f, 1f); // #F59E0B Phase 2
            Vector3 pos = new Vector3(bossPos.x, bossPos.y, -0.18f);

            int ring1Idx = AcquireSlot();
            StartSlot(
                ring1Idx,
                GetShockwaveRingSprite(),
                VfxMotionType.ExpandAndFade,
                pos,
                pos,
                new Vector3(0.45f, 0.45f, 1f),
                new Vector3(2.6f, 2.6f, 1f),
                0f,
                0f,
                0f,
                0.36f / speed,
                phaseColor);

            Color outerRingColor = new Color(phaseColor.r, phaseColor.g, phaseColor.b, 0.80f);
            int ring2Idx = AcquireSlot();
            StartSlot(
                ring2Idx,
                GetShockwaveRingSprite(),
                VfxMotionType.ExpandAndFade,
                pos,
                pos,
                new Vector3(0.65f, 0.65f, 1f),
                new Vector3(3.4f, 3.4f, 1f),
                0f,
                0f,
                0f,
                0.50f / speed,
                outerRingColor);

            int burstIdx = AcquireSlot();
            StartSlot(
                burstIdx,
                GetSparkBurstSprite(),
                VfxMotionType.ExpandAndFade,
                pos,
                pos,
                new Vector3(0.75f, 0.75f, 1f),
                new Vector3(2.0f, 2.0f, 1f),
                0f,
                45f,
                0f,
                0.42f / speed,
                phaseColor);
        }

        private void EnsurePoolInitialized()
        {
            if (_poolInitialized && _poolContainer != null) return;

            if (_poolContainer == null)
            {
                _poolContainer = new GameObject("VfxPoolContainer");
                _poolContainer.transform.SetParent(transform, false);
            }

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new GameObject($"VfxItem_{i:D2}");
                go.transform.SetParent(_poolContainer.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = VfxSortingOrder;
                sr.enabled = false;

                _pool[i] = new VfxInstance
                {
                    Go = go,
                    Renderer = sr,
                    Tr = go.transform,
                    Active = false
                };
            }

            _poolInitialized = true;
            _nextCursor = 0;
        }

        private int AcquireSlot()
        {
            for (int offset = 0; offset < PoolSize; offset++)
            {
                int idx = (_nextCursor + offset) % PoolSize;
                if (!_pool[idx].Active)
                {
                    _nextCursor = (idx + 1) % PoolSize;
                    return idx;
                }
            }

            // Nếu pool đầy 32/32, thu hồi hiệu ứng chạy gần xong nhất
            int bestIdx = 0;
            float highestProgress = -1f;
            for (int i = 0; i < PoolSize; i++)
            {
                float progress = _pool[i].Duration > 0.0001f ? (_pool[i].Elapsed / _pool[i].Duration) : 1f;
                if (progress > highestProgress)
                {
                    highestProgress = progress;
                    bestIdx = i;
                }
            }

            _nextCursor = (bestIdx + 1) % PoolSize;
            return bestIdx;
        }

        private void StartSlot(
            int index,
            Sprite sprite,
            VfxMotionType motion,
            Vector3 startPos,
            Vector3 endPos,
            Vector3 startScale,
            Vector3 endScale,
            float startAngleDeg,
            float endAngleDeg,
            float riseSpeed,
            float duration,
            Color baseColor)
        {
            if (index < 0 || index >= PoolSize) return;

            _pool[index].Active = true;
            _pool[index].Motion = motion;
            _pool[index].Elapsed = 0f;
            _pool[index].Duration = Mathf.Max(0.04f, duration);
            _pool[index].StartPos = startPos;
            _pool[index].EndPos = endPos;
            _pool[index].StartScale = startScale;
            _pool[index].EndScale = endScale;
            _pool[index].StartAngleDeg = startAngleDeg;
            _pool[index].EndAngleDeg = endAngleDeg;
            _pool[index].RiseSpeed = riseSpeed;
            _pool[index].BaseColor = baseColor;

            Transform tr = _pool[index].Tr;
            if (tr != null)
            {
                tr.position = startPos;
                tr.localRotation = Quaternion.Euler(0f, 0f, startAngleDeg);
                tr.localScale = startScale;
            }

            SpriteRenderer sr = _pool[index].Renderer;
            if (sr != null)
            {
                sr.sprite = sprite;
                sr.color = baseColor;
                sr.enabled = true;
            }
        }

        // =========================================================================
        // PROCEDURAL VFX TEXTURES & STATIC CACHE (Zero-GC trong lúc chiến đấu)
        // =========================================================================

        public static Sprite GetSlashArcSprite()
        {
            if (_slashArcSprite != null) return _slashArcSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "VfxSlashArcTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            const float centerX = -0.20f;
            const float arcRadius = 0.52f;
            const float maxTheta = 1.18f;

            for (int y = 0; y < size; y++)
            {
                float py = ((y + 0.5f) / size) - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float px = ((x + 0.5f) / size) - 0.5f;
                    float dx = px - centerX;
                    float dy = py;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float theta = Mathf.Atan2(dy, dx);

                    float alpha = 0f;
                    float core = 0f;
                    if (Mathf.Abs(theta) < maxTheta)
                    {
                        float tipTaper = Mathf.Cos((theta / maxTheta) * (Mathf.PI * 0.5f));
                        float halfThickness = 0.145f * tipTaper * tipTaper;
                        if (halfThickness > 0.002f)
                        {
                            float radialDist = r - arcRadius;
                            // Cạnh trước (radialDist > 0) sắc, cạnh sau mềm hơn để tạo cảm giác quét gió
                            float normalizedDist = radialDist >= 0f
                                ? radialDist / Mathf.Max(0.004f, halfThickness * 0.65f)
                                : (-radialDist) / Mathf.Max(0.004f, halfThickness * 1.25f);
                            alpha = Mathf.Clamp01(1f - normalizedDist);
                            alpha = Mathf.Sqrt(Mathf.Clamp01(alpha)) * tipTaper;
                            core = Mathf.Clamp01(1f - normalizedDist * 1.4f);
                        }
                    }

                    float rgb = Mathf.Lerp(0.82f, 1.0f, core);
                    pixels[y * size + x] = new Color(rgb, rgb, rgb, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _slashArcSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            _slashArcSprite.name = "VfxSlashArcSprite";
            return _slashArcSprite;
        }

        public static Sprite GetProjectileBoltSprite()
        {
            if (_projectileBoltSprite != null) return _projectileBoltSprite;

            const int width = 64;
            const int height = 32;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "VfxProjectileBoltTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float py = ((y + 0.5f) / height) - 0.5f; // -0.5 .. 0.5
                for (int x = 0; x < width; x++)
                {
                    float px = ((x + 0.5f) / width) - 0.5f; // -0.5 .. 0.5

                    float alpha = 0f;
                    float core = 0f;
                    if (px > -0.46f && px < 0.47f)
                    {
                        float halfH = px >= 0.12f
                            ? Mathf.Lerp(0.30f, 0.02f, (px - 0.12f) / 0.35f)
                            : Mathf.Lerp(0.03f, 0.30f, (px + 0.46f) / 0.58f);

                        float normY = Mathf.Abs(py) / Mathf.Max(0.01f, halfH);
                        if (normY < 1.25f)
                        {
                            float edge = Mathf.Clamp01(1f - normY);
                            float tailFade = px < 0.12f ? Mathf.Clamp01((px + 0.46f) / 0.52f) : 1f;
                            alpha = Mathf.Clamp01(edge * edge * tailFade * 1.15f);
                            core = Mathf.Clamp01((1f - normY * 1.7f) * tailFade);
                        }
                    }

                    float rgb = Mathf.Lerp(0.78f, 1.0f, core);
                    pixels[y * width + x] = new Color(rgb, rgb, rgb, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _projectileBoltSprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
            _projectileBoltSprite.name = "VfxProjectileBoltSprite";
            return _projectileBoltSprite;
        }

        public static Sprite GetShockwaveRingSprite()
        {
            if (_shockwaveRingSprite != null) return _shockwaveRingSprite;

            const int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "VfxShockwaveRingTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            const float ringRadius = 0.39f;
            const float halfBand = 0.095f;

            for (int y = 0; y < size; y++)
            {
                float py = ((y + 0.5f) / size) - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float px = ((x + 0.5f) / size) - 0.5f;
                    float dist = Mathf.Sqrt(px * px + py * py);
                    float ringDist = Mathf.Abs(dist - ringRadius);

                    float ringAlpha = Mathf.Clamp01(1f - (ringDist / halfBand));
                    ringAlpha = Mathf.Pow(Mathf.Clamp01(ringAlpha), 0.75f);

                    // Quầng sáng mềm bên trong vòng sóng giúp ShieldDome và shockwave nổi bật trên nền tối
                    float innerHalo = dist < ringRadius
                        ? Mathf.Clamp01(dist / ringRadius) * 0.22f
                        : 0f;

                    float alpha = Mathf.Clamp01(Mathf.Max(ringAlpha, innerHalo));
                    float rgb = Mathf.Lerp(0.84f, 1.0f, ringAlpha);
                    pixels[y * size + x] = new Color(rgb, rgb, rgb, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _shockwaveRingSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            _shockwaveRingSprite.name = "VfxShockwaveRingSprite";
            return _shockwaveRingSprite;
        }

        public static Sprite GetSparkBurstSprite()
        {
            if (_sparkBurstSprite != null) return _sparkBurstSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "VfxSparkBurstTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float py = ((y + 0.5f) / size) - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float px = ((x + 0.5f) / size) - 0.5f;
                    float r = Mathf.Sqrt(px * px + py * py);

                    // Tâm sáng tròn
                    float centerGlow = Mathf.Clamp01(1f - (r / 0.18f));
                    centerGlow *= centerGlow;

                    // 4 tia chính theo trục X/Y
                    float crossRayX = Mathf.Clamp01(1f - Mathf.Abs(py) / 0.055f) * Mathf.Clamp01(1f - Mathf.Abs(px) / 0.44f);
                    float crossRayY = Mathf.Clamp01(1f - Mathf.Abs(px) / 0.055f) * Mathf.Clamp01(1f - Mathf.Abs(py) / 0.44f);

                    // 4 tia phụ theo đường chéo 45 độ
                    float rx = (px + py) * 0.70710678f;
                    float ry = (px - py) * 0.70710678f;
                    float diagRay1 = Mathf.Clamp01(1f - Mathf.Abs(ry) / 0.042f) * Mathf.Clamp01(1f - Mathf.Abs(rx) / 0.31f);
                    float diagRay2 = Mathf.Clamp01(1f - Mathf.Abs(rx) / 0.042f) * Mathf.Clamp01(1f - Mathf.Abs(ry) / 0.31f);

                    // Các chấm hạt năng lượng nhỏ xung quanh
                    float dotA = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(px, py), new Vector2(0.28f, 0.24f)) / 0.065f);
                    float dotB = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(px, py), new Vector2(-0.27f, 0.26f)) / 0.055f);
                    float dotC = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(px, py), new Vector2(-0.25f, -0.28f)) / 0.060f);
                    float dotD = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(px, py), new Vector2(0.29f, -0.22f)) / 0.055f);

                    float star = Mathf.Max(Mathf.Max(crossRayX, crossRayY), Mathf.Max(diagRay1, diagRay2) * 0.85f);
                    float dots = Mathf.Max(Mathf.Max(dotA, dotB), Mathf.Max(dotC, dotD)) * 0.90f;
                    float alpha = Mathf.Clamp01(Mathf.Max(centerGlow, Mathf.Max(star, dots)));
                    float rgb = Mathf.Lerp(0.82f, 1.0f, centerGlow);
                    pixels[y * size + x] = new Color(rgb, rgb, rgb, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _sparkBurstSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            _sparkBurstSprite.name = "VfxSparkBurstSprite";
            return _sparkBurstSprite;
        }
    }
}
