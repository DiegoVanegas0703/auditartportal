using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Auditart.Application.Triage;

/// <summary>
/// Lee denuncias como formulario: busca la etiqueta y toma el valor más cercano
/// (derecha, abajo, izquierda o arriba), salteando otras etiquetas.
/// </summary>
public static class DenunciaSpatialExtractor
{
    private static readonly (string Key, string[] Phrases)[] Targets =
    [
        ("Nombre y Apellido", ["nombre y apellido", "apellido y nombre", "apellido, nombre"]),
        ("Apellido", ["apellidos", "apellido"]),
        ("Nombres", ["nombres"]),
        ("CUIL", ["cuil"]),
        ("DNI", ["dni", "nro. documento", "nro documento", "tipo y nro de doc", "tipo y nº de doc", "tipo y n° de doc"]),
        ("Celular", ["celular", "tel. contacto", "tel contacto"]),
        ("Teléfono_2", ["teléfono", "telefono", "tel"]),
        ("Razón Social", ["razón social", "razon social"]),
        ("Siniestro N°", ["nro siniestro", "número de siniestro", "numero de siniestro", "siniestro n°", "siniestro nro"]),
        ("Fecha del siniestro", ["fecha del siniestro", "fecha de ocurrencia o primera manifestación", "fecha de ocurrencia"]),
        ("Email", ["e-mail", "email", "correo"])
    ];

