namespace Auditart.Application.Informes;

public enum InformeTipo
{
    Telemedicina,
    Terreno,
    Consultorio
}

public sealed record InformeSelectOptionDto(string Value, string Label);

public sealed record InformeFieldDto(
    string Key,
    string Label,
    string Kind,
    string? LogicalKey,
    int Order,
    string Value,
    IReadOnlyList<InformeSelectOptionDto>? Options = null);

public sealed record InformePrefillDto(
    InformeTipo Tipo,
    string Title,
    string FileName,
    string SuggestedTipo,
    bool TieneFirmaPrestador,
    string? PrestadorNombre,
    string? FirmaWarning,
    IReadOnlyList<InformeFieldDto> Fields);

public sealed record InformeGenerateRequest(Dictionary<string, string>? Values);

public sealed record InformeAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);
