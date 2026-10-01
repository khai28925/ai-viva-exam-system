# Backend

ASP.NET Core Web API theo hướng modular monolith với các project:

- `AiViva.Domain`: entity và business rule cốt lõi.
- `AiViva.Application`: use case, DTO và abstraction.
- `AiViva.Infrastructure`: persistence và tích hợp dịch vụ ngoài.
- `AiViva.Api`: HTTP endpoints, middleware và dependency injection.

## Chạy local

```bash
dotnet restore AiVivaExamSystem.sln
dotnet run --project src/AiViva.Api
```

API mặc định: `http://localhost:5065`.

Endpoints mẫu:

- `GET /api/health`
- `GET /api/v1/exams`

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
