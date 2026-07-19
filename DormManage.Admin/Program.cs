using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// 添加 Razor Pages 服务
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");                          // 所有页面默认需要认证
    options.Conventions.AllowAnonymousToPage("/Account/Login");         // 登录页匿名访问
    options.Conventions.AllowAnonymousToPage("/Error");                 // 错误页匿名访问
    options.Conventions.AllowAnonymousToPage("/Privacy");               // 隐私页匿名访问
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

// 配置数据库（v2.12.42 BUGFIX: 支持 SQLite/SQL Server 切换）
// v2.12.44: 优先使用托盘进程通过环境变量注入的明文连接串 DormManage_DB_CONN
var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var envConnStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN");
var configConnStr = builder.Configuration.GetConnectionString("Default");
var connectionString = !string.IsNullOrEmpty(envConnStr) ? envConnStr
    : (configConnStr ?? "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;");

builder.Services.AddDbContext<DormDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        // v2.12.44: 优先使用托盘注入的图片盘/数据盘绝对路径，避免相对路径找不到 dorm.db
        var envDbPath = Environment.GetEnvironmentVariable("DormManage_DB_PATH");
        var dbPath = !string.IsNullOrEmpty(envDbPath) ? envDbPath
            : Path.Combine(builder.Environment.ContentRootPath, "dorm.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
    else
    {
        // 默认 SQL Server（生产）
        // v2.12.42 BUGFIX: 兼容 SQL Server 2014，使用低版本兼容级别（避免 OPENJSON 等 2016+ 特性）
        options.UseSqlServer(connectionString, sqlOptions =>
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
});

// 注册应用服务（v2.12.43 BUGFIX: Admin 缺失服务注册，导致 /Booking、/Dorms 等页面 DI 解析失败 500）
builder.Services.AddScoped<IBasicsService, BasicsService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPersonnelService, PersonnelService>();
builder.Services.AddScoped<IDormService, DormService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();  // v2.13.3: 首页数据看板聚合服务
builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();  // v2.13.3: 数据库深度验证
builder.Services.AddScoped<IBillingService, BillingService>();              // v2.13.9: 费用管理服务
builder.Services.AddScoped<ISysUserFilterCacheService, SysUserFilterCacheService>();  // v2.13.12: 用户筛选条件云端缓存

// v2.13.0: 认证服务
builder.Services.AddScoped<DormManage.Admin.Services.IAuthService, DormManage.Admin.Services.AuthService>();
builder.Services.AddHttpContextAccessor();  // Cookie 认证需要 IHttpContextAccessor

var app = builder.Build();

// v2.12.44: 显式绑定 Kestrel 到托盘注入的端口（修复 5000 vs 5001 问题）
var kestrelPort = Environment.GetEnvironmentVariable("DormManage_KESTREL_PORT") ?? "5001";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{kestrelPort}");

// 确保数据库创建和种子数据（仅 SQLite 需要；SQL Server 假定数据库已存在）
if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();
        db.Database.EnsureCreated();
    }
}

// 配置 HTTP 管道
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

app.Logger.LogInformation("DormManage.Admin 启动成功 - DB Provider: {Provider}, Port: {Port}", dbProvider, kestrelPort);

app.Run();
