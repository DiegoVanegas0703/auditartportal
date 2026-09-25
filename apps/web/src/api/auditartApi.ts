import { apiDownload, apiFetch, clearTokens, setTokens } from './client'
import type {
  AuditQueue,
  AuditStatus,
  ConversationMessage,
  EmailAttachment,
  EmailDirection,
  EmailMessageStatus,
  EmailRequestDetail,
  EmailRequestState,
  EmailRequestSummary,
  Permission,
  ReplyDefaults,
  ServiceType,
  ChronicPeriodicity,
  User,
  UserRole,
  UrgencyLevel,
} from '../types'

export interface AuthUserDto {
  id: string
  name: string
  email: string
  role: UserRole
  defaultQueue?: AuditQueue | null
  mustChangePassword: boolean
  permissions: {
    triage: boolean
    operationalBoard: boolean
    billing: boolean
    allQueues: boolean
    manageUsers: boolean
    reports: boolean
    precios: boolean
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
  isIgnored: boolean
  tags: string[]
  attachments: ApiEmailAttachment[]
}

export interface PagedEmailsDto {
  items: ApiEmail[]
  page: number
  pageSize: number
  total: number
  totalPages: number
  pendingCount: number
  ignoredCount: number
  availableTags: string[]
}

export interface ApiEmailAttachment {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
}

export interface ApiService {
  id: string
  numero: number
  pacienteId?: string | null
  paciente: string
  dni?: string | null
  art: string
  numeroSiniestro?: string | null
  telefonoPaciente?: string | null
  emailPaciente?: string | null
  tipoServicio: ServiceType
  especialidad?: string | null
  profesional?: string | null
  prestadorId?: string | null
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
  valorConciliadoArt?: number | null
  tipoProfesional?: string | null
  porcentajeConciliacionEspecialista?: number | null
  precioCatalogoId?: string | null
  requierePagoAnticipado?: boolean
  presupuestoEnviado: boolean
  autorizacionArt: boolean
  autorizacionCodigo?: string | null
  autorizacionDocumentoAttachmentId?: string | null
  autofisica: boolean
  notas?: string | null
  isChronicPeriodic?: boolean
  chronicPeriodicity?: string | null
  chronicIntervalDays?: number | null
  chronicScheduleStartUtc?: string | null
  nextRenewalDueUtc?: string | null
  lastRenewedAtUtc?: string | null
  chronicRenewalCount?: number
  needsOperadorAssignment?: boolean
  slaWarnBeforeHours?: number | null
}

export interface ApiUserListItem {
  id: string
  name: string
  email: string
  role: UserRole
  defaultQueue?: AuditQueue | null
  isActive?: boolean
  mustChangePassword?: boolean
}

export interface DenunciaParsedDto {
  nombreTrabajador?: string | null
  dniCuil?: string | null
  empleador?: string | null
  numeroSiniestro?: string | null
  fechaAccidente?: string | null
  descripcionCap?: string | null
  telefonoPaciente?: string | null
  emailPaciente?: string | null
}

export interface GmailSyncResult {
  fetched: number
  inserted: number
  skipped: number
  failed: number
  errors: string[]
}

/** Usuarios de prueba (solo referencia local) */
export const SEED_USERS_HINT = 'Contraseña inicial: Test'

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
      triage: !!dto.permissions.triage,
      operationalBoard: !!dto.permissions.operationalBoard,
      billing: !!dto.permissions.billing,
      allQueues: !!dto.permissions.allQueues,
      manageUsers: !!dto.permissions.manageUsers,
      reports: !!dto.permissions.reports,
      precios: !!dto.permissions.precios,
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
  const key = s.toLowerCase().replace(/_/g, '')
  if (key === 'cuentapagoanticipado' || key === 'celeste') return 'celeste'
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
    otraauditoria: 'otra_auditoria',
    otra_auditoria: 'otra_auditoria',
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
    pacienteId: s.pacienteId ?? undefined,
    paciente: s.paciente,
    dni: s.dni ?? '—',
    art: s.art,
    numeroSiniestro: s.numeroSiniestro ?? undefined,
    telefonoPaciente: s.telefonoPaciente ?? undefined,
    emailPaciente: s.emailPaciente ?? undefined,
    tipoServicio: normalizeServiceType(String(s.tipoServicio)),
    especialidad: s.especialidad ?? 'Por definir',
    profesional: s.profesional ?? undefined,
    prestadorId: s.prestadorId ?? undefined,
    operador: s.operadorName ?? '—',
    operadorId: s.operadorId ?? '',
    queue: normalizeQueue(String(s.queue)) ?? 'general',
    status: normalizeStatus(String(s.status)),
    urgency: String(s.urgency).toLowerCase() as UrgencyLevel,
    fechaIngreso: s.fechaIngresoUtc,
    fechaTurno: s.fechaTurnoUtc ?? undefined,
    fechaConsulta: s.fechaConsultaUtc ?? undefined,
    valorPactado: s.valorPactado ?? undefined,
    valorConciliadoArt: s.valorConciliadoArt ?? undefined,
    tipoProfesional: s.tipoProfesional
      ? (String(s.tipoProfesional).toLowerCase() as 'auditor' | 'especialista')
      : undefined,
    porcentajeConciliacionEspecialista: ((): 50 | 100 | undefined => {
      if (s.porcentajeConciliacionEspecialista === 50) return 50
      if (s.porcentajeConciliacionEspecialista === 100) return 100
      return undefined
    })(),
    precioCatalogoId: s.precioCatalogoId ?? undefined,
    requierePagoAnticipado: s.requierePagoAnticipado ?? false,
    presupuestoEnviado: s.presupuestoEnviado,
    autorizacionART: s.autorizacionArt,
    autorizacionCodigo: s.autorizacionCodigo ?? undefined,
    autorizacionDocumentoAttachmentId: s.autorizacionDocumentoAttachmentId ?? undefined,
    autofisica: s.autofisica,
    slaDeadline: s.slaDeadlineUtc ?? undefined,
    slaHoursRemaining: slaHoursRemaining(s.slaDeadlineUtc),
    notas: s.notas ?? '',
    isChronicPeriodic: s.isChronicPeriodic ?? false,
    chronicPeriodicity: normalizeChronicPeriodicity(s.chronicPeriodicity),
    chronicIntervalDays: s.chronicIntervalDays ?? undefined,
    nextRenewalDue: s.nextRenewalDueUtc ?? undefined,
    lastRenewedAt: s.lastRenewedAtUtc ?? undefined,
    chronicRenewalCount: s.chronicRenewalCount ?? 0,
    needsOperadorAssignment: s.needsOperadorAssignment ?? false,
    slaWarnBeforeHours: s.slaWarnBeforeHours ?? 12,
  }
}

