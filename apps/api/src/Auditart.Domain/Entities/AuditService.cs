using Auditart.Domain.Common;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;

namespace Auditart.Domain.Entities;

/// <summary>
/// Servicio de auditoría / turno operativo (reemplazo de fila Excel).
/// </summary>
public class AuditService : Entity
{
    public int Numero { get; private set; }
    public Guid? PacienteId { get; private set; }
    public Paciente? PacienteEntity { get; private set; }
    public string Paciente { get; private set; } = string.Empty;
    public string? Dni { get; private set; }
    public string Art { get; private set; } = string.Empty;
    public string? NumeroSiniestro { get; private set; }
    public string? TelefonoPaciente { get; private set; }
    public string? EmailPaciente { get; private set; }
    public ServiceType TipoServicio { get; private set; }
    public string? Especialidad { get; private set; }
    public string? Profesional { get; private set; }
    public Guid? PrestadorId { get; private set; }
    public Prestador? Prestador { get; private set; }

    public Guid? OperadorId { get; private set; }
    public User? Operador { get; private set; }
    public AuditQueue Queue { get; private set; }
    public AuditStatus Status { get; private set; } = AuditStatus.Rojo;
    public UrgencyLevel Urgency { get; private set; } = UrgencyLevel.Normal;

    public DateTime FechaIngresoUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? FechaTurnoUtc { get; private set; }
    public DateTime? FechaConsultaUtc { get; private set; }
    public DateTime? SlaDeadlineUtc { get; private set; }

    /// <summary>Valor de consulta / lo que se paga al profesional.</summary>
    public decimal? ValorPactado { get; private set; }
    /// <summary>Precio conciliado a cobrar a la ART.</summary>
    public decimal? ValorConciliadoArt { get; private set; }
    public TipoProfesional? TipoProfesional { get; private set; }
    /// <summary>Solo especialista: 50 o 100 (% del precio de catálogo cotizado a la ART).</summary>
    public int? PorcentajeConciliacionEspecialista { get; private set; }
    public Guid? PrecioCatalogoId { get; private set; }
    public PrecioCatalogo? PrecioCatalogo { get; private set; }
    public bool PresupuestoEnviado { get; private set; }
    public bool AutorizacionArt { get; private set; }
    public bool Autofisica { get; private set; }
    public bool RequierePagoAnticipado { get; private set; }
    public string? AutorizacionCodigo { get; private set; }
    public Guid? AutorizacionDocumentoAttachmentId { get; private set; }

    public string? Notas { get; private set; }
    public Guid? IncomingEmailId { get; private set; }
    public IncomingEmail? IncomingEmail { get; private set; }
    public Guid? EmailRequestId { get; private set; }
    public EmailRequest? EmailRequest { get; private set; }

    public bool IsChronicPeriodic { get; private set; }
    public ChronicPeriodicity? ChronicPeriodicity { get; private set; }
    public int? ChronicIntervalDays { get; private set; }
    public DateTime? ChronicScheduleStartUtc { get; private set; }
    public DateTime? NextRenewalDueUtc { get; private set; }
    public DateTime? LastRenewedAtUtc { get; private set; }
    public int ChronicRenewalCount { get; private set; }
    public bool NeedsOperadorAssignment { get; private set; }

    public ICollection<ServiceAttachment> Attachments { get; private set; } = new List<ServiceAttachment>();
    public ICollection<ServiceStatusHistory> StatusHistory { get; private set; } = new List<ServiceStatusHistory>();

    private AuditService() { }

    public static AuditService CreateFromTriage(
        int numero,
        string paciente,
        string art,
        ServiceType tipoServicio,
        AuditQueue queue,
        Guid? operadorId,
        UrgencyLevel urgency,
        Guid? incomingEmailId,
        string? especialidad = null,
        string? dni = null,
        string? notas = null,
        Guid? emailRequestId = null,
        string? numeroSiniestro = null,
        string? telefonoPaciente = null,
        string? emailPaciente = null)
    {
        var service = new AuditService
        {
            Numero = numero,
            Paciente = paciente.Trim(),
            Art = art.Trim(),
            NumeroSiniestro = string.IsNullOrWhiteSpace(numeroSiniestro) ? null : numeroSiniestro.Trim(),
            TelefonoPaciente = string.IsNullOrWhiteSpace(telefonoPaciente) ? null : telefonoPaciente.Trim(),
            EmailPaciente = string.IsNullOrWhiteSpace(emailPaciente) ? null : emailPaciente.Trim(),
            TipoServicio = tipoServicio,
            Queue = queue,
            OperadorId = operadorId,
            Urgency = urgency,
            IncomingEmailId = incomingEmailId,
            EmailRequestId = emailRequestId,
            Especialidad = especialidad,
            Dni = dni,
            Notas = notas,
            Status = AuditStatus.Rojo,
            FechaIngresoUtc = DateTime.UtcNow
        };

        service.StatusHistory.Add(ServiceStatusHistory.Create(
            service.Id,
            null,
            AuditStatus.Rojo,
            operadorId,
            "Creado desde triage"));

        return service;
    }

