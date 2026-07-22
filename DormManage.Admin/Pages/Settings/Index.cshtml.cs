using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Data;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// 系统设置页面模型（v2.13.67：改为 partial class 以支持嵌入 UserPanelPartial / RolePanelPartial）
/// </summary>
public partial class IndexModel : PageModel
{
    private readonly DormDbContext _db;

    public IndexModel(DormDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    /// <summary>
    /// 当前激活的 Tab
    /// </summary>
    public string ActiveTab => Tab ?? "service";

    /// <summary>
    /// PDA 服务端口
    /// </summary>
    public int PdaPort { get; set; } = 5100;

    /// <summary>
    /// Web 服务端口
    /// </summary>
    public int WebPort { get; set; } = 5001;

    /// <summary>
    /// Api 服务状态（来自托盘 IPC）
    /// </summary>
    public string ApiState { get; set; } = "Unknown";

    /// <summary>
    /// Admin 服务状态（来自托盘 IPC）
    /// </summary>
    public string AdminState { get; set; } = "Unknown";

    /// <summary>
    /// 托盘是否可达
    /// </summary>
    public bool TrayReachable { get; set; }

    /// <summary>
    /// PDA 服务是否运行中
    /// </summary>
    public bool PdaServiceRunning { get; set; } = true;

    /// <summary>
    /// Web 服务是否运行中
    /// </summary>
    public bool WebServiceRunning { get; set; } = true;

    /// <summary>
    /// 服务器域名 / IP
    /// </summary>
    public string ServerDomain { get; set; } = "localhost";

    /// <summary>
    /// 图片保存路径
    /// </summary>
    public string ImagePath { get; set; } = @"D:\MeterImages";

    /// <summary>
    /// 数据库服务器
    /// </summary>
    public string DbServer { get; set; } = "192.168.1.237";

    /// <summary>
    /// 数据库端口
    /// </summary>
    public int DbPort { get; set; } = 1433;

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string DbName { get; set; } = "WaterMeterDB";

    /// <summary>
    /// 数据库账号
    /// </summary>
    public string DbUser { get; set; } = "__DB_USER__";

    /// <summary>
    /// 数据库密码
    /// </summary>
    public string DbPassword { get; set; } = "__DB_PASSWORD__";

    /// <summary>
    /// PDA App 版本列表
    /// </summary>
    public List<AppVersionDto> AppVersions { get; set; } = new()
    {
        new() { Version = "1.0.3", Size = "12.5 MB", ReleaseDate = "2026-07-10", ReleaseNotes = "修复抄表上传偶发超时", IsLatest = true, IsEnabled = true },
        new() { Version = "1.0.2", Size = "12.3 MB", ReleaseDate = "2026-06-15", ReleaseNotes = "优化拍照水印功能", IsLatest = false, IsEnabled = false },
        new() { Version = "1.0.1", Size = "11.8 MB", ReleaseDate = "2026-05-20", ReleaseNotes = "初始版本", IsLatest = false, IsEnabled = false }
    };

    /// <summary>
    /// 用户列表
    /// </summary>
    public List<UserDto> Users { get; set; } = new()
    {
        new() { UserName = "admin", DisplayName = "系统管理员", Role = "管理员", LastLogin = "2026-07-13 09:00", IsEnabled = true, Remark = "" },
        new() { UserName = "finance01", DisplayName = "财务专员", Role = "财务", LastLogin = "2026-07-12 14:30", IsEnabled = true, Remark = "" },
        new() { UserName = "pda01", DisplayName = "抄表员A", Role = "PDA 操作员", LastLogin = "2026-07-13 08:00", IsEnabled = true, Remark = "" },
        new() { UserName = "pda02", DisplayName = "抄表员B", Role = "PDA 操作员", LastLogin = "2026-06-01 10:00", IsEnabled = false, Remark = "已离职" }
    };

    /// <summary>
    /// 角色权限矩阵
    /// </summary>
    public List<PermissionDto> PermissionMatrix { get; set; } = new()
    {
        new() { Module = "首页数据看板", AdminAccess = true, FinanceAccess = true, PdaAccess = true },
        new() { Module = "办理登记", AdminAccess = true, FinanceAccess = false, PdaAccess = false },
        new() { Module = "宿舍管理", AdminAccess = true, FinanceAccess = false, PdaAccess = false },
        new() { Module = "人员清单", AdminAccess = true, FinanceAccess = false, PdaAccess = false },
        new() { Module = "费用标准", AdminAccess = true, FinanceAccess = true, PdaAccess = false },
        new() { Module = "宿舍账单", AdminAccess = true, FinanceAccess = true, PdaAccess = false },
        new() { Module = "员工账单", AdminAccess = true, FinanceAccess = true, PdaAccess = false },
        new() { Module = "抄表记录", AdminAccess = true, FinanceAccess = false, PdaAccess = true },
        new() { Module = "基础资料", AdminAccess = true, FinanceAccess = false, PdaAccess = false },
        new() { Module = "系统设置", AdminAccess = true, FinanceAccess = false, PdaAccess = false }
    };

    /// <summary>
    /// 备份列表
    /// </summary>
    public List<BackupDto> Backups { get; set; } = new()
    {
        new() { FileName = "dorm_backup_20260713_020000.bak", FileSize = "2.3 MB", CreatedAt = "2026-07-13 02:00", IsAuto = true },
        new() { FileName = "dorm_backup_20260712_020000.bak", FileSize = "2.2 MB", CreatedAt = "2026-07-12 02:00", IsAuto = true },
        new() { FileName = "dorm_manual_20260710_153000.bak", FileSize = "2.1 MB", CreatedAt = "2026-07-10 15:30", IsAuto = false }
    };

    /// <summary>
    /// 系统集成列表
    /// </summary>
    public List<IntegrationDto> Integrations { get; set; } = new()
    {
        new() { Id = 1, SystemCode = "HR", SystemName = "HR 系统", ServerAddress = "http://hr.company.com/api", Account = "sync_user", Password = "******", IsEnabled = true, LastTestResult = true, LastTestTime = DateTime.Parse("2026-07-13 08:00") },
        new() { Id = 2, SystemCode = "K3ERP", SystemName = "K3 ERP", ServerAddress = "http://erp.company.com/service", Account = "erp_sync", Password = "******", IsEnabled = false, LastTestResult = false, LastTestTime = null }
    };

    /// <summary>
    /// 保存服务与端口配置（v2.13.29：通过托盘 IPC 同步真实保存）
    /// </summary>
    public async Task<IActionResult> OnPostSaveServiceConfig(int PdaPort, int WebPort, string ServerDomain, string ImagePath)
    {
        // v2.13.29: 服务与端口配置由托盘程序统一管理（防多端冲突）
        // Web 端仅展示，修改须通过托盘系统设置
        TempData["InfoMessage"] = "服务与端口配置请通过托盘程序的系统设置进行修改（保证唯一真源）";
        return RedirectToPage(new { tab = "service" });
    }

    /// <summary>
    /// 通过托盘 IPC 控制服务启停（v2.13.3）
    /// </summary>
    public async Task<IActionResult> OnPostControlServiceAsync(string action, string service)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(service))
        {
            TempData["ErrorMessage"] = "缺少 action 或 service 参数";
            return RedirectToPage(new { tab = "service" });
        }

