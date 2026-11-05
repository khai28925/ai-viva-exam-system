import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import SessionLayout from '../auth/SessionLayout.jsx'
import {
  approveRubricSet,
  createSubject,
  createSubjectQuestionBank,
  deleteStudyDocument,
  generateWf1Content,
  getSubjects,
  getSubjectWorkspace,
  reindexStudyDocument,
  reviewQuestionCandidate,
  saveDraftRubrics,
  updateQuestionCandidate,
  uploadStudyDocument,
} from '../../api/wf1Api.js'
import './wf1-ai.css'

const MAX_RUBRICS = 10
const MAX_CRITERIA = 10

// Stable React keys for editable rows: names change while typing, so they cannot be keys.
let nextRowKey = 0
const rowKey = () => `row-${nextRowKey++}`

function indexDescription(document) {
  switch (document.indexStatus) {
    case 'Indexed':
      return `${document.pageCount} trang · ${document.chunkCount} đoạn · ${
        document.semanticSearch ? 'tìm kiếm ngữ nghĩa' : 'tìm theo từ khóa'
      }`
    case 'NoText':
      return 'PDF scan không có lớp chữ · gửi nguyên văn cho AI'
    case 'Failed':
      return 'Không đọc được nội dung · gửi nguyên văn cho AI'
    default:
      return 'Chờ lập chỉ mục'
  }
}

function pageLabel(source) {
  if (!source.pageStart) return 'toàn bộ tài liệu'
  return source.pageStart === source.pageEnd
    ? `trang ${source.pageStart}`
    : `trang ${source.pageStart}–${source.pageEnd}`
}

const bloomLabels = {
  Remember: 'Nhớ',
  Understand: 'Hiểu',
  Apply: 'Vận dụng',
  Analyze: 'Phân tích',
}

function errorMessage(error) {
  if (error?.problem?.extensions?.code === 'ai_unavailable') {
    return 'Dịch vụ AI chưa sẵn sàng hoặc tạm thời không phản hồi. Vui lòng thử lại sau hoặc báo quản trị viên.'
  }
  return error?.message || 'Không thể hoàn tất thao tác. Vui lòng thử lại.'
}

