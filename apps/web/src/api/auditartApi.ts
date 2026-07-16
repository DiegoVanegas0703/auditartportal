import { apiFetch, clearTokens, setTokens } from './client'
import type { AuditQueue, AuditStatus, Permission, ServiceType, User, UserRole, UrgencyLevel } from '../types'

export interface AuthUserDto {
  id: string
  name: string
  email: string
  role: UserRole
  defaultQueue?: AuditQueue | null
  permissions: {
    triage: boolean
    operationalBoard: boolean
    billing: boolean
    allQueues: boolean
    manageUsers: boolean
  }
}

export interface AuthTokensDto {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAtUtc: string
  user: AuthUserDto
}

export interface ApiEmail {
  id: string
  from: string
  subject: string
  body: string
  receivedAtUtc: string
  attachmentCount: number
  suggestedQueue?: AuditQueue | null
  suggestedServiceType?: ServiceType | null
  suggestedArt?: string | null
  suggestedPatientName?: string | null
  isAssigned: boolean
}

export interface ApiService {
  id: string
  numero: number
  paciente: string
  dni?: string | null
  art: string
  tipoServicio: ServiceType
  especialidad?: string | null
  profesional?: string | null
  operadorId?: string | null
  operadorName?: string | null
  queue: AuditQueue
  status: AuditStatus
  urgency: UrgencyLevel
  fechaIngresoUtc: string
  fechaTurnoUtc?: string | null
  fechaConsultaUtc?: string | null
  slaDeadlineUtc?: string | null
  valorPactado?: number | null
  presupuestoEnviado: boolean
  autorizacionArt: boolean
  autofisica: boolean
  notas?: string | null
}

export interface ApiUserListItem {
  id: string
  name: string
  email: string
  role: UserRole
  defaultQueue?: AuditQueue | null
}

/** Usuarios de desarrollo para login con token `dev:email` */
export const DEV_LOGIN_USERS: { email: string; name: string; role: UserRole }[] = [
  { email: 'developmentcode@gmail.com', name: 'Dev Auditart', role: 'admin' },
  { email: 'diego.santamarina@auditart.com.ar', name: 'Diego Santamarina', role: 'admin' },
  { email: 'maria.gonzalez@auditart.com.ar', name: 'María González', role: 'jefatura' },
  { email: 'pablo.rodriguez@auditart.com.ar', name: 'Pablo Rodríguez', role: 'operador' },
  { email: 'laura.fernandez@auditart.com.ar', name: 'Laura Fernández', role: 'operador' },
  { email: 'martin.acosta@auditart.com.ar', name: 'Martín Acosta', role: 'operador' },
  { email: 'aylen.martinez@auditart.com.ar', name: 'Aylen Martínez', role: 'telemedicina' },
  { email: 'carolina.ruiz@auditart.com.ar', name: 'Carolina Ruiz', role: 'cronicos' },
  { email: 'damian.lopez@auditart.com.ar', name: 'Damián López', role: 'facturacion' },
]

export function mapAuthUser(dto: AuthUserDto): User & { permissions: Permission } {
  return {
    id: dto.id,
    name: dto.name,
    email: dto.email,
    role: dto.role.toLowerCase() as UserRole,
    queue: dto.defaultQueue
      ? (dto.defaultQueue.toLowerCase() as AuditQueue)
      : undefined,
    permissions: {
      triage: dto.permissions.triage,
      operationalBoard: dto.permissions.operationalBoard,
      billing: dto.permissions.billing,
      allQueues: dto.permissions.allQueues,
      manageUsers: dto.permissions.manageUsers,
    },
  }
}

function normalizeRole(role: string): UserRole {
  return role.toLowerCase() as UserRole
}

function normalizeQueue(q?: string | null): AuditQueue | undefined {
  return q ? (q.toLowerCase() as AuditQueue) : undefined
}

function normalizeStatus(s: string): AuditStatus {
  return s.toLowerCase() as AuditStatus
}

function normalizeServiceType(s: string): ServiceType {
  const map: Record<string, ServiceType> = {
    consultorio: 'consultorio',
    terreno: 'terreno',
    domicilio: 'domicilio',
    telemedicina: 'telemedicina',
    comisionmedica: 'comision_medica',
    comision_medica: 'comision_medica',
    valoraciondano: 'valoracion_dano',
    valoracion_dano: 'valoracion_dano',
  }
  return map[s.toLowerCase()] ?? 'consultorio'
}

function slaHoursRemaining(deadline?: string | null): number | undefined {
  if (!deadline) return undefined
  const ms = new Date(deadline).getTime() - Date.now()
  return Math.round(ms / 3600000)
}

