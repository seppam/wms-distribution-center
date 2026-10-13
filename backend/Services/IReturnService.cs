using WmsDistributionCenter.Api.DTOs.Return;

namespace WmsDistributionCenter.Api.Services;

public interface IReturnService
{
    Task<IEnumerable<ReturnResponseDto>> GetAllAsync();
    Task<ReturnResponseDto?> GetByIdAsync(Guid id);
    Task<ReturnResponseDto> CreateAsync(CreateReturnDto dto, Guid userId);
    /// <summary>Decides the disposition; Restock puts good items back into stock.</summary>
    Task<ReturnResponseDto?> ProcessAsync(Guid id, ProcessReturnDto dto, Guid userId);
}
