using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Prestadores;

public sealed class PrestadorService
{
    private static readonly Regex InactiveMarker = new(
        @"NO\s*TRABAJA(\s*MAS)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IAppDbContext _db;

    public PrestadorService(IAppDbContext db) => _db = db;

    public async Task<PrestadorPageDto> ListAsync(
        string? q,
        string? provincia,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var query = _db.Prestadores.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(provincia))
        {
            var p = provincia.Trim();
            query = query.Where(x => x.Provincia.Contains(p));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Nombre.Contains(term)
                || (x.Localidad != null && x.Localidad.Contains(term))
                || (x.Especialidad != null && x.Especialidad.Contains(term))
                || (x.Cuit != null && x.Cuit.Contains(term))
                || (x.Servicio != null && x.Servicio.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var items = await query
            .OrderBy(x => x.Provincia)
            .ThenBy(x => x.Localidad)
            .ThenBy(x => x.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var provincias = await _db.Prestadores.AsNoTracking()
            .Where(p => !isActive.HasValue || p.IsActive == isActive.Value)
            .Select(p => p.Provincia)
            .Where(p => p != null && p != "")
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync(ct);

        return new PrestadorPageDto(
            items.Select(Map).ToList(),
            page,
            pageSize,
            total,
            totalPages,
            provincias);
    }

    public async Task<IReadOnlyList<PrestadorOptionDto>> ListActiveOptionsAsync(
        string? q,
        string? provincia,
        int take,
        CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 500);
        var query = _db.Prestadores.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(provincia))
        {
            var p = provincia.Trim();
            query = query.Where(x => x.Provincia.Contains(p));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Nombre.Contains(term)
                || x.Localidad.Contains(term)
                || (x.Especialidad != null && x.Especialidad.Contains(term))
                || (x.Cuit != null && x.Cuit.Contains(term)));
        }

        return await query
            .OrderBy(x => x.Nombre)
            .Take(take)
            .Select(x => new PrestadorOptionDto(
                x.Id,
                x.Nombre,
                x.Provincia,
                x.Localidad,
                x.Especialidad,
                x.Servicio,
                x.Telefonos,
                x.MailContacto,
                x.RequierePagoAnticipado,
                x.ValorConsulta,
                x.ValoresAcordados,
                x.FormaPago,
                x.FirmaS3Key != null))
            .ToListAsync(ct);
    }

