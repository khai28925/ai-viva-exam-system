import { get } from './httpClient.js'

export function getHealth(signal) {
  return get('/health', { signal })
}
