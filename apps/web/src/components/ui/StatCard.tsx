import type { LucideIcon } from 'lucide-react'

interface StatCardProps {
  label: string
  value: number
  icon: LucideIcon
  variant: 'rojo' | 'amarillo' | 'azul' | 'verde'
  alert?: number
}

const variantStyles = {
  rojo: {
    iconBg: 'bg-red-100 text-red-600',
    value: 'text-red-700',
    bar: 'bg-red-400',
  },
  amarillo: {
    iconBg: 'bg-yellow-100 text-yellow-600',
    value: 'text-yellow-700',
    bar: 'bg-yellow-400',
  },
  azul: {
    iconBg: 'bg-blue-100 text-blue-600',
    value: 'text-blue-700',
    bar: 'bg-blue-400',
  },
  verde: {
    iconBg: 'bg-green-100 text-green-600',
    value: 'text-green-700',
    bar: 'bg-green-400',
  },
}

export function StatCard({ label, value, icon: Icon, variant, alert }: StatCardProps) {
  const styles = variantStyles[variant]

  return (
    <div className="card group relative overflow-hidden p-5">
      <div className={`absolute bottom-0 left-0 h-1 w-full ${styles.bar} opacity-60`} />
      <div className="flex items-start justify-between">
        <div
          className={`flex h-11 w-11 items-center justify-center rounded-xl ${styles.iconBg} transition-transform group-hover:scale-110`}
        >
          <Icon size={20} strokeWidth={2.5} />
        </div>
        {alert != null && alert > 0 && (
          <span className="flex items-center gap-1 rounded-full bg-red-500 px-2.5 py-1 text-xs font-bold text-white shadow-sm">
            {alert} SLA
          </span>
        )}
      </div>
      <p className={`mt-4 text-4xl font-extrabold tracking-tight ${styles.value}`}>
        {value}
      </p>
      <p className="mt-1 text-sm font-medium text-auditart-gray">{label}</p>
    </div>
  )
}
