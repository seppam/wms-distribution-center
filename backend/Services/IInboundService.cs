using WmsDistributionCenter.Api.DTOs.InboundReceipt;

namespace WmsDistributionCenter.Api.Services;

public interface IInboundService
{
    Task<IEnumerable<InboundReceiptResponseDto>> GetAllAsync();
    Task<InboundReceiptResponseDto?> GetByIdAsync(Guid id);
    Task<InboundReceiptResponseDto> CreateAsync(CreateInboundReceiptDto dto);
    Task<InboundReceiptResponseDto?> UpdateAsync(Guid id, UpdateInboundReceiptDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<ReceiptItemResponseDto> AddItemAsync(Guid receiptId, AddReceiptItemDto dto);
    Task<ReceiptItemResponseDto?> ReceiveItemAsync(Guid receiptId, ReceiveItemDto dto, Guid userId);
    Task<InboundReceiptResponseDto?> CompleteReceiptAsync(Guid receiptId, Guid userId);
}
