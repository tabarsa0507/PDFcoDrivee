namespace PDFcoDrive.Models
{
    public class FolderWithLevel
    {
        public Folder Folder { get; set; } = null!;
        public int Level { get; set; }
        public int FileCount { get; set; }
        public int ChildCount { get; set; }
    }

    public class UserFolderStats
    {
        public User User { get; set; } = null!;
        public int TotalFolders { get; set; }
        public int RootFolders { get; set; }
        public int TotalFiles { get; set; }
    }
}