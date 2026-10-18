import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as sessionApi from '../../api/examSessionApi.js'
import * as bankApi from '../../api/questionBankApi.js'
import SessionPreparationPanel from './SessionPreparationPanel.jsx'

vi.mock('../../api/examSessionApi.js', () => ({
  getExamSessionQuestions: vi.fn(),
  replaceExamSessionQuestions: vi.fn(),
  updateExamSession: vi.fn(),
}))
vi.mock('../../api/questionBankApi.js', () => ({
  getQuestions: vi.fn(),
  getRubric: vi.fn(),
  getRubricCriteria: vi.fn(),
  getExpectedKeyPoints: vi.fn(),
}))

const session = {
  id: 'session-1',
  title: 'Viva',
  questionBankId: 'bank-1',
  status: 'Draft',
}
const question = {
  id: 'question-1',
  questionBankId: 'bank-1',
  content: 'Explain SOLID',
}
const selection = [
  {
    questionId: 'question-1',
    contentSnapshot: 'Explain SOLID',
    displayOrder: 0,
    isRequired: true,
  },
]

describe('live session preparation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    bankApi.getQuestions.mockResolvedValue([question])
    bankApi.getRubric.mockResolvedValue({ id: 'rubric-1' })
    bankApi.getRubricCriteria.mockResolvedValue([{ weight: 1 }])
    bankApi.getExpectedKeyPoints.mockResolvedValue([{ id: 'point-1' }])
    sessionApi.getExamSessionQuestions.mockResolvedValue([])
    sessionApi.replaceExamSessionQuestions.mockResolvedValue(selection)
    sessionApi.updateExamSession.mockResolvedValue({
      ...session,
      status: 'Open',
    })
  })

  it('saves selected IDs, rechecks readiness, and publishes through the live API', async () => {
    const user = userEvent.setup()
    const onPublished = vi.fn()
    render(
      <SessionPreparationPanel
        session={session}
        onPublished={onPublished}
        onAccessError={vi.fn()}
      />,
    )
    expect(await screen.findByRole('checkbox')).not.toBeChecked()
    sessionApi.getExamSessionQuestions.mockResolvedValue(selection)
    await user.click(screen.getByRole('checkbox'))
    await waitFor(() =>
      expect(sessionApi.replaceExamSessionQuestions).toHaveBeenCalledWith(
        'session-1',
        ['question-1'],
      ),
    )
    const publish = await screen.findByRole('button', {
      name: /Mở phiên cho sinh viên/,
    })
    await waitFor(() => expect(publish).toBeEnabled())
    await user.click(publish)
    await waitFor(() =>
      expect(sessionApi.updateExamSession).toHaveBeenCalledWith('session-1', {
        title: 'Viva',
        questionBankId: 'bank-1',
        status: 'Open',
      }),
    )
    expect(onPublished).toHaveBeenCalledWith({ ...session, status: 'Open' })
  })
})
