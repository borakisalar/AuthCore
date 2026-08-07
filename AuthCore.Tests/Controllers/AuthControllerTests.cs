using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AuthCore.Controllers;
using AuthCore.Interfaces;
using AuthCore.Models;
using AuthCore.Models.Dto;
using Xunit;

namespace AuthCore.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<IMfaService> _mockMfaService;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockTokenService = new Mock<ITokenService>();
            _mockMfaService = new Mock<IMfaService>();
            _controller = new AuthController(_mockUserService.Object, _mockTokenService.Object, _mockMfaService.Object);
        }

        private void SetupHttpContext(string nameIdentifier = "1", string ipAddress = "127.0.0.1", string userAgent = "TestAgent")
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, nameIdentifier)
            }, "mock"));

            var context = new DefaultHttpContext { User = user };
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);
            context.Request.Headers["User-Agent"] = userAgent;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = context
            };
        }

        [Fact]
        public async Task Register_WithValidData_ReturnsOk()
        {
            var dto = new AddNewUserDto { Email = "test@test.com" };
            _mockUserService.Setup(s => s.AddNewUserAsync(dto)).Returns(Task.CompletedTask);

            var result = await _controller.Register(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Login_Success_ReturnsOk()
        {
            SetupHttpContext();
            var dto = new LoginDto { Email = "test@test.com", Password = "pwd" };
            var loginResult = new LoginResultDto { Success = true, AccessToken = "acc", RefreshToken = "ref" };
            _mockUserService.Setup(s => s.LoginAsync(dto.Email, dto.Password, "127.0.0.1", "TestAgent")).ReturnsAsync(loginResult);

            var result = await _controller.Login(dto);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(loginResult);
        }

        [Fact]
        public async Task Login_Lockout_Returns423()
        {
            SetupHttpContext();
            var dto = new LoginDto { Email = "test@test.com", Password = "pwd" };
            var loginResult = new LoginResultDto { Success = false, LockoutEnd = DateTime.UtcNow.AddMinutes(5) };
            _mockUserService.Setup(s => s.LoginAsync(dto.Email, dto.Password, "127.0.0.1", "TestAgent")).ReturnsAsync(loginResult);

            var result = await _controller.Login(dto);

            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(423);
        }

        [Fact]
        public async Task Login_Failure_ReturnsUnauthorized()
        {
            SetupHttpContext();
            var dto = new LoginDto { Email = "test@test.com", Password = "pwd" };
            var loginResult = new LoginResultDto { Success = false };
            _mockUserService.Setup(s => s.LoginAsync(dto.Email, dto.Password, "127.0.0.1", "TestAgent")).ReturnsAsync(loginResult);

            var result = await _controller.Login(dto);

            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public void RefreshToken_ValidRequest_ReturnsOk()
        {
            var dto = new RefreshTokenRequestDto { AccessToken = "acc", RefreshToken = "ref" };
            var tokenResponse = new TokenResponseDto { AccessToken = "new_acc", RefreshToken = "new_ref" };
            _mockTokenService.Setup(s => s.RefreshToken(dto)).Returns(tokenResponse);

            var result = _controller.RefreshToken(dto);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(tokenResponse);
        }

        [Fact]
        public async Task ChangeEmail_ValidRequest_ReturnsOk()
        {
            SetupHttpContext("1");
            var dto = new ChangeEmailDto { NewEmail = "new@test.com" };
            _mockUserService.Setup(s => s.EmailChangeAsync(1, dto)).Returns(Task.FromResult(true));

            var result = await _controller.ChangeEmail(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ChangeEmail_InvalidToken_ReturnsUnauthorized()
        {
            SetupHttpContext(""); // No user id
            var dto = new ChangeEmailDto { NewEmail = "new@test.com" };

            var result = await _controller.ChangeEmail(dto);

            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task VerifyEmailChange_ValidToken_ReturnsOk()
        {
            _mockUserService.Setup(s => s.VerifyEmailChangeAsync("token")).ReturnsAsync(true);

            var result = await _controller.VerifyEmailChange("token");

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task VerifyEmailChange_InvalidToken_ReturnsBadRequest()
        {
            _mockUserService.Setup(s => s.VerifyEmailChangeAsync("token")).ReturnsAsync(false);

            var result = await _controller.VerifyEmailChange("token");

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task ForgotPassword_ValidRequest_ReturnsOk()
        {
            var dto = new ForgotPasswordDto { Email = "test@test.com" };
            _mockUserService.Setup(s => s.ForgotPasswordAsync(dto.Email)).ReturnsAsync(true);

            var result = await _controller.ForgotPassword(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ResetPassword_ValidRequest_ReturnsOk()
        {
            var dto = new ResetPasswordDto { Token = "token", NewPassword = "pwd" };
            _mockUserService.Setup(s => s.ResetPasswordAsync(dto)).ReturnsAsync(true);

            var result = await _controller.ResetPassword(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ResetPassword_InvalidRequest_ReturnsBadRequest()
        {
            var dto = new ResetPasswordDto { Token = "token", NewPassword = "pwd" };
            _mockUserService.Setup(s => s.ResetPasswordAsync(dto)).ReturnsAsync(false);

            var result = await _controller.ResetPassword(dto);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public void ChangePassword_ValidRequest_ReturnsOk()
        {
            SetupHttpContext("1");
            var dto = new ChangePasswordDto { OldPassword = "old", NewPassword = "new" };
            _mockUserService.Setup(s => s.ChangePassword(1, dto)).Returns(true);

            var result = _controller.ChangePassword(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetCurrentSession_ValidToken_ReturnsOk()
        {
            SetupHttpContext();
            var session = new UserSession { Id = 1 };
            _mockUserService.Setup(s => s.GetCurrentSessionAsync("token")).ReturnsAsync(session);

            var result = await _controller.GetCurrentSession("token");

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetCurrentSession_InvalidToken_ReturnsNotFound()
        {
            SetupHttpContext();
            _mockUserService.Setup(s => s.GetCurrentSessionAsync("token")).ReturnsAsync((UserSession)null);

            var result = await _controller.GetCurrentSession("token");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Logout_ValidRequest_ReturnsOk()
        {
            SetupHttpContext();
            var dto = new RefreshTokenRequestDto { RefreshToken = "token" };
            _mockUserService.Setup(s => s.LogoutSessionAsync(dto.RefreshToken)).ReturnsAsync(true);

            var result = await _controller.Logout(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetSessions_ValidRequest_ReturnsOk()
        {
            SetupHttpContext("1");
            var sessions = new List<UserSession> { new UserSession { Id = 1 } };
            _mockUserService.Setup(s => s.GetActiveSessionsAsync(1)).ReturnsAsync(sessions);

            var result = await _controller.GetSessions();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task RevokeSession_ValidRequest_ReturnsOk()
        {
            SetupHttpContext("1");
            _mockUserService.Setup(s => s.RevokeSessionAsync(1, 1)).ReturnsAsync(true);

            var result = await _controller.RevokeSession(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task RevokeSession_InvalidRequest_ReturnsNotFound()
        {
            SetupHttpContext("1");
            _mockUserService.Setup(s => s.RevokeSessionAsync(1, 1)).ReturnsAsync(false);

            var result = await _controller.RevokeSession(1);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task VerifyEmail_ValidToken_ReturnsOk()
        {
            _mockUserService.Setup(s => s.VerifyEmailAsync("token")).ReturnsAsync(true);

            var result = await _controller.VerifyEmail("token");

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task VerifyEmail_InvalidToken_ReturnsBadRequest()
        {
            _mockUserService.Setup(s => s.VerifyEmailAsync("token")).ReturnsAsync(false);

            var result = await _controller.VerifyEmail("token");

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}
