namespace DormManage.TrayApp.Models;

/// <summary>
/// 托盘配置根对象（与 appsettings.json 结构一一对应）
/// </summary>
public class AppConfig
{
    public TraySection Tray { get; set; } = new();
    public DatabaseSection Database { get; set; } = new();
    public StorageSection Storage { get; set; } = new();
}

/// <summary>
/// 托盘与服务端口配置
/// </summary>
public class TraySection
{
    /// <summary>Api（PDA 接口服务）监听端口</summary>
    public int ApiPort { get; set; } = 5100;

    /// <summary>Admin（Web 管理后台）监听端口</summary>
    public int AdminPort { get; set; } = 5001;

    /// <summary>
    /// Api 可执行文件路径（相对/绝对均可，ConfigService.Load() 会做多候选 fallback）
    /// v2.13.197：默认改为相对路径 `..\Api\` —— TrayApp 从 release/latest/TrayApp/ 运行时，
    /// `..\Api\` 直接指向 release/latest/Api/（主目录），消除 v2.13.193 之前的"兄弟目录"陷阱。
    /// 详见 `00-方案文档/199-启动路径解析修复-v2.13.156.md` + `scripts/sync_publish_to_trayapp.sh` 废弃说明。
    /// </summary>
    public string ApiExecutable { get; set; } = "..\\Api\\DormManage.Api.exe";

    /// <summary>
    /// Admin 可执行文件路径（同 ApiExecutable 解析逻辑）
    /// v2.13.197：默认 ..\Admin\ 同样指回主目录
    /// </summary>
    public string AdminExecutable { get; set; } = "..\\Admin\\DormManage.Admin.exe";

    /// <summary>托盘启动后自动拉起 Api + Admin</summary>
    public bool AutoStartServices { get; set; } = true;

    /// <summary>子进程异常退出时自动重启</summary>
    public bool AutoRestartOnCrash { get; set; } = true;

    /// <summary>健康检查间隔（秒）</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;
}

/// <summary>
/// 数据库配置（v2.13.109 起 SQLite Provider 已移除，仅保留 SQL Server）
/// </summary>
public class DatabaseSection
{
    /// <summary>数据库类型：固定为 SqlServer（v2.13.109 起移除 SQLite 双 provider）</summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>SQL Server 连接串</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 历史 SQLite 配置字段。SQLite Provider 已于 v2.13.109 移除，
    /// 仅用于兼容旧版 appsettings.json 反序列化。不再参与任何运行时逻辑。
    /// </summary>
    [Obsolete("SQLite provider has been removed; retained for legacy configuration compatibility.")]
    public string SqlitePath { get; set; } = string.Empty;
}

/// <summary>
/// 存储路径配置
/// </summary>
public class StorageSection
{
    /// <summary>图片存储根路径（相对托盘 EXE 目录）</summary>
    public string ImageRoot { get; set; } = "Storage\\images";

    /// <summary>日志根路径</summary>
    public string LogRoot { get; set; } = "logs";
}