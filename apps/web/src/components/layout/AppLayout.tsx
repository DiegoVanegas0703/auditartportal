import { Bell, ChevronDown } from 'lucide-react'
import { Outlet } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useAudits } from '../../context/AuditContext'
import { ROLE_LABELS } from '../../types'
import { getInitials } from '../../utils/format'
import { Sidebar } from './Sidebar'

export function AppLayout() {
  const { user, switchUser, availableUsers, permissions } = useAuth()
  const { getStats } = useAudits()
  const stats = getStats()

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="app-bg flex flex-1 flex-col">
        <header className="glass-header sticky top-0 z-20 flex items-center justify-between px-8 py-4">
          <div>
            <h1 className="text-lg font-bold text-auditart-navy">
              Hola, {user?.name.split(' ')[0]} 👋
            </h1>
            <p className="text-sm text-auditart-gray">
              {user ? ROLE_LABELS[user.role] : ''} ·{' '}
              <span className="text-auditart-green font-medium">Sesión activa</span>
            </p>
          </div>

          <div className="flex items-center gap-3">
            {stats.slaAlertas > 0 && (
              <div className="hidden items-center gap-2 rounded-full bg-orange-50 px-3 py-1.5 text-xs font-semibold text-orange-700 ring-1 ring-orange-200 sm:flex">
                <Bell size={14} className="animate-pulse-soft" />
                {stats.slaAlertas} alertas SLA
              </div>
            )}

            {permissions.manageUsers && (
              <div className="relative">
                <select
                  value={user?.id}
                  onChange={(e) => switchUser(e.target.value)}
                  className="input-modern appearance-none cursor-pointer py-2 pl-3 pr-8 text-xs font-medium text-auditart-navy"
                  title="Cambiar usuario (solo admin)"
                >
                  {availableUsers.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name} — {ROLE_LABELS[u.role]}
                    </option>
                  ))}
                </select>
                <ChevronDown
                  size={14}
                  className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-auditart-gray"
                />
              </div>
            )}

            <div className="flex items-center gap-2 rounded-full bg-auditart-blue/8 px-3 py-1.5 ring-1 ring-auditart-blue/15">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-auditart-blue to-auditart-blue-dark text-[10px] font-bold text-white">
                {user ? getInitials(user.name) : ''}
              </div>
              <span className="hidden text-xs font-medium text-auditart-navy sm:inline">
                POC · Fase 2
              </span>
            </div>
          </div>
        </header>

        <main className="flex-1 overflow-auto px-8 py-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
