# The Crownless Warband (AI-Driven Tactical Roguelite)
> **Tài liệu Thiết kế Game (GDD), Kiến trúc Kỹ thuật & Kế hoạch Triển khai Đồ án (9 Tuần)**  
> **Sinh viên thực hiện:** Phạm Quốc Khánh — MSSV: 20225138 (Đại học Bách Khoa Hà Nội)  
> **Phiên bản:** 1.0 (Cập nhật Tiến độ Tuần 1–4 & Lộ trình Hoàn thiện Tuần 5–9)  
> **Thể loại:** Tactical Roguelite / Turn-based Auto Combat / Squad Strategy  
> **Nền tảng:** PC | **Engine:** Unity 2D (C#) | **Chế độ:** Single-player  

---

## 0. HƯỚNG DẪN DÀNH CHO AI TRỢ LÝ LẬP TRÌNH (SYSTEM PROMPT FOR AI ASSISTANTS)
*Nếu bạn là một AI Coding Agent đang đọc file này để hỗ trợ viết code, thiết kế dữ liệu hoặc debug cho dự án **The Crownless Warband**, hãy tuân thủ tuyệt đối các nguyên tắc bất biến sau:*

1. **Tách biệt Logic và Hiển thị (Humble Object Pattern — Phân tách 3 tầng bằng `.asmdef`):**
   - **`CrownlessWarband.Core` (`Assets/_Project/Scripts/Core/`):** Thuần C# 100% (Pure C#, tuyệt đối không kế thừa `MonoBehaviour`, không dùng `ScriptableObject`, không gọi `Debug.Log`). Trận đấu diễn ra bằng cách cập nhật `BattleState`, ghi vào `BattleState.CombatLog` và đẩy các `CombatEvent` vào `BattleState.EventQueue`.
   - **`CrownlessWarband.Data` (`Assets/_Project/Scripts/Data/`):** Chứa các định nghĩa `ScriptableObject` (`UnitDataSO`, `SkillDataSO`) để thiết kế dữ liệu trên Unity Inspector và chuyển đổi sang đối tượng Pure C# lúc Runtime.
   - **`CrownlessWarband.Presentation` (`Assets/_Project/Scripts/Presentation/`):** Chứa các lớp `MonoBehaviour` đọc `BattleState` và tiêu thụ `EventQueue` để hiển thị đồ họa 2D, diễn hoạt `DOTween` và vẽ giao diện HUD / `F3 Explainable AI Debugger`.
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

## 3. TỔNG HỢP TIẾN ĐỘ ĐÃ HOÀN THÀNH (TUẦN 1 – TUẦN 4)

Toàn bộ tầng **Core Logic, AI Engine, Roguelite Campaign, Data SO, Save/Load JSON và QA Automated Testing** đã hoàn thành **100%** với 19 file C# hiện có:

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

---

## 4. KẾ HOẠCH CHI TIẾT 5 TUẦN CÒN LẠI (TUẦN 5 – TUẦN 9)

Mục tiêu của 5 tuần cuối là chuyển hóa toàn bộ phần lõi Pure C# đã hoàn chỉnh thành một **trò chơi 2D trực quan, sinh động trên Unity**, bổ sung đầy đủ các tính năng **Must Have / Should Have / Could Have** trong GDD và đóng gói bản dựng `.exe` sẵn sàng bảo vệ đồ án.

---

### 📅 TUẦN 5 (Milestone 5): Trực quan hóa Sàn đấu 2D, Đội hình $3 \times 3$ & Tương tác Xếp vị trí
**Mục tiêu:** Thay thế cảm giác "chơi trên bảng chữ" bằng một **Sàn đấu 2D (Battlefield Scene View)** nằm ở trung tâm màn hình, hiển thị trực quan 2 lưới đội hình $3 \times 3$, nhân vật 2D, thanh máu/giáp/năng lượng, icon trạng thái và cho phép đổi vị trí đứng (`Formation`) trước khi vào pha giao chiến.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C#):
1. **`FormationSlotView.cs` (`CrownlessWarband.Presentation.BattleView`):**
   - Hiển thị từng ô đứng `(TeamSide, RowType, Column)` trên không gian 2D (`SpriteRenderer` hoặc World UI).
   - Đổi màu viền ô theo hàng (`Front`: Đỏ/Cam, `Mid`: Vàng, `Back`: Xanh) và sáng lên khi người chơi chọn đổi vị trí nhân vật hoặc chọn mục tiêu kỹ năng `Manual`.
2. **`UnitVisualView.cs` (`CrownlessWarband.Presentation.BattleView`):**
   - Gắn với một `UnitState` cụ thể; tự động dựng Sprite nhân vật theo `UnitRole` & `TeamSide`, vẽ thanh `HP Bar` (xanh/đỏ), `Shield Bar` (xanh dương), `Energy Bar` (vàng) và các nhãn trạng thái nổi bật (`[STUN]`, `[MARK]`, `[PROTECT]`, `[PHASE 2/3]`, `[AFFIX]`).
   - Hỗ trợ sự kiện click chuột vào nhân vật trên sàn đấu 2D để:
     - Chọn xem ngay bảng phân tích điểm số của nhân vật đó trên **F3 AI Debug Inspector**.
     - Chọn nhân vật để đổi ô đứng (`Formation Swap`) trong pha `Planning` (Turn 1).
3. **`BattlefieldGridView.cs` (`CrownlessWarband.Presentation.BattleView`):**
   - Quản lý toàn bộ 18 ô lưới ($9$ ô Player bên trái, $9$ ô Enemy bên phải) và đồng bộ vị trí của mọi `UnitVisualView` theo `BattleSandboxController.CurrentState`.
   - Tự động khởi tạo (procedural fallback) các GameObject/SpriteRenderer 2D ngay bằng code nếu người dùng chưa kéo thả Prefab thủ công, đảm bảo bấm Play là thấy ngay sàn đấu 2D.
4. **Cập nhật nhẹ `BattleSandboxController.cs`:**
   - Thu gọn chiều rộng bảng điều khiển bên trái để nhường không gian chính giữa màn hình cho **Sàn đấu 2D** (Bố cục 3 cột chuẩn: *Trái: Điều khiển & Log* — *Giữa: Sàn đấu 2D* — *Phải: Bảng F3 AI Debug*).

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 5):
1. Mở scene chính (`Assets/_Project/Scenes/MainScene.unity` hoặc scene hiện tại bạn đang dùng).
2. Chọn `Main Camera` trong Hierarchy:
   - Chuyển `Projection` sang **Orthographic**, đặt `Size = 5.5`, `Position = (0, 0, -10)`.
   - Chỉnh màu nền `Background` sang tông tối chiến thuật (ví dụ: `#161A22`).
