using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.Triage;

internal sealed class DenunciaGeminiExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public DenunciaGeminiExtractor(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public bool IsEnabled =>
        string.Equals(_config["Gemini:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]);

    public async Task<DenunciaParsedDto?> ExtractAsync(
        string text,
        IReadOnlyDictionary<string, string> formFields,
        CancellationToken ct)
    {
        if (!IsEnabled) return null;

        var model = _config["Gemini:Model"] ?? "gemini-2.5-flash";
        var apiKey = _config["Gemini:ApiKey"]!;
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var fieldsBlock = formFields.Count == 0
            ? "(sin campos AcroForm)"
            : string.Join('\n', formFields.Select(kv => $"{kv.Key}: {kv.Value}"));

        var clipped = text.Length > 8000 ? text[..8000] : text;
        var prompt =
            """
            Extraé datos de una denuncia de accidente laboral argentina (puede ser de cualquier ART).
            Devolvé SOLO un JSON con estas claves (string o null), sin markdown:
            {"nombreTrabajador":null,"dniCuil":null,"empleador":null,"numeroSiniestro":null,"fechaAccidente":null,"descripcionCap":null,"telefonoPaciente":null,"emailPaciente":null}
            Reglas:
            - nombreTrabajador: apellido y nombre del trabajador, no del empleador ni del prestador.
            - dniCuil: CUIL (20/23/24/27) o DNI del trabajador, no el CUIT de la empresa (30/33).
            - empleador: razón social de la empresa.
            - numeroSiniestro: número de siniestro/denuncia, no el contrato.
            - fechaAccidente: fecha del hecho, no fecha de nacimiento ni de carga.
            - telefonoPaciente: celular/teléfono del trabajador, no el 0800 ni el de la ART.
            - emailPaciente: mail del trabajador, no el de la ART.
            - descripcionCap: relato de cómo ocurrió.

            Campos de formulario:

            """
            + fieldsBlock
            + "\n\nTexto:\n"
            + clipped;

        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 512,
                responseMimeType = "application/json"
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var client = _httpClientFactory.CreateClient(nameof(DenunciaPdfParser));
            using var res = await client.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini denuncia {Status}: {Body}", (int)res.StatusCode, Truncate(body, 240));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var raw = ReadCandidateText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return ParseJsonDto(raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini no pudo completar el parseo de denuncia.");
            return null;
        }
    }

    private static string? ReadCandidateText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        var cand = candidates[0];
        if (!cand.TryGetProperty("content", out var content)) return null;
        if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            return null;
        return parts[0].TryGetProperty("text", out var t) ? t.GetString() : null;
    }

    private static DenunciaParsedDto? ParseJsonDto(string raw)
    {
        var json = raw.Trim();
        var fence = json.IndexOf('{');
        var last = json.LastIndexOf('}');
        if (fence < 0 || last <= fence) return null;
        json = json[fence..(last + 1)];

        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        return new DenunciaParsedDto(
            Read(r, "nombreTrabajador"),
            Read(r, "dniCuil"),
            Read(r, "empleador"),
            Read(r, "numeroSiniestro"),
            Read(r, "fechaAccidente"),
            Read(r, "descripcionCap"),
            Read(r, "telefonoPaciente"),
            Read(r, "emailPaciente"));
    }

    private static string? Read(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var v = el.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(v) || v is "null" ? null : v;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
