import {

  useCallback,

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

import type {

  AuditQueue,

  AuditRecord,

  AuditStatus,

  IncomingEmail,

} from '../types'

import { AuditContext, type AssignDetails, type AuditContextValue } from './audit-context'

import { useAuth } from './useAuth'



export function AuditProvider({ children }: { children: ReactNode }) {

  const { isAuthenticated, permissions, loading: authLoading } = useAuth()

  const [audits, setAudits] = useState<AuditRecord[]>([])

  const [emails, setEmails] = useState<IncomingEmail[]>([])

  const [pendingEmailCount, setPendingEmailCount] = useState(0)

  const [loading, setLoading] = useState(false)

  const [error, setError] = useState<string | null>(null)



  const refresh = useCallback(async () => {

    if (!isAuthenticated) {

      setAudits((prev) => (prev.length === 0 ? prev : []))

      setEmails((prev) => (prev.length === 0 ? prev : []))

      setPendingEmailCount((prev) => (prev === 0 ? prev : 0))

      return

    }



    setLoading(true)

    setError(null)

    try {

      const services = await servicesApi.list()

      setAudits(services.map(mapService))



      if (permissions.triage) {

        try {

          const result = await triageApi.listRequests(1, 15, false)

          setPendingEmailCount(result.pendingCount)

          setEmails([])

        } catch {

          const legacy = await triageApi.listEmails(1, 15, false)

          setEmails(legacy.items.map(mapEmail))

          setPendingEmailCount(legacy.pendingCount)

        }

      } else {

        setEmails([])

        setPendingEmailCount(0)

      }

    } catch (e) {

      setError(e instanceof Error ? e.message : 'Error al cargar datos')

    } finally {

      setLoading(false)

    }

  }, [isAuthenticated, permissions.triage])



  useEffect(() => {

    if (authLoading) return

    void refresh()

  }, [refresh, authLoading])



  const seedDemoEmails = useCallback(async () => {

    await triageApi.seedDemoEmails()

    await refresh()

  }, [refresh])



  const syncGmail = useCallback(async () => {

    const result = await triageApi.syncGmail()

    await refresh()

    return result

  }, [refresh])



  const assignEmailToQueue = useCallback(

    async (

      emailId: string,

      queue: AuditQueue,

      operadorId: string,

      details?: AssignDetails,

    ) => {

      await triageApi.assign(emailId, queue, operadorId, details)

      await refresh()

    },

    [refresh],

  )



  const assignRequestToQueue = useCallback(

    async (

      requestId: string,

      queue: AuditQueue,

      operadorId: string,

      details?: AssignDetails,

    ) => {

      await triageApi.assignRequest(requestId, queue, operadorId, {

        triageNote: details?.triageNote ?? '',

        paciente: details?.paciente,

        dni: details?.dni,

        art: details?.art,

        numeroSiniestro: details?.numeroSiniestro,

        telefonoPaciente: details?.telefonoPaciente,

        emailPaciente: details?.emailPaciente,

        tipoServicio: details?.tipoServicio,

        especialidad: details?.especialidad,

        urgency: details?.urgency,

        attachment: details?.attachment,

      })

      await refresh()

    },

    [refresh],

  )



  const updateAuditStatus = useCallback(

    async (

      auditId: string,

      status: AuditStatus,

      extras?: {
        fechaTurnoUtc?: string
        profesional?: string
        prestadorId?: string
        autorizacionCodigo?: string
        autorizacionDocumentoAttachmentId?: string
      },
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

    const emailsPendientes = pendingEmailCount

    return { rojo, amarillo, azul, verde, slaAlertas, emailsPendientes }

  }, [audits, pendingEmailCount])



  const value = useMemo<AuditContextValue>(

    () => ({

      audits,

      emails,

      loading,

      error,

      refresh,

      syncGmail,

      seedDemoEmails,

      assignEmailToQueue,

      assignRequestToQueue,

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

      syncGmail,

      seedDemoEmails,

      assignEmailToQueue,

      assignRequestToQueue,

      updateAuditStatus,

      updateAudit,

      getStats,

    ],

  )



  return <AuditContext.Provider value={value}>{children}</AuditContext.Provider>

}


