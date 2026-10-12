const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

export class ApiError extends Error {
  constructor(status, problem) {
    super(
      problem?.detail ??
        problem?.title ??
        `API request failed with status ${status}`,
    )
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

async function readResponse(response) {
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new ApiError(response.status, problem)
  }

  return response.status === 204 ? null : response.json()
}

async function getCsrfToken(signal) {
  const response = await fetch(`${API_BASE_URL}/v1/auth/csrf`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
    signal,
  })
  const result = await readResponse(response)
  if (!result?.token) {
    throw new Error('Không thể lấy mã bảo vệ yêu cầu. Vui lòng thử lại.')
  }
  return result.token
}

export async function request(path, { method = 'GET', body, ...options } = {}) {
  const verb = method.toUpperCase()
  const headers = {
    Accept: 'application/json',
    ...options.headers,
  }

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (!['GET', 'HEAD', 'OPTIONS'].includes(verb)) {
    headers['X-CSRF-TOKEN'] = await getCsrfToken(options.signal)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    method: verb,
    credentials: 'include',
    headers,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })

  return readResponse(response)
}

export function get(path, options) {
  return request(path, options)
}

export function post(path, body, options) {
  return request(path, { ...options, method: 'POST', body })
}

export function put(path, body, options) {
  return request(path, { ...options, method: 'PUT', body })
}

export function del(path, options) {
  return request(path, { ...options, method: 'DELETE' })
}
