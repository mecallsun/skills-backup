using System;
using System.Data.SqlClient;
using System.IO;

class Program
{
    static void Main()
    {
        const string connStr = "Server=172.16.0.100;Database=WaterMeterDB;User Id=user;Password=1234;Encrypt=false;TrustServerCertificate=true;";
        var sqlPath = @"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\tmp\ApplySeedMigration\migrate.sql";
        var sqlContent = File.ReadAllText(sqlPath);

        // 拆分 GO 段落（仅在行首 GO 处拆分）
        var batches = sqlContent.Split(new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO", "\nGO" }, StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"解析到 {batches.Length} 个批次");

        using var conn = new SqlConnection(connStr);
        conn.Open();
        Console.WriteLine($"=== 开始执行 {batches.Length} 个 SQL 批次 ===\n");

        int successCount = 0;
        for (int i = 0; i < batches.Length; i++)
        {
            var sql = batches[i].Trim();
            Console.WriteLine($"  [批次 {i+1}] 长度={sql.Length} 开头={sql.Substring(0, Math.Min(50, sql.Length))}");
            if (string.IsNullOrEmpty(sql)) continue;
            try
            {
                using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = 60;
                if (sql.StartsWith("SELECT"))
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Console.WriteLine($"    {rdr[0]}");
                        }
                    }
                }
                else
                {
                    var affected = cmd.ExecuteNonQuery();
                    Console.WriteLine($"  ✓ 批次 {i+1} 执行成功（影响行数: {affected}）");
                }
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 批次 {i+1} 失败：{ex.Message}");
            }
        }

        Console.WriteLine($"\n=== 完成：{successCount}/{batches.Length} 批次成功 ===\n");

        // 最终验证
        using (var verify = new SqlCommand("SELECT Id, FieldKey, FieldName, Module, SensitivityLevel FROM SysFieldPermission ORDER BY SortOrder", conn))
        {
            using var rdr = verify.ExecuteReader();
            Console.WriteLine("SysFieldPermission 当前记录：");
            while (rdr.Read())
            {
                Console.WriteLine($"  [{rdr.GetInt32(0),2}] {rdr.GetString(1),-30} {rdr.GetString(2),-10} ({rdr.GetString(3),-16}) Level={rdr.GetByte(4)}");
            }
        }
    }
}