---
description: Tạo hoặc mở rộng hệ thống Utility AI / Squad AI đảm bảo chuẩn Explainable AI (F3 Debug)
---

---
description: Tạo hoặc mở rộng hệ thống Utility AI / Squad AI đảm bảo chuẩn Explainable AI (F3 Debug)
---

1. Kiểm tra các class hiện có trong `Assets/_Project/Scripts/Core/AI/` và `Assets/_Project/Scripts/Core/Battle/`.
2. Đảm bảo mọi hàm tính điểm (`EvaluateTarget`, `EvaluateAction`, `SelectSquadStrategy`) đều lưu lại chi tiết từng thành phần điểm số (ví dụ: `Threat`, `StrategicValue`, `KillProbability`, `Vulnerability`, `RolePriority`, `CounterRisk`, `SquadDirectiveBonus`) vào cấu trúc dữ liệu log/debug.
3. Viết code C# sạch, đầy đủ XML comment tiếng Việt giải thích ý nghĩa từng công thức để phục vụ bảo vệ đồ án.