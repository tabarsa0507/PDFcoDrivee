using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Services;
using System.Security.Claims;

namespace PDFcoDrive.Controllers
{
    [Authorize]
    public class TrashController : Controller
    {
        private readonly ITrashService _trash;
        private readonly IConfiguration _config;

        public TrashController(ITrashService trash, IConfiguration config)
        {
            _trash = trash;
            _config = config;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        private string UploadsPath => _config["Storage:UploadsPath"] ?? "D:\\PDFcoDrive\\Uploads";

        // ============================================
        // لیست سبد بازیافت
        // ============================================
        public async Task<IActionResult> Index()
        {
            // پاکسازی خودکار فایل‌های منقضی شده
            await _trash.CleanupExpiredAsync();

            var items = CurrentRole == "Admin"
                ? await _trash.GetAllAsync()
                : await _trash.GetByUserAsync(CurrentUserId);

            return View(items);
        }

        // ============================================
        // بازیابی فایل
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var (success, message) = await _trash.RestoreAsync(id, CurrentUserId);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Index));
        }

        // ============================================
        // حذف دائمی
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, message) = await _trash.PermanentDeleteAsync(id, CurrentUserId);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Index));
        }

        // ============================================
        // پاک کردن همه
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmptyAll()
        {
            var items = CurrentRole == "Admin"
                ? await _trash.GetAllAsync()
                : await _trash.GetByUserAsync(CurrentUserId);

            int count = 0;
            foreach (var item in items.ToList())
            {
                var (success, _) = await _trash.PermanentDeleteAsync(item.Id, CurrentUserId);
                if (success) count++;
            }

            TempData["Success"] = $"{count} مورد برای همیشه حذف شد";
            return RedirectToAction(nameof(Index));
        }
    }
}