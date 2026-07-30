using System.ComponentModel.DataAnnotations;

namespace StajBackendProject.Models.Dto
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}