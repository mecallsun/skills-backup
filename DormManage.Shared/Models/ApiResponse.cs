namespace DormManage.Shared.Models;

/// <summary>
/// API 统一响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data = default, string? message = null) => new()
    {
        Success = true,
        Message = message ?? "操作成功",
        Data = data
    };

    public static ApiResponse<T> Fail(string code, string message) => new()
    {
        Success = false,
        Code = code,
        Message = message
    };
}

/// <summary>
/// 无数据的 API 响应
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }

    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message ?? "操作成功"
    };

    public static ApiResponse Fail(string code, string message) => new()
    {
        Success = false,
        Code = code,
        Message = message
    };
}


