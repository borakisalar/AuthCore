using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StajBackendProject.Factories;
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
        public UserService(UsersContext context, IPasswordHasher hasher, ITokenService tokenService, IMapper mapper)
        {
            _context = context;
            _hasher = hasher;
            _tokenService = tokenService;
            _mapper = mapper;
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

            bool isUserExist = await _context.Users.AnyAsync(u => u.Email == normalizedEmail
            || (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && u.PhoneNumber == dto.PhoneNumber));

            if (isUserExist)
            {
                throw new InvalidOperationException("This email or phone number is already registered.");
            }

            var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");

            var newUser = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = normalizedEmail,
                IsActive = true,
                InsertDate = DateTime.UtcNow,
                PasswordHash = _hasher.Hash(dto.Password),
                PhoneNumber = dto.PhoneNumber,
                Roles = new List<Role>()
            };

            if (defaultRole != null)
            {
                newUser.Roles.Add(defaultRole);
            }

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            NotificationFactory notificationFactory = new NotificationFactory();

            INotificationService notifier = notificationFactory.CreateNotification("email");

            notifier.Send(dto.Email, $"Welcome {dto.FirstName}! Registration successfully completed.");
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
        public async Task<LoginResultDto> LoginAsync(string email, string password)
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

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.LastLoginDate = DateTime.UtcNow;

            string refreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            string token = _tokenService.GenerateJwtToken(user);

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
    }
}
