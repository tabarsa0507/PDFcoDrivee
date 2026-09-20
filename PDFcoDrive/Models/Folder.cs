using System.ComponentModel.DataAnnotations;

namespace PDFcoDrive.Models
{
    public enum AccessLevel
    {
        Private = 0,
        Public = 1,
        Team = 2,
        Board = 3
    }

    public class Folder
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        [Required]
        public string OwnerUserId { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public AccessLevel Access { get; set; } = AccessLevel.Private;

        public List<string> AllowedUserIds { get; set; } = new();

        [StringLength(500)]
        public string? Description { get; set; }

        public string Icon { get; set; } = "bi-folder-fill";

        public string Color { get; set; } = "#0ea5e9";

        public bool IsSystem { get; set; } = false;

        public string? SystemType { get; set; }

        public bool IsFavorite { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}