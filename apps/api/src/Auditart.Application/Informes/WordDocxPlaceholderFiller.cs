using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Auditart.Application.Informes;

/// <summary>Rellena plantillas .docx con tokens {{CLAVE}} e inserta imagen de firma en {{FIRMA}}.</summary>
public static class WordDocxPlaceholderFiller
{
    private static readonly Regex TokenRx = new(@"\{\{([A-Z0-9_]+)\}\}", RegexOptions.Compiled);

    public static byte[] Fill(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        byte[]? firmaBytes,
        string? firmaContentType)
    {
        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(templatePath))
            fs.CopyTo(ms);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var main = doc.MainDocumentPart
                ?? throw new InvalidOperationException("Documento Word inválido.");
            ReplaceTextTokens(main.Document.Body!, values);

            if (firmaBytes is { Length: > 0 })
                ReplaceFirmaTokenWithImage(main, firmaBytes, firmaContentType);
            else
                ReplacePlainToken(main.Document.Body!, "FIRMA", string.Empty);

            main.Document.Save();
        }

        return ms.ToArray();
    }

    private static void ReplaceTextTokens(OpenXmlElement root, IReadOnlyDictionary<string, string> values)
    {
        foreach (var text in root.Descendants<Text>().ToList())
        {
            var raw = text.Text;
            if (string.IsNullOrEmpty(raw) || !raw.Contains("{{", StringComparison.Ordinal))
                continue;

            var run = text.Parent as Run;
            if (run is null) continue;

            var matches = TokenRx.Matches(raw);
            if (matches.Count == 0) continue;

            // Solo reemplazos de texto (FIRMA se maneja aparte).
            var sb = new StringBuilder();
            var last = 0;
            var hasBreaks = false;
            foreach (Match m in matches)
            {
                var key = m.Groups[1].Value;
                if (key == "FIRMA") continue;
                sb.Append(raw, last, m.Index - last);
                values.TryGetValue(key, out var val);
                val ??= string.Empty;
                if (val.Contains('\n') || val.Contains('\r'))
                    hasBreaks = true;
                sb.Append(val.Replace("\r\n", "\n").Replace('\r', '\n'));
                last = m.Index + m.Length;
            }

            sb.Append(raw, last, raw.Length - last);
            // Si quedó {{FIRMA}}, no lo tocamos aquí.
            var next = sb.ToString();
            if (!hasBreaks || next.Contains("{{FIRMA}}", StringComparison.Ordinal))
            {
                text.Text = next;
                text.Space = SpaceProcessingModeValues.Preserve;
                continue;
            }

            // Texto multilínea → runs con saltos de línea.
            var paragraph = run.Ancestors<Paragraph>().FirstOrDefault();
            if (paragraph is null)
            {
                text.Text = next.Replace('\n', ' ');
                continue;
            }

            var lines = next.Split('\n');
            text.Text = lines[0];
            text.Space = SpaceProcessingModeValues.Preserve;
            OpenXmlElement insertAfter = run;
            for (var i = 1; i < lines.Length; i++)
            {
                var brRun = (Run)run.CloneNode(true);
                brRun.RemoveAllChildren<Text>();
                brRun.RemoveAllChildren<Break>();
                brRun.AppendChild(new Break());
                brRun.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
                insertAfter.InsertAfterSelf(brRun);
                insertAfter = brRun;
            }
        }
    }

    private static void ReplacePlainToken(OpenXmlElement root, string key, string value)
    {
        var token = "{{" + key + "}}";
        foreach (var text in root.Descendants<Text>().ToList())
        {
            if (text.Text.Contains(token, StringComparison.Ordinal))
            {
                text.Text = text.Text.Replace(token, value, StringComparison.Ordinal);
                text.Space = SpaceProcessingModeValues.Preserve;
            }
        }
    }

    private static void ReplaceFirmaTokenWithImage(
        MainDocumentPart main,
        byte[] firmaBytes,
        string? contentType)
    {
        var body = main.Document.Body!;
        Text? target = null;
        foreach (var text in body.Descendants<Text>())
        {
            if (text.Text.Contains("{{FIRMA}}", StringComparison.Ordinal))
            {
                target = text;
                break;
            }
        }

        if (target is null) return;

        var run = target.Parent as Run
            ?? throw new InvalidOperationException("No se encontró el run de FIRMA.");

        target.Text = target.Text.Replace("{{FIRMA}}", string.Empty, StringComparison.Ordinal);

        var imagePartType = contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ImagePartType.Jpeg,
            "image/gif" => ImagePartType.Gif,
            _ => ImagePartType.Png
        };

        var imagePart = main.AddImagePart(imagePartType);
        using (var stream = new MemoryStream(firmaBytes))
            imagePart.FeedData(stream);

        var relId = main.GetIdOfPart(imagePart);
        // ~5.5 cm ancho × ~2.2 cm alto (EMU: 914400 por pulgada)
        const long widthEmu = 2000000L;
        const long heightEmu = 800000L;
        var drawing = CreateImageDrawing(relId, widthEmu, heightEmu);
        run.AppendChild(drawing);
    }

    private static Drawing CreateImageDrawing(string relationshipId, long cx, long cy)
    {
        var element =
            new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = 1U, Name = "Firma" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "firma.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = cx, Cy = cy }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                    {
                                        Preset = A.ShapeTypeValues.Rectangle
                                    }))
                        )
                        {
                            Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                        })
                )
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

        return element;
    }
}
