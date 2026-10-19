import { FileText, Loader2, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import {
  servicesApi,
  type InformeFieldDto,
  type InformeTipo,
  type ServiceAttachmentDto,
} from '../../api/auditartApi'
import type { ServiceType } from '../../types'

const TIPOS: { id: InformeTipo; label: string }[] = [
  { id: 'telemedicina', label: 'Telemedicina' },
  { id: 'terreno', label: 'Terreno' },
  { id: 'consultorio', label: 'Consultorio' },
]

function suggestTipo(serviceType?: ServiceType | null): InformeTipo {
  if (serviceType === 'telemedicina') return 'telemedicina'
  if (serviceType === 'terreno') return 'terreno'
  return 'consultorio'
}

export function InformeWordModal({
  serviceId,
  serviceType,
  onClose,
  onGenerated,
}: {
  serviceId: string
  serviceType?: ServiceType | null
  onClose: () => void
  onGenerated: (attachment: ServiceAttachmentDto) => void
}) {
  const [tipo, setTipo] = useState<InformeTipo>(() => suggestTipo(serviceType))
  const [title, setTitle] = useState('')
  const [fields, setFields] = useState<InformeFieldDto[]>([])
  const [values, setValues] = useState<Record<string, string>>({})
  const [firmaWarning, setFirmaWarning] = useState<string | null>(null)
  const [prestadorNombre, setPrestadorNombre] = useState<string | null>(null)
  const [tieneFirma, setTieneFirma] = useState(false)
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    void servicesApi
      .getInformePrefill(serviceId, tipo)
      .then((dto) => {
        if (cancelled) return
        setTitle(dto.title)
        setFields(dto.fields)
        setFirmaWarning(dto.firmaWarning ?? null)
        setPrestadorNombre(dto.prestadorNombre ?? null)
        setTieneFirma(!!dto.tieneFirmaPrestador)
        const next: Record<string, string> = {}
        for (const f of dto.fields) next[f.key] = f.value ?? ''
        setValues(next)
      })
      .catch((e: unknown) => {
        if (!cancelled)
          setError(e instanceof Error ? e.message : 'No se pudo cargar el informe')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [serviceId, tipo])

  const generate = async () => {
    setBusy(true)
    setError(null)
    try {
      const attachment = await servicesApi.generateInforme(serviceId, tipo, values)
      onGenerated(attachment)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo generar el Word')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div>
            <h2 className="text-lg font-bold text-auditart-navy">Informe de auditoría (Word)</h2>
            <p className="text-xs text-auditart-muted">
              Prefill con datos del caso. El .docx queda adjunto y se puede editar en Word.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-auditart-gray hover:bg-gray-100"
          >
            <X size={18} />
          </button>
        </div>

        <div className="flex flex-wrap gap-2 border-b border-gray-50 px-5 py-3">
          {TIPOS.map((t) => (
            <button
              key={t.id}
              type="button"
              onClick={() => setTipo(t.id)}
              className={`rounded-xl px-3 py-2 text-xs font-bold transition-colors ${
                tipo === t.id
                  ? 'bg-auditart-blue text-white'
                  : 'bg-gray-100 text-auditart-navy hover:bg-gray-200'
              }`}
            >
              {t.label}
              {suggestTipo(serviceType) === t.id ? ' · sugerido' : ''}
            </button>
          ))}
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {loading && (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando campos…
            </div>
          )}
          {error && (
            <div className="mb-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
          )}
          {!loading && (
            <>
              {title && (
                <p className="mb-3 text-sm font-semibold text-auditart-navy">{title}</p>
              )}
              {firmaWarning && (
                <div className="mb-3 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">
                  {firmaWarning}
                </div>
              )}
              {!firmaWarning && tieneFirma && (
                <p className="mb-3 text-xs text-emerald-700">
                  Se insertará la firma de {prestadorNombre ?? 'el prestador'} al final del
                  informe.
                </p>
              )}
              <div className="grid gap-3">
                {fields.map((f) => (
                  <FieldInput
                    key={f.key}
                    field={f}
                    value={values[f.key] ?? ''}
                    onChange={(v) => setValues((prev) => ({ ...prev, [f.key]: v }))}
                  />
                ))}
              </div>
            </>
          )}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-4">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border border-gray-200 px-4 py-2 text-sm font-semibold text-auditart-navy"
          >
            Cancelar
          </button>
          <button
            type="button"
            disabled={busy || loading || !(values.antecedentes ?? '').trim()}
            onClick={() => void generate()}
            className="inline-flex items-center gap-2 rounded-xl bg-auditart-blue px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {busy ? <Loader2 size={14} className="animate-spin" /> : <FileText size={14} />}
            Generar Word
          </button>
        </div>
      </div>
    </div>
  )
}

function FieldInput({
  field,
  value,
  onChange,
}: {
  field: InformeFieldDto
  value: string
  onChange: (v: string) => void
}) {
  const common =
    'w-full rounded-xl border border-gray-200 px-3 py-2 text-sm text-auditart-navy focus:border-auditart-blue focus:outline-none'

  return (
    <label className="block">
      <span className="mb-1 block text-xs font-semibold text-auditart-gray">{field.label}</span>
      {field.kind === 'select' ? (
        <select
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className={common}
        >
          {(field.options ?? []).map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      ) : field.kind === 'textarea' ? (
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          rows={field.key === 'antecedentes' ? 6 : 3}
          className={common}
          placeholder={
            field.key === 'antecedentes'
              ? 'Resumen de lo que indicó el médico…'
              : undefined
          }
        />
      ) : (
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className={common}
        />
      )}
    </label>
  )
}
