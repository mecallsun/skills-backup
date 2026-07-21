using System.Globalization;
using System.Text;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Tools.GenderBackfill;

/// <summary>
/// v2.13.83 一次性性别批量回填工具
/// 读取 行政宿舍资料/员工宿舍明细表.xlsx + 姓名推断 → UPDATE SysEmployee.Gender
/// 优先级：admin xlsx > 姓名推断 > 默认（1=男）
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("========================================");
        Console.WriteLine("  v2.13.83 性别批量回填工具");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // 默认路径（可被命令行覆盖）
        var xlsxPath = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "行政宿舍资料", "员工宿舍明细表.xlsx");

        // 解析 xlsx 为绝对路径
        xlsxPath = Path.GetFullPath(xlsxPath);

        if (!File.Exists(xlsxPath))
        {
            Console.WriteLine($"❌ 找不到行政资料文件：{xlsxPath}");
            Console.WriteLine($"   用法：dotnet run -- <xlsx绝对路径>");
            return 1;
        }

        Console.WriteLine($"📄 行政资料文件：{xlsxPath}");
        Console.WriteLine();

        // 1. 读取行政资料 xlsx
        var adminData = AdminDataReader.Read(xlsxPath);
        Console.WriteLine($"📋 行政资料加载完成：{adminData.Count} 条（男={adminData.Count(kv => kv.Value == 1)}, 女={adminData.Count(kv => kv.Value == 2)}）");
        Console.WriteLine();

        // 2. 连接数据库
        // 默认连接串（与 appsettings 保持一致；可通过环境变量覆盖）
        var connectionString = Environment.GetEnvironmentVariable("DormManage_DB_CONN")
            ?? "Server=192.168.1.237;Database=WaterMeterDB;User Id=__DB_USER__;Password=__DB_PASSWORD__;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<DormDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var db = new DormDbContext(options);

        // 3. 查询所有员工
        var employees = await db.Employees.AsNoTracking().ToListAsync();
        Console.WriteLine($"👥 数据库员工总数：{employees.Count}");
        Console.WriteLine();

        // 4. 对每个员工回填性别
        int adminFilled = 0, inferredFilled = 0, defaultKept = 0, skippedNoChange = 0;
        var reports = new List<BackfillReport>();

        foreach (var emp in employees)
        {
            int originalGender = emp.Gender;
            string source;
            int newGender;

            // 优先级 1：行政资料 xlsx 显式标注
            if (adminData.TryGetValue(emp.EmployeeCode, out var adminGender) && adminGender > 0)
            {
                newGender = adminGender;
                source = "行政资料";
                adminFilled++;
            }
            // 优先级 2：姓名推断
            else if (GenderInferrer.TryInfer(emp.RealName, out var inferredGender))
            {
                newGender = inferredGender;
                source = "姓名推断";
                inferredFilled++;
            }
            // 优先级 3：保持默认（1=男）
            else
            {
                newGender = 1;
                source = "默认（推断失败）";
                defaultKept++;
            }

            bool changed = originalGender != newGender;
            if (changed) skippedNoChange = 0; // reset
            else skippedNoChange++;

            reports.Add(new BackfillReport
            {
                EmployeeCode = emp.EmployeeCode,
                RealName = emp.RealName,
                OriginalGender = originalGender,
                NewGender = newGender,
                Source = source,
                Changed = changed
            });

            if (changed)
            {
                emp.Gender = newGender;
                emp.UpdatedAt = DateTime.Now;
                db.Employees.Update(emp);  // 显式标记 Modified（虽然 AsNoTracking 但 Update 会重置为 Tracked）
            }
        }

        // 5. 保存到数据库
        try
        {
            await db.SaveChangesAsync();
            Console.WriteLine($"💾 数据库保存成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 数据库保存失败：{ex.Message}");
            Console.WriteLine($"   报告已保存，但 DB 未更新");
            ResultReporter.WriteCsv(GetReportPath(), reports);
            return 1;
        }

        Console.WriteLine();

        // 6. 输出报告
        var reportPath = GetReportPath();
        ResultReporter.WriteCsv(reportPath, reports);

        // 7. 控制台汇总
        Console.WriteLine("========================================");
        Console.WriteLine("  ✅ 性别回填完成");
        Console.WriteLine("========================================");
        Console.WriteLine($"  - 行政资料回填：{adminFilled} 条");
        Console.WriteLine($"  - 姓名推断：{inferredFilled} 条");
        Console.WriteLine($"  - 默认（推断失败）：{defaultKept} 条");
        Console.WriteLine($"  - 总计：{employees.Count} 条");
        Console.WriteLine($"  - 数据变更：{reports.Count(r => r.Changed)} 条");
        Console.WriteLine($"  - 报告文件：{reportPath}");
        Console.WriteLine();
        Console.WriteLine("  性别分布：");
        Console.WriteLine($"    男（1）：{reports.Count(r => r.NewGender == 1)} 条 ({reports.Count(r => r.NewGender == 1) * 100.0 / reports.Count:F1}%)");
        Console.WriteLine($"    女（2）：{reports.Count(r => r.NewGender == 2)} 条 ({reports.Count(r => r.NewGender == 2) * 100.0 / reports.Count:F1}%)");
        Console.WriteLine();

        return 0;
    }

    private static string GetReportPath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var reportDir = Path.GetFullPath(Path.Combine(dir, "行政宿舍资料"));
        Directory.CreateDirectory(reportDir);
        return Path.Combine(reportDir, $"性别回填结果_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }
}

