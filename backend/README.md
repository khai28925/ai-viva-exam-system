# Backend

ASP.NET Core Web API theo hướng modular monolith với các project:

- `AiViva.Domain`: entity và business rule cốt lõi.
- `AiViva.Application`: use case, DTO và abstraction.
- `AiViva.Infrastructure`: persistence và tích hợp dịch vụ ngoài.
- `AiViva.Api`: HTTP endpoints, middleware và dependency injection.

## Chạy local

Question Bank dùng PostgreSQL. API cần biến môi trường
`ConnectionStrings__QuestionBank`; không có fallback in-memory để tránh tưởng
dữ liệu đã được lưu thật. Không commit chuỗi kết nối hoặc mật khẩu vào repo.

Nếu dùng Docker Desktop, chạy trong PowerShell tại `backend/`:

```powershell
$env:AIVES_POSTGRES_PASSWORD = Read-Host 'Local PostgreSQL password'
docker compose -f compose.postgres.yml up -d --wait
$env:ConnectionStrings__QuestionBank = "Host=localhost;Port=5432;Database=aives;Username=aives;Password=$env:AIVES_POSTGRES_PASSWORD"
dotnet tool restore
dotnet restore AiVivaExamSystem.sln
dotnet ef database update --project src/AiViva.Infrastructure --startup-project src/AiViva.Infrastructure
$env:AIVES_BOOTSTRAP_ADMIN_EMAIL = 'admin@example.local'
$bootstrapAdminPassword = Read-Host 'Initial admin password (12-128 characters)' -AsSecureString
$env:AIVES_BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $bootstrapAdminPassword).Password
dotnet run --project src/AiViva.Api
```

Nếu đã có PostgreSQL local, bỏ qua `docker compose` và đặt
`ConnectionStrings__QuestionBank` theo database/user của bạn. Cần tạo database
trước khi chạy migration hoặc dùng user có quyền tạo database. `dotnet-ef`
được khóa ở `backend/.config/dotnet-tools.json`; migration đầu tiên nằm trong
`AiViva.Infrastructure/Persistence/Migrations`. Migration chỉ chạy khi gọi
`dotnet ef database update`, không tự chạy lúc API khởi động.

Admin đầu tiên được tạo lúc API khởi động nếu có đủ hai biến
`AIVES_BOOTSTRAP_ADMIN_EMAIL` và `AIVES_BOOTSTRAP_ADMIN_PASSWORD` mà database
chưa có admin. Nếu đã có admin, khởi động lại **không** reset mật khẩu. Sau khi
bootstrap thành công, dừng API và xóa mật khẩu khỏi phiên PowerShell:

```powershell
Remove-Item Env:\AIVES_BOOTSTRAP_ADMIN_PASSWORD
Remove-Item Env:\AIVES_BOOTSTRAP_ADMIN_EMAIL
```

Không lưu mật khẩu bootstrap trong file cấu hình, lịch sử lệnh hoặc git. Ở
production, dùng secret manager, HTTPS và lưu Data Protection keys bền vững/dùng
chung giữa các instance để auth cookie không mất hiệu lực sau deploy.

Kiểm tra nhanh trên database thật:

1. Chạy FE, đăng nhập `ADMIN`/`LECTURER` và mở `http://localhost:5173/question-banks`.
2. Tạo một Question Bank rồi tạo một Question trong bank đó. Swagger ở
   `http://localhost:5065/swagger` dùng tham khảo contract; request ghi dữ liệu
   trực tiếp vẫn cần cookie đăng nhập và CSRF header.
3. Dừng API bằng Ctrl+C, chạy lại và GET cả hai resource: dữ liệu phải còn.
4. Xóa bank khi còn question phải trả `409`; xóa question trước, sau đó xóa bank
   phải thành công. Endpoint dùng contract trong `docs/question-bank-mvp-contract.md`.

