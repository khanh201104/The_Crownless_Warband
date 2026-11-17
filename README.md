# The Crownless Warband (AI-Driven Tactical Roguelite)
> **Tài liệu Thiết kế Game (GDD), Kiến trúc Kỹ thuật & Kế hoạch Triển khai Đồ án (12 Tuần)**  
> **Sinh viên thực hiện:** Phạm Quốc Khánh — MSSV: 20225138 (Đại học Bách Khoa Hà Nội)  
> **Phiên bản:** 2.0 (Hoàn thành Tuần 1–8 & Lộ trình Nâng cấp Đồ họa/UX-UI Tuần 9–12)  
> **Thể loại:** Tactical Roguelite / Turn-based Auto Combat / Squad Strategy  
> **Nền tảng:** PC | **Engine:** Unity 2D (C#) | **Chế độ:** Single-player  

---

## 0. HƯỚNG DẪN DÀNH CHO AI TRỢ LÝ LẬP TRÌNH (SYSTEM PROMPT FOR AI ASSISTANTS)
*Nếu bạn là một AI Coding Agent đang đọc file này để hỗ trợ viết code, thiết kế dữ liệu hoặc debug cho dự án **The Crownless Warband**, hãy tuân thủ tuyệt đối các nguyên tắc bất biến sau:*

1. **Tách biệt Logic và Hiển thị (Humble Object Pattern — Phân tách 3 tầng bằng `.asmdef`):**
   - **`CrownlessWarband.Core` (`Assets/_Project/Scripts/Core/`):** Thuần C# 100% (Pure C#, tuyệt đối không kế thừa `MonoBehaviour`, không dùng `ScriptableObject`, không gọi `Debug.Log`). Trận đấu diễn ra bằng cách cập nhật `BattleState`, ghi vào `BattleState.CombatLog` và đẩy các `CombatEvent` vào `BattleState.EventQueue`.
   - **`CrownlessWarband.Data` (`Assets/_Project/Scripts/Data/`):** Chứa các định nghĩa `ScriptableObject` (`UnitDataSO`, `SkillDataSO`) để thiết kế dữ liệu trên Unity Inspector và chuyển đổi sang đối tượng Pure C# lúc Runtime.
   - **`CrownlessWarband.Presentation` (`Assets/_Project/Scripts/Presentation/`):** Chứa các lớp `MonoBehaviour` đọc `BattleState` và tiêu thụ `EventQueue` để hiển thị đồ họa 2D, diễn hoạt Procedural Tweens / VFX và vẽ giao diện uGUI Canvas HUD / `F3 Explainable AI Debugger`.
2. **Không có Pathfinding & Di chuyển tự do trong trận:**
   - Chiến trường sử dụng hệ thống **Formation Slot cố định** (3 hàng: `Front`, `Mid`, `Back`, mỗi hàng 3 cột `0..2`). Tuyệt đối không sinh code tìm đường (`A*`, `NavMesh`) hay tính khoảng cách vật lý trong logic tính toán.
3. **AI là "Bạch hộp" (Explainable Utility AI), KHÔNG dùng Machine Learning hộp đen:**
   - Mọi quyết định của AI (chọn mục tiêu, chọn kỹ năng, đổi chiến thuật đội) đều phải xuất ra cấu trúc `AIDecisionRecord` (`TargetScoreBreakdown` & `ActionScoreBreakdown`) để hiển thị lên **AI Debug Mode (Phím F3)** và **Combat Log**.
4. **Tuân thủ Flash Coding Protocol:**
   - Không sửa quá **3 file C#** trong một bước thực thi trừ khi có chỉ định rõ.
   - Không dùng `LINQ` trong vòng lặp nóng (dùng `for`/`foreach` thuần để tránh cấp phát bộ nhớ rác GC).

---

## 1. TỔNG QUAN DỰ ÁN & TẦM NHÌN (GDD MỤC 1–6)

### 1.1. One-Sentence Pitch
**The Crownless Warband (Tàn Thành)** là game chiến thuật roguelite theo lượt, nơi người chơi xây dựng một đội hình và thiết lập chính sách hành vi chiến đấu cho từng unit, sau đó chứng kiến AI của hai phe tự phân tích, phối hợp và thích nghi qua từng trận đấu.

### 1.2. Ý tưởng cốt lõi (Core Concept)
Người chơi không trực tiếp điều khiển từng đòn đánh cơ bản của nhân vật trong trận chiến, mà đảm nhận vai trò **Người chỉ huy đội hình (Squad Commander)**:
- Tuyển chọn thành viên, xây dựng đội hình và xếp vị trí vào các ô Formation (`Front`, `Mid`, `Back`).
- Xây dựng bộ kỹ năng, nâng cấp (`Upgrades`) và thu thập Di vật (`Relics`).
- Thiết lập chính sách hành xử của AI (`UnitAIPolicy`) cho từng unit và chiến thuật toàn đội (`SquadStrategyType`).
- Quyết định kỹ năng nào để AI tự dùng (`Auto` / `SemiAuto`) và kỹ năng nào do người chơi kích hoạt (`Manual`).
- Quan sát diễn biến chiến đấu và can thiệp bằng kỹ năng/mệnh lệnh vào các thời điểm bước ngoặt.

> **Câu hỏi trọng tâm của Gameplay:** *"Mình đã xây dựng một đội hình đủ thông minh để tự chiến đấu chưa?"*

### 1.3. Cảm giác người chơi hướng tới (Game Fantasy)
Một trận đấu thành công phải khiến người chơi cảm nhận rõ:
- *"Mình đã sắp xếp đội hình khắc chế hoàn hảo!"*
- *"Đội hình này rất mạnh nhưng thiết lập ưu tiên AI của mình đang sai, chỉ cần chỉnh lại điều kiện của một unit là cục diện sẽ đảo chiều."*

### 1.4. Các nguyên tắc thiết kế (Design Pillars)
1. **Strategy over Micromanagement:** Tập trung vào đội hình, hướng build, ưu tiên mục tiêu và kỹ năng tối thượng thay vì click từng đòn đánh mỗi lượt.
2. **AI là Gameplay trung tâm:** Cả hai phe đều vận hành bằng AI (`PLAYER AI ↔ GAME STATE ↔ ENEMY AI`).
3. **Mỗi Run là duy nhất:** Khác biệt thông qua `Seed`, đội hình, nâng cấp kỹ năng, di vật, sự kiện, biến thể kẻ địch (`Affixes`) và điều kiện môi trường (`World Modifiers`).
4. **Mở rộng nội dung bằng Hệ thống (Systemic Content):** Kết hợp tự động `Biomes + Encounter + Enemy + Affix + Modifier + Reward + Boss` thay vì thiết kế hàng trăm màn chơi thủ công.

### 1.5. Vòng lặp Gameplay (Core Game Loop)
```text
[Khởi tạo Seed & Đội hình khởi đầu]
        ↓
[Campaign Map (5 Floors): Chọn Node tiếp theo trên nhánh đường đi]
        ↓
[Pre-Battle / Planning Phase: Sắp xếp Formation + Chỉnh sửa AI Policy + Xem trạng thái]
        ↓
[Auto Combat Phase: AI hai phe tự giao chiến theo Speed Queue + Người chơi can thiệp Manual Skill]
        ↓
[Post-Battle Resolution: Nhận Reward (1 trong 3) -> Nâng cấp Chỉ số / Nhận Relic -> Hồi 25% HP]
        ↓
[Tiến tới Boss "The Warden" (3 Phases) -> Thắng / Thua -> Endless Mode / Run mới]
```

---

## 2. HỆ THỐNG CHIẾN ĐẤU, TRÍ TUỆ NHÂN TẠO & ROGUELITE (GDD MỤC 7–59)

### 2.1. Bố cục Chiến trường & Đội hình (Formation Grid)
- Mỗi phe có một lưới vị trí cố định gồm 3 hàng (`Front`, `Mid`, `Back`) $\times$ 3 cột (`0, 1, 2`).
- **Quy tắc bảo vệ hàng sau:** Đòn đánh cận chiến thông thường (`CanTargetBackline == false`) buộc phải tấn công hàng `Front` (hoặc hàng gần nhất còn sống) trước. Chỉ các kỹ năng tầm xa hoặc ám sát (`CanTargetBackline == true`) mới được nhắm thẳng vào `Mid`/`Back` khi `Front` còn sống.
- **Cơ chế Đỡ đòn (`Interception`):** Khi `Guardian` kích hoạt kỹ năng bảo vệ đồng đội (`ProtectingAllyId`), mọi đòn đánh đơn mục tiêu nhắm vào đồng đội đó sẽ bị `Guardian` chặn lại và chịu sát thương thay (nếu `Guardian` còn sống và không bị `Stun`).

### 2.2. Cấu trúc 3 Pha của Lượt đấu (`TurnManager`)
1. **Phase 1 — `Planning`:**
   - Từ Turn 2 trở đi: Giảm `CurrentCooldown` đi 1 cho các kỹ năng đang hồi.
   - Kích hoạt biến số môi trường đầu lượt (ví dụ: `Overcharge` cộng $+15\text{ Energy}$ cho mọi nhân vật còn sống).
   - `SquadAI` của cả 2 phe đánh giá toàn cục chiến trường và cập nhật chiến thuật đội.
   - Người chơi có thể thay đổi `UnitAIPolicy`, đổi vị trí `Formation` (trước trận/đầu trận), hoặc kích hoạt kỹ năng `Manual`.
2. **Phase 2 — `AutoCombat`:**
   - Các nhân vật còn sống của cả 2 phe được sắp xếp vào hàng đợi hành động (`TurnOrderQueue`) theo `Speed` giảm dần.
   - Khi đến lượt: Xóa hiệu ứng đỡ đòn cũ của bản thân; nếu đang bị `Stun` thì bỏ qua lượt và xóa cờ `Stun`; nếu bình thường thì thực thi chu trình `UnitAI`: `Observe -> Evaluate Targets -> Evaluate Actions -> Execute`.
3. **Phase 3 — `Resolution`:**
   - Xử lý hiệu ứng cuối lượt: `AcidRain` (trừ $5\%\text{ MaxHP}$, giữ tối thiểu $1\text{ HP}$), `Regenerator` Affix (hồi $8\%\text{ MaxHP}$), cơ chế `Fatigue` từ Turn 15 trở đi (mỗi lượt trừ $(\text{CurrentTurn} - 14) \times 6\text{ HP}$ để chống kẹt trận đấu).
   - Kiểm tra điều kiện kết thúc trận đấu (hoặc phân định thắng thua ở Turn 25). Nếu chưa kết thúc, tăng `CurrentTurn` và quay lại `Planning`.

### 2.3. Kiến trúc AI 2 Tầng (`SquadAI` + `Utility AI`)
1. **Tầng 1 — `SquadAI` (Chỉ huy Chiến thuật Vĩ mô & Thích nghi Giữa trận):**
   - Các chiến thuật: `Balanced`, `DirectAssault`, `BacklineDive`, `FocusFire`, `ProtectBackline`, `Defensive`.
   - **Thích nghi giữa trận (`Mid-Battle Adaptation`):** Khi sát thủ hàng sau (`BacklineDiver` có `CanTargetBackline && BaseThreat >= 40`) tử trận, phe địch tự động chuyển từ `BacklineDive` sang `DirectAssault`. Khi đồng minh hàng sau xuống dưới $55\%\text{ HP}$, phe người chơi tự động chuyển sang `ProtectBackline`.
   - **Chống dao động (`Anti-Thrashing`):** Mỗi phe chỉ đổi chiến thuật tối đa 1 lần mỗi lượt (trừ khi có sự kiện nhân vật chủ chốt tử trận).
2. **Tầng 2 — `TargetEvaluator` & `ActionEvaluator` (Chấm điểm Utility Bạch hộp):**
   - **Công thức chấm điểm mục tiêu (`TargetScoreBreakdown`):**
     $$\text{TotalScore} = \text{Threat} + \text{StrategicValue} + \text{KillProb} + \text{Vulnerability} + \text{RolePriority} + \text{SquadBonus} + \text{PolicyBonus} - \text{CounterRisk}$$
   - **Công thức chấm điểm kỹ năng (`ActionScoreBreakdown`):**
     $$\text{TotalScore} = \text{BaseUtility} + \text{TacticalFit} + \text{Urgency} + \text{PolicyBonus}$$
   - Toàn bộ điểm thành phần được ghi vào `AIDecisionRecord` để hiển thị minh bạch trên bảng **F3 Explainable AI Debugger**.

---

## 3. TỔNG HỢP TIẾN ĐỘ ĐÃ HOÀN THÀNH (TUẦN 1 – TUẦN 8)

Toàn bộ tầng **Core Logic, AI Engine, Roguelite Campaign, Data SO, Save/Load JSON, Sàn đấu 2D cơ bản, Diễn hoạt theo sự kiện, Endless Mode & Âm thanh Procedural** đã hoàn thành **100%**:

### ✅ Tuần 1 (Milestone 1): Nền tảng Core Battle System (Pure C#)
- Thiết lập kiến trúc 3 tầng `.asmdef` (`CrownlessWarband.Core`, `CrownlessWarband.Data`, `CrownlessWarband.Presentation`).
- Hoàn thiện `BattleEnums.cs`, `SkillModel.cs`, `UnitState.cs`, `BattleState.cs`, `CombatEvent.cs`, `CombatResolver.cs`.
- Đảm bảo quy tắc trừ `Shield` trước `HP`, cộng hưởng `Marked (+25%)`, `ExecuteDamage (+50% khi HP < 40%)`, `Guardian Interception` và quy tắc chặn đánh cận chiến hàng sau.

### ✅ Tuần 2 (Milestone 2): Bộ Điều phối Lượt, AI 2 Tầng & F3 Explainable Debugger
- Hoàn thiện `TurnManager.cs`, `AIDecisionRecord.cs`, `TargetEvaluator.cs`, `ActionEvaluator.cs`, `SquadAI.cs`, `UnitAI.cs`.
- Xây dựng giao diện thử nghiệm `BattleSandboxController.cs` và bảng giải thích tư duy AI `AIDebugOverlayUI.cs` (bật/tắt bằng phím `F3`).
- Kiểm chứng thành công kịch bản mẫu 5v5 trong GDD Mục 53.

### ✅ Tuần 3 (Milestone 3): Vòng lặp Chiến dịch Roguelite, AI Policy, Relics & Save JSON
- Hoàn thiện `UnitAIPolicy.cs` (cho phép người chơi chỉnh `BehaviorPreset`, `TargetPriorityRule`, `SkillConditionType`).
- Hoàn thiện `RelicAndRewardModel.cs` (4 Di vật: `BloodContract`, `TacticalCore`, `EmergencyProtocol`, `ChainRelay` và các gói nâng cấp chỉ số).
- Hoàn thiện `CampaignModel.cs`, `CampaignGenerator.cs` (sinh bản đồ 5 tầng phân nhánh cố định theo `Seed`) và `SaveSystem.cs` (lưu/nạp `crownless_run.save.json` bằng `Newtonsoft.Json`).

### ✅ Tuần 4 (Milestone 4 & QA Audit): Modifiers, Boss 3 Pha, Data SO, Batch Simulator & Kiểm thử QA
- Bổ sung 4 `WorldModifierType` (`AcidRain`, `BloodMoon`, `Overcharge`, `NoHealing`) và 3 `EnemyAffixType` (`Armored`, `Berserker`, `Regenerator`).
- Triển khai Boss 3 giai đoạn **The Warden** (`Phase 1` khiên dày $\rightarrow$ `Phase 2 <= 65% HP` kích hoạt *Formation Breaker* +60 Shield & 100 Energy tung *Earthshatter Sweep* $\rightarrow$ `Phase 3 <= 35% HP` kích hoạt *Berserk Overdrive* +15 ATK, +5 SPD, +35% DMG, ép đội địch sang `FocusFire`).
- Tạo `SkillDataSO.cs` và `UnitDataSO.cs`.
- Xây dựng `AIBatchSimulator.cs` (chạy 100 trận 5v5 trong $\sim 620\text{ ms}$) và `QAAndBalanceAuditor.cs` đạt `[PASS]` toàn bộ Invariants, Save/Load JSON và tỷ lệ thắng chuẩn ($74.0\%\text{ Win Rate}$).

### ✅ Tuần 5 (Milestone 5): Sàn đấu 2D, Đội hình $3 \times 3$ & Tương tác Xếp vị trí
- Xây dựng `FormationSlotView.cs`, `UnitVisualView.cs` và `BattlefieldGridView.cs` (18 ô $3 \times 3$ cho 2 phe, procedural fallback sprite sắc nét theo màu Role, thanh HP/Shield/Energy & nhãn trạng thái).
- Hỗ trợ click chọn nhân vật đồng bộ với `AIDebugOverlayUI.cs` và đổi vị trí đứng (`Formation Swap`) trong pha `Planning` (Turn 1) kèm đánh giá lại `SquadAI`.
- Tái cấu trúc `BattleSandboxController.cs` theo bố cục 3 cột (`28% - 45% - 27%`) nhường trung tâm cho Sàn đấu 2D.

### ✅ Tuần 6 (Milestone 6): Diễn hoạt Chiến đấu Thời gian thực & Floating Combat Text
- Xây dựng `FloatingTextView.cs` với **Object Pool** 40 phần tử struct (zero-GC), tự chống chồng chữ và hiển thị Banner chiến thuật `SquadAI` phía trên sàn đấu.
- Bổ sung hệ thống **Procedural Mathematical Tweens** ngay trong `UnitVisualView.cs` (Attack Lunge, Guardian Intercept Slide, Hit Shake, Scale Pulse, Color Flash, Acting Turn Ring vàng) mà không cần thư viện ngoài.
- Xây dựng `CombatEventPresenter.cs` tiêu thụ `BattleState.EventQueue` với 4 mức tốc độ (`1x`, `2x`, `4x`, `Instant`), nút `Pause/Resume`, `Step Next Event`, `Skip` và chế độ `Auto-Follow` cho `F3 AI Debug`.

### ✅ Tuần 7 (Milestone 7): Chiều sâu GDD (Mission Objectives, Biomes, Skill Modes & Data SO)
- Mở rộng 3 `MissionType` (`Assault`, `Survival`, `Elimination` với huy hiệu `[VIP]`) và 3 `BiomeType` (`RuinedCity`, `IndustrialZone`, `LabComplex` kèm đổi màu nền Camera) trong Core & Presentation.
- Cập nhật `TargetEvaluator` (ưu tiên VIP $+45\text{f}$) và `SquadAI` (`FocusFire` với VIP / ưu tiên `ProtectBackline` khi `Survival`); bổ sung node `Shop` trên `CampaignMap`.
- Hoàn thiện chuyển đổi `Auto / SemiAuto / Manual` cho từng kỹ năng đặc biệt + nút `[CAST]` chọn mục tiêu thủ công; tạo `ContentDatabaseSO.cs` và menu `Tools -> Crownless Warband -> Generate Default ScriptableObject Assets`.

### ✅ Tuần 8 (Milestone 8): Âm thanh Procedural, Endless Mode, Adaptive Profile & Turn Order Bar
- Xây dựng `BattleAudioManager.cs` tự tổng hợp 9 sóng âm `AudioClip` (22050 Hz) bằng toán học kèm nút `[SFX: ON/OFF]`.
- Bổ sung `PlayerBehaviorProfile.cs` theo dõi lối chơi và kích hoạt **Adaptive Difficulty / Counter-Strategies** (tăng `Assassin`/Affix khi người chơi thiên về Ranged/Buff), đồng thời mở **Endless Mode (`Floor 5+`)** sinh Boss mỗi 3 tầng và tự cuộn bản đồ chiến dịch.
- Bổ sung `Turn Order Bar` (`Speed Queue`) phía trên sàn đấu, đồng bộ **pre-turn display snapshot** và xử lý ẩn/mờ dứt điểm các unit đã tử trận (`UnitDied`).

---

## 4. KẾ HOẠCH CHI TIẾT 4 TUẦN CÒN LẠI (TUẦN 9 – TUẦN 12)

> **Định hướng giai đoạn cuối:** Dành trọn **3 tuần (Tuần 9, 10, 11)** để nâng cấp toàn diện **Game Flow + Giao diện `uGUI Canvas` (Tuần 9) $\rightarrow$ Bản đồ Đường đi Có Nhân vật + Tạo hình Nhân vật & Đấu trường 2D (Tuần 10) $\rightarrow$ VFX Chiến đấu/Juice & Hoàn thiện (Tuần 11)** bằng bộ skill `.agents/skills/ux-design`, `.agents/skills/ux-review`, `.agents/skills/visual-polish-2d` và `ui-ugui`. Toàn bộ **giao diện Sandbox/Tab thử nghiệm hiện tại sẽ được đưa vào Debug Mode**, và **Bộ Phím tắt Demo Hội đồng (`F1–F7`), QA Audit cuối & Đóng gói `.exe` thực hiện ở Tuần 12**.

---

### 📅 TUẦN 9 (Milestone 9 — Đồ họa Tuần 1/3): Game Flow (Main Menu, Settings, Debug Mode) & HUD Chiến đấu `uGUI Canvas`
**Mục tiêu:** Chuyển từ màn hình Sandbox sang cấu trúc game hoàn chỉnh (**Main Menu $\rightarrow$ Map/Battle $\rightarrow$ Settings**), gom toàn bộ các tab công cụ hiện tại vào **Debug Mode** ẩn gọn, và dựng mới HUD chiến đấu toàn màn hình bằng `uGUI Canvas` theo bố cục RPG hiện đại (tham chiếu ảnh mẫu: *Top-Left Status, Top-Center Turn Banner, Top-Right Icon Controls, Bottom-Left Hero Portrait Cards, Bottom-Right Circular Skill Orbs*).

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C# & Skill `ux-design` + `ui-ugui` + `ux-review`):
1. **Màn hình Main Menu, Cài đặt (Settings) & Quản lý Luồng Game (`MainMenuView.cs` / `GameShellController.cs`):**
   - **Main Menu:** Nút **Chơi mới** (`New Run` kèm nhập Seed tùy chọn), **Tiếp tục** (`Continue` tự động kiểm tra và nạp từ `crownless_run.save.json`), **Cài đặt** (`Settings`), **Thoát** (`Quit`), và 1 nút/phím mở nhanh **Debug Sandbox**.
   - **Bảng Cài đặt (Settings Modal — mở được cả ở Main Menu và nút ⚙️ trong trận):** Chỉnh Âm thanh (`SFX ON/OFF`, Âm lượng SFX), Tốc độ diễn hoạt mặc định (`1x / 2x / 4x`), Độ phân giải & Chế độ màn hình (`Fullscreen Window` / `Windowed`).
2. **Tách Toàn bộ Giao diện Tab Hiện tại vào Debug Mode:**
   - Gom các tab hiện có (`Battle Simulator`, `Campaign Map Debug`, `AI Batch Simulator`, `QA Auditor`, `Combat Log` chi tiết, thông số thô) vào **Debug Overlay Drawer** bật/tắt bằng phím `F8` (hoặc nút icon `[DBG]` ở góc trên phải); bình thường khi chơi game các tab này ẩn hoàn toàn để nhường toàn bộ màn hình cho đấu trường.
   - Phím **`F3` (Explainable AI Debugger)** giữ nguyên chức năng bật/tắt bảng giải thích công thức AI trượt gọn ở cạnh phải khi cần thuyết trình hoặc kiểm tra.
3. **HUD Chiến đấu Toàn màn hình `uGUI Canvas` (`BattleHudView.cs` — Reference `1920x1080`):**
   - **Góc Trên-Trái (Top-Left):** Huy hiệu trạng thái gọn (Loại nhiệm vụ `Assault/Survival/Elimination`, `Floor/Turn`, `Biome`, `Relics`).
   - **Giữa Trên (Top-Center):** Banner **`Round / Turn`** sắc nét (kèm thanh máu Boss *The Warden* có vạch ngưỡng `65% / 35%` ngay bên dưới khi đấu Boss và thông báo ngắn `SquadAI`).
   - **Góc Trên-Phải (Top-Right):** Hàng nút icon nhỏ gọn (`1x/2x/4x`, `Pause/Step`, `Settings ⚙️` — chứa tùy chỉnh SFX bên trong, `F3 AI`, `F8 Debug`).
   - **Góc Dưới-Trái (Bottom-Left):** Dãy **Thẻ Chân dung Nhân vật Đồng minh (Ally Portrait Cards)** có viền phát sáng theo nhân vật đang tới lượt/đang chọn, thanh `HP` & `Energy/Shield` nằm gọn ngay dưới mỗi thẻ; click thẻ để chọn nhân vật hoặc mở nhanh bảng chỉnh `AI Policy`.
   - **Góc Dưới-Phải (Bottom-Right):** Cụm **Nút Kỹ năng Hình tròn (Circular Skill Orbs)** hiển thị kỹ năng của nhân vật, huy hiệu chế độ `Auto / SemiAuto / Manual`, nút `[CAST]` và nút bắt đầu lượt `[SPACE]`.
   - **Post-Battle Rewards, Shop & Rest Modal:** Dựng bằng thẻ bài `uGUI Canvas` đồng bộ phong cách thị giác mới.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 9):
