namespace PDFcoDrive.Models
{
    public class ViewLog
    {
        public int Id { get; set; }
        public int FileItemId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string IpHash { get; set; } = string.Empty;
        public DateTime ViewedAt { get; set; } = DateTime.Now;
    }
}