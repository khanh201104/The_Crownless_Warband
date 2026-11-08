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

    /// <summary>
    /// Biến số Môi trường chiến trường (World Modifiers - GDD Section 33).
    /// </summary>
    public enum WorldModifierType
    {
        None = 0,
        AcidRain,       // Cuối mỗi turn, toàn bộ unit mất 5% MaxHP
        BloodMoon,      // Cả 2 phe gây thêm +25% sát thương
        Overcharge,     // Đầu mỗi turn, toàn bộ unit nhận thêm +15 Energy
        NoHealing       // Hiệu quả hồi máu (Heal) bị giảm 70%
    }

    /// <summary>
    /// Tiền tố biến thể Kẻ địch (Enemy Affixes - GDD Section 34).
    /// </summary>
    public enum EnemyAffixType
    {
        None = 0,
        Armored,        // +35% Defense và bắt đầu trận với 40 Shield
        Berserker,      // +30% Attack khi HP < 50%
        Regenerator     // Hồi 8% MaxHP vào cuối mỗi turn
    }
}