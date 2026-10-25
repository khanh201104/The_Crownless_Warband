using System;
using System.Collections.Generic;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Events;

namespace CrownlessWarband.Core.Battle
{
    /// <summary>
    /// Điều phối chu trình 3 giai đoạn của một lượt đấu (GDD Mục 10):
    /// Phase 1 (Planning) -> Phase 2 (AutoCombat) -> Phase 3 (Resolution).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class TurnManager
    {
        /// <summary>
        /// Bắt đầu Phase 1: Planning (Lập kế hoạch).
        /// Cập nhật chiến lược Squad AI 2 phe để người chơi quan sát và điều chỉnh trước khi bấm Auto.
        /// </summary>
        public static void StartPlanningPhase(BattleState state)
        {
            if (state == null) return;

            state.CurrentPhase = TurnPhase.Planning;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.TurnStarted,
                TurnNumber = state.CurrentTurn,
                Description = $"Turn {state.CurrentTurn} planning phase begins."
            });

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.PhaseChanged,
                TurnNumber = state.CurrentTurn,
                Description = $"Phase changed to {TurnPhase.Planning}."
            });

            // Giảm hồi chiêu Cooldown ở đầu lượt (từ Turn 2 trở đi) trước khi đánh giá chiến lược
            if (state.CurrentTurn > 1)
            {
                for (int i = 0; i < state.Units.Count; i++)
                {
                    var u = state.Units[i];
                    if (u == null || !u.IsAlive) continue;

                    for (int s = 0; s < u.Skills.Count; s++)
                    {
                        if (u.Skills[s] != null && u.Skills[s].CurrentCooldown > 0)
                        {
                            u.Skills[s].CurrentCooldown--;
                        }
                    }
                }
            }

            // Đánh giá chiến lược đầu lượt cho cả 2 phe (GDD Mục 10, 20)
            SquadAI.EvaluateAndUpdateStrategy(state, TeamSide.Player);
            SquadAI.EvaluateAndUpdateStrategy(state, TeamSide.Enemy);
        }

        /// <summary>
        /// Kích hoạt kỹ năng thủ công (Manual Skill) do người chơi chọn trong Planning Phase (GDD Mục 11 & 13).
        /// </summary>
        public static bool TryExecuteManualSkill(BattleState state, string unitId, string skillId, string targetId)
        {
            if (state == null || state.CurrentPhase != TurnPhase.Planning)
            {
                return false;
            }

            UnitState unit = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u != null && u.InstanceId == unitId && u.IsAlive && u.Team == TeamSide.Player)
                {
                    unit = u;
                    break;
                }
            }

            if (unit == null || unit.IsStunned) return false;

            SkillInstance skill = null;
            for (int i = 0; i < unit.Skills.Count; i++)
            {
                if (unit.Skills[i].SkillId == skillId)
                {
                    skill = unit.Skills[i];
                    break;
                }
            }

            if (skill == null || !skill.IsReady(unit.CurrentEnergy)) return false;

            var targets = new List<UnitState>();

            if (skill.TargetType == SkillTargetType.Self)
            {
                targets.Add(unit);
            }
            else if (skill.TargetType == SkillTargetType.AllEnemies)
            {
                for (int i = 0; i < state.Units.Count; i++)
                {
                    var enemy = state.Units[i];
                    if (enemy != null && enemy.IsAlive && enemy.Team == TeamSide.Enemy)
                    {
                        targets.Add(enemy);
                    }
                }
            }
            else
            {
                UnitState target = null;
                for (int i = 0; i < state.Units.Count; i++)
                {
                    var u = state.Units[i];
                    if (u != null && u.InstanceId == targetId && u.IsAlive)
                    {
                        target = u;
                        break;
                    }
                }

                if (target == null) return false;

                if (!CombatResolver.IsValidTarget(state, unit, skill, target))
                {
                    return false;
                }

                if (skill.TargetType == SkillTargetType.EnemyRow)
                {
                    for (int i = 0; i < state.Units.Count; i++)
                    {
                        var enemy = state.Units[i];
                        if (enemy != null && enemy.IsAlive && enemy.Team == target.Team && enemy.Position.Row == target.Position.Row)
                        {
                            targets.Add(enemy);
                        }
                    }
                }
                else
                {
                    targets.Add(target);
                }
            }

            CombatResolver.ExecuteSkill(state, unit, skill, targets, "Manual Skill Execution by Player");
            return true;
        }

        /// <summary>
        /// Bắt đầu Phase 2: Auto Combat (Thực thi theo thứ tự Speed Queue).
        /// Mỗi lượt đi của Unit đều chạy chu trình Observe -> AI -> Action.
        /// Kết thúc Phase 2 sẽ tự động chuyển sang Phase 3: Resolution.
        /// </summary>
        public static void ExecuteAutoCombatPhase(BattleState state)
        {
            if (state == null) return;

            state.CurrentPhase = TurnPhase.AutoCombat;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.PhaseChanged,
                TurnNumber = state.CurrentTurn,
                Description = $"Phase changed to {TurnPhase.AutoCombat}."
            });

            List<UnitState> turnQueue = state.GetTurnOrderQueue();

            for (int i = 0; i < turnQueue.Count; i++)
            {
                var unit = turnQueue[i];

                // Kiểm tra điều kiện thắng/thua giữa lượt (nếu một phe đã chết sạch)
                if (IsBattleOver(state, out _))
                {
                    break;
                }

                if (!unit.IsAlive) continue;

                // Thích nghi chiến thuật ngay giữa trận trước khi Unit ra đòn (GDD Mục 23)
                SquadAI.EvaluateAndUpdateStrategy(state, unit.Team);

                // Hết hạn đỡ đòn (Interception): Xóa hiệu ứng bảo vệ từ lượt trước của Guardian
                for (int a = 0; a < state.Units.Count; a++)
                {
                    if (state.Units[a].ProtectingAllyId == unit.InstanceId)
                    {
                        state.Units[a].ProtectingAllyId = null;
                    }
                }

                // Xử lý hiệu ứng Stun: Bị choáng sẽ mất lượt và hết choáng
                if (unit.IsStunned)
                {
                    unit.IsStunned = false;
                    state.EnqueueEvent(new CombatEvent
                    {
                        EventType = CombatEventType.StatusApplied,
                        TurnNumber = state.CurrentTurn,
                        SourceUnitId = unit.InstanceId,
                        Description = $"{unit.Name} is Stunned and skips their turn!"
                    });
                    continue;
                }

                state.EnqueueEvent(new CombatEvent
                {
                    EventType = CombatEventType.UnitTurnStarted,
                    TurnNumber = state.CurrentTurn,
                    SourceUnitId = unit.InstanceId,
                    Description = $"{unit.Name}'s turn begins."
                });

                // Thực thi Utility AI
                UnitAI.ExecuteUnitTurn(state, unit);
            }

            // Tự động chuyển tiếp sang Resolution Phase
            ExecuteResolutionPhase(state);
        }

        /// <summary>
        /// Bắt đầu Phase 3: Battle Resolution (Giải quyết cuối vòng).
        /// Giảm Cooldown kỹ năng, kiểm tra Thắng/Thua. Nếu tiếp tục sẽ tăng Turn và quay lại Planning.
        /// </summary>
        public static void ExecuteResolutionPhase(BattleState state)
        {
            if (state == null) return;

            state.CurrentPhase = TurnPhase.Resolution;

            state.EnqueueEvent(new CombatEvent
            {
                EventType = CombatEventType.PhaseChanged,
                TurnNumber = state.CurrentTurn,
                Description = $"Phase changed to {TurnPhase.Resolution}."
            });

            // Kiểm tra điều kiện kết thúc trận đấu (Win / Loss)
            if (IsBattleOver(state, out TeamSide? winner))
            {
                string endMsg = winner.HasValue
                    ? $"Battle ended! Winner: {winner.Value}."
                    : "Battle ended in a Draw!";

                state.EnqueueEvent(new CombatEvent
                {
                    EventType = CombatEventType.BattleEnded,
                    TurnNumber = state.CurrentTurn,
                    Description = endMsg
                });
            }
            else
            {
                state.CurrentTurn++;
                StartPlanningPhase(state);
            }
        }

        /// <summary>
        /// Kiểm tra xem trận đấu đã ngã ngũ chưa (GDD Mục 10 & 25).
        /// </summary>
        public static bool IsBattleOver(BattleState state, out TeamSide? winner)
        {
            winner = null;
            if (state == null) return false;

            bool playerAlive = false;
            bool enemyAlive = false;

            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u != null && u.IsAlive)
                {
                    if (u.Team == TeamSide.Player)
                    {
                        playerAlive = true;
                    }
                    else if (u.Team == TeamSide.Enemy)
                    {
                        enemyAlive = true;
                    }
                }
            }

            if (!playerAlive && !enemyAlive)
            {
                winner = null; // Hòa
                return true;
            }

            if (!playerAlive)
            {
                winner = TeamSide.Enemy;
                return true;
            }

            if (!enemyAlive)
            {
                winner = TeamSide.Player;
                return true;
            }

            return false;
        }

        public static bool IsBattleOver(BattleState state)
        {
            return IsBattleOver(state, out _);
        }
    }
}
