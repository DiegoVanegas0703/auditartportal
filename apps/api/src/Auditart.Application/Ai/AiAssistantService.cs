using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.Ai;

public sealed class AiAssistantService
{
    private readonly IAppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(
        IAppDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AiAssistantService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public bool IsChatEnabled =>
        string.Equals(_config["Gemini:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]);

    public async Task<IReadOnlyList<AiAssistantDto>> ListAsync(CancellationToken ct)
    {
        return await _db.AiAssistants
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .Select(a => new AiAssistantDto(
                a.Id,
                a.Name,
                a.Description,
                a.ExternalGemUrl,
                !string.IsNullOrWhiteSpace(a.SystemPrompt)))
            .ToListAsync(ct);
    }

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        if (!IsChatEnabled)
            throw new InvalidOperationException("Gemini no está habilitado. Configurá Gemini__Enabled y Gemini__ApiKey.");

        var assistant = await _db.AiAssistants
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId && a.IsActive, ct)
            ?? throw new KeyNotFoundException("Asistente no encontrado.");

        if (string.IsNullOrWhiteSpace(assistant.SystemPrompt))
            throw new InvalidOperationException(
                "Este asistente solo tiene link a GEM. Abrí el enlace externo o configurá un system prompt.");

        // gemini-2.0-flash fue discontinuado (404). Preferir 2.5+ o alias latest.
        var model = _config["Gemini:Model"] ?? "gemini-2.5-flash";
        var apiKey = _config["Gemini:ApiKey"]!;
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var contents = new List<object>();
        foreach (var msg in request.Messages.TakeLast(20))
        {
            var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "model"
                : "user";
            contents.Add(new
            {
                role,
                parts = new[] { new { text = msg.Content } }
            });
        }

        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = assistant.SystemPrompt } }
            },
            contents,
            generationConfig = new
            {
                temperature = 0.4,
                maxOutputTokens = 2048
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, url);
        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = _httpClientFactory.CreateClient(nameof(AiAssistantService));
        using var res = await client.SendAsync(httpReq, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini error {Status}: {Body}", (int)res.StatusCode, body);
            var hint = (int)res.StatusCode == 404
                ? $" Modelo '{model}' no disponible (si usás gemini-2.0-flash, cambiá a gemini-2.5-flash)."
                : " Revisá API key / cuota.";
            var detail = body.Length > 220 ? body[..220] + "…" : body;
            throw new InvalidOperationException($"Gemini respondió {(int)res.StatusCode}.{hint} {detail}");
        }

        using var doc = JsonDocument.Parse(body);
        var text = ExtractText(doc.RootElement);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini no devolvió texto.");

        return new AiChatResponse(assistant.Id, assistant.Name, text.Trim());
    }

    private static string? ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        var cand = candidates[0];
        if (!cand.TryGetProperty("content", out var content)) return null;
        if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0) return null;
        return parts[0].TryGetProperty("text", out var t) ? t.GetString() : null;
    }
}

public sealed record AiAssistantDto(
    Guid Id,
    string Name,
    string? Description,
    string? ExternalGemUrl,
    bool SupportsChat);

public sealed record AiChatMessageDto(string Role, string Content);

public sealed record AiChatRequest(Guid AssistantId, IReadOnlyList<AiChatMessageDto> Messages);

public sealed record AiChatResponse(Guid AssistantId, string AssistantName, string Reply);
