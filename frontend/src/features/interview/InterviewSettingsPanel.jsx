import { useEffect, useState } from 'react'
import {
  getInterviewSettings,
  saveInterviewSettings,
} from '../../api/interviewApi.js'
import './viva.css'

export default function InterviewSettingsPanel({ session }) {
  const [settings, setSettings] = useState(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    getInterviewSettings(session.id, { signal: controller.signal })
      .then(setSettings)
      .catch((e) => {
        if (!controller.signal.aborted) setMessage(e.message)
      })
    return () => controller.abort()
  }, [session.id])
  async function save(event) {
    event.preventDefault()
    setBusy(true)
    try {
      await saveInterviewSettings(session.id, settings)
      setMessage('Đã lưu cấu hình vấn đáp.')
    } catch (e) {
      setMessage(e.message)
    } finally {
      setBusy(false)
    }
  }
  return (
    <form className="viva-panel" onSubmit={save}>
      <h3>Cấu hình phỏng vấn AI</h3>
      {message && <p role="status">{message}</p>}
      {settings && (
        <fieldset
          disabled={busy || session.status !== 'Draft'}
          className="viva-settings"
        >
          <label>
            Thời gian mỗi câu (giây)
            <input
              type="number"
              min="30"
              max="600"
              value={settings.answerTimeSeconds}
              onChange={(e) =>
                setSettings({
                  ...settings,
                  answerTimeSeconds: Number(e.target.value),
                })
              }
            />
          </label>
          <label>
            Số lượt hỏi thêm tối đa/câu
            <input
              type="number"
              min="0"
              max="5"
              value={settings.maxFollowUps}
              onChange={(e) =>
                setSettings({
                  ...settings,
                  maxFollowUps: Number(e.target.value),
                })
              }
            />
          </label>
          <label>
            Ngôn ngữ
            <select
              value={settings.language}
              onChange={(e) =>
                setSettings({ ...settings, language: e.target.value })
              }
            >
              <option value="vi-VN">Tiếng Việt</option>
              <option value="en-US">English</option>
            </select>
          </label>
          <label>
            Cách trả lời
            <select
              value={settings.answerMode ?? 'VoiceOrText'}
              onChange={(e) =>
                setSettings({ ...settings, answerMode: e.target.value })
              }
            >
              <option value="Voice">
                Chỉ giọng nói (chấm bản chép lời từ ghi âm)
              </option>
              <option value="VoiceOrText">
                Giọng nói, cho phép sửa/gõ bản chép lời
              </option>
            </select>
          </label>
          <button type="submit">Lưu cấu hình</button>
        </fieldset>
      )}
    </form>
  )
}
