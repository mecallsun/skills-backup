// v2.13.169 端到端测试用 mock IPC server（监听 5099）
// 命令行参数：
//   --regStatus <int>   枚举值：-1=未注册 1=有效 2=已过期 3=校验失败
//   --regDate <yyyy-MM-dd>
//   --ltdName <string>
//   --cdkey <string>
//   --port <int>        默认 5099
//
// 用法示例：
//   MockTrayIpc --regStatus 1 --regDate 2027-12-31 --ltdName "广东金戈新材料股份有限公司"
//   MockTrayIpc --regStatus -1                    （未注册试用）
//   MockTrayIpc --regStatus 2 --regDate 2025-01-01 --ltdName "X"  （已过期）
//   MockTrayIpc --regStatus 3 --cdkey "INVALID_CDKEY_HERE"          （校验失败）
using System.Net;
using System.Text.Json;
using DormManage.Shared.Services;

int port = 5099;
int regStatus = -1;
string? regDate = null;
string ltdName = "";
string cdkey = "";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--regstatus": regStatus = int.Parse(args[++i]); break;
        case "--regdate": regDate = args[++i]; break;
        case "--ltdname": ltdName = args[++i]; break;
        case "--cdkey": cdkey = args[++i]; break;
    }
}

var state = new ServiceIpc.RegStateDto
{
    RegInt = regStatus == 1 ? 1 : (regStatus == -1 ? -1 : 0),
    RegStatus = regStatus,
    SN = "BFEBFBFF000A06A4AA2E3B0E",
    CDKEY = cdkey,
    LTDName = ltdName,
    RegDate = string.IsNullOrEmpty(regDate) ? null : DateTime.Parse(regDate),
    UseTimes = 0,
    DetectedAtUtc = DateTime.UtcNow
};

var server = new IpcServer(port, (cmd, respond) =>
{
    switch (cmd.Command?.ToLowerInvariant())
    {
        case "getregstate":
            state.DetectedAtUtc = DateTime.UtcNow;
            respond(new ServiceIpc.IpcResponse { Success = true, Message = "ok", Data = state });
            break;
        case "ping":
            respond(new ServiceIpc.IpcResponse { Success = true, Message = "pong" });
            break;
        default:
            respond(new ServiceIpc.IpcResponse { Success = false, Message = $"未知: {cmd.Command}" });
            break;
    }
});
server.Start();
Console.Error.WriteLine($"[MockTray] regStatus={regStatus} regDate={regDate ?? "null"} ltdName={ltdName}");

Thread.Sleep(Timeout.Infinite);
server.Dispose();
