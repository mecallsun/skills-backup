using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 字段权限清单（v2.13.92 新增）：定义系统所有"敏感/可识别个人信息"字段，
/// 当角色拥有 privacy:field:enable 权限时，这些字段在所有相关页面默认隐藏。
/// 全局共享一份清单；与角色通过"隐私字段权限"总开关关联。
///
/// 典型用法：
///   - 默认敏感字段：姓名（realname）/ 手机号（phone）/ 工号（employeecode）/ 宿舍房号（dormcode）/ 备注（remark）
///   - 角色勾选「启用隐私字段保护」→ 看不到 SysFieldPermission.IsActive=true 的所有字段
///   - 字段不勾选（IsActive=false）→ 即使角色有隐私权限，该字段也仍然显示
///
/// 关联：
///   - 与 SysPermission 解耦（SysPermission 是 RBAC 配置，本表是字段元数据配置）
///   - 字段权限由「角色级总开关」（permission_code = privacy:field:enable，PermissionType=3）触发
/// </summary>
[Table("SysFieldPermission")]
public class SysFieldPermission
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>
    /// 字段唯一键（如 "employee.realname" / "employee.phone"）
    /// 命名约定：{模块}.{字段英文名}，小写
    /// </summary>
    [Required, MaxLength(64)]
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>
    /// 所属模块（如 "Personnel" / "Booking" / "Meter" / "DormBilling"）
    /// </summary>
    [Required, MaxLength(32)]
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// 字段显示名（如 "姓名" / "手机号" / "工号"），UI 展示用
    /// </summary>
    [Required, MaxLength(64)]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型（string/number/date/datetime/boolean）— 仅 UI 提示用，不影响逻辑
    /// </summary>
    [MaxLength(16)]
    public string? FieldType { get; set; }

    /// <summary>
    /// 敏感等级（1=高 PII / 2=中 / 3=低）— UI 显示用，辅助分类排序
    /// </summary>
    public byte SensitivityLevel { get; set; } = 2;

    /// <summary>排序权重（UI 显示顺序）</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用（true=该字段进隐藏清单；false=即使角色有隐私权限也仍显示）
    /// 勾选/取消勾选保存的是此字段
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>用途说明（为什么这个字段敏感）</summary>
    [MaxLength(200)]
    public string? Description { get; set; }

    /// <summary>首次配置时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最后更新时间</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>最后更新人（SysUser.UserName）</summary>
    [MaxLength(64)]
    public string? UpdatedBy { get; set; }
}