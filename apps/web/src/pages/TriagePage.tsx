import { ArrowRight, CheckCircle2, Mail, Paperclip, Users, Zap } from 'lucide-react'
import { useState } from 'react'
import { PageHeader } from '../components/ui/PageHeader'
import { useAudits } from '../context/AuditContext'
import { QUEUE_LABELS, SERVICE_LABELS, type AuditQueue } from '../types'
import { formatDateTime } from '../utils/format'

const QUEUES: { key: AuditQueue; icon: string; desc: string }[] = [
  { key: 'general', icon: '👥', desc: 'Pablo, Laura y Martín' },
  { key: 'telemedicina', icon: '🩺', desc: 'Aylen Martínez' },
  { key: 'cronicos', icon: '📋', desc: 'Carolina Ruiz' },
]

export function TriagePage() {
  const { emails, assignEmailToQueue } = useAudits()
  const [selectedEmail, setSelectedEmail] = useState(emails[0]?.id ?? '')
  const [toast, setToast] = useState('')

  const pending = emails.filter((e) => !e.assigned)
  const assigned = emails.filter((e) => e.assigned)
  const current = emails.find((e) => e.id === selectedEmail)

  const handleAssign = (queue: AuditQueue) => {
    if (!selectedEmail) return
    assignEmailToQueue(selectedEmail, queue)
    setToast(`Derivado a ${QUEUE_LABELS[queue]} · Notificación WhatsApp enviada`)
    setTimeout(() => setToast(''), 4000)
    const next = pending.find((e) => e.id !== selectedEmail)
    if (next) setSelectedEmail(next.id)
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Bandeja de Triage"
        subtitle="Correos entrantes de info@auditart.com.ar · Derivación a colas operativas"
        action={
          <div className="flex items-center gap-2 rounded-full bg-auditart-blue/8 px-4 py-2 text-sm font-semibold text-auditart-blue ring-1 ring-auditart-blue/15">
            <Mail size={16} />
            {pending.length} pendientes
          </div>
        }
      />

      {toast && (
        <div className="toast-success mb-5 flex items-center gap-2 px-4 py-3 text-sm font-semibold">
          <CheckCircle2 size={16} />
          {toast}
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-5">
        {/* Email list */}
        <div className="space-y-3 lg:col-span-2">
          <div className="card-flat p-4">
            <h3 className="mb-3 text-xs font-bold uppercase tracking-wider text-auditart-gray">
              Pendientes ({pending.length})
            </h3>
            <div className="space-y-2">
              {pending.map((email) => {
                const isUrgent = email.subject.toLowerCase().includes('urgent')
                return (
                  <button
                    key={email.id}
                    onClick={() => setSelectedEmail(email.id)}
                    className={`w-full rounded-xl border-2 p-4 text-left transition-all duration-200 ${
                      selectedEmail === email.id
                        ? 'border-auditart-blue bg-auditart-blue/4 shadow-md shadow-auditart-blue/8'
                        : 'border-transparent bg-gray-50 hover:border-auditart-blue/20 hover:bg-white'
                    }`}
                  >
                    <div className="mb-2 flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-auditart-blue/10">
                          <Mail size={13} className="text-auditart-blue" />
                        </div>
                        <span className="text-[11px] text-auditart-muted">
                          {formatDateTime(email.receivedAt)}
                        </span>
                      </div>
                      {isUrgent && (
                        <span className="flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-[10px] font-bold text-red-600">
                          <Zap size={9} /> URGENTE
                        </span>
                      )}
                    </div>
                    <p className="text-sm font-semibold leading-snug text-auditart-navy">
                      {email.subject}
                    </p>
                    <p className="mt-1 truncate text-xs text-auditart-muted">{email.from}</p>
                  </button>
                )
              })}
            </div>
          </div>

          {assigned.length > 0 && (
            <div className="card-flat p-4 opacity-70">
              <h3 className="mb-3 text-xs font-bold uppercase tracking-wider text-auditart-muted">
                Derivados ({assigned.length})
              </h3>
              {assigned.map((email) => (
                <div key={email.id} className="mb-2 rounded-xl bg-green-50/50 p-3 ring-1 ring-green-100">
                  <p className="truncate text-sm text-auditart-navy">{email.subject}</p>
                  <p className="mt-0.5 flex items-center gap-1 text-xs font-medium text-auditart-green">
                    <CheckCircle2 size={11} />
                    {QUEUE_LABELS[email.suggestedQueue]}
                  </p>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Email detail */}
        <div className="lg:col-span-3">
          {current ? (
            <div className="card-flat overflow-hidden">
              <div className="border-b border-gray-100 bg-gradient-to-r from-auditart-blue/5 to-transparent p-6">
                <h3 className="text-xl font-bold text-auditart-navy">{current.subject}</h3>
                <div className="mt-3 flex flex-wrap items-center gap-3 text-sm text-auditart-gray">
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
                    ...(current.patientName
                      ? [{ label: 'Paciente', value: current.patientName }]
                      : []),
                    { label: 'Cola sugerida', value: QUEUE_LABELS[current.suggestedQueue] },
                  ].map((item) => (
                    <div
                      key={item.label}
                      className="rounded-xl bg-auditart-light p-3.5 ring-1 ring-auditart-blue/8"
                    >
                      <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
                        {item.label}
                      </p>
                      <p className="mt-0.5 text-sm font-semibold text-auditart-navy">
                        {item.value}
                      </p>
                    </div>
                  ))}
                </div>
              </div>

              {!current.assigned && (
                <div className="border-t border-gray-100 bg-gray-50/50 p-6">
                  <p className="mb-4 flex items-center gap-2 text-sm font-bold text-auditart-navy">
                    <Users size={16} className="text-auditart-blue" />
                    Derivar a cola operativa
                  </p>
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                    {QUEUES.map((q) => (
                      <button
                        key={q.key}
                        onClick={() => handleAssign(q.key)}
                        className={`group rounded-2xl border-2 p-5 text-left transition-all duration-200 hover:border-auditart-blue hover:shadow-md hover:shadow-auditart-blue/10 ${
                          current.suggestedQueue === q.key
                            ? 'border-auditart-blue bg-auditart-blue/4'
                            : 'border-gray-200 bg-white'
                        }`}
                      >
                        <span className="text-2xl">{q.icon}</span>
                        <p className="mt-2 text-sm font-bold text-auditart-navy">
                          {QUEUE_LABELS[q.key]}
                        </p>
                        <p className="text-xs text-auditart-muted">{q.desc}</p>
                        {current.suggestedQueue === q.key && (
                          <span className="mt-2 inline-block rounded-full bg-auditart-blue/10 px-2 py-0.5 text-[10px] font-bold text-auditart-blue">
                            SUGERIDA
                          </span>
                        )}
                        <ArrowRight
                          size={16}
                          className="mt-3 text-auditart-blue opacity-0 transition-opacity group-hover:opacity-100"
                        />
                      </button>
                    ))}
                  </div>
                  <p className="mt-4 text-xs text-auditart-muted">
                    Al derivar se crea una fila en estado{' '}
                    <span className="rounded-md bg-red-100 px-1.5 py-0.5 font-bold text-red-700">
                      Rojo
                    </span>{' '}
                    y se notifica al colaborador por WhatsApp.
                  </p>
                </div>
              )}
            </div>
          ) : (
            <div className="card-flat flex h-80 flex-col items-center justify-center text-auditart-muted">
              <Mail size={40} className="mb-3 opacity-20" />
              <p className="font-medium">Seleccioná un email</p>
              <p className="text-sm">para ver el detalle y derivar</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
