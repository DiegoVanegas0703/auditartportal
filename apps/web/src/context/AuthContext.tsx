import {

  useCallback,

  useEffect,

  useMemo,

  useState,

  type ReactNode,

} from 'react'

import { authApi, mapAuthUser, type AuthUserDto } from '../api/auditartApi'

import { clearTokens, getAccessToken, getRefreshToken } from '../api/client'

import type { Permission, User } from '../types'

import { AuthContext, type AuthContextValue } from './auth-context'



type AuthUser = User & { permissions: Permission; mustChangePassword: boolean }



const emptyPermissions: Permission = {
  triage: false,
  operationalBoard: false,
  billing: false,
  allQueues: false,
  manageUsers: false,
  reports: false,
  precios: false,
}



export function AuthProvider({ children }: { children: ReactNode }) {

  const [user, setUser] = useState<AuthUser | null>(null)

  const [loading, setLoading] = useState(true)

  const [error, setError] = useState<string | null>(null)



  const applyAuthUser = useCallback((dto: AuthUserDto) => {

    const mapped = mapAuthUser(dto)

    setUser({ ...mapped, mustChangePassword: dto.mustChangePassword })

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



  const login = useCallback(

    async (email: string, password: string) => {

      setError(null)

      const tokens = await authApi.login(email, password)

      applyAuthUser(tokens.user)

      return tokens.user.mustChangePassword

    },

    [applyAuthUser],

  )



  const logout = useCallback(async () => {

    await authApi.logout(getRefreshToken())

    setUser(null)

  }, [])



  const completePasswordChange = useCallback(() => {

    setUser((prev) => (prev ? { ...prev, mustChangePassword: false } : prev))

  }, [])



  const permissions = useMemo(

    () => user?.permissions ?? emptyPermissions,

    [user],

  )



  const value = useMemo<AuthContextValue>(

    () => ({

      user,

      permissions,

      isAuthenticated: !!user,

      mustChangePassword: user?.mustChangePassword ?? false,

      loading,

      error,

      login,

      logout,

      completePasswordChange,

    }),

    [user, permissions, loading, error, login, logout, completePasswordChange],

  )



  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>

}


