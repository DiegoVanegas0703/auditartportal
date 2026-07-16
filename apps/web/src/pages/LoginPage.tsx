import { ArrowRight, Shield } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Logo } from '../components/ui/Logo'
import { useAuth } from '../context/AuthContext'
import { DEFAULT_USER } from '../data/mockUsers'
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
  const { login, availableUsers } = useAuth()
  const navigate = useNavigate()
  const [selectedId, setSelectedId] = useState(DEFAULT_USER.id)

  const handleLogin = () => {
    login(selectedId)
    navigate('/')
  }

  const selected = availableUsers.find((u) => u.id === selectedId)

  return (
    <div className="flex min-h-screen">
      {/* Hero panel */}
      <div className="login-hero relative hidden flex-1 flex-col justify-between p-12 text-white lg:flex">
        <div className="relative z-10">
          <Logo size="lg" variant="light" />
        </div>

        <div className="relative z-10 max-w-lg">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full bg-white/10 px-4 py-1.5 text-xs font-medium backdrop-blur-sm">
            <Shield size={14} />
            Plataforma segura · Cobertura nacional
          </div>
          <h2 className="font-[family-name:var(--font-display)] text-5xl leading-tight">
            Excelencia en
            <br />
            <span className="italic">Auditoría Médica</span>
          </h2>
          <p className="mt-5 text-base leading-relaxed text-white/75">
            Centralizá la gestión de auditorías, reemplazá las planillas de Excel y
            automatizá el triage de solicitudes en un solo portal.
          </p>

          <div className="mt-10 grid grid-cols-4 gap-3">
            {[
              { color: 'bg-red-400', label: 'Rojo', desc: 'Búsqueda' },
              { color: 'bg-yellow-400', label: 'Amarillo', desc: 'Coordinado' },
              { color: 'bg-blue-400', label: 'Azul', desc: 'Doc. pend.' },
              { color: 'bg-green-400', label: 'Verde', desc: 'Facturar' },
            ].map((s) => (
              <div
                key={s.label}
                className="rounded-2xl bg-white/10 p-4 text-center backdrop-blur-sm transition-transform hover:scale-105"
              >
                <div className={`mx-auto mb-2 h-2 w-8 rounded-full ${s.color}`} />
                <p className="text-xs font-bold">{s.label}</p>
                <p className="text-[10px] text-white/60">{s.desc}</p>
              </div>
            ))}
          </div>
        </div>

        <p className="relative z-10 text-xs text-white/40">
          © 2026 AUDITART SAS · Todos los derechos reservados
        </p>
      </div>

      {/* Login form */}
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
              Seleccioná un perfil para explorar el portal con datos simulados.
            </p>
          </div>

          <div className="mb-6 grid grid-cols-2 gap-2.5">
            {availableUsers.map((u) => (
              <button
                key={u.id}
                onClick={() => setSelectedId(u.id)}
                className={`group relative flex flex-col items-start rounded-2xl border-2 p-3.5 text-left transition-all duration-200 ${
                  selectedId === u.id
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
                {u.id === DEFAULT_USER.id && (
                  <span className="mt-2 rounded-full bg-auditart-green/10 px-2 py-0.5 text-[10px] font-bold text-auditart-green">
                    DEFAULT
                  </span>
                )}
              </button>
            ))}
          </div>

          {selected && (
            <div className="mb-5 rounded-2xl bg-auditart-light p-4 ring-1 ring-auditart-blue/10">
              <p className="text-xs font-medium text-auditart-gray">Ingresando como</p>
              <p className="mt-0.5 font-bold text-auditart-navy">{selected.name}</p>
              <p className="text-sm text-auditart-blue">{ROLE_LABELS[selected.role]}</p>
            </div>
          )}

          <button onClick={handleLogin} className="btn-primary flex w-full items-center justify-center gap-2 py-3.5 text-base">
            Ingresar al portal
            <ArrowRight size={18} />
          </button>

          <p className="mt-5 text-center text-xs text-auditart-muted">
            Usuario por defecto: Diego Santamarina · Administrador
          </p>
        </div>
      </div>
    </div>
  )
}
