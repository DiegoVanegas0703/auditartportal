using System.Security.Claims;
using Auditart.Application.Auth;
using Auditart.Application.Reports;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reports;

    public ReportsController(ReportsService reports)
    {
        _reports = reports;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportsSummaryDto>> Summary(CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()))
            return Forbid();

        var summary = await _reports.GetSummaryAsync(ct);
        return Ok(summary);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()))
            return Forbid();

        var summary = await _reports.GetSummaryAsync(ct);
        var bytes = ReportsExcelExporter.Build(summary);
        var fileName = $"auditorias-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }
}
