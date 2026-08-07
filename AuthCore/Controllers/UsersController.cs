using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthCore.Interfaces;
using AuthCore.Models.Dto;

namespace AuthCore.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        public UsersController(IUserService userService, ITokenService tokenService)
        {
            _userService = userService;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Retrieves a list of all registered users in the system.
        /// </summary>
        /// <remarks>
        /// Access is restricted to users with **Admin** or **Manager** roles.
        /// Returns standard user profile details across the organization.
        /// </remarks>
        /// <returns>A collection of all registered user profiles.</returns>
        /// <response code="200">User list successfully retrieved.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: Requires Admin or Manager role.</response>
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        /// <summary>
        /// Retrieves detailed information for a specific user by their unique identifier.
        /// </summary>
        /// <param name="id">The unique integer identifier (`Id`) of the target user.</param>
        /// <returns>The profile information of the specified user.</returns>
        /// <response code="200">User details successfully retrieved.</response>
        /// <response code="404">No user exists with the specified unique identifier.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUsersById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            return Ok(user);
        }

        /// <summary>
        /// Administratively creates and provisions a new user account.
        /// </summary>
        /// <remarks>
        /// Access is restricted to users with the **Admin** role. 
        /// Unlike public registration (`/api/auth/register`), administrators can use this endpoint to directly add users into the system.
        /// </remarks>
        /// <param name="dto">The payload containing the new user's personal details and credentials.</param>
        /// <returns>A confirmation message indicating successful user creation.</returns>
        /// <response code="200">User account successfully added.</response>
        /// <response code="400">The request payload is invalid or fails validation rules.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: Requires Admin role.</response>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateUserByAdmin([FromBody] AddNewUserDto dto)
        {
            await _userService.AddNewUserAsync(dto);
            return Ok("User successfully added.");
        }

        /// <summary>
        /// Permanently removes a user account from the database (Hard Delete).
        /// </summary>
        /// <remarks>
        /// Access is restricted to users with the **Admin** role.
        /// This action removes the user record completely. For non-destructive deactivation, use the soft-delete endpoint (`PUT /api/users/{id}/deactivate`).
        /// </remarks>
        /// <param name="id">The unique integer identifier (`Id`) of the user to permanently delete.</param>
        /// <returns>A confirmation response indicating the result of the deletion.</returns>
        /// <response code="200">User account successfully deleted.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: Requires Admin role.</response>
        /// <response code="404">No user found with the specified ID.</response>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DeleteUser(int id)
        {
            bool isDeleted = _userService.DeleteUser(id);
            if (!isDeleted)
            {
                return NotFound("No user found with given id.");
            }
            return Ok("User successfully deleted.");
        }

        /// <summary>
        /// Deactivates a user account without removing it from the database (Soft Delete).
        /// </summary>
        /// <remarks>
        /// Sets the user's active status to false (`IsActive = false`), preventing login while preserving historical data and relations.
        /// </remarks>
        /// <param name="id">The unique integer identifier (`Id`) of the user to deactivate.</param>
        /// <returns>A confirmation message indicating successful deactivation.</returns>
        /// <response code="200">User successfully deactivated.</response>
        /// <response code="404">User not found.</response>
        [HttpPut("{id}/deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult SoftDeleteUserById(int id)
        {
            bool isDeactivated = _userService.SoftDeleteUserById(id);
            if (!isDeactivated)
            {
                return NotFound("User not found.");
            }
            return Ok("User successfully deactivated.");
        }

        /// <summary>
        /// Reactivates a previously deactivated user account.
        /// </summary>
        /// <remarks>
        /// Sets the user's active status to true (`IsActive = true`), restoring their ability to log in and use the system.
        /// </remarks>
        /// <param name="id">The unique integer identifier (`Id`) of the user to activate.</param>
        /// <returns>A confirmation message indicating successful activation.</returns>
        /// <response code="200">User successfully activated.</response>
        /// <response code="404">User not found.</response>
        [HttpPut("{id}/activate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult ActivateUser(int id)
        {
            bool isActivated = _userService.ActivateUser(id);
            if (!isActivated)
            {
                return NotFound("User not found.");
            }
            return Ok("User successfully activated.");
        }

        /// <summary>
        /// Retrieves a list of all users ordered chronologically by their registration/creation date.
        /// </summary>
        /// <returns>A collection of users sorted by date.</returns>
        /// <response code="200">Users successfully retrieved and ordered by date.</response>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        public IActionResult GetAllUsersOrderByDate()
        {
            var users = _userService.GetAllUsersOrderByDate();
            return Ok(users);
        }

        /// <summary>
        /// Decodes and displays the authentication claims and metadata of the currently logged-in user.
        /// </summary>
        /// <remarks>
        /// Extracts user identifier, email address, assigned roles, and exact UTC expiration time directly from the Bearer JWT access token.
        /// </remarks>
        /// <returns>An object containing decoded token claims and expiration details.</returns>
        /// <response code="200">Token successfully decoded.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        [HttpGet("my-info")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetMyTokenInfo()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            var userRoles = User.Claims
                                .Where(c => c.Type == ClaimTypes.Role)
                                .Select(c => c.Value)
                                .ToList();
            var expClaim = User.FindFirstValue("exp");
            DateTime tokenExpirationDate = DateTime.MinValue;

            if (expClaim != null && long.TryParse(expClaim, out long expSeconds))
            {
                tokenExpirationDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            }

            return Ok(new
            {
                Message = "Token successfully decoded!",
                UserId = userId,
                Email = userEmail,
                Roles = userRoles,
                TokenExpiresAt = tokenExpirationDate
            });
        }

        /// <summary>
        /// Assigns a specific system role to a target user.
        /// </summary>
        /// <remarks>
        /// Access is restricted to users with the **Admin** role. 
        /// Links an existing role identifier (`RoleId`) to the specified user.
        /// </remarks>
        /// <param name="id">The unique integer identifier (`Id`) of the target user.</param>
        /// <param name="request">The payload containing the target Role ID to assign.</param>
        /// <returns>A confirmation message indicating successful role assignment.</returns>
        /// <response code="200">Role successfully assigned to the user.</response>
        /// <response code="400">Invalid payload or role assignment failure.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: Requires Admin role.</response>
        [HttpPost("{id}/roles")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult AssignRole(int id, [FromBody] AssignRoleDto request)
        {
            _userService.AssignRoleToUser(id, request.RoleId);
            return Ok(new { Message = "Role successfully assigned to the user." });
        }

        /// <summary>
        /// Revokes and removes a specific system role from a target user.
        /// </summary>
        /// <remarks>
        /// Access is restricted to users with the **Admin** role.
        /// </remarks>
        /// <param name="id">The unique integer identifier (`Id`) of the target user.</param>
        /// <param name="roleId">The unique integer identifier (`RoleId`) of the role to remove.</param>
        /// <returns>A confirmation message indicating successful role removal.</returns>
        /// <response code="200">Role successfully removed from the user.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: Requires Admin role.</response>
        [HttpDelete("{id}/roles/{roleId}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult RemoveRole(int id, int roleId)
        {
            _userService.RemoveRoleFromUser(id, roleId);
            return Ok(new { Message = "Role successfully removed from the user." });
        }

        /// <summary>
        /// Retrieves all users assigned to a specific role using a database Stored Procedure.
        /// </summary>
        /// <remarks>
        /// Access is restricted to users with **Admin** or **Manager** roles.
        /// Executes a dedicated SQL stored procedure (`GetUsersByRoleNameSpAsync`) to fetch users by their role name.
        /// </remarks>
        /// <param name="roleName">The name of the role to filter users by (e.g., "Admin", "Manager", "User").</param>
        /// <returns>A collection of users belonging to the specified role.</returns>
        /// <response code="200">Users successfully retrieved for the specified role.</response>
        /// <response code="401">Unauthorized access (Valid JWT Bearer token required).</response>
        /// <response code="403">Forbidden: Requires Admin or Manager role.</response>
        /// <response code="404">No users were found assigned to the specified role.</response>
        [HttpGet("by-role/{roleName}")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUsersByRole(string roleName)
        {
            var users = await _userService.GetUsersByRoleNameSpAsync(roleName);

            if (users == null || !users.Any())
            {
                return NotFound(new { Message = $"No users with the role {roleName} were found." });
            }

            return Ok(users);
        }
    }
}
