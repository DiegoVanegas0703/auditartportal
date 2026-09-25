using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Sla;

public sealed record InAppAlertDto(
    Guid Id,
    Guid AuditServiceId,
    int? ServiceNumero,
    string? Paciente,
    InAppAlertKind Kind,
    AuditStatus ServiceStatus,
    AuditQueue Queue,
    DateTime DeadlineUtc,
    string Message,
    bool IsRead,
    DateTime CreatedAtUtc);

public sealed class InAppAlertService
{
    private readonly IAppDbContext _db;

    public InAppAlertService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<InAppAlertDto>> ListForUserAsync(
        Guid userId,
        bool unreadOnly,
        CancellationToken ct)
    {
        var query = _db.InAppAlerts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.ResolvedAtUtc == null);

        if (unreadOnly)
            query = query.Where(a => !a.IsRead);

        return await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(100)
            .Select(a => new InAppAlertDto(
                a.Id,
                a.AuditServiceId,
                a.AuditService != null ? a.AuditService.Numero : null,
                a.AuditService != null ? a.AuditService.Paciente : null,
                a.Kind,
                a.ServiceStatus,
                a.Queue,
                a.DeadlineUtc,
                a.Message,
                a.IsRead,
                a.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct) =>
        await _db.InAppAlerts.CountAsync(
            a => a.UserId == userId && !a.IsRead && a.ResolvedAtUtc == null,
            ct);

    public async Task MarkReadAsync(Guid alertId, Guid userId, CancellationToken ct)
    {
        var alert = await _db.InAppAlerts.FirstOrDefaultAsync(
            a => a.Id == alertId && a.UserId == userId,
            ct) ?? throw new KeyNotFoundException("Alerta no encontrada.");

        alert.MarkRead();
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var alerts = await _db.InAppAlerts
            .Where(a => a.UserId == userId && !a.IsRead && a.ResolvedAtUtc == null)
            .ToListAsync(ct);

        foreach (var alert in alerts)
            alert.MarkRead();

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ProcessDueAlertsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rules = await _db.SlaRules.AsNoTracking().Where(r => r.IsEnabled).ToListAsync(ct);
        var ruleMap = rules.ToDictionary(r => (r.Queue, r.Status));

        var services = await _db.AuditServices
            .Where(s =>
                s.SlaDeadlineUtc != null &&
                (s.Status == AuditStatus.Rojo ||
                 s.Status == AuditStatus.Amarillo ||
                 s.Status == AuditStatus.Azul ||
                 s.Status == AuditStatus.Verde))
            .ToListAsync(ct);

        var jefaturaIds = await _db.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Jefatura || u.Role == UserRole.Admin))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var created = 0;

        foreach (var service in services)
        {
            if (!service.SlaDeadlineUtc.HasValue) continue;
            var deadline = service.SlaDeadlineUtc.Value;

            if (!ruleMap.TryGetValue((service.Queue, service.Status), out var rule))
                continue;

            var recipientIds = new HashSet<Guid>(jefaturaIds);
            if (service.OperadorId.HasValue)
                recipientIds.Add(service.OperadorId.Value);

            if (recipientIds.Count == 0) continue;

            if (now >= deadline)
            {
                created += await EnsureAlertsAsync(
                    service,
                    recipientIds,
                    InAppAlertKind.Expired,
                    deadline,
                    $"SLA vencido · #{service.Numero} · {service.Paciente} · {service.Status}",
                    ct);
            }
            else if (now >= rule.ComputeWarnAt(deadline))
            {
                created += await EnsureAlertsAsync(
                    service,
                    recipientIds,
                    InAppAlertKind.AboutToExpire,
                    deadline,
                    $"SLA próximo a vencer · #{service.Numero} · {service.Paciente} · {service.Status}",
                    ct);
            }
        }

        // Resolve alerts for services that left SLA tracking
        var openAlerts = await _db.InAppAlerts
            .Where(a => a.ResolvedAtUtc == null)
            .Include(a => a.AuditService)
            .ToListAsync(ct);

        foreach (var alert in openAlerts)
        {
            var s = alert.AuditService;
            if (s is null ||
                s.SlaDeadlineUtc is null ||
                s.Status == AuditStatus.Celeste ||
                s.Status != alert.ServiceStatus)
            {
                alert.Resolve();
            }
        }

        if (created > 0 || openAlerts.Any(a => a.ResolvedAtUtc != null))
            await _db.SaveChangesAsync(ct);

        return created;
    }

    private async Task<int> EnsureAlertsAsync(
        AuditService service,
        IEnumerable<Guid> recipientIds,
        InAppAlertKind kind,
        DateTime deadline,
        string message,
        CancellationToken ct)
    {
        var created = 0;
        foreach (var userId in recipientIds)
        {
            var exists = await _db.InAppAlerts.AnyAsync(
                a =>
                    a.AuditServiceId == service.Id &&
                    a.UserId == userId &&
                    a.Kind == kind &&
                    a.ServiceStatus == service.Status &&
                    a.ResolvedAtUtc == null,
                ct);

            if (exists) continue;

            _db.Add(InAppAlert.Create(
                service.Id,
                userId,
                kind,
                service.Status,
                service.Queue,
                deadline,
                message));
            created++;
        }

        return created;
    }
}
