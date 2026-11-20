using System;
using System.Text;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Presentation.UI;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    /// <summary>
    /// Đại diện trực quan 2D cho một UnitState trên sàn đấu (GDD Sections 8, 9, 43).
    /// Quản lý Sprite nhân vật theo Role/Team, thanh HP/Shield/Energy và nhãn trạng thái nổi.
    /// </summary>
    public class UnitVisualView : MonoBehaviour
    {
        private const float BarWidth = 0.96f;
        private const float HpBarHeight = 0.095f;
        private const float ShieldBarHeight = 0.065f;
        private const float EnergyBarHeight = 0.060f;

        public string UnitInstanceId { get; private set; }
        public bool IsVisibleOnBattlefield { get; set; } = true;
        public bool SuppressOverheadGui { get; set; }
        public event Action<string> OnUnitClicked;

        private SpriteRenderer _shadowRenderer;
        private SpriteRenderer _selectionRingRenderer;
        private SpriteRenderer _bodyRenderer;

        private SpriteRenderer _hpBgRenderer;
        private SpriteRenderer _hpTrailRenderer;
        private SpriteRenderer _hpFillRenderer;
        private SpriteRenderer _shieldBgRenderer;
        private SpriteRenderer _shieldFillRenderer;
        private SpriteRenderer _energyBgRenderer;
        private SpriteRenderer _energyFillRenderer;

        private BoxCollider2D _collider;
        private Color _aliveBodyColor;
        private bool _usesCustomSprite;
        private bool _isBoss;
        private bool _isInitialized;
        private bool _hasInitialPosition;
        private bool _isAlive = true;
        private bool _isSelected;
        private bool _isActingUnit;

        private Vector3 _baseWorldPos;
        private Vector3 _baseBodyScale;
        private Vector3 _baseRingScale;

        // Trạng thái diễn hoạt Procedural Tweens (Zero-GC)
        private float _lungeTimer;
        private float _lungeDuration = 0.36f;
        private Vector3 _lungeOffsetTarget;

        private float _interceptTimer;
        private float _interceptDuration = 0.48f;
        private Vector3 _interceptWorldPos;

        private float _shakeTimer;
        private float _shakeDuration = 0.30f;
        private float _shakeMagnitude = 0.16f;

        private float _pulseTimer;
        private float _pulseDuration = 0.35f;
        private float _pulsePeakScale = 1.22f;

        private float _flashTimer;
        private float _flashDuration = 0.30f;
        private Color _flashColor = Color.white;

        private float _hpBarY;
        private float _shieldBarY;
        private float _energyBarY;

        // Trạng thái hiển thị từng bước (Step-by-Step Playback Snapshot)
        private int _displayHp;
        private int _displayMaxHp = 1;
        private int _displayShield;
        private int _displayEnergy;
        private int _displayMaxEnergy = 100;
        private bool _displayStun;
        private bool _displayMark;
        private bool _displayProt;
        private bool _displayVip;
        private int _displayBossPhase;
        private float _deathFadeTimer;
        private const float DeathFadeDuration = 0.35f;
        private UnitState _lastSyncedUnit;

        private float _trailHpRatio = 1f;
        private float _targetHpRatio = 1f;
        private float _trailHoldTimer = 0f;
        private const float TrailHoldDuration = 0.18f;
        private const float TrailShrinkSpeed = 0.90f;

        // Cache dữ liệu hiển thị OnGUI để tránh cấp phát chuỗi mỗi frame
        private string _cachedLabelText = string.Empty;
        private string _cachedShadowText = string.Empty;
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private int _lastShield = -1;
        private bool _lastStun;
        private bool _lastMark;
        private bool _lastProt;
        private bool _lastVip;
        private int _lastBossPhase = -1;
        private EnemyAffixType _lastAffix = EnemyAffixType.None;
        private string _lastName;
        private bool _lastIsBoss;

        private static GUIStyle _overheadStyle;
        private static GUIStyle _overheadShadowStyle;
        private static readonly StringBuilder _labelSb = new StringBuilder(160);
        private static readonly StringBuilder _stripSb = new StringBuilder(160);

        public void Initialize(UnitState unit, Sprite defaultSquareSprite, Sprite customRoleSprite = null)
        {
            if (unit == null) return;

            UnitInstanceId = unit.InstanceId;
            gameObject.name = $"Unit_{unit.Team}_{unit.Name}_{unit.InstanceId}";
            _isBoss = unit.BossPhase > 0 || unit.InstanceId == "e_boss_warden";
            float bossMultiplier = _isBoss ? 1.35f : 1.0f;

            _collider = GetComponent<BoxCollider2D>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider2D>();
            }
            _collider.size = new Vector2(0.95f * bossMultiplier, 1.15f * bossMultiplier);

            if (!_isInitialized)
            {
                _shadowRenderer = CreateChildRenderer("DropShadow", 0);
                _selectionRingRenderer = CreateChildRenderer("SelectionRing", 1);
                _bodyRenderer = CreateChildRenderer("BodySprite", 2);

                _hpBgRenderer = CreateChildRenderer("HpBarBg", 4);
                _hpTrailRenderer = CreateChildRenderer("HpBarTrail", 5);
                _hpFillRenderer = CreateChildRenderer("HpBarFill", 6);

                _shieldBgRenderer = CreateChildRenderer("ShieldBarBg", 4);
                _shieldFillRenderer = CreateChildRenderer("ShieldBarFill", 6);

                _energyBgRenderer = CreateChildRenderer("EnergyBarBg", 4);
                _energyFillRenderer = CreateChildRenderer("EnergyBarFill", 6);

                _isInitialized = true;
            }

            _usesCustomSprite = customRoleSprite != null;
            _aliveBodyColor = Color.white;

            _shadowRenderer.sprite = ProceduralUnitArtFactory.GetDropShadowSprite();
            _shadowRenderer.color = new Color(0f, 0f, 0f, 0.58f);
            _shadowRenderer.transform.localPosition = new Vector3(0f, -0.45f * bossMultiplier, 0f);
            _shadowRenderer.transform.localScale = new Vector3(0.92f * bossMultiplier, 0.34f * bossMultiplier, 1f);

            _selectionRingRenderer.sprite = ProceduralUnitArtFactory.GetGroundRingSprite();
            _selectionRingRenderer.color = new Color(0.25f, 1.0f, 0.9f, 0.9f);
            _selectionRingRenderer.transform.localPosition = new Vector3(0f, -0.43f * bossMultiplier, 0f);
            _baseRingScale = new Vector3(1.16f * bossMultiplier, 0.48f * bossMultiplier, 1f);
            _selectionRingRenderer.transform.localScale = _baseRingScale;
            _selectionRingRenderer.enabled = false;

            Sprite bodySprite = _usesCustomSprite
                ? customRoleSprite
                : ProceduralUnitArtFactory.GetUnitSprite(unit.Role, unit.Team, _isBoss);
            _bodyRenderer.sprite = bodySprite;
            _bodyRenderer.transform.localPosition = Vector3.zero;
            float targetSize = 1.04f * bossMultiplier;
            if (bodySprite != null && bodySprite.bounds.size.x > 0.001f && bodySprite.bounds.size.y > 0.001f)
            {
                float maxDim = Mathf.Max(bodySprite.bounds.size.x, bodySprite.bounds.size.y);
                float uniformScale = targetSize / maxDim;
                _baseBodyScale = new Vector3(uniformScale, uniformScale, 1f);
            }
            else
            {
                _baseBodyScale = new Vector3(targetSize, targetSize, 1f);
            }
            _bodyRenderer.transform.localScale = _baseBodyScale;

            // Sắp xếp vị trí 3 thanh chỉ số phía trên thân nhân vật
            float topBodyEdge = 0.52f * bossMultiplier;
            _energyBarY = topBodyEdge + 0.045f;
            _shieldBarY = _energyBarY + 0.055f;
            _hpBarY = _shieldBarY + 0.075f;

            SetupBarSprites(ProceduralUnitArtFactory.GetRoundedBarSprite(), unit.Team);

            _hasInitialPosition = false;
            _isSelected = false;
            _isActingUnit = false;
            _lungeTimer = 0f;
            _interceptTimer = 0f;
            _shakeTimer = 0f;
            _pulseTimer = 0f;
            _flashTimer = 0f;
            _lastSyncedUnit = unit;
            SnapshotFromUnitState(unit);
            _lastHp = -1; // Ép dựng lại overhead label lần đầu
        }

        public void SnapshotFromUnitState(UnitState unit)
        {
            if (unit == null) return;
            _lastSyncedUnit = unit;
            _displayHp = unit.CurrentHP;
            _displayMaxHp = Mathf.Max(1, unit.MaxHP);
            _displayShield = unit.Shield;
            _displayEnergy = unit.CurrentEnergy;
            _displayMaxEnergy = Mathf.Max(1, unit.MaxEnergy);
            _displayStun = unit.IsStunned;
            _displayMark = unit.IsMarked;
            _displayProt = !string.IsNullOrEmpty(unit.ProtectingAllyId);
            _displayVip = unit.IsPriorityObjective;
            _displayBossPhase = unit.BossPhase;
            _isAlive = _displayHp > 0;
            _deathFadeTimer = 0f;

            float ratio = Mathf.Clamp01((float)_displayHp / Mathf.Max(1, _displayMaxHp));
            _targetHpRatio = ratio;
            _trailHpRatio = ratio;
            _trailHoldTimer = 0f;
        }

        public void ApplyVisualSkillUsed(int energyCost, int energyGain = 15)
        {
            if (energyCost > 0)
            {
                _displayEnergy = Mathf.Max(0, _displayEnergy - energyCost);
            }
            else
            {
                _displayEnergy = Mathf.Min(_displayMaxEnergy, _displayEnergy + energyGain);
            }
            RefreshDisplayedBarsAndLabel();
        }

        public void ApplyVisualDamage(int damageAmount)
        {
            if (damageAmount <= 0) return;
            int rem = damageAmount;
            if (_displayShield > 0)
            {
                int absorbed = Mathf.Min(_displayShield, rem);
                _displayShield -= absorbed;
                rem -= absorbed;
            }
            if (rem > 0)
            {
                _displayHp = Mathf.Max(0, _displayHp - rem);
            }
            _displayEnergy = Mathf.Min(_displayMaxEnergy, _displayEnergy + 10);
            RefreshDisplayedBarsAndLabel();
        }

        public void ApplyVisualHeal(int healAmount)
        {
            if (healAmount <= 0 || !_isAlive) return;
            _displayHp = Mathf.Min(_displayMaxHp, _displayHp + healAmount);
            RefreshDisplayedBarsAndLabel();
        }

        public void ApplyVisualShield(int shieldAmount)
        {
            if (shieldAmount <= 0 || !_isAlive) return;
            _displayShield += shieldAmount;
            RefreshDisplayedBarsAndLabel();
        }

        public void ApplyVisualStatus(string statusDescription)
        {
            if (string.IsNullOrEmpty(statusDescription)) return;
            if (statusDescription.IndexOf("Stunned and skips", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayStun = false;
            }
            else if (statusDescription.IndexOf("stun", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayStun = true;
            }
            if (statusDescription.IndexOf("mark", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayMark = true;
            }
            if (statusDescription.IndexOf("protect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                statusDescription.IndexOf("guard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayProt = true;
            }
            if (statusDescription.IndexOf("PHASE 2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayBossPhase = 2;
            }
            else if (statusDescription.IndexOf("PHASE 3", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayBossPhase = 3;
            }
            if (statusDescription.IndexOf("+15 Energy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _displayEnergy = Mathf.Min(_displayMaxEnergy, _displayEnergy + 15);
            }
            RefreshDisplayedBarsAndLabel();
        }

        public void ApplyVisualDeath()
        {
            _displayHp = 0;
            _displayShield = 0;
            _displayEnergy = 0;
            _displayStun = false;
            _displayMark = false;
            _displayProt = false;
            _isAlive = false;
            _deathFadeTimer = DeathFadeDuration;
            RefreshDisplayedBarsAndLabel();
        }

        public void SyncFromState(UnitState unit, Vector3 targetWorldPos, bool isSelected, bool isPlaybackActive = false)
        {
            if (unit == null || !_isInitialized) return;

            UnitInstanceId = unit.InstanceId;
            _lastSyncedUnit = unit;
            _isSelected = isSelected;

            if (!isPlaybackActive)
            {
                SnapshotFromUnitState(unit);
            }

            bool nowBoss = _displayBossPhase > 0 || unit.BossPhase > 0 || unit.InstanceId == "e_boss_warden";
            if (nowBoss != _isBoss)
            {
                _isBoss = nowBoss;
                float bossMultiplier = _isBoss ? 1.35f : 1.0f;

                if (_shadowRenderer != null)
                {
                    _shadowRenderer.transform.localPosition = new Vector3(0f, -0.45f * bossMultiplier, 0f);
                    _shadowRenderer.transform.localScale = new Vector3(0.92f * bossMultiplier, 0.34f * bossMultiplier, 1f);
                }

                _selectionRingRenderer.transform.localPosition = new Vector3(0f, -0.43f * bossMultiplier, 0f);
                _baseRingScale = new Vector3(1.16f * bossMultiplier, 0.48f * bossMultiplier, 1f);

                if (!_usesCustomSprite)
                {
                    Sprite bossSprite = ProceduralUnitArtFactory.GetUnitSprite(unit.Role, unit.Team, _isBoss);
                    _bodyRenderer.sprite = bossSprite;
                    float targetSize = 1.04f * bossMultiplier;
                    if (bossSprite != null && bossSprite.bounds.size.x > 0.001f && bossSprite.bounds.size.y > 0.001f)
                    {
                        float maxDim = Mathf.Max(bossSprite.bounds.size.x, bossSprite.bounds.size.y);
                        float uniformScale = targetSize / maxDim;
                        _baseBodyScale = new Vector3(uniformScale, uniformScale, 1f);
                    }
                    else
                    {
                        _baseBodyScale = new Vector3(targetSize, targetSize, 1f);
                    }
                }

                float topBodyEdge = 0.52f * bossMultiplier;
                _energyBarY = topBodyEdge + 0.045f;
                _shieldBarY = _energyBarY + 0.055f;
                _hpBarY = _shieldBarY + 0.075f;
                _energyBgRenderer.transform.localPosition = new Vector3(0f, _energyBarY, 0f);
                _shieldBgRenderer.transform.localPosition = new Vector3(0f, _shieldBarY, 0f);
                _hpBgRenderer.transform.localPosition = new Vector3(0f, _hpBarY, 0f);
            }

            if (!_hasInitialPosition || Vector3.Distance(_baseWorldPos, targetWorldPos) > 4.0f)
            {
                _baseWorldPos = targetWorldPos;
                transform.position = targetWorldPos;
                _hasInitialPosition = true;
            }
            else
            {
                _baseWorldPos = Vector3.Lerp(_baseWorldPos, targetWorldPos, Time.deltaTime * 18f);
            }

            RefreshDisplayedBarsAndLabel();
            ApplyProceduralVisuals();
        }

        private void RefreshDisplayedBarsAndLabel()
        {
            if (!_isInitialized) return;

            float hpRatio = Mathf.Clamp01((float)_displayHp / Mathf.Max(1, _displayMaxHp));
            float shieldRatio = Mathf.Clamp01((float)_displayShield / Mathf.Max(1, _displayMaxHp));
            float energyRatio = Mathf.Clamp01((float)_displayEnergy / Mathf.Max(1, _displayMaxEnergy));

            if (hpRatio < _targetHpRatio)
            {
                _trailHpRatio = Mathf.Max(_trailHpRatio, _targetHpRatio);
                _trailHoldTimer = TrailHoldDuration;
            }
            else if (hpRatio > _trailHpRatio)
            {
                _trailHpRatio = hpRatio;
                _trailHoldTimer = 0f;
            }
            _targetHpRatio = hpRatio;

            if (_hpTrailRenderer != null)
            {
                UpdateBarFill(_hpTrailRenderer, _hpBarY, HpBarHeight, _trailHpRatio);
            }
            UpdateBarFill(_hpFillRenderer, _hpBarY, HpBarHeight, hpRatio);
            UpdateBarFill(_shieldFillRenderer, _shieldBarY, ShieldBarHeight, shieldRatio);
            UpdateBarFill(_energyFillRenderer, _energyBarY, EnergyBarHeight, energyRatio);

            if (_lastSyncedUnit != null)
            {
                UpdateCachedLabel(_lastSyncedUnit);
            }
        }

        public void SetActingTurnHighlight(bool isActing)
        {
            _isActingUnit = isActing;
            if (_isInitialized)
            {
                ApplyProceduralVisuals();
            }
        }

        public void PlayAttackLunge(Vector3 targetWorldPos, float duration = 0.36f)
        {
            Vector3 origin = _hasInitialPosition ? _baseWorldPos : transform.position;
            Vector3 delta = targetWorldPos - origin;
            delta.z = 0f;

            Vector3 lungeVec = delta * 0.42f;
            if (lungeVec.magnitude > 1.8f)
            {
                lungeVec = lungeVec.normalized * 1.8f;
            }

            _lungeOffsetTarget = lungeVec;
            _lungeDuration = Mathf.Max(0.05f, duration);
            _lungeTimer = _lungeDuration;
        }

        public void PlayHitShake(float duration = 0.30f, float magnitude = 0.16f)
        {
            _shakeDuration = Mathf.Max(0.05f, duration);
            _shakeTimer = _shakeDuration;
            _shakeMagnitude = Mathf.Max(0f, magnitude);

            _flashColor = new Color(1.0f, 0.40f, 0.40f, 1.0f);
            _flashDuration = Mathf.Max(0.05f, duration * 0.85f);
            _flashTimer = _flashDuration;
        }

        public void PlayInterceptSlide(Vector3 protectedAllyWorldPos, float duration = 0.48f)
        {
            float frontSign = protectedAllyWorldPos.x <= 0f ? 1f : -1f;
            Vector3 guardSpot = protectedAllyWorldPos + new Vector3(frontSign * 0.55f, 0f, 0f);
            guardSpot.z = _hasInitialPosition ? _baseWorldPos.z : transform.position.z;

            _interceptWorldPos = guardSpot;
            _interceptDuration = Mathf.Max(0.05f, duration);
            _interceptTimer = _interceptDuration;
        }

        public void PlayPulseEffect(Color flashColor, float duration = 0.35f, float peakScale = 1.22f)
        {
            _pulseDuration = Mathf.Max(0.05f, duration);
            _pulseTimer = _pulseDuration;
            _pulsePeakScale = Mathf.Max(1.0f, peakScale);

            _flashColor = flashColor;
            _flashDuration = _pulseDuration;
            _flashTimer = _flashDuration;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_lungeTimer > 0f) _lungeTimer = Mathf.Max(0f, _lungeTimer - dt);
            if (_interceptTimer > 0f) _interceptTimer = Mathf.Max(0f, _interceptTimer - dt);
            if (_shakeTimer > 0f) _shakeTimer = Mathf.Max(0f, _shakeTimer - dt);
            if (_pulseTimer > 0f) _pulseTimer = Mathf.Max(0f, _pulseTimer - dt);
            if (_flashTimer > 0f) _flashTimer = Mathf.Max(0f, _flashTimer - dt);
            if (_deathFadeTimer > 0f) _deathFadeTimer = Mathf.Max(0f, _deathFadeTimer - dt);

            if (_trailHpRatio > _targetHpRatio)
            {
                if (_trailHoldTimer > 0f)
                {
                    _trailHoldTimer -= dt;
                }
                else
                {
                    _trailHpRatio = Mathf.MoveTowards(_trailHpRatio, _targetHpRatio, dt * TrailShrinkSpeed);
                    if (_hpTrailRenderer != null)
                    {
                        UpdateBarFill(_hpTrailRenderer, _hpBarY, HpBarHeight, _trailHpRatio);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized) return;
            ApplyProceduralVisuals();
        }

        private void ApplyProceduralVisuals()
        {
            bool isDeadAndFadedOut = !_isAlive && _deathFadeTimer <= 0f;
            if (isDeadAndFadedOut)
            {
                if (_shadowRenderer != null) _shadowRenderer.enabled = false;
                _bodyRenderer.enabled = false;
                _selectionRingRenderer.enabled = false;
                _hpBgRenderer.enabled = false;
                if (_hpTrailRenderer != null) _hpTrailRenderer.enabled = false;
                _hpFillRenderer.enabled = false;
                _shieldBgRenderer.enabled = false;
                _shieldFillRenderer.enabled = false;
                _energyBgRenderer.enabled = false;
                _energyFillRenderer.enabled = false;
                if (_collider != null) _collider.enabled = false;
                return;
            }

            if (_shadowRenderer != null) _shadowRenderer.enabled = true;
            _bodyRenderer.enabled = true;
            _hpBgRenderer.enabled = true;
            _shieldBgRenderer.enabled = true;
            _energyBgRenderer.enabled = true;
            if (_collider != null) _collider.enabled = _isAlive;

            Vector3 offset = Vector3.zero;

            // 1. Attack Lunge
            if (_lungeTimer > 0f && _lungeDuration > 0f)
            {
                float t = 1f - (_lungeTimer / _lungeDuration);
                offset += _lungeOffsetTarget * Mathf.Sin(t * Mathf.PI);
            }

            // 2. Guardian Interception Slide
            if (_interceptTimer > 0f && _interceptDuration > 0f)
            {
                float t = 1f - (_interceptTimer / _interceptDuration);
                float slideFactor;
                if (t < 0.25f)
                {
                    slideFactor = Mathf.SmoothStep(0f, 1f, t / 0.25f);
                }
                else if (t <= 0.65f)
                {
                    slideFactor = 1f;
                }
                else
                {
                    slideFactor = Mathf.SmoothStep(1f, 0f, (t - 0.65f) / 0.35f);
                }

                Vector3 interceptDelta = _interceptWorldPos - _baseWorldPos;
                interceptDelta.z = 0f;
                offset += interceptDelta * slideFactor;
            }

            // 3. Hit Shake
            if (_shakeTimer > 0f && _shakeDuration > 0f)
            {
                float damping = _shakeTimer / _shakeDuration;
                float phase = Time.time * 58f;
                offset.x += Mathf.Sin(phase) * _shakeMagnitude * damping;
                offset.y += Mathf.Cos(phase * 0.85f) * (_shakeMagnitude * 0.65f) * damping;
            }

            if (_hasInitialPosition)
            {
                transform.position = _baseWorldPos + offset;
            }

            // 4. Scale Pulse
            float scaleFactor = 1.0f;
            if (_pulseTimer > 0f && _pulseDuration > 0f)
            {
                float t = 1f - (_pulseTimer / _pulseDuration);
                scaleFactor = Mathf.Lerp(1.0f, _pulsePeakScale, Mathf.Sin(t * Mathf.PI));
            }
            _bodyRenderer.transform.localScale = _baseBodyScale * scaleFactor;

            // 5. Color Flash & Death Fade Alpha
            Color baseColor = _isAlive ? _aliveBodyColor : new Color(0.25f, 0.25f, 0.25f, 0.85f);
            if (_flashTimer > 0f && _flashDuration > 0f)
            {
                float blend = _flashTimer / _flashDuration;
                baseColor = Color.Lerp(baseColor, _flashColor, blend * 0.85f);
            }

            float shadowAlpha = 0.58f;
            if (!_isAlive && _deathFadeTimer > 0f)
            {
                float alpha = _deathFadeTimer / DeathFadeDuration;
                baseColor.a *= alpha;
                shadowAlpha *= alpha;
            }
            _bodyRenderer.color = baseColor;
            if (_shadowRenderer != null)
            {
                _shadowRenderer.color = new Color(0f, 0f, 0f, shadowAlpha);
            }

            // 6. Active Turn Ring (Vàng) vs Selection Ring (Cyan)
            if (_isActingUnit)
            {
                _selectionRingRenderer.enabled = true;
                _selectionRingRenderer.color = new Color(1.0f, 0.85f, 0.15f, 0.98f);
                float ringPulse = 1.0f + 0.07f * Mathf.Sin(Time.time * 8f);
                _selectionRingRenderer.transform.localScale = _baseRingScale * ringPulse;
            }
            else if (_isSelected)
            {
                _selectionRingRenderer.enabled = true;
                bool isEnemy = _lastSyncedUnit != null && _lastSyncedUnit.Team == TeamSide.Enemy;
                _selectionRingRenderer.color = isEnemy
                    ? new Color(1.0f, 0.36f, 0.28f, 0.92f)
                    : new Color(0.25f, 1.0f, 0.9f, 0.90f);
                _selectionRingRenderer.transform.localScale = _baseRingScale;
            }
            else
            {
                _selectionRingRenderer.enabled = false;
            }
        }

        private void OnMouseDown()
        {
            if (!IsVisibleOnBattlefield || string.IsNullOrEmpty(UnitInstanceId)) return;
            OnUnitClicked?.Invoke(UnitInstanceId);
        }

        private void OnGUI()
        {
            if (!IsVisibleOnBattlefield || SuppressOverheadGui || (!_isAlive && _deathFadeTimer <= 0f) || string.IsNullOrEmpty(_cachedLabelText)) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldAnchor = transform.position + Vector3.up * (_hpBarY + 0.03f);
            Vector3 screenPos = cam.WorldToScreenPoint(worldAnchor);
            if (screenPos.z <= 0f) return;

            EnsureGuiStyles();

            int targetFontSize = _isBoss ? 11 : 10;
            _overheadStyle.fontSize = targetFontSize;
            _overheadShadowStyle.fontSize = targetFontSize;

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            const float labelWidth = 172f;
            float labelHeight = _isBoss ? 22f : 32f;
            Rect rect = new Rect(guiX - labelWidth * 0.5f, guiY - labelHeight, labelWidth, labelHeight);
            Rect shadowRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);

            // Vẽ bóng mờ phía sau giúp chữ sắc nét trên mọi màu nền sàn đấu
            GUI.Label(shadowRect, _cachedShadowText, _overheadShadowStyle);
            GUI.Label(rect, _cachedLabelText, _overheadStyle);
        }

        private SpriteRenderer CreateChildRenderer(string childName, int sortingOrder)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private void SetupBarSprites(Sprite squareSprite, TeamSide team)
        {
            // HP Bar
            _hpBgRenderer.sprite = squareSprite;
            _hpBgRenderer.color = new Color(0.08f, 0.08f, 0.10f, 0.90f);
            _hpBgRenderer.transform.localPosition = new Vector3(0f, _hpBarY, 0f);
            _hpBgRenderer.transform.localScale = new Vector3(BarWidth + 0.04f, HpBarHeight + 0.03f, 1f);

            if (_hpTrailRenderer != null)
            {
                _hpTrailRenderer.sprite = squareSprite;
                _hpTrailRenderer.color = new Color(1.0f, 0.82f, 0.38f, 0.92f);
            }

            _hpFillRenderer.sprite = squareSprite;
            _hpFillRenderer.color = team == TeamSide.Player
                ? new Color(0.22f, 0.88f, 0.38f, 1f)
                : new Color(0.92f, 0.25f, 0.25f, 1f);

            // Shield Bar
            _shieldBgRenderer.sprite = squareSprite;
            _shieldBgRenderer.color = new Color(0.08f, 0.10f, 0.14f, 0.85f);
            _shieldBgRenderer.transform.localPosition = new Vector3(0f, _shieldBarY, 0f);
            _shieldBgRenderer.transform.localScale = new Vector3(BarWidth + 0.04f, ShieldBarHeight + 0.02f, 1f);

            _shieldFillRenderer.sprite = squareSprite;
            _shieldFillRenderer.color = new Color(0.30f, 0.85f, 1.00f, 1f);

            // Energy Bar
            _energyBgRenderer.sprite = squareSprite;
            _energyBgRenderer.color = new Color(0.10f, 0.09f, 0.05f, 0.85f);
            _energyBgRenderer.transform.localPosition = new Vector3(0f, _energyBarY, 0f);
            _energyBgRenderer.transform.localScale = new Vector3(BarWidth + 0.04f, EnergyBarHeight + 0.02f, 1f);

            _energyFillRenderer.sprite = squareSprite;
            _energyFillRenderer.color = new Color(1.00f, 0.82f, 0.15f, 1f);
        }

        private static void UpdateBarFill(SpriteRenderer fillRenderer, float yPos, float height, float ratio)
        {
            float clampedRatio = Mathf.Clamp01(ratio);
            float fillWidth = BarWidth * clampedRatio;
            float offsetX = -BarWidth * (1f - clampedRatio) * 0.5f;

            fillRenderer.transform.localScale = new Vector3(fillWidth, height, 1f);
            fillRenderer.transform.localPosition = new Vector3(offsetX, yPos, 0f);
            fillRenderer.enabled = clampedRatio > 0.001f;
        }

        private void UpdateCachedLabel(UnitState unit)
        {
            if (_lastHp == _displayHp &&
                _lastMaxHp == _displayMaxHp &&
                _lastShield == _displayShield &&
                _lastStun == _displayStun &&
                _lastMark == _displayMark &&
                _lastProt == _displayProt &&
                _lastVip == _displayVip &&
                _lastBossPhase == _displayBossPhase &&
                _lastAffix == unit.Affix &&
                _lastName == unit.Name &&
                _lastIsBoss == _isBoss)
            {
                return;
            }

            _lastHp = _displayHp;
            _lastMaxHp = _displayMaxHp;
            _lastShield = _displayShield;
            _lastStun = _displayStun;
            _lastMark = _displayMark;
            _lastProt = _displayProt;
            _lastVip = _displayVip;
            _lastBossPhase = _displayBossPhase;
            _lastAffix = unit.Affix;
            _lastName = unit.Name;
            _lastIsBoss = _isBoss;

            _labelSb.Length = 0;

            if (_isBoss)
            {
                // Boss: 1 dòng súc tích (Top-Center Boss Bar đã hiển thị Phase/HP chi tiết)
                if (!_isAlive)
                {
                    _labelSb.Append("<color=#888888>★ ").Append(unit.Name).Append(" (DEAD)</color>");
                }
                else
                {
                    _labelSb.Append("<color=#FFB8B8><b>★ ").Append(unit.Name)
                            .Append(" (").Append(_displayHp).Append('/').Append(_displayMaxHp).Append(")</b></color>");
                }
            }
            else
            {
                // Đơn vị thường: Dòng 1 Tên + Máu (gọn 1 dòng), Dòng 2 chỉ hiện huy hiệu khi có trạng thái/Affix
                string colorHex = !_isAlive
                    ? "#888888"
                    : (unit.Team == TeamSide.Player ? "#B8F2FF" : "#FFB8B8");

                _labelSb.Append("<color=").Append(colorHex).Append(">");
                if (_isAlive) _labelSb.Append("<b>");
                _labelSb.Append(unit.Name);
                if (!_isAlive)
                {
                    _labelSb.Append(" (DEAD)");
                }
                else
                {
                    _labelSb.Append(" (").Append(_displayHp).Append('/').Append(_displayMaxHp).Append(')');
                }
                if (_isAlive) _labelSb.Append("</b>");
                _labelSb.Append("</color>");

                bool hasAnyBadge = _displayVip || _displayStun || _displayMark || _displayProt || unit.Affix != EnemyAffixType.None;
                if (hasAnyBadge)
                {
                    _labelSb.Append("\n<size=9>");
                    if (_displayVip) _labelSb.Append("<color=#FFD700>[VIP]</color> ");
                    if (_displayStun) _labelSb.Append("<color=yellow>[STUN]</color> ");
                    if (_displayMark) _labelSb.Append("<color=red>[MARK]</color> ");
                    if (_displayProt) _labelSb.Append("<color=lime>[PROT]</color> ");
                    if (unit.Affix != EnemyAffixType.None) _labelSb.Append("<color=magenta>[").Append(unit.Affix).Append("]</color>");
                    _labelSb.Append("</size>");
                }
            }

            _cachedLabelText = _labelSb.ToString().TrimEnd();
            _cachedShadowText = StripColorTagsPreserveFormatting(_cachedLabelText);
        }

        private static string GetRoleShortTag(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Guardian: return "[G]";
                case UnitRole.Vanguard: return "[V]";
                case UnitRole.Ranger: return "[R]";
                case UnitRole.Mage: return "[M]";
                case UnitRole.Medic: return "[Md]";
                default: return "[?]";
            }
        }

        private static void EnsureGuiStyles()
        {
            if (_overheadStyle != null) return;

            Font uiFont = UguiThemeFactory.GetDefaultFont();

            _overheadStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.LowerCenter,
                richText = true,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            _overheadShadowStyle = new GUIStyle(_overheadStyle)
            {
                font = uiFont,
                richText = true,
                normal = { textColor = new Color(0f, 0f, 0f, 0.85f) }
            };
        }

        private static string StripColorTagsPreserveFormatting(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            _stripSb.Length = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '<' && i + 7 < input.Length)
                {
                    bool isColorOpen =
                        (input[i + 1] == 'c' || input[i + 1] == 'C') &&
                        (input[i + 2] == 'o' || input[i + 2] == 'O') &&
                        (input[i + 3] == 'l' || input[i + 3] == 'L') &&
                        (input[i + 4] == 'o' || input[i + 4] == 'O') &&
                        (input[i + 5] == 'r' || input[i + 5] == 'R') &&
                        input[i + 6] == '=';

                    bool isColorClose =
                        input[i + 1] == '/' &&
                        (input[i + 2] == 'c' || input[i + 2] == 'C') &&
                        (input[i + 3] == 'o' || input[i + 3] == 'O') &&
                        (input[i + 4] == 'l' || input[i + 4] == 'L') &&
                        (input[i + 5] == 'o' || input[i + 5] == 'O') &&
                        (input[i + 6] == 'r' || input[i + 6] == 'R') &&
                        input[i + 7] == '>';

                    if (isColorOpen || isColorClose)
                    {
                        while (i < input.Length && input[i] != '>')
                        {
                            i++;
                        }
                        continue;
                    }
                }
                _stripSb.Append(c);
            }
            return _stripSb.ToString();
        }
    }
}

