using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.Return;

public record CreateReturnDto(
    [Required] Guid WarehouseId,
    [Required] Guid ProductId,
    [Range(0.0001, 1000000)] decimal Qty,
    ReturnCondition Condition,
    /// <summary>Optional shipped order the goods came from. When set, the quantity is validated against what was shipped.</summary>
    Guid? OrderId,
    string? Reason,
    string? Notes
);

public record ProcessReturnDto(
    Disposition Disposition,
    /// <summary>Required when the disposition is Restock.</summary>
    Guid? RestockLocationId,
    string? Notes
);

public record ReturnResponseDto(
    Guid Id,
    string ReturnNo,
    Guid? OrderId,
    string? OrderNumber,
    Guid WarehouseId,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Qty,
    string? Reason,
    ReturnCondition Condition,
    Disposition? Disposition,
    Guid? RestockLocationId,
    string? RestockLocationCode,
    ReturnStatus Status,
    string? ReceiverName,
    DateTime? ReceivedAt,
    DateTime? ProcessedAt,
    string? Notes
);
