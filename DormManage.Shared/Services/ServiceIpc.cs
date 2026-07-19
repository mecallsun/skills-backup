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
}

/// <summary>
/// IPC 客户端（Web Admin 调用托盘）
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