    public static AuditService CreateForPaciente(
        int numero,
        Paciente paciente,
        ServiceType tipoServicio,
        AuditQueue queue,
        Guid? operadorId,
        string? especialidad = null,
        string? notas = null,
        bool coordinacionTardia = false,
        Domain.Enums.ChronicPeriodicity? periodicity = null,
        int? intervalDays = null,
        DateTime? scheduleStartUtc = null)
    {
        var service = new AuditService
        {
            Numero = numero,
            PacienteId = paciente.Id,
            Paciente = paciente.Nombre,
            Dni = paciente.Dni,
            Art = paciente.Art ?? "Por definir",
            NumeroSiniestro = paciente.NumeroSiniestro,
            TelefonoPaciente = paciente.Telefono,
            EmailPaciente = paciente.Email,
            TipoServicio = tipoServicio,
            Queue = queue,
            OperadorId = operadorId,
            Urgency = UrgencyLevel.Normal,
            Especialidad = especialidad,
            Notas = notas,
            Status = AuditStatus.Rojo,
            FechaIngresoUtc = DateTime.UtcNow,
            NeedsOperadorAssignment = operadorId is null
        };

        if (coordinacionTardia)
            service.ApplyChronicSchedule(
                periodicity ?? Domain.Enums.ChronicPeriodicity.Monthly,
                intervalDays,
                scheduleStartUtc ?? DateTime.UtcNow);

        service.StatusHistory.Add(ServiceStatusHistory.Create(
            service.Id,
            null,
            AuditStatus.Rojo,
            operadorId,
            coordinacionTardia
                ? "Prestación de coordinación tardía creada desde paciente"
                : "Prestación creada desde paciente"));

        return service;
    }

    public void LinkPaciente(Guid pacienteId)
    {
        PacienteId = pacienteId;
        Touch();
    }

    /// <summary>Sincroniza el snapshot de datos del paciente en la prestación.</summary>
    public void SyncPacienteSnapshot(Paciente paciente)
    {
        Paciente = paciente.Nombre;
        Dni = paciente.Dni;
        Art = string.IsNullOrWhiteSpace(paciente.Art) ? Art : paciente.Art;
        NumeroSiniestro = paciente.NumeroSiniestro;
        TelefonoPaciente = paciente.Telefono;
        EmailPaciente = paciente.Email;
        Touch();
    }

    public static AuditService CreateChronicManual(
        int numero,
        string paciente,
        string art,
        string numeroSiniestro,
        string telefonoPaciente,
        string emailPaciente,
        ChronicPeriodicity periodicity,
        int? intervalDays,
        DateTime? scheduleStartUtc,
        Guid? operadorId,
        ServiceType tipoServicio,
        string? especialidad = null,
        string? dni = null,
        string? notas = null)
    {
        var service = new AuditService
        {
            Numero = numero,
            Paciente = paciente.Trim(),
            Art = art.Trim(),
            NumeroSiniestro = numeroSiniestro.Trim(),
            TelefonoPaciente = telefonoPaciente.Trim(),
            EmailPaciente = emailPaciente.Trim(),
            TipoServicio = tipoServicio,
            Queue = AuditQueue.Cronicos,
            OperadorId = operadorId,
            Urgency = UrgencyLevel.Normal,
            Especialidad = especialidad,
            Dni = dni,
            Notas = notas,
            Status = AuditStatus.Rojo,
            FechaIngresoUtc = DateTime.UtcNow,
            IsChronicPeriodic = true,
            NeedsOperadorAssignment = operadorId is null
        };

        service.ApplyChronicSchedule(periodicity, intervalDays, scheduleStartUtc);

        service.StatusHistory.Add(ServiceStatusHistory.Create(
            service.Id,
            null,
            AuditStatus.Rojo,
            operadorId,
            "Creado manual — crónico periódico"));

        return service;
    }

