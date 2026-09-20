using System.ComponentModel.DataAnnotations;

namespace PDFcoDrive.Models
{
    public class FileItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        public string OriginalNameHash { get; set; } = string.Empty;

        [Required]
        public string StoredName { get; set; } = string.Empty;

        [Required]
        public string RelativePath { get; set; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string ContentHash { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        [StringLength(10)]
        public string Extension { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int? CategoryId { get; set; }

        public int? FolderId { get; set; }

        public string UploadedByUserId { get; set; } = string.Empty;

        public string UploadedByUserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int DownloadCount { get; set; } = 0;

        public int ViewCount { get; set; } = 0;

        public bool IsFavorite { get; set; } = false;

        public List<string> Tags { get; set; } = new();
    }
}