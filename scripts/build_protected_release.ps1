# v2.13.140 一键构建受保护发布包
# 设计来源：用户「保证 不被反编译和加壳」—— 全链路默认启用保护
# 三层保护：Obfuscar（源码级）+ ConfuserEx（IL 级，仅 Admin/Api）+ BitMono（PE 加壳，仅 TrayApp）
# 使用：.\scripts\build_protected_release.ps1

$ErrorActionPreference = "Stop"
$ROOT = (Resolve-Path "$PSScriptRoot\..").Path
$out = "DormManage-v2.13.140.zip"
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$logDir = Join-Path $ROOT "logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
$log = Join-Path $logDir "build_v2.13.140_$ts.log"

Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  v2.13.140 一键构建受保护发布包" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan

# 阶段 1：dotnet publish（自动触发 MSBuild.Obfuscar — 因 Directory.Build.props 已注入 NuGet 包）
Write-Host "`n[1/5] dotnet publish Release（自动触发 Obfuscar 混淆）..." -ForegroundColor Yellow
dotnet publish DormManage.TrayApp -c Release -r win-x64 --self-contained true -o "publish-final/TrayApp"  2>&1 | Tee-Object -FilePath $log -Append
dotnet publish DormManage.Api      -c Release -r win-x64 --self-contained true -o "publish-final/Api"      2>&1 | Tee-Object -FilePath $log -Append
dotnet publish DormManage.Admin    -c Release -r win-x64 --self-contained true -o "publish-final/Admin"    2>&1 | Tee-Object -FilePath $log -Append

# 阶段 2：Obfuscar 输出拷贝（v3.x 默认输出在 bin/.../Obfuscated/，需 copy 到 publish-final）
Write-Host "`n[2/5] Obfuscar 输出拷贝..." -ForegroundColor Yellow
$obfSrc = Join-Path $ROOT "publish-final\Admin\bin\Release\net8.0-windows\win-x64\Obfuscated"
if (Test-Path $obfSrc) {
    Copy-Item "$obfSrc\*" (Join-Path $ROOT "publish-final\Admin") -Recurse -Force
    Write-Host "  Admin Obfuscar 拷贝完成 ✅" -ForegroundColor Green
} else {
    Write-Host "  Admin Obfuscar 输出未找到（可能 NuGet 包尚未生效或跳过混淆）⚠" -ForegroundColor DarkYellow
}
$obfSrcApi = Join-Path $ROOT "publish-final\Api\bin\Release\net8.0-windows\win-x64\Obfuscated"
if (Test-Path $obfSrcApi) {
    Copy-Item "$obfSrcApi\*" (Join-Path $ROOT "publish-final\Api") -Recurse -Force
    Write-Host "  Api Obfuscar 拷贝完成 ✅" -ForegroundColor Green
}

# 阶段 3：BitMono 加壳（TrayApp）
Write-Host "`n[3/5] BitMono 加壳（TrayApp）..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "bitmono_protect_trayapp.ps1") 2>&1 | Tee-Object -FilePath $log -Append

# 阶段 4：ConfuserEx 加固（Admin/Api）
Write-Host "`n[4/5] ConfuserEx 加固（Admin/Api）..." -ForegroundColor Yellow
if (Get-Command ConfuserEx -ErrorAction SilentlyContinue) {
    & (Join-Path $PSScriptRoot "confuserex_protect_admin.ps1") 2>&1 | Tee-Object -FilePath $log -Append
    & (Join-Path $PSScriptRoot "confuserex_protect_api.ps1")   2>&1 | Tee-Object -FilePath $log -Append
} else {
    Write-Host "  ConfuserEx 未安装，跳过 IL 级混淆（仅 Obfuscar 生效）" -ForegroundColor DarkYellow
}

# 阶段 5：ZIP 打包
Write-Host "`n[5/5] ZIP 打包..." -ForegroundColor Yellow
if (Test-Path (Join-Path $ROOT $out)) { Remove-Item (Join-Path $ROOT $out) -Force }
Compress-Archive -Path "publish-final/Admin/*","publish-final/Api/*","publish-final/TrayApp/*" `
                 -DestinationPath $out -CompressionLevel Optimal

Write-Host "`n════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  构建完成：$out" -ForegroundColor Cyan
Write-Host "  日志路径：$log" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
