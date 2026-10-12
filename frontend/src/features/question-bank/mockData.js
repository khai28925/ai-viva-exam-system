// Matches docs/question-bank-mvp-contract.md: no UI-only fields in API objects.
export const mockQuestionBanks = [
  {
    id: '2a06d1ba-263b-45c0-bb41-7072b8760701',
    name: 'Kiến trúc phần mềm',
    description: 'Nguyên lý thiết kế, kiến trúc và các quyết định kỹ thuật.',
    createdAt: '2026-09-18T08:30:00Z',
  },
  {
    id: 'c7b6c22e-34ea-45e8-a811-d1fc8cf94a02',
    name: 'Lập trình hướng đối tượng',
    description: 'Các khái niệm OOP và cách áp dụng trong thực tế.',
    createdAt: '2026-09-20T03:00:00Z',
  },
  {
    id: '642c9dde-181a-4c92-9d06-6b7c876d4a03',
    name: 'Cơ sở dữ liệu',
    description: 'Mô hình quan hệ, chuẩn hóa và truy vấn dữ liệu.',
    createdAt: '2026-09-23T09:15:00Z',
  },
  {
    id: '8c260638-9148-4fee-bb69-7ce855168004',
    name: 'Kiểm thử phần mềm',
    description: null,
    createdAt: '2026-09-26T02:45:00Z',
  },
]

export const mockQuestions = [
  {
    id: '35472ed6-b2f1-4c93-a90a-af06140f1001',
    questionBankId: mockQuestionBanks[0].id,
    content:
      'Kiến trúc monolith và microservices khác nhau như thế nào? Khi nào nên chọn mỗi mô hình?',
    createdAt: '2026-09-18T09:00:00Z',
  },
  {
    id: '50ec6ebd-ef83-49e1-a3e2-dfb849f61002',
    questionBankId: mockQuestionBanks[0].id,
    content:
      'Giải thích nguyên tắc Single Responsibility và đưa ra một ví dụ áp dụng trong dự án.',
    createdAt: '2026-09-18T09:15:00Z',
  },
  {
    id: 'f23099ae-e3cc-456e-96d9-bf39cba51003',
    questionBankId: mockQuestionBanks[0].id,
    content:
      'Dependency Injection giúp giảm coupling giữa các thành phần như thế nào?',
    createdAt: '2026-09-19T04:20:00Z',
  },
  {
    id: '15bb75e7-1de1-476e-a565-513e64a11004',
    questionBankId: mockQuestionBanks[0].id,
    content:
      'Nếu một API có lượng truy cập tăng gấp mười lần, bạn sẽ đánh giá và cải thiện khả năng mở rộng ra sao?',
    createdAt: '2026-09-20T10:00:00Z',
  },
  {
    id: '168a5d50-cf9c-4741-a551-39ac6c241005',
    questionBankId: mockQuestionBanks[1].id,
    content: 'Phân biệt abstraction và encapsulation bằng một ví dụ cụ thể.',
    createdAt: '2026-09-20T04:00:00Z',
  },
  {
    id: '8d09de69-6a83-45ba-9e99-19322c971006',
    questionBankId: mockQuestionBanks[1].id,
    content:
      'Liskov Substitution Principle có ý nghĩa gì khi thiết kế hệ thống kế thừa?',
    createdAt: '2026-09-20T04:30:00Z',
  },
  {
    id: 'f5de8e7d-8ff5-492a-855e-79d372481007',
    questionBankId: mockQuestionBanks[2].id,
    content: 'Vì sao cần chuẩn hóa cơ sở dữ liệu? Hãy mô tả chuẩn 3NF.',
    createdAt: '2026-09-23T10:00:00Z',
  },
]
