namespace Auditart.Infrastructure.Gemini;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
}
