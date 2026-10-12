using WmsDistributionCenter.Api.DTOs.Report;

namespace WmsDistributionCenter.Api.Services;

public interface IReportService
{
    Task<DashboardDto> GetDashboardAsync();
    Task<IEnumerable<InventorySummaryDto>> GetInventorySummaryAsync();
    Task<InboundSummaryDto> GetInboundSummaryAsync();
    Task<OutboundSummaryDto> GetOutboundSummaryAsync();
}
