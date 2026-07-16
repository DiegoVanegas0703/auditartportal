import {
  ArrowLeft,
  CheckCircle2,
  ChevronRight,
  MessageCircle,
  Upload,
} from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAudits } from '../context/AuditContext'
import {
  QUEUE_LABELS,
  SERVICE_LABELS,
  STATUS_LABELS,
  type AuditStatus,
} from '../types'
import { formatDateTime, formatCurrency } from '../utils/format'

const TRANSITIONS: Record<
  AuditStatus,
  { next: AuditStatus; label: string; condition?: string }[]
> = {
  rojo: [{ next: 'amarillo', label: 'Profesional asignado y turno coordinado' }],
  amarillo: [{ next: 'azul', label: 'Consulta realizada — iniciar control SLA 24/48hs' }],
  azul: [
    {
      next: 'verde',
      label: 'Documentación completa — listo para facturar',
      condition: 'Requiere autofísica o valor pactado',
    },
  ],
  verde: [],
}

export function AuditDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { audits, updateAudit, updateAuditStatus } = useAudits()
  const [toast, setToast] = useState('')

  const audit = audits.find((a) => a.id === id)

  if (!audit) {
    return (
      <div className="flex flex-col items-center py-20 text-auditart-gray">
        <p className="font-medium">Servicio no encontrado</p>
        <Link
          to="/tablero"
          className="mt-4 flex items-center gap-1 text-sm font-semibold text-auditart-blue hover:underline"
        >
          <ArrowLeft size={14} /> Volver al tablero
        </Link>
      </div>
    )
  }

  const transitions = TRANSITIONS[audit.status]

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(''), 3000)
  }

  const handleTransition = (next: AuditStatus) => {
    const updates: Partial<typeof audit> = { status: next }
    if (next === 'amarillo' && !audit.fechaTurno) {
      updates.fechaTurno = new Date(Date.now() + 86400000).toISOString()
      updates.profesional = audit.profesional ?? 'Dr. Asignado'
      updates.autorizacionART = true
    }
    if (next === 'azul') {
      updates.fechaConsulta = new Date().toISOString()
      updates.slaDeadline = new Date(Date.now() + 48 * 3600000).toISOString()
      updates.slaHoursRemaining = 48
    }
    if (next === 'verde') {
      updates.autofisica = true
      updates.slaHoursRemaining = undefined
    }
    updateAudit(audit.id, updates)
    updateAuditStatus(audit.id, next)
    showToast(`Estado actualizado a ${STATUS_LABELS[next]} · WhatsApp simulado`)
  }

  const toggleFlag = (field: 'presupuestoEnviado' | 'autorizacionART' | 'autofisica') => {
    updateAudit(audit.id, { [field]: !audit[field] })
    showToast('Documentación actualizada')
  }

  return (
    <div className="mx-auto max-w-4xl animate-fade-in">
      <Link
        to="/tablero"
        className="mb-6 inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-1.5 text-sm font-semibold text-auditart-blue shadow-sm ring-1 ring-gray-200 transition-all hover:shadow-md"
      >
        <ArrowLeft size={14} />
        Volver al tablero
      </Link>

      {toast && (
        <div className="toast-success mb-5 flex items-center gap-2 px-4 py-3 text-sm font-semibold">
          <CheckCircle2 size={16} />
          {toast}
        </div>
      )}

      <div className="card-flat overflow-hidden">
        {/* Header */}
        <div className="border-b border-gray-100 bg-gradient-to-r from-auditart-blue/6 to-transparent p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <div className="mb-2 flex items-center gap-2">
                <span className="rounded-lg bg-auditart-navy/5 px-2.5 py-1 font-mono text-xs font-bold text-auditart-navy">
                  #{audit.numero}
                </span>
                <StatusBadge status={audit.status} />
              </div>
              <h2 className="text-2xl font-bold text-auditart-navy">{audit.paciente}</h2>
              <p className="mt-1 text-sm text-auditart-gray">
                DNI {audit.dni} · {audit.art}
              </p>
            </div>
            {audit.valorPactado && (
              <div className="rounded-2xl bg-auditart-blue/8 px-5 py-3 text-right ring-1 ring-auditart-blue/15">
                <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
                  Valor pactado
                </p>
                <p className="text-2xl font-extrabold text-auditart-blue">
                  {formatCurrency(audit.valorPactado)}
                </p>
              </div>
            )}
          </div>
        </div>

        {/* Data grid */}
        <div className="grid grid-cols-1 gap-0 md:grid-cols-2">
          <div className="space-y-0 border-b border-gray-100 p-6 md:border-b-0 md:border-r">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Datos del servicio
            </h3>
            <InfoRow label="Tipo" value={SERVICE_LABELS[audit.tipoServicio]} />
            <InfoRow label="Especialidad" value={audit.especialidad} />
            <InfoRow label="Cola" value={QUEUE_LABELS[audit.queue]} />
            <InfoRow label="Operador" value={audit.operador} />
            <InfoRow label="Profesional" value={audit.profesional ?? 'Sin asignar'} />
            <InfoRow label="Urgencia" value={audit.urgency} />
          </div>
          <div className="p-6">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Fechas y SLA
            </h3>
            <InfoRow label="Ingreso" value={formatDateTime(audit.fechaIngreso)} />
            <InfoRow label="Turno" value={formatDateTime(audit.fechaTurno)} />
            <InfoRow label="Consulta" value={formatDateTime(audit.fechaConsulta)} />
            <InfoRow
              label="SLA restante"
              value={
                audit.slaHoursRemaining != null
                  ? `${audit.slaHoursRemaining} horas`
                  : '—'
              }
              highlight={
                audit.slaHoursRemaining != null && audit.slaHoursRemaining <= 12
              }
            />
            <InfoRow label="Email origen" value={audit.emailOrigen ?? '—'} />
          </div>
        </div>

        {/* Checklist */}
        <div className="border-t border-gray-100 bg-gray-50/50 p-6">
          <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
            Checklist documentación
          </h3>
          <div className="flex flex-wrap gap-3">
            <CheckButton
              active={audit.presupuestoEnviado}
              label="Presupuesto enviado a ART"
              onClick={() => toggleFlag('presupuestoEnviado')}
            />
            <CheckButton
              active={audit.autorizacionART}
              label="Autorización ART"
              onClick={() => toggleFlag('autorizacionART')}
            />
            <CheckButton
              active={audit.autofisica}
              label="Autofísica cargada"
              onClick={() => toggleFlag('autofisica')}
            />
          </div>
        </div>

        {audit.notas && (
          <div className="border-t border-gray-100 p-6">
            <h3 className="mb-2 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Notas
            </h3>
            <p className="rounded-xl bg-auditart-light p-4 text-sm text-auditart-navy/80">
              {audit.notas}
            </p>
          </div>
        )}

        {/* Transitions */}
        {transitions.length > 0 && (
          <div className="border-t border-gray-100 p-6">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Avanzar estado
            </h3>
            <div className="space-y-3">
              {transitions.map((t) => (
                <button
                  key={t.next}
                  onClick={() => handleTransition(t.next)}
                  className="group flex w-full items-center justify-between rounded-2xl border-2 border-gray-100 bg-white p-5 text-left transition-all hover:border-auditart-blue hover:shadow-md hover:shadow-auditart-blue/8"
                >
                  <div>
                    <p className="font-bold text-auditart-navy group-hover:text-auditart-blue">
                      {t.label}
                    </p>
                    {t.condition && (
                      <p className="mt-0.5 text-xs text-auditart-muted">{t.condition}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={audit.status} compact />
                    <ChevronRight size={16} className="text-auditart-muted" />
                    <StatusBadge status={t.next} compact />
                  </div>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Actions */}
        <div className="flex flex-wrap gap-3 border-t border-gray-100 bg-gray-50/50 p-6">
          <button
            onClick={() => showToast('WhatsApp enviado al paciente (simulado)')}
            className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-green-500 to-emerald-600 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-green-500/25 transition-all hover:shadow-lg"
          >
            <MessageCircle size={16} />
            Avisar paciente (WS)
          </button>
          <button
            onClick={() => showToast('Archivo adjunto simulado')}
            className="flex items-center gap-2 rounded-xl border-2 border-gray-200 bg-white px-5 py-2.5 text-sm font-semibold text-auditart-navy transition-all hover:border-auditart-blue hover:shadow-sm"
          >
            <Upload size={16} />
            Adjuntar documento
          </button>
        </div>
      </div>
    </div>
  )
}

function InfoRow({
  label,
  value,
  highlight,
}: {
  label: string
  value: string
  highlight?: boolean
}) {
  return (
    <div className="flex justify-between border-b border-gray-50 py-3 text-sm last:border-0">
      <span className="text-auditart-muted">{label}</span>
      <span
        className={`font-semibold ${highlight ? 'text-red-600' : 'text-auditart-navy'}`}
      >
        {value}
      </span>
    </div>
  )
}

function CheckButton({
  active,
  label,
  onClick,
}: {
  active: boolean
  label: string
  onClick: () => void
}) {
  return (
    <button
      onClick={onClick}
      className={`flex items-center gap-2.5 rounded-xl border-2 px-4 py-2.5 text-sm font-semibold transition-all ${
        active
          ? 'border-green-200 bg-green-50 text-green-700 shadow-sm'
          : 'border-gray-200 bg-white text-auditart-gray hover:border-auditart-blue/30'
      }`}
    >
      <CheckCircle2 size={16} className={active ? 'text-green-500' : 'text-gray-300'} />
      {label}
    </button>
  )
}