3. Tạo một Empty GameObject tên là `BattlefieldVisualizer` trong Hierarchy, gắn script `BattlefieldGridView` vào và kéo thả tham chiếu `BattleSandboxController` vào ô tương ứng trên Inspector.
4. *(Tùy chọn nâng cao thẩm mỹ)* Nếu bạn có bộ ảnh Sprite 2D riêng cho 5 Class (`Guardian`, `Vanguard`, `Ranger`, `Mage`, `Medic`) và Boss `The Warden`, kéo thả chúng vào danh sách `RoleSprites` trên Inspector của `BattlefieldGridView` (nếu để trống, code sẽ tự sinh hình khối 2D sắc nét theo màu Role).

---

### 📅 TUẦN 6 (Milestone 6): Diễn hoạt Chiến đấu Thời gian thực với DOTween & Floating Combat Text
**Mục tiêu:** Giải quyết bài toán quan trọng nhất của GDD Mục 10 & Mục 43: Biến hàng đợi `BattleState.EventQueue` (`Queue<CombatEvent>`) thành chuỗi diễn hoạt chiến đấu tuần tự theo thời gian thực bằng thư viện **DOTween**, giúp người xem nhìn rõ từng đòn đánh, pha đỡ đòn của Guardian, hiệu ứng Stun/Mark và sự kiện đổi chiến thuật của `SquadAI`.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C#):
1. **`FloatingTextView.cs` (`CrownlessWarband.Presentation.UI`):**
   - Hiển thị chữ nổi bay lên tại vị trí nhân vật khi có sự kiện: Sát thương (`-45` màu đỏ/cam), Chí mạng/Execute (`-78!` phóng to), Hồi máu (`+52` màu xanh lá), Tạo khiên (`+60 SHIELD` màu xanh lam), Khống chế (`STUNNED!` / `MARKED!`) và Đổi chiến thuật đội (`[SQUAD: PROTECT BACKLINE!]`).
   - Sử dụng `DOTween` (`DOMoveY`, `DOFade`, `DOPunchScale`) và cơ chế **Object Pool** (tái sử dụng đối tượng, không gọi `Instantiate`/`Destroy` liên tục trong trận).
