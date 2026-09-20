using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Models;
using PDFcoDrive.Services;
using System.Security.Claims;

namespace PDFcoDrive.Controllers
{
    [Authorize]
    public class FoldersController : Controller
    {
        private readonly IFolderService _folders;
        private readonly IJsonStorageService _storage;
        private readonly IFileService _files;
        private readonly ITrashService _trash;
        private readonly IAuthService _auth;

        public FoldersController(
            IFolderService folders,
            IJsonStorageService storage,
            IFileService files,
            ITrashService trash,
            IAuthService auth)
        {
            _folders = folders;
            _storage = storage;
            _files = files;
            _trash = trash;
            _auth = auth;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        private string CurrentUserName => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        // ⚠️ فقط Admin
        private bool IsAdmin => CurrentRole == "Admin";

        // ============================================
        // لیست
        // ============================================
        public async Task<IActionResult> Index()
        {
            // فقط Admin لیست کاربران رو می‌بینه
            if (IsAdmin)
            {
                var users = await _auth.GetAllUsersAsync();
                var allFolders = await _storage.GetFoldersAsync();
                var allFiles = await _storage.GetFilesAsync();

                // نمایش User + Board (نه Admin)
                var stats = users
                    .Where(u => u.IsActive && (u.Role == "User" || u.Role == "Board"))
                    .Select(u => new UserFolderStats
                    {
                        User = u,
                        TotalFolders = allFolders.Count(f => f.OwnerUserId == u.Id),
                        RootFolders = allFolders.Count(f => f.OwnerUserId == u.Id && f.ParentId == null),
                        TotalFiles = allFiles.Count(f => f.UploadedByUserId == u.Id)
                    })
                    .OrderByDescending(s => s.TotalFolders)
                    .ToList();

                return View("Users", stats);
            }

            var allFoldersUser = await _storage.GetFoldersAsync();

            var myFolders = allFoldersUser
                .Where(f => f.OwnerUserId == CurrentUserId && f.ParentId == null)
                .OrderBy(f => f.Name)
                .ToList();

            var publicFolders = allFoldersUser
                .Where(f => f.OwnerUserId != CurrentUserId
                            && f.Access == AccessLevel.Public
                            && f.ParentId == null)
                .OrderByDescending(f => f.CreatedAt)
                .ToList();

            ViewBag.MyFolders = myFolders;
            ViewBag.PublicFolders = publicFolders;

            return View("Index");
        }

        // ============================================
        // مشاهده پوشه
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Browse(int id, string? returnUrl)
        {
            var folder = await _folders.GetByIdAsync(id);
            if (folder == null) return NotFound();
            if (!_folders.CanAccess(folder, CurrentUserId, CurrentRole))
                return Forbid();

            var children = (await _folders.GetChildrenAsync(id))
                .Where(f => _folders.CanAccess(f, CurrentUserId, CurrentRole))
                .ToList();

            var allFiles = await _storage.GetFilesAsync();
            var files = allFiles.Where(f => f.FolderId == id)
                .OrderByDescending(f => f.CreatedAt).ToList();

            var breadcrumb = await _folders.GetBreadcrumbAsync(id);

            ViewBag.CurrentFolder = folder;
            ViewBag.Children = children;
            ViewBag.Files = files;
            ViewBag.Breadcrumb = breadcrumb;
            ViewBag.CanUpload = _folders.CanUploadToFolder(folder, CurrentUserId, CurrentRole);
            ViewBag.CanManage = _folders.CanManageFolder(folder, CurrentUserId, CurrentRole);
            ViewBag.ReturnUrl = returnUrl;

            var accessibleFolders = await _folders.GetAccessibleAsync(CurrentUserId, CurrentRole);
            ViewBag.AllFoldersForSelect = accessibleFolders
                .Select(f => new { id = f.Id, name = f.Name })
                .ToList();

            return View();
        }

        // ============================================
        // ساخت پوشه
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Create(int? parentId, string? returnUrl)
        {
            ViewBag.ParentId = parentId;
            ViewBag.ReturnUrl = returnUrl;

            if (parentId.HasValue)
            {
                var parent = await _folders.GetByIdAsync(parentId.Value);
                if (parent != null)
                    ViewBag.ParentName = parent.Name;
            }

            var users = await _auth.GetAllUsersAsync();
            ViewBag.Users = users.Where(u => u.Id != CurrentUserId && u.IsActive).ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? parentId, string name, string? description, AccessLevel access, string icon, string color, List<string>? allowedUserIds, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ViewBag.Error = "نام پوشه الزامیست";
                ViewBag.ParentId = parentId;
                ViewBag.ReturnUrl = returnUrl;
                var users = await _auth.GetAllUsersAsync();
                ViewBag.Users = users.Where(u => u.Id != CurrentUserId && u.IsActive).ToList();
                return View();
            }

            var folder = new Folder
            {
                Name = name.Trim(),
                ParentId = parentId,
                OwnerUserId = CurrentUserId,
                OwnerName = CurrentUserName,
                Description = description,
                Access = access,
                Icon = string.IsNullOrEmpty(icon) ? "bi-folder-fill" : icon,
                Color = string.IsNullOrEmpty(color) ? "#00d4ff" : color,
                AllowedUserIds = access == AccessLevel.Team && allowedUserIds != null
                    ? allowedUserIds
                    : new List<string>()
            };

            var (success, message, newId) = await _folders.CreateAsync(folder);

            if (success)
            {
                TempData["Success"] = message;

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                if (parentId.HasValue)
                    return RedirectToAction(nameof(Browse), new { id = parentId.Value });
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Error = message;
            ViewBag.ParentId = parentId;
            ViewBag.ReturnUrl = returnUrl;
            var usersList = await _auth.GetAllUsersAsync();
            ViewBag.Users = usersList.Where(u => u.Id != CurrentUserId && u.IsActive).ToList();
            return View(folder);
        }

        // ============================================
        // ویرایش پوشه
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id, string? returnUrl)
        {
            var folder = await _folders.GetByIdAsync(id);
            if (folder == null) return NotFound();
            if (!_folders.CanManageFolder(folder, CurrentUserId, CurrentRole))
                return Forbid();

            ViewBag.ReturnUrl = returnUrl;

            var users = await _auth.GetAllUsersAsync();
            ViewBag.Users = users.Where(u => u.Id != CurrentUserId && u.IsActive).ToList();

            return View(folder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, string? description, AccessLevel access, string icon, string color, List<string>? allowedUserIds, string? returnUrl)
        {
            var folder = await _folders.GetByIdAsync(id);
            if (folder == null) return NotFound();
            if (!_folders.CanManageFolder(folder, CurrentUserId, CurrentRole))
                return Forbid();

            folder.Name = name.Trim();
            folder.Description = description;
            folder.Access = access;
            folder.Icon = icon;
            folder.Color = color;
            folder.AllowedUserIds = access == AccessLevel.Team && allowedUserIds != null
                ? allowedUserIds
                : new List<string>();

            var (success, message) = await _folders.UpdateAsync(folder);

            if (success)
            {
                TempData["Success"] = message;

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction(nameof(Browse), new { id });
            }

            ViewBag.Error = message;
            ViewBag.ReturnUrl = returnUrl;
            var users = await _auth.GetAllUsersAsync();
            ViewBag.Users = users.Where(u => u.Id != CurrentUserId && u.IsActive).ToList();
            return View(folder);
        }

        // ============================================
        // حذف پوشه
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl)
        {
            var folder = await _folders.GetByIdAsync(id);
            if (folder == null) return NotFound();

            var parentId = folder.ParentId;
            var (success, message) = await _folders.DeleteAsync(id, CurrentUserId, CurrentRole);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (parentId.HasValue)
                return RedirectToAction(nameof(Browse), new { id = parentId.Value });
            return RedirectToAction(nameof(Index));
        }

