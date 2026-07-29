namespace DormManage.Shared.Extensions;

/// <summary>
/// v2.13.193：账号有效期判断助手
/// 统一 LoginAsync + OnValidatePrincipal + 前端 Badge 显示的判断逻辑
///
/// 规则（deny-by-default）：过期当天即视为已过期（使用 >= 语义）
/// - Today >= ExpiresAt.Date → 已过期
/// - Today <  ExpiresAt.Date → 未过期
///
/// 修复历史：
/// - v2.13.93 引入 ExpiresAt 字段，使用 > 严格大于判断
/// - v2.13.193 修正为 >= 语义（过期当天即视为已过期，更严格安全策略）
/// </summary>
public static class UserExpiryHelper
{
    /// <summary>判断账号是否已过期（null = 永久有效）</summary>
    public static bool IsExpired(DateTime? expiresAt, DateTime? now = null)
    {
        if (!expiresAt.HasValue) return false;  // 永久有效
        var today = (now ?? DateTime.Today).Date;
        return today >= expiresAt.Value.Date;  // v2.13.193: 过期当天即视为已过期
    }

    /// <summary>计算到过期的剩余天数（负数 = 已过期 N 天；null = 永久）</summary>
    public static int? DaysUntilExpiry(DateTime? expiresAt, DateTime? now = null)
    {
        if (!expiresAt.HasValue) return null;
        return (int)(expiresAt.Value.Date - (now ?? DateTime.Today).Date).TotalDays;
    }
}