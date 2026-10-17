namespace Auditart.Application.Formularios;

public enum FormularioTipo
{
    Pmi,
    Pme,
    Pmr
}

public sealed record FormularioFieldDto(
    string PdfField,
    string Label,
    string Kind,
    string? LogicalKey,
    int Page,
    int Order,
    string Value);

public sealed record FormularioPrefillDto(
    FormularioTipo Tipo,
    string Title,
    string FileName,
    IReadOnlyList<FormularioFieldDto> Fields);

public sealed record FormularioGenerateRequest(
    Dictionary<string, string>? Values);

public sealed record FormularioAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);
