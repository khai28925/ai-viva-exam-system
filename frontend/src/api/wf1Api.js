import { del, get, post, put } from './httpClient.js'

const subjectsPath = '/v1/subjects'

export const getSubjects = (options) => get(subjectsPath, options)
export const getSubjectWorkspace = (subjectId, options) =>
  get(`${subjectsPath}/${subjectId}/workspace`, options)
export const createSubject = (payload, options) =>
  post(subjectsPath, payload, options)
export const createSubjectQuestionBank = (subjectId, payload, options) =>
  post(`${subjectsPath}/${subjectId}/question-banks`, payload, options)
export const uploadStudyDocument = (subjectId, file, options) => {
  const body = new FormData()
  body.append('file', file)
  return post(`${subjectsPath}/${subjectId}/documents`, body, options)
}
export const deleteStudyDocument = (subjectId, documentId, options) =>
  del(`${subjectsPath}/${subjectId}/documents/${documentId}`, options)
export const reindexStudyDocument = (subjectId, documentId, options) =>
  post(
    `${subjectsPath}/${subjectId}/documents/${documentId}/reindex`,
    undefined,
    options,
  )
export const generateWf1Content = (subjectId, payload, options) =>
  post(`${subjectsPath}/${subjectId}/ai/generate`, payload, options)
export const updateQuestionCandidate = (
  subjectId,
  candidateId,
  payload,
  options,
) =>
  put(
    `${subjectsPath}/${subjectId}/question-candidates/${candidateId}`,
    payload,
    options,
  )
export const reviewQuestionCandidate = (
  subjectId,
  candidateId,
  approve,
  options,
) =>
  post(
    `${subjectsPath}/${subjectId}/question-candidates/${candidateId}/review`,
    { approve },
    options,
  )
export const saveDraftRubrics = (subjectId, rubrics, options) =>
  put(`${subjectsPath}/${subjectId}/rubric-sets/draft`, { rubrics }, options)
export const approveRubricSet = (subjectId, rubricSetId, options) =>
  post(
    `${subjectsPath}/${subjectId}/rubric-sets/${rubricSetId}/approve`,
    undefined,
    options,
  )
