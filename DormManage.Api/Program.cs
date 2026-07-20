using Microsoft.EntityFrameworkCore;
using DormManage.Api.HostedServices;
using DormManage.Api.Middleware;
using DormManage.Shared.Data;
using DormManage.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// 添加服务
// R4 (v2.12.43): 统一 camelCase JSON 序列化（与前端 JS 字段命名对齐）
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// v2.13.32 数据源热加载架构：
// AddDbContextFactory 替代 AddDbContext，每次 CreateDbContext() 都从 AppConfigRuntime 读取最新配置
// 保留 AddScoped<DormDbContext> 让现有 Service / Page / Controller 构造函数签名零改动
// 切换连接 → AppConfigRuntime.ApplyExternalConfiguration → 下次 HTTP 请求自动用新连接
//
// 配置回退优先级（AppConfigRuntime 内部 4 级回退）：
//   1. SysParameter 表（运行时真源 - 后续版本支持）
//   2. db_setting.json（AES-256 字段式）
//   3. appsettings.json ConnectionStrings.Default
//   4. 硬编码默认 192.168.1.237/WaterMeterDB/__DB_USER__/__DB_PASSWORD__
//
// 环境变量优先级仅作为冷启动兜底（首次启动 db_setting.json 不存在时）

var contentRootPath = builder.Environment.ContentRootPath;
builder.Services.AddDbContextFactory<DormDbContext>((sp, options) =>
{
    // 关键：每次 CreateDbContext 都从 Runtime 读取最新配置
    var cfg = AppConfigRuntime.Instance.GetCurrent();

    if (string.Equals(cfg.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var sqlitePath = !string.IsNullOrWhiteSpace(cfg.SqlitePath)
            ? cfg.SqlitePath
            : Path.Combine(contentRootPath, "dorm.db");
        options.UseSqlite($"Data Source={sqlitePath}");
    }
    else
    {
        // 默认 SQL Server（生产）
        // v2.12.42 BUGFIX: 兼容 SQL Server 2014，使用低版本兼容级别（避免 OPENJSON 等 2016+ 特性）
        // v2.13.32: cfg.BuildConnectionString() 始终返回最新保存的连接串
        options.UseSqlServer(cfg.BuildConnectionString(), sqlOptions =>
        {
            sqlOptions.UseCompatibilityLevel(120);  // SQL Server 2014 兼容级别
            // 缩短重试参数，避免启动期长时间阻塞
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 2,
                maxRetryDelay: TimeSpan.FromSeconds(3),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(10);  // 单条命令 10s 超时
        });
    }

    // v2.13.32: 注入 EF Interceptor（连接/命令日志）
    var interceptor = sp.GetService<DormManage.Shared.Data.Interceptors.DatabaseOperationInterceptor>();
    if (interceptor is not null)
        options.AddInterceptors(interceptor);
});

// 保留 Scoped DbContext 注入（调用方零改动；容器负责 Dispose factory.CreateDbContext() 返回的实例）
builder.Services.AddScoped<DormDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<DormDbContext>>().CreateDbContext());

// v2.13.32: 注册 EF Interceptor 与运行时配置中心
builder.Services.AddSingleton<DormManage.Shared.Data.Interceptors.DatabaseOperationInterceptor>();
builder.Services.AddSingleton<IAppConfigRuntime>(sp => AppConfigRuntime.Instance);

// v2.13.32: 注册 FileSystemWatcher 跨进程同步（监听 db_setting.json 变更）
builder.Services.AddHostedService<DatabaseConfigFileWatcher>();

// 注册服务
builder.Services.AddScoped<IBasicsService, BasicsService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPersonnelService, PersonnelService>();  // v2.12.42 BUGFIX: 缺失注册
builder.Services.AddScoped<IDormService, DormService>();          // v2.12.42 BUGFIX: 缺失注册
builder.Services.AddScoped<IDashboardService, DashboardService>();  // v2.13.3: 首页数据看板聚合服务
builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();  // v2.13.3: 数据库深度验证
builder.Services.AddScoped<IBillingService, BillingService>();              // v2.13.9: 费用管理服务
builder.Services.AddScoped<ISysUserFilterCacheService, SysUserFilterCacheService>();  // v2.13.12: 用户筛选条件云端缓存
builder.Services.AddScoped<ISysUserSelfService, SysUserSelfService>();  // v2.13.26: 个人中心与账号安全服务
builder.Services.AddScoped<IOperationLogService, OperationLogService>();  // v2.13.29: 统一操作日志
builder.Services.AddHttpClient();  // v2.13.3: 系统集成测试连接

// 注册 v2.11.24 数据清洗后台服务（启动时一次性 FK 归一）
// 规范文档：00-方案文档/43-无效FK归一通用规范-v2.11.24.md
// v2.12.44: 改为后台服务（StartAsync 立即返回，真正工作在 ExecuteAsync 中执行）
// v2.13.25: 延迟从 30s 调整为 5s（启动机制保证表已就绪）
builder.Services.AddHostedService<DataCleanupHostedService>();

var app = builder.Build();

// v2.13.32 热加载：启动时同步预热 AppConfigRuntime（避免首个请求触发 lazy load 阻塞）
AppConfigRuntime.Instance.GetCurrent();

// v2.13.25：启动同步校验 + 数据库初始化（Kestrel 绑定前）
// 1) 数据库连通性、关键表检测、字典种子、管理员种子、AppVersion 登记
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");
var startupReport = await DatabaseInitializer.InitializeAsync(
    app.Services, startupLogger, CancellationToken.None);
app.Logger.LogInformation(startupReport.ToBanner());

// 确保数据库创建（仅 SQLite 需要；SQL Server 假定数据库已存在）
// v2.13.32: 改为运行时真源 AppConfigRuntime
var runtimeProvider = AppConfigRuntime.Instance.GetCurrent().Provider;
if (string.Equals(runtimeProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();
        db.Database.EnsureCreated();
    }
}

// v2.12.44: 显式绑定 Kestrel 到托盘注入的端口（避免默认 5000 冲突）
var kestrelPort = Environment.GetEnvironmentVariable("DormManage_KESTREL_PORT") ?? "5100";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{kestrelPort}");

// 配置 HTTP 管道
// v2.13.29: 性能监控中间件（最外层，记录所有 API 请求耗时）
app.UseMiddleware<PerformanceMonitoringMiddleware>();

// v2.13.29: 全局异常处理中间件（捕获所有未处理异常）
app.UseMiddleware<GlobalExceptionMiddleware>();

// v2.12.42 BUGFIX: Swagger 在所有环境都启用（V1.0 测试需要）
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Logger.LogInformation("DormManage.Api 启动成功 - DB Provider: {Provider}, Port: {Port}", runtimeProvider, kestrelPort);

app.Run();
