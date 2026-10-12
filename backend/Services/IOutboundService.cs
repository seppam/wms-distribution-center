using WmsDistributionCenter.Api.DTOs.OutboundOrder;

namespace WmsDistributionCenter.Api.Services;

public interface IOutboundService
{
    Task<IEnumerable<OutboundOrderResponseDto>> GetAllAsync();
    Task<OutboundOrderResponseDto?> GetByIdAsync(Guid id);
    Task<OutboundOrderResponseDto> CreateAsync(CreateOutboundOrderDto dto, Guid createdBy);
    Task<OutboundOrderResponseDto?> UpdateAsync(Guid id, UpdateOutboundOrderDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<OrderItemResponseDto> AddItemAsync(Guid orderId, AddOrderItemDto dto);
    Task<OutboundOrderResponseDto?> StartPickingAsync(Guid orderId);
    Task<OutboundOrderResponseDto?> AssignPickerAsync(Guid orderId, AssignPickerDto dto);
    Task<OutboundOrderResponseDto?> PackOrderAsync(Guid orderId, PackOrderDto dto, Guid userId);
    Task<ShipmentResponseDto> ShipOrderAsync(Guid orderId, CreateShipmentDto dto, Guid userId);
    Task<IEnumerable<OrderItemResponseDto>> GetOrderItemsAsync(Guid orderId);
}
