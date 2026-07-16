import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Clock,
  Inbox,
  Search,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../components/ui/PageHeader'
import { StatCard } from '../components/ui/StatCard'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAuth } from '../context/AuthContext'
import { filterAuditsForUser, useAudits } from '../context/AuditContext'
import { formatDateTime } from '../utils/format'

export function DashboardPage() {
  const { user, permissions } = useAuth()
  const { audits, emails, getStats } = useAudits()
  const stats = getStats()

  if (!user) return null

  const userAudits = filterAuditsForUser(audits, user.id, user.role, user.queue)
  const recentAudits = userAudits.slice(0, 5)
  const pendingEmails = emails.filter((e) => !e.assigned)

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Panel de control"
        subtitle={`Resumen operativo · ${formatDateTime(new Date().toISOString())}`}
      />

      <div className="mb-8 grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Búsqueda profesional" value={stats.rojo} icon={Search} variant="rojo" />
        <StatCard label="Coordinados / Pend. ART" value={stats.amarillo} icon={Clock} variant="amarillo" />
        <StatCard label="Doc. pendiente (SLA)" value={stats.azul} icon={AlertTriangle} variant="azul" alert={stats.slaAlertas} />
        <StatCard label="Listos facturación" value={stats.verde} icon={CheckCircle2} variant="verde" />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {permissions.triage && (
          <div className="card-flat p-6">
            <div className="mb-5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-auditart-blue/10">
                  <Inbox size={18} className="text-auditart-blue" />
                </div>
                <div>
                  <h3 className="font-bold text-auditart-navy">Emails pendientes</h3>
                  <p className="text-xs text-auditart-gray">Bandeja de triage</p>
                </div>
              </div>
              <Link
                to="/triage"
                className="flex items-center gap-1 rounded-lg bg-auditart-blue/8 px-3 py-1.5 text-xs font-semibold text-auditart-blue transition-colors hover:bg-auditart-blue/15"
              >
                Ver todos <ArrowRight size={12} />
              </Link>
            </div>

            {pendingEmails.length === 0 ? (
              <p className="py-8 text-center text-sm text-auditart-gray">No hay emails pendientes</p>
            ) : (
              <div className="space-y-2">
                {pendingEmails.slice(0, 3).map((email) => (
                  <div
                    key={email.id}
                    className="group flex items-start gap-3 rounded-xl border border-gray-100 p-4 transition-all hover:border-auditart-blue/20 hover:bg-auditart-blue/3"
                  >
                    <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-auditart-blue/10">
                      <Inbox size={14} className="text-auditart-blue" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold text-auditart-navy group-hover:text-auditart-blue">
                        {email.subject}
                      </p>
                      <p className="mt-0.5 text-xs text-auditart-gray">
                        {email.from} · {formatDateTime(email.receivedAt)}
                      </p>
                    </div>
                    {email.subject.toLowerCase().includes('urgent') && (
                      <span className="shrink-0 rounded-full bg-red-100 px-2 py-0.5 text-[10px] font-bold text-red-600">
                        URGENTE
                      </span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        <div className="card-flat p-6">
          <div className="mb-5 flex items-center justify-between">
            <div>
              <h3 className="font-bold text-auditart-navy">Últimos servicios</h3>
              <p className="text-xs text-auditart-gray">Actividad reciente</p>
            </div>
            {permissions.operationalBoard && (
              <Link
                to="/tablero"
                className="flex items-center gap-1 rounded-lg bg-auditart-blue/8 px-3 py-1.5 text-xs font-semibold text-auditart-blue transition-colors hover:bg-auditart-blue/15"
              >
                Ver tablero <ArrowRight size={12} />
              </Link>
            )}
          </div>

          <div className="space-y-2">
            {recentAudits.map((audit) => (
              <Link
                key={audit.id}
                to={`/servicio/${audit.id}`}
                className="group flex items-center justify-between rounded-xl border border-gray-100 p-4 transition-all hover:border-auditart-blue/20 hover:shadow-sm"
              >
                <div className="flex items-center gap-3">
                  <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-auditart-navy/5 font-mono text-xs font-bold text-auditart-navy">
                    {audit.numero}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-auditart-navy group-hover:text-auditart-blue">
                      {audit.paciente}
                    </p>
                    <p className="text-xs text-auditart-gray">{audit.art}</p>
                  </div>
                </div>
                <StatusBadge status={audit.status} compact />
              </Link>
            ))}
          </div>
        </div>
      </div>

      {stats.slaAlertas > 0 && permissions.operationalBoard && (
        <div className="mt-6 flex items-start gap-4 rounded-2xl border border-orange-200 bg-gradient-to-r from-orange-50 to-amber-50 p-5 shadow-sm">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-orange-100">
            <AlertTriangle className="text-orange-600" size={20} />
          </div>
          <div>
            <p className="font-bold text-orange-900">
              {stats.slaAlertas} servicio(s) con SLA próximo a vencer
            </p>
            <p className="mt-1 text-sm text-orange-700/80">
              Tenés 24/48hs post-turno para reclamar la consulta y procesarla ante la ART.
            </p>
            <Link
              to="/tablero"
              className="mt-3 inline-flex items-center gap-1 text-sm font-semibold text-orange-700 hover:underline"
            >
              Ir al tablero <ArrowRight size={14} />
            </Link>
          </div>
        </div>
      )}
    </div>
  )
}