2. **`CombatEventPresenter.cs` (`CrownlessWarband.Presentation.BattleView`):**
   - Đọc tuần tự từng `CombatEvent` từ `BattleState.EventQueue` thông qua Coroutine điều khiển nhịp độ.
   - Hỗ trợ 4 chế độ tốc độ diễn hoạt: **`1x` (Chuẩn để thuyết trình giải thích AI)**, **`2x` (Nhanh)**, **`4x` (Siêu tốc)** và **`Instant` (Chạy tức thì)**.
   - Kích hoạt diễn hoạt `DOTween` tương ứng trên `UnitVisualView`:
     - `UnitTurnStarted`: Sáng vòng chân nhân vật đang tới lượt và tự động focus nhân vật đó lên bảng `F3 AI Debug`.
     - `SkillUsed`: Nhân vật lao nhẹ về phía mục tiêu (`DOPunchPosition`), hiển thị tên kỹ năng.
     - `InterceptionTriggered`: `Guardian` trượt nhanh ra chắn trước mặt đồng đội đang được bảo vệ (`DOMove` tới vị trí chắn đòn rồi quay về).
     - `DamageDealt`: Mục tiêu rung lắc (`DOShakePosition`), nháy đỏ và cập nhật thanh HP/Shield mượt mà.
     - `UnitDied`: Nhân vật mờ dần (`DOFade`) và đổi trạng thái gục ngã.
3. **Cập nhật `CrownlessWarband.Presentation.asmdef` & `BattleSandboxController.cs`:**
   - Thêm nút chọn tốc độ `Playback Speed: [1x] [2x] [4x] [Instant]` và nút `[Pause / Step Next Event]` để bạn có thể **dừng hình ngay giữa lượt** khi đang thuyết trình trước Hội đồng.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 6):
1. Kiểm tra cài đặt **DOTween**:
   - Trên thanh menu Unity, vào `Tools -> Demigiant -> DOTween Utility Panel` $\rightarrow$ bấm **Setup DOTween...** $\rightarrow$ bấm **Apply** để Unity tạo các file Assembly Definition của DOTween.
   - Click vào file `Assets/_Project/Scripts/Presentation/CrownlessWarband.Presentation.asmdef` trong cửa sổ Project, tại mục **Assembly Definition References**, kiểm tra đã có `DOTween.Modules` (nếu Agent chưa thêm hoặc Unity yêu cầu Apply).
2. Trong `MainScene`, gắn component `CombatEventPresenter` vào GameObject `BattlefieldVisualizer` và kéo tham chiếu `BattlefieldGridView` + `BattleSandboxController` vào Inspector.
3. Bấm **Play**, nhấn `SPACE` ở tốc độ `1x` để kiểm tra hiệu ứng nhân vật lao lên đánh, Guardian nhảy ra đỡ đòn và chữ sát thương bay lên.

---

### 📅 TUẦN 7 (Milestone 7): Hoàn thiện Chiều sâu GDD (Mission Objectives, Biomes, Skill Modes & Data-Driven SO)
**Mục tiêu:** Lấp đầy toàn bộ các yêu cầu **Must Have & Should Have** còn lại trong GDD (Mục 13, 30, 32, 37, 47) để hệ thống chiến dịch và AI đa dạng đúng chuẩn thiết kế.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C#):
1. **Mở rộng 3 Loại Nhiệm vụ (`MissionType` — GDD Mục 37) & 3 Khu vực (`BiomeType` — GDD Mục 32):**
   - Cập nhật `BattleEnums.cs`, `BattleState.cs` và `TurnManager.cs` hỗ trợ 3 loại nhiệm vụ chiến đấu:
     1. `Assault`: Tiêu diệt toàn bộ kẻ địch (mặc định).
     2. `Survival`: Sống sót qua $N$ lượt (ví dụ: trụ vững 6 lượt trước đợt tấn công dữ dội của địch là Thắng).
     3. `Elimination`: Hạ gục 1 mục tiêu Chỉ huy (`IsPriorityObjective = true`) của địch để lập tức giành chiến thắng mà không cần diệt hết lính phụ.
   - Cập nhật `TargetEvaluator.cs` & `SquadAI.cs`:
     - Khi nhiệm vụ là `Elimination`, AI tự động cộng trọng số `StrategicValue` lớn lên mục tiêu VIP của địch.
     - Khi nhiệm vụ là `Survival`, `SquadAI` ưu tiên chuyển sang `Defensive` / `ProtectBackline` khi máu đồng minh giảm để câu giờ chiến thắng.
   - Bổ sung 3 `BiomeType` (`RuinedCity` - Thành phố đổ nát, `IndustrialZone` - Khu công nghiệp, `LabComplex` - Khu thí nghiệm) vào `CampaignModel.cs` & `CampaignGenerator.cs`, kèm lựa chọn `Shop / Event` đa dạng hơn.
