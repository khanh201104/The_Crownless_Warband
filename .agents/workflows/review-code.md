---
description: Đóng vai Senior C# Reviewer để kiểm tra toàn bộ code vừa sinh ra theo chuẩn The Crownless Warband
---

---
description: Đóng vai Senior C# Reviewer để kiểm tra toàn bộ code vừa sinh ra theo chuẩn The Crownless Warband
---

Bạn là Senior Software Engineer & Unity Technical Lead đang review code của dự án **The Crownless Warband**.
Hãy đọc các file vừa chỉnh sửa/tạo mới và kiểm tra nghiêm ngặt theo Checklist sau:

1. **Kiến trúc Core (Bắt buộc):**
   - Có class nào trong `CrownlessWarband.Core` dính `MonoBehaviour`, `Transform`, `GameObject` hay `Coroutine` không?
   - Có vi phạm ranh giới Assembly Definition không?
2. **Logic & Dữ liệu:**
   - Mọi đòn đánh/kỹ năng có đẩy đúng `CombatEvent` vào `EventQueue` không?
   - Các chỉ số máu, khiên, hồi năng lượng có bị vượt ngưỡng (Clamping: `0 <= HP <= MaxHP`) không?
3. **Explainable AI:**
   - Các hàm tính điểm Utility AI có lưu đầy đủ dữ liệu điểm thành phần (`Threat`, `KillProbability`, v.v.) vào bản ghi debug cho chế độ F3 không?
4. **An toàn bộ nhớ & Null-safety:**
   - Có khả năng xảy ra `NullReferenceException` khi mục tiêu vừa chết trong turn không?

**Đầu ra yêu cầu:**
- Nếu phát hiện lỗi: Chỉ rõ tên file, số dòng, lý do lỗi và đề xuất code sửa lại chính xác.
- Nếu đạt chuẩn: Xác nhận "PASS" và tóm tắt ngắn gọn trạng thái sẵn sàng để người dùng test trên Unity.