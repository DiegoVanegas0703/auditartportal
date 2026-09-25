import { Loader2, Pencil, Plus, ToggleLeft, ToggleRight } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import {
  preciosApi,
  type PrecioCatalogoDto,
} from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'
import type { TipoProfesional } from '../types'
import { formatCurrency } from '../utils/format'

export function PreciosPage() {
  const [items, setItems] = useState<PrecioCatalogoDto[]>([])
  const [arts, setArts] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState('')
  const [tipo, setTipo] = useState<TipoProfesional | ''>('')
  const [art, setArt] = useState('')
  const [q, setQ] = useState('')
  const [soloActivos, setSoloActivos] = useState(true)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [total, setTotal] = useState(0)
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<PrecioCatalogoDto | null>(null)
  const [formTipo, setFormTipo] = useState<TipoProfesional>('especialista')
  const [formArt, setFormArt] = useState('')
  const [formConcepto, setFormConcepto] = useState('')
  const [formValor, setFormValor] = useState('')
  const [formNotas, setFormNotas] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await preciosApi.list({
        tipo: tipo || undefined,
        art: art.trim() || undefined,
        q: q.trim() || undefined,
        soloActivos: soloActivos ? true : undefined,
        page,
        pageSize: 50,
      })
      setItems(result.items)
      setArts(result.arts)
      setTotal(result.total)
      setTotalPages(result.totalPages)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los precios')
    } finally {
      setLoading(false)
    }
  }, [tipo, art, q, soloActivos, page])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    setPage(1)
  }, [tipo, art, q, soloActivos])

  const openCreate = () => {
    setEditing(null)
    setFormTipo('especialista')
    setFormArt('')
    setFormConcepto('')
    setFormValor('')
    setFormNotas('')
    setShowForm(true)
  }

  const openEdit = (item: PrecioCatalogoDto) => {
    setEditing(item)
    setFormTipo(normalizeTipo(item.tipoProfesional))
    setFormArt(item.artNombre ?? '')
    setFormConcepto(item.concepto)
    setFormValor(String(item.valor))
    setFormNotas(item.notas ?? '')
    setShowForm(true)
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    const valor = Number(formValor.replace(',', '.'))
    if (!formConcepto.trim() || Number.isNaN(valor)) {
      setError('Concepto y valor numérico son obligatorios.')
      return
    }
    if (formTipo === 'auditor' && !formArt.trim()) {
      setError('Para médico auditor indicá la ART.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const body = {
        tipoProfesional: formTipo,
        concepto: formConcepto.trim(),
        valor,
        artNombre: formTipo === 'auditor' ? formArt.trim() : undefined,
        notas: formNotas.trim() || undefined,
        isActive: editing?.isActive ?? true,
      }
      if (editing) await preciosApi.update(editing.id, body)
      else await preciosApi.create(body)
      setShowForm(false)
      setToast(editing ? 'Precio actualizado' : 'Precio creado')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar')
    } finally {
      setBusy(false)
    }
  }

  const toggleActive = async (item: PrecioCatalogoDto) => {
    setBusy(true)
    try {
      if (item.isActive) await preciosApi.deactivate(item.id)
      else await preciosApi.activate(item.id)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cambiar el estado')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Precios conciliados"
        subtitle="Catálogo para Facturación y Jefatura: valor a cobrar a la ART según auditor o especialista."
        action={
          <button type="button" onClick={openCreate} className="btn-primary text-sm">
            <Plus size={14} className="mr-1 inline" /> Nuevo precio
          </button>
        }
      />

      {toast && (
        <div className="mb-4 rounded-xl bg-emerald-50 px-4 py-2 text-sm text-emerald-800">
          {toast}
          <button type="button" className="ml-3 underline" onClick={() => setToast('')}>
            ok
          </button>
        </div>
      )}
      {error && (
        <div className="mb-4 rounded-xl bg-red-50 px-4 py-2 text-sm text-red-700">{error}</div>
      )}

      <div className="mb-4 flex flex-wrap gap-3">
        <select
          value={tipo}
          onChange={(e) => setTipo(e.target.value as TipoProfesional | '')}
          className="input-modern px-3 py-2 text-sm"
        >
          <option value="">Todos los tipos</option>
          <option value="auditor">Médico auditor</option>
          <option value="especialista">Especialista / estudio</option>
        </select>
        <select
          value={art}
          onChange={(e) => setArt(e.target.value)}
          className="input-modern px-3 py-2 text-sm"
        >
          <option value="">Todas las ART</option>
          {arts.map((a) => (
            <option key={a} value={a}>
              {a}
            </option>
          ))}
        </select>
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Buscar concepto…"
          className="input-modern min-w-[200px] flex-1 px-3 py-2 text-sm"
        />
        <label className="flex items-center gap-2 text-sm text-auditart-navy">
          <input
            type="checkbox"
            checked={soloActivos}
            onChange={(e) => setSoloActivos(e.target.checked)}
          />
          Solo activos
        </label>
      </div>

      {showForm && (
        <form
          onSubmit={(e) => void save(e)}
          className="card-flat mb-6 grid gap-3 p-5 md:grid-cols-2"
        >
          <h3 className="text-sm font-bold text-auditart-navy md:col-span-2">
            {editing ? 'Editar precio' : 'Nuevo precio'}
          </h3>
          <select
            value={formTipo}
            onChange={(e) => setFormTipo(e.target.value as TipoProfesional)}
            className="input-modern px-3 py-2 text-sm"
          >
            <option value="especialista">Especialista / estudio</option>
            <option value="auditor">Médico auditor</option>
          </select>
          <input
            value={formArt}
            onChange={(e) => setFormArt(e.target.value)}
            placeholder={formTipo === 'auditor' ? 'ART (obligatorio)' : 'ART (no aplica)'}
            disabled={formTipo !== 'auditor'}
            className="input-modern px-3 py-2 text-sm disabled:opacity-50"
          />
          <input
            value={formConcepto}
            onChange={(e) => setFormConcepto(e.target.value)}
            placeholder="Concepto / prestación"
            className="input-modern px-3 py-2 text-sm md:col-span-2"
            required
          />
          <input
            value={formValor}
            onChange={(e) => setFormValor(e.target.value)}
            placeholder="Valor conciliado ($)"
            className="input-modern px-3 py-2 text-sm"
            required
          />
          <input
            value={formNotas}
            onChange={(e) => setFormNotas(e.target.value)}
            placeholder="Notas (opcional)"
            className="input-modern px-3 py-2 text-sm"
          />
          <div className="flex gap-2 md:col-span-2">
            <button type="submit" disabled={busy} className="btn-primary text-sm disabled:opacity-50">
              {busy ? <Loader2 size={14} className="inline animate-spin" /> : 'Guardar'}
            </button>
            <button
              type="button"
              className="rounded-xl border border-gray-200 px-4 py-2 text-sm"
              onClick={() => setShowForm(false)}
            >
              Cancelar
            </button>
          </div>
        </form>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      ) : (
        <div className="card-flat overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                <th className="px-4 py-3">Tipo</th>
                <th className="px-4 py-3">ART</th>
                <th className="px-4 py-3">Concepto</th>
                <th className="px-4 py-3">Valor</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="border-b border-gray-50">
                  <td className="px-4 py-3">
                    {normalizeTipo(item.tipoProfesional) === 'auditor'
                      ? 'Auditor'
                      : 'Especialista'}
                  </td>
                  <td className="px-4 py-3">{item.artNombre || '—'}</td>
                  <td className="px-4 py-3">
                    <div className="font-medium text-auditart-navy">{item.concepto}</div>
                    {item.notas ? (
                      <div className="text-xs text-auditart-muted">{item.notas}</div>
                    ) : null}
                  </td>
                  <td className="px-4 py-3 font-semibold">{formatCurrency(item.valor)}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`text-xs font-bold ${item.isActive ? 'text-emerald-700' : 'text-auditart-muted'}`}
                    >
                      {item.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => openEdit(item)}
                        className="text-auditart-blue hover:underline"
                        title="Editar"
                      >
                        <Pencil size={14} />
                      </button>
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => void toggleActive(item)}
                        className="text-auditart-gray hover:text-auditart-navy"
                        title={item.isActive ? 'Desactivar' : 'Activar'}
                      >
                        {item.isActive ? <ToggleRight size={16} /> : <ToggleLeft size={16} />}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {items.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-auditart-muted">
                    No hay precios. Creá el catálogo o importá conceptos manualmente.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          {totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-gray-100 px-4 py-3 text-xs">
              <span>
                {total} precios · pág. {page}/{totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                  className="rounded-lg border px-2 py-1 disabled:opacity-40"
                >
                  Anterior
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                  className="rounded-lg border px-2 py-1 disabled:opacity-40"
                >
                  Siguiente
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function normalizeTipo(value: string): TipoProfesional {
  return value.toLowerCase() === 'auditor' ? 'auditor' : 'especialista'
}
