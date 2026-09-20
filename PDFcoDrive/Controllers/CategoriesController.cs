using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Models;
using PDFcoDrive.Services;

namespace PDFcoDrive.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly IJsonStorageService _storage;

        public CategoriesController(IJsonStorageService storage)
        {
            _storage = storage;
        }

        // ============================================
        // لیست دسته‌بندی‌ها
        // ============================================
        public async Task<IActionResult> Index()
        {
            var categories = await _storage.GetCategoriesAsync();
            var files = await _storage.GetFilesAsync();

            foreach (var cat in categories)
            {
                cat.FileCount = files.Count(f => f.CategoryId == cat.Id);
            }

            return View(categories.OrderByDescending(c => c.CreatedAt).ToList());
        }

        // ============================================
        // ساخت دسته‌بندی جدید
        // ============================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                var categories = await _storage.GetCategoriesAsync();
                category.Id = categories.Count > 0 ? categories.Max(c => c.Id) + 1 : 1;
                category.CreatedAt = DateTime.Now;

                categories.Add(category);
                await _storage.SaveCategoriesAsync(categories);

                TempData["Success"] = "دسته‌بندی با موفقیت ساخته شد";
                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // ============================================
        // ویرایش
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var categories = await _storage.GetCategoriesAsync();
            var category = categories.FirstOrDefault(c => c.Id == id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var categories = await _storage.GetCategoriesAsync();
                var existing = categories.FirstOrDefault(c => c.Id == id);
                if (existing == null) return NotFound();

                existing.Name = category.Name;
                existing.Description = category.Description;
                existing.Icon = category.Icon;
                existing.Color = category.Color;

                await _storage.SaveCategoriesAsync(categories);

                TempData["Success"] = "دسته‌بندی ویرایش شد";
                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // ============================================
        // حذف
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var categories = await _storage.GetCategoriesAsync();
            var category = categories.FirstOrDefault(c => c.Id == id);
            if (category == null) return NotFound();

            var files = await _storage.GetFilesAsync();
            if (files.Any(f => f.CategoryId == id))
            {
                TempData["Error"] = "این دسته‌بندی دارای فایل است";
                return RedirectToAction(nameof(Index));
            }

            categories.Remove(category);
            await _storage.SaveCategoriesAsync(categories);

            TempData["Success"] = "دسته‌بندی حذف شد";
            return RedirectToAction(nameof(Index));
        }
    }
}