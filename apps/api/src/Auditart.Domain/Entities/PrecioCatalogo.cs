using Auditart.Domain.Common;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;

namespace Auditart.Domain.Entities;

/// <summary>
/// Precio conciliado configurable (Facturación / Jefatura).
/// Especialista: ArtNombre vacío (catálogo global tipo VALOR PRESTADORES).
/// Auditor: ArtNombre = nombre de la ART.
/// </summary>
public class PrecioCatalogo : Entity
{
    public TipoProfesional TipoProfesional { get; private set; }
    public string? ArtNombre { get; private set; }
    public string Concepto { get; private set; } = string.Empty;
    public decimal Valor { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notas { get; private set; }

    private PrecioCatalogo() { }

    public static PrecioCatalogo Create(
        TipoProfesional tipo,
        string concepto,
        decimal valor,
        string? artNombre = null,
        string? notas = null)
    {
        if (string.IsNullOrWhiteSpace(concepto))
            throw new DomainException("El concepto del precio es obligatorio.");
        if (valor < 0)
            throw new DomainException("El valor no puede ser negativo.");

        var art = NormalizeArt(tipo, artNombre);
        return new PrecioCatalogo
        {
            TipoProfesional = tipo,
            ArtNombre = art,
            Concepto = concepto.Trim(),
            Valor = valor,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim(),
            IsActive = true
        };
    }

    public void Update(
        TipoProfesional tipo,
        string concepto,
        decimal valor,
        string? artNombre = null,
        string? notas = null,
        bool? isActive = null)
    {
        if (string.IsNullOrWhiteSpace(concepto))
            throw new DomainException("El concepto del precio es obligatorio.");
        if (valor < 0)
            throw new DomainException("El valor no puede ser negativo.");

        TipoProfesional = tipo;
        ArtNombre = NormalizeArt(tipo, artNombre);
        Concepto = concepto.Trim();
        Valor = valor;
        Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        if (isActive.HasValue) IsActive = isActive.Value;
        Touch();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        Touch();
    }

    private static string? NormalizeArt(TipoProfesional tipo, string? artNombre)
    {
        if (tipo == TipoProfesional.Especialista)
            return null;

        if (string.IsNullOrWhiteSpace(artNombre))
            throw new DomainException("Para médico auditor indicá la ART del precio.");

        return artNombre.Trim();
    }
}
