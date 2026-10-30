import { ExternalLink, Loader2, MessageCircle, Save, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import {
  whatsappApi,
  type WhatsAppTemplateDto,
  type WhatsAppTurnoPreview,
} from '../../api/auditartApi'
import { useAuth } from '../../context/useAuth'

export function WhatsAppTurnoModal({
  serviceId,
  onClose,
  onSent,
}: {
  serviceId: string
  onClose: () => void
  onSent: (phone: string) => void
}) {
  const { user } = useAuth()
  const canEditTemplates = user?.role === 'admin' || user?.role === 'jefatura'
  const [preview, setPreview] = useState<WhatsAppTurnoPreview | null>(null)
  const [message, setMessage] = useState('')
  const [templates, setTemplates] = useState<WhatsAppTemplateDto[]>([])
  const [showTemplates, setShowTemplates] = useState(false)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [savingKey, setSavingKey] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState('')

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    void Promise.all([
      whatsappApi.previewTurno(serviceId),
      canEditTemplates ? whatsappApi.listTemplates() : Promise.resolve([]),
    ])
      .then(([p, t]) => {
        if (cancelled) return
        setPreview(p)
        setMessage(p.messageBody)
        setTemplates(t)
      })
      .catch((e: unknown) => {
        if (!cancelled)
          setError(e instanceof Error ? e.message : 'No se pudo armar la vista previa')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [serviceId, canEditTemplates])

  const send = async () => {
    setBusy(true)
    setError(null)
    try {
      const r = await whatsappApi.notifyTurno(serviceId, message)
      onSent(r.phone)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo enviar el WhatsApp')
    } finally {
      setBusy(false)
    }
  }

  const saveTemplate = async (key: string, body: string) => {
    setSavingKey(key)
    setError(null)
    try {
      const saved = await whatsappApi.saveTemplate(key, body)
      setTemplates((prev) => prev.map((t) => (t.key === saved.key ? saved : t)))
      setToast(`Plantilla ${saved.title} guardada`)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la plantilla')
    } finally {
      setSavingKey(null)
    }
  }

  const kindLabel =
    preview?.kind === 'telemedicina' ? 'Telemedicina' : 'Turno presencial'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div className="flex items-center gap-2">
            <MessageCircle size={18} className="text-green-600" />
            <div>
              <h2 className="text-lg font-bold text-auditart-navy">Avisar turno por WhatsApp</h2>
              <p className="text-xs text-auditart-muted">
                {preview
                  ? `Plantilla: ${kindLabel}. Podés editar el texto antes de enviar.`
                  : 'Vista previa del mensaje que recibirá el paciente'}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-auditart-gray hover:bg-gray-100"
          >
            <X size={18} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {loading && (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Armando mensaje…
            </div>
          )}
          {error && (
            <div className="mb-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
          )}
          {toast && (
            <div className="mb-3 rounded-xl bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
              {toast}
            </div>
          )}
          {preview && !loading && (
            <>
              <div className="mb-3 grid gap-1 text-xs text-auditart-gray">
                <p>
                  <span className="font-semibold text-auditart-navy">Destino:</span>{' '}
                  {preview.patientPhoneE164 ?? '— sin teléfono válido —'}
                </p>
                {preview.contactWhatsAppLink && (
                  <p className="flex flex-wrap items-center gap-1">
                    <span className="font-semibold text-auditart-navy">Tu contacto en el mensaje:</span>
                    <a
                      href={preview.contactWhatsAppLink}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex items-center gap-1 font-semibold text-green-700 hover:underline"
                    >
                      {preview.contactWhatsAppLink}
                      <ExternalLink size={12} />
                    </a>
                  </p>
                )}
              </div>
              {preview.blockReason && (
                <div className="mb-3 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">
                  {preview.blockReason}
                </div>
              )}
              <label className="block text-xs font-semibold text-auditart-gray">
                Mensaje (editable)
                <textarea
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                  rows={14}
                  className="mt-1 w-full rounded-2xl border border-green-100 bg-[#e7fce3] px-4 py-3 text-sm text-auditart-navy shadow-inner focus:border-green-400 focus:outline-none"
                />
              </label>
              <p className="mt-2 text-[11px] text-auditart-muted">
                En telemedicina solo se incluye el médico asignado (o el que coincida por
                apellido). Placeholders de plantilla: {'{{medico}}'}, {'{{telefono_medico}}'},{' '}
                {'{{doctor_line}}'}, {'{{paciente}}'}, {'{{fecha}}'}, {'{{direccion}}'}.
              </p>
              {canEditTemplates && (
                <div className="mt-4">
                  <button
                    type="button"
                    onClick={() => setShowTemplates((v) => !v)}
                    className="text-xs font-semibold text-auditart-blue hover:underline"
                  >
                    {showTemplates ? 'Ocultar plantillas' : 'Editar plantillas (turno y telemedicina)'}
                  </button>
                  {showTemplates && (
                    <div className="mt-3 grid gap-4">
                      {templates.map((t) => (
                        <TemplateEditor
                          key={t.key}
                          template={t}
                          saving={savingKey === t.key}
                          onSave={(body) => void saveTemplate(t.key, body)}
                        />
                      ))}
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-4">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border border-gray-200 px-4 py-2 text-sm font-semibold text-auditart-navy"
          >
            Cancelar
          </button>
          <button
            type="button"
            disabled={!preview?.canSend || busy || loading || !message.trim()}
            onClick={() => void send()}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-green-500 to-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {busy ? <Loader2 size={14} className="animate-spin" /> : <MessageCircle size={14} />}
            Enviar WhatsApp
          </button>
        </div>
      </div>
    </div>
  )
}

function TemplateEditor({
  template,
  saving,
  onSave,
}: {
  template: WhatsAppTemplateDto
  saving: boolean
  onSave: (body: string) => void
}) {
  const [body, setBody] = useState(template.body)
  useEffect(() => {
    setBody(template.body)
  }, [template.body])

  return (
    <div className="rounded-2xl border border-gray-100 p-3">
      <div className="mb-2 flex items-center justify-between gap-2">
        <p className="text-sm font-bold text-auditart-navy">{template.title}</p>
        <button
          type="button"
          disabled={saving || !body.trim()}
          onClick={() => onSave(body)}
          className="inline-flex items-center gap-1 rounded-lg bg-auditart-navy px-2.5 py-1 text-[11px] font-semibold text-white disabled:opacity-50"
        >
          {saving ? <Loader2 size={12} className="animate-spin" /> : <Save size={12} />}
          Guardar plantilla
        </button>
      </div>
      <textarea
        value={body}
        onChange={(e) => setBody(e.target.value)}
        rows={8}
        className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs text-auditart-navy focus:border-auditart-blue focus:outline-none"
      />
    </div>
  )
}
