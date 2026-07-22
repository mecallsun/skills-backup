using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using DormManage.Shared.Extensions;
using DormManage.Shared.Services;

namespace DormManage.Admin.Extensions;

/// <summary>
/// v2.13.92 字段权限 HtmlHelper 扩展：
///   - IsFieldHiddenAsync(html, "employee.realname")  判断字段对当前用户是否应该隐藏（true=隐藏）
///   - RenderFieldAsync(html, "employee.realname", value)  按权限条件渲染字段值或占位符
///
/// 使用场景：
///   - 列表 thead/tbody 整列条件渲染：`@if (!await Html.IsFieldHiddenAsync("employee.realname")) { <th>姓名</th> }`
///   - 详情页单点条件渲染：`@await Html.RenderFieldAsync("employee.phone", Model.Employee.Phone)`
///   - 表单 input 占位渲染：通常不需要（用户编辑自己的数据不应被屏蔽）
/// </summary>
public static class FieldPermissionHtmlHelperExtensions
{
    /// <summary>当前字段对当前用户是否应该隐藏（true=隐藏）</summary>
    public static async Task<bool> IsFieldHiddenAsync(this IHtmlHelper html, string fieldKey)
    {
        if (string.IsNullOrEmpty(fieldKey)) return false;
        var ctx = html.ViewContext?.HttpContext;
        if (ctx == null) return false;

        var perm = ctx.RequestServices.GetService(typeof(IPermissionService)) as IPermissionService;
        if (perm == null) return false;

        var userId = ctx.GetCurrentUserId();
        if (userId <= 0) return false;

        // HttpContext.Items 缓存：同一请求内多次调用只查一次 DB
        var cacheKey = "__FIELD_HIDDEN_KEYS__";
        if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is HashSet<string> keys)
        {
            return keys.Contains(fieldKey);
        }

        var hiddenKeys = await perm.GetHiddenFieldKeysAsync(userId);
        ctx.Items[cacheKey] = hiddenKeys;
        return hiddenKeys.Contains(fieldKey);
    }

    /// <summary>条件渲染：字段不隐藏时输出 value，否则输出占位符</summary>
    public static async Task<IHtmlContent> RenderFieldAsync(this IHtmlHelper html, string fieldKey, string? value, string placeholder = "***")
    {
        if (await IsFieldHiddenAsync(html, fieldKey))
            return new HtmlString($"<span class=\"text-muted\">{System.Net.WebUtility.HtmlEncode(placeholder)}</span>");
        var safe = value ?? string.Empty;
        return new HtmlString(System.Net.WebUtility.HtmlEncode(safe));
    }
}