import {
  AlertTriangle,
  ClipboardList,
  FileSpreadsheet,
  Inbox,
  LayoutDashboard,
  LogOut,
} from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useAudits } from '../../context/AuditContext'
import { ROLE_LABELS } from '../../types'
import { getInitials } from '../../utils/format'
import { Logo } from '../ui/Logo'

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard', perm: 'any' as const },
  { to: '/triage', icon: Inbox, label: 'Bandeja Triage', perm: 'triage' as const },
  { to: '/tablero', icon: ClipboardList, label: 'Tablero Operativo', perm: 'operationalBoard' as const },
  { to: '/facturacion', icon: FileSpreadsheet, label: 'Facturación', perm: 'billing' as const },
]

export function Sidebar() {
  const { user, permissions, logout } = useAuth()
  const { getStats } = useAudits()
  const stats = getStats()

  if (!user) return null

  const visibleItems = navItems.filter((item) => {
    if (item.perm === 'any') return true
    return permissions[item.perm]
  })

  return (
    <aside className="sidebar-gradient relative flex w-[260px] shrink-0 flex-col text-white">
      <div className="relative z-10 border-b border-white/8 px-6 py-6">
        <Logo size="sm" variant="light" />
      </div>

      <div className="relative z-10 px-4 pt-6">
        <p className="mb-3 px-3 text-[10px] font-bold uppercase tracking-[0.15em] text-white/40">
          Navegación
        </p>
        <nav className="space-y-1">
          {visibleItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                `group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all duration-200 ${
                  isActive
                    ? 'nav-active text-white'
                    : 'text-white/60 hover:bg-white/8 hover:text-white'
                }`
              }
            >
              <item.icon size={18} strokeWidth={2} />
              <span className="flex-1">{item.label}</span>
              {item.to === '/triage' && stats.emailsPendientes > 0 && (
                <span className="rounded-full bg-red-500 px-2 py-0.5 text-[10px] font-bold shadow-sm">
                  {stats.emailsPendientes}
                </span>
              )}
              {item.to === '/tablero' && stats.slaAlertas > 0 && (
                <span className="flex items-center gap-0.5 rounded-full bg-orange-500 px-2 py-0.5 text-[10px] font-bold shadow-sm">
                  <AlertTriangle size={9} />
                  {stats.slaAlertas}
                </span>
              )}
            </NavLink>
          ))}
        </nav>
      </div>

      <div className="relative z-10 mt-auto border-t border-white/8 p-4">
        <div className="mb-3 flex items-center gap-3 rounded-xl bg-white/6 p-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-auditart-blue-light to-auditart-blue text-sm font-bold shadow-md">
            {getInitials(user.name)}
          </div>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-semibold">{user.name}</p>
            <p className="truncate text-xs text-white/50">{ROLE_LABELS[user.role]}</p>
          </div>
        </div>
        <button
          onClick={() => void logout()}
          className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm text-white/50 transition-all hover:bg-white/8 hover:text-white"
        >
          <LogOut size={16} />
          Cerrar sesión
        </button>
      </div>
    </aside>
  )
}
