import { AlertTriangle, ExternalLink, MessageCircle, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../components/ui/PageHeader'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAuth } from '../context/AuthContext'
import { filterAuditsForUser, getUrgencyColor, useAudits } from '../context/AuditContext'
import {
  QUEUE_LABELS,
  SERVICE_LABELS,
  type AuditStatus,
} from '../types'
import { formatDateTime, formatCurrency } from '../utils/format'

const STATUS_FILTERS: { key: AuditStatus | 'todos'; label: string; dot?: string }[] = [
  { key: 'todos', label: 'Todos' },
  { key: 'rojo', label: 'Rojo', dot: 'bg-red-500' },
  { key: 'amarillo', label: 'Amarillo', dot: 'bg-yellow-500' },
  { key: 'azul', label: 'Azul', dot: 'bg-blue-500' },
  { key: 'verde', label: 'Verde', dot: 'bg-green-500' },
]

const rowStatusClass: Record<AuditStatus, string> = {
  rojo: 'row-status-rojo',
  amarillo: 'row-status-amarillo',
  azul: 'row-status-azul',
  verde: 'row-status-verde',
}

export function OperationalBoardPage() {
  const { user } = useAuth()
  const { audits } = useAudits()
  const [statusFilter, setStatusFilter] = useState<AuditStatus | 'todos'>('todos')
  const [search, setSearch] = useState('')

  const filtered = useMemo(() => {
    if (!user) return []
    let list = filterAuditsForUser(audits, user.id, user.role, user.queue)
    if (statusFilter !== 'todos') list = list.filter((a) => a.status === statusFilter)
    if (search) {
      const q = search.toLowerCase()
      list = list.filter(
        (a) =>
          a.paciente.toLowerCase().includes(q) ||
          a.art.toLowerCase().includes(q) ||
          String(a.numero).includes(q) ||
          a.dni.includes(q),
      )
    }
    return list
  }, [audits, user, statusFilter, search])

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Tablero Operativo"
        subtitle={`Reemplazo de planilla Excel · ${user?.queue ? QUEUE_LABELS[user.queue] : 'Todas las colas'}`}
        action={
          <div className="flex flex-wrap gap-2">
            {STATUS_FILTERS.filter((f) => f.key !== 'todos').map((f) => (
              <span
                key={f.key}
                className="inline-flex items-center gap-1.5 rounded-full bg-white px-3 py-1 text-xs font-medium text-auditart-gray ring-1 ring-gray-200"
              >
                <span className={`h-2 w-2 rounded-full ${f.dot}`} />
                {f.label}
              </span>
            ))}
          </div>
        }
      />

      {/* Toolbar */}
      <div className="card-flat mb-5 flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search
            size={16}
            className="absolute left-3.5 top-1/2 -translate-y-1/2 text-auditart-muted"
          />
          <input
            type="text"
            placeholder="Buscar por paciente, ART, DNI o N°..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="input-modern w-full py-2.5 pl-10 pr-4 text-sm"
          />
        </div>
        <div className="flex items-center gap-1.5 rounded-xl bg-gray-50 p-1">
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.key}
              onClick={() => setStatusFilter(f.key)}
              className={`flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-semibold transition-all ${
                statusFilter === f.key
                  ? 'bg-white text-auditart-navy shadow-sm'
                  : 'text-auditart-gray hover:text-auditart-navy'
              }`}
            >
              {f.dot && <span className={`h-2 w-2 rounded-full ${f.dot}`} />}
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {/* Table */}
      <div className="card-flat overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1100px] text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                <th className="px-5 py-4">N°</th>
                <th className="px-5 py-4">Paciente</th>
                <th className="px-5 py-4">ART</th>
                <th className="px-5 py-4">Servicio</th>
                <th className="px-5 py-4">Profesional</th>
                <th className="px-5 py-4">Operador</th>
                <th className="px-5 py-4">Turno</th>
                <th className="px-5 py-4">Valor</th>
                <th className="px-5 py-4">Estado</th>
                <th className="px-5 py-4">SLA</th>
                <th className="px-5 py-4"></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((audit, i) => {
                const slaWarning =
                  audit.status === 'azul' && (audit.slaHoursRemaining ?? 99) <= 12
                return (
                  <tr
                    key={audit.id}
                    className={`${rowStatusClass[audit.status]} border-b border-gray-50 transition-colors hover:bg-auditart-blue/3 ${
                      i % 2 === 0 ? 'bg-white' : 'bg-gray-50/40'
                    }`}
                  >
                    <td className="px-5 py-3.5">
                      <span className="font-mono text-xs font-bold text-auditart-navy">
                        #{audit.numero}
                      </span>
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="font-semibold text-auditart-navy">{audit.paciente}</div>
                      <div className="text-xs text-auditart-muted">{audit.dni}</div>
                    </td>
                    <td className="px-5 py-3.5 text-auditart-gray">{audit.art}</td>
                    <td className="px-5 py-3.5">
                      <div className="font-medium text-auditart-navy">
                        {SERVICE_LABELS[audit.tipoServicio]}
                      </div>
                      <div className="text-xs text-auditart-muted">{audit.especialidad}</div>
                    </td>
                    <td className="px-5 py-3.5 text-auditart-gray">
                      {audit.profesional ?? (
                        <span className="italic text-auditart-muted">Sin asignar</span>
                      )}
                    </td>
                    <td className="px-5 py-3.5 text-auditart-gray">{audit.operador}</td>
                    <td className="px-5 py-3.5 text-xs text-auditart-gray">
                      {audit.fechaTurno ? formatDateTime(audit.fechaTurno) : '—'}
                    </td>
                    <td className="px-5 py-3.5 font-semibold text-auditart-navy">
                      {formatCurrency(audit.valorPactado)}
                    </td>
                    <td className="px-5 py-3.5">
                      <StatusBadge status={audit.status} compact />
                    </td>
                    <td className="px-5 py-3.5">
                      {slaWarning ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-xs font-bold text-red-700">
                          <AlertTriangle size={11} />
                          {audit.slaHoursRemaining! <= 0
                            ? 'Vencido'
                            : `${audit.slaHoursRemaining}h`}
                        </span>
                      ) : audit.slaHoursRemaining != null ? (
                        <span className="text-xs text-auditart-muted">
                          {audit.slaHoursRemaining}h
                        </span>
                      ) : (
                        <span className="text-auditart-muted">—</span>
                      )}
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <span
                          className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${getUrgencyColor(audit.urgency)}`}
                        >
                          {audit.urgency}
                        </span>
                        <Link
                          to={`/servicio/${audit.id}`}
                          className="flex items-center gap-1 rounded-lg bg-auditart-blue/8 px-2.5 py-1 text-xs font-semibold text-auditart-blue transition-colors hover:bg-auditart-blue/15"
                        >
                          Ver <ExternalLink size={11} />
                        </Link>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>

        {filtered.length === 0 && (
          <div className="flex flex-col items-center py-16 text-auditart-gray">
            <Search size={32} className="mb-3 opacity-30" />
            <p className="font-medium">No hay servicios que coincidan</p>
            <p className="text-sm">Probá con otros filtros o términos de búsqueda</p>
          </div>
        )}

        {filtered.length > 0 && (
          <div className="border-t border-gray-100 bg-gray-50/50 px-5 py-3 text-xs text-auditart-muted">
            Mostrando {filtered.length} servicio(s)
          </div>
        )}
      </div>

      <div className="mt-5 flex items-center gap-3 rounded-2xl bg-gradient-to-r from-green-50 to-emerald-50 p-4 ring-1 ring-green-200/50">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-green-100">
          <MessageCircle size={16} className="text-green-600" />
        </div>
        <p className="text-sm text-green-800">
          <span className="font-semibold">WhatsApp simulado</span> — En producción se integrará
          WhatsApp Cloud API para alertas de urgencia, turnos y SLA.
        </p>
      </div>
    </div>
  )
}
