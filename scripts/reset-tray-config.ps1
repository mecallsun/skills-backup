# v2.13.156 一键重置托盘配置脚本
#
# 用途：当 DormManage.TrayApp 启动报「Api 可执行文件不存在」且
#      appsettings.json 中的 ApiExecutable / AdminExecutable 路径失效时，
#      把当前目录的 TrayApp 配置文件重置为自动探测到的正确路径。
#
# 用法（管理员 CMD 或 PowerShell，cd 到 TrayApp.exe 所在目录）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File reset-tray-config.ps1
#
# 该脚本不会删除文件，只重置 Path 字段为新默认值。

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$configPath = Join-Path (Get-Location) 'appsettings.json'
if (-not (Test-Path $configPath)) {
    Write-Error "未找到 appsettings.json：$configPath  `n请在 TrayApp.exe 所在目录运行本脚本。"
    exit 1
}

Write-Host "=== 托盘配置一键重置 v2.13.156 ===" -ForegroundColor Cyan
Write-Host "配置: $configPath"

# 备份原文件
$bakPath = "$configPath.bak.{0:yyyyMMddHHmmss}" -f (Get-Date)
Copy-Item $configPath $bakPath
Write-Host "原配置已备份至: $bakPath"

# 自动探测 Api / Admin EXE 路径
function Find-Exe([string]$dir) {
    foreach ($candidate in @(
        (Join-Path $dir 'Api\DormManage.Api.exe'),
        (Join-Path $dir '..\Api\DormManage.Api.exe'),
        (Join-Path $dir 'Admin\DormManage.Admin.exe'),
        (Join-Path $dir '..\Admin\DormManage.Admin.exe')
    )) {
        if (Test-Path $candidate) { return (Resolve-Path $candidate).Path }
    }
    return $null
}

# 加载并修改 JSON（处理 UTF-8 BOM）
$bytes = [System.IO.File]::ReadAllBytes($configPath)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    $content = $utf8NoBom.GetString($bytes, 3, $bytes.Length - 3)
} else {
    $content = $utf8NoBom.GetString($bytes)
}
$j = $content | ConvertFrom-Json

# 当前所在目录 = TrayApp.exe 目录
$here = (Get-Location).Path

$apiPath = Find-Exe $here
if ($apiPath -and $apiPath -like '*\Api\DormManage.Api.exe') {
    $j.Tray.ApiExecutable = $apiPath
    Write-Host "✅ Api: $apiPath" -ForegroundColor Green
} else {
    Write-Warning "❌ 当前目录未找到 DormManage.Api.exe，请确认 Api/ 子目录存在。"
}

$adminPath = Find-Exe $here
if ($adminPath -and $adminPath -like '*\Admin\DormManage.Admin.exe') {
    $j.Tray.AdminExecutable = $adminPath
    Write-Host "✅ Admin: $adminPath" -ForegroundColor Green
} else {
    Write-Warning "❌ 当前目录未找到 DormManage.Admin.exe，请确认 Admin/ 子目录存在。"
}

$newContent = ($j | ConvertTo-Json -Depth 10)
[System.IO.File]::WriteAllText($configPath, $newContent, $utf8NoBom)

Write-Host "`n=== 重置完成，请重新启动 DormManage.TrayApp.exe ===" -ForegroundColor Cyan
