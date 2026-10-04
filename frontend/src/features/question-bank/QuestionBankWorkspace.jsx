import { useEffect, useRef, useState } from 'react'
import './questionBank.css'

const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: 'UTC',
})

function Icon({ name, size = 20 }) {
  const paths = {
    book: (
      <>
        <path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v16H6.5A2.5 2.5 0 0 0 4 21V5.5Z" />
        <path d="M4 17.5A2.5 2.5 0 0 1 6.5 15H20" />
        <path d="M8 7h8M8 10h6" />
      </>
    ),
    grid: (
      <>
        <rect x="3" y="3" width="7" height="7" rx="1.5" />
        <rect x="14" y="3" width="7" height="7" rx="1.5" />
        <rect x="3" y="14" width="7" height="7" rx="1.5" />
        <rect x="14" y="14" width="7" height="7" rx="1.5" />
      </>
    ),
    search: (
      <>
        <circle cx="10.8" cy="10.8" r="6.8" />
        <path d="m16 16 4.5 4.5" />
      </>
    ),
    plus: <path d="M12 4v16M4 12h16" />,
    arrow: <path d="m9 18 6-6-6-6" />,
    edit: (
      <>
        <path d="m4 20 4.2-.8L20 7.4 16.6 4 4.8 15.8 4 20Z" />
        <path d="m14.5 6.1 3.4 3.4" />
      </>
    ),
    trash: (
      <>
        <path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v5M14 11v5" />
      </>
    ),
    close: <path d="M5 5 19 19M19 5 5 19" />,
    check: <path d="m5 12 4.5 4.5L19 7" />,
    alert: (
      <>
        <path d="M12 3 2 20h20L12 3Z" />
        <path d="M12 9v5M12 17h.01" />
      </>
    ),
    layers: (
      <>
        <path d="m12 3 9 5-9 5-9-5 9-5ZM3 12l9 5 9-5M3 16l9 5 9-5" />
      </>
    ),
  }

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name]}
    </svg>
  )
}

function EmptyState({ title, description, action }) {
  return (
    <div className="qb-empty">
      <div className="qb-empty__icon">
        <Icon name="book" size={30} />
      </div>
      <h3>{title}</h3>
      <p>{description}</p>
      {action}
    </div>
  )
}

