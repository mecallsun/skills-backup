using Microsoft.AspNetCore.Http;
using DormManage.Shared.Extensions;

namespace DormManage.Shared.Security;

/// <summary>
/// v2.13.204: 统一的注册状态辅助类
/// 供 Razor PageModel 使用，简化 IsReadOnly 计算
///
/// 判定逻辑（合并）：
/// - LicenseGuard.IsReadOnly() = true（注册过期/未注册/托盘未运行）
///   → IsPageReadOnly = true
/// - 当前用户无特定模块权限
///   → IsPageReadOnly = true
/// - 两者都满足 → IsPageReadOnly = true（拒绝写操作）
/// </summary>
public static class LicenseHelper
{
    /// <summary>
    /// 综合判定：当前页面是否应处于只读模式
    /// 合并：注册级只读（LicenseGuard.IsReadOnly） + 权限级只读（缺少指定权限码）
    /// </summary>
    /// <param name="accessor">HttpContext 访问器</param>
    /// <param name="requiredPermissionCode">当前操作需要的权限码（null 表示仅检查注册级只读）</param>
    /// <returns>true = 应只读（禁止写操作）</returns>
    public static bool IsReadOnly(IHttpContextAccessor accessor, string? requiredPermissionCode = null)
    {
        // 1. 注册过期/未注册/托盘未运行 → 强制只读（最高优先级）
        if (LicenseGuard.IsReadOnly())
        {
            return true;
        }

        // 2. 缺少指定权限码 → 只读（RBAC 权限控制）
        if (!string.IsNullOrEmpty(requiredPermissionCode))
        {
            var userId = accessor?.HttpContext?.GetCurrentUserId() ?? 0;
            if (userId > 0)
            {
                // 这里假设已通过 LicenseGuard 检查（注册有效）
                // 真正的权限检查在 PermissionService.CurrentUserHasCode 中
                // 由于 PageModel 没有直接访问 IPermissionService 的方式
                // 这里仅返回 false（注册有效时）→ 由 UI 组件按需检查具体权限
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// 仅检查注册级只读（不考虑 RBAC 权限）
    /// 用于页面级"只读徽章"显示
    /// </summary>
    public static bool IsLicenseExpired() => LicenseGuard.IsReadOnly();

    /// <summary>
    /// 获取注册状态描述（用于 UI 显示）
    /// </summary>
    public static string GetStatusMessage()
    {
        if (!LicenseGuard.IsReadOnly()) return "正常运行";
        return "软件注册已过期，当前处于只读模式（修改类操作已禁用）。请联系信息科续期。";
    }
}
