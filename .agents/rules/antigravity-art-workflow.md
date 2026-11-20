---
trigger: model_decision
description: Quy trình tạo và xử lý Sprite 2D, Map/Backdrop, UI Icon và VFX cho The Crownless Warband trên Antigravity (không dùng API key ngoài; dùng image-generator hoặc ảnh do người dùng gửi lên rồi hậu kỳ bằng agent-sprite-forge và pixelfx).
---

# Antigravity Art & Sprite Workflow (The Crownless Warband)

Khi tạo mới hoặc chỉnh sửa asset đồ họa 2D (`Sprite`, `Portrait`, `Map/Backdrop`, `Prop`, `Icon`, `VFX`) cho **The Crownless Warband**, **KHÔNG** gọi `route_media.py` qua API key trả phí hay Codex/Grok CLI ngoài (trừ khi người dùng yêu cầu rõ). Thay vào đó, luôn chạy theo **2 lựa chọn đầu vào** dưới đây kết hợp hậu kỳ bằng `agent-sprite-forge` và `pixelfx`:

## 1. Hai Lựa chọn Nguồn Ảnh Ban đầu

### Lựa chọn A — Chủ động Tạo Ảnh trên Antigravity (`image-generator`)
Áp dụng khi người dùng yêu cầu tạo mới từ đầu hoặc chưa gửi ảnh mẫu:
1. **Lập Spec & Prompt chuẩn dự án:**
   - Tuân thủ Art Bible trong `.agents/skills/visual-polish-2d/SKILL.md` (Dark Tactical Fantasy, màu phe Player/Enemy, 3 Biomes `RuinedCity` / `IndustrialZone` / `LabComplex`, nhận diện 5 Class + Boss *The Warden*).
   - Với nhân vật/mob/boss/prop: chạy `master_still.py prompt --spec <spec.json> --output-dir ArtWorkspace/<asset>-prompt` để sinh `prompt.txt` chuẩn khung & nền key (`#FF00FF` hoặc `#00FF00`).
2. **Tạo ảnh qua subagent `image-generator`:**
   - Gọi `invoke_subagent` (`TypeName: "image-generator"`) với prompt chi tiết (và truyền ảnh tham chiếu nếu cần giữ đồng bộ style giữa các Class).
3. **Kiểm tra bằng mắt & Tinh chỉnh:**
   - Đọc ảnh trả về bằng `read_file`. Nếu lệch hướng nhìn (`left`/`right`), sai vũ khí hoặc lẫn chữ/UI thừa, gọi lại `image-generator` với ảnh vừa tạo làm reference để sửa đúng 1 điểm sai.

### Lựa chọn B — Ảnh Ban đầu Do Người dùng Gửi lên
Áp dụng khi người dùng đính kèm ảnh gốc (concept, sprite thô, background, icon) trong khung chat:
1. **Đọc & Đánh giá so với Yêu cầu Dự án:**
   - Kiểm tra ngay: hướng mặt (`Player` quay phải, `Enemy` quay trái), tỷ lệ cơ thể/lưới $3 \times 3$, độ đọc silhouette ở cỡ game, nền phẳng/trong suốt, màu sắc theo Class/Biome, và có chữ/nền rác không.
2. **Chỉnh sửa & Chuẩn hóa Ưu tiên Tuyệt đối bằng Script Sẵn có của `agent-sprite-forge`:**
   - **BẮT BUỘC ưu tiên dùng các script Python có sẵn trong skill** (`master_still.py pad`/`approve`, `generate2dsprite.py process`, `finish_frames.py`, `palette_tool.py`, `pixel_reduce.py`, `scale_frames.py`, `conform_background.py`, `hibit_plate.py`, `extract_prop_pack.py`, `extract_terrain_tiles.py`, v.v.) để tách nền, crop/pad, lật hướng, ép tỷ lệ, lượng tử hóa bảng màu và xuất asset cho dự án.
   - **TUYỆT ĐỐI KHÔNG tự ý gọi `image-generator` trên Antigravity khi người dùng gửi ảnh lên.**
   - Chỉ khi nào ảnh gốc có lỗi cấu trúc/tạo hình mà script xử lý ảnh thuần túy thực sự không thể giải quyết được, agent mới được **đề xuất lý do cụ thể cho người dùng** và **bắt buộc phải chờ người dùng đồng ý rõ ràng** thì mới được phép gọi `image-generator` trong trường hợp này.

---

## 2. Hậu kỳ & Chuẩn hóa bằng `agent-sprite-forge` (Python)
Lưu các bước nháp ở `ArtWorkspace/` (ngoài `Assets/`, đã được `.gitignore` bỏ qua):
- **Chuẩn hóa Master Still:**
  ```powershell
  python ".agents/skills/generate2dsprite/scripts/master_still.py" approve --run ArtWorkspace/<asset>-prompt --still <input.png> --route <antigravity-image_gen|user-supplied> --output-dir ArtWorkspace/<asset>-master
  ```
- **Chốt finish (`hd`, `pixel`, hoặc `hibit`) & Palette:**
  - Dùng `finish_frames.py` (`hd` / `pixel` / `hibit`), `palette_tool.py`, `scale_frames.py`, `generate2dsprite.py process`.
- **Map / Battle Arena Backdrop / Prop Pack:**
  - Dùng `conform_background.py`, `hibit_plate.py`, `extract_prop_pack.py`, `extract_terrain_tiles.py`.
- **VFX 2D (không cần gen ảnh ngoài):**
  - Dùng `Tools/pixelfx` (`node tools/render.mjs <category/effect> --sheet`) hoặc `fx_pixelfx.py render --pixelfx Tools/pixelfx` để xuất sprite sheet PNG + JSON chuẩn Aseprite.

---

## 3. Đưa vào Unity (`Assets/_Project/`)
- Chỉ copy file `.png` / `.json` thành phẩm cuối cùng vào thư mục trong `Assets/_Project/` (ví dụ `Assets/_Project/Art/Sprites/...` hoặc `Assets/_Project/Art/VFX/...`).
- Gắn vào Presentation (`BattlefieldGridView`, `BattleArenaBackdropView`, `CombatVfxManager`, `UguiThemeFactory`) và kiểm tra quy tắc `Flash Coding Protocol` (tuyệt đối không chạm `Scripts/Core/`).
