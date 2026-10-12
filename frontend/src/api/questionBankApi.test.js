import { describe, expect, it, vi } from 'vitest'
import * as api from './questionBankApi.js'

const json = (value, status = 200) =>
  new Response(JSON.stringify(value), { status })

describe('Question Bank HTTP contract', () => {
  it.each([
    ['getQuestionBanks', [], '/v1/question-banks'],
    ['getQuestionBank', ['bank-1'], '/v1/question-banks/bank-1'],
    ['getQuestions', ['bank-1'], '/v1/question-banks/bank-1/questions'],
    [
      'getQuestion',
      ['bank-1', 'question-1'],
      '/v1/question-banks/bank-1/questions/question-1',
    ],
  ])(
    '%s uses the correct GET URL and forwards cancellation',
    async (method, args, path) => {
      const signal = new AbortController().signal
      const fetchMock = vi.fn().mockResolvedValue(json([]))
      vi.stubGlobal('fetch', fetchMock)
      await api[method](...args, signal)
      expect(fetchMock).toHaveBeenCalledExactlyOnceWith(
        `/api${path}`,
        expect.objectContaining({
          method: 'GET',
          signal,
          credentials: 'include',
        }),
      )
    },
  )

  it.each([
    [
      'createQuestionBank',
      [{ name: ' Bank ', description: '  ', id: 'ignored' }],
      'POST',
      '/v1/question-banks',
      { name: 'Bank', description: null },
    ],
    [
      'updateQuestionBank',
      ['bank-1', { name: ' Bank ', description: ' Desc ' }],
      'PUT',
      '/v1/question-banks/bank-1',
      { name: 'Bank', description: 'Desc' },
    ],
    [
      'createQuestion',
      ['bank-1', { content: ' Question? ', questionBankId: 'ignored' }],
      'POST',
      '/v1/question-banks/bank-1/questions',
      { content: 'Question?' },
    ],
    [
      'updateQuestion',
      ['bank-1', 'question-1', { content: ' Updated? ', id: 'ignored' }],
      'PUT',
      '/v1/question-banks/bank-1/questions/question-1',
      { content: 'Updated?' },
    ],
    [
      'deleteQuestionBank',
      ['bank-1'],
      'DELETE',
      '/v1/question-banks/bank-1',
      undefined,
    ],
    [
      'deleteQuestion',
      ['bank-1', 'question-1'],
      'DELETE',
      '/v1/question-banks/bank-1/questions/question-1',
      undefined,
    ],
  ])(
    '%s sends only contract fields with cookies and CSRF',
    async (method, args, verb, path, body) => {
      const signal = new AbortController().signal
      const response =
        verb === 'DELETE'
          ? new Response(null, { status: 204 })
          : json({ id: 'server-id' })
      const fetchMock = vi
        .fn()
        .mockResolvedValueOnce(json({ token: 'csrf-crud' }))
        .mockResolvedValueOnce(response)
      vi.stubGlobal('fetch', fetchMock)
      const result = await api[method](...args, signal)
      expect(fetchMock).toHaveBeenNthCalledWith(
        2,
        `/api${path}`,
        expect.objectContaining({
          method: verb,
          signal,
          credentials: 'include',
          headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-crud' }),
          ...(body === undefined ? {} : { body: JSON.stringify(body) }),
        }),
      )
      if (verb === 'DELETE') {
        expect(result).toBeNull()
        expect(fetchMock.mock.calls[1][1]).not.toHaveProperty('body')
      }
    },
  )

  it('retains problem details on a 409 rather than pretending the bank was deleted', async () => {
    const problem = {
      status: 409,
      title: 'Conflict',
      detail: 'Bank still contains questions.',
      traceId: 'trace-1',
    }
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(json({ token: 'csrf' }))
        .mockResolvedValueOnce(json(problem, 409)),
    )
    await expect(api.deleteQuestionBank('bank-1')).rejects.toMatchObject({
      status: 409,
      problem,
    })
  })

  it('loads every nested collection and handles empty banks', async () => {
    const fetchMock = vi.fn((path) =>
      Promise.resolve(
        json(
          path === '/api/v1/question-banks'
            ? [{ id: 'a' }, { id: 'b' }]
            : path.endsWith('/a/questions')
              ? [{ id: 'q', questionBankId: 'a' }]
              : [],
        ),
      ),
    )
    vi.stubGlobal('fetch', fetchMock)
    await expect(api.getQuestionBankWorkspace()).resolves.toEqual({
      banks: [{ id: 'a' }, { id: 'b' }],
      questions: [{ id: 'q', questionBankId: 'a' }],
    })
    fetchMock.mockReset().mockResolvedValue(json([]))
    await expect(api.getQuestionBankWorkspace()).resolves.toEqual({
      banks: [],
      questions: [],
    })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('does not silently turn a failed nested request into an empty collection', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(json([{ id: 'a' }]))
        .mockResolvedValueOnce(json({ title: 'Unavailable' }, 503)),
    )
    await expect(api.getQuestionBankWorkspace()).rejects.toMatchObject({
      status: 503,
    })
  })
})
