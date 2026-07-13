using Microsoft.EntityFrameworkCore;
using DormManage.Api.HostedServices;
using DormManage.Shared.Data;
using DormManage.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// 添加服务
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 配置数据库
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "dorm.db");
builder.Services.AddDbContext<DormDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// 注册服务
builder.Services.AddScoped<IBasicsService, BasicsService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// 注册 v2.11.24 数据清洗后台服务（启动时一次性 FK 归一）
// 规范文档：00-方案文档/43-无效FK归一通用规范-v2.11.24.md
builder.Services.AddHostedService<DataCleanupHostedService>();

var app = builder.Build();

// 自动迁移数据库
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();
    db.Database.EnsureCreated();
}

// 配置 HTTP 管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
