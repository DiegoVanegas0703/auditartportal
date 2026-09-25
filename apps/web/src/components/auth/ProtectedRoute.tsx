import { Navigate } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import type { Permission } from '../../types'

interface ProtectedRouteProps {
  children: React.ReactNode
  requiredPermission?: keyof Permission
  allowPasswordChange?: boolean
}

export function ProtectedRoute({
  children,
  requiredPermission,
  allowPasswordChange = false,
}: ProtectedRouteProps) {
  const { isAuthenticated, permissions, mustChangePassword, loading } = useAuth()

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

  if (mustChangePassword && !allowPasswordChange) {
    return <Navigate to="/cambiar-contrasena" replace />
  }

  if (requiredPermission && !permissions[requiredPermission]) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
