using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Events;
using CrownlessWarband.Presentation.UI;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    public enum PlaybackSpeedMode
    {
        Speed1x = 0,
        Speed2x = 1,
        Speed4x = 2,
        Instant = 3
    }

    /// <summary>
    /// Bộ điều phối tiêu thụ BattleState.EventQueue theo thời gian thực (GDD Sections 10, 43, 44, 53).
    /// Kích hoạt diễn hoạt Procedural Tweens trên UnitVisualView và chữ nổi chiến thuật trên FloatingTextView.
    /// </summary>
    public class CombatEventPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleSandboxController sandboxController;
        [SerializeField] private BattlefieldGridView gridView;
        [SerializeField] private FloatingTextView floatingTextView;
        [SerializeField] private BattleAudioManager audioManager;
        [SerializeField] private CombatVfxManager vfxManager;

        public PlaybackSpeedMode SpeedMode { get; set; } = PlaybackSpeedMode.Speed1x;
        public bool IsPaused { get; set; } = false;
        public bool IsPlayingEvents => _stepWaitTimer > 0f || (sandboxController != null && sandboxController.CurrentState != null && sandboxController.CurrentState.EventQueue.Count > 0);
        public string CurrentActingUnitId { get; private set; }
        public string LastPresentedEventSummary { get; private set; } = "Ready";
        public event Action<string> OnActingUnitChanged;
        public event Action<CombatEvent> OnCombatEventPresented;

        private BattleState _lastObservedState;
        private float _stepWaitTimer;
        private bool _wasInActiveBattle = true;
        private float _camShakeTimer;
        private float _camShakeDuration;
        private float _camShakeMagnitude;
        private static readonly Vector3 DefaultCameraPos = new Vector3(0f, 0f, -10f);

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            EnsureReferences();
            if (sandboxController == null || sandboxController.CurrentState == null)
            {
                ResetCameraShake();
                return;
            }

            BattleState currentState = sandboxController.CurrentState;

            // Phát hiện Reset trận đấu hoặc sang trận mới -> xóa hiệu ứng cũ và xả event khởi tạo Turn 1 Planning
            if (_lastObservedState != currentState)
            {
                _lastObservedState = currentState;
                _stepWaitTimer = 0f;
                SetActingUnitHighlight(null);
                LastPresentedEventSummary = "Battle Initialized";

                if (floatingTextView != null)
                {
                    floatingTextView.ClearAll();
                }
                vfxManager?.ClearAll();
                ResetCameraShake();

                DrainInitialPlanningBootEvents(currentState);
                return;
            }

            if (sandboxController.ScreenMode != SandboxScreenMode.ActiveBattle)
            {
                if (_wasInActiveBattle)
                {
                    _wasInActiveBattle = false;
                    _stepWaitTimer = 0f;
                    SetActingUnitHighlight(null);
                    if (floatingTextView != null)
                    {
                        floatingTextView.ClearAll();
                    }
                    vfxManager?.ClearAll();
                    ResetCameraShake();
                }
                return;
            }
            _wasInActiveBattle = true;

            if (SpeedMode == PlaybackSpeedMode.Instant)
            {
                if (currentState.EventQueue.Count > 0 || _stepWaitTimer > 0f)
                {
                    SkipAllPendingEvents();
                }
                ResetCameraShake();
                return;
            }

            if (IsPaused)
            {
                ResetCameraShake();
                return;
            }

            UpdateCameraShake(Time.deltaTime);

            if (_stepWaitTimer > 0f)
            {
                _stepWaitTimer -= Time.deltaTime;
                if (_stepWaitTimer > 0f) return;
            }

            if (currentState.EventQueue.Count > 0)
            {
                CombatEvent nextEvt = currentState.EventQueue.Dequeue();
                float baseDelay = PresentNextEvent(nextEvt, currentState);
                _stepWaitTimer = baseDelay / GetSpeedMultiplier();
            }
            else if (!string.IsNullOrEmpty(CurrentActingUnitId))
            {
                SetActingUnitHighlight(null);
            }
        }

        public void TriggerCameraShake(float duration, float magnitude)
        {
            float clampedMag = Mathf.Clamp(magnitude, 0f, 0.11f);
            if (clampedMag <= 0f) return;

            if (clampedMag >= _camShakeMagnitude || _camShakeTimer <= 0f)
            {
                _camShakeDuration = Mathf.Max(0.05f, duration);
                _camShakeTimer = _camShakeDuration;
                _camShakeMagnitude = clampedMag;
            }
        }

        private void UpdateCameraShake(float dt)
        {
            if (_camShakeTimer <= 0f) return;

            _camShakeTimer -= dt;
            Camera cam = Camera.main;
            if (cam == null) return;

            if (_camShakeTimer <= 0f)
            {
                _camShakeTimer = 0f;
                _camShakeMagnitude = 0f;
                cam.transform.position = DefaultCameraPos;
                return;
            }

            float damp = _camShakeDuration > 0.0001f ? (_camShakeTimer / _camShakeDuration) : 0f;
            float offsetX = Mathf.Sin(Time.unscaledTime * 68f) * _camShakeMagnitude * damp;
            float offsetY = Mathf.Cos(Time.unscaledTime * 54f) * _camShakeMagnitude * 0.7f * damp;
            cam.transform.position = DefaultCameraPos + new Vector3(offsetX, offsetY, 0f);
        }

        private void ResetCameraShake()
        {
            if (_camShakeTimer <= 0f && _camShakeMagnitude <= 0f) return;

            _camShakeTimer = 0f;
            _camShakeMagnitude = 0f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = DefaultCameraPos;
            }
        }

        public void CapturePreTurnSnapshot(BattleState state)
        {
            EnsureReferences();
            if (gridView != null && state != null)
            {
                gridView.RefreshBattlefield(state);
                gridView.SnapshotAllUnitsForPlayback(state);
            }
        }

        public void StepSingleEvent()
        {
            EnsureReferences();
            if (sandboxController == null || sandboxController.CurrentState == null) return;

            BattleState currentState = sandboxController.CurrentState;
            if (_lastObservedState != currentState)
            {
                _lastObservedState = currentState;
            }

            if (currentState.EventQueue.Count == 0)
            {
                _stepWaitTimer = 0f;
                SetActingUnitHighlight(null);
                return;
            }

            CombatEvent nextEvt = currentState.EventQueue.Dequeue();
            float baseDelay = PresentNextEvent(nextEvt, currentState);
            _stepWaitTimer = baseDelay / GetSpeedMultiplier();
        }

        public void SkipAllPendingEvents()
        {
            EnsureReferences();
            if (sandboxController == null || sandboxController.CurrentState == null) return;

            BattleState currentState = sandboxController.CurrentState;
            Queue<CombatEvent> queue = currentState.EventQueue;

            while (queue.Count > 0)
            {
                CombatEvent evt = queue.Dequeue();
                PresentNextEvent(evt, currentState);
            }

            _stepWaitTimer = 0f;
            SetActingUnitHighlight(null);
            vfxManager?.ClearAll();
            ResetCameraShake();
        }

        private float PresentNextEvent(CombatEvent evt, BattleState state)
        {
            if (evt == null) return 0f;

            float speedMult = GetSpeedMultiplier();

            LastPresentedEventSummary = !string.IsNullOrEmpty(evt.Description)
                ? evt.Description
                : $"{evt.EventType}: {evt.SkillName}";

            OnCombatEventPresented?.Invoke(evt);
            if (audioManager != null && (SpeedMode != PlaybackSpeedMode.Instant || evt.EventType == CombatEventType.BattleEnded))
            {
                audioManager.PlayForCombatEvent(evt);
            }

            switch (evt.EventType)
            {
                case CombatEventType.TurnStarted:
                {
                    if (evt.TurnNumber > 1 && floatingTextView != null)
                    {
                        floatingTextView.SpawnTacticalBanner($"=== TURN {evt.TurnNumber} ===", new Color(1f, 0.92f, 0.55f, 1f), 1.4f);
                    }
                    return 0.10f;
                }

                case CombatEventType.PhaseChanged:
                {
                    return 0.10f;
                }

                case CombatEventType.SquadStrategyChanged:
                {
                    if (floatingTextView != null)
                    {
                        bool isPlayerMsg = !string.IsNullOrEmpty(evt.Description) &&
                                           evt.Description.StartsWith("Player", StringComparison.OrdinalIgnoreCase);
                        Color bannerColor = isPlayerMsg
                            ? new Color(0.28f, 0.92f, 1.0f, 1f)
                            : new Color(1.0f, 0.42f, 0.25f, 1f);
                        floatingTextView.SpawnTacticalBanner($"SQUAD AI: {evt.Description}", bannerColor, 1.9f);
                    }
                    return 0.45f;
                }

                case CombatEventType.UnitTurnStarted:
                {
                    SetActingUnitHighlight(evt.SourceUnitId);
                    OnActingUnitChanged?.Invoke(CurrentActingUnitId);
                    return 0.28f;
                }

                case CombatEventType.SkillUsed:
                {
                    UnitState sourceUnit = FindUnitById(state, evt.SourceUnitId);
                    UnitState targetUnit = FindUnitById(state, evt.TargetUnitId);
                    UnitVisualView sourceView = gridView != null ? gridView.GetUnitVisual(evt.SourceUnitId) : null;
                    UnitVisualView targetView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;

                    if (sourceView != null)
                    {
                        int energyCost = 0;
                        int energyGain = 15;
                        if (sourceUnit != null && !string.IsNullOrEmpty(evt.SkillName))
                        {
                            for (int s = 0; s < sourceUnit.Skills.Count; s++)
                            {
                                var sk = sourceUnit.Skills[s];
                                if (sk != null && sk.Name == evt.SkillName)
                                {
                                    energyCost = sk.EnergyCost;
                                    energyGain = (sk.Slot == SkillSlotType.BasicAttack) ? 20 : 0;
                                    break;
                                }
                            }
                        }
                        sourceView.ApplyVisualSkillUsed(energyCost, energyGain);

                        if (sourceUnit != null && targetUnit != null && sourceUnit.Team != targetUnit.Team && targetView != null)
                        {
                            sourceView.PlayAttackLunge(targetView.transform.position);

                            if (sourceUnit.Role == UnitRole.Ranger ||
                                sourceUnit.Role == UnitRole.Mage ||
                                sourceUnit.Role == UnitRole.Medic)
                            {
                                Color projColor;
                                if (sourceUnit.Role == UnitRole.Ranger)
                                {
                                    projColor = new Color(1.0f, 0.80f, 0.20f, 1f); // #FFCC33
                                }
                                else if (sourceUnit.Role == UnitRole.Mage)
                                {
                                    projColor = new Color(0.702f, 0.40f, 1.0f, 1f); // #B366FF
                                }
                                else
                                {
                                    projColor = new Color(0.302f, 0.949f, 0.651f, 1f); // #4DF2A6
                                }
                                vfxManager?.SpawnProjectile(sourceView.transform.position, targetView.transform.position, projColor, speedMult);
                            }
                            else
                            {
                                Color slashColor = sourceUnit.Team == TeamSide.Player
                                    ? new Color(0.40f, 0.902f, 1.0f, 1f)  // #66E6FF
                                    : new Color(1.0f, 0.40f, 0.251f, 1f); // #FF6640
                                vfxManager?.SpawnSlashArc(sourceView.transform.position, targetView.transform.position, slashColor, speedMult);
                            }

                            if (IsHeavyAoESkill(evt.SkillName))
                            {
                                vfxManager?.SpawnShockwave(targetView.transform.position, new Color(1f, 0.55f, 0.20f, 1f), 2.5f, 0.40f, speedMult);
                                TriggerCameraShake(0.22f, 0.08f);
                            }
                        }
                        else
                        {
                            sourceView.PlayPulseEffect(Color.cyan);
                        }

                        SpawnOutwardFloatingText(
                            sourceView,
                            evt.SkillName,
                            new Color(1.0f, 0.92f, 0.28f, 1f),
                            yOffset: 0.16f,
                            sideOffset: 0f,
                            duration: 0.85f,
                            fontSize: 13,
                            riseSpeed: 0.28f
                        );
                    }
                    return 0.42f;
                }

                case CombatEventType.InterceptionTriggered:
                {
                    UnitVisualView guardianView = gridView != null ? gridView.GetUnitVisual(evt.SourceUnitId) : null;
                    UnitVisualView allyView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;

                    if (guardianView != null)
                    {
                        if (allyView != null)
                        {
                            guardianView.PlayInterceptSlide(allyView.transform.position);
                        }

                        vfxManager?.SpawnShieldDome(guardianView.transform.position, true, speedMult);
                        TriggerCameraShake(0.14f, 0.05f);

                        SpawnOutwardFloatingText(
                            guardianView,
                            "INTERCEPT!",
                            new Color(0.30f, 1.0f, 0.80f, 1f),
                            yOffset: 0.24f,
                            sideOffset: 0f,
                            duration: 0.95f,
                            fontSize: 15,
                            riseSpeed: 0.34f
                        );
                    }
                    return 0.48f;
                }

                case CombatEventType.DamageDealt:
                {
                    if (!string.IsNullOrEmpty(evt.TargetUnitId))
                    {
                        UnitVisualView targetView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;
                        if (targetView != null)
                        {
                            targetView.ApplyVisualDamage(evt.Value);
                            targetView.PlayHitShake();

                            bool heavyHit = evt.Value >= 40;
                            Color sparkColor = heavyHit
                                ? new Color(1f, 0.45f, 0.15f, 1f)
                                : new Color(1f, 0.85f, 0.45f, 1f);
                            vfxManager?.SpawnHitSpark(targetView.transform.position, sparkColor, heavyHit, speedMult);
                            if (heavyHit)
                            {
                                TriggerCameraShake(0.16f, 0.06f);
                            }

                            int fontSize = (evt.SkillName != null && evt.Value >= 45) ? 16 : 14;
                            SpawnOutwardFloatingText(
                                targetView,
                                $"-{evt.Value}",
                                new Color(1.0f, 0.32f, 0.25f, 1f),
                                yOffset: 0.14f,
                                sideOffset: 0.26f,
                                duration: 0.92f,
                                fontSize: fontSize,
                                riseSpeed: 0.42f
                            );
                        }
                    }
                    else
                    {
                        if (gridView != null && state != null)
                        {
                            for (int i = 0; i < state.Units.Count; i++)
                            {
                                var u = state.Units[i];
                                if (u == null || !u.IsAlive) continue;
                                UnitVisualView uView = gridView.GetUnitVisual(u.InstanceId);
                                if (uView != null)
                                {
                                    uView.ApplyVisualDamage(Math.Max(1, (int)(u.MaxHP * 0.05f)));
                                }
                            }
                        }
                        vfxManager?.SpawnShockwave(Vector3.zero, new Color(0.65f, 0.95f, 0.25f, 0.85f), 3.6f, 0.42f, speedMult);
                        if (floatingTextView != null)
                        {
                            floatingTextView.SpawnTacticalBanner("ACID RAIN (-5% HP)", new Color(0.68f, 0.95f, 0.25f, 1f), 1.6f);
                        }
                    }
                    return 0.34f;
                }

                case CombatEventType.HealApplied:
                {
                    UnitVisualView targetView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;
                    if (targetView != null)
                    {
                        targetView.ApplyVisualHeal(evt.Value);
                        targetView.PlayPulseEffect(Color.green);
                        vfxManager?.SpawnHealSparkle(targetView.transform.position, speedMult);
                        SpawnOutwardFloatingText(
                            targetView,
                            $"+{evt.Value} HP",
                            new Color(0.30f, 1.0f, 0.45f, 1f),
                            yOffset: 0.14f,
                            sideOffset: 0.26f,
                            duration: 0.92f,
                            fontSize: 14,
                            riseSpeed: 0.40f
                        );
                    }
                    return 0.30f;
                }

                case CombatEventType.ShieldApplied:
                {
                    UnitVisualView targetView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;
                    if (targetView != null)
                    {
                        targetView.ApplyVisualShield(evt.Value);
                        targetView.PlayPulseEffect(Color.cyan);
                        vfxManager?.SpawnShieldDome(targetView.transform.position, false, speedMult);
                        SpawnOutwardFloatingText(
                            targetView,
                            $"+{evt.Value} SHD",
                            new Color(0.35f, 0.90f, 1.0f, 1f),
                            yOffset: 0.14f,
                            sideOffset: 0.26f,
                            duration: 0.92f,
                            fontSize: 14,
                            riseSpeed: 0.40f
                        );
                    }
                    return 0.30f;
                }

                case CombatEventType.StatusApplied:
                {
                    string unitId = !string.IsNullOrEmpty(evt.TargetUnitId) ? evt.TargetUnitId : evt.SourceUnitId;
                    UnitVisualView unitView = gridView != null ? gridView.GetUnitVisual(unitId) : null;
                    string badgeText = ResolveStatusBadgeText(evt);
                    Color badgeColor = new Color(1.0f, 0.78f, 0.20f, 1f);

                    if (unitView != null)
                    {
                        unitView.ApplyVisualStatus(evt.Description);
                        unitView.PlayPulseEffect(badgeColor);

                        if (badgeText == "PHASE 2!" || badgeText == "PHASE 3!")
                        {
                            vfxManager?.SpawnBossPhaseBurst(unitView.transform.position, badgeText == "PHASE 3!" ? 3 : 2, speedMult);
                            TriggerCameraShake(0.28f, 0.10f);
                        }
                        else
                        {
                            vfxManager?.SpawnShockwave(unitView.transform.position, badgeColor, 1.45f, 0.30f, speedMult);
                        }

                        SpawnOutwardFloatingText(
                            unitView,
                            badgeText,
                            badgeColor,
                            yOffset: 0.22f,
                            sideOffset: 0f,
                            duration: 0.95f,
                            fontSize: 14,
                            riseSpeed: 0.34f
                        );
                    }
                    return 0.32f;
                }

                case CombatEventType.UnitDied:
                {
                    string deadUnitId = !string.IsNullOrEmpty(evt.TargetUnitId) ? evt.TargetUnitId : evt.SourceUnitId;
                    UnitVisualView deadView = gridView != null ? gridView.GetUnitVisual(deadUnitId) : null;
                    if (deadView != null)
                    {
                        deadView.ApplyVisualDeath();
                        deadView.PlayHitShake(0.40f, 0.22f);
                        vfxManager?.SpawnShockwave(deadView.transform.position, new Color(0.95f, 0.22f, 0.25f, 1f), 1.75f, 0.36f, speedMult);
                        TriggerCameraShake(0.20f, 0.07f);
                        SpawnOutwardFloatingText(
                            deadView,
                            "DEFEATED!",
                            new Color(0.95f, 0.18f, 0.25f, 1f),
                            yOffset: 0.22f,
                            sideOffset: 0f,
                            duration: 1.10f,
                            fontSize: 16,
                            riseSpeed: 0.32f
                        );
                    }
                    return 0.40f;
                }

                case CombatEventType.BattleEnded:
                {
                    SetActingUnitHighlight(null);
                    if (floatingTextView != null && !string.IsNullOrEmpty(evt.Description))
                    {
                        bool isPlayerWin = evt.Description.IndexOf("Victory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           evt.Description.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
                        Color endColor = isPlayerWin
                            ? new Color(0.35f, 1.0f, 0.45f, 1f)
                            : new Color(1.0f, 0.82f, 0.25f, 1f);
                        floatingTextView.SpawnTacticalBanner(evt.Description, endColor, 2.5f);
                    }
                    return 0.25f;
                }

                default:
                    return 0.15f;
            }
        }

        private void DrainInitialPlanningBootEvents(BattleState state)
        {
            if (state == null || state.CurrentTurn != 1 || state.CurrentPhase != TurnPhase.Planning) return;

            while (state.EventQueue.Count > 0)
            {
                CombatEvent bootEvt = state.EventQueue.Dequeue();
                if (bootEvt != null && !string.IsNullOrEmpty(bootEvt.Description))
                {
                    LastPresentedEventSummary = bootEvt.Description;
                }
            }
        }

        private void SetActingUnitHighlight(string unitInstanceId)
        {
            CurrentActingUnitId = unitInstanceId;

            if (gridView == null || _lastObservedState == null) return;

            for (int i = 0; i < _lastObservedState.Units.Count; i++)
            {
                UnitState u = _lastObservedState.Units[i];
                if (u == null) continue;

                UnitVisualView view = gridView.GetUnitVisual(u.InstanceId);
                if (view != null)
                {
                    view.SetActingTurnHighlight(!string.IsNullOrEmpty(unitInstanceId) && u.InstanceId == unitInstanceId);
                }
            }
        }

        private float GetSpeedMultiplier()
        {
            switch (SpeedMode)
            {
                case PlaybackSpeedMode.Speed2x: return 2f;
                case PlaybackSpeedMode.Speed4x: return 4f;
                case PlaybackSpeedMode.Instant: return 1000f;
                default: return 1f;
            }
        }

        private void OnDestroy()
        {
            ResetCameraShake();
        }

        private void EnsureReferences()
        {
            if (sandboxController == null)
            {
                sandboxController = FindFirstObjectByType<BattleSandboxController>();
            }

            if (gridView == null)
            {
                gridView = FindFirstObjectByType<BattlefieldGridView>();
            }

            if (floatingTextView == null)
            {
                floatingTextView = FindFirstObjectByType<FloatingTextView>();
                if (floatingTextView == null)
                {
                    floatingTextView = gameObject.AddComponent<FloatingTextView>();
                }
            }

            if (audioManager == null)
            {
                audioManager = FindFirstObjectByType<BattleAudioManager>();
                if (audioManager == null)
                {
                    audioManager = gameObject.AddComponent<BattleAudioManager>();
                }
            }

            if (vfxManager == null)
            {
                vfxManager = FindFirstObjectByType<CombatVfxManager>();
                if (vfxManager == null)
                {
                    vfxManager = gameObject.AddComponent<CombatVfxManager>();
                }
            }
        }

        private void SpawnOutwardFloatingText(
            UnitVisualView view,
            string text,
            Color color,
            float yOffset,
            float sideOffset,
            float duration,
            int fontSize,
            float riseSpeed)
        {
            if (floatingTextView == null || view == null || string.IsNullOrEmpty(text)) return;

            float sideSign = sideOffset > 0f ? (view.transform.position.x >= 0f ? sideOffset : -sideOffset) : 0f;
            Vector3 spawnPos = view.transform.position + new Vector3(sideSign, yOffset, 0f);
            floatingTextView.SpawnFloatingText(spawnPos, text, color, duration, fontSize, riseSpeed);
        }

        private static bool IsHeavyAoESkill(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return false;
            return skillName.IndexOf("Sweep", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   skillName.IndexOf("Tempest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   skillName.IndexOf("Meteor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   skillName.IndexOf("Breaker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   skillName.IndexOf("Overdrive", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static UnitState FindUnitById(BattleState state, string instanceId)
        {
            if (state == null || string.IsNullOrEmpty(instanceId)) return null;

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitState u = state.Units[i];
                if (u != null && u.InstanceId == instanceId)
                {
                    return u;
                }
            }
            return null;
        }

        private static string ResolveStatusBadgeText(CombatEvent evt)
        {
            string desc = evt.Description ?? string.Empty;
            string skill = evt.SkillName ?? string.Empty;

            if (desc.IndexOf("PHASE 2", StringComparison.OrdinalIgnoreCase) >= 0) return "PHASE 2!";
            if (desc.IndexOf("PHASE 3", StringComparison.OrdinalIgnoreCase) >= 0) return "PHASE 3!";
            if (desc.IndexOf("stun", StringComparison.OrdinalIgnoreCase) >= 0) return "STUNNED!";
            if (desc.IndexOf("mark", StringComparison.OrdinalIgnoreCase) >= 0) return "MARKED!";
            if (desc.IndexOf("energy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                desc.IndexOf("overcharge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int amount = evt.Value > 0 ? evt.Value : 15;
                return $"+{amount} ENERGY";
            }

            return !string.IsNullOrEmpty(skill) ? skill.ToUpperInvariant() : "STATUS!";
        }
    }
}

