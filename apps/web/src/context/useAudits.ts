import { useContext } from 'react'
import { AuditContext } from './audit-context'

export function useAudits() {
  const ctx = useContext(AuditContext)
  if (!ctx) throw new Error('useAudits debe usarse dentro de AuditProvider')
  return ctx
}
