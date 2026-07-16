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

export type AuditQueue = 'general' | 'telemedicina' | 'cronicos'

export type UrgencyLevel = 'normal' | 'alta' | 'critica'

export type ServiceType =
  | 'consultorio'
  | 'terreno'
  | 'domicilio'
  | 'telemedicina'
  | 'comision_medica'
  | 'valoracion_dano'

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
}

export interface IncomingEmail {
  id: string
  from: string
  subject: string
  body: string
  receivedAt: string
  attachments: number
  suggestedQueue: AuditQueue
  serviceType: ServiceType
  art: string
  patientName?: string
  assigned: boolean
}

export interface AuditRecord {
  id: string
  numero: number
  paciente: string
  dni: string
  art: string
  tipoServicio: ServiceType
  especialidad: string
  profesional?: string
  operador: string
  operadorId: string
  queue: AuditQueue
  status: AuditStatus
  urgency: UrgencyLevel
  fechaIngreso: string
  fechaTurno?: string
  fechaConsulta?: string
  valorPactado?: number
  presupuestoEnviado: boolean
  autorizacionART: boolean
  autofisica: boolean
  slaDeadline?: string
  slaHoursRemaining?: number
  notas: string
  emailOrigen?: string
}

export const STATUS_LABELS: Record<AuditStatus, string> = {
  rojo: 'Búsqueda profesional',
  amarillo: 'Coordinado / Pend. ART',
  azul: 'Realizado / Doc. pendiente',
  verde: 'Listo para facturación',
}

export const STATUS_COLORS: Record<AuditStatus, { bg: string; border: string; text: string }> = {
  rojo: { bg: 'bg-status-rojo', border: 'border-status-rojo-border', text: 'text-red-800' },
  amarillo: { bg: 'bg-status-amarillo', border: 'border-status-amarillo-border', text: 'text-yellow-800' },
  azul: { bg: 'bg-status-azul', border: 'border-status-azul-border', text: 'text-blue-800' },
  verde: { bg: 'bg-status-verde', border: 'border-status-verde-border', text: 'text-green-800' },
}

export const ROLE_LABELS: Record<UserRole, string> = {
  admin: 'Administrador',
  jefatura: 'Jefatura',
  operador: 'Operador',
  telemedicina: 'Telemedicina',
  cronicos: 'Crónicos',
  facturacion: 'Facturación',
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
}
