using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace StajBackendProject.Models.Dto
{
    public class ResetPasswordDto
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least 8 and at most 128 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&.,#])[A-Za-z\d@$!%*?&.,#]{8,128}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string NewPassword { get; set; }

        [Required]
        [Compare("NewPassword", ErrorMessage = "The passwords don't match.")]
        public string ConfirmNewPassword { get; set; }
    }
}