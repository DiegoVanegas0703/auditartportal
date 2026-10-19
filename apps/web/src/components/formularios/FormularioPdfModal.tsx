import { FileText, Loader2, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import {
  servicesApi,
  type FormularioFieldDto,
  type FormularioTipo,
  type ServiceAttachmentDto,
} from '../../api/auditartApi'

const TIPOS: { id: FormularioTipo; label: string; short: string }[] = [
  { id: 'pmi', label: 'PMI — Ingreso', short: 'PMI' },
  { id: 'pme', label: 'PME — Alta médica', short: 'PME' },
  { id: 'pmr', label: 'PMR — Reingreso', short: 'PMR' },
]

export function FormularioPdfModal({
  serviceId,
  onClose,
  onGenerated,
}: {
  serviceId: string
  onClose: () => void
  onGenerated: (attachment: ServiceAttachmentDto) => void
}) {
  const [tipo, setTipo] = useState<FormularioTipo | null>(null)
  const [title, setTitle] = useState('')
  const [fields, setFields] = useState<FormularioFieldDto[]>([])
  const [values, setValues] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!tipo) return
    let cancelled = false
    setLoading(true)
    setError(null)
    void servicesApi
      .getFormularioPrefill(serviceId, tipo)
      .then((dto) => {
        if (cancelled) return
        setTitle(dto.title)
        setFields(dto.fields)
        const next: Record<string, string> = {}
        for (const f of dto.fields) next[f.pdfField] = f.value ?? ''
        setValues(next)
      })
      .catch((e: unknown) => {
        if (!cancelled)
          setError(e instanceof Error ? e.message : 'No se pudo cargar el formulario')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [serviceId, tipo])

  const generate = async () => {
    if (!tipo) return
    setBusy(true)
    setError(null)
    try {
      const attachment = await servicesApi.generateFormulario(serviceId, tipo, values)
      onGenerated(attachment)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo generar el PDF')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div>
            <h2 className="text-lg font-bold text-auditart-navy">Formularios PDF</h2>
            <p className="text-xs text-auditart-muted">
              Prefill con datos del paciente. El PDF generado sigue editable como la plantilla.
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

        <div className="flex gap-2 border-b border-gray-50 px-5 py-3">
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
            </button>
          ))}
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {!tipo && (
            <p className="text-sm text-auditart-gray">Elegí un formulario para continuar.</p>
          )}
          {tipo && loading && (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando campos…
            </div>
          )}
          {error && (
            <div className="mb-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
          )}
          {tipo && !loading && fields.length > 0 && (
            <>
              <p className="mb-3 text-sm font-semibold text-auditart-navy">{title}</p>
              <div className="grid gap-3 sm:grid-cols-2">
                {fields.map((f) => (
                  <label
                    key={f.pdfField}
                    className={`text-xs font-semibold text-auditart-gray ${
                      f.kind === 'textarea' || f.kind === 'select' ? 'sm:col-span-2' : ''
                    }`}
                  >
                    {f.label}
                    {f.logicalKey && (
                      <span className="ml-1 font-normal text-auditart-muted">(auto)</span>
                    )}
                    {f.kind === 'select' ? (
                      <select
                        value={values[f.pdfField] ?? ''}
                        onChange={(e) =>
                          setValues((prev) => ({ ...prev, [f.pdfField]: e.target.value }))
                        }
                        className="input-modern mt-1 w-full px-3 py-2 text-sm font-normal"
                      >
                        <option value="">Seleccioná…</option>
                        {(f.options ?? []).map((o) => (
                          <option key={o.value} value={o.value}>
                            {o.label}
                          </option>
                        ))}
                      </select>
                    ) : f.kind === 'checkbox' ? (
                      <input
                        type="checkbox"
                        checked={['1', 'true', 'yes', 'on', 'sí', 'si', 'x'].includes(
                          (values[f.pdfField] ?? '').toLowerCase(),
                        )}
                        onChange={(e) =>
                          setValues((prev) => ({
                            ...prev,
                            [f.pdfField]: e.target.checked ? 'Yes' : 'Off',
                          }))
                        }
                        className="mt-1 block h-4 w-4"
                      />
                    ) : f.kind === 'textarea' ? (
                      <textarea
                        value={values[f.pdfField] ?? ''}
                        onChange={(e) =>
                          setValues((prev) => ({ ...prev, [f.pdfField]: e.target.value }))
                        }
                        rows={3}
                        className="input-modern mt-1 w-full px-3 py-2 text-sm font-normal"
                      />
                    ) : (
                      <input
                        value={values[f.pdfField] ?? ''}
                        onChange={(e) =>
                          setValues((prev) => ({ ...prev, [f.pdfField]: e.target.value }))
                        }
                        className="input-modern mt-1 w-full px-3 py-2 text-sm font-normal"
                      />
                    )}
                  </label>
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
            disabled={!tipo || loading || busy || fields.length === 0}
            onClick={() => void generate()}
            className="btn-primary inline-flex items-center gap-2 text-sm disabled:opacity-50"
          >
            {busy ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <FileText size={14} />
            )}
            Generar PDF editable
          </button>
        </div>
      </div>
    </div>
  )
}