2. **Hoàn thiện Chế độ Kỹ năng (`Auto` / `SemiAuto` / `Manual` — GDD Mục 13):**
   - Cho phép người chơi click đổi trực tiếp chế độ `Auto` $\leftrightarrow$ `SemiAuto` $\leftrightarrow$ `Manual` của từng kỹ năng đặc biệt (`Skill1`, `Ultimate`) trên bảng điều khiển, và cho phép chọn mục tiêu chỉ định khi kích hoạt kỹ năng `Manual` trong pha `Planning`.
3. **Hoàn thiện Hệ thống `ScriptableObject` (`ContentDatabaseSO.cs` & Editor Menu):**
   - Tạo `ContentDatabaseSO.cs` trong `CrownlessWarband.Data` và một script tiện ích trong `Editor` giúp tự động sinh ra các file `.asset` mẫu (`Guardian_SO`, `Vanguard_SO`, `Ranger_SO`, `Mage_SO`, `Medic_SO`, `TheWarden_SO` và bộ kỹ năng) chỉ bằng 1 cú click menu trên Unity Editor.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 7):
1. Trên thanh menu Unity Editor, bấm vào menu tự động (do Agent tạo): `Tools -> Crownless Warband -> Generate Default ScriptableObject Assets`.
   - Kiểm tra thư mục `Assets/_Project/ScriptableObjects/` sẽ xuất hiện đầy đủ các file `.asset` của Nhân vật, Quái, Boss và Kỹ năng (rất quan trọng để mở lên trình diễn tính chất **Data-Driven Architecture** khi thầy cô hỏi).
2. Kéo file `DefaultContentDatabase.asset` vào ô `Content Database` trên Inspector của `BattleSandboxController`.
3. Chạy thử 1 Run trên Campaign Map qua các node có nhiệm vụ `Survival` và `Elimination` để quan sát AI tự thay đổi mục tiêu ưu tiên.

---

### 📅 TUẦN 8 (Milestone 8): Âm thanh (SFX), Endless Mode, Player Behavior Profile & Đánh bóng UI/UX
**Mục tiêu:** Bổ sung các tính năng điểm cộng nâng cao (**Could Have** trong GDD Mục 40, 41, 43) và hoàn thiện hiệu ứng âm thanh, thanh thứ tự lượt đi (`Turn Order Bar`) để trải nghiệm game tròn trịa, chuyên nghiệp.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C#):
1. **Hệ thống Âm thanh Tự chủ (`BattleAudioManager.cs` — `CrownlessWarband.Presentation.BattleView`):**
   - Phát âm thanh phản hồi cho mọi sự kiện chiến đấu: Đòn đánh thường, Kỹ năng đặc biệt, Chí mạng/Execute, Tạo khiên, Hồi máu, Làm choáng (`Stun`), Guardian đỡ đòn (`Intercept`), Boss chuyển Phase và Chiến thắng/Thất bại.
   - **Tích hợp bộ tổng hợp âm thanh Procedural (Retro/Tactical Synth AudioClip):** Tự động tạo sóng âm `AudioClip` ngay trong bộ nhớ nếu bạn chưa gắn file `.wav`/`.mp3` bên ngoài, giúp game **có âm thanh chiến đấu sống động ngay lập tức 100%** mà không lo thiếu asset.
2. **Endless Mode & Hồ sơ Hành vi Người chơi (`PlayerBehaviorProfile.cs` — GDD Mục 40 & 41):**
   - Sau khi đánh bại Boss *The Warden* ở Tầng 4, mở nút **`[Enter Endless Mode (Floor 5+)]`** sinh các tầng tiếp theo với `Threat` và `Corruption` tăng dần cùng tổ hợp nhiều `EnemyAffix`.
   - Ghi nhận thống kê phong cách chỉ huy của người chơi (`PlayerBehaviorProfile`: Tỷ lệ dùng Ranged/AoE, số lần can thiệp `Manual Skill`, khuynh hướng chọn `BehaviorPreset`, thời lượng trận trung bình) và tích hợp cơ chế **Adaptive Encounter** (nếu người chơi lạm dụng Ranged DPS $>70\%$, các tầng sau tự động tăng tỷ lệ xuất hiện `Assassin/Diver` của địch).
