# v2.13.135 BitMono 后处理加壳脚本（仅 TrayApp）
# 设计来源：仓库物料汇总 Jinge.MaterialSummary/scripts/ 验证通过的 6 项稳定保护
# 使用：.\scripts\bitmono_protect_trayapp.ps1
# 注意：必须先执行 dotnet publish -c Release 完成 publish-final/TrayApp 部署

$ErrorActionPreference = "Stop"
$ROOT = (Resolve-Path "$PSScriptRoot\..").Path
$publishDir = Join-Path $ROOT "publish-final\TrayApp"
$dll = Join-Path $publishDir "DormManage.TrayApp.dll"
$bakDll = "$dll.bak"
$protectionsJson = Join-Path $ROOT "scripts\protections.json"
$protectedOut = Join-Path $ROOT "protected"

# 1. 前置检查
if (-not (Test-Path $dll)) {
    Write-Error "[BitMono] DLL 未找到: $dll`n请先执行: dotnet publish DormManage.TrayApp -c Release -o $publishDir"
    exit 1
}
if (-not (Test-Path $protectionsJson)) {
    Write-Error "[BitMono] 保护配置未找到: $protectionsJson"
    exit 1
}

# 2. 安装 BitMono 工具（如未安装）
$bitmonoInstalled = & dotnet tool list -g 2>&1 | Select-String "BitMono.GlobalTool"
if (-not $bitmonoInstalled) {
    Write-Host "[BitMono] 正在安装 BitMono.GlobalTool 0.43.0..." -ForegroundColor Yellow
    & dotnet tool install --global BitMono.GlobalTool --version 0.43.0
    if ($LASTEXITCODE -ne 0) {
        Write-Error "[BitMono] 安装失败"; exit 1
    }
}

# 3. 备份原 DLL
Copy-Item $dll $bakDll -Force
Write-Host "[BitMono] 已备份原 DLL 到 $bakDll" -ForegroundColor Cyan

# 4. 执行加壳
if (Test-Path $protectedOut) { Remove-Item $protectedOut -Recurse -Force }
New-Item -ItemType Directory -Path $protectedOut -Force | Out-Null

Write-Host "[BitMono] 正在加壳 DormManage.TrayApp.dll..." -ForegroundColor Yellow
& bitmono -f $dll `
          -l (Split-Path $dll) `
          -o $protectedOut `
          -n DormManage.TrayApp.dll `
          --protections-file $protectionsJson `
          --no-watermark
if ($LASTEXITCODE -ne 0) {
    Write-Warning "[BitMono] 加壳失败，恢复原 DLL"
    Copy-Item $bakDll $dll -Force
    exit 1
}

# 5. 替换回 publish-final/TrayApp/
$protectedDll = Join-Path $protectedOut "DormManage.TrayApp.dll"
if (Test-Path $protectedDll) {
    Copy-Item $protectedDll $dll -Force
    Remove-Item $protectedOut -Recurse -Force
    Remove-Item $bakDll -Force
    Write-Host "[BitMono] TrayApp 加壳完成 ✅" -ForegroundColor Green
    Write-Host "[BitMono] 验证: ildasm $dll 应报错 (AntiILdasm 生效)" -ForegroundColor Gray
} else {
    Write-Error "[BitMono] 加壳输出未找到: $protectedDll"
    Copy-Item $bakDll $dll -Force
    exit 1
}