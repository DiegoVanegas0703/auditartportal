import { CheckCircle2, DollarSign, Download, FileSpreadsheet, ListChecks } from 'lucide-react'
import { useMemo, useState } from 'react'
import { PageHeader } from '../components/ui/PageHeader'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAudits } from '../context/AuditContext'
import { SERVICE_LABELS } from '../types'
import { formatDate, formatCurrency } from '../utils/format'

export function BillingPage() {
  const { audits } = useAudits()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [exported, setExported] = useState(false)

  const greenAudits = useMemo(() => audits.filter((a) => a.status === 'verde'), [audits])

  const totalSelected = useMemo(
    () =>
      greenAudits
        .filter((a) => selected.has(a.id))
        .reduce((sum, a) => sum + (a.valorPactado ?? 0), 0),
    [greenAudits, selected],
  )

  const toggleAll = () => {
    setSelected(
      selected.size === greenAudits.length
        ? new Set()
        : new Set(greenAudits.map((a) => a.id)),
    )
  }

  const toggleOne = (id: string) => {
    const next = new Set(selected)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    setSelected(next)
  }

  const handleExport = () => {
    setExported(true)
    setTimeout(() => setExported(false), 3000)
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Consolidación Contable"
        subtitle="Servicios en estado Verde listos para facturación · Perfil Damián"
        action={
          <button
            onClick={handleExport}
            disabled={selected.size === 0}
            className="btn-primary flex items-center gap-2 !rounded-xl !py-2.5 !text-sm disabled:opacity-40 disabled:shadow-none"
          >
            {exported ? (
              <>
                <CheckCircle2 size={16} /> Exportado
              </>
            ) : (
              <>
                <Download size={16} /> Exportar a Excel ({selected.size})
              </>
            )}
          </button>
        }
      />

      <div className="mb-6 grid grid-cols-1 gap-5 sm:grid-cols-3">
        {[
          { label: 'Servicios listos', value: greenAudits.length, icon: ListChecks, color: 'text-green-600', bg: 'bg-green-100' },
          { label: 'Seleccionados', value: selected.size, icon: FileSpreadsheet, color: 'text-auditart-navy', bg: 'bg-auditart-blue/10' },
          { label: 'Total a facturar', value: formatCurrency(totalSelected), icon: DollarSign, color: 'text-auditart-blue', bg: 'bg-auditart-blue/10', isText: true },
        ].map((card) => (
          <div key={card.label} className="card p-5">
            <div className="flex items-center gap-3">
              <div className={`flex h-11 w-11 items-center justify-center rounded-xl ${card.bg}`}>
                <card.icon size={20} className={card.color} />
              </div>
              <div>
                <p className="text-xs font-medium text-auditart-gray">{card.label}</p>
                <p className={`text-2xl font-extrabold tracking-tight ${card.color}`}>
                  {card.isText ? card.value : card.value}
                </p>
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="card-flat overflow-hidden">
        <div className="flex items-center gap-3 border-b border-green-100 bg-gradient-to-r from-green-50 to-emerald-50 px-6 py-4">
          <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-green-100">
            <FileSpreadsheet size={18} className="text-green-600" />
          </div>
          <div>
            <p className="text-sm font-bold text-auditart-navy">Liquidación semanal</p>
            <p className="text-xs text-auditart-gray">Registros verdes · Listos para facturar</p>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                <th className="px-5 py-4">
                  <input
                    type="checkbox"
                    checked={selected.size === greenAudits.length && greenAudits.length > 0}
                    onChange={toggleAll}
                    className="h-4 w-4 rounded border-gray-300 text-auditart-blue focus:ring-auditart-blue"
                  />
                </th>
                <th className="px-5 py-4">N°</th>
                <th className="px-5 py-4">Paciente</th>
                <th className="px-5 py-4">ART</th>
                <th className="px-5 py-4">Servicio</th>
                <th className="px-5 py-4">Consulta</th>
                <th className="px-5 py-4">Valor pactado</th>
                <th className="px-5 py-4">Autorización</th>
                <th className="px-5 py-4">Estado</th>
              </tr>
            </thead>
            <tbody>
              {greenAudits.map((audit, i) => (
                <tr
                  key={audit.id}
                  className={`row-status-verde border-b border-gray-50 transition-colors hover:bg-green-50/50 ${
                    i % 2 === 0 ? 'bg-white' : 'bg-gray-50/30'
                  }`}
                >
                  <td className="px-5 py-3.5">
                    <input
                      type="checkbox"
                      checked={selected.has(audit.id)}
                      onChange={() => toggleOne(audit.id)}
                      className="h-4 w-4 rounded border-gray-300 text-auditart-blue focus:ring-auditart-blue"
                    />
                  </td>
                  <td className="px-5 py-3.5 font-mono text-xs font-bold">#{audit.numero}</td>
                  <td className="px-5 py-3.5 font-semibold text-auditart-navy">{audit.paciente}</td>
                  <td className="px-5 py-3.5 text-auditart-gray">{audit.art}</td>
                  <td className="px-5 py-3.5 text-auditart-gray">{SERVICE_LABELS[audit.tipoServicio]}</td>
                  <td className="px-5 py-3.5 text-auditart-gray">{formatDate(audit.fechaConsulta)}</td>
                  <td className="px-5 py-3.5 font-bold text-auditart-navy">
                    {formatCurrency(audit.valorPactado)}
                  </td>
                  <td className="px-5 py-3.5">
                    {audit.autorizacionART ? (
                      <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-semibold text-green-700">
                        <CheckCircle2 size={11} /> Cargada
                      </span>
                    ) : (
                      <span className="rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-semibold text-red-600">
                        Pendiente
                      </span>
                    )}
                  </td>
                  <td className="px-5 py-3.5">
                    <StatusBadge status="verde" compact />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {greenAudits.length === 0 && (
          <div className="flex flex-col items-center py-16 text-auditart-muted">
            <FileSpreadsheet size={36} className="mb-3 opacity-20" />
            <p className="font-medium">No hay servicios listos para facturación</p>
          </div>
        )}
      </div>
    </div>
  )
}
