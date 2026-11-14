using System;
using System.Text;
using CrownlessWarband.Core.Battle;
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
        public event Action<string> OnUnitClicked;

        private SpriteRenderer _selectionRingRenderer;
        private SpriteRenderer _bodyRenderer;

        private SpriteRenderer _hpBgRenderer;
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

        // Cache dữ liệu hiển thị OnGUI để tránh cấp phát chuỗi mỗi frame
        private string _cachedLabelText = string.Empty;
        private string _cachedShadowText = string.Empty;
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private int _lastShield = -1;
        private bool _lastStun;
        private bool _lastMark;
        private bool _lastProt;
        private int _lastBossPhase = -1;
        private EnemyAffixType _lastAffix = EnemyAffixType.None;
        private string _lastName;

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
                _selectionRingRenderer = CreateChildRenderer("SelectionRing", 1);
                _bodyRenderer = CreateChildRenderer("BodySprite", 2);

                _hpBgRenderer = CreateChildRenderer("HpBarBg", 4);
                _hpFillRenderer = CreateChildRenderer("HpBarFill", 5);

                _shieldBgRenderer = CreateChildRenderer("ShieldBarBg", 4);
                _shieldFillRenderer = CreateChildRenderer("ShieldBarFill", 5);

                _energyBgRenderer = CreateChildRenderer("EnergyBarBg", 4);
                _energyFillRenderer = CreateChildRenderer("EnergyBarFill", 5);

                _isInitialized = true;
            }

            _usesCustomSprite = customRoleSprite != null;
            _aliveBodyColor = _usesCustomSprite ? Color.white : ResolveRoleColor(unit.Role, unit.Team);

            _selectionRingRenderer.sprite = defaultSquareSprite;
            _selectionRingRenderer.color = new Color(0.25f, 1.0f, 0.9f, 0.9f);
            _selectionRingRenderer.transform.localPosition = Vector3.zero;
            _baseRingScale = new Vector3(0.84f * bossMultiplier, 1.00f * bossMultiplier, 1f);
            _selectionRingRenderer.transform.localScale = _baseRingScale;
            _selectionRingRenderer.enabled = false;

            _bodyRenderer.sprite = _usesCustomSprite ? customRoleSprite : defaultSquareSprite;
            _bodyRenderer.transform.localPosition = Vector3.zero;
            if (_usesCustomSprite && customRoleSprite.bounds.size.x > 0.001f && customRoleSprite.bounds.size.y > 0.001f)
            {
                float targetSize = 0.88f * bossMultiplier;
                float maxDim = Mathf.Max(customRoleSprite.bounds.size.x, customRoleSprite.bounds.size.y);
                float uniformScale = targetSize / maxDim;
                _baseBodyScale = new Vector3(uniformScale, uniformScale, 1f);
            }
            else
            {
                _baseBodyScale = new Vector3(0.70f * bossMultiplier, 0.86f * bossMultiplier, 1f);
            }
            _bodyRenderer.transform.localScale = _baseBodyScale;

            // Sắp xếp vị trí 3 thanh chỉ số phía trên thân nhân vật
            float topBodyEdge = 0.43f * bossMultiplier;
            _energyBarY = topBodyEdge + 0.09f;
            _shieldBarY = _energyBarY + 0.08f;
            _hpBarY = _shieldBarY + 0.10f;

            SetupBarSprites(defaultSquareSprite, unit.Team);

            _hasInitialPosition = false;
            _isAlive = unit.IsAlive;
            _isSelected = false;
            _isActingUnit = false;
            _lungeTimer = 0f;
            _interceptTimer = 0f;
            _shakeTimer = 0f;
            _pulseTimer = 0f;
            _flashTimer = 0f;
            _lastHp = -1; // Ép dựng lại overhead label lần đầu
        }

        public void SyncFromState(UnitState unit, Vector3 targetWorldPos, bool isSelected)
        {
            if (unit == null || !_isInitialized) return;

            UnitInstanceId = unit.InstanceId;
            _isAlive = unit.IsAlive;
            _isSelected = isSelected;

            bool nowBoss = unit.BossPhase > 0 || unit.InstanceId == "e_boss_warden";
            if (nowBoss != _isBoss)
            {
                _isBoss = nowBoss;
                float bossMultiplier = _isBoss ? 1.35f : 1.0f;
                if (!_usesCustomSprite)
                {
                    _baseBodyScale = new Vector3(0.70f * bossMultiplier, 0.86f * bossMultiplier, 1f);
                }
                _baseRingScale = new Vector3(0.84f * bossMultiplier, 1.00f * bossMultiplier, 1f);
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

            float hpRatio = Mathf.Clamp01(unit.HPPercentage);
            float shieldRatio = unit.MaxHP > 0 ? Mathf.Clamp01(unit.Shield / (float)unit.MaxHP) : 0f;
            float energyRatio = unit.MaxEnergy > 0 ? Mathf.Clamp01(unit.CurrentEnergy / (float)unit.MaxEnergy) : 0f;

            UpdateBarFill(_hpFillRenderer, _hpBarY, HpBarHeight, hpRatio);
            UpdateBarFill(_shieldFillRenderer, _shieldBarY, ShieldBarHeight, shieldRatio);
            UpdateBarFill(_energyFillRenderer, _energyBarY, EnergyBarHeight, energyRatio);

            UpdateCachedLabel(unit);
            ApplyProceduralVisuals();
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
        }

        private void LateUpdate()
        {
            if (!_isInitialized) return;
            ApplyProceduralVisuals();
        }

        private void ApplyProceduralVisuals()
        {
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

            // 5. Color Flash
            Color baseColor = _isAlive ? _aliveBodyColor : new Color(0.25f, 0.25f, 0.25f, 0.45f);
            if (_flashTimer > 0f && _flashDuration > 0f)
            {
                float blend = _flashTimer / _flashDuration;
                _bodyRenderer.color = Color.Lerp(baseColor, _flashColor, blend * 0.85f);
            }
            else
            {
                _bodyRenderer.color = baseColor;
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
                _selectionRingRenderer.color = new Color(0.25f, 1.0f, 0.9f, 0.90f);
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
            if (!IsVisibleOnBattlefield || string.IsNullOrEmpty(_cachedLabelText)) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldAnchor = transform.position + Vector3.up * 0.85f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldAnchor);
            if (screenPos.z <= 0f) return;

            EnsureGuiStyles();

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            const float labelWidth = 176f;
            const float labelHeight = 52f;
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
            bool hasProt = !string.IsNullOrEmpty(unit.ProtectingAllyId);
            if (_lastHp == unit.CurrentHP &&
                _lastMaxHp == unit.MaxHP &&
                _lastShield == unit.Shield &&
                _lastStun == unit.IsStunned &&
                _lastMark == unit.IsMarked &&
                _lastProt == hasProt &&
                _lastBossPhase == unit.BossPhase &&
                _lastAffix == unit.Affix &&
                _lastName == unit.Name)
            {
                return;
            }

            _lastHp = unit.CurrentHP;
            _lastMaxHp = unit.MaxHP;
            _lastShield = unit.Shield;
            _lastStun = unit.IsStunned;
            _lastMark = unit.IsMarked;
            _lastProt = hasProt;
            _lastBossPhase = unit.BossPhase;
            _lastAffix = unit.Affix;
            _lastName = unit.Name;

            _labelSb.Length = 0;

            // Dòng 1: Tên + Tag Role
            string roleTag = GetRoleShortTag(unit.Role);
            if (!unit.IsAlive)
            {
                _labelSb.Append("<color=#888888>").Append(unit.Name).Append(' ').Append(roleTag).Append("</color>\n");
            }
            else if (unit.Team == TeamSide.Player)
            {
                _labelSb.Append("<color=#B8F2FF><b>").Append(unit.Name).Append(' ').Append(roleTag).Append("</b></color>\n");
            }
            else
            {
                _labelSb.Append("<color=#FFB8B8><b>").Append(unit.Name).Append(' ').Append(roleTag).Append("</b></color>\n");
            }

            // Dòng 2: HP & Shield
            if (!unit.IsAlive)
            {
                _labelSb.Append("<color=#888888>DEAD</color>");
            }
            else
            {
                _labelSb.Append(unit.CurrentHP).Append('/').Append(unit.MaxHP);
                if (unit.Shield > 0)
                {
                    _labelSb.Append(" <color=#66E0FF>(+").Append(unit.Shield).Append(")</color>");
                }
            }

            // Dòng 3: Huy hiệu trạng thái nổi bật
            bool hasAnyBadge = unit.IsStunned || unit.IsMarked || hasProt || unit.BossPhase > 0 || unit.Affix != EnemyAffixType.None;
            if (hasAnyBadge)
            {
                _labelSb.Append('\n');
                if (unit.IsStunned) _labelSb.Append("<color=yellow>[STUN]</color> ");
                if (unit.IsMarked) _labelSb.Append("<color=red>[MARK]</color> ");
                if (hasProt) _labelSb.Append("<color=lime>[PROT]</color> ");
                if (unit.BossPhase > 0) _labelSb.Append("<color=orange>[PHASE ").Append(unit.BossPhase).Append("]</color> ");
                if (unit.Affix != EnemyAffixType.None) _labelSb.Append("<color=magenta>[").Append(unit.Affix).Append("]</color>");
            }

            _cachedLabelText = _labelSb.ToString().TrimEnd();
            _cachedShadowText = StripRichTextTags(_cachedLabelText);
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

        private static Color ResolveRoleColor(UnitRole role, TeamSide team)
        {
            if (team == TeamSide.Player)
            {
                switch (role)
                {
                    case UnitRole.Guardian: return new Color(0.20f, 0.75f, 0.95f, 1f);
                    case UnitRole.Vanguard: return new Color(0.20f, 0.85f, 0.55f, 1f);
                    case UnitRole.Ranger: return new Color(0.65f, 0.90f, 0.25f, 1f);
                    case UnitRole.Mage: return new Color(0.60f, 0.40f, 0.95f, 1f);
                    case UnitRole.Medic: return new Color(0.35f, 0.95f, 0.65f, 1f);
                }
            }
            else
            {
                switch (role)
                {
                    case UnitRole.Guardian: return new Color(0.75f, 0.25f, 0.25f, 1f);
                    case UnitRole.Vanguard: return new Color(0.90f, 0.35f, 0.20f, 1f);
                    case UnitRole.Ranger: return new Color(0.85f, 0.20f, 0.55f, 1f);
                    case UnitRole.Mage: return new Color(0.75f, 0.20f, 0.85f, 1f);
                    case UnitRole.Medic: return new Color(0.90f, 0.45f, 0.55f, 1f);
                }
            }

            return Color.white;
        }

        private static void EnsureGuiStyles()
        {
            if (_overheadStyle != null) return;

            _overheadStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.LowerCenter,
                richText = true,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            _overheadShadowStyle = new GUIStyle(_overheadStyle)
            {
                richText = false,
                normal = { textColor = new Color(0f, 0f, 0f, 0.85f) }
            };
        }

        private static string StripRichTextTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            _stripSb.Length = 0;
            bool insideTag = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '<')
                {
                    insideTag = true;
                    continue;
                }
                if (c == '>')
                {
                    insideTag = false;
                    continue;
                }
                if (!insideTag)
                {
                    _stripSb.Append(c);
                }
            }
            return _stripSb.ToString();
        }
    }
}

