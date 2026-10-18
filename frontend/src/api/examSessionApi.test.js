import { describe, expect, it, vi } from 'vitest'
import * as api from './examSessionApi.js'

const json = (value) => new Response(JSON.stringify(value), { status: 200 })

describe('exam session selection HTTP contract', () => {
  it('loads the manager-only selection with cookies', async () => {
    const signal = new AbortController().signal
    const fetchMock = vi.fn().mockResolvedValue(json([]))
    vi.stubGlobal('fetch', fetchMock)
    await api.getExamSessionQuestions('session-1', signal)
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/exam-sessions/session-1/questions',
      expect.objectContaining({
        method: 'GET',
        signal,
        credentials: 'include',
      }),
    )
  })

  it('replaces the entire ordered ID list with CSRF protection', async () => {
    const signal = new AbortController().signal
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(json({ token: 'csrf-selection' }))
      .mockResolvedValueOnce(json([]))
    vi.stubGlobal('fetch', fetchMock)
    await api.replaceExamSessionQuestions('session-1', ['q-2', 'q-1'], signal)
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      '/api/v1/exam-sessions/session-1/questions',
      expect.objectContaining({
        method: 'PUT',
        signal,
        credentials: 'include',
        body: JSON.stringify({ questionIds: ['q-2', 'q-1'] }),
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-selection' }),
      }),
    )
  })
})
