import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { MOCK_AUDITS } from '../data/mockAudits'
import { MOCK_EMAILS } from '../data/mockEmails'
import { MOCK_USERS } from '../data/mockUsers'
import type {
  AuditQueue,
  AuditRecord,
  AuditStatus,
  IncomingEmail,
  UrgencyLevel,
} from '../types'

interface AuditContextValue {
  audits: AuditRecord[]
  emails: IncomingEmail[]
  assignEmailToQueue: (emailId: string, queue: AuditQueue) => void
  updateAuditStatus: (auditId: string, status: AuditStatus) => void
  updateAudit: (auditId: string, updates: Partial<AuditRecord>) => void
  getStats: () => {
    rojo: number
    amarillo: number
    azul: number
    verde: number
    slaAlertas: number
    emailsPendientes: number
  }
}

const AuditContext = createContext<AuditContextValue | null>(null)

function getNextNumero(audits: AuditRecord[]): number {
  return Math.max(...audits.map((a) => a.numero), 1000) + 1
}

function getOperadorForQueue(queue: AuditQueue) {
  switch (queue) {
    case 'telemedicina':
      return MOCK_USERS.find((u) => u.id === 'u6')!
    case 'cronicos':
      return MOCK_USERS.find((u) => u.id === 'u7')!
    default:
      return MOCK_USERS.find((u) => u.id === 'u3')!
  }
}

export function AuditProvider({ children }: { children: ReactNode }) {
  const [audits, setAudits] = useState<AuditRecord[]>(MOCK_AUDITS)
  const [emails, setEmails] = useState<IncomingEmail[]>(MOCK_EMAILS)

  const assignEmailToQueue = useCallback((emailId: string, queue: AuditQueue) => {
    setEmails((prev) =>
      prev.map((e) => (e.id === emailId ? { ...e, assigned: true, suggestedQueue: queue } : e)),
    )

    setAudits((prev) => {
      const email = MOCK_EMAILS.find((e) => e.id === emailId)
      if (!email) return prev

      const operador = getOperadorForQueue(queue)
      const newAudit: AuditRecord = {
        id: `a-new-${Date.now()}`,
        numero: getNextNumero(prev),
        paciente: email.patientName ?? 'Sin identificar',
        dni: '—',
        art: email.art,
        tipoServicio: email.serviceType,
        especialidad: 'Por definir',
        operador: operador.name,
        operadorId: operador.id,
        queue,
        status: 'rojo',
        urgency: email.subject.toLowerCase().includes('urgent') ? 'critica' : 'alta',
        fechaIngreso: new Date().toISOString(),
        presupuestoEnviado: false,
        autorizacionART: false,
        autofisica: false,
        notas: `Derivado desde email: ${email.subject}`,
        emailOrigen: email.from,
      }
      return [newAudit, ...prev]
    })
  }, [])

  const updateAuditStatus = useCallback((auditId: string, status: AuditStatus) => {
    setAudits((prev) => prev.map((a) => (a.id === auditId ? { ...a, status } : a)))
  }, [])

  const updateAudit = useCallback((auditId: string, updates: Partial<AuditRecord>) => {
    setAudits((prev) => prev.map((a) => (a.id === auditId ? { ...a, ...updates } : a)))
  }, [])

  const getStats = useCallback(() => {
    const rojo = audits.filter((a) => a.status === 'rojo').length
    const amarillo = audits.filter((a) => a.status === 'amarillo').length
    const azul = audits.filter((a) => a.status === 'azul').length
    const verde = audits.filter((a) => a.status === 'verde').length
    const slaAlertas = audits.filter(
      (a) => a.status === 'azul' && (a.slaHoursRemaining ?? 99) <= 12,
    ).length
    const emailsPendientes = emails.filter((e) => !e.assigned).length
    return { rojo, amarillo, azul, verde, slaAlertas, emailsPendientes }
  }, [audits, emails])

  const value = useMemo(
    () => ({
      audits,
      emails,
      assignEmailToQueue,
      updateAuditStatus,
      updateAudit,
      getStats,
    }),
    [audits, emails, assignEmailToQueue, updateAuditStatus, updateAudit, getStats],
  )

  return <AuditContext.Provider value={value}>{children}</AuditContext.Provider>
}

export function useAudits() {
  const ctx = useContext(AuditContext)
  if (!ctx) throw new Error('useAudits debe usarse dentro de AuditProvider')
  return ctx
}

export function filterAuditsForUser(
  audits: AuditRecord[],
  userId: string,
  role: string,
  _queue?: AuditQueue,
): AuditRecord[] {
  if (role === 'admin' || role === 'jefatura') return audits
  if (role === 'facturacion') return audits.filter((a) => a.status === 'verde')
  if (role === 'telemedicina') return audits.filter((a) => a.queue === 'telemedicina')
  if (role === 'cronicos') return audits.filter((a) => a.queue === 'cronicos')
  if (role === 'operador') {
    return audits.filter((a) => a.queue === 'general' && a.operadorId === userId)
  }
  return audits
}

export function getUrgencyColor(urgency: UrgencyLevel): string {
  switch (urgency) {
    case 'critica':
      return 'text-red-600 bg-red-100'
    case 'alta':
      return 'text-orange-600 bg-orange-100'
    default:
      return 'text-gray-600 bg-gray-100'
  }
}