    public void ApplyChronicSchedule(
        ChronicPeriodicity periodicity,
        int? intervalDays,
        DateTime? scheduleStartUtc)
    {
        if (periodicity == Domain.Enums.ChronicPeriodicity.EveryXDays && intervalDays is null or < 1)
            throw new DomainException("Para periodicidad cada X días, el intervalo debe ser mayor a 0.");

        IsChronicPeriodic = true;
        ChronicPeriodicity = periodicity;
        ChronicIntervalDays = periodicity == Domain.Enums.ChronicPeriodicity.EveryXDays ? intervalDays : null;
        ChronicScheduleStartUtc = scheduleStartUtc ?? DateTime.UtcNow;
        NextRenewalDueUtc = CalculateNextRenewalDue(ChronicScheduleStartUtc.Value);
        Touch();
    }

    public void RenewForPeriodicCycle(Guid? changedByUserId, string? reason = null)
    {
        if (!IsChronicPeriodic)
            throw new DomainException("El servicio no es un crónico periódico.");

        var from = Status;
        Status = AuditStatus.Rojo;
        FechaTurnoUtc = null;
        FechaConsultaUtc = null;
        SlaDeadlineUtc = null;
        PresupuestoEnviado = false;
        AutorizacionArt = false;
        Autofisica = false;

        LastRenewedAtUtc = DateTime.UtcNow;
        ChronicRenewalCount++;
        NextRenewalDueUtc = CalculateNextRenewalDue(LastRenewedAtUtc.Value);
        NeedsOperadorAssignment = OperadorId is null;

        StatusHistory.Add(ServiceStatusHistory.Create(
            Id,
            from,
            AuditStatus.Rojo,
            changedByUserId,
            reason ?? $"Renovación periódica ciclo {ChronicRenewalCount}"));

        Touch();
    }

    public void MarkOperadorMissing()
    {
        OperadorId = null;
        NeedsOperadorAssignment = true;
        Touch();
    }

    public DateTime CalculateNextRenewalDue(DateTime fromUtc)
    {
        if (ChronicPeriodicity is null)
            throw new DomainException("Periodicidad crónica no configurada.");

        return ChronicPeriodicity switch
        {
            Domain.Enums.ChronicPeriodicity.Weekly => fromUtc.AddDays(7),
            Domain.Enums.ChronicPeriodicity.Monthly => fromUtc.AddMonths(1),
            Domain.Enums.ChronicPeriodicity.EveryXDays => fromUtc.AddDays(ChronicIntervalDays ?? 1),
            _ => throw new DomainException("Periodicidad crónica inválida.")
        };
    }

    public void AssignOperador(Guid operadorId)
    {
        OperadorId = operadorId;
        NeedsOperadorAssignment = false;
        Touch();
    }

    public void TransitionTo(
        AuditStatus next,
        Guid? changedByUserId,
        string? reason = null,
        DateTime? slaDeadlineUtc = null)
    {
        if (!AuditStatusMachine.CanTransition(Status, next))
        {
            throw new DomainException(
                $"Transición inválida: {Status} → {next}. Flujo: Rojo → Amarillo → Azul/Verde → Celeste.");
        }

        if (next == AuditStatus.Verde && !HasAutorizacion())
        {
            throw new DomainException(
                "Para pasar a Verde hace falta la autorización (código/radicado y/o PDF). Si aún no llegó, pasá a Azul para esperarla.");
        }

        var from = Status;
        Status = next;

        if (next == AuditStatus.Azul)
            FechaConsultaUtc ??= DateTime.UtcNow;

        if (next == AuditStatus.Celeste)
            SlaDeadlineUtc = null;
        else if (slaDeadlineUtc.HasValue)
            SlaDeadlineUtc = slaDeadlineUtc;
        else if (next == AuditStatus.Azul && !slaDeadlineUtc.HasValue)
            SlaDeadlineUtc = (FechaConsultaUtc ?? DateTime.UtcNow).AddHours(48);

        StatusHistory.Add(ServiceStatusHistory.Create(Id, from, next, changedByUserId, reason));
        Touch();
    }

