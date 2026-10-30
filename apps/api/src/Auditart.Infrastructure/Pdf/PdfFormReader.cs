using Auditart.Application.Abstractions;
using iText.Forms;
using iText.Kernel.Pdf;

namespace Auditart.Infrastructure.Pdf;

public sealed class PdfFormReader : IPdfFormReader
{
    public IReadOnlyDictionary<string, string> ReadFilledFields(Stream pdfStream)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var reader = new PdfReader(pdfStream);
            using var pdf = new PdfDocument(reader);
            var form = PdfAcroForm.GetAcroForm(pdf, false);
            if (form is null) return result;

            foreach (var (name, field) in form.GetAllFormFields())
            {
                if (string.IsNullOrWhiteSpace(name) || field is null) continue;
                string? raw;
                try { raw = field.GetValueAsString(); }
                catch { continue; }

                var value = (raw ?? string.Empty).Trim();
                if (value.Length == 0) continue;
                if (value is "Off" or "/Off" or "Yes" or "/Yes") continue;
                result[name] = value;
            }
        }
        catch
        {
            return result;
        }

        return result;
    }
}
