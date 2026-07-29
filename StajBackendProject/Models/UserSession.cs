namespace StajBackendProject.Models
{
    public class UserSession
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }

        public string RefreshToken { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public string? IpAddress { get; set; }
        public string? DeviceName { get; set; }
        public bool IsRevoked { get; set; } = false;
    }
}