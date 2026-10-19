import { ExternalLink, Loader2, X } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { pacientesApi, type PacienteLookupDto } from '../../api/auditartApi'
import { ArtSelect } from '../arts/ArtSelect'
import { DniLookupField } from './DniLookupField'

export function NuevoPacienteModal({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const [nombre, setNombre] = useState('')
  const [dni, setDni] = useState('')
  const [art, setArt] = useState('')
  const [numeroSiniestro, setNumeroSiniestro] = useState('')
  const [telefono, setTelefono] = useState('')
  const [email, setEmail] = useState('')
  const [domicilio, setDomicilio] = useState('')
  const [localidad, setLocalidad] = useState('')
  const [provincia, setProvincia] = useState('')
  const [existente, setExistente] = useState<PacienteLookupDto | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const applyLookup = (p: PacienteLookupDto) => {
    setExistente(p)
    setNombre(p.nombre)
    setArt(p.art ?? '')
    setNumeroSiniestro(p.numeroSiniestro ?? '')
    setTelefono(p.telefono ?? '')
    setEmail(p.email ?? '')
    setDomicilio(p.domicilio ?? '')
    setLocalidad(p.localidad ?? '')
    setProvincia(p.provincia ?? '')
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!nombre.trim() || !dni.trim()) {
      setError('Nombre y DNI son obligatorios.')
      return
    }
    if (!domicilio.trim() || !localidad.trim() || !provincia.trim()) {
      setError('Domicilio, localidad y provincia son obligatorios.')
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
        domicilio: domicilio.trim(),
        localidad: localidad.trim(),
        provincia: provincia.trim(),
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
              Cargá el DNI y tocá “Buscar” para traer los datos si el paciente ya existe.
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
            DNI *
            <div className="mt-1">
              <DniLookupField value={dni} onChange={setDni} onFound={applyLookup} required />
            </div>
          </label>
          {existente && (
            <div className="flex items-center justify-between gap-2 rounded-xl bg-auditart-blue/5 px-3 py-2 text-xs text-auditart-navy sm:col-span-2">
              <span>Este paciente ya está registrado.</span>
              <Link
                to={`/pacientes/${existente.id}`}
                className="inline-flex items-center gap-1 font-semibold text-auditart-blue hover:underline"
              >
                Abrir ficha <ExternalLink size={12} />
              </Link>
            </div>
          )}
          <label className="text-xs font-semibold text-auditart-gray sm:col-span-2">
            Nombre y apellido *
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <div className="text-xs font-semibold text-auditart-gray">
            ART
            <ArtSelect value={art} onChange={setArt} className="mt-1" />
          </div>
          <label className="text-xs font-semibold text-auditart-gray">
            Nº de siniestro
            <input
              value={numeroSiniestro}
              onChange={(e) => setNumeroSiniestro(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray sm:col-span-2">
            Domicilio (calle y número) *
            <input
              value={domicilio}
              onChange={(e) => setDomicilio(e.target.value)}
              placeholder="Calle y número"
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
              required
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            Localidad *
            <input
              value={localidad}
              onChange={(e) => setLocalidad(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
              required
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            Provincia *
            <input
              value={provincia}
              onChange={(e) => setProvincia(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
              required
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
          <label className="text-xs font-semibold text-auditart-gray">
            Email (opcional)
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
