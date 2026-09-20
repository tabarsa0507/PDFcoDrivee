using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Services;

namespace PDFcoDrive.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly IAuthService _auth;
        private readonly IJsonStorageService _storage;
        private readonly IHashService _hash;

        public UsersController(IAuthService auth, IJsonStorageService storage, IHashService hash)
        {
            _auth = auth;
            _storage = storage;
            _hash = hash;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _auth.GetAllUsersAsync();
            var files = await _storage.GetFilesAsync();

            ViewBag.FileCounts = files
                .GroupBy(f => f.UploadedByUserId)
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.PepperKey = _hash.GetPepperKey();

            return View(users.OrderByDescending(u => u.CreatedAt).ToList());
        }

        // ============================================
        // ساخت کاربر جدید
        // ============================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string nationalCode, string fullName, string password, string confirmPassword, string role = "User")
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "رمز عبور و تکرار آن یکسان نیستند";
                return View();
            }

            var (success, message) = await _auth.CreateUserAsync(nationalCode, fullName, password, role);

            if (success)
            {
                TempData["Success"] = "created";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Error = message;
            return View();
        }

        // ============================================
        // ویرایش
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _auth.GetUserByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string fullName, string role)
        {
            var (success, message) = await _auth.UpdateUserAsync(id, fullName, role);

            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Error = message;
            var user = await _auth.GetUserByIdAsync(id);
            return View(user);
        }

        // ============================================
        // تغییر رمز
        // ============================================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _auth.GetUserByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "رمز عبور و تکرار آن یکسان نیستند";
                var u = await _auth.GetUserByIdAsync(id);
                return View(u);
            }

            var (success, message) = await _auth.ResetPasswordAsync(id, newPassword);

            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Error = message;
            var user = await _auth.GetUserByIdAsync(id);
            return View(user);
        }

        // ============================================
        // حذف
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _auth.DeleteUserAsync(id);
            if (success)
                TempData["Success"] = "کاربر حذف شد";
            else
                TempData["Error"] = "حذف ناموفق بود (شاید ادمین اصلی باشه)";

            return RedirectToAction(nameof(Index));
        }
    }
}