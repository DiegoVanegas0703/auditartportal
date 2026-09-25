using Auditart.Application.Abstractions;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Reports;

public sealed record OperatorReportRow(
    Guid? OperadorId,
    string OperadorName,
    int CasosAProcesar,
    int VencidosAProcesar,
    int SlaVencidos,
    int SlaEnRiesgo,
    double? PromedioRetrasoSlaHoras,
    int IngresosHoy,
    int CoordinarVencidos,
    int CronicosPendientes,
    int Celeste,
    double? PromedioDemoraGestionHoras,
    double? PromedioDemoraProcesamientoHoras);

public sealed record ArtReportRow(
    string Art,
    int SiniestrosMesActual,
    int SiniestrosMesAnterior);

public sealed record SlaDelaySummaryDto(
    int CasosConSla,
    int SlaVencidos,
    int SlaEnRiesgo,
    int SlaATiempo,
    double? PromedioRetrasoHoras,
    double? MaxRetrasoHoras,
    int VencidosRojo,
    int VencidosAmarillo,
    int VencidosAzul);

public sealed record ReportsSummaryDto(
    IReadOnlyList<OperatorReportRow> Operators,
    IReadOnlyList<ArtReportRow> ByArt,
    SlaDelaySummaryDto SlaDelays,
    DateTime GeneratedAtUtc,
    DateTime MonthStartUtc,
    DateTime PreviousMonthStartUtc);

public sealed class ReportsService
{
    private static readonly AuditStatus[] CasosAProcesarStatuses =
    [
        AuditStatus.Rojo,
        AuditStatus.Amarillo,
        AuditStatus.Azul
    ];

    private readonly IAppDbContext _db;

    public ReportsService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ReportsSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = monthStart.AddMonths(-1);

        var warnRules = await _db.SlaRules
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .Select(r => new { r.Queue, r.Status, r.WarnBeforeHours })
            .ToListAsync(ct);
        var warnMap = warnRules.ToDictionary(
            r => (r.Queue, r.Status),
            r => r.WarnBeforeHours);

        var services = await _db.AuditServices
            .AsNoTracking()
            .Select(s => new ServiceSnapshot(
                s.Id,
                s.OperadorId,
                s.Operador != null ? s.Operador.Name : null,
                s.Art,
                s.Queue,
                s.Status,
                s.FechaIngresoUtc,
                s.FechaTurnoUtc,
                s.SlaDeadlineUtc,
                s.IsChronicPeriodic,
                s.NeedsOperadorAssignment))
            .ToListAsync(ct);

        var history = await _db.ServiceStatusHistories
            .AsNoTracking()
            .Where(h => h.ToStatus == AuditStatus.Amarillo || h.ToStatus == AuditStatus.Verde)
            .Select(h => new HistorySnapshot(h.AuditServiceId, h.ToStatus, h.CreatedAtUtc))
            .ToListAsync(ct);

        var historyByService = history
            .GroupBy(h => h.AuditServiceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var operatorIds = services
            .Where(s => s.OperadorId.HasValue)
            .Select(s => s.OperadorId!.Value)
            .Distinct()
            .ToList();

        var operatorNames = await _db.Users
            .AsNoTracking()
            .Where(u => operatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var slaContext = new SlaContext(now, warnMap);

        var grouped = services
            .GroupBy(s => s.OperadorId)
            .Select(g => BuildOperatorRow(g, historyByService, operatorNames, todayStart, todayEnd, slaContext))
            .OrderByDescending(r => r.SlaVencidos)
            .ThenByDescending(r => r.CasosAProcesar)
            .ThenBy(r => r.OperadorName)
            .ToList();

        var byArt = services
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Art) ? "Sin ART" : s.Art.Trim())
            .Select(g => new ArtReportRow(
                g.Key,
                g.Count(s => s.FechaIngresoUtc >= monthStart),
                g.Count(s => s.FechaIngresoUtc >= previousMonthStart && s.FechaIngresoUtc < monthStart)))
            .Where(r => r.SiniestrosMesActual > 0 || r.SiniestrosMesAnterior > 0)
            .OrderByDescending(r => r.SiniestrosMesActual)
            .ThenBy(r => r.Art)
            .ToList();

        var active = services.Where(s => CasosAProcesarStatuses.Contains(s.Status)).ToList();
        var withSla = active.Where(s => s.SlaDeadlineUtc.HasValue).ToList();
        var overdue = withSla.Where(s => IsSlaVencido(s, now)).ToList();
        var atRisk = withSla.Where(s => IsSlaEnRiesgo(s, slaContext)).ToList();
        var onTime = withSla.Count - overdue.Count - atRisk.Count;
        var delays = overdue
            .Select(s => (now - s.SlaDeadlineUtc!.Value).TotalHours)
            .Where(h => h >= 0)
            .ToList();

        var slaDelays = new SlaDelaySummaryDto(
            withSla.Count,
            overdue.Count,
            atRisk.Count,
            Math.Max(0, onTime),
            delays.Count > 0 ? Math.Round(delays.Average(), 1) : null,
            delays.Count > 0 ? Math.Round(delays.Max(), 1) : null,
            overdue.Count(s => s.Status == AuditStatus.Rojo),
            overdue.Count(s => s.Status == AuditStatus.Amarillo),
            overdue.Count(s => s.Status == AuditStatus.Azul));

