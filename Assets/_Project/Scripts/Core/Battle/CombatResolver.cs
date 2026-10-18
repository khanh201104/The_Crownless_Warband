using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Events;

namespace CrownlessWarband.Core.Battle
{
    /// <summary>
    /// Stateless helper class that executes a skill from attacker onto target(s),
    /// mutates BattleState, and pushes CombatEvents to EventQueue.
    /// Pure C# - no UnityEngine or MonoBehaviour dependencies.
    /// </summary>
    public static class CombatResolver
    {
        public static void ExecuteSkill(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary = null)
        {
            var targets = target != null ? new List<UnitState> { target } : new List<UnitState>();
            ExecuteSkill(state, attacker, skill, targets, aiDebugSummary);
        }

        public static void ExecuteSkill(BattleState state, UnitState attacker, SkillInstance skill, List<UnitState> targets, string aiDebugSummary = null)
        {
            if (state == null || attacker == null || skill == null) return;
            if (attacker.IsStunned || !attacker.IsAlive) return;
            // 1. Deduct Energy & set Cooldown
            attacker.CurrentEnergy = Math.Max(0, attacker.CurrentEnergy - skill.EnergyCost);
            skill.CurrentCooldown = skill.CooldownTurns;

            // BasicAttack grants +20 Energy to attacker
            if (skill.Slot == SkillSlotType.BasicAttack)
            {
                attacker.CurrentEnergy = Math.Min(attacker.MaxEnergy, attacker.CurrentEnergy + 20);
            }

            // 2. Enqueue SkillUsed event
            string primaryTargetId = (targets != null && targets.Count > 0) ? targets[0]?.InstanceId : null;
            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.SkillUsed,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = primaryTargetId,
                SkillName = skill.Name,
                Value = skill.EnergyCost,
                Description = $"{attacker.Name} uses {skill.Name}.",
                AIDebugSummary = aiDebugSummary
            });

            if (targets == null || targets.Count == 0) return;

