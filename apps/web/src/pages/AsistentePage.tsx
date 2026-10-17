import { ExternalLink, Loader2, Send, Sparkles } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { aiApi, type AiAssistantDto } from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'

type ChatMsg = { role: 'user' | 'assistant'; content: string }

export function AsistentePage() {
  const [assistants, setAssistants] = useState<AiAssistantDto[]>([])
  const [chatEnabled, setChatEnabled] = useState(false)
  const [selectedId, setSelectedId] = useState('')
  const [messages, setMessages] = useState<ChatMsg[]>([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const [status, list] = await Promise.all([aiApi.status(), aiApi.listAssistants()])
        if (cancelled) return
        setChatEnabled(status.chatEnabled)
        setAssistants(list)
        if (list.length) setSelectedId(list[0].id)
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'No se pudo cargar asistentes')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, busy])

  const selected = assistants.find((a) => a.id === selectedId)

  const send = async () => {
    const text = input.trim()
    if (!text || !selectedId || busy) return
    if (!selected?.supportsChat) {
      setError('Este asistente no tiene chat embebido. Usá “Abrir en Gemini”.')
      return
    }
    const nextMessages: ChatMsg[] = [...messages, { role: 'user', content: text }]
    setMessages(nextMessages)
    setInput('')
    setBusy(true)
    setError(null)
    try {
      const res = await aiApi.chat(
        selectedId,
        nextMessages.map((m) => ({ role: m.role, content: m.content })),
      )
      setMessages((prev) => [...prev, { role: 'assistant', content: res.reply }])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo obtener respuesta')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Asistente IA"
        subtitle="GEMs de Gemini (acceso rápido) y chat embebido con prompts de Auditart"
      />

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
          <aside className="card-flat space-y-2 p-3">
            <p className="px-2 text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
              Asistentes / GEMs
            </p>
            {assistants.map((a) => (
              <button
                key={a.id}
                type="button"
                onClick={() => {
                  setSelectedId(a.id)
                  setMessages([])
                  setError(null)
                }}
                className={`w-full rounded-xl px-3 py-2.5 text-left text-sm transition-colors ${
                  selectedId === a.id
                    ? 'bg-auditart-blue/10 font-semibold text-auditart-blue'
                    : 'text-auditart-navy hover:bg-gray-50'
                }`}
              >
                <span className="flex items-center gap-2">
                  <Sparkles size={14} />
                  {a.name}
                </span>
                {a.description && (
                  <span className="mt-0.5 block text-[11px] font-normal text-auditart-muted">
                    {a.description}
                  </span>
                )}
              </button>
            ))}
            {!assistants.length && (
              <p className="px-2 text-xs text-auditart-muted">No hay asistentes configurados.</p>
            )}
          </aside>

          <section className="card-flat flex min-h-[480px] flex-col">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-gray-100 px-4 py-3">
              <div>
                <h2 className="text-sm font-bold text-auditart-navy">
                  {selected?.name ?? 'Seleccioná un asistente'}
                </h2>
                <p className="text-[11px] text-auditart-muted">
                  {chatEnabled
                    ? selected?.supportsChat
                      ? 'Chat con Gemini API activo'
                      : 'Solo link externo (sin prompt local)'
                    : 'Chat API deshabilitado — configurá Gemini__ApiKey en el servidor'}
                </p>
              </div>
              {selected?.externalGemUrl && (
                <a
                  href={selected.externalGemUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex items-center gap-1.5 rounded-xl border border-gray-200 px-3 py-1.5 text-xs font-semibold text-auditart-navy hover:bg-gray-50"
                >
                  <ExternalLink size={14} /> Abrir en Gemini
                </a>
              )}
            </div>

            <div className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
              {!messages.length && (
                <p className="text-sm text-auditart-gray">
                  Escribí una consulta o abrí el GEM en Gemini para usar las herramientas del correo
                  info@.
                </p>
              )}
              {messages.map((m, i) => (
                <div
                  key={`${m.role}-${i}`}
                  className={`max-w-[90%] rounded-2xl px-3 py-2 text-sm whitespace-pre-wrap ${
                    m.role === 'user'
                      ? 'ml-auto bg-auditart-blue text-white'
                      : 'bg-gray-100 text-auditart-navy'
                  }`}
                >
                  {m.content}
                </div>
              ))}
              {busy && (
                <div className="flex items-center gap-2 text-xs text-auditart-muted">
                  <Loader2 size={14} className="animate-spin" /> Pensando…
                </div>
              )}
              <div ref={bottomRef} />
            </div>

            {error && (
              <div className="mx-4 mb-2 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">
                {error}
              </div>
            )}

            <form
              className="flex gap-2 border-t border-gray-100 p-3"
              onSubmit={(e) => {
                e.preventDefault()
                void send()
              }}
            >
              <input
                value={input}
                onChange={(e) => setInput(e.target.value)}
                placeholder={
                  selected?.supportsChat && chatEnabled
                    ? 'Preguntá al asistente…'
                    : 'Chat no disponible — usá Abrir en Gemini'
                }
                disabled={!selected?.supportsChat || !chatEnabled || busy}
                className="input-modern flex-1 px-3 py-2 text-sm disabled:opacity-50"
              />
              <button
                type="submit"
                disabled={!selected?.supportsChat || !chatEnabled || busy || !input.trim()}
                className="btn-primary px-3 disabled:opacity-50"
              >
                <Send size={16} />
              </button>
            </form>
          </section>
        </div>
      )}
    </div>
  )
}
