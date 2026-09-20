using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Models;
using PDFcoDrive.Services;
using System.Security.Claims;

namespace PDFcoDrive.Controllers
{
    [Authorize]
    public class FilesController : Controller
    {
        private readonly IFileService _files;
        private readonly IJsonStorageService _storage;
        private readonly IHashService _hash;
        private readonly ITrashService _trash;
        private readonly IFolderService _folders;
        private readonly IAuthService _auth;
        private readonly IConfiguration _config;

        public FilesController(
            IFileService files,
            IJsonStorageService storage,
            IHashService hash,
            ITrashService trash,
            IFolderService folders,
            IAuthService auth,
            IConfiguration config)
        {
            _files = files;
            _storage = storage;
            _hash = hash;
            _trash = trash;
            _folders = folders;
            _auth = auth;
            _config = config;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        private string CurrentUserName => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        private string UploadsPath => _config["Storage:UploadsPath"] ?? "D:\\PDFcoDrive\\Uploads";
        private long MaxFileSize => (_config.GetValue<int>("Storage:MaxFileSizeMB", 100)) * 1024L * 1024L;
        private string[] AllowedExtensions => _config.GetSection("Storage:AllowedExtensions").Get<string[]>()
            ?? new[] { ".pdf", ".jpg", ".png", ".zip", ".docx", ".xlsx" };

        // ============================================
        // لیست فایل‌ها (فقط Admin و Board)
        // ============================================
        [Authorize(Roles = "Admin,Board")]
        public async Task<IActionResult> Index(int? categoryId, string? search, string? sort)
        {
            var files = await _files.GetAllAsync();

            files = files.Where(f => f.UploadedByUserId == CurrentUserId).ToList();

            if (categoryId.HasValue)
                files = files.Where(f => f.CategoryId == categoryId.Value).ToList();

            if (!string.IsNullOrWhiteSpace(search))
                files = files.Where(f => f.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            files = sort switch
            {
                "name" => files.OrderBy(f => f.DisplayName).ToList(),
                "size" => files.OrderByDescending(f => f.SizeBytes).ToList(),
                "downloads" => files.OrderByDescending(f => f.DownloadCount).ToList(),
                _ => files.OrderByDescending(f => f.CreatedAt).ToList()
            };

            var allFolders = await _storage.GetFoldersAsync();
            var myFolders = allFolders
                .Where(f => f.OwnerUserId == CurrentUserId && f.ParentId == null)
                .OrderBy(f => f.Name)
                .ToList();

            var folderFileCounts = new Dictionary<int, int>();
            foreach (var folder in myFolders)
            {
                folderFileCounts[folder.Id] = files.Count(f => f.FolderId == folder.Id);
            }

            ViewBag.MyFolders = myFolders;
            ViewBag.FolderFileCounts = folderFileCounts;
            ViewBag.Categories = await _storage.GetCategoriesAsync();
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.Sort = sort;

            return View(files);
        }

        // ============================================
        // صفحه آپلود (همه کاربران)
        // نمایش همه کاربران + پوشه‌های قابل دسترس
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Upload(int? folderId)
        {
            ViewBag.Categories = await _storage.GetCategoriesAsync();

            var allUsers = await _auth.GetAllUsersAsync();
            var allFolders = await _storage.GetFoldersAsync();

            // فقط کاربران فعال
            var activeUsers = allUsers.Where(u => u.IsActive).ToList();

            var usersList = new List<Dictionary<string, object>>();

            foreach (var user in activeUsers)
            {
                // پوشه‌هایی که من می‌تونم توشون آپلود کنم
                var userFolders = allFolders
                    .Where(f => f.OwnerUserId == user.Id)
                    .Where(f => _folders.CanUploadToFolder(f, CurrentUserId, CurrentRole))
                    .ToList();

                // اگه خودم هستم، همه پوشه‌های خودم
                // اگه Admin هستم، همه پوشه‌های همه
                // اگه کاربر عادی هستم، فقط پوشه‌های عمومی

                bool canAccessThisUser = false;

                if (user.Id == CurrentUserId)
                {
                    canAccessThisUser = true;
                }
                else if (CurrentRole == "Admin")
                {
                    canAccessThisUser = true;
                }
                else
                {
                    // چک کن حداقل یه پوشه عمومی از این کاربر دارم
                    var publicFolders = allFolders
                        .Where(f => f.OwnerUserId == user.Id && f.Access == AccessLevel.Public)
                        .ToList();

                    if (publicFolders.Any())
                        canAccessThisUser = true;
                }

                if (!canAccessThisUser && !userFolders.Any())
                    continue;

                // ساخت درخت پوشه برای این کاربر
                var flatFolders = BuildFolderListWithPath(userFolders);

                usersList.Add(new Dictionary<string, object>
                {
                    ["userId"] = user.Id,
                    ["userName"] = user.FullName ?? user.NationalCode,
                    ["userRole"] = user.Role,
                    ["isMe"] = user.Id == CurrentUserId,
                    ["folders"] = flatFolders
                });
            }

            // مرتب‌سازی: خودم اول، بعد Admin/Board، بعد User
            usersList = usersList
                .OrderByDescending(u => (bool)u["isMe"])
                .ThenBy(u => u["userRole"].ToString() == "Admin" ? 0 : u["userRole"].ToString() == "Board" ? 1 : 2)
                .ThenBy(u => u["userName"].ToString())
                .ToList();

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            ViewBag.UsersJson = System.Text.Json.JsonSerializer.Serialize(usersList, jsonOptions);
            ViewBag.SelectedFolderId = folderId;

            return View();
        }

        // ============================================
        // ساخت لیست تخت پوشه با مسیر
        // ============================================
        private List<Dictionary<string, object>> BuildFolderListWithPath(List<Folder> allFolders)
        {
            var result = new List<Dictionary<string, object>>();
            var rootFolders = allFolders
                .Where(f => f.ParentId == null)
                .OrderBy(f => f.Name)
                .ToList();

            foreach (var root in rootFolders)
            {
                AddFolderRecursive(result, root, allFolders, root.Name, 0);
            }

            return result;
        }

        private void AddFolderRecursive(List<Dictionary<string, object>> result, Folder folder, List<Folder> allFolders, string fullPath, int level)
        {
            result.Add(new Dictionary<string, object>
            {
                ["id"] = folder.Id,
                ["name"] = folder.Name,
                ["path"] = fullPath,
                ["level"] = level,
                ["parentId"] = folder.ParentId ?? 0,
                ["access"] = (int)folder.Access,
                ["accessName"] = GetAccessName(folder.Access)
            });

            var children = allFolders
                .Where(f => f.ParentId == folder.Id)
                .OrderBy(f => f.Name)
                .ToList();

            foreach (var child in children)
            {
                var childPath = fullPath + " / " + child.Name;
                AddFolderRecursive(result, child, allFolders, childPath, level + 1);
            }
        }

        // ============================================
        // پردازش آپلود
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<IActionResult> Upload()
        {
            var files = Request.Form.Files;

            if (files == null || files.Count == 0)
            {
                TempData["Error"] = "هیچ فایلی انتخاب نشده";
                return RedirectToAction(nameof(Upload));
            }

            int? categoryId = null;
            var catStr = Request.Form["categoryId"].ToString();
            if (!string.IsNullOrWhiteSpace(catStr) && int.TryParse(catStr, out int catId) && catId > 0)
                categoryId = catId;

            int? folderId = null;
            var folStr = Request.Form["folderId"].ToString();
            if (!string.IsNullOrWhiteSpace(folStr) && int.TryParse(folStr, out int folId) && folId > 0)
                folderId = folId;

            string? description = Request.Form["description"].ToString();
            if (string.IsNullOrWhiteSpace(description)) description = null;

            if (folderId.HasValue)
            {
                var folder = await _folders.GetByIdAsync(folderId.Value);
                if (folder == null || !_folders.CanUploadToFolder(folder, CurrentUserId, CurrentRole))
                {
                    TempData["Error"] = "اجازه آپلود در این پوشه را ندارید";
                    return RedirectToAction(nameof(Upload));
                }
            }

            int successCount = 0;
            int failCount = 0;

            foreach (var file in files)
            {
                try
                {
                    if (file.Length > MaxFileSize) { failCount++; continue; }

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!AllowedExtensions.Contains(ext)) { failCount++; continue; }

                    string contentHash;
                    using (var stream = file.OpenReadStream())
                    {
                        contentHash = _hash.ComputeSha256(stream);
                    }

                    string storedName = $"{Guid.NewGuid():N}{ext}";
                    string subFolder = DateTime.Now.ToString("yyyy/MM/dd");
                    string relativePath = Path.Combine(subFolder, storedName).Replace("\\", "/");
                    string fullPath = Path.Combine(UploadsPath, subFolder, storedName);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var fileItem = new FileItem
                    {
                        DisplayName = file.FileName,
                        OriginalNameHash = _hash.ComputeSha256(file.FileName),
                        StoredName = storedName,
                        RelativePath = relativePath,
                        ContentHash = contentHash,
                        SizeBytes = file.Length,
                        Extension = ext,
                        Description = description,
                        CategoryId = categoryId,
                        FolderId = folderId,
                        UploadedByUserId = CurrentUserId,
                        UploadedByUserName = CurrentUserName,
                        CreatedAt = DateTime.Now
                    };

                    var (success, message, id) = await _files.CreateAsync(fileItem);
                    if (success) successCount++;
                    else failCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    failCount++;
                }
            }

            if (successCount > 0)
                TempData["Success"] = $"{successCount} فایل با موفقیت آپلود شد" +
                    (failCount > 0 ? $" ({failCount} ناموفق)" : "");
            else
                TempData["Error"] = "هیچ فایلی آپلود نشد";

            if (folderId.HasValue)
                return RedirectToAction("Browse", "Folders", new { id = folderId.Value });

            return RedirectToAction("Index", "Home");
        }

        // ============================================
        // دانلود
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var file = await _files.GetByIdAsync(id);
            if (file == null) return NotFound();

            string fullPath = Path.Combine(UploadsPath, file.RelativePath.Replace("/", "\\"));
            if (!System.IO.File.Exists(fullPath))
            {
                TempData["Error"] = "فایل روی سرور یافت نشد";
                return RedirectToAction("Index", "Home");
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var log = new DownloadLog
            {
                FileItemId = file.Id,
                FileName = file.DisplayName,
                UserId = CurrentUserId,
                UserName = CurrentUserName,
                IpHash = _hash.ComputeSha256(ipAddress),
                DownloadedAt = DateTime.Now
            };
            await _storage.AppendDownloadLogAsync(log);

            await _files.IncrementDownloadAsync(id);

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return File(stream, "application/octet-stream", file.DisplayName);
        }

        // ============================================
        // جزئیات
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var file = await _files.GetByIdAsync(id);
            if (file == null) return NotFound();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var viewLog = new ViewLog
            {
                FileItemId = file.Id,
                FileName = file.DisplayName,
                UserId = CurrentUserId,
                UserName = CurrentUserName,
                IpHash = _hash.ComputeSha256(ipAddress),
                ViewedAt = DateTime.Now
            };
            await _storage.AppendViewLogAsync(viewLog);
            await _files.IncrementViewAsync(id);

            var categories = await _storage.GetCategoriesAsync();
            ViewBag.Category = categories.FirstOrDefault(c => c.Id == file.CategoryId);

            var folders = await _storage.GetFoldersAsync();
            ViewBag.Folder = folders.FirstOrDefault(f => f.Id == file.FolderId);

            return View(file);
        }

