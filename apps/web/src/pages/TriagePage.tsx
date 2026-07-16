import {
  ArrowRight,
  CheckCircle2,
  Loader2,
  Mail,
  Paperclip,
  Sparkles,
  Users,
  Zap,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { usersApi, mapListedUser } from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'
import { useAudits } from '../context/AuditContext'
import { QUEUE_LABELS, SERVICE_LABELS, type AuditQueue, type User } from '../types'
import { formatDateTime } from '../utils/format'

const QUEUES: { key: AuditQueue; icon: string; desc: string }[] = [
  { key: 'general', icon: '👥', desc: 'Operadores generales' },
  { key: 'telemedicina', icon: '🩺', desc: 'Cola telemedicina' },
  { key: 'cronicos', icon: '📋', desc: 'Cola crónicos' },
]

export function TriagePage() {
  const { emails, assignEmailToQueue, seedDemoEmails, loading } = useAudits()
  const [selectedEmail, setSelectedEmail] = useState('')
  const [selectedQueue, setSelectedQueue] = useState<AuditQueue>('general')
  const [operadorId, setOperadorId] = useState('')
  const [operators, setOperators] = useState<User[]>([])
  const [toast, setToast] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const pending = emails.filter((e) => !e.assigned)
  const current = emails.find((e) => e.id === selectedEmail) ?? pending[0]

  useEffect(() => {
    if (pending.length && !pending.some((e) => e.id === selectedEmail)) {
      setSelectedEmail(pending[0].id)
    }
  }, [pending, selectedEmail])

  useEffect(() => {
    if (current?.suggestedQueue) setSelectedQueue(current.suggestedQueue)
  }, [current?.id, current?.suggestedQueue])

  useEffect(() => {
    void usersApi
      .operators(selectedQueue)
      .then((list) => {
        const mapped = list.map(mapListedUser)
        setOperators(mapped)
        setOperadorId(mapped[0]?.id ?? '')
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Error operadores'))
  }, [selectedQueue])

  const queueHint = useMemo(
    () => QUEUES.find((q) => q.key === selectedQueue)?.desc,
    [selectedQueue],
  )

  const handleSeed = async () => {
    setBusy(true)
    setError(null)
    try {
      await seedDemoEmails()
      setToast('Emails de demo cargados')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron crear emails demo')
    } finally {
      setBusy(false)
    }
  }

  const handleAssign = async () => {
    if (!current || !operadorId) return
    setBusy(true)
    setError(null)
    try {
      await assignEmailToQueue(current.id, selectedQueue, operadorId)
      setToast(`Derivado a ${QUEUE_LABELS[selectedQueue]} · servicio en Rojo`)
      setTimeout(() => setToast(''), 4000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo derivar')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Bandeja de Triage"
        subtitle="Emails API · La jefa elige cola y operador · Siempre nace en Rojo"
        action={
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => void handleSeed()}
              disabled={busy}
              className="flex items-center gap-2 rounded-full bg-white px-4 py-2 text-sm font-semibold text-auditart-navy shadow-sm ring-1 ring-gray-200 hover:bg-gray-50 disabled:opacity-50"
            >
              <Sparkles size={16} className="text-auditart-blue" />
              Cargar emails demo
            </button>
            <div className="flex items-center gap-2 rounded-full bg-auditart-blue/8 px-4 py-2 text-sm font-semibold text-auditart-blue ring-1 ring-auditart-blue/15">
              <Mail size={16} />
              {pending.length} pendientes
            </div>
          </div>
        }
      />

      {toast && (
        <div className="toast-success mb-5 flex items-center gap-2 px-4 py-3 text-sm font-semibold">
          <CheckCircle2 size={16} />
          {toast}
        </div>
      )}
      {error && (
        <div className="mb-5 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      {loading && (
        <div className="mb-4 flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-5">
        <div className="space-y-3 lg:col-span-2">
          <div className="card-flat p-4">
            <h3 className="mb-3 text-xs font-bold uppercase tracking-wider text-auditart-gray">
              Pendientes ({pending.length})
            </h3>
            {pending.length === 0 ? (
              <p className="py-8 text-center text-sm text-auditart-muted">
                No hay emails. Usá “Cargar emails demo” mientras Gmail no esté conectado.
              </p>
            ) : (
              <div className="space-y-2">
                {pending.map((email) => {
                  const isUrgent = email.subject.toLowerCase().includes('urgent')
                  const active = (current?.id ?? selectedEmail) === email.id
                  return (
                    <button
                      key={email.id}
                      type="button"
                      onClick={() => setSelectedEmail(email.id)}
                      className={`w-full rounded-xl border-2 p-4 text-left transition-all ${
                        active
                          ? 'border-auditart-blue bg-auditart-blue/4 shadow-md'
                          : 'border-transparent bg-gray-50 hover:border-auditart-blue/20'
                      }`}
                    >
                      <div className="mb-2 flex items-center justify-between">
                        <span className="text-[11px] text-auditart-muted">
                          {formatDateTime(email.receivedAt)}
                        </span>
                        {isUrgent && (
                          <span className="flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-[10px] font-bold text-red-600">
                            <Zap size={9} /> URGENTE
                          </span>
                        )}
                      </div>
                      <p className="text-sm font-semibold text-auditart-navy">{email.subject}</p>
                      <p className="mt-1 truncate text-xs text-auditart-muted">{email.from}</p>
                    </button>
                  )
                })}
              </div>
            )}
          </div>
        </div>

        <div className="lg:col-span-3">
          {current ? (
            <div className="card-flat overflow-hidden">
              <div className="border-b border-gray-100 bg-gradient-to-r from-auditart-blue/5 to-transparent p-6">
                <h3 className="text-xl font-bold text-auditart-navy">{current.subject}</h3>
                <div className="mt-3 flex flex-wrap gap-3 text-sm text-auditart-gray">
                  <span className="rounded-lg bg-white px-2.5 py-1 text-xs font-medium ring-1 ring-gray-200">
                    De: {current.from}
                  </span>
                  <span className="text-xs">{formatDateTime(current.receivedAt)}</span>
                  {current.attachments > 0 && (
                    <span className="flex items-center gap-1 rounded-lg bg-auditart-blue/8 px-2.5 py-1 text-xs font-medium text-auditart-blue">
                      <Paperclip size={12} />
                      {current.attachments} adjunto(s)
                    </span>
                  )}
                </div>
              </div>

              <div className="p-6">
                <p className="whitespace-pre-wrap text-sm leading-relaxed text-auditart-navy/80">
                  {current.body}
                </p>
                <div className="mt-5 grid grid-cols-2 gap-3">
                  {[
                    { label: 'ART', value: current.art },
                    { label: 'Servicio', value: SERVICE_LABELS[current.serviceType] },
                    { label: 'Paciente', value: current.patientName ?? '—' },
                    { label: 'Cola sugerida', value: QUEUE_LABELS[current.suggestedQueue] },
                  ].map((item) => (
                    <div
                      key={item.label}
                      className="rounded-xl bg-auditart-light p-3.5 ring-1 ring-auditart-blue/8"
                    >
                      <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
                        {item.label}
                      </p>
                      <p className="mt-0.5 text-sm font-semibold text-auditart-navy">{item.value}</p>
                    </div>
                  ))}
                </div>
              </div>

              <div className="border-t border-gray-100 bg-gray-50/50 p-6">
                <p className="mb-4 flex items-center gap-2 text-sm font-bold text-auditart-navy">
                  <Users size={16} className="text-auditart-blue" />
                  Derivar (elegí cola y operador)
                </p>

                <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
                  {QUEUES.map((q) => (
                    <button
                      key={q.key}
                      type="button"
                      onClick={() => setSelectedQueue(q.key)}
                      className={`rounded-2xl border-2 p-4 text-left transition-all ${
                        selectedQueue === q.key
                          ? 'border-auditart-blue bg-auditart-blue/4'
                          : 'border-gray-200 bg-white'
                      }`}
                    >
                      <span className="text-2xl">{q.icon}</span>
                      <p className="mt-2 text-sm font-bold">{QUEUE_LABELS[q.key]}</p>
                      <p className="text-xs text-auditart-muted">{q.desc}</p>
                    </button>
                  ))}
                </div>

                <label className="mb-2 block text-xs font-bold uppercase tracking-wider text-auditart-muted">
                  Operador · {queueHint}
                </label>
                <select
                  value={operadorId}
                  onChange={(e) => setOperadorId(e.target.value)}
                  className="input-modern mb-4 w-full px-3 py-2.5 text-sm"
                >
                  {operators.map((op) => (
                    <option key={op.id} value={op.id}>
                      {op.name}
                    </option>
                  ))}
                </select>

                <button
                  type="button"
                  onClick={() => void handleAssign()}
                  disabled={busy || !operadorId}
                  className="btn-primary flex items-center gap-2 disabled:opacity-50"
                >
                  {busy ? <Loader2 size={16} className="animate-spin" /> : <ArrowRight size={16} />}
                  Confirmar derivación
                </button>
              </div>
            </div>
          ) : (
            <div className="card-flat flex h-80 flex-col items-center justify-center text-auditart-muted">
              <Mail size={40} className="mb-3 opacity-20" />
              <p className="font-medium">Sin email seleccionado</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
