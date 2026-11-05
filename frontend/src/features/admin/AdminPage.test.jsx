import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from './adminApi.js'
import { AuthContext } from '../auth/authContext.js'
import AdminPage from './AdminPage.jsx'

vi.mock('./adminApi.js', () => ({
  getUsers: vi.fn(),
  createUser: vi.fn(),
  updateUserProfile: vi.fn(),
}))

function renderPage() {
  render(
    <AuthContext.Provider
      value={{
        user: { role: 'ADMIN', email: 'admin@local' },
        logout: vi.fn(),
        invalidate: vi.fn(),
      }}
    >
      <MemoryRouter>
        <AdminPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('admin accounts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.getUsers.mockResolvedValue([
      {
        id: 'student-1',
        email: 'an@fpt.edu.vn',
        role: 'STUDENT',
        fullName: null,
        studentCode: null,
      },
    ])
  })

  it('creates a student with the full name and student code printed on grade sheets', async () => {
    api.createUser.mockResolvedValue({
      id: 'student-2',
      email: 'binh@fpt.edu.vn',
      role: 'STUDENT',
      fullName: 'Trần Bình',
      studentCode: 'SE180002',
    })
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('an@fpt.edu.vn', { selector: 'strong' })
    await user.type(screen.getByLabelText('Email'), 'binh@fpt.edu.vn')
    await user.type(
      screen.getByLabelText('Mật khẩu ban đầu'),
      'student-password',
    )
    await user.type(screen.getByLabelText('Họ và tên'), ' Trần Bình ')
    await user.type(screen.getByLabelText('MSSV'), 'se180002')
    await user.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))

    await waitFor(() =>
      expect(api.createUser).toHaveBeenCalledWith({
        email: 'binh@fpt.edu.vn',
        password: 'student-password',
        role: 'STUDENT',
        fullName: 'Trần Bình',
        studentCode: 'se180002',
      }),
    )
    expect(await screen.findByText('Trần Bình')).toBeInTheDocument()
    expect(screen.getByText(/MSSV SE180002/)).toBeInTheDocument()
  })

  it('edits the profile of an existing account', async () => {
    api.updateUserProfile.mockResolvedValue({
      id: 'student-1',
      email: 'an@fpt.edu.vn',
      role: 'STUDENT',
      fullName: 'Nguyễn Văn An',
      studentCode: 'SE180001',
    })
    const user = userEvent.setup()
    renderPage()
    await user.click(
      await screen.findByRole('button', { name: 'Sửa hồ sơ an@fpt.edu.vn' }),
    )
    const names = screen.getAllByLabelText('Họ và tên')
    await user.type(names[names.length - 1], 'Nguyễn Văn An')
    const codes = screen.getAllByLabelText('MSSV')
    await user.type(codes[codes.length - 1], 'SE180001')
    await user.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))

    await waitFor(() =>
      expect(api.updateUserProfile).toHaveBeenCalledWith('student-1', {
        fullName: 'Nguyễn Văn An',
        studentCode: 'SE180001',
      }),
    )
    expect(
      await screen.findByText('Đã cập nhật hồ sơ an@fpt.edu.vn.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Nguyễn Văn An')).toBeInTheDocument()
  })
})
