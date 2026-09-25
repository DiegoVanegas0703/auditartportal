import { AlertTriangle, BarChart3, Clock, Download, Loader2, RefreshCw, Timer } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import {
  reportsApi,
  type ArtReportDto,
  type OperatorReportDto,
  type ReportsSummaryDto,
  type SlaDelaySummaryDto,
} from '../api/auditartApi'
import { PageHeader } from '../components/ui/PageHeader'
import { formatDateTime } from '../utils/format'

const COLORS = {
  navy: '#1e2d3d',
  blue: '#5d92b1',
  blueDark: '#3d6f8c',
  green: '#0db26b',
  red: '#ef4444',
  amber: '#eab308',
  azul: '#3b82f6',
  muted: '#94a3b8',
  gray: '#64748b',
}

const ART_PIE_PALETTE = [
  '#5d92b1',
  '#1e2d3d',
  '#0db26b',
  '#3d6f8c',
  '#7eb3cc',
  '#2c3e50',
  '#09965a',
  '#64748b',
]

export function ReportsPage() {
  const [data, setData] = useState<ReportsSummaryDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setData(await reportsApi.summary())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los reportes')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const chartData = useMemo(() => {
    if (!data) return null
    const sla = data.slaDelays

    const slaEstado = [
      { name: 'Vencidos', value: sla.slaVencidos, color: COLORS.red },
      { name: 'En riesgo', value: sla.slaEnRiesgo, color: COLORS.amber },
      { name: 'A tiempo', value: sla.slaATiempo, color: COLORS.green },
    ].filter((d) => d.value > 0)

    const vencidosPorEstado = [
      { name: 'Rojo', value: sla.vencidosRojo, color: COLORS.red },
      { name: 'Amarillo', value: sla.vencidosAmarillo, color: COLORS.amber },
      { name: 'Azul', value: sla.vencidosAzul, color: COLORS.azul },
    ].filter((d) => d.value > 0)

    const porOperador = data.operators
      .filter((o) => o.casosAProcesar > 0 || o.slaVencidos > 0)
      .slice(0, 8)
      .map((o) => ({
        name: shortName(o.operadorName),
        fullName: o.operadorName,
        aProcesar: o.casosAProcesar,
        slaVencidos: o.slaVencidos ?? 0,
        slaRiesgo: o.slaEnRiesgo ?? 0,
      }))

    const porArt = data.byArt
      .filter((a) => a.siniestrosMesActual > 0)
      .slice(0, 8)
      .map((a) => ({
        name: a.art.length > 22 ? `${a.art.slice(0, 20)}…` : a.art,
        fullName: a.art,
        value: a.siniestrosMesActual,
      }))

    const artComparativo = data.byArt.slice(0, 6).map((a) => ({
      name: a.art.length > 14 ? `${a.art.slice(0, 12)}…` : a.art,
      fullName: a.art,
      actual: a.siniestrosMesActual,
      anterior: a.siniestrosMesAnterior,
    }))

    return { slaEstado, vencidosPorEstado, porOperador, porArt, artComparativo }
  }, [data])

  const maxArt = useMemo(() => {
    if (!data?.byArt.length) return 1
    return Math.max(
      ...data.byArt.map((r) => Math.max(r.siniestrosMesActual, r.siniestrosMesAnterior)),
      1,
    )
  }, [data])

  const handleExport = async () => {
    setExporting(true)
    setError(null)
    try {
      await reportsApi.exportExcel()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo exportar Excel')
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="animate-fade-in">
      <PageHeader
        title="Auditorías"
        subtitle="Demoras SLA, métricas por operador y por ART · solo Jefatura / Admin"
        action={
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => void load()}
              disabled={loading}
              className="flex items-center gap-2 rounded-full bg-white px-4 py-2 text-sm font-semibold text-auditart-navy shadow-sm ring-1 ring-gray-200 hover:bg-gray-50 disabled:opacity-50"
            >
              <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
              Actualizar
            </button>
            <button
              type="button"
              onClick={() => void handleExport()}
              disabled={exporting || !data}
              className="btn-primary flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50"
            >
              {exporting ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />}
              Exportar Excel
            </button>
          </div>
        }
      />

      {error && (
        <div className="mb-5 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {loading && !data ? (
        <div className="flex items-center gap-2 text-sm text-auditart-gray">
          <Loader2 size={16} className="animate-spin" /> Cargando reportes…
        </div>
      ) : data && chartData ? (
        <>
          <p className="mb-5 text-xs text-auditart-muted">
            Generado {formatDateTime(data.generatedAtUtc)} · Mes actual desde{' '}
            {formatDateTime(data.monthStartUtc)}
          </p>

          <SlaDelaySection sla={data.slaDelays} />

          <section className="card-flat mb-6 overflow-hidden">
            <div className="border-b border-gray-100 px-6 py-4">
              <h3 className="flex items-center gap-2 text-sm font-bold text-auditart-navy">
                <BarChart3 size={16} className="text-auditart-blue" />
                Por operador
              </h3>
              <p className="mt-1 text-xs text-auditart-muted">
                Detalle tabular · demora de atención abierta si aún no hay turno
              </p>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[1280px] text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/80 text-left text-[11px] font-bold uppercase tracking-wider text-auditart-gray">
                    <th className="px-4 py-3">Operador</th>
                    <th className="px-4 py-3">A procesar</th>
                    <th className="px-4 py-3">SLA vencidos</th>
                    <th className="px-4 py-3">SLA riesgo</th>
                    <th className="px-4 py-3">Retraso SLA</th>
                    <th className="px-4 py-3">Ingresos hoy</th>
                    <th className="px-4 py-3">Coord. vencidos</th>
                    <th className="px-4 py-3">Crónicos pend.</th>
                    <th className="px-4 py-3">Celeste</th>
                    <th className="px-4 py-3">Demora atención</th>
                    <th className="px-4 py-3">Demora proc.</th>
                  </tr>
                </thead>
                <tbody>
                  {data.operators.length === 0 ? (
                    <tr>
                      <td colSpan={11} className="px-4 py-8 text-center text-auditart-muted">
                        Sin datos de operadores
                      </td>
                    </tr>
                  ) : (
                    data.operators.map((row) => (
                      <OperatorRow key={row.operadorId ?? 'none'} row={row} />
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </section>

          <section className="card-flat mb-6 overflow-hidden">
            <div className="border-b border-gray-100 px-6 py-4">
              <h3 className="text-sm font-bold text-auditart-navy">Por ART (aseguradora)</h3>
              <p className="mt-1 text-xs text-auditart-muted">
                Siniestros ingresados mes actual vs mes anterior
              </p>
            </div>
            <div className="space-y-4 p-6">
              {data.byArt.length === 0 ? (
                <p className="text-center text-sm text-auditart-muted">Sin datos por ART</p>
              ) : (
                data.byArt.map((row) => <ArtBar key={row.art} row={row} max={maxArt} />)
              )}
            </div>
          </section>

          <section className="mb-6 grid gap-6 lg:grid-cols-2">
            <ChartCard
              title="Estado SLA"
              subtitle="Distribución de casos con deadline"
            >
              <DonutChart
                data={chartData.slaEstado}
                emptyLabel="Sin casos con SLA"
              />
            </ChartCard>
            <ChartCard
              title="Vencidos por etapa"
              subtitle="Rojo · Amarillo · Azul"
            >
              <DonutChart
                data={chartData.vencidosPorEstado}
                emptyLabel="Sin SLA vencidos"
              />
            </ChartCard>
          </section>

          <section className="mb-6 grid gap-6 lg:grid-cols-2">
            <ChartCard
              title="Carga por operador"
              subtitle="Casos a procesar vs SLA vencidos / en riesgo"
            >
              {chartData.porOperador.length === 0 ? (
                <EmptyChart label="Sin datos de operadores" />
              ) : (
                <div className="h-72 w-full">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={chartData.porOperador} margin={{ top: 8, right: 8, left: 0, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" vertical={false} />
                      <XAxis
                        dataKey="name"
                        tick={{ fill: COLORS.gray, fontSize: 11 }}
                        axisLine={false}
                        tickLine={false}
                      />
                      <YAxis
                        allowDecimals={false}
                        tick={{ fill: COLORS.gray, fontSize: 11 }}
                        axisLine={false}
                        tickLine={false}
                        width={28}
                      />
                      <Tooltip
                        contentStyle={tooltipStyle}
                        formatter={(value, name) => [
                          value ?? 0,
                          name === 'aProcesar'
                            ? 'A procesar'
                            : name === 'slaVencidos'
                              ? 'SLA vencidos'
                              : 'SLA en riesgo',
                        ]}
                        labelFormatter={(_, payload) =>
                          String(payload?.[0]?.payload?.fullName ?? '')
                        }
                      />
                      <Legend
                        formatter={(value) =>
                          value === 'aProcesar'
                            ? 'A procesar'
                            : value === 'slaVencidos'
                              ? 'SLA vencidos'
                              : 'SLA en riesgo'
                        }
                      />
                      <Bar dataKey="aProcesar" fill={COLORS.blue} radius={[4, 4, 0, 0]} />
                      <Bar dataKey="slaVencidos" fill={COLORS.red} radius={[4, 4, 0, 0]} />
                      <Bar dataKey="slaRiesgo" fill={COLORS.amber} radius={[4, 4, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </ChartCard>

            <ChartCard
              title="Siniestros por ART (mes)"
              subtitle="Participación del mes actual"
            >
              <DonutChart
                data={chartData.porArt.map((d, i) => ({
                  ...d,
                  color: ART_PIE_PALETTE[i % ART_PIE_PALETTE.length],
                }))}
                emptyLabel="Sin siniestros este mes"
              />
            </ChartCard>
          </section>

          <section className="card-flat mb-6 overflow-hidden">
            <div className="border-b border-gray-100 px-6 py-4">
              <h3 className="text-sm font-bold text-auditart-navy">ART: mes actual vs anterior</h3>
              <p className="mt-1 text-xs text-auditart-muted">Comparativo de ingresos por aseguradora</p>
            </div>
            <div className="p-4 sm:p-6">
              {chartData.artComparativo.length === 0 ? (
                <EmptyChart label="Sin datos por ART" />
              ) : (
                <div className="h-72 w-full">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart
                      data={chartData.artComparativo}
                      margin={{ top: 8, right: 8, left: 0, bottom: 8 }}
                    >
                      <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" vertical={false} />
                      <XAxis
                        dataKey="name"
                        tick={{ fill: COLORS.gray, fontSize: 11 }}
                        axisLine={false}
                        tickLine={false}
                      />
                      <YAxis
                        allowDecimals={false}
                        tick={{ fill: COLORS.gray, fontSize: 11 }}
                        axisLine={false}
                        tickLine={false}
                        width={28}
                      />
                      <Tooltip
                        contentStyle={tooltipStyle}
                        formatter={(value, name) => [
                          value ?? 0,
                          name === 'actual' ? 'Mes actual' : 'Mes anterior',
                        ]}
                        labelFormatter={(_, payload) =>
                          String(payload?.[0]?.payload?.fullName ?? '')
                        }
                      />
                      <Legend
                        formatter={(value) =>
                          value === 'actual' ? 'Mes actual' : 'Mes anterior'
                        }
                      />
                      <Bar dataKey="actual" fill={COLORS.blue} radius={[4, 4, 0, 0]} />
                      <Bar dataKey="anterior" fill={COLORS.navy} radius={[4, 4, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </div>
          </section>
        </>
      ) : null}
    </div>
  )
}

const tooltipStyle = {
  borderRadius: 12,
  border: '1px solid #e2e8f0',
  fontSize: 12,
  boxShadow: '0 8px 24px rgba(30, 45, 61, 0.08)',
}

function ChartCard({
  title,
  subtitle,
  children,
}: {
  title: string
  subtitle: string
  children: ReactNode
}) {
  return (
    <div className="card-flat overflow-hidden">
      <div className="border-b border-gray-100 px-6 py-4">
        <h3 className="text-sm font-bold text-auditart-navy">{title}</h3>
        <p className="mt-1 text-xs text-auditart-muted">{subtitle}</p>
      </div>
      <div className="p-4 sm:p-6">{children}</div>
    </div>
  )
}

function DonutChart({
  data,
  emptyLabel,
}: {
  data: { name: string; value: number; color: string; fullName?: string }[]
  emptyLabel: string
}) {
  if (data.length === 0) return <EmptyChart label={emptyLabel} />

  const total = data.reduce((sum, d) => sum + d.value, 0)

  return (
    <div className="flex h-72 flex-col items-center sm:flex-row sm:items-stretch">
      <div className="relative h-56 w-full sm:h-full sm:flex-1">
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={data}
              dataKey="value"
              nameKey="name"
              cx="50%"
              cy="50%"
              innerRadius="58%"
              outerRadius="82%"
              paddingAngle={2}
              stroke="#fff"
              strokeWidth={2}
            >
              {data.map((entry) => (
                <Cell key={entry.name} fill={entry.color} />
              ))}
            </Pie>
            <Tooltip
              contentStyle={tooltipStyle}
              formatter={(value, _name, item) => {
                const v = Number(value ?? 0)
                const pct = total > 0 ? Math.round((v / total) * 100) : 0
                return [`${v} (${pct}%)`, String(item?.payload?.fullName ?? item?.name ?? '')]
              }}
            />
          </PieChart>
        </ResponsiveContainer>
        <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-2xl font-bold text-auditart-navy">{total}</span>
          <span className="text-[10px] font-bold uppercase tracking-wider text-auditart-muted">
            Total
          </span>
        </div>
      </div>
      <ul className="mt-2 w-full space-y-2 sm:mt-0 sm:w-40 sm:shrink-0 sm:self-center">
        {data.map((d) => (
          <li key={d.name} className="flex items-center gap-2 text-xs text-auditart-navy">
            <span
              className="h-2.5 w-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: d.color }}
            />
            <span className="truncate font-medium" title={d.fullName ?? d.name}>
              {d.name}
            </span>
            <span className="ml-auto font-bold tabular-nums">{d.value}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

function EmptyChart({ label }: { label: string }) {
  return (
    <div className="flex h-56 items-center justify-center text-sm text-auditart-muted">{label}</div>
  )
}

function shortName(name: string) {
  const parts = name.trim().split(/\s+/)
  if (parts.length === 1) return parts[0].slice(0, 10)
  return `${parts[0].slice(0, 8)} ${parts[parts.length - 1][0]}.`
}

function SlaDelaySection({ sla }: { sla: SlaDelaySummaryDto }) {
  return (
    <section className="card-flat mb-6 overflow-hidden">
      <div className="border-b border-gray-100 px-6 py-4">
        <h3 className="flex items-center gap-2 text-sm font-bold text-auditart-navy">
          <Timer size={16} className="text-auditart-blue" />
          Resumen demoras SLA
        </h3>
        <p className="mt-1 text-xs text-auditart-muted">
          Misma lógica del tablero operativo (deadline Rojo / Amarillo / Azul)
        </p>
      </div>
      <div className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
        <SlaStat
          label="SLA vencidos"
          value={sla.slaVencidos}
          hint={`${sla.vencidosRojo} rojo · ${sla.vencidosAmarillo} amarillo · ${sla.vencidosAzul} azul`}
          tone="danger"
          icon={AlertTriangle}
        />
        <SlaStat
          label="En riesgo"
          value={sla.slaEnRiesgo}
          hint="Dentro del umbral de aviso"
          tone="warn"
          icon={Clock}
        />
        <SlaStat
          label="A tiempo"
          value={sla.slaATiempo}
          hint={`${sla.casosConSla} casos con deadline`}
          tone="ok"
          icon={Timer}
        />
        <SlaStat
          label="Retraso promedio"
          value={
            sla.promedioRetrasoHoras != null ? formatHours(sla.promedioRetrasoHoras) : '—'
          }
          hint={
            sla.maxRetrasoHoras != null
              ? `Máximo ${formatHours(sla.maxRetrasoHoras)}`
              : 'Sin casos vencidos'
          }
          tone={sla.promedioRetrasoHoras != null ? 'danger' : 'neutral'}
          icon={Clock}
        />
      </div>
    </section>
  )
}

function SlaStat({
  label,
  value,
  hint,
  tone,
  icon: Icon,
}: {
  label: string
  value: number | string
  hint: string
  tone: 'danger' | 'warn' | 'ok' | 'neutral'
  icon: typeof Timer
}) {
  const tones = {
    danger: 'bg-red-50 text-red-700 ring-red-100',
    warn: 'bg-amber-50 text-amber-800 ring-amber-100',
    ok: 'bg-emerald-50 text-emerald-800 ring-emerald-100',
    neutral: 'bg-gray-50 text-auditart-navy ring-gray-100',
  }
  return (
    <div className={`rounded-2xl p-4 ring-1 ${tones[tone]}`}>
      <div className="mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wide opacity-80">
        <Icon size={14} />
        {label}
      </div>
      <div className="text-2xl font-bold">{value}</div>
      <p className="mt-1 text-xs opacity-70">{hint}</p>
    </div>
  )
}

function OperatorRow({ row }: { row: OperatorReportDto }) {
  return (
    <tr className="border-b border-gray-50 hover:bg-auditart-blue/3">
      <td className="px-4 py-3 font-semibold text-auditart-navy">{row.operadorName}</td>
      <td className="px-4 py-3">{row.casosAProcesar}</td>
      <td className="px-4 py-3">
        <Metric value={row.slaVencidos ?? 0} alert />
      </td>
      <td className="px-4 py-3">
        <Metric value={row.slaEnRiesgo ?? 0} warn />
      </td>
      <td className="px-4 py-3 text-auditart-gray">
        {row.promedioRetrasoSlaHoras != null ? formatHours(row.promedioRetrasoSlaHoras) : '—'}
      </td>
      <td className="px-4 py-3">{row.ingresosHoy}</td>
      <td className="px-4 py-3">
        <Metric value={row.coordinarVencidos} alert />
      </td>
      <td className="px-4 py-3">{row.cronicosPendientes}</td>
      <td className="px-4 py-3">{row.celeste}</td>
      <td className="px-4 py-3 text-auditart-gray">
        {row.promedioDemoraGestionHoras != null
          ? formatHours(row.promedioDemoraGestionHoras)
          : '—'}
      </td>
      <td className="px-4 py-3 text-auditart-gray">
        {row.promedioDemoraProcesamientoHoras != null
          ? formatHours(row.promedioDemoraProcesamientoHoras)
          : '—'}
      </td>
    </tr>
  )
}

function formatHours(hours: number): string {
  if (hours >= 24) {
    const days = Math.round(hours / 24)
    return `${days}d`
  }
  return `${Math.round(hours * 10) / 10}h`
}

function Metric({ value, alert, warn }: { value: number; alert?: boolean; warn?: boolean }) {
  if (value <= 0) return <span>{value}</span>
  return (
    <span
      className={
        alert
          ? 'rounded-full bg-red-100 px-2 py-0.5 text-xs font-bold text-red-700'
          : warn
            ? 'rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-800'
            : undefined
      }
    >
      {value}
    </span>
  )
}

function ArtBar({ row, max }: { row: ArtReportDto; max: number }) {
  const delta = row.siniestrosMesActual - row.siniestrosMesAnterior
  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between gap-3">
        <span className="text-sm font-semibold text-auditart-navy">{row.art}</span>
        <span className="text-xs text-auditart-muted">
          {row.siniestrosMesActual} vs {row.siniestrosMesAnterior}{' '}
          <span className={delta >= 0 ? 'text-green-700' : 'text-red-700'}>
            ({delta >= 0 ? '+' : ''}
            {delta})
          </span>
        </span>
      </div>
      <div className="space-y-1">
        <div className="flex items-center gap-2">
          <span className="w-16 text-[10px] font-bold uppercase text-auditart-muted">Actual</span>
          <div className="h-2.5 flex-1 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-auditart-blue"
              style={{ width: `${(row.siniestrosMesActual / max) * 100}%` }}
            />
          </div>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-16 text-[10px] font-bold uppercase text-auditart-muted">Anterior</span>
          <div className="h-2.5 flex-1 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-auditart-navy/40"
              style={{ width: `${(row.siniestrosMesAnterior / max) * 100}%` }}
            />
          </div>
        </div>
      </div>
    </div>
  )
}
