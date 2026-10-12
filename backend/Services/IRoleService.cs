using WmsDistributionCenter.Api.DTOs.Role;

namespace WmsDistributionCenter.Api.Services;

public interface IRoleService
{
    Task<IEnumerable<RoleResponseDto>> GetAllAsync();
    Task<RoleResponseDto?> GetByIdAsync(Guid id);
    Task<RoleResponseDto> CreateAsync(CreateRoleDto dto);
    Task<RoleResponseDto?> UpdateAsync(Guid id, UpdateRoleDto dto);
    Task<bool> DeleteAsync(Guid id);
}
