# MVP contract: Đăng nhập và phân quyền

Tài liệu này mô tả lát cắt xác thực/phân quyền cho ba vai trò `ADMIN`,
`LECTURER`, `STUDENT`. Conceptual ERD của dự án có các thực thể
`ROLE` và `USER` định hướng nghiệp vụ, không tự động ấn định tên bảng, PK/FK
hay cấu trúc token. Schema vật lý và migration phải theo code EF Core.

## Phạm vi và quy tắc

- Tài khoản đăng nhập bằng email và mật khẩu. Không có đăng ký công khai.
- Admin đầu tiên được tạo bằng cấu hình môi trường tại backend. Sau đó chỉ admin
  mới được tạo tài khoản `LECTURER` hoặc `STUDENT` qua API. API không cho tạo
  admin mới hoặc tự nâng quyền.
- Mật khẩu không xuất hiện trong response hoặc log và phải được lưu dưới dạng
  hash. Không commit mật khẩu, cookie hoặc connection string vào repository.
- Phiên đăng nhập dùng cookie `HttpOnly`; frontend không đọc cookie bằng
  JavaScript. Browser phải gửi cookie khi gọi API (`credentials: 'include'`).
- Các request thay đổi dữ liệu dùng CSRF token lấy từ API. Token không phải
  access token và không thay thế cookie đăng nhập.

## HTTP API

JSON dùng `camelCase`. Base URL local mặc định là `http://localhost:5065`.

| Method | Path | Quyền | Kết quả thành công |
| --- | --- | --- | --- |
| `GET` | `/api/v1/auth/csrf` | Anonymous | `200` + `{ "token": "<csrf-token>" }` |
| `POST` | `/api/v1/auth/login` | Anonymous | `200` + user, cấp auth cookie `HttpOnly` |
| `GET` | `/api/v1/auth/me` | Đã đăng nhập | `200` + thông tin người dùng hiện tại |
| `POST` | `/api/v1/auth/logout` | Đã đăng nhập | `204`, xóa phiên đăng nhập |
| `GET` | `/api/v1/admin/users` | `ADMIN` | `200` + danh sách người dùng |
| `GET` | `/api/v1/admin/users/{userId}` | `ADMIN` | `200` + thông tin người dùng |
| `POST` | `/api/v1/admin/users` | `ADMIN` | `201` + user, chỉ tạo `LECTURER`/`STUDENT` |

- Login request: `{ "email": "lecturer@example.com", "password": "<mật khẩu>" }`.
- Tạo user request: `{ "email": "student@example.com", "password": "<mật khẩu>", "role": "STUDENT" }`.
  Mật khẩu tài khoản mới dài 12–128 ký tự.
- `GET /api/v1/auth/me` trả trực tiếp `{ "id": "<uuid>", "email": "<email>", "role": "LECTURER" }`.
  User response không có `password`, password hash hoặc dữ liệu phiên.
  `GET /api/v1/admin/users` trả JSON array của các user response.
- Login sai thông tin trả `401` chung, không tiết lộ email có tồn tại hay không.
  Chưa đăng nhập trả `401`; đã đăng nhập nhưng sai vai trò trả `403`.
- Request thiếu/sai định dạng trả `400`; email trùng khi admin tạo user trả
  `409`. Thiếu/sai CSRF token trả `400`; login bị giới hạn tần suất có thể trả
  `429`. Lỗi trả Problem Details theo chuẩn API hiện có.

## CSRF và frontend

Gọi `GET /api/v1/auth/csrf` với cookie credentials trước request thay đổi dữ
liệu, rồi gửi giá trị `token` trong header `X-CSRF-TOKEN` và tiếp tục gửi cookie.
Áp dụng cho login, logout, tạo user và các thao tác ghi Question Bank/Question. CSRF
token không dùng thay cho kiểm tra role. Frontend nên gọi API cùng origin hoặc
qua proxy/CORS được cấu hình hỗ trợ credentials; không tắt CSRF để demo. Với
React/Vite local, dùng proxy `/api` tới backend để browser gọi cùng origin.

## Ma trận quyền MVP

| Chức năng | `ADMIN` | `LECTURER` | `STUDENT` |
| --- | --- | --- | --- |
| Đăng nhập, `GET /auth/me`, đăng xuất | Có | Có | Có |
| Xem và quản lý Question Bank/Question | Có | Có | Không |
| Xem và tạo user qua `/api/v1/admin/users` | Có | Không | Không |

Quyền Question Bank hiện ở mức role, **chưa có ràng buộc owner**: lecturer có
quyền trên các bank mà API cho phép, không chỉ bank do mình tạo. Không suy
diễn rằng phân quyền theo từng giảng viên đã hoàn thành.

`STUDENT` mới có tài khoản và phiên đăng nhập. Luồng lịch thi, làm bài, nộp
đáp án và xem kết quả chưa thuộc lát cắt này. Endpoint Exam GET của skeleton
không chứng minh rằng luồng thi cho student đã hoàn thành.

## Khởi tạo local

Chạy EF migration thủ công bằng lệnh trong `backend/README.md`. Backend vẫn dùng
`ConnectionStrings__QuestionBank` cho PostgreSQL; không tự chạy migration khi
API khởi động. Đặt `AIVES_BOOTSTRAP_ADMIN_EMAIL` và
`AIVES_BOOTSTRAP_ADMIN_PASSWORD` (12–128 ký tự) trong môi trường local để
tạo admin ban đầu, không lưu giá trị thật trong file. Sau khi đã khởi tạo thành
công, gỡ mật khẩu bootstrap khỏi shell/môi trường và chỉ quản lý tài khoản qua
API admin. Bootstrap chạy lúc API start nếu có đủ hai biến và chưa có admin;
nếu đã có admin thì không reset mật khẩu. Trong production phải dùng HTTPS,
cookie bảo mật và nơi lưu Data Protection keys bền vững dùng chung giữa các
instance, nếu không cookie có thể mất hiệu lực sau deploy/khởi động lại.
