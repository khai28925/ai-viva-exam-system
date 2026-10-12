import { Link } from 'react-router'
import { useAuth } from '../features/auth/authContext.js'
import { SessionStatus } from '../features/auth/RequireRole.jsx'
import { ROLE_HOME } from '../features/auth/roles.js'
import '../features/auth/auth.css'

export default function ForbiddenPage() {
  const { status, user } = useAuth()
  if (status === 'loading' || status === 'error') return <SessionStatus />

  return (
    <main className="auth-denied">
      <span>403 / ACCESS DENIED</span>
      <h1>Bạn không có quyền truy cập trang này.</h1>
      <p>Hãy trở về không gian phù hợp với vai trò của tài khoản.</p>
      <Link
        to={
          status === 'authenticated' ? (ROLE_HOME[user.role] ?? '/') : '/login'
        }
      >
        Quay lại
      </Link>
    </main>
  )
}
