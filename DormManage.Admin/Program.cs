using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using DormManage.Api.Middleware;
using DormManage.Admin.Filters;
using DormManage.Shared.Data;
using DormManage.Shared.Services;

// v2.13.72 进程唯一性守卫（必须在 WebApplication.CreateBuilder 之前执行）
// 使用全局命名 Mutex 防止 DormManage.Admin 重复启动：
//   - 若已被占用 → 记录 WARN + 等待 2s 让用户看到控制台消息 + 自动终止（return 退出 Main）
//   - 若获取成功 → using 变量在整个进程生命周期持有，主机停止时 Main 返回时 Dispose 释放
using var _singleInstanceMutex = new System.Threading.Mutex(
    initiallyOwned: true,
    name: @"Global\DormManage.Admin.SingleInstance.v1",
    createdNew: out bool adminCreatedNew);
if (!adminCreatedNew)
{
    Console.Error.WriteLine("[SINGLE-INSTANCE] DormManage.Admin 已在运行中（Mutex: Global\\DormManage.Admin.SingleInstance.v1）。");
    Console.Error.WriteLine("[SINGLE-INSTANCE] 若确认无残留实例，请打开任务管理器结束 DormManage.Admin 进程后再启动。");
    Console.Error.WriteLine("[SINGLE-INSTANCE] 本次启动将在 2 秒后自动终止...");
    Thread.Sleep(2000);
    return;
}
Console.WriteLine("[SINGLE-INSTANCE] 进程唯一锁已获取: Global\\DormManage.Admin.SingleInstance.v1");

var builder = WebApplication.CreateBuilder(args);

// 添加 Razor Pages 服务
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");                          // 所有页面默认需要认证
    options.Conventions.AllowAnonymousToPage("/Account/Login");         // 登录页匿名访问
    options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword"); // v2.13.26 密码找回匿名访问
    options.Conventions.AllowAnonymousToPage("/Error");                 // 错误页匿名访问
    options.Conventions.AllowAnonymousToPage("/Privacy");               // 隐私页匿名访问
})
.AddMvcOptions(options =>
{
    // v2.13.29: Razor Pages 全局异常过滤器
    options.Filters.Add<GlobalExceptionFilter>();
});

// v2.13.0: Cookie 认证配置
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";          // 未登录重定向到登录页
        options.LogoutPath = "/Account/Logout";        // 登出路劲
        options.AccessDeniedPath = "/Account/Login";   // 无权限重定向
        options.Cookie.Name = "DormManage.Auth";       // Cookie 名称
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;                // 防止 XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;   // 开发环境允许 HTTP
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);  // 默认过期 8 小时
        options.SlidingExpiration = true;              // 滑动过期（活跃时续期）
    });

builder.Services.AddAuthorization();  // 启用授权

// v2.12.43: 进程内自托管 API 控制器（DormManage.Api 程序集），使前端相对 /api/v1 调用可用
// R4: 统一 camelCase JSON 序列化（与前端 JS 字段命名对齐）
builder.Services.AddControllers()
    .AddApplicationPart(typeof(DormManage.Api.Controllers.Booking.BookingController).Assembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// v2.13.32 数据源热加载架构（与 Api Program.cs 对齐）：
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
        // 优先使用 db_setting.json 中保存的 SqlitePath，其次 ContentRootPath/dorm.db
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
builder.Services.AddHostedService<DormManage.Api.HostedServices.DatabaseConfigFileWatcher>();

// 注册应用服务（v2.12.43 BUGFIX: Admin 缺失服务注册，导致 /Booking、/Dorms 等页面 DI 解析失败 500）
builder.Services.AddScoped<IBasicsService, BasicsService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPersonnelService, PersonnelService>();
builder.Services.AddScoped<IDormService, DormService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();  // v2.13.3: 首页数据看板聚合服务
builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();  // v2.13.3: 数据库深度验证
builder.Services.AddScoped<IBillingService, BillingService>();              // v2.13.9: 费用管理服务
builder.Services.AddScoped<ISysUserFilterCacheService, SysUserFilterCacheService>();  // v2.13.12: 用户筛选条件云端缓存
builder.Services.AddScoped<ISysUserSelfService, SysUserSelfService>();  // v2.13.26: 个人中心与账号安全
builder.Services.AddScoped<IOperationLogService, OperationLogService>();  // v2.13.29: 统一操作日志

// v2.13.0: 认证服务
builder.Services.AddScoped<DormManage.Admin.Services.IAuthService, DormManage.Admin.Services.AuthService>();
builder.Services.AddHttpContextAccessor();  // Cookie 认证需要 IHttpContextAccessor

var app = builder.Build();

// v2.13.32 热加载：启动时同步预热 AppConfigRuntime（避免首个请求触发 lazy load 阻塞）
AppConfigRuntime.Instance.GetCurrent();

// v2.13.25：启动同步校验 + 数据库初始化（Kestrel 绑定前）
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");
var startupReport = await DatabaseInitializer.InitializeAsync(
    app.Services, startupLogger, CancellationToken.None);
app.Logger.LogInformation(startupReport.ToBanner());

// v2.12.44: 显式绑定 Kestrel 到托盘注入的端口（修复 5000 vs 5001 问题）
var kestrelPort = Environment.GetEnvironmentVariable("DormManage_KESTREL_PORT") ?? "5001";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{kestrelPort}");

// 确保数据库创建和种子数据（仅 SQLite 需要；SQL Server 假定数据库已存在）
// v2.13.32: 改为运行时真源 AppConfigRuntime
var runtimeProvider = AppConfigRuntime.Instance.GetCurrent().Provider;
if (string.Equals(runtimeProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataImportService>>();
        db.Database.EnsureCreated();

        // v2.13.19: 从行政宿舍 Excel 导入正式主数据
        var excelPath = Path.Combine(builder.Environment.ContentRootPath, "..", "行政宿舍资料", "员工宿舍明细表.xlsx");
        if (File.Exists(excelPath))
        {
            var importer = new DataImportService(db, logger);
            var result = await importer.ImportAsync(excelPath);
            app.Logger.LogInformation("行政宿舍数据导入完成: {Result}", result);
        }
        else
        {
            app.Logger.LogWarning("未找到行政宿舍数据文件: {Path}", excelPath);
        }
    }
}

// 配置 HTTP 管道
// v2.13.29: 全局异常处理中间件（最外层）
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();

// v2.13.0: 认证中间件（必须在 Routing 之后、MapRazorPages 之前）
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();  // v2.12.43: 启用进程内 API 控制器路由

app.Logger.LogInformation("DormManage.Admin 启动成功 - DB Provider: {Provider}, Port: {Port}", runtimeProvider, kestrelPort);

app.Run();
