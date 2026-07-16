import { ArrowRight, Loader2, Shield } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Logo } from '../components/ui/Logo'
import { useAuth } from '../context/AuthContext'
import { ROLE_LABELS } from '../types'
import { getInitials } from '../utils/format'

const roleColors: Record<string, string> = {
  admin: 'from-purple-500 to-indigo-600',
  jefatura: 'from-blue-500 to-cyan-600',
  operador: 'from-auditart-blue to-auditart-blue-dark',
  telemedicina: 'from-teal-500 to-emerald-600',
  cronicos: 'from-amber-500 to-orange-600',
  facturacion: 'from-green-500 to-emerald-600',
}

export function LoginPage() {
  const { loginDev, availableDevUsers } = useAuth()
  const navigate = useNavigate()
  const [selectedEmail, setSelectedEmail] = useState(availableDevUsers[0]?.email ?? '')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const selected = availableDevUsers.find((u) => u.email === selectedEmail)

  const handleLogin = async () => {
    if (!selectedEmail) return
    setLoading(true)
    setError(null)
    try {
      await loginDev(selectedEmail)
      navigate('/')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo iniciar sesión')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen">
      <div className="login-hero relative hidden flex-1 flex-col justify-between p-12 text-white lg:flex">
        <div className="relative z-10">
          <Logo size="lg" variant="light" />
        </div>

        <div className="relative z-10 max-w-lg">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full bg-white/10 px-4 py-1.5 text-xs font-medium backdrop-blur-sm">
            <Shield size={14} />
            API conectada · Auth JWT + Google (Dev)
          </div>
          <h2 className="font-[family-name:var(--font-display)] text-5xl leading-tight">
            Excelencia en
            <br />
            <span className="italic">Auditoría Médica</span>
          </h2>
          <p className="mt-5 text-base leading-relaxed text-white/75">
            Login de desarrollo contra la API. En producción se usará Google OAuth
            real con las cuentas del equipo.
          </p>
        </div>

        <p className="relative z-10 text-xs text-white/40">
          © 2026 AUDITART SAS · Todos los derechos reservados
        </p>
      </div>

      <div className="flex flex-1 flex-col items-center justify-center bg-white p-6 sm:p-10">
        <div className="w-full max-w-[420px] animate-fade-in">
          <div className="mb-8 lg:hidden">
            <Logo size="md" />
          </div>

          <div className="mb-8">
            <h1 className="text-3xl font-bold tracking-tight text-auditart-navy">
              Iniciar sesión
            </h1>
            <p className="mt-2 text-sm text-auditart-gray">
              Seleccioná un usuario seed de la API (token Dev).
            </p>
          </div>

          <div className="mb-6 grid grid-cols-2 gap-2.5">
            {availableDevUsers.map((u) => (
              <button
                key={u.email}
                type="button"
                onClick={() => setSelectedEmail(u.email)}
                className={`group relative flex flex-col items-start rounded-2xl border-2 p-3.5 text-left transition-all duration-200 ${
                  selectedEmail === u.email
                    ? 'border-auditart-blue bg-auditart-blue/4 shadow-md shadow-auditart-blue/10'
                    : 'border-gray-100 bg-gray-50/50 hover:border-auditart-blue/30 hover:bg-white hover:shadow-sm'
                }`}
              >
                <div className="flex w-full items-center gap-2.5">
                  <div
                    className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-xs font-bold text-white shadow-sm ${
                      roleColors[u.role] ?? 'from-gray-400 to-gray-500'
                    }`}
                  >
                    {getInitials(u.name)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-semibold text-auditart-navy">
                      {u.name.split(' ').slice(0, 2).join(' ')}
                    </p>
                    <p className="truncate text-[11px] text-auditart-gray">
                      {ROLE_LABELS[u.role]}
                    </p>
                  </div>
                </div>
              </button>
            ))}
          </div>

          {selected && (
            <div className="mb-5 rounded-2xl bg-auditart-light p-4 ring-1 ring-auditart-blue/10">
              <p className="text-xs font-medium text-auditart-gray">Ingresando como</p>
              <p className="mt-0.5 font-bold text-auditart-navy">{selected.name}</p>
              <p className="text-sm text-auditart-blue">{selected.email}</p>
            </div>
          )}

          {error && (
            <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700 ring-1 ring-red-100">
              {error}
            </div>
          )}

          <button
            type="button"
            onClick={() => void handleLogin()}
            disabled={loading}
            className="btn-primary flex w-full items-center justify-center gap-2 py-3.5 text-base disabled:opacity-60"
          >
            {loading ? (
              <>
                <Loader2 size={18} className="animate-spin" />
                Conectando…
              </>
            ) : (
              <>
                Ingresar al portal
                <ArrowRight size={18} />
              </>
            )}
          </button>

          <p className="mt-5 text-center text-xs text-auditart-muted">
            API: http://localhost:5070 · Modo Dev (sin Google Console)
          </p>
        </div>
      </div>
    </div>
  )
}