    public async Task<PrestadorDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Prestadores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Prestador no encontrado.");
        return Map(entity);
    }

    public async Task<PrestadorDto> CreateAsync(UpsertPrestadorRequest request, CancellationToken ct)
    {
        var data = ToData(request);
        var entity = Prestador.Create(data);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<PrestadorDto> UpdateAsync(Guid id, UpsertPrestadorRequest request, CancellationToken ct)
    {
        var entity = await _db.Prestadores.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Prestador no encontrado.");
        entity.Update(ToData(request));
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var entity = await _db.Prestadores.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Prestador no encontrado.");
        entity.SetActive(isActive);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PrestadorDto> SetFirmaAsync(
        Guid id,
        Stream content,
        string fileName,
        string contentType,
        long sizeBytes,
        IObjectStorage storage,
        CancellationToken ct)
    {
        if (sizeBytes <= 0)
            throw new InvalidOperationException("El archivo de firma está vacío.");
        if (sizeBytes > 5L * 1024 * 1024)
            throw new InvalidOperationException("La firma no puede superar 5 MB.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif"))
            throw new InvalidOperationException("La firma debe ser una imagen (PNG/JPG/WEBP).");

        var entity = await _db.Prestadores.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Prestador no encontrado.");

        var key = await storage.UploadAsync(
            content,
            fileName,
            contentType,
            $"prestadores/{id:N}/firma",
            ct);
        entity.SetFirma(key, fileName, contentType);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task ClearFirmaAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Prestadores.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Prestador no encontrado.");
        entity.ClearFirma();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(string Key, string FileName, string? ContentType)?> GetFirmaAsync(
        Guid id,
        CancellationToken ct)
    {
        var entity = await _db.Prestadores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Prestador no encontrado.");
        if (string.IsNullOrWhiteSpace(entity.FirmaS3Key))
            return null;
        return (entity.FirmaS3Key, entity.FirmaFileName ?? "firma.png", entity.FirmaContentType);
    }

    /// <summary>
    /// Recalcula valor de consulta y pago anticipado desde ValoresAcordados / FormaPago.
    /// </summary>
    public async Task<int> RecomputeValoresAsync(CancellationToken ct)
    {
        var items = await _db.Prestadores.ToListAsync(ct);
        var updated = 0;
        foreach (var p in items)
        {
            var valor = Prestador.ParseValorConsulta(p.ValoresAcordados);
            var anticipado = Prestador.DetectaPagoAnticipado(p.FormaPago);
            if (p.ValorConsulta == valor && p.RequierePagoAnticipado == anticipado)
                continue;
            p.RecomputeValoresFromText();
            updated++;
        }

        if (updated > 0)
            await _db.SaveChangesAsync(ct);
        return updated;
    }

    public async Task<PrestadorImportResult> ImportExcelAsync(Stream stream, CancellationToken ct)
    {
        var rows = ReadPrestadoresSheet(stream);
        if (rows.Count == 0)
            return new PrestadorImportResult(0, 0, 0, 0, ["No se encontraron filas en la hoja PRESTADORES."]);

        var existing = await _db.Prestadores.ToListAsync(ct);
        var byRow = existing
            .Where(x => x.ExcelRowNumber.HasValue)
            .GroupBy(x => x.ExcelRowNumber!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var byCuit = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.Cuit))
            .GroupBy(x => x.Cuit!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var byKey = existing.ToDictionary(
            x => MatchKey(x.Nombre, x.Provincia, x.Localidad),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(row.Data.Nombre))
                {
                    skipped++;
                    continue;
                }

                Prestador? match = null;
                if (row.ExcelRowNumber is int rn && byRow.TryGetValue(rn, out var byRn))
                    match = byRn;
                else if (!string.IsNullOrWhiteSpace(row.Data.Cuit) && byCuit.TryGetValue(row.Data.Cuit!, out var byC))
                    match = byC;
                else if (byKey.TryGetValue(MatchKey(row.Data.Nombre, row.Data.Provincia, row.Data.Localidad), out var byK))
                    match = byK;

                if (match is null)
                {
                    var entity = Prestador.Create(row.Data, row.ExcelRowNumber);
                    _db.Add(entity);
                    existing.Add(entity);
                    if (row.ExcelRowNumber is int newRn)
                        byRow[newRn] = entity;
                    if (!string.IsNullOrWhiteSpace(entity.Cuit))
                        byCuit[entity.Cuit!] = entity;
                    byKey[MatchKey(entity.Nombre, entity.Provincia, entity.Localidad)] = entity;
                    created++;
                }
                else
                {
                    match.Update(row.Data);
                    match.SetExcelRowNumber(row.ExcelRowNumber);
                    updated++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Fila {row.ExcelRowNumber}: {ex.Message}");
                if (errors.Count >= 25) break;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new PrestadorImportResult(rows.Count, created, updated, skipped, errors);
    }

    private static string MatchKey(string? nombre, string? provincia, string? localidad) =>
        $"{(nombre ?? "").Trim()}|{(provincia ?? "").Trim()}|{(localidad ?? "").Trim()}";

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }

    private static PrestadorData ToData(UpsertPrestadorRequest r) =>
        new(
            Truncate(r.Provincia, 120),
            Truncate(r.Localidad, 120),
            Truncate(r.Cuit, 32),
            Truncate(r.Nombre, 300) ?? throw new ArgumentException("El nombre del prestador es obligatorio."),
            Truncate(r.Drive, 500),
            Truncate(r.Especialidad, 200),
            Truncate(r.Servicio, 300),
            Truncate(r.Domicilio, 500),
            Truncate(r.CodigoPostal, 32),
            Truncate(r.Telefonos, 1000),
            Truncate(r.Interno, 64),
            Truncate(r.Horario, 200),
            Truncate(r.MailContacto, 320),
            Truncate(r.MailAdmision, 320),
            Truncate(r.Convenios, 500),
            Truncate(r.Operativo, 200),
            Truncate(r.Adhesion, 200),
            Truncate(r.Dni, 32),
            Truncate(r.Matricula, 64),
            Truncate(r.Afip, 120),
            Truncate(r.Iibb, 120),
            Truncate(r.Superintendencia, 200),
            Truncate(r.Seguro, 200),
            Truncate(r.HabSalud, 120),
            Truncate(r.HabMunic, 120),
            Truncate(r.Banco, 120),
            Truncate(r.Sucursal, 120),
            Truncate(r.TipoCuenta, 64),
            Truncate(r.NumeroCuenta, 64),
            Truncate(r.Cbu, 64),
            Truncate(r.Alias, 120),
            Truncate(r.UltimaActualizacionValores, 200),
            Truncate(r.ValoresAcordados, 2000),
            Truncate(r.FormaPago, 200),
            Truncate(r.Observaciones, 4000),
            r.IsActive,
            r.RequierePagoAnticipado,
            r.ValorConsulta);

    private static PrestadorDto Map(Prestador p) =>
        new(
            p.Id,
            p.Provincia,
            p.Localidad,
            p.Cuit,
            p.Nombre,
            p.Drive,
            p.Especialidad,
            p.Servicio,
            p.Domicilio,
            p.CodigoPostal,
            p.Telefonos,
            p.Interno,
            p.Horario,
            p.MailContacto,
            p.MailAdmision,
            p.Convenios,
            p.Operativo,
            p.Adhesion,
            p.Dni,
            p.Matricula,
            p.Afip,
            p.Iibb,
            p.Superintendencia,
            p.Seguro,
            p.HabSalud,
            p.HabMunic,
            p.Banco,
            p.Sucursal,
            p.TipoCuenta,
            p.NumeroCuenta,
            p.Cbu,
            p.Alias,
            p.UltimaActualizacionValores,
            p.ValoresAcordados,
            p.FormaPago,
            p.Observaciones,
            p.RequierePagoAnticipado,
            p.ValorConsulta,
            p.FirmaS3Key != null,
            p.FirmaFileName,
            p.FirmaUploadedAtUtc,
            p.IsActive,
            p.ExcelRowNumber,
            p.CreatedAtUtc,
            p.UpdatedAtUtc);

    private static List<(int ExcelRowNumber, PrestadorData Data)> ReadPrestadoresSheet(Stream stream)
    {
        using var doc = SpreadsheetDocument.Open(stream, false);
        var wbPart = doc.WorkbookPart
            ?? throw new InvalidOperationException("Excel inválido.");
        var sheet = wbPart.Workbook.Sheets?.Elements<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, "PRESTADORES", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No se encontró la hoja PRESTADORES.");
        var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
        var sst = wbPart.SharedStringTablePart?.SharedStringTable;

        var result = new List<(int, PrestadorData)>();
        foreach (var row in wsPart.Worksheet.Descendants<Row>())
        {
            var rowIndex = (int)(row.RowIndex?.Value ?? 0);
            if (rowIndex <= 1) continue;

            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements<Cell>())
            {
                var col = ColumnIndex(cell.CellReference?.Value);
                if (col <= 0) continue;
                cells[col] = GetCellText(sst, cell);
            }

            string Cell(int c) => cells.TryGetValue(c, out var v) ? v : string.Empty;

            var nombre = Cell(4).Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            var isActive = !InactiveMarker.IsMatch(nombre);
            var data = new PrestadorData(
                Provincia: Truncate(Clean(Cell(1)), 120),
                Localidad: Truncate(Clean(Cell(2)), 120),
                Cuit: Truncate(NormalizeCuit(Cell(3)), 32),
                Nombre: Truncate(nombre, 300)!,
                Drive: Truncate(Clean(Cell(5)), 500),
                Especialidad: Truncate(Clean(Cell(6)), 200),
                Servicio: Truncate(Clean(Cell(7)), 300),
                Domicilio: Truncate(Clean(Cell(8)), 500),
                CodigoPostal: Truncate(NormalizeCode(Cell(9)), 32),
                Telefonos: Truncate(Clean(Cell(10)), 1000),
                Interno: Truncate(Clean(Cell(11)), 64),
                Horario: Truncate(Clean(Cell(12)), 200),
                MailContacto: Truncate(Clean(Cell(13)), 320),
                MailAdmision: Truncate(Clean(Cell(14)), 320),
                Convenios: Truncate(Clean(Cell(15)), 500),
                Operativo: Truncate(Clean(Cell(16)), 200),
                Adhesion: Truncate(Clean(Cell(17)), 200),
                Dni: Truncate(NormalizeCode(Cell(18)), 32),
                Matricula: Truncate(Clean(Cell(19)), 64),
                Afip: Truncate(Clean(Cell(20)), 120),
                Iibb: Truncate(Clean(Cell(21)), 120),
                Superintendencia: Truncate(Clean(Cell(22)), 200),
                Seguro: Truncate(Clean(Cell(23)), 200),
                HabSalud: Truncate(Clean(Cell(24)), 120),
                HabMunic: Truncate(Clean(Cell(25)), 120),
                Banco: Truncate(Clean(Cell(26)), 120),
                Sucursal: Truncate(Clean(Cell(27)), 120),
                TipoCuenta: Truncate(Clean(Cell(28)), 64),
                NumeroCuenta: Truncate(NormalizeCode(Cell(29)), 64),
                Cbu: Truncate(NormalizeCode(Cell(30)), 64),
                Alias: Truncate(Clean(Cell(31)), 120),
                UltimaActualizacionValores: Truncate(Clean(Cell(32)), 200),
                ValoresAcordados: Truncate(Clean(Cell(33)), 2000),
                FormaPago: Truncate(Clean(Cell(34)), 200),
                Observaciones: null,
                IsActive: isActive);

            result.Add((rowIndex, data));
        }

        return result;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static string? NormalizeCode(string? raw)
    {
        var cleaned = Clean(raw);
        if (cleaned is null) return null;
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            || double.TryParse(cleaned, NumberStyles.Float, CultureInfo.GetCultureInfo("es-AR"), out n))
        {
            if (!double.IsFinite(n)) return cleaned;
            // Excel often stores CUIT/CP as scientific notation; keep digits without overflow.
            if (Math.Abs(n - Math.Truncate(n)) < 0.0000001 && Math.Abs(n) < 1e18)
                return Math.Truncate(n).ToString("0", CultureInfo.InvariantCulture);
            return n.ToString("0.################", CultureInfo.InvariantCulture);
        }
        return cleaned;
    }

    private static string? NormalizeCuit(string? raw)
    {
        var code = NormalizeCode(raw);
        if (code is null) return null;
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return digits.Length > 0 ? digits : code;
    }

    private static int ColumnIndex(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef)) return 0;
        var letters = new StringBuilder();
        foreach (var ch in cellRef)
        {
            if (char.IsLetter(ch)) letters.Append(char.ToUpperInvariant(ch));
            else break;
        }
        var col = 0;
        foreach (var ch in letters.ToString())
            col = col * 26 + (ch - 'A' + 1);
        return col;
    }

    private static string GetCellText(SharedStringTable? sst, Cell cell)
    {
        if (cell.CellValue is null) return string.Empty;
        var raw = cell.CellValue.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString
            && sst is not null
            && int.TryParse(raw, out var idx))
        {
            return sst.ElementAt(idx).InnerText ?? string.Empty;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
            return raw == "1" ? "TRUE" : "FALSE";

        return raw;
    }
}

