namespace PDFcoDrive.Models
{
    public class DownloadLog
    {
        public int Id { get; set; }
        public int FileItemId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string IpHash { get; set; } = string.Empty;
        public DateTime DownloadedAt { get; set; } = DateTime.Now;
    }
}