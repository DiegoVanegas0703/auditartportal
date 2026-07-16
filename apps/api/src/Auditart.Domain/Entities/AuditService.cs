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
    public string Paciente { get; private set; } = string.Empty;
    public string? Dni { get; private set; }
    public string Art { get; private set; } = string.Empty;
    public ServiceType TipoServicio { get; private set; }
    public string? Especialidad { get; private set; }
    public string? Profesional { get; private set; }

    public Guid? OperadorId { get; private set; }
    public User? Operador { get; private set; }
    public AuditQueue Queue { get; private set; }
    public AuditStatus Status { get; private set; } = AuditStatus.Rojo;
    public UrgencyLevel Urgency { get; private set; } = UrgencyLevel.Normal;

    public DateTime FechaIngresoUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? FechaTurnoUtc { get; private set; }
    public DateTime? FechaConsultaUtc { get; private set; }
    public DateTime? SlaDeadlineUtc { get; private set; }

    public decimal? ValorPactado { get; private set; }
    public bool PresupuestoEnviado { get; private set; }
    public bool AutorizacionArt { get; private set; }
    public bool Autofisica { get; private set; }

    public string? Notas { get; private set; }
    public Guid? IncomingEmailId { get; private set; }
    public IncomingEmail? IncomingEmail { get; private set; }

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
        string? notas = null)
    {
        var service = new AuditService
        {
            Numero = numero,
            Paciente = paciente.Trim(),
            Art = art.Trim(),
            TipoServicio = tipoServicio,
            Queue = queue,
            OperadorId = operadorId,
            Urgency = urgency,
            IncomingEmailId = incomingEmailId,
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

    public void AssignOperador(Guid operadorId)
    {
        OperadorId = operadorId;
        Touch();
    }

    public void TransitionTo(
        AuditStatus next,
        Guid? changedByUserId,
        string? reason = null)
    {
        if (!AuditStatusMachine.CanTransition(Status, next))
        {
            throw new DomainException(
                $"Transición inválida: {Status} → {next}. Solo se permite el flujo Rojo → Amarillo → Azul → Verde.");
        }

        if (next == AuditStatus.Verde && !CanGoToVerde())
        {
            throw new DomainException(
                "No se puede pasar a Verde sin autorización ART o valor pactado, y autofísica o valor pactado confirmado.");
        }

        var from = Status;
        Status = next;

        if (next == AuditStatus.Azul)
        {
            FechaConsultaUtc ??= DateTime.UtcNow;
            SlaDeadlineUtc = (FechaConsultaUtc ?? DateTime.UtcNow).AddHours(48);
        }

        if (next == AuditStatus.Verde)
        {
            SlaDeadlineUtc = null;
        }

        StatusHistory.Add(ServiceStatusHistory.Create(Id, from, next, changedByUserId, reason));
        Touch();
    }

    public void SetTurno(DateTime fechaTurnoUtc, string? profesional)
    {
        FechaTurnoUtc = fechaTurnoUtc;
        if (!string.IsNullOrWhiteSpace(profesional))
            Profesional = profesional.Trim();
        Touch();
    }

    public void UpdateCommercialFlags(
        bool? presupuestoEnviado = null,
        bool? autorizacionArt = null,
        bool? autofisica = null,
        decimal? valorPactado = null)
    {
        if (presupuestoEnviado.HasValue) PresupuestoEnviado = presupuestoEnviado.Value;
        if (autorizacionArt.HasValue) AutorizacionArt = autorizacionArt.Value;
        if (autofisica.HasValue) Autofisica = autofisica.Value;
        if (valorPactado.HasValue) ValorPactado = valorPactado;
        Touch();
    }

    public void UpdateNotes(string? notas)
    {
        Notas = notas;
        Touch();
    }

    private bool CanGoToVerde()
    {
        var hasAuthOrValue = AutorizacionArt || ValorPactado.HasValue;
        var hasBillingReady = Autofisica || ValorPactado.HasValue;
        return hasAuthOrValue && hasBillingReady;
    }
}
