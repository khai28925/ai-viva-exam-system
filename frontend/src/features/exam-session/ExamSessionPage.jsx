import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import * as api from '../../api/examSessionApi.js'
import { getQuestionBanks } from '../../api/questionBankApi.js'
import { useAuth } from '../auth/authContext.js'
import SessionLayout from '../auth/SessionLayout.jsx'
import SessionPreparationPanel from './SessionPreparationPanel.jsx'
import './examSession.css'

const statusLabels = { Draft: 'Bản nháp', Open: 'Đang mở', Closed: 'Đã đóng' }

function errorMessage(error, action) {
  if (error.status === 409) {
    if (action === 'delete')
      return 'Chỉ có thể xóa phiên Draft chưa có sinh viên tham gia.'
    if (action === 'join')
      return (
        error.problem?.detail ||
        'Phiên không còn mở hoặc chưa được chuẩn bị. Hãy tải lại danh sách.'
      )
    return (
      error.problem?.detail ||
      'Không thể lưu thay đổi. Hãy tải lại để kiểm tra.'
    )
  }
  if (error.status === 404)
    return 'Phiên thi hoặc ngân hàng không còn tồn tại. Hãy tải lại danh sách.'
  if (error.status === 400) {
    const errors = Object.values(error.problem?.errors ?? {})
      .flat()
      .join(' ')
    return (
      errors ||
      error.problem?.detail ||
      'Thông tin chưa hợp lệ. Vui lòng kiểm tra lại.'
    )
  }
  return 'Không thể xử lý yêu cầu lúc này. Vui lòng thử lại.'
}

function SessionForm({ session, banks, pending, error, onSave, onCancel }) {
  const [title, setTitle] = useState(session?.title ?? '')
  const [bankId, setBankId] = useState(
    session?.questionBankId ?? banks[0]?.id ?? '',
  )
  const [validation, setValidation] = useState('')

  function submit(event) {
    event.preventDefault()
    if (pending) return
    if (!title.trim() || title.trim().length > 120) {
      setValidation('Tên phiên thi cần từ 1 đến 120 ký tự.')
      return
    }
    if (!bankId) {
      setValidation('Hãy chọn ngân hàng câu hỏi.')
      return
    }
    setValidation('')
    onSave({ title: title.trim(), questionBankId: bankId, status: 'Draft' })
  }

  return (
    <section className="auth-card exam-form" aria-labelledby="exam-form-title">
      <h2 id="exam-form-title">
        {session ? 'Sửa phiên thi' : 'Tạo phiên thi'}
      </h2>
      <form onSubmit={submit} noValidate>
        <fieldset disabled={pending}>
          <label htmlFor="exam-title">Tên phiên thi</label>
          <input
            id="exam-title"
            value={title}
            maxLength={120}
            required
            onChange={(event) => setTitle(event.target.value)}
          />
          <label htmlFor="exam-bank">Ngân hàng câu hỏi</label>
          <select
            id="exam-bank"
            value={bankId}
            required
            onChange={(event) => setBankId(event.target.value)}
          >
            <option value="">Chọn ngân hàng</option>
            {banks.map((bank) => (
              <option key={bank.id} value={bank.id}>
                {bank.name}
              </option>
            ))}
          </select>
          <p>
            Phiên mới bắt đầu ở trạng thái Draft. Hãy chọn câu hỏi, thêm ý chính
            và rubric rồi mở phiên. Không thể đổi ngân hàng khi đã chọn câu hỏi.
          </p>
          {(validation || error) && (
            <p className="auth-form-error" role="alert">
              {validation || error}
            </p>
          )}
          <div className="exam-actions">
            <button className="exam-button exam-button--primary" type="submit">
              {pending ? 'Đang lưu...' : 'Lưu phiên thi'}
            </button>
            <button className="exam-button" type="button" onClick={onCancel}>
              Hủy
            </button>
          </div>
        </fieldset>
      </form>
    </section>
  )
}

function DeleteConfirmation({ session, pending, error, onConfirm, onCancel }) {
  const dialog = useRef(null)
  useEffect(() => {
    const element = dialog.current
    const previousFocus = document.activeElement
    element.showModal()
    return () => {
      element.close()
      previousFocus?.focus()
    }
  }, [])

  return (
    <dialog
      ref={dialog}
      className="exam-dialog"
      aria-labelledby="exam-delete-title"
      onCancel={(event) => {
        event.preventDefault()
        if (!pending) onCancel()
      }}
    >
      <h2 id="exam-delete-title">Xóa phiên thi?</h2>
      <p>Bạn muốn xóa “{session.title}”? Thao tác này không thể hoàn tác.</p>
      <p>Chỉ phiên Draft chưa có sinh viên tham gia mới được xóa.</p>
      {error && (
        <p role="alert" className="auth-form-error">
          {error}
        </p>
      )}
      <div className="exam-actions">
        <button
          className="exam-button"
          type="button"
          disabled={pending}
          onClick={onCancel}
        >
          Giữ lại
        </button>
        <button
          className="exam-button exam-button--danger"
          type="button"
          disabled={pending}
          onClick={onConfirm}
        >
          {pending ? 'Đang xóa...' : 'Xác nhận xóa'}
        </button>
      </div>
    </dialog>
  )
}

