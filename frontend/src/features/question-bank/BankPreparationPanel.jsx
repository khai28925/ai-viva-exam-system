import { useCallback, useEffect, useState } from 'react'
import * as api from '../../api/questionBankApi.js'
import ExpectedKeyPointsCard from '../wf1-preparation/ExpectedKeyPointsCard.jsx'
import RubricCard from '../wf1-preparation/RubricCard.jsx'
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
  const [rubric, setRubric] = useState(null)
  const [criteria, setCriteria] = useState([])
  const [pointLoading, setPointLoading] = useState(Boolean(questions[0]))
  const [rubricLoading, setRubricLoading] = useState(true)
  const [pointError, setPointError] = useState(null)
  const [rubricError, setRubricError] = useState(null)
  const [pointRevision, setPointRevision] = useState(0)
  const [rubricRevision, setRubricRevision] = useState(0)

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

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([
      api.getRubric(bank.id, controller.signal).catch((error) => {
        if (error.status === 404) return null
        throw error
      }),
      api.getRubricCriteria(bank.id, controller.signal).catch((error) => {
        if (error.status === 404) return []
        throw error
      }),
    ])
      .then(([nextRubric, nextCriteria]) => {
        if (!controller.signal.aborted) {
          setRubric(nextRubric)
          setCriteria(nextCriteria)
          setRubricError(null)
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          onAccessError(error)
          setRubricError(error)
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setRubricLoading(false)
      })
    return () => controller.abort()
  }, [bank.id, rubricRevision, onAccessError])

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

  const changeRubric = useCallback(
    async (operation) => {
      try {
        await operation()
        setRubricRevision((value) => value + 1)
      } catch (error) {
        onAccessError(error)
        throw error
      }
    },
    [onAccessError],
  )

  return (
    <section className="wf-live" aria-label="Chuẩn bị ngân hàng câu hỏi">
      <h2>Chuẩn bị đề thi</h2>
      <p>
        Chọn từng câu hỏi để viết ý chính kỳ vọng; thiết lập rubric cho ngân
        hàng trước khi mở phiên.
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
        <RubricCard
          rubric={rubric}
          criteria={criteria}
          isLoading={rubricLoading}
          error={rubricError}
          onRetry={() => {
            setRubricLoading(true)
            setRubricRevision((value) => value + 1)
          }}
          onCreateRubric={(request) =>
            changeRubric(() => api.createRubric(bank.id, request))
          }
          onUpdateRubric={(request) =>
            changeRubric(() => api.updateRubric(bank.id, request))
          }
          onDeleteRubric={() => changeRubric(() => api.deleteRubric(bank.id))}
          onCreateCriterion={(request) =>
            changeRubric(() => api.createRubricCriterion(bank.id, request))
          }
          onUpdateCriterion={(id, request) =>
            changeRubric(() => api.updateRubricCriterion(bank.id, id, request))
          }
          onDeleteCriterion={(id) =>
            changeRubric(() => api.deleteRubricCriterion(bank.id, id))
          }
        />
      </div>
    </section>
  )
}
