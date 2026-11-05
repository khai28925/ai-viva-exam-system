import { get, post, put } from './httpClient.js'

export const resumeInterview = (id, options) =>
  post(`/v1/interviews/${id}/resume`, undefined, options)
export const submitAnswer = (id, questionId, transcript) =>
  post(`/v1/interviews/${id}/answers`, { questionId, transcript })
export const transcribeAudio = (id, questionId, audio) => {
  const data = new FormData()
  data.append('file', audio, 'answer.webm')
  return post(`/v1/interviews/${id}/questions/${questionId}/transcribe`, data)
}
export const getInterviewSettings = (id, options) =>
  get(`/v1/exam-sessions/${id}/interview-settings`, options)
export const saveInterviewSettings = (id, settings) =>
  put(`/v1/exam-sessions/${id}/interview-settings`, settings)
export const getReports = (options) => get('/v1/reports', options)
export const getReport = (id, options) => get(`/v1/reports/${id}`, options)
export const finalizeReport = (id, request) =>
  post(`/v1/reports/${id}/finalize`, request)
export const getStatistics = (sessionId, options) =>
  get(`/v1/reports/sessions/${sessionId}/statistics`, options)
// xlsx is the grade-sheet layout (STT, MSSV, Họ và tên, per-question marks).
export const exportUrl = (sessionId, format = 'xlsx') =>
  `${import.meta.env.VITE_API_BASE_URL ?? '/api'}/v1/reports/sessions/${sessionId}/export?format=${format}`
