# v2.13.141 一键构建受保护发布包（修复 TrayApp BadImageFormatException）
#
# 设计来源：v2.13.140 BadImageFormatException 修复
# 关键发现：BitMono 0.43.0 在 .NET 8 / net8.0-windows 上**破坏 IL 头**
#   - Obfuscar-only 输出可正常启动（已验证 TrayApp/Admin）
#   - BitMono + Obfuscar 叠加后报 BadImageFormatException
#   - 根因：BitMono PE 级加壳改写 .NET 元数据头，托管运行时无法解析
#
# 修复策略：
#   - Obfuscar 全栈默认启用（已验证）
#   - BitMono 加壳禁用（与 .NET 8 不兼容）
#   - 替代方案：
#     1) PublishReadyToRun=true (Directory.Build.props) R2R 预编译
#     2) Obfuscar HideStrings + SuppressIldasm + Unicode names
#     3) 全栈使用 Obfuscar 单一混淆链路
#   - 攻击成本仍保持「最大化」（混淆+R2R+字符串加密+AntiILDasm+IL 优化）

$ErrorActionPreference = "Stop"
$ROOT = (Resolve-Path "$PSScriptRoot\..").Path
$out = "DormManage-v2.13.141.zip"
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$logDir = Join-Path $ROOT "logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
$log = Join-Path $logDir "build_v2.13.141_$ts.log"

Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  v2.13.141 一键构建受保护发布包（修复版）" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan

# 阶段 1：dotnet publish 3 项目
Write-Host "`n[1/5] dotnet publish Release..." -ForegroundColor Yellow
dotnet publish DormManage.TrayApp -c Release -r win-x64 --self-contained true -o "publish-final/TrayApp"  2>&1 | Tee-Object -FilePath $log -Append
dotnet publish DormManage.Api      -c Release -r win-x64 --self-contained true -o "publish-final/Api"      2>&1 | Tee-Object -FilePath $log -Append
dotnet publish DormManage.Admin    -c Release -r win-x64 --self-contained true -o "publish-final/Admin"    2>&1 | Tee-Object -FilePath $log -Append

# 阶段 2：拷贝 4 DLL 到 tmp/in（Obfuscar 输入）
Write-Host "`n[2/5] 收集 DLL 到 tmp/in/..." -ForegroundColor Yellow
if (Test-Path "tmp") { Remove-Item tmp -Recurse -Force }
New-Item -ItemType Directory -Path "tmp/in" -Force | Out-Null
# TrayApp win-x64 (含 Microsoft.WindowsDesktop.App 引用)
Copy-Item DormManage.TrayApp/bin/Release/net8.0-windows/win-x64/*.dll tmp/in/ -Force
# Admin/Api win-x64
Copy-Item DormManage.Api/bin/Release/net8.0/win-x64/*.dll tmp/in/ -Force
Copy-Item DormManage.Admin/bin/Release/net8.0/win-x64/*.dll tmp/in/ -Force
# Shared net8.0 (跨项目共用)
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
Copy-Item tmp/out/DormManage.Shared.dll publish-final/TrayApp/ -Force
Copy-Item tmp/out/DormManage.Shared.dll publish-final/Admin/ -Force
Copy-Item tmp/out/DormManage.Shared.dll publish-final/Api/ -Force
Copy-Item tmp/out/DormManage.TrayApp.dll publish-final/TrayApp/ -Force
Copy-Item tmp/out/DormManage.Admin.dll publish-final/Admin/ -Force
Copy-Item tmp/out/DormManage.Api.dll publish-final/Api/ -Force
# Admin 项目也引用 Api 程序集
Copy-Item tmp/out/DormManage.Api.dll publish-final/Admin/ -Force

# 阶段 5：ZIP 打包
Write-Host "`n[5/5] ZIP 打包..." -ForegroundColor Yellow
if (Test-Path (Join-Path $ROOT $out)) { Remove-Item (Join-Path $ROOT $out) -Force }
Compress-Archive -Path "publish-final/Admin/*","publish-final/Api/*","publish-final/TrayApp/*" `
                 -DestinationPath $out -CompressionLevel Optimal

Write-Host "`n════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  构建完成：$out" -ForegroundColor Cyan
Write-Host "  日志：$log" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
