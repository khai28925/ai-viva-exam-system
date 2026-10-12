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

// The MVP has no pagination/count endpoint: load the nested collections in parallel.
// A failed request fails the whole load, never displaying an incomplete count as zero.
export async function getQuestionBankWorkspace(signal) {
  const banks = await getQuestionBanks(signal)
  const collections = await Promise.all(
    banks.map((bank) => getQuestions(bank.id, signal)),
  )
  return { banks, questions: collections.flat() }
}
