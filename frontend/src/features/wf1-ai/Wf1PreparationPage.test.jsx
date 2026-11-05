import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../api/wf1Api.js'
import { AuthContext } from '../auth/authContext.js'
import Wf1PreparationPage from './Wf1PreparationPage.jsx'

vi.mock('../../api/wf1Api.js', () => ({
  approveRubricSet: vi.fn(),
  createSubject: vi.fn(),
  createSubjectQuestionBank: vi.fn(),
  deleteStudyDocument: vi.fn(),
  generateWf1Content: vi.fn(),
  getSubjects: vi.fn(),
  getSubjectWorkspace: vi.fn(),
  reindexStudyDocument: vi.fn(),
  reviewQuestionCandidate: vi.fn(),
  saveDraftRubrics: vi.fn(),
  updateQuestionCandidate: vi.fn(),
  uploadStudyDocument: vi.fn(),
}))

const bank = { id: 'bank-1', name: 'Main bank' }
const approvedRubricSet = {
  id: 'set-1',
  version: 1,
  status: 'Approved',
  source: 'AI',
  rubrics: [
    {
      id: 'rubric-1',
      name: 'Kiến thức',
      description: null,
      weightPercent: 100,
      criteria: [
        {
          id: 'criterion-1',
          name: 'Chính xác',
          description: null,
          maxScore: 10,
        },
      ],
    },
  ],
}
const workspace = {
  subject: { id: 'subject-1', name: 'Databases', questionBanks: [bank] },
  documents: [
    {
      id: 'document-1',
      fileName: 'lesson.pdf',
      sizeBytes: 2048,
      indexStatus: 'Indexed',
      pageCount: 12,
      chunkCount: 9,
      semanticSearch: false,
      indexError: 'Chưa tạo được embedding; đang dùng tìm kiếm theo từ khóa.',
    },
  ],
  questionCandidates: [
    {
      id: 'candidate-1',
      questionBankId: 'bank-1',
      content: 'Vì sao cần chuẩn hóa lược đồ?',
      bloomLevel: 'Analyze',
      difficultyLevel: 3,
      rationale: 'So sánh đánh đổi.',
      expectedKeyPoints: ['Giảm dư thừa'],
      status: 'Pending',
      approvedQuestionId: null,
      topic: 'Chuẩn hóa',
      sources: [
        {
          fileName: 'lesson.pdf',
          pageStart: 3,
          pageEnd: 4,
          excerpt: 'Chuẩn hóa loại bỏ dư thừa dữ liệu.',
        },
      ],
    },
  ],
  draftRubricSet: null,
  approvedRubricSet,
}

