import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../api/interviewApi.js'
import { AuthContext } from '../auth/authContext.js'
import InterviewPage from './InterviewPage.jsx'

vi.mock('../../api/interviewApi.js', () => ({
  resumeInterview: vi.fn(),
  submitAnswer: vi.fn(),
  transcribeAudio: vi.fn(),
}))

function renderPage() {
  return render(
    <AuthContext.Provider
      value={{
        user: { role: 'STUDENT', email: 'student@local' },
        logout: vi.fn(),
      }}
    >
      <MemoryRouter initialEntries={['/interviews/attempt-1']}>
        <Routes>
          <Route path="/interviews/:attemptId" element={<InterviewPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('AI viva interview', () => {
  let listener
  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal(
      'SpeechRecognition',
      class {
        constructor() {
          listener = this
        }
        start() {}
        stop() {}
        abort() {}
      },
    )
    api.resumeInterview.mockResolvedValue({
      id: 'attempt-1',
      title: 'Demo viva',
      status: 'InProgress',
      language: 'vi-VN',
      totalMainQuestions: 1,
      maxFollowUps: 1,
      serverTime: new Date().toISOString(),
      currentQuestion: {
        id: 'question-1',
        content: 'Explain dependency injection.',
        source: 'Bank',
        mainQuestionNumber: 1,
        followUpNumber: 0,
        deadline: new Date(Date.now() + 120000).toISOString(),
      },
    })
    api.submitAnswer.mockResolvedValue({
      id: 'attempt-1',
      title: 'Demo viva',
      status: 'Completed',
      serverTime: new Date().toISOString(),
      currentQuestion: null,
    })
  })

  it('waits for consent, receives speech transcript, submits it and links to the report', async () => {
    const user = userEvent.setup()
    renderPage()
    expect(api.resumeInterview).not.toHaveBeenCalled()
    const start = screen.getByRole('button', { name: /Bắt đầu \/ Tiếp tục/ })
    expect(start).toBeDisabled()
    await user.click(screen.getByRole('checkbox', { name: /Tôi đồng ý/ }))
    await user.click(start)
    expect(
      await screen.findByText('Explain dependency injection.'),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Bắt đầu nói/ }))
    act(() =>
      listener.onresult({
        results: [
          {
            isFinal: true,
            0: { transcript: 'Dependencies are supplied from outside.' },
          },
        ],
      }),
    )
    expect(screen.getByRole('textbox')).toHaveValue(
      'Dependencies are supplied from outside.',
    )
    await user.click(screen.getByRole('button', { name: 'Gửi câu trả lời' }))
    await waitFor(() =>
      expect(api.submitAnswer).toHaveBeenCalledWith(
        'attempt-1',
        'question-1',
        'Dependencies are supplied from outside.',
      ),
    )
    expect(
      await screen.findByRole('link', { name: /Xem bảng điểm/ }),
    ).toHaveAttribute('href', '/reports/attempt-1')
  })

  it('in a voice-only session records the answer and submits the server transcript, never typed text', async () => {
    api.resumeInterview.mockResolvedValue({
      id: 'attempt-1',
      title: 'Demo viva',
      status: 'InProgress',
      language: 'vi-VN',
      answerMode: 'Voice',
      totalMainQuestions: 1,
      maxFollowUps: 1,
      serverTime: new Date().toISOString(),
      currentQuestion: {
        id: 'question-1',
        content: 'Explain dependency injection.',
        source: 'Bank',
        mainQuestionNumber: 1,
        followUpNumber: 0,
        deadline: new Date(Date.now() + 120000).toISOString(),
      },
    })
    api.transcribeAudio.mockResolvedValue({ text: 'Server transcript' })
    const stopTrack = vi.fn()
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: {
        getUserMedia: vi
          .fn()
          .mockResolvedValue({ getTracks: () => [{ stop: stopTrack }] }),
      },
    })
    vi.stubGlobal(
      'MediaRecorder',
      class {
        state = 'inactive'
        mimeType = 'audio/webm'
        start() {
          this.state = 'recording'
        }
        stop() {
          this.state = 'inactive'
          this.ondataavailable?.({
            data: new Blob(['voice'], { type: 'audio/webm' }),
          })
          this.onstop?.()
        }
      },
    )
    const user = userEvent.setup()
    renderPage()
    await user.click(screen.getByRole('checkbox', { name: /Tôi đồng ý/ }))
    await user.click(
      screen.getByRole('button', { name: /Bắt đầu \/ Tiếp tục/ }),
    )
    const preview = await screen.findByRole('textbox')
    expect(preview).toHaveAttribute('readonly')
    expect(
      screen.queryByRole('button', { name: 'Gửi câu trả lời' }),
    ).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Bắt đầu trả lời' }))
    act(() =>
      listener.onresult({
        results: [{ isFinal: true, 0: { transcript: 'Live preview words' } }],
      }),
    )
    expect(preview).toHaveValue('Live preview words')
    await user.click(
      screen.getByRole('button', { name: 'Kết thúc và gửi câu trả lời' }),
    )

    await waitFor(() =>
      expect(api.submitAnswer).toHaveBeenCalledWith(
        'attempt-1',
        'question-1',
        'Server transcript',
      ),
    )
    expect(api.transcribeAudio).toHaveBeenCalledWith(
      'attempt-1',
      'question-1',
      expect.any(Blob),
    )
    expect(stopTrack).toHaveBeenCalled()
    expect(await screen.findByText('Đã hoàn thành bài thi')).toBeInTheDocument()
  })

  it('retains the answer and displays the provider error so the student can retry', async () => {
    api.submitAnswer.mockRejectedValueOnce(
      new Error('Dịch vụ AI tạm thời không khả dụng'),
    )
    const user = userEvent.setup()
    renderPage()
    await user.click(screen.getByRole('checkbox', { name: /Tôi đồng ý/ }))
    await user.click(
      screen.getByRole('button', { name: /Bắt đầu \/ Tiếp tục/ }),
    )
    await user.type(await screen.findByRole('textbox'), 'My answer')
    await user.click(screen.getByRole('button', { name: 'Gửi câu trả lời' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Dịch vụ AI tạm thời không khả dụng',
    )
    expect(screen.getByRole('textbox')).toHaveValue('My answer')
    await user.click(screen.getByRole('button', { name: 'Gửi câu trả lời' }))
    expect(await screen.findByText('Đã hoàn thành bài thi')).toBeInTheDocument()
  })
})
