import { AlertCircle, ArrowDownLeft, ArrowUpRight } from 'lucide-react'
import type { ConversationMessage } from '../../types'
import { formatDateTime } from '../../utils/format'
import { EmailAttachmentsList } from './EmailAttachmentsList'

const STATUS_LABELS: Record<string, string> = {
  received: 'Recibido',
  pending_send: 'Pendiente de envío',
  sending: 'Enviando…',
  sent: 'Enviado',
  failed: 'Error de envío',
}

export function EmailMessageCard({
  message,
  onError,
}: {
  message: ConversationMessage
  onError?: (message: string) => void
}) {
  const outbound = message.direction === 'outbound'

  return (
    <article
      className={`rounded-2xl border p-4 ${
        outbound
          ? 'border-auditart-blue/20 bg-auditart-blue/4'
          : 'border-gray-200 bg-white'
      }`}
    >
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-xs font-semibold">
          {outbound ? (
            <ArrowUpRight size={14} className="text-auditart-blue" />
          ) : (
            <ArrowDownLeft size={14} className="text-emerald-600" />
          )}
          <span className="text-auditart-navy">{message.from}</span>
          <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[10px] text-auditart-muted">
            {STATUS_LABELS[message.status] ?? message.status}
          </span>
        </div>
        <span className="text-[11px] text-auditart-muted">
          {formatDateTime(message.occurredAt)}
        </span>
      </div>

      {(message.to.length > 0 || message.cc.length > 0) && (
        <p className="mb-2 text-[11px] text-auditart-muted">
          Para: {message.to.join(', ') || '—'}
          {message.cc.length > 0 ? ` · Cc: ${message.cc.join(', ')}` : ''}
        </p>
      )}

      <p className="mb-2 text-sm font-semibold text-auditart-navy">{message.subject}</p>
      {message.bodyHtml ? (
        <div
          className="prose prose-sm max-w-none text-sm leading-relaxed text-auditart-navy/80 [&_a]:text-auditart-blue"
          dangerouslySetInnerHTML={{ __html: message.bodyHtml }}
        />
      ) : (
        <p className="whitespace-pre-wrap text-sm leading-relaxed text-auditart-navy/80">
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
    </article>
  )
}
