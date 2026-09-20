namespace PDFcoDrive.Models
{
    public class TrashItem
    {
        public int Id { get; set; }
        public string ItemType { get; set; } = "File";
        public int OriginalId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string StoredName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Extension { get; set; } = string.Empty;
        public int? OriginalFolderId { get; set; }
        public string DeletedByUserId { get; set; } = string.Empty;
        public string DeletedByUserName { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddDays(30);
    }
}