function Modal({ title, description, children, onClose, labelledBy }) {
  const closeRef = useRef(null)

  useEffect(() => {
    closeRef.current?.focus()
    const onKeyDown = (event) => {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  return (
    <div
      className="qb-modal-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <section
        className="qb-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
      >
        <div className="qb-modal__head">
          <div>
            <p className="qb-eyebrow">Ngân hàng câu hỏi</p>
            <h2 id={labelledBy}>{title}</h2>
            <p>{description}</p>
          </div>
          <button
            ref={closeRef}
            className="qb-icon-button"
            type="button"
            aria-label="Đóng hộp thoại"
            onClick={onClose}
          >
            <Icon name="close" size={19} />
          </button>
        </div>
        {children}
      </section>
    </div>
  )
}

function QuestionFormDialog({ mode, question, bank, onClose, onSubmit }) {
  const [content, setContent] = useState(question?.content ?? '')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > 2000) {
      setError('Nội dung cần từ 1 đến 2000 ký tự.')
      return
    }

    setError('')
    setSubmitting(true)
    try {
      await onSubmit({ content: trimmed })
      onClose()
    } catch {
      setError('Không thể lưu câu hỏi. Vui lòng thử lại.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      title={mode === 'create' ? 'Thêm câu hỏi' : 'Chỉnh sửa câu hỏi'}
      description={`Trong ngân hàng “${bank.name}”`}
      onClose={onClose}
      labelledBy="qb-form-title"
    >
      <form onSubmit={handleSubmit} noValidate>
        <label className="qb-label" htmlFor="qb-question-content">
          Nội dung câu hỏi <span>*</span>
        </label>
        <textarea
          id="qb-question-content"
          value={content}
          onChange={(event) => {
            setContent(event.target.value)
            setError('')
          }}
          maxLength={2000}
          rows={6}
          placeholder="Nhập câu hỏi bạn muốn giảng viên sử dụng..."
          aria-invalid={Boolean(error)}
          aria-describedby={error ? 'qb-form-error' : 'qb-form-hint'}
        />
        <div className="qb-field-meta">
          <span
            id={error ? 'qb-form-error' : 'qb-form-hint'}
            className={error ? 'qb-field-error' : ''}
          >
            {error || 'Một câu hỏi rõ ràng sẽ giúp buổi vấn đáp hiệu quả hơn.'}
          </span>
          <span>{content.length}/2000</span>
        </div>
        <div className="qb-modal__actions">
          <button
            type="button"
            className="qb-button qb-button--ghost"
            onClick={onClose}
            disabled={submitting}
          >
            Hủy
          </button>
          <button
            type="submit"
            className="qb-button qb-button--primary"
            disabled={submitting}
          >
            {submitting
              ? 'Đang lưu...'
              : mode === 'create'
                ? 'Thêm câu hỏi'
                : 'Lưu thay đổi'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

function DeleteConfirmationDialog({ question, onClose, onConfirm }) {
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function handleConfirm() {
    setSubmitting(true)
    try {
      await onConfirm()
      onClose()
    } catch {
      setError('Không thể xóa câu hỏi. Vui lòng thử lại.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      title="Xóa câu hỏi?"
      description="Thao tác này không thể hoàn tác."
      onClose={onClose}
      labelledBy="qb-delete-title"
    >
      <blockquote className="qb-delete-quote">{question.content}</blockquote>
      {error && (
        <p className="qb-field-error" role="alert">
          {error}
        </p>
      )}
      <div className="qb-modal__actions">
        <button
          type="button"
          className="qb-button qb-button--ghost"
          onClick={onClose}
          disabled={submitting}
        >
          Giữ lại
        </button>
        <button
          type="button"
          className="qb-button qb-button--danger"
          onClick={handleConfirm}
          disabled={submitting}
        >
          {submitting ? 'Đang xóa...' : 'Xóa câu hỏi'}
        </button>
      </div>
    </Modal>
  )
}

export default function QuestionBankWorkspace({
  banks,
  questions,
  isLoading = false,
  error = null,
  onRetry,
  onCreateQuestion,
  onUpdateQuestion,
  onDeleteQuestion,
}) {
  const [selectedBankId, setSelectedBankId] = useState(null)
  const [bankSearch, setBankSearch] = useState('')
  const [questionSearch, setQuestionSearch] = useState('')
  const [dialog, setDialog] = useState(null)
  const selectedBank =
    banks.find((bank) => bank.id === selectedBankId) ?? banks[0]
  const visibleBanks = banks.filter((bank) =>
    bank.name
      .toLocaleLowerCase('vi')
      .includes(bankSearch.toLocaleLowerCase('vi')),
  )
  const bankQuestions = questions.filter(
    (question) => question.questionBankId === selectedBank?.id,
  )
  const visibleQuestions = bankQuestions.filter((question) =>
    question.content
      .toLocaleLowerCase('vi')
      .includes(questionSearch.toLocaleLowerCase('vi')),
  )

  function selectBank(bankId) {
    setSelectedBankId(bankId)
    setQuestionSearch('')
    setDialog(null)
  }

  function closeDialog() {
    setDialog(null)
  }

  return (
    <div className="qb-app">
      <aside className="qb-sidebar" aria-label="Điều hướng bản xem trước">
        <div className="qb-logo">
          <span className="qb-logo__mark">
            <Icon name="layers" size={22} />
          </span>
          <span>
            AIVES<span className="qb-logo__period">.</span>
            <small>TEACHER WORKSPACE</small>
          </span>
        </div>
        <div className="qb-sidebar__section">KHÔNG GIAN LÀM VIỆC</div>
        <div className="qb-sidebar__item qb-sidebar__item--muted">
          <Icon name="grid" size={18} /> Tổng quan
        </div>
        <div className="qb-sidebar__item qb-sidebar__item--active">
          <Icon name="book" size={18} /> Ngân hàng câu hỏi
        </div>
        <div className="qb-sidebar__bottom">
          <span className="qb-avatar">GV</span>
          <span>
            <strong>Giảng viên</strong>
            <small>Bản xem trước UI</small>
          </span>
        </div>
      </aside>

      <main className="qb-main">
        <div className="qb-topbar">
          <span>
            Không gian giảng viên <Icon name="arrow" size={14} />{' '}
            <strong>Ngân hàng câu hỏi</strong>
          </span>
          <span className="qb-demo-pill">
            <span /> Dữ liệu minh họa
          </span>
        </div>
        <div className="qb-content">
          <header className="qb-page-head">
            <div>
              <p className="qb-eyebrow">QUESTION BANK / QUẢN LÝ NỘI DUNG</p>
              <h1>
                Ngân hàng câu hỏi<span>.</span>
              </h1>
              <p>Chuẩn bị những câu hỏi chất lượng cho mỗi buổi vấn đáp.</p>
            </div>
            <div className="qb-page-head__metric">
              <span>{banks.length.toString().padStart(2, '0')}</span>
              <small>NGÂN HÀNG HIỆN CÓ</small>
            </div>
          </header>

          {isLoading ? (
            <section className="qb-panel qb-state" aria-live="polite">
              <div className="qb-spinner" />
              <h2>Đang tải ngân hàng câu hỏi</h2>
              <p>Vui lòng chờ trong giây lát...</p>
            </section>
          ) : error ? (
            <section className="qb-panel qb-state" role="alert">
              <div className="qb-state__icon qb-state__icon--error">
                <Icon name="alert" size={30} />
              </div>
              <h2>Không thể tải dữ liệu</h2>
              <p>
                {typeof error === 'string'
                  ? error
                  : 'Đã có lỗi xảy ra. Vui lòng thử lại.'}
              </p>
              {onRetry && (
                <button
                  className="qb-button qb-button--primary"
                  type="button"
                  onClick={onRetry}
                >
                  Thử lại
                </button>
              )}
            </section>
          ) : banks.length === 0 ? (
            <section className="qb-panel">
              <EmptyState
                title="Chưa có ngân hàng câu hỏi"
                description="Các ngân hàng câu hỏi sẽ xuất hiện tại đây khi được tạo."
              />
            </section>
          ) : (
            <div className="qb-layout">
              <section
                className="qb-panel qb-bank-panel"
                aria-label="Danh sách ngân hàng câu hỏi"
              >
                <div className="qb-panel__heading">
                  <div>
                    <span className="qb-section-number">01 / DANH SÁCH</span>
                    <h2>
                      Ngân hàng <span className="qb-count">{banks.length}</span>
                    </h2>
                  </div>
                </div>
                <label className="qb-search">
                  <Icon name="search" size={18} />
                  <span className="qb-sr-only">Tìm ngân hàng</span>
                  <input
                    value={bankSearch}
                    onChange={(event) => setBankSearch(event.target.value)}
                    placeholder="Tìm ngân hàng..."
                  />
                </label>
                <div className="qb-bank-list">
                  {visibleBanks.length === 0 ? (
                    <p className="qb-no-results">
                      Không tìm thấy ngân hàng phù hợp.
                    </p>
                  ) : (
                    visibleBanks.map((bank, index) => {
                      const count = questions.filter(
                        (question) => question.questionBankId === bank.id,
                      ).length
                      return (
                        <button
                          key={bank.id}
                          className={`qb-bank-card ${selectedBank?.id === bank.id ? 'qb-bank-card--active' : ''}`}
                          type="button"
                          onClick={() => selectBank(bank.id)}
                          aria-pressed={selectedBank?.id === bank.id}
                        >
                          <span className="qb-bank-card__number">
                            {String(index + 1).padStart(2, '0')}
                          </span>
                          <span className="qb-bank-card__body">
                            <strong>{bank.name}</strong>
                            <span>{bank.description || 'Chưa có mô tả'}</span>
                            <small>
                              <Icon name="book" size={14} /> {count} câu hỏi
                            </small>
                          </span>
                          <Icon name="arrow" size={18} />
                        </button>
                      )
                    })
                  )}
                </div>
                <div className="qb-bank-panel__foot">
                  <Icon name="check" size={15} /> Dữ liệu theo MVP contract #7
                </div>
              </section>

              <section
                className="qb-panel qb-questions-panel"
                aria-label="Câu hỏi trong ngân hàng"
              >
                <div className="qb-panel__heading qb-panel__heading--questions">
                  <div>
                    <span className="qb-section-number">
                      02 / CHI TIẾT NGÂN HÀNG
                    </span>
                    <h2>{selectedBank.name}</h2>
                    <p>
                      {selectedBank.description || 'Ngân hàng chưa có mô tả.'}
                    </p>
                  </div>
                  <span className="qb-question-total">
                    {bankQuestions.length} CÂU HỎI
                  </span>
                </div>
                <div className="qb-questions-toolbar">
                  <label className="qb-search">
                    <Icon name="search" size={18} />
                    <span className="qb-sr-only">Tìm câu hỏi</span>
                    <input
                      value={questionSearch}
                      onChange={(event) =>
                        setQuestionSearch(event.target.value)
                      }
                      placeholder="Tìm trong câu hỏi..."
                    />
                  </label>
                  <button
                    className="qb-button qb-button--primary"
                    type="button"
                    onClick={() => setDialog({ type: 'create' })}
                  >
                    <Icon name="plus" size={17} /> Thêm câu hỏi
                  </button>
                </div>
                {visibleQuestions.length === 0 ? (
                  <EmptyState
                    title={
                      questionSearch
                        ? 'Không tìm thấy câu hỏi'
                        : 'Chưa có câu hỏi nào'
                    }
                    description={
                      questionSearch
                        ? 'Thử một từ khóa khác để tìm câu hỏi.'
                        : 'Thêm câu hỏi đầu tiên cho ngân hàng này.'
                    }
                    action={
                      !questionSearch && (
                        <button
                          className="qb-button qb-button--outline"
                          type="button"
                          onClick={() => setDialog({ type: 'create' })}
                        >
                          Thêm câu hỏi đầu tiên
                        </button>
                      )
                    }
                  />
                ) : (
                  <div className="qb-question-list">
                    {visibleQuestions.map((question, index) => (
                      <article className="qb-question" key={question.id}>
                        <div className="qb-question__index">
                          {String(index + 1).padStart(2, '0')}
                        </div>
                        <div className="qb-question__body">
                          <p>{question.content}</p>
                          <span>
                            ĐÃ THÊM{' '}
                            {dateFormatter.format(new Date(question.createdAt))}
                          </span>
                        </div>
                        <div className="qb-question__actions">
                          <button
                            className="qb-icon-button"
                            type="button"
                            aria-label={`Sửa câu hỏi ${index + 1}`}
                            title="Sửa câu hỏi"
                            onClick={() =>
                              setDialog({ type: 'edit', question })
                            }
                          >
                            <Icon name="edit" size={17} />
                          </button>
                          <button
                            className="qb-icon-button qb-icon-button--danger"
                            type="button"
                            aria-label={`Xóa câu hỏi ${index + 1}`}
                            title="Xóa câu hỏi"
                            onClick={() =>
                              setDialog({ type: 'delete', question })
                            }
                          >
                            <Icon name="trash" size={17} />
                          </button>
                        </div>
                      </article>
                    ))}
                  </div>
                )}
                <div className="qb-questions-panel__foot">
                  Hiển thị {visibleQuestions.length} / {bankQuestions.length}{' '}
                  câu hỏi <span>•</span> Sắp xếp theo ngày tạo
                </div>
              </section>
            </div>
          )}
          <footer className="qb-footer">
            <span>© 2026 AIVES · AI-powered Viva Exam System</span>
            <span>Bản thiết kế UI/UX · Issue #11</span>
          </footer>
        </div>
      </main>

      {dialog?.type === 'create' && (
        <QuestionFormDialog
          mode="create"
          bank={selectedBank}
          onClose={closeDialog}
          onSubmit={(request) => onCreateQuestion(selectedBank.id, request)}
        />
      )}
      {dialog?.type === 'edit' && (
        <QuestionFormDialog
          mode="edit"
          bank={selectedBank}
          question={dialog.question}
          onClose={closeDialog}
          onSubmit={(request) =>
            onUpdateQuestion(selectedBank.id, dialog.question.id, request)
          }
        />
      )}
      {dialog?.type === 'delete' && (
        <DeleteConfirmationDialog
          question={dialog.question}
          onClose={closeDialog}
          onConfirm={() =>
            onDeleteQuestion(selectedBank.id, dialog.question.id)
          }
        />
      )}
    </div>
  )
}
