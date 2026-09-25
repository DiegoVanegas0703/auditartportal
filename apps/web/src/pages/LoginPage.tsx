import { ArrowRight, Loader2, Lock, Shield } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Logo } from '../components/ui/Logo'
import { useAuth } from '../context/useAuth'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('admin@auditart.local')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const mustChange = await login(email.trim(), password)
      navigate(mustChange ? '/cambiar-contrasena' : '/')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo iniciar sesión')
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
            Acceso seguro · Email y contraseña
          </div>
          <h2 className="font-[family-name:var(--font-display)] text-5xl leading-tight">
            Excelencia en
            <br />
            <span className="italic">Auditoría Médica</span>
          </h2>
          <p className="mt-5 text-base leading-relaxed text-white/75">
            Ingresá con tu cuenta de Auditart. El portal valida credenciales en el
            backend y aplica los permisos de tu rol.
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
              Usá tu email y contraseña asignados por el administrador.
            </p>
          </div>

          {error && (
            <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700 ring-1 ring-red-100">
              {error}
            </div>
          )}

          <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">Email</span>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoComplete="username"
                className="input-modern w-full px-4 py-2.5 text-sm"
                placeholder="usuario@auditart.local"
              />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">
                Contraseña
              </span>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="current-password"
                className="input-modern w-full px-4 py-2.5 text-sm"
                placeholder="••••••••"
              />
            </label>
            <button
              type="submit"
              disabled={loading}
              className="btn-primary flex w-full items-center justify-center gap-2 py-3 text-sm disabled:opacity-60"
            >
              {loading ? (
                <>
                  <Loader2 size={18} className="animate-spin" />
                  Validando…
                </>
              ) : (
                <>
                  Ingresar
                  <ArrowRight size={16} />
                </>
              )}
            </button>
          </form>

          <p className="mt-5 flex items-center justify-center gap-1.5 text-center text-xs text-auditart-muted">
            <Lock size={12} />
            Contraseña inicial de prueba: <strong>Test</strong> (cambio obligatorio)
          </p>
        </div>
      </div>
    </div>
  )
}
