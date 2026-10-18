import { mockQuestionBanks, mockQuestions } from '../question-bank/mockData.js'

export const previewBanks = mockQuestionBanks
export const previewQuestions = mockQuestions.map((question) => ({
  ...question,
  questionType: 'Bank',
}))
export const previewPoints = [
  {
    id: '94d92008-8b1a-4095-b2f8-01d01a516001',
    questionId: previewQuestions[0].id,
    content:
      'So sánh khả năng triển khai, mở rộng và độ phức tạp vận hành của hai mô hình.',
    displayOrder: 0,
    createdAt: '2026-10-18T08:00:00Z',
  },
  {
    id: '94d92008-8b1a-4095-b2f8-01d01a516002',
    questionId: previewQuestions[0].id,
    content: 'Nêu bối cảnh phù hợp để chọn monolith hoặc microservices.',
    displayOrder: 1,
    createdAt: '2026-10-18T08:05:00Z',
  },
  {
    id: '94d92008-8b1a-4095-b2f8-01d01a516003',
    questionId: previewQuestions[1].id,
    content: 'Một lớp chỉ nên có một lý do để thay đổi; đưa ra ví dụ cụ thể.',
    displayOrder: 0,
    createdAt: '2026-10-18T08:10:00Z',
  },
]
export const previewRubric = {
  id: '490d39e3-e174-4f94-a471-f62cf39c7001',
  questionBankId: previewBanks[0].id,
  name: 'Đánh giá hiểu biết kiến trúc',
  description: 'Chấm độ chính xác và khả năng lập luận của sinh viên.',
  createdAt: '2026-10-18T08:15:00Z',
}
export const previewCriteria = [
  {
    id: 'db311a43-af57-4480-9c4f-cb1732fd8001',
    rubricId: previewRubric.id,
    name: 'Độ chính xác',
    description: 'Nêu đúng khái niệm và sự khác biệt.',
    maxScore: 10,
    weight: 0.6,
    createdAt: '2026-10-18T08:20:00Z',
  },
  {
    id: 'db311a43-af57-4480-9c4f-cb1732fd8002',
    rubricId: previewRubric.id,
    name: 'Lập luận và ví dụ',
    description: 'Áp dụng được vào một tình huống thực tế.',
    maxScore: 10,
    weight: 0.4,
    createdAt: '2026-10-18T08:25:00Z',
  },
]
export const previewSession = {
  id: '46f0d93e-31bb-4a71-b130-47ac789b9001',
  title: 'Vấn đáp Kiến trúc phần mềm',
  questionBankId: previewBanks[0].id,
  questionBankName: previewBanks[0].name,
  status: 'Draft',
  createdBy: 'e53fa2b8-4cb1-4fa5-8256-066173715111',
  participantCount: 0,
  createdAt: '2026-10-18T09:00:00Z',
}

export function getReadiness({
  selectedQuestionIds,
  points,
  rubric,
  criteria,
}) {
  const missing = []
  const questionIdsWithoutKeyPoints = selectedQuestionIds.filter(
    (id) => !points.some((point) => point.questionId === id),
  )
  if (selectedQuestionIds.length === 0) missing.push('selection')
  if (questionIdsWithoutKeyPoints.length > 0) missing.push('keyPoints')
  if (!rubric) missing.push('rubric')
  if (criteria.length === 0) missing.push('rubricCriteria')
  if (
    criteria.length > 0 &&
    criteria.reduce((sum, item) => sum + Math.round(item.weight * 10000), 0) !==
      10000
  )
    missing.push('rubricWeight')
  return { missing, questionIdsWithoutKeyPoints }
}
