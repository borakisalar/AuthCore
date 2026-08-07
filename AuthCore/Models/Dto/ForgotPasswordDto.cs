using System.ComponentModel.DataAnnotations;

namespace AuthCore.Models.Dto
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}