using System.Text;
using Auditart.Application.Abstractions;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Auditart.Application.Triage;

public sealed record DenunciaParsedDto(
    string? NombreTrabajador,
    string? DniCuil,
    string? Empleador,
    string? NumeroSiniestro,
    string? FechaAccidente,
    string? DescripcionCap,
    string? TelefonoPaciente,
    string? EmailPaciente);

public sealed class DenunciaPdfParser
{
    private readonly IAppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IPdfFormReader _formReader;
    private readonly DenunciaGeminiExtractor _gemini;

    public DenunciaPdfParser(
        IAppDbContext db,
        IObjectStorage storage,
        IPdfFormReader formReader,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<DenunciaPdfParser> logger)
    {
        _db = db;
        _storage = storage;
        _formReader = formReader;
        _gemini = new DenunciaGeminiExtractor(httpClientFactory, config, logger);
    }

    public async Task<DenunciaParsedDto> ParseFromRequestAsync(
        Guid requestId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var pdfAttachment = await LoadSelectedPdfAsync(requestId, attachmentId, ct);
        var download = await _storage.DownloadAsync(pdfAttachment.S3Key, ct);
        await using (download.Content)
        {
            using var memory = new MemoryStream();
            await download.Content.CopyToAsync(memory, ct);
            return await ParseBytesAsync(memory.ToArray(), ct);
        }
    }

    public async Task<DenunciaParsedDto> ParseBytesAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        IReadOnlyDictionary<string, string> fields;
        using (var formStream = new MemoryStream(pdfBytes, writable: false))
            fields = _formReader.ReadFilledFields(formStream);

        var merged = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in DenunciaSpatialExtractor.Extract(pdfBytes))
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            // AcroForm gana: no pisar valores ya leídos del formulario.
            if (merged.ContainsKey(key) && !string.IsNullOrWhiteSpace(merged[key]))
                continue;
            merged[key] = value;
        }

        string text;
        using (var textStream = new MemoryStream(pdfBytes, writable: false))
            text = ExtractText(textStream);

        var heuristic = DenunciaHeuristicExtractor.Extract(text, merged);
        if (!NeedsAi(heuristic) || !_gemini.IsEnabled)
            return heuristic;

        var ai = await _gemini.ExtractAsync(text, fields, ct);
        return ai is null ? heuristic : Merge(heuristic, ai);
    }

    private async Task<Domain.Entities.EmailAttachment> LoadSelectedPdfAsync(
        Guid requestId,
        Guid attachmentId,
        CancellationToken ct)
    {
        var attachment = await _db.EmailAttachments
            .Where(a =>
                a.Id == attachmentId &&
                a.EmailMessage!.EmailThread!.EmailRequestId == requestId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("El adjunto seleccionado no pertenece al requerimiento.");

        if (!IsPdf(attachment))
            throw new InvalidOperationException("El adjunto seleccionado no es un PDF.");

        return attachment;
    }

    private static bool IsPdf(Domain.Entities.EmailAttachment attachment) =>
        attachment.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || attachment.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public static string ExtractText(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count > 0)
            {
                string? currentLine = null;
                double? currentBaseline = null;
                const double lineTolerance = 4d;

                foreach (var word in words)
                {
                    var baseline = word.BoundingBox.Bottom;
                    if (currentLine is null
                        || currentBaseline is null
                        || Math.Abs(baseline - currentBaseline.Value) > lineTolerance)
                    {
                        if (currentLine is not null)
                            builder.AppendLine(currentLine);
                        currentLine = word.Text;
                        currentBaseline = baseline;
                        continue;
                    }

                    currentLine += " " + word.Text;
                }

                if (currentLine is not null)
                    builder.AppendLine(currentLine);
            }
            else if (!string.IsNullOrWhiteSpace(page.Text))
            {
                builder.AppendLine(page.Text);
            }
        }

        return builder.ToString();
    }

    private static bool NeedsAi(DenunciaParsedDto dto) =>
        string.IsNullOrWhiteSpace(dto.NombreTrabajador)
        || string.IsNullOrWhiteSpace(dto.DniCuil);

    private static DenunciaParsedDto Merge(DenunciaParsedDto a, DenunciaParsedDto b) => new(
        a.NombreTrabajador ?? b.NombreTrabajador,
        a.DniCuil ?? b.DniCuil,
        a.Empleador ?? b.Empleador,
        a.NumeroSiniestro ?? b.NumeroSiniestro,
        a.FechaAccidente ?? b.FechaAccidente,
        a.DescripcionCap ?? b.DescripcionCap,
        a.TelefonoPaciente ?? b.TelefonoPaciente,
        a.EmailPaciente ?? b.EmailPaciente);
}