/// <summary>
/// v2.13.83 行政资料 xlsx 读取器
/// 期望格式（员工宿舍明细表.xlsx）：
///   列 1 员工姓名（重复）
///   列 2 员工编号（如 JG002723）
///   列 3 员工姓名
///   列 4 部门
///   列 5 性别（'男' / '女' / None）
/// </summary>
public static class AdminDataReader
{
    public static Dictionary<string, int> Read(string xlsxPath)
    {
        var result = new Dictionary<string, int>();

        // ExcelDataReader 需要注册 CodePages 编码
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var stream = File.Open(xlsxPath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        bool headerSkipped = false;
        while (reader.Read())
        {
            if (!headerSkipped)
            {
                headerSkipped = true;
                continue;  // 跳过表头
            }

            try
            {
                // 列索引（0-based）：0=员工姓名(重复), 1=员工编号, 2=员工姓名, 3=部门, 4=性别
                var empCode = reader.GetValue(1)?.ToString()?.Trim();
                var genderStr = reader.GetValue(4)?.ToString()?.Trim();

                if (string.IsNullOrEmpty(empCode)) continue;

                int gender = 0;
                if (genderStr == "男") gender = 1;
                else if (genderStr == "女") gender = 2;

                if (gender > 0 && !result.ContainsKey(empCode))
                {
                    result[empCode] = gender;
                }
            }
            catch
            {
                // 跳过无法解析的行
            }
        }

        return result;
    }
}

/// <summary>
/// v2.13.83 姓名性别推断器
/// 算法：
///   1. 末位字匹配「女性常用字」→ 推断为女（2）
///   2. 末位字匹配「男性常用字」→ 推断为男（1）
///   3. 倒数第二字匹配「女性常用字」→ 推断为女
///   4. 倒数第二字匹配「男性常用字」→ 推断为男
///   5. 都不匹配 → 推断失败（返回 false）
/// </summary>
public static class GenderInferrer
{
    private static readonly HashSet<char> StrongFemaleChars = new()
    {
        '婷','娜','芳','丽','萍','玲','敏','霞','燕','梅','兰','雪','菊','莲',
        '慧','静','淑','娟','芬','秀','珍','珠','颖','欣','怡','洁','倩','娣',
        '凤','鸾','鸳','鸯','妹','姬','娘','媛','嫦','娥','婕','馨','蔓','菲',
        '萦','蕾','薇','妍','姣','瑶','璐','珂','琴','瑛','珏','瑜','琪','琳',
        '莹','璇','碧'
    };

    private static readonly HashSet<char> StrongMaleChars = new()
    {
        '强','伟','刚','勇','军','涛','明','辉','斌','鹏','飞','龙','虎','彪',
        '健','雄','达','进','平','康','宁','安','泰','盛','俊','杰','浩','宇',
        '轩','哲','凯','旭','阳','松','柏','楠','栋','梁','柱','国','民','家',
        '邦','志','毅','铮','铁','钢','磊','石','钧','锋','锐','利','剑','豪'
    };

    public static bool TryInfer(string? name, out int gender)
    {
        gender = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var trimmed = name.Trim();
        if (trimmed.Length == 0) return false;

        // 末位字优先
        var lastChar = trimmed[^1];
        if (StrongFemaleChars.Contains(lastChar)) { gender = 2; return true; }
        if (StrongMaleChars.Contains(lastChar)) { gender = 1; return true; }

        // 倒数第二字
        if (trimmed.Length >= 2)
        {
            var secondLast = trimmed[^2];
            if (StrongFemaleChars.Contains(secondLast)) { gender = 2; return true; }
            if (StrongMaleChars.Contains(secondLast)) { gender = 1; return true; }
        }

        return false;
    }
}

/// <summary>
/// v2.13.83 回填报告记录
/// </summary>
public class BackfillReport
{
    public string EmployeeCode { get; set; } = "";
    public string RealName { get; set; } = "";
    public int OriginalGender { get; set; }
    public int NewGender { get; set; }
    public string Source { get; set; } = "";
    public bool Changed { get; set; }
}

/// <summary>
/// v2.13.83 CSV 报告输出器
/// </summary>
public static class ResultReporter
{
    public static void WriteCsv(string path, List<BackfillReport> reports)
    {
        // UTF-8 BOM 让 Excel 正确识别中文
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("工号,姓名,原性别,新性别,数据来源,是否变更");
        foreach (var r in reports)
        {
            sw.WriteLine($"{EscapeCsv(r.EmployeeCode)},{EscapeCsv(r.RealName)}," +
                         $"{GetGenderName(r.OriginalGender)}," +
                         $"{GetGenderName(r.NewGender)}," +
                         $"{EscapeCsv(r.Source)}," +
                         $"{(r.Changed ? "是" : "否")}");
        }
    }

    private static string GetGenderName(int g) => g == 1 ? "男" : g == 2 ? "女" : "未知";

    private static string EscapeCsv(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}