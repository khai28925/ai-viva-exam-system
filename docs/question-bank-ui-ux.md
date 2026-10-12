# Question Bank UI/UX — Issue #11

## Mục đích và ranh giới

Bản thiết kế dành cho giảng viên quản lý câu hỏi của một ngân hàng. Dữ liệu minh
họa chỉ có các trường từ [MVP contract](question-bank-mvp-contract.md):
`QuestionBank { id, name, description, createdAt }` và
`Question { id, questionBankId, content, createdAt }`. Không có `difficulty`,
`type`, `rubric` hoặc đăng nhập trong lát cắt này.

UI ở `frontend/src/features/question-bank/QuestionBankWorkspace.jsx` nhận `banks`,
`questions`, `isLoading`, `error` và callback qua props. Không import API client;
không tự điều khiển router. Trang `/question-bank-preview.html` là host mock để có thể
trình bày độc lập khi BE chưa chạy.

Issue #8 bổ sung container `QuestionBankPage` tại route `/question-banks` để
gọi API thật, cùng các callback tạo/sửa/xóa ngân hàng và trạng thái phiên đăng
nhập. Workspace vẫn nhận dữ liệu/callback qua props, không tự import API client;
mock preview Issue #11 được giữ độc lập. Xem [demo tích hợp](issue-8-demo.md).

## Luồng giảng viên

1. Xem danh sách ngân hàng bên trái; chọn một ngân hàng để xem câu hỏi bên phải.
2. Tìm ngân hàng/câu hỏi bằng từ khóa; lọc tại giao diện, không làm thay đổi dữ liệu.
3. Chọn **Thêm câu hỏi**; nhập `content` (trim, 1–2000 ký tự) rồi lưu.
4. Chọn nút sửa ở một câu hỏi; chỉnh `content` rồi lưu.
5. Chọn nút xóa; xem lại nội dung trong hộp thoại xác nhận rồi xóa.

Trạng thái loading, lỗi, ngân hàng rỗng và câu hỏi rỗng có nội dung riêng. Lỗi
khi callback lưu/xóa thất bại giữ hộp thoại mở để giảng viên thử lại.

## Bố cục và visual language

- Desktop: sidebar điều hướng, breadcrumb, danh sách ngân hàng và chi tiết câu hỏi
  cạnh nhau (master–detail). Mobile: hai khu vực xếp dọc, nút thao tác vẫn nhìn rõ.
- Màu nền trắng/ngà, xanh đậm `#173a35` cho điều hướng, cam `#c96f48` cho hành
  động chính, đỏ cho xóa. Font dùng Manrope + DM Sans đang có trong frontend.
- Dùng nhãn chữ và trạng thái focus bên cạnh màu sắc để thao tác bàn phím được.
  Form gắn label, thông báo validation và bộ đếm ký tự; hộp thoại đóng bằng Escape.
- Sidebar chỉ là hình minh họa, không phải route điều hướng hoàn chỉnh.

## Ảnh và demo

Chạy `npm ci` (nếu chưa cài) và `npm run dev` trong `frontend/`, sau đó mở:

- `/question-bank-preview.html`: danh sách và chi tiết có dữ liệu.
- `/question-bank-preview.html?state=empty`: ngân hàng có dữ liệu nhưng chưa có câu hỏi.
- `/question-bank-preview.html?state=empty-banks`: chưa có ngân hàng.
- `/question-bank-preview.html?state=loading`: trạng thái đang tải.
- `/question-bank-preview.html?state=error`: lỗi với nút **Thử lại**.

Ảnh thiết kế minh họa:

- [Tổng quan desktop](diagram/question-bank-ui/desktop-overview.svg)
- [Hộp thoại thêm câu hỏi](diagram/question-bank-ui/question-form.svg)

Đây là mockup SVG của thiết kế, không phải screenshot từ trình duyệt. Khi mở PR,
chụp thêm ảnh giao diện thực tế từ trang preview để đối chiếu triển khai.

## Contract callback để lead nối API

| Prop | Ý nghĩa |
| --- | --- |
| `onCreateBank({ name, description })` | `POST /api/v1/question-banks` (Issue #8) |
| `onUpdateBank(bankId, { name, description })` | `PUT /api/v1/question-banks/{bankId}` (Issue #8) |
| `onDeleteBank(bankId)` | `DELETE /api/v1/question-banks/{bankId}`; hiển thị lỗi 409 nếu bank còn question (Issue #8) |
| `onCreateQuestion(bankId, { content })` | `POST /api/v1/question-banks/{bankId}/questions` |
| `onUpdateQuestion(bankId, questionId, { content })` | `PUT /api/v1/question-banks/{bankId}/questions/{questionId}` |
| `onDeleteQuestion(bankId, questionId)` | `DELETE /api/v1/question-banks/{bankId}/questions/{questionId}` |
| `onRetry()` | Tải lại dữ liệu khi có lỗi |

Callback có thể trả Promise; component chờ Promise hoàn thành. Container tích hợp
API có trách nhiệm cập nhật `banks`/`questions` sau request thành công. Không truyền
`questionBankId` trong body của request câu hỏi: parent id nằm trên URL theo contract.
