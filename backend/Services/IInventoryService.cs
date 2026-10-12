using WmsDistributionCenter.Api.DTOs.Inventory;

namespace WmsDistributionCenter.Api.Services;

public interface IInventoryService
{
    Task<IEnumerable<InventoryResponseDto>> QueryAsync(InventoryQueryDto query);
    Task<InventoryResponseDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AdjustmentResponseDto>> GetAdjustmentsAsync();
    Task<AdjustmentResponseDto> CreateAdjustmentAsync(CreateAdjustmentDto dto, Guid userId);
    Task<AdjustmentResponseDto?> ApproveAdjustmentAsync(Guid adjId, Guid userId);
}
