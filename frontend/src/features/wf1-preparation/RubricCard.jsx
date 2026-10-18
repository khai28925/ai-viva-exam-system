import { useState } from 'react'
import { describePreparationError } from './feedback.js'

function validDecimal(value, places) {
  return Number.isFinite(value) && Number(value.toFixed(places)) === value
}

export default function RubricCard({
  rubric,
  criteria = [],
  isLoading = false,
  error = null,
  onRetry,
  onCreateRubric,
  onUpdateRubric,
  onDeleteRubric,
  onCreateCriterion,
  onUpdateCriterion,
  onDeleteCriterion,
}) {
  const [rubricForm, setRubricForm] = useState(false)
  const [rubricName, setRubricName] = useState('')
  const [rubricDescription, setRubricDescription] = useState('')
  const [criterionForm, setCriterionForm] = useState(null)
  const [criterionName, setCriterionName] = useState('')
  const [criterionDescription, setCriterionDescription] = useState('')
  const [maxScore, setMaxScore] = useState('10')
  const [weight, setWeight] = useState('0.5')
  const [confirm, setConfirm] = useState(null)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)
  const totalWeight = criteria.reduce((sum, item) => sum + item.weight, 0)

  function editRubric() {
    setRubricName(rubric?.name ?? '')
    setRubricDescription(rubric?.description ?? '')
    setRubricForm(true)
    setFormError('')
  }

  function editCriterion(criterion) {
    setCriterionName(criterion?.name ?? '')
    setCriterionDescription(criterion?.description ?? '')
    setMaxScore(String(criterion?.maxScore ?? 10))
    setWeight(String(criterion?.weight ?? 0.5))
    setCriterionForm(criterion?.id ?? 'new')
    setFormError('')
  }

  async function saveRubric(event) {
    event.preventDefault()
    if (busy) return
    const name = rubricName.trim()
    const description = rubricDescription.trim()
    if (name.length < 1 || name.length > 120 || description.length > 500) {
      setFormError('Tên cần 1–120 ký tự; mô tả tối đa 500 ký tự.')
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const request = { name, description: description || null }
      if (rubric) await onUpdateRubric(request)
      else await onCreateRubric(request)
      setRubricForm(false)
    } catch (failure) {
      setFormError(describePreparationError(failure, 'Không thể lưu rubric.'))
    } finally {
      setBusy(false)
    }
  }

  async function saveCriterion(event) {
    event.preventDefault()
    if (busy) return
    const name = criterionName.trim()
    const description = criterionDescription.trim()
    const score = Number(maxScore)
    const criterionWeight = Number(weight)
    if (name.length < 1 || name.length > 120 || description.length > 500) {
      setFormError('Tên cần 1–120 ký tự; mô tả tối đa 500 ký tự.')
      return
    }
    if (score <= 0 || !validDecimal(score, 2)) {
      setFormError(
        'Điểm tối đa phải lớn hơn 0 và có tối đa 2 chữ số thập phân.',
      )
      return
    }
    if (
      criterionWeight <= 0 ||
      criterionWeight > 1 ||
      !validDecimal(criterionWeight, 4)
    ) {
      setFormError(
        'Trọng số phải lớn hơn 0, không quá 1 và có tối đa 4 chữ số thập phân.',
      )
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const request = {
        name,
        description: description || null,
        maxScore: score,
        weight: criterionWeight,
      }
      if (criterionForm === 'new') await onCreateCriterion(request)
      else await onUpdateCriterion(criterionForm, request)
      setCriterionForm(null)
    } catch (failure) {
      setFormError(describePreparationError(failure, 'Không thể lưu tiêu chí.'))
    } finally {
      setBusy(false)
    }
  }

  async function remove() {
    if (busy) return
    setBusy(true)
    setFormError('')
    try {
      if (confirm === 'rubric') await onDeleteRubric()
      else await onDeleteCriterion(confirm)
      setConfirm(null)
    } catch (failure) {
      setFormError(describePreparationError(failure, 'Không thể xóa dữ liệu.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="wf-card" aria-labelledby="wf-rubric-title">
      <div className="wf-card__heading">
        <div>
          <span className="wf-kicker">04 / TIÊU CHÍ ĐÁNH GIÁ</span>
          <h2 id="wf-rubric-title">Rubric & criteria</h2>
          <p>
            Một rubric cho mỗi ngân hàng; tổng trọng số bằng 1 khi mở phiên.
          </p>
        </div>
        <span className="wf-count">
          {criteria.length.toString().padStart(2, '0')}
        </span>
      </div>

      {isLoading ? (
        <p className="wf-state" role="status">
          Đang tải rubric...
        </p>
      ) : error ? (
        <div className="wf-state wf-state--error" role="alert">
          <p>{describePreparationError(error, 'Không thể tải rubric.')}</p>
          {onRetry && (
            <button type="button" onClick={onRetry}>
              Thử lại
            </button>
          )}
        </div>
      ) : (
        <>
          {rubric ? (
            <div className="wf-rubric-summary">
              <div>
                <strong>{rubric.name}</strong>
                <p>{rubric.description || 'Chưa có mô tả.'}</p>
              </div>
              <div className="wf-rubric-summary__weight">
                <span>TỔNG TRỌNG SỐ</span>
                <strong>{totalWeight.toFixed(4)} / 1.0000</strong>
              </div>
            </div>
          ) : (
            <p className="wf-empty">
              Chưa có rubric. Hãy tạo rubric trước khi thêm tiêu chí.
            </p>
          )}

          {rubric && criteria.length > 0 && (
            <ul className="wf-criteria">
              {criteria.map((criterion) => (
                <li key={criterion.id}>
                  <div>
                    <strong>{criterion.name}</strong>
                    <small>{criterion.description || 'Không có mô tả'}</small>
                  </div>
                  <span>{criterion.maxScore} điểm</span>
                  <span>{criterion.weight.toFixed(4)}</span>
                  <button
                    type="button"
                    className="wf-link"
                    onClick={() => editCriterion(criterion)}
                    disabled={busy}
                  >
                    Sửa
                  </button>
                  <button
                    type="button"
                    className="wf-link wf-link--danger"
                    onClick={() => {
                      setConfirm(criterion.id)
                      setFormError('')
                    }}
                    disabled={busy}
                  >
                    Xóa
                  </button>
                </li>
              ))}
            </ul>
          )}
          {rubric && criteria.length === 0 && (
            <p className="wf-empty">
              Rubric chưa có tiêu chí. Thêm ít nhất một tiêu chí để mở phiên.
            </p>
          )}

          {!rubricForm && criterionForm === null && !confirm && (
            <div className="wf-actions wf-actions--start">
              <button
                type="button"
                className="wf-button wf-button--outline"
                onClick={editRubric}
              >
                {rubric ? 'Sửa rubric' : '+ Tạo rubric'}
              </button>
              {rubric && (
                <button
                  type="button"
                  className="wf-button wf-button--outline"
                  onClick={() => editCriterion(null)}
                >
                  + Thêm tiêu chí
                </button>
              )}
              {rubric && (
                <button
                  type="button"
                  className="wf-link wf-link--danger"
                  onClick={() => {
                    setConfirm('rubric')
                    setFormError('')
                  }}
                  disabled={criteria.length > 0}
                  title={
                    criteria.length > 0 ? 'Xóa hết tiêu chí trước' : undefined
                  }
                >
                  Xóa rubric
                </button>
              )}
            </div>
          )}

          {rubricForm && (
            <form className="wf-inline-form" onSubmit={saveRubric} noValidate>
              <label htmlFor="wf-rubric-name">Tên rubric</label>
              <input
                id="wf-rubric-name"
                value={rubricName}
                maxLength={120}
                onChange={(event) => setRubricName(event.target.value)}
                disabled={busy}
              />
              <label htmlFor="wf-rubric-description">Mô tả</label>
              <textarea
                id="wf-rubric-description"
                value={rubricDescription}
                maxLength={500}
                rows={2}
                onChange={(event) => setRubricDescription(event.target.value)}
                disabled={busy}
              />
              {formError && (
                <p className="wf-form-error" role="alert">
                  {formError}
                </p>
              )}
              <div className="wf-actions">
                <button
                  type="button"
                  className="wf-button wf-button--quiet"
                  onClick={() => setRubricForm(false)}
                  disabled={busy}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="wf-button wf-button--solid"
                  disabled={busy}
                >
                  {busy ? 'Đang lưu...' : 'Lưu rubric'}
                </button>
              </div>
            </form>
          )}

          {criterionForm !== null && (
            <form
              className="wf-inline-form"
              onSubmit={saveCriterion}
              noValidate
            >
              <label htmlFor="wf-criterion-name">Tên tiêu chí</label>
              <input
                id="wf-criterion-name"
                value={criterionName}
                maxLength={120}
                onChange={(event) => setCriterionName(event.target.value)}
                disabled={busy}
              />
              <label htmlFor="wf-criterion-description">Mô tả</label>
              <textarea
                id="wf-criterion-description"
                value={criterionDescription}
                maxLength={500}
                rows={2}
                onChange={(event) =>
                  setCriterionDescription(event.target.value)
                }
                disabled={busy}
              />
              <div className="wf-form-grid">
                <div>
                  <label htmlFor="wf-criterion-score">Điểm tối đa</label>
                  <input
                    id="wf-criterion-score"
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={maxScore}
                    onChange={(event) => setMaxScore(event.target.value)}
                    disabled={busy}
                  />
                </div>
                <div>
                  <label htmlFor="wf-criterion-weight">Trọng số</label>
                  <input
                    id="wf-criterion-weight"
                    type="number"
                    min="0.0001"
                    max="1"
                    step="0.0001"
                    value={weight}
                    onChange={(event) => setWeight(event.target.value)}
                    disabled={busy}
                  />
                </div>
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
                  onClick={() => setCriterionForm(null)}
                  disabled={busy}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="wf-button wf-button--solid"
                  disabled={busy}
                >
                  {busy ? 'Đang lưu...' : 'Lưu tiêu chí'}
                </button>
              </div>
            </form>
          )}

          {confirm && (
            <div
              className="wf-confirm"
              role="alertdialog"
              aria-label={
                confirm === 'rubric'
                  ? 'Xác nhận xóa rubric'
                  : 'Xác nhận xóa tiêu chí'
              }
            >
              <strong>
                {confirm === 'rubric' ? 'Xóa rubric?' : 'Xóa tiêu chí?'}
              </strong>
              <p>Thao tác này không thể hoàn tác.</p>
              {formError && (
                <p className="wf-form-error" role="alert">
                  {formError}
                </p>
              )}
              <div className="wf-actions">
                <button
                  type="button"
                  className="wf-button wf-button--quiet"
                  onClick={() => setConfirm(null)}
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
      )}
    </section>
  )
}
