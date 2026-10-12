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
    string? Notes
);

public record UpdateInboundReceiptDto(
    string? PoNumber,
    string? AsnNumber,
    DateOnly? ExpectedDate,
    string? DockDoor,
    string? Notes,
    InboundStatus? Status
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
    DateTime CreatedAt
);

public record AddReceiptItemDto(
    [Required] Guid ProductId,
    [Required] decimal ExpectedQty,
    decimal UnitCost,
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
    [Required] decimal ReceivedQty,
    decimal DamagedQty,
    Guid? PutawayLocationId,
    string? LotNumber,
    DateOnly? ExpiryDate
);
