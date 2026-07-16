import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './components/auth/ProtectedRoute'
import { AppLayout } from './components/layout/AppLayout'
import { AuthProvider, useAuth } from './context/AuthContext'
import { AuditProvider } from './context/AuditContext'
import { AuditDetailPage } from './pages/AuditDetailPage'
import { BillingPage } from './pages/BillingPage'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { OperationalBoardPage } from './pages/OperationalBoardPage'
import { TriagePage } from './pages/TriagePage'

function AppRoutes() {
  const { isAuthenticated } = useAuth()

  return (
    <Routes>
      <Route
        path="/login"
        element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage />}
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
          path="facturacion"
          element={
            <ProtectedRoute requiredPermission="billing">
              <BillingPage />
            </ProtectedRoute>
          }
        />
        <Route path="servicio/:id" element={<AuditDetailPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
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
