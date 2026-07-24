using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DormManage.Api.Middleware;
using DormManage.Admin.Filters;
using DormManage.Admin.Middleware;
using DormManage.Shared.Data;
using DormManage.Shared.Security;
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

// v2.13.135 暗桩校验：运行时限 + 过期返回 403 + 客户端 5-2-0 解锁（_Layout.cshtml 监听）
// 设计来源：仓库物料汇总 FR-07；时间窗口与 v2.13.94 RegisterSdk 取较早
var _adminExpiryDays = RuntimeWindowGuard.CheckExpiry();
if (_adminExpiryDays.HasValue)
{
    if (_adminExpiryDays < 0)
    {
        // 早于起始日期：静默退出
        Console.Error.WriteLine("[TAMPER-GUARD] 系统日期早于起始日期，进程退出。");
        Thread.Sleep(2000);
        return;
    }
    // 晚于截止日期：保留启动但返回 403；客户端 JS 弹伪装错误框 + 5-2-0 解锁后可访问
    Console.Error.WriteLine($"[TAMPER-GUARD] 已过期 {_adminExpiryDays} 天，所有页面将返回 403 直至 5-2-0 解锁。");
}

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
    // v2.13.76 RBAC：Razor Pages 路由级权限守卫（IAsyncPageFilter，DI 解析）
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(PagePermissionFilter)));
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
        // v2.13.93 新增：每次请求校验账号有效性（停用/锁定/过期），自动踢出已失效会话
        // 无 OnValidatePrincipal 时，Cookie 一旦签发直到自然过期都不会被踢出 → 已过期账号仍能继续操作
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return;
            var db = context.HttpContext.RequestServices.GetRequiredService<DormDbContext>();
            var user = await db.SysUsers.FindAsync(userId);
            if (user == null || !user.IsActive || user.IsLocked
                || (user.ExpiresAt.HasValue && DateTime.Today > user.ExpiresAt.Value.Date))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
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

    // v2.13.109 起 SQLite Provider 已彻底移除；硬拒绝非 SqlServer，避免历史 Provider=Sqlite 静默失败
    if (!string.Equals(cfg.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Database provider must be SqlServer (got: '{cfg.Provider}'). " +
            "Current version no longer supports SQLite. " +
            "Please update db_setting.json to set provider=SqlServer.");
    }

    // SQL Server（生产 + 开发统一）
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
builder.Services.AddScoped<IPermissionService, PermissionService>();  // v2.13.76 RBAC 三级权限控制
builder.Services.AddScoped<PagePermissionFilter>();                    // v2.13.76 Razor Pages 路由级权限守卫
builder.Services.AddScoped<ISysFieldPermissionService, SysFieldPermissionService>();  // v2.13.92 字段权限服务

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

// v2.13.109 起 SQLite 已移除；不再调用 EnsureCreated()。
// SQL Server Schema 由 init_schema.sql 运维脚本管理；行政宿舍 Excel 导入仅在 SQLite 开发环境使用，删除。
var runtimeProvider = AppConfigRuntime.Instance.GetCurrent().Provider;
if (!string.Equals(runtimeProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogError("Database provider must be SqlServer (got: {Provider}). Service startup aborted.", runtimeProvider);
    throw new InvalidOperationException($"Database provider must be SqlServer (got: '{runtimeProvider}').");
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

// v2.13.135 暗桩中间件：过期时阻断非静态资源请求（让 _Layout.cshtml 5-2-0 解锁 JS 可加载）
if (_adminExpiryDays.HasValue && _adminExpiryDays >= 0)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";
        // 允许：静态资源（CSS/JS/字体/图片）、解锁 API、Error 页
        if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib")
            || path.StartsWith("/images") || path.StartsWith("/favicon") || path.EndsWith(".css")
            || path.EndsWith(".js") || path.EndsWith(".png") || path.EndsWith(".jpg")
            || path.EndsWith(".svg") || path.EndsWith(".ico") || path.EndsWith(".woff")
            || path.EndsWith(".woff2") || path.EndsWith(".ttf") || path == "/TamperUnlock"
            || path == "/Error")
        {
            await next();
            return;
        }
        // 其他请求：返回 503 + 过期横幅 HTML（前端 JS 5-2-0 解锁后会 reload）
        context.Response.StatusCode = 503;
        await context.Response.WriteAsync($@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>服务受限</title>
<style>body{{font-family:'Microsoft YaHei UI',sans-serif;background:#1a1a1a;color:#eee;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0}}
.box{{background:#2a2a2a;padding:40px;border-radius:12px;text-align:center;max-width:500px;box-shadow:0 8px 24px rgba(0,0,0,.5)}}
h1{{color:#d13438;font-size:20px;margin:0 0 16px}}p{{color:#999;font-size:14px;line-height:1.8}}
</style></head>
<body><div class='box'>
<h1>⚠ 系统内存访问冲突</h1>
<p>服务暂时不可用。代码: <code>0xC0000005</code></p>
<p>如需技术支持，请联系信息科。</p>
</div>
<script>
(function(){{
  var buf=[];
  document.addEventListener('keydown',function(e){{
    if(e.key>='0'&&e.key<='9'){{buf.push(e.key.charCodeAt(0));if(buf.length>3)buf.shift();
    if(buf.length===3&&buf[0]===53&&buf[1]===50&&buf[2]===48){{buf.length=0;location.reload();}}}}}}
  }});
}})();
</script>
</body></html>");
    });
}

app.UseRouting();

// v2.13.0: 认证中间件（必须在 Routing 之后、MapRazorPages 之前）
app.UseAuthentication();
app.UseAuthorization();

// v2.13.136 全局只读中间件：注册失败/过期时所有 POST/PUT/DELETE → 403
// 必须在 Authorization 之后、Razor Pages 之前；白名单含 /Account/* 登录页
// 注意：DormManage.Api.Middleware 也有同名中间件（v2.13.136），此处必须用全限定名
app.UseMiddleware<DormManage.Admin.Middleware.LicenseReadOnlyMiddleware>();

app.MapRazorPages();
app.MapControllers();  // v2.12.43: 启用进程内 API 控制器路由

app.Logger.LogInformation("DormManage.Admin 启动成功 - DB Provider: {Provider}, Port: {Port}", runtimeProvider, kestrelPort);

app.Run();
