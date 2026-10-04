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
dotnet run --project src/AiViva.Api
```

Nếu đã có PostgreSQL local, bỏ qua `docker compose` và đặt
`ConnectionStrings__QuestionBank` theo database/user của bạn. Cần tạo database
trước khi chạy migration hoặc dùng user có quyền tạo database. `dotnet-ef`
được khóa ở `backend/.config/dotnet-tools.json`; migration đầu tiên nằm trong
`AiViva.Infrastructure/Persistence/Migrations`. Migration chỉ chạy khi gọi
`dotnet ef database update`, không tự chạy lúc API khởi động.

Kiểm tra nhanh trên database thật:

1. Mở `http://localhost:5065/swagger` (Development).
2. Tạo một Question Bank rồi tạo một Question trong bank đó.
3. Dừng API bằng Ctrl+C, chạy lại và GET cả hai resource: dữ liệu phải còn.
4. Xóa bank khi còn question phải trả `409`; xóa question trước, sau đó xóa bank
   phải thành công. Endpoint dùng contract trong `docs/question-bank-mvp-contract.md`.

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
