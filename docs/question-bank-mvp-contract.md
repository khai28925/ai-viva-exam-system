# MVP contract: Question Bank CRUD

Trạng thái: baseline cho milestone CRUD. Tài liệu này là đầu vào chung cho API,
PostgreSQL, frontend, Class Diagram và Physical ERD. Mọi thay đổi về trường dữ
liệu, đường dẫn API hoặc quan hệ cần được thống nhất trong Issue #7 trước khi
cập nhật các nhánh triển khai.

## Phạm vi

- Giảng viên quản lý ngân hàng câu hỏi (`QuestionBank`) và câu hỏi (`Question`).
- Một ngân hàng có nhiều câu hỏi; mỗi câu hỏi thuộc đúng một ngân hàng.
- Đăng nhập/phân quyền được bổ sung theo [auth MVP contract](auth-mvp-contract.md).
- Chưa làm phiên thi vấn đáp, expected key points, rubric,
  AI, Speech-to-Text hoặc Text-to-Speech trong lát cắt CRUD này.
- Endpoint `GET /api/v1/exams` hiện có là ví dụ của skeleton, không phải dữ liệu
  hay API của Question Bank.

## Quyền truy cập

Các endpoint Question Bank/Question yêu cầu đăng nhập với vai trò `LECTURER`
hoặc `ADMIN`. Chưa đăng nhập trả `401`; tài khoản `STUDENT` trả `403`. Request
`POST`, `PUT`, `DELETE` cần cookie đăng nhập và header `X-CSRF-TOKEN` lấy từ
`GET /api/v1/auth/csrf`. Hiện phân quyền theo vai trò; schema chưa có người sở
hữu ngân hàng câu hỏi để giới hạn theo từng giảng viên.

## Domain và dữ liệu

| Entity | Trường | Quy tắc |
| --- | --- | --- |
| `QuestionBank` | `id: Guid` | UUID do server sinh, không cho client sửa. |
| | `name: string` | Bắt buộc; trim; 1–120 ký tự. |
| | `description: string?` | Tùy chọn; trim; tối đa 500 ký tự; chuỗi rỗng thành `null`. |
| | `createdAt: DateTimeOffset` | UTC do server sinh, không cho client sửa. |
| `Question` | `id: Guid` | UUID do server sinh, không cho client sửa. |
| | `questionBankId: Guid` | FK bắt buộc tới `QuestionBank`; không đổi sau khi tạo. |
| | `content: string` | Bắt buộc; trim; 1–2000 ký tự. |
| | `createdAt: DateTimeOffset` | UTC do server sinh, không cho client sửa. |

Entity và repository interface được định nghĩa ở Domain/Application. Tầng
Infrastructure giữ adapter in-memory ban đầu để tham chiếu/kiểm thử, nhưng
runtime hiện đăng ký `PostgresQuestionBankRepository` và yêu cầu chuỗi kết nối
PostgreSQL. Không có fallback in-memory khi database thiếu hoặc lỗi.

## Physical schema PostgreSQL

| Bảng | Cột | Kiểu / ràng buộc |
| --- | --- | --- |
| `question_banks` | `id` | `uuid primary key` |
| | `name` | `varchar(120) not null` |
| | `description` | `varchar(500) null` |
| | `created_at` | `timestamptz not null` |
| `questions` | `id` | `uuid primary key` |
| | `question_bank_id` | `uuid not null`, FK tới `question_banks.id` |
| | `content` | `varchar(2000) not null` |
| | `created_at` | `timestamptz not null` |

Tạo index trên `questions.question_bank_id`. FK dùng `ON DELETE RESTRICT`:
không xóa ngân hàng còn câu hỏi; API trả `409 Conflict`. Không yêu cầu `name`
duy nhất trong MVP. Migration phải phản ánh đúng schema này.

## HTTP API contract

Base path: `/api/v1/question-banks`. JSON dùng `camelCase`. ID và thời gian do
server sinh; request không nhận các trường này. Không phân trang trong MVP.
Danh sách sắp theo `createdAt` tăng dần, cùng thời điểm thì theo `id`.

| Method | Path | Thành công | Lỗi nghiệp vụ |
| --- | --- | --- | --- |
| `GET` | `/api/v1/question-banks` | `200` + mảng bank | — |
| `GET` | `/api/v1/question-banks/{bankId}` | `200` + bank | `404` |
| `POST` | `/api/v1/question-banks` | `201` + bank, `Location` | `400` |
| `PUT` | `/api/v1/question-banks/{bankId}` | `200` + bank | `400`, `404` |
| `DELETE` | `/api/v1/question-banks/{bankId}` | `204` | `404`, `409` nếu còn question |
| `GET` | `/api/v1/question-banks/{bankId}/questions` | `200` + mảng question | `404` nếu bank không tồn tại |
| `GET` | `/api/v1/question-banks/{bankId}/questions/{questionId}` | `200` + question | `404` |
| `POST` | `/api/v1/question-banks/{bankId}/questions` | `201` + question, `Location` | `400`, `404` |
| `PUT` | `/api/v1/question-banks/{bankId}/questions/{questionId}` | `200` + question | `400`, `404` |
| `DELETE` | `/api/v1/question-banks/{bankId}/questions/{questionId}` | `204` | `404` |

`PUT` thay toàn bộ các trường có thể sửa của resource. `bankId` trong URL quyết
định cha của question; `questionBankId` không xuất hiện trong request body.

### Request và response

- Tạo/sửa bank: `{ "name": "Entrepreneurship", "description": "Câu hỏi nền tảng" }`.
- Tạo/sửa question: `{ "content": "What is a value proposition?" }`.
- Bank response: `{ "id": "<uuid>", "name": "Entrepreneurship", "description": "Câu hỏi nền tảng", "createdAt": "<UTC ISO-8601>" }`.
- Question response: `{ "id": "<uuid>", "questionBankId": "<uuid>", "content": "What is a value proposition?", "createdAt": "<UTC ISO-8601>" }`.
- `GET` danh sách trả JSON array (mảng rỗng nếu không có dữ liệu).

### Validation và lỗi

- Áp dụng các giới hạn ở bảng Domain sau khi trim; thiếu trường bắt buộc hoặc
  vượt giới hạn trả `400 ValidationProblemDetails` với `errors` theo tên field.
- UUID trên URL không hợp lệ trả `400`; resource hoặc bank cha không tồn tại trả
  `404`; xóa bank còn question trả `409`.
- Lỗi dùng `application/problem+json` theo cấu hình API hiện có, gồm `status`,
  `title`, `instance`, `traceId`; có `errors` cho lỗi validation. Không gửi stack
  trace hoặc chi tiết nội bộ cho client.

## Chia ranh giới triển khai

- Lead: giữ contract này và model/interface baseline.
- BE database: EF Core, DbContext, migration, adapter PostgreSQL theo schema.
- BE API: endpoint, application service, validation, backend test theo HTTP API.
- FE: UI Issue #11 giữ preview mock response cùng shape; lead nối API thật ở
  `/question-banks` trong Issue #8, không thay preview thành nguồn dữ liệu thật.
- Tài liệu: Physical ERD theo đúng hai bảng của MVP.

PostgreSQL adapter đã được đăng ký tại `AddInfrastructure()` qua Issue #9 mà
không đổi HTTP contract. Phân quyền và tích hợp FE trên nhánh Issue #8 kế thừa
PR #20; cần merge dependency đó trước khi đưa phần tích hợp vào `develop`.
