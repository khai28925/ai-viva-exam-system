import { del, get, post, put } from './httpClient.js'

const basePath = '/v1/question-banks'
const bankPath = (bankId) => `${basePath}/${encodeURIComponent(bankId)}`
const questionsPath = (bankId) => `${bankPath(bankId)}/questions`
const questionPath = (bankId, questionId) =>
  `${questionsPath(bankId)}/${encodeURIComponent(questionId)}`

// Only editable contract fields are sent; identifiers/timestamps belong to the API.
const bankBody = ({ name, description }) => ({
  name: name.trim(),
  description: description?.trim() || null,
})
const questionBody = ({ content }) => ({ content: content.trim() })

export const getQuestionBanks = (signal) => get(basePath, { signal })
export const getQuestionBank = (bankId, signal) =>
  get(bankPath(bankId), { signal })
export const createQuestionBank = (request, signal) =>
  post(basePath, bankBody(request), { signal })
export const updateQuestionBank = (bankId, request, signal) =>
  put(bankPath(bankId), bankBody(request), { signal })
export const deleteQuestionBank = (bankId, signal) =>
  del(bankPath(bankId), { signal })

export const getQuestions = (bankId, signal) =>
  get(questionsPath(bankId), { signal })
export const getQuestion = (bankId, questionId, signal) =>
  get(questionPath(bankId, questionId), { signal })
export const createQuestion = (bankId, request, signal) =>
  post(questionsPath(bankId), questionBody(request), { signal })
export const updateQuestion = (bankId, questionId, request, signal) =>
  put(questionPath(bankId, questionId), questionBody(request), { signal })
export const deleteQuestion = (bankId, questionId, signal) =>
  del(questionPath(bankId, questionId), { signal })

const keyPointsPath = (bankId, questionId) =>
  `${questionPath(bankId, questionId)}/expected-key-points`
const rubricPath = (bankId) => `${bankPath(bankId)}/rubric`
const criteriaPath = (bankId) => `${rubricPath(bankId)}/criteria`

export const getExpectedKeyPoints = (bankId, questionId, signal) =>
  get(keyPointsPath(bankId, questionId), { signal })
export const createExpectedKeyPoint = (bankId, questionId, request, signal) =>
  post(keyPointsPath(bankId, questionId), request, { signal })
export const updateExpectedKeyPoint = (
  bankId,
  questionId,
  pointId,
  request,
  signal,
) => put(`${keyPointsPath(bankId, questionId)}/${pointId}`, request, { signal })
export const deleteExpectedKeyPoint = (bankId, questionId, pointId, signal) =>
  del(`${keyPointsPath(bankId, questionId)}/${pointId}`, { signal })

export const getRubric = (bankId, signal) => get(rubricPath(bankId), { signal })
export const createRubric = (bankId, request, signal) =>
  post(rubricPath(bankId), request, { signal })
export const updateRubric = (bankId, request, signal) =>
  put(rubricPath(bankId), request, { signal })
export const deleteRubric = (bankId, signal) =>
  del(rubricPath(bankId), { signal })
export const getRubricCriteria = (bankId, signal) =>
  get(criteriaPath(bankId), { signal })
export const createRubricCriterion = (bankId, request, signal) =>
  post(criteriaPath(bankId), request, { signal })
export const updateRubricCriterion = (bankId, criterionId, request, signal) =>
  put(`${criteriaPath(bankId)}/${criterionId}`, request, { signal })
export const deleteRubricCriterion = (bankId, criterionId, signal) =>
  del(`${criteriaPath(bankId)}/${criterionId}`, { signal })

// The MVP has no pagination/count endpoint: load the nested collections in parallel.
// A failed request fails the whole load, never displaying an incomplete count as zero.
export async function getQuestionBankWorkspace(signal) {
  const banks = await getQuestionBanks(signal)
  const collections = await Promise.all(
    banks.map((bank) => getQuestions(bank.id, signal)),
  )
  return { banks, questions: collections.flat() }
}