        try
        {
            var ipc = new DormManage.Shared.Services.IpcClient();
            var resp = await ipc.SendAsync(new DormManage.Shared.Services.ServiceIpc.IpcCommand
            {
                Command = action,
                Service = service
            }, 30000);

            if (resp.Success)
                TempData["SuccessMessage"] = $"操作成功：{resp.Message}";
            else
                TempData["ErrorMessage"] = $"操作失败：{resp.Message}";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"托盘不可达：{ex.Message}";
        }

        return RedirectToPage(new { tab = "service" });
    }

    /// <summary>
    /// 通过托盘 IPC Ping 测试（v2.13.3）
    /// </summary>
    public async Task<IActionResult> OnPostPingTrayAsync()
    {
        try
        {
            var ipc = new DormManage.Shared.Services.IpcClient();
            var resp = await ipc.SendAsync(new DormManage.Shared.Services.ServiceIpc.IpcCommand { Command = "ping" }, 2000);
            TempData["SuccessMessage"] = resp.Success ? $"托盘可达：{resp.Message}" : "托盘返回失败";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"托盘不可达：{ex.Message}";
        }
        return RedirectToPage(new { tab = "service" });
    }

    /// <summary>
    /// 加载页面时探测托盘状态 + 用户/角色子 Tab 数据（v2.13.67 嵌入子 Tab 后需在 OnGet 加载）
    /// </summary>
    public async Task OnGetAsync()
    {
        try
        {
            var ipc = new DormManage.Shared.Services.IpcClient();
            var resp = await ipc.SendAsync(new DormManage.Shared.Services.ServiceIpc.IpcCommand { Command = "status" }, 2000);
            TrayReachable = resp.Success;
            if (resp.Data is System.Text.Json.JsonElement elem)
            {
                if (elem.TryGetProperty("api", out var api) && api.TryGetProperty("state", out var s1))
                    ApiState = s1.GetString() ?? "Unknown";
                if (elem.TryGetProperty("admin", out var admin) && admin.TryGetProperty("state", out var s2))
                    AdminState = s2.GetString() ?? "Unknown";
            }
        }
        catch
        {
            TrayReachable = false;
        }

        // v2.13.67：嵌入子 Tab 后，OnGet 需要同时加载 User + Role 面板数据
        await LoadUserPanelAsync();
        await LoadRolePanelAsync();
        await LoadFieldPermissionPanelAsync();  // v2.13.92 字段权限加载
    }

    // 注意：数据库连接配置保存已迁移到 /api/v1/system/dbconfig/save (v2.13.19)

    /// <summary>
    /// v2.13.46 P0-5 修复：系统集成配置保存（接收 Razor 数组 Integration[id] 语法）
    /// </summary>
    public async Task<IActionResult> OnPostSaveIntegrationAsync([FromForm] List<IntegrationFormItem> Integration)
    {
        if (Integration == null || Integration.Count == 0)
        {
            TempData["ErrorMessage"] = "未接收到集成配置数据";
            return RedirectToPage(new { tab = "integration" });
        }

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}") };
            int updated = 0;
            foreach (var item in Integration)
            {
                var resp = await http.PutAsJsonAsync($"/api/v1/system/integration/{item.Id}", item);
                if (resp.IsSuccessStatusCode) updated++;
            }
            TempData["SuccessMessage"] = $"已更新 {updated} 条系统集成配置";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"保存失败：{ex.Message}";
        }

        return RedirectToPage(new { tab = "integration" });
    }
}

