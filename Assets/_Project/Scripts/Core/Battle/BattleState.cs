using System;
using System.Collections.Generic;
using System.Linq;
using CrownlessWarband.Core.Events;

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

        public List<UnitState> Units = new List<UnitState>();
        public List<string> CombatLog = new List<string>();
        public Queue<CombatEvent> EventQueue = new Queue<CombatEvent>();

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
        /// </summary>
        public bool HasAliveFrontline(TeamSide team)
        {
            return Units.Any(u => u.Team == team && u.IsAlive && u.Position.Row == RowType.Front);
        }

        /// <summary>
        /// Lấy thứ tự hành động trong Phase 2 dựa theo Speed từ cao xuống thấp
        /// </summary>
        public List<UnitState> GetTurnOrderQueue()
        {
            return Units
                .Where(u => u.IsAlive)
                .OrderByDescending(u => u.Speed)
                .ThenBy(u => u.Team) // Ưu tiên Player nếu đồng tốc
                .ToList();
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