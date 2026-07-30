using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StajBackendProject.Interfaces;
using StajBackendProject.Models;
using StajBackendProject.Models.Dto;

namespace StajBackendProject.Implements
{
    public class UserService : IUserService
    {
        private readonly UsersContext _context;
        private readonly IPasswordHasher _hasher;
        private readonly ITokenService _tokenService;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        public UserService(UsersContext context, IPasswordHasher hasher, ITokenService tokenService, IMapper mapper, IEmailService emailService)
        {
            _context = context;
            _hasher = hasher;
            _tokenService = tokenService;
            _mapper = mapper;
            _emailService = emailService;
        }

        public async Task<List<UserResponseDto>> GetAllUsersAsync()
        {
            var rawData = await _context.Database
                .SqlQuery<UserRoleSpResponseDto>($"EXEC sp_GetAllUsersWithRoles")
                .ToListAsync();

            var groupedUsers = rawData
                .GroupBy(u => u.Id)
                .Select(group => new UserResponseDto
                {
                    Id = group.Key,
                    FirstName = group.First().FirstName,
                    LastName = group.First().LastName,
                    Email = group.First().Email,
                    Roles = group.Select(x => x.RoleName).ToList()
                })
                .ToList();

            return groupedUsers;
        }
        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.Id == id && u.IsActive == true);
        }
        public async Task AddNewUserAsync(AddNewUserDto dto)
        {
            string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            bool isUserExist = await _context.Users.AnyAsync(u => u.Email == normalizedEmail);

            if (isUserExist)
            {
                throw new InvalidOperationException("This email or phone number is already registered.");
            }

            var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");

            string verificationToken = _tokenService.GenerateEmailVerificationToken();

            var newUser = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = normalizedEmail,
                IsActive = false,
                IsEmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
                InsertDate = DateTime.UtcNow,
                PasswordHash = _hasher.Hash(dto.Password),
                Roles = new List<Role>()
            };

            if (defaultRole != null)
            {
                newUser.Roles.Add(defaultRole);
            }

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            string verificationUrl = $"https://localhost:7123/api/auth/verify-email?token={verificationToken}";

            string emailSubject = "Backend Project - Verify Your Account 🔒";
            string emailBody = $@"
                <h3>Hello {dto.FirstName},</h3>
                <p>Welcome to our community! To activate your account, please click the link below:</p>
                <p><a href='{verificationUrl}'>Activate my account</a></p>
                <br>
                <p><small>This link is valid for 24 hours. If the button doesn't work, paste this link into your browser:<br>{verificationUrl}</small></p>";

            _emailService.SendEmail(dto.Email, emailSubject, emailBody);
        }
        public bool DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return false;
            }
            _context.Users.Remove(user);
            _context.SaveChanges();
            return true;
        }
        public bool SoftDeleteUserById(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return false;
            }
            if (user.IsActive)
            {
                user.IsActive = false;
                user.DeletedAt = DateTime.UtcNow;
                _context.SaveChanges();
            }
            return true;
        }
        public bool ActivateUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return false;
            }
            if (!user.IsActive)
            {
                user.IsActive = true;
                user.DeletedAt = null;
                _context.SaveChanges();
            }
            return true;
        }
        public async Task<LoginResultDto> LoginAsync(string email, string password, string? ipAddress = null, string? deviceName = null)
        {
            email = email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.Email == email && u.IsActive == true);

            if (user == null || !user.IsActive)
            {
                return new LoginResultDto
                {
                    Success = false,
                    Message = "Email or password is incorrect."
                };
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd <= DateTime.UtcNow)
            {
                user.LockoutEnd = null;
                user.FailedLoginAttempts = 0;
                await _context.SaveChangesAsync();
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                return new LoginResultDto
                {
                    Success = false,
                    Message = "Account is temporarily locked.",
                    LockoutEnd = user.LockoutEnd
                };
            }

            if (!_hasher.Verify(password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    user.FailedLoginAttempts = 0;
                }

                await _context.SaveChangesAsync();

                return new LoginResultDto
                {
                    Success = false,
                    Message = user.LockoutEnd.HasValue
                        ? "Account has been locked for 15 minutes."
                        : "Email or password is incorrect.",
                    LockoutEnd = user.LockoutEnd
                };
            }

            string token = _tokenService.GenerateJwtToken(user);
            string refreshToken = _tokenService.GenerateRefreshToken();

            var newSession = new UserSession
            {
                UserId = user.Id,
                RefreshToken = refreshToken,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                IpAddress = ipAddress,
                DeviceName = deviceName ?? "Unknown Device",
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserSessions.Add(newSession);

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.LastLoginDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new LoginResultDto
            {
                Success = true,
                Message = "Login successful.",
                AccessToken = token,
                RefreshToken = refreshToken
            };
        }
        public List<User> GetAllUsersOrderByDate() 
        {
            return _context.Users.Where(u => u.IsActive && u.DeletedAt == null).OrderByDescending(u => u.InsertDate).ToList();
        }
        public bool AssignRoleToUser(int userId, int roleId)
        {
            var user = _context.Users.Include(u => u.Roles).FirstOrDefault(u => u.Id == userId);
            if (user == null) throw new Exception("User not found.");

            var role = _context.Roles.Find(roleId);
            if (role == null) throw new Exception("Role not found.");

            if (user.Roles.Any(r => r.Id == roleId))
            {
                throw new Exception("User already has this role.");
            }

            user.Roles.Add(role);
            _context.SaveChanges();

            return true;
        }

        public bool RemoveRoleFromUser(int userId, int roleId)
        {
            var user = _context.Users.Include(u => u.Roles).FirstOrDefault(u => u.Id == userId);
            if (user == null) throw new Exception("User not found.");

            var role = user.Roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null) throw new Exception("The user does not have this role.");

            user.Roles.Remove(role);
            _context.SaveChanges();

            return true;
        }
        public bool ChangePassword(int userId, ChangePasswordDto request)
        {
            var user = _context.Users.Find(userId);
            if (user == null) throw new Exception("User not found.");

            bool isOldPasswordCorrect = _hasher.Verify(request.OldPassword, user.PasswordHash);

            if (!isOldPasswordCorrect)
            {
                throw new Exception("The old password you entered is incorrect.");
            }

            user.PasswordHash = _hasher.Hash(request.NewPassword);

            user.UpdatedAt = DateTime.UtcNow;
            user.PasswordLastChangedAt = DateTime.UtcNow;

            _context.SaveChanges();

            return true;
        }
        public bool ChangeEmail(int userId, ChangeEmailDto request)
        {
            var user = _context.Users.Find(userId);
            if (user == null) throw new Exception("User not found.");

            bool isPasswordCorrect = _hasher.Verify(request.Password, user.PasswordHash);
            if (!isPasswordCorrect)
            {
                throw new Exception("Incorrect password. Email update failed.");
            }

            string normalizedNewEmail = request.NewEmail.Trim().ToLowerInvariant();

            bool isEmailTaken = _context.Users.Any(u => u.Email == normalizedNewEmail && u.Id != userId);
            if (isEmailTaken)
            {
                throw new Exception("This email address is already in use by another account.");
            }

            user.Email = normalizedNewEmail;

            _context.SaveChanges();

            return true;
        }
        public async Task<List<UserRoleSpResponseDto>> GetUsersByRoleNameSpAsync(string roleName)
        {
            var users = await _context.Database
                .SqlQuery<UserRoleSpResponseDto>($"EXEC sp_GetUsersByRoleName {roleName}")
                .ToListAsync();

            return users;
        }

        public async Task<List<UserSession>> GetActiveSessionsAsync(int userId)
        {
            return await _context.UserSessions
                .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiryDate > DateTime.UtcNow)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> RevokeSessionAsync(int userId, int sessionId)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null || session.IsRevoked) return false;

            session.IsRevoked = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LogoutSessionAsync(string refreshToken)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

            if (session == null) return false;

            session.IsRevoked = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserSession?> GetCurrentSessionAsync(string refreshToken)
        {
            return await _context.UserSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && !s.IsRevoked && s.ExpiryDate > DateTime.UtcNow);
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            var user = await _context.Users
                .SingleOrDefaultAsync(u => u.EmailVerificationToken == token);

            if (user == null || user.EmailVerificationTokenExpiry <= DateTime.UtcNow)
            {
                return false;
            }

            user.IsActive = true;
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            string normalizedEmail = email.Trim().ToLowerInvariant();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive == true);

            if (user == null)
            {
                return false;
            }

            string resetToken = _tokenService.GeneratePasswordResetToken();
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            string resetUrl = $"https://localhost:7123/api/auth/reset-password?token={resetToken}";

            string emailSubject = "Backend Project - Password Reset Request 🔑";
            string emailBody = $@"
                <h3>Hello {user.FirstName},</h3>
                <p>A password reset request has been submitted for your account. You can click the link below to reset your password:</p>
                <p><a href='{resetUrl}'>Reset password</a></p>
                <br>
                <p><b>Not:</b> This link is only valid for <b>15 minutes</b>. If you did not make this request, you can ignore this email.</p>";

            _emailService.SendEmail(user.Email, emailSubject, emailBody);

            return true;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordDto request)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u =>
                u.PasswordResetToken == request.Token &&
                u.PasswordResetTokenExpiry > DateTime.UtcNow &&
                u.IsActive == true);

            if (user == null)
            {
                return false;
            }

            user.PasswordHash = _hasher.Hash(request.NewPassword);

            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;
            user.PasswordLastChangedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}
