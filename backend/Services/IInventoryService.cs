using WmsDistributionCenter.Api.DTOs.Inventory;

namespace WmsDistributionCenter.Api.Services;

public interface IInventoryService
{
    Task<IEnumerable<InventoryResponseDto>> QueryAsync(InventoryQueryDto query);
    Task<InventoryResponseDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<InventoryTransactionResponseDto>> GetTransactionsAsync(Guid? productId, Guid? locationId, int take);
    Task<IEnumerable<AdjustmentResponseDto>> GetAdjustmentsAsync();
    Task<AdjustmentResponseDto> CreateAdjustmentAsync(CreateAdjustmentDto dto, Guid userId);
    /// <summary>Applies the adjustment to stock. Non-admins cannot approve adjustments they created themselves.</summary>
    Task<AdjustmentResponseDto?> ApproveAdjustmentAsync(Guid adjId, Guid userId, bool isAdmin = true);
    Task<AdjustmentResponseDto?> RejectAdjustmentAsync(Guid adjId, Guid userId);
}
