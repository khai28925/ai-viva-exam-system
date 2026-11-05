import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { getSubjectWorkspace } from '../../api/wf1Api.js'

// Grading always uses the subject's approved rubric set (shared by every bank of the subject),
// so this card shows that set instead of editing a per-bank rubric that grading ignores.
export default function SubjectRubricCard({ bank, onAccessError }) {
  const [result, setResult] = useState(null)
  const subjectId = bank.subjectId

  useEffect(() => {
    if (!subjectId) return undefined
    const controller = new AbortController()
    getSubjectWorkspace(subjectId, { signal: controller.signal })
      .then((workspace) => {
        if (!controller.signal.aborted)
          setResult({ subjectId, workspace, error: null })
      })
      .catch((error) => {
        if (controller.signal.aborted) return
        // A 403 only means another lecturer owns the subject; keep the bank page usable.
        if (error.status === 401) onAccessError(error)
        setResult({ subjectId, workspace: null, error })
      })
    return () => controller.abort()
  }, [subjectId, onAccessError])

  const current = result?.subjectId === subjectId ? result : null
  const approved = current?.workspace?.approvedRubricSet
  const draft = current?.workspace?.draftRubricSet
  const editorLink = `/wf1-preparation?subject=${encodeURIComponent(subjectId ?? '')}`

  return (
    <section className="wf-card" aria-labelledby="wf-subject-rubric-title">
      <div className="wf-card__heading">
        <div>
          <span className="wf-kicker">04 / RUBRIC CHẤM ĐIỂM</span>
          <h2 id="wf-subject-rubric-title">Bộ rubric của môn học</h2>
          <p>
            AI chấm theo bộ rubric đã duyệt của môn (dùng chung cho mọi câu
            hỏi); ý chính kỳ vọng của từng câu bổ sung căn cứ chấm.
          </p>
        </div>
        <span className="wf-count">
          {(approved?.rubrics.length ?? 0).toString().padStart(2, '0')}
        </span>
      </div>

      {!subjectId ? (
        <p className="wf-empty">Bộ câu hỏi chưa gắn với môn học.</p>
      ) : !current ? (
        <p className="wf-state" role="status">
          Đang tải bộ rubric...
        </p>
      ) : current.error ? (
        <p className="wf-state wf-state--error" role="alert">
          {current.error.status === 403
            ? 'Môn học này thuộc giảng viên khác nên không xem được bộ rubric.'
            : 'Không thể tải bộ rubric của môn học.'}
        </p>
      ) : approved ? (
        <>
          <div className="wf-rubric-summary">
            <div>
              <strong>Phiên bản {approved.version} · đã duyệt</strong>
              <p>
                {draft
                  ? 'Đang có bản nháp mới chờ duyệt.'
                  : 'Phiên thi mới sẽ ghim phiên bản này.'}
              </p>
            </div>
            <div className="wf-rubric-summary__weight">
              <span>TỔNG TỶ TRỌNG</span>
              <strong>
                {approved.rubrics.reduce(
                  (total, rubric) => total + Number(rubric.weightPercent),
                  0,
                )}
                %
              </strong>
            </div>
          </div>
          <ul className="wf-subject-rubrics">
            {approved.rubrics.map((rubric) => (
              <li key={rubric.id}>
                <strong>
                  {rubric.name} · {rubric.weightPercent}%
                </strong>
                <small>
                  {rubric.criteria
                    .map(
                      (criterion) =>
                        `${criterion.name} (tối đa ${criterion.maxScore})`,
                    )
                    .join(' · ')}
                </small>
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p className="wf-empty">
          Môn học chưa có bộ rubric được duyệt
          {draft ? ' (đã có bản nháp)' : ''}. Cần duyệt rubric trước khi mở
          phiên thi.
        </p>
      )}

      {subjectId && (
        <div className="wf-actions wf-actions--start">
          <Link className="wf-button wf-button--outline" to={editorLink}>
            Soạn / duyệt rubric của môn
          </Link>
        </div>
      )}
    </section>
  )
}
