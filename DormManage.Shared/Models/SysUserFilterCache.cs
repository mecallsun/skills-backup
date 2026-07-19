using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 用户筛选条件云端缓存（v2.13.12）
///
/// 用途：
///   - 当用户在个人中心勾选"存储筛选条件"后，跨设备同步其 6 大列表模块（Personnel/Dorms/Booking/Meter/DormBilling/EmployeeBilling）
///     的筛选条件值，下次登录自动加载到对应模块筛选区。
///   - 与 localStorage 缓存并存：localStorage 优先（实时），服务端兜底（跨设备）。
///   - 退出登录时若未勾选"存储筛选条件"，由前端清空 localStorage；服务端缓存需用户主动调用 Reset 接口清除。
///
/// 字段：
///   - Id：主键
///   - UserId：所属用户
///   - ModuleName：模块标识（personnel/dorms/booking/meter/dormBilling/employeeBilling）
///   - FilterJson：筛选条件 JSON 字符串（与前端 FilterPersistence.collectFromForm 输出一致）
///   - UpdatedAt：最后更新时间（秒）
///   - CreatedAt：首次写入时间
///
/// 关联：
///   - 与 SysUser 多对一关系（UserId → SysUser.UserId）
///   - 唯一约束：(UserId, ModuleName) 防止重复存储
/// </summary>
[Table("SysUserFilterCache")]
public class SysUserFilterCache
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>所属用户 ID（v2.13.7 已改为 UserId 而非 Id，关联 SysUser.UserId）</summary>
    public int UserId { get; set; }

    /// <summary>
    /// 模块标识（v2.13.12 标准化枚举）
    /// 可选值：personnel / dorms / booking / meter / billingStandard / dormBilling / employeeBilling
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// 筛选条件 JSON 字符串
    /// 格式：与前端 FilterPersistence.collectFromForm 输出格式一致
    /// 示例：{"Keyword":"张三","DepartmentId":1,"Status":2}
    /// </summary>
    [Required]
    [MaxLength(4000)]
    public string FilterJson { get; set; } = "{}";

    /// <summary>首次写入时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}