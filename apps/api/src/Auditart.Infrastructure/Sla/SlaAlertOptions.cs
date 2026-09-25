namespace Auditart.Infrastructure.Sla;

public sealed class SlaAlertOptions
{
    public const string SectionName = "SlaAlerts";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
}
