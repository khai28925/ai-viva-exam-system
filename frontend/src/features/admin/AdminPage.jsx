import { useEffect, useState } from 'react'
import { createUser, getUsers } from './adminApi.js'
import { useAuth } from '../auth/authContext.js'
import SessionLayout from '../auth/SessionLayout.jsx'

const roleLabels = {
  ADMIN: 'Quản trị viên',
  LECTURER: 'Giảng viên',
  STUDENT: 'Sinh viên',
}

export default function AdminPage() {
  const { invalidate } = useAuth()
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')
  const [pending, setPending] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('STUDENT')

  useEffect(() => {
    const controller = new AbortController()
    getUsers(controller.signal)
      .then((accounts) => {
        if (!controller.signal.aborted) setUsers(accounts)
      })
      .catch((error) => {
        if (controller.signal.aborted || error.name === 'AbortError') return
        if (error.status === 401) invalidate()
        else setLoadError('Không thể tải danh sách tài khoản.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [invalidate])

  async function handleCreate(event) {
    event.preventDefault()
    setFormError('')
    setSuccess('')
    setPending(true)
    try {
      const created = await createUser({ email: email.trim(), password, role })
      setUsers((current) => [...current, created])
      setEmail('')
      setPassword('')
      setRole('STUDENT')
      setSuccess(`Đã tạo tài khoản ${created.email}.`)
    } catch (error) {
      if (error.status === 401) invalidate()
      else {
        setFormError(
          error.problem?.detail ??
            (error.status === 409
              ? 'Email này đã được sử dụng.'
              : 'Không thể tạo tài khoản. Vui lòng thử lại.'),
        )
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <SessionLayout
      title="Quản lý tài khoản"
      eyebrow="ADMIN WORKSPACE / USERS"
      description="Cấp tài khoản giảng viên và sinh viên cho hệ thống."
    >
      <div className="auth-admin-grid">
        <section className="auth-card auth-admin-form">
          <span>01 / TẠO TÀI KHOẢN</span>
          <h2>Thành viên mới</h2>
          <p>
            Cấp tài khoản cho giảng viên hoặc sinh viên với vai trò phù hợp.
          </p>
          <form onSubmit={handleCreate}>
            <label htmlFor="admin-email">Email</label>
            <input
              id="admin-email"
              type="email"
              autoComplete="off"
              required
              maxLength={254}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="name@example.com"
            />
            <label htmlFor="admin-password">Mật khẩu ban đầu</label>
            <input
              id="admin-password"
              type="password"
              autoComplete="new-password"
              required
              minLength={12}
              maxLength={128}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Nhập mật khẩu"
            />
            <label htmlFor="admin-role">Vai trò</label>
            <select
              id="admin-role"
              value={role}
              onChange={(event) => setRole(event.target.value)}
            >
              <option value="STUDENT">Sinh viên</option>
              <option value="LECTURER">Giảng viên</option>
            </select>
            {formError && (
              <p className="auth-form-error" role="alert">
                {formError}
              </p>
            )}
            {success && (
              <p className="auth-form-success" role="status">
                {success}
              </p>
            )}
            <button type="submit" disabled={pending}>
              {pending ? 'Đang tạo...' : 'Tạo tài khoản'}
            </button>
          </form>
        </section>

        <section className="auth-card auth-admin-list">
          <span>02 / DANH SÁCH</span>
          <div className="auth-admin-list__title">
            <h2>Tài khoản hiện có</h2>
            <strong>{users.length}</strong>
          </div>
          {loading ? (
            <p role="status">Đang tải tài khoản...</p>
          ) : loadError ? (
            <p role="alert">{loadError}</p>
          ) : users.length === 0 ? (
            <p>Chưa có tài khoản nào.</p>
          ) : (
            <ul>
              {users.map((account) => (
                <li key={account.id}>
                  <span className="auth-admin-list__avatar" aria-hidden="true">
                    {account.email.charAt(0).toUpperCase()}
                  </span>
                  <span className="auth-admin-list__identity">
                    <strong>{account.email}</strong>
                    <small>{roleLabels[account.role] ?? account.role}</small>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </SessionLayout>
  )
}
