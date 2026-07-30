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
        Task<LoginResultDto> LoginAsync(string email, string password, string? ipAddress = null, string? deviceName = null);
        List<User> GetAllUsersOrderByDate();
        bool AssignRoleToUser(int userId, int roleId);
        bool RemoveRoleFromUser(int userId, int roleId);
        bool ChangePassword(int userId, ChangePasswordDto request);
        bool ChangeEmail(int userId, ChangeEmailDto request);
        Task<List<UserRoleSpResponseDto>> GetUsersByRoleNameSpAsync(string roleName);
        Task<List<UserSession>> GetActiveSessionsAsync(int userId);
        Task<bool> RevokeSessionAsync(int userId, int sessionId);
        Task<bool> LogoutSessionAsync(string refreshToken);
        Task<UserSession?> GetCurrentSessionAsync(string refreshToken);
        Task<bool> VerifyEmailAsync(string token);
        Task<bool> ForgotPasswordAsync(string email);
        Task<bool> ResetPasswordAsync(ResetPasswordDto request);
        Task<bool> EmailChangeAsync(int userId, ChangeEmailDto request);
        Task<bool> VerifyEmailChangeAsync(string token);
    }
}
