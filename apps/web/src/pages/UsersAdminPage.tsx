import { Loader2, UserMinus, UserPlus } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { mapListedUser, usersApi } from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'
import { ROLE_LABELS, type AuditQueue, type User, type UserRole } from '../types'

export function UsersAdminPage() {
  const [users, setUsers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<UserRole>('operador')
  const [queue, setQueue] = useState<AuditQueue>('general')

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const list = await usersApi.list()
      setUsers(list.map(mapListedUser))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los usuarios')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await usersApi.create({
        name: name.trim(),
        email: email.trim(),
        role,
        defaultQueue: role === 'operador' || role === 'telemedicina' || role === 'cronicos' ? queue : null,
      })
      setName('')
      setEmail('')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el usuario')
    } finally {
      setBusy(false)
    }
  }

  const handleDeactivate = async (id: string) => {
    if (!confirm('¿Desactivar este usuario?')) return
    setBusy(true)
    try {
      await usersApi.deactivate(id)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo desactivar')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Administración de usuarios"
        subtitle="Crear y desactivar cuentas (solo Admin)"
      />

      {error && (
        <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="card-flat p-6">
          <h2 className="mb-4 flex items-center gap-2 text-sm font-bold text-auditart-navy">
            <UserPlus size={16} /> Nuevo usuario
          </h2>
          <form onSubmit={(e) => void handleCreate(e)} className="space-y-3">
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Nombre completo"
              required
              className="input-modern w-full px-3 py-2 text-sm"
            />
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="email@auditart.local"
              required
              className="input-modern w-full px-3 py-2 text-sm"
            />
            <select
              value={role}
              onChange={(e) => setRole(e.target.value as UserRole)}
              className="input-modern w-full px-3 py-2 text-sm"
            >
              {Object.entries(ROLE_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
            {(role === 'operador' || role === 'telemedicina' || role === 'cronicos') && (
              <select
                value={queue}
                onChange={(e) => setQueue(e.target.value as AuditQueue)}
                className="input-modern w-full px-3 py-2 text-sm"
              >
                <option value="general">General</option>
                <option value="telemedicina">Telemedicina</option>
                <option value="cronicos">Crónicos</option>
              </select>
            )}
            <button
              type="submit"
              disabled={busy}
              className="btn-primary flex items-center gap-2 text-sm disabled:opacity-50"
            >
              {busy ? <Loader2 size={14} className="animate-spin" /> : 'Crear con contraseña temporal'}
            </button>
            <p className="text-xs text-auditart-muted">
              Se genera contraseña temporal y cambio obligatorio al primer ingreso.
            </p>
          </form>
        </div>

        <div className="card-flat p-6">
          <h2 className="mb-4 text-sm font-bold text-auditart-navy">Usuarios activos</h2>
          {loading ? (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando…
            </div>
          ) : (
            <ul className="divide-y divide-gray-100">
              {users.map((u) => (
                <li key={u.id} className="flex items-center justify-between py-3">
                  <div>
                    <p className="font-semibold text-auditart-navy">{u.name}</p>
                    <p className="text-xs text-auditart-muted">
                      {u.email} · {ROLE_LABELS[u.role]}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => void handleDeactivate(u.id)}
                    disabled={busy}
                    className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-red-600 hover:bg-red-50 disabled:opacity-40"
                  >
                    <UserMinus size={14} /> Desactivar
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  )
}
