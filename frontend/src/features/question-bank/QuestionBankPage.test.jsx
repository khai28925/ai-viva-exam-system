import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import App from '../../App.jsx'

const bank = {
  id: 'bank-1',
  name: 'Ngân hàng từ API',
  description: 'Dữ liệu server',
  createdAt: '2026-10-01T00:00:00Z',
}
const question = {
  id: 'question-1',
  questionBankId: bank.id,
  content: 'Câu hỏi từ server?',
  createdAt: bank.createdAt,
}
const json = (value, status = 200) =>
  new Response(JSON.stringify(value), { status })

function setup({
  role = 'LECTURER',
  handler = () => undefined,
  banks = [bank],
  questions = [question],
} = {}) {
  const fetchMock = vi.fn((path, options = {}) => {
    const override = handler(path, options)
    if (override !== undefined) return Promise.resolve(override)
    if (path === '/api/v1/auth/me')
      return Promise.resolve(
        json({ id: 'teacher', email: 'teacher@example.test', role }),
      )
    if (path === '/api/v1/auth/csrf')
      return Promise.resolve(json({ token: 'csrf' }))
    if (path === '/api/v1/question-banks') return Promise.resolve(json(banks))
    if (path === `/api/v1/question-banks/${bank.id}/questions`)
      return Promise.resolve(json(questions))
    throw new Error(`Unexpected ${options.method}: ${path}`)
  })
  vi.stubGlobal('fetch', fetchMock)
  const view = render(
    <MemoryRouter initialEntries={['/question-banks']}>
      <App />
    </MemoryRouter>,
  )
  return { ...view, fetchMock, user: userEvent.setup() }
}

