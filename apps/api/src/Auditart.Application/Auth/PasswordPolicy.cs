using System.Security.Cryptography;

namespace Auditart.Application.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 10;

    public const string RequirementsText =
        "Mínimo 10 caracteres, con al menos una letra y un número.";

    public static void EnsureValid(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new InvalidOperationException($"La contraseña debe tener al menos {MinLength} caracteres.");

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new InvalidOperationException("La contraseña debe incluir al menos una letra y un número.");
    }

    public static string GenerateTemporary()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        return $"Aa{raw}1";
    }
}