/// <summary>
/// v2.13.46 P0-5：系统集成表单接收 DTO（对应 Razor Integration[id] 数组语法）
/// </summary>
public class IntegrationFormItem
{
    public int Id { get; set; }
    public string? ServerAddress { get; set; }
    public string? Account { get; set; }
    public string? Password { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// App 版本数据传输对象
/// </summary>
public class AppVersionDto
{
    public string Version { get; set; } = "";
    public string Size { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool IsLatest { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 用户数据传输对象
/// </summary>
public class UserDto
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string LastLogin { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string Remark { get; set; } = "";
}

/// <summary>
/// 权限数据传输对象
/// </summary>
public class PermissionDto
{
    public string Module { get; set; } = "";
    public bool AdminAccess { get; set; }
    public bool FinanceAccess { get; set; }
    public bool PdaAccess { get; set; }
}

/// <summary>
/// 备份数据传输对象
/// </summary>
public class BackupDto
{
    public string FileName { get; set; } = "";
    public string FileSize { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool IsAuto { get; set; }
}

/// <summary>
/// 系统集成数据传输对象
/// </summary>
public class IntegrationDto
{
    public int Id { get; set; }
    public string SystemCode { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string ServerAddress { get; set; } = "";
    public string Account { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsEnabled { get; set; }
    public bool? LastTestResult { get; set; }
    public DateTime? LastTestTime { get; set; }
}