        // ============================================
        // میز کار شخصی
        // ============================================
        [HttpGet]
        public async Task<IActionResult> MyWorkspace()
        {
            var folders = await _storage.GetFoldersAsync();
            var personalFolder = folders.FirstOrDefault(f =>
                f.OwnerUserId == CurrentUserId && f.SystemType == "Personal");

            if (personalFolder == null)
            {
                personalFolder = new Folder
                {
                    Name = "My Workspace",
                    OwnerUserId = CurrentUserId,
                    OwnerName = CurrentUserName,
                    Access = AccessLevel.Private,
                    SystemType = "Personal",
                    IsSystem = true,
                    Icon = "bi-person-workspace",
                    Color = "#7c3aed"
                };

                var (success, message, newId) = await _folders.CreateAsync(personalFolder);
                if (success && newId.HasValue)
                    return RedirectToAction(nameof(Browse), new { id = newId.Value });
            }

            return RedirectToAction(nameof(Browse), new { id = personalFolder.Id });
        }

        // ============================================
        // مشاهده پوشه‌های کاربر (فقط Admin)
        // ============================================
        [HttpGet]
        public async Task<IActionResult> ViewUserFolders(string userId)
        {
            if (!IsAdmin)
                return Forbid();

            var user = await _auth.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            // فقط Admin قابل مشاهده نباشه (خودش Admin هست)
            if (user.Role == "Admin")
                return Forbid();

            var allFolders = await _storage.GetFoldersAsync();
            var allFiles = await _storage.GetFilesAsync();

            var userFolders = allFolders.Where(f => f.OwnerUserId == userId).ToList();

            var flatList = new List<FolderWithLevel>();
            var rootFolders = userFolders
                .Where(f => f.ParentId == null)
                .OrderBy(f => f.Name);

            foreach (var root in rootFolders)
            {
                AddFolderTree(flatList, root, userFolders, allFiles, 0);
            }

            ViewBag.SelectedUser = user;
            ViewBag.TotalFolders = userFolders.Count;
            ViewBag.TotalFiles = allFiles.Count(f => f.UploadedByUserId == userId);
            ViewBag.TotalSize = allFiles.Where(f => f.UploadedByUserId == userId).Sum(f => f.SizeBytes);

            return View("UserFolders", flatList);
        }

