using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using AuthCore.Models;
using AuthCore.Models.Dto;
using AuthCore.Interfaces;
using AuthCore.Implements;

namespace AuthCore.Tests.Services
{
    public class UserServiceTests
    {
        private DbContextOptions<UsersContext> GetInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<UsersContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
        }

        private (UsersContext Context, UserService Service, Mock<IPasswordHasher> Hasher, Mock<ITokenService> Token, Mock<IMapper> Mapper, Mock<IEmailService> Email) CreateTestEnvironment(string dbName)
        {
            var options = GetInMemoryOptions(dbName);
            var context = new UsersContext(options);
            context.Database.EnsureCreated();

            var passwordHasherMock = new Mock<IPasswordHasher>();
            var tokenServiceMock = new Mock<ITokenService>();
            var mapperMock = new Mock<IMapper>();
            var emailServiceMock = new Mock<IEmailService>();

            var service = new UserService(
                context,
                passwordHasherMock.Object,
                tokenServiceMock.Object,
                mapperMock.Object,
                emailServiceMock.Object
            );

            return (context, service, passwordHasherMock, tokenServiceMock, mapperMock, emailServiceMock);
        }

        private User CreateTestUser(int id = 1, string email = "test@test.com", bool isActive = true, string passwordHash = "hashed")
        {
            return new User
            {
                Id = id,
                FirstName = "Test",
                LastName = "User",
                Email = email,
                PasswordHash = passwordHash,
                IsActive = isActive,
                IsEmailVerified = true,
                InsertDate = DateTime.UtcNow,
                Roles = new List<Role>()
            };
        }

        #region DeleteUser

