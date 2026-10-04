import { useState } from 'react'
import { Link, Navigate } from 'react-router'
import { useAuth } from './authContext.js'
import { SessionStatus } from './RequireRole.jsx'
import { ROLE_HOME } from './roles.js'
import './auth.css'

export default function LoginPage() {
  const { status, user, login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  if (status === 'loading' || status === 'error') return <SessionStatus />
  if (status === 'authenticated') {
    return <Navigate to={ROLE_HOME[user.role] ?? '/forbidden'} replace />
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setError('')
    setPending(true)
    try {
      await login({ email: email.trim(), password })
    } catch (requestError) {
      setError(
        requestError.status === 401
          ? 'Email hoặc mật khẩu không đúng.'
          : requestError.status === 429
            ? 'Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau.'
            : (requestError.problem?.detail ??
              'Không thể đăng nhập lúc này. Vui lòng thử lại.'),
      )
    } finally {
      setPending(false)
    }
  }

  return (
    <main className="auth-login">
      <section className="auth-login__intro" aria-label="Giới thiệu AIVES">
        <Link className="auth-logo" to="/">
          <span className="auth-logo__mark">A</span>
          <span>
            AIVES<span className="auth-logo__dot">.</span>
          </span>
        </Link>
        <div className="auth-login__statement">
          <p className="auth-kicker">AI-POWERED VIVA EXAM SYSTEM</p>
          <h1>
            Một không gian.
            <br />
            Ba vai trò.
            <br />
            <em>Chung một mục tiêu.</em>
          </h1>
          <p>
            Quản lý ngân hàng câu hỏi, tổ chức kỳ thi vấn đáp và theo dõi kết
            quả trong một nền tảng thống nhất.
          </p>
        </div>
        <div className="auth-login__roles" aria-label="Các vai trò sử dụng">
          <span>ADMIN</span>
          <span>LECTURER</span>
          <span>STUDENT</span>
        </div>
      </section>

      <section className="auth-login__form-area">
        <div className="auth-login__form-wrap">
          <p className="auth-kicker">CHÀO MỪNG TRỞ LẠI</p>
          <h2>
            Đăng nhập<span>.</span>
          </h2>
          <p className="auth-login__hint">
            Dùng tài khoản được quản trị viên cấp để truy cập không gian của
            bạn.
          </p>

          <form onSubmit={handleSubmit}>
            <label htmlFor="auth-email">Email</label>
            <input
              id="auth-email"
              type="email"
              autoComplete="username"
              required
              maxLength={254}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="name@example.com"
            />
            <label htmlFor="auth-password">Mật khẩu</label>
            <input
              id="auth-password"
              type="password"
              autoComplete="current-password"
              required
              maxLength={128}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Nhập mật khẩu"
            />
            {error && (
              <p className="auth-form-error" role="alert">
                {error}
              </p>
            )}
            <button type="submit" disabled={pending}>
              {pending ? 'Đang đăng nhập...' : 'Đăng nhập'}
              <span aria-hidden="true">↗</span>
            </button>
          </form>
          <p className="auth-login__support">
            Chưa có tài khoản? Liên hệ quản trị viên để được cấp quyền truy cập.
          </p>
        </div>
        <p className="auth-login__footer">
          © 2026 AIVES · AI-powered Viva Exam System
        </p>
      </section>
    </main>
  )
}