        private void AddFolderTree(List<FolderWithLevel> list, Folder folder, List<Folder> allFolders, List<FileItem> allFiles, int level)
        {
            var children = allFolders.Where(f => f.ParentId == folder.Id).OrderBy(f => f.Name).ToList();
            var fileCount = allFiles.Count(f => f.FolderId == folder.Id);

            list.Add(new FolderWithLevel
            {
                Folder = folder,
                Level = level,
                FileCount = fileCount,
                ChildCount = children.Count
            });

            foreach (var child in children)
            {
                AddFolderTree(list, child, allFolders, allFiles, level + 1);
            }
        }

        // ============================================
        // فرم انتقال فایل
        // ============================================
        [HttpGet]
        public async Task<IActionResult> SaveToMyFolder(int fileId)
        {
            var file = await _files.GetByIdAsync(fileId);
            if (file == null) return NotFound();

            var allFolders = await _storage.GetFoldersAsync();
            var myFolders = allFolders
                .Where(f => f.OwnerUserId == CurrentUserId && f.Id != file.FolderId)
                .ToList();

            ViewBag.File = file;
            ViewBag.MyFolders = myFolders;

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var foldersDto = myFolders.Select(f => new
            {
                id = f.Id,
                name = f.Name ?? "",
                parentId = f.ParentId
            }).ToList();

            ViewBag.FoldersJson = System.Text.Json.JsonSerializer.Serialize(foldersDto, jsonOptions);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveToMyFolder(int fileId, int? targetFolderId, string? returnUrl)
        {
            var allFiles = await _storage.GetFilesAsync();
            var file = allFiles.FirstOrDefault(f => f.Id == fileId);

            if (file == null)
            {
                TempData["Error"] = "فایل پیدا نشد";
                return RedirectToAction("Index", "Home");
            }

            file.FolderId = targetFolderId;
            await _storage.SaveFilesAsync(allFiles);

            TempData["Success"] = "فایل منتقل شد";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // ============================================
        // انتقال فایل
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveFile(int fileId, int? targetFolderId, int? currentFolderId)
        {
            var file = await _files.GetByIdAsync(fileId);
            if (file == null) return NotFound();

            bool canMove = file.UploadedByUserId == CurrentUserId || IsAdmin;

            if (!canMove)
            {
                TempData["Error"] = "اجازه انتقال این فایل را ندارید";
                if (currentFolderId.HasValue)
                    return RedirectToAction(nameof(Browse), new { id = currentFolderId.Value });
                return RedirectToAction(nameof(Index));
            }

            var (success, message) = await _files.MoveAsync(fileId, targetFolderId);

            if (success)
                TempData["Success"] = "فایل منتقل شد";
            else
                TempData["Error"] = message;

            if (currentFolderId.HasValue)
                return RedirectToAction(nameof(Browse), new { id = currentFolderId.Value });
            return RedirectToAction(nameof(Index));
        }

        // ============================================
        // کپی فایل
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyFile(int fileId, int? targetFolderId, int? currentFolderId)
        {
            var (success, message, newId) = await _files.CopyAsync(fileId, targetFolderId, CurrentUserId);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            if (currentFolderId.HasValue)
                return RedirectToAction(nameof(Browse), new { id = currentFolderId.Value });
            return RedirectToAction(nameof(Index));
        }

        // ============================================
        // حذف فایل
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFile(int fileId, int? currentFolderId)
        {
            var file = await _files.GetByIdAsync(fileId);
            if (file == null) return NotFound();

            bool canDelete = file.UploadedByUserId == CurrentUserId || IsAdmin;

            if (!canDelete)
            {
                TempData["Error"] = "اجازه حذف این فایل را ندارید";
                if (currentFolderId.HasValue)
                    return RedirectToAction(nameof(Browse), new { id = currentFolderId.Value });
                return RedirectToAction(nameof(Index));
            }

            var (success, message) = await _trash.MoveToTrashAsync(file, CurrentUserId, CurrentUserName);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            if (currentFolderId.HasValue)
                return RedirectToAction(nameof(Browse), new { id = currentFolderId.Value });
            return RedirectToAction(nameof(Index));
        }

        // ============================================
        // فرم اشتراک‌گذاری فایل
        // ============================================
        [HttpGet]
        public async Task<IActionResult> ShareFile(int fileId)
        {
            var file = await _files.GetByIdAsync(fileId);
            if (file == null) return NotFound();

            if (file.UploadedByUserId != CurrentUserId && !IsAdmin)
            {
                TempData["Error"] = "این فایل مال شما نیست";
                return RedirectToAction("Index", "Home");
            }

            var users = await _auth.GetAllUsersAsync();
            ViewBag.File = file;
            ViewBag.Users = users.Where(u => u.Id != CurrentUserId && u.IsActive).ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareFile(int fileId, string receiverId, string? message)
        {
            var file = await _files.GetByIdAsync(fileId);
            if (file == null) return NotFound();

            if (file.UploadedByUserId != CurrentUserId && !IsAdmin)
            {
                TempData["Error"] = "این فایل مال شما نیست";
                return RedirectToAction("Index", "Home");
            }

            var receiver = await _auth.GetUserByIdAsync(receiverId);
            if (receiver == null)
            {
                TempData["Error"] = "گیرنده یافت نشد";
                return RedirectToAction(nameof(ShareFile), new { fileId });
            }

            var sharedService = HttpContext.RequestServices.GetRequiredService<ISharedFileService>();

            var sharedFile = new SharedFile
            {
                FileItemId = file.Id,
                SharedByUserId = CurrentUserId,
                SharedByUserName = CurrentUserName,
                SharedWithUserId = receiver.Id,
                SharedWithUserName = receiver.FullName,
                Message = message
            };

            var (success, msg) = await sharedService.ShareAsync(sharedFile);

            if (success)
            {
                TempData["Success"] = msg;
                return RedirectToAction("Index", "Home");
            }

            TempData["Error"] = msg;
            return RedirectToAction(nameof(ShareFile), new { fileId });
        }
    }
}