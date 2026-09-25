import type { AuditQueue, AuditRecord, UrgencyLevel } from '../types'

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
