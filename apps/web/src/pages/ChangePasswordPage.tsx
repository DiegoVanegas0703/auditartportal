import { CheckCircle2, Loader2, Lock } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { authApi } from '../api/auditartApi'
import { Logo } from '../components/ui/Logo'
import { useAuth } from '../context/useAuth'

export function ChangePasswordPage() {
  const { user, completePasswordChange } = useAuth()
  const navigate = useNavigate()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (newPassword !== confirmPassword) {
      setError('Las contraseñas nuevas no coinciden.')
      return
    }
    if (newPassword.length < 4) {
      setError('La contraseña debe tener al menos 4 caracteres.')
      return
    }

    setLoading(true)
    setError(null)
    try {
      await authApi.changePassword(currentPassword, newPassword)
      completePasswordChange()
      setSuccess(true)
      setTimeout(() => navigate('/'), 1500)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cambiar la contraseña')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 p-6">
      <div className="w-full max-w-md animate-fade-in rounded-3xl bg-white p-8 shadow-xl ring-1 ring-gray-100">
        <div className="mb-6 flex justify-center">
          <Logo size="sm" />
        </div>
        <div className="mb-6 text-center">
          <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-2xl bg-auditart-blue/10 text-auditart-blue">
            <Lock size={22} />
          </div>
          <h1 className="text-2xl font-bold text-auditart-navy">Cambiar contraseña</h1>
          <p className="mt-2 text-sm text-auditart-gray">
            {user?.name}, debés establecer una nueva contraseña antes de continuar.
          </p>
        </div>

        {success && (
          <div className="mb-4 flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-700">
            <CheckCircle2 size={16} />
            Contraseña actualizada. Redirigiendo…
          </div>
        )}
        {error && (
          <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
        )}

        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
          <label className="block">
            <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">
              Contraseña actual
            </span>
            <input
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
              className="input-modern w-full px-4 py-2.5 text-sm"
            />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">
              Nueva contraseña
            </span>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              minLength={4}
              className="input-modern w-full px-4 py-2.5 text-sm"
            />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">
              Confirmar nueva contraseña
            </span>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              minLength={4}
              className="input-modern w-full px-4 py-2.5 text-sm"
            />
          </label>
          <button
            type="submit"
            disabled={loading || success}
            className="btn-primary flex w-full items-center justify-center gap-2 py-3 text-sm disabled:opacity-60"
          >
            {loading ? <Loader2 size={18} className="animate-spin" /> : 'Guardar contraseña'}
          </button>
        </form>
      </div>
    </div>
  )
}
