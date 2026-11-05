import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import {
  exportUrl,
  finalizeReport,
  getReport,
  getReports,
  getStatistics,
} from '../../api/interviewApi.js'
import { getExamSessions } from '../../api/examSessionApi.js'
import { useAuth } from '../auth/authContext.js'
import SessionLayout from '../auth/SessionLayout.jsx'
import './viva.css'

const bloomLabels = {
  Remember: 'Nhớ',
  Understand: 'Hiểu',
  Apply: 'Vận dụng',
  Analyze: 'Phân tích',
}

// How a follow-up probed the student's previous answer.
const probeLabels = {
  clarify: 'Làm rõ ý',
  justify: 'Hỏi lý do',
  example: 'Yêu cầu ví dụ',
  challenge: 'Tình huống phản biện',
  contradiction: 'Chỉ ra mâu thuẫn',
  deepen: 'Đào sâu',
}

function formatDuration(seconds) {
  if (seconds === null || seconds === undefined) return null
  const minutes = Math.floor(seconds / 60)
  return minutes > 0
    ? `${minutes} phút ${seconds % 60} giây`
    : `${seconds} giây`
}

function StudentIdentity({ name, code, email }) {
  return (
    <>
      {code && <b className="viva-code">{code}</b>} {name || email}
      {name && <small>{email}</small>}
    </>
  )
}

