using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Application.Prestadores;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/prestadores")]
public class PrestadoresController : ControllerBase
{
    private readonly PrestadorService _service;
    private readonly IObjectStorage _storage;

    public PrestadoresController(PrestadorService service, IObjectStorage storage)
    {
        _service = service;
        _storage = storage;
    }

    /// <summary>Listado paginado (Admin / Jefatura).</summary>
    [HttpGet]
    public async Task<ActionResult<PrestadorPageDto>> List(
        [FromQuery] string? q,
        [FromQuery] string? provincia,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        return Ok(await _service.ListAsync(q, provincia, isActive, page, pageSize, ct));
    }

    /// <summary>Opciones activas para operadores al coordinar turno.</summary>
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<PrestadorOptionDto>>> Active(
        [FromQuery] string? q,
        [FromQuery] string? provincia,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        if (!PermissionService.CanOperateBoard(GetRole())) return Forbid();
        return Ok(await _service.ListActiveOptionsAsync(q, provincia, take, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrestadorDto>> Get(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()) && !PermissionService.CanOperateBoard(GetRole()))
            return Forbid();
        try
        {
            return Ok(await _service.GetAsync(id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<ActionResult<PrestadorDto>> Create(
        [FromBody] UpsertPrestadorRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        try
        {
            return Ok(await _service.CreateAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PrestadorDto>> Update(
        Guid id,
        [FromBody] UpsertPrestadorRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        try
        {
            return Ok(await _service.UpdateAsync(id, request, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        try
        {
            await _service.SetActiveAsync(id, true, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        try
        {
            await _service.SetActiveAsync(id, false, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("import")]
    [RequestSizeLimit(80_000_000)]
    public async Task<ActionResult<PrestadorImportResult>> Import(IFormFile file, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Archivo Excel requerido." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".xlsm"))
            return BadRequest(new { error = "Solo se aceptan archivos .xlsx." });

        await using var stream = file.OpenReadStream();
        try
        {
            var result = await _service.ImportExcelAsync(stream, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("recompute-valores")]
    public async Task<ActionResult<object>> RecomputeValores(CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        var updated = await _service.RecomputeValoresAsync(ct);
        return Ok(new { updated });
    }

    [HttpPost("{id:guid}/firma")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<PrestadorDto>> UploadFirma(Guid id, IFormFile file, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Imagen de firma requerida." });

        try
        {
            await using var stream = file.OpenReadStream();
            var dto = await _service.SetFirmaAsync(
                id,
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.Length,
                _storage,
                ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/firma")]
    public async Task<IActionResult> ClearFirma(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole())) return Forbid();
        try
        {
            await _service.ClearFirmaAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/firma")]
    public async Task<IActionResult> DownloadFirma(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()) && !PermissionService.CanOperateBoard(GetRole()))
            return Forbid();

        try
        {
            var firma = await _service.GetFirmaAsync(id, ct);
            if (firma is null)
                return NotFound(new { error = "Este prestador no tiene firma cargada." });

            var download = await _storage.DownloadAsync(firma.Value.Key, ct);
            return File(
                download.Content,
                download.ContentType ?? firma.Value.ContentType ?? "application/octet-stream",
                firma.Value.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }
}
