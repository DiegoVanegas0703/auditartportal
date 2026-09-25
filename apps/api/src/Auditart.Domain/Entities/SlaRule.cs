using Auditart.Domain.Common;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;

namespace Auditart.Domain.Entities;

public class SlaRule : Entity
{
    public AuditQueue Queue { get; private set; }
    public AuditStatus Status { get; private set; }
    public int DurationValue { get; private set; }
    public SlaDurationUnit DurationUnit { get; private set; }
    public int WarnBeforeHours { get; private set; }
    public bool IsEnabled { get; private set; } = true;

    private SlaRule() { }

    public static SlaRule Create(
        AuditQueue queue,
        AuditStatus status,
        int durationValue,
        SlaDurationUnit durationUnit,
        int warnBeforeHours)
    {
        Validate(durationValue, warnBeforeHours);
        return new SlaRule
        {
            Queue = queue,
            Status = status,
            DurationValue = durationValue,
            DurationUnit = durationUnit,
            WarnBeforeHours = warnBeforeHours,
            IsEnabled = true
        };
    }

    public void Update(int durationValue, SlaDurationUnit durationUnit, int warnBeforeHours, bool isEnabled)
    {
        Validate(durationValue, warnBeforeHours);
        DurationValue = durationValue;
        DurationUnit = durationUnit;
        WarnBeforeHours = warnBeforeHours;
        IsEnabled = isEnabled;
        Touch();
    }

    public DateTime ComputeDeadline(DateTime fromUtc) =>
        DurationUnit switch
        {
            SlaDurationUnit.Hours => fromUtc.AddHours(DurationValue),
            SlaDurationUnit.Days => fromUtc.AddDays(DurationValue),
            _ => fromUtc.AddHours(DurationValue)
        };

    public DateTime ComputeWarnAt(DateTime deadlineUtc) =>
        deadlineUtc.AddHours(-Math.Max(0, WarnBeforeHours));

    private static void Validate(int durationValue, int warnBeforeHours)
    {
        if (durationValue < 1)
            throw new DomainException("La duración del SLA debe ser al menos 1.");
        if (warnBeforeHours < 0)
            throw new DomainException("El aviso previo no puede ser negativo.");
    }
}
