using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.OutboundOrder;

public record CreateOutboundOrderDto(
    [Required] Guid WarehouseId,
    OrderType OrderType,
    Priority Priority,
    string? Destination,
    DateOnly? RequestedDate,
    DateOnly? DueDate,
    string? Notes
);

public record UpdateOutboundOrderDto(
    Priority? Priority,
    string? Destination,
    DateOnly? DueDate,
    string? Notes,
    OrderStatus? Status
);

public record OutboundOrderResponseDto(
    Guid Id,
    string OrderNumber,
    Guid WarehouseId,
    string WarehouseName,
    OrderType OrderType,
    OrderStatus Status,
    Priority Priority,
    string? Destination,
    DateOnly? RequestedDate,
    DateOnly? DueDate,
    Guid CreatedBy,
    string CreatorName,
    string? Notes,
    DateTime CreatedAt
);

public record AddOrderItemDto(
    [Required] Guid ProductId,
    [Required] decimal OrderedQty,
    decimal UnitPrice
);

public record OrderItemResponseDto(
    Guid Id,
    Guid OrderId,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal OrderedQty,
    decimal PickedQty,
    decimal ShippedQty,
    decimal UnitPrice,
    OrderItemStatus Status
);

public record AssignPickerDto(
    [Required] Guid PickerId
);

public record PickItemDto(
    [Required] Guid PickTaskId,
    [Required] decimal QtyPicked
);

public record PackOrderDto(
    [Required] string CartonNo,
    [Required] decimal WeightKg
);

public record CreateShipmentDto(
    [Required] Guid CarrierId,
    string? TrackingNo,
    string? BolNumber,
    string? VehiclePlate,
    string? DriverName
);

public record ShipmentResponseDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    Guid CarrierId,
    string CarrierName,
    string? TrackingNo,
    string? BolNumber,
    string? VehiclePlate,
    string? DriverName,
    ShipmentStatus Status,
    DateTime? ShippedAt,
    DateTime? DeliveredAt
);
