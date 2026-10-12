import { useEffect, useRef, useState } from 'react'
import { Link, Navigate, useLocation } from 'react-router'
import { useAuth } from './authContext.js'
import { SessionStatus } from './RequireRole.jsx'
import { ROLE_HOME } from './roles.js'
import './auth.css'

export default function LoginPage() {
  const { status, user, login } = useAuth()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [retryUntil, setRetryUntil] = useState(null)
  const [retrySeconds, setRetrySeconds] = useState(0)
  const submitting = useRef(false)

  useEffect(() => {
    if (retryUntil === null) return
    const timer = window.setInterval(() => {
      const remaining = Math.max(0, Math.ceil((retryUntil - Date.now()) / 1000))
      setRetrySeconds(remaining)
      if (remaining === 0) setRetryUntil(null)
    }, 250)
    return () => window.clearInterval(timer)
  }, [retryUntil])

  if (status === 'loading' || status === 'error') return <SessionStatus />
  if (status === 'authenticated') {
    const returnToQuestionBanks =
      location.state?.from === '/question-banks' &&
      ['ADMIN', 'LECTURER'].includes(user.role)
    return (
      <Navigate
        to={
          returnToQuestionBanks
            ? '/question-banks'
            : (ROLE_HOME[user.role] ?? '/forbidden')
        }
        replace
      />
    )
  }

  async function handleSubmit(event) {
    event.preventDefault()
    if (submitting.current || (retryUntil !== null && Date.now() < retryUntil))
      return
    submitting.current = true
    setError('')
    setPending(true)
    try {
      await login({ email: email.trim(), password })
    } catch (requestError) {
      if (requestError.status === 429) {
        const suggestedWait = Number(requestError.problem?.retryAfterSeconds)
        const seconds =
          Number.isFinite(suggestedWait) && suggestedWait > 0
            ? Math.ceil(Math.min(suggestedWait, 3600))
            : 60
        setRetryUntil(Date.now() + seconds * 1000)
        setRetrySeconds(seconds)
        setError('Đã hết thời gian chờ. Bạn có thể thử đăng nhập lại.')
      } else {
        setError(
          requestError.status === 401
            ? 'Email hoặc mật khẩu không đúng.'
            : (requestError.problem?.detail ??
                'Không thể đăng nhập lúc này. Vui lòng thử lại.'),
        )
      }
    } finally {
      submitting.current = false
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
                {retrySeconds > 0
                  ? `Bạn đã thử đăng nhập quá nhiều lần. Vui lòng chờ ${retrySeconds} giây rồi thử lại.`
                  : error}
              </p>
            )}
            <button type="submit" disabled={pending || retrySeconds > 0}>
              {pending
                ? 'Đang đăng nhập...'
                : retrySeconds > 0
                  ? `Thử lại sau ${retrySeconds}s`
                  : 'Đăng nhập'}
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
