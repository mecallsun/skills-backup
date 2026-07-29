using System.Reflection;
using DormManage.Shared.Models;

using DormManage.Shared.Services;
using DormManage.TrayApp.Services;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘守护程序入口
///
/// 流程：
/// 1. 全局单实例锁（Mutex）— 防止重复启动
/// 2. 加载配置 → 初始化日志 → 创建 TrayAppContext
/// 3. Application.Run(context) 进入消息循环
/// 4. 退出时释放所有资源（子进程、托盘图标、Mutex）
/// </summary>
internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        // 1) 单实例锁
        const string mutexName = @"Global\DormManage.TrayApp.SingleInstance.v2";
        _singleInstance = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "金戈宿舍管理系统托盘守护程序已在运行中。\n\n请检查任务栏托盘区（系统托盘图标）。",
                "已在运行",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // 2) WinForms 基础设置
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, $"tray-uncaught-{DateTime.Now:yyyyMMdd}.log"),
                    $"[{DateTime.Now}] UI异常：{e.Exception}\n");
            }
            catch { /* 兜底 */ }
            MessageBox.Show($"UI 异常：{e.Exception.Message}", "托盘异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, $"tray-uncaught-{DateTime.Now:yyyyMMdd}.log"),
                    $"[{DateTime.Now}] 非UI异常：{e.ExceptionObject}\n");
            }
            catch { /* 兜底 */ }
        };

        // 3) 加载配置 + 初始化日志
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var initialLog = new LogService(Path.Combine(AppContext.BaseDirectory, "logs"));
        var config = new ConfigService(configPath, initialLog);
        config.Load();

        // v2.13.19：启动时将 db_setting.json 同步到 appsettings.json，
        // 保证 Web 端保存的配置在 Tray 重启后生效
        await SyncDbSettingToAppsettingsAsync(config);

        var logDir = string.IsNullOrWhiteSpace(config.Current.Storage.LogRoot)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : (Path.IsPathRooted(config.Current.Storage.LogRoot)
                ? config.Current.Storage.LogRoot
                : Path.Combine(AppContext.BaseDirectory, config.Current.Storage.LogRoot));
        var log = new LogService(logDir);
        log.Info($"=== 托盘启动 v{Assembly.GetExecutingAssembly().GetName().Version} ===");
        log.Info($"BaseDirectory: {AppContext.BaseDirectory}");

        // 3.5) v2.13.94 软件注册授权：初始化机器码（TrayApp 端 WMI 取真实硬件特征）
        try
        {
            var sn = MachineCodeProvider.Initialize();
            log.Info($"[LICENSE] 机器码已初始化：{sn}");
        }
        catch (Exception ex)
        {
            log.Warn($"[LICENSE] 机器码初始化失败，将使用 fallback：{ex.Message}");
        }

        // 4) 创建并运行 ApplicationContext
        try
        {
            using var context = new TrayAppContext(config, log);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            log.Error("托盘运行异常", ex);
            MessageBox.Show($"托盘运行异常：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
            _singleInstance = null;
            log.Info("=== 托盘退出 ===");
        }
    }

    /// <summary>
    /// v2.13.19：若 db_setting.json 存在，将字段式数据库配置同步到 appsettings.json，
    /// 保证 Web 端保存的配置在 Tray 重启后生效。
    /// </summary>
    private static async Task SyncDbSettingToAppsettingsAsync(ConfigService config)
    {
        try
        {
            var dto = await AppConfigManager.Instance.LoadAsync();
            if (dto is null) return;

            config.UpdateDatabaseSection(dto);
        }
        catch (Exception ex)
        {
            // 启动同步失败不应阻塞托盘启动，记录到默认日志目录
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, $"tray-{DateTime.Now:yyyyMMdd}.log"),
                    $"[{DateTime.Now}] 启动同步 db_setting.json 失败：{ex}\n");
            }
            catch { /* 兜底 */ }
        }
    }
}