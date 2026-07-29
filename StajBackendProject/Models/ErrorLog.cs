namespace StajBackendProject.Models
{
    public class ErrorLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string Path { get; set; }
        public string Method { get; set; }
        public string ExceptionMessage { get; set; }
        public string? StackTrace { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}