    private static readonly HashSet<string> LabelTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "cuil", "cuit", "dni", "documento", "apellido", "apellidos", "nombre", "nombres",
        "sexo", "fecha", "hora", "siniestro", "domicilio", "localidad", "provincia",
        "teléfono", "telefono", "tel", "celular", "email", "e-mail", "turno", "nacionalidad",
        "datos", "del", "trabajador", "empleador", "razón", "razon", "social", "contrato",
        "nº", "n°", "nro", "ciiu", "cp", "c.p.", "establecimiento", "categoría", "profesión",
        "tipo", "doc", "doc.", "genero", "género", "civil", "edad", "postal", "arg",
        "ingreso", "obra", "soc", "fax", "adm", "médica", "medica"
    };

    public static Dictionary<string, string> Extract(byte[] pdfBytes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var stream = new MemoryStream(pdfBytes, writable: false);
        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords()
                .Select(w => new Box(
                    w.Text.Trim(),
                    w.BoundingBox.Left,
                    w.BoundingBox.Right,
                    w.BoundingBox.Bottom,
                    w.BoundingBox.Top))
                .Where(w => w.Text.Length > 0)
                .OrderByDescending(w => w.MidY)
                .ThenBy(w => w.Left)
                .ToList();

            if (words.Count == 0) continue;

            foreach (var (key, phrases) in Targets)
            {
                if (result.ContainsKey(key)) continue;
                var value = FindValue(words, phrases, key);
                if (!string.IsNullOrWhiteSpace(value))
                    result[key] = value.Trim();
            }
        }

        return result;
    }

    private static string? FindValue(List<Box> words, string[] phrases, string key)
    {
        foreach (var phrase in phrases)
        {
            var span = FindPhrase(words, phrase);
            if (span is null) continue;

            var (start, end) = span.Value;
            var label = Merge(words, start, end);
            var yTol = Math.Max(3, (label.Top - label.Bottom) * 0.6);

            var right = TakeRun(words, w =>
                Math.Abs(w.MidY - label.MidY) <= yTol
                && w.Left >= label.Right - 1
                && !IsLabelToken(w.Text), key);
            if (LooksLike(key, right)) return right;

            var left = TakeRun(words, w =>
                Math.Abs(w.MidY - label.MidY) <= yTol
                && w.Right <= label.Left + 1
                && !IsLabelToken(w.Text), key, rightward: false);
            if (LooksLike(key, left)) return left;

            if (key is "Nombre y Apellido" or "Apellido" or "Nombres")
            {
                var above = TakeRun(words, w =>
                    w.Bottom >= label.Top - 1
                    && w.Bottom - label.Top < 22
                    && w.Left < label.Right + 80
                    && w.Right > label.Left - 20
                    && !IsLabelToken(w.Text), key);
                if (LooksLike(key, above)) return above;
            }
        }

        return null;
    }

    private static (int Start, int End)? FindPhrase(List<Box> words, string phrase)
    {
        var parts = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Count; i++)
        {
            if (!Normalize(words[i].Text).Equals(Normalize(parts[0]), StringComparison.OrdinalIgnoreCase))
                continue;
            var ok = true;
            for (var p = 1; p < parts.Length; p++)
            {
                if (i + p >= words.Count
                    || !Normalize(words[i + p].Text).Equals(Normalize(parts[p]), StringComparison.OrdinalIgnoreCase)
                    || Math.Abs(words[i + p].MidY - words[i].MidY) > 5)
                {
                    ok = false;
                    break;
                }
            }

            if (ok) return (i, i + parts.Length - 1);
        }

        return null;
    }

    private static string? TakeRun(
        List<Box> words,
        Func<Box, bool> predicate,
        string key,
        bool rightward = true)
    {
        var picked = words.Where(predicate).ToList();
        if (picked.Count == 0) return null;
        picked = rightward
            ? picked.OrderBy(w => w.Left).ToList()
            : picked.OrderByDescending(w => w.Left).ToList();

        var parts = new List<string>();
        foreach (var word in picked)
        {
            if (IsLabelToken(word.Text) && parts.Count > 0) break;
            if (IsLabelToken(word.Text)) continue;
            if (!rightward) parts.Insert(0, word.Text);
            else parts.Add(word.Text);
            if (key is "CUIL" or "DNI" or "Celular" or "Teléfono_2" or "Siniestro N°" or "Fecha del siniestro")
            {
                var joined = string.Join(' ', parts);
                if (LooksLike(key, joined)) break;
            }

            if (parts.Count >= 8) break;
        }

        var text = string.Join(' ', parts).Trim();
        return text.Length == 0 ? null : text;
    }

    private static bool LooksLike(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return key switch
        {
            "CUIL" => Regex.IsMatch(v, @"\b(?:20|23|24|27)-?\d{8}-?\d\b"),
            "DNI" => Regex.IsMatch(v, @"\b\d{7,8}\b"),
            "Celular" or "Teléfono_2" => Regex.IsMatch(Regex.Replace(v, @"\D", ""), @"^\d{8,13}$"),
            "Fecha del siniestro" => Regex.IsMatch(v, @"\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}"),
            "Siniestro N°" => Regex.IsMatch(v, @"\d{4,}") && !Regex.IsMatch(v, @"\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}"),
            "Email" => v.Contains('@'),
            "Nombre y Apellido" or "Apellido" or "Nombres" =>
                Regex.IsMatch(v, @"[A-Za-zÁÉÍÓÚÑáéíóúñ]{3,}")
                && !Regex.IsMatch(v, @"\d")
                && !Regex.IsMatch(v, @"\b(B°|Localidad|Domicilio|Formosa|Genero)\b", RegexOptions.IgnoreCase)
                && v.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 6,
            "Razón Social" => Regex.IsMatch(v, @"[A-Za-zÁÉÍÓÚÑáéíóúñ]{3,}"),
            "Descripción" => v.Length >= 8,
            _ => v.Length >= 2
        };
    }

    private static bool IsLabelToken(string text)
    {
        var n = Normalize(text).Trim(':');
        if (n.Length == 0) return true;
        if (n is ":" or "-" or "/" or "y" or "de" or "del" or "la" or "el") return true;
        return LabelTokens.Contains(n);
    }

    private static Box Merge(List<Box> words, int start, int end)
    {
        var slice = words.Skip(start).Take(end - start + 1).ToList();
        return new Box(
            string.Join(' ', slice.Select(s => s.Text)),
            slice.Min(s => s.Left),
            slice.Max(s => s.Right),
            slice.Min(s => s.Bottom),
            slice.Max(s => s.Top));
    }

    private static string Normalize(string text) =>
        text.Trim().Trim(':').ToLowerInvariant();

    private readonly record struct Box(string Text, double Left, double Right, double Bottom, double Top)
    {
        public double MidY => (Bottom + Top) / 2d;
    }
}
