namespace PDFcoDrive.Models
{
    public class SharedFile
    {
        public int Id { get; set; }

        public int FileItemId { get; set; }

        public string SharedByUserId { get; set; } = "";
        public string SharedByUserName { get; set; } = "";

        public string SharedWithUserId { get; set; } = "";
        public string SharedWithUserName { get; set; } = "";

        public string? Message { get; set; }

        public DateTime SharedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}