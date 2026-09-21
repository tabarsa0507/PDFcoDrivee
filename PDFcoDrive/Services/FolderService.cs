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
        Task<bool> CanAccessAsync(Folder folder, string userId, string role);
        bool CanUploadToFolder(Folder folder, string userId, string role);
        bool CanManageFolder(Folder folder, string userId, string role);
        Task<List<Folder>> GetBreadcrumbAsync(int folderId);
    }

    public class FolderService : IFolderService
    {
        private readonly IJsonStorageService _storage;
        private readonly IGroupService _groups;

        public FolderService(IJsonStorageService storage, IGroupService groups)
        {
            _storage = storage;
            _groups = groups;
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
            var result = new List<Folder>();

            foreach (var folder in folders)
            {
                if (await CanAccessAsync(folder, userId, role))
                    result.Add(folder);
            }

            return result.OrderBy(f => f.Name).ToList();
        }

        // ============================================
        // قوانین دسترسی
        // Admin: همه چیز
        // Board: همه چیز بجز پوشه Private عضو Board دیگه
        // User: پوشه خودش + عمومی + گروه‌ها
        // ============================================
        public async Task<bool> CanAccessAsync(Folder folder, string userId, string role)
        {
            // مالک خودش
            if (folder.OwnerUserId == userId) return true;

            // Admin → همه چیز
            if (role == "Admin") return true;

            // دریافت نقش مالک
            var users = await _storage.GetUsersAsync();
            var owner = users.FirstOrDefault(u => u.Id == folder.OwnerUserId);
            var ownerRole = owner?.Role ?? "User";

            // Board → همه چیز بجز پوشه Private عضو دیگه‌ای Board
            if (role == "Board")
            {
                if (ownerRole == "Board" && folder.Access == AccessLevel.Private)
                    return false;

                // پوشه Private کاربر عادی رو هم نمی‌بینه (فقط Admin)
                if (ownerRole == "User" && folder.Access == AccessLevel.Private)
                    return false;

                return true;
            }

            // کاربر عادی
            if (folder.SystemType == "Shared") return true;

            // گروه
            if (folder.Access == AccessLevel.Group)
            {
                var userGroups = await _groups.GetByUserAsync(userId);
                var userGroupIds = userGroups.Select(g => g.Id).ToList();
                return folder.AllowedGroupIds.Any(gid => userGroupIds.Contains(gid));
            }

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
        // اجازه آپلود
        // ============================================
        public bool CanUploadToFolder(Folder folder, string userId, string role)
        {
            // مالک
            if (folder.OwnerUserId == userId) return true;

            // Admin همه جا
            if (role == "Admin") return true;

            // Board همه جا بجز پوشه Private
            if (role == "Board" && folder.Access != AccessLevel.Private) return true;

            // کاربر عادی
            if (folder.Access == AccessLevel.Public) return true;
            if (folder.SystemType == "Shared") return true;
            if (folder.Access == AccessLevel.Team && folder.AllowedUserIds.Contains(userId)) return true;

            return false;
        }

        // ============================================
        // مدیریت پوشه
        // ============================================
        public bool CanManageFolder(Folder folder, string userId, string role)
        {
            if (folder.OwnerUserId == userId) return true;
            if (role == "Admin") return true;
            if (role == "Board" && folder.Access != AccessLevel.Private) return true;

            return false;
        }

        public async Task<(bool success, string message, int? id)> CreateAsync(Folder folder)
        {
            if (string.IsNullOrWhiteSpace(folder.Name))
                return (false, "نام پوشه الزامیست", null);

            var folders = await _storage.GetFoldersAsync();

            if (folders.Any(f => f.ParentId == folder.ParentId &&
                                  f.Name == folder.Name &&
                                  f.OwnerUserId == folder.OwnerUserId))
                return (false, "پوشه‌ای با این نام وجود دارد", null);

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
            existing.AllowedGroupIds = folder.AllowedGroupIds;

            await _storage.SaveFoldersAsync(folders);
            return (true, "پوشه ویرایش شد");
        }

        public async Task<(bool success, string message)> DeleteAsync(int id, string userId, string role)
        {
            var folders = await _storage.GetFoldersAsync();
            var folder = folders.FirstOrDefault(f => f.Id == id);
            if (folder == null) return (false, "پوشه یافت نشد");
            if (folder.IsSystem) return (false, "پوشه سیستمی قابل حذف نیست");

            if (folder.OwnerUserId != userId && role != "Admin")
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