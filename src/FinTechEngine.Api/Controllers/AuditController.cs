using FinTechEngine.Api.Infrastructure.Services;
using FinTechEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinTechEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("reconcile")]
    public async Task<ActionResult<LedgerAuditReportDto>> Reconcile()
    {
        var report = await _auditService.ReconcileLedgerAsync();
        return Ok(report);
    }
}
