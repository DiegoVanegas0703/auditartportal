using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

/// <summary>ART (aseguradora de riesgos del trabajo) del catálogo.</summary>
public class Aseguradora : Entity
{
    public string Nombre { get; private set; } = string.Empty;
    public string Cuit { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Aseguradora() { }

    public static Aseguradora Create(string nombre, string cuit)
    {
        var entity = new Aseguradora();
        entity.Update(nombre, cuit, isActive: true);
        return entity;
    }

    public void Update(string nombre, string cuit, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la ART es obligatorio.");
        Nombre = nombre.Trim();
        Cuit = NormalizeCuit(cuit);
        IsActive = isActive;
        Touch();
    }

    /// <summary>Formato XX-XXXXXXXX-X cuando hay 11 dígitos; si no, el texto recortado.</summary>
    public static string NormalizeCuit(string? cuit)
    {
        var digits = new string((cuit ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 11
            ? $"{digits[..2]}-{digits[2..10]}-{digits[10]}"
            : (cuit ?? string.Empty).Trim();
    }
}
