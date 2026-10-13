using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Services;

public interface IOutboundService
{
    Task<IEnumerable<OutboundOrderResponseDto>> GetAllAsync();
    Task<OutboundOrderResponseDto?> GetByIdAsync(Guid id);
    Task<OutboundOrderResponseDto> CreateAsync(CreateOutboundOrderDto dto, Guid createdBy);
    Task<OutboundOrderResponseDto?> UpdateAsync(Guid id, UpdateOutboundOrderDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<OrderItemResponseDto> AddItemAsync(Guid orderId, AddOrderItemDto dto);
    Task<IEnumerable<OrderItemResponseDto>> GetOrderItemsAsync(Guid orderId);

    /// <summary>Allocates stock (first-expiry-first-out) and creates the pick tasks; Draft -> Picking.</summary>
    Task<OutboundOrderResponseDto?> StartPickingAsync(Guid orderId);
    Task<OutboundOrderResponseDto?> AssignPickerAsync(Guid orderId, AssignPickerDto dto);
    Task<IEnumerable<PickTaskResponseDto>> GetPickTasksAsync(PickTaskStatus? status, Guid? orderId, Guid? pickerId);
    /// <summary>Confirms a pick task; Picking -> Picked once every task is done.</summary>
    Task<PickTaskResponseDto?> ConfirmPickAsync(Guid taskId, ConfirmPickDto dto, Guid userId);
    Task<OutboundOrderResponseDto?> PackOrderAsync(Guid orderId, PackOrderDto dto, Guid userId);
    Task<ShipmentResponseDto> ShipOrderAsync(Guid orderId, CreateShipmentDto dto, Guid userId);
    Task<OutboundOrderResponseDto?> CancelAsync(Guid orderId);

    Task<IEnumerable<ShipmentResponseDto>> GetShipmentsAsync();
    Task<ShipmentResponseDto?> MarkDeliveredAsync(Guid shipmentId);
}
