import { get, post, put } from '../../api/httpClient.js'

export function getUsers(signal) {
  return get('/v1/admin/users', { signal })
}

export function createUser(account) {
  return post('/v1/admin/users', account)
}

export function updateUserProfile(userId, profile) {
  return put(`/v1/admin/users/${userId}/profile`, profile)
}
