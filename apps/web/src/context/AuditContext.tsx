import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  mapEmail,
  mapService,
  servicesApi,
  triageApi,
} from '../api/auditartApi'
import { useAuth } from './AuthContext'
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
  loading: boolean
  error: string | null
  refresh: () => Promise<void>
  seedDemoEmails: () => Promise<void>
  assignEmailToQueue: (
    emailId: string,
    queue: AuditQueue,
    operadorId: string,
  ) => Promise<void>
  updateAuditStatus: (
    auditId: string,
    status: AuditStatus,
    extras?: { fechaTurnoUtc?: string; profesional?: string },
  ) => Promise<void>
  updateAudit: (
    auditId: string,
    updates: Partial<AuditRecord>,
  ) => Promise<void>
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

export function AuditProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated, permissions } = useAuth()
  const [audits, setAudits] = useState<AuditRecord[]>([])
  const [emails, setEmails] = useState<IncomingEmail[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!isAuthenticated) {
      setAudits([])
      setEmails([])
      return
    }

    setLoading(true)
    setError(null)
    try {
      const services = await servicesApi.list()
      setAudits(services.map(mapService))

      if (permissions.triage) {
        const list = await triageApi.listEmails()
        setEmails(list.map(mapEmail))
      } else {
        setEmails([])
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al cargar datos')
    } finally {
      setLoading(false)
    }
  }, [isAuthenticated, permissions.triage])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const seedDemoEmails = useCallback(async () => {
    await triageApi.seedDemoEmails()
    await refresh()
  }, [refresh])

  const assignEmailToQueue = useCallback(
    async (emailId: string, queue: AuditQueue, operadorId: string) => {
      await triageApi.assign(emailId, queue, operadorId)
      await refresh()
    },
    [refresh],
  )

  const updateAuditStatus = useCallback(
    async (
      auditId: string,
      status: AuditStatus,
      extras?: { fechaTurnoUtc?: string; profesional?: string },
    ) => {
      await servicesApi.transition(auditId, status, extras)
      await refresh()
    },
    [refresh],
  )

  const updateAudit = useCallback(
    async (auditId: string, updates: Partial<AuditRecord>) => {
      await servicesApi.updateFlags(auditId, {
        presupuestoEnviado: updates.presupuestoEnviado,
        autorizacionArt: updates.autorizacionART,
        autofisica: updates.autofisica,
        valorPactado: updates.valorPactado,
        notas: updates.notas,
      })
      await refresh()
    },
    [refresh],
  )

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
      loading,
      error,
      refresh,
      seedDemoEmails,
      assignEmailToQueue,
      updateAuditStatus,
      updateAudit,
      getStats,
    }),
    [
      audits,
      emails,
      loading,
      error,
      refresh,
      seedDemoEmails,
      assignEmailToQueue,
      updateAuditStatus,
      updateAudit,
      getStats,
    ],
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
  _userId: string,
  _role: string,
  _queue?: AuditQueue,
): AuditRecord[] {
  // El filtrado por rol ya lo hace el backend.
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
