import {
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Loader2,
  MessageCircle,
  Upload,
} from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  mapRequestDetail,
  mapService,
  prestadoresApi,
  preciosApi,
  servicesApi,
  usersApi,
  type PrecioOptionDto,
  type PrestadorOptionDto,
  type ServiceAttachmentDto,
} from '../api/auditartApi'
import { EmailAttachmentsList } from '../components/email/EmailAttachmentsList'
import { EmailReplyComposer } from '../components/email/EmailReplyComposer'
import { RichTextEditor } from '../components/email/RichTextEditor'
import { EmailTimeline } from '../components/email/EmailTimeline'
import { PrestadorDetailModal } from '../components/prestadores/PrestadorDetailModal'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAuth } from '../context/useAuth'
import { useAudits } from '../context/useAudits'
import {
  CHRONIC_PERIODICITY_LABELS,
  QUEUE_LABELS,
  SERVICE_LABELS,
  STATUS_LABELS,
  type AuditRecord,
  type AuditStatus,
  type EmailRequestDetail,
  type TipoProfesional,
} from '../types'
import { formatDateTime, formatCurrency } from '../utils/format'

/** 50 = +50% (base×1.5), 100 = +100% (base×2). No es % DEL precio. */
function conciliadoEspecialista(base: number, pct: 50 | 100): number {
  return Math.round(base * (1 + pct / 100) * 100) / 100
}

const TRANSITIONS: Record<
  AuditStatus,
  { next: AuditStatus; label: string; condition?: string }[]
> = {
  rojo: [{ next: 'amarillo', label: 'Profesional asignado y turno coordinado' }],
  amarillo: [
    {
      next: 'azul',
      label: 'Consulta realizada — esperar autorización',
      condition: 'Si la autorización ya está cargada, podés ir directo a Verde',
    },
    {
      next: 'verde',
      label: 'Autorización ya cargada — pasar a Verde',
      condition: 'Solo si hay código/radicado o PDF',
    },
  ],
  azul: [
    {
      next: 'verde',
      label: 'Autorización lista — pasar a facturación',
      condition: 'Si falta autorización, se pide al confirmar',
    },
  ],
  verde: [
    {
      next: 'celeste',
      label: 'Pago realizado — cerrar prestación',
    },
  ],
  celeste: [],
}

