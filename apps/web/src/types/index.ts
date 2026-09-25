export type UserRole =
  | 'admin'
  | 'jefatura'
  | 'operador'
  | 'telemedicina'
  | 'cronicos'
  | 'facturacion'

export type AuditStatus =
  | 'rojo'
  | 'amarillo'
  | 'azul'
  | 'verde'
  | 'celeste'

export type AuditQueue = 'general' | 'telemedicina' | 'cronicos'

export type ChronicPeriodicity = 'weekly' | 'monthly' | 'every_x_days'

export type UrgencyLevel = 'normal' | 'alta' | 'critica'

export type ServiceType =
  | 'consultorio'
  | 'terreno'
  | 'domicilio'
  | 'telemedicina'
  | 'comision_medica'
  | 'valoracion_dano'
  | 'otra_auditoria'

export interface User {
  id: string
  name: string
  email: string
  role: UserRole
  avatar?: string
  queue?: AuditQueue
}

export interface Permission {
  triage: boolean
  operationalBoard: boolean
  billing: boolean
  allQueues: boolean
  manageUsers: boolean
  reports: boolean
  precios: boolean
}

export type TipoProfesional = 'auditor' | 'especialista'

export interface IncomingEmail {
  id: string
  from: string
  subject: string
  body: string
  receivedAt: string
  attachments: number
  attachmentFiles: EmailAttachment[]
  suggestedQueue: AuditQueue
  serviceType: ServiceType
  art: string
  patientName?: string
  assigned: boolean
  ignored: boolean
  tags: string[]
}

export type EmailRequestState = 'pending' | 'ignored' | 'assigned' | 'closed'
export type EmailDirection = 'inbound' | 'outbound'
export type EmailMessageStatus =
  | 'received'
  | 'pending_send'
  | 'sending'
  | 'sent'
  | 'failed'

export interface EmailAttachment {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  messageId?: string
}

export type EmailChannel = 'general' | 'cronicos'

export interface EmailRequestSummary {
  id: string
  subject: string
  state: EmailRequestState
  channel: EmailChannel
  lastMessageAt: string
  messageCount: number
  attachmentCount: number
  tags: string[]
  participants: string[]
  auditServiceId?: string
  preview?: string
}

export interface ConversationMessage {
  id: string
  threadId: string
  providerMessageId?: string
  direction: EmailDirection
  status: EmailMessageStatus
  from: string
  to: string[]
  cc: string[]
  subject: string
  bodyText: string
  bodyHtml?: string
  occurredAt: string
  lastError?: string
  attachments: EmailAttachment[]
}

export interface ReplyDefaults {
  to: string[]
  cc: string[]
  subject: string
}

export interface EmailRequestDetail extends EmailRequestSummary {
  messages: ConversationMessage[]
  attachments: EmailAttachment[]
  replyDefaults: ReplyDefaults
}

export interface AuditRecord {
  id: string
  numero: number
  paciente: string
  dni: string
  art: string
  numeroSiniestro?: string
  telefonoPaciente?: string
  emailPaciente?: string
  tipoServicio: ServiceType
  especialidad: string
  profesional?: string
  prestadorId?: string
  pacienteId?: string
  operador: string
  operadorId: string
  queue: AuditQueue
  status: AuditStatus
  urgency: UrgencyLevel
  fechaIngreso: string
  fechaTurno?: string
  fechaConsulta?: string
  valorPactado?: number
  valorConciliadoArt?: number
  tipoProfesional?: TipoProfesional
  porcentajeConciliacionEspecialista?: 50 | 100
  precioCatalogoId?: string
  requierePagoAnticipado?: boolean
  presupuestoEnviado: boolean
  autorizacionART: boolean
  autorizacionCodigo?: string
  autorizacionDocumentoAttachmentId?: string
  autofisica: boolean
  slaDeadline?: string
  slaHoursRemaining?: number
  notas: string
  emailOrigen?: string
  isChronicPeriodic?: boolean
  chronicPeriodicity?: ChronicPeriodicity
  chronicIntervalDays?: number
  nextRenewalDue?: string
  lastRenewedAt?: string
  chronicRenewalCount?: number
  needsOperadorAssignment?: boolean
  slaWarnBeforeHours?: number
}

export const STATUS_LABELS: Record<AuditStatus, string> = {
  rojo: 'Búsqueda profesional',
  amarillo: 'Coordinado / Pend. ART',
  azul: 'Realizado / Doc. pendiente',
  verde: 'Listo para facturación',
  celeste: 'Pagado / Cerrada',
}

export const CHRONIC_PERIODICITY_LABELS: Record<ChronicPeriodicity, string> = {
  weekly: 'Semanal',
  monthly: 'Mensual',
  every_x_days: 'Cada X días',
}

export const STATUS_COLORS: Record<AuditStatus, { bg: string; border: string; text: string }> = {
  rojo: { bg: 'bg-status-rojo', border: 'border-status-rojo-border', text: 'text-red-800' },
  amarillo: { bg: 'bg-status-amarillo', border: 'border-status-amarillo-border', text: 'text-yellow-800' },
  azul: { bg: 'bg-status-azul', border: 'border-status-azul-border', text: 'text-blue-800' },
  verde: { bg: 'bg-status-verde', border: 'border-status-verde-border', text: 'text-green-800' },
  celeste: {
    bg: 'bg-sky-100',
    border: 'border-sky-300',
    text: 'text-sky-900',
  },
}

export const ROLE_LABELS: Record<UserRole, string> = {
  admin: 'Administrador',
  jefatura: 'Jefatura',
  operador: 'Operador',
  telemedicina: 'Telemedicina',
  cronicos: 'Crónicos',
  facturacion: 'Facturación',
}

export const CHANNEL_LABELS: Record<EmailChannel, string> = {
  general: 'General',
  cronicos: 'Crónicos',
}

export const QUEUE_LABELS: Record<AuditQueue, string> = {
  general: 'Operadores Generales',
  telemedicina: 'Telemedicina (Aylen)',
  cronicos: 'Crónicos',
}

export const SERVICE_LABELS: Record<ServiceType, string> = {
  consultorio: 'Auditoría de Consultorio',
  terreno: 'Auditoría de Terreno',
  domicilio: 'Auditoría en Domicilio',
  telemedicina: 'Telemedicina',
  comision_medica: 'Comisión Médica',
  valoracion_dano: 'Valoración del Daño',
  otra_auditoria: 'Otra auditoría',
}
