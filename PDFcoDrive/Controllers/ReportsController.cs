using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Services;
using System.Security.Claims;

namespace PDFcoDrive.Controllers
{
    [Authorize(Roles = "Admin,Board")]
    public class ReportsController : Controller
    {
        private readonly IJsonStorageService _storage;
        private readonly IAuthService _auth;

        public ReportsController(IJsonStorageService storage, IAuthService auth)
        {
            _storage = storage;
            _auth = auth;
        }

        // ============================================
        // صفحه اصلی گزارش‌ها
        // ============================================
        public async Task<IActionResult> Index(string? type, string? userId, DateTime? from, DateTime? to)
        {
            var files = await _storage.GetFilesAsync();
            var downloadLogs = await _storage.GetDownloadLogsAsync();
            var viewLogs = await _storage.GetViewLogsAsync();
            var users = await _auth.GetAllUsersAsync();
            var folders = await _storage.GetFoldersAsync();

            // فیلتر تاریخ
            if (from.HasValue)
            {
                downloadLogs = downloadLogs.Where(l => l.DownloadedAt >= from.Value).ToList();
                viewLogs = viewLogs.Where(l => l.ViewedAt >= from.Value).ToList();
            }
            if (to.HasValue)
            {
                downloadLogs = downloadLogs.Where(l => l.DownloadedAt <= to.Value).ToList();
                viewLogs = viewLogs.Where(l => l.ViewedAt <= to.Value).ToList();
            }

            // فیلتر کاربر
            if (!string.IsNullOrEmpty(userId))
            {
                downloadLogs = downloadLogs.Where(l => l.UserId == userId).ToList();
                viewLogs = viewLogs.Where(l => l.UserId == userId).ToList();
            }

            // آمار کلی
            ViewBag.TotalFiles = files.Count;
            ViewBag.TotalDownloads = files.Sum(f => f.DownloadCount);
            ViewBag.TotalViews = files.Sum(f => f.ViewCount);
            ViewBag.TotalUsers = users.Count;
            ViewBag.TotalFolders = folders.Count;
            ViewBag.TotalSize = files.Sum(f => f.SizeBytes);

            // آمار امروز
            var today = DateTime.Now.Date;
            ViewBag.TodayDownloads = downloadLogs.Count(l => l.DownloadedAt.Date == today);
            ViewBag.TodayViews = viewLogs.Count(l => l.ViewedAt.Date == today);
            ViewBag.TodayFiles = files.Count(f => f.CreatedAt.Date == today);

            // آخرین لاگ‌ها
            ViewBag.RecentDownloads = downloadLogs
                .OrderByDescending(l => l.DownloadedAt)
                .Take(20)
                .ToList();

            ViewBag.RecentViews = viewLogs
                .OrderByDescending(l => l.ViewedAt)
                .Take(20)
                .ToList();

            // پر دانلودها
            ViewBag.TopDownloads = files
                .OrderByDescending(f => f.DownloadCount)
                .Take(10)
                .ToList();

            // پر بازدیدها
            ViewBag.TopViews = files
                .OrderByDescending(f => f.ViewCount)
                .Take(10)
                .ToList();

            // کاربران
            ViewBag.Users = users;

            // نمودار 7 روز اخیر
            var chartData = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Now.AddDays(-i).Date;
                chartData.Add(new
                {
                    Date = date.ToString("MM/dd"),
                    Day = date.ToString("ddd", new System.Globalization.CultureInfo("fa-IR")),
                    Downloads = downloadLogs.Count(l => l.DownloadedAt.Date == date),
                    Views = viewLogs.Count(l => l.ViewedAt.Date == date),
                    Files = files.Count(f => f.CreatedAt.Date == date)
                });
            }
            ViewBag.ChartData = chartData;

            // فیلترهای فعلی
            ViewBag.FilterUserId = userId;
            ViewBag.FilterFrom = from?.ToString("yyyy-MM-dd");
            ViewBag.FilterTo = to?.ToString("yyyy-MM-dd");

            return View();
        }
    }
}