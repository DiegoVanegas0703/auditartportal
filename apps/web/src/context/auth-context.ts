import { createContext } from 'react'
import type { Permission, User } from '../types'

type AuthUser = User & { permissions: Permission; mustChangePassword: boolean }

export interface AuthContextValue {
  user: AuthUser | null
  permissions: Permission
  isAuthenticated: boolean
  mustChangePassword: boolean
  loading: boolean
  error: string | null
  login: (email: string, password: string) => Promise<boolean>
  logout: () => Promise<void>
  completePasswordChange: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
