import type { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  subtitle?: string
  action?: ReactNode
}

export function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  return (
    <div className="mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="animate-fade-in">
        <h2 className="text-2xl font-bold tracking-tight text-auditart-navy">{title}</h2>
        {subtitle && (
          <p className="mt-1 text-sm text-auditart-gray">{subtitle}</p>
        )}
      </div>
      {action && <div className="animate-fade-in">{action}</div>}
    </div>
  )
}
