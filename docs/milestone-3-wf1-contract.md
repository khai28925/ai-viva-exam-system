# Milestone 3 — hợp đồng WF1: chuẩn bị và mở phiên thi

**Trạng thái:** contract đích đã chốt cho Milestone 3 (v1.0), **chưa phải toàn bộ code đã triển khai**. **Nhánh nền:** `develop` sau PR #25.
**Nguồn nghiệp vụ:** `docs/diagram/Swimlane wf1.png`. Tài liệu này là nguồn thống nhất cho BE, FE, sequence diagram, state diagram và document review. Nếu cần đổi URL, JSON, quyền hoặc quy tắc trạng thái, thảo luận trong Issue/PR rồi cập nhật tài liệu trước khi sửa từng nhánh.

## 1. Phạm vi và điểm kết thúc

Giảng viên đăng nhập, tạo ngân hàng câu hỏi và câu hỏi (API đã có), thêm **ý chính kỳ vọng** cho câu hỏi, tạo **rubric/tiêu chí đánh giá** cho ngân hàng, chọn câu hỏi dùng trong một phiên thi, rồi mở phiên. Sinh viên thấy phiên `Open`, bấm **Vào thi** và đọc đúng các câu hỏi đã chọn. Lượt tham gia được lưu trong `exam_attempts` như bản hiện tại.

Không làm trong WF1/Milestone 3: lịch thi (`exam_schedules`), phân công sinh viên, trả lời/nộp bài, ghi âm, STT/TTS/LLM, chấm điểm, báo cáo, AI follow-up. Các bảng dành cho những luồng đó đã có trong schema nhưng **không đồng nghĩa đã có tính năng**.

Các đường dẫn hiện có cho Question Bank/Question và `GET/POST/PUT/DELETE /api/v1/exam-sessions`, `POST /api/v1/exam-sessions/{id}/join` được giữ. Phần dưới chỉ bổ sung đường dẫn và làm rõ hành vi phiên thi; FE phải cập nhật luồng tạo phiên theo quy tắc mới.

## 2. Quy ước chung

- Base path `/api/v1`; JSON `camelCase`; ID là UUID, thời gian UTC ISO 8601. ID, `createdAt`, `displayOrder` (key point) do server tạo. Không phân trang trong phạm vi demo.
- Mọi endpoint mới của key point/rubric: `LECTURER` và `ADMIN` được đọc/ghi; `STUDENT` nhận `403`. Chưa đăng nhập nhận `401`. Question Bank hiện chưa thực thi sở hữu theo giảng viên (`created_by` còn nullable); **không** tự thêm luật “chỉ chủ bank được sửa” trong Milestone 3.
- Session: `ADMIN` đọc/quản lý tất cả; `LECTURER` chỉ đọc/quản lý phiên mình tạo (phiên không thuộc mình trả `404` như code hiện tại); `STUDENT` chỉ thấy phiên `Open`, chỉ Student được gọi `join`.
- Mọi `POST/PUT/DELETE` dùng cookie đăng nhập và `X-CSRF-TOKEN` lấy từ `GET /api/v1/auth/csrf`, giống API hiện có.
- Validation request trả `400 ValidationProblem` với `errors` theo tên field. Resource/parent không tồn tại hoặc không khớp quan hệ trả `404 ProblemDetails`. Xung đột nghiệp vụ trả `409 ProblemDetails` có extension `code` ổn định (field `code` ở JSON response), mô tả ở từng mục. Không trả stack trace hoặc lỗi khóa ngoại PostgreSQL thô cho FE.
- Các request ghi được trim các chuỗi trước khi validate. `description` rỗng sau trim được lưu `null`. Server phải xử lý unique/FK race thành `409` hoặc `404`, không thành `500`.

## 3. Expected Key Point — BE 1

Đường dẫn gốc: `/api/v1/question-banks/{bankId}/questions/{questionId}/expected-key-points`. `questionId` phải thuộc `bankId` và là câu hỏi loại `Bank`; dữ liệu nằm trong bảng `expected_key_points` đã có.

