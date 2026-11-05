import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as wf1Api from '../../api/wf1Api.js'
import SubjectRubricCard from './SubjectRubricCard.jsx'

vi.mock('../../api/wf1Api.js', () => ({ getSubjectWorkspace: vi.fn() }))

function renderCard(bank, onAccessError = vi.fn()) {
  render(
    <MemoryRouter>
      <SubjectRubricCard bank={bank} onAccessError={onAccessError} />
    </MemoryRouter>,
  )
  return onAccessError
}

describe('subject rubric card on the question-bank page', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows the approved subject rubric set used for grading and links to its editor', async () => {
    wf1Api.getSubjectWorkspace.mockResolvedValue({
      draftRubricSet: null,
      approvedRubricSet: {
        id: 'set-1',
        version: 2,
        rubrics: [
          {
            id: 'rubric-1',
            name: 'Kiến thức',
            weightPercent: 70,
            criteria: [{ id: 'c-1', name: 'Chính xác', maxScore: 10 }],
          },
          {
            id: 'rubric-2',
            name: 'Diễn đạt',
            weightPercent: 30,
            criteria: [{ id: 'c-2', name: 'Mạch lạc', maxScore: 5 }],
          },
        ],
      },
    })
    renderCard({ id: 'bank-1', subjectId: 'subject-1' })

    expect(
      await screen.findByText('Phiên bản 2 · đã duyệt'),
    ).toBeInTheDocument()
    expect(screen.getByText('Kiến thức · 70%')).toBeInTheDocument()
    expect(screen.getByText('Mạch lạc (tối đa 5)')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Soạn / duyệt rubric của môn' }),
    ).toHaveAttribute('href', '/wf1-preparation?subject=subject-1')
  })

  it('explains that a session cannot open without an approved set', async () => {
    wf1Api.getSubjectWorkspace.mockResolvedValue({
      draftRubricSet: { id: 'draft' },
      approvedRubricSet: null,
    })
    renderCard({ id: 'bank-1', subjectId: 'subject-1' })
    expect(
      await screen.findByText(
        /chưa có bộ rubric được duyệt \(đã có bản nháp\)/,
      ),
    ).toBeInTheDocument()
  })

  it('keeps the page usable when another lecturer owns the subject', async () => {
    wf1Api.getSubjectWorkspace.mockRejectedValue({ status: 403 })
    const onAccessError = renderCard({ id: 'bank-1', subjectId: 'subject-1' })
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'thuộc giảng viên khác',
    )
    expect(onAccessError).not.toHaveBeenCalled()
  })
})
