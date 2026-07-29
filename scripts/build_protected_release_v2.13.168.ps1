# v2.13.168 一键构建受保护发布包（设备档案设备ID 全局唯一校验）
#
# 基于 build_protected_release.ps1（v2.13.141 修复版：Obfuscar-only，BitMono 已禁用）
# 变更：版本号 → v2.13.168；输出名带时间戳；zip 保留 Admin/Api/TrayApp 目录结构；附带交付文档
#
# 保护链路：Obfuscar 全栈混淆 + PublishReadyToRun R2R + HideStrings + SuppressIldasm + IL 优化

$ErrorActionPreference = "Stop"
$ROOT = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $ROOT
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$out = "DormManage-v2.13.168_$ts.zip"
$logDir = Join-Path $ROOT "logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
$log = Join-Path $logDir "build_v2.13.168_$ts.log"

Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  v2.13.168 一键构建受保护发布包" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan

# 阶段 1：dotnet publish 3 项目（Release 自包含 win-x64；Release 自动跑 Obfuscar via Directory.Build.props）
Write-Host "`n[1/5] dotnet publish Release..." -ForegroundColor Yellow
dotnet publish DormManage.TrayApp -c Release -r win-x64 --self-contained true -o "publish-final/TrayApp" 2>&1 | Tee-Object -FilePath $log -Append
dotnet publish DormManage.Api      -c Release -r win-x64 --self-contained true -o "publish-final/Api"     2>&1 | Tee-Object -FilePath $log -Append
dotnet publish DormManage.Admin    -c Release -r win-x64 --self-contained true -o "publish-final/Admin"   2>&1 | Tee-Object -FilePath $log -Append

# 阶段 2：收集 4 DLL 到 tmp/in（Obfuscar 输入）
Write-Host "`n[2/5] 收集 DLL 到 tmp/in/..." -ForegroundColor Yellow
if (Test-Path "tmp/in") { Remove-Item tmp/in -Recurse -Force }
if (Test-Path "tmp/out") { Remove-Item tmp/out -Recurse -Force }
New-Item -ItemType Directory -Path "tmp/in" -Force | Out-Null
Copy-Item DormManage.TrayApp/bin/Release/net8.0-windows/win-x64/*.dll tmp/in/ -Force
Copy-Item DormManage.Api/bin/Release/net8.0/win-x64/*.dll tmp/in/ -Force
Copy-Item DormManage.Admin/bin/Release/net8.0/win-x64/*.dll tmp/in/ -Force
Copy-Item DormManage.Shared/bin/Release/net8.0/*.dll tmp/in/ -Force
Write-Host "  DLL 总数: $((Get-ChildItem tmp/in/*.dll).Count)" -ForegroundColor Cyan

# 阶段 3：Obfuscar 全栈混淆
Write-Host "`n[3/5] Obfuscar 全栈混淆..." -ForegroundColor Yellow
if (-not (Get-Command obfuscar.console -ErrorAction SilentlyContinue)) {
    Write-Host "  安装 Obfuscar.GlobalTool..." -ForegroundColor Yellow
    dotnet tool install --global Obfuscar.GlobalTool
}
obfuscar.console Obfuscar.xml 2>&1 | Tee-Object -FilePath $log -Append

# 阶段 4：拷贝混淆输出到 publish-final
Write-Host "`n[4/5] 拷贝混淆输出到 publish-final/..." -ForegroundColor Yellow
Copy-Item tmp/out/DormManage.Shared.dll  publish-final/TrayApp/ -Force
Copy-Item tmp/out/DormManage.Shared.dll  publish-final/Admin/   -Force
Copy-Item tmp/out/DormManage.Shared.dll  publish-final/Api/     -Force
Copy-Item tmp/out/DormManage.TrayApp.dll publish-final/TrayApp/ -Force
Copy-Item tmp/out/DormManage.Admin.dll   publish-final/Admin/   -Force
Copy-Item tmp/out/DormManage.Api.dll     publish-final/Api/     -Force
Copy-Item tmp/out/DormManage.Api.dll     publish-final/Admin/   -Force  # Admin 引用 Api 程序集

# 附带交付文档 + 迁移脚本到各包 docs（便于运维）
$docDir = "publish-final/_delivery_v2.13.168"
if (Test-Path $docDir) { Remove-Item $docDir -Recurse -Force }
New-Item -ItemType Directory -Path $docDir -Force | Out-Null
Copy-Item "00-方案文档/209-设备档案设备ID唯一性校验-v2.13.168.md" $docDir/ -Force
Copy-Item "01-Database/03_Migration_v2.13.168_DeviceIdUnique.sql" $docDir/ -Force
Copy-Item "CLAUDE.md" $docDir/ -Force

# 阶段 5：ZIP 打包（保留 Admin/Api/TrayApp 目录结构 + 交付文档）
Write-Host "`n[5/5] ZIP 打包（保留目录结构）..." -ForegroundColor Yellow
if (Test-Path (Join-Path $ROOT $out)) { Remove-Item (Join-Path $ROOT $out) -Force }
Compress-Archive -Path "publish-final/Admin","publish-final/Api","publish-final/TrayApp",$docDir `
                 -DestinationPath $out -CompressionLevel Optimal

$sizeMB = [math]::Round((Get-Item (Join-Path $ROOT $out)).Length / 1MB, 2)
Write-Host "`n════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  构建完成：$out  ($sizeMB MB)" -ForegroundColor Green
Write-Host "  日志：$log" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
