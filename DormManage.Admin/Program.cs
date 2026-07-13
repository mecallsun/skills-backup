using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

// 添加 Razor Pages 服务
builder.Services.AddRazorPages();

// 配置数据库
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "dorm.db");
builder.Services.AddDbContext<DormDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

// 自动迁移数据库
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();
    db.Database.EnsureCreated();
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
app.UseAuthorization();

app.MapRazorPages();

app.Run();
