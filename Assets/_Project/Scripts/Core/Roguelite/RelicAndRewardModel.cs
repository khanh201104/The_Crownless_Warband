using System;

namespace CrownlessWarband.Core.Roguelite
{
    /// <summary>
    /// Các loại Di vật chiến thuật (Tactical Relics - GDD Section 28 & 29).
    /// </summary>
    public enum RelicType
    {
        None = 0,
        BloodContract,
        TacticalCore,
        EmergencyProtocol,
        ChainRelay
    }

    /// <summary>
    /// Các loại phần thưởng nâng cấp sau trận (Post-Battle Upgrades - GDD Section 27).
    /// </summary>
    public enum UpgradeRewardType
    {
        TeamHeal30Percent,
        BoostGuardianDefense,
        BoostRangerCritMark,
        BoostMageAoE,
        GrantRelic
    }

    /// <summary>
    /// Tùy chọn phần thưởng xuất hiện sau khi thắng trận (GDD Section 27, 28, 29).
    /// </summary>
    [Serializable]
    public class RewardOption
    {
        public string RewardId;
        public string Title;
        public string Description;
        public UpgradeRewardType RewardType;
        public RelicType GrantedRelic;

        public RewardOption() { }

        public RewardOption(string rewardId, string title, string description, UpgradeRewardType rewardType, RelicType grantedRelic = RelicType.None)
        {
            RewardId = rewardId;
            Title = title;
            Description = description;
            RewardType = rewardType;
            GrantedRelic = grantedRelic;
        }
    }
}

