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
        if (digits.StartsWith("54")) return digits.Length >= 12 ? digits : null;

        if (digits.StartsWith('0')) digits = digits[1..];

        // 15XXXXXXXX → móvil CABA sin código área
        if (digits.StartsWith("15") && digits.Length == 10)
            digits = "911" + digits[2..];

        // 11XXXXXXXX (10 dígitos) → 54911XXXXXXXX… wait: 11 + 8 = 10 → 549 + 11xxxxxxxx
        if (digits.Length == 10)
            return "54" + digits;

        // 9XXXXXXXXXX (11) ya con 9 de móvil
        if (digits.Length == 11 && digits.StartsWith('9'))
            return "54" + digits;

        if (digits.Length is >= 10 and <= 13)
            return digits;

        return null;
    }
}