function Statistics({ statistics }) {
  const maxBand = Math.max(1, ...statistics.distribution.map((b) => b.count))
  return (
    <section className="viva-panel">
      <h2>Thống kê: {statistics.title}</h2>
      <div className="viva-kpis">
        <div>
          <span>Đã thi xong</span>
          <strong>
            {statistics.completed}/{statistics.participants}
          </strong>
        </div>
        <div>
          <span>Đã chốt điểm</span>
          <strong>{statistics.finalized ?? 0}</strong>
        </div>
        <div>
          <span>Điểm trung bình</span>
          <strong>{statistics.averageScore ?? '—'}</strong>
        </div>
        <div>
          <span>Tỷ lệ trả lời tốt</span>
          <strong>
            {statistics.goodAnswerRate === null ||
            statistics.goodAnswerRate === undefined
              ? '—'
              : `${statistics.goodAnswerRate}%`}
          </strong>
        </div>
        <div>
          <span>Cao nhất / thấp nhất</span>
          <strong>
            {statistics.highestScore ?? '—'} / {statistics.lowestScore ?? '—'}
          </strong>
        </div>
      </div>
      <p className="viva-muted">
        Dùng điểm đã chốt nếu có, còn lại dùng điểm AI gợi ý. Trả lời tốt: từ
        7/10.
      </p>
      <h3>Phân bố điểm</h3>
      <div className="viva-dist" role="list" aria-label="Phân bố điểm">
        {statistics.distribution.map((band) => (
          <div key={band.label} role="listitem">
            <span>{band.label} điểm</span>
            <span className="viva-dist__track" aria-hidden="true">
              <span
                className="viva-dist__bar"
                style={{ width: `${(band.count / maxBand) * 100}%` }}
              />
            </span>
            <strong>{band.count}</strong>
          </div>
        ))}
      </div>
      {statistics.bloomLevels?.length > 0 && (
        <>
          <h3>Theo mức nhận thức Bloom</h3>
          <div className="viva-table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mức Bloom</th>
                  <th>Lượt trả lời</th>
                  <th>Điểm TB</th>
                  <th>Trả lời tốt</th>
                </tr>
              </thead>
              <tbody>
                {statistics.bloomLevels.map((level) => (
                  <tr key={level.bloomLevel}>
                    <td>{bloomLabels[level.bloomLevel] ?? level.bloomLevel}</td>
                    <td>{level.responseCount}</td>
                    <td>{level.averageScore}</td>
                    <td>{level.goodAnswerRate}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      <h3>Câu hỏi theo độ khó (điểm trung bình thấp nhất trước)</h3>
      {statistics.questions.length === 0 ? (
        <p>Chưa có bài hoàn thành để thống kê.</p>
      ) : (
        <div className="viva-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Câu hỏi</th>
                <th>Lượt trả lời</th>
                <th>Điểm TB</th>
                <th>Trả lời tốt</th>
                <th>TG trả lời TB</th>
                <th>Ý hay bị thiếu</th>
              </tr>
            </thead>
            <tbody>
              {statistics.questions.map((q) => (
                <tr key={q.questionId}>
                  <td>
                    {q.content}
                    {q.bloomLevel && (
                      <small>Bloom: {bloomLabels[q.bloomLevel]}</small>
                    )}
                  </td>
                  <td>{q.responseCount}</td>
                  <td>{q.averageScore}</td>
                  <td>{q.goodAnswerRate}%</td>
                  <td>{formatDuration(q.averageDurationSeconds) ?? '—'}</td>
                  <td>{q.commonMissingKeyPoints?.join('; ') || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

export default function ReportsPage() {
  const { attemptId } = useParams()
  const { user } = useAuth()
  const teacher = ['ADMIN', 'LECTURER'].includes(user.role)
  const [rows, setRows] = useState([])
  const [sessions, setSessions] = useState([])
  const [sessionId, setSessionId] = useState('')
  const [report, setReport] = useState(null)
  const [statistics, setStatistics] = useState(null)
  const [marks, setMarks] = useState({})
  const [feedback, setFeedback] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    const options = { signal: controller.signal }
    const operation = attemptId
      ? getReport(attemptId, options)
      : getReports(options)
    operation
      .then((result) => {
        if (controller.signal.aborted) return
        if (attemptId) {
          setReport(result)
          setMarks(
            Object.fromEntries(
              result.questions.map((q) => [
                q.questionId,
                q.finalScore ?? q.aiScore,
              ]),
            ),
          )
          setFeedback(result.lecturerFeedback ?? '')
        } else setRows(result)
      })
      .catch((failure) => {
        if (!controller.signal.aborted) setError(failure.message)
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    if (teacher && !attemptId)
      getExamSessions(controller.signal)
        .then((items) => {
          if (!controller.signal.aborted) {
            setSessions(items)
            setSessionId((current) => current || items[0]?.id || '')
          }
        })
        .catch((failure) => {
          if (!controller.signal.aborted) setError(failure.message)
        })
    return () => controller.abort()
  }, [attemptId, teacher, revision])

  useEffect(() => {
    if (!teacher || !sessionId) return undefined
    const controller = new AbortController()
    getStatistics(sessionId, { signal: controller.signal })
      .then((result) => {
        if (!controller.signal.aborted) setStatistics(result)
      })
      .catch((failure) => {
        if (!controller.signal.aborted) setError(failure.message)
      })
    return () => controller.abort()
  }, [teacher, sessionId, revision])

  async function finalize(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const result = await finalizeReport(attemptId, {
        questions: report.questions.map((q) => ({
          questionId: q.questionId,
          score: Number(marks[q.questionId]),
        })),
        feedback,
      })
      setReport(result)
    } catch (failure) {
      setError(failure.message)
    } finally {
      setBusy(false)
    }
  }

  const filteredRows =
    sessionId && teacher
      ? rows.filter((r) => r.examSessionId === sessionId)
      : rows
  return (
    <SessionLayout
      title={attemptId ? 'Kết quả vấn đáp' : 'Bảng điểm & phản hồi'}
      eyebrow="RESULTS / FEEDBACK"
      description="Điểm trên thang 10. AI đưa gợi ý; giảng viên duyệt và chốt điểm chính thức."
      busy={busy}
    >
      {error && (
        <p role="alert" className="viva-error">
          {error}
        </p>
      )}
      {loading && <p role="status">Đang tải kết quả...</p>}
      {!attemptId && (
        <>
          <div className="viva-toolbar">
            {teacher && (
              <label>
                Phiên thi
                <select
                  value={sessionId}
                  onChange={(e) => {
                    setSessionId(e.target.value)
                    setStatistics(null)
                  }}
                >
                  <option value="">Tất cả phiên</option>
                  {sessions.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.title}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <button
              type="button"
              onClick={() => {
                setLoading(true)
                setError('')
                setRevision((v) => v + 1)
              }}
            >
              Tải lại
            </button>
            {teacher && sessionId && (
              <span className="viva-export">
                <a className="viva-button" href={exportUrl(sessionId)} download>
                  Xuất bảng điểm Excel
                </a>
                <a
                  className="viva-button viva-button--ghost"
                  href={exportUrl(sessionId, 'csv')}
                  download
                >
                  CSV
                </a>
              </span>
            )}
          </div>
          {teacher && statistics && <Statistics statistics={statistics} />}
          <section className="viva-panel">
            <h2>{teacher ? 'Kết quả sinh viên' : 'Bài thi của tôi'}</h2>
            {filteredRows.length === 0 ? (
              <p>Chưa có bài thi.</p>
            ) : (
              <div className="viva-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Sinh viên / Phiên thi</th>
                      <th>Điểm AI</th>
                      <th>Điểm chốt</th>
                      <th>Trạng thái</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRows.map((r) => (
                      <tr key={r.attemptId}>
                        <td>
                          <StudentIdentity
                            name={r.studentName}
                            code={r.studentCode}
                            email={r.studentEmail}
                          />
                          <small>{r.title}</small>
                        </td>
                        <td>{r.aiScore ?? '—'}</td>
                        <td>{r.finalScore ?? 'Chờ chốt'}</td>
                        <td>
                          {r.status === 'Finalized'
                            ? 'Đã chốt'
                            : r.status === 'Completed'
                              ? 'Chờ giảng viên'
                              : 'Đang thi'}
                        </td>
                        <td>
                          {r.status !== 'InProgress' ? (
                            <Link to={`/reports/${r.attemptId}`}>
                              Xem chi tiết
                            </Link>
                          ) : (
                            !teacher && (
                              <Link to={`/interviews/${r.attemptId}`}>
                                Tiếp tục thi
                              </Link>
                            )
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
      {attemptId && report && (
        <>
          <section className="viva-panel">
            <h2>{report.title}</h2>
            <p className="viva-student">
              <StudentIdentity
                name={report.studentName}
                code={report.studentCode}
                email={report.studentEmail}
              />
            </p>
            <div className="viva-scores">
              <div>
                <span>Điểm AI gợi ý</span>
                <strong>{report.aiScore}/10</strong>
              </div>
              <div>
                <span>Điểm giảng viên chốt</span>
                <strong>
                  {report.finalScore === null
                    ? 'Chờ chốt'
                    : `${report.finalScore}/10`}
                </strong>
              </div>
              {formatDuration(report.totalDurationSeconds) && (
                <div>
                  <span>Tổng thời gian trả lời</span>
                  <strong>{formatDuration(report.totalDurationSeconds)}</strong>
                </div>
              )}
            </div>
            <p>
              {report.isFinalized
                ? 'Giảng viên đã duyệt kết quả.'
                : 'Kết quả AI là tạm thời, chưa phải điểm chính thức.'}
            </p>
            {report.lecturerFeedback && (
              <p>Nhận xét giảng viên: {report.lecturerFeedback}</p>
            )}
          </section>
          {report.questions.map((q, index) => (
            <section className="viva-panel" key={q.questionId}>
              <div className="viva-toolbar">
                <h2>Câu {index + 1}</h2>
                <span className="viva-marks">
                  <strong>{q.aiScore}/10 · AI</strong>
                  {q.finalScore !== null && q.finalScore !== undefined && (
                    <strong className="viva-final-mark">
                      {q.finalScore}/10 · Giảng viên chốt
                    </strong>
                  )}
                </span>
              </div>
              {(q.bloomLevel || formatDuration(q.durationSeconds)) && (
                <p className="viva-chips">
                  {q.bloomLevel && (
                    <span className="viva-chip">
                      Bloom: {bloomLabels[q.bloomLevel] ?? q.bloomLevel}
                    </span>
                  )}
                  {formatDuration(q.durationSeconds) && (
                    <span className="viva-chip">
                      Trả lời trong {formatDuration(q.durationSeconds)}
                    </span>
                  )}
                </p>
              )}
              <h3>{q.question}</h3>
              <p className="viva-answer">
                <b>Câu trả lời:</b> {q.transcript || '(Không trả lời)'}
              </p>
              {q.followUps.map((turn, i) => (
                <div className="viva-followup" key={i}>
                  <b>
                    Hỏi thêm {i + 1}: {turn.question}
                  </b>
                  {(turn.probeType || turn.probedQuote) && (
                    <p className="viva-probe">
                      {turn.probeType && (
                        <span className="viva-chip viva-chip--probe">
                          {probeLabels[turn.probeType] ?? turn.probeType}
                        </span>
                      )}
                      {turn.probedQuote && (
                        <span>Hỏi xoáy vào: “{turn.probedQuote}”</span>
                      )}
                    </p>
                  )}
                  <p>{turn.answer || '(Không trả lời)'}</p>
                  {formatDuration(turn.durationSeconds) && (
                    <small className="viva-muted">
                      Trả lời trong {formatDuration(turn.durationSeconds)}
                    </small>
                  )}
                </div>
              ))}
              <p>
                <b>Nhận xét AI:</b> {q.feedback}
              </p>
              {q.strengths.length > 0 && (
                <p>
                  <b>Điểm mạnh:</b> {q.strengths.join('; ')}
                </p>
              )}
              {q.missingKeyPoints.length > 0 && (
                <p>
                  <b>Cần bổ sung:</b> {q.missingKeyPoints.join('; ')}
                </p>
              )}
              <details>
                <summary>Điểm theo tiêu chí</summary>
                <ul>
                  {q.criteria.map((c) => (
                    <li key={c.criterionId}>
                      {c.rubric} ({c.rubricWeightPercent}%) · {c.name}:{' '}
                      {c.applicable === false
                        ? 'Không áp dụng cho câu này'
                        : `${c.score}/${c.maxScore}`}{' '}
                      — {c.feedback}
                    </li>
                  ))}
                </ul>
              </details>
            </section>
          ))}
          {teacher && (
            <form className="viva-panel" onSubmit={finalize}>
              <h2>
                {report.isFinalized
                  ? 'Cập nhật điểm giảng viên'
                  : 'Duyệt và chốt điểm'}
              </h2>
              <p>
                Mỗi câu chính tính trọng số bằng nhau; nội dung hỏi thêm được
                tính chung vào câu chính đó.
              </p>
              <fieldset disabled={busy} className="viva-settings">
                {report.questions.map((q, i) => (
                  <label key={q.questionId}>
                    Câu {i + 1} /10
                    <input
                      type="number"
                      required
                      min="0"
                      max="10"
                      step="0.01"
                      value={marks[q.questionId] ?? ''}
                      onChange={(e) =>
                        setMarks({ ...marks, [q.questionId]: e.target.value })
                      }
                    />
                  </label>
                ))}
              </fieldset>
              <label className="viva-field">
                Nhận xét
                <textarea
                  maxLength={2000}
                  value={feedback}
                  onChange={(e) => setFeedback(e.target.value)}
                />
              </label>
              <button type="submit" className="viva-button" disabled={busy}>
                {busy ? 'Đang lưu…' : 'Chốt điểm và công bố'}
              </button>
            </form>
          )}
          <Link to="/reports">← Quay lại bảng điểm</Link>
        </>
      )}
    </SessionLayout>
  )
}
