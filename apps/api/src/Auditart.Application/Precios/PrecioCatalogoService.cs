using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Precios;

public sealed class PrecioCatalogoService
{
    private readonly IAppDbContext _db;

    public PrecioCatalogoService(IAppDbContext db) => _db = db;

    public async Task<PrecioCatalogoListDto> ListAsync(
        TipoProfesional? tipo,
        string? art,
        string? q,
        bool? soloActivos,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var query = _db.PreciosCatalogo.AsNoTracking().AsQueryable();
        if (tipo.HasValue) query = query.Where(x => x.TipoProfesional == tipo);
        if (soloActivos == true) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(art))
        {
            var artNorm = art.Trim().ToLower();
            query = query.Where(x => x.ArtNombre != null && x.ArtNombre.ToLower().Contains(artNorm));
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x =>
                x.Concepto.ToLower().Contains(term)
                || (x.ArtNombre != null && x.ArtNombre.ToLower().Contains(term))
                || (x.Notas != null && x.Notas.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.TipoProfesional)
            .ThenBy(x => x.ArtNombre)
            .ThenBy(x => x.Concepto)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => Map(x))
            .ToListAsync(ct);

        var arts = await _db.PreciosCatalogo.AsNoTracking()
            .Where(x => x.ArtNombre != null && x.ArtNombre != "")
            .Select(x => x.ArtNombre!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

        return new PrecioCatalogoListDto(
            items,
            page,
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize),
            arts);
    }

    public async Task<IReadOnlyList<PrecioOptionDto>> OptionsAsync(
        TipoProfesional tipo,
        string? art,
        string? q,
        CancellationToken ct)
    {
        var query = _db.PreciosCatalogo.AsNoTracking()
            .Where(x => x.IsActive && x.TipoProfesional == tipo);

        if (tipo == TipoProfesional.Auditor)
        {
            if (string.IsNullOrWhiteSpace(art))
                return Array.Empty<PrecioOptionDto>();

            var artNorm = art.Trim().ToLower();
            query = query.Where(x =>
                x.ArtNombre != null &&
                (x.ArtNombre.ToLower() == artNorm || x.ArtNombre.ToLower().Contains(artNorm)));
        }
        else
        {
            query = query.Where(x => x.ArtNombre == null);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.Concepto.ToLower().Contains(term));
        }

        return await query
            .OrderBy(x => x.Concepto)
            .Take(80)
            .Select(x => new PrecioOptionDto(x.Id, x.Concepto, x.Valor, x.ArtNombre, x.TipoProfesional))
            .ToListAsync(ct);
    }

    public async Task<PrecioCatalogoDto> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await _db.PreciosCatalogo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
        return Map(item);
    }

    public async Task<PrecioCatalogoDto> CreateAsync(UpsertPrecioRequest request, CancellationToken ct)
    {
        var entity = PrecioCatalogo.Create(
            request.TipoProfesional,
            request.Concepto,
            request.Valor,
            request.ArtNombre,
            request.Notas);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<PrecioCatalogoDto> UpdateAsync(Guid id, UpsertPrecioRequest request, CancellationToken ct)
    {
        var entity = await _db.PreciosCatalogo.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
        entity.Update(
            request.TipoProfesional,
            request.Concepto,
            request.Valor,
            request.ArtNombre,
            request.Notas,
            request.IsActive);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var entity = await _db.PreciosCatalogo.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
        entity.SetActive(active);
        await _db.SaveChangesAsync(ct);
    }

    private static PrecioCatalogoDto Map(PrecioCatalogo x) => new(
        x.Id,
        x.TipoProfesional,
        x.ArtNombre,
        x.Concepto,
        x.Valor,
        x.IsActive,
        x.Notas,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);
}

public sealed record PrecioCatalogoDto(
    Guid Id,
    TipoProfesional TipoProfesional,
    string? ArtNombre,
    string Concepto,
    decimal Valor,
    bool IsActive,
    string? Notas,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record PrecioCatalogoListDto(
    IReadOnlyList<PrecioCatalogoDto> Items,
    int Page,
    int PageSize,
    int Total,
    int TotalPages,
    IReadOnlyList<string> Arts);

public sealed record PrecioOptionDto(
    Guid Id,
    string Concepto,
    decimal Valor,
    string? ArtNombre,
    TipoProfesional TipoProfesional);

public sealed record UpsertPrecioRequest(
    TipoProfesional TipoProfesional,
    string Concepto,
    decimal Valor,
    string? ArtNombre,
    string? Notas,
    bool? IsActive = null);
