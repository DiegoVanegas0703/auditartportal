using System.Text;

namespace Auditart.Application.WhatsApp;

public static class PhoneNormalizer
{
    /// <summary>Normaliza teléfonos argentinos a E.164 sin '+' (ej. 5491112345678).</summary>
    public static string? ToE164Argentina(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsDigit(c)) sb.Append(c);
        }

        var digits = sb.ToString();
        if (digits.Length < 8) return null;

        if (digits.StartsWith("00")) digits = digits[2..];

        // Ya con código país 54
        if (digits.StartsWith("54"))
        {
            // 5411XXXXXXXX (12) → falta el 9 de móvil
            if (digits.Length == 12 && !digits.StartsWith("549"))
                return "549" + digits[2..];
            // 549XXXXXXXXXX (13) u otras formas válidas
            return digits.Length is >= 12 and <= 13 ? digits : null;
        }

        if (digits.StartsWith('0')) digits = digits[1..];

        // 15XXXXXXXX → móvil CABA sin código de área
        if (digits.StartsWith("15") && digits.Length == 10)
            digits = "911" + digits[2..];

        // Área + número local (10 dígitos), ej. 11 1234-5678 → 54911…
        if (digits.Length == 10)
            return "549" + digits;

        // Ya con 9 de móvil (11 dígitos), ej. 91112345678
        if (digits.Length == 11 && digits.StartsWith('9'))
            return "54" + digits;

        return null;
    }
}
