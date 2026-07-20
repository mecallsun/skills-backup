using Microsoft.EntityFrameworkCore;
using DormManage.Api.HostedServices;
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
        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "dorm.db");
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
builder.Services.AddHttpClient();  // v2.13.3: 系统集成测试连接

// 注册 v2.11.24 数据清洗后台服务（启动时一次性 FK 归一）
// 规范文档：00-方案文档/43-无效FK归一通用规范-v2.11.24.md
// v2.12.44: 改为后台服务（StartAsync 立即返回，真正工作在 ExecuteAsync 中执行）
// v2.13.25: 延迟从 30s 调整为 5s（启动机制保证表已就绪）
builder.Services.AddHostedService<DataCleanupHostedService>();

var app = builder.Build();

// v2.13.25：启动同步校验 + 数据库初始化（Kestrel 绑定前）
// 1) 数据库连通性、关键表检测、字典种子、管理员种子、AppVersion 登记
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");
var startupReport = await DatabaseInitializer.InitializeAsync(
    app.Services, startupLogger, CancellationToken.None);
app.Logger.LogInformation(startupReport.ToBanner());

// 确保数据库创建（仅 SQLite 需要；SQL Server 假定数据库已存在）
if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
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
// v2.12.42 BUGFIX: Swagger 在所有环境都启用（V1.0 测试需要）
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Logger.LogInformation("DormManage.Api 启动成功 - DB Provider: {Provider}, Port: {Port}", dbProvider, kestrelPort);

app.Run();
