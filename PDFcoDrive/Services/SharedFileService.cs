using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface ISharedFileService
    {
        Task<List<SharedFile>> GetSharedWithMeAsync(string userId);
        Task<List<SharedFile>> GetSharedByMeAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<(bool success, string message)> ShareAsync(SharedFile sharedFile);
        Task<(bool success, string message)> MarkAsReadAsync(int id, string userId);
        Task<(bool success, string message)> DeleteAsync(int id, string userId);
        Task<SharedFile?> GetByIdAsync(int id);
    }

    public class SharedFileService : ISharedFileService
    {
        private readonly IJsonStorageService _storage;

        public SharedFileService(IJsonStorageService storage)
        {
            _storage = storage;
        }

        public async Task<List<SharedFile>> GetSharedWithMeAsync(string userId)
        {
            var shared = await _storage.GetSharedFilesAsync();
            return shared
                .Where(s => s.SharedWithUserId == userId)
                .OrderByDescending(s => s.SharedAt)
                .ToList();
        }

        public async Task<List<SharedFile>> GetSharedByMeAsync(string userId)
        {
            var shared = await _storage.GetSharedFilesAsync();
            return shared
                .Where(s => s.SharedByUserId == userId)
                .OrderByDescending(s => s.SharedAt)
                .ToList();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            var shared = await _storage.GetSharedFilesAsync();
            return shared.Count(s => s.SharedWithUserId == userId && !s.IsRead);
        }

        public async Task<(bool success, string message)> ShareAsync(SharedFile sharedFile)
        {
            if (string.IsNullOrEmpty(sharedFile.SharedWithUserId))
                return (false, "گیرنده انتخاب نشده");

            if (sharedFile.SharedWithUserId == sharedFile.SharedByUserId)
                return (false, "نمی‌توانید برای خودتان بفرستید");

            var shared = await _storage.GetSharedFilesAsync();

            // چک تکراری
            if (shared.Any(s => s.FileItemId == sharedFile.FileItemId
                                && s.SharedWithUserId == sharedFile.SharedWithUserId
                                && s.SharedByUserId == sharedFile.SharedByUserId))
            {
                return (false, "این فایل قبلاً برای این کاربر ارسال شده");
            }

            sharedFile.Id = shared.Count > 0 ? shared.Max(s => s.Id) + 1 : 1;
            sharedFile.SharedAt = DateTime.Now;
            sharedFile.IsRead = false;

            shared.Add(sharedFile);
            await _storage.SaveSharedFilesAsync(shared);

            return (true, "فایل با موفقیت به اشتراک گذاشته شد");
        }

        public async Task<(bool success, string message)> MarkAsReadAsync(int id, string userId)
        {
            var shared = await _storage.GetSharedFilesAsync();
            var item = shared.FirstOrDefault(s => s.Id == id);

            if (item == null) return (false, "یافت نشد");
            if (item.SharedWithUserId != userId) return (false, "دسترسی ندارید");

            if (!item.IsRead)
            {
                item.IsRead = true;
                item.ReadAt = DateTime.Now;
                await _storage.SaveSharedFilesAsync(shared);
            }

            return (true, "خوانده شد");
        }

        public async Task<(bool success, string message)> DeleteAsync(int id, string userId)
        {
            var shared = await _storage.GetSharedFilesAsync();
            var item = shared.FirstOrDefault(s => s.Id == id);

            if (item == null) return (false, "یافت نشد");
            if (item.SharedWithUserId != userId && item.SharedByUserId != userId)
                return (false, "دسترسی ندارید");

            shared.Remove(item);
            await _storage.SaveSharedFilesAsync(shared);

            return (true, "حذف شد");
        }

        public async Task<SharedFile?> GetByIdAsync(int id)
        {
            var shared = await _storage.GetSharedFilesAsync();
            return shared.FirstOrDefault(s => s.Id == id);
        }
    }
}