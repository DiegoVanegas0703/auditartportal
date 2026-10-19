import { CheckCircle2, ExternalLink, Loader2, Lock, MessageCircle, PenLine } from 'lucide-react'
import { useEffect, useState } from 'react'
import { authApi } from '../api/auditartApi'
import { EmailHtmlFrame } from '../components/email/EmailHtmlFrame'
import { RichTextEditor } from '../components/email/RichTextEditor'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuth } from '../context/useAuth'
import { ROLE_LABELS } from '../types'

export function MiCuentaPage() {
  const { user } = useAuth()
  if (!user) return null

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Mi cuenta"
        subtitle={`${user.name} · ${user.email} · ${ROLE_LABELS[user.role]}`}
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <SignatureCard />
        <WhatsAppPhoneCard />
        <PasswordCard />
      </div>
    </div>
  )
}

function WhatsAppPhoneCard() {
  const { user, updateWhatsAppPhone } = useAuth()
  const saved = user?.whatsAppPhone ?? ''
  const [phone, setPhone] = useState(saved)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  useEffect(() => setPhone(saved), [saved])

  const digits = phone.replace(/\D/g, '')
  const waLink =
    digits.length >= 8
      ? `https://wa.me/${digits.startsWith('54') ? digits : `549${digits.replace(/^0/, '')}`}`
      : null

  const handleSave = async () => {
    setBusy(true)
    setMessage(null)
    try {
      await updateWhatsAppPhone(phone)
      setMessage({
        ok: true,
        text: phone.trim()
          ? 'WhatsApp guardado. Se incluirá como link en los avisos de turno.'
          : 'WhatsApp eliminado.',
      })
    } catch (e) {
      setMessage({
        ok: false,
        text: e instanceof Error ? e.message : 'No se pudo guardar el WhatsApp',
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card-flat p-6">
      <div className="mb-4 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-green-500/10 text-green-700">
          <MessageCircle size={18} />
        </div>
        <div>
          <h2 className="text-base font-bold text-auditart-navy">WhatsApp de contacto</h2>
          <p className="text-xs text-auditart-muted">
            Cuando avisás un turno al paciente, el mensaje indica que no responda a ese chat e
            incluye un link wa.me a este número.
          </p>
        </div>
      </div>

      <label className="block">
        <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">
          Celular (Argentina)
        </span>
        <input
          type="tel"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          placeholder="11 1234-5678"
          className="input-modern w-full px-4 py-2.5 text-sm"
        />
      </label>

      {waLink && (
        <p className="mt-2 flex flex-wrap items-center gap-1 text-xs text-auditart-muted">
          Vista previa del link:
          <a
            href={waLink}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1 font-semibold text-green-700 hover:underline"
          >
            {waLink}
            <ExternalLink size={12} />
          </a>
        </p>
      )}

      {message && (
        <p className={`mt-3 text-sm ${message.ok ? 'text-emerald-700' : 'text-red-600'}`}>
          {message.text}
        </p>
      )}

      <button
        type="button"
        onClick={() => void handleSave()}
        disabled={busy || phone.trim() === saved.trim()}
        className="btn-primary mt-4 flex items-center gap-2 disabled:opacity-50"
      >
        {busy && <Loader2 size={16} className="animate-spin" />}
        Guardar WhatsApp
      </button>
    </section>
  )
}

function SignatureCard() {
  const { user, updateSignature } = useAuth()
  const saved = user?.emailSignatureHtml ?? ''
  const [html, setHtml] = useState(saved)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  useEffect(() => setHtml(saved), [saved])

  const handleSave = async () => {
    setBusy(true)
    setMessage(null)
    try {
      await updateSignature(html)
      setMessage({ ok: true, text: html ? 'Firma guardada.' : 'Firma eliminada.' })
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error ? e.message : 'No se pudo guardar la firma' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card-flat p-6">
      <div className="mb-4 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-auditart-blue/10 text-auditart-blue">
          <PenLine size={18} />
        </div>
        <div>
          <h2 className="text-base font-bold text-auditart-navy">Firma de correo</h2>
          <p className="text-xs text-auditart-muted">
            Se agrega automáticamente al redactar o responder correos. Podés editarla antes de enviar.
          </p>
        </div>
      </div>

      <RichTextEditor
        valueHtml={html}
        onChange={({ html: next }) => setHtml(next)}
        placeholder="Ej.: Nombre Apellido · Auditoría médica · Auditart · Tel. 11 1234-5678"
        minHeightClass="min-h-[120px]"
        allowImages
      />
      <p className="mt-1.5 text-[11px] text-auditart-muted">
        Usá el botón de imagen para cargar el logo (PNG o JPG). Se reduce automáticamente; podés
        ajustar su tamaño arrastrando las esquinas.
      </p>

      {html && (
        <div className="mt-4">
          <p className="mb-1 text-[11px] font-bold uppercase tracking-wider text-auditart-muted">
            Vista previa
          </p>
          <div className="rounded-xl border border-dashed border-gray-200 bg-gray-50/60 p-3">
            <EmailHtmlFrame html={html} />
          </div>
        </div>
      )}

      {message && (
        <p className={`mt-3 text-sm ${message.ok ? 'text-emerald-700' : 'text-red-600'}`}>{message.text}</p>
      )}

      <button
        type="button"
        onClick={() => void handleSave()}
        disabled={busy || html === saved}
        className="btn-primary mt-4 flex items-center gap-2 disabled:opacity-50"
      >
        {busy && <Loader2 size={16} className="animate-spin" />}
        Guardar firma
      </button>
    </section>
  )
}

function PasswordCard() {
  const { completePasswordChange } = useAuth()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSuccess(false)
    if (newPassword !== confirmPassword) {
      setError('Las contraseñas nuevas no coinciden.')
      return
    }
    if (newPassword.length < 4) {
      setError('La contraseña debe tener al menos 4 caracteres.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await authApi.changePassword(currentPassword, newPassword)
      completePasswordChange()
      setSuccess(true)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cambiar la contraseña')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card-flat p-6">
      <div className="mb-4 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-auditart-blue/10 text-auditart-blue">
          <Lock size={18} />
        </div>
        <div>
          <h2 className="text-base font-bold text-auditart-navy">Cambiar contraseña</h2>
          <p className="text-xs text-auditart-muted">Mínimo 4 caracteres.</p>
        </div>
      </div>

      {success && (
        <div className="mb-4 flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-700">
          <CheckCircle2 size={16} />
          Contraseña actualizada.
        </div>
      )}
      {error && <div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <PasswordField label="Contraseña actual" value={currentPassword} onChange={setCurrentPassword} />
        <PasswordField label="Nueva contraseña" value={newPassword} onChange={setNewPassword} />
        <PasswordField label="Confirmar nueva contraseña" value={confirmPassword} onChange={setConfirmPassword} />
        <button type="submit" disabled={busy} className="btn-primary flex items-center gap-2 disabled:opacity-50">
          {busy && <Loader2 size={16} className="animate-spin" />}
          Guardar contraseña
        </button>
      </form>
    </section>
  )
}

function PasswordField({
  label,
  value,
  onChange,
}: {
  label: string
  value: string
  onChange: (value: string) => void
}) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-xs font-semibold text-auditart-gray">{label}</span>
      <input
        type="password"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        required
        className="input-modern w-full px-4 py-2.5 text-sm"
      />
    </label>
  )
}
