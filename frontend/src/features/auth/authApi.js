import { get, post } from '../../api/httpClient.js'

export function getCurrentUser(signal) {
  return get('/v1/auth/me', { signal })
}

export function login(credentials) {
  return post('/v1/auth/login', credentials)
}

export function logout() {
  return post('/v1/auth/logout')
}
