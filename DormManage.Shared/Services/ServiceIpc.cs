using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DormManage.Shared.Services;

/// <summary>
/// 服务 IPC 通信（P1-11，v2.13.19 增加数据库配置双向同步）
///
/// 协议：TCP + JSON 行（每条命令一行 JSON）
/// 默认端口：5099（127.0.0.1）
///
/// 命令格式：
/// { "command": "ping" }
/// { "command": "status" }
/// { "command": "start", "service": "api" }
/// { "command": "stop", "service": "api" }
/// { "command": "restart", "service": "all" }
/// { "command": "getdbconfig" }                                                -- v2.13.19 新增
/// { "command": "setdbconfig", "payload": { DatabaseConfigDto 字段 } }          -- v2.13.19 新增
/// { "command": "dbconfig.updated", "payload": { DatabaseConfigDto 字段 } }     -- TrayApp 主动推送
/// { "command": "getregstate" }                                                -- v2.13.137 新增（注册状态查询）
/// { "command": "regstate.changed", "payload": { RegStateDto } }               -- v2.13.137 新增（TrayApp 主动推送）
///
/// 响应格式：
/// { "success": true, "message": "已启动", "data": { ... } }
/// </summary>
public static class ServiceIpc
{
    public const int DefaultPort = 5099;

    public class IpcCommand
    {
        public string Command { get; set; } = "";
        public string? Service { get; set; }
        public Dictionary<string, object?>? Payload { get; set; }
    }

    public class IpcResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }

    /// <summary>
    /// v2.13.137 注册状态 DTO（TrayApp → Admin/Api 的注册信息传输格式）
    /// 数据来源：托盘端 RegisterSdk.CheckReg()（Web/Api 端不再独立调用 RegisterSdk）
    /// </summary>
    public class RegStateDto
    {
        /// <summary>
        /// 注册结果：0=已过期 1=已注册 -1=未注册
        /// 保留 v2.13.137 兼容（LicenseGuard.IsReadOnly 内部使用）
        /// </summary>
        public int RegInt { get; set; } = -1;

        /// <summary>
        /// v2.13.169 拆分：注册状态枚举 -1=Unregistered / 1=Valid / 2=Expired / 3=Invalid
        /// 与 RegInt 区别：RegInt 是 RegisterSdk 内嵌的字符串表示（0=-1/1=2），RegStatus 是清晰枚举
        /// 用于前端 status 字段，LicenseStatusController 返回 -1/-2/1/2/3 四态 + 不可用态
        /// </summary>
        public int RegStatus { get; set; } = -1;

        /// <summary>机器码 SN（24 位 hex，由托盘端生成）</summary>
        public string SN { get; set; } = "";

        /// <summary>注册码 CDKEY（脱敏显示，可空）</summary>
        public string CDKEY { get; set; } = "";

        /// <summary>公司/单位名称</summary>
        public string LTDName { get; set; } = "";

        /// <summary>注册有效日期（ISO 8601，可空）</summary>
        public DateTime? RegDate { get; set; }

        /// <summary>试用次数累计</summary>
        public int UseTimes { get; set; }

        /// <summary>TrayApp 检测时间戳（ISO 8601）</summary>
        public DateTime DetectedAtUtc { get; set; }
    }
}

/// <summary>
/// IPC 客户端（Web Admin 调用托盘）
/// v2.13.137 扩展：增加注册状态查询便捷方法
/// </summary>
public class IpcClient
{
    private readonly int _port;
    public IpcClient(int port = ServiceIpc.DefaultPort) { _port = port; }

    public async Task<ServiceIpc.IpcResponse> SendAsync(ServiceIpc.IpcCommand cmd, int timeoutMs = 5000)
    {
        using var client = new TcpClient { ReceiveTimeout = timeoutMs, SendTimeout = timeoutMs };
        await client.ConnectAsync(IPAddress.Loopback, _port);

        using var stream = client.GetStream();
        var json = JsonSerializer.Serialize(cmd) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(bytes);

        var buffer = new byte[4096];
        var ms = new MemoryStream();
        while (true)
        {
            var n = await stream.ReadAsync(buffer);
            if (n == 0) break;
            ms.Write(buffer, 0, n);
            if (ms.Length > 0 && ms.ToArray()[ms.Length - 1] == '\n') break;
        }

        var responseText = Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\n');
        return JsonSerializer.Deserialize<ServiceIpc.IpcResponse>(responseText)
            ?? new ServiceIpc.IpcResponse { Success = false, Message = "空响应" };
    }