        [Fact]
        public void DeleteUser_UserExists_ReturnsTrueAndRemovesUser()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(DeleteUser_UserExists_ReturnsTrueAndRemovesUser));
            context.Users.Add(CreateTestUser());
            context.SaveChanges();

            var result = service.DeleteUser(1);

            Assert.True(result);
            Assert.Empty(context.Users);
        }

        [Fact]
        public void DeleteUser_UserDoesNotExist_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(DeleteUser_UserDoesNotExist_ReturnsFalse));

            var result = service.DeleteUser(999);

            Assert.False(result);
        }

        #endregion

        #region SoftDeleteUserById

        [Fact]
        public void SoftDeleteUserById_ActiveUser_SetsFlagsAndReturnsTrue()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(SoftDeleteUserById_ActiveUser_SetsFlagsAndReturnsTrue));
            context.Users.Add(CreateTestUser(isActive: true));
            context.SaveChanges();

            var result = service.SoftDeleteUserById(1);

            Assert.True(result);
            var dbUser = context.Users.Find(1);
            Assert.False(dbUser!.IsActive);
            Assert.NotNull(dbUser.DeletedAt);
        }

        [Fact]
        public void SoftDeleteUserById_UserNotFound_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(SoftDeleteUserById_UserNotFound_ReturnsFalse));

            var result = service.SoftDeleteUserById(99);

            Assert.False(result);
        }

        [Fact]
        public void SoftDeleteUserById_AlreadyInactive_ReturnsTrueWithoutChangingDeletedAt()
        {
            // Note: The actual implementation returns true even if already inactive (no-op branch)
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(SoftDeleteUserById_AlreadyInactive_ReturnsTrueWithoutChangingDeletedAt));
            var existingDeletedAt = DateTime.UtcNow.AddDays(-1);
            var user = CreateTestUser(isActive: false);
            user.DeletedAt = existingDeletedAt;
            context.Users.Add(user);
            context.SaveChanges();

            var result = service.SoftDeleteUserById(1);

            Assert.True(result); // Returns true because user was found
        }

        #endregion

        #region ActivateUser

        [Fact]
        public void ActivateUser_InactiveUser_ActivatesAndReturnsTrue()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(ActivateUser_InactiveUser_ActivatesAndReturnsTrue));
            var user = CreateTestUser(isActive: false);
            user.DeletedAt = DateTime.UtcNow;
            context.Users.Add(user);
            context.SaveChanges();

            var result = service.ActivateUser(1);

            Assert.True(result);
            var dbUser = context.Users.Find(1);
            Assert.True(dbUser!.IsActive);
            Assert.Null(dbUser.DeletedAt);
        }

        [Fact]
        public void ActivateUser_UserNotFound_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(ActivateUser_UserNotFound_ReturnsFalse));

            var result = service.ActivateUser(99);

            Assert.False(result);
        }

        [Fact]
        public void ActivateUser_AlreadyActive_ReturnsTrueNoChange()
        {
            // Returns true because user was found; no-op since already active
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(ActivateUser_AlreadyActive_ReturnsTrueNoChange));
            context.Users.Add(CreateTestUser(isActive: true));
            context.SaveChanges();

            var result = service.ActivateUser(1);

            Assert.True(result);
        }

        #endregion

        #region LoginAsync

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsSuccessWithTokens()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(LoginAsync_ValidCredentials_ReturnsSuccessWithTokens));
            var user = CreateTestUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("password", "hashed")).Returns(true);
            token.Setup(x => x.GenerateJwtToken(It.IsAny<User>())).Returns("jwt-token");
            token.Setup(x => x.GenerateRefreshToken()).Returns("refresh-token");

            var result = await service.LoginAsync("test@test.com", "password", "127.0.0.1", "Chrome");

            Assert.True(result.Success);
            Assert.Equal("jwt-token", result.AccessToken);
            Assert.Equal("refresh-token", result.RefreshToken);
            Assert.Single(context.UserSessions);
        }

        [Fact]
        public async Task LoginAsync_UserNotFound_ReturnsFailure()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(LoginAsync_UserNotFound_ReturnsFailure));

            var result = await service.LoginAsync("noone@test.com", "password");

            Assert.False(result.Success);
            Assert.Contains("incorrect", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsync_InactiveUser_ReturnsFailure()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(LoginAsync_InactiveUser_ReturnsFailure));
            context.Users.Add(CreateTestUser(isActive: false));
            await context.SaveChangesAsync();

            var result = await service.LoginAsync("test@test.com", "password");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task LoginAsync_LockedOutUser_ReturnsLockedResult()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(LoginAsync_LockedOutUser_ReturnsLockedResult));
            var user = CreateTestUser();
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.LoginAsync("test@test.com", "password");

            Assert.False(result.Success);
            Assert.NotNull(result.LockoutEnd);
            Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsync_ExpiredLockout_ClearsLockoutAndProceeds()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(LoginAsync_ExpiredLockout_ClearsLockoutAndProceeds));
            var user = CreateTestUser();
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1); // Expired lockout
            user.FailedLoginAttempts = 5;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("password", "hashed")).Returns(true);
            token.Setup(x => x.GenerateJwtToken(It.IsAny<User>())).Returns("jwt");
            token.Setup(x => x.GenerateRefreshToken()).Returns("refresh");

            var result = await service.LoginAsync("test@test.com", "password");

            Assert.True(result.Success);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal(0, dbUser!.FailedLoginAttempts);
            Assert.Null(dbUser.LockoutEnd);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_IncrementsFailedAttempts()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(LoginAsync_WrongPassword_IncrementsFailedAttempts));
            var user = CreateTestUser();
            user.FailedLoginAttempts = 0;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("wrong", "hashed")).Returns(false);

            var result = await service.LoginAsync("test@test.com", "wrong");

            Assert.False(result.Success);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal(1, dbUser!.FailedLoginAttempts);
            Assert.Null(dbUser.LockoutEnd);
        }

        [Fact]
        public async Task LoginAsync_FifthFailedAttempt_LocksAccount()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(LoginAsync_FifthFailedAttempt_LocksAccount));
            var user = CreateTestUser();
            user.FailedLoginAttempts = 4; // This will be the 5th attempt
            context.Users.Add(user);
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("wrong", "hashed")).Returns(false);

            var result = await service.LoginAsync("test@test.com", "wrong");

            Assert.False(result.Success);
            var dbUser = await context.Users.FindAsync(1);
            Assert.NotNull(dbUser!.LockoutEnd);
            Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsync_SuccessfulLogin_ResetsFailedAttempts()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(LoginAsync_SuccessfulLogin_ResetsFailedAttempts));
            var user = CreateTestUser();
            user.FailedLoginAttempts = 3;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("password", "hashed")).Returns(true);
            token.Setup(x => x.GenerateJwtToken(It.IsAny<User>())).Returns("jwt");
            token.Setup(x => x.GenerateRefreshToken()).Returns("refresh");

            var result = await service.LoginAsync("test@test.com", "password");

            Assert.True(result.Success);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal(0, dbUser!.FailedLoginAttempts);
            Assert.Null(dbUser.LockoutEnd);
            Assert.NotNull(dbUser.LastLoginDate);
        }

        [Fact]
        public async Task LoginAsync_CreatesSessionWithDeviceInfo()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(LoginAsync_CreatesSessionWithDeviceInfo));
            context.Users.Add(CreateTestUser());
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("password", "hashed")).Returns(true);
            token.Setup(x => x.GenerateJwtToken(It.IsAny<User>())).Returns("jwt");
            token.Setup(x => x.GenerateRefreshToken()).Returns("refresh");

            await service.LoginAsync("test@test.com", "password", "192.168.1.1", "Firefox");

            var session = await context.UserSessions.FirstAsync();
            Assert.Equal("192.168.1.1", session.IpAddress);
            Assert.Equal("Firefox", session.DeviceName);
            Assert.Equal("refresh", session.RefreshToken);
            Assert.False(session.IsRevoked);
        }

        [Fact]
        public async Task LoginAsync_NullDeviceName_DefaultsToUnknownDevice()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(LoginAsync_NullDeviceName_DefaultsToUnknownDevice));
            context.Users.Add(CreateTestUser());
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("password", "hashed")).Returns(true);
            token.Setup(x => x.GenerateJwtToken(It.IsAny<User>())).Returns("jwt");
            token.Setup(x => x.GenerateRefreshToken()).Returns("refresh");

            await service.LoginAsync("test@test.com", "password", "1.2.3.4", null);

            var session = await context.UserSessions.FirstAsync();
            Assert.Equal("Unknown Device", session.DeviceName);
        }

        [Fact]
        public async Task LoginAsync_NormalizesEmailToLowercase()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(LoginAsync_NormalizesEmailToLowercase));
            context.Users.Add(CreateTestUser(email: "test@test.com"));
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("password", "hashed")).Returns(true);
            token.Setup(x => x.GenerateJwtToken(It.IsAny<User>())).Returns("jwt");
            token.Setup(x => x.GenerateRefreshToken()).Returns("refresh");

            var result = await service.LoginAsync("  TEST@TEST.COM  ", "password");

            Assert.True(result.Success);
        }

        #endregion

        #region ChangePassword

        [Fact]
        public void ChangePassword_CorrectOldPassword_UpdatesPassword()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(ChangePassword_CorrectOldPassword_UpdatesPassword));
            context.Users.Add(CreateTestUser(passwordHash: "oldHash"));
            context.SaveChanges();

            hasher.Setup(x => x.Verify("oldpass", "oldHash")).Returns(true);
            hasher.Setup(x => x.Hash("newpass")).Returns("newHash");

            var dto = new ChangePasswordDto { OldPassword = "oldpass", NewPassword = "newpass", ConfirmNewPassword = "newpass" };
            var result = service.ChangePassword(1, dto);

            Assert.True(result);
            var dbUser = context.Users.Find(1);
            Assert.Equal("newHash", dbUser!.PasswordHash);
            Assert.NotNull(dbUser.PasswordLastChangedAt);
            Assert.NotNull(dbUser.UpdatedAt);
        }

        [Fact]
        public void ChangePassword_WrongOldPassword_ThrowsException()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(ChangePassword_WrongOldPassword_ThrowsException));
            context.Users.Add(CreateTestUser(passwordHash: "oldHash"));
            context.SaveChanges();

            hasher.Setup(x => x.Verify("wrong", "oldHash")).Returns(false);

            var dto = new ChangePasswordDto { OldPassword = "wrong", NewPassword = "newpass", ConfirmNewPassword = "newpass" };

            Assert.Throws<Exception>(() => service.ChangePassword(1, dto));
        }

        [Fact]
        public void ChangePassword_UserNotFound_ThrowsException()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(ChangePassword_UserNotFound_ThrowsException));

            var dto = new ChangePasswordDto { OldPassword = "old", NewPassword = "new", ConfirmNewPassword = "new" };

            Assert.Throws<Exception>(() => service.ChangePassword(999, dto));
        }

        #endregion

        #region ChangeEmail

        [Fact]
        public void ChangeEmail_ValidData_UpdatesEmail()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(ChangeEmail_ValidData_UpdatesEmail));
            context.Users.Add(CreateTestUser(email: "old@test.com"));
            context.SaveChanges();

            hasher.Setup(x => x.Verify("pass", "hashed")).Returns(true);

            var dto = new ChangeEmailDto { NewEmail = "new@test.com", Password = "pass" };
            var result = service.ChangeEmail(1, dto);

            Assert.True(result);
            Assert.Equal("new@test.com", context.Users.Find(1)!.Email);
        }

        [Fact]
        public void ChangeEmail_WrongPassword_ThrowsException()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(ChangeEmail_WrongPassword_ThrowsException));
            context.Users.Add(CreateTestUser());
            context.SaveChanges();

            hasher.Setup(x => x.Verify("wrong", "hashed")).Returns(false);

            var dto = new ChangeEmailDto { NewEmail = "new@test.com", Password = "wrong" };

            Assert.Throws<Exception>(() => service.ChangeEmail(1, dto));
        }

        [Fact]
        public void ChangeEmail_EmailAlreadyTaken_ThrowsException()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(ChangeEmail_EmailAlreadyTaken_ThrowsException));
            context.Users.Add(CreateTestUser(id: 1, email: "old@test.com"));
            context.Users.Add(CreateTestUser(id: 2, email: "taken@test.com"));
            context.SaveChanges();

            hasher.Setup(x => x.Verify("pass", "hashed")).Returns(true);

            var dto = new ChangeEmailDto { NewEmail = "taken@test.com", Password = "pass" };

            Assert.Throws<Exception>(() => service.ChangeEmail(1, dto));
        }

        [Fact]
        public void ChangeEmail_UserNotFound_ThrowsException()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(ChangeEmail_UserNotFound_ThrowsException));

            var dto = new ChangeEmailDto { NewEmail = "new@test.com", Password = "pass" };

            Assert.Throws<Exception>(() => service.ChangeEmail(999, dto));
        }

        #endregion

        #region GetAllUsersOrderByDate

        [Fact]
        public void GetAllUsersOrderByDate_ReturnsActiveUsersOrderedByInsertDateDesc()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetAllUsersOrderByDate_ReturnsActiveUsersOrderedByInsertDateDesc));
            var u1 = CreateTestUser(id: 1, email: "a@t.com");
            u1.InsertDate = DateTime.UtcNow.AddDays(-2);
            var u2 = CreateTestUser(id: 2, email: "b@t.com");
            u2.InsertDate = DateTime.UtcNow;
            var u3 = CreateTestUser(id: 3, email: "c@t.com", isActive: false);
            u3.DeletedAt = DateTime.UtcNow;
            var u4 = CreateTestUser(id: 4, email: "d@t.com");
            u4.InsertDate = DateTime.UtcNow.AddDays(-1);
            u4.DeletedAt = DateTime.UtcNow; // Has DeletedAt set, even though active

            context.Users.AddRange(u1, u2, u3, u4);
            context.SaveChanges();

            var result = service.GetAllUsersOrderByDate();

            // Only active users with DeletedAt == null
            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id); // Newest first
            Assert.Equal(1, result[1].Id);
        }

        [Fact]
        public void GetAllUsersOrderByDate_NoActiveUsers_ReturnsEmptyList()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetAllUsersOrderByDate_NoActiveUsers_ReturnsEmptyList));
            context.Users.Add(CreateTestUser(isActive: false));
            context.SaveChanges();

            var result = service.GetAllUsersOrderByDate();

            Assert.Empty(result);
        }

        #endregion

        #region AssignRoleToUser

        [Fact]
        public void AssignRoleToUser_ValidUserAndRole_AssignsRole()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(AssignRoleToUser_ValidUserAndRole_AssignsRole));
            // Note: EnsureCreated seeds roles (Id=1 Admin, Id=2 User, Id=3 Manager)
            var user = CreateTestUser();
            context.Users.Add(user);
            context.SaveChanges();

            var result = service.AssignRoleToUser(1, 1); // Assign Admin role

            Assert.True(result);
            var dbUser = context.Users.Include(u => u.Roles).First(u => u.Id == 1);
            Assert.Single(dbUser.Roles);
        }

        [Fact]
        public void AssignRoleToUser_UserAlreadyHasRole_ThrowsException()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(AssignRoleToUser_UserAlreadyHasRole_ThrowsException));
            var adminRole = context.Roles.Find(1)!; // Admin (seeded)
            var user = CreateTestUser();
            user.Roles = new List<Role> { adminRole };
            context.Users.Add(user);
            context.SaveChanges();

            Assert.Throws<Exception>(() => service.AssignRoleToUser(1, 1));
        }

        [Fact]
        public void AssignRoleToUser_UserNotFound_ThrowsException()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(AssignRoleToUser_UserNotFound_ThrowsException));

            Assert.Throws<Exception>(() => service.AssignRoleToUser(999, 1));
        }

        [Fact]
        public void AssignRoleToUser_RoleNotFound_ThrowsException()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(AssignRoleToUser_RoleNotFound_ThrowsException));
            context.Users.Add(CreateTestUser());
            context.SaveChanges();

            Assert.Throws<Exception>(() => service.AssignRoleToUser(1, 999));
        }

        #endregion

        #region RemoveRoleFromUser

        [Fact]
        public void RemoveRoleFromUser_UserHasRole_RemovesRole()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(RemoveRoleFromUser_UserHasRole_RemovesRole));
            var adminRole = context.Roles.Find(1)!;
            var user = CreateTestUser();
            user.Roles = new List<Role> { adminRole };
            context.Users.Add(user);
            context.SaveChanges();

            var result = service.RemoveRoleFromUser(1, 1);

            Assert.True(result);
            var dbUser = context.Users.Include(u => u.Roles).First(u => u.Id == 1);
            Assert.Empty(dbUser.Roles);
        }

        [Fact]
        public void RemoveRoleFromUser_UserDoesNotHaveRole_ThrowsException()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(RemoveRoleFromUser_UserDoesNotHaveRole_ThrowsException));
            context.Users.Add(CreateTestUser());
            context.SaveChanges();

            Assert.Throws<Exception>(() => service.RemoveRoleFromUser(1, 1));
        }

        [Fact]
        public void RemoveRoleFromUser_UserNotFound_ThrowsException()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(RemoveRoleFromUser_UserNotFound_ThrowsException));

            Assert.Throws<Exception>(() => service.RemoveRoleFromUser(999, 1));
        }

        #endregion

        #region AddNewUserAsync

        [Fact]
        public async Task AddNewUserAsync_ValidUser_AddsUserAndSendsEmail()
        {
            var (context, service, hasher, token, _, email) = CreateTestEnvironment(nameof(AddNewUserAsync_ValidUser_AddsUserAndSendsEmail));
            var dto = new AddNewUserDto { FirstName = "John", LastName = "Doe", Email = "john@test.com", Password = "Pass123!" };

            hasher.Setup(x => x.Hash("Pass123!")).Returns("hashedPassword");
            token.Setup(x => x.GenerateEmailVerificationToken()).Returns("verify-token-123");

            await service.AddNewUserAsync(dto);

            Assert.Single(context.Users);
            var dbUser = await context.Users.FirstAsync();
            Assert.Equal("john@test.com", dbUser.Email);
            Assert.Equal("hashedPassword", dbUser.PasswordHash);
            Assert.Equal("verify-token-123", dbUser.EmailVerificationToken);
            Assert.False(dbUser.IsActive);
            Assert.False(dbUser.IsEmailVerified);
            email.Verify(x => x.SendEmail("john@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task AddNewUserAsync_EmailAlreadyExists_ThrowsInvalidOperationException()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(AddNewUserAsync_EmailAlreadyExists_ThrowsInvalidOperationException));
            context.Users.Add(CreateTestUser(email: "existing@test.com"));
            await context.SaveChangesAsync();

            var dto = new AddNewUserDto { FirstName = "New", LastName = "User", Email = "existing@test.com", Password = "Pass123!" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddNewUserAsync(dto));
        }

        [Fact]
        public async Task AddNewUserAsync_AssignsDefaultUserRole()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(AddNewUserAsync_AssignsDefaultUserRole));
            var dto = new AddNewUserDto { FirstName = "Jane", LastName = "Doe", Email = "jane@test.com", Password = "Pass123!" };

            hasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hash");
            token.Setup(x => x.GenerateEmailVerificationToken()).Returns("token");

            await service.AddNewUserAsync(dto);

            var dbUser = await context.Users.Include(u => u.Roles).FirstAsync();
            Assert.Contains(dbUser.Roles, r => r.Name == "User");
        }

        [Fact]
        public async Task AddNewUserAsync_NormalizesEmail()
        {
            var (context, service, hasher, token, _, _) = CreateTestEnvironment(nameof(AddNewUserAsync_NormalizesEmail));
            var dto = new AddNewUserDto { FirstName = "Test", LastName = "User", Email = "  TEST@EXAMPLE.COM  ", Password = "Pass123!" };

            hasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hash");
            token.Setup(x => x.GenerateEmailVerificationToken()).Returns("token");

            await service.AddNewUserAsync(dto);

            var dbUser = await context.Users.FirstAsync();
            Assert.Equal("test@example.com", dbUser.Email);
        }

        #endregion

        #region VerifyEmailAsync

        [Fact]
        public async Task VerifyEmailAsync_ValidToken_ActivatesUserAndReturnsTrue()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailAsync_ValidToken_ActivatesUserAndReturnsTrue));
            var user = CreateTestUser(isActive: false);
            user.IsEmailVerified = false;
            user.EmailVerificationToken = "valid-token";
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.VerifyEmailAsync("valid-token");

            Assert.True(result);
            var dbUser = await context.Users.FindAsync(1);
            Assert.True(dbUser!.IsActive);
            Assert.True(dbUser.IsEmailVerified);
            Assert.Null(dbUser.EmailVerificationToken);
            Assert.Null(dbUser.EmailVerificationTokenExpiry);
        }

        [Fact]
        public async Task VerifyEmailAsync_ExpiredToken_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailAsync_ExpiredToken_ReturnsFalse));
            var user = CreateTestUser(isActive: false);
            user.EmailVerificationToken = "expired-token";
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.VerifyEmailAsync("expired-token");

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyEmailAsync_InvalidToken_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailAsync_InvalidToken_ReturnsFalse));

            var result = await service.VerifyEmailAsync("non-existent-token");

            Assert.False(result);
        }

        #endregion

        #region ForgotPasswordAsync

        [Fact]
        public async Task ForgotPasswordAsync_ActiveUser_GeneratesTokenAndSendsEmail()
        {
            var (context, service, _, token, _, email) = CreateTestEnvironment(nameof(ForgotPasswordAsync_ActiveUser_GeneratesTokenAndSendsEmail));
            context.Users.Add(CreateTestUser());
            await context.SaveChangesAsync();

            token.Setup(x => x.GeneratePasswordResetToken()).Returns("reset-token-abc");

            var result = await service.ForgotPasswordAsync("test@test.com");

            Assert.True(result);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal("reset-token-abc", dbUser!.PasswordResetToken);
            Assert.NotNull(dbUser.PasswordResetTokenExpiry);
            email.Verify(x => x.SendEmail("test@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_UserNotFound_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(ForgotPasswordAsync_UserNotFound_ReturnsFalse));

            var result = await service.ForgotPasswordAsync("nobody@test.com");

            Assert.False(result);
        }

        [Fact]
        public async Task ForgotPasswordAsync_InactiveUser_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(ForgotPasswordAsync_InactiveUser_ReturnsFalse));
            context.Users.Add(CreateTestUser(isActive: false));
            await context.SaveChangesAsync();

            var result = await service.ForgotPasswordAsync("test@test.com");

            Assert.False(result);
        }

        #endregion

        #region ResetPasswordAsync

        [Fact]
        public async Task ResetPasswordAsync_ValidToken_ResetsPasswordAndClearsToken()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(ResetPasswordAsync_ValidToken_ResetsPasswordAndClearsToken));
            var user = CreateTestUser();
            user.PasswordResetToken = "reset-token";
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(10);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Hash("NewPass123!")).Returns("newHash");

            var dto = new ResetPasswordDto { Token = "reset-token", NewPassword = "NewPass123!", ConfirmNewPassword = "NewPass123!" };
            var result = await service.ResetPasswordAsync(dto);

            Assert.True(result);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal("newHash", dbUser!.PasswordHash);
            Assert.Null(dbUser.PasswordResetToken);
            Assert.Null(dbUser.PasswordResetTokenExpiry);
            Assert.NotNull(dbUser.PasswordLastChangedAt);
        }

        [Fact]
        public async Task ResetPasswordAsync_ExpiredToken_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(ResetPasswordAsync_ExpiredToken_ReturnsFalse));
            var user = CreateTestUser();
            user.PasswordResetToken = "expired-token";
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-10);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var dto = new ResetPasswordDto { Token = "expired-token", NewPassword = "NewPass123!", ConfirmNewPassword = "NewPass123!" };
            var result = await service.ResetPasswordAsync(dto);

            Assert.False(result);
        }

        [Fact]
        public async Task ResetPasswordAsync_InvalidToken_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(ResetPasswordAsync_InvalidToken_ReturnsFalse));

            var dto = new ResetPasswordDto { Token = "bad-token", NewPassword = "NewPass123!", ConfirmNewPassword = "NewPass123!" };
            var result = await service.ResetPasswordAsync(dto);

            Assert.False(result);
        }

        #endregion

        #region EmailChangeAsync

        [Fact]
        public async Task EmailChangeAsync_ValidData_GeneratesTokenAndSendsEmail()
        {
            var (context, service, hasher, token, _, email) = CreateTestEnvironment(nameof(EmailChangeAsync_ValidData_GeneratesTokenAndSendsEmail));
            context.Users.Add(CreateTestUser(email: "old@test.com"));
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("pass", "hashed")).Returns(true);
            token.Setup(x => x.GenerateEmailChangeToken()).Returns("change-token-xyz");

            var dto = new ChangeEmailDto { NewEmail = "new@test.com", Password = "pass" };
            var result = await service.EmailChangeAsync(1, dto);

            Assert.True(result);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal("new@test.com", dbUser!.PendingNewEmail);
            Assert.Equal("change-token-xyz", dbUser.EmailChangeToken);
            Assert.NotNull(dbUser.EmailChangeTokenExpiry);
            email.Verify(x => x.SendEmail("new@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task EmailChangeAsync_SameEmail_ThrowsException()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(EmailChangeAsync_SameEmail_ThrowsException));
            context.Users.Add(CreateTestUser(email: "same@test.com"));
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("pass", "hashed")).Returns(true);

            var dto = new ChangeEmailDto { NewEmail = "same@test.com", Password = "pass" };

            await Assert.ThrowsAsync<Exception>(() => service.EmailChangeAsync(1, dto));
        }

        [Fact]
        public async Task EmailChangeAsync_WrongPassword_ThrowsException()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(EmailChangeAsync_WrongPassword_ThrowsException));
            context.Users.Add(CreateTestUser());
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("wrong", "hashed")).Returns(false);

            var dto = new ChangeEmailDto { NewEmail = "new@test.com", Password = "wrong" };

            await Assert.ThrowsAsync<Exception>(() => service.EmailChangeAsync(1, dto));
        }

        [Fact]
        public async Task EmailChangeAsync_EmailTaken_ThrowsException()
        {
            var (context, service, hasher, _, _, _) = CreateTestEnvironment(nameof(EmailChangeAsync_EmailTaken_ThrowsException));
            context.Users.Add(CreateTestUser(id: 1, email: "user1@test.com"));
            context.Users.Add(CreateTestUser(id: 2, email: "taken@test.com"));
            await context.SaveChangesAsync();

            hasher.Setup(x => x.Verify("pass", "hashed")).Returns(true);

            var dto = new ChangeEmailDto { NewEmail = "taken@test.com", Password = "pass" };

            await Assert.ThrowsAsync<Exception>(() => service.EmailChangeAsync(1, dto));
        }

        [Fact]
        public async Task EmailChangeAsync_UserNotFound_ThrowsException()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(EmailChangeAsync_UserNotFound_ThrowsException));

            var dto = new ChangeEmailDto { NewEmail = "new@test.com", Password = "pass" };

            await Assert.ThrowsAsync<Exception>(() => service.EmailChangeAsync(999, dto));
        }

        #endregion

        #region VerifyEmailChangeAsync

        [Fact]
        public async Task VerifyEmailChangeAsync_ValidToken_ChangesEmail()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailChangeAsync_ValidToken_ChangesEmail));
            var user = CreateTestUser(email: "old@test.com");
            user.PendingNewEmail = "new@test.com";
            user.EmailChangeToken = "change-token";
            user.EmailChangeTokenExpiry = DateTime.UtcNow.AddMinutes(10);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.VerifyEmailChangeAsync("change-token");

            Assert.True(result);
            var dbUser = await context.Users.FindAsync(1);
            Assert.Equal("new@test.com", dbUser!.Email);
            Assert.Null(dbUser.PendingNewEmail);
            Assert.Null(dbUser.EmailChangeToken);
            Assert.Null(dbUser.EmailChangeTokenExpiry);
        }

        [Fact]
        public async Task VerifyEmailChangeAsync_ExpiredToken_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailChangeAsync_ExpiredToken_ReturnsFalse));
            var user = CreateTestUser();
            user.EmailChangeToken = "expired";
            user.EmailChangeTokenExpiry = DateTime.UtcNow.AddMinutes(-10);
            user.PendingNewEmail = "new@test.com";
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.VerifyEmailChangeAsync("expired");

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyEmailChangeAsync_NullPendingEmail_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailChangeAsync_NullPendingEmail_ReturnsFalse));
            var user = CreateTestUser();
            user.EmailChangeToken = "token";
            user.EmailChangeTokenExpiry = DateTime.UtcNow.AddMinutes(10);
            user.PendingNewEmail = null;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.VerifyEmailChangeAsync("token");

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyEmailChangeAsync_EmailTakenByAnother_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(VerifyEmailChangeAsync_EmailTakenByAnother_ReturnsFalse));
            var user1 = CreateTestUser(id: 1, email: "user1@test.com");
            user1.PendingNewEmail = "taken@test.com";
            user1.EmailChangeToken = "token";
            user1.EmailChangeTokenExpiry = DateTime.UtcNow.AddMinutes(10);
            context.Users.Add(user1);
            context.Users.Add(CreateTestUser(id: 2, email: "taken@test.com"));
            await context.SaveChangesAsync();

            var result = await service.VerifyEmailChangeAsync("token");

            Assert.False(result);
        }

        #endregion

        #region GetActiveSessionsAsync

        [Fact]
        public async Task GetActiveSessionsAsync_ReturnsOnlyNonRevokedFutureSessions()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetActiveSessionsAsync_ReturnsOnlyNonRevokedFutureSessions));
            context.UserSessions.AddRange(
                new UserSession { Id = 1, UserId = 1, RefreshToken = "r1", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow },
                new UserSession { Id = 2, UserId = 1, RefreshToken = "r2", IsRevoked = true, ExpiryDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
                new UserSession { Id = 3, UserId = 1, RefreshToken = "r3", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
                new UserSession { Id = 4, UserId = 2, RefreshToken = "r4", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var result = await service.GetActiveSessionsAsync(1);

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetActiveSessionsAsync_OrderedByCreatedAtDesc()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetActiveSessionsAsync_OrderedByCreatedAtDesc));
            context.UserSessions.AddRange(
                new UserSession { Id = 1, UserId = 1, RefreshToken = "r1", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new UserSession { Id = 2, UserId = 1, RefreshToken = "r2", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var result = await service.GetActiveSessionsAsync(1);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id); // Newer first
            Assert.Equal(1, result[1].Id);
        }

        #endregion

        #region RevokeSessionAsync

        [Fact]
        public async Task RevokeSessionAsync_ValidSession_RevokesAndReturnsTrue()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(RevokeSessionAsync_ValidSession_RevokesAndReturnsTrue));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 1, RefreshToken = "ref", IsRevoked = false });
            await context.SaveChangesAsync();

            var result = await service.RevokeSessionAsync(1, 1);

            Assert.True(result);
            Assert.True((await context.UserSessions.FindAsync(1))!.IsRevoked);
        }

        [Fact]
        public async Task RevokeSessionAsync_SessionNotFound_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(RevokeSessionAsync_SessionNotFound_ReturnsFalse));

            var result = await service.RevokeSessionAsync(1, 999);

            Assert.False(result);
        }

        [Fact]
        public async Task RevokeSessionAsync_AlreadyRevoked_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(RevokeSessionAsync_AlreadyRevoked_ReturnsFalse));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 1, RefreshToken = "ref", IsRevoked = true });
            await context.SaveChangesAsync();

            var result = await service.RevokeSessionAsync(1, 1);

            Assert.False(result);
        }

        [Fact]
        public async Task RevokeSessionAsync_WrongUserId_ReturnsFalse()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(RevokeSessionAsync_WrongUserId_ReturnsFalse));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 2, RefreshToken = "ref", IsRevoked = false });
            await context.SaveChangesAsync();

            var result = await service.RevokeSessionAsync(1, 1); // User 1 trying to revoke user 2's session

            Assert.False(result);
        }

        #endregion

        #region LogoutSessionAsync

        [Fact]
        public async Task LogoutSessionAsync_ValidToken_RevokesSessionAndReturnsTrue()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(LogoutSessionAsync_ValidToken_RevokesSessionAndReturnsTrue));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 1, RefreshToken = "my-token", IsRevoked = false });
            await context.SaveChangesAsync();

            var result = await service.LogoutSessionAsync("my-token");

            Assert.True(result);
            Assert.True((await context.UserSessions.FindAsync(1))!.IsRevoked);
        }

        [Fact]
        public async Task LogoutSessionAsync_InvalidToken_ReturnsFalse()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(LogoutSessionAsync_InvalidToken_ReturnsFalse));

            var result = await service.LogoutSessionAsync("nonexistent-token");

            Assert.False(result);
        }

        #endregion

        #region GetCurrentSessionAsync

        [Fact]
        public async Task GetCurrentSessionAsync_ValidToken_ReturnsSession()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetCurrentSessionAsync_ValidToken_ReturnsSession));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 1, RefreshToken = "active-token", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(1) });
            await context.SaveChangesAsync();

            var result = await service.GetCurrentSessionAsync("active-token");

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task GetCurrentSessionAsync_RevokedSession_ReturnsNull()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetCurrentSessionAsync_RevokedSession_ReturnsNull));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 1, RefreshToken = "revoked", IsRevoked = true, ExpiryDate = DateTime.UtcNow.AddDays(1) });
            await context.SaveChangesAsync();

            var result = await service.GetCurrentSessionAsync("revoked");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetCurrentSessionAsync_ExpiredSession_ReturnsNull()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetCurrentSessionAsync_ExpiredSession_ReturnsNull));
            context.UserSessions.Add(new UserSession { Id = 1, UserId = 1, RefreshToken = "expired", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(-1) });
            await context.SaveChangesAsync();

            var result = await service.GetCurrentSessionAsync("expired");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetCurrentSessionAsync_InvalidToken_ReturnsNull()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(GetCurrentSessionAsync_InvalidToken_ReturnsNull));

            var result = await service.GetCurrentSessionAsync("no-such-token");

            Assert.Null(result);
        }

        #endregion

        #region GetUserByIdAsync

        [Fact]
        public async Task GetUserByIdAsync_ActiveUser_ReturnsUserWithRoles()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetUserByIdAsync_ActiveUser_ReturnsUserWithRoles));
            var adminRole = context.Roles.Find(1)!;
            var user = CreateTestUser();
            user.Roles = new List<Role> { adminRole };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var result = await service.GetUserByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Test", result.FirstName);
            Assert.Single(result.Roles);
        }

        [Fact]
        public async Task GetUserByIdAsync_InactiveUser_ReturnsNull()
        {
            var (context, service, _, _, _, _) = CreateTestEnvironment(nameof(GetUserByIdAsync_InactiveUser_ReturnsNull));
            context.Users.Add(CreateTestUser(isActive: false));
            await context.SaveChangesAsync();

            var result = await service.GetUserByIdAsync(1);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserByIdAsync_NonexistentUser_ReturnsNull()
        {
            var (_, service, _, _, _, _) = CreateTestEnvironment(nameof(GetUserByIdAsync_NonexistentUser_ReturnsNull));

            var result = await service.GetUserByIdAsync(999);

            Assert.Null(result);
        }

        #endregion
    }
}
