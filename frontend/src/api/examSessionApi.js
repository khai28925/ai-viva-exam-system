import { del, get, post, put } from './httpClient.js'

const basePath = '/v1/exam-sessions'
const sessionPath = (id) => `${basePath}/${encodeURIComponent(id)}`
const editableFields = ({ title, questionBankId, status }) => ({
  title: title.trim(),
  questionBankId,
  status,
})

export const getExamSessions = (signal) => get(basePath, { signal })
export const createExamSession = (values, signal) =>
  post(basePath, editableFields(values), { signal })
export const updateExamSession = (id, values, signal) =>
  put(sessionPath(id), editableFields(values), { signal })
export const deleteExamSession = (id, signal) =>
  del(sessionPath(id), { signal })
export const joinExamSession = (id, signal) =>
  post(`${sessionPath(id)}/join`, undefined, { signal })
