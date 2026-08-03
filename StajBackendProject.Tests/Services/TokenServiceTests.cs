using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using StajBackendProject.Implements;
using StajBackendProject.Models;
using StajBackendProject.Models.Dto;

namespace StajBackendProject.Tests.Services
{
    public class TokenServiceTests : IDisposable
    {
        private readonly UsersContext _context;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;

        public TokenServiceTests()
        {
            var options = new DbContextOptionsBuilder<UsersContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new UsersContext(options);

            var inMemorySettings = new Dictionary<string, string>
            {
                {"Jwt:Key", "ThisIsASecretKeyForTestingPurposesOnly1234!"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _tokenService = new TokenService(_configuration, _context);
        }

        [Fact]
        public void GenerateJwtToken_ShouldReturnNonEmptyString()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Roles = new List<Role>() };
            var token = _tokenService.GenerateJwtToken(user);
            token.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GenerateJwtToken_ShouldContainNameIdentifierClaim()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Roles = new List<Role>() };
            var tokenString = _tokenService.GenerateJwtToken(user);

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "1");
        }

        [Fact]
        public void GenerateJwtToken_ShouldContainEmailClaim()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Roles = new List<Role>() };
            var tokenString = _tokenService.GenerateJwtToken(user);

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "john@example.com");
        }

        [Fact]
        public void GenerateJwtToken_ShouldContainNameClaim()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", PasswordHash = "hash", Roles = new List<Role>() };
            var tokenString = _tokenService.GenerateJwtToken(user);

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "John");
        }

        [Fact]
        public void GenerateJwtToken_ShouldContainRoleClaim()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Roles = new List<Role> { new Role { Name = "Admin" } } };
            var tokenString = _tokenService.GenerateJwtToken(user);

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        }

        [Fact]
        public void GenerateJwtToken_ShouldHaveCorrectIssuer()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Roles = new List<Role>() };
            var tokenString = _tokenService.GenerateJwtToken(user);

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Issuer.Should().Be("TestIssuer");
        }

        [Fact]
        public void GenerateJwtToken_ShouldHaveCorrectAudience()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Roles = new List<Role>() };
            var tokenString = _tokenService.GenerateJwtToken(user);

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Audiences.Should().Contain("TestAudience");
        }

        [Fact]
        public void GenerateRefreshToken_ShouldReturnNonEmptyString()
        {
            var token = _tokenService.GenerateRefreshToken();
            token.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void RefreshToken_WithValidToken_ShouldReturnNewTokens()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", PasswordHash = "hash", Roles = new List<Role>() };
            _context.Users.Add(user);
            var session = new UserSession
            {
                Id = 1,
                UserId = 1,
                RefreshToken = "valid_refresh_token",
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsRevoked = false
            };
            _context.UserSessions.Add(session);
            _context.SaveChanges();

            var request = new RefreshTokenRequestDto { RefreshToken = "valid_refresh_token" };
            var result = _tokenService.RefreshToken(request);

            result.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrWhiteSpace();
            result.RefreshToken.Should().NotBeNullOrWhiteSpace();
            result.RefreshToken.Should().NotBe("valid_refresh_token");

            var dbSession = _context.UserSessions.Find(1);
            dbSession.IsRevoked.Should().BeTrue();
            _context.UserSessions.Count().Should().Be(2); // The revoked one and the new one
        }

        [Fact]
        public void RefreshToken_WithInvalidToken_ShouldThrowException()
        {
            var request = new RefreshTokenRequestDto { RefreshToken = "invalid_refresh_token" };
            
            Action act = () => _tokenService.RefreshToken(request);

            act.Should().Throw<Exception>().WithMessage("Invalid or expired refresh token. Please login again.");
        }

        [Fact]
        public void RefreshToken_WithExpiredToken_ShouldThrowException()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", PasswordHash = "hash", Roles = new List<Role>() };
            _context.Users.Add(user);
            var session = new UserSession
            {
                Id = 1,
                UserId = 1,
                RefreshToken = "expired_refresh_token",
                ExpiryDate = DateTime.UtcNow.AddDays(-1),
                IsRevoked = false
            };
            _context.UserSessions.Add(session);
            _context.SaveChanges();

            var request = new RefreshTokenRequestDto { RefreshToken = "expired_refresh_token" };

            Action act = () => _tokenService.RefreshToken(request);

            act.Should().Throw<Exception>().WithMessage("Invalid or expired refresh token. Please login again.");
        }

        [Fact]
        public void RefreshToken_WithRevokedToken_ShouldThrowException()
        {
            var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", PasswordHash = "hash", Roles = new List<Role>() };
            _context.Users.Add(user);
            var session = new UserSession
            {
                Id = 1,
                UserId = 1,
                RefreshToken = "revoked_refresh_token",
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsRevoked = true
            };
            _context.UserSessions.Add(session);
            _context.SaveChanges();

            var request = new RefreshTokenRequestDto { RefreshToken = "revoked_refresh_token" };

            Action act = () => _tokenService.RefreshToken(request);

            act.Should().Throw<Exception>().WithMessage("Invalid or expired refresh token. Please login again.");
        }

        [Fact]
        public void GenerateEmailVerificationToken_ShouldReturnNonEmptyString()
        {
            var token = _tokenService.GenerateEmailVerificationToken();
            token.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GeneratePasswordResetToken_ShouldReturnNonEmptyString()
        {
            var token = _tokenService.GeneratePasswordResetToken();
            token.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GenerateEmailChangeToken_ShouldReturnNonEmptyString()
        {
            var token = _tokenService.GenerateEmailChangeToken();
            token.Should().NotBeNullOrWhiteSpace();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
