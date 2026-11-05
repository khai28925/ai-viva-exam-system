import { useCallback, useEffect, useState } from 'react'
import * as sessionApi from '../../api/examSessionApi.js'
import * as bankApi from '../../api/questionBankApi.js'
import * as wf1Api from '../../api/wf1Api.js'
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
    const rubricSetPromise = wf1Api
      .getSubjects({ signal: controller.signal })
      .then((subjects) => {
        const subject = subjects.find((item) =>
          item.questionBanks.some((bank) => bank.id === session.questionBankId),
        )
        return subject
          ? wf1Api.getSubjectWorkspace(subject.id, {
              signal: controller.signal,
            })
          : null
      })
    Promise.all([
      bankApi.getQuestions(session.questionBankId, controller.signal),
      sessionApi.getExamSessionQuestions(session.id, controller.signal),
      rubricSetPromise,
    ])
      .then(async ([bankQuestions, selectedQuestions, workspace]) => {
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
        const rubrics = workspace?.approvedRubricSet?.rubrics ?? []
        if (!workspace?.approvedRubricSet) gaps.push('rubric')
        else if (
          rubrics.length === 0 ||
          rubrics.some((rubric) => rubric.criteria.length === 0)
        )
          gaps.push('rubricCriteria')
        else if (
          Math.abs(
            rubrics.reduce((sum, rubric) => sum + rubric.weightPercent, 0) -
              100,
          ) > 0.001
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
