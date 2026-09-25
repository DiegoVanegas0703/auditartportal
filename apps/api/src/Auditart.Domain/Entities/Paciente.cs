using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

/// <summary>
/// Paciente padre de una o más prestaciones. Identidad: nombre + DNI.
/// </summary>
public class Paciente : Entity
{
    private static readonly Regex NonCode = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

    public string Nombre { get; private set; } = string.Empty;
    public string NombreNormalizado { get; private set; } = string.Empty;
    public string? Dni { get; private set; }
    public string? DniNormalizado { get; private set; }
    public string? Telefono { get; private set; }
    public string? Email { get; private set; }
    public string? Art { get; private set; }
    public string? NumeroSiniestro { get; private set; }

    public ICollection<AuditService> Prestaciones { get; private set; } = new List<AuditService>();

    private Paciente() { }

    public static Paciente Create(
        string nombre,
        string? dni,
        string? telefono = null,
        string? email = null,
        string? art = null,
        string? numeroSiniestro = null)
    {
        var entity = new Paciente();
        entity.Apply(nombre, dni, telefono, email, art, numeroSiniestro);
        return entity;
    }

    public void UpdateContact(
        string? telefono,
        string? email,
        string? art,
        string? numeroSiniestro)
    {
        if (!string.IsNullOrWhiteSpace(telefono)) Telefono = telefono.Trim();
        if (!string.IsNullOrWhiteSpace(email)) Email = email.Trim();
        if (!string.IsNullOrWhiteSpace(art)) Art = art.Trim();
        if (!string.IsNullOrWhiteSpace(numeroSiniestro)) NumeroSiniestro = numeroSiniestro.Trim();
        Touch();
    }

    /// <summary>Actualización completa de datos del paciente (ficha editable).</summary>
    public void Update(
        string nombre,
        string? dni,
        string? telefono,
        string? email,
        string? art,
        string? numeroSiniestro)
    {
        Apply(nombre, dni, telefono, email, art, numeroSiniestro);
        Touch();
    }

    public void ApplyIdentity(string nombre, string? dni)
    {
        Apply(nombre, dni, Telefono, Email, Art, NumeroSiniestro);
        Touch();
    }

    private void Apply(
        string nombre,
        string? dni,
        string? telefono,
        string? email,
        string? art,
        string? numeroSiniestro)
    {
        Nombre = (nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(Nombre))
            Nombre = "Sin identificar";
        NombreNormalizado = NormalizeNombre(Nombre);
        Dni = string.IsNullOrWhiteSpace(dni) ? null : dni.Trim();
        DniNormalizado = NormalizeDni(Dni);
        Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Art = string.IsNullOrWhiteSpace(art) ? null : art.Trim();
        NumeroSiniestro = string.IsNullOrWhiteSpace(numeroSiniestro) ? null : numeroSiniestro.Trim();
    }

    public static string NormalizeNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;
        var formD = nombre.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(ch);
        }

        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ")
            .Trim()
            .ToUpperInvariant();
    }

    public static string? NormalizeDni(string? dni)
    {
        if (string.IsNullOrWhiteSpace(dni)) return null;
        var compact = NonCode.Replace(dni, string.Empty).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(compact) ? null : compact;
    }
}
