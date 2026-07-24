// verify_cdkey_v2.13.142.csx - 完整 CDKEY 验证脚本
#:package System.Security.Cryptography.Common@5.0.0
using System;
using System.Security.Cryptography;
using System.Text;

const string SECRET_KEY = "JINGE-DORM-MANAGE-2026-LICENSE";

// 用户提供的数据
const string SN = "BFEBFBFF000A06A4AA2E3B0E";
const string CDKEY_DISPLAY = "3B55C-A8SE9-38W5B-FB456-4A83F";
const string LTD = "广东金戈新材料股份有限公司";
const int EXPECTED_YEAR = 2027;
const int EXPECTED_MONTH = 7;
const int EXPECTED_DAY = 24;

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("  CDKEY 验证完整分析 — v2.13.142 机器码无连接符规则配套验证");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

// ===== 步骤 1：CDKEY 基础格式校验 =====
Console.WriteLine("【步骤 1】CDKEY 基础格式校验");
Console.WriteLine($"  输入 CDKEY（带连字符）: {CDKEY_DISPLAY}");
var cdkeyRaw = CDKEY_DISPLAY.Replace("-", "").ToUpperInvariant();
Console.WriteLine($"  去连字符 + upper      : {cdkeyRaw}");
Console.WriteLine($"  长度                  : {cdkeyRaw.Length} 位（期望 25 位 hex）");
Console.WriteLine();

var invalidChars = new System.Text.StringBuilder();
foreach (var c in cdkeyRaw)
{
    if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
        invalidChars.Append(c).Append(' ');
}
if (invalidChars.Length > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ❌ 检测到非法字符: '{invalidChars.ToString().Trim()}'");
    Console.WriteLine($"     （TryNormalizeCDKey 严格校验 0-9 A-F）");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("  🚫 结论：注册码校验 FAIL（步骤 1 就被拒绝）");
    return;
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✅ 字符集合法（0-9 A-F）");
    Console.ResetColor();
}

// ===== 步骤 2：SN 长度校验 =====
Console.WriteLine();
Console.WriteLine("【步骤 2】SN 机器码校验");
Console.WriteLine($"  SN: {SN}");
Console.WriteLine($"  SN 长度: {SN.Length} 位（期望 24 位 hex）");
var snRaw = SN.Replace("-", "").ToUpperInvariant();
if (snRaw.Length == 24)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✅ SN 长度合规");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ❌ SN 长度不合规");
    Console.ResetColor();
    return;
}

// ===== 步骤 3：验证段（前 20 位）MD5 计算 =====
Console.WriteLine();
Console.WriteLine("【步骤 3】验证段（前 20 位）MD5 计算");
var verifyInput = snRaw + "|" + LTD.ToUpperInvariant() + "|" + SECRET_KEY;
Console.WriteLine($"  MD5 输入: {verifyInput}");
using var md5 = MD5.Create();
var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(verifyInput));
var hexFull = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
var expectedVerify = hexFull.Substring(0, 20);
Console.WriteLine($"  MD5(hex full): {hexFull}");
Console.WriteLine($"  预期验证段(20位): {expectedVerify}");

var actualVerify = cdkeyRaw.Substring(0, 20);
Console.WriteLine($"  CDKEY 前 20 位    : {actualVerify}");
Console.Write($"  验证段匹配        : ");
if (actualVerify == expectedVerify)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ MATCH");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ MISMATCH");
    Console.ResetColor();
}

