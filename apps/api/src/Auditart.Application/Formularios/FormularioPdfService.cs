using System.Globalization;
using System.Text.Json;
using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.Formularios;

public sealed class FormularioPdfService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IAcroFormFiller _filler;
    private readonly ILogger<FormularioPdfService> _logger;

    public FormularioPdfService(
        IAppDbContext db,
        IObjectStorage storage,
        IAcroFormFiller filler,
        ILogger<FormularioPdfService> logger)
    {
        _db = db;
        _storage = storage;
        _filler = filler;
        _logger = logger;
    }

    public static bool TryParseTipo(string tipo, out FormularioTipo parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(tipo)) return false;
        return Enum.TryParse(tipo.Trim(), ignoreCase: true, out parsed);
    }

    public async Task<FormularioPrefillDto> GetPrefillAsync(Guid serviceId, FormularioTipo tipo, CancellationToken ct)
    {
        var service = await LoadServiceAsync(serviceId, ct);
        var meta = LoadMeta(tipo);
        var logical = BuildLogicalValues(service);
        var fields = meta.Fields
            .OrderBy(f => f.Order)
            .Select(f =>
            {
                var value = string.Empty;
                if (!string.IsNullOrWhiteSpace(f.LogicalKey)
                    && logical.TryGetValue(f.LogicalKey, out var lv)
                    && !string.IsNullOrWhiteSpace(lv))
                {
                    value = lv;
                }
                return new FormularioFieldDto(
                    f.PdfField,
                    f.Label,
                    f.Kind,
                    f.LogicalKey,
                    f.Page,
                    f.Order,
                    value);
            })
            .ToList();

        return new FormularioPrefillDto(tipo, meta.Title, FileNameFor(tipo, service), fields);
    }

    public async Task<FormularioAttachmentDto> GenerateAsync(
        Guid serviceId,
        FormularioTipo tipo,
        Dictionary<string, string>? values,
        CancellationToken ct)
    {
        var service = await LoadServiceAsync(serviceId, ct);
        var meta = LoadMeta(tipo);
        var logical = BuildLogicalValues(service);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in meta.Fields)
        {
            if (values is not null && values.TryGetValue(field.PdfField, out var fromUi))
            {
                merged[field.PdfField] = fromUi ?? string.Empty;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(field.LogicalKey)
                && logical.TryGetValue(field.LogicalKey, out var lv)
                && !string.IsNullOrWhiteSpace(lv))
            {
                merged[field.PdfField] = lv;
            }
        }

        var templatePath = ResolveTemplatePdfPath(tipo);
        var bytes = _filler.Fill(templatePath, merged);
        var fileName = FileNameFor(tipo, service);

        await using var stream = new MemoryStream(bytes);
        var key = await _storage.UploadAsync(
            stream,
            fileName,
            "application/pdf",
            $"services/{serviceId}",
            ct);

        var entity = ServiceAttachment.Create(
            fileName,
            "application/pdf",
            bytes.LongLength,
            key,
            s3Bucket: null,
            auditServiceId: serviceId);

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Formulario {Tipo} generado para servicio {ServiceId} → adjunto {AttachmentId}",
            tipo, serviceId, entity.Id);

        return new FormularioAttachmentDto(
            entity.Id,
            entity.FileName,
            entity.ContentType,
            entity.SizeBytes,
            entity.CreatedAtUtc);
    }

    private async Task<AuditService> LoadServiceAsync(Guid serviceId, CancellationToken ct)
    {
        var service = await _db.AuditServices
            .Include(s => s.PacienteEntity)
            .Include(s => s.Prestador)
            .FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Prestación no encontrada.");
        return service;
    }

    private static Dictionary<string, string> BuildLogicalValues(AuditService service)
    {
        var p = service.PacienteEntity;
        var argentina = CultureInfo.GetCultureInfo("es-AR");
        string Fmt(DateTime? utc) =>
            utc.HasValue ? utc.Value.ToLocalTime().ToString("dd/MM/yyyy", argentina) : string.Empty;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pacienteNombre"] = p?.Nombre ?? service.Paciente ?? string.Empty,
            ["dni"] = p?.Dni ?? service.Dni ?? string.Empty,
            ["telefono"] = p?.Telefono ?? service.TelefonoPaciente ?? string.Empty,
            ["email"] = p?.Email ?? service.EmailPaciente ?? string.Empty,
            ["domicilio"] = p?.Domicilio ?? string.Empty,
            ["art"] = p?.Art ?? service.Art ?? string.Empty,
            ["numeroSiniestro"] = p?.NumeroSiniestro ?? service.NumeroSiniestro ?? string.Empty,
            ["especialidad"] = service.Especialidad ?? string.Empty,
            ["profesional"] = service.Prestador?.Nombre ?? service.Profesional ?? string.Empty,
            ["fechaIngreso"] = Fmt(service.FechaIngresoUtc),
            ["fechaTurno"] = Fmt(service.FechaTurnoUtc),
        };
    }

    private static string FileNameFor(FormularioTipo tipo, AuditService service)
    {
        var safe = (service.Paciente ?? "paciente")
            .Replace(' ', '_')
            .Replace('/', '-');
        if (safe.Length > 40) safe = safe[..40];
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        return tipo switch
        {
            FormularioTipo.Pmi => $"PMI-ingreso-{service.Numero}-{safe}-{stamp}.pdf",
            FormularioTipo.Pme => $"PME-alta-{service.Numero}-{safe}-{stamp}.pdf",
            FormularioTipo.Pmr => $"PMR-reingreso-{service.Numero}-{safe}-{stamp}.pdf",
            _ => $"formulario-{service.Numero}-{stamp}.pdf"
        };
    }

    private TemplateMeta LoadMeta(FormularioTipo tipo)
    {
        var path = ResolveTemplateMetaPath(tipo);
        if (!File.Exists(path))
            throw new InvalidOperationException($"No se encontró el mapa de campos: {path}");
        var json = File.ReadAllText(path);
        var meta = JsonSerializer.Deserialize<TemplateMeta>(json, JsonOpts)
            ?? throw new InvalidOperationException("Mapa de campos inválido.");
        if (meta.Fields is null || meta.Fields.Count == 0)
            throw new InvalidOperationException("El mapa de campos está vacío.");
        return meta;
    }

    private static string ResolveTemplatesRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "templates", "formularios"),
            Path.Combine(Directory.GetCurrentDirectory(), "templates", "formularios"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "formularios")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "templates", "formularios")),
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }

        throw new InvalidOperationException(
            "No se encontró la carpeta templates/formularios. Verificá el deploy de plantillas PDF.");
    }

    private static string ResolveTemplatePdfPath(FormularioTipo tipo)
    {
        var root = ResolveTemplatesRoot();
        var file = tipo switch
        {
            FormularioTipo.Pmi => "PMI-ingreso-2026.pdf",
            FormularioTipo.Pme => "PME-alta-2026.pdf",
            FormularioTipo.Pmr => "PMR-reingreso-2026.pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        var path = Path.Combine(root, file);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Plantilla PDF no encontrada: {path}");
        return path;
    }

    private static string ResolveTemplateMetaPath(FormularioTipo tipo)
    {
        var root = ResolveTemplatesRoot();
        var file = tipo switch
        {
            FormularioTipo.Pmi => "PMI-ingreso-2026.fields.json",
            FormularioTipo.Pme => "PME-alta-2026.fields.json",
            FormularioTipo.Pmr => "PMR-reingreso-2026.fields.json",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        return Path.Combine(root, file);
    }

    private sealed class TemplateMeta
    {
        public string Template { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<TemplateField> Fields { get; set; } = [];
    }

    private sealed class TemplateField
    {
        public string PdfField { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Kind { get; set; } = "text";
        public string? LogicalKey { get; set; }
        public int Page { get; set; }
        public int Order { get; set; }
    }
}
