using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Sla;

public sealed record SlaRuleDto(
    Guid Id,
    AuditQueue Queue,
    AuditStatus Status,
    int DurationValue,
    SlaDurationUnit DurationUnit,
    int WarnBeforeHours,
    bool IsEnabled);

public sealed record UpdateSlaRuleCommand(
    int DurationValue,
    SlaDurationUnit DurationUnit,
    int WarnBeforeHours,
    bool IsEnabled);

public sealed class SlaRuleService
{
    private static readonly AuditStatus[] ConfigurableStatuses =
    [
        AuditStatus.Rojo,
        AuditStatus.Amarillo,
        AuditStatus.Azul,
        AuditStatus.Verde
    ];

    private readonly IAppDbContext _db;

    public SlaRuleService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        var existing = await _db.SlaRules
            .Select(r => new { r.Queue, r.Status })
            .ToListAsync(ct);
        var set = existing.Select(x => (x.Queue, x.Status)).ToHashSet();

        foreach (var queue in Enum.GetValues<AuditQueue>())
        {
            foreach (var status in ConfigurableStatuses)
            {
                if (set.Contains((queue, status))) continue;
                var (value, unit, warn) = DefaultFor(status);
                _db.Add(SlaRule.Create(queue, status, value, unit, warn));
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SlaRuleDto>> ListAsync(CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        return await _db.SlaRules
            .AsNoTracking()
            .OrderBy(r => r.Queue)
            .ThenBy(r => r.Status)
            .Select(r => new SlaRuleDto(
                r.Id,
                r.Queue,
                r.Status,
                r.DurationValue,
                r.DurationUnit,
                r.WarnBeforeHours,
                r.IsEnabled))
            .ToListAsync(ct);
    }

    public async Task<SlaRule?> FindEnabledAsync(AuditQueue queue, AuditStatus status, CancellationToken ct) =>
        await _db.SlaRules.FirstOrDefaultAsync(
            r => r.Queue == queue && r.Status == status && r.IsEnabled,
            ct);

    public async Task UpdateAsync(Guid id, UpdateSlaRuleCommand command, CancellationToken ct)
    {
        var rule = await _db.SlaRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Regla SLA no encontrada.");

        rule.Update(command.DurationValue, command.DurationUnit, command.WarnBeforeHours, command.IsEnabled);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DateTime?> ComputeDeadlineAsync(
        AuditQueue queue,
        AuditStatus status,
        DateTime fromUtc,
        CancellationToken ct)
    {
        var rule = await FindEnabledAsync(queue, status, ct);
        return rule?.ComputeDeadline(fromUtc);
    }

    /// <summary>
    /// Asigna deadline a servicios activos que no tienen SlaDeadlineUtc
    /// (p. ej. creados antes de habilitar reglas SLA).
    /// </summary>
    public async Task<int> BackfillMissingDeadlinesAsync(CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);

        var rules = await _db.SlaRules.AsNoTracking().Where(r => r.IsEnabled).ToListAsync(ct);
        var ruleMap = rules.ToDictionary(r => (r.Queue, r.Status));

        var services = await _db.AuditServices
            .Where(s =>
                s.SlaDeadlineUtc == null &&
                (s.Status == AuditStatus.Rojo ||
                 s.Status == AuditStatus.Amarillo ||
                 s.Status == AuditStatus.Azul ||
                 s.Status == AuditStatus.Verde))
            .ToListAsync(ct);

        if (services.Count == 0)
            return 0;

        var serviceIds = services.Select(s => s.Id).ToList();
        var history = await _db.ServiceStatusHistories
            .AsNoTracking()
            .Where(h => serviceIds.Contains(h.AuditServiceId))
            .Select(h => new { h.AuditServiceId, h.ToStatus, h.CreatedAtUtc })
            .ToListAsync(ct);

        var historyByService = history
            .GroupBy(h => h.AuditServiceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var updated = 0;
        foreach (var service in services)
        {
            if (!ruleMap.TryGetValue((service.Queue, service.Status), out var rule))
                continue;

            DateTime? enteredAt = null;
            if (historyByService.TryGetValue(service.Id, out var events))
            {
                enteredAt = events
                    .Where(e => e.ToStatus == service.Status)
                    .OrderByDescending(e => e.CreatedAtUtc)
                    .Select(e => (DateTime?)e.CreatedAtUtc)
                    .FirstOrDefault();
            }

            var anchor = enteredAt ?? ResolveFallbackAnchor(service);
            service.ApplySlaDeadline(rule.ComputeDeadline(anchor));
            updated++;
        }

        if (updated > 0)
            await _db.SaveChangesAsync(ct);

        return updated;
    }

    private static DateTime ResolveFallbackAnchor(AuditService service) =>
        service.Status switch
        {
            AuditStatus.Rojo => service.FechaIngresoUtc,
            AuditStatus.Amarillo => service.FechaTurnoUtc ?? service.FechaIngresoUtc,
            AuditStatus.Azul => service.FechaConsultaUtc ?? service.FechaTurnoUtc ?? service.FechaIngresoUtc,
            AuditStatus.Verde => service.FechaConsultaUtc ?? service.FechaIngresoUtc,
            _ => service.FechaIngresoUtc
        };

    public static (int Value, SlaDurationUnit Unit, int WarnBeforeHours) DefaultFor(AuditStatus status) =>
        status switch
        {
            AuditStatus.Rojo => (24, SlaDurationUnit.Hours, 4),
            AuditStatus.Amarillo => (24, SlaDurationUnit.Hours, 4),
            AuditStatus.Azul => (48, SlaDurationUnit.Hours, 12),
            AuditStatus.Verde => (5, SlaDurationUnit.Days, 24),
            _ => (24, SlaDurationUnit.Hours, 4)
        };
}