1. Nếu Unity hỏi cài **TMP Essentials** khi tạo UI TextMeshPro, bấm **Import TMP Essential Resources** (hoặc vào `Window -> TextMeshPro -> Import TMP Essential Resources`).
2. Mở `Assets/Scenes/SampleScene.unity`, bấm **Play** kiểm tra luồng `Main Menu -> Chơi mới / Tiếp tục / Cài đặt`, kiểm tra HUD chiến đấu ở cả **`1920x1080`** và **`1280x720`**, và bấm `F8` / `F3` xác nhận Debug Mode bật/tắt sạch sẽ.

---

### 📅 TUẦN 10 (Milestone 10 — Đồ họa Tuần 2/3): Bản đồ Chiến dịch Có Đường đi & Nhân vật Di chuyển + Tạo hình Nhân vật & Sàn đấu 2D
**Mục tiêu:** Thay thế hoàn toàn kiểu chọn node bằng nút bấm chữ hiện tại bằng **Bản đồ 2D vẽ đầy đủ có con đường quanh co, nhân vật của người chơi đi dọc theo con đường và các lựa chọn (Nodes) đặt trực tiếp trên đường**; đồng thời nâng cấp tạo hình nhân vật 2D & phông nền đấu trường toàn màn hình hài hòa với HUD mới.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C# & Skill `visual-polish-2d` + `ux-design`):
1. **Bản đồ Chiến dịch 2D Có Đường đi & Nhân vật Bước Trên Đường (`CampaignWorldMapView.cs` & `MapNodeMarkerView.cs`):**
   - Vẽ cảnh quan bản đồ 2D hoàn chỉnh theo từng `BiomeType` (`RuinedCity`, `IndustrialZone`, `LabComplex`) và **con đường quanh co nối liền các nhánh Node** (`Floor 0` $\rightarrow$ `Floor 4+` Endless, hỗ trợ cuộn/pan mượt mà theo tiến trình tầng).
   - Các **Node lựa chọn** (`Battle`, `Elite`, `Shop`, `Rest`, `Boss`) được đặt trực tiếp thành các điểm mốc trên con đường (có biểu tượng trực quan, trạng thái đã đi / có thể chọn / bị khóa và thẻ preview thông tin khi rê chuột), thay thế hoàn toàn giao diện nút bấm bảng thô.
   - Hiển thị **Nhân vật Đại diện của Người chơi (Player Map Avatar)** đứng tại node hiện tại; khi người chơi click chọn node tiếp theo, nhân vật tự động **di chuyển dọc theo đường nối** tới node đó trước khi mở trận chiến / Shop / Rest.