export default function ExamSessionPage() {
  const { user, invalidate } = useAuth()
  const navigate = useNavigate()
  const teacher = ['ADMIN', 'LECTURER'].includes(user.role)
  const [sessions, setSessions] = useState([])
  const [banks, setBanks] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [actionError, setActionError] = useState('')
  const [notice, setNotice] = useState('')
  const [pending, setPending] = useState(false)
  const [revision, setRevision] = useState(0)
  const [form, setForm] = useState(null)
  const [deleting, setDeleting] = useState(null)
  const [attempt, setAttempt] = useState(null)
  const [preparingId, setPreparingId] = useState(null)
  const requests = useRef(null)
  const busy = useRef(false)

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
    Promise.all([
      api.getExamSessions(controller.signal),
      teacher ? getQuestionBanks(controller.signal) : Promise.resolve([]),
    ])
      .then(([items, bankItems]) => {
        if (controller.signal.aborted) return
        setSessions(items)
        setBanks(bankItems)
      })
      .catch((error) => {
        if (controller.signal.aborted) return
        handleAccessError(error)
        setLoadError('Không thể tải phiên thi. Vui lòng thử lại.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [revision, teacher, handleAccessError])

  function reload() {
    if (busy.current) return
    setForm(null)
    setDeleting(null)
    setAttempt(null)
    setPreparingId(null)
    setActionError('')
    setLoadError('')
    setNotice('')
    setLoading(true)
    setRevision((value) => value + 1)
  }

  async function mutate(action, operation, onSuccess) {
    if (busy.current) return
    busy.current = true
    setPending(true)
    setActionError('')
    setNotice('')
    const signal = requests.current.signal
    try {
      const result = await operation(signal)
      if (!signal.aborted) onSuccess(result)
    } catch (error) {
      if (!signal.aborted) {
        handleAccessError(error)
        setActionError(errorMessage(error, action))
      }
    } finally {
      busy.current = false
      if (!signal.aborted) setPending(false)
    }
  }

  function save(values) {
    mutate(
      'save',
      (signal) =>
        form.session
          ? api.updateExamSession(form.session.id, values, signal)
          : api.createExamSession(values, signal),
      (result) => {
        setSessions((current) =>
          form.session
            ? current.map((item) => (item.id === result.id ? result : item))
            : [...current, result],
        )
        setForm(null)
        setNotice('Đã lưu phiên thi.')
      },
    )
  }

  return (
    <SessionLayout
      title={teacher ? 'Quản lý phiên thi' : 'Phiên thi của bạn'}
      eyebrow="EXAM SESSIONS / BẢN CƠ BẢN"
      description={
        teacher
          ? 'Tạo phiên Draft, chọn câu hỏi và kiểm tra điều kiện trước khi mở cho sinh viên.'
          : 'Chọn một phiên đang mở để vào xem câu hỏi.'
      }
      busy={pending}
    >
      <p className="exam-scope">
        Bản cơ bản: chưa nộp bài/chấm điểm, chưa có lịch thi, AI hoặc ghi âm.
        Việc vào thi được lưu vào hệ thống.
      </p>
      {attempt ? (
        <section
          className="auth-card exam-attempt"
          aria-labelledby="exam-attempt-title"
        >
          <span>ĐÃ VÀO PHIÊN THI</span>
          <h2 id="exam-attempt-title">{attempt.title}</h2>
          <p>
            Thời điểm tham gia:{' '}
            {new Date(attempt.startedAt).toLocaleString('vi-VN')}
          </p>
          <p>
            Câu hỏi chỉ để đọc trong bản demo này; chưa có ô trả lời hoặc nút
            nộp bài.
          </p>
          <ol className="exam-questions">
            {attempt.questions.map((question) => (
              <li key={question.id}>{question.content}</li>
            ))}
          </ol>
          {attempt.questions.length === 0 && (
            <p>Phiên chưa có câu hỏi để hiển thị.</p>
          )}
          <button className="exam-button" type="button" onClick={reload}>
            Về danh sách phiên thi
          </button>
        </section>
      ) : (
        <>
          <div className="exam-toolbar">
            <h2>{teacher ? 'Danh sách phiên thi' : 'Phiên đang mở'}</h2>
            <div className="exam-actions">
              <button
                className="exam-button"
                type="button"
                onClick={reload}
                disabled={loading || pending}
              >
                Tải lại
              </button>
              {teacher && (
                <button
                  className="exam-button exam-button--primary"
                  type="button"
                  disabled={loading || pending || !!loadError || !!form}
                  onClick={() => {
                    setForm({ session: null })
                    setActionError('')
                    setNotice('')
                  }}
                >
                  Tạo phiên thi
                </button>
              )}
            </div>
          </div>
          {notice && (
            <p className="auth-form-success" role="status">
              {notice}
            </p>
          )}
          {actionError && !form && !deleting && (
            <p className="auth-form-error" role="alert">
              {actionError}
            </p>
          )}
          {loading ? (
            <p role="status">Đang tải phiên thi...</p>
          ) : loadError ? (
            <div className="auth-form-error" role="alert">
              <p>{loadError}</p>
              <button className="exam-button" type="button" onClick={reload}>
                Thử lại
              </button>
            </div>
          ) : (
            <>
              {form && (
                <SessionForm
                  key={form.session?.id ?? 'new'}
                  session={form.session}
                  banks={banks}
                  pending={pending}
                  error={actionError}
                  onSave={save}
                  onCancel={() => {
                    setForm(null)
                    setActionError('')
                  }}
                />
              )}
              {teacher && banks.length === 0 && (
                <p className="exam-scope">
                  Chưa có ngân hàng câu hỏi.{' '}
                  <Link to="/question-banks">
                    Tạo ngân hàng và câu hỏi trước
                  </Link>
                  .
                </p>
              )}
              {sessions.length === 0 ? (
                <p className="exam-empty">
                  {teacher
                    ? 'Chưa có phiên thi. Tạo phiên đầu tiên để bắt đầu.'
                    : 'Chưa có phiên thi đang mở. Hãy quay lại khi giảng viên mở phiên.'}
                </p>
              ) : (
                <div className="exam-list">
                  {sessions.map((session) => (
                    <article
                      className="auth-card exam-card"
                      key={session.id}
                      aria-label={session.title}
                    >
                      <div className="exam-card__heading">
                        <h3>{session.title}</h3>
                        <span
                          className={`exam-badge exam-badge--${session.status.toLowerCase()}`}
                        >
                          {statusLabels[session.status] ?? session.status}
                        </span>
                      </div>
                      <p>Ngân hàng: {session.questionBankName}</p>
                      <p>{session.participantCount} sinh viên đã tham gia</p>
                      <div className="exam-actions">
                        {teacher ? (
                          <>
                            <button
                              className="exam-button"
                              type="button"
                              disabled={
                                pending || !!form || session.status !== 'Draft'
                              }
                              onClick={() => {
                                setForm({ session })
                                setActionError('')
                                setNotice('')
                              }}
                              aria-label={`Sửa ${session.title}`}
                            >
                              Sửa
                            </button>
                            <button
                              className="exam-button exam-button--danger"
                              type="button"
                              disabled={
                                pending || !!form || session.status !== 'Draft'
                              }
                              onClick={() => {
                                setDeleting(session)
                                setActionError('')
                                setNotice('')
                              }}
                              aria-label={`Xóa ${session.title}`}
                            >
                              Xóa
                            </button>
                            <button
                              className="exam-button"
                              type="button"
                              disabled={pending || !!form}
                              onClick={() =>
                                setPreparingId((current) =>
                                  current === session.id ? null : session.id,
                                )
                              }
                            >
                              {preparingId === session.id
                                ? 'Ẩn chuẩn bị đề'
                                : 'Chuẩn bị đề'}
                            </button>
                            {session.status === 'Open' && (
                              <button
                                className="exam-button"
                                type="button"
                                disabled={pending}
                                onClick={() =>
                                  mutate(
                                    'close',
                                    (signal) =>
                                      api.updateExamSession(
                                        session.id,
                                        {
                                          title: session.title,
                                          questionBankId:
                                            session.questionBankId,
                                          status: 'Closed',
                                        },
                                        signal,
                                      ),
                                    (result) =>
                                      setSessions((current) =>
                                        current.map((item) =>
                                          item.id === result.id ? result : item,
                                        ),
                                      ),
                                  )
                                }
                              >
                                Đóng phiên
                              </button>
                            )}
                          </>
                        ) : (
                          <button
                            className="exam-button exam-button--primary"
                            type="button"
                            disabled={pending || session.status !== 'Open'}
                            onClick={() =>
                              mutate(
                                'join',
                                (signal) =>
                                  api.joinExamSession(session.id, signal),
                                (result) =>
                                  setAttempt({
                                    ...result,
                                    title: session.title,
                                  }),
                              )
                            }
                          >
                            {pending ? 'Đang xử lý...' : 'Vào thi'}
                          </button>
                        )}
                      </div>
                      {teacher && preparingId === session.id && (
                        <SessionPreparationPanel
                          key={session.id}
                          session={session}
                          onAccessError={handleAccessError}
                          onPublished={(result) => {
                            setSessions((current) =>
                              current.map((item) =>
                                item.id === result.id ? result : item,
                              ),
                            )
                            setNotice('Phiên đã mở cho sinh viên.')
                          }}
                        />
                      )}
                    </article>
                  ))}
                </div>
              )}
            </>
          )}
        </>
      )}
      {deleting && (
        <DeleteConfirmation
          session={deleting}
          pending={pending}
          error={actionError}
          onCancel={() => {
            setDeleting(null)
            setActionError('')
          }}
          onConfirm={() =>
            mutate(
              'delete',
              (signal) => api.deleteExamSession(deleting.id, signal),
              () => {
                setSessions((current) =>
                  current.filter((item) => item.id !== deleting.id),
                )
                setDeleting(null)
                setNotice('Đã xóa phiên thi.')
              },
            )
          }
        />
      )}
    </SessionLayout>
  )
}
