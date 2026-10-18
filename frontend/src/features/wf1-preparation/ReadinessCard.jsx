import { useState } from 'react'
import { describePreparationError } from './feedback.js'

const readinessLabels = {
  selection: 'Đã chọn ít nhất một câu hỏi',
  keyPoints: 'Mỗi câu đã chọn có ý chính kỳ vọng',
  rubric: 'Ngân hàng có rubric',
  rubricCriteria: 'Rubric có ít nhất một tiêu chí',
  rubricWeight: 'Tổng trọng số tiêu chí bằng 1.0000',
}

export default function ReadinessCard({
  status = 'Draft',
  missing = [],
  questionIdsWithoutKeyPoints = [],
  serverError = null,
  onPublish,
}) {
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState(null)
  const remaining =
    actionError?.problem?.missing ?? serverError?.problem?.missing ?? missing
  const missingQuestions =
    actionError?.problem?.questionIdsWithoutKeyPoints ??
    serverError?.problem?.questionIdsWithoutKeyPoints ??
    questionIdsWithoutKeyPoints
  const ready = missing.length === 0

  async function publish() {
    if (busy || !ready || status !== 'Draft') return
    setBusy(true)
    setActionError(null)
    try {
      await onPublish()
    } catch (failure) {
      setActionError(failure)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section
      className="wf-card wf-card--readiness"
      aria-labelledby="wf-readiness-title"
    >
      <div className="wf-card__heading">
        <div>
          <span className="wf-kicker">06 / KIỂM TRA & MỞ PHIÊN</span>
          <h2 id="wf-readiness-title">Sẵn sàng công bố?</h2>
          <p>Hệ thống kiểm tra lại điều kiện khi chuyển Draft → Open.</p>
        </div>
        <span className={`wf-status wf-status--${status.toLowerCase()}`}>
          {status}
        </span>
      </div>
      <ul className="wf-checklist">
        {Object.entries(readinessLabels).map(([code, label]) => (
          <li
            key={code}
            className={
              remaining.includes(code)
                ? 'wf-checklist__missing'
                : 'wf-checklist__done'
            }
          >
            <span aria-hidden="true">
              {remaining.includes(code) ? '○' : '✓'}
            </span>
            {label}
          </li>
        ))}
      </ul>
      {missingQuestions.length > 0 && (
        <p className="wf-note">
          Câu hỏi còn thiếu ý chính: {missingQuestions.length}. Chọn từng câu để
          bổ sung trước khi mở.
        </p>
      )}
      {(serverError || actionError) && (
        <p className="wf-form-error" role="alert">
          {describePreparationError(
            actionError ?? serverError,
            'Không thể mở phiên.',
          )}
        </p>
      )}
      {status === 'Draft' ? (
        <button
          type="button"
          className="wf-button wf-button--solid wf-button--wide"
          disabled={!ready || busy}
          onClick={publish}
        >
          {busy
            ? 'Đang kiểm tra...'
            : ready
              ? 'Mở phiên cho sinh viên →'
              : `Còn ${missing.length} điều kiện cần hoàn tất`}
        </button>
      ) : (
        <p className="wf-state" role="status">
          {status === 'Open'
            ? 'Phiên đã mở. Sinh viên có thể bấm Vào thi.'
            : 'Phiên đã đóng, không nhận lượt tham gia mới.'}
        </p>
      )}
    </section>
  )
}
