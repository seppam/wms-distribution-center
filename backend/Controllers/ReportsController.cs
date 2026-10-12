using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var dashboard = await _reportService.GetDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("inventory-summary")]
    public async Task<IActionResult> InventorySummary()
    {
        var summary = await _reportService.GetInventorySummaryAsync();
        return Ok(summary);
    }

    [HttpGet("inbound-summary")]
    public async Task<IActionResult> InboundSummary()
    {
        var summary = await _reportService.GetInboundSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("outbound-summary")]
    public async Task<IActionResult> OutboundSummary()
    {
        var summary = await _reportService.GetOutboundSummaryAsync();
        return Ok(summary);
    }
}
