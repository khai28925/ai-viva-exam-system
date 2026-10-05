# Issue #8 — Tích hợp Question Bank và kịch bản demo

## Phạm vi và phần còn chờ

Phần tích hợp dùng UI React → API ASP.NET Core → PostgreSQL thật cho CRUD
`QuestionBank` và `Question`. API, schema và UI theo
[Question Bank contract](question-bank-mvp-contract.md) và
[auth contract](auth-mvp-contract.md).

Class Diagram cho lát cắt Question Bank CRUD đã có
[nguồn Mermaid](diagram/class-diagram-question-bank.mmd) và
[ảnh SVG](diagram/class-diagram-question-bank.svg). Sơ đồ phản ánh các lớp thật
ở Domain, Application, Infrastructure và API; không đưa phiên thi, rubric, AI,
STT hoặc TTS vào phần CRUD đã chạy. Vẫn cần nghiệm thu giao diện thực tế trước
khi đóng Issue #8.

Nhánh tích hợp kế thừa đăng nhập/phân quyền từ PR #20. PR #20 và PR sơ đồ tổng
quan #21 đã merge vào `develop`; nhánh Issue #8 cần đồng bộ các merge này trước
khi mở PR.

## Chuẩn bị môi trường

- .NET SDK theo `global.json`, Node theo `.nvmrc`/`.node-version`.
- Docker Desktop đang chạy, hoặc PostgreSQL riêng đã tạo database/user.
- API mặc định `http://localhost:5065`, FE `http://localhost:5173`.
- Làm demo trên database phát triển riêng; không dùng dữ liệu thật của người dùng.

Thực hiện phần **Chạy local** trong [backend/README.md](../backend/README.md):
khởi động PostgreSQL, đặt `ConnectionStrings__QuestionBank`, restore tool/package,
chạy `dotnet ef database update`, bootstrap admin đầu tiên rồi chạy API. Cần cả
migration `InitialQuestionBank` và `AddAuthUsers`. API không tự chạy migration.

Docker Compose ánh xạ PostgreSQL vào cổng `5432`; nếu cổng đó đã có dịch vụ khác,
dùng PostgreSQL riêng với chuỗi kết nối tương ứng hoặc đổi cấu hình cổng local.
Không dừng/xóa database của dự án khác để nhường cổng.

Mở terminal thứ hai tại repository root:

```powershell
cd frontend
npm ci
npm run dev
```

Vite proxy `/api` tới backend `5065`. Dùng cùng một hostname `localhost` trong
trình duyệt; không trộn `localhost` và `127.0.0.1` khi kiểm tra cookie phiên.
Không commit mật khẩu hoặc chuỗi kết nối. Mật khẩu bootstrap chỉ cần lúc tạo
admin đầu tiên; xóa biến chứa mật khẩu sau bước bootstrap như hướng dẫn backend.

## Đăng nhập và dữ liệu mẫu

1. Mở `http://localhost:5173/login`, đăng nhập bằng admin đã bootstrap.
2. Tại `/admin`, tạo một tài khoản `LECTURER` và một tài khoản `STUDENT` dùng cho
   demo. Đặt mật khẩu riêng ít nhất 12 ký tự; không chụp hoặc đưa mật khẩu lên PR.
3. Đăng nhập bằng `LECTURER` hoặc `ADMIN`, mở `/question-banks`.
4. Có thể tự thêm ngân hàng/câu hỏi từ UI, hoặc mở terminal thứ ba tại repository
   root và chạy script seed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\backend\scripts\Seed-QuestionBankDemo.ps1 -BaseUrl http://localhost:5065
