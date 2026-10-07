import { Loader2, X } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { pacientesApi } from '../../api/auditartApi'

export function NuevoPacienteModal({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const [nombre, setNombre] = useState('')
  const [dni, setDni] = useState('')
  const [art, setArt] = useState('')
  const [numeroSiniestro, setNumeroSiniestro] = useState('')
  const [telefono, setTelefono] = useState('')
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!nombre.trim() || !dni.trim()) {
      setError('Nombre y DNI son obligatorios.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const result = await pacientesApi.create({
        nombre: nombre.trim(),
        dni: dni.trim(),
        art: art.trim() || undefined,
        numeroSiniestro: numeroSiniestro.trim() || undefined,
        telefono: telefono.trim() || undefined,
        email: email.trim() || undefined,
      })
      navigate(`/pacientes/${result.paciente.id}`, {
        state: result.created ? { pacienteNuevo: true } : { pacienteExistente: true },
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el paciente')
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <form
        onSubmit={(e) => void submit(e)}
        className="flex max-h-[90vh] w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
      >
        <div className="flex items-start justify-between border-b border-gray-100 px-5 py-4">
          <div>
            <h3 className="text-lg font-bold text-auditart-navy">Nuevo paciente</h3>
            <p className="text-xs text-auditart-muted">
              Alta manual. Si ya existe un paciente con el mismo nombre y DNI, se abre su ficha.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="rounded-lg p-1.5 text-auditart-muted hover:bg-gray-50 hover:text-auditart-navy disabled:opacity-50"
          >
            <X size={18} />
          </button>
        </div>

        <div className="grid gap-3 overflow-y-auto px-5 py-4 sm:grid-cols-2">
          {error && (
            <div className="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700 sm:col-span-2">
              {error}
            </div>
          )}
          <label className="text-xs font-semibold text-auditart-gray sm:col-span-2">
            Nombre y apellido *
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              autoFocus
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            DNI *
            <input
              value={dni}
              onChange={(e) => setDni(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            ART
            <input
              value={art}
              onChange={(e) => setArt(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            Nº de siniestro
            <input
              value={numeroSiniestro}
              onChange={(e) => setNumeroSiniestro(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            Teléfono
            <input
              value={telefono}
              onChange={(e) => setTelefono(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray sm:col-span-2">
            Email
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-3">
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="rounded-xl border border-gray-200 px-4 py-2 text-sm font-semibold text-auditart-navy hover:bg-gray-50 disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={busy}
            className="btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50"
          >
            {busy && <Loader2 size={14} className="animate-spin" />}
            Crear paciente
          </button>
        </div>
      </form>
    </div>
  )
}
