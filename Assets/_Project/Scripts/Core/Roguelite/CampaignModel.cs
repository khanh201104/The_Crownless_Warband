using System;
using System.Collections.Generic;
using CrownlessWarband.Core.Battle;

namespace CrownlessWarband.Core.Roguelite
{
    /// <summary>
    /// Các loại Node trên bản đồ phân nhánh Campaign (GDD Section 4, 30, 31).
    /// </summary>
    public enum CampaignNodeType
    {
        Combat,
        EliteCombat,
        Event,
        Recovery,
        Boss
    }

    /// <summary>
    /// Đại diện cho một điểm dừng (Node) trên bản đồ Campaign theo tầng (Floor & Lane).
    /// </summary>
    [Serializable]
    public class CampaignNode
    {
        public int FloorIndex; // 0 to 4 cho MVP run 5 tầng
        public int LaneIndex;  // 0 hoặc 1 cho nhánh rẽ
        public string NodeId;  // Ví dụ: "F0_L0", "F1_L1"
        public CampaignNodeType NodeType;
        public string Title;
        public int ThreatLevel;
        public bool IsCompleted;
        public WorldModifierType Modifier = WorldModifierType.None;
        public List<string> NextNodeIds = new List<string>();

        public CampaignNode() { }

        public CampaignNode(int floorIndex, int laneIndex, string nodeId, CampaignNodeType nodeType, string title, int threatLevel)
        {
            FloorIndex = floorIndex;
            LaneIndex = laneIndex;
            NodeId = nodeId;
            NodeType = nodeType;
            Title = title;
            ThreatLevel = threatLevel;
            IsCompleted = false;
        }

        public override string ToString()
        {
            return $"[{NodeId}] {Title} ({NodeType}, Threat: {ThreatLevel})";
        }
    }

    /// <summary>
    /// Trạng thái toàn bộ lượt chơi Roguelite (GDD Section 4, 5, 6, 30, 31, 35, 36).
    /// Thuần C#, hỗ trợ Serialize ra JSON để Save/Load giữa chừng.
    /// </summary>
    [Serializable]
    public class RunState
    {
        public int Seed;
        public int CurrentFloor = 0;
        public int CorruptionLevel = 0; // 0% đến 100%, tăng 15% mỗi tầng (GDD Section 36)
        public int BattlesWon = 0;
        public bool IsRunCompleted = false;
        public bool IsRunFailed = false;
        public string CurrentNodeId;

        public List<CampaignNode> Nodes = new List<CampaignNode>();
        public List<UnitState> PlayerRoster = new List<UnitState>();
        public List<RelicType> CollectedRelics = new List<RelicType>();
        public List<RewardOption> PendingRewards = new List<RewardOption>();

        public CampaignNode GetCurrentNode()
        {
            return GetNode(CurrentNodeId);
        }

        public CampaignNode GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || Nodes == null) return null;
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].NodeId == nodeId)
                {
                    return Nodes[i];
                }
            }
            return null;
        }

        public bool HasRelic(RelicType relic)
        {
            if (CollectedRelics == null) return false;
            for (int i = 0; i < CollectedRelics.Count; i++)
            {
                if (CollectedRelics[i] == relic) return true;
            }
            return false;
        }
    }
}

