using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 费用标准（v2.13.24 P0-3 修复：与 init_schema.sql [BillingStandard] 表 1:1 对齐）
/// v2.13.61 修复：适用员工类型改为 FK → EmployeeType.Id（基础资料真源关联）
///
/// SQL 列名 → EF Property 名映射（HasColumnName）：
/// - HotWaterPrice  → HotWaterUnitPrice
/// - ColdWaterPrice → ColdWaterUnitPrice
/// - ElectricityPrice → ElectricUnitPrice
///
/// 业务接口中 EF Property 命名不变（C# 习惯 unitPrice 后缀），数据库层通过 HasColumnName 映射。
/// </summary>
[Table("BillingStandard")]
public class BillingStandard : BaseEntity
{
    /// <summary>标准名称</summary>
    [Required]
    [MaxLength(100)]
    public string StandardName { get; set; } = string.Empty;

    /// <summary>
    /// 适用员工类型 ID（v2.13.61 新增 FK 关联）
    /// FK → EmployeeType.Id（基础资料字典的真源）
    /// </summary>
    [Required]
    public int ApplicableTypeId { get; set; }

    /// <summary>
    /// 适用员工类型名称（v2.13.61 冗余字段，便于查询）
    /// 保存时由 ApplicableTypeId 关联 EmployeeType 表写入；不再由前端硬编码
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string ApplicableType { get; set; } = string.Empty;

    /// <summary>
    /// v2.13.61 新增：适用员工类型导航属性（[NotMapped]）
    /// 用于 LINQ JOIN 查询 EmployeeType 表获取 Name/Code
    /// </summary>
    [ForeignKey(nameof(ApplicableTypeId))]
    public EmployeeType? ApplicableTypeNav { get; set; }

    /// <summary>热水单价（元/吨）— 映射 SQL 列 HotWaterPrice</summary>
    [Column("HotWaterPrice")]
    public decimal HotWaterUnitPrice { get; set; }

    /// <summary>冷水单价（元/吨）— 映射 SQL 列 ColdWaterPrice</summary>
    [Column("ColdWaterPrice")]
    public decimal ColdWaterUnitPrice { get; set; }

    /// <summary>电费单价（元/度）— 映射 SQL 列 ElectricityPrice</summary>
    [Column("ElectricityPrice")]
    public decimal ElectricUnitPrice { get; set; }

    /// <summary>
    /// v2.13.93 新增：每员工类型每月补贴标准（元/人·月）
    /// 与 EmployeeType.ApplicableTypeId 配合使用；
    /// 在个人账单中按「入住天数 / 当月天数」折算；
    /// 0 表示该员工类型不享受补贴。
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal SubsidyAmount { get; set; }

    /// <summary>生效开始日期（DATE NOT NULL）</summary>
    [Required]
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>生效结束日期（DATE NULL - 允许永久有效）</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// 是否启用（BIT DEFAULT 1）
    /// v2.13.61 BUG 修复：去掉 `new` 关键字，直接继承 BaseEntity.IsActive
    /// 旧 `new bool IsActive` 在 EF Core 物化时与 BaseEntity.IsActive 冲突导致保存异常
    /// </summary>
    public bool IsActive { get; set; } = true;
}