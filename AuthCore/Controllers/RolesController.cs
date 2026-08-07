using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthCore.Interfaces;
using AuthCore.Models.Dto;

namespace AuthCore.Controllers
{
    [Route("api/roles")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        /// <summary>
        /// Retrieves a comprehensive list of all defined user roles in the system.
        /// </summary>
        /// <remarks>
        /// This endpoint returns all system roles (e.g., `Admin`, `User`, `Manager`) along with their 
        /// unique identifiers. Often utilized by admin dashboards to populate role-assignment dropdowns or permission tables.
        /// </remarks>
        /// <returns>A collection of role records currently configured in the database.</returns>
        /// <response code="200">The list of roles was successfully retrieved.</response>
        /// <response code="401">Unauthorized access (A valid JWT Bearer token is required).</response>
        /// <response code="403">Forbidden: The authenticated user lacks the required administrative permissions.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetAllRoles()
        {
            var roles = _roleService.GetAllRoles();
            return Ok(roles);
        }
    }
}