        // ============================================
        // حذف
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var file = await _files.GetByIdAsync(id);
            if (file == null) return NotFound();

            if (file.UploadedByUserId != CurrentUserId && CurrentRole != "Admin")
            {
                TempData["Error"] = "اجازه حذف این فایل را ندارید";
                return RedirectToAction("Index", "Home");
            }

            var folderId = file.FolderId;
            var (success, message) = await _trash.MoveToTrashAsync(file, CurrentUserId, CurrentUserName);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            if (folderId.HasValue)
                return RedirectToAction("Browse", "Folders", new { id = folderId.Value });

            return RedirectToAction("Index", "Home");
        }

        // ============================================
        // تغییر نام
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rename(int id, string newName)
        {
            var (success, message) = await _files.RenameAsync(id, newName);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Details), new { id });
        }

        // ============================================
        // علاقه‌مندی
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var (success, message) = await _files.ToggleFavoriteAsync(id);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction("Index", "Home");
        }

        private static string GetAccessName(AccessLevel access)
        {
            return access switch
            {
                AccessLevel.Public => "عمومی",
                AccessLevel.Private => "خصوصی",
                AccessLevel.Team => "تیمی",
                AccessLevel.Board => "هیئت مدیره",
                _ => "?"
            };
        }
    }
}