function normalizeChronicPeriodicity(value?: string | null): ChronicPeriodicity | undefined {
  if (!value) return undefined
  const normalized = value.toLowerCase().replace(/_/g, '')
  if (normalized === 'weekly') return 'weekly'
  if (normalized === 'monthly') return 'monthly'
  if (normalized === 'everyxdays') return 'every_x_days'
  return undefined
}

function chronicPeriodicityToApi(value: ChronicPeriodicity): string {
  const map: Record<ChronicPeriodicity, string> = {
    weekly: 'Weekly',
    monthly: 'Monthly',
    every_x_days: 'EveryXDays',
  }
  return map[value]
}

export function mapEmail(e: ApiEmail) {
  return {
    id: e.id,
    from: e.from,
    subject: e.subject,
    body: e.body,
    receivedAt: e.receivedAtUtc,
    attachments: e.attachmentCount,
    attachmentFiles: e.attachments ?? [],
    suggestedQueue: (normalizeQueue(e.suggestedQueue ? String(e.suggestedQueue) : null) ??
      'general') as AuditQueue,
    serviceType: e.suggestedServiceType
      ? normalizeServiceType(String(e.suggestedServiceType))
      : ('consultorio' as ServiceType),
    art: e.suggestedArt ?? 'Por definir',
    patientName: e.suggestedPatientName ?? undefined,
    assigned: e.isAssigned,
    ignored: e.isIgnored,
    tags: e.tags ?? [],
  }
}

export interface ApiEmailAttachmentDto {
  id: string
  messageId: string
  fileName: string
  contentType: string
  sizeBytes: number
}

export interface ApiEmailMessageDto {
  id: string
  threadId: string
  providerMessageId?: string | null
  direction: string
  status: string
  from: string
  to: string[]
  cc: string[]
  subject: string
  bodyText: string
  bodyHtml?: string | null
  occurredAtUtc: string
  lastError?: string | null
  attachments: ApiEmailAttachmentDto[]
}

export interface ApiReplyDefaultsDto {
  to: string[]
  cc: string[]
  subject: string
}

export interface ApiEmailRequestSummary {
  id: string
  subject: string
  state: string
  channel: string
  lastMessageAtUtc: string
  messageCount: number
  attachmentCount: number
  tags: string[]
  participants: string[]
  auditServiceId?: string | null
  preview?: string | null
}

export interface ApiEmailRequestDetail extends ApiEmailRequestSummary {
  messages: ApiEmailMessageDto[]
  attachments: ApiEmailAttachmentDto[]
  replyDefaults: ApiReplyDefaultsDto
}

export interface PagedRequestsDto {
  items: ApiEmailRequestSummary[]
  page: number
  pageSize: number
  total: number
  totalPages: number
  pendingCount: number
  ignoredCount: number
  availableTags: string[]
}

function normalizeRequestState(value: string): EmailRequestState {
  return value.toLowerCase() as EmailRequestState
}

function normalizeDirection(value: string): EmailDirection {
  return value.toLowerCase() as EmailDirection
}

function normalizeMessageStatus(value: string): EmailMessageStatus {
  const key = value.replace(/([a-z])([A-Z])/g, '$1_$2').toLowerCase()
  const map: Record<string, EmailMessageStatus> = {
    received: 'received',
    pendingsend: 'pending_send',
    pending_send: 'pending_send',
    sending: 'sending',
    sent: 'sent',
    failed: 'failed',
  }
  return map[key] ?? map[value.toLowerCase()] ?? 'received'
}

function mapAttachmentDto(a: ApiEmailAttachmentDto): EmailAttachment {
  return {
    id: a.id,
    messageId: a.messageId,
    fileName: a.fileName,
    contentType: a.contentType,
    sizeBytes: a.sizeBytes,
  }
}

function normalizeChannel(value: string): import('../types').EmailChannel {
  return value.toLowerCase() as import('../types').EmailChannel
}