2. **Tạo hình Nhân vật 2D & Đấu trường Toàn màn hình (`ProceduralUnitArtFactory.cs`, `UnitVisualView.cs`, `BattlefieldGridView.cs`):**
   - Mở rộng sân đấu 2D chiếm trọn vùng trung tâm toàn màn hình giống ảnh mẫu: dựng phông nền môi trường có chiều sâu cho 3 Biomes + sàn đấu đá/đất tinh tế tích hợp lưới $3 \times 3$ (bỏ cảm giác khung debug khối vuông).
   - Thay ô vuông đơn sắc bằng tạo hình 2D có viền giáp, vũ khí/biểu tượng đặc trưng cho 5 Class (`Guardian`, `Vanguard`, `Ranger`, `Mage`, `Medic`), kẻ địch và Boss *The Warden*, kèm bóng đổ elip dưới chân (`Drop Shadow`), vẫn ưu tiên dùng sprite PNG nếu người dùng kéo vào `RoleSprites`.
   - Làm gọn thanh trạng thái trên đầu mỗi nhân vật giống ảnh mẫu (viên kim cương chỉ vai trò/cấp + thanh HP/Shield/Energy sát gọn).
   - Kết hợp `Global Light 2D` trong URP để tạo ánh sáng môi trường tách lớp nhân vật và nền.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 10):
