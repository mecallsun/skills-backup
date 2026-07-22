using System;

namespace DormManage.Shared.Register;

/// <summary>
/// v2.13.94 软件注册授权 — 注册项 DTO（等价 Public.Core.SDK.Register.RegItem）
/// 用于 Razor 页面、API Controller、注册/校验存储共用。
/// </summary>
public class RegItem
{
    /// <summary>机器码 SN（只读，由 GetSN() 生成，绑定 CPU+Disk+OS）</summary>
    public string SN { get; set; } = "";

    /// <summary>注册码 CDKEY（29 位字符 = 5-5-5-5-5 含分隔符）</summary>
    public string CDKEY { get; set; } = "";

    /// <summary>公司/单位名称</summary>
    public string LTDName { get; set; } = "";

    /// <summary>注册结果：0=已过期 1=已注册 -1=未注册</summary>
    public int RegInt { get; set; } = -1;

    /// <summary>注册有效日期（由 GetDateByRegCDKey 解码 CDKEY）</summary>
    public DateTime? RegDate { get; set; }

    /// <summary>试用次数累计（每启动 +1）</summary>
    public int UseTimes { get; set; }
}