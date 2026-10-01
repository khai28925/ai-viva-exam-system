# AI Viva Exam System

Hệ thống thi vấn đáp có hỗ trợ AI cho Entrepreneurship Experience Project.

## Công nghệ

- **Frontend:** React 19, Vite 8
- **Backend:** ASP.NET Core 10 Web API
- **Kiến trúc backend:** Modular monolith, tách Domain/Application/Infrastructure/API
- **Database dự kiến:** PostgreSQL
- **Dịch vụ ngoài dự kiến:** Speech-to-Text, LLM, Text-to-Speech

## Cấu trúc repository

```text
.
├── backend/
│   ├── src/
│   │   ├── AiViva.Api/
│   │   ├── AiViva.Application/
│   │   ├── AiViva.Domain/
│   │   └── AiViva.Infrastructure/
│   └── AiVivaExamSystem.sln
├── frontend/
│   ├── src/
│   │   ├── api/
│   │   └── styles/
│   └── package.json
└── docs/
    └── diagram/
```

## Yêu cầu môi trường

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0): `10.0.401`
  (cho phép bản patch mới hơn trong cùng feature band qua `global.json`).
- Node.js `22.19.0`, đồng bộ bằng `.nvmrc` và `.node-version` ở thư mục gốc.

## Chạy dự án

Mở hai terminal tại thư mục gốc.

Backend:

```bash
cd backend
dotnet restore AiVivaExamSystem.sln
dotnet run --project src/AiViva.Api
```

Frontend:

```bash
cd frontend
npm ci
npm run dev
```

Truy cập `http://localhost:5173`. Vite sẽ proxy `/api` sang backend tại
`http://localhost:5065`.

## API mẫu

- `GET /api/health`: kiểm tra trạng thái API.
- `GET /api/v1/exams`: luồng mẫu Domain → Application → Infrastructure → API.
- Swagger UI (Development): `http://localhost:5065/swagger`.
- OpenAPI JSON: `http://localhost:5065/swagger/v1/swagger.json`.
- `GET /api/v1/exams?limit=0` trả HTTP 400 với lỗi validation; `limit` hợp lệ
  từ 1 đến 100, mặc định 50.

## Kiểm tra trước khi mở PR

```bash
cd backend
dotnet build AiVivaExamSystem.sln
dotnet format AiVivaExamSystem.sln --verify-no-changes --no-restore
```

```bash
cd frontend
npm ci
npm run check
```

`npm run check` chạy ESLint, Prettier check, Vitest và production build.
Format frontend bằng `npm run format`, backend bằng `dotnet format`.

## Hướng phát triển tiếp theo

- Thay `InMemoryExamRepository` bằng EF Core + PostgreSQL.
- Thêm authentication/authorization và các role Admin, Lecturer, Student.
- Tách module Question Bank, Viva Session, Assessment và Reporting.
- Bổ sung unit test, integration test và CI pipeline.

## Quy ước Git

- `main`: phiên bản ổn định.
- `develop`: nhánh tích hợp.
- `feature/*`, `fix/*`, `docs/*`: nhánh theo từng task, tạo từ `develop` và mở
  pull request quay lại `develop`.
- Prefix commit khuyến nghị: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`,
  `chore:`.
