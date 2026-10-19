using System.Globalization;
using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.Informes;

public sealed class InformeWordService
{
    private readonly IAppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly ILogger<InformeWordService> _logger;

    public InformeWordService(
        IAppDbContext db,
        IObjectStorage storage,
        ILogger<InformeWordService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public static bool TryParseTipo(string tipo, out InformeTipo parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(tipo)) return false;
        return Enum.TryParse(tipo.Trim(), ignoreCase: true, out parsed);
    }

    public static InformeTipo SuggestTipo(ServiceType serviceType) => serviceType switch
    {
        ServiceType.Telemedicina => InformeTipo.Telemedicina,
        ServiceType.Terreno => InformeTipo.Terreno,
        _ => InformeTipo.Consultorio
    };

    public async Task<InformePrefillDto> GetPrefillAsync(
        Guid serviceId,
        InformeTipo? tipo,
        CancellationToken ct)
    {
        var service = await LoadServiceAsync(serviceId, ct);
        var resolved = tipo ?? SuggestTipo(service.TipoServicio);
        var logical = BuildLogicalValues(service);
        var fields = FieldDefs(resolved)
            .Select(f =>
            {
                var value = string.Empty;
                if (!string.IsNullOrWhiteSpace(f.LogicalKey)
                    && logical.TryGetValue(f.LogicalKey, out var lv)
                    && !string.IsNullOrWhiteSpace(lv))
                {
                    value = lv;
                }

                return new InformeFieldDto(
                    f.Key,
                    f.Label,
                    f.Kind,
                    f.LogicalKey,
                    f.Order,
                    value,
                    f.Options);
            })
            .ToList();

        var tieneFirma = !string.IsNullOrWhiteSpace(service.Prestador?.FirmaS3Key);
        string? warning = null;
        if (service.PrestadorId is null)
            warning = "Este caso no tiene prestador asignado. La firma del médico no se podrá insertar.";
        else if (!tieneFirma)
            warning = "El prestador no tiene firma cargada. Generá el informe igual o cargá la firma en Prestadores.";

        return new InformePrefillDto(
            resolved,
            TitleFor(resolved),
            FileNameFor(resolved, service),
            SuggestTipo(service.TipoServicio).ToString(),
            tieneFirma,
            service.Prestador?.Nombre,
            warning,
            fields);
    }

    public async Task<InformeAttachmentDto> GenerateAsync(
        Guid serviceId,
        InformeTipo tipo,
        Dictionary<string, string>? values,
        CancellationToken ct)
    {
        var service = await LoadServiceAsync(serviceId, ct);
        if (service.Status != AuditStatus.Amarillo)
            throw new InvalidOperationException(
                "El informe de auditoría solo se puede generar cuando el caso está en Amarillo.");

        var logical = BuildLogicalValues(service);
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in FieldDefs(tipo))
        {
            if (values is not null && values.TryGetValue(field.Key, out var fromUi)
                && !string.IsNullOrWhiteSpace(fromUi))
            {
                merged[field.Key] = fromUi.Trim();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(field.LogicalKey)
                && logical.TryGetValue(field.LogicalKey, out var lv)
                && !string.IsNullOrWhiteSpace(lv))
            {
                merged[field.Key] = lv;
            }
            else
            {
                merged[field.Key] = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(merged.GetValueOrDefault("antecedentes")))
            throw new InvalidOperationException(
                "Completá el resumen (antecedentes / lo que indicó el médico).");

        var tokens = ToTokens(tipo, merged);

        byte[]? firmaBytes = null;
        string? firmaCt = null;
        if (service.Prestador is { FirmaS3Key: not null } prestador)
        {
            var download = await _storage.DownloadAsync(prestador.FirmaS3Key, ct);
            await using (download.Content)
            {
                using var ms = new MemoryStream();
                await download.Content.CopyToAsync(ms, ct);
                firmaBytes = ms.ToArray();
            }

            firmaCt = prestador.FirmaContentType ?? download.ContentType;
        }

        var templatePath = ResolveTemplatePath(tipo);
        var bytes = WordDocxPlaceholderFiller.Fill(templatePath, tokens, firmaBytes, firmaCt);
        var fileName = FileNameFor(tipo, service);

        await using var stream = new MemoryStream(bytes);
        var key = await _storage.UploadAsync(
            stream,
            fileName,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"services/{serviceId}",
            ct);

        var entity = ServiceAttachment.Create(
            fileName,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            bytes.LongLength,
            key,
            s3Bucket: null,
            auditServiceId: serviceId);

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Informe Word {Tipo} generado para servicio {ServiceId} → adjunto {AttachmentId}",
            tipo, serviceId, entity.Id);

