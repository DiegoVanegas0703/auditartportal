import { createContext } from 'react'
import type {
  AuditQueue,
  AuditRecord,
  AuditStatus,
  IncomingEmail,
  ServiceType,
  UrgencyLevel,
} from '../types'
import type { GmailSyncResult } from '../api/auditartApi'

export interface AssignDetails {
  triageNote: string
  paciente?: string
  dni?: string
  art?: string
  numeroSiniestro?: string
  telefonoPaciente?: string
  emailPaciente?: string
  tipoServicio?: ServiceType
  especialidad?: string
  urgency?: UrgencyLevel
  attachment?: File | null
}

export interface AuditContextValue {
  audits: AuditRecord[]
  emails: IncomingEmail[]
  loading: boolean
  error: string | null
  refresh: () => Promise<void>
  syncGmail: () => Promise<GmailSyncResult>
  seedDemoEmails: () => Promise<void>
  assignEmailToQueue: (
    emailId: string,
    queue: AuditQueue,
    operadorId: string,
    details?: AssignDetails,
  ) => Promise<void>
  assignRequestToQueue: (
    requestId: string,
    queue: AuditQueue,
    operadorId: string,
    details?: AssignDetails,
  ) => Promise<void>
  updateAuditStatus: (
    auditId: string,
    status: AuditStatus,
    extras?: {
      fechaTurnoUtc?: string
      profesional?: string
      prestadorId?: string
      autorizacionCodigo?: string
      autorizacionDocumentoAttachmentId?: string
    },
  ) => Promise<void>
  updateAudit: (auditId: string, updates: Partial<AuditRecord>) => Promise<void>
  getStats: () => {
    rojo: number
    amarillo: number
    azul: number
    verde: number
    slaAlertas: number
    emailsPendientes: number
  }
}

export const AuditContext = createContext<AuditContextValue | null>(null)
