import { describe, expect, it, vi } from 'vitest'
import { ApiError, get, post } from './httpClient.js'

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
      expect.objectContaining({ signal, credentials: 'include' }),
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

  it('fetches a fresh CSRF token for every mutation, including after login', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ token: 'before-login' })),
      )
      .mockResolvedValueOnce(new Response(JSON.stringify({ role: 'STUDENT' })))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ token: 'after-login' })),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await post('/v1/auth/login', {
      email: 'student@example.com',
      password: 'secret',
    })
    await post('/v1/auth/logout')

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      '/api/v1/auth/csrf',
      expect.objectContaining({ credentials: 'include' }),
    )
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      '/api/v1/auth/login',
      expect.objectContaining({
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'before-login' }),
      }),
    )
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      '/api/v1/auth/csrf',
      expect.objectContaining({ credentials: 'include' }),
    )
    expect(fetchMock).toHaveBeenNthCalledWith(
      4,
      '/api/v1/auth/logout',
      expect.objectContaining({
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'after-login' }),
      }),
    )
  })
})
