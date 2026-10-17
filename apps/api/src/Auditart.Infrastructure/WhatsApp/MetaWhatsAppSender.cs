using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Auditart.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auditart.Infrastructure.WhatsApp;

public sealed class MetaWhatsAppSender : IWhatsAppSender
{
    private readonly WhatsAppOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MetaWhatsAppSender> _logger;

    public MetaWhatsAppSender(
        IOptions<WhatsAppOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<MetaWhatsAppSender> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.PhoneNumberId)
        && !string.IsNullOrWhiteSpace(_options.AccessToken);

    public async Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneE164,
        string templateName,
        string languageCode,
        IReadOnlyList<string> bodyParameters,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
            return new WhatsAppSendResult(false, null, "WhatsApp no está habilitado o faltan credenciales.");

        var url =
            $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";

        var parameters = bodyParameters
            .Select(p => new Dictionary<string, string>
            {
                ["type"] = "text",
                ["text"] = string.IsNullOrWhiteSpace(p) ? "-" : p
            })
            .ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            to = phoneE164,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = parameters.Length == 0
                    ? Array.Empty<object>()
                    : new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters
                        }
                    }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(MetaWhatsAppSender));
            using var res = await client.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("WhatsApp API error {Status}: {Body}", (int)res.StatusCode, body);
                return new WhatsAppSendResult(false, null, Truncate(body, 500));
            }

            using var doc = JsonDocument.Parse(body);
            string? messageId = null;
            if (doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var idEl))
            {
                messageId = idEl.GetString();
            }

            return new WhatsAppSendResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo enviando WhatsApp a {Phone}", phoneE164);
            return new WhatsAppSendResult(false, null, ex.Message);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
