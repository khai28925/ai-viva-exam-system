import { useState } from 'react'
import { describePreparationError } from './feedback.js'

export default function StudentSessionsCard({
  sessions = [],
  attempt = null,
  isLoading = false,
  error = null,
  onRetry,
  onJoin,
  onBack,
}) {
  const [busyId, setBusyId] = useState(null)
  const [actionError, setActionError] = useState('')
  const openSessions = sessions.filter((session) => session.status === 'Open')

  async function join(sessionId) {
    if (busyId) return
    setBusyId(sessionId)
    setActionError('')
    try {
      await onJoin(sessionId)
    } catch (failure) {
      setActionError(
        describePreparationError(failure, 'Không thể vào phiên thi.'),
      )
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="wf-student" aria-labelledby="wf-student-title">
      <header className="wf-student__header">
        <span className="wf-kicker">STUDENT WORKSPACE / WF1</span>
        <h1 id="wf-student-title">
          Phiên thi của bạn<span>.</span>
        </h1>
        <p>
          Chỉ những phiên Open mới hiển thị. Lượt tham gia được lưu lại khi bấm
          Vào thi.
        </p>
      </header>
      {attempt ? (
        <div className="wf-card wf-attempt">
          <span className="wf-status wf-status--open">ĐÃ VÀO THI</span>
          <h2>Câu hỏi đã chọn</h2>
          <p>
            Đây là bản xem trước WF1: chỉ đọc câu hỏi, chưa ghi âm, trả lời hay
            nộp bài.
          </p>
          <ol>
            {attempt.questions.map((question, index) => (
              <li key={question.id}>
                <span>{String(index + 1).padStart(2, '0')}</span>
                {question.content}
              </li>
            ))}
          </ol>
          <button
            type="button"
            className="wf-button wf-button--outline"
            onClick={onBack}
          >
            ← Về danh sách phiên
          </button>
        </div>
      ) : isLoading ? (
        <p className="wf-state" role="status">
          Đang tải phiên thi...
        </p>
      ) : error ? (
        <div className="wf-state wf-state--error" role="alert">
          <p>{describePreparationError(error, 'Không thể tải phiên thi.')}</p>
          {onRetry && (
            <button type="button" onClick={onRetry}>
              Thử lại
            </button>
          )}
        </div>
      ) : openSessions.length === 0 ? (
        <div className="wf-card wf-empty">
          <h2>Chưa có phiên đang mở</h2>
          <p>Hãy quay lại sau khi giảng viên công bố phiên thi.</p>
        </div>
      ) : (
        <div className="wf-session-grid">
          {openSessions.map((session) => (
            <article className="wf-card wf-session" key={session.id}>
              <div className="wf-session__top">
                <span className="wf-status wf-status--open">OPEN</span>
                <span>{session.participantCount ?? 0} đã tham gia</span>
              </div>
              <h2>{session.title}</h2>
              <p>Ngân hàng: {session.questionBankName}</p>
              <button
                type="button"
                className="wf-button wf-button--solid"
                disabled={Boolean(busyId)}
                onClick={() => join(session.id)}
              >
                {busyId === session.id ? 'Đang vào thi...' : 'Vào thi →'}
              </button>
            </article>
          ))}
        </div>
      )}
      {actionError && (
        <p className="wf-form-error" role="alert">
          {actionError}
        </p>
      )}
    </section>
  )
}