function CandidateCard({ subjectId, candidate, onChanged, setNotice }) {
  const [draft, setDraft] = useState({
    content: candidate.content,
    bloomLevel: candidate.bloomLevel,
    difficultyLevel: candidate.difficultyLevel,
    rationale: candidate.rationale,
    keyPoints: candidate.expectedKeyPoints.join('\n'),
  })
  const [busy, setBusy] = useState(false)
  const pending = candidate.status === 'Pending'

  async function save() {
    setBusy(true)
    try {
      await updateQuestionCandidate(subjectId, candidate.id, {
        content: draft.content,
        bloomLevel: draft.bloomLevel,
        difficultyLevel: Number(draft.difficultyLevel),
        rationale: draft.rationale,
        expectedKeyPoints: draft.keyPoints
          .split('\n')
          .map((item) => item.trim())
          .filter(Boolean),
      })
      setNotice('Đã lưu nội dung câu hỏi nháp.')
      await onChanged()
    } catch (error) {
      setNotice(errorMessage(error), true)
    } finally {
      setBusy(false)
    }
  }

  async function review(approve) {
    setBusy(true)
    try {
      await reviewQuestionCandidate(subjectId, candidate.id, approve)
      setNotice(
        approve
          ? 'Đã duyệt, câu hỏi đã được đưa vào bộ chính thức.'
          : 'Đã loại câu hỏi AI đề xuất.',
      )
      await onChanged()
    } catch (error) {
      setNotice(errorMessage(error), true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <article
      className={`wf1-candidate wf1-candidate--${candidate.status.toLowerCase()}`}
    >
      <div className="wf1-card-heading">
        <span className="wf1-pill">{bloomLabels[draft.bloomLevel]}</span>
        <span className="wf1-pill">Độ khó {draft.difficultyLevel}/5</span>
        {candidate.topic && (
          <span className="wf1-pill">Chủ đề: {candidate.topic}</span>
        )}
        <span className="wf1-status">{candidate.status}</span>
      </div>
      {candidate.sources?.length > 0 && (
        <div className="wf1-sources">
          <strong>Căn cứ trong tài liệu</strong>
          <ul>
            {candidate.sources.map((source, index) => (
              <li key={`${source.fileName}-${source.pageStart}-${index}`}>
                <span>
                  {source.fileName} · {pageLabel(source)}
                </span>
                <q>{source.excerpt}</q>
              </li>
            ))}
          </ul>
        </div>
      )}
      <label>
        Nội dung câu hỏi
        <textarea
          value={draft.content}
          disabled={!pending || busy}
          onChange={(event) =>
            setDraft((current) => ({ ...current, content: event.target.value }))
          }
        />
      </label>
      <div className="wf1-fields wf1-fields--compact">
        <label>
          Mức Bloom
          <select
            value={draft.bloomLevel}
            disabled={!pending || busy}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                bloomLevel: event.target.value,
              }))
            }
          >
            {Object.entries(bloomLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Độ khó
          <select
            value={draft.difficultyLevel}
            disabled={!pending || busy}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                difficultyLevel: event.target.value,
              }))
            }
          >
            {[1, 2, 3, 4, 5].map((level) => (
              <option key={level} value={level}>
                {level}/5
              </option>
            ))}
          </select>
        </label>
      </div>
      <label>
        Lý do AI phân loại
        <textarea
          className="wf1-textarea--small"
          value={draft.rationale}
          disabled={!pending || busy}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              rationale: event.target.value,
            }))
          }
        />
      </label>
      <label>
        Ý chính kỳ vọng (mỗi dòng một ý)
        <textarea
          className="wf1-textarea--small"
          value={draft.keyPoints}
          disabled={!pending || busy}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              keyPoints: event.target.value,
            }))
          }
        />
      </label>
      {pending && (
        <div className="wf1-actions">
          <button
            type="button"
            className="wf1-button wf1-button--ghost"
            onClick={save}
            disabled={busy}
          >
            Lưu chỉnh sửa
          </button>
          <button
            type="button"
            className="wf1-button wf1-button--danger"
            onClick={() => review(false)}
            disabled={busy}
          >
            Loại bỏ
          </button>
          <button
            type="button"
            className="wf1-button wf1-button--primary"
            onClick={() => review(true)}
            disabled={busy}
          >
            Duyệt vào bộ câu hỏi
          </button>
        </div>
      )}
    </article>
  )
}

const emptyCriterion = () => ({
  key: rowKey(),
  name: '',
  description: '',
  maxScore: 10,
})

function toEditableRubrics(set) {
  return (set?.rubrics || []).map((rubric) => ({
    key: rowKey(),
    name: rubric.name,
    description: rubric.description || '',
    weightPercent: rubric.weightPercent,
    criteria: rubric.criteria.map((criterion) => ({
      key: rowKey(),
      name: criterion.name,
      description: criterion.description || '',
      maxScore: criterion.maxScore,
    })),
  }))
}

function toRubricPayload(rubrics) {
  return rubrics.map((rubric) => ({
    name: rubric.name,
    description: rubric.description,
    weightPercent: Number(rubric.weightPercent),
    criteria: rubric.criteria.map((criterion) => ({
      name: criterion.name,
      description: criterion.description,
      maxScore: Number(criterion.maxScore),
    })),
  }))
}