        return new ReportsSummaryDto(grouped, byArt, slaDelays, now, monthStart, previousMonthStart);
    }

    private static OperatorReportRow BuildOperatorRow(
        IGrouping<Guid?, ServiceSnapshot> group,
        Dictionary<Guid, List<HistorySnapshot>> historyByService,
        Dictionary<Guid, string> operatorNames,
        DateTime todayStart,
        DateTime todayEnd,
        SlaContext sla)
    {
        var rows = group.ToList();
        var gestionHours = new List<double>();
        var procHours = new List<double>();
        var retrasoSla = new List<double>();

        foreach (var s in rows)
        {
            historyByService.TryGetValue(s.Id, out var events);
            var toAmarillo = events?
                .Where(e => e.ToStatus == AuditStatus.Amarillo)
                .OrderBy(e => e.AtUtc)
                .Select(e => (DateTime?)e.AtUtc)
                .FirstOrDefault();
            var toVerde = events?
                .Where(e => e.ToStatus == AuditStatus.Verde)
                .OrderBy(e => e.AtUtc)
                .Select(e => (DateTime?)e.AtUtc)
                .FirstOrDefault();

            var turnoAt = s.FechaTurnoUtc ?? toAmarillo;
            if (turnoAt.HasValue)
            {
                var hours = (turnoAt.Value - s.FechaIngresoUtc).TotalHours;
                if (hours >= 0) gestionHours.Add(hours);
            }
            else if (CasosAProcesarStatuses.Contains(s.Status))
            {
                // Sin turno aún: demora de atención abierta desde el ingreso.
                var hours = (sla.Now - s.FechaIngresoUtc).TotalHours;
                if (hours >= 0) gestionHours.Add(hours);
            }

            if (turnoAt.HasValue && toVerde.HasValue)
            {
                var hours = (toVerde.Value - turnoAt.Value).TotalHours;
                if (hours >= 0) procHours.Add(hours);
            }

            if (IsSlaVencido(s, sla.Now))
            {
                var delay = (sla.Now - s.SlaDeadlineUtc!.Value).TotalHours;
                if (delay >= 0) retrasoSla.Add(delay);
            }
        }

        var operadorId = group.Key;
        var name = operadorId.HasValue
            ? operatorNames.GetValueOrDefault(operadorId.Value) ?? rows.First().OperadorName ?? "Sin nombre"
            : "Sin asignar";

        var activos = rows.Where(s => CasosAProcesarStatuses.Contains(s.Status)).ToList();

        return new OperatorReportRow(
            operadorId,
            name,
            activos.Count,
            activos.Count(s => IsSlaVencido(s, sla.Now) || IsCoordinarVencido(s)),
            activos.Count(s => IsSlaVencido(s, sla.Now)),
            activos.Count(s => IsSlaEnRiesgo(s, sla)),
            retrasoSla.Count > 0 ? Math.Round(retrasoSla.Average(), 1) : null,
            rows.Count(s => s.FechaIngresoUtc >= todayStart && s.FechaIngresoUtc < todayEnd),
            rows.Count(IsCoordinarVencido),
            rows.Count(IsCronicoPendiente),
            rows.Count(s => s.Status == AuditStatus.Celeste),
            gestionHours.Count > 0 ? Math.Round(gestionHours.Average(), 1) : null,
            procHours.Count > 0 ? Math.Round(procHours.Average(), 1) : null);
    }

    private static bool IsSlaVencido(ServiceSnapshot s, DateTime now) =>
        CasosAProcesarStatuses.Contains(s.Status) &&
        s.SlaDeadlineUtc.HasValue &&
        s.SlaDeadlineUtc.Value < now;

    private static bool IsSlaEnRiesgo(ServiceSnapshot s, SlaContext sla)
    {
        if (!CasosAProcesarStatuses.Contains(s.Status) || !s.SlaDeadlineUtc.HasValue)
            return false;
        if (s.SlaDeadlineUtc.Value < sla.Now)
            return false;

        var warnHours = sla.WarnMap.GetValueOrDefault((s.Queue, s.Status), 12);
        var hoursLeft = (s.SlaDeadlineUtc.Value - sla.Now).TotalHours;
        return hoursLeft <= warnHours;
    }

    private static bool IsCoordinarVencido(ServiceSnapshot s) =>
        s.Status == AuditStatus.Amarillo &&
        s.FechaTurnoUtc.HasValue &&
        s.FechaTurnoUtc.Value < DateTime.UtcNow;

    private static bool IsCronicoPendiente(ServiceSnapshot s) =>
        s.IsChronicPeriodic &&
        (s.NeedsOperadorAssignment || CasosAProcesarStatuses.Contains(s.Status));

    private sealed record ServiceSnapshot(
        Guid Id,
        Guid? OperadorId,
        string? OperadorName,
        string Art,
        AuditQueue Queue,
        AuditStatus Status,
        DateTime FechaIngresoUtc,
        DateTime? FechaTurnoUtc,
        DateTime? SlaDeadlineUtc,
        bool IsChronicPeriodic,
        bool NeedsOperadorAssignment);

    private sealed record HistorySnapshot(Guid AuditServiceId, AuditStatus ToStatus, DateTime AtUtc);

    private sealed record SlaContext(
        DateTime Now,
        Dictionary<(AuditQueue Queue, AuditStatus Status), int> WarnMap);
}
