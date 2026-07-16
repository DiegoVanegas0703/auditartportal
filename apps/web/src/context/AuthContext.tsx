import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { DEFAULT_USER, getPermissions, MOCK_USERS } from '../data/mockUsers'
import type { Permission, User } from '../types'

interface AuthContextValue {
  user: User | null
  permissions: Permission
  isAuthenticated: boolean
  login: (userId: string) => void
  logout: () => void
  switchUser: (userId: string) => void
  availableUsers: User[]
}

const AuthContext = createContext<AuthContextValue | null>(null)

const SESSION_KEY = 'auditart_session'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)

  useEffect(() => {
    const stored = sessionStorage.getItem(SESSION_KEY)
    if (stored) {
      const found = MOCK_USERS.find((u) => u.id === stored)
      setUser(found ?? DEFAULT_USER)
    }
  }, [])

  const login = useCallback((userId: string) => {
    const found = MOCK_USERS.find((u) => u.id === userId) ?? DEFAULT_USER
    setUser(found)
    sessionStorage.setItem(SESSION_KEY, found.id)
  }, [])

  const logout = useCallback(() => {
    setUser(null)
    sessionStorage.removeItem(SESSION_KEY)
  }, [])

  const switchUser = useCallback((userId: string) => {
    const found = MOCK_USERS.find((u) => u.id === userId) ?? DEFAULT_USER
    setUser(found)
    sessionStorage.setItem(SESSION_KEY, found.id)
  }, [])

  const permissions = useMemo(
    () => (user ? getPermissions(user.role) : getPermissions('operador')),
    [user],
  )

  const value = useMemo(
    () => ({
      user,
      permissions,
      isAuthenticated: !!user,
      login,
      logout,
      switchUser,
      availableUsers: MOCK_USERS,
    }),
    [user, permissions, login, logout, switchUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return ctx
}
