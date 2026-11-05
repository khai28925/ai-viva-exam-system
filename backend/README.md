# Backend

ASP.NET Core Web API là một ứng dụng triển khai duy nhất, gồm các module
Identity, QuestionPreparation, Viva và LegacyExams. Mỗi module có phần API,
Application và Infrastructure; các module trao đổi qua `AiViva.ModuleContracts`.
Xem [ranh giới và quy tắc phụ thuộc](ARCHITECTURE.md) trước khi thêm tính năng.

Các project dùng chung cho các module:

- `AiViva.Domain`: entity và business rule cốt lõi.
- `AiViva.Application`: use case, DTO và abstraction.
- `AiViva.Infrastructure`: persistence và tích hợp dịch vụ ngoài.
- `AiViva.ModuleContracts`: contract đọc dữ liệu tối thiểu giữa các module.
- `AiViva.Api`: HTTP endpoints, middleware và dependency injection.

## Chạy local

Backend và lệnh migration tự đọc `.env` ở thư mục gốc repository. Dùng mẫu
`../.env.example` nếu máy thành viên chưa có file này. Điền `GEMINI_API_KEY`;
đổi `Gemini__BaseUrl`/`Gemini__Model` khi cần. Có thể bỏ dấu `#` và điền
`ConnectionStrings__QuestionBank` để lưu kết nối local tại đây. Biến môi trường
đã đặt trong PowerShell được ưu tiên hơn `.env`; cần khởi động lại backend sau
khi sửa file. File `.env` được Git bỏ qua, còn `.env.example` dùng chung cho nhóm.

Xem [hướng dẫn setup PostgreSQL cho cả nhóm](../docs/database-setup.md) để chọn
Docker Compose hoặc PostgreSQL cài trực tiếp; DBeaver không bắt buộc.

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
$geminiKey = Read-Host 'Gemini API key' -AsSecureString
$env:GEMINI_API_KEY = [System.Net.NetworkCredential]::new('', $geminiKey).Password
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

## WF1: tạo câu hỏi và rubric bằng Gemini

Trang lecturer `http://localhost:5173/wf1-preparation` hỗ trợ luồng: tạo môn
học và ngân hàng, tải PDF tối đa 10 MB, chọn số câu hỏi, để Gemini tạo bản
nháp, chỉnh sửa/loại/duyệt câu hỏi, rồi duyệt bộ rubric dùng chung của môn học.
AI không tự đưa câu hỏi vào ngân hàng chính thức; thao tác duyệt của giảng viên
là bắt buộc. Câu hỏi lưu riêng mức Bloom (`Remember`, `Understand`, `Apply`,
`Analyze`) và độ khó từ 1 đến 5. Tỷ trọng nằm ở cấp rubric và phải cộng đúng
100% trước khi duyệt.

Sinh câu hỏi dùng RAG trên tài liệu môn học:

1. Khi tải PDF, backend trích văn bản từng trang (PdfPig), chia thành các đoạn
   có số trang và tạo embedding bằng `Gemini__EmbeddingModel` (mặc định
   `gemini-embedding-001`, 768 chiều) lưu trong bảng `study_document_chunks`.
2. Khi sinh câu hỏi, giảng viên có thể nhập chủ đề/chương. Hệ thống xếp hạng các
   đoạn theo độ tương đồng embedding kết hợp BM25 (gộp dấu tiếng Việt); không có
   chủ đề thì lấy mẫu đều toàn bộ tài liệu. Chỉ các đoạn được chọn được gửi cho
   Gemini, và mỗi câu hỏi AI lưu lại nguồn (tệp, trang, trích đoạn) để giảng
   viên đối chiếu trước khi duyệt.
3. Nếu chưa có API key hoặc dịch vụ embedding lỗi, các đoạn vẫn được lưu và truy
   xuất theo từ khóa; nút **Lập chỉ mục lại** tạo embedding sau. PDF scan không
   có lớp chữ được gửi nguyên văn cho Gemini như trước. Tài liệu tải lên trước
   khi có RAG được lập chỉ mục ở lần sinh câu hỏi đầu tiên.

Bỏ chọn **Đề xuất lại bộ rubric** để chỉ sinh câu hỏi mà không ghi đè bản nháp
rubric đang sửa. Giảng viên cũng có thể tự tạo, thêm/xóa rubric và tiêu chí rồi
**Lưu và duyệt** mà không cần Gemini. Trang `/question-banks` cho phép tạo câu hỏi
thủ công kèm Bloom/độ khó và **Nhập CSV** (cột `content`, `bloomLevel`,
`difficultyLevel`, `expectedKeyPoints` phân tách bằng `|`; nhận tiêu đề và nhãn
Bloom tiếng Việt, dấu `,` hoặc `;`). Một dòng lỗi thì không dòng nào được nhập.
Rubric cấp ngân hàng (`/question-banks/{id}/rubric`) là dữ liệu cũ, không dùng để
chấm; chấm điểm luôn dùng bộ rubric đã duyệt của môn học.

API key chỉ đặt ở tiến trình backend qua `GEMINI_API_KEY`, tuyệt đối không đặt
trong Vite, source code hoặc commit. Nếu không cấu hình key, toàn bộ CRUD môn
học/tài liệu/duyệt vẫn hoạt động; riêng thao tác sinh nội dung trả lỗi 503 rõ
ràng. Sau khi dừng API, có thể xóa key khỏi PowerShell bằng:

```powershell
Remove-Item Env:\GEMINI_API_KEY
```

`ADMIN` và `LECTURER` được quản lý Question Bank/Question; `STUDENT` có thể
tham gia phiên mở, thi vấn đáp với AI và xem bảng điểm của chính mình. Quyền
Question Bank chưa giới hạn theo owner/giảng viên tạo bank. Chi tiết request,
response, lỗi và ma trận quyền xem [auth MVP contract](../docs/auth-mvp-contract.md).

