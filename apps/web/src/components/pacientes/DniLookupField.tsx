import { Loader2, UserSearch } from 'lucide-react'
import { useState } from 'react'
import { pacientesApi, type PacienteLookupDto } from '../../api/auditartApi'

/** Campo DNI con botón para traer los datos de un paciente ya registrado. */
export function DniLookupField({
  value,
  onChange,
  onFound,
  inputClassName = 'input-modern w-full px-3 py-2 text-sm',
  required,
}: {
  value: string
  onChange: (dni: string) => void
  onFound: (paciente: PacienteLookupDto) => void
  inputClassName?: string
  required?: boolean
}) {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  const lookup = async () => {
    if (value.replace(/\D/g, '').length < 6) {
      setMessage({ ok: false, text: 'Ingresá un DNI válido para buscar.' })
      return
    }
    setBusy(true)
    setMessage(null)
    try {
      const matches = await pacientesApi.findByDni(value)
      if (matches.length === 0) {
        setMessage({ ok: false, text: 'No hay pacientes registrados con ese DNI.' })
        return
      }
      onFound(matches[0])
      setMessage({
        ok: true,
        text:
          matches.length > 1
            ? `Datos cargados de ${matches[0].nombre} (hay ${matches.length} pacientes con ese DNI).`
            : `Datos cargados de ${matches[0].nombre}.`,
      })
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error ? e.message : 'No se pudo buscar el DNI' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div>
      <div className="flex gap-2">
        <input
          value={value}
          onChange={(e) => {
            onChange(e.target.value)
            setMessage(null)
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              void lookup()
            }
          }}
          required={required}
          className={inputClassName}
        />
        <button
          type="button"
          onClick={() => void lookup()}
          disabled={busy || !value.trim()}
          title="Buscar paciente por DNI y traer sus datos"
          className="inline-flex shrink-0 items-center gap-1.5 rounded-xl bg-auditart-blue/10 px-3 text-xs font-semibold text-auditart-blue hover:bg-auditart-blue/15 disabled:opacity-50"
        >
          {busy ? <Loader2 size={14} className="animate-spin" /> : <UserSearch size={14} />}
          Buscar
        </button>
      </div>
      {message && (
        <p className={`mt-1 text-[11px] font-normal ${message.ok ? 'text-emerald-700' : 'text-amber-700'}`}>
          {message.text}
        </p>
      )}
    </div>
  )
}
