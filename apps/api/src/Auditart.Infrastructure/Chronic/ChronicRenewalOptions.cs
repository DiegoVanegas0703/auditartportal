namespace Auditart.Infrastructure.Chronic;

public sealed class ChronicRenewalOptions
{
    public const string SectionName = "ChronicRenewal";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
}
