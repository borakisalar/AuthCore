using StajBackendProject.Models;
using StajBackendProject.Models.Dto;

namespace StajBackendProject.Interfaces
{
    public interface IUserService
    {
        Task<List<UserResponseDto>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int id);
        Task AddNewUserAsync(AddNewUserDto request);
        bool DeleteUser(int id);
        bool SoftDeleteUserById(int id);
        bool ActivateUser (int id);
        Task<LoginResultDto> LoginAsync(string email, string password);
        List<User> GetAllUsersOrderByDate();
        bool AssignRoleToUser(int userId, int roleId);
        bool RemoveRoleFromUser(int userId, int roleId);
        bool ChangePassword(int userId, ChangePasswordDto request);
        bool ChangeEmail(int userId, ChangeEmailDto request);
    }
}
