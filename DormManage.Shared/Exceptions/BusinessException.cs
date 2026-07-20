namespace DormManage.Shared.Exceptions;

/// <summary>
/// 业务异常（v2.13.29 新增）
/// 用于业务规则校验失败、表单错误等可预期的用户级别错误
/// 不记录为系统错误日志，会向用户展示错误消息
///
/// 使用示例：
/// <code>
/// if (await IsConflict())
///     throw new BusinessException("DUPLICATE", "该员工已在住宿中");
/// </code>
/// </summary>
public class BusinessException : Exception
{
    /// <summary>错误码（前端可用于国际化或分支判断）</summary>
    public string ErrorCode { get; }

    public BusinessException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}