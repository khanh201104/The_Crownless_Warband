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

        public PlaybackSpeedMode SpeedMode { get; set; } = PlaybackSpeedMode.Speed1x;
        public bool IsPaused { get; set; } = false;
        public bool IsPlayingEvents => _stepWaitTimer > 0f || (sandboxController != null && sandboxController.CurrentState != null && sandboxController.CurrentState.EventQueue.Count > 0);
        public string CurrentActingUnitId { get; private set; }
        public string LastPresentedEventSummary { get; private set; } = "Ready";
        public event Action<string> OnActingUnitChanged;

        private BattleState _lastObservedState;
        private float _stepWaitTimer;
        private bool _wasInActiveBattle = true;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            EnsureReferences();
            if (sandboxController == null || sandboxController.CurrentState == null) return;

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
                return;
            }

            if (IsPaused) return;

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
        }

        private float PresentNextEvent(CombatEvent evt, BattleState state)
        {
            if (evt == null) return 0f;

            LastPresentedEventSummary = !string.IsNullOrEmpty(evt.Description)
                ? evt.Description
                : $"{evt.EventType}: {evt.SkillName}";

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
                        if (sourceUnit != null && targetUnit != null && sourceUnit.Team != targetUnit.Team && targetView != null)
                        {
                            sourceView.PlayAttackLunge(targetView.transform.position);
                        }
                        else
                        {
                            sourceView.PlayPulseEffect(Color.cyan);
                        }

                        if (floatingTextView != null && !string.IsNullOrEmpty(evt.SkillName))
                        {
                            floatingTextView.SpawnFloatingText(
                                sourceView.transform.position + Vector3.up * 0.70f,
                                evt.SkillName,
                                new Color(1.0f, 0.92f, 0.28f, 1f),
                                duration: 1.05f,
                                fontSize: 14,
                                riseSpeed: 0.70f
                            );
                        }
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

                        if (floatingTextView != null)
                        {
                            floatingTextView.SpawnFloatingText(
                                guardianView.transform.position + Vector3.up * 0.65f,
                                "INTERCEPT!",
                                new Color(0.30f, 1.0f, 0.80f, 1f),
                                duration: 1.15f,
                                fontSize: 15,
                                riseSpeed: 0.85f
                            );
                        }
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
                            targetView.PlayHitShake();
                            if (floatingTextView != null)
                            {
                                int fontSize = (evt.SkillName != null && evt.Value >= 45) ? 16 : 14;
                                floatingTextView.SpawnFloatingText(
                                    targetView.transform.position + Vector3.up * 0.55f,
                                    $"-{evt.Value}",
                                    new Color(1.0f, 0.32f, 0.25f, 1f),
                                    duration: 1.15f,
                                    fontSize: fontSize,
                                    riseSpeed: 0.90f
                                );
                            }
                        }
                    }
                    else if (floatingTextView != null)
                    {
                        floatingTextView.SpawnTacticalBanner("ACID RAIN (-5% HP)", new Color(0.68f, 0.95f, 0.25f, 1f), 1.6f);
                    }
                    return 0.34f;
                }

                case CombatEventType.HealApplied:
                {
                    UnitVisualView targetView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;
                    if (targetView != null)
                    {
                        targetView.PlayPulseEffect(Color.green);
                        if (floatingTextView != null)
                        {
                            floatingTextView.SpawnFloatingText(
                                targetView.transform.position + Vector3.up * 0.55f,
                                $"+{evt.Value} HP",
                                new Color(0.30f, 1.0f, 0.45f, 1f),
                                duration: 1.15f,
                                fontSize: 14,
                                riseSpeed: 0.85f
                            );
                        }
                    }
                    return 0.30f;
                }

                case CombatEventType.ShieldApplied:
                {
                    UnitVisualView targetView = gridView != null ? gridView.GetUnitVisual(evt.TargetUnitId) : null;
                    if (targetView != null)
                    {
                        targetView.PlayPulseEffect(Color.cyan);
                        if (floatingTextView != null)
                        {
                            floatingTextView.SpawnFloatingText(
                                targetView.transform.position + Vector3.up * 0.55f,
                                $"+{evt.Value} SHD",
                                new Color(0.35f, 0.90f, 1.0f, 1f),
                                duration: 1.15f,
                                fontSize: 14,
                                riseSpeed: 0.85f
                            );
                        }
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
                        unitView.PlayPulseEffect(badgeColor);
                        if (floatingTextView != null && !string.IsNullOrEmpty(badgeText))
                        {
                            floatingTextView.SpawnFloatingText(
                                unitView.transform.position + Vector3.up * 0.65f,
                                badgeText,
                                badgeColor,
                                duration: 1.20f,
                                fontSize: 14,
                                riseSpeed: 0.80f
                            );
                        }
                    }
                    return 0.32f;
                }

                case CombatEventType.UnitDied:
                {
                    string deadUnitId = !string.IsNullOrEmpty(evt.TargetUnitId) ? evt.TargetUnitId : evt.SourceUnitId;
                    UnitVisualView deadView = gridView != null ? gridView.GetUnitVisual(deadUnitId) : null;
                    if (deadView != null)
                    {
                        deadView.PlayHitShake(0.40f, 0.22f);
                        if (floatingTextView != null)
                        {
                            floatingTextView.SpawnFloatingText(
                                deadView.transform.position + Vector3.up * 0.65f,
                                "DEFEATED!",
                                new Color(0.95f, 0.18f, 0.25f, 1f),
                                duration: 1.35f,
                                fontSize: 16,
                                riseSpeed: 0.75f
                            );
                        }
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

