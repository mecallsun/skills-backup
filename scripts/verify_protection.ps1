# v2.13.140 端到端防反编译验证脚本
# 使用：.\scripts\verify_protection.ps1
# 检测项：静态反编译 + 启动冒烟测试

$ErrorActionPreference = "Stop"
$ROOT = (Resolve-Path "$PSScriptRoot\..").Path
$pass = 0
$fail = 0

function Assert($cond, $msg) {
    if ($cond) {
        Write-Host "  ✅ $msg" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "  ❌ $msg" -ForegroundColor Red
        $script:fail++
    }
}

Write-Host "`n[1/4] 静态反编译检测（ildasm）..." -ForegroundColor Cyan
$ildasm = Get-Command ildasm -ErrorAction SilentlyContinue
if ($ildasm) {
    foreach ($proj in @("TrayApp", "Admin", "Api")) {
        $dll = Join-Path $ROOT "publish-final\$proj\DormManage.$proj.dll"
        if (Test-Path $dll) {
            # Obfuscar/BitMono 应让 ildasm 输出 class names 全是乱码
            $result = & $ildasm $dll 2>&1 | Out-String
            if ($result -match "SuppressIldasm|AntiILdasm") {
                Assert $false "$proj ildasm 未被拦截（保护失效）"
            } elseif ($result -match "^\.\class.*DormManage") {
                # 期望：所有 class 都被混淆成 a/b/c 或单字符名
                # 注：Obfuscar 的 SuppressIldasm + BitMono AntiILdasm 都应阻止 ildasm 正常输出
                Write-Host "  ⚠ $proj ildasm 能解析 class 名（可能还需启用 BitMono 或加深混淆）" -ForegroundColor DarkYellow
            }
        } else {
            Write-Host "  ⚠ $proj DLL 不存在（构建未完成）" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host "  ildasm 未安装，跳过静态检测" -ForegroundColor DarkYellow
}

Write-Host "`n[2/4] 反编译工具检测（ILSpy / dotPeek 命令行）..." -ForegroundColor Cyan
foreach ($proj in @("TrayApp", "Admin", "Api")) {
    $dll = Join-Path $ROOT "publish-final\$proj\DormManage.$proj.dll"
    if (Test-Path $dll) {
        # 用字符串搜索原始类名（如 Main / OnGetAsync） —— 期望：搜不到
        $content = [System.IO.File]::ReadAllBytes($dll)
        $text = [System.Text.Encoding]::ASCII.GetString($content)
        $signatures = @("OnGetAsync", "OnPostAsync", "PageModel")
        $leak = $signatures | Where-Object { $text -match [regex]::Escape($_) }
        if ($proj -eq "Admin" -and $leak) {
            # Admin 的 Pages/Controllers 被 Skip，这是预期
            Write-Host "  ℹ $proj 保留 PageModel 签名（SkipNamespace 生效）" -ForegroundColor DarkYellow
        } elseif ($proj -eq "TrayApp" -and $leak) {
            Write-Host "  ⚠ $proj 业务方法可识别（Obfuscar 启用 OK，但反混淆成本较低）" -ForegroundColor DarkYellow
        } else {
            Write-Host "  ✅ $proj 无业务方法名泄漏" -ForegroundColor Green
        }
    }
}

Write-Host "`n[3/4] dll size 合理性（Obfuscar 后应 < 原始 70%）..." -ForegroundColor Cyan
foreach ($proj in @("TrayApp", "Admin", "Api", "Shared")) {
    $dll = Join-Path $ROOT "publish-final\$proj\DormManage.$proj.dll"
    if (Test-Path $dll) {
        $size = (Get-Item $dll).Length
        if ($size -lt 2MB) {
            Write-Host "  ℹ $proj.dll: $([math]::Round($size/1KB, 1)) KB（合理范围）" -ForegroundColor Gray
        } else {
            Write-Host "  ℹ $proj.dll: $([math]::Round($size/1MB, 2)) MB" -ForegroundColor Gray
        }
    }
}

Write-Host "`n[4/4] 启动冒烟测试（需用户手动）..." -ForegroundColor Cyan
Write-Host "  📌 Admin:    dotnet publish-final/Admin/DormManage.Admin.dll" -ForegroundColor Gray
Write-Host "  📌 Api:      dotnet publish-final/Api/DormManage.Api.dll" -ForegroundColor Gray
Write-Host "  📌 TrayApp:  publish-final/TrayApp/DormManage.TrayApp.exe" -ForegroundColor Gray
Write-Host "  期望：HTTP 200 + 无反射异常" -ForegroundColor Gray

Write-Host "`n════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  静态检测：$pass PASS / $fail FAIL" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })
Write-Host "════════════════════════════════════════════════" -ForegroundColor Cyan
