using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Models;
using PDFcoDrive.Services;
using System.Security.Claims;

namespace PDFcoDrive.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IJsonStorageService _storage;
        private readonly IFolderService _folders;
        private readonly ISharedFileService _shared;

        public HomeController(
            IJsonStorageService storage,
            IFolderService folders,
            ISharedFileService shared)
        {
            _storage = storage;
            _folders = folders;
            _shared = shared;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        public async Task<IActionResult> Index()
        {
            var allFiles = await _storage.GetFilesAsync();
            var allFolders = await _storage.GetFoldersAsync();
            var users = await _storage.GetUsersAsync();
            var trash = await _storage.GetTrashAsync();

            // فقط فایل‌های خودم
            var myFiles = allFiles
                .Where(f => f.UploadedByUserId == CurrentUserId)
                .ToList();

            var myRootFolders = allFolders
                .Where(f => f.OwnerUserId == CurrentUserId && f.ParentId == null)
                .ToList();

            ViewBag.TotalFiles = myFiles.Count;
            ViewBag.TotalDownloads = myFiles.Sum(f => f.DownloadCount);
            ViewBag.TotalCategories = (await _storage.GetCategoriesAsync()).Count;
            ViewBag.TotalUsers = users.Count;
            ViewBag.TotalFolders = myRootFolders.Count;
            ViewBag.TotalTrash = trash.Where(t => t.DeletedByUserId == CurrentUserId).Count();
            ViewBag.TotalSize = myFiles.Sum(f => f.SizeBytes);

            var latestFiles = myFiles
                .OrderByDescending(f => f.CreatedAt)
                .Take(6)
                .ToList();
            ViewBag.LatestFiles = latestFiles;

            var topDownloads = myFiles
                .OrderByDescending(f => f.DownloadCount)
                .Take(5)
                .ToList();
            ViewBag.TopDownloads = topDownloads;

            // فایل‌های به اشتراک گذاشته شده با من
            var sharedWithMe = await _shared.GetSharedWithMeAsync(CurrentUserId);
            var sharedListView = new List<SharedFileView>();

            foreach (var s in sharedWithMe)
            {
                var file = allFiles.FirstOrDefault(f => f.Id == s.FileItemId);
                if (file != null)
                {
                    sharedListView.Add(new SharedFileView
                    {
                        Shared = s,
                        File = file
                    });
                }
            }

            ViewBag.SharedWithMe = sharedListView;
            ViewBag.UnreadSharedCount = sharedListView.Count(x => !x.Shared.IsRead);

            return View();
        }

        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}