    /// <summary>
    /// v2.13.137：查询托盘端当前注册状态（Web/Api 中间件调用）
    /// 设计要点：
    /// - 超时 2s（避免阻塞 HTTP 请求管道）
    /// - 托盘未运行 → 抛 IpcUnavailableException（中间件按"只读"处理）
    /// - 托盘响应失败 → 抛 IpcUnavailableException
    /// </summary>
    public async Task<ServiceIpc.RegStateDto> GetRegStateAsync(int timeoutMs = 2000)
    {
        var resp = await SendAsync(
            new ServiceIpc.IpcCommand { Command = "getregstate" },
            timeoutMs);

        if (!resp.Success || resp.Data is null)
        {
            throw new IpcUnavailableException(
                $"托盘端响应失败：{resp.Message ?? "无数据"}（可能托盘未运行）");
        }

        var json = JsonSerializer.Serialize(resp.Data);
        var state = JsonSerializer.Deserialize<ServiceIpc.RegStateDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return state ?? throw new IpcUnavailableException("注册状态反序列化失败");
    }
}

/// <summary>
/// v2.13.137：IPC 不可用异常（托盘未运行 / 响应失败）
/// 中间件捕获此异常 → 进入"只读"模式（拒绝所有 POST/PUT/DELETE）
/// </summary>
public class IpcUnavailableException : Exception
{
    public IpcUnavailableException(string message) : base(message) { }
}

/// <summary>
/// IPC 服务端（托盘程序内置，接收 Web Admin 命令）
/// </summary>
public class IpcServer : IDisposable
{
    private readonly int _port;
    private readonly Action<ServiceIpc.IpcCommand, Action<ServiceIpc.IpcResponse>> _handler;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public bool IsRunning => _loop is not null;

    public IpcServer(int port, Action<ServiceIpc.IpcCommand, Action<ServiceIpc.IpcResponse>> handler)
    {
        _port = port;
        _handler = handler;
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _loop = Task.Run(() => LoopAsync(_cts.Token));
        }
        catch
        {
            // 端口占用等异常
            _loop = null;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct));
            }
            catch (OperationCanceledException) { break; }
            catch { /* 忽略单次错误，继续接受 */ }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                var ms = new MemoryStream();
                while (!ct.IsCancellationRequested)
                {
                    var n = await stream.ReadAsync(buffer, ct);
                    if (n == 0) break;
                    ms.Write(buffer, 0, n);
                    var data = ms.ToArray();
                    if (data.Length > 0 && data[^1] == '\n') break;
                }

                var line = Encoding.UTF8.GetString(ms.ToArray()).Trim();
                if (string.IsNullOrEmpty(line)) return;

                ServiceIpc.IpcCommand? cmd = null;
                try { cmd = JsonSerializer.Deserialize<ServiceIpc.IpcCommand>(line); } catch { }

                ServiceIpc.IpcResponse resp;
                if (cmd is null)
                {
                    resp = new ServiceIpc.IpcResponse { Success = false, Message = "无效 JSON" };
                }
                else
                {
                    ServiceIpc.IpcResponse? captured = null;
                    _handler(cmd, r => captured = r);
                    resp = captured ?? new ServiceIpc.IpcResponse { Success = false, Message = "无响应" };
                }

                var respJson = JsonSerializer.Serialize(resp) + "\n";
                var respBytes = Encoding.UTF8.GetBytes(respJson);
                await stream.WriteAsync(respBytes, ct);
            }
        }
        catch { /* 客户端断开 */ }
    }

    public void Dispose() => Stop();
}