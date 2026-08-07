using System.ComponentModel.DataAnnotations;

namespace AuthCore.Models.Dto
{
    public class AssignRoleDto
    {
        [Required]
        public int RoleId { get; set; }
    }
}
