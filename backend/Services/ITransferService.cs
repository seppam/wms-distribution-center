using WmsDistributionCenter.Api.DTOs.Transfer;

namespace WmsDistributionCenter.Api.Services;

public interface ITransferService
{
    Task<IEnumerable<TransferResponseDto>> GetAllAsync();
    Task<TransferResponseDto?> GetByIdAsync(Guid id);
    Task<TransferResponseDto> CreateAsync(CreateTransferDto dto, Guid userId);
    /// <summary>Moves the stock between the two locations and writes the ledger entries.</summary>
    Task<TransferResponseDto?> CompleteAsync(Guid id, Guid userId);
    Task<TransferResponseDto?> CancelAsync(Guid id);
}