1. Trong `Assets/Scenes/SampleScene.unity`, kiểm tra `Main Camera` (Orthographic Size `5.5`) và `Global Light 2D` đảm bảo bật URP 2D bình thường.
2. Bấm **Play** $\rightarrow$ **Chơi mới**, đi qua Bản đồ 2D xem nhân vật di chuyển trên con đường qua các Node và vào trận xem tạo hình 5 Class + Boss + 3 Biomes *(tùy chọn: bạn có thể kéo bộ Sprite PNG nhân vật hoặc ảnh nền riêng vào Inspector nếu muốn dùng art vẽ tay)*.

---

### 📅 TUẦN 11 (Milestone 11 — Đồ họa Tuần 3/3): Hiệu ứng Chiến đấu VFX/Juice 2D, Micro Camera Shake & Kiểm duyệt `/ux-review` Toàn diện
**Mục tiêu:** Bổ sung lớp hiệu ứng chiến đấu VFX/Juice cho tạo hình nhân vật 2D mới, thanh máu tụt mượt và kiểm duyệt toàn bộ luồng từ Main Menu $\rightarrow$ Map đường đi $\rightarrow$ Đấu trường $\rightarrow$ Debug Mode đạt chuẩn chất lượng.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C# & Skill `visual-polish-2d` + `ux-review`):
1. **Hệ thống VFX Pooling (`CombatVfxManager.cs` — `CrownlessWarband.Presentation.BattleView`):**
   - Tạo **Object Pool** hiệu ứng 2D procedural (zero-GC trong lúc chiến đấu):
     - **Vệt chém (`Slash Arc`)** khi cận chiến (`Vanguard`, `Guardian`, `The Warden`).
     - **Tia đạn năng lượng (`Projectile Trail`)** bay từ `Ranger` / `Mage` tới mục tiêu.
     - **Sóng xung kích (`Shockwave Ring`)** cho chiêu AoE (`Earthshatter Sweep`, `Arcane Tempest`), `Stun`, `Intercept` và hiệu ứng hồi máu / tạo khiên (`Heal Sparkle`, `Shield Dome`).
     - **Bùng nổ chuyển Phase Boss** khi *The Warden* kích hoạt Phase 2 (`<= 65%`) và Phase 3 (`<= 35%`).
