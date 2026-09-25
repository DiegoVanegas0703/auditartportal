import { ArrowLeft, Loader2, Pencil, Plus } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { pacientesApi, type PacienteDto } from '../api/auditartApi'
import { useAudits } from '../context/useAudits'
import { PageHeader } from '../components/ui/PageHeader'
import { StatusBadge } from '../components/ui/StatusBadge'
import {
  CHRONIC_PERIODICITY_LABELS,
  QUEUE_LABELS,
  SERVICE_LABELS,
  type AuditQueue,
  type AuditStatus,
  type ChronicPeriodicity,
  type ServiceType,
} from '../types'
import { formatCurrency, formatDateTime } from '../utils/format'

const SERVICE_OPTIONS: ServiceType[] = [
  'consultorio',
  'terreno',
  'domicilio',
  'telemedicina',
  'comision_medica',
  'valoracion_dano',
  'otra_auditoria',
]

export function PacientePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { refresh } = useAudits()
  const [data, setData] = useState<PacienteDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [editingPaciente, setEditingPaciente] = useState(false)
  const [busy, setBusy] = useState(false)
  const [editBusy, setEditBusy] = useState(false)
  const [tipo, setTipo] = useState<ServiceType>('consultorio')
  const [especialidad, setEspecialidad] = useState('')
  const [notas, setNotas] = useState('')
  const [queue, setQueue] = useState<AuditQueue>('general')
  const [tardia, setTardia] = useState(false)
  const [periodicity, setPeriodicity] = useState<ChronicPeriodicity>('monthly')
  const [intervalDays, setIntervalDays] = useState('30')
  const [scheduleStart, setScheduleStart] = useState('')
  const [editNombre, setEditNombre] = useState('')
  const [editDni, setEditDni] = useState('')
  const [editTelefono, setEditTelefono] = useState('')
  const [editEmail, setEditEmail] = useState('')
  const [editArt, setEditArt] = useState('')
  const [editSiniestro, setEditSiniestro] = useState('')

  const load = async (pacienteId: string) => {
    setLoading(true)
    setError(null)
    try {
      const dto = await pacientesApi.get(pacienteId)
      setData(dto)
      setEditNombre(dto.nombre)
      setEditDni(dto.dni ?? '')
      setEditTelefono(dto.telefono ?? '')
      setEditEmail(dto.email ?? '')
      setEditArt(dto.art ?? '')
      setEditSiniestro(dto.numeroSiniestro ?? '')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo cargar el paciente')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (id) void load(id)
  }, [id])

  const savePaciente = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!id) return
    if (!editNombre.trim()) {
      setError('El nombre del paciente es obligatorio.')
      return
    }
    setEditBusy(true)
    setError(null)
    try {
      const updated = await pacientesApi.update(id, {
        nombre: editNombre.trim(),
        dni: editDni.trim() || undefined,
        telefono: editTelefono.trim() || undefined,
        email: editEmail.trim() || undefined,
        art: editArt.trim() || undefined,
        numeroSiniestro: editSiniestro.trim() || undefined,
      })
      setData(updated)
      setEditingPaciente(false)
      setToast('Datos del paciente actualizados')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar el paciente')
    } finally {
      setEditBusy(false)
    }
  }

  const createPrestacion = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!id) return
    setError(null)

    if (tardia && periodicity === 'every_x_days') {
      const days = Number(intervalDays)
      if (!intervalDays.trim() || Number.isNaN(days) || days < 1) {
        setError('Para coordinación tardía “cada X días”, indicá un intervalo válido.')
        return
      }
    }

    setBusy(true)
    try {
      const created = await pacientesApi.createPrestacion(id, {
        tipoServicio: tipo,
        especialidad: especialidad.trim() || undefined,
        notas: notas.trim() || undefined,
        queue: tardia ? 'cronicos' : queue,
        coordinacionTardia: tardia,
        periodicity: tardia ? periodicity : undefined,
        intervalDays:
          tardia && periodicity === 'every_x_days' ? Number(intervalDays) : undefined,
        scheduleStartUtc:
          tardia && scheduleStart ? new Date(scheduleStart).toISOString() : undefined,
      })
      await refresh()
      const nextId = created.id ?? (created as { Id?: string }).Id
      if (!nextId) throw new Error('La prestación se creó pero no se recibió el ID.')
      navigate(`/servicio/${nextId}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear la prestación')
      setBusy(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <Link
        to="/tablero"
        className="mb-4 inline-flex items-center gap-1.5 text-sm font-semibold text-auditart-blue hover:underline"
      >
        <ArrowLeft size={14} /> Volver al tablero
      </Link>
      <PageHeader
        title={data?.nombre ?? 'Paciente'}
        subtitle={
          data
            ? `DNI ${data.dni ?? '—'} · ${data.art ?? 'ART'} · ${data.prestacionesAbiertas} abierta(s)`
            : 'Ficha del paciente y sus prestaciones'
        }
        action={
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => {
                if (data) {
                  setEditNombre(data.nombre)
                  setEditDni(data.dni ?? '')
                  setEditTelefono(data.telefono ?? '')
                  setEditEmail(data.email ?? '')
                  setEditArt(data.art ?? '')
                  setEditSiniestro(data.numeroSiniestro ?? '')
                }
                setEditingPaciente((v) => !v)
              }}
              className="inline-flex items-center gap-2 rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-auditart-navy hover:bg-gray-50"
            >
              <Pencil size={14} /> {editingPaciente ? 'Cancelar edición' : 'Editar datos'}
            </button>
            <button
              type="button"
              onClick={() => setShowForm((v) => !v)}
              className="btn-primary inline-flex items-center gap-2 text-sm"
            >
              <Plus size={14} /> Nueva prestación
            </button>
          </div>
        }
      />

      {toast && (
        <div className="mb-4 rounded-xl bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          {toast}
          <button type="button" className="ml-3 underline" onClick={() => setToast('')}>
            ok
          </button>
        </div>
      )}
      {error && (
        <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {editingPaciente && data ? (
        <form
          onSubmit={(e) => void savePaciente(e)}
          className="card-flat mb-4 grid gap-3 p-4 md:grid-cols-2"
        >
          <p className="text-sm text-auditart-gray md:col-span-2">
            Podés corregir o actualizar los datos del paciente en cualquier momento. Se
            propagan a las prestaciones abiertas.
          </p>
          <label className="text-xs font-semibold text-auditart-gray">
            Nombre *
            <input
              value={editNombre}
              onChange={(e) => setEditNombre(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
              required
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            DNI
            <input
              value={editDni}
              onChange={(e) => setEditDni(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            Teléfono
            <input
              value={editTelefono}
              onChange={(e) => setEditTelefono(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            Email
            <input
              type="email"
              value={editEmail}
              onChange={(e) => setEditEmail(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            ART
            <input
              value={editArt}
              onChange={(e) => setEditArt(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-semibold text-auditart-gray">
            N° siniestro
            <input
              value={editSiniestro}
              onChange={(e) => setEditSiniestro(e.target.value)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            />
          </label>
          <button
            type="submit"
            disabled={editBusy}
            className="btn-primary text-sm disabled:opacity-50 md:col-span-2"
          >
            {editBusy ? (
              <Loader2 size={14} className="inline animate-spin" />
            ) : (
              'Guardar datos del paciente'
            )}
          </button>
        </form>
      ) : data ? (
        <div className="card-flat mb-4 grid gap-3 p-4 sm:grid-cols-3">
          <Field label="Teléfono" value={data.telefono} />
          <Field label="Email" value={data.email} />
          <Field label="Siniestro" value={data.numeroSiniestro} />
          <Field label="DNI" value={data.dni} />
          <Field label="ART" value={data.art} />
          <Field label="Nombre" value={data.nombre} />
        </div>
      ) : null}

      {showForm && (
        <form
          onSubmit={(e) => void createPrestacion(e)}
          className="card-flat mb-4 grid gap-3 p-4 md:grid-cols-2"
        >
          <p className="text-sm text-auditart-gray md:col-span-2">
            Los datos del paciente ya están en la ficha. Completá solo lo de esta prestación.
          </p>
          <label className="text-xs font-semibold text-auditart-gray">
            Tipo
            <select
              value={tipo}
              onChange={(e) => setTipo(e.target.value as ServiceType)}
              className="input-modern mt-1 w-full px-3 py-2 text-sm"
            >
              {SERVICE_OPTIONS.map((opt) => (
                <option key={opt} value={opt}>
                  {SERVICE_LABELS[opt]}
                </option>
              ))}
            </select>
          </label>
          {!tardia ? (
            <label className="text-xs font-semibold text-auditart-gray">
              Cola
              <select
                value={queue}
                onChange={(e) => setQueue(e.target.value as AuditQueue)}
                className="input-modern mt-1 w-full px-3 py-2 text-sm"
              >
                {(Object.keys(QUEUE_LABELS) as AuditQueue[]).map((q) => (
                  <option key={q} value={q}>
                    {QUEUE_LABELS[q]}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <div className="text-xs font-semibold text-auditart-gray">
              Cola
              <p className="input-modern mt-1 px-3 py-2 text-sm text-auditart-navy">
                Crónicos (coordinación tardía)
              </p>
            </div>
          )}
          <input
            value={especialidad}
            onChange={(e) => setEspecialidad(e.target.value)}
            placeholder="Especialidad (ej. Traumatología, Cirugía)"
            className="input-modern px-3 py-2 text-sm md:col-span-2"
          />
          <textarea
            value={notas}
            onChange={(e) => setNotas(e.target.value)}
            placeholder="Notas de la prestación"
            rows={3}
            className="input-modern px-3 py-2 text-sm md:col-span-2"
          />
          <label className="flex items-center gap-2 text-sm text-auditart-navy md:col-span-2">
            <input
              type="checkbox"
              checked={tardia}
              onChange={(e) => {
                const on = e.target.checked
                setTardia(on)
                if (on) setQueue('cronicos')
              }}
            />
            Coordinación tardía (periódica; vuelve a Rojo al vencer el período)
          </label>
          {tardia && (
            <>
              <label className="text-xs font-semibold text-auditart-gray">
                Periodicidad *
                <select
                  value={periodicity}
                  onChange={(e) => setPeriodicity(e.target.value as ChronicPeriodicity)}
                  className="input-modern mt-1 w-full px-3 py-2 text-sm"
                  required
                >
                  {Object.entries(CHRONIC_PERIODICITY_LABELS).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
              {periodicity === 'every_x_days' ? (
                <label className="text-xs font-semibold text-auditart-gray">
                  Cada X días *
                  <input
                    type="number"
                    min={1}
                    value={intervalDays}
                    onChange={(e) => setIntervalDays(e.target.value)}
                    className="input-modern mt-1 w-full px-3 py-2 text-sm"
                    required
                  />
                </label>
              ) : (
                <div />
              )}
              <label className="text-xs font-semibold text-auditart-gray md:col-span-2">
                Primera renovación (opcional)
                <input
                  type="datetime-local"
                  value={scheduleStart}
                  onChange={(e) => setScheduleStart(e.target.value)}
                  className="input-modern mt-1 w-full px-3 py-2 text-sm"
                />
              </label>
            </>
          )}
          <button type="submit" disabled={busy} className="btn-primary text-sm disabled:opacity-50">
            {busy ? <Loader2 size={14} className="inline animate-spin" /> : 'Crear prestación en Rojo'}
          </button>
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
                <th className="px-4 py-3">N°</th>
                <th className="px-4 py-3">Tipo</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3">Profesional</th>
                <th className="px-4 py-3">Valor</th>
                <th className="px-4 py-3">Ingreso</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {(data?.prestaciones ?? []).map((p) => (
                <tr key={p.id} className="border-b border-gray-50">
                  <td className="px-4 py-3 font-mono text-xs font-bold">#{p.numero}</td>
                  <td className="px-4 py-3">
                    {SERVICE_LABELS[normalizeType(p.tipoServicio)]}
                    {p.coordinacionTardia ? (
                      <span className="ml-2 text-[10px] font-bold text-amber-700">Tardía</span>
                    ) : null}
                    <div className="text-xs text-auditart-muted">{p.especialidad || '—'}</div>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={normalizeStatus(p.status)} compact />
                  </td>
                  <td className="px-4 py-3">{p.profesional || '—'}</td>
                  <td className="px-4 py-3">{formatCurrency(p.valorPactado ?? undefined)}</td>
                  <td className="px-4 py-3 text-xs">{formatDateTime(p.fechaIngresoUtc)}</td>
                  <td className="px-4 py-3">
                    <Link
                      to={`/servicio/${p.id}`}
                      className="text-xs font-semibold text-auditart-blue hover:underline"
                    >
                      Abrir
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function Field({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">{label}</p>
      <p className="text-sm text-auditart-navy">{value?.trim() ? value : '—'}</p>
    </div>
  )
}

function normalizeType(value: string): ServiceType {
  const key = value.toLowerCase().replace(/_/g, '')
  if (key === 'comisionmedica') return 'comision_medica'
  if (key === 'valoraciondano') return 'valoracion_dano'
  if (key === 'otraauditoria') return 'otra_auditoria'
  return (value.toLowerCase() as ServiceType) || 'consultorio'
}

function normalizeStatus(value: string): AuditStatus {
  const key = value.toLowerCase().replace(/_/g, '')
  if (key === 'cuentapagoanticipado') return 'celeste'
  return value.toLowerCase() as AuditStatus
}
