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

export async function get(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      Accept: 'application/json',
      ...options.headers,
    },
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new ApiError(response.status, problem)
  }

  return response.status === 204 ? null : response.json()
}