export function AuditDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { permissions } = useAuth()
  const { audits, updateAuditStatus, refresh } = useAudits()
  const [toast, setToast] = useState('')
  const [correspondence, setCorrespondence] = useState<EmailRequestDetail | null>(null)
  const [corrLoading, setCorrLoading] = useState(false)
  const [corrError, setCorrError] = useState<string | null>(null)
  const [replyBusy, setReplyBusy] = useState(false)
  const [composeTo, setComposeTo] = useState('')
  const [composeCc, setComposeCc] = useState('')
  const [composeSubject, setComposeSubject] = useState('')
  const [composeBodyHtml, setComposeBodyHtml] = useState('')
  const [composeBodyText, setComposeBodyText] = useState('')
  const [composeBusy, setComposeBusy] = useState(false)
  const [composeFiles, setComposeFiles] = useState<File[]>([])
  const [composeSelectedIds, setComposeSelectedIds] = useState<string[]>([])
  const [assignOperadorId, setAssignOperadorId] = useState('')
  const [chronicOperators, setChronicOperators] = useState<
    { id: string; name: string }[]
  >([])
  const [assignBusy, setAssignBusy] = useState(false)
  const [turnoModalOpen, setTurnoModalOpen] = useState(false)
  const [turnoFechaLocal, setTurnoFechaLocal] = useState('')
  const [prestadorQuery, setPrestadorQuery] = useState('')
  const [prestadorOptions, setPrestadorOptions] = useState<PrestadorOptionDto[]>([])
  const [selectedPrestadorId, setSelectedPrestadorId] = useState('')
  const [turnoBusy, setTurnoBusy] = useState(false)
  const [turnoError, setTurnoError] = useState<string | null>(null)
  const [authModalOpen, setAuthModalOpen] = useState(false)
  const [authModalMode, setAuthModalMode] = useState<'save' | 'verde'>('save')
  const [autorizacionCodigo, setAutorizacionCodigo] = useState('')
  const [autorizacionPdf, setAutorizacionPdf] = useState<File | null>(null)
  const [azulBusy, setAzulBusy] = useState(false)
  const [azulError, setAzulError] = useState<string | null>(null)
  const [tardiaBusy, setTardiaBusy] = useState(false)
  const [detailPrestadorId, setDetailPrestadorId] = useState<string | null>(null)
  const [serviceAttachments, setServiceAttachments] = useState<ServiceAttachmentDto[]>([])
  const [uploadBusy, setUploadBusy] = useState(false)
  const [deletingAttachmentId, setDeletingAttachmentId] = useState<string | null>(null)
  const [fetchedAudit, setFetchedAudit] = useState<AuditRecord | null>(null)
  const [auditLoading, setAuditLoading] = useState(false)
  const [preciosModalOpen, setPreciosModalOpen] = useState(false)
  const [precioTipo, setPrecioTipo] = useState<TipoProfesional>('especialista')
  const [precioQuery, setPrecioQuery] = useState('')
  const [precioOptions, setPrecioOptions] = useState<PrecioOptionDto[]>([])
  const [selectedPrecioId, setSelectedPrecioId] = useState('')
  const [catalogBaseValor, setCatalogBaseValor] = useState<number | null>(null)
  const [pctEspecialista, setPctEspecialista] = useState<50 | 100>(100)
  const [valorConsultaInput, setValorConsultaInput] = useState('')
  const [valorConciliadoInput, setValorConciliadoInput] = useState('')
  const [preciosBusy, setPreciosBusy] = useState(false)
  const [preciosError, setPreciosError] = useState<string | null>(null)

  const audit = audits.find((a) => a.id === id) ?? fetchedAudit

  const loadServiceAttachments = async (serviceId: string) => {
    try {
      setServiceAttachments(await servicesApi.listAttachments(serviceId))
    } catch {
      setServiceAttachments([])
    }
  }

  useEffect(() => {
    if (!permissions.triage) return
    void usersApi.operators('cronicos').then((list) =>
      setChronicOperators(list.map((u) => ({ id: u.id, name: u.name }))),
    )
  }, [permissions.triage])

  useEffect(() => {
    if (!id) return
    void loadServiceAttachments(id)
  }, [id])

  useEffect(() => {
    if (!id) return
    let cancelled = false
    setFetchedAudit(null)
    setAuditLoading(true)
    void servicesApi
      .get(id)
      .then((dto) => {
        if (cancelled) return
        setFetchedAudit(mapService(dto))
      })
      .catch(() => {
        if (!cancelled) setFetchedAudit(null)
      })
      .finally(() => {
        if (!cancelled) setAuditLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [id])

  useEffect(() => {
    if (!audit || audit.status !== 'rojo') return
    setPrecioTipo(audit.tipoProfesional ?? 'especialista')
    setSelectedPrecioId(audit.precioCatalogoId ?? '')
    setPctEspecialista(audit.porcentajeConciliacionEspecialista === 50 ? 50 : 100)
    setCatalogBaseValor(null)
    setValorConsultaInput(
      audit.valorPactado != null ? String(audit.valorPactado) : '',
    )
    setValorConciliadoInput(
      audit.valorConciliadoArt != null ? String(audit.valorConciliadoArt) : '',
    )
  }, [
    audit?.id,
    audit?.status,
    audit?.tipoProfesional,
    audit?.precioCatalogoId,
    audit?.porcentajeConciliacionEspecialista,
    audit?.valorPactado,
    audit?.valorConciliadoArt,
  ])

  useEffect(() => {
    if (!preciosModalOpen || !audit || audit.status !== 'rojo') return
    let cancelled = false
    void preciosApi
      .options({
        tipo: precioTipo,
        art: precioTipo === 'auditor' ? audit.art : undefined,
        q: precioQuery.trim() || undefined,
      })
      .then((list) => {
        if (!cancelled) setPrecioOptions(list)
      })
      .catch(() => {
        if (!cancelled) setPrecioOptions([])
      })
    return () => {
      cancelled = true
    }
  }, [preciosModalOpen, audit?.id, audit?.status, audit?.art, precioTipo, precioQuery])

  useEffect(() => {
    if (!id) return
    let cancelled = false
    setCorrLoading(true)
    setCorrError(null)
    void servicesApi
      .getCorrespondence(id)
      .then((dto) => {
        if (!cancelled) setCorrespondence(mapRequestDetail(dto))
      })
      .catch((e) => {
        if (!cancelled) {
          setCorrespondence(null)
          setCorrError(e instanceof Error ? e.message : 'Sin conversación vinculada')
        }
      })
      .finally(() => {
        if (!cancelled) setCorrLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [id])

  if (!audit) {
    if (auditLoading) {
      return (
        <div className="flex items-center justify-center gap-2 py-20 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando prestación…
        </div>
      )
    }
    return (
      <div className="flex flex-col items-center py-20 text-auditart-gray">
        <p className="font-medium">Servicio no encontrado</p>
        <Link
          to="/tablero"
          className="mt-4 flex items-center gap-1 text-sm font-semibold text-auditart-blue hover:underline"
        >
          <ArrowLeft size={14} /> Volver al tablero
        </Link>
      </div>
    )
  }

  const transitions = TRANSITIONS[audit.status]

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(''), 3000)
  }

  const handleTransition = async (next: AuditStatus) => {
    try {
      if (next === 'amarillo') {
        const tomorrow = new Date(Date.now() + 86400000)
        const pad = (n: number) => String(n).padStart(2, '0')
        setTurnoFechaLocal(
          `${tomorrow.getFullYear()}-${pad(tomorrow.getMonth() + 1)}-${pad(tomorrow.getDate())}T${pad(tomorrow.getHours())}:${pad(tomorrow.getMinutes())}`,
        )
        setSelectedPrestadorId(audit.prestadorId ?? '')
        setPrestadorQuery(audit.profesional ?? '')
        setTurnoError(null)
        setTurnoModalOpen(true)
        void prestadoresApi
          .active({ q: audit.profesional, take: 40 })
          .then(setPrestadorOptions)
          .catch(() => setPrestadorOptions([]))
        return
      }

      if (next === 'verde') {
        const hasAuth =
          audit.autorizacionART ||
          Boolean(audit.autorizacionCodigo) ||
          Boolean(audit.autorizacionDocumentoAttachmentId)
        if (!hasAuth) {
          setAutorizacionCodigo(audit.autorizacionCodigo ?? '')
          setAutorizacionPdf(null)
          setAzulError(null)
          setAuthModalMode('verde')
          setAuthModalOpen(true)
          return
        }
      }

      await updateAuditStatus(audit.id, next)

      showToast(`Estado actualizado a ${STATUS_LABELS[next]}`)
    } catch (e) {
      showToast(e instanceof Error ? e.message : 'Error al cambiar estado')
    }
  }

  const searchPrestadores = async (term: string) => {
    setPrestadorQuery(term)
    try {
      setPrestadorOptions(await prestadoresApi.active({ q: term.trim() || undefined, take: 40 }))
    } catch {
      setPrestadorOptions([])
    }
  }

  const confirmTurnoAmarillo = async () => {
    if (!turnoFechaLocal) {
      setTurnoError('Indicá fecha y hora del turno.')
      return
    }
    const selected = prestadorOptions.find((p) => p.id === selectedPrestadorId)
    if (!selected && !prestadorQuery.trim()) {
      setTurnoError('Seleccioná un profesional de la cartilla o escribí el nombre.')
      return
    }
    setTurnoBusy(true)
    setTurnoError(null)
    try {
      const fechaTurnoUtc = new Date(turnoFechaLocal).toISOString()
      await updateAuditStatus(audit.id, 'amarillo', {
        fechaTurnoUtc,
        profesional: selected?.nombre ?? prestadorQuery.trim(),
        prestadorId: selected?.id,
      })
      setTurnoModalOpen(false)
      showToast(`Estado actualizado a ${STATUS_LABELS.amarillo}`)
    } catch (e) {
      setTurnoError(e instanceof Error ? e.message : 'No se pudo coordinar el turno')
    } finally {
      setTurnoBusy(false)
    }
  }


  const applyPctPreview = (pct: 50 | 100) => {
    setPctEspecialista(pct)
    if (catalogBaseValor != null) {
      setValorConciliadoInput(String(conciliadoEspecialista(catalogBaseValor, pct)))
    }
  }

  const openPreciosModal = () => {
    setPreciosError(null)
    setPreciosModalOpen(true)
  }

  const savePreciosRojo = async () => {
    const valorConsulta = valorConsultaInput.trim()
      ? Number(valorConsultaInput.replace(',', '.'))
      : undefined
    const valorConciliado = valorConciliadoInput.trim()
      ? Number(valorConciliadoInput.replace(',', '.'))
      : undefined
    if (valorConsulta != null && Number.isNaN(valorConsulta)) {
      setPreciosError('Valor de consulta inválido.')
      return
    }
    if (valorConciliado != null && Number.isNaN(valorConciliado)) {
      setPreciosError('Precio conciliado inválido.')
      return
    }
    if (valorConciliado == null && !selectedPrecioId) {
      setPreciosError('Elegí un concepto del catálogo o cargá el precio conciliado.')
      return
    }
    setPreciosBusy(true)
    setPreciosError(null)
    try {
      const dto = await servicesApi.setPrecios(audit.id, {
        tipoProfesional: precioTipo,
        valorConsulta,
        valorConciliadoArt: valorConciliado,
        precioCatalogoId: selectedPrecioId || undefined,
        porcentajeConciliacionEspecialista:
          precioTipo === 'especialista' ? pctEspecialista : undefined,
      })
      setFetchedAudit(mapService(dto))
      await refresh()
      setPreciosModalOpen(false)
      showToast('Precios guardados')
    } catch (e) {
      setPreciosError(e instanceof Error ? e.message : 'No se pudieron guardar los precios')
    } finally {
      setPreciosBusy(false)
    }
  }

  const submitAutorizacion = async () => {
    const codigo = autorizacionCodigo.trim()
    if (!codigo && !autorizacionPdf && !audit.autorizacionDocumentoAttachmentId && !audit.autorizacionCodigo) {
      setAzulError('Indicá un código/radicado, un PDF, o ambos.')
      return
    }
    setAzulBusy(true)
    setAzulError(null)
    try {
      let attachmentId = audit.autorizacionDocumentoAttachmentId
      if (autorizacionPdf) {
        const uploaded = await servicesApi.uploadAttachments(audit.id, [autorizacionPdf])
        attachmentId = uploaded[0]?.id ?? attachmentId
        await loadServiceAttachments(audit.id)
      }
      if (authModalMode === 'verde') {
        await updateAuditStatus(audit.id, 'verde', {
          autorizacionCodigo: codigo || undefined,
          autorizacionDocumentoAttachmentId: attachmentId,
        })
        showToast(`Estado actualizado a ${STATUS_LABELS.verde}`)
      } else {
        await servicesApi.setAutorizacion(audit.id, {
          autorizacionCodigo: codigo || undefined,
          autorizacionDocumentoAttachmentId: attachmentId,
        })
        await refresh()
        showToast('Autorización cargada')
      }
      setAuthModalOpen(false)
    } catch (e) {
      setAzulError(e instanceof Error ? e.message : 'No se pudo guardar la autorización')
    } finally {
      setAzulBusy(false)
    }
  }

  const markTardia = async () => {
    setTardiaBusy(true)
    try {
      await servicesApi.markTardia(audit.id, { periodicity: 'monthly' })
      await refresh()
      showToast('Prestación marcada como coordinación tardía (mensual)')
    } catch (e) {
      showToast(e instanceof Error ? e.message : 'No se pudo marcar como tardía')
    } finally {
      setTardiaBusy(false)
    }
  }

  const handleSendNewEmail = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!id) return
    if (!composeBodyText.trim()) {
      showToast('Escribí el cuerpo del mensaje')
      return
    }
    setComposeBusy(true)
    try {
      const key = crypto.randomUUID().replace(/-/g, '')
      await servicesApi.sendEmail(
        id,
        {
          to: composeTo.split(/[,;]/).map((s) => s.trim()).filter(Boolean),
          cc: composeCc
            ? composeCc.split(/[,;]/).map((s) => s.trim()).filter(Boolean)
            : undefined,
          subject: composeSubject,
          bodyText: composeBodyText,
          bodyHtml: composeBodyHtml || undefined,
          existingAttachmentIds: composeSelectedIds,
          files: composeFiles,
        },
        key,
      )
      const dto = await servicesApi.getCorrespondence(id)
      setCorrespondence(mapRequestDetail(dto))
      setComposeTo('')
      setComposeCc('')
      setComposeSubject('')
      setComposeBodyHtml('')
      setComposeBodyText('')
      setComposeFiles([])
      setComposeSelectedIds([])
      await loadServiceAttachments(id)
      showToast('Email nuevo encolado para envío')
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'No se pudo enviar el email')
    } finally {
      setComposeBusy(false)
    }
  }

  const handleUploadDocuments = async (files: FileList | null) => {
    if (!id || !files?.length) return
    const allowed = /\.(pdf|docx?|xlsx?|jpe?g|png|gif|webp|bmp)$/i
    const maxBytes = 50 * 1024 * 1024
    const selected: File[] = []
    for (const file of Array.from(files)) {
      if (!allowed.test(file.name)) {
        showToast(`Tipo no permitido: ${file.name}`)
        return
      }
      if (file.size > maxBytes) {
        showToast(`${file.name} supera 50 MB`)
        return
      }
      selected.push(file)
    }
    setUploadBusy(true)
    try {
      await servicesApi.uploadAttachments(id, selected)
      await loadServiceAttachments(id)
      showToast(
        selected.length === 1
          ? 'Documento adjuntado'
          : `${selected.length} documentos adjuntados`,
      )
    } catch (e) {
      showToast(e instanceof Error ? e.message : 'No se pudieron adjuntar los archivos')
    } finally {
      setUploadBusy(false)
    }
  }

  const handleDeleteAttachment = async (attachmentId: string) => {
    if (!id) return
    if (!confirm('¿Quitar este archivo del caso? Seguirá disponible en el historial de correo si se envió.')) {
      return
    }
    setDeletingAttachmentId(attachmentId)
    try {
      await servicesApi.deleteAttachment(id, attachmentId)
      await loadServiceAttachments(id)
      showToast('Archivo quitado del caso')
    } catch (e) {
      showToast(e instanceof Error ? e.message : 'No se pudo eliminar el adjunto')
    } finally {
      setDeletingAttachmentId(null)
    }
  }

  const toggleComposeAttachment = (attachmentId: string) => {
    setComposeSelectedIds((prev) =>
      prev.includes(attachmentId)
        ? prev.filter((id) => id !== attachmentId)
        : [...prev, attachmentId],
    )
  }

  const onComposeFilesSelected = (files: FileList | null) => {
    if (!files?.length) return
    const maxBytes = 50 * 1024 * 1024
    const allowed = /\.(pdf|doc|docx|xls|xlsx|jpe?g|png|gif|webp|bmp)$/i
    const next: File[] = []
    for (const file of Array.from(files)) {
      if (!allowed.test(file.name)) {
        showToast(`Tipo no permitido: ${file.name}`)
        continue
      }
      if (file.size > maxBytes) {
        showToast(`${file.name} supera 50 MB`)
        continue
      }
      next.push(file)
    }
    if (next.length) setComposeFiles((prev) => [...prev, ...next])
  }

  const handleReply = async (payload: {
    to: string[]
    cc: string[]
    bodyText: string
    bodyHtml?: string
    files?: File[]
  }) => {
    if (!id) return
    setReplyBusy(true)
    try {
      const key = crypto.randomUUID().replace(/-/g, '')
      await servicesApi.reply(
        id,
        {
          to: payload.to,
          cc: payload.cc,
          bodyText: payload.bodyText,
          bodyHtml: payload.bodyHtml,
          files: payload.files,
        },
        key,
      )
      const dto = await servicesApi.getCorrespondence(id)
      setCorrespondence(mapRequestDetail(dto))
      await loadServiceAttachments(id)
      showToast('Respuesta encolada para envío')
    } finally {
      setReplyBusy(false)
    }
  }

  const handleAssignOperador = async () => {
    if (!id || !assignOperadorId) return
    setAssignBusy(true)
    try {
      await servicesApi.assignOperador(id, assignOperadorId)
      await refresh()
      showToast('Operador asignado')
    } catch (e) {
      showToast(e instanceof Error ? e.message : 'No se pudo asignar operador')
    } finally {
      setAssignBusy(false)
    }
  }

  const handleRenewChronic = async () => {
    if (!id) return
    try {
      await servicesApi.renewChronic(id)
      await refresh()
      showToast('Crónico renovado — vuelve a Rojo')
    } catch (e) {
      showToast(e instanceof Error ? e.message : 'No se pudo renovar')
    }
  }

  return (
    <div className="mx-auto max-w-4xl animate-fade-in">
      <Link
        to="/tablero"
        className="mb-6 inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-1.5 text-sm font-semibold text-auditart-blue shadow-sm ring-1 ring-gray-200 transition-all hover:shadow-md"
      >
        <ArrowLeft size={14} />
        Volver al tablero
      </Link>

      {toast && (
        <div className="toast-success mb-5 flex items-center gap-2 px-4 py-3 text-sm font-semibold">
          <CheckCircle2 size={16} />
          {toast}
        </div>
      )}

      <div className="card-flat overflow-hidden">
        {/* Header */}
        <div className="border-b border-gray-100 bg-gradient-to-r from-auditart-blue/6 to-transparent p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <div className="mb-2 flex items-center gap-2">
                <span className="rounded-lg bg-auditart-navy/5 px-2.5 py-1 font-mono text-xs font-bold text-auditart-navy">
                  #{audit.numero}
                </span>
                <StatusBadge status={audit.status} />
              </div>
              {audit.pacienteId ? (
                <Link
                  to={`/pacientes/${audit.pacienteId}`}
                  className="text-2xl font-bold text-auditart-navy hover:text-auditart-blue hover:underline"
                >
                  {audit.paciente}
                </Link>
              ) : (
                <h2 className="text-2xl font-bold text-auditart-navy">{audit.paciente}</h2>
              )}
              <p className="mt-1 text-sm text-auditart-gray">
                DNI {audit.dni} · {audit.art}
                {audit.isChronicPeriodic ? ' · Coordinación tardía' : ''}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              {(audit.valorPactado != null || audit.valorConciliadoArt != null) && (
                <>
                  {audit.valorPactado != null && (
                    <div className="rounded-2xl bg-auditart-blue/8 px-5 py-3 text-right ring-1 ring-auditart-blue/15">
                      <p className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
                        Valor consulta
                      </p>
                      <p className="text-xl font-extrabold text-auditart-blue">
                        {formatCurrency(audit.valorPactado)}
                      </p>
                    </div>
                  )}
                  {audit.valorConciliadoArt != null && (
                    <div className="rounded-2xl bg-emerald-50 px-5 py-3 text-right ring-1 ring-emerald-200">
                      <p className="text-[10px] font-bold uppercase tracking-wider text-emerald-700/70">
                        Precio conciliado ART
                        {audit.tipoProfesional === 'especialista' &&
                        audit.porcentajeConciliacionEspecialista
                          ? ` (+${audit.porcentajeConciliacionEspecialista}%)`
                          : ''}
                      </p>
                      <p className="text-xl font-extrabold text-emerald-800">
                        {formatCurrency(audit.valorConciliadoArt)}
                      </p>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </div>

        {/* Sticky CTAs */}
        <div className="sticky top-0 z-20 flex flex-wrap items-center gap-2 border-b border-gray-200 bg-white/95 px-4 py-3 shadow-sm backdrop-blur">
          {audit.status === 'rojo' && (
            <button
              type="button"
              onClick={openPreciosModal}
              className="rounded-xl bg-auditart-blue px-3.5 py-2 text-sm font-semibold text-white shadow-sm hover:bg-auditart-blue/90"
            >
              Negociar precios
            </button>
          )}
          {(audit.status === 'amarillo' || audit.status === 'azul') && (
            <button
              type="button"
              onClick={() => {
                setAutorizacionCodigo(audit.autorizacionCodigo ?? '')
                setAutorizacionPdf(null)
                setAzulError(null)
                setAuthModalMode('save')
                setAuthModalOpen(true)
              }}
              className="rounded-xl border border-auditart-blue/30 bg-auditart-blue/5 px-3.5 py-2 text-sm font-semibold text-auditart-blue hover:bg-auditart-blue/10"
            >
              Autorización
            </button>
          )}
          {transitions.map((t) => (
            <button
              key={t.next}
              type="button"
              onClick={() => void handleTransition(t.next)}
              className="inline-flex items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-3.5 py-2 text-sm font-semibold text-auditart-navy hover:border-auditart-blue hover:text-auditart-blue"
            >
              → {STATUS_LABELS[t.next]}
            </button>
          ))}
          {audit.status === 'rojo' && !audit.isChronicPeriodic && (
            <button
              type="button"
              disabled={tardiaBusy}
              onClick={() => void markTardia()}
              className="rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-2 text-sm font-semibold text-amber-800 hover:bg-amber-100 disabled:opacity-50"
            >
              {tardiaBusy ? 'Marcando…' : 'Marcar tardía'}
            </button>
          )}
        </div>

        <DetailAccordion title="Datos" defaultOpen>
        <div className="grid grid-cols-1 gap-0 md:grid-cols-2">
          <div className="space-y-0 border-b border-gray-100 p-6 md:border-b-0 md:border-r">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Datos del servicio
            </h3>
            <InfoRow label="Tipo" value={SERVICE_LABELS[audit.tipoServicio]} />
            <InfoRow label="Especialidad" value={audit.especialidad} />
            <InfoRow label="Cola" value={QUEUE_LABELS[audit.queue]} />
            <InfoRow label="Operador" value={audit.operador} />
            <div className="flex justify-between border-b border-gray-50 py-3 text-sm">
              <span className="text-auditart-muted">Profesional</span>
              {audit.prestadorId ? (
                <button
                  type="button"
                  onClick={() => setDetailPrestadorId(audit.prestadorId!)}
                  className="font-semibold text-auditart-blue hover:underline"
                >
                  {audit.profesional ?? 'Ver ficha'}
                </button>
              ) : (
                <span className="font-semibold text-auditart-navy">
                  {audit.profesional ?? 'Sin asignar'}
                </span>
              )}
            </div>
            <InfoRow
              label="Pago anticipado"
              value={audit.requierePagoAnticipado ? 'Sí' : 'No'}
            />
            <InfoRow label="Urgencia" value={audit.urgency} />
          </div>
          <div className="p-6">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Fechas y SLA
            </h3>
            <InfoRow label="Ingreso" value={formatDateTime(audit.fechaIngreso)} />
            <InfoRow label="Turno" value={formatDateTime(audit.fechaTurno)} />
            <InfoRow label="Consulta" value={formatDateTime(audit.fechaConsulta)} />
            <InfoRow
              label="SLA restante"
              value={
                audit.slaHoursRemaining != null
                  ? `${audit.slaHoursRemaining} horas`
                  : '—'
              }
              highlight={
                audit.slaHoursRemaining != null && audit.slaHoursRemaining <= 12
              }
            />
            <InfoRow label="Email origen" value={audit.emailOrigen ?? '—'} />
          </div>
        </div>
        </DetailAccordion>

        {audit.isChronicPeriodic && (
          <DetailAccordion title="Coordinación tardía" defaultOpen>
          <div className="bg-auditart-blue/4 p-6">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Crónico periódico
            </h3>
            <div className="grid gap-3 sm:grid-cols-2">
              <InfoRow
                label="Periodicidad"
                value={
                  audit.chronicPeriodicity
                    ? CHRONIC_PERIODICITY_LABELS[audit.chronicPeriodicity]
                    : '—'
                }
              />
              <InfoRow
                label="Próxima renovación"
                value={formatDateTime(audit.nextRenewalDue)}
              />
              <InfoRow
                label="Última renovación"
                value={formatDateTime(audit.lastRenewedAt)}
              />
              <InfoRow
                label="Ciclos completados"
                value={String(audit.chronicRenewalCount ?? 0)}
              />
            </div>
            {audit.needsOperadorAssignment && permissions.triage && (
              <div className="mt-4 flex flex-wrap items-end gap-3">
                <label className="block min-w-[200px]">
                  <span className="mb-1 block text-xs font-semibold text-auditart-gray">
                    Asignar operador crónicos
                  </span>
                  <select
                    value={assignOperadorId}
                    onChange={(e) => setAssignOperadorId(e.target.value)}
                    className="input-modern w-full px-3 py-2 text-sm"
                  >
                    <option value="">Seleccionar…</option>
                    {chronicOperators.map((op) => (
                      <option key={op.id} value={op.id}>{op.name}</option>
                    ))}
                  </select>
                </label>
                <button
                  type="button"
                  disabled={!assignOperadorId || assignBusy}
                  onClick={() => void handleAssignOperador()}
                  className="btn-primary px-4 py-2 text-sm disabled:opacity-50"
                >
                  Asignar
                </button>
              </div>
            )}
            {permissions.triage && audit.status === 'verde' && (
              <button
                type="button"
                onClick={() => void handleRenewChronic()}
                className="mt-4 rounded-xl bg-white px-4 py-2 text-sm font-semibold text-auditart-blue ring-1 ring-auditart-blue/20 hover:bg-auditart-blue/5"
              >
                Renovar manualmente (Rojo)
              </button>
            )}
          </div>
          </DetailAccordion>
        )}

        <DetailAccordion title="Documentación" defaultOpen>
        <div className="bg-gray-50/50 p-6">
          <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
            Checklist documentación
          </h3>
          <p className="mb-3 text-xs text-auditart-muted">
            Por ahora solo informativo: se marcará automáticamente según el avance del caso.
          </p>
          <div className="flex flex-wrap gap-3">
            <CheckBadge active={audit.presupuestoEnviado} label="Presupuesto enviado a ART" />
            <CheckBadge
              active={
                audit.autorizacionART ||
                Boolean(audit.autorizacionCodigo) ||
                Boolean(audit.autorizacionDocumentoAttachmentId)
              }
              label="Autorización ART"
            />
            <CheckBadge active={audit.autofisica} label="Autofísica cargada" />
          </div>
          {(audit.status === 'amarillo' || audit.status === 'azul') && (
            <button
              type="button"
              className="mt-4 text-sm font-semibold text-auditart-blue hover:underline"
              onClick={() => {
                setAutorizacionCodigo(audit.autorizacionCodigo ?? '')
                setAutorizacionPdf(null)
                setAzulError(null)
                setAuthModalMode('save')
                setAuthModalOpen(true)
              }}
            >
              Cargar / actualizar autorización (radicado y/o PDF)
            </button>
          )}
        </div>
        </DetailAccordion>

        {audit.notas && (
          <DetailAccordion title="Notas" defaultOpen>
          <div className="p-6">
            <p className="rounded-xl bg-auditart-light p-4 text-sm text-auditart-navy/80">
              {audit.notas}
            </p>
          </div>
          </DetailAccordion>
        )}

        {/* Transitions */}
        {transitions.length > 0 && (
          <div className="border-t border-gray-100 p-6">
            <h3 className="mb-4 text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
              Avanzar estado
            </h3>
            <div className="space-y-3">
              {transitions.map((t) => (
                <button
                  key={t.next}
                  onClick={() => void handleTransition(t.next)}
                  className="group flex w-full items-center justify-between rounded-2xl border-2 border-gray-100 bg-white p-5 text-left transition-all hover:border-auditart-blue hover:shadow-md hover:shadow-auditart-blue/8"
                >
                  <div>
                    <p className="font-bold text-auditart-navy group-hover:text-auditart-blue">
                      {t.label}
                    </p>
                    {t.condition && (
                      <p className="mt-0.5 text-xs text-auditart-muted">{t.condition}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={audit.status} compact />
                    <ChevronRight size={16} className="text-auditart-muted" />
                    <StatusBadge status={t.next} compact />
                  </div>
                </button>
              ))}
            </div>
          </div>
        )}

        {preciosModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
            <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="text-lg font-bold text-auditart-navy">Negociar precios</h3>
                  <p className="mt-1 text-sm text-auditart-muted">
                    Elegí modalidad, concepto y confirmá valor consulta vs precio a cotizar a la ART.
                    Para especialista: 50% más o 100% más sobre la base del catálogo.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setPreciosModalOpen(false)}
                  className="rounded-lg px-2 py-1 text-sm font-semibold text-auditart-muted hover:bg-gray-50"
                >
                  Cerrar
                </button>
              </div>
              {preciosError && (
                <div className="mt-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">
                  {preciosError}
                </div>
              )}

              <div className="mt-5 grid gap-3 sm:grid-cols-2">
                <button
                  type="button"
                  onClick={() => {
                    setPrecioTipo('auditor')
                    setSelectedPrecioId('')
                    setCatalogBaseValor(null)
                  }}
                  className={`rounded-2xl border-2 p-4 text-left transition-all ${
                    precioTipo === 'auditor'
                      ? 'border-auditart-blue bg-auditart-blue/8 shadow-sm'
                      : 'border-gray-200 hover:border-auditart-blue/40'
                  }`}
                >
                  <p className="font-bold text-auditart-navy">Médico auditor</p>
                  <p className="mt-1 text-xs text-auditart-muted">
                    Precio de catálogo ART sin recargo porcentual.
                  </p>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setPrecioTipo('especialista')
                    setSelectedPrecioId('')
                    setCatalogBaseValor(null)
                    setPctEspecialista(100)
                  }}
                  className={`rounded-2xl border-2 p-4 text-left transition-all ${
                    precioTipo === 'especialista'
                      ? 'border-auditart-blue bg-auditart-blue/8 shadow-sm'
                      : 'border-gray-200 hover:border-auditart-blue/40'
                  }`}
                >
                  <p className="font-bold text-auditart-navy">Especialista / estudio</p>
                  <p className="mt-1 text-xs text-auditart-muted">
                    Base del catálogo +50% o +100% a cotizar a la ART.
                  </p>
                </button>
              </div>

              {precioTipo === 'especialista' && (
                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    onClick={() => applyPctPreview(50)}
                    className={`rounded-2xl border-2 p-4 text-left transition-all ${
                      pctEspecialista === 50
                        ? 'border-emerald-400 bg-emerald-50'
                        : 'border-gray-200 hover:border-emerald-300'
                    }`}
                  >
                    <p className="text-lg font-extrabold text-emerald-800">+50%</p>
                    <p className="mt-0.5 text-xs text-auditart-muted">50% más · base × 1.5</p>
                    {catalogBaseValor != null && (
                      <p className="mt-2 text-sm font-semibold text-emerald-800">
                        {formatCurrency(catalogBaseValor)} →{' '}
                        {formatCurrency(conciliadoEspecialista(catalogBaseValor, 50))}
                      </p>
                    )}
                  </button>
                  <button
                    type="button"
                    onClick={() => applyPctPreview(100)}
                    className={`rounded-2xl border-2 p-4 text-left transition-all ${
                      pctEspecialista === 100
                        ? 'border-emerald-400 bg-emerald-50'
                        : 'border-gray-200 hover:border-emerald-300'
                    }`}
                  >
                    <p className="text-lg font-extrabold text-emerald-800">+100%</p>
                    <p className="mt-0.5 text-xs text-auditart-muted">100% más · base × 2</p>
                    {catalogBaseValor != null && (
                      <p className="mt-2 text-sm font-semibold text-emerald-800">
                        {formatCurrency(catalogBaseValor)} →{' '}
                        {formatCurrency(conciliadoEspecialista(catalogBaseValor, 100))}
                      </p>
                    )}
                  </button>
                </div>
              )}

              <label className="mt-5 block text-xs font-semibold text-auditart-gray">
                Buscar concepto del catálogo
                <input
                  value={precioQuery}
                  onChange={(e) => setPrecioQuery(e.target.value)}
                  placeholder={
                    precioTipo === 'auditor'
                      ? `Precios de ${audit.art}…`
                      : 'VALOR PRESTADORES…'
                  }
                  className="input-modern mt-1 w-full px-3 py-2 text-sm"
                />
              </label>
              <div className="mt-2 max-h-44 overflow-y-auto rounded-xl border border-gray-100">
                {precioOptions.length === 0 ? (
                  <p className="px-3 py-4 text-xs text-auditart-muted">
                    Sin conceptos activos. Facturación/Jefatura puede cargarlos en Precios.
                    Podés tipear el monto conciliado a mano.
                  </p>
                ) : (
                  precioOptions.map((p) => (
                    <button
                      key={p.id}
                      type="button"
                      onClick={() => {
                        setSelectedPrecioId(p.id)
                        setCatalogBaseValor(p.valor)
                        const conciliado =
                          precioTipo === 'especialista'
                            ? conciliadoEspecialista(p.valor, pctEspecialista)
                            : p.valor
                        setValorConciliadoInput(String(conciliado))
                      }}
                      className={`flex w-full items-center justify-between px-3 py-2.5 text-left text-sm hover:bg-auditart-light ${
                        selectedPrecioId === p.id ? 'bg-auditart-blue/10' : ''
                      }`}
                    >
                      <span className="font-medium text-auditart-navy">{p.concepto}</span>
                      <span className="font-semibold text-auditart-blue">
                        {formatCurrency(p.valor)}
                      </span>
                    </button>
                  ))
                )}
              </div>

              <div className="mt-4 grid gap-3 md:grid-cols-2">
                <label className="text-xs font-semibold text-auditart-gray">
                  Valor consulta (prestador)
                  <input
                    value={valorConsultaInput}
                    onChange={(e) => setValorConsultaInput(e.target.value)}
                    placeholder="Ej. 45000"
                    className="input-modern mt-1 w-full px-3 py-2 text-sm"
                  />
                </label>
                <label className="text-xs font-semibold text-auditart-gray">
                  Precio conciliado (ART)
                  <input
                    value={valorConciliadoInput}
                    onChange={(e) => setValorConciliadoInput(e.target.value)}
                    placeholder="Desde catálogo o manual"
                    className="input-modern mt-1 w-full px-3 py-2 text-sm"
                  />
                </label>
              </div>

              {precioTipo === 'especialista' && catalogBaseValor != null && (
                <div className="mt-4 rounded-2xl bg-emerald-50 px-4 py-3 text-sm text-emerald-900 ring-1 ring-emerald-200">
                  <span className="font-semibold">Resumen: </span>
                  {formatCurrency(catalogBaseValor)} + {pctEspecialista}% ={' '}
                  <span className="font-extrabold">
                    {formatCurrency(conciliadoEspecialista(catalogBaseValor, pctEspecialista))}
                  </span>{' '}
                  ART
                </div>
              )}

              <div className="mt-5 flex justify-end gap-2">
                <button
                  type="button"
                  disabled={preciosBusy}
                  onClick={() => setPreciosModalOpen(false)}
                  className="rounded-xl px-4 py-2 text-sm font-semibold text-auditart-gray hover:bg-gray-50"
                >
                  Cancelar
                </button>
                <button
                  type="button"
                  disabled={preciosBusy}
                  onClick={() => void savePreciosRojo()}
                  className="btn-primary inline-flex items-center gap-2 text-sm disabled:opacity-50"
                >
                  {preciosBusy && <Loader2 size={14} className="animate-spin" />}
                  Guardar precios
                </button>
              </div>
            </div>
          </div>
        )}

        {turnoModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
            <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
              <h3 className="text-lg font-bold text-auditart-navy">Coordinar turno</h3>
              <p className="mt-1 text-sm text-auditart-muted">
                Elegí profesional de la cartilla y fecha del turno (estado Amarillo).
              </p>
              {turnoError && (
                <div className="mt-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">
                  {turnoError}
                </div>
              )}
              <label className="mt-4 block text-xs font-semibold text-auditart-gray">
                Fecha y hora del turno
                <input
                  type="datetime-local"
                  value={turnoFechaLocal}
                  onChange={(e) => setTurnoFechaLocal(e.target.value)}
                  className="input-modern mt-1 w-full px-3 py-2 text-sm"
                />
              </label>
              <label className="mt-3 block text-xs font-semibold text-auditart-gray">
                Buscar profesional
                <input
                  value={prestadorQuery}
                  onChange={(e) => void searchPrestadores(e.target.value)}
                  placeholder="Nombre, localidad, especialidad…"
                  className="input-modern mt-1 w-full px-3 py-2 text-sm"
                />
              </label>
              <div className="mt-2 max-h-48 overflow-y-auto rounded-xl border border-gray-100">
                {prestadorOptions.length === 0 ? (
                  <p className="px-3 py-4 text-xs text-auditart-muted">
                    Sin resultados activos. Importá la cartilla en Doctores o escribí el nombre
                    manualmente.
                  </p>
                ) : (
                  prestadorOptions.map((p) => (
                    <button
                      key={p.id}
                      type="button"
                      onClick={() => {
                        setSelectedPrestadorId(p.id)
                        setPrestadorQuery(p.nombre)
                      }}
                      className={`flex w-full flex-col border-b border-gray-50 px-3 py-2 text-left text-sm hover:bg-auditart-light ${
                        selectedPrestadorId === p.id ? 'bg-auditart-blue/8' : ''
                      }`}
                    >
                      <span className="font-semibold text-auditart-navy">{p.nombre}</span>
                      <span className="text-xs text-auditart-muted">
                        {[p.provincia, p.localidad, p.especialidad].filter(Boolean).join(' · ')}
                      </span>
                      <span className="text-xs text-auditart-navy/80">
                        {formatCurrency(p.valorConsulta ?? undefined)}
                        {' · '}
                        {p.requierePagoAnticipado ? 'Pago anticipado' : 'Sin anticipado'}
                      </span>
                    </button>
                  ))
                )}
              </div>
              {selectedPrestadorId && (
                <p className="mt-2 text-xs text-auditart-muted">
                  Al confirmar se cargan en el caso el valor de consulta y si requiere pago
                  anticipado.
                </p>
              )}
              <div className="mt-5 flex justify-end gap-2">
                <button
                  type="button"
                  disabled={turnoBusy}
                  onClick={() => setTurnoModalOpen(false)}
                  className="rounded-xl px-4 py-2 text-sm font-semibold text-auditart-gray hover:bg-gray-50"
                >
                  Cancelar
                </button>
                <button
                  type="button"
                  disabled={turnoBusy}
                  onClick={() => void confirmTurnoAmarillo()}
                  className="btn-primary inline-flex items-center gap-2 text-sm disabled:opacity-50"
                >
                  {turnoBusy && <Loader2 size={14} className="animate-spin" />}
                  Confirmar turno
                </button>
              </div>
            </div>
          </div>
        )}

        {authModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
            <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
              <h3 className="text-lg font-bold text-auditart-navy">
                {authModalMode === 'verde' ? 'Autorización para Verde' : 'Cargar autorización'}
              </h3>
              <p className="mt-1 text-sm text-auditart-muted">
                Código/radicado, PDF, o ambos.
              </p>
              {azulError && (
                <div className="mt-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">
                  {azulError}
                </div>
              )}
              <label className="mt-4 block text-xs font-semibold text-auditart-gray">
                Código / radicado
                <input
                  value={autorizacionCodigo}
                  onChange={(e) => setAutorizacionCodigo(e.target.value)}
                  placeholder="Ej. AUT-2026-45821"
                  className="input-modern mt-1 w-full px-3 py-2 text-sm"
                />
              </label>
              <label className="mt-3 block text-xs font-semibold text-auditart-gray">
                PDF de autorización (opcional si hay código)
                <input
                  type="file"
                  accept=".pdf,application/pdf"
                  className="mt-1 block w-full text-sm"
                  onChange={(e) => setAutorizacionPdf(e.target.files?.[0] ?? null)}
                />
              </label>
              {autorizacionPdf && (
                <p className="mt-1 text-xs text-auditart-navy">{autorizacionPdf.name}</p>
              )}
              <div className="mt-5 flex justify-end gap-2">
                <button
                  type="button"
                  disabled={azulBusy}
                  onClick={() => setAuthModalOpen(false)}
                  className="rounded-xl px-4 py-2 text-sm font-semibold text-auditart-gray hover:bg-gray-50"
                >
                  Cancelar
                </button>
                <button
                  type="button"
                  disabled={azulBusy}
                  onClick={() => void submitAutorizacion()}
                  className="btn-primary inline-flex items-center gap-2 text-sm disabled:opacity-50"
                >
                  {azulBusy && <Loader2 size={14} className="animate-spin" />}
                  {authModalMode === 'verde' ? 'Guardar y pasar a Verde' : 'Guardar autorización'}
                </button>
              </div>
            </div>
          </div>
        )}

        {detailPrestadorId && (
          <PrestadorDetailModal
            prestadorId={detailPrestadorId}
            onClose={() => setDetailPrestadorId(null)}
          />
        )}

        {/* Actions */}
        <div className="flex flex-wrap gap-3 border-t border-gray-100 bg-gray-50/50 p-6">
          <button
            onClick={() => showToast('WhatsApp enviado al paciente (simulado)')}
            className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-green-500 to-emerald-600 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-green-500/25 transition-all hover:shadow-lg"
          >
            <MessageCircle size={16} />
            Avisar paciente (WS)
          </button>
          <label
            className={`flex cursor-pointer items-center gap-2 rounded-xl border-2 border-gray-200 bg-white px-5 py-2.5 text-sm font-semibold text-auditart-navy transition-all hover:border-auditart-blue hover:shadow-sm ${
              uploadBusy ? 'pointer-events-none opacity-50' : ''
            }`}
          >
            {uploadBusy ? (
              <Loader2 size={16} className="animate-spin" />
            ) : (
              <Upload size={16} />
            )}
            Adjuntar documento
            <input
              type="file"
              className="hidden"
              multiple
              accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.gif,.webp,.bmp"
              disabled={uploadBusy}
              onChange={(e) => {
                void handleUploadDocuments(e.target.files)
                e.target.value = ''
              }}
            />
          </label>
        </div>

        <DetailAccordion title="Documentos" defaultOpen={false}>
        <div className="p-6">
          <EmailAttachmentsList
            attachments={serviceAttachments.map((a) => ({
              id: a.id,
              fileName: a.fileName,
              contentType: a.contentType,
              sizeBytes: a.sizeBytes,
            }))}
            title="Archivos adjuntos"
            onError={(msg) => showToast(msg)}
            deletingId={deletingAttachmentId}
            onDelete={(attachment) => handleDeleteAttachment(attachment.id)}
          />
          {serviceAttachments.length === 0 && (
            <p className="rounded-xl bg-gray-50 px-4 py-3 text-sm text-auditart-muted">
              Todavía no hay documentos en este caso. Usá “Adjuntar documento” o enviá un
              correo con adjuntos.
            </p>
          )}
        </div>
        </DetailAccordion>

        {/* Correspondencia persistente */}
        <DetailAccordion title="Correspondencia" defaultOpen={false}>
        <div className="p-6">
          {corrLoading && (
            <div className="flex items-center gap-2 text-sm text-auditart-gray">
              <Loader2 size={16} className="animate-spin" /> Cargando conversación…
            </div>
          )}
          {!corrLoading && correspondence && (
            <div className="space-y-5">
              <EmailTimeline
                messages={correspondence.messages}
                onError={(msg) => showToast(msg)}
              />
              <EmailAttachmentsList
                attachments={correspondence.attachments}
                title="Adjuntos de la conversación"
                onError={(msg) => showToast(msg)}
              />
              <EmailReplyComposer
                defaults={correspondence.replyDefaults}
                busy={replyBusy}
                onSend={handleReply}
              />
            </div>
          )}
          {!corrLoading && !correspondence && (
            <p className="rounded-xl bg-gray-50 px-4 py-3 text-sm text-auditart-muted">
              {corrError ?? 'Este servicio no tiene conversación vinculada.'}
            </p>
          )}

          <form
            onSubmit={(e) => void handleSendNewEmail(e)}
            className="mt-6 space-y-3 rounded-2xl border border-dashed border-auditart-blue/30 bg-auditart-blue/5 p-4"
          >
            <h4 className="text-xs font-bold uppercase tracking-wider text-auditart-muted">
              Nuevo email (no respuesta)
            </h4>
            <input
              type="text"
              value={composeTo}
              onChange={(e) => setComposeTo(e.target.value)}
              placeholder="Para (emails separados por coma)"
              required
              className="input-modern w-full px-3 py-2 text-sm"
            />
            <input
              type="text"
              value={composeCc}
              onChange={(e) => setComposeCc(e.target.value)}
              placeholder="CC (opcional)"
              className="input-modern w-full px-3 py-2 text-sm"
            />
            <input
              type="text"
              value={composeSubject}
              onChange={(e) => setComposeSubject(e.target.value)}
              placeholder="Asunto"
              required
              className="input-modern w-full px-3 py-2 text-sm"
            />
            <RichTextEditor
              valueHtml={composeBodyHtml}
              onChange={({ html, text }) => {
                setComposeBodyHtml(html)
                setComposeBodyText(text)
              }}
              placeholder="Cuerpo del mensaje"
            />

            {(correspondence?.attachments.length ?? 0) > 0 && (
              <div className="rounded-xl border border-gray-200 bg-white p-3">
                <p className="mb-2 text-xs font-bold uppercase tracking-wider text-auditart-muted">
                  Adjuntos del caso (opcional)
                </p>
                <ul className="max-h-40 space-y-1.5 overflow-y-auto">
                  {correspondence!.attachments.map((attachment) => (
                    <li key={attachment.id}>
                      <label className="flex cursor-pointer items-center gap-2 text-sm text-auditart-navy">
                        <input
                          type="checkbox"
                          checked={composeSelectedIds.includes(attachment.id)}
                          onChange={() => toggleComposeAttachment(attachment.id)}
                          className="rounded border-gray-300"
                        />
                        <span className="truncate">{attachment.fileName}</span>
                        <span className="ml-auto shrink-0 text-xs text-auditart-muted">
                          {(attachment.sizeBytes / 1024).toFixed(0)} KB
                        </span>
                      </label>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div className="rounded-xl border border-dashed border-gray-300 bg-white p-3">
              <label className="flex cursor-pointer flex-col items-start gap-2">
                <span className="inline-flex items-center gap-2 text-sm font-semibold text-auditart-navy">
                  <Upload size={16} />
                  Adjuntar archivos nuevos
                </span>
                <span className="text-xs text-auditart-muted">
                  PDF, Word, Excel o imágenes · máx. 50 MB c/u · opcional
                </span>
                <input
                  type="file"
                  multiple
                  accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.gif,.webp,.bmp,application/pdf,image/*"
                  className="text-sm"
                  onChange={(e) => {
                    onComposeFilesSelected(e.target.files)
                    e.target.value = ''
                  }}
                />
              </label>
              {composeFiles.length > 0 && (
                <ul className="mt-2 space-y-1">
                  {composeFiles.map((file, index) => (
                    <li
                      key={`${file.name}-${index}`}
                      className="flex items-center justify-between gap-2 text-xs text-auditart-navy"
                    >
                      <span className="truncate">
                        {file.name} ({(file.size / 1024).toFixed(0)} KB)
                      </span>
                      <button
                        type="button"
                        className="font-semibold text-red-600 hover:underline"
                        onClick={() =>
                          setComposeFiles((prev) => prev.filter((_, i) => i !== index))
                        }
                      >
                        Quitar
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <button
              type="submit"
              disabled={composeBusy}
              className="btn-primary flex items-center gap-2 text-sm disabled:opacity-50"
            >
              {composeBusy ? <Loader2 size={16} className="animate-spin" /> : 'Enviar email nuevo'}
            </button>
          </form>
        </div>
        </DetailAccordion>
      </div>
    </div>
  )
}

function DetailAccordion({
  title,
  defaultOpen = false,
  children,
}: {
  title: string
  defaultOpen?: boolean
  children: ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="border-t border-gray-100">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center justify-between gap-3 px-6 py-3.5 text-left hover:bg-gray-50/80"
      >
        <span className="text-[10px] font-bold uppercase tracking-[0.15em] text-auditart-muted">
          {title}
        </span>
        <ChevronDown
          size={16}
          className={`text-auditart-muted transition-transform ${open ? 'rotate-180' : ''}`}
        />
      </button>
      {open && <div>{children}</div>}
    </div>
  )
}

function InfoRow({
  label,
  value,
  highlight,
}: {
  label: string
  value: string
  highlight?: boolean
}) {
  return (
    <div className="flex justify-between border-b border-gray-50 py-3 text-sm last:border-0">
      <span className="text-auditart-muted">{label}</span>
      <span
        className={`font-semibold ${highlight ? 'text-red-600' : 'text-auditart-navy'}`}
      >
        {value}
      </span>
    </div>
  )
}

function CheckBadge({ active, label }: { active: boolean; label: string }) {
  return (
    <div
      className={`flex items-center gap-2.5 rounded-xl border-2 px-4 py-2.5 text-sm font-semibold ${
        active
          ? 'border-green-200 bg-green-50 text-green-700 shadow-sm'
          : 'border-gray-200 bg-white text-auditart-gray'
      }`}
    >
      <CheckCircle2 size={16} className={active ? 'text-green-500' : 'text-gray-300'} />
      {label}
    </div>
  )
}
