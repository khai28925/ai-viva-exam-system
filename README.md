# AI Viva Exam System

Hệ thống thi vấn đáp có hỗ trợ AI cho Entrepreneurship Experience Project.

## Công nghệ

- **Frontend:** React 19, Vite 8
- **Backend:** ASP.NET Core 10 Web API
- **Kiến trúc backend:** Một API modular monolith với các module Identity, QuestionPreparation, Viva và LegacyExams; các module dùng chung các lớp Domain/Application/Infrastructure và trao đổi qua `AiViva.ModuleContracts`
- **Database:** PostgreSQL (Question Bank và tài khoản trên nhánh tích hợp)
- **Dịch vụ ngoài dự kiến:** Speech-to-Text, LLM, Text-to-Speech

## Cấu trúc repository

```text
.
├── backend/
│   ├── src/
│   │   ├── AiViva.Api/
│   │   ├── AiViva.Application/
│   │   ├── AiViva.Domain/
│   │   ├── AiViva.Infrastructure/
│   │   └── AiViva.ModuleContracts/
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

URL API và Gemini key được cấu hình trong `.env` tại thư mục gốc. Nếu chưa có,
sao chép `.env.example` thành `.env`. Điền `GEMINI_API_KEY` và, nếu cần, chuỗi
kết nối PostgreSQL. Frontend, backend và lệnh migration tự đọc file này; khởi
động lại các tiến trình sau khi chỉnh sửa. Chỉ `.env.example` được đưa lên Git.

Question Bank hiện dùng PostgreSQL và yêu cầu đăng nhập. Trước lần chạy đầu,
thực hiện [hướng dẫn setup database cho thành viên](docs/database-setup.md),
sau đó xem [bootstrap admin](backend/README.md); các lệnh
`dotnet run` dưới đây giả định terminal đã có chuỗi kết nối đúng.
Xem [hướng dẫn demo CRUD FE → BE → PostgreSQL](docs/issue-8-demo.md) để chuẩn bị
dữ liệu và mở `/question-banks` bằng tài khoản `ADMIN`/`LECTURER`.

### Chạy nhanh bằng một lệnh (Windows)

Sau khi `.env` đã có `GEMINI_API_KEY` và dòng `ConnectionStrings__QuestionBank`
(bỏ dấu `#` và điền mật khẩu PostgreSQL), chạy tại thư mục gốc:

```powershell
.\start-dev.cmd
```

Script [`scripts/Start-Dev.ps1`](scripts/Start-Dev.ps1) sẽ:

1. Đọc `.env`. Biến môi trường đã đặt sẵn trong terminal được ưu tiên.
2. Kiểm tra PostgreSQL. Nếu chưa chạy, script bật Docker Desktop (nếu cần), rồi
   bật container đặt trong `AIVES_DB_CONTAINER` (nếu có trong `.env`), hoặc với
   `localhost:5432` thì chạy `backend/compose.postgres.yml`, dùng mật khẩu lấy từ
   chuỗi kết nối. Dòng sai cú pháp trong `.env` được báo theo số dòng, vì backend
   sẽ không khởi động với dòng đó.
3. Chạy `dotnet ef database update`.
4. Mở backend và frontend trong hai cửa sổ riêng.
5. Mở `http://localhost:5173` bằng Edge.

Tùy chọn: `-SkipMigrations` (bỏ qua migration), `-NoBrowser` (không mở trình
duyệt). Dừng app bằng cách đóng hai cửa sổ "AIVES backend" và "AIVES frontend".
Database Docker vẫn chạy cho tới khi gọi
`docker compose -f backend/compose.postgres.yml stop`.

### Chạy thủ công

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

- [Contract MVP Question Bank CRUD](docs/question-bank-mvp-contract.md): baseline
  cho API, database, frontend và các sơ đồ của milestone CRUD.
- [Demo và checklist Issue #8](docs/issue-8-demo.md),
  [Class Diagram](docs/diagram/class-diagram-question-bank.svg),
  [Physical ERD + data dictionary đã đối chiếu](docs/diagram/question-bank/README.md).
- Question Bank đã dùng EF Core + PostgreSQL. `InMemoryExamRepository` chỉ còn
  phục vụ endpoint Exam mẫu; thay nó khi triển khai nghiệp vụ phiên thi thật.
- Đăng nhập/phân quyền Admin, Lecturer, Student đã được merge vào `develop`
  qua [PR #20](https://github.com/khai28925/ai-viva-exam-system/pull/20).
- Cùng nhóm nghiệm thu giao diện và luồng demo Issue #8 trước khi merge.
- Tách module Question Bank, Viva Session, Assessment và Reporting.
- Mở rộng test cho các module mới và bổ sung CI pipeline.

## Quy ước Git

- `main`: phiên bản ổn định.
- `develop`: nhánh tích hợp.
- `feature/*`, `fix/*`, `docs/*`: nhánh theo từng task, tạo từ `develop` và mở
  pull request quay lại `develop`.
- Prefix commit khuyến nghị: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`,
  `chore:`.
