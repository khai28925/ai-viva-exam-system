import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import SessionLayout from '../auth/SessionLayout.jsx'
import {
  resumeInterview,
  submitAnswer,
  transcribeAudio,
} from '../../api/interviewApi.js'
import './viva.css'

function voiceFor(language) {
  const prefix = language?.slice(0, 2).toLowerCase()
  return (window.speechSynthesis?.getVoices?.() ?? []).find((voice) =>
    voice.lang?.toLowerCase().startsWith(prefix),
  )
}

function speak(content, language) {
  if (!window.speechSynthesis) return
  const utterance = new SpeechSynthesisUtterance(content)
  utterance.lang = language
  const voice = voiceFor(language)
  if (voice) utterance.voice = voice
  window.speechSynthesis.cancel()
  window.speechSynthesis.speak(utterance)
}

export default function InterviewPage() {
  const { attemptId } = useParams()
  const [state, setState] = useState(null)
  const [text, setText] = useState('')
  const [interim, setInterim] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [listening, setListening] = useState(false)
  const [remaining, setRemaining] = useState(null)
  const [consent, setConsent] = useState(false)
  const [started, setStarted] = useState(false)
  const [audio, setAudio] = useState(null)
  const [recording, setRecording] = useState(false)
  const [autoRead, setAutoRead] = useState(true)
  const [, setVoicesVersion] = useState(0)
  const recognition = useRef(null)
  const recorder = useRef(null)
  const recordingDone = useRef(null)
  const audioRef = useRef(null)
  const transcribedFor = useRef(null)
  const stream = useRef(null)
  const textRef = useRef('')
  const interimRef = useRef('')
  const locked = useRef(false)
  const offset = useRef(0)
  const questionRef = useRef(null)
  const stateRef = useRef(null)
  const autoSubmitted = useRef(null)
  const speechSupported = !!(
    window.SpeechRecognition || window.webkitSpeechRecognition
  )
  const voiceOnly = state?.answerMode === 'Voice'
  const voices = window.speechSynthesis?.getVoices?.() ?? []
  const voiceMissing =
    !!state?.language && voices.length > 0 && !voiceFor(state.language)

  function stopSpeech() {
    recognition.current?.stop()
    recognition.current = null
    setListening(false)
    if (recorder.current?.state === 'recording') recorder.current.stop()
    stream.current?.getTracks().forEach((track) => track.stop())
    window.speechSynthesis?.cancel()
  }

  function acceptState(result) {
    stateRef.current = result
    offset.current = new Date(result.serverTime).getTime() - Date.now()
    if (questionRef.current !== result.currentQuestion?.id) {
      questionRef.current = result.currentQuestion?.id
      setText('')
      textRef.current = ''
      setInterim('')
      interimRef.current = ''
      setAudio(null)
      audioRef.current = null
      transcribedFor.current = null
      autoSubmitted.current = null
    }
    setState(result)
  }

  // Voices load asynchronously in Chrome; re-render once they are known.
  useEffect(() => {
    const synth = window.speechSynthesis
    if (!synth?.addEventListener) return undefined
    const refresh = () => setVoicesVersion((value) => value + 1)
    synth.addEventListener('voiceschanged', refresh)
    return () => synth.removeEventListener('voiceschanged', refresh)
  }, [])

  useEffect(() => {
    if (!started) return undefined
    const controller = new AbortController()
    resumeInterview(attemptId, { signal: controller.signal })
      .then((result) => {
        if (!controller.signal.aborted) acceptState(result)
      })
      .catch((failure) => {
        if (!controller.signal.aborted) setError(failure.message)
      })
    return () => {
      controller.abort()
      recognition.current?.abort()
      stream.current?.getTracks().forEach((track) => track.stop())
      if (recorder.current?.state === 'recording') recorder.current.stop()
      window.speechSynthesis?.cancel()
    }
  }, [attemptId, started])

  const send = useCallback(async () => {
    const current = stateRef.current?.currentQuestion
    if (locked.current || !current) return
    locked.current = true
    setBusy(true)
    setError('')
    stopSpeech()
    try {
      acceptState(
        await submitAnswer(
          attemptId,
          current.id,
          `${textRef.current} ${interimRef.current}`.trim(),
        ),
      )
      interimRef.current = ''
    } catch (failure) {
      setError(failure.message || 'Chưa gửi được câu trả lời. Hãy bấm gửi lại.')
    } finally {
      locked.current = false
      setBusy(false)
    }
  }, [attemptId])

  // Voice-only sessions: the server transcribes the recording and grades only that transcript.
  const finishVoiceAnswer = useCallback(async () => {
    const current = stateRef.current?.currentQuestion
    if (locked.current || !current) return
    locked.current = true
    setBusy(true)
    setError('')
    recognition.current?.stop()
    recognition.current = null
    setListening(false)
    try {
      let blob = audioRef.current
      if (recorder.current?.state === 'recording') {
        recorder.current.stop()
        blob = await recordingDone.current
      }
      if (blob?.size && transcribedFor.current !== current.id) {
        try {
          const result = await transcribeAudio(attemptId, current.id, blob)
          transcribedFor.current = current.id
          textRef.current = result.text
          setText(result.text)
        } catch (failure) {
          // 409: the deadline passed or the recording was already received. Submitting lets
          // the server record what it holds instead of leaving the student stuck on retry.
          if (failure.status !== 409) throw failure
        }
      }
      acceptState(await submitAnswer(attemptId, current.id, textRef.current))
    } catch (failure) {
      setError(
        failure.message ||
          'Chưa gửi được bản ghi. Bản ghi vẫn được giữ, hãy bấm gửi lại.',
      )
    } finally {
      locked.current = false
      setBusy(false)
    }
  }, [attemptId])

  useEffect(() => {
    if (!state?.currentQuestion?.deadline) return undefined
    const update = () => {
      const value = Math.max(
        0,
        Math.ceil(
          (new Date(state.currentQuestion.deadline).getTime() -
            Date.now() -
            offset.current) /
            1000,
        ),
      )
      setRemaining(value)
      if (
        value === 0 &&
        !locked.current &&
        autoSubmitted.current !== state.currentQuestion.id
      ) {
        autoSubmitted.current = state.currentQuestion.id
        void (stateRef.current?.answerMode === 'Voice'
          ? finishVoiceAnswer()
          : send())
      }
    }
    const interval = window.setInterval(update, 500)
    return () => window.clearInterval(interval)
  }, [
    state?.currentQuestion?.id,
    state?.currentQuestion?.deadline,
    send,
    finishVoiceAnswer,
  ])

  useEffect(() => {
    if (
      !autoRead ||
      !consent ||
      !state?.currentQuestion?.content ||
      !window.speechSynthesis
    )
      return
    speak(state.currentQuestion.content, state.language)
    return () => window.speechSynthesis.cancel()
  }, [
    state?.currentQuestion?.id,
    state?.currentQuestion?.content,
    state?.language,
    autoRead,
    consent,
  ])

  function readQuestion() {
    if (!window.speechSynthesis || !state?.currentQuestion) return
    recognition.current?.stop()
    speak(state.currentQuestion.content, state.language)
  }

  function startRecognition() {
    if (!consent || busy || !speechSupported) return
    window.speechSynthesis?.cancel()
    const SpeechRecognition =
      window.SpeechRecognition || window.webkitSpeechRecognition
    const listener = new SpeechRecognition()
    const initial = textRef.current
    listener.lang = state.language
    listener.continuous = true
    listener.interimResults = true
    const listeningQuestionId = state.currentQuestion.id
    listener.onresult = (event) => {
      if (locked.current || questionRef.current !== listeningQuestionId) return
      let final = initial
      let provisional = ''
      for (let i = 0; i < event.results.length; i++) {
        if (event.results[i].isFinal)
          final += ` ${event.results[i][0].transcript}`
        else provisional += event.results[i][0].transcript
      }
      textRef.current = final.trim()
      setText(final.trim())
      setInterim(provisional)
      interimRef.current = provisional
    }
    listener.onerror = (event) =>
      setError(
        event.error === 'not-allowed'
          ? 'Hãy cấp quyền micro cho trình duyệt.'
          : stateRef.current?.answerMode === 'Voice'
            ? `Bản chép lời xem trước bị gián đoạn (${event.error}); bản ghi âm vẫn tiếp tục và sẽ được chép lời khi gửi.`
            : `Nhận giọng nói bị gián đoạn (${event.error}). Có thể bấm Nói tiếp hoặc dùng ghi âm.`,
      )
    listener.onend = () => {
      setListening(false)
      setInterim('')
      if (
        !locked.current &&
        questionRef.current === listeningQuestionId &&
        interimRef.current
      ) {
        textRef.current = `${textRef.current} ${interimRef.current}`.trim()
        setText(textRef.current)
        interimRef.current = ''
      }
    }
    recognition.current = listener
    try {
      listener.start()
      setListening(true)
      setError('')
    } catch {
      setError('Không mở được nhận giọng nói. Thử lại hoặc dùng ghi âm.')
    }
  }

  async function beginRecording() {
    const mediaStream = await navigator.mediaDevices.getUserMedia({
      audio: true,
    })
    stream.current = mediaStream
    const chunks = []
    const mediaRecorder = new MediaRecorder(mediaStream)
    recordingDone.current = new Promise((resolve) => {
      mediaRecorder.onstop = () => {
        const blob = new Blob(chunks, { type: mediaRecorder.mimeType })
        audioRef.current = blob
        setAudio(blob)
        setRecording(false)
        mediaStream.getTracks().forEach((track) => track.stop())
        resolve(blob)
      }
    })
    mediaRecorder.ondataavailable = (event) => {
      if (event.data.size) chunks.push(event.data)
    }
    recorder.current = mediaRecorder
    mediaRecorder.start()
    setRecording(true)
    setError('')
  }

  async function startRecording() {
    try {
      recognition.current?.stop()
      window.speechSynthesis?.cancel()
      await beginRecording()
    } catch {
      setError(
        'Không mở được micro. Kiểm tra quyền micro hoặc dùng Chrome/Edge.',
      )
    }
  }

  async function startVoiceAnswer() {
    if (!consent || busy) return
    window.speechSynthesis?.cancel()
    audioRef.current = null
    setAudio(null)
    try {
      await beginRecording()
    } catch {
      setError(
        'Không mở được micro. Kiểm tra quyền micro hoặc dùng Chrome/Edge.',
      )
      return
    }
    startRecognition()
  }

  async function transcribe() {
    if (!audio || locked.current) return
    locked.current = true
    setBusy(true)
    setError('')
    try {
      const result = await transcribeAudio(
        attemptId,
        state.currentQuestion.id,
        audio,
      )
      textRef.current = result.text
      setText(result.text)
      acceptState(
        await submitAnswer(attemptId, state.currentQuestion.id, result.text),
      )
    } catch (failure) {
      setError(failure.message)
    } finally {
      locked.current = false
      setBusy(false)
    }
  }

  return (
    <SessionLayout
      title={state?.title || 'Phòng thi vấn đáp'}
      eyebrow="AI VIVA / INTERVIEW"
      description="Nghe câu hỏi, trả lời bằng giọng nói và kiểm tra bản chép lời trước khi gửi."
      busy={busy}
    >
      {error && (
        <p role="alert" className="viva-error">
          {error}
        </p>
      )}
      {!started && (
        <section className="viva-panel">
          <h2>Sẵn sàng bắt đầu?</h2>
          <p>
            Thời gian bắt đầu tính ngay khi câu hỏi được hiển thị. Dùng Chrome
            hoặc Edge để nhận bản chép lời gần thời gian thực.
          </p>
          <label className="viva-consent">
            <input
              type="checkbox"
              checked={consent}
              onChange={(e) => setConsent(e.target.checked)}
            />
            Tôi đồng ý dùng micro; giọng nói có thể được gửi tới dịch vụ nhận
            dạng và nội dung trả lời tới AI để đánh giá.
          </label>
          <button
            className="viva-button"
            type="button"
            disabled={!consent}
            onClick={() => setStarted(true)}
          >
            Bắt đầu / Tiếp tục bài thi
          </button>
        </section>
      )}
      {started && !state && <p role="status">Đang mở bài thi...</p>}
      {state?.status === 'Completed' ? (
        <section className="viva-panel">
          <h2>Đã hoàn thành bài thi</h2>
          <p>
            Kết quả AI đã được lưu. Điểm chính thức sẽ có sau khi giảng viên
            chốt.
          </p>
          <Link to={`/reports/${attemptId}`} className="viva-button">
            Xem bảng điểm và phản hồi
          </Link>
        </section>
      ) : (
        state?.currentQuestion && (
          <section className="viva-panel">
            <div className="viva-toolbar">
              <span>
                Câu chính {state.currentQuestion.mainQuestionNumber}/
                {state.totalMainQuestions}
                {state.currentQuestion.source === 'AiFollowUp' &&
                  ` · Hỏi thêm ${state.currentQuestion.followUpNumber}/${state.maxFollowUps}`}
              </span>
              <strong
                className={
                  remaining !== null && remaining < 20 ? 'viva-danger' : ''
                }
                role="timer"
              >
                {remaining ?? '—'} giây
              </strong>
            </div>
            {state.currentQuestion.source === 'AiFollowUp' && (
              <p className="viva-followup-note">
                Giám khảo AI hỏi thêm dựa trên chính câu trả lời vừa rồi của
                bạn.
              </p>
            )}
            <h2>{state.currentQuestion.content}</h2>
            <div className="viva-actions">
              <button
                type="button"
                onClick={readQuestion}
                disabled={busy || listening || recording}
              >
                Đọc câu hỏi
              </button>
              <label>
                <input
                  type="checkbox"
                  checked={autoRead}
                  onChange={(e) => setAutoRead(e.target.checked)}
                />{' '}
                Tự đọc câu tiếp theo
              </label>
            </div>
            {voiceMissing && (
              <p className="viva-muted">
                Máy này chưa cài giọng đọc cho ngôn ngữ của phiên; trình duyệt
                sẽ dùng giọng mặc định. Câu hỏi luôn hiển thị dạng chữ.
              </p>
            )}
            {voiceOnly ? (
              <>
                <div className="viva-actions">
                  {recording ? (
                    <button
                      type="button"
                      className="viva-button"
                      onClick={finishVoiceAnswer}
                      disabled={busy}
                    >
                      Kết thúc và gửi câu trả lời
                    </button>
                  ) : audio ? (
                    <button
                      type="button"
                      className="viva-button"
                      onClick={finishVoiceAnswer}
                      disabled={busy}
                    >
                      {busy ? 'AI đang xử lý…' : 'Gửi lại bản ghi'}
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="viva-button"
                      onClick={startVoiceAnswer}
                      disabled={busy || !consent}
                    >
                      Bắt đầu trả lời
                    </button>
                  )}
                </div>
                {recording && (
                  <p role="status">
                    Đang ghi âm câu trả lời…
                    {listening && interim && ` ${interim}`}
                  </p>
                )}
                <label className="viva-field">
                  Bản chép lời trực tiếp (chỉ để xem trước)
                  <textarea
                    value={`${text} ${interim}`.trim()}
                    readOnly
                    placeholder="Lời nói của bạn hiện tại đây trong lúc ghi âm."
                  />
                </label>
                <p className="viva-muted">
                  Phiên này chỉ chấm theo giọng nói: máy chủ chép lời từ bản ghi
                  âm, không nhận nội dung gõ tay. Hết giờ, bản ghi được gửi tự
                  động. Mỗi câu chính có tối đa {state.maxFollowUps} lượt hỏi
                  thêm.
                </p>
                {!recording && !audio && (
                  <button
                    className="viva-button viva-button--ghost"
                    type="button"
                    onClick={finishVoiceAnswer}
                    disabled={busy}
                  >
                    {busy ? 'AI đang xử lý…' : 'Bỏ qua câu hỏi'}
                  </button>
                )}
              </>
            ) : (
              <>
                <div className="viva-actions">
                  {speechSupported && (
                    <button
                      type="button"
                      onClick={listening ? stopSpeech : startRecognition}
                      disabled={busy || !consent || recording}
                    >
                      {listening
                        ? 'Dừng nhận giọng nói'
                        : 'Bắt đầu nói / Nói tiếp'}
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={
                      recording
                        ? () => recorder.current?.stop()
                        : startRecording
                    }
                    disabled={busy || !consent || listening}
                  >
                    {recording ? 'Dừng ghi âm' : 'Ghi âm để AI chép lời'}
                  </button>
                  {audio && (
                    <button
                      type="button"
                      onClick={transcribe}
                      disabled={busy || recording}
                    >
                      Chép lời và gửi bản ghi
                    </button>
                  )}
                </div>
                {listening && <p role="status">Đang nghe… {interim}</p>}
                {recording && (
                  <p role="status">
                    Đang ghi âm. Dừng sớm để còn thời gian chép lời và gửi.
                  </p>
                )}
                <label className="viva-field">
                  Bản chép lời / Câu trả lời
                  <textarea
                    value={text}
                    maxLength={12000}
                    disabled={busy || listening || recording}
                    onChange={(e) => {
                      setText(e.target.value)
                      textRef.current = e.target.value
                    }}
                    placeholder="Bản chép lời xuất hiện tại đây. Có thể sửa thuật ngữ bị nhận sai."
                  />
                </label>
                <p className="viva-muted">
                  Hết giờ, hệ thống tự gửi nội dung đã nhận. Mỗi câu chính có
                  tối đa {state.maxFollowUps} lượt hỏi thêm.
                </p>
                <button
                  className="viva-button"
                  type="button"
                  onClick={send}
                  disabled={busy || recording}
                >
                  {busy
                    ? 'AI đang xử lý…'
                    : text.trim()
                      ? 'Gửi câu trả lời'
                      : 'Bỏ qua câu hỏi'}
                </button>
              </>
            )}
          </section>
        )
      )}
      <p>
        <Link to="/reports">Các bài thi và bảng điểm của tôi</Link>
      </p>
    </SessionLayout>
  )
}
