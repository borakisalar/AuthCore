using AuthCore.Models.Dto;

namespace AuthCore.Interfaces
{
    public interface IMfaService
    {
        Task<bool> SendMfaSetupEmailAsync(int userId);
        Task<MfaSetupResponseDto> SetupMfaAsync(int userId, MfaSetupDto request);
        Task<List<string>> VerifyMfaSetupAsync(int userId, MfaVerifyDto request);
        Task<bool> DisableMfaAsync(int userId, MfaVerifyDto request);
        Task<bool> VerifyRecoveryCodeAsync(int userId, MfaRecoveryCodeDto request);
    }
}
