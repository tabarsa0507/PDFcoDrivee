using System.ComponentModel.DataAnnotations;

namespace PDFcoDrive.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        [StringLength(10, MinimumLength = 10)]
        public string NationalCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string PasswordSalt { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginAt { get; set; }

        public bool IsActive { get; set; } = true;

        public string AvatarColor { get; set; } = "#0ea5e9";
    }
}