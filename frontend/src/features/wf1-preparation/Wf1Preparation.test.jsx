import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import ExpectedKeyPointsCard from './ExpectedKeyPointsCard.jsx'
import QuestionSelectionCard from './QuestionSelectionCard.jsx'
import ReadinessCard from './ReadinessCard.jsx'
import RubricCard from './RubricCard.jsx'
import StudentSessionsCard from './StudentSessionsCard.jsx'
import {
  getReadiness,
  previewCriteria,
  previewPoints,
  previewQuestions,
  previewRubric,
  previewSession,
} from './mockData.js'

describe('WF1 preparation UI', () => {
  it('validates and saves an expected key point using the contract body', async () => {
    const user = userEvent.setup()
    const onCreate = vi.fn()
    render(
      <ExpectedKeyPointsCard
        question={previewQuestions[0]}
        points={[]}
        onCreate={onCreate}
      />,
    )
    expect(screen.getByText(/Chưa có ý chính/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Thêm ý chính/ }))
    await user.click(screen.getByRole('button', { name: 'Lưu ý chính' }))
    expect(screen.getByRole('alert')).toHaveTextContent('1 đến 2000 ký tự')
    await user.type(
      screen.getByRole('textbox', { name: 'Thêm ý chính' }),
      '  Nêu ví dụ  ',
    )
    await user.click(screen.getByRole('button', { name: 'Lưu ý chính' }))
    expect(onCreate).toHaveBeenCalledWith({ content: 'Nêu ví dụ' })
  })

  it('requires confirmation before deleting a key point', async () => {
    const user = userEvent.setup()
    const onDelete = vi.fn()
    render(
      <ExpectedKeyPointsCard
        question={previewQuestions[0]}
        points={[previewPoints[0]]}
        onDelete={onDelete}
      />,
    )
    await user.click(screen.getByRole('button', { name: 'Xóa ý chính 1' }))
    expect(onDelete).not.toHaveBeenCalled()
    const confirmation = screen.getByRole('alertdialog', {
      name: 'Xác nhận xóa ý chính',
    })
    await user.click(
      within(confirmation).getByRole('button', { name: 'Xác nhận xóa' }),
    )
    expect(onDelete).toHaveBeenCalledWith(previewPoints[0].id)
  })

  it('creates a criterion with trimmed fields and numeric score/weight', async () => {
    const user = userEvent.setup()
    const onCreateCriterion = vi.fn()
    render(
      <RubricCard
        rubric={previewRubric}
        criteria={[]}
        onCreateCriterion={onCreateCriterion}
      />,
    )
    await user.click(screen.getByRole('button', { name: /Thêm tiêu chí/ }))
    await user.type(
      screen.getByRole('textbox', { name: 'Tên tiêu chí' }),
      '  Lập luận  ',
    )
    await user.click(screen.getByRole('button', { name: 'Lưu tiêu chí' }))
    expect(onCreateCriterion).toHaveBeenCalledWith({
      name: 'Lập luận',
      description: null,
      maxScore: 10,
      weight: 0.5,
    })
  })

  it('confirms criterion deletion and exposes a permission error without calling mutations', async () => {
    const user = userEvent.setup()
    const onDeleteCriterion = vi.fn()
    const { rerender } = render(
      <RubricCard
        rubric={previewRubric}
        criteria={[previewCriteria[0]]}
        onDeleteCriterion={onDeleteCriterion}
      />,
    )
    await user.click(screen.getByRole('button', { name: 'Xóa' }))
    expect(onDeleteCriterion).not.toHaveBeenCalled()
    await user.click(
      within(
        screen.getByRole('alertdialog', { name: 'Xác nhận xóa tiêu chí' }),
      ).getByRole('button', { name: 'Xác nhận xóa' }),
    )
    expect(onDeleteCriterion).toHaveBeenCalledWith(previewCriteria[0].id)
    rerender(<RubricCard rubric={previewRubric} error={{ status: 403 }} />)
    expect(screen.getByRole('alert')).toHaveTextContent('không có quyền')
  })

  it('keeps question selection locked outside Draft and displays the stored snapshot', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(
      <QuestionSelectionCard
        questions={previewQuestions.slice(0, 2)}
        selectedQuestionIds={[previewQuestions[0].id]}
        selectedQuestions={[
          {
            questionId: previewQuestions[0].id,
            contentSnapshot: 'Nội dung đã lưu lúc chọn',
            displayOrder: 0,
            isRequired: true,
          },
        ]}
        status="Open"
        onChange={onChange}
      />,
    )
    expect(screen.getByText('Nội dung đã lưu lúc chọn')).toBeInTheDocument()
    expect(
      screen.getByRole('checkbox', { name: previewQuestions[0].content }),
    ).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Chọn cả ngân hàng' }))
    expect(onChange).not.toHaveBeenCalled()
  })

  it('does not silently truncate a bank of more than 50 questions', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    const questions = Array.from({ length: 51 }, (_, index) => ({
      id: String(index),
      content: `Câu ${index + 1}`,
      questionType: 'Bank',
    }))
    render(<QuestionSelectionCard questions={questions} onChange={onChange} />)
    await user.click(screen.getByRole('button', { name: 'Chọn cả ngân hàng' }))
    expect(screen.getByRole('alert')).toHaveTextContent('tối đa 50')
    expect(onChange).not.toHaveBeenCalled()
  })

  it('shows missing readiness criteria and server-side 409 details', async () => {
    const user = userEvent.setup()
    const onPublish = vi.fn()
    const missing = ['keyPoints', 'rubricWeight']
    render(
      <ReadinessCard
        missing={missing}
        questionIdsWithoutKeyPoints={[previewQuestions[1].id]}
        serverError={{
          status: 409,
          problem: { code: 'session_not_ready', missing },
        }}
        onPublish={onPublish}
      />,
    )
    expect(screen.getByText(/Câu hỏi còn thiếu ý chính: 1/)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('chưa đủ điều kiện')
    await user.click(screen.getByRole('button', { name: /Còn 2 điều kiện/ }))
    expect(onPublish).not.toHaveBeenCalled()
  })

  it('shows only Open sessions and displays selected questions after join', async () => {
    const user = userEvent.setup()
    const onJoin = vi.fn()
    const sessions = [
      { ...previewSession, status: 'Open' },
      {
        ...previewSession,
        id: 'draft-id',
        title: 'Phiên chưa mở',
        status: 'Draft',
      },
    ]
    const attempt = {
      questions: [{ id: previewQuestions[0].id, content: 'Snapshot từ API' }],
    }
    const { rerender } = render(
      <StudentSessionsCard sessions={sessions} onJoin={onJoin} />,
    )
    expect(screen.queryByText('Phiên chưa mở')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Vào thi/ }))
    expect(onJoin).toHaveBeenCalledWith(previewSession.id)
    rerender(<StudentSessionsCard sessions={sessions} attempt={attempt} />)
    expect(screen.getByText('Snapshot từ API')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /Nộp bài|Trả lời/ }),
    ).not.toBeInTheDocument()
  })

  it('shows loading and empty student states', () => {
    const { rerender } = render(<StudentSessionsCard isLoading />)
    expect(screen.getByRole('status')).toHaveTextContent('Đang tải phiên thi')
    rerender(<StudentSessionsCard sessions={[]} />)
    expect(
      screen.getByRole('heading', { name: 'Chưa có phiên đang mở' }),
    ).toBeInTheDocument()
  })

  it('evaluates the mock readiness from the same five contract codes', () => {
    const ready = getReadiness({
      selectedQuestionIds: [previewQuestions[0].id],
      points: previewPoints,
      rubric: previewRubric,
      criteria: previewCriteria,
    })
    expect(ready).toEqual({ missing: [], questionIdsWithoutKeyPoints: [] })
    const incomplete = getReadiness({
      selectedQuestionIds: [previewQuestions[2].id],
      points: [],
      rubric: null,
      criteria: [],
    })
    expect(incomplete.missing).toContain('keyPoints')
    expect(incomplete.missing).toContain('rubric')
    expect(incomplete.missing).toContain('rubricCriteria')
    expect(incomplete.questionIdsWithoutKeyPoints).toEqual([
      previewQuestions[2].id,
    ])
  })
})
