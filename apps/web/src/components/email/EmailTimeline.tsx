import { useEffect, useState } from 'react'
import type { ConversationMessage } from '../../types'
import { EmailMessageCard } from './EmailMessageCard'

export function EmailTimeline({
  messages,
  onError,
}: {
  messages: ConversationMessage[]
  onError?: (message: string) => void
}) {
  const lastId = messages[messages.length - 1]?.id
  const [expandedIds, setExpandedIds] = useState<Set<string>>(
    () => new Set(lastId ? [lastId] : []),
  )

  useEffect(() => {
    setExpandedIds(new Set(lastId ? [lastId] : []))
  }, [lastId])

  if (messages.length === 0) {
    return (
      <p className="rounded-xl bg-gray-50 px-4 py-6 text-center text-sm text-auditart-muted">
        Sin mensajes en la conversación.
      </p>
    )
  }

  const toggle = (id: string) =>
    setExpandedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const allExpanded = messages.every((m) => expandedIds.has(m.id))

  return (
    <div className="space-y-2">
      {messages.length > 1 && (
        <div className="flex justify-end">
          <button
            type="button"
            onClick={() =>
              setExpandedIds(
                allExpanded ? new Set(lastId ? [lastId] : []) : new Set(messages.map((m) => m.id)),
              )
            }
            className="text-xs font-semibold text-auditart-blue hover:underline"
          >
            {allExpanded ? 'Contraer anteriores' : `Expandir todos (${messages.length})`}
          </button>
        </div>
      )}
      {messages.map((message) => (
        <EmailMessageCard
          key={message.id}
          message={message}
          expanded={expandedIds.has(message.id)}
          onToggle={messages.length > 1 ? () => toggle(message.id) : undefined}
          onError={onError}
        />
      ))}
    </div>
  )
}
