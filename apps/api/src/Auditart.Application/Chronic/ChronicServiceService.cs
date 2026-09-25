using Auditart.Application.Abstractions;
using Auditart.Application.Pacientes;
using Auditart.Application.Sla;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Chronic;

public sealed record CreateChronicServiceCommand(
    string Paciente,
    string Art,
    string NumeroSiniestro,
    string TelefonoPaciente,
    string EmailPaciente,
    ChronicPeriodicity Periodicity,
    int? IntervalDays,
    DateTime? ScheduleStartUtc,
    Guid? OperadorId,
    ServiceType TipoServicio,
    string? Especialidad,
    string? Dni,
    string? Notas);

public sealed record UpdateChronicScheduleCommand(
    ChronicPeriodicity Periodicity,
    int? IntervalDays,
    DateTime? ScheduleStartUtc);

public sealed class ChronicServiceService
{
    private readonly IAppDbContext _db;
    private readonly SlaRuleService _slaRules;
    private readonly PacienteService _pacientes;

    public ChronicServiceService(IAppDbContext db, SlaRuleService slaRules, PacienteService pacientes)
    {
        _db = db;
        _slaRules = slaRules;
        _pacientes = pacientes;
    }

    public async Task<AuditService> CreateManualAsync(CreateChronicServiceCommand command, CancellationToken ct)
    {
        ValidateContactFields(command.Art, command.NumeroSiniestro, command.TelefonoPaciente, command.EmailPaciente);

        if (command.OperadorId.HasValue)
            await ValidateOperadorAsync(command.OperadorId.Value, ct);

        var nextNumero = await _db.AuditServices.AnyAsync(ct)
            ? await _db.AuditServices.MaxAsync(s => s.Numero, ct) + 1
            : 1001;

        var service = AuditService.CreateChronicManual(
            nextNumero,
            command.Paciente,
            command.Art,
            command.NumeroSiniestro,
            command.TelefonoPaciente,
            command.EmailPaciente,
            command.Periodicity,
            command.IntervalDays,
            command.ScheduleStartUtc,
            command.OperadorId,
            command.TipoServicio,
            command.Especialidad,
            command.Dni,
            command.Notas);

        var deadline = await _slaRules.ComputeDeadlineAsync(
            AuditQueue.Cronicos,
            AuditStatus.Rojo,
            DateTime.UtcNow,
            ct);
        service.ApplySlaDeadline(deadline);

        _db.Add(service);
        await _db.SaveChangesAsync(ct);
        return service;
    }

    public async Task UpdateScheduleAsync(Guid serviceId, UpdateChronicScheduleCommand command, CancellationToken ct)
    {
        var service = await _db.AuditServices.FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Servicio no encontrado.");

        if (!service.IsChronicPeriodic)
            throw new DomainException("El servicio no es un crónico periódico.");

        service.ApplyChronicSchedule(command.Periodicity, command.IntervalDays, command.ScheduleStartUtc);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignOperadorAsync(Guid serviceId, Guid operadorId, CancellationToken ct)
    {
        var service = await _db.AuditServices.FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Servicio no encontrado.");

        await ValidateOperadorAsync(operadorId, ct);
        service.AssignOperador(operadorId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RenewAsync(Guid serviceId, Guid? changedByUserId, CancellationToken ct)
    {
        var service = await _db.AuditServices.FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Servicio no encontrado.");

        await RenewServiceAsync(service, changedByUserId, "Renovación manual", ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ProcessDueRenewalsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dueServices = await _db.AuditServices
            .Where(s =>
                s.IsChronicPeriodic &&
                s.Status != AuditStatus.Rojo &&
                s.NextRenewalDueUtc != null &&
                s.NextRenewalDueUtc <= now)
            .ToListAsync(ct);

        var renewed = 0;
        foreach (var service in dueServices)
        {
            await RenewServiceAsync(service, null, null, ct);
            renewed++;
        }

        if (renewed > 0)
            await _db.SaveChangesAsync(ct);

        return renewed;
    }

    private async Task RenewServiceAsync(
        AuditService service,
        Guid? changedByUserId,
        string? reason,
        CancellationToken ct)
    {
        await ResolveOperadorForRenewalAsync(service, ct);
        service.RenewForPeriodicCycle(changedByUserId, reason);
        var deadline = await _slaRules.ComputeDeadlineAsync(
            service.Queue,
            AuditStatus.Rojo,
            DateTime.UtcNow,
            ct);
        service.ApplySlaDeadline(deadline);
    }

    private async Task ResolveOperadorForRenewalAsync(AuditService service, CancellationToken ct)
    {
        if (service.OperadorId is null)
        {
            service.MarkOperadorMissing();
            return;
        }

        var isActive = await _db.Users.AnyAsync(
            u => u.Id == service.OperadorId && u.IsActive,
            ct);

        if (!isActive)
            service.MarkOperadorMissing();
    }

    private async Task ValidateOperadorAsync(Guid operadorId, CancellationToken ct)
    {
        var operador = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == operadorId && u.IsActive && u.Role == UserRole.Cronicos,
            ct);

        if (operador is null)
            throw new InvalidOperationException("Operador inválido para cola crónicos.");
    }

    private static void ValidateContactFields(
        string art,
        string numeroSiniestro,
        string telefonoPaciente,
        string emailPaciente)
    {
        if (string.IsNullOrWhiteSpace(art))
            throw new InvalidOperationException("La ART (aseguradora) es obligatoria.");
        if (string.IsNullOrWhiteSpace(numeroSiniestro))
            throw new InvalidOperationException("El número de siniestro es obligatorio.");
        if (string.IsNullOrWhiteSpace(telefonoPaciente))
            throw new InvalidOperationException("El teléfono del paciente es obligatorio.");
        if (string.IsNullOrWhiteSpace(emailPaciente))
            throw new InvalidOperationException("El correo del paciente es obligatorio.");
    }
}
