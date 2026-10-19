using System.Globalization;
using System.Text;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;

namespace Auditart.Application.WhatsApp;

public static class WhatsAppTurnoMessage
{
    public static string Build(
        AuditService service,
        User? notifier,
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

        var medico = service.Prestador?.Nombre?.Trim()
            ?? service.Profesional?.Trim()
            ?? "(a confirmar)";

        var notifierPhone = PhoneNormalizer.ToE164Argentina(notifier?.WhatsAppPhone);
        contactWaLink = notifierPhone is null ? null : $"https://wa.me/{notifierPhone}";

        var sb = new StringBuilder();
        sb.AppendLine($"Estimado/a *{paciente}*, buen día! Le enviamos los datos de su nuevo turno para *{motivo}*:");
        sb.AppendLine();
        sb.AppendLine($"📆 Fecha: {fecha}");
        sb.AppendLine($"📍 Dirección: {direccion}");
        sb.AppendLine($"🏥 Médico asignado: {medico}");
        sb.AppendLine();
        sb.AppendLine("⚠️ IMPORTANTE: Al presentarse en el centro médico o consultorio, debe indicar que asiste de parte de AUDITART, no de su ART.");
        sb.AppendLine();
        sb.AppendLine("En caso de no poder asistir, le pedimos avisar con anticipación.");
        sb.AppendLine("Recuerde que no debe abonar ningún importe al momento del turno.");
        sb.AppendLine("Si le solicitan algún pago, comuníquese de inmediato con nosotros.");
        sb.AppendLine();
        sb.AppendLine("————————————");
        sb.AppendLine("ℹ️ Este mensaje es solo informativo: *no respondas a este número*.");
        if (contactWaLink is not null && notifier is not null)
        {
            sb.AppendLine(
                $"Para consultas o avisos, escribinos por WhatsApp a *{notifier.Name}*:");
            sb.AppendLine(contactWaLink);
        }
        else
        {
            sb.AppendLine(
                "Para consultas, contactá a tu operador de Auditart (el número de contacto no está configurado todavía).");
        }

        sb.AppendLine();
        sb.AppendLine("Muchas gracias,");
        sb.Append("¡Saludos! 😊👋🏻");
        return sb.ToString();
    }
}
