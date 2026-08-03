namespace StajBackendProject.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        public string? PendingNewEmail { get; set; }
        public string? EmailChangeToken { get; set; }
        public DateTime? EmailChangeTokenExpiry { get; set; }

        public ICollection<Role> Roles { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime InsertDate { get; set; }
        public DateTime? PasswordLastChangedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool MfaEnabled { get; set; }
        public string? MfaSecret { get; set; }
        public string? MfaRecoveryCodes { get; set; }
        public string? MfaSetupEmailCode { get; set; }
        public DateTime? MfaSetupEmailCodeExpiry { get; set; }
        public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    }
}