export default function Wf1PreparationPage() {
  const [searchParams] = useSearchParams()
  const [subjects, setSubjects] = useState([])
  // The question-bank page links here with ?subject= to edit that subject's rubric set.
  const [subjectId, setSubjectId] = useState(
    () => searchParams.get('subject') ?? '',
  )
  const [workspace, setWorkspace] = useState(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState(null)
  const [subjectForm, setSubjectForm] = useState({
    name: '',
    description: '',
    questionBankName: '',
  })
  const [bankForm, setBankForm] = useState({ name: '', description: '' })
  const [creatingBank, setCreatingBank] = useState(false)
  const [questionCount, setQuestionCount] = useState(5)
  const [topic, setTopic] = useState('')
  // null = automatic: only propose a rubric set when the subject has none yet.
  const [regenerateRubrics, setRegenerateRubrics] = useState(null)
  const [bankId, setBankId] = useState('')
  const [rubrics, setRubrics] = useState([])
  const includeRubrics =
    regenerateRubrics ??
    !(workspace?.draftRubricSet || workspace?.approvedRubricSet)

  function notify(text, error = false) {
    setMessage({ text, error })
  }

  async function loadSubjects(preferredId) {
    const result = await getSubjects()
    setSubjects(result)
    setSubjectId((current) => preferredId || current || result[0]?.id || '')
  }

  async function loadWorkspace() {
    if (!subjectId) {
      setWorkspace(null)
      return
    }
    const result = await getSubjectWorkspace(subjectId)
    setWorkspace(result)
    setBankId((current) =>
      result.subject.questionBanks.some((bank) => bank.id === current)
        ? current
        : result.subject.questionBanks[0]?.id || '',
    )
    setRubrics(
      toEditableRubrics(result.draftRubricSet || result.approvedRubricSet),
    )
  }

  useEffect(() => {
    let active = true
    getSubjects()
      .then((result) => {
        if (!active) return
        setSubjects(result)
        setSubjectId((current) => current || result[0]?.id || '')
      })
      .catch((error) => {
        if (active) setMessage({ text: errorMessage(error), error: true })
      })
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    if (!subjectId) return undefined
    let active = true
    getSubjectWorkspace(subjectId)
      .then((result) => {
        if (!active) return
        setWorkspace(result)
        setBankId((current) =>
          result.subject.questionBanks.some((bank) => bank.id === current)
            ? current
            : result.subject.questionBanks[0]?.id || '',
        )
        setRubrics(
          toEditableRubrics(result.draftRubricSet || result.approvedRubricSet),
        )
      })
      .catch((error) => {
        if (active) setMessage({ text: errorMessage(error), error: true })
      })
    return () => {
      active = false
    }
  }, [subjectId])

  const weightTotal = useMemo(
    () =>
      Math.round(
        rubrics.reduce(
          (total, rubric) => total + Number(rubric.weightPercent || 0),
          0,
        ) * 100,
      ) / 100,
    [rubrics],
  )

  async function run(action, success) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      if (success) notify(success)
    } catch (error) {
      notify(errorMessage(error), true)
    } finally {
      setBusy(false)
    }
  }

  async function handleCreateSubject(event) {
    event.preventDefault()
    await run(async () => {
      const created = await createSubject(subjectForm)
      setSubjectForm({ name: '', description: '', questionBankName: '' })
      await loadSubjects(created.id)
    }, 'Đã tạo môn học và bộ câu hỏi đầu tiên.')
  }

  async function handleCreateBank(event) {
    event.preventDefault()
    await run(async () => {
      const created = await createSubjectQuestionBank(subjectId, bankForm)
      setBankForm({ name: '', description: '' })
      setCreatingBank(false)
      // The new set becomes the destination of the next generated questions.
      if (created?.id) setBankId(created.id)
      await loadSubjects(subjectId)
      await loadWorkspace()
    }, 'Đã tạo bộ câu hỏi mới và chọn làm nơi lưu câu hỏi.')
  }

  function cancelCreateBank() {
    setCreatingBank(false)
    setBankForm({ name: '', description: '' })
  }

  async function handleUpload(event) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    await run(async () => {
      await uploadStudyDocument(subjectId, file)
      await loadWorkspace()
    }, 'Đã tải tài liệu học tập lên.')
  }

  async function handleGenerate() {
    const withRubrics = includeRubrics
    await run(
      async () => {
        const result = await generateWf1Content(subjectId, {
          questionBankId: bankId,
          questionCount: Number(questionCount),
          topic: topic.trim() || null,
          includeRubrics: withRubrics,
        })
        setWorkspace(result)
        if (withRubrics)
          setRubrics(
            toEditableRubrics(
              result.draftRubricSet || result.approvedRubricSet,
            ),
          )
        setRegenerateRubrics(null)
      },
      withRubrics
        ? 'Đã tạo bản nháp câu hỏi và rubric. Hãy kiểm tra trước khi duyệt.'
        : 'Đã tạo bản nháp câu hỏi; bộ rubric hiện tại được giữ nguyên.',
    )
  }

  async function handleReindex(document) {
    await run(async () => {
      await reindexStudyDocument(subjectId, document.id)
      await loadWorkspace()
    }, `Đã lập chỉ mục lại ${document.fileName}.`)
  }

  function addRubric() {
    setRubrics((current) => [
      ...current,
      {
        key: rowKey(),
        name: current.length === 0 ? 'Kiến thức chuyên môn' : '',
        description: '',
        weightPercent: current.length === 0 ? 100 : 0,
        criteria: [emptyCriterion()],
      },
    ])
  }

  function removeRubric(index) {
    setRubrics((current) =>
      current.filter((_, itemIndex) => itemIndex !== index),
    )
  }

  function addCriterion(rubricIndex) {
    setRubrics((current) =>
      current.map((rubric, itemIndex) =>
        itemIndex === rubricIndex
          ? { ...rubric, criteria: [...rubric.criteria, emptyCriterion()] }
          : rubric,
      ),
    )
  }

  function removeCriterion(rubricIndex, criterionIndex) {
    setRubrics((current) =>
      current.map((rubric, itemIndex) =>
        itemIndex === rubricIndex
          ? {
              ...rubric,
              criteria: rubric.criteria.filter(
                (_, childIndex) => childIndex !== criterionIndex,
              ),
            }
          : rubric,
      ),
    )
  }

  function updateRubric(index, field, value) {
    setRubrics((current) =>
      current.map((rubric, itemIndex) =>
        itemIndex === index ? { ...rubric, [field]: value } : rubric,
      ),
    )
  }

  function updateCriterion(rubricIndex, criterionIndex, field, value) {
    setRubrics((current) =>
      current.map((rubric, itemIndex) =>
        itemIndex !== rubricIndex
          ? rubric
          : {
              ...rubric,
              criteria: rubric.criteria.map((criterion, childIndex) =>
                childIndex === criterionIndex
                  ? { ...criterion, [field]: value }
                  : criterion,
              ),
            },
      ),
    )
  }

  async function handleSaveRubrics() {
    await run(async () => {
      await saveDraftRubrics(subjectId, toRubricPayload(rubrics))
      await loadWorkspace()
    }, 'Đã lưu bộ rubric nháp.')
  }

  // Saving first turns any edit (or a brand-new manual set) into the draft that gets approved.
  async function handleApproveRubrics() {
    await run(async () => {
      await saveDraftRubrics(subjectId, toRubricPayload(rubrics))
      const refreshed = await getSubjectWorkspace(subjectId)
      await approveRubricSet(subjectId, refreshed.draftRubricSet.id)
      await loadWorkspace()
    }, 'Đã duyệt bộ rubric dùng chung cho môn học.')
  }

  return (
    <SessionLayout
      eyebrow="WF1 / AI QUESTION PREPARATION"
      title="Chuẩn bị câu hỏi bằng AI"
      description="Tải tài liệu môn học để hệ thống tự động đề xuất câu hỏi và rubric, sau đó giảng viên kiểm duyệt trước khi sử dụng."
      busy={busy}
    >
      {message && (
        <p
          className={`wf1-message ${message.error ? 'wf1-message--error' : ''}`}
          role={message.error ? 'alert' : 'status'}
        >
          {message.text}
        </p>
      )}

      <section className="wf1-panel">
        <div className="wf1-panel__title">
          <span>01</span>
          <div>
            <h2>Môn học</h2>
            <p>
              Mỗi môn có thể chứa nhiều bộ câu hỏi, ví dụ mỗi chương một bộ.
            </p>
          </div>
        </div>
        <div className="wf1-fields">
          <label>
            Môn đang làm việc
            <select
              value={subjectId}
              onChange={(event) => setSubjectId(event.target.value)}
            >
              <option value="">Chọn môn học</option>
              {subjects.map((subject) => (
                <option key={subject.id} value={subject.id}>
                  {subject.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        <details>
          <summary>Tạo môn học mới</summary>
          <form className="wf1-form" onSubmit={handleCreateSubject}>
            <div className="wf1-fields">
              <label>
                Tên môn
                <input
                  required
                  maxLength={120}
                  value={subjectForm.name}
                  onChange={(event) =>
                    setSubjectForm({ ...subjectForm, name: event.target.value })
                  }
                />
              </label>
              <label>
                Bộ câu hỏi đầu tiên
                <input
                  required
                  maxLength={120}
                  value={subjectForm.questionBankName}
                  onChange={(event) =>
                    setSubjectForm({
                      ...subjectForm,
                      questionBankName: event.target.value,
                    })
                  }
                />
              </label>
            </div>
            <label>
              Mô tả
              <textarea
                className="wf1-textarea--small"
                value={subjectForm.description}
                onChange={(event) =>
                  setSubjectForm({
                    ...subjectForm,
                    description: event.target.value,
                  })
                }
              />
            </label>
            <button className="wf1-button wf1-button--primary" disabled={busy}>
              Tạo môn học
            </button>
          </form>
        </details>
      </section>

      {workspace && (
        <>
          <section className="wf1-panel">
            <div className="wf1-panel__title">
              <span>02</span>
              <div>
                <h2>Tài liệu và bộ câu hỏi đích</h2>
                <p>Chỉ nhận tài liệu PDF, tối đa 10 MB.</p>
              </div>
            </div>
            <div className="wf1-fields">
              <div className="wf1-field">
                <label htmlFor="wf1-bank">Lưu vào bộ câu hỏi</label>
                <div className="wf1-inline">
                  <select
                    id="wf1-bank"
                    value={bankId}
                    onChange={(event) => setBankId(event.target.value)}
                  >
                    {workspace.subject.questionBanks.map((bank) => (
                      <option key={bank.id} value={bank.id}>
                        {bank.name}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="wf1-button wf1-button--ghost"
                    aria-expanded={creatingBank}
                    aria-controls="wf1-new-bank"
                    onClick={() =>
                      creatingBank ? cancelCreateBank() : setCreatingBank(true)
                    }
                    disabled={busy}
                  >
                    + Tạo bộ mới
                  </button>
                </div>
                {creatingBank && (
                  <form
                    id="wf1-new-bank"
                    className="wf1-inline-form"
                    aria-label="Tạo bộ câu hỏi mới"
                    onSubmit={handleCreateBank}
                  >
                    <label>
                      Tên bộ câu hỏi
                      <input
                        required
                        autoFocus
                        maxLength={120}
                        placeholder="Ví dụ: Chương 24 - Hệ thống xe tự hành AGV"
                        value={bankForm.name}
                        onChange={(event) =>
                          setBankForm({ ...bankForm, name: event.target.value })
                        }
                      />
                    </label>
                    <label>
                      Mô tả (không bắt buộc)
                      <input
                        maxLength={500}
                        value={bankForm.description}
                        onChange={(event) =>
                          setBankForm({
                            ...bankForm,
                            description: event.target.value,
                          })
                        }
                      />
                    </label>
                    <div className="wf1-inline">
                      <button
                        className="wf1-button wf1-button--primary"
                        disabled={busy}
                      >
                        Tạo bộ câu hỏi
                      </button>
                      <button
                        type="button"
                        className="wf1-button wf1-button--ghost"
                        onClick={cancelCreateBank}
                        disabled={busy}
                      >
                        Hủy
                      </button>
                    </div>
                  </form>
                )}
              </div>
              <label className="wf1-upload">
                Thêm tài liệu PDF
                <input
                  type="file"
                  accept="application/pdf,.pdf"
                  onChange={handleUpload}
                  disabled={busy}
                />
                <small className="wf1-hint">
                  Tài liệu dùng chung cho mọi bộ câu hỏi của môn.
                </small>
              </label>
            </div>
            <ul className="wf1-documents">
              {workspace.documents.map((document) => (
                <li key={document.id}>
                  <span>
                    <strong>{document.fileName}</strong>
                    <small>
                      {(document.sizeBytes / 1024).toFixed(1)} KB ·{' '}
                      {indexDescription(document)}
                    </small>
                    {document.indexError && (
                      <small className="wf1-warning">
                        {document.indexError}
                      </small>
                    )}
                  </span>
                  <span className="wf1-document-actions">
                    {(document.indexStatus !== 'Indexed' ||
                      !document.semanticSearch) && (
                      <button
                        type="button"
                        onClick={() => handleReindex(document)}
                        disabled={busy}
                      >
                        Lập chỉ mục lại
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() =>
                        run(async () => {
                          await deleteStudyDocument(subjectId, document.id)
                          await loadWorkspace()
                        }, 'Đã xóa tài liệu.')
                      }
                      disabled={busy}
                    >
                      Xóa
                    </button>
                  </span>
                </li>
              ))}
              {workspace.documents.length === 0 && (
                <li className="wf1-empty">Chưa có tài liệu học tập.</li>
              )}
            </ul>
          </section>

          <section className="wf1-panel wf1-panel--accent">
            <div className="wf1-panel__title">
              <span>03</span>
              <div>
                <h2>Tạo câu hỏi tự động từ tài liệu</h2>
                <p>
                  Hệ thống truy xuất các đoạn liên quan nhất trong tài liệu, AI
                  chỉ sinh câu hỏi từ các đoạn đó và ghi rõ nguồn. Kết quả là
                  bản nháp, không tự vào bộ câu hỏi chính thức.
                </p>
              </div>
            </div>
            <div className="wf1-generate">
              <label>
                Chủ đề / chương (không bắt buộc)
                <input
                  value={topic}
                  maxLength={200}
                  placeholder="Ví dụ: Chuẩn hóa cơ sở dữ liệu"
                  onChange={(event) => setTopic(event.target.value)}
                />
              </label>
              <label>
                Số câu hỏi
                <select
                  value={questionCount}
                  onChange={(event) => setQuestionCount(event.target.value)}
                >
                  {[1, 5, 10, 20].map((count) => (
                    <option key={count} value={count}>
                      {count} câu
                    </option>
                  ))}
                </select>
              </label>
              <label className="wf1-check">
                <input
                  type="checkbox"
                  checked={includeRubrics}
                  onChange={(event) =>
                    setRegenerateRubrics(event.target.checked)
                  }
                />
                Đề xuất lại bộ rubric (ghi đè bản nháp rubric hiện tại)
              </label>
              <button
                type="button"
                className="wf1-button wf1-button--primary"
                disabled={busy || !bankId || workspace.documents.length === 0}
                onClick={handleGenerate}
              >
                {busy
                  ? 'Đang xử lý...'
                  : includeRubrics
                    ? 'Tạo câu hỏi và rubric'
                    : 'Tạo câu hỏi'}
              </button>
            </div>
          </section>

          <section className="wf1-panel">
            <div className="wf1-panel__title">
              <span>04</span>
              <div>
                <h2>Duyệt câu hỏi AI</h2>
                <p>Chỉnh sửa Bloom, độ khó và ý chính trước khi duyệt.</p>
              </div>
            </div>
            <div className="wf1-candidates">
              {workspace.questionCandidates.map((candidate) => (
                <CandidateCard
                  key={candidate.id}
                  subjectId={subjectId}
                  candidate={candidate}
                  onChanged={loadWorkspace}
                  setNotice={notify}
                />
              ))}
              {workspace.questionCandidates.length === 0 && (
                <p className="wf1-empty">Chưa có câu hỏi AI đề xuất.</p>
              )}
            </div>
          </section>

          <section className="wf1-panel">
            <div className="wf1-panel__title">
              <span>05</span>
              <div>
                <h2>Rubric dùng chung của môn</h2>
                <p>
                  Mỗi rubric có một tỷ trọng; các tiêu chí bên trong chỉ có
                  thang điểm. Bộ rubric chấm mọi câu hỏi của môn, nên tiêu chí
                  nên mô tả chất lượng câu trả lời (độ chính xác, đủ ý chính,
                  lập luận, diễn đạt) thay vì một chương cụ thể.
                </p>
              </div>
            </div>
            <div
              className={`wf1-total ${weightTotal === 100 ? 'wf1-total--ok' : ''}`}
            >
              Tổng tỷ trọng: <strong>{weightTotal}%</strong> / 100%
            </div>
            {workspace.draftRubricSet ? (
              <p className="wf1-rubric-state">
                Đang sửa bản nháp phiên bản {workspace.draftRubricSet.version}
                {workspace.draftRubricSet.source === 'AI'
                  ? ' (AI đề xuất)'
                  : ''}
                .
              </p>
            ) : (
              workspace.approvedRubricSet && (
                <p className="wf1-rubric-state">
                  Đang xem phiên bản {workspace.approvedRubricSet.version} đã
                  duyệt; lưu thay đổi sẽ tạo bản nháp phiên bản mới.
                </p>
              )
            )}
            <div className="wf1-rubrics">
              {rubrics.map((rubric, rubricIndex) => (
                <article className="wf1-rubric" key={rubric.key}>
                  <div className="wf1-fields">
                    <label>
                      Tên rubric
                      <input
                        value={rubric.name}
                        maxLength={120}
                        onChange={(event) =>
                          updateRubric(rubricIndex, 'name', event.target.value)
                        }
                      />
                    </label>
                    <label>
                      Tỷ trọng %
                      <input
                        type="number"
                        min="0.01"
                        max="100"
                        step="0.01"
                        value={rubric.weightPercent}
                        onChange={(event) =>
                          updateRubric(
                            rubricIndex,
                            'weightPercent',
                            event.target.value,
                          )
                        }
                      />
                    </label>
                  </div>
                  <label>
                    Mô tả
                    <input
                      value={rubric.description}
                      onChange={(event) =>
                        updateRubric(
                          rubricIndex,
                          'description',
                          event.target.value,
                        )
                      }
                    />
                  </label>
                  <h3>Tiêu chí</h3>
                  {rubric.criteria.map((criterion, criterionIndex) => (
                    <div className="wf1-criterion" key={criterion.key}>
                      <input
                        aria-label="Tên tiêu chí"
                        value={criterion.name}
                        maxLength={120}
                        onChange={(event) =>
                          updateCriterion(
                            rubricIndex,
                            criterionIndex,
                            'name',
                            event.target.value,
                          )
                        }
                      />
                      <input
                        aria-label="Điểm tối đa"
                        type="number"
                        min="0.01"
                        step="0.01"
                        value={criterion.maxScore}
                        onChange={(event) =>
                          updateCriterion(
                            rubricIndex,
                            criterionIndex,
                            'maxScore',
                            event.target.value,
                          )
                        }
                      />
                      <input
                        aria-label="Mô tả tiêu chí"
                        className="wf1-criterion__description"
                        placeholder="Mô tả mức đạt (không bắt buộc)"
                        maxLength={500}
                        value={criterion.description}
                        onChange={(event) =>
                          updateCriterion(
                            rubricIndex,
                            criterionIndex,
                            'description',
                            event.target.value,
                          )
                        }
                      />
                      <button
                        type="button"
                        className="wf1-link wf1-link--danger"
                        aria-label={`Xóa tiêu chí ${criterionIndex + 1} của ${rubric.name || 'rubric'}`}
                        onClick={() =>
                          removeCriterion(rubricIndex, criterionIndex)
                        }
                        disabled={busy || rubric.criteria.length === 1}
                      >
                        Xóa
                      </button>
                    </div>
                  ))}
                  <div className="wf1-actions wf1-actions--start">
                    <button
                      type="button"
                      className="wf1-link"
                      onClick={() => addCriterion(rubricIndex)}
                      disabled={busy || rubric.criteria.length >= MAX_CRITERIA}
                    >
                      + Thêm tiêu chí
                    </button>
                    <button
                      type="button"
                      className="wf1-link wf1-link--danger"
                      onClick={() => removeRubric(rubricIndex)}
                      disabled={busy || rubrics.length === 1}
                    >
                      Xóa rubric này
                    </button>
                  </div>
                </article>
              ))}
              {rubrics.length === 0 && (
                <p className="wf1-empty">
                  Chưa có rubric. Hệ thống có thể đề xuất sau khi đọc tài liệu,
                  hoặc giảng viên tự tạo.
                </p>
              )}
            </div>
            <div className="wf1-actions">
              <button
                type="button"
                className="wf1-button wf1-button--ghost"
                onClick={addRubric}
                disabled={busy || rubrics.length >= MAX_RUBRICS}
              >
                {rubrics.length === 0 ? 'Tạo rubric thủ công' : '+ Thêm rubric'}
              </button>
              {rubrics.length > 0 && (
                <>
                  <button
                    type="button"
                    className="wf1-button wf1-button--ghost"
                    onClick={handleSaveRubrics}
                    disabled={busy}
                  >
                    Lưu bản nháp
                  </button>
                  <button
                    type="button"
                    className="wf1-button wf1-button--primary"
                    onClick={handleApproveRubrics}
                    disabled={busy || weightTotal !== 100}
                  >
                    Lưu và duyệt bộ rubric
                  </button>
                </>
              )}
            </div>
            {workspace.approvedRubricSet && (
              <p className="wf1-approved">
                Bộ rubric phiên bản {workspace.approvedRubricSet.version} đã
                được duyệt.
              </p>
            )}
          </section>
        </>
      )}
    </SessionLayout>
  )
}