```

Script hỏi email, mật khẩu dưới dạng nhập ẩn và yêu cầu xác nhận API đích trước
khi ghi dữ liệu. Chỉ dùng tài khoản `ADMIN`/`LECTURER`; script không tạo tài khoản
và không bootstrap admin. Dữ liệu được thêm qua API có cookie/CSRF, không chèn SQL
bỏ qua validation. Script chỉ thêm các bản ghi demo còn thiếu, không sửa/xóa dữ
liệu đang có; có thể chạy lại để bổ sung sau khi thực hành xóa. Dữ liệu mẫu gồm
hai bank `[AIVES DEMO] Software Architecture` và `[AIVES DEMO] Object-Oriented
Programming`, mỗi bank ba câu hỏi; description có marker `[seed:issue-8:v1]`.
Nếu gặp bank trùng tên nhưng không đúng marker/nội dung mô tả seed hoặc có nhiều
bank trùng tên đó, script dừng để tránh ghi nhầm vào bank của người khác.

Hai trang có mục đích khác nhau:

| Địa chỉ FE | Dữ liệu và mục đích |
| --- | --- |
| `/question-banks` | Đăng nhập `ADMIN`/`LECTURER`; gọi API và lưu PostgreSQL thật. Dùng cho demo CRUD xuyên suốt. |
| `/question-bank-preview.html` | Mock data của Issue #11, không cần backend; chỉ dùng trình bày UI/UX và trạng thái rỗng/loading/lỗi. Reload sẽ mất thay đổi mock. |

Không dùng preview để chứng minh persistence. Student không được quản lý
Question Bank; backend cũng trả `403`, không chỉ ẩn nút ở frontend. Hiện quyền
theo vai trò, chưa giới hạn ngân hàng theo giảng viên sở hữu.

## Kịch bản trình bày theo bốn yêu cầu

| Yêu cầu | Nội dung trình bày / bằng chứng |
| --- | --- |
| 1. UI/UX design | [Luồng và thiết kế](question-bank-ui-ux.md), hai mockup SVG đã có và giao diện thực tế. Mở preview khi muốn minh họa trạng thái lỗi/loading chủ động. |
| 2. Demo CRUD features | `/question-banks`, Network tab cho request API và kiểm tra dữ liệu sau restart bên dưới. |
| 3. Class Diagram | [Nguồn Mermaid](diagram/class-diagram-question-bank.mmd) và [ảnh SVG](diagram/class-diagram-question-bank.svg). Trình bày Entity → Service → Repository → DbContext và phân biệt với Physical ERD. |
| 4. Database diagram (Physical ERD) | [Ảnh hiện tại](diagram/question-bank/physical-erd.svg), [nguồn draw.io](<diagram/question-bank/Physical ERD.drawio>), [dictionary và đối chiếu migration](diagram/question-bank/README.md). |

Luồng demo CRUD đề nghị:

1. Đăng nhập Lecturer, mở `/question-banks`; chỉ rõ đây là dữ liệu từ API thật.
2. Tạo bank có tên dễ nhận biết, ví dụ `DEMO - Nhóm SWD392`, thêm mô tả. Reload
   trình duyệt và xác nhận bank vẫn còn; sửa tên/mô tả rồi tải lại để kiểm chứng.
3. Chọn bank, tạo hai câu hỏi; sửa nội dung một câu. Dùng tìm kiếm để lọc; xóa
   một câu qua hộp thoại xác nhận rồi kiểm tra danh sách.
4. Thử lưu tên/nội dung chỉ có khoảng trắng: form phải báo lỗi, không tạo bản ghi.
   API cũng kiểm tra giới hạn bank 120/500 ký tự và question 2000 ký tự.
5. Thử xóa bank còn câu hỏi: API trả `409`, UI báo rõ cần xóa câu hỏi trước và
   không giả vờ đã xóa thành công. Đóng hộp thoại, giữ lại dữ liệu để thử restart.
6. Ctrl+C ở terminal API, chạy lại `dotnet run --project src/AiViva.Api` từ
   `backend/` trong cùng terminal còn chuỗi kết nối. Không recreate/drop database.
   Refresh UI: bank/câu hỏi đã tạo/sửa vẫn còn. Đăng nhập lại nếu phiên hết hạn.
7. Xóa câu hỏi cuối cùng, rồi xóa bank rỗng. Reload và xác nhận cả hai đã biến mất.
8. Đăng xuất, đăng nhập Student; thử `/question-banks` phải bị từ chối. Không
   trình bày trang Student hiện tại như một luồng thi đã hoàn thành.

Để minh họa lỗi kết nối: dừng API rồi tải lại danh sách, quan sát thông báo lỗi;
khởi động API và chọn **Thử lại**. Request thất bại không được thay bằng dữ liệu
mock, không được coi là lưu thành công. Chưa đăng nhập/phiên hết hạn cần đăng
nhập lại; không bypass phân quyền chỉ để demo.

## Đối chiếu Physical ERD

Review tĩnh đã đối chiếu hai bảng với EF configuration và migration
`20261004200441_InitialQuestionBank`. Tên cột, kiểu, nullability, PK/FK,
`ON DELETE RESTRICT` và index đều khớp; không cần sửa migration. Nguồn draw.io
đã sửa quan hệ thành **bank có 0..* question**, bổ sung ghi chú FK/index và tên
bảng đúng lowercase. Xem [chi tiết review](diagram/question-bank/README.md).

Archive gốc Issue #12 được giữ nguyên dù đuôi `.zip` thực chất là RAR. Source,
dictionary và PNG cũ đã được giải nén để đọc trực tiếp trên repo. PNG cũ chỉ giữ
làm tham chiếu; trình bày bằng SVG hiện tại hoặc export lại nguồn draw.io đã sửa.

ERD hai bảng không đại diện cho toàn hệ thống. Auth thêm `roles`/`users` ở
migration `AddAuthUsers`; nếu slide bao gồm schema đăng nhập, cần bổ sung hai
bảng đó. Không tự vẽ thêm quan hệ `User → QuestionBank` khi schema chưa có FK này.

## Đối chiếu Class Diagram

- `QuestionBank` có quan hệ logic 1–0..* với `Question` qua
  `Question.QuestionBankId`; domain model không có navigation collection.
- `QuestionBankService` và `QuestionService` implement hai interface Application
  tương ứng, cùng gọi `IQuestionBankRepository`.
- `PostgresQuestionBankRepository` implement repository interface và dùng
  `QuestionBankDbContext`; EF Core ánh xạ hai entity này tới PostgreSQL.
- `QuestionBankEndpoints` gọi hai service interface; quyền `ADMIN`/`LECTURER`
  được thực thi bằng authorization policy, không phải liên kết owner của bank.
- DbContext cũng có `Roles` và `Users` do tính năng đăng nhập, nhưng sơ đồ tập
  trung vào các lớp trực tiếp tham gia Question Bank CRUD. Sơ đồ không biểu diễn
  các lớp Exam/AI chưa tham gia lát cắt demo này.

## Kiểm tra trước demo / mở PR

Kết quả kiểm tra local ngày 2026-10-05 trên nhánh
`feature/issue-8-crud-integration` (chưa merge):

- `npm run check`: lint, format, build và 57 test đạt. Test live được skip trong
  lượt kiểm tra thông thường, rồi chạy riêng bằng smoke harness bên dưới.
- Backend build: 0 warning, 0 error; 34/34 test đạt; kiểm tra format đạt.
- Smoke trên PostgreSQL tạm: auth/CSRF/quyền, CRUD, validation, lỗi 400/404/409,
  dữ liệu giữ nguyên sau restart API, seed chạy hai lần không tạo trùng đều đạt.
  Swagger của cả bốn request tạo/sửa bank/question có trường bắt buộc đúng và
  cho phép nhập dữ liệu (không bị đánh dấu `readOnly`).
- Test React/jsdom → HTTP API → PostgreSQL: 1/1 đạt; form tạo/sửa/xóa bank và
  question, tải lại ứng dụng và lỗi xóa bank còn câu hỏi được kiểm tra tự động.
- Chưa kiểm tra trực quan hoặc chụp ảnh trên trình duyệt vì không có browser
  kết nối. Các mục kiểm tra thủ công dưới đây vẫn cần nghiệm thu.

Từ repository root:

```powershell
cd frontend
npm run check
```

Từ `backend/`, dừng API local trước khi build để tránh khóa DLL:

```powershell
dotnet build AiVivaExamSystem.sln
dotnet test AiVivaExamSystem.sln --no-build
dotnet format AiVivaExamSystem.sln --verify-no-changes --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-QuestionBankSmoke.ps1
```

Smoke script cần Docker; tạo database/API kiểm thử riêng, chạy migration,
kiểm tra CRUD/validation/quyền và persistence khi restart, sau đó dọn tài nguyên
kiểm thử. Script **không seed hoặc kiểm tra database demo đang mở trên FE**.
Script cũng chạy `QuestionBankLive.test.jsx`: render React trong **jsdom** nhưng
gửi HTTP thật tới API/PostgreSQL kiểm thử. Đây là test opt-in (bình thường bị
skip trong `npm run check`); harness tự cấp URL, tài khoản tạm và bật test,
không cần tự đặt mật khẩu test vào lệnh. Cần chạy `npm ci` ở frontend trước.
Test này không khởi chạy trình duyệt thật, không kiểm tra layout/screenshot hay
chính sách cookie của browser; vẫn phải thao tác kiểm tra giao diện theo checklist.

Checklist nghiệm thu (chỉ đánh dấu sau khi thực hiện trên revision định merge):

- [ ] FE lint/format/test/build và BE build/test/format đạt.
- [ ] Smoke API + PostgreSQL đạt; lưu kết quả trong PR, không lưu secret.
- [ ] Thử CRUD cả bank và question bằng giao diện `/question-banks`.
- [ ] Thử tải lại/restart, `409`, lỗi kết nối/retry và quyền Student trên UI.
- [ ] Chụp UI thật: danh sách, form bank, form question và lỗi xóa bank còn câu hỏi.
- [ ] Physical ERD/source/dictionary đã review đúng phạm vi trình bày.
- [x] Có Class Diagram source + ảnh SVG, đối chiếu với code hiện tại.
- [x] PR #20 đã merge; PR tích hợp vẫn cần review/merge vào `develop`.

Chỉ đóng Issue #8 sau khi phần tích hợp, Class Diagram và các mục demo thủ công
được nghiệm thu; không đóng chỉ vì một nhóm test đã chạy xanh.