export function mapRequestSummary(r: ApiEmailRequestSummary): EmailRequestSummary {
  return {
    id: r.id,
    subject: r.subject,
    state: normalizeRequestState(String(r.state)),
    channel: normalizeChannel(String(r.channel ?? 'general')),
    lastMessageAt: r.lastMessageAtUtc,
    messageCount: r.messageCount,
    attachmentCount: r.attachmentCount,
    tags: r.tags ?? [],
    participants: r.participants ?? [],
    auditServiceId: r.auditServiceId ?? undefined,
    preview: r.preview ?? undefined,
  }
}

export function mapConversationMessage(m: ApiEmailMessageDto): ConversationMessage {
  return {
    id: m.id,
    threadId: m.threadId,
    providerMessageId: m.providerMessageId ?? undefined,
    direction: normalizeDirection(String(m.direction)),
    status: normalizeMessageStatus(String(m.status)),
    from: m.from,
    to: m.to ?? [],
    cc: m.cc ?? [],
    subject: m.subject,
    bodyText: m.bodyText,
    bodyHtml: m.bodyHtml ?? undefined,
    occurredAt: m.occurredAtUtc,
    lastError: m.lastError ?? undefined,
    attachments: (m.attachments ?? []).map(mapAttachmentDto),
  }
}

export function mapRequestDetail(r: ApiEmailRequestDetail): EmailRequestDetail {
  const replyDefaults: ReplyDefaults = {
    to: r.replyDefaults?.to ?? [],
    cc: r.replyDefaults?.cc ?? [],
    subject: r.replyDefaults?.subject ?? r.subject,
  }
  return {
    ...mapRequestSummary(r),
    messages: (r.messages ?? []).map(mapConversationMessage),
    attachments: (r.attachments ?? []).map(mapAttachmentDto),
    replyDefaults,
  }
}

export const authApi = {
  async login(email: string, password: string): Promise<AuthTokensDto> {
    const data = await apiFetch<AuthTokensDto>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    setTokens(data.accessToken, data.refreshToken)
    return data
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await apiFetch('/api/auth/change-password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    })
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
  listEmails: (page = 1, pageSize = 15, ignored = false, tag?: string) => {
    const params = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
      ignored: String(ignored),
    })
    if (tag) params.set('tag', tag)
    return apiFetch<PagedEmailsDto>(`/api/triage/emails?${params}`)
  },
  listRequests: (page = 1, pageSize = 15, ignored = false, tag?: string) => {
    const params = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
      ignored: String(ignored),
    })
    if (tag) params.set('tag', tag)
    return apiFetch<PagedRequestsDto>(`/api/triage/requests?${params}`)
  },
  getRequest: (requestId: string) =>
    apiFetch<ApiEmailRequestDetail>(`/api/triage/requests/${requestId}`),
  syncGmail: () =>
    apiFetch<GmailSyncResult>('/api/triage/sync-gmail', {
      method: 'POST',
    }),
  backfillRequests: () =>
    apiFetch<GmailSyncResult>('/api/triage/requests/backfill', { method: 'POST' }),
  seedDemoEmails: () =>
    apiFetch<{ message: string; created: number }>('/api/triage/seed-demo-emails', {
      method: 'POST',
    }),
  assign: (
    emailId: string,
    queue: AuditQueue,
    operadorId: string,
    details?: {
      paciente?: string
      dni?: string
      art?: string
      tipoServicio?: ServiceType
      especialidad?: string
      urgency?: UrgencyLevel
    },
  ) =>
    apiFetch<ApiService>(`/api/triage/emails/${emailId}/assign`, {
      method: 'POST',
      body: JSON.stringify({
        queue: queueToApi(queue),
        operadorId,
        paciente: details?.paciente || null,
        dni: details?.dni || null,
        art: details?.art || null,
        tipoServicio: details?.tipoServicio ? serviceTypeToApi(details.tipoServicio) : null,
        especialidad: details?.especialidad || null,
        urgency: details?.urgency ? urgencyToApi(details.urgency) : null,
      }),
    }),
  assignRequest: (
    requestId: string,
    queue: AuditQueue,
    operadorId: string,
    details: {
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
    },
  ) => {
    const form = new FormData()
    form.append('queue', queueToApi(queue))
    form.append('operadorId', operadorId)
    form.append('triageNote', details.triageNote)
    if (details.paciente) form.append('paciente', details.paciente)
    if (details.dni) form.append('dni', details.dni)
    if (details.art) form.append('art', details.art)
    if (details.numeroSiniestro) form.append('numeroSiniestro', details.numeroSiniestro)
    if (details.telefonoPaciente) form.append('telefonoPaciente', details.telefonoPaciente)
    if (details.emailPaciente) form.append('emailPaciente', details.emailPaciente)
    if (details.tipoServicio) form.append('tipoServicio', serviceTypeToApi(details.tipoServicio))
    if (details.especialidad) form.append('especialidad', details.especialidad)
    if (details.urgency) form.append('urgency', urgencyToApi(details.urgency))
    if (details.attachment) form.append('attachment', details.attachment)
    return apiFetch<ApiService>(`/api/triage/requests/${requestId}/assign`, {
      method: 'POST',
      body: form,
    })
  },
  parseDenuncia: (requestId: string, attachmentId: string) =>
    apiFetch<DenunciaParsedDto>(`/api/triage/requests/${requestId}/parse-denuncia`, {
      method: 'POST',
      body: JSON.stringify({ attachmentId }),
    }),
  ignore: (emailId: string) =>
    apiFetch<void>(`/api/triage/emails/${emailId}/ignore`, { method: 'POST' }),
  ignoreRequest: (requestId: string) =>
    apiFetch<void>(`/api/triage/requests/${requestId}/ignore`, { method: 'POST' }),
  restore: (emailId: string) =>
    apiFetch<void>(`/api/triage/emails/${emailId}/restore`, { method: 'POST' }),
  restoreRequest: (requestId: string) =>
    apiFetch<void>(`/api/triage/requests/${requestId}/restore`, { method: 'POST' }),
  updateTags: (emailId: string, tags: string[]) =>
    apiFetch<string[]>(`/api/triage/emails/${emailId}/tags`, {
      method: 'PUT',
      body: JSON.stringify({ tags }),
    }),
  updateRequestTags: (requestId: string, tags: string[]) =>
    apiFetch<string[]>(`/api/triage/requests/${requestId}/tags`, {
      method: 'PUT',
      body: JSON.stringify({ tags }),
    }),
  mergeRequests: (targetRequestId: string, sourceRequestIds: string[]) =>
    apiFetch<void>('/api/triage/requests/merge', {
      method: 'POST',
      body: JSON.stringify({ targetRequestId, sourceRequestIds }),
    }),
  replyRequest: (
    requestId: string,
    body: { to: string[]; cc?: string[]; bodyText: string; bodyHtml?: string },
    idempotencyKey: string,
  ) =>
    apiFetch<ApiEmailMessageDto>(`/api/triage/requests/${requestId}/reply`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ ...body, idempotencyKey }),
    }),
}

