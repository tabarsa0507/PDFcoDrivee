using System.ComponentModel.DataAnnotations;

namespace PDFcoDrive.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string Icon { get; set; } = "bi-folder-fill";

        public string Color { get; set; } = "#0ea5e9";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int FileCount { get; set; } = 0;
    }
}