import { Loader2, Paperclip, Send, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { ReplyDefaults } from '../../types'
import { RichTextEditor } from './RichTextEditor'

function parseAddresses(value: string): string[] {
  return value
    .split(/[,;]+/)
    .map((part) => part.trim())
    .filter(Boolean)
}

const ALLOWED = /\.(pdf|docx?|xlsx?|jpe?g|png|gif|webp|bmp)$/i
const MAX_BYTES = 50 * 1024 * 1024

export function EmailReplyComposer({
  defaults,
  busy,
  onSend,
}: {
  defaults: ReplyDefaults
  busy?: boolean
  onSend: (payload: {
    to: string[]
    cc: string[]
    bodyText: string
    bodyHtml?: string
    files?: File[]
  }) => Promise<void>
}) {
  const [to, setTo] = useState(defaults.to.join(', '))
  const [cc, setCc] = useState(defaults.cc.join(', '))
  const [bodyHtml, setBodyHtml] = useState('')
  const [bodyText, setBodyText] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setTo(defaults.to.join(', '))
    setCc(defaults.cc.join(', '))
    setBodyHtml('')
    setBodyText('')
    setFiles([])
    setError(null)
  }, [defaults.to, defaults.cc, defaults.subject])

  const addFiles = (list: FileList | null) => {
    if (!list?.length) return
    const next = [...files]
    for (const file of Array.from(list)) {
      if (!ALLOWED.test(file.name)) {
        setError(`Tipo no permitido: ${file.name}`)
        return
      }
      if (file.size > MAX_BYTES) {
        setError(`${file.name} supera 50 MB`)
        return
      }
      next.push(file)
    }
    setError(null)
    setFiles(next)
  }

  const handleSend = async () => {
    const toList = parseAddresses(to)
    if (toList.length === 0) {
      setError('Indicá al menos un destinatario.')
      return
    }
    if (!bodyText.trim()) {
      setError('Escribí el cuerpo de la respuesta.')
      return
    }
    setError(null)
    try {
      await onSend({
        to: toList,
        cc: parseAddresses(cc),
        bodyText: bodyText.trim(),
        bodyHtml: bodyHtml || undefined,
        files: files.length ? files : undefined,
      })
      setBodyHtml('')
      setBodyText('')
      setFiles([])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo encolar la respuesta')
    }
  }

  return (
    <div className="rounded-2xl border border-auditart-blue/20 bg-white p-4">
      <p className="mb-3 text-xs font-bold uppercase tracking-wider text-auditart-muted">
        Responder a todos · {defaults.subject}
      </p>
      <div className="mb-3 grid gap-2">
        <label className="block">
          <span className="mb-1 block text-xs font-semibold text-auditart-gray">Para</span>
          <input
            type="text"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="input-modern w-full px-3 py-2 text-sm"
            placeholder="destinatario@ejemplo.com"
          />
        </label>
        <label className="block">
          <span className="mb-1 block text-xs font-semibold text-auditart-gray">Cc</span>
          <input
            type="text"
            value={cc}
            onChange={(e) => setCc(e.target.value)}
            className="input-modern w-full px-3 py-2 text-sm"
            placeholder="opcional"
          />
        </label>
        <div>
          <span className="mb-1 block text-xs font-semibold text-auditart-gray">Mensaje</span>
          <RichTextEditor
            valueHtml={bodyHtml}
            onChange={({ html, text }) => {
              setBodyHtml(html)
              setBodyText(text)
            }}
            placeholder="Escribí la respuesta…"
          />
        </div>
        <div>
          <label className="inline-flex cursor-pointer items-center gap-2 text-xs font-semibold text-auditart-blue hover:underline">
            <Paperclip size={14} />
            Adjuntar archivos a la respuesta
            <input
              type="file"
              className="hidden"
              multiple
              accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.gif,.webp,.bmp"
              onChange={(e) => {
                addFiles(e.target.files)
                e.target.value = ''
              }}
            />
          </label>
          {files.length > 0 && (
            <ul className="mt-2 space-y-1">
              {files.map((file, index) => (
                <li
                  key={`${file.name}-${index}`}
                  className="flex items-center justify-between rounded-lg bg-auditart-light px-2 py-1 text-xs text-auditart-navy"
                >
                  <span className="truncate">
                    {file.name} ({(file.size / 1024).toFixed(0)} KB)
                  </span>
                  <button
                    type="button"
                    onClick={() => setFiles((prev) => prev.filter((_, i) => i !== index))}
                    className="ml-2 text-auditart-muted hover:text-red-600"
                  >
                    <X size={12} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
      {error && <p className="mb-2 text-xs text-red-600">{error}</p>}
      <button
        type="button"
        onClick={() => void handleSend()}
        disabled={busy}
        className="btn-primary flex items-center gap-2 disabled:opacity-50"
      >
        {busy ? <Loader2 size={16} className="animate-spin" /> : <Send size={16} />}
        Enviar respuesta
      </button>
    </div>
  )
}