export const attachmentsApi = {
  async download(id: string, fileName: string) {
    const blob = await apiDownload(`/api/attachments/${id}/download`)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  },
}

export interface ServiceAttachmentDto {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAtUtc: string
}

export const servicesApi = {
  list: () => apiFetch<ApiService[]>('/api/services'),
  get: (id: string) => apiFetch<ApiService>(`/api/services/${id}`),
  getCorrespondence: (id: string) =>
    apiFetch<ApiEmailRequestDetail>(`/api/services/${id}/correspondence`),
  listAttachments: (id: string) =>
    apiFetch<ServiceAttachmentDto[]>(`/api/services/${id}/attachments`),
  uploadAttachments: (id: string, files: File[]) => {
    const form = new FormData()
    for (const file of files) {
      form.append('files', file, file.name)
    }
    return apiFetch<ServiceAttachmentDto[]>(`/api/services/${id}/attachments`, {
      method: 'POST',
      body: form,
    })
  },
  deleteAttachment: (serviceId: string, attachmentId: string) =>
    apiFetch<void>(`/api/services/${serviceId}/attachments/${attachmentId}`, {
      method: 'DELETE',
    }),
  sendEmail: (
    id: string,
    body: {
      to: string[]
      cc?: string[]
      subject: string
      bodyText: string
      bodyHtml?: string
      existingAttachmentIds?: string[]
      files?: File[]
    },
    idempotencyKey: string,
  ) => {
    const form = new FormData()
    form.append('to', body.to.join(', '))
    if (body.cc?.length) form.append('cc', body.cc.join(', '))
    form.append('subject', body.subject)
    form.append('bodyText', body.bodyText)
    if (body.bodyHtml) form.append('bodyHtml', body.bodyHtml)
    form.append('idempotencyKey', idempotencyKey)
    for (const attachmentId of body.existingAttachmentIds ?? []) {
      form.append('existingAttachmentIds', attachmentId)
    }
    for (const file of body.files ?? []) {
      form.append('files', file, file.name)
    }
    return apiFetch<ApiEmailMessageDto>(`/api/services/${id}/send-email`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: form,
    })
  },
  reply: (
    id: string,
    body: {
      to: string[]
      cc?: string[]
      bodyText: string
      bodyHtml?: string
      existingAttachmentIds?: string[]
      files?: File[]
    },
    idempotencyKey: string,
  ) => {
    const hasFiles =
      (body.files?.length ?? 0) > 0 || (body.existingAttachmentIds?.length ?? 0) > 0
    if (hasFiles) {
      const form = new FormData()
      form.append('to', body.to.join(', '))
      if (body.cc?.length) form.append('cc', body.cc.join(', '))
      form.append('bodyText', body.bodyText)
      if (body.bodyHtml) form.append('bodyHtml', body.bodyHtml)
      form.append('idempotencyKey', idempotencyKey)
      for (const attachmentId of body.existingAttachmentIds ?? []) {
        form.append('existingAttachmentIds', attachmentId)
      }
      for (const file of body.files ?? []) {
        form.append('files', file, file.name)
      }
      return apiFetch<ApiEmailMessageDto>(`/api/services/${id}/reply`, {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey },
        body: form,
      })
    }
    return apiFetch<ApiEmailMessageDto>(`/api/services/${id}/reply`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ ...body, idempotencyKey }),
    })
  },
  transition: (
    id: string,
    nextStatus: AuditStatus,
    extras?: {
      fechaTurnoUtc?: string
      profesional?: string
      prestadorId?: string
      reason?: string
      autorizacionCodigo?: string
      autorizacionDocumentoAttachmentId?: string
    },
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
      valorConciliadoArt?: number
      notas?: string
    },
  ) =>
    apiFetch(`/api/services/${id}/flags`, {
      method: 'PATCH',
      body: JSON.stringify(flags),
    }),
  setPrecios: (
    id: string,
    body: {
      tipoProfesional: 'auditor' | 'especialista'
      valorConsulta?: number
      valorConciliadoArt?: number
      precioCatalogoId?: string
      porcentajeConciliacionEspecialista?: 50 | 100
    },
  ) =>
    apiFetch<ApiService>(`/api/services/${id}/precios`, {
      method: 'POST',
      body: JSON.stringify({
        tipoProfesional: body.tipoProfesional === 'auditor' ? 'Auditor' : 'Especialista',
        valorConsulta: body.valorConsulta ?? null,
        valorConciliadoArt: body.valorConciliadoArt ?? null,
        precioCatalogoId: body.precioCatalogoId ?? null,
        porcentajeConciliacionEspecialista:
          body.tipoProfesional === 'especialista'
            ? (body.porcentajeConciliacionEspecialista ?? 100)
            : null,
      }),
    }),
  setAutorizacion: (
    id: string,
    body: { autorizacionCodigo?: string; autorizacionDocumentoAttachmentId?: string },
  ) =>
    apiFetch(`/api/services/${id}/autorizacion`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  markTardia: (
    id: string,
    body: { periodicity?: ChronicPeriodicity; intervalDays?: number; scheduleStartUtc?: string },
  ) =>
    apiFetch(`/api/services/${id}/coordinacion-tardia`, {
      method: 'POST',
      body: JSON.stringify({
        periodicity: body.periodicity ? chronicPeriodicityToApi(body.periodicity) : 'Monthly',
        intervalDays: body.intervalDays ?? null,
        scheduleStartUtc: body.scheduleStartUtc ?? null,
      }),
    }),
  createChronic: (body: {
    paciente: string
    art: string
    numeroSiniestro: string
    telefonoPaciente: string
    emailPaciente: string
    periodicity: ChronicPeriodicity
    intervalDays?: number
    scheduleStartUtc?: string
    operadorId?: string
    tipoServicio?: ServiceType
    especialidad?: string
    dni?: string
    notas?: string
  }) =>
    apiFetch<ApiService>('/api/services/chronic', {
      method: 'POST',
      body: JSON.stringify({
        paciente: body.paciente,
        art: body.art,
        numeroSiniestro: body.numeroSiniestro,
        telefonoPaciente: body.telefonoPaciente,
        emailPaciente: body.emailPaciente,
        periodicity: chronicPeriodicityToApi(body.periodicity),
        intervalDays: body.intervalDays ?? null,
        scheduleStartUtc: body.scheduleStartUtc ?? null,
        operadorId: body.operadorId ?? null,
        tipoServicio: body.tipoServicio ? serviceTypeToApi(body.tipoServicio) : null,
        especialidad: body.especialidad ?? null,
        dni: body.dni ?? null,
        notas: body.notas ?? null,
      }),
    }),
  updateChronicSchedule: (
    id: string,
    body: { periodicity: ChronicPeriodicity; intervalDays?: number; scheduleStartUtc?: string },
  ) =>
    apiFetch(`/api/services/${id}/chronic-schedule`, {
      method: 'PATCH',
      body: JSON.stringify({
        periodicity: chronicPeriodicityToApi(body.periodicity),
        intervalDays: body.intervalDays ?? null,
        scheduleStartUtc: body.scheduleStartUtc ?? null,
      }),
    }),
  assignOperador: (id: string, operadorId: string) =>
    apiFetch(`/api/services/${id}/assign-operador`, {
      method: 'POST',
      body: JSON.stringify({ operadorId }),
    }),
  renewChronic: (id: string) =>
    apiFetch(`/api/services/${id}/renew`, { method: 'POST' }),
}

