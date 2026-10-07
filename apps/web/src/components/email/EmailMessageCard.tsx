import { AlertCircle, ArrowDownLeft, ArrowUpRight, ChevronDown, Paperclip } from 'lucide-react'
import type { ConversationMessage } from '../../types'
import { formatDateTime } from '../../utils/format'
import { EmailAttachmentsList } from './EmailAttachmentsList'
import { EmailHtmlFrame } from './EmailHtmlFrame'

const STATUS_LABELS: Record<string, string> = {
  received: 'Recibido',
  pending_send: 'Pendiente de envío',
  sending: 'Enviando…',
  sent: 'Enviado',
  failed: 'Error de envío',
}

function previewText(message: ConversationMessage) {
  const text = message.bodyText?.replace(/\s+/g, ' ').trim()
  return text ? text.slice(0, 160) : '(sin contenido)'
}

export function EmailMessageCard({
  message,
  expanded = true,
  onToggle,
  onError,
}: {
  message: ConversationMessage
  expanded?: boolean
  onToggle?: () => void
  onError?: (message: string) => void
}) {
  const outbound = message.direction === 'outbound'

  return (
    <article
      className={`rounded-2xl border ${
        outbound
          ? 'border-auditart-blue/20 bg-auditart-blue/4'
          : 'border-gray-200 bg-white'
      }`}
    >
      <button
        type="button"
        onClick={onToggle}
        disabled={!onToggle}
        aria-expanded={expanded}
        className="flex w-full items-start gap-3 p-4 text-left disabled:cursor-default"
      >
        <span className="mt-0.5 shrink-0">
          {outbound ? (
            <ArrowUpRight size={14} className="text-auditart-blue" />
          ) : (
            <ArrowDownLeft size={14} className="text-emerald-600" />
          )}
        </span>
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center justify-between gap-2">
            <span className="flex min-w-0 items-center gap-2 text-xs font-semibold">
              <span className="truncate text-auditart-navy">{message.from}</span>
              <span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-[10px] text-auditart-muted">
                {STATUS_LABELS[message.status] ?? message.status}
              </span>
              {!expanded && message.attachments.length > 0 && (
                <Paperclip size={12} className="shrink-0 text-auditart-muted" />
              )}
            </span>
            <span className="shrink-0 text-[11px] text-auditart-muted">
              {formatDateTime(message.occurredAt)}
            </span>
          </span>
          {!expanded && (
            <span className="mt-1 block truncate text-xs text-auditart-muted">
              {previewText(message)}
            </span>
          )}
        </span>
        {onToggle && (
          <ChevronDown
            size={16}
            className={`mt-0.5 shrink-0 text-auditart-muted transition-transform ${
              expanded ? 'rotate-180' : ''
            }`}
          />
        )}
      </button>

      {expanded && (
        <div className="px-4 pb-4">
          {(message.to.length > 0 || message.cc.length > 0) && (
            <p className="mb-2 text-[11px] text-auditart-muted">
              Para: {message.to.join(', ') || '—'}
              {message.cc.length > 0 ? ` · Cc: ${message.cc.join(', ')}` : ''}
            </p>
          )}

          <p className="mb-2 text-sm font-semibold text-auditart-navy">{message.subject}</p>
          {message.bodyHtml ? (
            <EmailHtmlFrame html={message.bodyHtml} />
          ) : (
            <p className="whitespace-pre-wrap break-words text-sm leading-relaxed text-auditart-navy/80">
              {message.bodyText}
            </p>
          )}

          {message.lastError && (
            <p className="mt-2 flex items-center gap-1.5 text-xs font-medium text-red-600">
              <AlertCircle size={12} />
              {message.lastError}
            </p>
          )}

          {message.attachments.length > 0 && (
            <div className="mt-3">
              <EmailAttachmentsList
                attachments={message.attachments}
                title="Adjuntos del mensaje"
                onError={onError}
              />
            </div>
          )}
        </div>
      )}
    </article>
  )
}
