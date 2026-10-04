import { Route, Routes } from 'react-router'
import AdminPage from './features/admin/AdminPage.jsx'
import { AuthProvider } from './features/auth/AuthProvider.jsx'
import LoginPage from './features/auth/LoginPage.jsx'
import RequireRole, { RoleHomeRedirect } from './features/auth/RequireRole.jsx'
import ForbiddenPage from './pages/ForbiddenPage.jsx'
import HomePage from './pages/HomePage.jsx'
import NotFoundPage from './pages/NotFoundPage.jsx'
import RoleDashboard from './pages/RoleDashboard.jsx'

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/" element={<RoleHomeRedirect />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/about" element={<HomePage />} />
        <Route path="/forbidden" element={<ForbiddenPage />} />
        <Route
          path="/admin"
          element={
            <RequireRole allowed={['ADMIN']}>
              <AdminPage />
            </RequireRole>
          }
        />
        <Route
          path="/lecturer"
          element={
            <RequireRole allowed={['LECTURER', 'ADMIN']}>
              <RoleDashboard role="LECTURER" />
            </RequireRole>
          }
        />
        <Route
          path="/student"
          element={
            <RequireRole allowed={['STUDENT']}>
              <RoleDashboard role="STUDENT" />
            </RequireRole>
          }
        />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </AuthProvider>
  )
}
