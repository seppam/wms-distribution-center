namespace WmsDistributionCenter.Api.DTOs.Report;

public record DashboardDto(
    int TotalProducts,
    int TotalLocations,
    int TotalInboundToday,
    int TotalOutboundToday,
    decimal TotalInventoryValue,
    int LowStockCount,
    int ExpiringSoonCount
);

public record InventorySummaryDto(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal TotalOnHand,
    decimal TotalReserved,
    decimal Available,
    int LocationCount
);

public record InboundSummaryDto(
    int Draft,
    int InProgress,
    int Received,
    int Partial,
    int Cancelled
);

public record OutboundSummaryDto(
    int Draft,
    int Picked,
    int Packed,
    int Shipped,
    int Completed,
    int Cancelled
);
