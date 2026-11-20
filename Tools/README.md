# Art & VFX Pipeline Tools (Ngoài thư mục Assets/ của Unity)

Thư mục này chứa các công cụ dựng tài nguyên 2D & VFX bên ngoài `Assets/` để không làm ảnh hưởng đến Unity AssetDatabase và quá trình biên dịch C#.

## 1. `pixelfx` (`Tools/pixelfx/`)
- **Repo:** `https://github.com/patch-gg/pixelfx` (MIT)
- **Skill:** `.agents/skills/pixelfx/SKILL.md`
- **Chức năng:** 244 hiệu ứng pixel-art viết bằng JS thuần (không cần `npm install`, chỉ cần Node 18+), xuất ra Sprite Sheet PNG nền trong suốt + file JSON chuẩn Aseprite để đưa vào Unity.
- **Lệnh kiểm tra nhanh (chạy từ `Tools/pixelfx`):**
  ```powershell
  node tools/list.mjs slash
  node tools/render.mjs melee/sword-slash --sheet
  ```
- **Chạy qua `agent-sprite-forge` (`fx_pixelfx.py`):**
  ```powershell
  python ".agents/skills/generate2dsprite/scripts/fx_pixelfx.py" check --pixelfx "Tools/pixelfx"
  ```
  *(Hoặc thiết lập biến môi trường `$env:PIXELFX_DIR = "D:\The_Crownless_Warband\Tools\pixelfx"`.)*

## 2. `agent-sprite-forge` (`.agents/skills/`)
- **Repo:** `https://github.com/0x0funky/agent-sprite-forge`
- **Các skill đã cài vào `.agents/skills/`:**
  - `generate2dsprite`: Tạo master still nhân vật, icon, prop, sheet FX, finish `hd` / `pixel` / `hibit`.
  - `generate2dmap`: Tạo battle plate, terrain/tilemap, prop pack, parallax background.
  - `video2dsprite`: Tạo chuỗi animation từ master still + video clip, tách nền soft-matte, loop/retime.
  - `generate2dmedia`: Lớp định tuyến API/CLI tạo ảnh & video (`forge_doctor.py`, `route_media.py`).
  - `codeart2d`: Dự phòng vẽ sprite/SVG/tileset/FX bằng code thuần (`PixelSpec`, `SVG` qua `resvg-py`).
- **Quy trình tạo & chỉnh sửa ảnh trên Antigravity (mặc định của dự án, không cần API key ngoài):**
  1. **Lựa chọn 1 — Chủ động tạo ảnh trên Antigravity:** Sinh prompt/spec từ `master_still.py prompt` (hoặc Art Bible) $\rightarrow$ dùng subagent `image-generator` của Antigravity để tạo ảnh gốc $\rightarrow$ hậu kỳ bằng `master_still.py approve`, `finish_frames.py`, `hibit_plate.py`, `extract_prop_pack.py`, v.v.
  2. **Lựa chọn 2 — Người dùng gửi ảnh ban đầu lên chat:** **Luôn ưu tiên chỉnh sửa & chuẩn hóa ảnh bằng các script Python có sẵn trong skill** (`master_still.py pad`/`approve`, `generate2dsprite.py process`, `finish_frames.py`, `palette_tool.py`, `conform_background.py`, `hibit_plate.py`, `extract_prop_pack.py`, v.v.) để tách nền, căn khung, chỉnh palette và xuất PNG sạch vào `Assets/_Project/`. **Tuyệt đối không tự ý gọi `image-generator`** khi người dùng gửi ảnh; chỉ khi thật sự cần thiết mới đề xuất và bắt buộc phải có sự đồng ý của người dùng mới được sử dụng `image-generator`.
- **Kiểm tra trạng thái môi trường Python/ffmpeg:**
  ```powershell
  $env:PYTHONUTF8 = "1"
  python ".agents/skills/generate2dmedia/scripts/forge_doctor.py" --host-tools image_gen,image_edit
  ```
- **Ghi chú:** Workflow chi tiết được quy định tại `.agents/rules/antigravity-art-workflow.md`; dữ liệu trung gian khi xử lý ảnh đặt trong `ArtWorkspace/` (đã bỏ qua trong `.gitignore`, nằm ngoài `Assets/`).
