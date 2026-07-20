using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 费用标准（v2.13.24 P0-3 修复：与 init_schema.sql [BillingStandard] 表 1:1 对齐）
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

    /// <summary>适用类型（NVARCHAR(40) NOT NULL）— 合同工/临时工/外包/实习生/驻场</summary>
    [Required]
    [MaxLength(40)]
    public string ApplicableType { get; set; } = string.Empty;

    /// <summary>热水单价（元/吨）— 映射 SQL 列 HotWaterPrice</summary>
    [Column("HotWaterPrice")]
    public decimal HotWaterUnitPrice { get; set; }

    /// <summary>冷水单价（元/吨）— 映射 SQL 列 ColdWaterPrice</summary>
    [Column("ColdWaterPrice")]
    public decimal ColdWaterUnitPrice { get; set; }

    /// <summary>电费单价（元/度）— 映射 SQL 列 ElectricityPrice</summary>
    [Column("ElectricityPrice")]
    public decimal ElectricUnitPrice { get; set; }

    /// <summary>生效开始日期（DATE NOT NULL）</summary>
    [Required]
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>生效结束日期（DATE NOT NULL）</summary>
    [Required]
    public DateOnly EffectiveTo { get; set; }

    /// <summary>是否启用（BIT DEFAULT 1）</summary>
    public new bool IsActive { get; set; } = true;
}