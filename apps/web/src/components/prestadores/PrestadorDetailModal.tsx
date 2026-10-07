import { Loader2, Pencil, PenLine, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import {
  prestadoresApi,
  type PrestadorDto,
  type PrestadorUpsertBody,
} from '../../api/auditartApi'
import { formatCurrency, formatDateTime } from '../../utils/format'

type TextFieldKey = Exclude<
  keyof PrestadorUpsertBody,
  'isActive' | 'requierePagoAnticipado' | 'valorConsulta'
>

type FieldDef = { key: TextFieldKey; label: string; wide?: boolean; multiline?: boolean }

const FIELD_GROUPS: { title: string; fields: FieldDef[] }[] = [
  {
    title: 'Identificación',
    fields: [
      { key: 'nombre', label: 'Nombre / prestador *', wide: true },
      { key: 'especialidad', label: 'Especialidad' },
      { key: 'servicio', label: 'Servicio' },
      { key: 'cuit', label: 'CUIT' },
      { key: 'dni', label: 'DNI' },
      { key: 'matricula', label: 'Matrícula' },
    ],
  },
  {
    title: 'Ubicación y contacto',
    fields: [
      { key: 'provincia', label: 'Provincia' },
      { key: 'localidad', label: 'Localidad' },
      { key: 'domicilio', label: 'Domicilio', wide: true },
      { key: 'codigoPostal', label: 'CP' },
      { key: 'telefonos', label: 'Teléfonos' },
      { key: 'interno', label: 'Interno' },
      { key: 'horario', label: 'Horario' },
      { key: 'mailContacto', label: 'Mail contacto' },
      { key: 'mailAdmision', label: 'Mail admisión' },
    ],
  },
  {
    title: 'Valores y pago',
    fields: [
      { key: 'valoresAcordados', label: 'Valores acordados', wide: true, multiline: true },
      { key: 'formaPago', label: 'Forma de pago' },
      { key: 'ultimaActualizacionValores', label: 'Últ. act. valores' },
    ],
  },
  {
    title: 'Convenios y habilitaciones',
    fields: [
      { key: 'convenios', label: 'Convenios' },
      { key: 'operativo', label: 'Operativo' },
      { key: 'adhesion', label: 'Adhesión' },
      { key: 'afip', label: 'AFIP' },
      { key: 'iibb', label: 'IIBB' },
      { key: 'superintendencia', label: 'Superintendencia' },
      { key: 'seguro', label: 'Seguro' },
      { key: 'habSalud', label: 'Hab. salud' },
      { key: 'habMunic', label: 'Hab. munic.' },
    ],
  },
  {
    title: 'Datos bancarios',
    fields: [
      { key: 'banco', label: 'Banco' },
      { key: 'sucursal', label: 'Sucursal' },
      { key: 'tipoCuenta', label: 'Tipo cuenta' },
      { key: 'numeroCuenta', label: 'Nº cuenta' },
      { key: 'cbu', label: 'CBU' },
      { key: 'alias', label: 'Alias' },
      { key: 'drive', label: 'Drive', wide: true },
    ],
  },
  {
    title: 'Observaciones',
    fields: [{ key: 'observaciones', label: 'Observaciones', wide: true, multiline: true }],
  },
]

type FormState = Record<TextFieldKey, string> & {
  valorConsulta: string
  requierePagoAnticipado: boolean
  isActive: boolean
}

function toForm(p: PrestadorDto): FormState {
  const form = {} as FormState
  for (const group of FIELD_GROUPS) {
    for (const f of group.fields) {
      form[f.key] = (p[f.key] as string | null | undefined) ?? ''
    }
  }
  form.valorConsulta = p.valorConsulta != null ? String(p.valorConsulta) : ''
  form.requierePagoAnticipado = p.requierePagoAnticipado
  form.isActive = p.isActive
  return form
}

function toBody(form: FormState): PrestadorUpsertBody {
  const body = { nombre: form.nombre.trim() } as PrestadorUpsertBody
  for (const group of FIELD_GROUPS) {
    for (const f of group.fields) {
      if (f.key === 'nombre') continue
      body[f.key] = form[f.key].trim() || null
    }
  }
  const valor = form.valorConsulta.trim()
  // "45.000,50" (formato AR) o "45000.5" (formato máquina)
  const normalized = valor.includes(',') ? valor.replace(/\./g, '').replace(',', '.') : valor
  body.valorConsulta = valor ? Number(normalized) : null
  body.requierePagoAnticipado = form.requierePagoAnticipado
  body.isActive = form.isActive
  return body
}

export function PrestadorDetailModal({
  prestadorId,
  allowFirmaUpload,
  allowEdit,
  onClose,
  onUpdated,
}: {
  prestadorId: string
  allowFirmaUpload?: boolean
  allowEdit?: boolean
  onClose: () => void
  onUpdated?: (p: PrestadorDto) => void
}) {
  const [data, setData] = useState<PrestadorDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [form, setForm] = useState<FormState | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    void prestadoresApi
      .get(prestadorId)
      .then((dto) => {
        if (!cancelled) setData(dto)
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof Error ? e.message : 'No se pudo cargar el prestador')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [prestadorId])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose()
    }
    window.addEventListener('keydown', onKey)
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = previousOverflow
    }
  }, [busy, onClose])

  const editing = form !== null

  const setField = (key: keyof FormState, value: string | boolean) =>
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev))

  const saveEdit = async () => {
    if (!data || !form) return
    if (!form.nombre.trim()) {
      setError('El nombre del prestador es obligatorio.')
      return
    }
    const body = toBody(form)
    if (body.valorConsulta != null && Number.isNaN(body.valorConsulta)) {
      setError('Valor de consulta inválido.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const updated = await prestadoresApi.update(data.id, body)
      setData(updated)
      onUpdated?.(updated)
      setForm(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar el prestador')
    } finally {
      setBusy(false)
    }
  }

  const uploadFirma = async (file: File | null) => {
    if (!file || !data) return
    setBusy(true)
    setError(null)
    try {
      const updated = await prestadoresApi.uploadFirma(data.id, file)
      setData(updated)
      onUpdated?.(updated)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo subir la firma')
    } finally {
      setBusy(false)
    }
  }

  const clearFirma = async () => {
    if (!data) return
    setBusy(true)
    setError(null)
    try {
      await prestadoresApi.clearFirma(data.id)
      const updated = await prestadoresApi.get(data.id)
      setData(updated)
      onUpdated?.(updated)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo quitar la firma')
    } finally {
      setBusy(false)
    }
  }

  const downloadFirma = async () => {
    if (!data) return
    try {
      await prestadoresApi.downloadFirma(data.id, data.firmaFileName ?? 'firma.png')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo descargar la firma')
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget && !busy && !editing) onClose()
      }}
    >
      <div className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-start justify-between gap-3 border-b border-gray-100 px-5 py-4">
          <div className="min-w-0">
            <h3 className="truncate text-lg font-bold text-auditart-navy">
              {editing ? 'Editar doctor' : (data?.nombre ?? 'Ficha del doctor')}
            </h3>
            <p className="text-xs text-auditart-muted">
              {[data?.especialidad, data?.provincia, data?.localidad].filter(Boolean).join(' · ') ||
                'Detalle completo del prestador'}
            </p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {allowEdit && data && !editing && (
              <button
                type="button"
                onClick={() => {
                  setError(null)
                  setForm(toForm(data))
                }}
                className="inline-flex items-center gap-1.5 rounded-xl bg-auditart-blue px-3 py-1.5 text-xs font-semibold text-white hover:bg-auditart-blue/90"
              >
                <Pencil size={13} /> Editar
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              disabled={busy}
              className="rounded-lg p-1.5 text-auditart-muted hover:bg-gray-50 hover:text-auditart-navy disabled:opacity-50"
            >
              <X size={18} />
            </button>
          </div>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {loading && (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando…
            </div>
          )}
          {error && (
            <div className="mb-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
          )}

          {data && form && (
            <div className="space-y-5">
              <section className="grid gap-3 rounded-xl bg-auditart-light/60 p-4 sm:grid-cols-3">
                <label className="text-xs font-semibold text-auditart-gray">
                  Valor consulta
                  <input
                    value={form.valorConsulta}
                    onChange={(e) => setField('valorConsulta', e.target.value)}
                    inputMode="decimal"
                    placeholder="Ej. 45000"
                    className="input-modern mt-1 w-full px-3 py-2 text-sm"
                  />
                </label>
                <label className="flex items-center gap-2 pt-5 text-sm text-auditart-navy">
                  <input
                    type="checkbox"
                    checked={form.requierePagoAnticipado}
                    onChange={(e) => setField('requierePagoAnticipado', e.target.checked)}
                  />
                  Pago anticipado
                </label>
                <label className="flex items-center gap-2 pt-5 text-sm text-auditart-navy">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setField('isActive', e.target.checked)}
                  />
                  Activo
                </label>
              </section>

              {FIELD_GROUPS.map((group) => (
                <section key={group.title}>
                  <h4 className="mb-2 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
                    {group.title}
                  </h4>
                  <div className="grid gap-3 sm:grid-cols-2">
                    {group.fields.map((f) => (
                      <label
                        key={f.key}
                        className={`text-xs font-semibold text-auditart-gray ${
                          f.wide ? 'sm:col-span-2' : ''
                        }`}
                      >
                        {f.label}
                        {f.multiline ? (
                          <textarea
                            value={form[f.key]}
                            onChange={(e) => setField(f.key, e.target.value)}
                            rows={3}
                            className="input-modern mt-1 w-full px-3 py-2 text-sm"
                          />
                        ) : (
                          <input
                            value={form[f.key]}
                            onChange={(e) => setField(f.key, e.target.value)}
                            className="input-modern mt-1 w-full px-3 py-2 text-sm"
                          />
                        )}
                      </label>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          )}

          {data && !form && (
            <div className="space-y-4">
              <section className="grid gap-2 rounded-xl bg-auditart-light/60 p-4 sm:grid-cols-2">
                <Field label="Valor consulta" value={formatCurrency(data.valorConsulta ?? undefined)} emphasize />
                <Field
                  label="Pago anticipado"
                  value={data.requierePagoAnticipado ? 'Sí' : 'No'}
                  emphasize
                />
                <Field label="Valores acordados" value={data.valoresAcordados} />
                <Field label="Forma de pago" value={data.formaPago} />
              </section>

              <section className="grid gap-2 sm:grid-cols-2">
                <Field label="CUIT" value={data.cuit} />
                <Field label="DNI" value={data.dni} />
                <Field label="Matrícula" value={data.matricula} />
                <Field label="Servicio" value={data.servicio} />
                <Field label="Domicilio" value={data.domicilio} />
                <Field label="CP" value={data.codigoPostal} />
                <Field label="Teléfonos" value={data.telefonos} />
                <Field label="Interno" value={data.interno} />
                <Field label="Horario" value={data.horario} />
                <Field label="Mail contacto" value={data.mailContacto} />
                <Field label="Mail admisión" value={data.mailAdmision} />
                <Field label="Convenios" value={data.convenios} />
                <Field label="Operativo" value={data.operativo} />
                <Field label="Adhesión" value={data.adhesion} />
              </section>

              <section className="grid gap-2 sm:grid-cols-2">
                <Field label="AFIP" value={data.afip} />
                <Field label="IIBB" value={data.iibb} />
                <Field label="Superintendencia" value={data.superintendencia} />
                <Field label="Seguro" value={data.seguro} />
                <Field label="Hab. salud" value={data.habSalud} />
                <Field label="Hab. munic." value={data.habMunic} />
                <Field label="Banco" value={data.banco} />
                <Field label="Sucursal" value={data.sucursal} />
                <Field label="Tipo cuenta" value={data.tipoCuenta} />
                <Field label="Nº cuenta" value={data.numeroCuenta} />
                <Field label="CBU" value={data.cbu} />
                <Field label="Alias" value={data.alias} />
                <Field label="Últ. act. valores" value={data.ultimaActualizacionValores} />
                <Field label="Drive" value={data.drive} />
              </section>

              {data.observaciones && (
                <p className="rounded-xl bg-gray-50 p-3 text-sm text-auditart-navy/80">
                  {data.observaciones}
                </p>
              )}

              <section className="rounded-xl border border-gray-100 p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-auditart-navy">
                  <PenLine size={16} /> Firma del doctor
                </div>
                {data.tieneFirma ? (
                  <div className="flex flex-wrap items-center gap-3 text-sm">
                    <span className="text-auditart-gray">
                      {data.firmaFileName ?? 'Firma cargada'}
                      {data.firmaUploadedAtUtc
                        ? ` · ${formatDateTime(data.firmaUploadedAtUtc)}`
                        : ''}
                    </span>
                    <button
                      type="button"
                      onClick={() => void downloadFirma()}
                      className="font-semibold text-auditart-blue hover:underline"
                    >
                      Ver / descargar
                    </button>
                    {allowFirmaUpload && (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => void clearFirma()}
                        className="font-semibold text-red-600 hover:underline disabled:opacity-50"
                      >
                        Quitar
                      </button>
                    )}
                  </div>
                ) : (
                  <p className="text-sm text-auditart-muted">Sin firma cargada.</p>
                )}
                {allowFirmaUpload && (
                  <label className="mt-3 inline-flex cursor-pointer items-center gap-2 text-xs font-semibold text-auditart-blue hover:underline">
                    {busy ? <Loader2 size={14} className="animate-spin" /> : null}
                    {data.tieneFirma ? 'Reemplazar firma' : 'Subir firma (PNG/JPG)'}
                    <input
                      type="file"
                      accept=".png,.jpg,.jpeg,.webp,.gif,image/*"
                      className="hidden"
                      disabled={busy}
                      onChange={(e) => {
                        void uploadFirma(e.target.files?.[0] ?? null)
                        e.target.value = ''
                      }}
                    />
                  </label>
                )}
              </section>
            </div>
          )}
        </div>

        {editing && (
          <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-3">
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                setError(null)
                setForm(null)
              }}
              className="rounded-xl border border-gray-200 px-4 py-2 text-sm font-semibold text-auditart-navy hover:bg-gray-50 disabled:opacity-50"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => void saveEdit()}
              className="btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50"
            >
              {busy && <Loader2 size={14} className="animate-spin" />}
              Guardar cambios
            </button>
          </div>
        )}
      </div>
    </div>
  )
}

function Field({
  label,
  value,
  emphasize,
}: {
  label: string
  value?: string | null
  emphasize?: boolean
}) {
  return (
    <div>
      <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">{label}</p>
      <p
        className={`text-sm ${emphasize ? 'font-bold text-auditart-navy' : 'text-auditart-navy/90'}`}
      >
        {value?.trim() ? value : '—'}
      </p>
    </div>
  )
}
