import { Loader2, Pencil, Plus, Save, Search, X } from 'lucide-react'
import { Fragment, useCallback, useEffect, useMemo, useState } from 'react'
import { artsApi, type ArtDto } from '../api/auditartApi'
import { loadArts } from '../components/arts/ArtSelect'
import { PageHeader } from '../components/ui/PageHeader'

type Draft = { nombre: string; cuit: string; isActive: boolean }

const EMPTY: Draft = { nombre: '', cuit: '', isActive: true }

export function ArtsPage() {
  const [arts, setArts] = useState<ArtDto[]>([])
  const [loading, setLoading] = useState(true)
  const [query, setQuery] = useState('')
  const [editingId, setEditingId] = useState<string | 'new' | null>(null)
  const [draft, setDraft] = useState<Draft>(EMPTY)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setArts(await artsApi.list(true))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar las ARTs')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    const digits = query.replace(/\D/g, '')
    if (!q) return arts
    return arts.filter(
      (a) =>
        a.nombre.toLowerCase().includes(q) ||
        (digits.length > 0 && a.cuit.replace(/\D/g, '').includes(digits)),
    )
  }, [arts, query])

  const startEdit = (art: ArtDto | null) => {
    setError(null)
    setEditingId(art?.id ?? 'new')
    setDraft(art ? { nombre: art.nombre, cuit: art.cuit, isActive: art.isActive } : EMPTY)
  }

  const save = async () => {
    if (!draft.nombre.trim()) {
      setError('El nombre es obligatorio.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      if (editingId === 'new') {
        await artsApi.create({ nombre: draft.nombre, cuit: draft.cuit })
      } else if (editingId) {
        await artsApi.update(editingId, draft)
      }
      setEditingId(null)
      await load()
      void loadArts(true)
      setToast('ART guardada')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la ART')
    } finally {
      setBusy(false)
    }
  }

  const editRow = (
    <tr className="bg-auditart-blue/5">
      <td className="px-4 py-2">
        <input
          autoFocus
          value={draft.nombre}
          onChange={(e) => setDraft({ ...draft, nombre: e.target.value })}
          placeholder="Nombre"
          className="input-modern w-full px-3 py-1.5 text-sm"
        />
      </td>
      <td className="px-4 py-2">
        <input
          value={draft.cuit}
          onChange={(e) => setDraft({ ...draft, cuit: e.target.value })}
          placeholder="30-12345678-9"
          className="input-modern w-full px-3 py-1.5 text-sm"
        />
      </td>
      <td className="px-4 py-2">
        <label className="inline-flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            checked={draft.isActive}
            disabled={editingId === 'new'}
            onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })}
          />
          Activa
        </label>
      </td>
      <td className="px-4 py-2 text-right">
        <div className="flex justify-end gap-1">
          <button
            type="button"
            onClick={() => void save()}
            disabled={busy}
            className="btn-primary flex items-center gap-1 px-3 py-1.5 text-xs disabled:opacity-50"
          >
            {busy ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
            Guardar
          </button>
          <button
            type="button"
            onClick={() => setEditingId(null)}
            className="rounded-lg p-1.5 text-auditart-muted hover:bg-gray-100"
            title="Cancelar"
          >
            <X size={16} />
          </button>
        </div>
      </td>
    </tr>
  )

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="ARTs"
        subtitle="Catálogo de aseguradoras · se usa en todos los desplegables de ART"
        action={
          <button
            type="button"
            onClick={() => startEdit(null)}
            disabled={editingId !== null}
            className="btn-primary flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50"
          >
            <Plus size={16} />
            Nueva ART
          </button>
        }
      />

      {toast && (
        <div className="mb-4 rounded-xl bg-green-50 px-4 py-3 text-sm font-semibold text-green-700">{toast}</div>
      )}
      {error && <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <div className="card-flat overflow-x-auto">
        <div className="flex items-center gap-2 border-b border-gray-100 px-4 py-3">
          <Search size={16} className="text-auditart-muted" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Buscar por nombre o CUIT…"
            className="w-full bg-transparent text-sm outline-none"
          />
          <span className="shrink-0 text-xs text-auditart-muted">{filtered.length} ART(s)</span>
        </div>
        {loading ? (
          <div className="flex items-center gap-2 p-4 text-sm text-auditart-gray">
            <Loader2 size={16} className="animate-spin" /> Cargando…
          </div>
        ) : (
          <table className="w-full min-w-[640px] text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                <th className="px-4 py-3">Nombre</th>
                <th className="px-4 py-3">CUIT</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {editingId === 'new' && editRow}
              {filtered.map((art) =>
                editingId === art.id ? (
                  <Fragment key={art.id}>{editRow}</Fragment>
                ) : (
                  <tr key={art.id} className={art.isActive ? '' : 'opacity-50'}>
                    <td className="px-4 py-3 font-semibold text-auditart-navy">{art.nombre}</td>
                    <td className="px-4 py-3 font-mono text-xs text-auditart-gray">{art.cuit || '—'}</td>
                    <td className="px-4 py-3 text-xs">
                      {art.isActive ? (
                        <span className="rounded-full bg-emerald-50 px-2 py-0.5 font-semibold text-emerald-700">
                          Activa
                        </span>
                      ) : (
                        <span className="rounded-full bg-gray-100 px-2 py-0.5 font-semibold text-auditart-muted">
                          Inactiva
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        type="button"
                        onClick={() => startEdit(art)}
                        disabled={editingId !== null}
                        className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-auditart-blue hover:bg-auditart-blue/8 disabled:opacity-40"
                      >
                        <Pencil size={13} />
                        Editar
                      </button>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
