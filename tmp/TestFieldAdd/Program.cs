using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestFieldAdd;

class Program
{
    static async Task Main(string[] args)
    {
        var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5100";
        Console.WriteLine($"测试目标：{baseUrl}");
        Console.WriteLine($"{"".PadRight(60, '=')}");

        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add("X-User-Name", "admin");

        // 1. 测试新增 - IsActive=true
        Console.WriteLine("\n[测试] POST 新增 - IsActive=true");
        var dto1 = new
        {
            Module = "Personnel",
            FieldKey = $"employee.api_test_{Guid.NewGuid().ToString("N").Substring(0, 6)}",
            FieldName = "API测试字段",
            FieldType = "string",
            SensitivityLevel = 2,
            SortOrder = 0,
            IsActive = true,
            Description = "API 测试 IsActive=true"
        };
        await TestCreate(client, dto1);

        // 2. 测试新增 - IsActive=false
        Console.WriteLine("\n[测试] POST 新增 - IsActive=false (修复 BUG #1)");
        var dto2 = new
        {
            Module = "Personnel",
            FieldKey = $"employee.api_test_{Guid.NewGuid().ToString("N").Substring(0, 6)}",
            FieldName = "API测试字段2",
            FieldType = "string",
            SensitivityLevel = 2,
            SortOrder = 0,
            IsActive = false,
            Description = "API 测试 IsActive=false"
        };
        await TestCreate(client, dto2);

        Console.WriteLine($"\n{"".PadRight(60, '=')}");
        Console.WriteLine("测试完成");
    }

    static async Task TestCreate(HttpClient client, object dto)
    {
        try
        {
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/v1/system/field-permissions", content);
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"  提交数据 FieldKey: {System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("FieldKey").GetString()}");
            Console.WriteLine($"  HTTP 状态：{(int)resp.StatusCode}");
            Console.WriteLine($"  响应：{body.Substring(0, Math.Min(300, body.Length))}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 异常：{ex.Message}");
        }
    }
}