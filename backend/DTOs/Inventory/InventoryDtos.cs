using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.Inventory;

public record InventoryResponseDto(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid LocationId,
    string LocationCode,
    string ZoneName,
    decimal QtyOnHand,
    decimal QtyReserved,
    string? LotNumber,
    DateOnly? ExpiryDate,
    decimal UnitCost,
    InventoryStatus Status,
    DateOnly? LastCountedDate
);

public record InventoryQueryDto(
    Guid? ProductId,
    Guid? LocationId,
    Guid? WarehouseId,
    InventoryStatus? Status,
    int Page = 1,
    int PageSize = 20
);

public record CreateAdjustmentDto(
    [Required] Guid ProductId,
    [Required] Guid LocationId,
    AdjType AdjType,
    decimal AdjQty,
    string? Reason
);

public record AdjustmentResponseDto(
    Guid Id,
    string AdjNumber,
    Guid ProductId,
    string ProductName,
    Guid LocationId,
    string LocationCode,
    AdjType AdjType,
    decimal QtyBefore,
    decimal AdjQty,
    decimal QtyAfter,
    string? Reason,
    Guid CreatedBy,
    string CreatorName,
    Guid? ApprovedBy,
    string? ApproverName,
    AdjStatus Status,
    DateTime CreatedAt
);
