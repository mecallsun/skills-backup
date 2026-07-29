using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

class Program
{
    static void Main()
    {
        var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
        var optionsBuilder = new DbContextOptionsBuilder<DormDbContext>();
        optionsBuilder.UseSqlServer(connStr);
        using var db = new DormDbContext(optionsBuilder.Options);
        
        var user = db.SysUsers.FirstOrDefault(u => u.UserName == "test");
        if (user == null) { Console.WriteLine("test not found"); return; }
        
        Console.WriteLine($"User: {user.UserName}");
        Console.WriteLine($"PasswordHash: {user.PasswordHash.Substring(0, 20)}...");
        Console.WriteLine($"ExpiresAt: {user.ExpiresAt:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"IsActive: {user.IsActive}");
        Console.WriteLine($"IsLocked: {user.IsLocked}");
        Console.WriteLine($"UpdatedAt: {user.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff}");
        
        // Check if BCrypt verifies '123456' for this user
        var valid = BCrypt.Net.BCrypt.Verify("123456", user.PasswordHash);
        Console.WriteLine($"BCrypt Verify('123456'): {valid}");
    }
}
