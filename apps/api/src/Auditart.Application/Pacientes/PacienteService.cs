using Auditart.Application.Abstractions;
using Auditart.Application.Sla;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Pacientes;

public sealed class PacienteService
{
    private readonly IAppDbContext _db;
    private readonly SlaRuleService _slaRules;

    public PacienteService(IAppDbContext db, SlaRuleService slaRules)
    {
        _db = db;
        _slaRules = slaRules;
    }

    public async Task<Paciente> FindOrCreateAsync(
        string nombre,
        string? dni,
        string? telefono,
        string? email,
        string? art,
        string? numeroSiniestro,
        CancellationToken ct)
    {
        var existing = await FindMatchAsync(nombre, dni, ct);
        if (existing is not null)
        {
            existing.UpdateContact(telefono, email, art, numeroSiniestro);
            return existing;
        }

        var created = Paciente.Create(nombre, dni, telefono, email, art, numeroSiniestro);
        _db.Add(created);
        return created;
    }

    public async Task<Paciente?> FindMatchAsync(string nombre, string? dni, CancellationToken ct)
    {
        var dniNorm = Paciente.NormalizeDni(dni);
        var nombreNorm = Paciente.NormalizeNombre(nombre);
        if (dniNorm is null || string.IsNullOrWhiteSpace(nombreNorm))
            return null;

        return await _db.Pacientes.FirstOrDefaultAsync(
            p => p.DniNormalizado == dniNorm && p.NombreNormalizado == nombreNorm,
            ct);
    }

    public async Task<PacienteDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Paciente no encontrado.");

        var prestaciones = await _db.AuditServices.AsNoTracking()
            .Where(s => s.PacienteId == id)
            .OrderByDescending(s => s.FechaIngresoUtc)
            .Select(s => new PacientePrestacionDto(
                s.Id,
                s.Numero,
                s.TipoServicio,
                s.Especialidad,
                s.Status,
                s.Queue,
                s.Profesional,
                s.FechaIngresoUtc,
                s.FechaTurnoUtc,
                s.IsChronicPeriodic,
                s.ValorPactado,
                s.AutorizacionArt
                    || s.AutorizacionCodigo != null
                    || s.AutorizacionDocumentoAttachmentId != null))
            .ToListAsync(ct);

        return Map(entity, prestaciones);
    }

    public async Task<PacienteDto> UpdateAsync(Guid id, UpdatePacienteRequest request, CancellationToken ct)
    {
        var entity = await _db.Pacientes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Paciente no encontrado.");

        if (string.IsNullOrWhiteSpace(request.Nombre))
            throw new ArgumentException("El nombre del paciente es obligatorio.");

        entity.Update(
            request.Nombre,
            request.Dni,
            request.Telefono,
            request.Email,
            request.Art,
            request.NumeroSiniestro);

        // Propagar a prestaciones abiertas (no cerradas en Celeste).
        var abiertas = await _db.AuditServices
            .Where(s => s.PacienteId == id && s.Status != AuditStatus.Celeste)
            .ToListAsync(ct);
        foreach (var prestacion in abiertas)
            prestacion.SyncPacienteSnapshot(entity);

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AuditService> CreatePrestacionAsync(
        Guid pacienteId,
        CreatePrestacionRequest request,
        Guid? operadorId,
        CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct)
            ?? throw new KeyNotFoundException("Paciente no encontrado.");

        var nextNumero = await _db.AuditServices.AnyAsync(ct)
            ? await _db.AuditServices.MaxAsync(s => s.Numero, ct) + 1
            : 1001;

        var queue = request.Queue ?? AuditQueue.General;
        if (request.CoordinacionTardia && request.Queue is null)
            queue = AuditQueue.Cronicos;

        var service = AuditService.CreateForPaciente(
            nextNumero,
            paciente,
            request.TipoServicio ?? ServiceType.Consultorio,
            queue,
            request.OperadorId ?? operadorId,
            request.Especialidad,
            request.Notas,
            request.CoordinacionTardia,
            request.Periodicity,
            request.IntervalDays,
            request.ScheduleStartUtc);

        var deadline = await _slaRules.ComputeDeadlineAsync(queue, AuditStatus.Rojo, DateTime.UtcNow, ct);
        service.ApplySlaDeadline(deadline);
        _db.Add(service);
        await _db.SaveChangesAsync(ct);
        return service;
    }

    public async Task LinkServiceAsync(AuditService service, CancellationToken ct)
    {
        var paciente = await FindOrCreateAsync(
            service.Paciente,
            service.Dni,
            service.TelefonoPaciente,
            service.EmailPaciente,
            service.Art,
            service.NumeroSiniestro,
            ct);
        service.LinkPaciente(paciente.Id);
    }

    public async Task BackfillMissingAsync(CancellationToken ct)
    {
        var orphan = await _db.AuditServices
            .Where(s => s.PacienteId == null)
            .ToListAsync(ct);

        foreach (var service in orphan)
            await LinkServiceAsync(service, ct);

        if (orphan.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private static PacienteDto Map(Paciente p, IReadOnlyList<PacientePrestacionDto> prestaciones) =>
        new(
            p.Id,
            p.Nombre,
            p.Dni,
            p.Telefono,
            p.Email,
            p.Art,
            p.NumeroSiniestro,
            prestaciones.Count(x => x.Status != AuditStatus.Celeste),
            prestaciones);
}

public sealed record PacienteDto(
    Guid Id,
    string Nombre,
    string? Dni,
    string? Telefono,
    string? Email,
    string? Art,
    string? NumeroSiniestro,
    int PrestacionesAbiertas,
    IReadOnlyList<PacientePrestacionDto> Prestaciones);

public sealed record PacientePrestacionDto(
    Guid Id,
    int Numero,
    ServiceType TipoServicio,
    string? Especialidad,
    AuditStatus Status,
    AuditQueue Queue,
    string? Profesional,
    DateTime FechaIngresoUtc,
    DateTime? FechaTurnoUtc,
    bool CoordinacionTardia,
    decimal? ValorPactado,
    bool TieneAutorizacion);

public sealed record CreatePrestacionRequest(
    ServiceType? TipoServicio,
    AuditQueue? Queue,
    string? Especialidad,
    string? Notas,
    Guid? OperadorId,
    bool CoordinacionTardia = false,
    ChronicPeriodicity? Periodicity = null,
    int? IntervalDays = null,
    DateTime? ScheduleStartUtc = null);

public sealed record UpdatePacienteRequest(
    string Nombre,
    string? Dni,
    string? Telefono,
    string? Email,
    string? Art,
    string? NumeroSiniestro);
