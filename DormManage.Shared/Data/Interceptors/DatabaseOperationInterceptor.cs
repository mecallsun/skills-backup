using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DormManage.Shared.Data.Interceptors;

/// <summary>
/// 统一 EF Interceptor（v2.13.32 新增）：
/// - 监听连接 / 命令生命周期，输出 [DB-CONN] / [DB-EXEC] 日志
/// - 采样：默认每 100 条记录 1 条；慢查询（>1000ms）+ 错误全量记录
/// - 所有页面使用统一的连接日志便于追踪数据源切换
///
/// 输出格式：
///   [DB-CONN]  OPENED Server=172.16.0.100 Database=WaterMeterDB Duration=12ms
///   [DB-EXEC]  SELECT * FROM [DormBooking]... (35ms)
///   [DB-EXEC-SLOW] SELECT * FROM... (1523ms) Sql=...
///   [DB-CMD-FAIL] Server=... Database=... Duration=15ms Exception=...
///
/// 实现策略（EF Core 8 接口/类拆分）：
///   - 继承 DbCommandInterceptor（提供 override 点：CommandFailed / ReaderExecuted / ScalarExecuted / NonQueryExecuted）
///   - 同时实现 IDbConnectionInterceptor（marker 接口；显式实现 4 个 Connection 方法）
///   - EF Core DI 自动注册两个接口标记
/// </summary>
public sealed class DatabaseOperationInterceptor : DbCommandInterceptor, IDbConnectionInterceptor
{
    private readonly ILogger<DatabaseOperationInterceptor> _logger;
    private long _commandCounter;

    public DatabaseOperationInterceptor(ILogger<DatabaseOperationInterceptor> logger)
    {
        _logger = logger;
    }

    // ===== DbCommandInterceptor override =====

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        _logger.LogError(eventData.Exception,
            "[DB-CMD-FAIL] Server={Server} Database={Database} Duration={Duration}ms Sql={Sql}",
            ExtractServer(command.Connection?.ConnectionString),
            command.Connection?.Database,
            eventData.Duration.TotalMilliseconds.ToString("F0"),
            TruncateSql(command.CommandText));
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogExecutedCommand(command, eventData);
        return result;
    }

    public override object? ScalarExecuted(
        DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogExecutedCommand(command, eventData);
        return result;
    }

    public override int NonQueryExecuted(
        DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogExecutedCommand(command, eventData);
        return result;
    }

    // ===== IDbConnectionInterceptor 显式实现（EF Core 8 接口有 4 个 connection 方法）=====

    void IDbConnectionInterceptor.ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        _logger.LogInformation(
            "[DB-CONN] OPENED Server={Server} Database={Database} Duration={Duration}ms",
            ExtractServer(connection.ConnectionString),
            connection.Database,
            eventData.Duration.TotalMilliseconds.ToString("F0"));
    }

    async Task IDbConnectionInterceptor.ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DB-CONN] OPENED Server={Server} Database={Database} Duration={Duration}ms",
            ExtractServer(connection.ConnectionString),
            connection.Database,
            eventData.Duration.TotalMilliseconds.ToString("F0"));
        await Task.CompletedTask;
    }

    void IDbConnectionInterceptor.ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        _logger.LogDebug(
            "[DB-CONN] CLOSED Database={Database} Duration={Duration}ms",
            connection.Database,
            eventData.Duration.TotalMilliseconds.ToString("F0"));
    }

    void IDbConnectionInterceptor.ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        _logger.LogError(eventData.Exception,
            "[DB-CONN-FAIL] Server={Server} Database={Database} Duration={Duration}ms",
            ExtractServer(connection.ConnectionString),
            connection.Database,
            eventData.Duration.TotalMilliseconds.ToString("F0"));
    }

    // ===== 共享日志逻辑 =====

    private void LogExecutedCommand(DbCommand command, CommandExecutedEventData eventData)
    {
        var durationMs = eventData.Duration.TotalMilliseconds;
        var counter = Interlocked.Increment(ref _commandCounter);
        var isSampled = counter % 100 == 0;
        var isSlow = durationMs > 1000;

        if (!isSlow && !isSampled) return;  // 普通查询采样跳过

        var tag = isSlow ? "[DB-EXEC-SLOW]" : "[DB-EXEC]";
        var server = ExtractServer(command.Connection?.ConnectionString);
        var db = command.Connection?.Database ?? "?";
        var sql = TruncateSql(command.CommandText);

        if (isSlow)
        {
            _logger.LogWarning(
                "{Tag} Server={Server} Database={Database} Duration={Duration}ms Sql={Sql}",
                tag, server, db, durationMs.ToString("F0"), sql);
        }
        else
        {
            _logger.LogInformation(
                "{Tag} Server={Server} Database={Database} Duration={Duration}ms Sql={Sql}",
                tag, server, db, durationMs.ToString("F0"), sql);
        }
    }

    // ===== 辅助 =====

    private static string ExtractServer(string? connStr)
    {
        if (string.IsNullOrEmpty(connStr)) return "?";
        try
        {
            var parts = connStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 &&
                    (kv[0].Trim().Equals("Server", StringComparison.OrdinalIgnoreCase) ||
                     kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase)))
                {
                    return kv[1].Trim();
                }
            }
        }
        catch { }
        return "?";
    }

    private static string TruncateSql(string sql, int max = 500)
    {
        if (string.IsNullOrEmpty(sql)) return string.Empty;
        return sql.Length <= max ? sql : sql.Substring(0, max) + "...";
    }
}