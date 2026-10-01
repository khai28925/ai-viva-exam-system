import { describe, expect, it, vi } from 'vitest'
import { ApiError, get } from './httpClient.js'

describe('HTTP client', () => {
  it('fetches JSON and forwards the cancellation signal', async () => {
    const signal = new AbortController().signal
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ status: 'healthy' })))
    vi.stubGlobal('fetch', fetchMock)
    await expect(get('/health', { signal })).resolves.toEqual({
      status: 'healthy',
    })
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/health',
      expect.objectContaining({ signal }),
    )
  })

  it('preserves validation fields and traceId from Problem Details', async () => {
    const problem = {
      title: 'Validation failed',
      errors: { limit: ['Out of range'] },
      traceId: 'test',
    }
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify(problem), { status: 400 }),
        ),
    )
    await expect(get('/v1/exams')).rejects.toMatchObject({
      status: 400,
      problem,
    })
  })

  it('handles non-JSON error responses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('Bad gateway', { status: 502 })),
    )
    await expect(get('/health')).rejects.toBeInstanceOf(ApiError)
    await expect(get('/health')).rejects.toMatchObject({
      status: 502,
      problem: null,
    })
  })

  it('handles an empty 204 response', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(null, { status: 204 })),
    )
    await expect(get('/health')).resolves.toBeNull()
  })
})