export interface OperatorReportDto {
  operadorId?: string | null
  operadorName: string
  casosAProcesar: number
  vencidosAProcesar: number
  slaVencidos: number
  slaEnRiesgo: number
  promedioRetrasoSlaHoras?: number | null
  ingresosHoy: number
  coordinarVencidos: number
  cronicosPendientes: number
  celeste: number
  promedioDemoraGestionHoras?: number | null
  promedioDemoraProcesamientoHoras?: number | null
}

export interface ArtReportDto {
  art: string
  siniestrosMesActual: number
  siniestrosMesAnterior: number
}

export interface SlaDelaySummaryDto {
  casosConSla: number
  slaVencidos: number
  slaEnRiesgo: number
  slaATiempo: number
  promedioRetrasoHoras?: number | null
  maxRetrasoHoras?: number | null
  vencidosRojo: number
  vencidosAmarillo: number
  vencidosAzul: number
}

export interface ReportsSummaryDto {
  operators: OperatorReportDto[]
  byArt: ArtReportDto[]
  slaDelays: SlaDelaySummaryDto
  generatedAtUtc: string
  monthStartUtc: string
  previousMonthStartUtc: string
}

export const reportsApi = {
  summary: () => apiFetch<ReportsSummaryDto>('/api/reports/summary'),
  async exportExcel() {
    const blob = await apiDownload('/api/reports/export')
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `auditorias-${new Date().toISOString().slice(0, 10)}.xlsx`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  },
}