// ===== 步骤 4：日期段（后 5 位）解码 =====
Console.WriteLine();
Console.WriteLine("【步骤 4】日期段（后 5 位）解码");
var dateStr = cdkeyRaw.Substring(20, 5);
Console.WriteLine($"  CDKEY 日期段     : {dateStr}");
try
{
    var year = 2000 + Convert.ToInt32(dateStr.Substring(0, 2), 16);
    var month = Convert.ToInt32(dateStr.Substring(2, 1), 16) + 1;
    var day = Convert.ToInt32(dateStr.Substring(3, 2), 16);
    var actualDate = new DateTime(year, Math.Max(1, Math.Min(month, 12)), Math.Max(1, Math.Min(day, 28)));
    Console.WriteLine($"  实际到期日        : {actualDate:yyyy-MM-dd}");
    Console.WriteLine($"  预期到期日        : {EXPECTED_YEAR}-{EXPECTED_MONTH:D2}-{EXPECTED_DAY:D2}");
    Console.WriteLine();
    Console.WriteLine($"  日期段编码算法详情:");
    Console.WriteLine($"    year = 2000 + hex(0x{dateStr.Substring(0, 2)}) = 2000 + {year - 2000} = {year}");
    Console.WriteLine($"    month = hex(0x{dateStr.Substring(2, 1)}) + 1 = {month - 1} + 1 = {month}");
    Console.WriteLine($"    day = hex(0x{dateStr.Substring(3, 2)}) = {day}（capped at 28）");
    Console.WriteLine();

    if (actualDate.Year == EXPECTED_YEAR && actualDate.Month == EXPECTED_MONTH && actualDate.Day == EXPECTED_DAY)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✅ 到期日匹配: {actualDate:yyyy-MM-dd}");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ❌ 到期日不匹配");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ❌ 解码异常: {ex.Message}");
    Console.ResetColor();
}

// ===== 步骤 5：2027-07-24 对应的日期段编码 =====
Console.WriteLine();
Console.WriteLine("【步骤 5】2027-07-24 对应的正确日期段编码");
var targetYear = 2027;
var targetMonth = 7;
var targetDay = 24;
var yearHex = (targetYear - 2000).ToString("X2");
var monthHex = (targetMonth - 1).ToString("X");
var dayHex = targetDay.ToString("X2");
var targetDateStr = yearHex + monthHex + dayHex;
Console.WriteLine($"  year=2027 → 0x{yearHex}（YY=27）");
Console.WriteLine($"  month=7 → 0x{monthHex}（M-1=6）");
Console.WriteLine($"  day=24 → 0x{dayHex}");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"  ✅ 正确日期段应为: {targetDateStr}");
Console.ResetColor();
Console.WriteLine();

// ===== 步骤 6：完整正确的 25 hex CDKEY 应该长什么样 =====
Console.WriteLine("【步骤 6】用户预期 CDKEY 应是怎样的");
Console.WriteLine($"  验证段 (前 20 hex): {expectedVerify}");
Console.WriteLine($"  日期段 (后 5 hex) : {targetDateStr}");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"  ✅ 完整 25 hex (raw)    : {expectedVerify}{targetDateStr}");
Console.ResetColor();
// 5-5-5-5-5 分组展示
var full25 = expectedVerify + targetDateStr;
var grouped = $"{full25.Substring(0, 5)}-{full25.Substring(5, 5)}-{full25.Substring(10, 5)}-{full25.Substring(15, 5)}-{full25.Substring(20, 5)}";
Console.WriteLine($"  ✅ 带连字符显示 (5-5-5-5-5): {grouped}");
Console.WriteLine();

// ===== 步骤 7：综合结论 =====
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("  综合结论");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("  🚫 用户提供的 CDKEY 校验结果: FAIL");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine("  问题清单:");
var problemNo = 1;
if (invalidChars.Length > 0)
{
    Console.WriteLine($"  {problemNo++}. ❌ 字符集非法: CDKEY 含 '{invalidChars.ToString().Trim()}'，仅允许 0-9 和 A-F");
    Console.WriteLine($"       当前 CDKEY: 3B55C-A8SE9-38W5B-FB456-4A83F");
    Console.WriteLine($"                  (   ↑S↑ ↑W↑                 )");
}
if (true)
{
    Console.WriteLine($"  {problemNo++}. ❌ 日期段解码不一致: 输入的 4A83F 解码为 2074-09-28，预期为 2027-07-24");
}
Console.WriteLine();
Console.WriteLine("  正确的 CDKEY 应该是:");
Console.WriteLine($"    Raw 25 hex  : {expectedVerify}{targetDateStr}");
Console.WriteLine($"    带连字符显示: {grouped}");
Console.WriteLine();
Console.WriteLine("  说明:");
Console.WriteLine($"    * 验证段（前 20 hex）必须由供应商按 SN+LTDName+SECRET_KEY 重算");
Console.WriteLine($"    * 日期段（后 5 hex）必须是 (2027-07-24) → 1B618");
