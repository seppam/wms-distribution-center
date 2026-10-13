using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.InboundReceipt;

public record CreateInboundReceiptDto(
    [Required] Guid SupplierId,
    [Required] Guid WarehouseId,
    string? PoNumber,
    string? AsnNumber,
    DateOnly? ExpectedDate,
    string? DockDoor,
    string? Notes,
    /// <summary>Optional expected lines, created together with the receipt in one transaction.</summary>
    IReadOnlyList<AddReceiptItemDto>? Items = null
);

public record UpdateInboundReceiptDto(
    string? PoNumber,
    string? AsnNumber,
    DateOnly? ExpectedDate,
    string? DockDoor,
    string? Notes
);

public record InboundReceiptResponseDto(
    Guid Id,
    string ReceiptNo,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    string? PoNumber,
    string? AsnNumber,
    InboundStatus Status,
    DateOnly? ExpectedDate,
    DateTime? ReceivedDate,
    Guid? ReceivedBy,
    string? ReceiverName,
    string? DockDoor,
    string? Notes,
    DateTime CreatedAt,
    /// <summary>Only populated by the detail endpoint.</summary>
    IReadOnlyList<ReceiptItemResponseDto>? Items = null
);

public record AddReceiptItemDto(
    [Required] Guid ProductId,
    [Range(0.0001, 1000000)] decimal ExpectedQty,
    [Range(0, 100000000)] decimal UnitCost,
    string? LotNumber,
    DateOnly? ExpiryDate,
    Guid? PutawayLocationId
);

public record ReceiptItemResponseDto(
    Guid Id,
    Guid ReceiptId,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal ExpectedQty,
    decimal ReceivedQty,
    decimal DamagedQty,
    string? LotNumber,
    DateOnly? ExpiryDate,
    decimal UnitCost,
    ReceiptItemStatus Status,
    Guid? PutawayLocationId,
    string? PutawayLocationCode
);

public record ReceiveItemDto(
    [Required] Guid ReceiptItemId,
    [Range(0, 1000000)] decimal ReceivedQty,
    [Range(0, 1000000)] decimal DamagedQty,
    /// <summary>Put-away location: received good quantity is stocked here in the same transaction.</summary>
    Guid? PutawayLocationId,
    string? LotNumber,
    DateOnly? ExpiryDate
);
