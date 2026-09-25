using System.Text;

using System.Text.RegularExpressions;

using Auditart.Application.Abstractions;

using Microsoft.EntityFrameworkCore;

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

    private static readonly Regex TrabajadorBlockRegex = new(

        @"([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑ\s\.]{2,}?)\s+((?:20|23|24|27|30|33)\d{9})\s+DATOS\s+DEL\s+TRABAJADOR",

        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);



    private static readonly Regex DocumentoTrabajadorRegex = new(

        @"DATOS\s+DEL\s+TRABAJADOR.*?Tipo\s+y\s+N[°º]\s+de\s+Doc\.\s*((?:20|23|24|27|30|33)\d{9})",

        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);



    private static readonly Regex SiniestroRegex = new(

        @"Siniestro:\s*(\d+)",

        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);



    private static readonly Regex EmpleadorRegex = new(

        @"ENFERMEDAD\s+PROFESIONAL\s+(.+?)\s+(\d{11})\s+(\d+)",

        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);



    private static readonly Regex FechaSiniestroRegex = new(

        @"INFORMACION\s+SOBRE\s+EL\s+SINIESTRO\s+(\d{2}-\d{2}-\d{4})",

        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);



    private static readonly Regex TelefonoTrabajadorRegex = new(

        @"DATOS\s+DEL\s+TRABAJADOR.*?(?:20|23|24|27|30|33)\d{9}\s+(\d{10,15})",

        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);



    private static readonly Regex EmailRegex = new(

        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",

        RegexOptions.CultureInvariant);



    private readonly IAppDbContext _db;

    private readonly IObjectStorage _storage;



    public DenunciaPdfParser(IAppDbContext db, IObjectStorage storage)

    {

        _db = db;

        _storage = storage;

    }



    public async Task<DenunciaParsedDto> ParseFromRequestAsync(

        Guid requestId,

        Guid attachmentId,

        CancellationToken ct = default)

    {

        var pdfAttachment = await LoadSelectedPdfAsync(requestId, attachmentId, ct);



        var download = await _storage.DownloadAsync(pdfAttachment.S3Key, ct);

        using var memory = new MemoryStream();

        await download.Content.CopyToAsync(memory, ct);

        memory.Position = 0;



        var text = ExtractText(memory);

        return ParseText(text);

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

        attachment.ContentType.ToLower().Contains("pdf") ||

        attachment.FileName.ToLower().EndsWith(".pdf");



    internal static string ExtractText(Stream pdfStream)

    {

        using var document = PdfDocument.Open(pdfStream);

        var builder = new StringBuilder();



        foreach (var page in document.GetPages())

        {

            var words = page.GetWords().ToList();

            if (words.Count == 0) continue;



            string? currentLine = null;

            double? currentBaseline = null;

            const double lineTolerance = 4d;



            foreach (var word in words)

            {

                var baseline = word.BoundingBox.Bottom;

                if (currentLine is null ||

                    currentBaseline is null ||

                    Math.Abs(baseline - currentBaseline.Value) > lineTolerance)

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



        return builder.ToString();

    }



    internal static DenunciaParsedDto ParseText(string text)

    {

        if (string.IsNullOrWhiteSpace(text))

            return new DenunciaParsedDto(null, null, null, null, null, null, null, null);



        var normalized = Normalize(text);

        var trabajador = MatchTrabajador(normalized);



        return new DenunciaParsedDto(

            trabajador.Nombre,

            trabajador.Documento ?? MatchDocumentoFallback(normalized),

            MatchEmpleador(normalized),

            MatchSiniestro(normalized),

            MatchFechaAccidente(normalized),

            MatchCapDescription(normalized),

            MatchTelefono(normalized),

            MatchEmail(normalized));

    }



    private static string Normalize(string text) =>

        Regex.Replace(text, @"[ \t]+", " ").Trim();



    private static (string? Nombre, string? Documento) MatchTrabajador(string text)

    {

        var match = TrabajadorBlockRegex.Match(text);

        if (!match.Success)

            return (null, null);



        return (CleanName(match.Groups[1].Value), match.Groups[2].Value);

    }



    private static string? MatchDocumentoFallback(string text)

    {

        var match = DocumentoTrabajadorRegex.Match(text);

        if (match.Success)

            return match.Groups[1].Value;



        var cuil = Regex.Match(text, @"\b(20|23|24|27|30|33)-?\d{8}-?\d\b");

        if (cuil.Success)

            return Regex.Replace(cuil.Value, @"\D", string.Empty);



        return MatchLabel(text, "cuil", "dni", "cuit");

    }



    private static string? MatchEmpleador(string text)

    {

        var match = EmpleadorRegex.Match(text);

        if (match.Success)

            return CleanName(match.Groups[1].Value);



        return MatchLabel(text, "nombre de la empresa", "empleador", "empresa", "razón social", "razon social");

    }



    private static string? MatchSiniestro(string text)

    {

        var match = SiniestroRegex.Match(text);

        return match.Success ? match.Groups[1].Value : MatchLabel(text, "siniestro", "número de siniestro", "numero de siniestro");

    }



    private static string? MatchTelefono(string text)

    {

        var match = TelefonoTrabajadorRegex.Match(text);

        if (match.Success)

            return match.Groups[1].Value;



        return MatchLabel(text, "telefono", "teléfono", "celular");

    }



    private static string? MatchEmail(string text)

    {

        var match = EmailRegex.Match(text);

        return match.Success ? match.Value : MatchLabel(text, "correo", "email", "e-mail", "mail");

    }



    private static string? MatchFechaAccidente(string text)

    {

        var match = FechaSiniestroRegex.Match(text);

        if (match.Success)

            return match.Groups[1].Value;



        return MatchLabel(text, "fecha del accidente", "fecha accidente", "fecha");

    }



    private static string? MatchLabel(string text, params string[] keywords)

    {

        foreach (var keyword in keywords)

        {

            var pattern = $@"{Regex.Escape(keyword)}\s*[:\-]?\s*(.+?)(?:\r?\n|$)";

            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

            if (match.Success)

                return match.Groups[1].Value.Trim();

        }



        return null;

    }



    private static string? MatchCapDescription(string text)

    {

        var capMatch = Regex.Match(

            text,

            @"\bCAP:\s*(.+?)(?:\r?\nHORARIO LABORAL:|\r?\nRECIBIO ATENCION|\r?\nSiniestro:|\z)",

            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (capMatch.Success)

            return capMatch.Groups[1].Value.Trim();



        capMatch = Regex.Match(

            text,

            @"(?:descripci[oó]n\s*(?:del\s*)?(?:hecho|accidente|cap)?|hecho\s*denunciado)\s*[:\-]?\s*(.+)",

            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (capMatch.Success)

            return capMatch.Groups[1].Value.Trim();



        return MatchLabel(text, "descripcion", "descripción", "hecho");

    }



    private static string CleanName(string value) =>

        Regex.Replace(value.Trim(), @"\s{2,}", " ");

}


