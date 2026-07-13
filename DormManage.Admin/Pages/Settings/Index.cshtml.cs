using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// 系统设置页面模型
/// </summary>
public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    /// <summary>
    /// 当前激活的 Tab
    /// </summary>
    public string ActiveTab => Tab ?? "service";

    /// <summary>
    /// PDA 服务端口
    /// </summary>
    public int PdaPort { get; set; } = 5000;

    /// <summary>
    /// Web 服务端口
    /// </summary>
    public int WebPort { get; set; } = 5001;

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
    public string DbServer { get; set; } = "localhost";

    /// <summary>
    /// 数据库端口
    /// </summary>
    public int DbPort { get; set; } = 1433;

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string DbName { get; set; } = "DormManage";

    /// <summary>
    /// 数据库账号
    /// </summary>
    public string DbUser { get; set; } = "sa";

    /// <summary>
    /// 数据库密码
    /// </summary>
    public string DbPassword { get; set; } = "";

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
    /// 保存服务与端口配置
    /// </summary>
    public IActionResult OnPostSaveServiceConfig(int PdaPort, int WebPort, string ServerDomain, string ImagePath)
    {
        // TODO: 实际项目中应写入配置文件或数据库
        TempData["SuccessMessage"] = "服务与端口配置保存成功，部分改动需重启服务后生效";
        return RedirectToPage("/Settings/Index");
    }

    /// <summary>
    /// 保存数据库连接配置
    /// </summary>
    public IActionResult OnPostSaveDatabaseConfig(string DbServer, int DbPort, string DbName, string DbUser, string DbPassword)
    {
        // TODO: 实际项目中应写入配置文件或数据库
        TempData["SuccessMessage"] = "数据库连接配置保存成功";
        return RedirectToPage("/Settings/Index");
    }

    /// <summary>
    /// 保存系统集成配置
    /// </summary>
    public IActionResult OnPostSaveIntegrationConfig()
    {
        // TODO: 实际项目中应保存各系统的配置
        TempData["SuccessMessage"] = "系统集成配置保存成功";
        return RedirectToPage("/Settings/Index");
    }
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
