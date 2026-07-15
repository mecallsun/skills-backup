using System.Reflection;
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
        var logDir = string.IsNullOrWhiteSpace(config.Current.Storage.LogRoot)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : (Path.IsPathRooted(config.Current.Storage.LogRoot)
                ? config.Current.Storage.LogRoot
                : Path.Combine(AppContext.BaseDirectory, config.Current.Storage.LogRoot));
        var log = new LogService(logDir);
        log.Info($"=== 托盘启动 v{Assembly.GetExecutingAssembly().GetName().Version} ===");
        log.Info($"BaseDirectory: {AppContext.BaseDirectory}");

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
}