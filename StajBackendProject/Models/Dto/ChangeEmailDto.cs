using System.ComponentModel.DataAnnotations;

namespace StajBackendProject.Models.Dto
{
    public class ChangeEmailDto
    {
        [Required(ErrorMessage = "New email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string NewEmail { get; set; }

        [Required(ErrorMessage = "Password is required to change your email.")]
        public string Password { get; set; }
    }
}
