import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './components/auth/ProtectedRoute'
import { AppLayout } from './components/layout/AppLayout'
import { AuthProvider } from './context/AuthContext'
import { AuditProvider } from './context/AuditContext'
import { useAuth } from './context/useAuth'
import { AlertsPage } from './pages/AlertsPage'
import { AuditDetailPage } from './pages/AuditDetailPage'
import { BillingPage } from './pages/BillingPage'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { OperationalBoardPage } from './pages/OperationalBoardPage'
import { PacientePage } from './pages/PacientePage'
import { ReportsPage } from './pages/ReportsPage'
import { SlaRulesPage } from './pages/SlaRulesPage'
import { PrestadoresPage } from './pages/PrestadoresPage'
import { PreciosPage } from './pages/PreciosPage'
import { TriagePage } from './pages/TriagePage'
import { UsersAdminPage } from './pages/UsersAdminPage'

function AppRoutes() {
  const { isAuthenticated, loading, mustChangePassword } = useAuth()

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center text-auditart-gray">
        Cargando…
      </div>
    )
  }

  return (
    <Routes>
      <Route
        path="/login"
        element={
          isAuthenticated ? (
            <Navigate
              to={mustChangePassword ? '/cambiar-contrasena' : '/'}
              replace
            />
          ) : (
            <LoginPage />
          )
        }
      />
      <Route
        path="/cambiar-contrasena"
        element={
          isAuthenticated ? (
            <ProtectedRoute allowPasswordChange>
              <ChangePasswordPage />
            </ProtectedRoute>
          ) : (
            <Navigate to="/login" replace />
          )
        }
      />
      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route
          path="triage"
          element={
            <ProtectedRoute requiredPermission="triage">
              <TriagePage />
            </ProtectedRoute>
          }
        />
        <Route
          path="tablero"
          element={
            <ProtectedRoute requiredPermission="operationalBoard">
              <OperationalBoardPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="cronicos/nuevo"
          element={<Navigate to="/tablero" replace />}
        />
        <Route
          path="alertas"
          element={
            <ProtectedRoute requiredPermission="operationalBoard">
              <AlertsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="sla"
          element={
            <ProtectedRoute requiredPermission="reports">
              <SlaRulesPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="doctores"
          element={
            <ProtectedRoute requiredPermission="reports">
              <PrestadoresPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="precios"
          element={
            <ProtectedRoute requiredPermission="precios">
              <PreciosPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="auditorias"
          element={
            <ProtectedRoute requiredPermission="reports">
              <ReportsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="facturacion"
          element={
            <ProtectedRoute requiredPermission="billing">
              <BillingPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="usuarios"
          element={
            <ProtectedRoute requiredPermission="manageUsers">
              <UsersAdminPage />
            </ProtectedRoute>
          }
        />
        <Route path="servicio/:id" element={<AuditDetailPage />} />
        <Route
          path="pacientes/:id"
          element={
            <ProtectedRoute requiredPermission="operationalBoard">
              <PacientePage />
            </ProtectedRoute>
          }
        />
      </Route>
      <Route
        path="*"
        element={
          <Navigate to={mustChangePassword ? '/cambiar-contrasena' : '/'} replace />
        }
      />
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AuditProvider>
          <AppRoutes />
        </AuditProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
