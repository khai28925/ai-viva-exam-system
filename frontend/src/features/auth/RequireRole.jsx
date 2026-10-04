import { Navigate, useLocation } from 'react-router'
import { useAuth } from './authContext.js'
import { ROLE_HOME } from './roles.js'

export function SessionStatus() {
  const { status, refresh } = useAuth()

  if (status === 'loading') {
    return (
      <main className="auth-status" aria-live="polite">
        <div className="auth-status__spinner" />
        <p>Đang kiểm tra phiên đăng nhập...</p>
      </main>
    )
  }

  return (
    <main className="auth-status" role="alert">
      <h1>Không thể kết nối tới máy chủ</h1>
      <p>Chưa thể kiểm tra quyền truy cập. Vui lòng thử lại.</p>
      <button type="button" onClick={() => refresh()}>
        Thử lại
      </button>
    </main>
  )
}

export function RoleHomeRedirect() {
  const { status, user } = useAuth()

  if (status === 'loading' || status === 'error') return <SessionStatus />
  if (status === 'anonymous') return <Navigate to="/login" replace />
  return <Navigate to={ROLE_HOME[user.role] ?? '/forbidden'} replace />
}

export default function RequireRole({ allowed, children }) {
  const { status, user } = useAuth()
  const location = useLocation()

  if (status === 'loading' || status === 'error') return <SessionStatus />
  if (status === 'anonymous') {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }
  if (!allowed.includes(user.role)) return <Navigate to="/forbidden" replace />
  return children
}
