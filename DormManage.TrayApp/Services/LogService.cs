using System.Text;

namespace DormManage.TrayApp.Services;

/// <summary>
/// 文件日志服务
///
/// 输出位置：&lt;托盘 EXE 目录&gt;/logs/tray-YYYYMMDD.log
/// 格式：[2026-07-15 14:30:25.123] [INFO] message
///
/// 线程安全：使用 SemaphoreSlim 串行化写入。
/// </summary>
public class LogService
{
    private readonly string _logDir;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public LogService(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
    }

    private string CurrentFile => Path.Combine(_logDir, $"tray-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        // 异步写日志，避免阻塞调用线程
        _ = Task.Run(async () =>
        {
            await _writeLock.WaitAsync();
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                await File.AppendAllTextAsync(CurrentFile, line + Environment.NewLine, Encoding.UTF8);

                // 控制台同步输出（开发调试可见）
                Console.WriteLine(line);
            }
            catch
            {
                // 日志写失败时静默，避免递归
            }
            finally
            {
                _writeLock.Release();
            }
        });
    }
}