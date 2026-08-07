using AuthCore.Models.Dto;

namespace AuthCore.Interfaces
{
    public interface IRoleService
    {
        List<RoleResponseDto> GetAllRoles();
    }
}