import { Route, Routes } from 'react-router'
import AdminPage from './features/admin/AdminPage.jsx'
import { AuthProvider } from './features/auth/AuthProvider.jsx'
import LoginPage from './features/auth/LoginPage.jsx'
import RequireRole, { RoleHomeRedirect } from './features/auth/RequireRole.jsx'
import ForbiddenPage from './pages/ForbiddenPage.jsx'
import HomePage from './pages/HomePage.jsx'
import NotFoundPage from './pages/NotFoundPage.jsx'
import RoleDashboard from './pages/RoleDashboard.jsx'
import QuestionBankPage from './features/question-bank/QuestionBankPage.jsx'
import ExamSessionPage from './features/exam-session/ExamSessionPage.jsx'
import Wf1PreparationPage from './features/wf1-ai/Wf1PreparationPage.jsx'
import InterviewPage from './features/interview/InterviewPage.jsx'
import ReportsPage from './features/interview/ReportsPage.jsx'

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/" element={<RoleHomeRedirect />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/about" element={<HomePage />} />
        <Route path="/forbidden" element={<ForbiddenPage />} />
        <Route
          path="/interviews/:attemptId"
          element={
            <RequireRole allowed={['STUDENT']}>
              <InterviewPage />
            </RequireRole>
          }
        />
        <Route
          path="/reports"
          element={
            <RequireRole allowed={['ADMIN', 'LECTURER', 'STUDENT']}>
              <ReportsPage />
            </RequireRole>
          }
        />
        <Route
          path="/reports/:attemptId"
          element={
            <RequireRole allowed={['ADMIN', 'LECTURER', 'STUDENT']}>
              <ReportsPage />
            </RequireRole>
          }
        />
        <Route
          path="/exam-sessions"
          element={
            <RequireRole allowed={['LECTURER', 'ADMIN', 'STUDENT']}>
              <ExamSessionPage />
            </RequireRole>
          }
        />
        <Route
          path="/wf1-preparation"
          element={
            <RequireRole allowed={['LECTURER', 'ADMIN']}>
              <Wf1PreparationPage />
            </RequireRole>
          }
        />
        <Route
          path="/question-banks"
          element={
            <RequireRole allowed={['LECTURER', 'ADMIN']}>
              <QuestionBankPage />
            </RequireRole>
          }
        />
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
