using AuthCore.Models;
using AuthCore.Models.Dto;

namespace AuthCore.Interfaces
{
    public interface ITokenService
    {
        string GenerateJwtToken(User user);
        string GenerateRefreshToken();
        TokenResponseDto RefreshToken(RefreshTokenRequestDto request);
        string GenerateEmailVerificationToken();
        string GeneratePasswordResetToken();
        string GenerateEmailChangeToken();
    }
}
