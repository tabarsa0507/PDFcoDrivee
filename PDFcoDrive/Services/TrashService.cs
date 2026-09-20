using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface ITrashService
    {
        Task<List<TrashItem>> GetAllAsync();
        Task<List<TrashItem>> GetByUserAsync(string userId);
        Task<(bool success, string message)> MoveToTrashAsync(FileItem file, string userId, string userName);
        Task<(bool success, string message)> RestoreAsync(int trashId, string userId);
        Task<(bool success, string message)> PermanentDeleteAsync(int trashId, string userId);
        Task CleanupExpiredAsync();
        Task<int> GetCountAsync();
    }

    public class TrashService : ITrashService
    {
        private readonly IJsonStorageService _storage;
        private readonly IFileService _fileService;

        public TrashService(IJsonStorageService storage, IFileService fileService)
        {
            _storage = storage;
            _fileService = fileService;
        }

        public async Task<List<TrashItem>> GetAllAsync()
        {
            var trash = await _storage.GetTrashAsync();
            return trash.OrderByDescending(t => t.DeletedAt).ToList();
        }

        public async Task<List<TrashItem>> GetByUserAsync(string userId)
        {
            var trash = await _storage.GetTrashAsync();
            return trash.Where(t => t.DeletedByUserId == userId)
                .OrderByDescending(t => t.DeletedAt).ToList();
        }

        public async Task<(bool success, string message)> MoveToTrashAsync(FileItem file, string userId, string userName)
        {
            var trash = await _storage.GetTrashAsync();
            var trashItem = new TrashItem
            {
                Id = trash.Count > 0 ? trash.Max(t => t.Id) + 1 : 1,
                ItemType = "File",
                OriginalId = file.Id,
                DisplayName = file.DisplayName,
                StoredName = file.StoredName,
                RelativePath = file.RelativePath,
                SizeBytes = file.SizeBytes,
                Extension = file.Extension,
                OriginalFolderId = file.FolderId,
                DeletedByUserId = userId,
                DeletedByUserName = userName,
                DeletedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(30)
            };

            trash.Add(trashItem);
            await _storage.SaveTrashAsync(trash);

            var files = await _storage.GetFilesAsync();
            var original = files.FirstOrDefault(f => f.Id == file.Id);
            if (original != null)
            {
                files.Remove(original);
                await _storage.SaveFilesAsync(files);
            }

            return (true, "Ø¨Ù‡ Ø³Ø¨Ø¯ Ø¨Ø§Ø²ÛŒØ§ÙØª Ù…Ù†ØªÙ‚Ù„ Ø´Ø¯");
        }

        public async Task<(bool success, string message)> RestoreAsync(int trashId, string userId)
        {
            var trash = await _storage.GetTrashAsync();
            var item = trash.FirstOrDefault(t => t.Id == trashId);
            if (item == null) return (false, "Ø¢ÛŒØªÙ… ÛŒØ§ÙØª Ù†Ø´Ø¯");
            if (item.DeletedByUserId != userId) return (false, "Ø¯Ø³ØªØ±Ø³ÛŒ Ù†Ø¯Ø§Ø±ÛŒØ¯");

            var file = new FileItem
            {
                DisplayName = item.DisplayName,
                StoredName = item.StoredName,
                RelativePath = item.RelativePath,
                SizeBytes = item.SizeBytes,
                Extension = item.Extension,
                FolderId = item.OriginalFolderId,
                UploadedByUserId = userId,
                UploadedByUserName = item.DeletedByUserName,
                CreatedAt = DateTime.Now
            };

            await _fileService.CreateAsync(file);
            trash.Remove(item);
            await _storage.SaveTrashAsync(trash);

            return (true, "ÙØ§ÛŒÙ„ Ø¨Ø§Ø²ÛŒØ§Ø¨ÛŒ Ø´Ø¯");
        }

        public async Task<(bool success, string message)> PermanentDeleteAsync(int trashId, string userId)
        {
            var trash = await _storage.GetTrashAsync();
            var item = trash.FirstOrDefault(t => t.Id == trashId);
            if (item == null) return (false, "Ø¢ÛŒØªÙ… ÛŒØ§ÙØª Ù†Ø´Ø¯");
            if (item.DeletedByUserId != userId) return (false, "Ø¯Ø³ØªØ±Ø³ÛŒ Ù†Ø¯Ø§Ø±ÛŒØ¯");

            trash.Remove(item);
            await _storage.SaveTrashAsync(trash);

            return (true, "Ø¨Ø±Ø§ÛŒ Ù‡Ù…ÛŒØ´Ù‡ Ø­Ø°Ù Ø´Ø¯");
        }

        public async Task CleanupExpiredAsync()
        {
            var trash = await _storage.GetTrashAsync();
            var expired = trash.Where(t => t.ExpiresAt < DateTime.Now).ToList();

            if (expired.Any())
            {
                foreach (var item in expired)
                    trash.Remove(item);

                await _storage.SaveTrashAsync(trash);
            }
        }

        public async Task<int> GetCountAsync()
        {
            var trash = await _storage.GetTrashAsync();
            return trash.Count;
        }
    }
}