describe('Question Bank API-connected route', () => {
  it.each(['ADMIN', 'LECTURER'])(
    'loads real response shapes for %s, without mock data',
    async (role) => {
      setup({ role })
      expect(await screen.findByText(question.content)).toBeInTheDocument()
      expect(screen.getByText('Dữ liệu thật · PostgreSQL')).toBeInTheDocument()
      expect(screen.queryByText('Dữ liệu minh họa')).not.toBeInTheDocument()
      expect(screen.queryByText('Kiến trúc phần mềm')).not.toBeInTheDocument()
    },
  )

  it('blocks a student before making Question Bank requests', async () => {
    const { fetchMock } = setup({ role: 'STUDENT' })
    expect(
      await screen.findByRole('heading', {
        name: 'Bạn không có quyền truy cập trang này.',
      }),
    ).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(([path]) => path.includes('question-banks')),
    ).toBe(false)
  })

  it('shows a retryable error and reloads successfully', async () => {
    let unavailable = true
    const { user } = setup({
      handler: (path) =>
        path === '/api/v1/question-banks' && unavailable
          ? json({}, 503)
          : undefined,
    })
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không thể tải dữ liệu',
    )
    unavailable = false
    await user.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(await screen.findByText(question.content)).toBeInTheDocument()
  })

  it.each([401, 403])(
    'handles lost access (%i) while loading',
    async (status) => {
      setup({
        handler: (path) =>
          path === '/api/v1/question-banks' ? json({}, status) : undefined,
      })
      expect(
        await screen.findByRole('heading', {
          name:
            status === 401
              ? 'Đăng nhập.'
              : 'Bạn không có quyền truy cập trang này.',
        }),
      ).toBeInTheDocument()
    },
  )

  it('preserves the question form on server validation failure, then uses the returned entity', async () => {
    let rejectCreate = true
    const { user, fetchMock } = setup({
      handler: (path, options) => {
        if (path.endsWith('/questions') && options.method === 'POST')
          return rejectCreate
            ? json(
                { errors: { content: ['Nội dung bị từ chối bởi máy chủ.'] } },
                400,
              )
            : json(
                {
                  ...question,
                  id: 'server-generated-id',
                  content: 'Câu hỏi mới từ API',
                },
                201,
              )
      },
    })
    await screen.findByText(question.content)
    await user.click(screen.getByRole('button', { name: 'Thêm câu hỏi' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm câu hỏi' })
    await user.type(
      within(dialog).getByRole('textbox', { name: /Nội dung câu hỏi/ }),
      'Câu hỏi mới từ API',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
    )
    expect(
      await within(dialog).findByText('Nội dung bị từ chối bởi máy chủ.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    rejectCreate = false
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
    )
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    expect(screen.getByText('Câu hỏi mới từ API')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/question-banks/bank-1/questions',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ content: 'Câu hỏi mới từ API' }),
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
      }),
    )
  })

  it('keeps a nonempty bank on 409 and removes it only after a successful DELETE', async () => {
    let conflict = true
    const { user } = setup({
      handler: (path, options) =>
        path === '/api/v1/question-banks/bank-1' && options.method === 'DELETE'
          ? conflict
            ? json({ title: 'Conflict' }, 409)
            : new Response(null, { status: 204 })
          : undefined,
    })
    await screen.findByText(question.content)
    await user.click(
      screen.getByRole('button', { name: 'Xóa ngân hàng', exact: true }),
    )
    const dialog = screen.getByRole('dialog', { name: 'Xóa ngân hàng?' })
    await user.click(
      within(dialog).getByRole('button', {
        name: 'Xóa ngân hàng',
        exact: true,
      }),
    )
    expect(
      await within(dialog).findByText(/Ngân hàng vẫn còn câu hỏi/),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: bank.name })).toBeInTheDocument()
    conflict = false
    await user.click(
      within(dialog).getByRole('button', {
        name: 'Xóa ngân hàng',
        exact: true,
      }),
    )
    expect(
      await screen.findByText('Chưa có ngân hàng câu hỏi'),
    ).toBeInTheDocument()
    expect(screen.queryByText(question.content)).not.toBeInTheDocument()
  })

  it('returns to login if the session expires during a mutation', async () => {
    const { user } = setup({
      handler: (path, options) =>
        path.endsWith('/questions/question-1') && options.method === 'DELETE'
          ? json({}, 401)
          : undefined,
    })
    await screen.findByText(question.content)
    await user.click(screen.getByRole('button', { name: 'Xóa câu hỏi 1' }))
    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', {
        name: 'Xóa câu hỏi',
        exact: true,
      }),
    )
    expect(
      await screen.findByRole('heading', { name: 'Đăng nhập.' }),
    ).toBeInTheDocument()
  })

  it('does not send CRUD requests while logout is in progress', async () => {
    let completeLogout
    const logoutResponse = new Promise((resolve) => {
      completeLogout = resolve
    })
    const { user, fetchMock } = setup({
      handler: (path) =>
        path === '/api/v1/auth/logout' ? logoutResponse : undefined,
    })
    await screen.findByText(question.content)
    await user.click(screen.getByRole('button', { name: 'Đăng xuất' }))
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([path]) => path === '/api/v1/auth/logout'),
      ).toBe(true),
    )
    expect(
      screen.getByRole('button', { name: 'Đang đăng xuất...' }),
    ).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Thêm câu hỏi' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm câu hỏi' })
    await user.type(
      within(dialog).getByRole('textbox', { name: /Nội dung câu hỏi/ }),
      'Không lưu trong lúc đăng xuất',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
    )
    expect(within(dialog).getByRole('alert')).toHaveTextContent(
      'Không thể lưu câu hỏi.',
    )
    expect(
      fetchMock.mock.calls.some(
        ([path, options]) =>
          path.includes('/question-banks') &&
          ['POST', 'PUT', 'DELETE'].includes(options.method),
      ),
    ).toBe(false)

    await act(async () => completeLogout(new Response(null, { status: 204 })))
    expect(
      await screen.findByRole('heading', { name: 'Đăng nhập.' }),
    ).toBeInTheDocument()
  })

  it('disables logout during a mutation without falsely displaying a logout in progress', async () => {
    let completeCreate
    const createResponse = new Promise((resolve) => {
      completeCreate = resolve
    })
    const { user, fetchMock } = setup({
      handler: (path, options) =>
        path.endsWith('/questions') && options.method === 'POST'
          ? createResponse
          : undefined,
    })
    await screen.findByText(question.content)
    await user.click(screen.getByRole('button', { name: 'Thêm câu hỏi' }))
    const dialog = screen.getByRole('dialog', { name: 'Thêm câu hỏi' })
    await user.type(
      within(dialog).getByRole('textbox', { name: /Nội dung câu hỏi/ }),
      'Câu hỏi đang lưu',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Thêm câu hỏi' }),
    )
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([path, options]) =>
            path.endsWith('/questions') && options.method === 'POST',
        ),
      ).toBe(true),
    )
    expect(
      within(dialog).getByRole('button', { name: 'Đang lưu...' }),
    ).toBeDisabled()
    const logoutButton = screen.getByRole('button', { name: 'Đăng xuất' })
    expect(logoutButton).toBeDisabled()
    expect(
      screen.queryByRole('button', { name: 'Đang đăng xuất...' }),
    ).not.toBeInTheDocument()
    await user.click(logoutButton)
    expect(
      fetchMock.mock.calls.some(([path]) => path === '/api/v1/auth/logout'),
    ).toBe(false)

    await act(async () =>
      completeCreate(
        json(
          { ...question, id: 'new-question', content: 'Câu hỏi đang lưu' },
          201,
        ),
      ),
    )
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    expect(screen.getByRole('button', { name: 'Đăng xuất' })).toBeEnabled()
  })

  it('aborts outstanding requests on unmount', async () => {
    let requestSignal
    const { unmount } = setup({
      handler: (path, options) => {
        if (path === '/api/v1/question-banks') {
          requestSignal = options.signal
          return new Promise(() => {})
        }
      },
    })
    await waitFor(() => expect(requestSignal).toBeDefined())
    unmount()
    expect(requestSignal.aborted).toBe(true)
  })
})
