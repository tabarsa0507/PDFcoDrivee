using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface IFileService
    {
        Task<List<FileItem>> GetAllAsync();
        Task<List<FileItem>> GetByUserAsync(string userId);
        Task<List<FileItem>> GetByFolderAsync(int folderId);
        Task<FileItem?> GetByIdAsync(int id);
        Task<(bool success, string message, int? id)> CreateAsync(FileItem file);
        Task<(bool success, string message)> DeleteAsync(int id, string userId, string role);
        Task<(bool success, string message)> RenameAsync(int id, string newName);
        Task<(bool success, string message)> MoveAsync(int id, int? targetFolderId);
        Task<(bool success, string message, int? newId)> CopyAsync(int fileId, int? targetFolderId, string userId);
        Task<(bool success, string message)> ToggleFavoriteAsync(int id);
        Task IncrementDownloadAsync(int id);
        Task IncrementViewAsync(int id);
    }

    public class FileService : IFileService
    {
        private readonly IJsonStorageService _storage;

        public FileService(IJsonStorageService storage)
        {
            _storage = storage;
        }

        public async Task<List<FileItem>> GetAllAsync() => await _storage.GetFilesAsync();

        public async Task<List<FileItem>> GetByUserAsync(string userId)
        {
            var files = await _storage.GetFilesAsync();
            return files.Where(f => f.UploadedByUserId == userId)
                .OrderByDescending(f => f.CreatedAt).ToList();
        }

        public async Task<List<FileItem>> GetByFolderAsync(int folderId)
        {
            var files = await _storage.GetFilesAsync();
            return files.Where(f => f.FolderId == folderId)
                .OrderByDescending(f => f.CreatedAt).ToList();
        }

        public async Task<FileItem?> GetByIdAsync(int id)
        {
            var files = await _storage.GetFilesAsync();
            return files.FirstOrDefault(f => f.Id == id);
        }

        public async Task<(bool success, string message, int? id)> CreateAsync(FileItem file)
        {
            var files = await _storage.GetFilesAsync();
            file.Id = files.Count > 0 ? files.Max(f => f.Id) + 1 : 1;
            file.CreatedAt = DateTime.Now;
            files.Add(file);
            await _storage.SaveFilesAsync(files);
            return (true, "File added", file.Id);
        }

        public async Task<(bool success, string message)> DeleteAsync(int id, string userId, string role)
        {
            var files = await _storage.GetFilesAsync();
            var file = files.FirstOrDefault(f => f.Id == id);
            if (file == null) return (false, "File not found");
            if (file.UploadedByUserId != userId && role != "Admin")
                return (false, "No permission");

            files.Remove(file);
            await _storage.SaveFilesAsync(files);
            return (true, "File deleted");
        }

        public async Task<(bool success, string message)> RenameAsync(int id, string newName)
        {
            var files = await _storage.GetFilesAsync();
            var file = files.FirstOrDefault(f => f.Id == id);
            if (file == null) return (false, "File not found");
            if (string.IsNullOrWhiteSpace(newName)) return (false, "Name required");

            file.DisplayName = newName.Trim();
            await _storage.SaveFilesAsync(files);
            return (true, "Renamed");
        }

        // ============================================
        // انتقال فایل به پوشه جدید (فقط FolderId عوض می‌شه)
        // ============================================
        public async Task<(bool success, string message)> MoveAsync(int id, int? targetFolderId)
        {
            var files = await _storage.GetFilesAsync();
            var file = files.FirstOrDefault(f => f.Id == id);
            if (file == null) return (false, "File not found");

            // فقط FolderId رو عوض کن - فایل خودش روی دیسک سر جاشه
            file.FolderId = targetFolderId;
            await _storage.SaveFilesAsync(files);

            return (true, "File moved");
        }

        // ============================================
        // کپی فایل (رکورد جدید ساخته می‌شه)
        // ============================================
        public async Task<(bool success, string message, int? newId)> CopyAsync(int fileId, int? targetFolderId, string userId)
        {
            var files = await _storage.GetFilesAsync();
            var original = files.FirstOrDefault(f => f.Id == fileId);

            if (original == null)
                return (false, "File not found", null);

            var newFile = new FileItem
            {
                Id = files.Count > 0 ? files.Max(f => f.Id) + 1 : 1,
                DisplayName = original.DisplayName,
                OriginalNameHash = original.OriginalNameHash,
                StoredName = original.StoredName,
                RelativePath = original.RelativePath,
                ContentHash = original.ContentHash,
                SizeBytes = original.SizeBytes,
                Extension = original.Extension,
                Description = original.Description,
                CategoryId = original.CategoryId,
                FolderId = targetFolderId,
                UploadedByUserId = userId,
                UploadedByUserName = original.UploadedByUserName,
                CreatedAt = DateTime.Now,
                DownloadCount = 0,
                ViewCount = 0
            };

            files.Add(newFile);
            await _storage.SaveFilesAsync(files);

            return (true, "File copied", newFile.Id);
        }

        public async Task<(bool success, string message)> ToggleFavoriteAsync(int id)
        {
            var files = await _storage.GetFilesAsync();
            var file = files.FirstOrDefault(f => f.Id == id);
            if (file == null) return (false, "File not found");

            file.IsFavorite = !file.IsFavorite;
            await _storage.SaveFilesAsync(files);
            return (true, file.IsFavorite ? "Added to favorites" : "Removed from favorites");
        }

        public async Task IncrementDownloadAsync(int id)
        {
            var files = await _storage.GetFilesAsync();
            var file = files.FirstOrDefault(f => f.Id == id);
            if (file != null)
            {
                file.DownloadCount++;
                await _storage.SaveFilesAsync(files);
            }
        }

        public async Task IncrementViewAsync(int id)
        {
            var files = await _storage.GetFilesAsync();
            var file = files.FirstOrDefault(f => f.Id == id);
            if (file != null)
            {
                file.ViewCount++;
                await _storage.SaveFilesAsync(files);
            }
        }
    }
}