import { STATUS_LABELS, type AuditStatus } from '../../types'

const badgeStyles: Record<AuditStatus, string> = {
  rojo: 'bg-red-50 text-red-700 ring-1 ring-red-200',
  amarillo: 'bg-yellow-50 text-yellow-700 ring-1 ring-yellow-200',
  azul: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  verde: 'bg-green-50 text-green-700 ring-1 ring-green-200',
}

const dotStyles: Record<AuditStatus, string> = {
  rojo: 'bg-red-500',
  amarillo: 'bg-yellow-500',
  azul: 'bg-blue-500',
  verde: 'bg-green-500',
}

interface StatusBadgeProps {
  status: AuditStatus
  compact?: boolean
}

export function StatusBadge({ status, compact }: StatusBadgeProps) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${badgeStyles[status]}`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${dotStyles[status]}`} />
      {compact ? status.charAt(0).toUpperCase() + status.slice(1) : STATUS_LABELS[status]}
    </span>
  )
}
