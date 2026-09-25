import {
  ArrowRight,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  GitMerge,
  Loader2,
  Mail,
  Paperclip,
  Plus,
  RefreshCw,
  RotateCcw,
  Sparkles,
  Tag,
  Trash2,
  Users,
  X,
  Zap,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  mapListedUser,
  mapRequestDetail,
  mapRequestSummary,
  triageApi,
  usersApi,
} from '../api/auditartApi'
import { EmailAttachmentsList } from '../components/email/EmailAttachmentsList'
import { EmailReplyComposer } from '../components/email/EmailReplyComposer'
import { EmailTimeline } from '../components/email/EmailTimeline'
import { PageHeader } from '../components/ui/PageHeader'
import { useAudits } from '../context/useAudits'
import {
  CHANNEL_LABELS,
  QUEUE_LABELS,
  SERVICE_LABELS,
  type AuditQueue,
  type EmailRequestDetail,
  type EmailRequestSummary,
  type ServiceType,
  type UrgencyLevel,
  type User,
} from '../types'
import { formatDateTime } from '../utils/format'

const URGENCY_LABELS: Record<UrgencyLevel, string> = {
  normal: 'Normal',
  alta: 'Alta',
  critica: 'Crítica',
}

const QUEUES: { key: AuditQueue; icon: string; desc: string }[] = [
  { key: 'general', icon: '👥', desc: 'Operadores generales' },
  { key: 'telemedicina', icon: '🩺', desc: 'Cola telemedicina' },
  { key: 'cronicos', icon: '📋', desc: 'Cola crónicos' },
]

