import { useState } from 'react'
import { describePreparationError, validateContent } from './feedback.js'

export default function ExpectedKeyPointsCard({
  question,
  points = [],
  isLoading = false,
  error = null,
  onRetry,
  onCreate,
  onUpdate,
  onDelete,
}) {
  const [editingId, setEditingId] = useState(null)
  const [draft, setDraft] = useState('')
  const [confirmId, setConfirmId] = useState(null)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)
  const sortedPoints = [...points].sort(
    (left, right) =>
      left.displayOrder - right.displayOrder || left.id.localeCompare(right.id),
  )

  function startEdit(point) {
    setEditingId(point?.id ?? 'new')
    setDraft(point?.content ?? '')
    setFormError('')
  }

  async function save(event) {
    event.preventDefault()
    if (busy) return
    const validationError = validateContent(draft)
    if (validationError) {
      setFormError(validationError)
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const request = { content: draft.trim() }
      if (editingId === 'new') await onCreate(request)
      else await onUpdate(editingId, request)
      setEditingId(null)
    } catch (failure) {
      setFormError(
        describePreparationError(
          failure,
          'Không thể lưu ý chính. Vui lòng thử lại.',
        ),
      )
    } finally {
      setBusy(false)
    }
  }

  async function remove() {
    if (busy) return
    setBusy(true)
    setFormError('')
    try {
      await onDelete(confirmId)
      setConfirmId(null)
    } catch (failure) {
      setFormError(
        describePreparationError(
          failure,
          'Không thể xóa ý chính. Vui lòng thử lại.',
        ),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="wf-card" aria-labelledby="wf-key-points-title">
      <div className="wf-card__heading">
        <div>
          <span className="wf-kicker">03 / Ý CHÍNH KỲ VỌNG</span>
          <h2 id="wf-key-points-title">Expected key points</h2>
          <p>Giảng viên xác định những ý sinh viên nên đề cập.</p>
        </div>
        <span className="wf-count">
          {sortedPoints.length.toString().padStart(2, '0')}
        </span>
      </div>

      {question ? (
        <div className="wf-context">
          <span>CÂU HỎI ĐANG CHỌN</span>
          <strong>{question.content}</strong>
        </div>
      ) : (
        <p className="wf-empty">Chọn một câu hỏi để xem và soạn ý chính.</p>
      )}

      {question &&
        (isLoading ? (
          <p className="wf-state" role="status">
            Đang tải ý chính...
          </p>
        ) : error ? (
          <div className="wf-state wf-state--error" role="alert">
            <p>{describePreparationError(error, 'Không thể tải ý chính.')}</p>
            {onRetry && (
              <button type="button" onClick={onRetry}>
                Thử lại
              </button>
            )}
          </div>
        ) : (
          <>
            {sortedPoints.length === 0 ? (
              <p className="wf-empty">
                Chưa có ý chính. Hãy thêm ít nhất một ý để có thể mở phiên.
              </p>
            ) : (
              <ol className="wf-key-list">
                {sortedPoints.map((point) => (
                  <li key={point.id}>
                    <span className="wf-order">
                      {String(point.displayOrder + 1).padStart(2, '0')}
                    </span>
                    <span className="wf-key-list__content">
                      {point.content}
                    </span>
                    <button
                      type="button"
                      className="wf-link"
                      onClick={() => startEdit(point)}
                      disabled={busy}
                      aria-label={`Sửa ý chính ${point.displayOrder + 1}`}
                    >
                      Sửa
                    </button>
                    <button
                      type="button"
                      className="wf-link wf-link--danger"
                      onClick={() => {
                        setConfirmId(point.id)
                        setFormError('')
                      }}
                      disabled={busy}
                      aria-label={`Xóa ý chính ${point.displayOrder + 1}`}
                    >
                      Xóa
                    </button>
                  </li>
                ))}
              </ol>
            )}
            {onCreate && editingId === null && !confirmId && (
              <button
                type="button"
                className="wf-button wf-button--outline"
                onClick={() => startEdit(null)}
              >
                + Thêm ý chính
              </button>
            )}
            {editingId !== null && (
              <form className="wf-inline-form" onSubmit={save} noValidate>
                <label htmlFor="wf-key-content">
                  {editingId === 'new' ? 'Thêm ý chính' : 'Sửa ý chính'}
                </label>
                <textarea
                  id="wf-key-content"
                  value={draft}
                  onChange={(event) => {
                    setDraft(event.target.value)
                    setFormError('')
                  }}
                  maxLength={2000}
                  rows={3}
                  disabled={busy}
                  aria-invalid={Boolean(formError)}
                  placeholder="Ví dụ: nêu đúng trách nhiệm của từng thành phần..."
                />
                <div className="wf-field-meta">
                  <span>1–2000 ký tự · thứ tự do hệ thống gán</span>
                  <span>{draft.length}/2000</span>
                </div>
                {formError && (
                  <p className="wf-form-error" role="alert">
                    {formError}
                  </p>
                )}
                <div className="wf-actions">
                  <button
                    type="button"
                    className="wf-button wf-button--quiet"
                    onClick={() => setEditingId(null)}
                    disabled={busy}
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    className="wf-button wf-button--solid"
                    disabled={busy}
                  >
                    {busy ? 'Đang lưu...' : 'Lưu ý chính'}
                  </button>
                </div>
              </form>
            )}
            {confirmId && (
              <div
                className="wf-confirm"
                role="alertdialog"
                aria-label="Xác nhận xóa ý chính"
              >
                <strong>Xóa ý chính này?</strong>
                <p>
                  Thao tác không thể hoàn tác; thứ tự các ý còn lại sẽ được giữ
                  nguyên.
                </p>
                {formError && (
                  <p className="wf-form-error" role="alert">
                    {formError}
                  </p>
                )}
                <div className="wf-actions">
                  <button
                    type="button"
                    className="wf-button wf-button--quiet"
                    onClick={() => setConfirmId(null)}
                    disabled={busy}
                  >
                    Giữ lại
                  </button>
                  <button
                    type="button"
                    className="wf-button wf-button--danger"
                    onClick={remove}
                    disabled={busy}
                  >
                    {busy ? 'Đang xóa...' : 'Xác nhận xóa'}
                  </button>
                </div>
              </div>
            )}
          </>
        ))}
    </section>
  )
}