3. **Nâng cấp Thanh Thứ tự Tốc độ (`TurnOrderBarUI`) & Bố cục HUD:**
   - Hiển thị thanh ngang phía trên sàn đấu 2D liệt kê thứ tự hành động của các nhân vật trong lượt theo `Speed` (`Initiative Queue`), giúp người chơi dễ dàng quyết định thời điểm can thiệp.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 8):
1. Trong cửa sổ **Game View** của Unity, chuyển độ phân giải từ `Free Aspect` sang **`Full HD (1920x1080)`** để cố định tỷ lệ khung hình chuẩn khi demo và quay video.
2. Thêm một GameObject `AudioManager` vào `MainScene`, gắn script `BattleAudioManager` (và tùy chọn kéo thả các file nhạc nền/SFX `.wav` của bạn vào Inspector nếu muốn thay thế âm thanh synth mặc định).
3. Chơi thử qua Tầng 4 và bấm vào **Endless Mode** để kiểm tra độ mượt của giao diện.

---

### 📅 TUẦN 9 (Milestone 9): Hệ thống Phím tắt Demo Bảo vệ Đồ án (F1–F8), Kiểm thử QA Cuối & Đóng gói `.exe`
**Mục tiêu:** Đảm bảo buổi bảo vệ đồ án diễn ra hoàn hảo: Chỉ cần bấm 1 phím tắt là tái hiện ngay lập tức bất kỳ kịch bản AI nào mà Hội đồng chấm thi yêu cầu, vượt qua 100% bài test tự động và xuất file thực thi `.exe`.

#### 🤖 Phần việc do AI Coding Agent thực hiện (Code C#):
1. **Bộ Phím tắt Kịch bản Bảo vệ Đồ án (`DemoScenarioController` tích hợp trong `BattleSandboxController`):**
   - Thiết lập dãy phím tắt nạp nhanh kịch bản chứng minh AI ngay lập tức:
     - **`F1` — Kịch bản Chuẩn GDD Mục 53 (5v5 Classic):** Tái hiện chính xác pha `Enemy Assassin` lao vào `Ranger` $\rightarrow$ `Player SquadAI` bật `ProtectBackline` $\rightarrow$ `Guardian` nhảy ra chắn đòn, `Mage` Stun Assassin, `Medic` hồi máu.
     - **`F2` — Chứng minh Enemy AI Adaptation:** Nạp trận đấu đang ở chiến thuật `BacklineDive`, hạ gục Assassin địch để chứng minh `Enemy SquadAI` lập tức chuyển sang `DirectAssault` dồn sát thương vào `Guardian`.
     - **`F3` — Bật/Tắt Explainable AI Debugger:** Hiển thị bảng công thức chấm điểm `TargetScoreBreakdown` và `ActionScoreBreakdown` chi tiết từng con số.
     - **`F4` — Chứng minh Sức mạnh của Player AI Policy:** So sánh trực tiếp cùng 1 đội hình nhưng khi đổi Policy của `Ranger` và `Medic` thì chuyển từ thua sang thắng áp đảo.
     - **`F5` — Kịch bản Boss 3 Pha (*The Warden*):** Đưa máu Boss xuống sát ngưỡng $65\%$ và $35\%$ để trình diễn trực tiếp hiệu ứng chuyển Phase (*Formation Breaker* & *Berserk Overdrive*).
     - **`F6` — Kịch bản Nhiệm vụ `Elimination` & `Survival` + World Modifiers.**
     - **`F7` — Mở nhanh Headless AI Batch Simulator & QA Audit:** Chạy 100 trận đấu trong $\sim 0.6\text{ giây}$ và xuất báo cáo `[PASS]` toàn diện.
2. **Chạy rà soát `QAAndBalanceAuditor.cs` lần cuối:**
   - Kiểm tra toàn bộ bất biến trạng thái cho cả các tính năng mới (`Survival`, `Elimination`, `Endless Mode`), đảm bảo 0 lỗi biên dịch, 0 cảnh báo `NullReferenceException` và tốc độ giả lập giữ vững $< 10\text{ ms/trận}$.

