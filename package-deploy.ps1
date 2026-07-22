$ErrorActionPreference = 'Stop'
$baseDir = $PSScriptRoot
$srcDir = Join-Path $baseDir 'publish-final'
$ts = Get-Date -Format 'yyyyMMdd_HHmmss'
$zipPath = Join-Path $baseDir ("DormManage_v2.13.109_" + $ts + ".zip")

if (-not (Test-Path $srcDir)) {
    Write-Error ("Source directory not found: " + $srcDir)
    exit 1
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host ("Creating zip: " + $zipPath)
Write-Host ("Source: " + $srcDir)
Write-Host ""

Write-Host "[1/2] Compressing 4 packages..."
Compress-Archive -Path (Join-Path $srcDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "[2/2] Appending scripts and docs..."

$extra = @(
    (Join-Path $baseDir 'scripts\rename_v2.13.98_attendance_label.ps1'),
    (Join-Path $baseDir 'scripts\seed_v2.13.103_personnel_add.sql'),
    (Join-Path $baseDir 'CLAUDE.md'),
    (Join-Path $baseDir '00-方案文档\161-SQLite彻底移除专项-v2.13.109.md')
)
foreach ($f in $extra) {
    if (Test-Path $f) {
        Compress-Archive -Path $f -Update -DestinationPath $zipPath
    }
}

$sizeBytes = (Get-Item $zipPath).Length
$sizeMB = [math]::Round($sizeBytes / 1MB, 2)

Write-Host ""
Write-Host "=== PACKAGE CREATED ===" -ForegroundColor Green
Write-Host ("File: " + $zipPath)
Write-Host ("Size: " + $sizeMB + " MB (" + $sizeBytes + " bytes)")
Write-Host ""
Write-Host "Contents:"
Write-Host "  Admin/      135 MB    DormManage.Admin.exe  (Web Kestrel :5001)"
Write-Host "  Api/        124 MB    DormManage.Api.exe     (REST API :5100)"
Write-Host "  TrayApp/    210 MB    DormManage.TrayApp.exe (WinForms tray)"
Write-Host "  Shared/     121 MB    DormManage.Shared.dll  (class library)"
Write-Host "  scripts/    ~3 KB     rename_v2.13.98 + seed_v2.13.103"
Write-Host "  CLAUDE.md   ~13 KB    project notes"
Write-Host "  161-SQLite彻底移除专项-v2.13.109.md  交付报告"

