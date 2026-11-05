import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getReport,
  getReports,
  getStatistics,
  finalizeReport,
} from '../../api/interviewApi.js'
import { getExamSessions } from '../../api/examSessionApi.js'
import { AuthContext } from '../auth/authContext.js'
import ReportsPage from './ReportsPage.jsx'

vi.mock('../../api/interviewApi.js', () => ({
  getReport: vi.fn(),
  getReports: vi.fn(),
  getStatistics: vi.fn(),
  finalizeReport: vi.fn(),
  exportUrl: (sessionId, format = 'xlsx') =>
    `/api/v1/reports/sessions/${sessionId}/export?format=${format}`,
}))
vi.mock('../../api/examSessionApi.js', () => ({
  getExamSessions: vi.fn(),
}))

const report = {
  attemptId: 'attempt-1',
  title: 'Viva',
  studentEmail: 'student@local',
  aiScore: 6,
  finalScore: null,
  isFinalized: false,
  questions: [
    {
      questionId: 'question-1',
      question: 'What is DI?',
      transcript: 'It supplies dependencies.',
      aiScore: 6,
      feedback: 'Give an example.',
      criteria: [],
      strengths: ['Core idea'],
      missingKeyPoints: ['Example'],
      followUps: [],
    },
  ],
}

function renderPage(role, path = '/reports/attempt-1') {
  render(
    <AuthContext.Provider
      value={{ user: { role, email: 'user@local' }, logout: vi.fn() }}
    >
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/reports" element={<ReportsPage />} />
          <Route path="/reports/:attemptId" element={<ReportsPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('viva score report', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getReport.mockResolvedValue(report)
  })
  it('shows provisional AI feedback to the student without a grading form', async () => {
    renderPage('STUDENT')
    expect(await screen.findByText('What is DI?')).toBeInTheDocument()
    expect(
      screen.getByText('Kết quả AI là tạm thời, chưa phải điểm chính thức.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /Chốt điểm và công bố/ }),
    ).not.toBeInTheDocument()
    expect(screen.getByText(/It supplies dependencies/)).toBeInTheDocument()
  })
  it('lets the lecturer adjust and publish final marks through the API', async () => {
    finalizeReport.mockResolvedValue({
      ...report,
      finalScore: 8,
      isFinalized: true,
    })
    const user = userEvent.setup()
    renderPage('LECTURER')
    const score = await screen.findByRole('spinbutton')
    await user.clear(score)
    await user.type(score, '8')
    await user.click(
      screen.getByRole('button', { name: 'Chốt điểm và công bố' }),
    )
    await waitFor(() =>
      expect(finalizeReport).toHaveBeenCalledWith('attempt-1', {
        questions: [{ questionId: 'question-1', score: 8 }],
        feedback: '',
      }),
    )
    expect(
      await screen.findByText('Giảng viên đã duyệt kết quả.'),
    ).toBeInTheDocument()
  })
  it('shows the student identity, the lecturer mark per question, Bloom level and answer time', async () => {
    getReport.mockResolvedValue({
      ...report,
      studentName: 'Nguyễn Văn An',
      studentCode: 'SE180001',
      finalScore: 8.5,
      isFinalized: true,
      totalDurationSeconds: 95,
      questions: [
        {
          ...report.questions[0],
          finalScore: 8.5,
          bloomLevel: 'Understand',
          durationSeconds: 65,
          followUps: [
            {
              question:
                "You said 'supplies dependencies'. Can you give an example?",
              answer: 'Constructor injection.',
              durationSeconds: 30,
              probedQuote: 'supplies dependencies',
              probeType: 'example',
            },
          ],
        },
      ],
    })
    renderPage('STUDENT')
    expect(await screen.findByText('SE180001')).toBeInTheDocument()
    expect(screen.getByText('Yêu cầu ví dụ')).toBeInTheDocument()
    expect(
      screen.getByText('Hỏi xoáy vào: “supplies dependencies”'),
    ).toBeInTheDocument()
    expect(screen.getByText(/Nguyễn Văn An/)).toBeInTheDocument()
    expect(screen.getByText('8.5/10 · Giảng viên chốt')).toBeInTheDocument()
    expect(screen.getByText('6/10 · AI')).toBeInTheDocument()
    expect(screen.getByText('Bloom: Hiểu')).toBeInTheDocument()
    expect(screen.getByText('Trả lời trong 1 phút 5 giây')).toBeInTheDocument()
    expect(screen.getByText('Trả lời trong 30 giây')).toBeInTheDocument()
    expect(screen.getByText('1 phút 35 giây')).toBeInTheDocument()
  })
  it('gives lecturers class statistics and grade-sheet exports per session', async () => {
    getExamSessions.mockResolvedValue([{ id: 'session-1', title: 'Viva' }])
    getReports.mockResolvedValue([
      {
        attemptId: 'attempt-1',
        examSessionId: 'session-1',
        title: 'Viva',
        studentEmail: 'student@local',
        studentName: 'Nguyễn Văn An',
        studentCode: 'SE180001',
        status: 'Finalized',
        aiScore: 6,
        finalScore: 8,
      },
    ])
    getStatistics.mockResolvedValue({
      sessionId: 'session-1',
      title: 'Viva',
      participants: 2,
      completed: 1,
      finalized: 1,
      averageScore: 8,
      goodAnswerRate: 100,
      highestScore: 8,
      lowestScore: 8,
      distribution: [
        { label: '0–<5', count: 0 },
        { label: '5–<7', count: 0 },
        { label: '7–<9', count: 1 },
        { label: '9–10', count: 0 },
      ],
      bloomLevels: [
        {
          bloomLevel: 'Apply',
          responseCount: 1,
          averageScore: 8,
          goodAnswerRate: 100,
        },
      ],
      questions: [
        {
          questionId: 'q-1',
          content: 'What is DI?',
          responseCount: 1,
          averageScore: 8,
          goodAnswerRate: 100,
          bloomLevel: 'Apply',
          averageDurationSeconds: 42,
          commonMissingKeyPoints: ['Example'],
        },
      ],
    })
    renderPage('LECTURER', '/reports')
    expect(await screen.findByText('Thống kê: Viva')).toBeInTheDocument()
    expect(screen.getByText('1/2')).toBeInTheDocument()
    expect(
      within(screen.getByRole('list', { name: 'Phân bố điểm' })).getAllByRole(
        'listitem',
      ),
    ).toHaveLength(4)
    expect(screen.getByText('Vận dụng')).toBeInTheDocument()
    expect(screen.getByText('42 giây')).toBeInTheDocument()
    expect(screen.getAllByText('SE180001').length).toBeGreaterThan(0)
    expect(
      screen.getByRole('link', { name: 'Xuất bảng điểm Excel' }),
    ).toHaveAttribute(
      'href',
      '/api/v1/reports/sessions/session-1/export?format=xlsx',
    )
    expect(screen.getByRole('link', { name: 'CSV' })).toHaveAttribute(
      'href',
      '/api/v1/reports/sessions/session-1/export?format=csv',
    )
  })
})
