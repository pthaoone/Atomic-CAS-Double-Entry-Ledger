using FinTechEngine.Api.Infrastructure.Services;
using FinTechEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinTechEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsCollector _metrics;

    public MetricsController(IMetricsCollector metrics)
    {
        _metrics = metrics;
    }

    [HttpGet]
    public ActionResult<EngineMetricsDto> GetMetrics()
    {
        return Ok(_metrics.GetSnapshot());
    }

    [HttpPost("reset")]
    public IActionResult ResetMetrics()
    {
        _metrics.Reset();
        return Ok(new { message = "Metrics reset successfully." });
    }
}