export interface InAppAlertDto {
  id: string
  auditServiceId: string
  serviceNumero?: number | null
  paciente?: string | null
  kind: string
  serviceStatus: string
  queue: string
  deadlineUtc: string
  message: string
  isRead: boolean
  createdAtUtc: string
}

export interface SlaRuleDto {
  id: string
  queue: string
  status: string
  durationValue: number
  durationUnit: 'hours' | 'days'
  warnBeforeHours: number
  isEnabled: boolean
}

export const alertsApi = {
  list: (unreadOnly = false) =>
    apiFetch<InAppAlertDto[]>(`/api/alerts?unreadOnly=${unreadOnly}`).then((items) =>
      items.map((a) => ({
        ...a,
        kind: String(a.kind),
        serviceStatus: String(a.serviceStatus).toLowerCase(),
        queue: String(a.queue).toLowerCase(),
      })),
    ),
  count: () => apiFetch<{ unread: number }>('/api/alerts/count'),
  markRead: (id: string) => apiFetch(`/api/alerts/${id}/read`, { method: 'POST' }),
  markAllRead: () => apiFetch('/api/alerts/read-all', { method: 'POST' }),
}

export const slaRulesApi = {
  list: () =>
    apiFetch<SlaRuleDto[]>('/api/sla-rules').then((rules) =>
      rules.map((r) => ({
        ...r,
        queue: String(r.queue).toLowerCase(),
        status: normalizeStatus(String(r.status)),
        durationUnit: String(r.durationUnit).toLowerCase() === 'days' ? 'days' : 'hours',
      })),
    ),
  update: (
    id: string,
    body: {
      durationValue: number
      durationUnit: 'hours' | 'days'
      warnBeforeHours: number
      isEnabled: boolean
    },
  ) =>
    apiFetch(`/api/sla-rules/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        durationValue: body.durationValue,
        durationUnit: body.durationUnit === 'days' ? 'Days' : 'Hours',
        warnBeforeHours: body.warnBeforeHours,
        isEnabled: body.isEnabled,
      }),
    }),
}

export const usersApi = {
  list: () => apiFetch<ApiUserListItem[]>('/api/users'),
  create: (body: {
    name: string
    email: string
    role: UserRole
    defaultQueue?: AuditQueue | null
    tempPassword?: string
  }) =>
    apiFetch<ApiUserListItem>('/api/users', {
      method: 'POST',
      body: JSON.stringify({
        name: body.name,
        email: body.email,
        role: roleToApi(body.role),
        defaultQueue: body.defaultQueue ? queueToApi(body.defaultQueue) : null,
        tempPassword: body.tempPassword ?? null,
      }),
    }),
  update: (
    id: string,
    body: { name: string; email: string; role: UserRole; defaultQueue?: AuditQueue | null },
  ) =>
    apiFetch<ApiUserListItem>(`/api/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: body.name,
        email: body.email,
        role: roleToApi(body.role),
        defaultQueue: body.defaultQueue ? queueToApi(body.defaultQueue) : null,
      }),
    }),
  deactivate: (id: string) =>
    apiFetch<void>(`/api/users/${id}/deactivate`, { method: 'POST' }),
  operators: (queue?: AuditQueue) => {
    const q = queue ? `?queue=${queueToApi(queue)}` : ''
    return apiFetch<ApiUserListItem[]>(`/api/users/operators${q}`)
  },
}

export interface PrestadorDto {
  id: string
  provincia: string
  localidad: string
  cuit?: string | null
  nombre: string
  drive?: string | null
  especialidad?: string | null
  servicio?: string | null
  domicilio?: string | null
  codigoPostal?: string | null
  telefonos?: string | null
  interno?: string | null
  horario?: string | null
  mailContacto?: string | null
  mailAdmision?: string | null
  convenios?: string | null
  operativo?: string | null
  adhesion?: string | null
  dni?: string | null
  matricula?: string | null
  afip?: string | null
  iibb?: string | null
  superintendencia?: string | null
  seguro?: string | null
  habSalud?: string | null
  habMunic?: string | null
  banco?: string | null
  sucursal?: string | null
  tipoCuenta?: string | null
  numeroCuenta?: string | null
  cbu?: string | null
  alias?: string | null
  ultimaActualizacionValores?: string | null
  valoresAcordados?: string | null
  formaPago?: string | null
  observaciones?: string | null
  requierePagoAnticipado: boolean
  valorConsulta?: number | null
  tieneFirma: boolean
  firmaFileName?: string | null
  firmaUploadedAtUtc?: string | null
  isActive: boolean
  excelRowNumber?: number | null
  createdAtUtc: string
  updatedAtUtc?: string | null
}

export interface PrestadorOptionDto {
  id: string
  nombre: string
  provincia: string
  localidad: string
  especialidad?: string | null
  servicio?: string | null
  telefonos?: string | null
  mailContacto?: string | null
  requierePagoAnticipado: boolean
  valorConsulta?: number | null
  valoresAcordados?: string | null
  formaPago?: string | null
  tieneFirma: boolean
}

export interface PrestadorImportResult {
  totalRows: number
  created: number
  updated: number
  skipped: number
  errors: string[]
}

