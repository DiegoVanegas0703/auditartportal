import { ExternalLink, Loader2, MessageCircle, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { whatsappApi, type WhatsAppTurnoPreview } from '../../api/auditartApi'

export function WhatsAppTurnoModal({
  serviceId,
  onClose,
  onSent,
}: {
  serviceId: string
  onClose: () => void
  onSent: (phone: string) => void
}) {
  const [preview, setPreview] = useState<WhatsAppTurnoPreview | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    void whatsappApi
      .previewTurno(serviceId)
      .then((p) => {
        if (!cancelled) setPreview(p)
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
  }, [serviceId])

  const send = async () => {
    setBusy(true)
    setError(null)
    try {
      const r = await whatsappApi.notifyTurno(serviceId)
      onSent(r.phone)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo enviar el WhatsApp')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div className="flex items-center gap-2">
            <MessageCircle size={18} className="text-green-600" />
            <div>
              <h2 className="text-lg font-bold text-auditart-navy">Avisar turno por WhatsApp</h2>
              <p className="text-xs text-auditart-muted">
                Vista previa del mensaje que recibirá el paciente
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
              <div className="rounded-2xl border border-green-100 bg-[#e7fce3] px-4 py-3 text-sm whitespace-pre-wrap text-auditart-navy shadow-inner">
                {preview.messageBody}
              </div>
              <p className="mt-3 text-[11px] text-auditart-muted">
                El mensaje aclara que no se responde en ese chat. El paciente puede tocar el link
                wa.me para escribirte a tu WhatsApp.
              </p>
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
            disabled={!preview?.canSend || busy || loading}
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
