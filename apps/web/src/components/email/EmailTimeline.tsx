import type { ConversationMessage } from '../../types'
import { EmailMessageCard } from './EmailMessageCard'

export function EmailTimeline({
  messages,
  onError,
}: {
  messages: ConversationMessage[]
  onError?: (message: string) => void
}) {
  if (messages.length === 0) {
    return (
      <p className="rounded-xl bg-gray-50 px-4 py-6 text-center text-sm text-auditart-muted">
        Sin mensajes en la conversación.
      </p>
    )
  }

  return (
    <div className="space-y-3">
      {messages.map((message) => (
        <EmailMessageCard key={message.id} message={message} onError={onError} />
      ))}
    </div>
  )
}
