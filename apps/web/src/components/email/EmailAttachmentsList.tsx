import { Download, Loader2, Paperclip, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { attachmentsApi } from '../../api/auditartApi'
import type { EmailAttachment } from '../../types'

function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function EmailAttachmentsList({
  attachments,
  title = 'Adjuntos',
  onError,
  onDelete,
  deletingId,
}: {
  attachments: EmailAttachment[]
  title?: string
  onError?: (message: string) => void
  onDelete?: (attachment: EmailAttachment) => void | Promise<void>
  deletingId?: string | null
}) {
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  if (attachments.length === 0) return null

  const handleDownload = async (attachment: EmailAttachment) => {
    setDownloadingId(attachment.id)
    try {
      await attachmentsApi.download(attachment.id, attachment.fileName)
    } catch (e) {
      onError?.(e instanceof Error ? e.message : 'No se pudo descargar el adjunto')
    } finally {
      setDownloadingId(null)
    }
  }

  return (
    <div>
      <p className="mb-2 text-xs font-bold uppercase tracking-wider text-auditart-muted">
        {title} ({attachments.length})
      </p>
      <div className="space-y-2">
        {attachments.map((attachment) => (
          <div
            key={attachment.id}
            className="flex w-full items-center gap-2 rounded-xl border border-gray-200 bg-white px-3.5 py-3"
          >
            <button
              type="button"
              onClick={() => void handleDownload(attachment)}
              disabled={downloadingId === attachment.id}
              className="flex min-w-0 flex-1 items-center gap-3 text-left transition-colors hover:text-auditart-blue disabled:opacity-60"
            >
              <Paperclip size={16} className="shrink-0 text-auditart-blue" />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold text-auditart-navy">
                  {attachment.fileName}
                </span>
                <span className="text-xs text-auditart-muted">
                  {formatFileSize(attachment.sizeBytes)}
                </span>
              </span>
              {downloadingId === attachment.id ? (
                <Loader2 size={16} className="animate-spin text-auditart-blue" />
              ) : (
                <Download size={16} className="shrink-0 text-auditart-muted" />
              )}
            </button>
            {onDelete && (
              <button
                type="button"
                title="Quitar del caso"
                disabled={deletingId === attachment.id}
                onClick={() => void onDelete(attachment)}
                className="rounded-lg p-2 text-red-600 transition-colors hover:bg-red-50 disabled:opacity-50"
              >
                {deletingId === attachment.id ? (
                  <Loader2 size={16} className="animate-spin" />
                ) : (
                  <Trash2 size={16} />
                )}
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