| Method | Path tiếp theo | Thành công | Ghi chú |
| --- | --- | --- | --- |
| `GET` | `/` | `200` + mảng | `[]` nếu câu hỏi tồn tại nhưng chưa có key point; sắp theo `displayOrder`, rồi `id`. |
| `GET` | `/{keyPointId}` | `200` + DTO | `404` nếu không thuộc câu hỏi trong URL. |
| `POST` | `/` | `201` + DTO, `Location` | Body `{ "content": "..." }`. |
| `PUT` | `/{keyPointId}` | `200` + DTO | Body `{ "content": "..." }`; không đổi thứ tự. |
| `DELETE` | `/{keyPointId}` | `204` | `404` nếu không thuộc câu hỏi trong URL. |

DTO: `{ id, questionId, content, displayOrder, createdAt }`. `content`: trim, bắt buộc, 1–2000 ký tự. Khi thêm, server gán `displayOrder = max(existing)+1` (bắt đầu 0); xóa không đánh số lại. Không có thao tác kéo-thả/sắp lại thứ tự trong Milestone 3. Việc thêm cần chịu được hai request đồng thời và ràng buộc unique `(question_id, display_order)`; có thể dùng khóa/transaction hoặc retry có giới hạn.

Khi xóa Question còn key point hoặc đã được chọn trong phiên thi, API Question hiện có phải trả `409` thay vì lỗi DB thô. Phần điều chỉnh endpoint/repository chung này do **lead** tích hợp để BE 1 không phải sửa file CRUD của nhóm khác.

Sau khi một Question đã được chọn trong bất kỳ phiên `Open` hoặc `Closed`, thêm/sửa/xóa key point của Question đó trả `409`, code `question_preparation_locked`. Đây là giới hạn đơn giản của Milestone 3 để nội dung chuẩn bị cho phiên đã công bố không bị thay đổi; việc version hóa key point thuộc milestone sau.

## 4. Rubric và Criterion — BE 2

Mỗi Question Bank có **tối đa một** rubric theo unique index `rubrics.question_bank_id`. Đường dẫn gốc: `/api/v1/question-banks/{bankId}/rubric`.

| Method | Path tiếp theo | Thành công | Ghi chú |
| --- | --- | --- | --- |
| `GET` | `/` | `200` + Rubric DTO | `404` nếu chưa tạo rubric. |
| `POST` | `/` | `201` + Rubric DTO | Body `{ "name": "...", "description": null }`; đã có rubric trả `409`, code `rubric_already_exists`. |
| `PUT` | `/` | `200` + Rubric DTO | Thay toàn bộ `name`, `description`. |
| `DELETE` | `/` | `204` | Chỉ khi không còn criterion; nếu còn trả `409`, code `rubric_has_criteria`. |
| `GET` | `/criteria` | `200` + mảng Criterion DTO | `[]` nếu rubric tồn tại nhưng rỗng; `404` nếu thiếu rubric. |
| `GET` | `/criteria/{criterionId}` | `200` + Criterion DTO | `404` nếu không thuộc rubric trong URL. |
| `POST` | `/criteria` | `201` + Criterion DTO, `Location` | Body `{ "name": "...", "description": null, "maxScore": 10.00, "weight": 0.5000 }`. |
| `PUT` | `/criteria/{criterionId}` | `200` + Criterion DTO | Thay toàn bộ field được nhập khi tạo. |
| `DELETE` | `/criteria/{criterionId}` | `204` | `404` nếu criterion không thuộc rubric của bank trong URL. |

Rubric DTO: `{ id, questionBankId, name, description, createdAt }`. Criterion DTO: `{ id, rubricId, name, description, maxScore, weight, createdAt }`. `name`: trim, bắt buộc, 1–120 ký tự; `description`: tối đa 500 ký tự. `maxScore`: decimal, `> 0`, tối đa 2 chữ số thập phân. `weight`: decimal, `> 0` và `<= 1`, tối đa 4 chữ số thập phân. Trùng tên criterion sau trim **đúng cùng ký tự** trong một rubric trả `409`, code `criterion_name_exists` (schema hiện phân biệt hoa/thường). Tổng weight **chưa bắt buộc bằng 1** khi đang soạn rubric; chỉ kiểm tra lúc mở phiên.

Xóa Question Bank còn rubric hoặc phiên thi phải trả `409` có thông báo đúng nguyên nhân. Phần sửa API xóa bank hiện có do **lead** tích hợp. Không tạo migration mới nếu mapping/bảng hiện hữu đáp ứng contract.

Sau khi bank được dùng trong bất kỳ phiên `Open` hoặc `Closed`, tạo/sửa/xóa rubric hoặc thêm/sửa/xóa criterion trả `409`, code `rubric_preparation_locked`. Muốn dùng bộ tiêu chí khác ở bản demo, tạo bank mới; version hóa rubric thuộc milestone sau. Các phiên `Draft` không khóa rubric.

