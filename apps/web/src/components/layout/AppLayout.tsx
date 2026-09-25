import { Bell } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, Outlet } from 'react-router-dom'
import { alertsApi } from '../../api/auditartApi'
import { useAuth } from '../../context/useAuth'
import { ROLE_LABELS } from '../../types'
import { getInitials } from '../../utils/format'
import { Sidebar } from './Sidebar'

export function AppLayout() {
  const { user, permissions } = useAuth()
  const [unreadAlerts, setUnreadAlerts] = useState(0)

  useEffect(() => {
    if (!permissions.operationalBoard) return
    let cancelled = false
    const load = () => {
      void alertsApi
        .count()
        .then((r) => {
          if (!cancelled) setUnreadAlerts(r.unread)
        })
        .catch(() => {
          if (!cancelled) setUnreadAlerts(0)
        })
    }
    load()
    const timer = window.setInterval(load, 60000)
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [permissions.operationalBoard])

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="app-bg flex flex-1 flex-col">
        <header className="glass-header sticky top-0 z-20 flex items-center justify-between px-8 py-4">
          <div>
            <h1 className="text-lg font-bold text-auditart-navy">
              Hola, {user?.name.split(' ')[0]}
            </h1>
            <p className="text-sm text-auditart-gray">
              {user ? ROLE_LABELS[user.role] : ''} ·{' '}
              <span className="font-medium text-auditart-green">API conectada</span>
            </p>
          </div>

          <div className="flex items-center gap-3">
            {permissions.operationalBoard && (
              <Link
                to="/alertas"
                className="hidden items-center gap-2 rounded-full bg-orange-50 px-3 py-1.5 text-xs font-semibold text-orange-700 ring-1 ring-orange-200 hover:bg-orange-100 sm:flex"
              >
                <Bell size={14} className={unreadAlerts > 0 ? 'animate-pulse-soft' : ''} />
                {unreadAlerts > 0 ? `${unreadAlerts} alertas SLA` : 'Alertas SLA'}
              </Link>
            )}

            <div className="flex items-center gap-2 rounded-full bg-auditart-blue/8 px-3 py-1.5 ring-1 ring-auditart-blue/15">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-auditart-blue to-auditart-blue-dark text-[10px] font-bold text-white">
                {user ? getInitials(user.name) : ''}
              </div>
              <span className="hidden text-xs font-medium text-auditart-navy sm:inline">
                Live API
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
