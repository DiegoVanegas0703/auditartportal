import { Loader2, Save } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { slaRulesApi, type SlaRuleDto } from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'
import {
  QUEUE_LABELS,
  STATUS_LABELS,
  type AuditQueue,
  type AuditStatus,
} from '../types'

const UNIT_LABELS = {
  hours: 'Horas',
  days: 'Días',
} as const

export function SlaRulesPage() {
  const [rules, setRules] = useState<SlaRuleDto[]>([])
  const [loading, setLoading] = useState(true)
  const [savingId, setSavingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setRules(await slaRulesApi.list())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar las reglas SLA')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const updateLocal = (id: string, patch: Partial<SlaRuleDto>) => {
    setRules((prev) => prev.map((r) => (r.id === id ? { ...r, ...patch } : r)))
  }

  const save = async (rule: SlaRuleDto) => {
    setSavingId(rule.id)
    setError(null)
    try {
      await slaRulesApi.update(rule.id, {
        durationValue: rule.durationValue,
        durationUnit: rule.durationUnit,
        warnBeforeHours: rule.warnBeforeHours,
        isEnabled: rule.isEnabled,
      })
      setToast('Regla guardada')
      setTimeout(() => setToast(''), 2500)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar')
    } finally {
      setSavingId(null)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Reglas SLA"
        subtitle="Configuración por cola y etapa · aviso “próximo a vencer” configurable"
      />

      {toast && (
        <div className="mb-4 rounded-xl bg-green-50 px-4 py-3 text-sm font-semibold text-green-700">
          {toast}
        </div>
      )}
      {error && (
        <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      ) : (
        <div className="card-flat overflow-x-auto">
          <table className="w-full min-w-[900px] text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                <th className="px-4 py-3">Cola</th>
                <th className="px-4 py-3">Etapa</th>
                <th className="px-4 py-3">Duración</th>
                <th className="px-4 py-3">Unidad</th>
                <th className="px-4 py-3">Aviso previo (h)</th>
                <th className="px-4 py-3">Activa</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {rules.map((rule) => (
                <tr key={rule.id} className="border-b border-gray-50">
                  <td className="px-4 py-3 font-semibold text-auditart-navy">
                    {QUEUE_LABELS[rule.queue as AuditQueue] ?? rule.queue}
                  </td>
                  <td className="px-4 py-3">
                    {STATUS_LABELS[rule.status as AuditStatus] ?? rule.status}
                  </td>
                  <td className="px-4 py-3">
                    <input
                      type="number"
                      min={1}
                      value={rule.durationValue}
                      onChange={(e) =>
                        updateLocal(rule.id, { durationValue: Number(e.target.value) })
                      }
                      className="input-modern w-24 px-2 py-1.5 text-sm"
                    />
                  </td>
                  <td className="px-4 py-3">
                    <select
                      value={rule.durationUnit}
                      onChange={(e) =>
                        updateLocal(rule.id, {
                          durationUnit: e.target.value as 'hours' | 'days',
                        })
                      }
                      className="input-modern px-2 py-1.5 text-sm"
                    >
                      <option value="hours">{UNIT_LABELS.hours}</option>
                      <option value="days">{UNIT_LABELS.days}</option>
                    </select>
                  </td>
                  <td className="px-4 py-3">
                    <input
                      type="number"
                      min={0}
                      value={rule.warnBeforeHours}
                      onChange={(e) =>
                        updateLocal(rule.id, { warnBeforeHours: Number(e.target.value) })
                      }
                      className="input-modern w-24 px-2 py-1.5 text-sm"
                    />
                  </td>
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      checked={rule.isEnabled}
                      onChange={(e) => updateLocal(rule.id, { isEnabled: e.target.checked })}
                    />
                  </td>
                  <td className="px-4 py-3">
                    <button
                      type="button"
                      disabled={savingId === rule.id}
                      onClick={() => void save(rule)}
                      className="flex items-center gap-1 rounded-lg bg-auditart-blue/10 px-3 py-1.5 text-xs font-bold text-auditart-blue disabled:opacity-50"
                    >
                      {savingId === rule.id ? (
                        <Loader2 size={12} className="animate-spin" />
                      ) : (
                        <Save size={12} />
                      )}
                      Guardar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
