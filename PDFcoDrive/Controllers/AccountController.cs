using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Services;
using System.Security.Claims;

namespace PDFcoDrive.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _auth;
        private readonly ICaptchaService _captcha;
        private readonly IHashService _hash;

        public AccountController(IAuthService auth, ICaptchaService captcha, IHashService hash)
        {
            _auth = auth;
            _captcha = captcha;
            _hash = hash;
        }

        // ============================================
        // Login GET
        // ============================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ============================================
        // Login POST
        // ============================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string nationalCode, string password, string captcha, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            // ===== چک کپچا =====
            var sessionCode = HttpContext.Session.GetString("CaptchaCode");
            if (string.IsNullOrEmpty(sessionCode))
            {
                ViewBag.Error = "کد امنیتی منقضی شده. لطفاً مجدد تلاش کنید";
                return View();
            }

            if (_captcha.HashCode(captcha ?? "") != sessionCode)
            {
                ViewBag.Error = "کد امنیتی اشتباه است";
                return View();
            }

            HttpContext.Session.Remove("CaptchaCode");

            // ===== اعتبارسنجی =====
            if (string.IsNullOrWhiteSpace(nationalCode) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "کد ملی و رمز عبور الزامیست";
                return View();
            }

            // ===== بررسی کاربر =====
            var user = await _auth.ValidateUserAsync(nationalCode, password);
            if (user == null)
            {
                ViewBag.Error = "کد ملی یا رمز عبور اشتباه است";
                return View();
            }

            // ===== ساخت Claims =====
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.NationalCode),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            await _auth.UpdateLastLoginAsync(user.Id);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // ============================================
        // Logout
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // ============================================
        // Change Password
        // ============================================
        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login");

            var user = await _auth.GetUserByIdAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            // چک پسورد فعلی
            if (!_hash.VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
            {
                ViewBag.Error = "رمز عبور فعلی اشتباه است";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "رمز عبور جدید و تکرار آن یکسان نیستند";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "رمز عبور جدید باید حداقل 6 کاراکتر باشد";
                return View();
            }

            await _auth.ChangePasswordAsync(userId, newPassword);
            ViewBag.Success = "رمز عبور با موفقیت تغییر کرد";
            return View();
        }

        // ============================================
        // Access Denied
        // ============================================
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}