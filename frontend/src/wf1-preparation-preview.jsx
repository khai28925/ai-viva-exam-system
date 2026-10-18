import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import ExpectedKeyPointsCard from './features/wf1-preparation/ExpectedKeyPointsCard.jsx'
import RubricCard from './features/wf1-preparation/RubricCard.jsx'
import QuestionSelectionCard from './features/wf1-preparation/QuestionSelectionCard.jsx'
import ReadinessCard from './features/wf1-preparation/ReadinessCard.jsx'
import StudentSessionsCard from './features/wf1-preparation/StudentSessionsCard.jsx'
import {
  getReadiness,
  previewBanks,
  previewCriteria,
  previewPoints,
  previewQuestions,
  previewRubric,
  previewSession,
} from './features/wf1-preparation/mockData.js'
import './styles/index.css'
import './features/wf1-preparation/wf1Preparation.css'

const initialScenario =
  new URLSearchParams(window.location.search).get('state') ?? 'default'
const scenarios = [
  ['default', 'Bản đầy đủ'],
  ['loading', 'Đang tải'],
  ['empty', 'Dữ liệu rỗng'],
  ['error', 'Lỗi tải dữ liệu'],
  ['forbidden', 'Không có quyền (403)'],
  ['conflict', 'Chưa đủ điều kiện (409)'],
  ['student', 'Góc nhìn sinh viên'],
  ['attempt', 'Sinh viên đã vào thi'],
]

function newId() {
  return crypto.randomUUID()
}

function snapshotsFor(ids) {
  return ids.map((id, displayOrder) => ({
    questionId: id,
    contentSnapshot: previewQuestions.find((item) => item.id === id).content,
    displayOrder,
    isRequired: true,
  }))
}

const defaultSelection = [previewQuestions[0].id, previewQuestions[1].id]
const conflictSelection = [previewQuestions[0].id, previewQuestions[2].id]

function previewAttempt(selectedQuestions) {
  return {
    id: newId(),
    examSessionId: previewSession.id,
    studentId: 'd6147213-75a0-4fd3-9f7a-0e4014fa6222',
    startedAt: new Date().toISOString(),
    questions: selectedQuestions.map((item) => ({
      id: item.questionId,
      content: item.contentSnapshot,
    })),
  }
}

