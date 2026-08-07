using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthCore.Interfaces;
using AuthCore.Models;
using AuthCore.Models.Dto;

namespace AuthCore.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        private readonly IMfaService _mfaService;

        public AuthController(IUserService userService, ITokenService tokenService, IMfaService mfaService)
        {
            _userService = userService;
            _tokenService = tokenService;
            _mfaService = mfaService;
        }

        /// <summary>
        /// Registers a new user in the system and sends an account verification email.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/auth/register
        ///     {
        ///        "firstName": "John",
        ///        "lastName": "Doe",
        ///        "email": "john.doe@example.com",
        ///        "password": "Password123*"
        ///     }
        ///     
        /// Note: The newly registered user starts in an inactive state (`IsActive = false`) until their email is verified.
        /// </remarks>
        /// <param name="request">The user registration payload containing personal and credential details.</param>
        /// <returns>A confirmation message indicating the registration status.</returns>
        /// <response code="200">Registration successful; verification email sent.</response>
        /// <response code="400">The request payload is invalid or contains formatting errors.</response>
        /// <response code="409">The provided email address is already registered in the system.</response>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] AddNewUserDto dto)
        {
            await _userService.AddNewUserAsync(dto);
            return Ok(new { Message = "User successfully registered." });
        }

        /// <summary>
        /// Authenticates a user and returns a JWT Access Token along with a Refresh Token.
        /// </summary>
        /// <remarks>
        /// Upon successful authentication, a new `UserSession` is generated in the database, 
        /// capturing the user's IP address and device information for security auditing.
        /// </remarks>
        /// <param name="request">The login credentials (email and password).</param>
        /// <returns>An object containing the JWT Access Token and Refresh Token.</returns>
        /// <response code="200">Authentication successful.</response>
        /// <response code="400">Invalid email/password, or the account has not been verified yet.</response>
        /// <response code="423">The account is temporarily locked due to excessive failed login attempts.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status423Locked)]
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

        /// <summary>
        /// Issues a new JWT Access Token and Refresh Token pair using a valid Refresh Token.
        /// </summary>
        /// <remarks>
        /// This endpoint implements **Refresh Token Rotation**. When a valid refresh token is submitted, 
        /// it is immediately revoked in the database and replaced with a newly generated one to prevent replay attacks.
        /// </remarks>
        /// <param name="request">The payload containing the expired access token and the active refresh token.</param>
        /// <returns>A new JWT Access Token and Refresh Token pair.</returns>
        /// <response code="200">Tokens successfully refreshed and rotated.</response>
        /// <response code="400">The request payload is invalid or missing required token strings.</response>
        /// <response code="401">The refresh token is expired, invalid, or has already been revoked.</response>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            var tokens = _tokenService.RefreshToken(request);
            return Ok(tokens);
        }

        /// <summary>
        /// Initiates an email change request for an authenticated user.
        /// </summary>
        /// <remarks>
        /// The primary email address is **not** changed immediately. A verification link is sent to the 
        /// **new email address** with a 15-minute expiration timer.
        /// </remarks>
        /// <param name="request">The user's current password and their desired new email address.</param>
        /// <returns>A confirmation message instructing the user to check their new inbox.</returns>
        /// <response code="200">Verification link successfully dispatched to the new email address.</response>
        /// <response code="400">Incorrect password, the new email is identical to the old one, or the new email is already in use.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        [HttpPost("change-email")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new { Message = "Invalid or expired token." });
            }

            await _userService.EmailChangeAsync(userId, request);

            return Ok(new { Message = "A verification link has been sent to your new email address. Please confirm the link to your new address to complete the change." });
        }

        /// <summary>
        /// Confirms an email change request and updates the account's primary email address.
        /// </summary>
        /// <param name="token">The unique email change token sent to the new email address.</param>
        /// <returns>A confirmation message indicating the result of the update.</returns>
        /// <response code="200">Email address successfully updated.</response>
        /// <response code="400">The verification token is invalid, expired, or the email was claimed by another user.</response>
        [HttpGet("verify-email-change")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmailChange([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { Message = "A verification token is required." });
            }

            var success = await _userService.VerifyEmailChangeAsync(token);

            if (!success)
            {
                return BadRequest(new { Message = "Invalid or expired email change token!" });
            }

            return Ok(new { Message = "Your email address has been successfully updated! You can now log in with your new email address." });
        }

        /// <summary>
        /// Initiates a password reset request by generating a token and sending a recovery email.
        /// </summary>
        /// <remarks>
        /// For security reasons (to prevent Email Enumeration attacks), this endpoint always returns a success message 
        /// even if the email address is not registered in the database.
        /// </remarks>
        /// <param name="request">The email address associated with the account.</param>
        /// <returns>A generic confirmation message.</returns>
        /// <response code="200">If the account exists, a password reset email has been dispatched.</response>
        /// <response code="400">The provided email address format is invalid.</response>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            await _userService.ForgotPasswordAsync(request.Email);

            return Ok(new { Message = "If there is an active account registered with this email address in our system, a password reset link has been sent." });
        }

        /// <summary>
        /// Resets the user's password using a valid recovery token.
        /// </summary>
        /// <param name="request">The payload containing the reset token and the new password.</param>
        /// <returns>A confirmation message indicating whether the password reset succeeded.</returns>
        /// <response code="200">Password successfully reset. You can now log in with the new password.</response>
        /// <response code="400">The reset token is invalid, expired (15-minute limit), or the passwords do not match.</response>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var success = await _userService.ResetPasswordAsync(request);

            if (!success)
            {
                return BadRequest(new { Message = "Invalid or expired reset token." });
            }

            return Ok(new { Message = "Your password has been successfully reset. You can log in with your new password." });
        }

        /// <summary>
        /// Updates the password for the currently authenticated user.
        /// </summary>
        /// <remarks>
        /// For security purposes, the user must provide their **current valid password** alongside the new password.
        /// Upon a successful password change, `PasswordLastChangedAt` is updated in the database. 
        /// Optionally, you can revoke all active sessions after password update.
        /// </remarks>
        /// <param name="request">The payload containing the current password, new password, and password confirmation.</param>
        /// <returns>A confirmation message indicating the result of the password update.</returns>
        /// <response code="200">Password successfully updated.</response>
        /// <response code="400">The current password is incorrect, or the new password fails validation/complexity rules.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        /// <summary>
        /// Retrieves metadata about the currently active session of the authenticated user.
        /// </summary>
        /// <remarks>
        /// Extracts the session identifier from the current request context and returns details 
        /// such as the login IP address, device/browser user agent, and creation timestamp.
        /// </remarks>
        /// <returns>Detailed metadata about the current session.</returns>
        /// <response code="200">Current session details retrieved successfully.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="404">The current session record could not be found in the database.</response>
        [HttpGet("session")]
        [Authorize]
        [ProducesResponseType(typeof(UserSession), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        /// <summary>
        /// Terminates the current session (Logout) by revoking its associated Refresh Token.
        /// </summary>
        /// <remarks>
        /// This endpoint effectively logs the user out of the current device. The refresh token tied 
        /// to this specific session is marked as revoked/deleted in the database.
        /// </remarks>
        /// <returns>A confirmation message indicating the session was terminated.</returns>
        /// <response code="200">Current session successfully terminated.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        [HttpDelete("session")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
        {
            await _userService.LogoutSessionAsync(request.RefreshToken);
            return Ok(new { Message = "Session was successfully revoked." });
        }

        /// <summary>
        /// Retrieves a list of all active sessions across all devices for the authenticated user.
        /// </summary>
        /// <remarks>
        /// Useful for account security dashboards where users can inspect where their account is currently logged in 
        /// (e.g., viewing active logins from different mobile devices or web browsers).
        /// </remarks>
        /// <returns>A list of active user session records.</returns>
        /// <response code="200">Active sessions list retrieved successfully.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        [HttpGet("sessions")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<UserSession>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        /// <summary>
        /// Terminates a specific active session by its unique identifier.
        /// </summary>
        /// <remarks>
        /// Allows a user to remotely log out of another device (e.g., revoking an open office computer session 
        /// while using a personal mobile phone). Users can only revoke sessions belonging to their own account.
        /// </remarks>
        /// <param name="id">The unique identifier (`Id`) of the target session to be revoked.</param>
        /// <returns>A confirmation message indicating the target session was terminated.</returns>
        /// <response code="200">The specified session was successfully revoked.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: You cannot revoke a session that belongs to another user.</response>
        /// <response code="404">The specified session ID does not exist or is already inactive.</response>
        [HttpDelete("sessions/{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeSession(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _userService.RevokeSessionAsync(userId, id);

            if (!success) return NotFound(new { Message = "Session not found or already cancelled." });
            return Ok(new { Message = "Session was successfully terminated." });
        }

        /// <summary>
        /// Verifies a newly registered user's email address and activates their account.
        /// </summary>
        /// <param name="token">The unique 64-character verification token sent via email.</param>
        /// <returns>A confirmation message indicating whether the activation succeeded.</returns>
        /// <response code="200">Email successfully verified; account activated.</response>
        /// <response code="400">The verification token is invalid, expired (24-hour limit), or missing.</response>
        [HttpGet("verify-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        /// <summary>
        /// Initiates the MFA setup process by sending a 6-digit verification code to the user's verified email.
        /// </summary>
        /// <remarks>
        /// This is the first step in enabling Multi-Factor Authentication (MFA). 
        /// A user must have a verified email address to proceed. The 6-digit code expires in 5 minutes.
        /// </remarks>
        /// <returns>A confirmation message indicating that the code was sent.</returns>
        /// <response code="200">The verification code was successfully sent.</response>
        /// <response code="400">MFA is already enabled, the email is unverified, or the user was not found.</response>
        [HttpPost("mfa/send-setup-code")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMfaSetupCode()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "Invalid token." });

            try
            {
                var result = await _mfaService.SendMfaSetupEmailAsync(userId);
                if (!result)
                    return BadRequest(new { Message = "MFA is already enabled or user not found." });

                return Ok(new { Message = "A 6-digit verification code has been sent to your email address." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Validates the email verification code and generates the MFA setup configuration (QR Code and Secret).
        /// </summary>
        /// <remarks>
        /// This is the second step in the MFA setup process. The user must provide the 6-digit code received via email.
        /// Upon success, this endpoint returns a QR code URI (for authenticator apps) and a manual entry key.
        /// Note: MFA is **not** fully enabled yet until the user verifies their first authenticator PIN.
        /// </remarks>
        /// <param name="request">Payload containing the 6-digit EmailCode.</param>
        /// <returns>The MFA configuration containing the manual entry key and QR code image URL.</returns>
        /// <response code="200">The MFA setup configuration was successfully generated.</response>
        /// <response code="400">The email code is invalid or expired.</response>
        [HttpPost("mfa/setup")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetupMfa([FromBody] MfaSetupDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "Invalid token." });

            try
            {
                var result = await _mfaService.SetupMfaAsync(userId, request);
                if (result == null)
                    return BadRequest(new { Message = "MFA is already enabled or user not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Finalizes MFA setup by verifying the first PIN generated by the user's authenticator app.
        /// </summary>
        /// <remarks>
        /// This is the final step of the MFA setup process. The user scans the QR code and enters the 6-digit PIN 
        /// from their authenticator app. If valid, MFA is officially enabled on the account, and 10 one-time-use 
        /// recovery codes are generated and returned. **The user must save these codes in a secure location.**
        /// </remarks>
        /// <param name="request">Payload containing the 6-digit authenticator app Code.</param>
        /// <returns>A confirmation message along with a list of 10 recovery codes.</returns>
        /// <response code="200">MFA was successfully enabled and recovery codes were issued.</response>
        /// <response code="400">The authenticator code is invalid, or the setup process failed.</response>
        [HttpPost("mfa/verify")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "Invalid token." });

            var recoveryCodes = await _mfaService.VerifyMfaSetupAsync(userId, request);
            if (recoveryCodes == null)
                return BadRequest(new { Message = "Invalid MFA code or setup failed." });

            return Ok(new { Message = "MFA successfully enabled.", RecoveryCodes = recoveryCodes });
        }

        /// <summary>
        /// Disables Multi-Factor Authentication (MFA) for the current user.
        /// </summary>
        /// <remarks>
        /// To disable MFA, the user must prove their identity by providing a valid 6-digit PIN from their 
        /// authenticator app. Upon success, MFA is disabled, and all associated secrets and recovery codes are deleted.
        /// </remarks>
        /// <param name="request">Payload containing the current 6-digit authenticator app Code.</param>
        /// <returns>A confirmation message indicating MFA was disabled.</returns>
        /// <response code="200">MFA was successfully disabled.</response>
        /// <response code="400">The authenticator code is invalid, or MFA is not currently enabled.</response>
        [HttpPost("mfa/disable")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DisableMfa([FromBody] MfaVerifyDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "Invalid token." });

            var result = await _mfaService.DisableMfaAsync(userId, request);
            if (!result)
                return BadRequest(new { Message = "Invalid MFA code or MFA is not enabled." });

            return Ok(new { Message = "MFA successfully disabled." });
        }

        /// <summary>
        /// Verifies a single-use recovery code.
        /// </summary>
        /// <remarks>
        /// If a user loses access to their authenticator app, they can bypass the TOTP requirement 
        /// by consuming one of their 10 recovery codes. Once used, a recovery code is permanently removed 
        /// from the database and cannot be reused.
        /// </remarks>
        /// <param name="request">Payload containing a single recovery code string.</param>
        /// <returns>A confirmation message indicating successful recovery verification.</returns>
        /// <response code="200">The recovery code was verified successfully.</response>
        /// <response code="400">The recovery code is invalid, already used, or MFA is not enabled.</response>
        [HttpPost("mfa/recovery-code")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyRecoveryCode([FromBody] MfaRecoveryCodeDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "Invalid token." });

            var result = await _mfaService.VerifyRecoveryCodeAsync(userId, request);
            if (!result)
                return BadRequest(new { Message = "Invalid or already used recovery code." });

            return Ok(new { Message = "Recovery code verified successfully." });
        }
    }
}