## Thi vấn đáp AI và báo cáo (nhóm 3, 6)

Chạy migration mới trước khi khởi động API; dùng chuỗi kết nối và API key Gemini
đã cấu hình ở phiên PowerShell hiện tại:

```powershell
dotnet tool restore
dotnet ef database update --project src/AiViva.Infrastructure --startup-project src/AiViva.Infrastructure
dotnet run --project src/AiViva.Api --launch-profile http
```

1. Giảng viên chuẩn bị WF1: câu hỏi có ý chính, bộ rubric đã duyệt. Tạo phiên
   Draft, chọn câu hỏi, cấu hình thời gian mỗi câu (30–600 giây), hỏi thêm (0–5
   lần/câu chính), ngôn ngữ Việt/Anh, cách trả lời rồi mở phiên.
2. Sinh viên vào `/exam-sessions`, bấm **Vào thi**, đồng ý dùng micro và bắt đầu.
   Câu hỏi được đọc qua giọng đọc của trình duyệt (chọn giọng đúng ngôn ngữ nếu
   máy có; giọng Việt tùy máy/trình duyệt). Có hai cách trả lời:
   - **Chỉ giọng nói** (mặc định cho phiên mới): **Bắt đầu trả lời** ghi âm và
     hiển thị transcript xem trước gần thời gian thực (Chrome/Edge). Khi kết thúc
     hoặc hết giờ, bản ghi được gửi để Gemini chép lời ở backend; chỉ transcript
     này được chấm, nội dung gõ tay bị bỏ qua. Câu hỏi được gửi kèm làm ngữ cảnh
     để nhận đúng thuật ngữ; ý chính kỳ vọng thì không bao giờ được gửi.
   - **Giọng nói, cho phép sửa/gõ**: như trước, sinh viên có thể sửa transcript
     hoặc ghi âm rồi chọn **Chép lời và gửi bản ghi** (hỗ trợ tiếp cận). Phiên
     tạo trước migration này giữ cách này.
   Mỗi câu chỉ gửi một bản ghi tối đa 10 MB. Độ trễ chép lời được ghi vào
   `service_interaction_logs` (`STT`) bên cạnh độ trễ chấm (`LLM`).
3. Backend chấm theo snapshot rubric, sinh câu hỏi làm rõ dựa trên câu trả lời,
   kiểm soát giới hạn hỏi thêm, deadline và lượt gửi trùng. Câu trả lời gửi đúng
   giờ được giữ khi Gemini lỗi hoặc kết nối bị ngắt; bấm gửi lại để tiếp tục.
   Đóng phiên chỉ ngừng nhận sinh viên mới; lượt thi đã tạo có thể hoàn thành.
4. `/reports` hiển thị kết quả từng câu chính và các lượt hỏi thêm, transcript,
   mức Bloom, thời gian trả lời, điểm tiêu chí và nhận xét. Điểm AI là gợi ý trên
   thang 10; hỏi thêm được tính chung vào câu chính, các câu chính có trọng số
   bằng nhau. Sau khi chốt, mỗi câu hiển thị cả điểm AI và điểm giảng viên.
5. Giảng viên xem bài, chỉnh/chốt điểm từng câu; hệ thống công bố điểm cuối cho
   sinh viên. Giảng viên xem thống kê theo phiên (nhóm sinh viên cùng bài thi):
   số bài đã chốt, điểm trung bình/cao nhất/thấp nhất, tỷ lệ trả lời tốt (từ
   7/10), phân bố điểm, kết quả theo mức Bloom, câu có điểm trung bình thấp nhất
   kèm thời gian trả lời trung bình và các ý hay bị thiếu. Thống kê dùng điểm đã
   chốt nếu có, nếu chưa thì dùng điểm AI gợi ý.
6. **Xuất bảng điểm Excel** (`/export?format=xlsx`, mặc định) theo mẫu: STT, MSSV,
   Họ và tên, Email, điểm giảng viên từng câu, điểm AI gợi ý, điểm chốt, trạng
   thái, thời điểm hoàn thành (GMT+7) và nhận xét; `format=csv` cho cùng dữ liệu.
   Admin nhập họ tên và MSSV khi tạo tài khoản hoặc sửa hồ sơ ở `/admin`.

Mở hai trình duyệt hoặc dùng cửa sổ riêng tư để demo Lecturer/Student cùng lúc;
hai cửa sổ thông thường của cùng trình duyệt chia sẻ cookie đăng nhập.
Nhận giọng nói qua trình duyệt phụ thuộc hỗ trợ của trình duyệt và kết nối mạng;
ghi âm/chép lời Gemini là lựa chọn thay thế. Micro cần localhost hoặc HTTPS.
Âm thanh chỉ xử lý để chép lời, không lưu bản ghi lâu dài; hệ thống lưu transcript
và kết quả chấm trong PostgreSQL. Không gửi tài liệu/âm thanh thật khi chưa có sự
đồng ý của người tham gia. Đây không phải luồng ghi hình/giám sát nhóm 5.

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

`AddApplication()` và `AddInfrastructure()` gọi phần đăng ký của từng module.
`QuestionBankDbContext` được đăng ký một lần cho cùng scope HTTP; các module
không đọc trực tiếp bảng của nhau mà dùng contract trong
`AiViva.ModuleContracts`. HTTP services, Swagger và Problem Details nằm trong
`AddApi()`.

SDK được chọn bởi `global.json` ở repository root. Khi API đang chạy, dừng bằng
Ctrl+C trước khi build lại để tránh khóa DLL trên Windows.
