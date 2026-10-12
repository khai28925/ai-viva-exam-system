import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import QuestionBankWorkspace from './features/question-bank/QuestionBankWorkspace.jsx'
import {
  mockQuestionBanks,
  mockQuestions,
} from './features/question-bank/mockData.js'
import './styles/index.css'

export function QuestionBankPreview() {
  const initialState = new URLSearchParams(window.location.search).get('state')
  const [previewState, setPreviewState] = useState(initialState)
  const [questions, setQuestions] = useState(
    initialState === 'empty' ? [] : mockQuestions,
  )
  const banks = initialState === 'empty-banks' ? [] : mockQuestionBanks

  function createQuestion(bankId, request) {
    setQuestions((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        questionBankId: bankId,
        content: request.content,
        createdAt: new Date().toISOString(),
      },
    ])
  }

  function updateQuestion(bankId, questionId, request) {
    setQuestions((current) =>
      current.map((question) =>
        question.id === questionId && question.questionBankId === bankId
          ? { ...question, content: request.content }
          : question,
      ),
    )
  }

  function deleteQuestion(bankId, questionId) {
    setQuestions((current) =>
      current.filter(
        (question) =>
          question.id !== questionId || question.questionBankId !== bankId,
      ),
    )
  }

  return (
    <QuestionBankWorkspace
      banks={banks}
      questions={questions}
      isLoading={previewState === 'loading'}
      error={
        previewState === 'error' ? 'Kết nối dữ liệu tạm thời gián đoạn.' : null
      }
      onRetry={() => setPreviewState(null)}
      onCreateQuestion={createQuestion}
      onUpdateQuestion={updateQuestion}
      onDeleteQuestion={deleteQuestion}
    />
  )
}

createRoot(document.getElementById('root')).render(<QuestionBankPreview />)