2. **Thanh Máu Tụt Mượt & Micro Camera Shake (`UnitVisualView.cs` & `CombatEventPresenter.cs`):**
   - Bổ sung lớp **Damage Trail** trên thanh HP (phần máu mất đi tụt trễ nhịp màu trắng/cam giúp nhìn rõ độ lớn sát thương vừa nhận).
   - Bổ sung **Micro Camera Shake** nhẹ khi trúng đòn Chí mạng / `Execute`, `Ultimate` hoặc Boss chuyển Phase.
3. **Nghiệm thu Toàn diện bằng Skill `/ux-review`:**
   - Kiểm tra toàn bộ 5 trụ cột của `.agents/skills/ux-review/SKILL.md` xuyên suốt `Main Menu -> Campaign World Map -> Active Battle HUD -> Reward/Shop -> Debug Mode (F8/F3)` ở cả `1920x1080` và `1280x720`, sửa triệt để tới khi đạt **`APPROVED`**.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 11):
1. Trong `SampleScene.unity`, gắn `CombatVfxManager` lên `BattlefieldVisualizer` (hoặc kiểm tra tự động khởi tạo).
2. Bấm **Play**, kiểm tra trận đấu ở tốc độ `1x` và `2x` (đặc biệt pha *Guardian* đỡ đòn và Boss chuyển Phase) để chắc chắn VFX rõ nét, không che mất thanh máu hay các nút HUD góc dưới.