        return new InformeAttachmentDto(
            entity.Id,
            entity.FileName,
            entity.ContentType,
            entity.SizeBytes,
            entity.CreatedAtUtc);
    }

    private async Task<AuditService> LoadServiceAsync(Guid serviceId, CancellationToken ct)
    {
        return await _db.AuditServices
            .Include(s => s.PacienteEntity)
            .Include(s => s.Prestador)
            .FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Prestación no encontrada.");
    }

    private static Dictionary<string, string> BuildLogicalValues(AuditService service)
    {
        var p = service.PacienteEntity;
        var argentina = CultureInfo.GetCultureInfo("es-AR");
        string Fmt(DateTime? utc) =>
            utc.HasValue ? utc.Value.ToLocalTime().ToString("dd/MM/yyyy", argentina) : string.Empty;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fecha"] = Fmt(service.FechaTurnoUtc) is { Length: > 0 } f ? f : Fmt(service.FechaIngresoUtc),
            ["paciente"] = p?.Nombre ?? service.Paciente ?? string.Empty,
            ["dni"] = p?.Dni ?? service.Dni ?? string.Empty,
            ["art"] = p?.Art ?? service.Art ?? string.Empty,
            ["stro"] = p?.NumeroSiniestro ?? service.NumeroSiniestro ?? string.Empty,
            ["diagnostico"] = service.Especialidad ?? string.Empty,
            ["caso"] = service.Numero.ToString(CultureInfo.InvariantCulture),
            ["antecedentes"] = string.Empty,
            ["conclusion"] = string.Empty,
            ["internado"] = "no",
            ["fechaAlta"] = string.Empty,
            ["institucion"] = string.Empty,
            ["direccion"] = service.Prestador?.Domicilio ?? string.Empty,
        };
    }

    private static Dictionary<string, string> ToTokens(InformeTipo tipo, Dictionary<string, string> merged)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FECHA"] = merged.GetValueOrDefault("fecha") ?? string.Empty,
            ["PACIENTE"] = merged.GetValueOrDefault("paciente") ?? string.Empty,
            ["DNI"] = merged.GetValueOrDefault("dni") ?? string.Empty,
            ["ART"] = merged.GetValueOrDefault("art") ?? string.Empty,
            ["STRO"] = merged.GetValueOrDefault("stro") ?? string.Empty,
            ["DIAGNOSTICO"] = merged.GetValueOrDefault("diagnostico") ?? string.Empty,
            ["CASO"] = merged.GetValueOrDefault("caso") ?? string.Empty,
            ["ANTECEDENTES"] = merged.GetValueOrDefault("antecedentes") ?? string.Empty,
            ["CONCLUSION"] = merged.GetValueOrDefault("conclusion") ?? string.Empty,
        };

        if (tipo == InformeTipo.Terreno)
        {
            var internado = (merged.GetValueOrDefault("internado") ?? "no")
                .Trim()
                .Equals("si", StringComparison.OrdinalIgnoreCase);
            tokens["INTERNADO_SI"] = internado ? "X" : string.Empty;
            tokens["INTERNADO_NO"] = internado ? string.Empty : "X";
            tokens["FECHA_ALTA"] = merged.GetValueOrDefault("fechaAlta") ?? string.Empty;
            tokens["INSTITUCION"] = merged.GetValueOrDefault("institucion") ?? string.Empty;
            tokens["DIRECCION"] = merged.GetValueOrDefault("direccion") ?? string.Empty;
        }

        return tokens;
    }

    private static IReadOnlyList<FieldDef> FieldDefs(InformeTipo tipo)
    {
        var common = new List<FieldDef>
        {
            new("fecha", "Fecha", "text", "fecha", 10, null),
            new("paciente", "Paciente", "text", "paciente", 20, null),
            new("dni", "DNI / CUIL", "text", "dni", 30, null),
            new("art", "ART", "text", "art", 40, null),
            new("stro", "STRO / N° siniestro", "text", "stro", 50, null),
        };

        if (tipo == InformeTipo.Terreno)
        {
            common.Add(new(
                "internado",
                "¿Se encuentra internado actualmente?",
                "select",
                "internado",
                55,
                [
                    new InformeSelectOptionDto("si", "Sí"),
                    new InformeSelectOptionDto("no", "No"),
                ]));
            common.Add(new("fechaAlta", "Fecha de alta sanatorial", "text", "fechaAlta", 56, null));
            common.Add(new("institucion", "Institución donde se encuentra internado", "text", "institucion", 57, null));
            common.Add(new("direccion", "Dirección", "text", "direccion", 58, null));
        }

        common.Add(new("diagnostico", "Diagnóstico", "text", "diagnostico", 60, null));
        common.Add(new("caso", "Caso", "text", "caso", 70, null));
        common.Add(new(
            "antecedentes",
            "Resumen / Antecedentes y examen físico (lo indicado por el médico)",
            "textarea",
            "antecedentes",
            80,
            null));
        common.Add(new("conclusion", "Conclusión", "textarea", "conclusion", 90, null));
        return common;
    }

    private static string TitleFor(InformeTipo tipo) => tipo switch
    {
        InformeTipo.Telemedicina => "Informe — Auditoría de telemedicina",
        InformeTipo.Terreno => "Informe — Auditoría de terreno",
        InformeTipo.Consultorio => "Informe — Auditoría de consultorio",
        _ => "Informe de auditoría"
    };

    private static string FileNameFor(InformeTipo tipo, AuditService service)
    {
        var safe = (service.Paciente ?? "paciente")
            .Replace(' ', '_')
            .Replace('/', '-');
        if (safe.Length > 40) safe = safe[..40];
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        var prefix = tipo switch
        {
            InformeTipo.Telemedicina => "Informe-Telemedicina",
            InformeTipo.Terreno => "Informe-Terreno",
            InformeTipo.Consultorio => "Informe-Consultorio",
            _ => "Informe"
        };
        return $"{prefix}-{service.Numero}-{safe}-{stamp}.docx";
    }

    private static string ResolveTemplatePath(InformeTipo tipo)
    {
        var root = ResolveTemplatesRoot();
        var file = tipo switch
        {
            InformeTipo.Telemedicina => "auditoria-telemedicina.docx",
            InformeTipo.Terreno => "auditoria-terreno.docx",
            InformeTipo.Consultorio => "auditoria-consultorio.docx",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        var path = Path.Combine(root, file);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Plantilla Word no encontrada: {path}");
        return path;
    }

    private static string ResolveTemplatesRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "templates", "informes"),
            Path.Combine(Directory.GetCurrentDirectory(), "templates", "informes"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "informes")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "templates", "informes")),
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }

        throw new InvalidOperationException(
            "No se encontró la carpeta templates/informes. Verificá el deploy de plantillas Word.");
    }

    private sealed record FieldDef(
        string Key,
        string Label,
        string Kind,
        string? LogicalKey,
        int Order,
        IReadOnlyList<InformeSelectOptionDto>? Options);
}
