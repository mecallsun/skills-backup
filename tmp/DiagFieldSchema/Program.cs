using System;
using Microsoft.Data.SqlClient;

namespace DiagFieldSchema;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SysFieldPermission 表结构诊断 (直接 SQL) ===\n");

        var connStr = "Server=172.16.0.100;Database=WaterMeterDB;User ID=user;Password=1234;TrustServerCertificate=True;";

        using var conn = new SqlConnection(connStr);
        conn.Open();

        // 查询表结构
        var sql = @"SELECT
            c.COLUMN_NAME,
            c.IS_NULLABLE,
            c.DATA_TYPE,
            COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') as IsIdentity
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME = 'SysFieldPermission'
            ORDER BY c.ORDINAL_POSITION";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();

        Console.WriteLine("列名 | 类型 | 可空 | 是ID列");
        Console.WriteLine(new string('-', 50));
        while (reader.Read())
        {
            var name = reader["COLUMN_NAME"].ToString();
            var type = reader["DATA_TYPE"].ToString();
            var nullable = reader["IS_NULLABLE"].ToString();
            var isIdentity = reader["IsIdentity"].ToString();
            Console.WriteLine($"{name,-15} | {type,-15} | {nullable,-5} | {isIdentity}");
        }
        reader.Close();

        // 检查现有种子数据
        Console.WriteLine("\n=== 现有数据 ===");
        var dataSql = "SELECT TOP 5 Id, FieldKey, Module FROM SysFieldPermission ORDER BY Id";
        using var cmd2 = new SqlCommand(dataSql, conn);
        using var reader2 = cmd2.ExecuteReader();
        while (reader2.Read())
        {
            Console.WriteLine($"  Id={reader2["Id"]}, FieldKey={reader2["FieldKey"]}, Module={reader2["Module"]}");
        }
        reader2.Close();

        // 测试直接 INSERT
        Console.WriteLine("\n=== 直接 INSERT 测试 ===");
        var insertSql = @"INSERT INTO SysFieldPermission (FieldKey, Module, FieldName, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt, UpdatedAt, UpdatedBy)
            VALUES ('test.direct_insert', 'Personnel', '测试', 2, 999, 1, '直接 INSERT 测试', GETDATE(), GETDATE(), 'system')";
        using var cmd3 = new SqlCommand(insertSql, conn);
        try
        {
            var rows = cmd3.ExecuteNonQuery();
            Console.WriteLine($"✓ 直接 INSERT 成功，影响行数：{rows}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 直接 INSERT 失败：{ex.Message}");
        }

        // 查看新插入的记录
        Console.WriteLine("\n=== 最新 3 条记录 ===");
        var verifySql = "SELECT TOP 3 Id, FieldKey, Module FROM SysFieldPermission ORDER BY Id DESC";
        using var cmd4 = new SqlCommand(verifySql, conn);
        using var reader4 = cmd4.ExecuteReader();
        while (reader4.Read())
        {
            Console.WriteLine($"  Id={reader4["Id"]}, FieldKey={reader4["FieldKey"]}, Module={reader4["Module"]}");
        }
    }
}