import {
  ChevronLeft,
  ChevronRight,
  Loader2,
  Stethoscope,
  Upload,
  UserMinus,
  UserPlus,
} from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import {
  prestadoresApi,
  type PrestadorDto,
  type PrestadorImportResult,
} from '../api/auditartApi'
import { PrestadorDetailModal } from '../components/prestadores/PrestadorDetailModal'
import { PageHeader } from '../components/ui/PageHeader'
import { formatCurrency } from '../utils/format'

const PAGE_SIZE_OPTIONS = [25, 50, 100] as const

export function PrestadoresPage() {
  const [items, setItems] = useState<PrestadorDto[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState('')
  const [q, setQ] = useState('')
  const [qDebounced, setQDebounced] = useState('')
  const [provincia, setProvincia] = useState('')
  const [provincias, setProvincias] = useState<string[]>([])
  const [soloActivos, setSoloActivos] = useState(true)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [importResult, setImportResult] = useState<PrestadorImportResult | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [nombre, setNombre] = useState('')
  const [createProvincia, setCreateProvincia] = useState('')
  const [createLocalidad, setCreateLocalidad] = useState('')
  const [especialidad, setEspecialidad] = useState('')
  const [telefonos, setTelefonos] = useState('')
  const [mailContacto, setMailContacto] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  useEffect(() => {
    const timer = window.setTimeout(() => setQDebounced(q.trim()), 300)
    return () => window.clearTimeout(timer)
  }, [q])

  useEffect(() => {
    setPage(1)
  }, [qDebounced, provincia, soloActivos, pageSize])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await prestadoresApi.list({
        q: qDebounced || undefined,
        provincia: provincia.trim() || undefined,
        isActive: soloActivos ? true : undefined,
        page,
        pageSize,
      })
      setItems(result.items)
      setPage(result.page)
      setTotal(result.total)
      setTotalPages(result.totalPages)
      setProvincias(result.provincias)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los prestadores')
    } finally {
      setLoading(false)
    }
  }, [qDebounced, provincia, soloActivos, page, pageSize])

  useEffect(() => {
    void load()
  }, [load])

  const showMsg = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(''), 3500)
  }

  const handleImport = async (file: File | null) => {
    if (!file) return
    setBusy(true)
    setError(null)
    setImportResult(null)
    try {
      const result = await prestadoresApi.importExcel(file)
      setImportResult(result)
      const recomputed = await prestadoresApi.recomputeValores()
      showMsg(
        `Importación: ${result.created} nuevos, ${result.updated} actualizados · valores: ${recomputed.updated}`,
      )
      setPage(1)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al importar Excel')
    } finally {
      setBusy(false)
    }
  }

  const handleRecompute = async () => {
    setBusy(true)
    setError(null)
    try {
      const result = await prestadoresApi.recomputeValores()
      showMsg(`Valores recalculados en ${result.updated} prestadores`)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron recalcular valores')
    } finally {
      setBusy(false)
    }
  }

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await prestadoresApi.create({
        nombre: nombre.trim(),
        provincia: createProvincia.trim() || null,
        localidad: createLocalidad.trim() || null,
        especialidad: especialidad.trim() || null,
        telefonos: telefonos.trim() || null,
        mailContacto: mailContacto.trim() || null,
        isActive: true,
      })
      setNombre('')
      setCreateProvincia('')
      setCreateLocalidad('')
      setEspecialidad('')
      setTelefonos('')
      setMailContacto('')
      setShowCreate(false)
      showMsg('Prestador creado')
      setPage(1)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear')
    } finally {
      setBusy(false)
    }
  }

  const toggleActive = async (p: PrestadorDto) => {
    setBusy(true)
    try {
      if (p.isActive) await prestadoresApi.deactivate(p.id)
      else await prestadoresApi.activate(p.id)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar')
    } finally {
      setBusy(false)
    }
  }

  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Doctores / Prestadores"
        subtitle="Cartilla operativa · importar Excel PRESTADORES · Admin / Jefatura"
      />

      {toast && (
        <div className="mb-4 rounded-xl bg-green-50 px-4 py-3 text-sm font-semibold text-green-700">
          {toast}
        </div>
      )}
      {error && (
        <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      {importResult && (
        <div className="mb-4 rounded-xl bg-auditart-light px-4 py-3 text-sm text-auditart-navy">
          Filas leídas: {importResult.totalRows} · Creados: {importResult.created} ·
          Actualizados: {importResult.updated} · Omitidos: {importResult.skipped}
          {importResult.errors.length > 0 && (
            <ul className="mt-2 list-disc pl-5 text-xs text-red-700">
              {importResult.errors.map((err) => (
                <li key={err}>{err}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-xs font-semibold text-auditart-gray">
          Buscar
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Nombre, CUIT, especialidad…"
            className="input-modern w-64 px-3 py-2 text-sm"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs font-semibold text-auditart-gray">
          Provincia
          <input
            value={provincia}
            onChange={(e) => setProvincia(e.target.value)}
            list="prov-list"
            placeholder="Filtrar provincia"
            className="input-modern w-48 px-3 py-2 text-sm"
          />
          <datalist id="prov-list">
            {provincias.map((p) => (
              <option key={p} value={p} />
            ))}
          </datalist>
        </label>
        <label className="flex flex-col gap-1 text-xs font-semibold text-auditart-gray">
          Por página
          <select
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value))}
            className="input-modern px-3 py-2 text-sm"
          >
            {PAGE_SIZE_OPTIONS.map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 pb-2 text-sm text-auditart-navy">
          <input
            type="checkbox"
            checked={soloActivos}
            onChange={(e) => setSoloActivos(e.target.checked)}
          />
          Solo activos
        </label>
        <label className="btn-primary inline-flex cursor-pointer items-center gap-2 text-sm disabled:opacity-50">
          {busy ? <Loader2 size={14} className="animate-spin" /> : <Upload size={14} />}
          Importar Excel
          <input
            type="file"
            accept=".xlsx,.xlsm"
            className="hidden"
            disabled={busy}
            onChange={(e) => void handleImport(e.target.files?.[0] ?? null)}
          />
        </label>
        <button
          type="button"
          disabled={busy}
          onClick={() => void handleRecompute()}
          className="inline-flex items-center gap-2 rounded-xl border border-auditart-navy/15 px-4 py-2 text-sm font-semibold text-auditart-navy hover:bg-auditart-light disabled:opacity-50"
        >
          Recalcular valores
        </button>
        <button
          type="button"
          onClick={() => setShowCreate((v) => !v)}
          className="inline-flex items-center gap-2 rounded-xl border border-auditart-navy/15 px-4 py-2 text-sm font-semibold text-auditart-navy hover:bg-auditart-light"
        >
          <UserPlus size={14} /> Nuevo
        </button>
      </div>

      {showCreate && (
        <form
          onSubmit={(e) => void handleCreate(e)}
          className="card-flat mb-4 grid gap-3 p-4 md:grid-cols-3"
        >
          <input
            required
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Nombre / prestador *"
            className="input-modern px-3 py-2 text-sm md:col-span-2"
          />
          <input
            value={especialidad}
            onChange={(e) => setEspecialidad(e.target.value)}
            placeholder="Especialidad"
            className="input-modern px-3 py-2 text-sm"
          />
          <input
            value={createProvincia}
            onChange={(e) => setCreateProvincia(e.target.value)}
            placeholder="Provincia"
            className="input-modern px-3 py-2 text-sm"
          />
          <input
            value={createLocalidad}
            onChange={(e) => setCreateLocalidad(e.target.value)}
            placeholder="Localidad"
            className="input-modern px-3 py-2 text-sm"
          />
          <input
            value={telefonos}
            onChange={(e) => setTelefonos(e.target.value)}
            placeholder="Teléfonos"
            className="input-modern px-3 py-2 text-sm"
          />
          <input
            value={mailContacto}
            onChange={(e) => setMailContacto(e.target.value)}
            placeholder="Mail contacto"
            className="input-modern px-3 py-2 text-sm md:col-span-2"
          />
          <button
            type="submit"
            disabled={busy}
            className="btn-primary text-sm disabled:opacity-50 md:col-span-3"
          >
            Guardar prestador
          </button>
        </form>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      ) : items.length === 0 ? (
        <div className="card-flat flex flex-col items-center gap-2 p-10 text-sm text-auditart-gray">
          <Stethoscope size={28} className="text-auditart-muted" />
          Sin prestadores. Importá el Excel de cartilla (hoja PRESTADORES).
        </div>
      ) : (
        <div className="card-flat overflow-x-auto">
          <table className="w-full min-w-[1200px] text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                <th className="px-3 py-3">Prestador</th>
                <th className="px-3 py-3">Provincia</th>
                <th className="px-3 py-3">Localidad</th>
                <th className="px-3 py-3">Especialidad</th>
                <th className="px-3 py-3">Valor consulta</th>
                <th className="px-3 py-3">Pago anticipado</th>
                <th className="px-3 py-3">Firma</th>
                <th className="px-3 py-3">Contacto</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {items.map((p) => (
                <tr key={p.id} className="border-b border-gray-50 align-top">
                  <td className="px-3 py-3">
                    <button
                      type="button"
                      onClick={() => setSelectedId(p.id)}
                      className="text-left font-semibold text-auditart-blue hover:underline"
                    >
                      {p.nombre}
                    </button>
                  </td>
                  <td className="px-3 py-3">{p.provincia || '—'}</td>
                  <td className="px-3 py-3">{p.localidad || '—'}</td>
                  <td className="px-3 py-3">{p.especialidad || '—'}</td>
                  <td className="px-3 py-3 font-semibold text-auditart-navy">
                    {formatCurrency(p.valorConsulta ?? undefined)}
                  </td>
                  <td className="px-3 py-3">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                        p.requierePagoAnticipado
                          ? 'bg-amber-100 text-amber-800'
                          : 'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {p.requierePagoAnticipado ? 'Sí' : 'No'}
                    </span>
                  </td>
                  <td className="px-3 py-3 text-xs">
                    {p.tieneFirma ? (
                      <span className="font-semibold text-green-700">Cargada</span>
                    ) : (
                      <span className="text-auditart-muted">—</span>
                    )}
                  </td>
                  <td className="px-3 py-3 text-xs">
                    <div>{p.telefonos || '—'}</div>
                    <div className="text-auditart-muted">{p.mailContacto || ''}</div>
                  </td>
                  <td className="px-3 py-3">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                        p.isActive
                          ? 'bg-green-100 text-green-800'
                          : 'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {p.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="px-3 py-3">
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void toggleActive(p)}
                      className="inline-flex items-center gap-1 text-xs font-semibold text-auditart-blue hover:underline disabled:opacity-50"
                    >
                      <UserMinus size={12} />
                      {p.isActive ? 'Desactivar' : 'Activar'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-50 px-4 py-3">
            <p className="text-xs text-auditart-muted">
              {from}–{to} de {total} · clic en el nombre para ver ficha y firma
            </p>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={page <= 1 || loading}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-auditart-navy hover:bg-auditart-light disabled:opacity-40"
              >
                <ChevronLeft size={14} /> Anterior
              </button>
              <span className="text-xs font-semibold text-auditart-gray">
                {page} / {totalPages}
              </span>
              <button
                type="button"
                disabled={page >= totalPages || loading}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-auditart-navy hover:bg-auditart-light disabled:opacity-40"
              >
                Siguiente <ChevronRight size={14} />
              </button>
            </div>
          </div>
        </div>
      )}

      {selectedId && (
        <PrestadorDetailModal
          prestadorId={selectedId}
          allowFirmaUpload
          onClose={() => setSelectedId(null)}
          onUpdated={(updated) => {
            setItems((prev) => prev.map((p) => (p.id === updated.id ? updated : p)))
          }}
        />
      )}
    </div>
  )
}