export type PrestadorUpsertBody = {
  nombre: string
  provincia?: string | null
  localidad?: string | null
  cuit?: string | null
  drive?: string | null
  especialidad?: string | null
  servicio?: string | null
  domicilio?: string | null
  codigoPostal?: string | null
  telefonos?: string | null
  interno?: string | null
  horario?: string | null
  mailContacto?: string | null
  mailAdmision?: string | null
  convenios?: string | null
  operativo?: string | null
  adhesion?: string | null
  dni?: string | null
  matricula?: string | null
  afip?: string | null
  iibb?: string | null
  superintendencia?: string | null
  seguro?: string | null
  habSalud?: string | null
  habMunic?: string | null
  banco?: string | null
  sucursal?: string | null
  tipoCuenta?: string | null
  numeroCuenta?: string | null
  cbu?: string | null
  alias?: string | null
  ultimaActualizacionValores?: string | null
  valoresAcordados?: string | null
  formaPago?: string | null
  observaciones?: string | null
  isActive?: boolean
}

export interface PrestadorPageDto {
  items: PrestadorDto[]
  page: number
  pageSize: number
  total: number
  totalPages: number
  provincias: string[]
}

export const prestadoresApi = {
  list: (params?: {
    q?: string
    provincia?: string
    isActive?: boolean
    page?: number
    pageSize?: number
  }) => {
    const sp = new URLSearchParams()
    if (params?.q) sp.set('q', params.q)
    if (params?.provincia) sp.set('provincia', params.provincia)
    if (params?.isActive !== undefined) sp.set('isActive', String(params.isActive))
    if (params?.page) sp.set('page', String(params.page))
    if (params?.pageSize) sp.set('pageSize', String(params.pageSize))
    const qs = sp.toString()
    return apiFetch<PrestadorPageDto>(`/api/prestadores${qs ? `?${qs}` : ''}`)
  },
  active: (params?: { q?: string; provincia?: string; take?: number }) => {
    const sp = new URLSearchParams()
    if (params?.q) sp.set('q', params.q)
    if (params?.provincia) sp.set('provincia', params.provincia)
    if (params?.take) sp.set('take', String(params.take))
    const qs = sp.toString()
    return apiFetch<PrestadorOptionDto[]>(`/api/prestadores/active${qs ? `?${qs}` : ''}`)
  },
  create: (body: PrestadorUpsertBody) =>
    apiFetch<PrestadorDto>('/api/prestadores', {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  update: (id: string, body: PrestadorUpsertBody) =>
    apiFetch<PrestadorDto>(`/api/prestadores/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  activate: (id: string) =>
    apiFetch<void>(`/api/prestadores/${id}/activate`, { method: 'POST' }),
  deactivate: (id: string) =>
    apiFetch<void>(`/api/prestadores/${id}/deactivate`, { method: 'POST' }),
  importExcel: async (file: File) => {
    const form = new FormData()
    form.append('file', file, file.name)
    return apiFetch<PrestadorImportResult>('/api/prestadores/import', {
      method: 'POST',
      body: form,
    })
  },
  get: (id: string) => apiFetch<PrestadorDto>(`/api/prestadores/${id}`),
  recomputeValores: () =>
    apiFetch<{ updated: number }>('/api/prestadores/recompute-valores', { method: 'POST' }),
  uploadFirma: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file, file.name)
    return apiFetch<PrestadorDto>(`/api/prestadores/${id}/firma`, {
      method: 'POST',
      body: form,
    })
  },
  clearFirma: (id: string) =>
    apiFetch<void>(`/api/prestadores/${id}/firma`, { method: 'DELETE' }),
  downloadFirma: async (id: string, fileName: string) => {
    const blob = await apiDownload(`/api/prestadores/${id}/firma`)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
  },
}

export interface PacientePrestacionDto {
  id: string
  numero: number
  tipoServicio: ServiceType
  especialidad?: string | null
  status: string
  queue: string
  profesional?: string | null
  fechaIngresoUtc: string
  fechaTurnoUtc?: string | null
  coordinacionTardia: boolean
  valorPactado?: number | null
  tieneAutorizacion: boolean
}

export interface PacienteDto {
  id: string
  nombre: string
  dni?: string | null
  telefono?: string | null
  email?: string | null
  art?: string | null
  numeroSiniestro?: string | null
  prestacionesAbiertas: number
  prestaciones: PacientePrestacionDto[]
}

export const pacientesApi = {
  get: (id: string) => apiFetch<PacienteDto>(`/api/pacientes/${id}`),
  update: (
    id: string,
    body: {
      nombre: string
      dni?: string
      telefono?: string
      email?: string
      art?: string
      numeroSiniestro?: string
    },
  ) =>
    apiFetch<PacienteDto>(`/api/pacientes/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        nombre: body.nombre,
        dni: body.dni || null,
        telefono: body.telefono || null,
        email: body.email || null,
        art: body.art || null,
        numeroSiniestro: body.numeroSiniestro || null,
      }),
    }),
  createPrestacion: (
    id: string,
    body: {
      tipoServicio?: ServiceType
      queue?: AuditQueue
      especialidad?: string
      notas?: string
      coordinacionTardia?: boolean
      periodicity?: ChronicPeriodicity
      intervalDays?: number
      scheduleStartUtc?: string
    },
  ) =>
    apiFetch<{ id: string; numero: number }>(`/api/pacientes/${id}/prestaciones`, {
      method: 'POST',
      body: JSON.stringify({
        tipoServicio: body.tipoServicio ? serviceTypeToApi(body.tipoServicio) : undefined,
        queue: body.queue ? queueToApi(body.queue) : undefined,
        especialidad: body.especialidad,
        notas: body.notas,
        coordinacionTardia: body.coordinacionTardia ?? false,
        periodicity: body.periodicity ? chronicPeriodicityToApi(body.periodicity) : undefined,
        intervalDays: body.intervalDays,
        scheduleStartUtc: body.scheduleStartUtc ?? null,
      }),
    }),
  backfill: () => apiFetch<void>('/api/pacientes/backfill', { method: 'POST' }),
}

