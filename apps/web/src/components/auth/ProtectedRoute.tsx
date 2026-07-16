import { Navigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import type { Permission } from '../../types'

interface ProtectedRouteProps {
  children: React.ReactNode
  requiredPermission?: keyof Permission
}

export function ProtectedRoute({ children, requiredPermission }: ProtectedRouteProps) {
  const { isAuthenticated, permissions, loading } = useAuth()

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center text-auditart-gray">
        Cargando sesión…
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (requiredPermission && !permissions[requiredPermission]) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
