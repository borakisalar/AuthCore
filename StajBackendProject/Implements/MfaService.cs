using Google.Authenticator;
using StajBackendProject.Interfaces;
using StajBackendProject.Models;
using StajBackendProject.Models.Dto;

namespace StajBackendProject.Implements
{
    public class MfaService : IMfaService
    {
        private readonly UsersContext _context;
        private readonly IEmailService _emailService;
        private readonly string _appName = "StajBackendProject";

        public MfaService(UsersContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<bool> SendMfaSetupEmailAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.MfaEnabled) return false;

            if (!user.IsEmailVerified)
                throw new Exception("MFA cannot be enabled for an unverified email address.");

            // Generate 6 digit random code
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            user.MfaSetupEmailCode = code;
            user.MfaSetupEmailCodeExpiry = DateTime.UtcNow.AddMinutes(5); // Valid for 5 minutes
            
            await _context.SaveChangesAsync();

            var message = $"Your 6-digit verification code to setup MFA is: {code}. This code will expire in 5 minutes.";
            _emailService.SendEmail(user.Email, "MFA Setup Verification Code", message);

            return true;
        }

        public async Task<MfaSetupResponseDto> SetupMfaAsync(int userId, MfaSetupDto request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.MfaEnabled) return null;

            if (user.MfaSetupEmailCode != request.EmailCode || user.MfaSetupEmailCodeExpiry < DateTime.UtcNow)
                throw new Exception("Invalid or expired email verification code.");

            // Clear the code so it can't be reused
            user.MfaSetupEmailCode = null;
            user.MfaSetupEmailCodeExpiry = null;

            var secretKey = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 20);
            user.MfaSecret = secretKey;
            await _context.SaveChangesAsync();

            var tfa = new TwoFactorAuthenticator();
            var setupInfo = tfa.GenerateSetupCode(_appName, user.Email, secretKey, false, 3);

            return new MfaSetupResponseDto
            {
                FormattedKey = setupInfo.ManualEntryKey,
                QrCodeSetupImageUrl = setupInfo.QrCodeSetupImageUrl
            };
        }

        public async Task<List<string>> VerifyMfaSetupAsync(int userId, MfaVerifyDto request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.MfaEnabled || string.IsNullOrEmpty(user.MfaSecret)) return null;

            var tfa = new TwoFactorAuthenticator();
            bool isValid = tfa.ValidateTwoFactorPIN(user.MfaSecret, request.Code);

            if (!isValid) return null;

            user.MfaEnabled = true;
            var recoveryCodes = GenerateRecoveryCodes(10);
            user.MfaRecoveryCodes = string.Join(",", recoveryCodes);
            await _context.SaveChangesAsync();

            return recoveryCodes;
        }

        public async Task<bool> DisableMfaAsync(int userId, MfaVerifyDto request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.MfaEnabled || string.IsNullOrEmpty(user.MfaSecret)) return false;

            var tfa = new TwoFactorAuthenticator();
            bool isValid = tfa.ValidateTwoFactorPIN(user.MfaSecret, request.Code);

            if (!isValid) return false;

            user.MfaEnabled = false;
            user.MfaSecret = null;
            user.MfaRecoveryCodes = null;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> VerifyRecoveryCodeAsync(int userId, MfaRecoveryCodeDto request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.MfaEnabled || string.IsNullOrEmpty(user.MfaRecoveryCodes)) return false;

            var codes = user.MfaRecoveryCodes.Split(',').ToList();
            if (codes.Contains(request.RecoveryCode))
            {
                codes.Remove(request.RecoveryCode);
                user.MfaRecoveryCodes = string.Join(",", codes);
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        private List<string> GenerateRecoveryCodes(int count)
        {
            var codes = new List<string>();
            for (int i = 0; i < count; i++)
            {
                codes.Add(Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10).ToUpper());
            }
            return codes;
        }
    }
}
