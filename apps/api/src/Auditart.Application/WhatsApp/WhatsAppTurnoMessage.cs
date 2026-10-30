using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;

namespace Auditart.Application.WhatsApp;

public static class WhatsAppTurnoMessage
{
    public const string KindTurno = WhatsAppTemplate.KeyTurno;
    public const string KindTelemedicina = WhatsAppTemplate.KeyTelemedicina;

    public static readonly string DefaultTurno = """
        Estimado/a *{{paciente}}*, buen día! Le enviamos los datos de su nuevo turno para *{{motivo}}*:

        📆 Fecha: {{fecha}}
        📍 Dirección: {{direccion}}
        🏥 Médico asignado: {{medico}}

        ⚠️ IMPORTANTE: Al presentarse en el centro médico o consultorio, debe indicar que asiste de parte de AUDITART, no de su ART.

        En caso de no poder asistir, le pedimos avisar con anticipación.
        Recuerde que no debe abonar ningún importe al momento del turno.
        Si le solicitan algún pago, comuníquese de inmediato con nosotros.

        ————————————
        ℹ️ Este mensaje es solo informativo: *no respondas a este número*.
        {{contacto_bloque}}

        Muchas gracias,
        ¡Saludos! 😊👋🏻
        """.Replace("\r\n", "\n");

    public static readonly string DefaultTelemedicina = """
        ¡Hola!
        Le informamos que un médico auditor de su ART se estará comunicando con usted a lo largo del día. Le solicitamos, por favor, que este atento/a a la llamada.
        El profesional le enviará un aviso previo antes de contactarlo/a. En caso de que no pueda atender en ese momento, podrá coordinar otro horario para la llamada:

        Le adjunto el teléfono:
        {{doctor_line}}

        Muchas gracias.
        Saludos cordiales,
        """.Replace("\r\n", "\n");

    public static readonly IReadOnlyList<WhatsAppDoctorContact> TelemedicinaDoctors =
    [
        new("Dra. Monica Atie", "Atie", "+54 9 2994 51-0696"),
        new("Dra. Ana Cañizal", "Cañizal", "+54 9 11 4082-8249"),
        new("Dra. Natasha Kovacs", "Kovacs", "+54 9 11 5115-5487"),
        new("Dra. Tamara Okulowicz", "Okulowicz", "+54 9 11 5376-0915"),
        new("Dra. Edith Esper", "Esper", "+54 9 11 4031-5373"),
        new("Dr. Fernando Morales", "Morales", "+54 9 11 4946-5303"),
        new("Dr. Carlos Correa", "Correa", "+54 9 11 3974-5943"),
        new("Dra. Nora Fontañez", "Fontañez", "+54 9 2974 08-8540"),
        new("Dr. Santiago Aldaz", "Aldaz", "+54 9 11 3107-2335"),
    ];

    public static string DefaultFor(string kind) =>
        IsTelemedicina(kind) ? DefaultTelemedicina : DefaultTurno;

    public static bool IsTelemedicina(string? kind) =>
        string.Equals(kind, KindTelemedicina, StringComparison.OrdinalIgnoreCase);

    public static string KindFor(AuditService service) =>
        service.TipoServicio == ServiceType.Telemedicina ? KindTelemedicina : KindTurno;

    public static string Build(
        AuditService service,
        User? notifier,
        string template,
        out string? contactWaLink)
    {
        var argentina = CultureInfo.GetCultureInfo("es-AR");
        var paciente = (service.PacienteEntity?.Nombre ?? service.Paciente ?? "paciente").Trim();
        var motivo = !string.IsNullOrWhiteSpace(service.Especialidad)
            ? service.Especialidad.Trim()
            : service.TipoServicio.ToString();

        var fecha = service.FechaTurnoUtc.HasValue
            ? service.FechaTurnoUtc.Value.ToLocalTime().ToString("dddd dd/MM/yyyy HH:mm", argentina)
            : "(a confirmar)";

        var direccion = service.Prestador?.Domicilio?.Trim();
        if (string.IsNullOrWhiteSpace(direccion))
            direccion = "(consultar con Auditart)";

        var medicoRaw = service.Prestador?.Nombre?.Trim()
            ?? service.Profesional?.Trim()
            ?? "(a confirmar)";

        var matched = MatchDoctor(medicoRaw);
        var medico = matched?.DisplayName ?? medicoRaw;
        var telefonoMedico = matched?.Phone
            ?? FirstPhone(service.Prestador?.Telefonos)
            ?? "(a confirmar)";
        var doctorLine = $"- {medico}: {telefonoMedico}";

        var notifierPhone = PhoneNormalizer.ToE164Argentina(notifier?.WhatsAppPhone);
        contactWaLink = notifierPhone is null ? null : $"https://wa.me/{notifierPhone}";

        string contactoBloque;
        if (contactWaLink is not null && notifier is not null)
        {
            contactoBloque =
                $"Para consultas o avisos, escribinos por WhatsApp a *{notifier.Name}*:\n{contactWaLink}";
        }
        else
        {
            contactoBloque =
                "Para consultas, contactá a tu operador de Auditart (el número de contacto no está configurado todavía).";
        }

        var body = string.IsNullOrWhiteSpace(template) ? DefaultFor(KindFor(service)) : template;
        return ReplaceTokens(body, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["paciente"] = paciente,
            ["motivo"] = motivo,
            ["fecha"] = fecha,
            ["direccion"] = direccion,
            ["medico"] = medico,
            ["telefono_medico"] = telefonoMedico,
            ["doctor_line"] = doctorLine,
            ["contacto_nombre"] = notifier?.Name ?? string.Empty,
            ["contacto_link"] = contactWaLink ?? string.Empty,
            ["contacto_bloque"] = contactoBloque,
        });
    }

    public static WhatsAppDoctorContact? MatchDoctor(string? name)
    {
        var haystack = Normalize(name);
        if (string.IsNullOrEmpty(haystack)) return null;
        return TelemedicinaDoctors.FirstOrDefault(d =>
            haystack.Contains(Normalize(d.MatchKey), StringComparison.Ordinal)
            || haystack.Contains(Normalize(d.DisplayName), StringComparison.Ordinal));
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> values)
    {
        return Regex.Replace(
            template.Replace("\r\n", "\n"),
            @"\{\{\s*([a-z0-9_]+)\s*\}\}",
            m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value,
            RegexOptions.IgnoreCase);
    }

    private static string? FirstPhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var first = raw.Split(['/', ';', ',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var formD = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.ToLowerInvariant(ch));
        }

        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", "");
    }
}

public sealed record WhatsAppDoctorContact(string DisplayName, string MatchKey, string Phone);
