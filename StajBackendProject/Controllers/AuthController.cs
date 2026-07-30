using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajBackendProject.Interfaces;
using StajBackendProject.Models.Dto;

namespace StajBackendProject.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;

        public AuthController(IUserService userService, ITokenService tokenService)
        {
            _userService = userService;
            _tokenService = tokenService;
        }

        // POST: api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] AddNewUserDto dto)
        {
            await _userService.AddNewUserAsync(dto);
            return Ok(new { Message = "User successfully registered." });
        }

        // POST : api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var deviceName = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _userService.LoginAsync(request.Email, request.Password, ipAddress, deviceName);

            if (result.Success)
            {
                return Ok(result);
            }

            if (result.LockoutEnd.HasValue)
            {
                return StatusCode(StatusCodes.Status423Locked, result);
            }

            return Unauthorized(result);
        }

        // POST : api/auth/refresh-token
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            var tokens = _tokenService.RefreshToken(request);
            return Ok(tokens);
        }

        // POST : api/auth/change-email
        [HttpPost("change-email")]
        [Authorize]
        public IActionResult ChangeEmail([FromBody] ChangeEmailDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new { Message = "Invalid or expired token." });
            }

            _userService.ChangeEmail(userId, request);

            return Ok(new { Message = "Email address successfully updated." });
        }

        // POST: api/auth/forgot-password
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            await _userService.ForgotPasswordAsync(request.Email);

            return Ok(new { Message = "If there is an active account registered with this email address in our system, a password reset link has been sent." });
        }

        // POST: api/auth/reset-password
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var success = await _userService.ResetPasswordAsync(request);

            if (!success)
            {
                return BadRequest(new { Message = "Invalid or expired reset token." });
            }

            return Ok(new { Message = "Your password has been successfully reset. You can log in with your new password." });
        }

        // POST : api/auth/change-password
        [HttpPost("change-password")]
        [Authorize]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new { Message = "Invalid or expired token." });
            }

            _userService.ChangePassword(userId, request);

            return Ok(new { Message = "Password changed successfully." });
        }

        // GET: api/auth/session
        [HttpGet("session")]
        [Authorize]
        public async Task<IActionResult> GetCurrentSession([FromQuery] string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return BadRequest(new { Message = "Refresh token parameter is required." });
            }

            var session = await _userService.GetCurrentSessionAsync(refreshToken);

            if (session == null)
            {
                return NotFound(new { Message = "No active session was found or the session has expired." });
            }

            return Ok(new
            {
                session.Id,
                session.UserId,
                session.DeviceName,
                session.IpAddress,
                session.CreatedAt,
                session.ExpiryDate
            });
        }

        // DELETE: api/auth/session
        [HttpDelete("session")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
        {
            await _userService.LogoutSessionAsync(request.RefreshToken);
            return Ok(new { Message = "Session was successfully revoked." });
        }

        // GET: api/auth/sessions
        [HttpGet("sessions")]
        [Authorize]
        public async Task<IActionResult> GetSessions()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var sessions = await _userService.GetActiveSessionsAsync(userId);

            return Ok(sessions.Select(s => new {
                s.Id,
                s.DeviceName,
                s.IpAddress,
                s.CreatedAt,
                s.ExpiryDate
            }));
        }

        // DELETE: api/auth/sessions/{id}
        [HttpDelete("sessions/{id}")]
        [Authorize]
        public async Task<IActionResult> RevokeSession(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _userService.RevokeSessionAsync(userId, id);

            if (!success) return NotFound(new { Message = "Session not found or already cancelled." });
            return Ok(new { Message = "Session was successfully terminated." });
        }

        // GET: api/auth/verify-email?token=3F4B9A8C1D...
        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { Message = "A verification token is required." });
            }

            var isSuccess = await _userService.VerifyEmailAsync(token);

            if (!isSuccess)
            {
                return BadRequest(new { Message = "Invalid or expired verification link!" });
            }

            return Ok(new { Message = "Your email address has been successfully verified! You can now log in." });
        }
    }
}