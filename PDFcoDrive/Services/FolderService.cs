using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface IFolderService
    {
        Task<List<Folder>> GetAllAsync();
        Task<Folder?> GetByIdAsync(int id);
        Task<List<Folder>> GetByOwnerAsync(string userId);
        Task<List<Folder>> GetChildrenAsync(int? parentId);
        Task<List<Folder>> GetAccessibleAsync(string userId, string role);
        Task<(bool success, string message, int? id)> CreateAsync(Folder folder);
        Task<(bool success, string message)> UpdateAsync(Folder folder);
        Task<(bool success, string message)> DeleteAsync(int id, string userId, string role);
        bool CanAccess(Folder folder, string userId, string role);
        bool CanUploadToFolder(Folder folder, string userId, string role);
        bool CanManageFolder(Folder folder, string userId, string role);
        Task<List<Folder>> GetBreadcrumbAsync(int folderId);
    }

    public class FolderService : IFolderService
    {
        private readonly IJsonStorageService _storage;

        public FolderService(IJsonStorageService storage)
        {
            _storage = storage;
        }

        public async Task<List<Folder>> GetAllAsync() => await _storage.GetFoldersAsync();

        public async Task<Folder?> GetByIdAsync(int id)
        {
            var folders = await _storage.GetFoldersAsync();
            return folders.FirstOrDefault(f => f.Id == id);
        }

        public async Task<List<Folder>> GetByOwnerAsync(string userId)
        {
            var folders = await _storage.GetFoldersAsync();
            return folders.Where(f => f.OwnerUserId == userId).OrderBy(f => f.Name).ToList();
        }

        public async Task<List<Folder>> GetChildrenAsync(int? parentId)
        {
            var folders = await _storage.GetFoldersAsync();
            return folders.Where(f => f.ParentId == parentId).OrderBy(f => f.Name).ToList();
        }

        public async Task<List<Folder>> GetAccessibleAsync(string userId, string role)
        {
            var folders = await _storage.GetFoldersAsync();
            return folders.Where(f => CanAccess(f, userId, role)).OrderBy(f => f.Name).ToList();
        }

        // ============================================
        // دسترسی به پوشه
        // Admin و Board → همه پوشه‌ها
        // ============================================
        public bool CanAccess(Folder folder, string userId, string role)
        {
            // Admin و Board همه چیز رو می‌بینن
            if (role == "Admin") return true;
            // مالک خودش
            if (folder.OwnerUserId == userId) return true;
            // پوشه‌های سیستمی
            if (folder.SystemType == "Shared") return true;

            return folder.Access switch
            {
                AccessLevel.Public => true,
                AccessLevel.Private => false,
                AccessLevel.Team => folder.AllowedUserIds.Contains(userId),
                AccessLevel.Board => role == "Board",
                _ => false
            };
        }

        // ============================================
        // اجازه آپلود در پوشه
        // Admin و Board → همه جا
        // ============================================
        public bool CanUploadToFolder(Folder folder, string userId, string role)
        {
            // Admin و Board همه جا می‌تونن آپلود کنن
            if (role == "Admin") return true;

            if (folder.OwnerUserId == userId) return true;
            if (folder.Access == AccessLevel.Public) return true;
            if (folder.SystemType == "Shared") return true;
            if (folder.Access == AccessLevel.Team && folder.AllowedUserIds.Contains(userId)) return true;

            return false;
        }

        // ============================================
        // اجازه مدیریت پوشه
        // ============================================
        public bool CanManageFolder(Folder folder, string userId, string role)
        {
            // Admin و Board همه پوشه‌ها رو مدیریت می‌کنن
            if (role == "Admin") return true;

            return folder.OwnerUserId == userId;
        }

        public async Task<(bool success, string message, int? id)> CreateAsync(Folder folder)
        {
            if (string.IsNullOrWhiteSpace(folder.Name))
                return (false, "نام پوشه الزامیست", null);

            var folders = await _storage.GetFoldersAsync();

            if (folders.Any(f => f.ParentId == folder.ParentId &&
                                  f.Name == folder.Name &&
                                  f.OwnerUserId == folder.OwnerUserId))
                return (false, "پوشه‌ای با این نام در این مسیر وجود دارد", null);

            folder.Id = folders.Count > 0 ? folders.Max(f => f.Id) + 1 : 1;
            folder.CreatedAt = DateTime.Now;
            folders.Add(folder);
            await _storage.SaveFoldersAsync(folders);

            return (true, "پوشه ساخته شد", folder.Id);
        }

        public async Task<(bool success, string message)> UpdateAsync(Folder folder)
        {
            var folders = await _storage.GetFoldersAsync();
            var existing = folders.FirstOrDefault(f => f.Id == folder.Id);
            if (existing == null) return (false, "پوشه یافت نشد");

            existing.Name = folder.Name;
            existing.Description = folder.Description;
            existing.Icon = folder.Icon;
            existing.Color = folder.Color;
            existing.Access = folder.Access;
            existing.AllowedUserIds = folder.AllowedUserIds;

            await _storage.SaveFoldersAsync(folders);
            return (true, "پوشه ویرایش شد");
        }

        public async Task<(bool success, string message)> DeleteAsync(int id, string userId, string role)
        {
            var folders = await _storage.GetFoldersAsync();
            var folder = folders.FirstOrDefault(f => f.Id == id);
            if (folder == null) return (false, "پوشه یافت نشد");
            if (folder.IsSystem) return (false, "پوشه سیستمی قابل حذف نیست");

            // Admin و Board می‌تونن حذف کنن
            if (folder.OwnerUserId != userId && role != "Admin" && role != "Board")
                return (false, "اجازه حذف ندارید");

            if (folders.Any(f => f.ParentId == id))
                return (false, "ابتدا زیرپوشه‌ها را حذف کنید");

            var files = await _storage.GetFilesAsync();
            if (files.Any(f => f.FolderId == id))
                return (false, "ابتدا فایل‌های داخل پوشه را حذف کنید");

            folders.Remove(folder);
            await _storage.SaveFoldersAsync(folders);
            return (true, "پوشه حذف شد");
        }

        public async Task<List<Folder>> GetBreadcrumbAsync(int folderId)
        {
            var folders = await _storage.GetFoldersAsync();
            var breadcrumb = new List<Folder>();

            var current = folders.FirstOrDefault(f => f.Id == folderId);
            while (current != null)
            {
                breadcrumb.Insert(0, current);
                current = current.ParentId.HasValue
                    ? folders.FirstOrDefault(f => f.Id == current.ParentId.Value)
                    : null;
            }
            return breadcrumb;
        }
    }
}