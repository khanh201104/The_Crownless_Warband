---
description: Sửa lỗi biên dịch C# trong Unity mà không phá vỡ kiến trúc Assembly Definition
---

---
description: Sửa lỗi biên dịch C# trong Unity mà không phá vỡ kiến trúc Assembly Definition
---

1. Đọc thông báo lỗi (Compiler Error) mà người dùng vừa dán vào.
2. Kiểm tra file bị lỗi và các file liên quan, đặc biệt chú ý:
   - Thiếu `using` namespace (`CrownlessWarband.Core.Battle`, `CrownlessWarband.Core.AI`, v.v.).
   - Vi phạm ranh giới Assembly Definition (`.asmdef`), ví dụ gọi `Presentation` từ `Core`.
   - Lỗi không khớp kiểu dữ liệu hoặc thiếu tham số khởi tạo.
3. Sửa trực tiếp vào file bị lỗi với thay đổi tối thiểu, không viết lại toàn bộ cấu trúc class nếu không cần thiết.