export function TriagePage() {
  const { assignRequestToQueue, seedDemoEmails, syncGmail, refresh } = useAudits()
  const [requests, setRequests] = useState<EmailRequestSummary[]>([])
  const [detail, setDetail] = useState<EmailRequestDetail | null>(null)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [total, setTotal] = useState(0)
  const [pendingCount, setPendingCount] = useState(0)
  const [ignoredCount, setIgnoredCount] = useState(0)
  const [availableTags, setAvailableTags] = useState<string[]>([])
  const [view, setView] = useState<'pending' | 'ignored'>('pending')
  const [tagFilter, setTagFilter] = useState('')
  const [listLoading, setListLoading] = useState(true)
  const [detailLoading, setDetailLoading] = useState(false)
  const [selectedId, setSelectedId] = useState('')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [selectedQueue, setSelectedQueue] = useState<AuditQueue>('general')
  const [operadorId, setOperadorId] = useState('')
  const [operators, setOperators] = useState<User[]>([])
  const [toast, setToast] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [paciente, setPaciente] = useState('')
  const [dni, setDni] = useState('')
  const [art, setArt] = useState('')
  const [numeroSiniestro, setNumeroSiniestro] = useState('')
  const [telefonoPaciente, setTelefonoPaciente] = useState('')
  const [emailPaciente, setEmailPaciente] = useState('')
  const [tipoServicio, setTipoServicio] = useState<ServiceType>('consultorio')
  const [especialidad, setEspecialidad] = useState('')
  const [urgency, setUrgency] = useState<UrgencyLevel>('alta')
  const [tagInput, setTagInput] = useState('')
  const [triageNote, setTriageNote] = useState('')
  const [assignAttachment, setAssignAttachment] = useState<File | null>(null)
  const [parseBusy, setParseBusy] = useState(false)
  const [selectedPdfAttachmentId, setSelectedPdfAttachmentId] = useState('')

  const loadPage = useCallback(async () => {
    setListLoading(true)
    setError(null)
    try {
      const result = await triageApi.listRequests(
        page,
        15,
        view === 'ignored',
        tagFilter || undefined,
      )
      setRequests(result.items.map(mapRequestSummary))
      setPage(result.page)
      setTotalPages(result.totalPages)
      setTotal(result.total)
      setPendingCount(result.pendingCount)
      setIgnoredCount(result.ignoredCount)
      setAvailableTags(result.availableTags)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los requerimientos')
    } finally {
      setListLoading(false)
    }
  }, [page, view, tagFilter])

  const loadDetail = useCallback(async (requestId: string) => {
    if (!requestId) {
      setDetail(null)
      return
    }
    setDetailLoading(true)
    setError(null)
    try {
      const dto = await triageApi.getRequest(requestId)
      setDetail(mapRequestDetail(dto))
    } catch (e) {
      setDetail(null)
      setError(e instanceof Error ? e.message : 'No se pudo cargar el requerimiento')
    } finally {
      setDetailLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadPage()
  }, [loadPage])

  useEffect(() => {
    if (requests.length && !requests.some((r) => r.id === selectedId)) {
      setSelectedId(requests[0].id)
    } else if (!requests.length) {
      setSelectedId('')
    }
  }, [requests, selectedId])

  useEffect(() => {
    void loadDetail(selectedId)
  }, [selectedId, loadDetail])

  const pdfAttachments = useMemo(
    () =>
      (detail?.attachments ?? []).filter(
        (attachment) =>
          attachment.contentType.toLowerCase().includes('pdf') ||
          attachment.fileName.toLowerCase().endsWith('.pdf'),
      ),
    [detail?.attachments],
  )

  useEffect(() => {
    if (!detail) return
    setSelectedQueue('general')
    setPaciente('')
    setDni('')
    setArt('')
    setNumeroSiniestro('')
    setTelefonoPaciente('')
    setEmailPaciente('')
    setTipoServicio('consultorio')
    setEspecialidad('')
    setUrgency(detail.subject.toLowerCase().includes('urgent') ? 'critica' : 'alta')
    setTagInput('')
    setTriageNote('')
    setAssignAttachment(null)
    setSelectedPdfAttachmentId('')
  }, [detail?.id])

  useEffect(() => {
    if (!pdfAttachments.length) {
      setSelectedPdfAttachmentId('')
      return
    }
    setSelectedPdfAttachmentId((current) =>
      pdfAttachments.some((attachment) => attachment.id === current)
        ? current
        : pdfAttachments[0].id,
    )
  }, [pdfAttachments])

  useEffect(() => {
    void usersApi
      .operators(selectedQueue)
      .then((list) => {
        const mapped = list.map(mapListedUser)
        setOperators(mapped)
        setOperadorId(mapped[0]?.id ?? '')
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Error operadores'))
  }, [selectedQueue])

  const queueHint = useMemo(
    () => QUEUES.find((q) => q.key === selectedQueue)?.desc,
    [selectedQueue],
  )

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((value) => value !== id) : [...prev, id],
    )
  }

  const handleSeed = async () => {
    setBusy(true)
    setError(null)
    try {
      await seedDemoEmails()
      await triageApi.backfillRequests()
      await loadPage()
      setToast('Emails de demo cargados y agrupados')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron crear emails demo')
    } finally {
      setBusy(false)
    }
  }

  const handleSync = async () => {
    setBusy(true)
    setError(null)
    try {
      const result = await syncGmail()
      await loadPage()
      if (selectedId) await loadDetail(selectedId)

      if (result.failed > 0) {
        setError(
          `Se importaron ${result.inserted} mensaje(s) y fallaron ${result.failed}. ${result.errors[0] ?? ''}`,
        )
      } else {
        setToast(
          result.inserted > 0
            ? `${result.inserted} mensaje(s) importado(s) desde Gmail`
            : 'No hay mensajes nuevos en Gmail',
        )
        setTimeout(() => setToast(''), 4000)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo sincronizar Gmail')
    } finally {
      setBusy(false)
    }
  }

  const handleBackfill = async () => {
    setBusy(true)
    setError(null)
    try {
      const result = await triageApi.backfillRequests()
      await loadPage()
      setToast(`Backfill: ${result.inserted} requerimiento(s) creados`)
      setTimeout(() => setToast(''), 4000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo ejecutar el backfill')
    } finally {
      setBusy(false)
    }
  }

  const handleAssign = async () => {
    if (!detail || !operadorId) return
    if (!triageNote.trim()) {
      setError('La nota de triage es obligatoria para derivar.')
      return
    }
    if (!art.trim()) {
      setError('La ART (aseguradora) es obligatoria para derivar.')
      return
    }
    if (!numeroSiniestro.trim()) {
      setError('El número de siniestro es obligatorio para derivar.')
      return
    }
    if (!telefonoPaciente.trim()) {
      setError('El teléfono del paciente es obligatorio para derivar.')
      return
    }
    if (!emailPaciente.trim()) {
      setError('El correo del paciente es obligatorio para derivar.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await assignRequestToQueue(detail.id, selectedQueue, operadorId, {
        triageNote: triageNote.trim(),
        paciente: paciente.trim() || undefined,
        dni: dni.trim() || undefined,
        art: art.trim(),
        numeroSiniestro: numeroSiniestro.trim(),
        telefonoPaciente: telefonoPaciente.trim(),
        emailPaciente: emailPaciente.trim(),
        tipoServicio,
        especialidad: especialidad.trim() || undefined,
        urgency,
        attachment: assignAttachment,
      })
      await loadPage()
      setToast(`Derivado a ${QUEUE_LABELS[selectedQueue]} · servicio en Rojo`)
      setTimeout(() => setToast(''), 4000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo derivar')
    } finally {
      setBusy(false)
    }
  }

  const handleIgnore = async () => {
    if (!detail) return
    setBusy(true)
    setError(null)
    try {
      await triageApi.ignoreRequest(detail.id)
      await Promise.all([loadPage(), refresh()])
      setToast('Requerimiento ignorado')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo ignorar')
    } finally {
      setBusy(false)
    }
  }

  const handleRestore = async () => {
    if (!detail) return
    setBusy(true)
    setError(null)
    try {
      await triageApi.restoreRequest(detail.id)
      await Promise.all([loadPage(), refresh()])
      setToast('Requerimiento restaurado a pendientes')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo restaurar')
    } finally {
      setBusy(false)
    }
  }

  const saveTags = async (tags: string[]) => {
    if (!detail) return
    setBusy(true)
    setError(null)
    try {
      await triageApi.updateRequestTags(detail.id, tags)
      await Promise.all([loadPage(), loadDetail(detail.id)])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron actualizar los tags')
    } finally {
      setBusy(false)
    }
  }

  const handleAddTag = async () => {
    const tag = tagInput.trim().toLowerCase()
    if (!detail || !tag || detail.tags.includes(tag)) return
    setTagInput('')
    await saveTags([...detail.tags, tag])
  }

  const handleMerge = async () => {
    if (!detail || selectedIds.length === 0) return
    const sources = selectedIds.filter((id) => id !== detail.id)
    if (sources.length === 0) {
      setError('Seleccioná al menos otro requerimiento para unir.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await triageApi.mergeRequests(detail.id, sources)
      setSelectedIds([])
      await loadPage()
      await loadDetail(detail.id)
      setToast('Requerimientos unidos')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron unir los requerimientos')
    } finally {
      setBusy(false)
    }
  }

  const handleReply = async (payload: {
    to: string[]
    cc: string[]
    bodyText: string
    bodyHtml?: string
  }) => {
    if (!detail) return
    setBusy(true)
    setError(null)
    try {
      const key = crypto.randomUUID().replace(/-/g, '')
      await triageApi.replyRequest(detail.id, payload, key)
      await loadDetail(detail.id)
      setToast('Respuesta encolada para envío')
      setTimeout(() => setToast(''), 3000)
    } finally {
      setBusy(false)
    }
  }

  const handleParseDenuncia = async () => {
    if (!detail || !selectedPdfAttachmentId) return
    setParseBusy(true)
    setError(null)
    try {
      const parsed = await triageApi.parseDenuncia(detail.id, selectedPdfAttachmentId)
      if (parsed.nombreTrabajador) setPaciente(parsed.nombreTrabajador)
      if (parsed.dniCuil) setDni(parsed.dniCuil)
      if (parsed.numeroSiniestro) setNumeroSiniestro(parsed.numeroSiniestro)
      if (parsed.telefonoPaciente) setTelefonoPaciente(parsed.telefonoPaciente)
      if (parsed.emailPaciente) setEmailPaciente(parsed.emailPaciente)
      if (parsed.descripcionCap && !triageNote.trim()) setTriageNote(parsed.descripcionCap)
      setToast('Datos extraídos del PDF de denuncia')
      setTimeout(() => setToast(''), 3000)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo parsear la denuncia')
    } finally {
      setParseBusy(false)
    }
  }

  const changeView = (next: 'pending' | 'ignored') => {
    setView(next)
    setPage(1)
    setSelectedId('')
    setSelectedIds([])
  }

  const changeTagFilter = (tag: string) => {
    setTagFilter(tag)
    setPage(1)
    setSelectedId('')
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Bandeja de Triage"
        subtitle="Requerimientos conversacionales · agrupados por hilo Gmail · unión manual"
        action={
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => void handleSync()}
              disabled={busy}
              className="btn-primary flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50"
            >
              <RefreshCw size={16} className={busy ? 'animate-spin' : ''} />
              Sincronizar Gmail
            </button>
            <button
              type="button"
              onClick={() => void handleBackfill()}
              disabled={busy}
              className="flex items-center gap-2 rounded-full bg-white px-4 py-2 text-sm font-semibold text-auditart-navy shadow-sm ring-1 ring-gray-200 hover:bg-gray-50 disabled:opacity-50"
            >
              Agrupar existentes
            </button>
            <button
              type="button"
              onClick={() => void handleSeed()}
              disabled={busy}
              className="flex items-center gap-2 rounded-full bg-white px-4 py-2 text-sm font-semibold text-auditart-navy shadow-sm ring-1 ring-gray-200 hover:bg-gray-50 disabled:opacity-50"
            >
              <Sparkles size={16} className="text-auditart-blue" />
              Demo
            </button>
            <div className="flex items-center gap-2 rounded-full bg-auditart-blue/8 px-4 py-2 text-sm font-semibold text-auditart-blue ring-1 ring-auditart-blue/15">
              <Mail size={16} />
              {pendingCount} pendientes
            </div>
          </div>
        }
      />

      {toast && (
        <div className="toast-success mb-5 flex items-center gap-2 px-4 py-3 text-sm font-semibold">
          <CheckCircle2 size={16} />
          {toast}
        </div>
      )}
      {error && (
        <div className="mb-5 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      {listLoading && (
        <div className="mb-4 flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando…
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-5">
        <div className="space-y-3 lg:col-span-2">
          <div className="card-flat p-4">
            <div className="mb-3 grid grid-cols-2 rounded-xl bg-gray-100 p-1">
              <button
                type="button"
                onClick={() => changeView('pending')}
                className={`rounded-lg px-3 py-2 text-xs font-bold transition-colors ${
                  view === 'pending'
                    ? 'bg-white text-auditart-blue shadow-sm'
                    : 'text-auditart-gray'
                }`}
              >
                Pendientes ({pendingCount})
              </button>
              <button
                type="button"
                onClick={() => changeView('ignored')}
                className={`rounded-lg px-3 py-2 text-xs font-bold transition-colors ${
                  view === 'ignored'
                    ? 'bg-white text-auditart-blue shadow-sm'
                    : 'text-auditart-gray'
                }`}
              >
                Ignorados ({ignoredCount})
              </button>
            </div>

            <select
              value={tagFilter}
              onChange={(e) => changeTagFilter(e.target.value)}
              className="input-modern mb-3 w-full px-3 py-2 text-xs"
            >
              <option value="">Todos los tags</option>
              {availableTags.map((tag) => (
                <option key={tag} value={tag}>
                  #{tag}
                </option>
              ))}
            </select>

            {selectedIds.length > 0 && view === 'pending' && (
              <button
                type="button"
                onClick={() => void handleMerge()}
                disabled={busy || !detail}
                className="mb-3 flex w-full items-center justify-center gap-2 rounded-xl bg-violet-50 px-3 py-2 text-xs font-bold text-violet-700 ring-1 ring-violet-200 disabled:opacity-50"
              >
                <GitMerge size={14} />
                Unir {selectedIds.length} con el seleccionado
              </button>
            )}

            <p className="mb-3 text-xs font-semibold text-auditart-muted">
              {total} requerimiento(s) · página {page} de {totalPages}
            </p>

            {requests.length === 0 ? (
              <p className="py-8 text-center text-sm text-auditart-muted">
                {view === 'pending'
                  ? 'No hay requerimientos pendientes. Probá “Agrupar existentes”.'
                  : 'No hay requerimientos ignorados.'}
              </p>
            ) : (
              <div className="space-y-2">
                {requests.map((request) => {
                  const isUrgent = request.subject.toLowerCase().includes('urgent')
                  const active = selectedId === request.id
                  const checked = selectedIds.includes(request.id)
                  return (
                    <div
                      key={request.id}
                      className={`rounded-xl border-2 p-4 transition-all ${
                        active
                          ? 'border-auditart-blue bg-auditart-blue/4 shadow-md'
                          : 'border-transparent bg-gray-50 hover:border-auditart-blue/20'
                      }`}
                    >
                      <div className="mb-2 flex items-center justify-between gap-2">
                        <label className="flex items-center gap-2 text-[11px] text-auditart-muted">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => toggleSelect(request.id)}
                            onClick={(e) => e.stopPropagation()}
                          />
                          {formatDateTime(request.lastMessageAt)}
                        </label>
                        {isUrgent && (
                          <span className="flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-[10px] font-bold text-red-600">
                            <Zap size={9} /> URGENTE
                          </span>
                        )}
                      </div>
                      <button
                        type="button"
                        onClick={() => setSelectedId(request.id)}
                        className="w-full text-left"
                      >
                        <p className="text-sm font-semibold text-auditart-navy">
                          {request.subject}
                        </p>
                        <p className="mt-1 truncate text-xs text-auditart-muted">
                          {request.participants.slice(0, 3).join(', ') || 'Sin participantes'}
                        </p>
                        <div className="mt-2 flex flex-wrap gap-2 text-[10px] font-semibold text-auditart-blue">
                          <span className="rounded-full bg-white px-2 py-0.5 ring-1 ring-gray-200">
                            {request.messageCount} msg
                          </span>
                          {request.attachmentCount > 0 && (
                            <span className="flex items-center gap-1 rounded-full bg-white px-2 py-0.5 ring-1 ring-gray-200">
                              <Paperclip size={10} />
                              {request.attachmentCount}
                            </span>
                          )}
                        </div>
                        {request.tags.length > 0 && (
                          <div className="mt-2 flex flex-wrap gap-1">
                            {request.tags.slice(0, 3).map((tag) => (
                              <span
                                key={tag}
                                className="rounded-full bg-auditart-blue/8 px-2 py-0.5 text-[10px] font-semibold text-auditart-blue"
                              >
                                #{tag}
                              </span>
                            ))}
                          </div>
                        )}
                      </button>
                    </div>
                  )
                })}
              </div>
            )}

            <div className="mt-4 flex items-center justify-between border-t border-gray-100 pt-3">
              <button
                type="button"
                onClick={() => setPage((value) => Math.max(1, value - 1))}
                disabled={page <= 1 || listLoading}
                className="flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs font-semibold text-auditart-gray hover:bg-gray-100 disabled:opacity-30"
              >
                <ChevronLeft size={14} /> Anterior
              </button>
              <span className="text-xs text-auditart-muted">
                {page} / {totalPages}
              </span>
              <button
                type="button"
                onClick={() => setPage((value) => Math.min(totalPages, value + 1))}
                disabled={page >= totalPages || listLoading}
                className="flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs font-semibold text-auditart-gray hover:bg-gray-100 disabled:opacity-30"
              >
                Siguiente <ChevronRight size={14} />
              </button>
            </div>
          </div>
        </div>

        <div className="lg:col-span-3">
          {detailLoading && (
            <div className="mb-3 flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando conversación…
            </div>
          )}
          {detail ? (
            <div className="card-flat overflow-hidden">
              <div className="border-b border-gray-100 bg-gradient-to-r from-auditart-blue/5 to-transparent p-6">
                <h3 className="text-xl font-bold text-auditart-navy">{detail.subject}</h3>
                <div className="mt-3 flex flex-wrap gap-3 text-sm text-auditart-gray">
                  <span className="rounded-lg bg-violet-50 px-2.5 py-1 text-xs font-semibold text-violet-700 ring-1 ring-violet-200">
                    Canal: {CHANNEL_LABELS[detail.channel]}
                  </span>
                  <span className="rounded-lg bg-white px-2.5 py-1 text-xs font-medium ring-1 ring-gray-200">
                    {detail.messageCount} mensajes
                  </span>
                  <span className="text-xs">{formatDateTime(detail.lastMessageAt)}</span>
                  {detail.attachmentCount > 0 && (
                    <span className="flex items-center gap-1 rounded-lg bg-auditart-blue/8 px-2.5 py-1 text-xs font-medium text-auditart-blue">
                      <Paperclip size={12} />
                      {detail.attachmentCount} adjunto(s)
                    </span>
                  )}
                </div>
                {detail.participants.length > 0 && (
                  <p className="mt-2 text-xs text-auditart-muted">
                    Participantes: {detail.participants.join(', ')}
                  </p>
                )}
                <div className="mt-4">
                  {detail.state === 'ignored' ? (
                    <button
                      type="button"
                      onClick={() => void handleRestore()}
                      disabled={busy}
                      className="flex items-center gap-2 rounded-xl bg-emerald-50 px-3 py-2 text-xs font-bold text-emerald-700 ring-1 ring-emerald-200 disabled:opacity-50"
                    >
                      <RotateCcw size={14} /> Restaurar a pendientes
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={() => void handleIgnore()}
                      disabled={busy}
                      className="flex items-center gap-2 rounded-xl bg-gray-100 px-3 py-2 text-xs font-bold text-auditart-gray ring-1 ring-gray-200 hover:bg-red-50 hover:text-red-700 disabled:opacity-50"
                    >
                      <Trash2 size={14} /> Ignorar requerimiento
                    </button>
                  )}
                </div>
              </div>

              <div className="space-y-5 p-6">
                <div className="rounded-2xl border border-gray-200 bg-gray-50/70 p-4">
                  <p className="mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-auditart-muted">
                    <Tag size={13} /> Tags
                  </p>
                  <div className="mb-3 flex min-h-7 flex-wrap gap-1.5">
                    {detail.tags.length === 0 && (
                      <span className="text-xs text-auditart-muted">Sin tags</span>
                    )}
                    {detail.tags.map((tag) => (
                      <span
                        key={tag}
                        className="flex items-center gap-1 rounded-full bg-auditart-blue/10 px-2.5 py-1 text-xs font-semibold text-auditart-blue"
                      >
                        #{tag}
                        <button
                          type="button"
                          onClick={() =>
                            void saveTags(detail.tags.filter((value) => value !== tag))
                          }
                          disabled={busy}
                          aria-label={`Quitar tag ${tag}`}
                        >
                          <X size={12} />
                        </button>
                      </span>
                    ))}
                  </div>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={tagInput}
                      onChange={(e) => setTagInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.preventDefault()
                          void handleAddTag()
                        }
                      }}
                      maxLength={40}
                      placeholder="Ej: telemedicina, spam, ART"
                      className="input-modern min-w-0 flex-1 px-3 py-2 text-sm"
                    />
                    <button
                      type="button"
                      onClick={() => void handleAddTag()}
                      disabled={busy || !tagInput.trim()}
                      className="flex items-center gap-1 rounded-xl bg-auditart-blue px-3 py-2 text-xs font-bold text-white disabled:opacity-40"
                    >
                      <Plus size={14} /> Agregar
                    </button>
                  </div>
                </div>

                <div>
                  <p className="mb-3 text-xs font-bold uppercase tracking-wider text-auditart-muted">
                    Cronología
                  </p>
                  <EmailTimeline messages={detail.messages} onError={setError} />
                </div>

                <EmailAttachmentsList
                  attachments={detail.attachments}
                  title="Todos los adjuntos"
                  onError={setError}
                />

                {detail.state !== 'ignored' && (
                  <EmailReplyComposer
                    defaults={detail.replyDefaults}
                    busy={busy}
                    onSend={handleReply}
                  />
                )}

                {detail.state !== 'ignored' && (
                  <div className="rounded-2xl bg-auditart-light p-4 ring-1 ring-auditart-blue/8">
                    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                      <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
                        Datos del servicio (editá antes de derivar)
                      </p>
                      <div className="flex flex-wrap items-center gap-2">
                        {pdfAttachments.length > 0 ? (
                          <select
                            value={selectedPdfAttachmentId}
                            onChange={(e) => setSelectedPdfAttachmentId(e.target.value)}
                            disabled={parseBusy || busy}
                            className="input-modern max-w-[220px] px-3 py-1.5 text-xs"
                            title="PDF de referencia para autocompletar"
                          >
                            {pdfAttachments.map((attachment) => (
                              <option key={attachment.id} value={attachment.id}>
                                {attachment.fileName}
                              </option>
                            ))}
                          </select>
                        ) : (
                          <span className="text-xs text-auditart-muted">
                            Sin PDF en el requerimiento
                          </span>
                        )}
                        <button
                          type="button"
                          onClick={() => void handleParseDenuncia()}
                          disabled={parseBusy || busy || !selectedPdfAttachmentId}
                          className="flex items-center gap-1 rounded-lg bg-white px-3 py-1.5 text-xs font-bold text-auditart-blue ring-1 ring-auditart-blue/20 hover:bg-auditart-blue/5 disabled:opacity-50"
                        >
                          {parseBusy ? (
                            <Loader2 size={13} className="animate-spin" />
                          ) : (
                            <Sparkles size={13} />
                          )}
                          Parsear denuncia
                        </button>
                      </div>
                    </div>
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          Paciente
                        </span>
                        <input
                          type="text"
                          value={paciente}
                          onChange={(e) => setPaciente(e.target.value)}
                          placeholder="Nombre y apellido"
                          className="input-modern w-full px-3 py-2 text-sm"
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          DNI
                        </span>
                        <input
                          type="text"
                          value={dni}
                          onChange={(e) => setDni(e.target.value)}
                          placeholder="Sin puntos"
                          className="input-modern w-full px-3 py-2 text-sm"
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          ART (aseguradora)
                        </span>
                        <input
                          type="text"
                          value={art}
                          onChange={(e) => setArt(e.target.value)}
                          placeholder="Ej: La Caja ART"
                          className="input-modern w-full px-3 py-2 text-sm"
                          required
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          N° de siniestro
                        </span>
                        <input
                          type="text"
                          value={numeroSiniestro}
                          onChange={(e) => setNumeroSiniestro(e.target.value)}
                          placeholder="Ej: 870595"
                          className="input-modern w-full px-3 py-2 text-sm"
                          required
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          Teléfono paciente
                        </span>
                        <input
                          type="text"
                          value={telefonoPaciente}
                          onChange={(e) => setTelefonoPaciente(e.target.value)}
                          placeholder="Sin espacios"
                          className="input-modern w-full px-3 py-2 text-sm"
                          required
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          Correo paciente
                        </span>
                        <input
                          type="email"
                          value={emailPaciente}
                          onChange={(e) => setEmailPaciente(e.target.value)}
                          placeholder="paciente@ejemplo.com"
                          className="input-modern w-full px-3 py-2 text-sm"
                          required
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          Tipo de servicio
                        </span>
                        <select
                          value={tipoServicio}
                          onChange={(e) => setTipoServicio(e.target.value as ServiceType)}
                          className="input-modern w-full px-3 py-2 text-sm"
                        >
                          {Object.entries(SERVICE_LABELS).map(([value, label]) => (
                            <option key={value} value={value}>
                              {label}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          Especialidad
                        </span>
                        <input
                          type="text"
                          value={especialidad}
                          onChange={(e) => setEspecialidad(e.target.value)}
                          placeholder="Ej: Traumatología"
                          className="input-modern w-full px-3 py-2 text-sm"
                        />
                      </label>
                      <label className="block">
                        <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                          Urgencia
                        </span>
                        <select
                          value={urgency}
                          onChange={(e) => setUrgency(e.target.value as UrgencyLevel)}
                          className="input-modern w-full px-3 py-2 text-sm"
                        >
                          {Object.entries(URGENCY_LABELS).map(([value, label]) => (
                            <option key={value} value={value}>
                              {label}
                            </option>
                          ))}
                        </select>
                      </label>
                    </div>
                  </div>
                )}
              </div>

              {detail.state !== 'ignored' && (
                <div className="border-t border-gray-100 bg-gray-50/50 p-6">
                  <p className="mb-4 flex items-center gap-2 text-sm font-bold text-auditart-navy">
                    <Users size={16} className="text-auditart-blue" />
                    Derivar (elegí cola y operador)
                  </p>

                  <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
                    {QUEUES.map((q) => (
                      <button
                        key={q.key}
                        type="button"
                        onClick={() => setSelectedQueue(q.key)}
                        className={`rounded-2xl border-2 p-4 text-left transition-all ${
                          selectedQueue === q.key
                            ? 'border-auditart-blue bg-auditart-blue/4'
                            : 'border-gray-200 bg-white'
                        }`}
                      >
                        <span className="text-2xl">{q.icon}</span>
                        <p className="mt-2 text-sm font-bold">{QUEUE_LABELS[q.key]}</p>
                        <p className="text-xs text-auditart-muted">{q.desc}</p>
                      </button>
                    ))}
                  </div>

                  <label className="mb-2 block text-xs font-bold uppercase tracking-wider text-auditart-muted">
                    Operador · {queueHint}
                  </label>
                  <select
                    value={operadorId}
                    onChange={(e) => setOperadorId(e.target.value)}
                    className="input-modern mb-4 w-full px-3 py-2.5 text-sm"
                  >
                    {operators.map((op) => (
                      <option key={op.id} value={op.id}>
                        {op.name}
                      </option>
                    ))}
                  </select>

                  <label className="mb-4 block">
                    <span className="mb-1 block text-xs font-bold uppercase tracking-wider text-auditart-muted">
                      Nota de triage <span className="text-red-500">*</span>
                    </span>
                    <textarea
                      value={triageNote}
                      onChange={(e) => setTriageNote(e.target.value)}
                      required
                      rows={3}
                      placeholder="Motivo de derivación, contexto clínico, observaciones…"
                      className="input-modern w-full px-3 py-2 text-sm"
                    />
                  </label>

                  <label className="mb-4 block">
                    <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                      Adjunto opcional al derivar
                    </span>
                    <input
                      type="file"
                      onChange={(e) => setAssignAttachment(e.target.files?.[0] ?? null)}
                      className="block w-full text-xs text-auditart-gray"
                    />
                  </label>

                  <button
                    type="button"
                    onClick={() => void handleAssign()}
                    disabled={busy || !operadorId || !triageNote.trim()}
                    className="btn-primary flex items-center gap-2 disabled:opacity-50"
                  >
                    {busy ? (
                      <Loader2 size={16} className="animate-spin" />
                    ) : (
                      <ArrowRight size={16} />
                    )}
                    Confirmar derivación
                  </button>
                </div>
              )}
            </div>
          ) : (
            <div className="card-flat flex h-80 flex-col items-center justify-center text-auditart-muted">
              <Mail size={40} className="mb-3 opacity-20" />
              <p className="font-medium">Sin requerimiento seleccionado</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