---

### 📅 TUẦN 12 (Milestone 12): Bộ Phím tắt Demo Hội đồng (`F1–F7`), Rà soát QA Cuối & Đóng gói `.exe`
**Mục tiêu:** Chốt toàn bộ kịch bản trình diễn bảo vệ đồ án bằng 1 phím bấm, vượt qua 100% bài kiểm thử tự động `QAAndBalanceAuditor` (`[TEST 1..4]`), đạt duyệt `/ux-review` lần cuối và xuất file `.exe` chạy độc lập.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C#):
1. **Bộ Phím tắt Kịch bản Bảo vệ Đồ án (`DemoScenarioController` tích hợp trong `BattleSandboxController`):**
   - Thiết lập dãy phím tắt nạp nhanh kịch bản chứng minh AI ngay lập tức trên nền đồ họa & UI mới:
     - **`F1` — Kịch bản Chuẩn GDD Mục 53 (5v5 Classic):** Tái hiện chính xác pha `Enemy Assassin` lao vào `Ranger` $\rightarrow$ `Player SquadAI` bật `ProtectBackline` $\rightarrow$ `Guardian` nhảy ra chắn đòn, `Mage` Stun Assassin, `Medic` hồi máu.
     - **`F2` — Chứng minh Enemy AI Adaptation:** Nạp trận đấu đang ở chiến thuật `BacklineDive`, hạ gục Assassin địch để chứng minh `Enemy SquadAI` lập tức chuyển sang `DirectAssault` dồn sát thương vào `Guardian`.
     - **`F3` — Bật/Tắt Explainable AI Debugger:** Hiển thị bảng công thức chấm điểm `TargetScoreBreakdown` và `ActionScoreBreakdown` chi tiết từng con số.
     - **`F4` — Chứng minh Sức mạnh của Player AI Policy:** So sánh trực tiếp cùng 1 đội hình nhưng khi đổi Policy của `Ranger` và `Medic` thì chuyển từ thua sang thắng áp đảo.
     - **`F5` — Kịch bản Boss 3 Pha (*The Warden*):** Đưa máu Boss xuống sát ngưỡng $65\%$ và $35\%$ để trình diễn trực tiếp hiệu ứng chuyển Phase (*Formation Breaker* & *Berserk Overdrive*), đồng thời nối thông node Boss `F4_L0` sang `Endless Mode`.
     - **`F6` — Kịch bản Nhiệm vụ `Elimination` & `Survival` + World Modifiers.**
     - **`F7` — Mở nhanh Headless AI Batch Simulator & QA Audit:** Chạy 100 trận đấu trong $\sim 0.6\text{ giây}$ và xuất báo cáo `[PASS]` toàn diện.
