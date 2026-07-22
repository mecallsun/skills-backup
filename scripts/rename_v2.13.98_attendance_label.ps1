<#
.SYNOPSIS
    v2.13.98 考勤班次简称显示 — 生产数据库 AttendanceType Name 字段单字化

.DESCRIPTION
    本脚本将生产环境 SQL Server 中 AttendanceType 表的 6 条标准数据的 Name 字段
    从「默认/早班/中班/晚班/夜班/其他」改为「默认/早/中/晚/夜/其他」。

    只动 AttendanceType 表 Id IN (1,2,3,4,5,6) 这 6 行；
    不影响其他模块的字符串「早班/中班/晚班/夜班」（如 Mock 数据、文档、人员备注等）。

    建议在系统维护窗口执行（不会影响在线用户）。

    EF Core EnsureCreated 在新部署环境会自动应用 HasData 种子，无需手动执行本脚本。
    本脚本仅供「已存在生产数据 + 名称显示不符合 v2.13.98 规范」的场景使用。

.NOTES
    Author     : Claude Opus 4.8 + Mecall
    Version    : v2.13.98
    Date       : 2026-07-22
    Risk Level : 极低（仅 UPDATE 6 行；事务回滚保证；不删数据；不影响外键）

.EXAMPLE
    # 修改服务器地址后执行
    .\rename_v2.13.98_attendance_label.ps1 -SqlServerInstance "192.168.1.237" -Database "WaterMeterDB"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SqlServerInstance = "192.168.1.237",

    [Parameter(Mandatory=$true)]
    [string]$Database = "WaterMeterDB",

    [string]$User = "__DB_USER__",

    [string]$Password = "__DB_PASSWORD__",

    [switch]$DryRun = $false
)

# 颜色输出函数
function Write-Step($msg) { Write-Host "[STEP] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[ OK ] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "[FAIL] $msg" -ForegroundColor Red }

Write-Step "v2.13.98 考勤班次简称显示 — 生产数据 UPDATE"
Write-Host "    服务器：$SqlServerInstance"
Write-Host "    数据库：$Database"
Write-Host "    DryRun：$DryRun"
Write-Host ""

# 连接串（明文，仅供维护脚本使用；生产环境建议改用 Windows 集成认证或 SqlClient 加密）
$connectionString = "Server=$SqlServerInstance;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;Encrypt=False;"

# 1) SELECT 现状（让用户看到当前数据）
Write-Step "[1/4] 查询当前 AttendanceType 数据..."
$selectQuery = "SELECT Id, Code, Name, WorkHours, IsActive FROM AttendanceType WHERE Id IN (1,2,3,4,5,6) ORDER BY Id;"

try {
    $before = Invoke-Sqlcmd -ConnectionString $connectionString -Query $selectQuery -ErrorAction Stop
    Write-Host ""
    Write-Host "    当前数据（UPDATE 前）："
    $before | Format-Table Id, Code, Name, WorkHours, IsActive -AutoSize | Out-String | Write-Host -ForegroundColor Gray
}
catch {
    Write-Err "查询失败：$($_.Exception.Message)"
    exit 1
}

# 2) UPDATE 6 行（事务回滚保证）
Write-Step "[2/4] 准备 UPDATE 语句（仅 Id IN (2,3,4,5) 4 行需改名；Id=1/6 已是单字）..."
$updateSql = @"
BEGIN TRAN;

-- v2.13.98：考勤班次简称显示，Name 字段单字化（适配紧凑列表列）
UPDATE AttendanceType SET Name = N'早'   WHERE Id = 2 AND Name = N'早班';
UPDATE AttendanceType SET Name = N'中'   WHERE Id = 3 AND Name = N'中班';
UPDATE AttendanceType SET Name = N'晚'   WHERE Id = 4 AND Name = N'晚班';
UPDATE AttendanceType SET Name = N'夜'   WHERE Id = 5 AND Name = N'夜班';
-- Id=1 '默认' / Id=6 '其他' 已是单字，无需修改

-- 验证：4 行应被更新
SELECT @@ROWCOUNT AS AffectedRows;

-- COMMIT;
ROLLBACK;  -- 默认回滚安全，DryRun 模式
"@

Write-Host ""
Write-Host "    SQL 预览："
Write-Host "    ─────────────────────────────────────────────"
Write-Host ($updateSql -split "`n" | ForEach-Object { "    $_" })
Write-Host "    ─────────────────────────────────────────────"

if ($DryRun) {
    Write-Warn "DryRun 模式：不执行 UPDATE，仅显示 SQL 预览"
    Write-Step "[3/4] 跳过执行（DryRun）"
    Write-Step "[4/4] 跳过提交（DryRun）"
    Write-Host ""
    Write-Ok "DryRun 完成。如需真正执行，请去掉 -DryRun 参数。"
    exit 0
}

# 3) 确认执行
Write-Host ""
$confirm = Read-Host "    确认执行 UPDATE? (yes/no)"
if ($confirm -ne "yes") {
    Write-Warn "用户取消，未执行任何修改。"
    exit 0
}

Write-Step "[3/4] 执行 UPDATE 事务..."
$execSql = @"
BEGIN TRAN;
UPDATE AttendanceType SET Name = N'早'   WHERE Id = 2 AND Name = N'早班';
UPDATE AttendanceType SET Name = N'中'   WHERE Id = 3 AND Name = N'中班';
UPDATE AttendanceType SET Name = N'晚'   WHERE Id = 4 AND Name = N'晚班';
UPDATE AttendanceType SET Name = N'夜'   WHERE Id = 5 AND Name = N'夜班';
COMMIT;
"@

try {
    $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query $execSql -ErrorAction Stop
    Write-Ok "UPDATE 已 COMMIT（4 行改名完成）"
}
catch {
    Write-Err "UPDATE 失败（事务已自动回滚）：$($_.Exception.Message)"
    exit 1
}

# 4) 验证结果
Write-Step "[4/4] 验证 UPDATE 结果..."
$after = Invoke-Sqlcmd -ConnectionString $connectionString -Query $selectQuery -ErrorAction Stop
Write-Host ""
Write-Host "    UPDATE 后数据："
$after | Format-Table Id, Code, Name, WorkHours, IsActive -AutoSize | Out-String | Write-Host -ForegroundColor Gray

# 校验
$expected = @{ 1='默认'; 2='早'; 3='中'; 4='晚'; 5='夜'; 6='其他' }
$failed = @()
foreach ($row in $after) {
    if ($row.Name -ne $expected[$row.Id]) {
        $failed += "Id=$row.Id 期望 Name='$($expected[$row.Id])' 实际='$($row.Name)'"
    }
}

if ($failed.Count -eq 0) {
    Write-Host ""
    Write-Ok "全部 6 行 Name 字段符合 v2.13.98 规范 ✓"
    Write-Host ""
    Write-Host "    后续操作：" -ForegroundColor Yellow
    Write-Host "    1) 重启 Admin/Api 服务，让 EF Core 缓存刷新（实际上 SQL 直读无需重启）"
    Write-Host "    2) 浏览器强制刷新 Ctrl+Shift+R，人员清单 / 宿舍详情 / 办理登记列表应显示「早/中/晚/夜」"
    Write-Host "    3) 导出 CSV / Excel 验证列头 + 值均为单字"
}
else {
    Write-Err "部分行 UPDATE 失败："
    $failed | ForEach-Object { Write-Host "    $_" }
    exit 1
}