Xem [kịch bản demo Issue #8](../docs/issue-8-demo.md) cho seed dữ liệu qua API,
script `Test-QuestionBankSmoke.ps1`, kiểm tra UI thật và đối chiếu Physical ERD.
Smoke Question Bank cũng chạy test React/jsdom với HTTP thật; cần `npm ci`
ở frontend trước. Test không thay thế kiểm tra trực quan trong trình duyệt.

Để xem SQL mà không cập nhật database:

```powershell
dotnet ef migrations script --project src/AiViva.Infrastructure --startup-project src/AiViva.Infrastructure
```

Docker volume `aives_postgres_data` giữ dữ liệu khi dừng container. Tránh dùng
`docker compose down --volumes` nếu muốn giữ dữ liệu demo.

### Chỉ chạy API sau khi đã cấu hình database

```bash
dotnet restore AiVivaExamSystem.sln
dotnet run --project src/AiViva.Api
```

API mặc định: `http://localhost:5065`.

Endpoints mẫu:

- `GET /api/health`
- `GET /api/v1/exams`
- CRUD `/api/v1/question-banks` và `/api/v1/question-banks/{bankId}/questions`

## Đăng nhập và phân quyền MVP

Ba vai trò: `ADMIN`, `LECTURER`, `STUDENT`. Không có đăng ký công khai. Admin
đầu tiên do bootstrap tạo, sau đó chỉ admin được tạo `LECTURER`/`STUDENT` qua
`POST /api/v1/admin/users`; không tạo admin qua endpoint này. Admin có thể xem
user bằng `GET /api/v1/admin/users`.

Luồng frontend: gọi `GET /api/v1/auth/csrf` để nhận `{ "token": "..." }`, sau đó
gọi `POST /api/v1/auth/login` với `{ "email": "...", "password": "..." }`,
cookie credentials và header `X-CSRF-TOKEN: <token>`. Dùng cookie credentials
cho `GET /api/v1/auth/me` (trả `{ id, email, role }`),
`POST /api/v1/auth/logout` và các API còn lại. Mọi request thay đổi dữ liệu
phải gửi CSRF header. Auth cookie là `HttpOnly`, không đọc bằng JavaScript.
React/Vite nên cấu hình proxy `/api` tới `http://localhost:5065` để dùng cùng
origin khi phát triển local.

`ADMIN` và `LECTURER` được quản lý Question Bank/Question; `STUDENT` hiện chỉ
có luồng đăng nhập, xem chính mình và đăng xuất, chưa có luồng thi. Quyền
Question Bank chưa giới hạn theo owner/giảng viên tạo bank. Chi tiết request,
response, lỗi và ma trận quyền xem [auth MVP contract](../docs/auth-mvp-contract.md).

## Kiểm thử đăng nhập và phân quyền với PostgreSQL

Khi Docker Desktop đang chạy, từ `backend/` chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-AuthSmoke.ps1
```

Script tạo PostgreSQL tạm trên cổng riêng, chạy migration và kiểm tra đăng nhập,
CSRF, ba vai trò, tài khoản trùng và giới hạn thử đăng nhập. Mật khẩu test được
sinh ngẫu nhiên. API test và container được dọn sau khi chạy; dữ liệu test
không được giữ lại. Script không dùng database demo đang có.

## API documentation và validation

- Development: Swagger UI tại `/swagger`, OpenAPI JSON tại `/swagger/v1/swagger.json`.
- Query mẫu: `/api/v1/exams?limit=10`; `limit` từ 1 đến 100, mặc định 50.
- `AddValidation()` kiểm tra DataAnnotations trên request/parameter của Minimal API.
  Dùng `[Required]`, `[StringLength]`, `[Range]` cho request mới.
- Lỗi validation trả `400 application/problem+json`, có `errors` và `traceId`.
- Lỗi không xử lý và các HTTP error chưa có body dùng Problem Details với
  `status`, `title`, `instance`, `traceId`; exception không được gửi cho client.
- Health check hiện chỉ báo API đang chạy, chưa kiểm tra database/dịch vụ AI.

## Dependency injection

Đăng ký use case/service tại `AiViva.Application/DependencyInjection.cs`
(`AddApplication()`), repository và adapter tại
`AiViva.Infrastructure/DependencyInjection.cs` (`AddInfrastructure()`).
HTTP services, Swagger và Problem Details nằm trong `AddApi()`.

SDK được chọn bởi `global.json` ở repository root. Khi API đang chạy, dừng bằng
Ctrl+C trước khi build lại để tránh khóa DLL trên Windows.
