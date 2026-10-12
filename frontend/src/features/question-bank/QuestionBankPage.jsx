import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import * as api from '../../api/questionBankApi.js'
import { useAuth } from '../auth/authContext.js'
import { ROLE_HOME } from '../auth/roles.js'
import QuestionBankWorkspace from './QuestionBankWorkspace.jsx'

export default function QuestionBankPage() {
  const { user, invalidate, logout } = useAuth()
  const navigate = useNavigate()
  const [data, setData] = useState({ banks: [], questions: [] })
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [revision, setRevision] = useState(0)
  const [pending, setPending] = useState(false)
  const [loggingOut, setLoggingOut] = useState(false)
  const [sessionError, setSessionError] = useState('')
  const requests = useRef(null)
  const mutationPending = useRef(false)
  const logoutPending = useRef(false)

  const handleAccessError = useCallback(
    (error) => {
      if (error.status === 401) invalidate()
      if (error.status === 403) navigate('/forbidden', { replace: true })
    },
    [invalidate, navigate],
  )

  useEffect(() => {
    const controller = new AbortController()
    requests.current = controller
    api
      .getQuestionBankWorkspace(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((error) => {
        if (controller.signal.aborted) return
        handleAccessError(error)
        setLoadError(
          error.status === 404
            ? 'Một ngân hàng vừa bị xóa. Hãy tải lại danh sách.'
            : 'Không thể kết nối hoặc tải đầy đủ dữ liệu từ API. Vui lòng thử lại.',
        )
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [revision, handleAccessError])

  function reload() {
    if (mutationPending.current || logoutPending.current) return
    setLoading(true)
    setLoadError('')
    setRevision((current) => current + 1)
  }

  async function mutate(operation, update) {
    if (mutationPending.current || logoutPending.current)
      throw new Error('Một thao tác đang được xử lý.')
    const signal = requests.current.signal
    mutationPending.current = true
    setPending(true)
    try {
      const result = await operation(signal)
      if (!signal.aborted) setData((current) => update(current, result))
      return result
    } catch (error) {
      if (!signal.aborted) handleAccessError(error)
      throw error
    } finally {
      mutationPending.current = false
      if (!signal.aborted) setPending(false)
    }
  }

  async function handleLogout() {
    if (mutationPending.current || logoutPending.current) return
    logoutPending.current = true
    setLoggingOut(true)
    setSessionError('')
    try {
      await logout()
    } catch {
      setSessionError('Không thể đăng xuất. Vui lòng thử lại.')
    } finally {
      logoutPending.current = false
      setLoggingOut(false)
    }
  }

  return (
    <QuestionBankWorkspace
      {...data}
      isLoading={loading}
      error={loadError}
      onRetry={reload}
      dataSourceLabel="Dữ liệu thật · PostgreSQL"
      accountLabel={user.role === 'ADMIN' ? 'Quản trị viên' : 'Giảng viên'}
      accountDetail={user.email}
      onLogout={handleLogout}
      loggingOut={loggingOut}
      logoutDisabled={pending}
      sessionError={sessionError}
      navigation={
        <>
          <Link
            className="qb-button qb-button--ghost"
            to={ROLE_HOME[user.role]}
          >
            Về không gian làm việc
          </Link>
          <button
            className="qb-button qb-button--ghost"
            type="button"
            onClick={reload}
            disabled={loading || pending || loggingOut}
          >
            Làm mới dữ liệu
          </button>
        </>
      }
      onCreateBank={(request) =>
        mutate(
          (signal) => api.createQuestionBank(request, signal),
          (current, bank) => ({ ...current, banks: [...current.banks, bank] }),
        )
      }
      onUpdateBank={(bankId, request) =>
        mutate(
          (signal) => api.updateQuestionBank(bankId, request, signal),
          (current, bank) => ({
            ...current,
            banks: current.banks.map((item) =>
              item.id === bankId ? bank : item,
            ),
          }),
        )
      }
      onDeleteBank={(bankId) =>
        mutate(
          (signal) => api.deleteQuestionBank(bankId, signal),
          (current) => ({
            banks: current.banks.filter((item) => item.id !== bankId),
            questions: current.questions.filter(
              (item) => item.questionBankId !== bankId,
            ),
          }),
        )
      }
      onCreateQuestion={(bankId, request) =>
        mutate(
          (signal) => api.createQuestion(bankId, request, signal),
          (current, question) => ({
            ...current,
            questions: [...current.questions, question],
          }),
        )
      }
      onUpdateQuestion={(bankId, questionId, request) =>
        mutate(
          (signal) => api.updateQuestion(bankId, questionId, request, signal),
          (current, question) => ({
            ...current,
            questions: current.questions.map((item) =>
              item.id === questionId ? question : item,
            ),
          }),
        )
      }
      onDeleteQuestion={(bankId, questionId) =>
        mutate(
          (signal) => api.deleteQuestion(bankId, questionId, signal),
          (current) => ({
            ...current,
            questions: current.questions.filter(
              (item) => item.id !== questionId,
            ),
          }),
        )
      }
    />
  )
}
