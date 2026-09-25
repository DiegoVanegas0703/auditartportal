using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

/// <summary>
/// Prestador / doctor de la cartilla operativa (reemplazo del Excel PRESTADORES).
/// El orden de propiedades sigue las columnas del Excel.
/// </summary>
public class Prestador : Entity
{
    public string Provincia { get; private set; } = string.Empty;
    public string Localidad { get; private set; } = string.Empty;
    public string? Cuit { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string? Drive { get; private set; }
    public string? Especialidad { get; private set; }
    public string? Servicio { get; private set; }
    public string? Domicilio { get; private set; }
    public string? CodigoPostal { get; private set; }
    public string? Telefonos { get; private set; }
    public string? Interno { get; private set; }
    public string? Horario { get; private set; }
    public string? MailContacto { get; private set; }
    public string? MailAdmision { get; private set; }
    public string? Convenios { get; private set; }
    public string? Operativo { get; private set; }
    public string? Adhesion { get; private set; }
    public string? Dni { get; private set; }
    public string? Matricula { get; private set; }
    public string? Afip { get; private set; }
    public string? Iibb { get; private set; }
    public string? Superintendencia { get; private set; }
    public string? Seguro { get; private set; }
    public string? HabSalud { get; private set; }
    public string? HabMunic { get; private set; }
    public string? Banco { get; private set; }
    public string? Sucursal { get; private set; }
    public string? TipoCuenta { get; private set; }
    public string? NumeroCuenta { get; private set; }
    public string? Cbu { get; private set; }
    public string? Alias { get; private set; }
    public string? UltimaActualizacionValores { get; private set; }
    public string? ValoresAcordados { get; private set; }
    public string? FormaPago { get; private set; }
    public string? Observaciones { get; private set; }

    /// <summary>Derivado de FormaPago (p.ej. "ANTICIPADO").</summary>
    public bool RequierePagoAnticipado { get; private set; }

    /// <summary>Valor de consulta parseado desde ValoresAcordados cuando es posible.</summary>
    public decimal? ValorConsulta { get; private set; }

    public string? FirmaS3Key { get; private set; }
    public string? FirmaFileName { get; private set; }
    public string? FirmaContentType { get; private set; }
    public DateTime? FirmaUploadedAtUtc { get; private set; }

    public bool IsActive { get; private set; } = true;
    public int? ExcelRowNumber { get; private set; }

    private Prestador() { }

    public static Prestador Create(PrestadorData data, int? excelRowNumber = null)
    {
        var entity = new Prestador();
        entity.Apply(data);
        entity.ExcelRowNumber = excelRowNumber;
        entity.IsActive = data.IsActive;
        return entity;
    }

    public void Update(PrestadorData data)
    {
        Apply(data);
        IsActive = data.IsActive;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    public void SetExcelRowNumber(int? excelRowNumber)
    {
        ExcelRowNumber = excelRowNumber;
        Touch();
    }

    private void Apply(PrestadorData data)
    {
        Provincia = Normalize(data.Provincia) ?? string.Empty;
        Localidad = Normalize(data.Localidad) ?? string.Empty;
        Cuit = Normalize(data.Cuit);
        Nombre = Normalize(data.Nombre) ?? throw new ArgumentException("El nombre del prestador es obligatorio.");
        Drive = Normalize(data.Drive);
        Especialidad = Normalize(data.Especialidad);
        Servicio = Normalize(data.Servicio);
        Domicilio = Normalize(data.Domicilio);
        CodigoPostal = Normalize(data.CodigoPostal);
        Telefonos = Normalize(data.Telefonos);
        Interno = Normalize(data.Interno);
        Horario = Normalize(data.Horario);
        MailContacto = Normalize(data.MailContacto);
        MailAdmision = Normalize(data.MailAdmision);
        Convenios = Normalize(data.Convenios);
        Operativo = Normalize(data.Operativo);
        Adhesion = Normalize(data.Adhesion);
        Dni = Normalize(data.Dni);
        Matricula = Normalize(data.Matricula);
        Afip = Normalize(data.Afip);
        Iibb = Normalize(data.Iibb);
        Superintendencia = Normalize(data.Superintendencia);
        Seguro = Normalize(data.Seguro);
        HabSalud = Normalize(data.HabSalud);
        HabMunic = Normalize(data.HabMunic);
        Banco = Normalize(data.Banco);
        Sucursal = Normalize(data.Sucursal);
        TipoCuenta = Normalize(data.TipoCuenta);
        NumeroCuenta = Normalize(data.NumeroCuenta);
        Cbu = Normalize(data.Cbu);
        Alias = Normalize(data.Alias);
        UltimaActualizacionValores = Normalize(data.UltimaActualizacionValores);
        ValoresAcordados = Normalize(data.ValoresAcordados);
        FormaPago = Normalize(data.FormaPago);
        Observaciones = Normalize(data.Observaciones);
        RequierePagoAnticipado = data.RequierePagoAnticipado
            ?? DetectaPagoAnticipado(FormaPago);
        ValorConsulta = data.ValorConsulta ?? ParseValorConsulta(ValoresAcordados);
    }

    public void SetFirma(string s3Key, string fileName, string contentType)
    {
        FirmaS3Key = s3Key;
        FirmaFileName = Path.GetFileName(fileName);
        FirmaContentType = contentType;
        FirmaUploadedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void ClearFirma()
    {
        FirmaS3Key = null;
        FirmaFileName = null;
        FirmaContentType = null;
        FirmaUploadedAtUtc = null;
        Touch();
    }

    public void SetValorYPago(decimal? valorConsulta, bool? requierePagoAnticipado)
    {
        if (valorConsulta.HasValue) ValorConsulta = valorConsulta;
        if (requierePagoAnticipado.HasValue) RequierePagoAnticipado = requierePagoAnticipado.Value;
        Touch();
    }

    public void RecomputeValoresFromText()
    {
        ValorConsulta = ParseValorConsulta(ValoresAcordados);
        RequierePagoAnticipado = DetectaPagoAnticipado(FormaPago);
        Touch();
    }

    public static bool DetectaPagoAnticipado(string? formaPago) =>
        !string.IsNullOrWhiteSpace(formaPago)
        && (formaPago.Contains("ANTICIP", StringComparison.OrdinalIgnoreCase)
            || formaPago.Contains("ADELANT", StringComparison.OrdinalIgnoreCase));

    public static decimal? ParseValorConsulta(string? valores)
    {
        if (string.IsNullOrWhiteSpace(valores)) return null;

        var patterns = new[]
        {
            @"CONSULTA[^\d$]*\$?\s*([\d][\d\.\,]*)",
            @"\$\s*([\d][\d\.\,]*)",
            @"([\d]{1,3}(?:[.\s]\d{3})+(?:,\d{1,2})?)",
            @"([\d]+,\d{2})\b",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                valores,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            var parsed = TryParseMoney(match.Groups[1].Value);
            if (parsed is > 0) return parsed;
        }

        return null;
    }

    private static decimal? TryParseMoney(string raw)
    {
        var s = raw.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (s.Length == 0) return null;

        // 18.000,00 o 48,400.50 style
        if (s.Contains(',') && s.Contains('.'))
        {
            if (s.LastIndexOf(',') > s.LastIndexOf('.'))
                s = s.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
            else
                s = s.Replace(",", "", StringComparison.Ordinal);
        }
        else if (s.Contains(','))
        {
            var parts = s.Split(',');
            s = parts[^1].Length <= 2
                ? s.Replace(",", ".", StringComparison.Ordinal)
                : s.Replace(",", "", StringComparison.Ordinal);
        }
        else if (s.Contains('.'))
        {
            var parts = s.Split('.');
            if (parts.Length > 2 || (parts.Length == 2 && parts[^1].Length == 3))
                s = s.Replace(".", "", StringComparison.Ordinal);
        }

        return decimal.TryParse(
            s,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }
}

public sealed record PrestadorData(
    string? Provincia,
    string? Localidad,
    string? Cuit,
    string Nombre,
    string? Drive,
    string? Especialidad,
    string? Servicio,
    string? Domicilio,
    string? CodigoPostal,
    string? Telefonos,
    string? Interno,
    string? Horario,
    string? MailContacto,
    string? MailAdmision,
    string? Convenios,
    string? Operativo,
    string? Adhesion,
    string? Dni,
    string? Matricula,
    string? Afip,
    string? Iibb,
    string? Superintendencia,
    string? Seguro,
    string? HabSalud,
    string? HabMunic,
    string? Banco,
    string? Sucursal,
    string? TipoCuenta,
    string? NumeroCuenta,
    string? Cbu,
    string? Alias,
    string? UltimaActualizacionValores,
    string? ValoresAcordados,
    string? FormaPago,
    string? Observaciones = null,
    bool IsActive = true,
    bool? RequierePagoAnticipado = null,
    decimal? ValorConsulta = null);
