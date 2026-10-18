import { useCallback, useEffect, useState } from 'react'
import * as sessionApi from '../../api/examSessionApi.js'
import * as bankApi from '../../api/questionBankApi.js'
import QuestionSelectionCard from '../wf1-preparation/QuestionSelectionCard.jsx'
import ReadinessCard from '../wf1-preparation/ReadinessCard.jsx'
import { describePreparationError } from '../wf1-preparation/feedback.js'
import '../wf1-preparation/wf1Preparation.css'

export default function SessionPreparationPanel({
  session,
  onPublished,
  onAccessError,
}) {
  const [questions, setQuestions] = useState([])
  const [selected, setSelected] = useState([])
  const [missing, setMissing] = useState(['selection'])
  const [withoutKeyPoints, setWithoutKeyPoints] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([
      bankApi.getQuestions(session.questionBankId, controller.signal),
      sessionApi.getExamSessionQuestions(session.id, controller.signal),
      bankApi
        .getRubric(session.questionBankId, controller.signal)
        .catch((failure) => {
          if (failure.status === 404) return null
          throw failure
        }),
      bankApi
        .getRubricCriteria(session.questionBankId, controller.signal)
        .catch((failure) => {
          if (failure.status === 404) return []
          throw failure
        }),
    ])
      .then(async ([bankQuestions, selectedQuestions, rubric, criteria]) => {
        const pointLists = await Promise.all(
          selectedQuestions.map((item) =>
            bankApi.getExpectedKeyPoints(
              session.questionBankId,
              item.questionId,
              controller.signal,
            ),
          ),
        )
        if (controller.signal.aborted) return
        const unprepared = selectedQuestions
          .filter((_, index) => pointLists[index].length === 0)
          .map((item) => item.questionId)
        const gaps = []
        if (selectedQuestions.length === 0) gaps.push('selection')
        if (unprepared.length > 0) gaps.push('keyPoints')
        if (!rubric) gaps.push('rubric')
        else if (criteria.length === 0) gaps.push('rubricCriteria')
        else if (
          Math.abs(criteria.reduce((sum, item) => sum + item.weight, 0) - 1) >
          0.00001
        )
          gaps.push('rubricWeight')
        setQuestions(bankQuestions)
        setSelected(selectedQuestions)
        setMissing(gaps)
        setWithoutKeyPoints(unprepared)
        setError(null)
      })
      .catch((failure) => {
        if (!controller.signal.aborted) {
          onAccessError(failure)
          setError(failure)
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [session.id, session.questionBankId, revision, onAccessError])

  const refresh = useCallback(() => {
    setLoading(true)
    setRevision((value) => value + 1)
  }, [])

  async function change(questionIds) {
    try {
      const result = await sessionApi.replaceExamSessionQuestions(
        session.id,
        questionIds,
      )
      setSelected(result)
      refresh()
    } catch (failure) {
      onAccessError(failure)
      throw failure
    }
  }

  async function publish() {
    try {
      const result = await sessionApi.updateExamSession(session.id, {
        title: session.title,
        questionBankId: session.questionBankId,
        status: 'Open',
      })
      onPublished(result)
    } catch (failure) {
      onAccessError(failure)
      refresh()
      throw failure
    }
  }

  return (
    <div className="wf-live" aria-label={`Chuẩn bị phiên ${session.title}`}>
      <div className="wf-live__grid">
        <QuestionSelectionCard
          questions={questions}
          selectedQuestionIds={selected.map((item) => item.questionId)}
          selectedQuestions={selected}
          status={session.status}
          isLoading={loading}
          error={error}
          onRetry={refresh}
          onChange={change}
        />
        <ReadinessCard
          status={session.status}
          missing={loading ? ['selection'] : missing}
          questionIdsWithoutKeyPoints={withoutKeyPoints}
          serverError={error}
          onPublish={publish}
        />
      </div>
      {error && (
        <p role="alert">
          {describePreparationError(error, 'Không thể tải phần chuẩn bị đề.')}
        </p>
      )}
    </div>
  )
}
