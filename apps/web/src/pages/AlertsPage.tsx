import { Bell, CheckCheck, Loader2, RefreshCw } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { alertsApi, type InAppAlertDto } from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'
import { QUEUE_LABELS, STATUS_LABELS, type AuditQueue, type AuditStatus } from '../types'
import { formatDateTime } from '../utils/format'

export function AlertsPage() {
  const [items, setItems] = useState<InAppAlertDto[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [unreadOnly, setUnreadOnly] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setItems(await alertsApi.list(unreadOnly))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar alertas')
    } finally {
      setLoading(false)
    }
  }, [unreadOnly])

  useEffect(() => {
    void load()
  }, [load])

  const markRead = async (id: string) => {
    setBusy(true)
    try {
      await alertsApi.markRead(id)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo marcar como leída')
    } finally {
      setBusy(false)
    }
  }

  const markAll = async () => {
    setBusy(true)
    try {
      await alertsApi.markAllRead()
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron marcar todas')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Alertas SLA"
        subtitle="Avisos en la app · operador asignado y jefatura"
        action={
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => setUnreadOnly((v) => !v)}
              className="rounded-full bg-white px-4 py-2 text-sm font-semibold text-auditart-navy shadow-sm ring-1 ring-gray-200"
            >
              {unreadOnly ? 'Ver todas' : 'Solo no leídas'}
            </button>
            <button
              type="button"
              onClick={() => void load()}
              disabled={loading}
              className="flex items-center gap-2 rounded-full bg-white px-4 py-2 text-sm font-semibold text-auditart-navy shadow-sm ring-1 ring-gray-200 disabled:opacity-50"
            >
              <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
              Actualizar
            </button>
            <button
              type="button"
              onClick={() => void markAll()}
              disabled={busy}
              className="btn-primary flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50"
            >
              <CheckCheck size={16} />
              Marcar todas leídas
            </button>
          </div>
        }
      />

      {error && (
        <div className="mb-5 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      ) : items.length === 0 ? (
        <div className="card-flat flex flex-col items-center py-16 text-auditart-muted">
          <Bell size={28} className="mb-3 opacity-40" />
          <p className="text-sm font-medium">No hay alertas{unreadOnly ? ' sin leer' : ''}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((alert) => (
            <div
              key={alert.id}
              className={`card-flat flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between ${
                alert.isRead ? 'opacity-70' : 'ring-1 ring-orange-200'
              }`}
            >
              <div className="min-w-0">
                <div className="mb-1 flex flex-wrap items-center gap-2">
                  <span
                    className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${
                      alert.kind.toLowerCase().includes('expired') ||
                      String(alert.kind).toLowerCase() === 'expired'
                        ? 'bg-red-100 text-red-700'
                        : 'bg-orange-100 text-orange-700'
                    }`}
                  >
                    {String(alert.kind).toLowerCase().includes('about') ||
                    String(alert.kind).toLowerCase() === 'abouttoexpire'
                      ? 'Próximo a vencer'
                      : 'Vencido'}
                  </span>
                  <span className="text-xs text-auditart-muted">
                    {QUEUE_LABELS[alert.queue as AuditQueue] ?? alert.queue} ·{' '}
                    {STATUS_LABELS[alert.serviceStatus as AuditStatus] ?? alert.serviceStatus}
                  </span>
                </div>
                <p className="text-sm font-semibold text-auditart-navy">{alert.message}</p>
                <p className="mt-1 text-xs text-auditart-muted">
                  Deadline {formatDateTime(alert.deadlineUtc)} · {formatDateTime(alert.createdAtUtc)}
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <Link
                  to={`/servicio/${alert.auditServiceId}`}
                  className="rounded-lg bg-auditart-blue/8 px-3 py-1.5 text-xs font-semibold text-auditart-blue"
                >
                  Ver servicio
                </Link>
                {!alert.isRead && (
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void markRead(alert.id)}
                    className="rounded-lg bg-white px-3 py-1.5 text-xs font-semibold text-auditart-navy ring-1 ring-gray-200 disabled:opacity-50"
                  >
                    Marcar leída
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
