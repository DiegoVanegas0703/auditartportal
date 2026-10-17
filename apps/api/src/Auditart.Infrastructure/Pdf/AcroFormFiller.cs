using Auditart.Application.Abstractions;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;

namespace Auditart.Infrastructure.Pdf;

public sealed class AcroFormFiller : IAcroFormFiller
{
    public byte[] Fill(string templatePdfPath, IReadOnlyDictionary<string, string> values)
    {
        using var input = new FileStream(templatePdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new MemoryStream();
        using (var reader = new PdfReader(input))
        using (var writer = new PdfWriter(output))
        using (var pdf = new PdfDocument(reader, writer))
        {
            var form = PdfAcroForm.GetAcroForm(pdf, true);
            form.SetGenerateAppearance(true);

            foreach (var (name, raw) in values)
            {
                var field = form.GetField(name);
                if (field is null) continue;

                var value = raw ?? string.Empty;
                if (field is PdfButtonFormField button)
                {
                    var on = IsChecked(value);
                    ApplyCheckbox(button, on);
                }
                else
                {
                    field.SetValue(value);
                }
            }

            form.FlattenFields();
        }

        return output.ToArray();
    }

    private static bool IsChecked(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return v is "1" or "true" or "True" or "yes" or "Yes" or "YES" or "on" or "On" or "Sí" or "Si" or "SI";
    }

    private static void ApplyCheckbox(PdfButtonFormField button, bool on)
    {
        if (!on)
        {
            button.SetValue("Off");
            return;
        }

        var states = button.GetAppearanceStates();
        var onState = states?
            .FirstOrDefault(s => !string.Equals(s, "Off", StringComparison.OrdinalIgnoreCase))
            ?? "Yes";
        try
        {
            button.SetValue(onState);
        }
        catch
        {
            button.SetValue("Yes");
        }
    }
}