#### 🛠️ Phần việc BẠN cần thao tác trên Unity Editor (Tuần 9):
1. **Kiểm tra QA tự động trên Editor:**
   - Bấm Play trong Unity, chuyển sang tab `[3] AI Batch Simulator` (hoặc bấm `F7`) $\rightarrow$ bấm **`[RUN FULL QA & BALANCE AUDIT]`** và chụp màn hình kết quả `[PASS]` đưa vào báo cáo quyển đồ án.
2. **Đóng gói Bản dựng Windows PC (`.exe`):**
   - Vào menu `File -> Build Settings...`
   - Bấm **Add Open Scenes** để đưa `MainScene` vào danh sách build.
   - Chọn Target Platform là **Windows, Mac, Linux** (Architecture: `Intel 64-bit` / `x86_64`).
   - Bấm vào **Player Settings...**:
     - `Company Name`: `HUST_20225138`
     - `Product Name`: `The Crownless Warband`
     - `Fullscreen Mode`: `Fullscreen Window` (Độ phân giải mặc định `1920 x 1080`, bật `Resizable Window`).
   - Quay lại cửa sổ Build Settings, bấm **Build**, chọn thư mục `D:\The_Crownless_Warband\Builds\TheCrownlessWarband.exe`.
3. **Chạy thử bản `.exe` độc lập & Quay video dự phòng:**
   - Mở trực tiếp file `TheCrownlessWarband.exe` ngoài màn hình Desktop, bấm thử lần lượt các phím `F1` $\rightarrow$ `F7` và `SPACE` để đảm bảo mọi hiệu ứng chạy mượt mà.
   - Quay lại 1 video ngắn (3–5 phút) trình diễn các phím `F1`–`F7` để làm tư liệu dự phòng tuyệt đối an toàn khi lên bục bảo vệ đồ án.

---

## 5. BẢNG TỔNG HỢP TRẠNG THÁI CÁC HẠNG MỤC ĐỒ ÁN (GDD MỤC 58)

| Phân loại GDD | Hạng mục chức năng | Trạng thái hiện tại | Tuần hoàn thiện |
| :--- | :--- | :---: | :---: |
| **Must Have** | Turn-based Battle (3 Phases) & Formation Grid ($3 \times 3$) | ✅ Hoàn thành (Core) | Tuần 1–2 (+Visual Tuần 5) |
| **Must Have** | 5 Class nhân vật, Basic Attack & Skills, Interception | ✅ Hoàn thành | Tuần 1–2 |
| **Must Have** | Utility AI (`TargetEvaluator` + `ActionEvaluator`) | ✅ Hoàn thành | Tuần 2 |
| **Must Have** | Squad AI (2 phe) & Mid-Battle Adaptation (Chống Thrashing) | ✅ Hoàn thành | Tuần 2 & Tuần 4 |
| **Must Have** | Player-Configurable AI Policy & Skill Auto/Semi/Manual | ✅ Hoàn thành (Core) | Tuần 3 (+UI Tuần 7) |
| **Must Have** | Campaign Map 5 tầng theo Seed, Reward (1 trong 3), 4 Relics | ✅ Hoàn thành | Tuần 3 |
| **Must Have** | Save / Load JSON (`Newtonsoft.Json`) | ✅ Hoàn thành | Tuần 3 |
| **Must Have** | Giao diện Sàn đấu 2D trực quan & Diễn hoạt `DOTween` | 🔄 Đang triển khai | **Tuần 5 – Tuần 6** |
| **Should Have** | Explainable AI Debugger (`Phím F3`) & Combat Log chi tiết | ✅ Hoàn thành | Tuần 2 |
| **Should Have** | Boss Đa giai đoạn (*The Warden* — 3 Phases) | ✅ Hoàn thành | Tuần 4 |
| **Should Have** | World Modifiers (4 loại) & Enemy Affixes (3 loại) | ✅ Hoàn thành | Tuần 4 |
| **Should Have** | 3 Loại Nhiệm vụ (`Assault`, `Survival`, `Elimination`) & 3 Biomes | ⏳ Kế hoạch | **Tuần 7** |
| **Could Have** | Headless AI Batch Simulator (100 trận) & QA Automated Auditor | ✅ Hoàn thành | Tuần 4 |
| **Could Have** | Endless Mode, Player Behavior Profile & Adaptive Encounter | ⏳ Kế hoạch | **Tuần 8** |
| **Could Have** | Âm thanh chiến đấu (SFX) & Phím tắt Demo Hội đồng (`F1–F7`) | ⏳ Kế hoạch | **Tuần 8 – Tuần 9** |