            // 3. Process targets
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive) continue;

                // Check Interception for single-target enemy attacks (GDD Section 26 & 53)
                if (skill.TargetType == SkillTargetType.SingleEnemy && target.Team != attacker.Team && !string.IsNullOrEmpty(target.ProtectingAllyId))
                {
                    UnitState guardian = FindAliveUnitById(state, target.ProtectingAllyId);
                    if (guardian != null && guardian.IsAlive && !guardian.IsStunned)
                    {
                        state.EnqueueEvent(new CombatEvent
                        {
                            EventType = CombatEventType.InterceptionTriggered,
                            TurnNumber = state.CurrentTurn,
                            SourceUnitId = guardian.InstanceId,
                            TargetUnitId = target.InstanceId,
                            SkillName = skill.Name,
                            Description = $"{guardian.Name} intercepts attack aimed at {target.Name}!",
                            AIDebugSummary = aiDebugSummary
                        });
                        target = guardian;
                    }
                }

                ApplySkillEffect(state, attacker, skill, target, aiDebugSummary);
            }
        }
        public static bool IsValidTarget(BattleState state, UnitState attacker, SkillInstance skill, UnitState target)
        {
            if (state == null || attacker == null || skill == null || target == null || !target.IsAlive)
                return false;

            // Chiêu hỗ trợ/buff đồng minh
            if (skill.TargetType == SkillTargetType.SingleAlly || skill.TargetType == SkillTargetType.LowestHpAlly)
            {
                return target.Team == attacker.Team;
            }

            // Chiêu tấn công kẻ địch
            if (target.Team == attacker.Team)
                return false;

            // Kiểm tra hàng sau: Nếu chiêu không đánh được Backline, mà hàng Front của địch vẫn còn người thì không được đánh hàng Back
            if (target.Position.Row == RowType.Back && !skill.CanTargetBackline)
            {
                if (state.HasAliveFrontline(target.Team))
                {
                    return false;
                }
            }

            return true;
        }
        private static void ApplySkillEffect(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            switch (skill.EffectType)
            {
                case SkillEffectType.Damage:
                case SkillEffectType.ExecuteDamage:
                    ResolveDamage(state, attacker, skill, target, aiDebugSummary);
                    break;

                case SkillEffectType.Heal:
                    ResolveHeal(state, attacker, skill, target, aiDebugSummary);
                    break;

                case SkillEffectType.Shield:
                    ResolveShield(state, attacker, skill, target, aiDebugSummary);
                    break;

                case SkillEffectType.Stun:
                    ResolveStun(state, attacker, skill, target, aiDebugSummary);
                    break;

                case SkillEffectType.Mark:
                    ResolveMark(state, attacker, skill, target, aiDebugSummary);
                    break;

                case SkillEffectType.ProtectAlly:
                    ResolveProtectAlly(state, attacker, skill, target, aiDebugSummary);
                    break;
            }
        }

        public static int CalculateDamage(UnitState attacker, SkillInstance skill, UnitState target)
        {
            if (attacker == null || skill == null || target == null) return 0;

            int baseDamage = Math.Max(1, (int)(attacker.Attack * skill.PowerMultiplier) - target.Defense / 2);
            float multiplier = 1.0f;

            if (target.IsMarked)
            {
                multiplier += 0.25f;
            }

            if (skill.EffectType == SkillEffectType.ExecuteDamage && target.HPPercentage < 0.4f)
            {
                multiplier += 0.50f;
            }

            return Math.Max(1, (int)(baseDamage * multiplier));
        }

        private static void ResolveDamage(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            int finalDamage = CalculateDamage(attacker, skill, target);

            // Deduct Shield first, then HP
            int remainingDamage = finalDamage;
            if (target.Shield > 0)
            {
                if (target.Shield >= remainingDamage)
                {
                    target.Shield -= remainingDamage;
                    remainingDamage = 0;
                }
                else
                {
                    remainingDamage -= target.Shield;
                    target.Shield = 0;
                }
            }

            target.CurrentHP = Math.Max(0, target.CurrentHP - remainingDamage);

            // Target gains +10 Energy when taking damage (only if still alive)
            if (target.CurrentHP > 0)
            {
                target.CurrentEnergy = Math.Min(target.MaxEnergy, target.CurrentEnergy + 10);
            }

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.DamageDealt,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = target.InstanceId,
                SkillName = skill.Name,
                Value = finalDamage,
                Description = $"{target.Name} takes {finalDamage} damage from {attacker.Name}'s {skill.Name}.",
                AIDebugSummary = aiDebugSummary
            });

            // Check death
            if (target.CurrentHP <= 0)
            {
                target.CurrentHP = 0;
                target.Shield = 0;
                target.CurrentEnergy = 0;
                target.IsMarked = false;
                target.IsStunned = false;
                target.ProtectingAllyId = null;

                // Clear any other unit pointing to target as protector
                for (int i = 0; i < state.Units.Count; i++)
                {
                    if (state.Units[i].ProtectingAllyId == target.InstanceId)
                    {
                        state.Units[i].ProtectingAllyId = null;
                    }
                }

                state.EnqueueEvent(new CombatEvent
                {
                    EventType = CombatEventType.UnitDied,
                    TurnNumber = state.CurrentTurn,
                    SourceUnitId = attacker.InstanceId,
                    TargetUnitId = target.InstanceId,
                    SkillName = skill.Name,
                    Description = $"{target.Name} has fallen!",
                    AIDebugSummary = aiDebugSummary
                });
            }
        }

        private static void ResolveHeal(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            int healAmount = Math.Max(1, (int)(attacker.Attack * skill.PowerMultiplier));
            int oldHP = target.CurrentHP;
            target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
            int actualHealed = target.CurrentHP - oldHP;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.HealApplied,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = target.InstanceId,
                SkillName = skill.Name,
                Value = actualHealed,
                Description = $"{target.Name} is healed for {actualHealed} HP by {attacker.Name}.",
                AIDebugSummary = aiDebugSummary
            });
        }

        private static void ResolveShield(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            int shieldAmount = Math.Max(1, (int)(attacker.Attack * skill.PowerMultiplier));
            target.Shield += shieldAmount;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.ShieldApplied,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = target.InstanceId,
                SkillName = skill.Name,
                Value = shieldAmount,
                Description = $"{target.Name} receives {shieldAmount} Shield from {attacker.Name}.",
                AIDebugSummary = aiDebugSummary
            });
        }

        private static void ResolveStun(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            target.IsStunned = true;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.StatusApplied,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = target.InstanceId,
                SkillName = skill.Name,
                Description = $"{target.Name} is Stunned by {attacker.Name}!",
                AIDebugSummary = aiDebugSummary
            });
        }

        private static void ResolveMark(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            target.IsMarked = true;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.StatusApplied,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = target.InstanceId,
                SkillName = skill.Name,
                Description = $"{target.Name} is Marked by {attacker.Name}!",
                AIDebugSummary = aiDebugSummary
            });
        }

        private static void ResolveProtectAlly(BattleState state, UnitState attacker, SkillInstance skill, UnitState target, string aiDebugSummary)
        {
            target.ProtectingAllyId = attacker.InstanceId;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.StatusApplied,
                TurnNumber = state.CurrentTurn,
                SourceUnitId = attacker.InstanceId,
                TargetUnitId = target.InstanceId,
                SkillName = skill.Name,
                Description = $"{attacker.Name} is now protecting {target.Name}!",
                AIDebugSummary = aiDebugSummary
            });
        }

        private static UnitState FindAliveUnitById(BattleState state, string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || state == null || state.Units == null) return null;

            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.InstanceId == unitId && u.IsAlive)
                {
                    return u;
                }
            }

            return null;
        }
    }
}