## 5. Chọn câu hỏi và vòng đời phiên thi — lead

### 5.1. API chọn câu hỏi

Bổ sung dưới `/api/v1/exam-sessions/{id}`:

| Method | Path tiếp theo | Thành công | Quyền |
| --- | --- | --- | --- |
| `GET` | `/questions` | `200` + mảng câu hỏi đã chọn | ADMIN hoặc giảng viên tạo phiên; Student chỉ thấy qua `join`. |
| `PUT` | `/questions` | `200` + mảng mới | ADMIN hoặc giảng viên tạo phiên; **chỉ khi Draft**. |

`PUT` body: `{ "questionIds": ["uuid-1", "uuid-2"] }`. Mảng có 0–50 UUID khác nhau; `[]` cho phép xóa lựa chọn khi còn `Draft`. Thứ tự mảng là thứ tự đề thi (0-based). Mỗi ID phải là Question loại `Bank` thuộc `questionBankId` của phiên; ID trùng hoặc sai bank trả `400 ValidationProblem` ở `questionIds`. Phiên không tồn tại/không thuộc giảng viên trả `404`. Phiên không còn `Draft` trả `409`, code `selection_locked`.

Lựa chọn “dùng cả ngân hàng” trong swimlane WF1 được FE chuyển thành **danh sách ID của toàn bộ Question loại `Bank` hiện có**, gửi qua cùng endpoint này. Không có chế độ tự động thêm những Question được tạo sau đó; selection là một danh sách cố định và tối đa 50 câu trong Milestone 3.

Response item: `{ questionId, contentSnapshot, displayOrder, isRequired }`, với `isRequired = true` trong Milestone 3. `PUT` **thay toàn bộ danh sách trong một transaction** vào `exam_session_questions`; `contentSnapshot` lấy từ Question.Content khi chọn. Lặp lại `PUT` cùng mảng không tạo bản ghi trùng. Nếu sửa nội dung câu hỏi nguồn khi phiên còn `Draft`, snapshot chỉ cập nhật khi chọn lại; UI hiển thị snapshot để giảng viên kiểm tra trước lúc mở. Khi phiên đã `Open`, danh sách và snapshot không thay đổi; Student `join` chỉ nhận các câu hỏi đã chọn theo `displayOrder`, **không** đọc toàn bộ câu hỏi hiện thời của bank. Trong `ExamAttemptDto.questions`, `id` vẫn là Question ID như contract hiện tại; `content` lấy từ snapshot. Chưa tạo `attempt_questions` hoặc answer ở WF1.

### 5.2. State contract

`POST /api/v1/exam-sessions` giữ body `{ title, questionBankId, status }` nhưng **chỉ chấp nhận** `status: "Draft"`; tạo trực tiếp `Open`/`Closed` trả `400`. `PUT /api/v1/exam-sessions/{id}` giữ cùng body và tuân theo:

| Trạng thái hiện tại | Yêu cầu hợp lệ | Kết quả |
| --- | --- | --- |
| Chưa tồn tại | Tạo `Draft` | `Draft`. |
| `Draft` | Sửa title; đổi bank chỉ khi selection rỗng; chọn lại câu hỏi | Vẫn `Draft`. |
| `Draft` | Đổi thành `Open` khi đủ điều kiện bên dưới | `Open`; selection đóng băng. |
| `Open` | Đổi thành `Closed`, giữ nguyên title và bank | `Closed`; Student không join mới. |
| `Closed` | Không có chuyển trạng thái khác | Trạng thái kết thúc. |

`PUT` giữ nguyên mọi field/status là no-op `200`. Mọi chuyển đổi khác (`Draft → Closed`, `Open → Draft`, `Closed → Open`) hoặc sửa title/bank sau khi mở trả `409`, code `invalid_status_transition`. Đổi bank khi Draft còn selection trả `409`, code `selection_not_empty` (FE phải xóa selection trước). `DELETE` chỉ cho phiên `Draft` chưa có attempt; lead xóa selection con rồi xóa phiên trong một transaction. Các trường hợp khác trả `409`, code `session_not_deletable`.

Điều kiện `Draft → Open` phải được kiểm tra cùng transaction và trả `409`, code `session_not_ready` nếu thiếu:

