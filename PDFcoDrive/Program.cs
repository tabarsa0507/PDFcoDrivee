using Microsoft.AspNetCore.Authentication.Cookies;
using PDFcoDrive.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// MVC
// ============================================
builder.Services.AddControllersWithViews();

// ============================================
// Session (برای کپچا)
// ============================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".PDFcoDrive.Session";
});

// ============================================
// Cookie Authentication
// ============================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(
            builder.Configuration.GetValue<int>("Auth:ExpireDays", 7));
        options.SlidingExpiration = true;
        options.Cookie.Name = builder.Configuration["Auth:CookieName"] ?? "PDFcoDrive.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

// ============================================
// سرویس‌های ما
// ============================================
builder.Services.AddScoped<IHashService, HashService>();
builder.Services.AddScoped<IJsonStorageService, JsonStorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ITrashService, TrashService>();
builder.Services.AddScoped<ISharedFileService, SharedFileService>();
builder.Services.AddScoped<IGroupService, GroupService>();

// ============================================
// Build
// ============================================
var app = builder.Build();

// ============================================
// ساخت ادمین پیش‌فرض
// ============================================
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var defaultCode = config["Admin:DefaultNationalCode"] ?? "2122767111";
    var defaultPass = config["Admin:DefaultPassword"] ?? "Admin@1234";
    var defaultName = config["Admin:DefaultFullName"] ?? "مدیر سیستم";

    var users = await authService.GetAllUsersAsync();

    if (!users.Any(u => u.Role == "Admin"))
    {
        await authService.CreateUserAsync(defaultCode, defaultName, defaultPass, "Admin");
    }
}

// ============================================
// Middleware
// ============================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();