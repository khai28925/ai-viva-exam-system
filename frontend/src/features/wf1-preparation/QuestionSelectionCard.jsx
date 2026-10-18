import { useState } from 'react'
import { describePreparationError } from './feedback.js'

export default function QuestionSelectionCard({
  questions = [],
  selectedQuestionIds = [],
  selectedQuestions = [],
  status = 'Draft',
  isLoading = false,
  error = null,
  onRetry,
  onChange,
}) {
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState('')
  const bankQuestions = questions.filter(
    (question) => question.questionType === 'Bank' || !question.questionType,
  )
  const selected =
    selectedQuestions.length > 0
      ? [...selectedQuestions].sort(
          (left, right) => left.displayOrder - right.displayOrder,
        )
      : selectedQuestionIds
          .map((id, displayOrder) => {
            const question = bankQuestions.find((item) => item.id === id)
            return question
              ? {
                  questionId: id,
                  contentSnapshot: question.content,
                  displayOrder,
                  isRequired: true,
                }
              : null
          })
          .filter(Boolean)
  const locked = status !== 'Draft'

  async function change(nextIds) {
    if (busy || locked) return
    if (nextIds.length > 50) {
      setActionError('Mỗi phiên chỉ được chọn tối đa 50 câu hỏi.')
      return
    }
    setBusy(true)
    setActionError('')
    try {
      await onChange(nextIds)
    } catch (failure) {
      setActionError(
        describePreparationError(failure, 'Không thể lưu danh sách câu hỏi.'),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="wf-card" aria-labelledby="wf-selection-title">
      <div className="wf-card__heading">
        <div>
          <span className="wf-kicker">05 / CẤU HÌNH ĐỀ THI</span>
          <h2 id="wf-selection-title">Chọn câu hỏi</h2>
          <p>Chọn 0–50 câu hỏi; thứ tự trong danh sách là thứ tự đề thi.</p>
        </div>
        <span className="wf-count">
          {selectedQuestionIds.length.toString().padStart(2, '0')}
        </span>
      </div>
      {isLoading ? (
        <p className="wf-state" role="status">
          Đang tải câu hỏi...
        </p>
      ) : error ? (
        <div className="wf-state wf-state--error" role="alert">
          <p>{describePreparationError(error, 'Không thể tải câu hỏi.')}</p>
          {onRetry && (
            <button type="button" onClick={onRetry}>
              Thử lại
            </button>
          )}
        </div>
      ) : (
        <>
          {locked && (
            <p className="wf-note">
              Phiên {status} đã khóa danh sách câu hỏi và nội dung snapshot.
            </p>
          )}
          <div className="wf-selection-toolbar">
            <strong>{selectedQuestionIds.length}/50 câu đã chọn</strong>
            <button
              type="button"
              className="wf-link"
              disabled={busy || locked || bankQuestions.length === 0}
              onClick={() =>
                change(bankQuestions.map((question) => question.id))
              }
            >
              Chọn cả ngân hàng
            </button>
            <button
              type="button"
              className="wf-link"
              disabled={busy || locked || selectedQuestionIds.length === 0}
              onClick={() => change([])}
            >
              Bỏ chọn
            </button>
          </div>
          {bankQuestions.length === 0 ? (
            <p className="wf-empty">Ngân hàng chưa có câu hỏi loại Bank.</p>
          ) : (
            <ul className="wf-selection-list">
              {bankQuestions.map((question) => (
                <li key={question.id}>
                  <label>
                    <input
                      type="checkbox"
                      checked={selectedQuestionIds.includes(question.id)}
                      disabled={busy || locked}
                      onChange={() =>
                        change(
                          selectedQuestionIds.includes(question.id)
                            ? selectedQuestionIds.filter(
                                (id) => id !== question.id,
                              )
                            : [...selectedQuestionIds, question.id],
                        )
                      }
                    />
                    <span>{question.content}</span>
                  </label>
                </li>
              ))}
            </ul>
          )}
          {actionError && (
            <p className="wf-form-error" role="alert">
              {actionError}
            </p>
          )}
          <div className="wf-snapshot">
            <span>SNAPSHOT CÂU HỎI SẼ HIỂN THỊ CHO SINH VIÊN</span>
            {selected.length === 0 ? (
              <p>Chưa chọn câu hỏi nào.</p>
            ) : (
              <ol>
                {selected.map((item) => (
                  <li key={item.questionId}>
                    <b>{String(item.displayOrder + 1).padStart(2, '0')}</b>
                    <span>{item.contentSnapshot}</span>
                    <small>{item.isRequired ? 'Bắt buộc' : 'Tùy chọn'}</small>
                  </li>
                ))}
              </ol>
            )}
          </div>
        </>
      )}
    </section>
  )
}
