namespace CrownlessWarband.Core.Battle
{
    public enum TeamSide
    {
        Player = 0,
        Enemy = 1
    }

    /// <summary>
    /// 3 hàng cố định theo GDD Mục 8 & 9: Front, Mid, Back
    /// </summary>
    public enum RowType
    {
        Front = 0,
        Mid = 1,
        Back = 2
    }

    public enum UnitRole
    {
        Guardian,   // Tank / Protection
        Vanguard,   // Frontline / Control
        Ranger,     // Single-target DPS
        Mage,       // AoE / Control
        Medic       // Support / Healer
    }

    public enum TurnPhase
    {
        Planning,       // Phase 1: Người chơi xem thông tin, chỉnh chiến thuật/AI policy
        AutoCombat,     // Phase 2: AI hai phe thực thi theo thứ tự Speed
        Resolution      // Phase 3: Cập nhật cooldown, status, kiểm tra thắng/thua
    }

    public enum SkillControlMode
    {
        Auto,           // AI tự quyết định hoàn toàn
        SemiAuto,       // Chạy theo điều kiện người chơi cài đặt
        Manual          // Chỉ kích hoạt khi người chơi bấm nút
    }

    public enum SquadStrategyType
    {
        Balanced,
        DirectAssault,
        BacklineDive,
        FocusFire,
        ProtectBackline,
        AntiHeal,
        Attrition,
        Defensive
    }
}