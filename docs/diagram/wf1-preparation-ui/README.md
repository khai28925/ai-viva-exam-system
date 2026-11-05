# WF1 — thiết kế UI/UX chuẩn bị phiên thi (Milestone 3)

Đây là bản thiết kế tương tác dùng mock data cho Issue #30. Mã nguồn thiết kế có thể chỉnh sửa tại `frontend/src/wf1-preparation-preview.jsx` và `frontend/src/features/wf1-preparation/`; bản này **không gọi API, không sửa router hoặc API client của ứng dụng chính**. Contract nguồn: [`docs/milestone-3-wf1-contract.md`](../../milestone-3-wf1-contract.md).

## Xem thiết kế

Từ thư mục `frontend` chạy `npm install` (nếu chưa cài) rồi `npm run dev`. Mở `http://localhost:5173/wf1-preparation-preview.html`. Chọn vai trò và trạng thái trong thanh trên để xem/click thử:

- Giảng viên: ngân hàng/câu hỏi → ý chính kỳ vọng → rubric/criteria → chọn câu hỏi và xem snapshot → kiểm tra điều kiện → mở `Open`.
- Sinh viên: chỉ thấy phiên `Open` → **Vào thi** → đọc đúng các câu hỏi đã chọn. WF1 chưa có trả lời, nộp bài, STT hoặc chấm điểm.
- Trạng thái phụ: loading, dữ liệu rỗng, lỗi tải, `403`, `409 session_not_ready` và xác nhận xóa trong các component.

Preview chạy hoàn toàn trên trình duyệt, nên sửa dữ liệu ở đây không lưu PostgreSQL. Tải lại trang sẽ trở về mock ban đầu. Trang thật và tích hợp API do lead xử lý riêng.

## Bố cục và quy tắc tương tác

Màn hình giảng viên chia thành hai cột trên desktop: nội dung nguồn/chuẩn bị bên trái, rubric/cấu hình phiên bên phải. Các thẻ độc lập nhận DTO và callback qua props. `ExpectedKeyPointsCard` chỉ nhận Question loại `Bank`; `RubricCard` gắn một rubric với một bank; `QuestionSelectionCard` nhận mảng DTO snapshot đã chọn; `ReadinessCard` hiển thị năm mã `missing` của contract. `StudentSessionsCard` chỉ lọc phiên `Open` và hiển thị `ExamAttemptDto.questions` sau khi vào thi. Ở màn hình hẹp, các thẻ xếp thành một cột.

Quy tắc dữ liệu: ID và `displayOrder` do server gán; key point 1–2000 ký tự; tên rubric/criterion 1–120 ký tự, mô tả tối đa 500; `maxScore > 0`, `0 < weight <= 1`; tối đa 50 câu hỏi, theo đúng thứ tự chọn. Có xác nhận trước khi xóa. Nút mở phiên bị vô hiệu khi preview biết còn điều kiện thiếu, nhưng server vẫn là nguồn quyết định cuối cùng và UI hiển thị `409` nếu server từ chối.

## Ảnh trình bày

- [`lecturer.png`](lecturer.png): giảng viên chuẩn bị câu hỏi/rubric/phiên.
- [`student.png`](student.png): sinh viên xem phiên đã mở.
- [`student-attempt.png`](student-attempt.png): sinh viên sau khi vào thi, chỉ thấy câu hỏi được chọn.
- [`not-ready.png`](not-ready.png): trạng thái `409` thiếu điều kiện.

Ảnh được xuất từ preview ở kích thước desktop 1440 px rộng. Có thể chụp lại sau khi thay đổi JSX/CSS; chính các file mã nguồn ở trên là bản thiết kế chỉnh sửa được.
