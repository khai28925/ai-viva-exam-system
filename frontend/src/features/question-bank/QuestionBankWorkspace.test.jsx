import { act, fireEvent, render, screen, within } from '@testing-library/react'
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
  return { ...callbacks, ...overrides }
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

  it('preserves the standalone mock preview when bank callbacks are omitted', () => {
    renderWorkspace()
    expect(screen.getByText('Dữ liệu minh họa')).toBeInTheDocument()
    expect(screen.getByText('Bản xem trước UI')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Thêm ngân hàng' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Sửa ngân hàng' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Đăng xuất' }),
    ).not.toBeInTheDocument()
  })

  it('renders signed-in account context and logout controls', async () => {
    const onLogout = vi.fn()
    renderWorkspace({
      dataSourceLabel: 'Dữ liệu PostgreSQL',
      accountLabel: 'Admin',
      accountDetail: 'admin@example.com',
      navigation: <a href="/admin">Trang quản trị</a>,
      onLogout,
      sessionError: 'Đăng xuất chưa thành công.',
    })
    expect(screen.getByText('Dữ liệu PostgreSQL')).toBeInTheDocument()
    expect(screen.getByText('admin@example.com')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Trang quản trị' }),
    ).toHaveAttribute('href', '/admin')
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Đăng xuất chưa thành công.',
    )
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Đăng xuất' }))
    expect(onLogout).toHaveBeenCalledOnce()
  })

  it('creates the first bank from the empty state and sends a nullable description', async () => {
    const onCreateBank = vi.fn()
    renderWorkspace({ banks: [], questions: [], onCreateBank })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Thêm ngân hàng' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm ngân hàng' })
    await user.type(
      within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ }),
      '  Kiến trúc  ',
    )
    await user.type(
      within(dialog).getByRole('textbox', { name: 'Mô tả' }),
      '   ',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm ngân hàng' }),
    )
    expect(onCreateBank).toHaveBeenCalledWith({
      name: 'Kiến trúc',
      description: null,
    })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('validates bank name and description limits before submitting', async () => {
    const onCreateBank = vi.fn()
    renderWorkspace({ onCreateBank })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Thêm ngân hàng' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm ngân hàng' })
    const name = within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ })
    const description = within(dialog).getByRole('textbox', { name: 'Mô tả' })
    const submit = within(dialog).getByRole('button', {
      name: 'Thêm ngân hàng',
    })
    await user.click(submit)
    expect(within(dialog).getByRole('alert')).toHaveTextContent(
      'Tên ngân hàng cần từ 1 đến 120 ký tự.',
    )
    expect(name).toHaveAttribute('maxLength', '120')
    expect(description).toHaveAttribute('maxLength', '500')
    fireEvent.change(name, { target: { value: 'a'.repeat(121) } })
    await user.click(submit)
    expect(within(dialog).getByRole('alert')).toHaveTextContent(
      'Tên ngân hàng cần từ 1 đến 120 ký tự.',
    )
    fireEvent.change(name, { target: { value: 'Valid bank' } })
    fireEvent.change(description, { target: { value: 'a'.repeat(501) } })
    await user.click(submit)
    expect(within(dialog).getByRole('alert')).toHaveTextContent(
      'Mô tả không được vượt quá 500 ký tự.',
    )
    expect(onCreateBank).not.toHaveBeenCalled()
  })

  it('updates the selected bank with a trimmed payload', async () => {
    const onUpdateBank = vi.fn()
    renderWorkspace({ onUpdateBank })
    const user = userEvent.setup()
    await user.click(
      screen.getByRole('button', { name: /Lập trình hướng đối tượng/ }),
    )
    await user.click(screen.getByRole('button', { name: 'Sửa ngân hàng' }))
    const dialog = screen.getByRole('dialog', { name: 'Chỉnh sửa ngân hàng' })
    const name = within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ })
    const description = within(dialog).getByRole('textbox', { name: 'Mô tả' })
    expect(name).toHaveValue(mockQuestionBanks[1].name)
    await user.clear(name)
    await user.type(name, '  OOP cập nhật  ')
    await user.clear(description)
    await user.type(description, '  Nội dung mới  ')
    await user.click(
      within(dialog).getByRole('button', { name: 'Lưu thay đổi' }),
    )
    expect(onUpdateBank).toHaveBeenCalledWith(mockQuestionBanks[1].id, {
      name: 'OOP cập nhật',
      description: 'Nội dung mới',
    })
  })

  it('keeps bank errors visible and explains how to resolve a nonempty-bank conflict', async () => {
    const onDeleteBank = vi.fn().mockRejectedValue({
      status: 409,
      problem: { detail: 'Bank is not empty' },
    })
    renderWorkspace({ onDeleteBank })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Xóa ngân hàng' }))
    const dialog = screen.getByRole('dialog', { name: 'Xóa ngân hàng?' })
    expect(
      within(dialog).getByText(mockQuestionBanks[0].name),
    ).toBeInTheDocument()
    await user.click(
      within(dialog).getByRole('button', { name: 'Xóa ngân hàng' }),
    )
    expect(onDeleteBank).toHaveBeenCalledWith(mockQuestionBanks[0].id)
    expect(within(dialog).getByRole('alert')).toHaveTextContent(
      'Hãy xóa hết câu hỏi trước khi xóa ngân hàng.',
    )
    onDeleteBank.mockResolvedValueOnce(null)
    await user.click(
      within(dialog).getByRole('button', { name: 'Xóa ngân hàng' }),
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('renders server validation details without discarding the entered bank data', async () => {
    const onCreateBank = vi.fn().mockRejectedValue({
      status: 400,
      problem: { errors: { Name: ['Tên ngân hàng không hợp lệ.'] } },
    })
    renderWorkspace({ onCreateBank })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Thêm ngân hàng' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm ngân hàng' })
    await user.type(
      within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ }),
      'Ngân hàng mới',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm ngân hàng' }),
    )
    expect(within(dialog).getByRole('alert')).toHaveTextContent(
      'Tên ngân hàng không hợp lệ.',
    )
    expect(
      within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ }),
    ).toHaveValue('Ngân hàng mới')
  })

  it('prevents dismissing or submitting a bank form twice while saving', async () => {
    let resolveCreate
    const onCreateBank = vi.fn().mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveCreate = resolve
        }),
    )
    renderWorkspace({ onCreateBank })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Thêm ngân hàng' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm ngân hàng' })
    await user.type(
      within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ }),
      'Ngân hàng mới',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm ngân hàng' }),
    )
    expect(dialog).toHaveAttribute('aria-busy', 'true')
    expect(
      within(dialog).getByRole('button', { name: 'Đóng hộp thoại' }),
    ).toBeDisabled()
    expect(within(dialog).getByRole('button', { name: 'Hủy' })).toBeDisabled()
    await user.keyboard('{Escape}')
    fireEvent.mouseDown(dialog.parentElement)
    fireEvent.submit(dialog.querySelector('form'))
    expect(onCreateBank).toHaveBeenCalledOnce()
    expect(dialog).toBeInTheDocument()
    await act(async () => resolveCreate({ id: 'created-bank-id' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('traps keyboard focus in the dialog and restores focus on cancel', async () => {
    renderWorkspace({ onCreateBank: vi.fn() })
    const user = userEvent.setup()
    const trigger = screen.getByRole('button', { name: 'Thêm ngân hàng' })
    await user.click(trigger)
    const dialog = screen.getByRole('dialog', { name: 'Thêm ngân hàng' })
    expect(
      within(dialog).getByRole('button', { name: 'Đóng hộp thoại' }),
    ).toHaveFocus()
    await user.tab({ shift: true })
    expect(
      within(dialog).getByRole('button', { name: 'Thêm ngân hàng' }),
    ).toHaveFocus()
    await user.tab()
    expect(
      within(dialog).getByRole('button', { name: 'Đóng hộp thoại' }),
    ).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })

  it.each([
    [
      {
        status: 400,
        problem: { errors: { Content: ['Nội dung không hợp lệ.'] } },
      },
      'Nội dung không hợp lệ.',
    ],
    [{ status: 401 }, 'Phiên đăng nhập đã hết hạn.'],
    [{ status: 403 }, 'Bạn không có quyền'],
    [{ status: 404 }, 'Dữ liệu không còn tồn tại.'],
    [
      { status: 500, problem: { detail: 'Dịch vụ tạm thời không khả dụng.' } },
      'Dịch vụ tạm thời không khả dụng.',
    ],
  ])(
    'shows question mutation errors and keeps the dialog open (%j)',
    async (error, expected) => {
      renderWorkspace({ onCreateQuestion: vi.fn().mockRejectedValue(error) })
      const user = userEvent.setup()
      await user.click(screen.getByRole('button', { name: 'Thêm câu hỏi' }))
      const dialog = screen.getByRole('dialog', { name: 'Thêm câu hỏi' })
      await user.type(
        within(dialog).getByRole('textbox', { name: /Nội dung câu hỏi/ }),
        'Nội dung mới',
      )
      await user.click(
        within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
      )
      expect(within(dialog).getByRole('alert')).toHaveTextContent(expected)
      expect(dialog).toBeInTheDocument()
    },
  )
})
