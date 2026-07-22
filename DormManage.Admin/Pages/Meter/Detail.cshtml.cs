using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 智能抄表详情页面模型（v2.13.41 100% 原型对齐）
///
/// 改造点（vs 原型 meter/detail.html）：
/// 1. 补 CreatedAt（ServerCreatedAt）/ DeviceSn / ReadDate 字段
/// 2. 补 CorrectionReason / CorrectedBy 字段（用于 Remark 历史格式化）
/// 3. 提供按状态的操作按钮上下文（StatusAction 可用性）
///
/// v2.13.88 RBAC：详情页只读模式（无 meter:edit 权限时隐藏「修正/补录/删除」按钮，仅显示「返回列表」）
/// </summary>
public class DetailModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IPermissionService _perm;

    public DetailModel(DormDbContext db, IPermissionService perm)
    {
        _db = db;
        _perm = perm;
    }

    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public MeterRecordDetailDto Record { get; set; } = new();
    public UsageInfo Usage { get; set; } = new();
    public ComparisonInfo? Comparison { get; set; }

    /// <summary>
    /// v2.13.41 新增：详情页操作上下文（按状态决定可用按钮）
    /// </summary>
    public DetailActions Actions { get; set; } = new();

    /// <summary>v2.13.88 RBAC：当前用户是否可编辑/修正抄表</summary>
    public bool CanEdit { get; set; }
    /// <summary>v2.13.88 RBAC：当前用户是否可删除抄表</summary>
    public bool CanDeletePerm { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var record = await _db.MeterRecords.FindAsync(Id);
        if (record == null)
        {
            TempData["ErrorMessage"] = "智能抄表不存在";
            return RedirectToPage("/Meter/Index");
        }

        Record = new MeterRecordDetailDto
        {
            Id = record.Id,
            DormCode = record.DormCode,
            ReadMonth = record.ReadMonth,
            ColdMeter = record.ColdMeter,
            HotMeter = record.HotMeter,
            ElectricMeter = record.ElectricMeter,
            Operator = record.Operator,
            OperatorUserId = record.OperatorUserId,
            // v2.13.89：JOIN SysUser 取 DisplayName，fallback 到 Operator 字符串
            OperatorDisplayName = record.OperatorUserId.HasValue
                ? await _db.SysUsers.Where(u => u.Id == record.OperatorUserId.Value)
                    .Select(u => u.DisplayName).FirstOrDefaultAsync() ?? record.Operator
                : record.Operator,
            Status = record.Status,
            StatusName = record.GetStatusName(),
            Remark = record.Remark,
            // v2.13.41 新增字段
            DeviceSn = record.DeviceSn,
            CreatedAt = record.ServerCreatedAt,
            ReadDate = record.ReadDate,
            CorrectionReason = record.CorrectionReason,
            CorrectedBy = record.CorrectedBy,
            CorrectedAt = record.CorrectedAt
        };

        // 计算用量 — 取该宿舍所有正常记录，在内存中找上月
        var allRecords = await _db.MeterRecords
            .Where(r => r.DormCode == record.DormCode && r.Status == 1)
            .OrderByDescending(r => r.ReadMonth)
            .ToListAsync();
        var prevRecord = allRecords.Skip(1).FirstOrDefault();

        Usage.ColdUsage = Math.Max(0, record.ColdMeter - (prevRecord?.ColdMeter ?? 0));
        Usage.HotUsage = Math.Max(0, record.HotMeter - (prevRecord?.HotMeter ?? 0));
        Usage.ElectricUsage = Math.Max(0, record.ElectricMeter - (prevRecord?.ElectricMeter ?? 0));

        if (prevRecord != null)
        {
            Comparison = new ComparisonInfo
            {
                PreviousCold = prevRecord.ColdMeter,
                PreviousHot = prevRecord.HotMeter,
                PreviousElectric = prevRecord.ElectricMeter,
                PreviousReadMonth = prevRecord.ReadMonth,
                CurrentCold = record.ColdMeter,
                CurrentHot = record.HotMeter,
                CurrentElectric = record.ElectricMeter
            };
        }

        // v2.13.88 RBAC：检测 meter:edit / meter:delete 权限（用于详情页按钮可见性）
        var userId = HttpContext.GetCurrentUserId();
        CanEdit = await _perm.HasPermissionCodeAsync(userId, "meter:edit");
        CanDeletePerm = await _perm.HasPermissionCodeAsync(userId, "meter:delete");

        // v2.13.41 按状态决定操作按钮可用性 + v2.13.88 RBAC 二次校验
        // Status: 0=草稿/占位, 1=正常, 2=已修正, 3=已取消
        Actions = new DetailActions
        {
            // 删除需 meter:delete 权限 + 状态允许（草稿/取消）
            CanDelete = CanDeletePerm && (record.Status == 0 || record.Status == 3),
            // 修正需 meter:edit 权限 + 仅正常记录
            CanEdit = CanEdit && record.Status == 1,
            // 补录需 meter:entry 权限 + 占位/取消
            CanReEntry = await _perm.HasPermissionCodeAsync(userId, "meter:entry") && (record.Status == 0 || record.Status == 3),
            // 二次修正需 meter:edit 权限 + 正常/已修正
            CanCorrect = CanEdit && (record.Status == 1 || record.Status == 2)
        };

        return Page();
    }
}

public class MeterRecordDetailDto
{
    public long Id { get; set; }
    public string DormCode { get; set; } = "";
    public string ReadMonth { get; set; } = "";
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    public string Operator { get; set; } = "";
    // v2.13.89：抄表员 UserId FK + DisplayName（JOIN 派生）
    public int? OperatorUserId { get; set; }
    public string? OperatorDisplayName { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }

    // v2.13.41 新增
    public string? DeviceSn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateOnly? ReadDate { get; set; }
    public string? CorrectionReason { get; set; }
    public string? CorrectedBy { get; set; }
    public DateTime? CorrectedAt { get; set; }
}

public class UsageInfo
{
    public decimal ColdUsage { get; set; }
    public decimal HotUsage { get; set; }
    public decimal ElectricUsage { get; set; }
}

public class ComparisonInfo
{
    public decimal PreviousCold { get; set; }
    public decimal PreviousHot { get; set; }
    public decimal PreviousElectric { get; set; }
    public string PreviousReadMonth { get; set; } = "";
    public decimal CurrentCold { get; set; }
    public decimal CurrentHot { get; set; }
    public decimal CurrentElectric { get; set; }
}

/// <summary>
/// v2.13.41 新增：详情页操作按钮上下文
/// </summary>
public class DetailActions
{
    public bool CanDelete { get; set; }
    public bool CanEdit { get; set; }
    public bool CanReEntry { get; set; }
    public bool CanCorrect { get; set; }
}