export type TipoProfesionalApi = 'auditor' | 'especialista'

export interface PrecioCatalogoDto {
  id: string
  tipoProfesional: string
  artNombre?: string | null
  concepto: string
  valor: number
  isActive: boolean
  notas?: string | null
  createdAtUtc: string
  updatedAtUtc?: string | null
}

export interface PrecioCatalogoListDto {
  items: PrecioCatalogoDto[]
  page: number
  pageSize: number
  total: number
  totalPages: number
  arts: string[]
}

export interface PrecioOptionDto {
  id: string
  concepto: string
  valor: number
  artNombre?: string | null
  tipoProfesional: string
}

export const preciosApi = {
  list: (params?: {
    tipo?: TipoProfesionalApi
    art?: string
    q?: string
    soloActivos?: boolean
    page?: number
    pageSize?: number
  }) => {
    const qs = new URLSearchParams()
    if (params?.tipo)
      qs.set('tipo', params.tipo === 'auditor' ? 'Auditor' : 'Especialista')
    if (params?.art) qs.set('art', params.art)
    if (params?.q) qs.set('q', params.q)
    if (params?.soloActivos != null) qs.set('soloActivos', String(params.soloActivos))
    if (params?.page) qs.set('page', String(params.page))
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize))
    const q = qs.toString()
    return apiFetch<PrecioCatalogoListDto>(`/api/precios${q ? `?${q}` : ''}`)
  },
  options: (params: { tipo: TipoProfesionalApi; art?: string; q?: string }) => {
    const qs = new URLSearchParams()
    qs.set('tipo', params.tipo === 'auditor' ? 'Auditor' : 'Especialista')
    if (params.art) qs.set('art', params.art)
    if (params.q) qs.set('q', params.q)
    return apiFetch<PrecioOptionDto[]>(`/api/precios/options?${qs}`)
  },
  create: (body: {
    tipoProfesional: TipoProfesionalApi
    concepto: string
    valor: number
    artNombre?: string
    notas?: string
  }) =>
    apiFetch<PrecioCatalogoDto>('/api/precios', {
      method: 'POST',
      body: JSON.stringify({
        tipoProfesional: body.tipoProfesional === 'auditor' ? 'Auditor' : 'Especialista',
        concepto: body.concepto,
        valor: body.valor,
        artNombre: body.artNombre || null,
        notas: body.notas || null,
      }),
    }),
  update: (
    id: string,
    body: {
      tipoProfesional: TipoProfesionalApi
      concepto: string
      valor: number
      artNombre?: string
      notas?: string
      isActive?: boolean
    },
  ) =>
    apiFetch<PrecioCatalogoDto>(`/api/precios/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        tipoProfesional: body.tipoProfesional === 'auditor' ? 'Auditor' : 'Especialista',
        concepto: body.concepto,
        valor: body.valor,
        artNombre: body.artNombre || null,
        notas: body.notas || null,
        isActive: body.isActive,
      }),
    }),
  activate: (id: string) =>
    apiFetch<void>(`/api/precios/${id}/activate`, { method: 'POST' }),
  deactivate: (id: string) =>
    apiFetch<void>(`/api/precios/${id}/deactivate`, { method: 'POST' }),
}

function roleToApi(role: UserRole): string {
  const map: Record<UserRole, string> = {
    admin: 'Admin',
    jefatura: 'Jefatura',
    operador: 'Operador',
    telemedicina: 'Telemedicina',
    cronicos: 'Cronicos',
    facturacion: 'Facturacion',
  }
  return map[role]
}

function queueToApi(queue: AuditQueue): string {
  const map: Record<AuditQueue, string> = {
    general: 'General',
    telemedicina: 'Telemedicina',
    cronicos: 'Cronicos',
  }
  return map[queue]
}

function serviceTypeToApi(type: ServiceType): string {
  const map: Record<ServiceType, string> = {
    consultorio: 'Consultorio',
    terreno: 'Terreno',
    domicilio: 'Domicilio',
    telemedicina: 'Telemedicina',
    comision_medica: 'ComisionMedica',
    valoracion_dano: 'ValoracionDano',
    otra_auditoria: 'OtraAuditoria',
  }
  return map[type]
}

function urgencyToApi(urgency: UrgencyLevel): string {
  const map: Record<UrgencyLevel, string> = {
    normal: 'Normal',
    alta: 'Alta',
    critica: 'Critica',
  }
  return map[urgency]
}

function statusToApi(status: AuditStatus): string {
  const map: Record<AuditStatus, string> = {
    rojo: 'Rojo',
    amarillo: 'Amarillo',
    azul: 'Azul',
    verde: 'Verde',
    celeste: 'Celeste',
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
