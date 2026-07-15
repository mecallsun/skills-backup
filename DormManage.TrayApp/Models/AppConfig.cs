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

    /// <summary>Api 可执行文件相对路径（相对托盘 EXE 所在目录）</summary>
    public string ApiExecutable { get; set; } = "Api\\DormManage.Api.exe";

    /// <summary>Admin 可执行文件相对路径</summary>
    public string AdminExecutable { get; set; } = "Admin\\DormManage.Admin.exe";

    /// <summary>托盘启动后自动拉起 Api + Admin</summary>
    public bool AutoStartServices { get; set; } = true;

    /// <summary>子进程异常退出时自动重启</summary>
    public bool AutoRestartOnCrash { get; set; } = true;

    /// <summary>健康检查间隔（秒）</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;
}

/// <summary>
/// 数据库配置
/// </summary>
public class DatabaseSection
{
    /// <summary>数据库类型：SqlServer / Sqlite</summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>SQL Server 连接串（Provider=SqlServer 时使用）</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>SQLite 数据库文件绝对路径（Provider=Sqlite 时使用）</summary>
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