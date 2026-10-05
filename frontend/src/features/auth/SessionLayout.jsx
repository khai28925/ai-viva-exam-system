import { useState } from 'react'
import { Link } from 'react-router'
import { useAuth } from './authContext.js'
import './auth.css'

const roleLabels = {
  ADMIN: 'Quản trị viên',
  LECTURER: 'Giảng viên',
  STUDENT: 'Sinh viên',
}

export default function SessionLayout({
  title,
  eyebrow,
  description,
  children,
}) {
  const { user, logout } = useAuth()
  const [loggingOut, setLoggingOut] = useState(false)
  const [error, setError] = useState('')

  async function handleLogout() {
    setError('')
    setLoggingOut(true)
    try {
      await logout()
    } catch {
      setError('Không thể đăng xuất. Vui lòng thử lại.')
    } finally {
      setLoggingOut(false)
    }
  }

  return (
    <main className="auth-workspace">
      <header className="auth-workspace__header">
        <Link className="auth-logo auth-logo--dark" to="/">
          <span className="auth-logo__mark">A</span>
          <span>
            AIVES<span className="auth-logo__dot">.</span>
          </span>
        </Link>
        <div className="auth-workspace__identity">
          {['ADMIN', 'LECTURER'].includes(user.role) && (
            <Link to="/question-banks">Ngân hàng câu hỏi</Link>
          )}
          <span className="auth-workspace__role">
            {roleLabels[user.role] ?? user.role}
          </span>
          <span className="auth-workspace__email">{user.email}</span>
          <button type="button" onClick={handleLogout} disabled={loggingOut}>
            {loggingOut ? 'Đang đăng xuất...' : 'Đăng xuất'}
          </button>
        </div>
      </header>
      {error && (
        <p className="auth-workspace__error" role="alert">
          {error}
        </p>
      )}
      <div className="auth-workspace__body">
        <div className="auth-workspace__heading">
          <p className="auth-kicker">{eyebrow}</p>
          <h1>
            {title}
            <span>.</span>
          </h1>
          <p>{description}</p>
        </div>
        {children}
      </div>
    </main>
  )
}
