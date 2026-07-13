$ErrorActionPreference = "Continue"
$ROOT = "E:\AI工作目录\AI编程开发\JINGE开发\水电抄表系统"
$PUBLISH = Join-Path $ROOT "publish-final"

# 清理旧产物
if (Test-Path $PUBLISH) { Remove-Item $PUBLISH -Recurse -Force }
New-Item -ItemType Directory -Path $PUBLISH -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PUBLISH "Admin") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PUBLISH "Api") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PUBLISH "TrayApp") -Force | Out-Null

Write-Host "[1/4] Publish WaterMeter.Api ..." -ForegroundColor Cyan
& "C:\Program Files\dotnet\dotnet.exe" publish "$ROOT\05-Standalone\Api\WaterMeter.Api.csproj" -c Release -r win-x64 --self-contained true -o (Join-Path $PUBLISH "Api") 2>&1 | Select-Object -Last 5
if ($LASTEXITCODE -ne 0) { Write-Host "Api publish failed" -ForegroundColor Red; exit 1 }

Write-Host "[2/4] Publish WaterMeter.Admin ..." -ForegroundColor Cyan
& "C:\Program Files\dotnet\dotnet.exe" publish "$ROOT\05-Standalone\Admin\WaterMeter.Admin.csproj" -c Release -r win-x64 --self-contained true -o (Join-Path $PUBLISH "Admin") 2>&1 | Select-Object -Last 5
if ($LASTEXITCODE -ne 0) { Write-Host "Admin publish failed" -ForegroundColor Red; exit 1 }

Write-Host "[3/4] Publish WaterMeter.TrayApp ..." -ForegroundColor Cyan
& "C:\Program Files\dotnet\dotnet.exe" publish "$ROOT\06-TrayApp\WaterMeter.TrayApp.csproj" -c Release -r win-x64 --self-contained true -o (Join-Path $PUBLISH "TrayApp") 2>&1 | Select-Object -Last 5
if ($LASTEXITCODE -ne 0) { Write-Host "TrayApp publish failed" -ForegroundColor Red; exit 1 }

Write-Host "[4/4] Publish WaterMeter.Bootstrapper ..." -ForegroundColor Cyan
& "C:\Program Files\dotnet\dotnet.exe" publish "$ROOT\08-Bootstrapper\Bootstrapper.csproj" -c Release -r win-x64 --self-contained true -o $PUBLISH 2>&1 | Select-Object -Last 5
if ($LASTEXITCODE -ne 0) { Write-Host "Bootstrapper publish failed" -ForegroundColor Red; exit 1 }

# 生成 Embedded.zip（包含 Api/Admin/TrayApp 三个文件夹）
Write-Host ""
Write-Host "Creating Embedded.zip ..." -ForegroundColor Yellow
$appsRoot = Join-Path $PUBLISH "apps_temp"
New-Item -ItemType Directory -Path $appsRoot -Force | Out-Null
Copy-Item (Join-Path $PUBLISH "Api") (Join-Path $appsRoot "Api") -Recurse -Force
Copy-Item (Join-Path $PUBLISH "Admin") (Join-Path $appsRoot "Admin") -Recurse -Force
Copy-Item (Join-Path $PUBLISH "TrayApp") (Join-Path $appsRoot "TrayApp") -Recurse -Force

$zipPath = Join-Path $PUBLISH "Embedded.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($appsRoot, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
Remove-Item $appsRoot -Recurse -Force

Write-Host ""
Write-Host "=== Publish complete ===" -ForegroundColor Green
Get-ChildItem $PUBLISH | Format-Table Name, @{N='Size(MB)';E={[math]::Round($_.Length/1MB, 2)}} -AutoSize
$zipSize = (Get-Item $zipPath).Length
Write-Host ("Embedded.zip size: " + [math]::Round($zipSize/1MB, 2) + " MB") -ForegroundColor Green