2. **Hoàn thiện & Chạy rà soát `QAAndBalanceAuditor.cs` (`[TEST 1..4]`):**
   - Kiểm tra toàn bộ bất biến trạng thái cho cả các tính năng mới (`Survival`, `Elimination`, `Endless Mode`, `PlayerBehaviorProfile`), đảm bảo 0 lỗi biên dịch, 0 cảnh báo `NullReferenceException` và tốc độ giả lập giữ vững $< 10\text{ ms/trận}$.
3. **Kiểm duyệt `/ux-review` lần cuối:** Xác nhận toàn bộ 7 kịch bản `F1–F7` hiển thị sắc nét, không lỗi layout trên uGUI Canvas.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 12):
1. **Kiểm tra QA tự động trên Editor:**
   - Bấm Play trong Unity, bấm `F7` (hoặc mở tab `AI Batch Simulator`) $\rightarrow$ chạy **`[RUN FULL QA & BALANCE AUDIT]`** và chụp màn hình kết quả `[PASS]` đưa vào báo cáo quyển đồ án.
2. **Đóng gói Bản dựng Windows PC (`.exe`) trên Unity 6:**
   - Vào menu `File -> Build Profiles` (hoặc `File -> Build Settings...` trên bản cũ).
   - Chọn **Windows**, bấm **Add Open Scenes** để đảm bảo `Assets/Scenes/SampleScene.unity` nằm trong danh sách build (Architecture: `Intel 64-bit` / `x86_64`).
   - Bấm vào **Player Settings...**:
     - `Company Name`: `HUST_20225138`
     - `Product Name`: `The Crownless Warband`
     - `Fullscreen Mode`: `Fullscreen Window` (Độ phân giải mặc định `1920 x 1080`, bật `Resizable Window`).
   - Bấm **Build**, chọn thư mục `D:\The_Crownless_Warband\Builds\TheCrownlessWarband.exe`.
