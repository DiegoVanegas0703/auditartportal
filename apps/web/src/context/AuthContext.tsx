import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  authApi,
  DEV_LOGIN_USERS,
  mapAuthUser,
  type AuthUserDto,
} from '../api/auditartApi'
import { clearTokens, getAccessToken, getRefreshToken } from '../api/client'
import type { Permission, User } from '../types'

type AuthUser = User & { permissions: Permission }

interface AuthContextValue {
  user: AuthUser | null
  permissions: Permission
  isAuthenticated: boolean
  loading: boolean
  error: string | null
  loginDev: (email: string) => Promise<void>
  logout: () => Promise<void>
  switchDevUser: (email: string) => Promise<void>
  availableDevUsers: typeof DEV_LOGIN_USERS
}

const AuthContext = createContext<AuthContextValue | null>(null)

const emptyPermissions: Permission = {
  triage: false,
  operationalBoard: false,
  billing: false,
  allQueues: false,
  manageUsers: false,
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const applyAuthUser = useCallback((dto: AuthUserDto) => {
    setUser(mapAuthUser(dto))
  }, [])

  useEffect(() => {
    const boot = async () => {
      const token = getAccessToken()
      if (!token) {
        setLoading(false)
        return
      }
      try {
        const me = await authApi.me()
        applyAuthUser(me)
      } catch {
        clearTokens()
        setUser(null)
      } finally {
        setLoading(false)
      }
    }
    void boot()
  }, [applyAuthUser])

  const loginDev = useCallback(
    async (email: string) => {
      setError(null)
      const tokens = await authApi.loginDev(email)
      applyAuthUser(tokens.user)
    },
    [applyAuthUser],
  )

  const logout = useCallback(async () => {
    await authApi.logout(getRefreshToken())
    setUser(null)
  }, [])

  const switchDevUser = useCallback(
    async (email: string) => {
      await loginDev(email)
    },
    [loginDev],
  )

  const permissions = user?.permissions ?? emptyPermissions

  const value = useMemo(
    () => ({
      user,
      permissions,
      isAuthenticated: !!user,
      loading,
      error,
      loginDev,
      logout,
      switchDevUser,
      availableDevUsers: DEV_LOGIN_USERS,
    }),
    [user, permissions, loading, error, loginDev, logout, switchDevUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return ctx
}
