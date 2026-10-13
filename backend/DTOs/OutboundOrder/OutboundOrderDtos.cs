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
    string? Notes,
    /// <summary>Optional order lines, created together with the order in one transaction.</summary>
    IReadOnlyList<AddOrderItemDto>? Items = null
);

public record UpdateOutboundOrderDto(
    Priority? Priority,
    string? Destination,
    DateOnly? DueDate,
    string? Notes
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
    DateTime CreatedAt,
    /// <summary>Only populated by the detail endpoint.</summary>
    IReadOnlyList<OrderItemResponseDto>? Items = null,
    IReadOnlyList<PackListResponseDto>? PackLists = null,
    IReadOnlyList<ShipmentResponseDto>? Shipments = null
);

public record AddOrderItemDto(
    [Required] Guid ProductId,
    [Range(0.0001, 1000000)] decimal OrderedQty,
    [Range(0, 100000000)] decimal UnitPrice
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

public record ConfirmPickDto(
    /// <summary>Quantity actually picked: 0 to the task quantity. Less than the task quantity is a short pick.</summary>
    [Range(0, 1000000)] decimal QtyPicked
);

public record PickTaskResponseDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    Guid OrderItemId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid FromLocationId,
    string FromLocationCode,
    decimal QtyToPick,
    decimal QtyPicked,
    PickTaskStatus Status,
    Guid? PickerId,
    string? PickerName,
    DateTime? AssignedAt,
    DateTime? PickedAt
);

public record PackOrderDto(
    [Required][StringLength(100)] string CartonNo,
    [Range(0, 100000)] decimal WeightKg
);

public record PackListResponseDto(
    Guid Id,
    string CartonNo,
    decimal WeightKg,
    DateTime? PackedAt,
    string PackedByName,
    PackListStatus Status
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