1. Ít nhất **một** câu hỏi đã chọn trong `exam_session_questions`; tất cả vẫn thuộc bank và loại `Bank`.
2. **Mỗi** câu hỏi đã chọn có ít nhất một expected key point.
3. Bank có rubric với ít nhất một criterion, và tổng `weight` của criteria đúng `1.0000` bằng decimal (không cộng bằng floating point).

Extension `missing` của `ProblemDetails` (field `missing` ở JSON response) là mảng các mã trong `selection`, `keyPoints`, `rubric`, `rubricCriteria`, `rubricWeight`; khi thiếu key point, thêm `questionIdsWithoutKeyPoints` là mảng UUID. FE dùng thông tin này để chỉ đúng phần cần bổ sung. `title` trim 1–120 ký tự, `questionBankId` UUID khác rỗng, bank không tồn tại trả `404` như API hiện tại.

`STUDENT` thấy danh sách `Open`; `POST /join` chỉ khi `Open`, lặp lại với cùng sinh viên trả về attempt cũ (không tạo trùng), lưu `exam_attempts`. Việc chuyển `Open → Closed` không xóa attempt đã có. Không có nút trả lời/nộp bài trong Milestone 3.

Các phiên `Open`/`Closed` tạo bởi bản Milestone 2 có thể chưa có dòng `exam_session_questions`. Không xóa hoặc sửa âm thầm dữ liệu cũ. Manager vẫn xem được phiên; `join` trên phiên cũ `Open` thiếu selection trả `409`, code `legacy_session_unprepared`, thay vì tự lấy toàn bộ câu hỏi trong bank. Demo WF1 tạo phiên mới theo contract này. Nếu muốn dùng dữ liệu cũ cho demo, lead phải làm backfill có kiểm tra và backup riêng.

## 6. Hợp đồng FE và diagram

- FE thiết kế các khối độc lập nhận data/callback qua props: key point trong Question detail, rubric/criteria trong Bank detail, chọn câu hỏi trong form phiên thi. Mock data dùng đúng DTO ở trên; không tự đặt URL/field khác. Lead gắn API client, router và trạng thái loading/error/403/409 vào trang thật.
- UI/UX cần ảnh hoặc file thiết kế mới của luồng giảng viên (bank → question → key point/rubric → selection → Open) và Student (thấy Open → Vào thi). Không gắn nhãn “thi hoàn tất” khi mới chỉ vào/xem câu hỏi.
- Sequence diagram thể hiện Lecturer/React → API + auth/CSRF → application/service → EF Core/PostgreSQL, bao gồm validate khi mở phiên và Student join. Không vẽ AI/STT/TTS. State diagram dùng đúng bảng chuyển trạng thái mục 5.2, tách `ExamSession` với `ExamAttempt`.
- Document review đối chiếu swimlane WF1, UI/UX, sequence/state, contract này, schema/migration và code chạy. Mọi sai khác phải được sửa hoặc ghi rõ là “chưa triển khai”, không âm thầm coi bảng DB là tính năng hoàn thành.

## 7. Chia ranh giới file và nghiệm thu

BE 1 sở hữu endpoint/service/test **ExpectedKeyPoint**; BE 2 sở hữu endpoint/service/test **Rubric/Criterion**. Hai bạn dùng DbContext và mapping hiện có, không cùng sửa `Program.cs`, `DependencyInjection.cs`, `QuestionBankEndpoints.cs` hay `PostgresQuestionBankRepository.cs`; lead nối đăng ký DI/route và sửa xử lý xóa parent. FE tạo component/thiết kế mới, không sửa API client/route mà lead đang nối. Thành viên SAP sở hữu state diagram và checklist document review; lead sở hữu sequence diagram, selection/status và demo tích hợp. Mỗi PR tách nhánh từ `develop`, build/test độc lập khi có thể, nêu API mẫu và cách kiểm tra.

Nghiệm thu chung: đăng nhập Lecturer → tạo bank/question → thêm key point → tạo rubric/criteria có tổng weight 1 → tạo phiên Draft → chọn câu hỏi → mở Open → đăng nhập Student → thấy phiên, vào thi và thấy **chỉ** câu hỏi đã chọn. Sau restart API, dữ liệu vẫn còn. Kiểm tra thêm 401/403, mở khi thiếu dữ liệu 409, chuyển trạng thái sai 409, Student join Closed 409, không tạo trùng attempt, `dotnet test`, `npm run check` và ảnh UI mới.
