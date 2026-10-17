namespace Auditart.Application.Abstractions;

public interface IWhatsAppSender
{
    bool IsEnabled { get; }

    Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneE164,
        string templateName,
        string languageCode,
        IReadOnlyList<string> bodyParameters,
        CancellationToken ct = default);
}

public sealed record WhatsAppSendResult(bool Ok, string? ProviderMessageId, string? Error);
