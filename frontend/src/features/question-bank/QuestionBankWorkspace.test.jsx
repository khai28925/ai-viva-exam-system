import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import QuestionBankWorkspace from './QuestionBankWorkspace.jsx'
import { mockQuestionBanks, mockQuestions } from './mockData.js'

function renderWorkspace(overrides = {}) {
  const callbacks = {
    onCreateQuestion: vi.fn(),
    onUpdateQuestion: vi.fn(),
    onDeleteQuestion: vi.fn(),
    onRetry: vi.fn(),
  }
  render(
    <QuestionBankWorkspace
      banks={mockQuestionBanks}
      questions={mockQuestions}
      {...callbacks}
      {...overrides}
    />,
  )
  return callbacks
}

describe('QuestionBankWorkspace', () => {
  it('shows the first bank and switches to another bank without fetching data', async () => {
    renderWorkspace()
    expect(
      screen.getByRole('heading', { name: 'Kiến trúc phần mềm' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Kiến trúc monolith và microservices/),
    ).toBeInTheDocument()

    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: /Lập trình hướng đối tượng/ }))
    expect(
      screen.getByRole('heading', { name: 'Lập trình hướng đối tượng' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Phân biệt abstraction và encapsulation/),
    ).toBeInTheDocument()
    expect(
      screen.queryByText(/Kiến trúc monolith và microservices/),
    ).not.toBeInTheDocument()
  })

  it('filters the question list', async () => {
    renderWorkspace()
    await userEvent
      .setup()
      .type(
        screen.getByRole('textbox', { name: 'Tìm câu hỏi' }),
        'Dependency Injection',
      )
    expect(
      screen.getByText(/Dependency Injection giúp giảm coupling/),
    ).toBeInTheDocument()
    expect(
      screen.queryByText(/Kiến trúc monolith và microservices/),
    ).not.toBeInTheDocument()
  })

  it('validates and submits a trimmed create payload to the parent callback', async () => {
    const callbacks = renderWorkspace()
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Thêm câu hỏi' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm câu hỏi' })
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
    )
    expect(
      within(dialog).getByText('Nội dung cần từ 1 đến 2000 ký tự.'),
    ).toBeInTheDocument()
    expect(callbacks.onCreateQuestion).not.toHaveBeenCalled()

    await user.type(
      within(dialog).getByRole('textbox', { name: /Nội dung câu hỏi/ }),
      '  Câu hỏi mới?  ',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
    )
    expect(callbacks.onCreateQuestion).toHaveBeenCalledWith(
      mockQuestionBanks[0].id,
      { content: 'Câu hỏi mới?' },
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('passes bank and question ids when editing and deleting', async () => {
    const callbacks = renderWorkspace()
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Sửa câu hỏi 1' }))
    const editDialog = screen.getByRole('dialog', { name: 'Chỉnh sửa câu hỏi' })
    const input = within(editDialog).getByRole('textbox', {
      name: /Nội dung câu hỏi/,
    })
    await user.clear(input)
    await user.type(input, 'Nội dung đã chỉnh sửa')
    await user.click(
      within(editDialog).getByRole('button', { name: 'Lưu thay đổi' }),
    )
    expect(callbacks.onUpdateQuestion).toHaveBeenCalledWith(
      mockQuestionBanks[0].id,
      mockQuestions[0].id,
      { content: 'Nội dung đã chỉnh sửa' },
    )

    await user.click(screen.getByRole('button', { name: 'Xóa câu hỏi 1' }))
    const deleteDialog = screen.getByRole('dialog', { name: 'Xóa câu hỏi?' })
    expect(
      within(deleteDialog).getByText(mockQuestions[0].content),
    ).toBeInTheDocument()
    await user.click(
      within(deleteDialog).getByRole('button', { name: 'Xóa câu hỏi' }),
    )
    expect(callbacks.onDeleteQuestion).toHaveBeenCalledWith(
      mockQuestionBanks[0].id,
      mockQuestions[0].id,
    )
  })

  it('shows empty, loading, and error states', async () => {
    const { rerender } = render(
      <QuestionBankWorkspace banks={[]} questions={[]} />,
    )
    expect(screen.getByText('Chưa có ngân hàng câu hỏi')).toBeInTheDocument()

    rerender(<QuestionBankWorkspace banks={mockQuestionBanks} questions={[]} />)
    expect(screen.getByText('Chưa có câu hỏi nào')).toBeInTheDocument()

    rerender(
      <QuestionBankWorkspace
        banks={mockQuestionBanks}
        questions={[]}
        isLoading
      />,
    )
    expect(screen.getByText('Đang tải ngân hàng câu hỏi')).toBeInTheDocument()

    const onRetry = vi.fn()
    rerender(
      <QuestionBankWorkspace
        banks={mockQuestionBanks}
        questions={[]}
        error="Không có kết nối"
        onRetry={onRetry}
      />,
    )
    expect(screen.getByText('Không có kết nối')).toBeInTheDocument()
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(onRetry).toHaveBeenCalledOnce()
  })
})