    public void ApplySlaDeadline(DateTime? deadlineUtc)
    {
        SlaDeadlineUtc = deadlineUtc;
        Touch();
    }

    public void SetTurno(
        DateTime fechaTurnoUtc,
        string? profesional,
        Guid? prestadorId = null,
        decimal? valorConsulta = null,
        bool? requierePagoAnticipado = null)
    {
        FechaTurnoUtc = fechaTurnoUtc;
        if (!string.IsNullOrWhiteSpace(profesional))
            Profesional = profesional.Trim();
        if (prestadorId.HasValue)
            PrestadorId = prestadorId;
        if (valorConsulta.HasValue)
            ValorPactado = valorConsulta;
        if (requierePagoAnticipado.HasValue)
            RequierePagoAnticipado = requierePagoAnticipado.Value;
        Touch();
    }

    public void AssignPrestador(
        Guid prestadorId,
        string profesionalNombre,
        decimal? valorConsulta = null,
        bool? requierePagoAnticipado = null)
    {
        PrestadorId = prestadorId;
        Profesional = profesionalNombre.Trim();
        if (valorConsulta.HasValue)
            ValorPactado = valorConsulta;
        if (requierePagoAnticipado.HasValue)
            RequierePagoAnticipado = requierePagoAnticipado.Value;
        Touch();
    }

    public void SetAutorizacion(string? codigo, Guid? documentoAttachmentId)
    {
        var code = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
        if (code is null && documentoAttachmentId is null)
            throw new DomainException("Indicá un código de autorización, un PDF, o ambos.");

        AutorizacionCodigo = code;
        AutorizacionDocumentoAttachmentId = documentoAttachmentId;
        AutorizacionArt = true;
        Touch();
    }

    public void UpdateCommercialFlags(
        bool? presupuestoEnviado = null,
        bool? autorizacionArt = null,
        bool? autofisica = null,
        decimal? valorPactado = null,
        decimal? valorConciliadoArt = null)
    {
        if (presupuestoEnviado.HasValue) PresupuestoEnviado = presupuestoEnviado.Value;
        if (autorizacionArt.HasValue) AutorizacionArt = autorizacionArt.Value;
        if (autofisica.HasValue) Autofisica = autofisica.Value;
        if (valorPactado.HasValue) ValorPactado = valorPactado;
        if (valorConciliadoArt.HasValue) ValorConciliadoArt = valorConciliadoArt;
        Touch();
    }

    /// <summary>
    /// Negociación comercial en Rojo: modalidad auditor/especialista + valores.
    /// </summary>
    public void SetPreciosRojo(
        TipoProfesional tipoProfesional,
        decimal? valorConsulta,
        decimal? valorConciliadoArt,
        Guid? precioCatalogoId = null,
        int? porcentajeConciliacionEspecialista = null)
    {
        if (Status != AuditStatus.Rojo)
            throw new DomainException("Los precios conciliados solo se definen en estado Rojo.");

        if (valorConsulta is < 0)
            throw new DomainException("El valor de consulta no puede ser negativo.");
        if (valorConciliadoArt is < 0)
            throw new DomainException("El precio conciliado no puede ser negativo.");

        if (tipoProfesional == global::Auditart.Domain.Enums.TipoProfesional.Especialista)
        {
            var pct = porcentajeConciliacionEspecialista
                ?? PorcentajeConciliacionEspecialista
                ?? 100;
            if (pct is not (50 or 100))
                throw new DomainException("Para especialista elegí 50% más o 100% más del precio.");
            PorcentajeConciliacionEspecialista = pct;
        }
        else
        {
            PorcentajeConciliacionEspecialista = null;
        }

        TipoProfesional = tipoProfesional;
        PrecioCatalogoId = precioCatalogoId;
        if (valorConsulta.HasValue) ValorPactado = valorConsulta;
        if (valorConciliadoArt.HasValue) ValorConciliadoArt = valorConciliadoArt;
        Touch();
    }

    public void UpdateNotes(string? notas)
    {
        Notas = notas;
        Touch();
    }

    public bool HasAutorizacion() =>
        AutorizacionArt
        || !string.IsNullOrWhiteSpace(AutorizacionCodigo)
        || AutorizacionDocumentoAttachmentId.HasValue;
}
