import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import App from './App.jsx'

function json(value, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function renderApp(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('Authentication and role routes', () => {
  it('redirects anonymous users to login, then to their role workspace', async () => {
    const fetchMock = vi.fn((path) => {
      if (path === '/api/v1/auth/me') return Promise.resolve(json({}, 401))
      if (path === '/api/v1/auth/csrf') {
        return Promise.resolve(json({ token: 'csrf-login' }))
      }
      if (path === '/api/v1/auth/login') {
        return Promise.resolve(
          json({ id: '1', email: 'teacher@example.com', role: 'LECTURER' }),
        )
      }
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)

    renderApp()
    expect(
      await screen.findByRole('heading', { name: 'Đăng nhập.' }),
    ).toBeInTheDocument()
    await userEvent
      .setup()
      .type(screen.getByLabelText('Email'), 'teacher@example.com')
    await userEvent
      .setup()
      .type(screen.getByLabelText('Mật khẩu'), 'secret-password')
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: /Đăng nhập/ }))

    expect(
      await screen.findByRole('heading', { name: 'Không gian giảng viên.' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/auth/login',
      expect.objectContaining({
        credentials: 'include',
        method: 'POST',
        body: JSON.stringify({
          email: 'teacher@example.com',
          password: 'secret-password',
        }),
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-login' }),
      }),
    )
  })

  it('does not allow a student to enter the admin workspace', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          json({ id: '2', email: 'student@example.com', role: 'STUDENT' }),
        ),
    )
    renderApp('/admin')
    expect(
      await screen.findByRole('heading', {
        name: 'Bạn không có quyền truy cập trang này.',
      }),
    ).toBeInTheDocument()
    await userEvent
      .setup()
      .click(screen.getByRole('link', { name: 'Quay lại' }))
    expect(
      await screen.findByRole('heading', { name: 'Không gian sinh viên.' }),
    ).toBeInTheDocument()
  })

  it.each([
    ['STUDENT', '/lecturer'],
    ['LECTURER', '/admin'],
    ['LECTURER', '/student'],
  ])('blocks %s from %s', async (role, path) => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          json({ id: 'user-1', email: 'user@example.com', role }),
        ),
    )
    renderApp(path)
    expect(
      await screen.findByRole('heading', {
        name: 'Bạn không có quyền truy cập trang này.',
      }),
    ).toBeInTheDocument()
  })

  it('lets admin list accounts and create only student or lecturer accounts', async () => {
    const fetchMock = vi.fn((path) => {
      if (path === '/api/v1/auth/me') {
        return Promise.resolve(
          json({ id: 'admin-1', email: 'admin@example.com', role: 'ADMIN' }),
        )
      }
      if (path === '/api/v1/admin/users') {
        return Promise.resolve(
          json([{ id: 'admin-1', email: 'admin@example.com', role: 'ADMIN' }]),
        )
      }
      if (path === '/api/v1/auth/csrf') {
        return Promise.resolve(json({ token: 'csrf-create' }))
      }
      throw new Error(`Unexpected request: ${path}`)
    })
    fetchMock.mockImplementationOnce(() =>
      Promise.resolve(
        json({ id: 'admin-1', email: 'admin@example.com', role: 'ADMIN' }),
      ),
    )
    vi.stubGlobal('fetch', fetchMock)
    renderApp('/admin')

    expect(
      await screen.findByText('admin@example.com', { selector: 'strong' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('option', { name: 'Quản trị viên' }),
    ).not.toBeInTheDocument()
    await userEvent
      .setup()
      .type(screen.getByLabelText('Email'), 'new@example.com')
    await userEvent
      .setup()
      .type(screen.getByLabelText('Mật khẩu ban đầu'), 'student-password')
    await userEvent
      .setup()
      .selectOptions(screen.getByLabelText('Vai trò'), 'STUDENT')

    fetchMock.mockImplementationOnce(() =>
      Promise.resolve(json({ token: 'csrf-create' })),
    )
    fetchMock.mockImplementationOnce(() =>
      Promise.resolve(
        json(
          { id: 'student-1', email: 'new@example.com', role: 'STUDENT' },
          201,
        ),
      ),
    )
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Tạo tài khoản' }))

    expect(await screen.findByRole('status', { name: '' })).toHaveTextContent(
      'Đã tạo tài khoản new@example.com.',
    )
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/admin/users',
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        body: JSON.stringify({
          email: 'new@example.com',
          password: 'student-password',
          role: 'STUDENT',
        }),
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-create' }),
      }),
    )
  })

  it.each([204, 401])(
    'returns to login after logout responds with %i',
    async (logoutStatus) => {
      const fetchMock = vi.fn((path) => {
        if (path === '/api/v1/auth/me') {
          return Promise.resolve(
            json({ id: '2', email: 'student@example.com', role: 'STUDENT' }),
          )
        }
        if (path === '/api/v1/auth/csrf')
          return Promise.resolve(json({ token: 'csrf-logout' }))
        if (path === '/api/v1/auth/logout')
          return Promise.resolve(new Response(null, { status: logoutStatus }))
        throw new Error(`Unexpected request: ${path}`)
      })
      vi.stubGlobal('fetch', fetchMock)
      renderApp('/student')
      expect(
        await screen.findByRole('heading', { name: 'Không gian sinh viên.' }),
      ).toBeInTheDocument()
      await userEvent
        .setup()
        .click(screen.getByRole('button', { name: 'Đăng xuất' }))
      expect(
        await screen.findByRole('heading', { name: 'Đăng nhập.' }),
      ).toBeInTheDocument()
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/v1/auth/logout',
        expect.objectContaining({
          credentials: 'include',
          method: 'POST',
          headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-logout' }),
        }),
      )
    },
  )
})