export function mapService(s: ApiService) {
  return {
    id: s.id,
    numero: s.numero,
    paciente: s.paciente,
    dni: s.dni ?? '—',
    art: s.art,
    tipoServicio: normalizeServiceType(String(s.tipoServicio)),
    especialidad: s.especialidad ?? 'Por definir',
    profesional: s.profesional ?? undefined,
    operador: s.operadorName ?? '—',
    operadorId: s.operadorId ?? '',
    queue: normalizeQueue(String(s.queue)) ?? 'general',
    status: normalizeStatus(String(s.status)),
    urgency: String(s.urgency).toLowerCase() as UrgencyLevel,
    fechaIngreso: s.fechaIngresoUtc,
    fechaTurno: s.fechaTurnoUtc ?? undefined,
    fechaConsulta: s.fechaConsultaUtc ?? undefined,
    valorPactado: s.valorPactado ?? undefined,
    presupuestoEnviado: s.presupuestoEnviado,
    autorizacionART: s.autorizacionArt,
    autofisica: s.autofisica,
    slaDeadline: s.slaDeadlineUtc ?? undefined,
    slaHoursRemaining: slaHoursRemaining(s.slaDeadlineUtc),
    notas: s.notas ?? '',
  }
}

export function mapEmail(e: ApiEmail) {
  return {
    id: e.id,
    from: e.from,
    subject: e.subject,
    body: e.body,
    receivedAt: e.receivedAtUtc,
    attachments: e.attachmentCount,
    suggestedQueue: (normalizeQueue(e.suggestedQueue ? String(e.suggestedQueue) : null) ??
      'general') as AuditQueue,
    serviceType: e.suggestedServiceType
      ? normalizeServiceType(String(e.suggestedServiceType))
      : ('consultorio' as ServiceType),
    art: e.suggestedArt ?? 'Por definir',
    patientName: e.suggestedPatientName ?? undefined,
    assigned: e.isAssigned,
  }
}

export const authApi = {
  async loginDev(email: string): Promise<AuthTokensDto> {
    const data = await apiFetch<AuthTokensDto>('/api/auth/google', {
      method: 'POST',
      body: JSON.stringify({ idToken: `dev:${email}` }),
    })
    setTokens(data.accessToken, data.refreshToken)
    return data
  },

  async me(): Promise<AuthUserDto> {
    return apiFetch<AuthUserDto>('/api/auth/me')
  },

  async logout(refreshToken: string | null) {
    try {
      if (refreshToken) {
        await apiFetch('/api/auth/logout', {
          method: 'POST',
          body: JSON.stringify({ refreshToken }),
        })
      }
    } finally {
      clearTokens()
    }
  },
}

export const triageApi = {
  listEmails: () => apiFetch<ApiEmail[]>('/api/triage/emails'),
  seedDemoEmails: () =>
    apiFetch<{ message: string; created: number }>('/api/triage/seed-demo-emails', {
      method: 'POST',
    }),
  assign: (emailId: string, queue: AuditQueue, operadorId: string) =>
    apiFetch<ApiService>(`/api/triage/emails/${emailId}/assign`, {
      method: 'POST',
      body: JSON.stringify({
        queue: queueToApi(queue),
        operadorId,
      }),
    }),
}

export const servicesApi = {
  list: () => apiFetch<ApiService[]>('/api/services'),
  get: (id: string) => apiFetch<ApiService>(`/api/services/${id}`),
  transition: (
    id: string,
    nextStatus: AuditStatus,
    extras?: { fechaTurnoUtc?: string; profesional?: string; reason?: string },
  ) =>
    apiFetch(`/api/services/${id}/transition`, {
      method: 'POST',
      body: JSON.stringify({
        nextStatus: statusToApi(nextStatus),
        ...extras,
      }),
    }),
  updateFlags: (
    id: string,
    flags: {
      presupuestoEnviado?: boolean
      autorizacionArt?: boolean
      autofisica?: boolean
      valorPactado?: number
      notas?: string
    },
  ) =>
    apiFetch(`/api/services/${id}/flags`, {
      method: 'PATCH',
      body: JSON.stringify(flags),
    }),
}

export const usersApi = {
  list: () => apiFetch<ApiUserListItem[]>('/api/users'),
  operators: (queue?: AuditQueue) => {
    const q = queue ? `?queue=${queueToApi(queue)}` : ''
    return apiFetch<ApiUserListItem[]>(`/api/users/operators${q}`)
  },
}

function queueToApi(queue: AuditQueue): string {
  const map: Record<AuditQueue, string> = {
    general: 'General',
    telemedicina: 'Telemedicina',
    cronicos: 'Cronicos',
  }
  return map[queue]
}

function statusToApi(status: AuditStatus): string {
  const map: Record<AuditStatus, string> = {
    rojo: 'Rojo',
    amarillo: 'Amarillo',
    azul: 'Azul',
    verde: 'Verde',
  }
  return map[status]
}

export function mapListedUser(u: ApiUserListItem): User {
  return {
    id: u.id,
    name: u.name,
    email: u.email,
    role: normalizeRole(String(u.role)),
    queue: normalizeQueue(u.defaultQueue ? String(u.defaultQueue) : null),
  }
}
