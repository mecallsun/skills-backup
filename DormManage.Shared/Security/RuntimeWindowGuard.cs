using System;
using DormManage.Shared.Register;

namespace DormManage.Shared.Security;

/// <summary>
/// v2.13.135 暗桩保护（运行时限 + 与 v2.13.94 RegisterSdk 注册到期日取较早）
///
/// 设计来源：复用「仓库物料汇总」Jinge.MaterialSummary FR-07 暗桩机制
/// - 时间窗口硬编码 2026-06-01 至 2027-01-30
/// - 早于起始：静默退出（不显示任何窗口/日志）
/// - 晚于截止：调用方弹出伪装系统内存错误框（带 5-2-0 隐藏解锁序列）
///
/// 与现有 v2.13.94 注册授权的协作：
/// - 注册有效且到期日 < 暗桩截止 → 实际截止 = 注册到期日（更严格）
/// - 注册无效 / 已过期 → 实际截止 = 暗桩截止
/// - 注册未到期但 > 暗桩截止 → 实际截止 = 暗桩截止
///
/// 调用入口：
/// 1. DormManage.TrayApp/Program.cs（WinForms 启动前同步阻塞）
/// 2. DormManage.Admin/Program.cs（WebApplication.CreateBuilder 前）
/// 3. DormManage.Api/Program.cs（WebApplication.CreateBuilder 前，无 5-2-0 解锁）
/// </summary>
public static class RuntimeWindowGuard
{
    /// <summary>暗桩硬窗口起始（与仓库物料汇总 FR-07 一致）</summary>
    public static readonly DateTime ValidFrom = new(2026, 6, 1, 0, 0, 0);

    /// <summary>暗桩硬窗口截止</summary>
    public static readonly DateTime ValidTo = new(2027, 1, 30, 23, 59, 59);

    /// <summary>
    /// 计算实际生效截止日 = Max(暗桩截止, RegisterSdk.CheckReg().RegDate)
    /// 如注册无效或无 RegDate，则使用暗桩截止
    ///
    /// v2.13.144 用户原话（取较晚 Max）：
    /// - 当注册码解码后的日期 **早于** 暗桩的日期 → 以暗桩的日期为限（取较晚的暗桩）
    /// - 当注册码解码后的日期 **晚于** 暗桩的日期 → 以注册码的日期为限（取较晚的注册码）
    /// - 实际效果：暗桩是兜底机制（不会限制付费用户的注册日期），但暗桩本身是绝对截止日
    ///
    /// v2.13.135 旧版（Min 取较早）已被用户否决：
    /// 旧逻辑下，付费买 2027-07-24 注册码 + 暗桩 2027-01-30 → 实际只到 2027-01-30
    /// 用户实际能用到的时间 = Min(付费时间, 暗桩时间) 损失 6 个月
    /// </summary>
    public static DateTime GetEffectiveDeadline()
    {
        var tamperLimit = ValidTo;
        try
        {
            var reg = RegisterSdk.CheckReg();
            if (reg.RegInt == 1 && reg.RegDate.HasValue)
            {
                // v2.13.144 取较晚者（用户原话语义）：
                // - RegDate < ValidTo  → 截止 = ValidTo（暗桩较晚，取暗桩）
                // - RegDate >= ValidTo → 截止 = RegDate（注册码较晚，取注册码）
                return reg.RegDate.Value > tamperLimit ? reg.RegDate.Value : tamperLimit;
            }
        }
        catch
        {
            // 注册 SDK 读取异常时不影响暗桩逻辑，使用硬窗口
        }
        return tamperLimit;
    }

    /// <summary>
    /// 校验当前系统时间是否在窗口内。
    /// </summary>
    /// <returns>
    /// null = 通过（当前时间在窗口内）
    /// -1 = 早于起始日期（应静默退出）
    /// 0 或正数 = 过期天数（应弹出伪装错误框或阻断）
    /// </returns>
    public static int? CheckExpiry()
    {
        var now = DateTime.Now;

        // 早于起始：返回 -1，调用方应静默退出
        if (now < ValidFrom) return -1;

        var deadline = GetEffectiveDeadline();

        // 晚于截止：返回过期天数（供 UI 提示）
        if (now > deadline)
        {
            var overdueDays = (int)(now.Date - deadline.Date).TotalDays;
            return overdueDays;
        }

        // 在窗口内
        return null;
    }

    /// <summary>
    /// 简化检查：仅返回 true/false，不返回具体天数
    /// </summary>
    public static bool IsWithinWindow()
    {
        return CheckExpiry() == null;
    }
}