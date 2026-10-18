# The Crowless Warband (AI-Driven Tactical Roguelite)
> **Tài liệu Thiết kế Game & Kiến trúc Kỹ thuật (AI Assistant Context Document)**  
> **Phiên bản:** 0.2 (Tối ưu hóa cho AI Coding Assistant & Phát triển Unity)  
> **Thể loại:** Tactical Roguelite / Turn-based Auto Combat / Squad Strategy  
> **Nền tảng:** PC | **Engine:** Unity 2D (C#) | **Chế độ:** Single-player  

---

## 0. HƯỚNG DẪN DÀNH CHO AI TRỢ LÝ LẬP TRÌNH (SYSTEM PROMPT FOR AI ASSISTANTS)
*Nếu bạn là một AI (Claude, ChatGPT, Cursor, GitHub Copilot) đang đọc file này để hỗ trợ viết code, thiết kế dữ liệu hoặc debug cho dự án **The Crowless Warband**, hãy tuân thủ tuyệt đối các nguyên tắc bất biến sau:*

1. **Tách biệt Logic và Hiển thị (Pure C# Simulation vs. Unity View):**
   - Toàn bộ hệ thống `BattleState`, `TurnManager`, `UnitAI`, `SquadAI`, `DamageSystem` **PHẢI** được viết bằng **Pure C# classes** (không kế thừa `MonoBehaviour`, không dùng `Transform`, `Coroutine`, hay `Animator` trực tiếp trong logic tính toán).
   - Trận đấu diễn ra bằng cách thay đổi `BattleState` và đẩy các `CombatEvent` vào hàng đợi (`Queue<CombatEvent>`). Tầng UI/Visual của Unity sẽ đọc từ hàng đợi này để diễn hoạt.
2. **Không có Pathfinding & Di chuyển tự do trong trận:**
   - Chiến trường sử dụng hệ thống **Formation Slot cố định** (3 hàng: `FRONT`, `MID`, `BACK`, mỗi phe 6 ô). Tuyệt đối không sinh code tìm đường (`A*`, `NavMesh`) hay tính toán khoảng cách vật lý `Vector2.Distance` trong combat.
3. **AI là "Bạch hộp" (Explainable Utility AI), KHÔNG dùng Machine Learning hộp đen:**
   - Mọi quyết định của AI (chọn mục tiêu, chọn kỹ năng, đổi chiến thuật đội) đều phải xuất ra được **bảng phân rã điểm số (Score Breakdown)** để hiển thị lên **AI Debug Mode (Phím F3)** và **Combat Log**.
4. **Kiến trúc Hướng dữ liệu (Data-Driven với `ScriptableObject`):**
   - Nhân vật, Kẻ địch, Kỹ năng, Di vật (Relic), và các yếu tố chấm điểm AI (`AIConsideration`) phải được thiết kế dưới dạng `ScriptableObject` hoặc JSON có thể cấu hình trên Inspector mà không cần hard-code.

---

## 1. TỔNG QUAN DỰ ÁN & TẦM NHÌN

### 1.1. One-Sentence Pitch
**The Crowless Warband** là game chiến thuật roguelite theo lượt, nơi người chơi xây dựng một đội hình và thiết lập chính sách hành vi chiến đấu cho từng unit, sau đó chứng kiến AI của hai phe tự phân tích, phối hợp và thích nghi qua từng trận đấu.

### 1.2. Ý tưởng cốt lõi (Core Concept)
Người chơi không trực tiếp điều khiển từng đòn đánh cơ bản của nhân vật trong trận chiến, mà đảm nhận vai trò **Người chỉ huy đội hình (Squad Commander)**:
- Tuyển chọn thành viên, xây dựng đội hình và xếp vị trí vào các ô Formation.
- Xây dựng bộ kỹ năng, nâng cấp (Upgrades) và thu thập Di vật (Relics).
- Thiết lập chính sách hành xử của AI (AI Policy/Behavior) cho từng unit và toàn đội.
- Quyết định kỹ năng nào để AI tự dùng (`Auto` / `Semi-auto`) và kỹ năng nào do người chơi kích hoạt (`Manual`).
- Quan sát diễn biến chiến đấu và can thiệp bằng kỹ năng/mệnh lệnh vào các thời điểm bước ngoặt.

**Câu hỏi trọng tâm của Gameplay:** *"Mình đã xây dựng một đội hình đủ thông minh để tự chiến đấu chưa?"*

### 1.3. Cảm giác người chơi hướng tới (Game Fantasy)
Một trận đấu thành công phải khiến người chơi thốt lên:
- *"Mình đã sắp xếp đội hình khắc chế hoàn hảo!"*
- *"Đội hình này rất mạnh nhưng thiết lập ưu tiên AI của mình đang sai, chỉ cần chỉnh lại điều kiện của một unit là cục diện sẽ đảo chiều."*

### 1.4. Các nguyên tắc thiết kế (Design Pillars)
1. **Strategy over Micromanagement:** Tập trung vào đội hình, hướng build, ưu tiên mục tiêu và kỹ năng tối thượng thay vì click từng đòn đánh mỗi lượt.
2. **AI là Gameplay trung tâm:** Cả hai phe đều vận hành bằng AI (`PLAYER AI ↔ GAME STATE ↔ ENEMY AI`).
3. **Mỗi Run là duy nhất:** Khác biệt thông qua Seed, đội hình, nâng cấp kỹ năng, di vật, sự kiện, biến thể kẻ địch và điều kiện môi trường (Modifiers).
4. **Mở rộng nội dung bằng Hệ thống (Systemic Content):** Kết hợp tự động `Biomes + Encounter + Enemy + Affix + Modifier + Reward` thay vì thiết kế hàng trăm màn chơi thủ công.

---

## 2. VÒNG LẶP GAMEPLAY & CẤU TRÚC CHIẾN DỊCH (ROGUELITE LOOP)

### 2.1. Hai lớp cấu trúc Game
1. **Campaign Layer (Lớp Chiến dịch):** Bản đồ dạng Node phân nhánh (giống *Slay the Spire*). Các loại Node bao gồm:
   - `Combat` (Giao chiến thường)
   - `Elite Combat` (Kẻ địch tinh anh)
   - `Boss` (Trùm cuối khu vực)
   - `Event` (Sự kiện ngẫu nhiên có lựa chọn Rủi ro/Phần thưởng)
   - `Shop` (Cửa hàng mua bán Relic/Upgrade)
   - `Recovery` (Hồi phục HP / Nâng cấp tại trại)
2. **Battle Layer (Lớp Chiến đấu):** Nơi diễn ra trận đánh theo lượt tự động dựa trên đội hình và AI đã thiết lập.

### 2.2. Core Game Loop
```text
[Căn cứ / Đầu Run: Khởi tạo Seed & Chọn Đội hình khởi đầu]
       ↓
[Campaign Map: Chọn Node tiếp theo trên nhánh đường đi]
       ↓
[Pre-Battle / Planning: Sắp xếp Formation + Chỉnh sửa AI Policy]
       ↓
[Battle Execution: AI hai phe tự giao chiến + Người chơi can thiệp Manual Skill]
       ↓
[Post-Battle: Nhận Reward -> Chọn Nâng cấp Kỹ năng / Relic -> Tiến tới Boss]
       ↓
[Kết thúc Run (Thắng/Thua) -> Mở khóa Meta Progression -> Bắt đầu Run mới / Endless Mode]
```

---

## 3. HỆ THỐNG CHIẾN ĐẤU & ĐỘI HÌNH (BATTLE & FORMATION SYSTEM)

### 3.1. Bố cục Chiến trường (Battlefield Formation Slots)
Chiến trường gồm 2 phe đối diện nhau, mỗi phe có một lưới vị trí cố định chia làm 3 hàng (`FRONT`, `MID`, `BACK`), tối đa 6 ô (3x2 hoặc 3x3 tùy cấu hình, chuẩn MVP sử dụng 6 slot mỗi bên hoặc 3 hàng rõ rệt):

```text
                 PHE ĐỊCH (ENEMY)
       Hàng Sau (Back):    [E4]   [E5]   [E6]
       Hàng Trước (Front): [E1]   [E2]   [E3]
       ====================================== (Ranh giới chiến tuyến)
       Hàng Trước (Front): [P1]   [P2]   [P3]
       Hàng Sau (Back):    [P4]   [P5]   [P6]
                 PHE NGƯỜI CHƠI (PLAYER)
```
*(Lưu ý: Hệ thống hỗ trợ chia chi tiết thành 3 lớp vị trí logic `FRONT` - `MID` - `BACK` để tính tầm đánh và mức độ che chắn).*

### 3.2. Ý nghĩa Chiến thuật của Formation
Vị trí đứng ảnh hưởng trực tiếp đến công thức tính toán của AI và hệ thống chiến đấu:
- **Khả năng bị nhắm mục tiêu (Targetability):** Các đòn cận chiến thông thường bị chặn bởi `FRONT` line, trừ khi kẻ địch có kỹ năng `Dive` / `Flank` hoặc đánh tầm xa (`Ranged`).
- **Hiệu quả che chắn (Protection/Interception):** Tank đứng ở `FRONT` tăng điểm che chắn cho các unit đứng cùng cột ở `MID`/`BACK`.
- **Phạm vi AoE:** Kỹ năng diện rộng có thể đánh theo hàng ngang (Row), cột dọc (Column), hoặc chữ thập (Cross).

**Các mẫu Đội hình tiêu biểu:**
- **Fortress Formation:** 2 Guardian + 1 Vanguard ở `FRONT`, Medic ở `MID`, Ranger + Mage ở `BACK` (Chắc chắn, kéo dài trận đấu).
- **Assault Formation:** 2 Assassin + 1 Vanguard ở `FRONT`, Mage ở `MID`, Medic ở `BACK` (Dồn sát thương tốc độ cao, rủi ro lớn).
- **Ranged Formation:** 1 Guardian ở `FRONT`, Vanguard + Mage ở `MID`, 2 Ranger + Medic ở `BACK` (Tối ưu hỏa lực tầm xa, phụ thuộc hoàn toàn vào 1 Tank duy nhất).

### 3.3. Cấu trúc Một Lượt Đấu (Turn Structure & Initiative)
Mỗi vòng đấu lớn (Round/Turn) được chia thành 3 giai đoạn rõ rệt:

1. **Phase 1 — Planning (Lập kế hoạch đầu vòng):**
   - Người chơi quan sát toàn bộ thông tin: `HP`, `Energy`, `Cooldown`, `Buff/Debuff`, `Intended Target` (mục tiêu dự kiến), `Squad Strategy` hiện tại.
   - Người chơi có thể: Thay đổi chiến thuật chung (`Team Tactic`), điều chỉnh `AI Policy` của unit, chuyển chế độ kỹ năng (`Auto/Semi/Manual`), hoặc ra lệnh ưu tiên đánh dấu mục tiêu (`Manual Target Override`).
2. **Phase 2 — Auto Combat (Thực thi theo Hàng đợi Tốc độ - Speed Queue):**
   - *Bổ sung kỹ thuật:* Đầu Phase 2, các unit còn sống của cả 2 phe được sắp xếp vào một `TurnQueue` dựa trên chỉ số `Speed` giảm dần:
     $$\text{Initiative} = \text{BaseSpeed} \times (1 + \text{SpeedModifiers})$$
   - Khi đến lượt của một Unit trong hàng đợi, nếu unit không bị khống chế cứng (`Stun`/`Freeze`), nó sẽ chạy chu trình AI ngay tại thời điểm thực của `BattleState`:
     `Observe` $\rightarrow$ `Evaluate Targets` $\rightarrow$ `Evaluate Actions` $\rightarrow$ `Execute Skill/Attack`.
   - Người chơi có thể nhấn kích hoạt `Manual Ultimate` khi kỹ năng sẵn sàng.
3. **Phase 3 — Battle Resolution (Giải quyết cuối vòng):**
   - Cập nhật sát thương theo thời gian (`DoT` như Burn, Poison), hiệu ứng môi trường (`World Modifiers` như Acid Rain), giảm thời gian hồi chiêu (`Cooldown - 1`), giảm thời hạn `Buff/Debuff`, kiểm tra điều kiện Thắng/Thua (`Mission Objective`), và ghi nhận `Combat Log`.

---

## 4. HỆ THỐNG TRÍ TUỆ NHÂN TẠO (CORE AI ARCHITECTURE)
*Đây là trái tim của đồ án. AI hoạt động công bằng (không gian lận thông tin ẩn) và được chia làm 2 tầng: **Squad AI (Tầng Chỉ huy Đội)** và **Unit Utility AI (Tầng Thực thi Cá nhân)**.*

### 4.1. Nhận thức của AI (AI Perception & Fair Play)
AI chỉ được phép truy xuất dữ liệu công khai trong `BattleState`:
- **Được biết (Known):** HP/Energy hiện tại của mọi unit, vị trí Formation, Role, các Buff/Debuff đang hiển thị, các kỹ năng đã lộ diện.
- **Không được biết (Unknown):** Unit đang tàng hình (`Stealth/Hidden`), ý định kích hoạt `Manual Skill` sắp tới của người chơi, chỉ số ngẫu nhiên chưa quay (`RNG Roll`).

### 4.2. Tầng 1: Squad AI (Rule-Based Strategy & Coordination)
Đầu mỗi Turn (hoặc khi có sự kiện lớn như 1 unit tử trận), `SquadAI` của mỗi phe đánh giá toàn cục chiến trường và chọn một **Chiến lược Đội (`SquadStrategy`)**:

- **Các Chiến lược của Phe Người Chơi (`Team Tactic`):**
  - `Aggressive` (Tấn công dồn dập)
  - `Defensive` (Phòng thủ an toàn)
  - `Focus Fire` (Tập trung dồn sát thương 1 mục tiêu)
  - `Control` (Ưu tiên khống chế diện rộng)
  - `Survival` (Ưu tiên cứu sống đội hình khi nguy kịch)
- **Các Chiến lược của Phe Địch (`Enemy Strategy`):**
  - `Direct Assault`: Đánh thẳng vào tuyến đầu.
  - `Backline Dive`: Bỏ qua Tank, dùng Assassin/Ranged lao vào giết DPS/Support hàng sau.
  - `Focus Fire`: Cả đội hội đồng 1 mục tiêu yếu nhất hoặc nguy hiểm nhất.
  - `Anti-Heal`: Ưu tiên triệt hạ hoặc khống chế Medic của người chơi.
  - `Attrition`: Đánh cầm chừng, tích lũy giáp và sát thương theo thời gian.

**Cơ chế thích nghi giữa trận (Mid-Battle AI Adaptation):**
`SquadAI` không dùng cố định 1 kịch bản. Ví dụ:
- Turn 1: Địch chọn `Backline Dive` vì có Assassin.
- Turn 2: Assassin địch bị giết $\rightarrow$ Điểm hiệu quả của `Backline Dive` tụt xuống mức `LOW` $\rightarrow$ `Enemy SquadAI` lập tức chuyển sang `Focus Frontline` để dồn sát thương phá giáp Guardian của người chơi.

**Cách `SquadAI` điều phối `UnitAI` (Cơ chế Directive Weights):**
Thay vì ép cứng unit bằng lệnh `if-else`, `SquadAI` gắn các **Trọng số chỉ đạo (`SquadDirectiveBonus`)** lên các mục tiêu hoặc hành động.
- *Ví dụ:* Khi `SquadAI` phát hiện Ranger phe ta đang bị đe dọa nghiêm trọng, nó phát lệnh `Protect Backline` và gán:
  - Guardian nhận `+40 điểm` cho hành động `Protect/Taunt` lên kẻ đang nhắm vào Ranger.
  - Mage nhận `+35 điểm` cho kỹ năng `Disable/Stun` lên Assassin địch.
  - Medic nhận `+30 điểm` cho hành động `Heal` lên Ranger.

### 4.3. Tầng 2: Unit AI (Utility Scoring System)
Khi đến lượt hành động, mỗi unit thực hiện chấm điểm theo 2 bước:

#### Bước A: Chấm điểm chọn Mục tiêu (Target Selection)
Với mỗi mục tiêu hợp lệ $t$ trong tầm đánh, điểm số $S_{\text{target}}(t)$ được tính theo công thức tổng trọng số:

$$S_{\text{target}}(t) = W_{\text{threat}} \cdot \text{Threat}(t) + W_{\text{strat}} \cdot \text{StrategicValue}(t) + W_{\text{kill}} \cdot \text{KillProb}(t) + W_{\text{vuln}} \cdot \text{Vulnerability}(t) + W_{\text{role}} \cdot \text{RolePriority}(t) + \text{PolicyBonus}(t) + \text{SquadBonus}(t) - W_{\text{risk}} \cdot \text{CounterRisk}(t)$$

Trong đó:
- $\text{Threat}(t)$: Mức độ nguy hiểm của mục tiêu (sát thương gây ra, kỹ năng sắp hồi).
- $\text{StrategicValue}(t)$: Giá trị chiến lược (ví dụ: Y sĩ hồi máu hoặc mục tiêu nhiệm vụ `Elimination`).
- $\text{KillProb}(t)$: Xác suất kết liễu mục tiêu ngay trong đòn đánh này (tăng vọt nếu $\text{Damage dự kiến} \ge \text{HP hiện tại}$).
- $\text{Vulnerability}(t)$: Mức độ mỏng manh (đang bị `Marked`, giảm giáp, hoặc bị `Stun`).
- $\text{PolicyBonus}(t)$: Điểm cộng từ cấu hình ưu tiên của người chơi (`Player-Configurable AI`).
- $\text{SquadBonus}(t)$: Điểm cộng từ lệnh điều phối của `SquadAI`.
- $\text{CounterRisk}(t)$: Rủi ro bị phản đòn (`Counter`) hoặc bị chặn đòn (`Intercept`).

#### Bước B: Chấm điểm chọn Hành động / Kỹ năng (Action Selection)
Với mỗi kỹ năng khả dụng $a$ (đủ `Energy`, `Cooldown == 0`, thỏa mãn điều kiện kích hoạt) trên mục tiêu tốt nhất:

$$S_{\text{action}}(a, t) = \text{BaseUtility}(a) + \text{ExpectedImpact}(a, t) + \text{SynergyBonus}(a, t) + \text{BehaviorWeight}(a) + \text{PriorityOrderBonus}(a)$$

- Hành động có $S_{\text{action}}$ cao nhất sẽ được chọn để thực thi.

### 4.4. Thiết lập AI của Người chơi (Player-Configurable AI & Skill Modes)
Người chơi cấu hình AI mà không cần viết code thông qua 3 công cụ:
1. **Skill Mode (Chế độ kỹ năng):**
   - `Auto`: AI tự tính điểm Utility và tự dùng.
   - `Semi-Auto`: AI chỉ tự dùng khi thỏa mãn **Điều kiện (Condition)** do người chơi khóa cứng (ví dụ: Chỉ dùng `Snipe` khi `Target HP < 40%`).
   - `Manual`: AI không bao giờ tự dùng; chờ người chơi nhấn nút kích hoạt và chỉ định mục tiêu.
2. **Behavior Preset ( Khuynh hướng hành vi):** `Aggressive`, `Defensive`, `Support`, `Tactical`, `Balanced` (thay đổi bộ hệ số nhân $W$ trong công thức chấm điểm).
3. **Target & Skill Priority List:** Danh sách thứ tự ưu tiên mục tiêu (ví dụ: `1. Marked -> 2. Lowest HP -> 3. Support -> 4. Nearest`) và thứ tự ưu tiên kỹ năng kèm điều kiện kích hoạt (`Use Heal if Ally HP < 50%`, `Use AoE if Enemies >= 3`).

---

## 5. HỆ THỐNG NHÂN VẬT, BUILD & TƯƠNG TÁC (CHARACTERS & SYNERGIES)

### 5.1. Năm Lớp Nhân Vật Cốt Lõi (MVP Classes)
| Class | Vai trò (Role) | Vị trí chuẩn | Đặc trưng cơ chế & Kỹ năng |
| :--- | :--- | :--- | :--- |
| **Guardian** | Tank / Protection | `FRONT` | HP/Def cao, Khiêu khích (`Taunt`), Đỡ đòn hộ đồng đội (`Intercept`), Tạo khiên (`Shield`). |
| **Vanguard** | Frontline / Control | `FRONT` / `MID` | Làm choáng (`Stun`), Đẩy lùi phá vỡ vị trí (`Formation Break`), Khống chế đám đông. |
| **Ranger** | Single-target DPS | `BACK` | Tầm xa, Chí mạng cao, Đánh dấu mục tiêu (`Mark`), Kết liễu mục tiêu thấp máu (`Execute`). |
| **Mage** | AoE / Control | `MID` / `BACK` | Sát thương diện rộng, Gây hiệu ứng xấu (`Burn`, `Weaken`), Kiểm soát vùng. |
| **Medic** | Support / Healer | `BACK` | Hồi máu đơn/đa mục tiêu, Tăng chỉ số (`Buff`), Giải trừ hiệu ứng xấu (`Cleanse`). |

Each unit sở hữu: `Basic Attack`, `Skill 1`, `Skill 2`, và `Ultimate`.

### 5.2. Hệ thống Nâng cấp (Upgrades), Di vật (Relics) & Tương tác (Synergies)
- **Skill Upgrades (Sau mỗi trận):** Thay đổi cơ chế kỹ năng (ví dụ: *Shield của Guardian kéo dài thêm +1 turn*, *Mục tiêu bị Marked nhận thêm +25% sát thương từ Ranger*).
- **Relics (Di vật chiến thuật):** Tác động trực tiếp vào sự kiện trận đấu và hành vi AI:
  - *Blood Contract:* $-10\%$ Max HP, $+30\%$ Critical Damage.
  - *Tactical Core:* Mỗi khi `Squad AI` đổi chiến thuật giữa trận, toàn đội nhận $+10$ Energy.
  - *Emergency Protocol:* Khi có đồng minh $<30\%$ HP, Medic tự động cộng điểm ưu tiên tuyệt đối cho đồng minh đó và tăng $+25\%$ lượng máu hồi.
  - *Chain Relay:* Khi mục tiêu đang bị `Marked` tử trận, dấu ấn `Mark` tự động nhảy sang kẻ địch gần nhất.
- **Combo Synergies (Tương tác trạng thái):**
  - `Mark` + `Sniper Shot` $\rightarrow$ Sát thương chuẩn / Chí mạng 100%.
  - `Burn` + `Poison` $\rightarrow$ Kích hoạt vụ nổ độc (`Toxic Explosion` gây sát thương lan).
  - `Stun` + `Heavy Hit` $\rightarrow$ Tăng $+50\%$ sát thương lên mục tiêu đang choáng.
  - `Shield` + `Counter Stance` $\rightarrow$ Phản đòn ngay khi khiên bị tấn công.

---

## 6. HỆ THỐNG SINH TỰ ĐỘNG, ĐỘ KHÓ & ENEMY (PROCEDURAL & PROGRESSION)

### 6.1. Procedural Generation theo Seed
Mỗi Run khởi tạo bằng một số nguyên `Seed` (ví dụ: `847291`). Mọi hàm sinh ngẫu nhiên (Bản đồ nhánh, Đội hình địch, Phần thưởng, Sự kiện) đều phải truyền qua bộ sinh số giả ngẫu nhiên có kiểm soát (`System.Random(seed)`) để đảm bảo **cùng một Seed sẽ tái tạo chính xác 100% cùng một Run**.

### 6.2. Hệ thống Kẻ địch Hướng dữ liệu (Data-Driven Enemies & Affixes)
Một mẫu kẻ địch gốc (`EnemyData`) kết hợp với các tiền tố biến thể (`EnemyAffix`) để nhân bản nội dung nhanh chóng:
- `Soldier` (Gốc) $\rightarrow$ `Armored Soldier` (Có giáp đầu trận), `Fast Soldier` (Tăng mạnh Speed), `Berserker Soldier` (Càng mất máu đánh càng đau), `Regenerator Soldier` (Tự hồi máu cuối turn).

### 6.3. Hệ thống Khu vực (Biomes), Biến số Môi trường (World Modifiers) & Nhiệm vụ
- **3 Biomes:** Thành phố đổ nát (Urban/Scavenger), Khu công nghiệp (Machine/Explosive Hazard), Khu thí nghiệm (Mutant/Status Effects).
- **World Modifiers:** `Blackout` (Giảm tầm nhìn/chính xác), `Acid Rain` (Cuối turn toàn trường mất máu), `Blood Moon` (Địch tăng sát thương nhưng rơi thưởng xịn hơn), `No Healing` (Giảm 75% hiệu quả hồi máu), `Overcharge` (Hồi năng lượng nhanh gấp đôi), `Reinforcement` (Địch tiếp viện sau Turn X).
- **Loại Nhiệm vụ (Mission Objectives):**
  - *MVP Priority:* `Assault` (Tiêu diệt toàn bộ địch), `Survival` (Sống sót qua $X$ lượt), `Elimination` (Hạ gục 1 mục tiêu chỉ định).
  - *Mở rộng:* `Defense` (Bảo vệ mục tiêu tĩnh), `Escort`, `Extraction`.

### 6.4. Hệ thống Đe dọa (Threat), Tha hóa (Corruption) & Boss
- **Threat & Corruption (0% $\rightarrow$ 100%):** Càng tiến sâu hoặc chọn nhánh nguy hiểm, độ Tha hóa càng tăng, mở khóa thêm `Enemy Mutation`, `Modifiers` khắc nghiệt hơn nhưng phần thưởng giá trị cao hơn.
- **Boss Đa giai đoạn (Multi-Phase Boss):** Ví dụ *The Warden*:
  - *Phase 1 (100% - 60% HP):* Triệu hồi lính canh che chắn hàng trước.
  - *Phase 2 (60% - 25% HP):* Dùng kỹ năng quét phá vỡ Formation của người chơi, buộc AI phải thích nghi lại vị trí.
  - *Phase 3 (<25% HP):* Chuyển AI sang trạng thái `Enraged Burst`, ưu tiên dồn sát thương mục tiêu thấp máu nhất.

---

## 7. GIAO DIỆN, CÔNG CỤ DEBUG AI (F3) & LƯU TRỮ

### 7.1. AI Debug Mode (Phím tắt F3 — Tính năng Bắt buộc để Bảo vệ Đồ án)
Khi nhấn `F3` trong trận đấu, một lớp phủ (Overlay Panel) hiển thị trực tiếp tư duy của AI:
```text
================ AI DEBUG INSPECTOR (F3) ================
[Selected Unit]: Guardian (Player Front-1) | Role: Protector
[Player Squad Strategy]: PROTECT BACKLINE (Triggered: Ranger Threat High)

--- Target Evaluation Breakdown ---
1. Enemy Assassin : Total = 82 (Threat: 35 + Strat: 15 + SquadBonus: 32)
2. Enemy Mage     : Total = 61 (Threat: 30 + Strat: 20 + Vuln: 11)
3. Enemy Tank     : Total = 34 (Threat: 10 + Strat: 10 + Nearest: 14)

--- Action Evaluation Breakdown ---
-> [CHOSEN] Protect Ranger : Score = 91 (Base: 40 + EmergencyBonus: 51)
   Shield Bash (Assassin)  : Score = 74 (Base: 50 + StunSynergy: 24)
   Basic Attack (Tank)     : Score = 28

[Enemy Squad Strategy]: BACKLINE DIVE -> Switching to FOCUS FRONTLINE
=========================================================
```

### 7.2. Combat Log (Nhật ký Giải thích Quyết định)
Ghi lại không chỉ kết quả sát thương mà cả **lý do AI ra quyết định**:
> `[Turn 4] Medic đánh giá: Guardian HP (31%) < Ngưỡng khẩn cấp (50%) -> Quyết định: Dùng Emergency Heal lên Guardian (+140 HP).`

---

## 8. KIẾN TRÚC KỸ THUẬT & QUY ƯỚC CODE UNITY C# (TECHNICAL SPECIFICATION)
*Phần này được bổ sung để định hướng cấu trúc code chuẩn mực cho lập trình viên và AI Assistant.*

### 8.1. Cấu trúc Thư mục Dự án (`Assets/_Project`)
```text
Assets/_Project/
├── Scripts/
│   ├── Core/                  # Pure C# Battle Simulation (KHÔNG phụ thuộc MonoBehaviour)
│   │   ├── State/             # BattleState, UnitState, FormationGrid, StatusEffectState
│   │   ├── Rules/             # TurnManager, DamageCalculator, SkillExecutor, SynergyResolver
│   │   ├── Events/            # CombatEvent, CombatEventQueue, BattleEventBus (Observer)
│   │   └── AI/                # SquadAI, UnitAI, TargetEvaluator, ActionEvaluator, ScoreBreakdown
│   ├── Data/                  # ScriptableObject Definitions
│   │   ├── Characters/        # CharacterDataSO,EnemyDataSO, EnemyAffixSO
│   │   ├── Skills/            # SkillDataSO, SkillEffectSO, UpgradeDataSO
│   │   ├── AI/                # AIConsiderationSO, AIPresetSO, SquadStrategySO
│   │   └── Roguelite/         # RelicDataSO, BiomeDataSO, WorldModifierSO, EventDataSO
│   ├── Presentation/          # Unity MonoBehaviour Views (Đọc CombatEventQueue để hiển thị)
│   │   ├── BattleView/        # BattleVisualizer, UnitView, FloatingText, FormationSlotView
│   │   ├── UI/                # BattleHUD, AIPolicyConfigUI, CombatLogUI, AIDebugOverlayUI (F3)
│   │   └── CampaignView/      # CampaignMapUI, NodeView, RewardSelectionUI
│   └── Systems/               # SaveLoadManager, SeedManager, GameFlowController
└── ScriptableObjects/         # Các file tài nguyên .asset thực tế
```

### 8.2. Mẫu thiết kế Bắt buộc (Core Design Patterns)
1. **Command / Event Queue (Tách Logic và Animation):**
   - Khi `SkillExecutor` xử lý một kỹ năng trong `BattleState`, nó tạo ra các đối tượng `CombatEvent` (ví dụ: `SkillUsedEvent`, `DamageDealtEvent`, `StatusAppliedEvent`, `AIDecisionLoggedEvent`) và đẩy vào `CombatEventQueue`.
   - `BattleVisualizer` (MonoBehaviour) duyệt qua hàng đợi này bằng Coroutine/UniTask để chạy hiệu ứng hình ảnh tuần tự theo tốc độ trận đấu (`1x`, `2x`, hoặc `Skip`).
2. **Pluggable Utility AI với `ScriptableObject` & `AnimationCurve`:**
   - Mỗi yếu tố chấm điểm kế thừa từ lớp cơ sở `AIConsiderationSO`, sử dụng `AnimationCurve` của Unity để chuẩn hóa giá trị đầu vào (ví dụ: `% HP còn lại`) thành điểm số từ $[0, 100]$.
   - Luôn trả về cấu trúc `EvaluationResult { float score; string reason; }` để phục vụ trực tiếp cho bảng `F3 Debug`.
3. **Event Bus cho Relics & Modifiers (`BattleEventBus`):**
   - Các Relic và World Modifier đăng ký lắng nghe các sự kiện (`OnTurnStart`, `OnBeforeDamage`, `OnUnitKilled`, `OnStrategyChanged`, `OnModifyTargetScore`) thay vì viết `if (HasRelic("X"))` rải rác trong code lõi.
4. **Tuần tự hóa Trạng thái (`JSON Serialization`):**
   - `BattleState` và `RunState` phải có khả năng chuyển đổi sang JSON chỉ bằng 1 lệnh để phục vụ: Save/Load giữa chừng, Chạy giả lập 1.000 trận kiểm thử tự động (Headless Simulation), và Tạo kịch bản Demo tức thời (Sandbox Mode).

---

## 9. LỘ TRÌNH PHÁT TRIỂN & PHẠM VI ĐỒ ÁN (ROADMAP & SCOPE)

### 9.1. Thứ tự Ưu tiên Triển khai (15 Bước Phát triển)
```text
[Giai đoạn 1: Hộp cát Chiến đấu Cốt lõi]
1. BattleState (Pure C#) -> 2. TurnManager (Speed Queue) -> 3. Unit & Formation Slot -> 4. Basic Combat & Status -> 5. Skill System & Event Queue

[Giai đoạn 2: Bộ não Trí tuệ Nhân tạo & Công cụ Giải thích]
6. AI Debug UI (F3) & Combat Log -> 7. Player Unit Utility AI -> 8. Enemy AI -> 9. Squad AI & Mid-Battle Adaptation -> 10. Player AI Config UI & Manual Skill

[Giai đoạn 3: Vòng lặp Roguelite & Sinh tự động]
11. Roguelite Upgrades & Relics (Event Bus) -> 12. Campaign Node Map -> 13. Seed-based Procedural Generation -> 14. Multi-Phase Boss

[Giai đoạn 4: Hoàn thiện & Kịch bản Bảo vệ Đồ án]
15. Save/Load + Sandbox Test Scenarios (Phím tắt nạp nhanh kịch bản chứng minh AI thông minh trước Hội đồng)
```

### 9.2. Phân loại Phạm vi (MoSCoW Scope)
- **MUST HAVE (Bắt buộc có trong MVP):**
  - Turn-based auto battle trên Formation Slot cố định.
  - 4–5 Class nhân vật, 6 mẫu Enemy + Affixes, 1–2 Boss nhiều phase.
  - Kỹ năng 3 chế độ (`Auto` / `Semi-auto` / `Manual`).
  - Hệ thống `Utility AI` (Target & Action Evaluation) + `Squad AI` (2 phe) + `AI Adaptation` giữa trận.
  - Bảng cấu hình `Player AI Policy`.
  - Công cụ `AI Debug Mode (F3)` và `Combat Log` giải thích quyết định.
  - Bản đồ Campaign, chọn Reward/Upgrade/Relic (15–20 loại), và Save/Load cơ bản.
- **SHOULD HAVE (Nên có):**
  - 3 loại Mission Objectives (`Assault`, `Survival`, `Elimination`).
  - 3–4 World Modifiers tác động lên chiến trường.
  - Hệ thống Threat / Corruption tăng dần theo tầng.
- **COULD HAVE (Mở rộng nếu còn thời gian):**
  - Adaptive Difficulty & Player Behavior Modeling (Phân tích thói quen người chơi để sinh đội hình khắc chế).
  - Endless Mode, Daily Seed, tính năng Replay trận đấu.
- **WON'T HAVE (Tuyệt đối KHÔNG làm trong bản đầu):**
  - Multiplayer / PvP / Online Server.
  - Đồ họa 3D hoặc hệ thống di chuyển tự do / tìm đường (Pathfinding) trong trận.
  - Dùng Machine Learning hộp đen làm core AI chiến đấu.