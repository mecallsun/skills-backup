using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Shared.Services;

public interface IDormService
{
    Task<PagedResult<Dorm>> GetListAsync(string? keyword, int? buildingId, int page, int pageSize);
    Task<Dorm?> GetByIdAsync(int id);

    /// <summary>
    /// 更新宿舍容量（P2-5 约束）
    /// 规则：新容量 >= 当前在宿人数（Status=Staying），否则返回业务错误
    /// </summary>
    Task<ApiResponse<Dorm>> UpdateCapacityAsync(int id, int newCapacity);
}

public class DormService : IDormService
{
    private readonly DormDbContext _db;

    public DormService(DormDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Dorm>> GetListAsync(string? keyword, int? buildingId, int page, int pageSize)
    {
        var query = _db.Dorms.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(d => d.DormCode.Contains(keyword));
        if (buildingId.HasValue)
            query = query.Where(d => d.BuildingId == buildingId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DormCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<Dorm>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<Dorm?> GetByIdAsync(int id)
    {
        return await _db.Dorms.FindAsync(id);
    }

    public async Task<ApiResponse<Dorm>> UpdateCapacityAsync(int id, int newCapacity)
    {
        if (newCapacity < 1)
            return ApiResponse<Dorm>.Fail("INVALID_CAPACITY", "容量必须 ≥ 1");

        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm is null)
            return ApiResponse<Dorm>.Fail("NOT_FOUND", "宿舍不存在");

        // P2-5 核心约束：新容量必须 ≥ 当前在宿人数
        var stayingBookings = await _db.DormBookings
            .Where(b => b.DormCode == dorm.DormCode && b.Status == BookingStatus.Staying)
            .ToListAsync();
        var currentStaying = stayingBookings.Count;

        if (newCapacity < currentStaying)
        {
            return ApiResponse<Dorm>.Fail(
                "CAPACITY_BELOW_OCCUPANCY",
                $"新容量 {newCapacity} 小于当前在宿人数 {currentStaying}，请先办理退房后再调整");
        }

        var oldCapacity = dorm.Capacity;
        dorm.Capacity = newCapacity;

        // v2.13.24 联动2：容量减少时自动重新分配超床位号员工（文档 36 §R-BED-002）
        var bedChanges = new List<string>();
        if (newCapacity < oldCapacity)
        {
            // 找出床位号 > 新容量的员工列表
            var overflowEmployees = stayingBookings
                .Where(b => b.BedNo.HasValue && b.BedNo.Value > newCapacity)
                .ToList();

            if (overflowEmployees.Any())
            {
                // 空闲床位号集合 = [1..新容量] - 已入住员工的床位号
                var usedBeds = stayingBookings
                    .Where(b => b.BedNo.HasValue && b.BedNo.Value <= newCapacity)
                    .Select(b => b.BedNo!.Value)
                    .ToHashSet();
                var freeBeds = Enumerable.Range(1, newCapacity).Where(n => !usedBeds.Contains(n)).ToList();

                // 随机打乱空闲床位号
                var rng = new Random();
                freeBeds = freeBeds.OrderBy(_ => rng.Next()).ToList();

                for (int i = 0; i < overflowEmployees.Count && freeBeds.Any(); i++)
                {
                    var emp = overflowEmployees[i];
                    var newBed = freeBeds[0];
                    freeBeds.RemoveAt(0);

                    var oldBed = emp.BedNo;
                    emp.BedNo = newBed;
                    bedChanges.Add($"员工ID={emp.EmployeeId} 床位{oldBed}→{newBed}");

                    // 同步 SysEmployee.BedNo
                    var sysEmp = await _db.Employees.FindAsync(emp.EmployeeId);
                    if (sysEmp != null && sysEmp.BedNo == oldBed)
                    {
                        sysEmp.BedNo = newBed;
                    }
                }
            }
        }

        // 更新 BedNumbers 床位号集合（v2.12.34）
        dorm.BedNumbers = string.Join(",", Enumerable.Range(1, newCapacity));
        dorm.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        if (bedChanges.Any())
        {
            Console.WriteLine($"[v2.13.24 CapacityAdjust] {dorm.DormCode} 容量{oldCapacity}→{newCapacity}, 床位重新分配: {string.Join("; ", bedChanges)}");
        }

        return ApiResponse<Dorm>.Ok(dorm, $"容量已从 {oldCapacity} 调整为 {newCapacity}{(bedChanges.Any() ? "，已自动重新分配超床位号员工" : "")}");
    }
}