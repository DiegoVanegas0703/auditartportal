import { Loader2, PenLine, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { prestadoresApi, type PrestadorDto } from '../../api/auditartApi'
import { formatCurrency, formatDateTime } from '../../utils/format'

export function PrestadorDetailModal({
  prestadorId,
  allowFirmaUpload,
  onClose,
  onUpdated,
}: {
  prestadorId: string
  allowFirmaUpload?: boolean
  onClose: () => void
  onUpdated?: (p: PrestadorDto) => void
}) {
  const [data, setData] = useState<PrestadorDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-start justify-between border-b border-gray-100 px-5 py-4">
          <div>
            <h3 className="text-lg font-bold text-auditart-navy">
              {data?.nombre ?? 'Ficha del doctor'}
            </h3>
            <p className="text-xs text-auditart-muted">
              {[data?.especialidad, data?.provincia, data?.localidad].filter(Boolean).join(' · ') ||
                'Detalle completo del prestador'}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-auditart-muted hover:bg-gray-50 hover:text-auditart-navy"
          >
            <X size={18} />
          </button>
        </div>

        <div className="overflow-y-auto px-5 py-4">
          {loading && (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando…
            </div>
          )}
          {error && (
            <div className="mb-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
          )}
          {data && (
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
