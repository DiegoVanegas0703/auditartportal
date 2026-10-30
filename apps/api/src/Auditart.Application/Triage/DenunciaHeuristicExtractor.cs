using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Auditart.Application.Triage;

/// <summary>
/// Extrae campos de denuncias de distintas ART (texto etiquetado, layouts mezclados y AcroForm).
/// </summary>
public static class DenunciaHeuristicExtractor
{
    private static readonly Regex CuilRegex = new(
        @"\b((?:20|23|24|27)-?\d{8}-?\d)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex CuitEmpresaRegex = new(
        @"\b((?:30|33)-?\d{8}-?\d)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex DniLabeledRegex = new(
        @"\bDNI\s*[:.]?\s*(\d{7,8})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex DateRegex = new(
        @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})\b",
        RegexOptions.CultureInvariant);

    private static readonly string[] ArtEmailNeedles =
    [
        "art.com.ar", "reconquista", "sanfranciscoart", "galenoart", "asociart",
        "provinciaart", "sancor", "prevencion@", "cecap", "smata.com"
    ];

    public static DenunciaParsedDto Extract(
        string text,
        IReadOnlyDictionary<string, string> formFields)
    {
        var spaced = Normalize(text);
        var compact = Compact(spaced);
        var fields = formFields ?? new Dictionary<string, string>();

        var fullName = FromFields(fields, "Apellido y Nombre", "Nombre y Apellido", "Nombre y apellido");
        var lastName = FromFields(fields, "Apellido", "Apellidos");
        var firstName = FromFields(fields, "Nombres", "Nombre");
        if (!string.IsNullOrWhiteSpace(lastName)
            && !string.IsNullOrWhiteSpace(fullName)
            && !fullName.Contains(lastName, StringComparison.OrdinalIgnoreCase)
            && lastName.Split(' ').Length <= 2)
        {
            fullName = $"{lastName} {fullName}";
        }

        var nombre = PreferRicherName(
            fullName,
            JoinName(lastName, firstName),
            MatchPainefilName(spaced, compact),
            Labeled(spaced, compact,
                "nombre y apellido", "apellido y nombre", "apellidos y nombres", "apellido, nombre"));

        var documento = FirstNonEmpty(
            FromFields(fields, "CUIL", "Cuil", "Nro. Documento", "Nro Documento", "Documento"),
            Labeled(spaced, compact, "tipo y nro de doc", "tipo y nº de doc", "tipo y n° de doc",
                "doc. tipo y nro", "cuil", "cuit del trabajador"),
            MatchGrupo(CuilRegex, spaced, compact),
            MatchGrupo(DniLabeledRegex, spaced, compact));

        var empleador = FirstNonEmpty(
            FromFields(fields, "Razón Social", "Razon Social"),
            SectionLabeled(spaced, compact, "datos del empleador",
                "razón social", "razon social", "nombre de la empresa", "empleador"),
            MatchGrupo(new Regex(@"Razon Social\s+([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑ0-9\s\.]{3,40}?)(?=\s+CUIT|\s+Establecimiento|$)", RegexOptions.IgnoreCase), spaced, compact));

        var siniestro = FirstNonEmpty(
            FromFields(fields, "Aviso SiniestroNroContr", "Siniestro N°", "Siniestro Nro", "Siniestro"),
            Labeled(spaced, compact, "nro siniestro", "número de siniestro", "numero de siniestro",
                "siniestro n°", "siniestro nro", "siniestro:"),
            MatchSiniestroLoose(spaced, compact));

        var fecha = FirstNonEmpty(
            FromFields(fields, "Fecha de ocurrencia o primera manifestación", "Fecha del siniestro"),
            Labeled(spaced, compact,
                "fecha del siniestro", "fecha de ocurrencia o primera manifestación",
                "fecha de ocurrencia", "fecha del accidente", "fecha accidente"),
            MatchFechaSiniestro(spaced, compact));

        var telefono = FirstNonEmpty(
            FromFields(fields, "Celular", "Teléfono_2", "Tel. Contacto", "Telefono contacto"),
            Labeled(spaced, compact, "celular", "tel. contacto", "tel contacto", "teléfono", "telefono", "tel:"),
            MatchPhoneLoose(spaced));

        var email = FirstNonEmpty(
            WorkerEmail(FromFields(fields, "Email", "E-mail", "E-Mail", "Mail")),
            WorkerEmail(Labeled(spaced, compact, "e-mail", "email", "correo")),
            WorkerEmail(MatchGrupo(EmailRegex, spaced, compact)));

        var descripcion = FirstNonEmpty(
            FromFields(fields, "Descripción", "Descripcion", "Descripción del hecho Denunciante"),
            LabeledBlock(spaced, compact,
                "descripción de cómo ocurrió el accidente",
                "descripcion de como ocurrio el accidente",
                "breve relato",
                "actividad que realizaba y descripción del hecho",
                "descripción del accidente",
                "descripcion del accidente",
                "descripción del hecho"));

        return new DenunciaParsedDto(
            CleanName(nombre),
            CleanDocumento(documento),
            CleanEmpleador(empleador),
            CleanSiniestro(siniestro),
            CleanFecha(fecha),
            CleanDescription(descripcion),
            CleanPhone(telefono),
            email);
    }

    private static string Normalize(string text)
    {
        var t = text.Replace('\u00a0', ' ').Replace('\t', ' ');
        t = Regex.Replace(t, @"[ ]{2,}", " ");
        // Separa etiquetas pegadas al valor (PDFs sin espacios).
        t = Regex.Replace(t, @"(?<=[A-Za-zÁÉÍÓÚÑáéíóúñ])(?=CUIL|CUIT|DNI|C\.P\.|CP:|Tel:|Celular|E-Mail|Email)", " ");
        t = Regex.Replace(t, @"(CUIL|CUIT|DNI|Celular|Teléfono|Telefono)(?=\d)", "$1 ");
        t = Regex.Replace(t, @"(Nombre y Apellido|Apellido y Nombre|Apellido, Nombre|Razón Social|Razon Social|Nro Siniestro|Fecha del siniestro|Datos del Trabajador|Datos del Empleador)(?=[A-Z0-9ÁÉÍÓÚÑ])", "$1 ");
        return t.Trim();
    }

    private static string Compact(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();

    private static string? FromFields(IReadOnlyDictionary<string, string> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        foreach (var (key, value) in fields)
        {
            foreach (var name in names)
            {
                if (key.Contains(name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        return null;
    }

    private static string? JoinName(string? apellido, string? nombres)
    {
        var a = apellido?.Trim();
        var n = nombres?.Trim();
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(n)) return null;
        if (string.IsNullOrEmpty(a)) return n;
        if (string.IsNullOrEmpty(n)) return a;
        return $"{a} {n}";
    }

    private static string? Labeled(string spaced, string compact, params string[] labels)
    {
        foreach (var label in labels)
        {
            var hit = LabeledIn(spaced, label) ?? LabeledIn(compact, label);
            if (!string.IsNullOrWhiteSpace(hit)) return hit;
        }

        return null;
    }

    private static readonly string NextFieldPattern =
        @"CUIL|CUIT|DNI|C\.P\.|CP:|Localidad|Provincia|Sexo|Teléfono|Telefono|Celular|E-mail|E-Mail|Email|Fecha Nac|Fecha de Nac|Fecha del siniestro|Fecha de ocurrencia|Tipo y|Doc\.|Turno|Obra Social|Nacionalidad|Establecimiento|Contrato|CIIU|Domicilio|Genero|Género|Estado civil|Edad|Puesto de Trabajo|Información|INFORMACION|DATOS DEL|Nro Siniestro|Siniestro";

    private static string? LabeledIn(string text, string label)
    {
        var pattern =
            $@"(?:^|[^\w]){Regex.Escape(label)}\s*[:.]?\s*(?:[\r\n]+\s*)?(.+?)(?=(?:\s{{2,}}|[|]|\r?\n|(?:{NextFieldPattern})\b)|$)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var value = match.Groups[1].Value.Trim().Trim(',', ';', '-', ':');
        value = Regex.Replace(value, @"\s+", " ");
        if (value.Length < 2) return null;
        if (value.Equals(label, StringComparison.OrdinalIgnoreCase)) return null;
        if (LooksLikeLabelNoise(value)) return null;
        return value;
    }

    private static bool LooksLikeLabelNoise(string value)
    {
        if (Regex.IsMatch(value, @"\b(Contrato\s*N|Fax|C\.P\.|Localidad|Domicilio|Información|Datos del)\b", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(value, @"\bB°\b|\bC\.P\b"))
            return true;
        return false;
    }

    private static string? SectionLabeled(string spaced, string compact, string section, params string[] labels)
    {
        var slice = SliceAfter(spaced, section) ?? SliceAfter(compact, section) ?? spaced;
        return Labeled(slice, Compact(slice), labels);
    }

    private static string? SliceAfter(string text, string header)
    {
        var idx = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + header.Length;
        var nextHeaders = new[]
        {
            "DATOS DEL TRABAJADOR", "Datos del Trabajador",
            "INFORMACIÓN SOBRE EL SINIESTRO", "INFORMACION SOBRE EL SINIESTRO",
            "CENTRO MÉDICO", "PRESTADOR"
        };
        var end = text.Length;
        foreach (var h in nextHeaders)
        {
            var at = text.IndexOf(h, start, StringComparison.OrdinalIgnoreCase);
            if (at > start && at < end) end = at;
        }

        return text[start..end];
    }

    private static string? LabeledBlock(string spaced, string compact, params string[] labels)
    {
        foreach (var source in new[] { spaced, compact })
        {
            foreach (var label in labels)
            {
                var idx = source.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                var start = idx + label.Length;
                while (start < source.Length && (source[start] is ':' or '.' or ' ' or '\n' or '\r'))
                    start++;
                var rest = source[start..].Trim();
                rest = Regex.Split(rest, @"\r?\n\s*(?:INFORMACIÓN|ACCIDENTE DE TRABAJO|ENFERMEDAD PROFESIONAL|Códigos Diagnóstico|Para ser llenado|RAM 02|Lugar y Fecha)\b", RegexOptions.IgnoreCase)[0];
                rest = rest.Trim();
                if (rest.Length >= 8) return rest;
            }
        }

        return null;
    }

    private static string? MatchPainefilName(string spaced, string compact)
    {
        var sf = Regex.Match(
            compact,
            @"Nombre y Apellido[:\sA-Za-zº°NroDoc\.]*?([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑ ,]{6,}?)\s+(?:20|23|24|27)-?\d",
            RegexOptions.IgnoreCase);
        if (sf.Success) return sf.Groups[1].Value.Trim().Trim(',');

        var tula = Regex.Match(
            compact,
            @"Apellido y Nombre\s*[:.]?\s*([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑ ,]{6,}?)\s+(?:Doc|CUIL|Sexo)",
            RegexOptions.IgnoreCase);
        if (tula.Success) return tula.Groups[1].Value.Trim().Trim(',');

        var last = Regex.Match(
            compact,
            @"\b([A-ZÁÉÍÓÚÑ]{4,})\s+CUIL\b",
            RegexOptions.IgnoreCase);
        var first = Regex.Match(
            compact,
            @"\b([A-ZÁÉÍÓÚÑ]{3,}(?:\s+[A-ZÁÉÍÓÚÑ]{3,}){1,3})\s+\d{2}/\d{2}/\d{2}\s+\d{2}:");
        if (last.Success && first.Success)
            return $"{last.Groups[1].Value.Trim()} {first.Groups[1].Value.Trim()}";
        if (first.Success && last.Success == false)
            return first.Groups[1].Value.Trim();
        if (last.Success) return last.Groups[1].Value.Trim();
        return null;
    }

    private static string? CleanEmpleador(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = Regex.Replace(value, @"\b(?:20|23|24|27|30|33)\d{8,}\b", " ");
        v = Regex.Replace(v, @"\d{5,}", " ");
        v = Regex.Replace(v, @"\s+", " ").Trim(" .,-".ToCharArray());
        if (v.Count(char.IsLetter) < 6 || v.Length > 90) return null;
        if (LooksLikeLabelNoise(v)) return null;
        return v;
    }

    private static string? MatchGrupo(Regex regex, params string[] sources)
    {
        foreach (var source in sources)
        {
            var m = regex.Match(source);
            if (m.Success) return m.Groups[1].Success ? m.Groups[1].Value : m.Value;
        }

        return null;
    }

    private static string? MatchSiniestroLoose(string spaced, string compact)
    {
        var m = Regex.Match(compact, @"Nro\s*Siniestro\s*(\d+\s*/\s*\d+|\d{4,})", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(spaced, @"Siniestro\s*N[°ºo]?\s*[:.]?\s*(\d{4,})", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? MatchFechaSiniestro(string spaced, string compact)
    {
        foreach (var source in new[] { spaced, compact })
        {
                var around = Regex.Match(
                source,
                @"(?:Fecha del siniestro|Fecha de ocurrencia|Información sobre el accidente|Siniestro Fecha|Siniestro)\D{0,40}?(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})",
                RegexOptions.IgnoreCase);
            if (around.Success) return around.Groups[1].Value;
        }

        return null;
    }

    private static string? MatchPhoneLoose(string text)
    {
        foreach (Match m in Regex.Matches(text, @"\b(0?\d{2,4}[\s-]?\d{6,8})\b"))
        {
            var digits = Digits(m.Value);
            if (digits.Length is >= 8 and <= 13
                && !digits.StartsWith("0800")
                && !digits.StartsWith("30")
                && !digits.StartsWith("33")
                && !IsLikelyArtPhone(digits))
                return digits;
        }

        return null;
    }

    private static bool IsLikelyArtPhone(string digits) =>
        digits is "541139878410" or "43407400" or "541143221394" or "541143221395";

    private static string? WorkerEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var m = EmailRegex.Match(value);
        if (!m.Success) return null;
        var email = m.Value;
        return ArtEmailNeedles.Any(n => email.Contains(n, StringComparison.OrdinalIgnoreCase))
            ? null
            : email;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? PreferRicherName(params string?[] values)
    {
        return values
            .Select(CleanName)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .OrderByDescending(v => v!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
            .ThenByDescending(v => v!.Length)
            .FirstOrDefault();
    }

    private static string? CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = Regex.Replace(value, @"\s+", " ").Trim(" .,-".ToCharArray());
        v = Regex.Replace(v, @"\b(CUIL|CUIT|DNI|DATOS DEL TRABAJADOR|Sexo)\b.*", "", RegexOptions.IgnoreCase).Trim();
        if (v.Length < 3 || v.Length > 80) return null;
        if (CuilRegex.IsMatch(v) || CuitEmpresaRegex.IsMatch(v)) return null;
        if (LooksLikeLabelNoise(v) || Regex.IsMatch(v, @"\d{4,}")) return null;
        return v;
    }

    private static string? CleanDocumento(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = Digits(value);
        if (digits.Length is 11 or 7 or 8) return digits;
        var m = CuilRegex.Match(value);
        return m.Success ? Digits(m.Value) : null;
    }

    private static string? CleanSiniestro(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = Regex.Replace(value, @"[^\d/ ]", " ");
        var parts = Regex.Matches(digits, @"\d+")
            .Select(m => m.Value)
            .Where(p => p.Length >= 1)
            .ToArray();
        if (parts.Length == 0) return null;
        if (parts.Length >= 2 && parts[0].Length <= 2 && parts[1].Length >= 4)
            return $"{parts[0]}/{parts[1]}";
        var longest = parts.OrderByDescending(p => p.Length).First();
        if (longest.Length < 3 || Regex.IsMatch(value, @"\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}"))
            return null;
        return longest;
    }

    private static string? CleanFecha(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var m = DateRegex.Match(value);
        if (!m.Success) return null;
        var raw = m.Value.Replace('-', '/');
        var parts = raw.Split('/');
        if (parts.Length != 3) return raw;
        if (parts[2].Length == 2)
            parts[2] = (int.Parse(parts[2], CultureInfo.InvariantCulture) >= 70 ? "19" : "20") + parts[2];
        return string.Join('/', parts);
    }

    private static string? CleanPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Contains("SIN TEL", StringComparison.OrdinalIgnoreCase)) return null;
        var digits = Digits(value);
        if (digits.StartsWith("54") && digits.Length >= 12)
            digits = digits[2..];
        if (digits.Length is < 8 or > 12) return null;
        if (digits.StartsWith("0800") || IsLikelyArtPhone(digits)) return null;
        // CUIT de empresa (30/33 + 11 dígitos), no es un celular.
        if (digits.Length == 11 && (digits.StartsWith("30") || digits.StartsWith("33")))
            return null;
        return digits;
    }

    private static string? CleanDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = Regex.Replace(value, @"\s+", " ").Trim();
        if (v.Length < 8) return null;
        return v.Length > 800 ? v[..800] : v;
    }

    private static string Digits(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            if (char.IsDigit(c)) sb.Append(c);
        return sb.ToString();
    }
}
