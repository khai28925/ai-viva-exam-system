import { useCallback, useEffect, useState } from 'react'
import {
  getCurrentUser,
  login as loginRequest,
  logout as logoutRequest,
} from './authApi.js'
import { AuthContext } from './authContext.js'

export function AuthProvider({ children }) {
  const [session, setSession] = useState({ status: 'loading', user: null })

  const loadSession = useCallback(async (signal) => {
    try {
      const user = await getCurrentUser(signal)
      if (!signal?.aborted) setSession({ status: 'authenticated', user })
    } catch (error) {
      if (signal?.aborted || error.name === 'AbortError') return
      setSession({
        status: error.status === 401 ? 'anonymous' : 'error',
        user: null,
      })
    }
  }, [])

  const refresh = useCallback(
    (signal) => {
      setSession({ status: 'loading', user: null })
      return loadSession(signal)
    },
    [loadSession],
  )

  useEffect(() => {
    const controller = new AbortController()
    getCurrentUser(controller.signal)
      .then((user) => {
        if (!controller.signal.aborted) {
          setSession({ status: 'authenticated', user })
        }
      })
      .catch((error) => {
        if (controller.signal.aborted || error.name === 'AbortError') return
        setSession({
          status: error.status === 401 ? 'anonymous' : 'error',
          user: null,
        })
      })
    return () => controller.abort()
  }, [])

  async function login(credentials) {
    const user = await loginRequest(credentials)
    setSession({ status: 'authenticated', user })
    return user
  }

  async function logout() {
    try {
      await logoutRequest()
    } catch (error) {
      if (error.status !== 401) throw error
    }
    setSession({ status: 'anonymous', user: null })
  }

  const invalidate = useCallback(() => {
    setSession({ status: 'anonymous', user: null })
  }, [])

  return (
    <AuthContext.Provider
      value={{ ...session, refresh, login, logout, invalidate }}
    >
      {children}
    </AuthContext.Provider>
  )
}