public sealed record PrestadorPageDto(
    IReadOnlyList<PrestadorDto> Items,
    int Page,
    int PageSize,
    int Total,
    int TotalPages,
    IReadOnlyList<string> Provincias);

public sealed record PrestadorDto(
    Guid Id,
    string Provincia,
    string Localidad,
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
    string? Observaciones,
    bool RequierePagoAnticipado,
    decimal? ValorConsulta,
    bool TieneFirma,
    string? FirmaFileName,
    DateTime? FirmaUploadedAtUtc,
    bool IsActive,
    int? ExcelRowNumber,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record PrestadorOptionDto(
    Guid Id,
    string Nombre,
    string Provincia,
    string Localidad,
    string? Especialidad,
    string? Servicio,
    string? Telefonos,
    string? MailContacto,
    bool RequierePagoAnticipado,
    decimal? ValorConsulta,
    string? ValoresAcordados,
    string? FormaPago,
    bool TieneFirma);

public sealed record UpsertPrestadorRequest(
    string Nombre,
    string? Provincia,
    string? Localidad,
    string? Cuit,
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
    string? Observaciones,
    bool IsActive = true,
    bool? RequierePagoAnticipado = null,
    decimal? ValorConsulta = null);

public sealed record PrestadorImportResult(
    int TotalRows,
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Errors);
