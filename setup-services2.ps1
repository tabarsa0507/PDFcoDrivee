# ============================================================
#  MakeSnapshot.ps1
#  ساخت ZIP از سورس پروژه PDFcoDrive برای ارسال به چت
# ============================================================

$ErrorActionPreference = "Stop"

# مسیر ریشهٔ پروژه = محل اجرای اسکریپت
$Root     = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutFile  = Join-Path $Root "PDFcoDrive-Snapshot.zip"

# پوشه‌هایی که کلاً نادیده گرفته می‌شوند
$ExcludedDirs = @("bin", "obj", ".vs", ".git", ".idea", "node_modules", "packages")

# فایل‌هایی که کپی نمی‌شوند
$ExcludedFiles = @("*.user", "*.suo", "*.zip", "*.7z", "*.rar")

# زیرمسیرهایی که داخل wwwroot\lib نادیده گرفته می‌شوند (کتابخانه‌های استاندارد)
$ExcludedLibs = @("bootstrap", "jquery", "jquery-validation", "jquery-validation-unobtrusive")

Write-Host ""
Write-Host "=== PDFcoDrive Snapshot Builder ===" -ForegroundColor Cyan
Write-Host "Root: $Root" -ForegroundColor Gray
Write-Host ""

# پاک کردن فایل قدیمی اگر هست
if (Test-Path $OutFile) {
    Remove-Item $OutFile -Force
    Write-Host "ZIP قدیمی پاک شد." -ForegroundColor Yellow
}

# ساخت پوشهٔ موقت
$TempDir = Join-Path $env:TEMP ("PDFcoDrive-Snap-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TempDir | Out-Null
Write-Host "پوشهٔ موقت: $TempDir" -ForegroundColor Gray

# گرفتن همهٔ فایل‌ها
$allFiles = Get-ChildItem -Path $Root -Recurse -File -Force

$copiedCount  = 0
$skippedCount = 0

foreach ($file in $allFiles) {

    # مسیر نسبی
    $relPath = $file.FullName.Substring($Root.Length).TrimStart('\')

    # رد کردن پوشه‌های مستثنی
    $inExcludedDir = $false
    foreach ($dir in $ExcludedDirs) {
        if ($relPath -match "(^|\\)$([regex]::Escape($dir))\\") {
            $inExcludedDir = $true
            break
        }
    }
    if ($inExcludedDir) { $skippedCount++; continue }

    # رد کردن کتابخانه‌های استاندارد در wwwroot\lib
    if ($relPath -match "^wwwroot\\lib\\") {
        $skipLib = $false
        foreach ($lib in $ExcludedLibs) {
            if ($relPath -match "^wwwroot\\lib\\$([regex]::Escape($lib))\\") {
                $skipLib = $true
                break
            }
        }
        if ($skipLib) { $skippedCount++; continue }
    }

    # رد کردن فایل‌های خاص
    $skipFile = $false
    foreach ($pattern in $ExcludedFiles) {
        if ($file.Name -like $pattern) {
            $skipFile = $true
            break
        }
    }
    if ($skipFile) { $skippedCount++; continue }

    # کپی به پوشهٔ موقت با حفظ ساختار
    $destPath = Join-Path $TempDir $relPath
    $destDir  = Split-Path -Parent $destPath
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item -Path $file.FullName -Destination $destPath -Force
    $copiedCount++
}

Write-Host "فایل‌های کپی‌شده: $copiedCount" -ForegroundColor Green
Write-Host "فایل‌های نادیده‌گرفته‌شده: $skippedCount" -ForegroundColor DarkGray
Write-Host ""

# ساخت ZIP
Write-Host "در حال ساخت ZIP..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $TempDir "*") -DestinationPath $OutFile -CompressionLevel Optimal -Force

# پاک کردن پوشهٔ موقت
Remove-Item $TempDir -Recurse -Force

# نتیجه
$size = (Get-Item $OutFile).Length
$sizeKB = [math]::Round($size / 1KB, 2)
$sizeMB = [math]::Round($size / 1MB, 2)

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host " ZIP ساخته شد!" -ForegroundColor Green
Write-Host " مسیر: $OutFile" -ForegroundColor White
Write-Host " حجم: $sizeKB KB  ($sizeMB MB)" -ForegroundColor White
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "این فایل رو در چت آپلود کن:" -ForegroundColor Yellow
Write-Host $OutFile -ForegroundColor Yellow
Write-Host ""

# باز کردن پوشه در Explorer (اختیاری)
Start-Process explorer.exe -ArgumentList "/select,`"$OutFile`""