using System;
using System.Collections.Generic;
using System.Linq;
using CrownlessWarband.Core.AI;
using CrownlessWarband.Core.Events;
using CrownlessWarband.Core.Roguelite;

namespace CrownlessWarband.Core.Battle
{
    /// <summary>
    /// Quản lý toàn bộ trạng thái trận đấu (GDD Mục 25).
    /// Thuần C# 100%, hỗ trợ Serialize ra JSON để Save/Load, Replay và AI Simulation.
    /// </summary>
    [Serializable]
    public class BattleState
    {
        public int RunSeed;
        public int CurrentTurn = 1;
        public TurnPhase CurrentPhase = TurnPhase.Planning;

        public SquadStrategyType PlayerSquadStrategy = SquadStrategyType.Balanced;
        public SquadStrategyType EnemySquadStrategy = SquadStrategyType.DirectAssault;
        public WorldModifierType ActiveModifier = WorldModifierType.None;
        public int LastPlayerStrategyChangeTurn = -1;
        public int LastEnemyStrategyChangeTurn = -1;

        public List<UnitState> Units = new List<UnitState>();
        public List<RelicType> ActiveRelics = new List<RelicType>();
        public List<string> CombatLog = new List<string>();
        public Queue<CombatEvent> EventQueue = new Queue<CombatEvent>();
        public Dictionary<string, UnitAIDecisionLog> LastUnitDecisions = new Dictionary<string, UnitAIDecisionLog>();

        /// <summary>
        /// Kiểm tra phe người chơi có sở hữu một Di vật nhất định không (không dùng LINQ)
        /// </summary>
        public bool HasRelic(RelicType relic)
        {
            if (ActiveRelics == null) return false;
            for (int i = 0; i < ActiveRelics.Count; i++)
            {
                if (ActiveRelics[i] == relic) return true;
            }
            return false;
        }

        // --- Các hàm truy vấn nhanh cho AI Observation (GDD Mục 15) ---

        public IEnumerable<UnitState> GetAliveUnits(TeamSide team)
        {
            return Units.Where(u => u.Team == team && u.IsAlive);
        }

        public IEnumerable<UnitState> GetEnemiesOf(UnitState unit)
        {
            TeamSide enemySide = unit.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            return GetAliveUnits(enemySide);
        }

        public IEnumerable<UnitState> GetAlliesOf(UnitState unit, bool includeSelf = false)
        {
            return GetAliveUnits(unit.Team).Where(u => includeSelf || u.InstanceId != unit.InstanceId);
        }

        /// <summary>
        /// Kiểm tra xem hàng Front của một phe còn ai sống sót để che chắn cho hàng sau không (GDD Mục 9)
        /// Không dùng LINQ để tránh GC Allocation trong vòng lặp lọc mục tiêu
        /// </summary>
        public bool HasAliveFrontline(TeamSide team)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u != null && u.Team == team && u.IsAlive && u.Position.Row == RowType.Front)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Lấy thứ tự hành động trong Phase 2 dựa theo Speed từ cao xuống thấp
        /// </summary>
        public List<UnitState> GetTurnOrderQueue()
        {
            var queue = new List<UnitState>();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u != null && u.IsAlive)
                {
                    queue.Add(u);
                }
            }

            queue.Sort((a, b) =>
            {
                int speedCmp = b.Speed.CompareTo(a.Speed);
                if (speedCmp != 0) return speedCmp;
                return a.Team.CompareTo(b.Team); // Ưu tiên Player nếu đồng tốc
            });

            return queue;
        }

        public void AppendLog(string message)
        {
            CombatLog.Add($"[T{CurrentTurn}] {message}");
        }

        public void EnqueueEvent(CombatEvent evt)
        {
            if (evt == null) return;
            EventQueue.Enqueue(evt);
            if (!string.IsNullOrEmpty(evt.Description))
            {
                AppendLog(evt.Description);
            }
        }
    }
}