export function Wf1PreparationPreview() {
  const [scenario, setScenario] = useState(initialScenario)
  const [mode, setMode] = useState(
    initialScenario === 'student' || initialScenario === 'attempt'
      ? 'student'
      : 'lecturer',
  )
  const [bankId, setBankId] = useState(previewBanks[0].id)
  const [questionId, setQuestionId] = useState(previewQuestions[0].id)
  const [points, setPoints] = useState(previewPoints)
  const [rubrics, setRubrics] = useState([previewRubric])
  const [criteria, setCriteria] = useState(previewCriteria)
  const [selectedQuestionIds, setSelectedQuestionIds] = useState(
    initialScenario === 'conflict' ? conflictSelection : defaultSelection,
  )
  const [selectedQuestions, setSelectedQuestions] = useState(
    snapshotsFor(
      initialScenario === 'conflict' ? conflictSelection : defaultSelection,
    ),
  )
  const [session, setSession] = useState({
    ...previewSession,
    status: ['student', 'attempt'].includes(initialScenario) ? 'Open' : 'Draft',
  })
  const [attempt, setAttempt] = useState(
    initialScenario === 'attempt'
      ? previewAttempt(snapshotsFor(defaultSelection))
      : null,
  )

  const banks = scenario === 'empty' ? [] : previewBanks
  const bank = banks.find((item) => item.id === bankId) ?? null
  const questions = bank
    ? previewQuestions.filter((item) => item.questionBankId === bank.id)
    : []
  const question =
    questions.find((item) => item.id === questionId) ?? questions[0] ?? null
  const questionPoints = points.filter(
    (item) => item.questionId === question?.id,
  )
  const rubric =
    rubrics.find((item) => item.questionBankId === bank?.id) ?? null
  const bankCriteria = criteria.filter((item) => item.rubricId === rubric?.id)
  const readiness = getReadiness({
    selectedQuestionIds: bank ? selectedQuestionIds : [],
    points,
    rubric,
    criteria: bankCriteria,
  })
  const loading = scenario === 'loading'
  const loadError =
    scenario === 'error'
      ? { status: 503 }
      : scenario === 'forbidden'
        ? { status: 403 }
        : null
  const conflictError =
    scenario === 'conflict'
      ? { status: 409, problem: { code: 'session_not_ready', ...readiness } }
      : null

  function changeScenario(next) {
    setScenario(next)
    setBankId(previewBanks[0].id)
    setQuestionId(previewQuestions[0].id)
    if (next === 'student' || next === 'attempt') {
      setMode('student')
      setSession((current) => ({
        ...current,
        ...previewSession,
        status: 'Open',
      }))
      setSelectedQuestionIds(defaultSelection)
      setSelectedQuestions(snapshotsFor(defaultSelection))
      setAttempt(
        next === 'attempt'
          ? previewAttempt(snapshotsFor(defaultSelection))
          : null,
      )
    } else {
      setMode('lecturer')
      setSession((current) => ({
        ...current,
        ...previewSession,
        status: 'Draft',
      }))
      const ids = next === 'conflict' ? conflictSelection : defaultSelection
      setSelectedQuestionIds(ids)
      setSelectedQuestions(snapshotsFor(ids))
      setAttempt(null)
    }
  }

  function selectBank(nextId) {
    const nextBank = previewBanks.find((item) => item.id === nextId)
    setBankId(nextId)
    setQuestionId(
      previewQuestions.find((item) => item.questionBankId === nextId)?.id ?? '',
    )
    setSelectedQuestionIds([])
    setSelectedQuestions([])
    setSession((current) => ({
      ...current,
      questionBankId: nextId,
      questionBankName: nextBank.name,
      status: 'Draft',
    }))
  }

  function assertQuestionEditable() {
    if (
      session.status !== 'Draft' &&
      selectedQuestionIds.includes(question.id)
    ) {
      throw { status: 409, problem: { code: 'question_preparation_locked' } }
    }
  }

  function assertRubricEditable() {
    if (session.status !== 'Draft') {
      throw { status: 409, problem: { code: 'rubric_preparation_locked' } }
    }
  }

  function addPoint(request) {
    assertQuestionEditable()
    const current = points.filter((point) => point.questionId === question.id)
    setPoints((items) => [
      ...items,
      {
        id: newId(),
        questionId: question.id,
        content: request.content,
        displayOrder:
          Math.max(-1, ...current.map((point) => point.displayOrder)) + 1,
        createdAt: new Date().toISOString(),
      },
    ])
  }

  function updatePoint(id, request) {
    assertQuestionEditable()
    setPoints((items) =>
      items.map((point) =>
        point.id === id ? { ...point, content: request.content } : point,
      ),
    )
  }

  function deletePoint(id) {
    assertQuestionEditable()
    setPoints((items) => items.filter((point) => point.id !== id))
  }

  function createRubric(request) {
    assertRubricEditable()
    setRubrics((items) => [
      ...items,
      {
        id: newId(),
        questionBankId: bank.id,
        ...request,
        createdAt: new Date().toISOString(),
      },
    ])
  }

  function updateRubric(request) {
    assertRubricEditable()
    setRubrics((items) =>
      items.map((item) =>
        item.id === rubric.id ? { ...item, ...request } : item,
      ),
    )
  }

  function deleteRubric() {
    assertRubricEditable()
    setRubrics((items) => items.filter((item) => item.id !== rubric.id))
  }

  function createCriterion(request) {
    assertRubricEditable()
    if (bankCriteria.some((item) => item.name === request.name)) {
      throw {
        status: 409,
        problem: {
          code: 'criterion_name_exists',
          detail: 'Tên tiêu chí đã tồn tại.',
        },
      }
    }
    setCriteria((items) => [
      ...items,
      {
        id: newId(),
        rubricId: rubric.id,
        ...request,
        createdAt: new Date().toISOString(),
      },
    ])
  }

  function updateCriterion(id, request) {
    assertRubricEditable()
    if (
      bankCriteria.some((item) => item.id !== id && item.name === request.name)
    ) {
      throw {
        status: 409,
        problem: {
          code: 'criterion_name_exists',
          detail: 'Tên tiêu chí đã tồn tại.',
        },
      }
    }
    setCriteria((items) =>
      items.map((item) => (item.id === id ? { ...item, ...request } : item)),
    )
  }

  function deleteCriterion(id) {
    assertRubricEditable()
    setCriteria((items) => items.filter((item) => item.id !== id))
  }

  function changeSelection(ids) {
    if (session.status !== 'Draft') {
      throw { status: 409, problem: { code: 'selection_locked' } }
    }
    setSelectedQuestionIds(ids)
    setSelectedQuestions(snapshotsFor(ids))
  }

  function publish() {
    const current = getReadiness({
      selectedQuestionIds,
      points,
      rubric,
      criteria: bankCriteria,
    })
    if (current.missing.length > 0) {
      throw { status: 409, problem: { code: 'session_not_ready', ...current } }
    }
    setSession((item) => ({ ...item, status: 'Open' }))
  }

  function join() {
    if (session.status !== 'Open') {
      throw { status: 409, problem: { code: 'session_not_open' } }
    }
    const joined = attempt ?? previewAttempt(selectedQuestions)
    setAttempt(joined)
    return joined
  }

  return (
    <div className="wf-app">
      <aside className="wf-sidebar">
        <div className="wf-brand">
          <span className="wf-brand__mark">◇</span>
          <span>
            AIVES<b>.</b>
            <small>VIVA WORKSPACE</small>
          </span>
        </div>
        <div className="wf-sidebar__label">KHÔNG GIAN LÀM VIỆC</div>
        <div className="wf-sidebar__active">
          ▤ &nbsp;{' '}
          {mode === 'lecturer' ? 'Chuẩn bị phiên thi' : 'Phiên thi của bạn'}
        </div>
        {mode === 'lecturer' ? (
          <ol className="wf-steps">
            <li>
              01 <span>Ngân hàng & câu hỏi</span>
            </li>
            <li>
              02 <span>Ý chính kỳ vọng</span>
            </li>
            <li>
              03 <span>Rubric & tiêu chí</span>
            </li>
            <li>
              04 <span>Chọn câu hỏi</span>
            </li>
            <li>
              05 <span>Mở phiên & vào thi</span>
            </li>
          </ol>
        ) : (
          <ol className="wf-steps">
            <li>
              01 <span>Phiên đang mở</span>
            </li>
            <li>
              02 <span>Vào thi</span>
            </li>
            <li>
              03 <span>Xem câu hỏi</span>
            </li>
          </ol>
        )}
        <div className="wf-sidebar__foot">
          <span>{mode === 'lecturer' ? 'GV' : 'SV'}</span>
          <div>
            <strong>{mode === 'lecturer' ? 'Giảng viên' : 'Sinh viên'}</strong>
            <small>Bản xem trước UI/UX</small>
          </div>
        </div>
      </aside>
      <main className="wf-main">
        <div className="wf-topbar">
          <span>
            Không gian {mode === 'lecturer' ? 'giảng viên' : 'sinh viên'}{' '}
            <b>›</b> WF1 /{' '}
            {mode === 'lecturer' ? 'Chuẩn bị phiên thi' : 'Vào thi'}
          </span>
          <div className="wf-topbar__right">
            <label htmlFor="wf-scenario">Trạng thái demo</label>
            <select
              id="wf-scenario"
              value={scenario}
              onChange={(event) => changeScenario(event.target.value)}
            >
              {scenarios.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
            <span className="wf-demo-pill">● MOCK DATA</span>
          </div>
        </div>
        <div className="wf-content">
          <div
            className="wf-view-switch"
            role="group"
            aria-label="Chọn vai trò xem trước"
          >
            <button
              type="button"
              className={mode === 'lecturer' ? 'is-active' : ''}
              onClick={() => setMode('lecturer')}
            >
              Giảng viên
            </button>
            <button
              type="button"
              className={mode === 'student' ? 'is-active' : ''}
              onClick={() => setMode('student')}
            >
              Sinh viên
            </button>
          </div>
          {mode === 'student' ? (
            <StudentSessionsCard
              sessions={session.status === 'Open' ? [session] : []}
              attempt={attempt}
              isLoading={loading}
              error={loadError}
              onRetry={() => setScenario('default')}
              onJoin={join}
              onBack={() => setAttempt(null)}
            />
          ) : (
            <>
              <header className="wf-hero">
                <div>
                  <span className="wf-kicker">
                    WORKFLOW 01 / LATEST UI DESIGN
                  </span>
                  <h1>
                    Chuẩn bị phiên thi<span>.</span>
                  </h1>
                  <p>
                    Từ ngân hàng câu hỏi đến phiên vấn đáp sẵn sàng cho sinh
                    viên.
                  </p>
                </div>
                <div className="wf-hero__stat">
                  <strong>
                    {5 - readiness.missing.length}
                    <span>/05</span>
                  </strong>
                  <small>ĐIỀU KIỆN ĐÃ ĐẠT</small>
                </div>
              </header>
              <div className="wf-board">
                <section
                  className="wf-card wf-card--bank"
                  aria-labelledby="wf-bank-title"
                >
                  <div className="wf-card__heading">
                    <div>
                      <span className="wf-kicker">01–02 / NỘI DUNG GỐC</span>
                      <h2 id="wf-bank-title">Ngân hàng & câu hỏi</h2>
                      <p>Chọn ngân hàng và một câu hỏi để biên soạn ý chính.</p>
                    </div>
                  </div>
                  {banks.length === 0 ? (
                    <p className="wf-empty">
                      Chưa có ngân hàng câu hỏi. Tạo ngân hàng trước khi chuẩn
                      bị phiên.
                    </p>
                  ) : (
                    <>
                      <label
                        className="wf-select-label"
                        htmlFor="wf-bank-select"
                      >
                        Ngân hàng đang sử dụng
                      </label>
                      <select
                        id="wf-bank-select"
                        value={bankId}
                        onChange={(event) => selectBank(event.target.value)}
                        disabled={session.status !== 'Draft'}
                      >
                        {banks.map((item) => (
                          <option key={item.id} value={item.id}>
                            {item.name}
                          </option>
                        ))}
                      </select>
                      <p className="wf-bank-description">
                        {bank?.description || 'Chưa có mô tả ngân hàng.'}
                      </p>
                      <div className="wf-question-rail">
                        <span>{questions.length} CÂU HỎI LOẠI BANK</span>
                        {questions.length === 0 ? (
                          <p className="wf-empty">
                            Ngân hàng này chưa có câu hỏi.
                          </p>
                        ) : (
                          questions.map((item, index) => (
                            <button
                              type="button"
                              key={item.id}
                              className={
                                question?.id === item.id ? 'is-selected' : ''
                              }
                              onClick={() => setQuestionId(item.id)}
                            >
                              <small>
                                {String(index + 1).padStart(2, '0')}
                              </small>
                              <span>{item.content}</span>
                              <b>›</b>
                            </button>
                          ))
                        )}
                      </div>
                    </>
                  )}
                </section>
                <ExpectedKeyPointsCard
                  key={question?.id ?? 'none'}
                  question={question}
                  points={questionPoints}
                  isLoading={loading}
                  error={loadError}
                  onRetry={() => setScenario('default')}
                  onCreate={addPoint}
                  onUpdate={updatePoint}
                  onDelete={deletePoint}
                />
                <RubricCard
                  key={bank?.id ?? 'none'}
                  rubric={rubric}
                  criteria={bankCriteria}
                  isLoading={loading}
                  error={loadError}
                  onRetry={() => setScenario('default')}
                  onCreateRubric={createRubric}
                  onUpdateRubric={updateRubric}
                  onDeleteRubric={deleteRubric}
                  onCreateCriterion={createCriterion}
                  onUpdateCriterion={updateCriterion}
                  onDeleteCriterion={deleteCriterion}
                />
                <QuestionSelectionCard
                  questions={questions}
                  selectedQuestionIds={bank ? selectedQuestionIds : []}
                  selectedQuestions={bank ? selectedQuestions : []}
                  status={session.status}
                  isLoading={loading}
                  error={loadError}
                  onRetry={() => setScenario('default')}
                  onChange={changeSelection}
                />
                <ReadinessCard
                  status={session.status}
                  missing={readiness.missing}
                  questionIdsWithoutKeyPoints={
                    readiness.questionIdsWithoutKeyPoints
                  }
                  serverError={conflictError}
                  onPublish={publish}
                />
              </div>
            </>
          )}
        </div>
      </main>
    </div>
  )
}

createRoot(document.getElementById('root')).render(<Wf1PreparationPreview />)