3. **Chạy thử bản `.exe` độc lập & Quay video dự phòng:**
   - Mở trực tiếp file `TheCrownlessWarband.exe`, bấm thử lần lượt các phím `F1` $\rightarrow$ `F7` và `SPACE` để đảm bảo mọi hiệu ứng, âm thanh và VFX chạy mượt mà.
   - Quay lại 1 video ngắn (3–5 phút) trình diễn bộ `F1–F7` làm tư liệu dự phòng an toàn khi bảo vệ đồ án.

---

## 5. BẢNG TỔNG HỢP TRẠNG THÁI CÁC HẠNG MỤC ĐỒ ÁN (GDD MỤC 58)

| Phân loại GDD | Hạng mục chức năng | Trạng thái hiện tại | Tuần hoàn thiện |
| :--- | :--- | :---: | :---: |
| **Must Have** | Turn-based Battle (3 Phases) & Formation Grid ($3 \times 3$) | ✅ Hoàn thành | Tuần 1–2 & Tuần 5 |
| **Must Have** | 5 Class nhân vật, Basic Attack & Skills, Interception | ✅ Hoàn thành | Tuần 1–2 |
| **Must Have** | Utility AI (`TargetEvaluator` + `ActionEvaluator`) | ✅ Hoàn thành | Tuần 2 |
| **Must Have** | Squad AI (2 phe) & Mid-Battle Adaptation (Chống Thrashing) | ✅ Hoàn thành | Tuần 2 & Tuần 4 |
| **Must Have** | Player-Configurable AI Policy & Skill Auto/Semi/Manual | ✅ Hoàn thành | Tuần 3 & Tuần 7 |
| **Must Have** | Campaign Map 5 tầng theo Seed, Reward (1 trong 3), 4 Relics & Shop | ✅ Hoàn thành | Tuần 3 & Tuần 7 |
| **Must Have** | Save / Load JSON (`Newtonsoft.Json`) | ✅ Hoàn thành | Tuần 3 |
| **Must Have** | Sàn đấu 2D trực quan & Diễn hoạt Procedural theo Sự kiện | ✅ Hoàn thành | Tuần 5–6 |
| **Should Have** | Explainable AI Debugger (`Phím F3`) & Combat Log chi tiết | ✅ Hoàn thành | Tuần 2 & Tuần 6 |
| **Should Have** | Boss Đa giai đoạn (*The Warden* — 3 Phases) | ✅ Hoàn thành | Tuần 4 |
| **Should Have** | World Modifiers (4 loại) & Enemy Affixes (3 loại) | ✅ Hoàn thành | Tuần 4 |
| **Should Have** | 3 Loại Nhiệm vụ (`Assault`, `Survival`, `Elimination`) & 3 Biomes | ✅ Hoàn thành | Tuần 7 |
| **Could Have** | Headless AI Batch Simulator (100 trận) & QA Automated Auditor | ✅ Hoàn thành | Tuần 4 *(Mở rộng `[TEST 4]` Tuần 12)* |
| **Could Have** | Endless Mode, Player Behavior Profile, Turn Order Bar & Âm thanh (SFX) | ✅ Hoàn thành | Tuần 8 |
| **UX/UI Overhaul** | Main Menu, Settings, Debug Mode (`F8/F3`) & HUD Chiến đấu kiểu RPG (`uGUI Canvas`) | ⏳ Kế hoạch | **Tuần 9** |
| **Visual Polish** | Bản đồ Đường đi 2D (nhân vật bước trên đường) + Tạo hình Nhân vật & Sàn đấu 2D | ⏳ Kế hoạch | **Tuần 10** |
| **Visual Polish** | Hiệu ứng Chiến đấu VFX Pooling, Damage Trail, Camera Shake & `/ux-review` | ⏳ Kế hoạch | **Tuần 11** |
| **Could Have / Release** | Bộ Phím tắt Demo Hội đồng (`F1–F7`), QA Audit Cuối & Build `.exe` | ⏳ Kế hoạch | **Tuần 12** |
