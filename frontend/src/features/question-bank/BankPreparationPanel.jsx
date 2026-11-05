import { useCallback, useEffect, useState } from 'react'
import * as api from '../../api/questionBankApi.js'
import ExpectedKeyPointsCard from '../wf1-preparation/ExpectedKeyPointsCard.jsx'
import SubjectRubricCard from './SubjectRubricCard.jsx'
import '../wf1-preparation/wf1Preparation.css'

export default function BankPreparationPanel({
  bank,
  questions,
  onAccessError,
}) {
  const [questionId, setQuestionId] = useState(questions[0]?.id ?? null)
  const question =
    questions.find((item) => item.id === questionId) ?? questions[0]
  const [points, setPoints] = useState([])
  const [pointLoading, setPointLoading] = useState(Boolean(questions[0]))
  const [pointError, setPointError] = useState(null)
  const [pointRevision, setPointRevision] = useState(0)

  useEffect(() => {
    if (!question) return undefined
    const controller = new AbortController()
    api
      .getExpectedKeyPoints(bank.id, question.id, controller.signal)
      .then((items) => {
        if (!controller.signal.aborted) {
          setPoints(items)
          setPointError(null)
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          onAccessError(error)
          setPointError(error)
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setPointLoading(false)
      })
    return () => controller.abort()
  }, [bank.id, question?.id, pointRevision, onAccessError, question])

  const changePoint = useCallback(
    async (operation) => {
      try {
        await operation()
        setPointRevision((value) => value + 1)
      } catch (error) {
        onAccessError(error)
        throw error
      }
    },
    [onAccessError],
  )

  return (
    <section className="wf-live" aria-label="Chuẩn bị bộ câu hỏi">
      <h2>Chuẩn bị đề thi</h2>
      <p>
        Chọn từng câu hỏi để viết ý chính kỳ vọng; bộ rubric chấm điểm được
        duyệt một lần cho cả môn học trước khi mở phiên.
      </p>
      <div
        className="wf-live__questions"
        aria-label="Chọn câu hỏi để soạn ý chính"
      >
        {questions.map((item) => (
          <button
            key={item.id}
            type="button"
            aria-pressed={question?.id === item.id}
            onClick={() => {
              setQuestionId(item.id)
              setPointLoading(true)
              setPointError(null)
            }}
          >
            {item.content}
          </button>
        ))}
      </div>
      <div className="wf-live__grid">
        <ExpectedKeyPointsCard
          key={question?.id ?? 'empty'}
          question={question}
          points={points}
          isLoading={pointLoading}
          error={pointError}
          onRetry={() => {
            setPointLoading(true)
            setPointRevision((value) => value + 1)
          }}
          onCreate={(request) =>
            changePoint(() =>
              api.createExpectedKeyPoint(bank.id, question.id, request),
            )
          }
          onUpdate={(id, request) =>
            changePoint(() =>
              api.updateExpectedKeyPoint(bank.id, question.id, id, request),
            )
          }
          onDelete={(id) =>
            changePoint(() =>
              api.deleteExpectedKeyPoint(bank.id, question.id, id),
            )
          }
        />
        <SubjectRubricCard bank={bank} onAccessError={onAccessError} />
      </div>
    </section>
  )
}