function renderPage() {
  render(
    <AuthContext.Provider
      value={{
        user: { role: 'LECTURER', email: 'lecturer@local' },
        logout: vi.fn(),
      }}
    >
      <MemoryRouter initialEntries={['/wf1-preparation?subject=subject-1']}>
        <Wf1PreparationPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('WF1 AI preparation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.getSubjects.mockResolvedValue([
      { id: 'subject-0', name: 'Another subject', questionBanks: [] },
      { id: 'subject-1', name: 'Databases', questionBanks: [bank] },
    ])
    api.getSubjectWorkspace.mockResolvedValue(workspace)
  })

  it('opens the subject from the link, cites sources and re-indexes keyword-only documents', async () => {
    api.reindexStudyDocument.mockResolvedValue({})
    const user = userEvent.setup()
    renderPage()

    expect(
      await screen.findByText('lesson.pdf · trang 3–4'),
    ).toBeInTheDocument()
    expect(api.getSubjectWorkspace).toHaveBeenCalledWith('subject-1')
    expect(
      screen.getByText('Chuẩn hóa loại bỏ dư thừa dữ liệu.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Chủ đề: Chuẩn hóa')).toBeInTheDocument()
    expect(
      screen.getByText(/12 trang · 9 đoạn · tìm theo từ khóa/),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Lập chỉ mục lại' }))
    await waitFor(() =>
      expect(api.reindexStudyDocument).toHaveBeenCalledWith(
        'subject-1',
        'document-1',
      ),
    )
  })

  it('generates topic-focused questions without overwriting an approved rubric set', async () => {
    api.generateWf1Content.mockResolvedValue(workspace)
    const user = userEvent.setup()
    renderPage()
    const topic = await screen.findByRole('textbox', { name: /Chủ đề/ })
    const regenerate = screen.getByRole('checkbox', {
      name: /Đề xuất lại bộ rubric/,
    })
    expect(regenerate).not.toBeChecked()

    await user.type(topic, 'Chuẩn hóa')
    await user.click(screen.getByRole('button', { name: 'Tạo câu hỏi' }))

    await waitFor(() =>
      expect(api.generateWf1Content).toHaveBeenCalledWith('subject-1', {
        questionBankId: 'bank-1',
        questionCount: 5,
        topic: 'Chuẩn hóa',
        includeRubrics: false,
      }),
    )
  })

  it('creates a question set next to the set picker and generates into it', async () => {
    const chapter = { id: 'bank-2', name: 'Chương 24', description: null }
    const withChapter = {
      ...workspace,
      subject: { ...workspace.subject, questionBanks: [bank, chapter] },
    }
    api.createSubjectQuestionBank.mockResolvedValue(chapter)
    api.generateWf1Content.mockResolvedValue(withChapter)
    const user = userEvent.setup()
    renderPage()
    const picker = await screen.findByLabelText('Lưu vào bộ câu hỏi')
    expect(picker).toHaveValue('bank-1')
    api.getSubjectWorkspace.mockResolvedValue(withChapter)

    await user.click(screen.getByRole('button', { name: '+ Tạo bộ mới' }))
    const form = screen.getByRole('form', { name: 'Tạo bộ câu hỏi mới' })
    await user.type(
      screen.getByRole('textbox', { name: 'Tên bộ câu hỏi' }),
      'Chương 24',
    )
    await user.click(screen.getByRole('button', { name: 'Tạo bộ câu hỏi' }))

    await waitFor(() => expect(picker).toHaveValue('bank-2'))
    expect(api.createSubjectQuestionBank).toHaveBeenCalledWith('subject-1', {
      name: 'Chương 24',
      description: '',
    })
    expect(form).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Tạo câu hỏi' }))
    await waitFor(() =>
      expect(api.generateWf1Content).toHaveBeenCalledWith(
        'subject-1',
        expect.objectContaining({ questionBankId: 'bank-2' }),
      ),
    )
  })

  it('lets the lecturer add rubrics and criteria, then saves and approves them', async () => {
    api.saveDraftRubrics.mockResolvedValue({})
    api.getSubjectWorkspace
      .mockResolvedValueOnce(workspace)
      .mockResolvedValueOnce({
        ...workspace,
        draftRubricSet: { ...approvedRubricSet, id: 'set-2', version: 2 },
      })
    api.approveRubricSet.mockResolvedValue({})
    const user = userEvent.setup()
    renderPage()
    await screen.findAllByLabelText('Tên rubric')

    await user.click(screen.getByRole('button', { name: '+ Thêm rubric' }))
    const names = screen.getAllByLabelText('Tên rubric')
    const weights = screen.getAllByLabelText('Tỷ trọng %')
    expect(names).toHaveLength(2)
    await user.type(names[1], 'Diễn đạt')
    await user.clear(weights[0])
    await user.type(weights[0], '60')
    await user.clear(weights[1])
    await user.type(weights[1], '40')
    const criterionNames = screen.getAllByLabelText('Tên tiêu chí')
    await user.type(criterionNames[1], 'Rõ ràng')
    await user.type(
      screen.getAllByLabelText('Mô tả tiêu chí')[1],
      'Trình bày mạch lạc',
    )

    await user.click(
      screen.getAllByRole('button', { name: '+ Thêm tiêu chí' })[0],
    )
    expect(screen.getAllByLabelText('Tên tiêu chí')).toHaveLength(3)
    await user.click(
      screen.getByRole('button', { name: 'Xóa tiêu chí 2 của Kiến thức' }),
    )
    expect(screen.getAllByLabelText('Tên tiêu chí')).toHaveLength(2)
    expect(screen.getByText('100%')).toBeInTheDocument()

    await user.click(
      screen.getByRole('button', { name: 'Lưu và duyệt bộ rubric' }),
    )
    await waitFor(() =>
      expect(api.approveRubricSet).toHaveBeenCalledWith('subject-1', 'set-2'),
    )
    expect(api.saveDraftRubrics).toHaveBeenCalledWith('subject-1', [
      {
        name: 'Kiến thức',
        description: '',
        weightPercent: 60,
        criteria: [{ name: 'Chính xác', description: '', maxScore: 10 }],
      },
      {
        name: 'Diễn đạt',
        description: '',
        weightPercent: 40,
        criteria: [
          { name: 'Rõ ràng', description: 'Trình bày mạch lạc', maxScore: 10 },
        ],
      },
    ])
  })
})
