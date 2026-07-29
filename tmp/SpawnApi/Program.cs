// 用 TrayLaunchGuard 相同密钥计算有效握手令牌，绕过 2 秒延迟，看到 Api 真实崩溃堆栈
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

const string ApiExe = @"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\publish-final\Api\DormManage.Api.exe";
const string KEY = "Jinge#Dorm@2026$Tray^Handshake&Key!v1";
const string ChildApi = "Api";

// 模拟 TrayApp 拉起：payload = childKey|trayPid, token = payload|HMAC(payload)
var logFile = @"C:\Users\Mecall\AppData\Local\Temp\api_spawn_capture.log";
File.WriteAllText(logFile, $"=== Spawn started {DateTime.Now} ===\n");
var psi = new ProcessStartInfo
{
    FileName = ApiExe,
    WorkingDirectory = Path.GetDirectoryName(ApiExe),
    UseShellExecute = false,
    CreateNoWindow = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
};

psi.EnvironmentVariables["DormManage_KESTREL_PORT"] = "5100";
psi.EnvironmentVariables["DormManage_DB_CONN"] = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";

// 真实 HMAC：payload 与 TrayApp.CreateHandshakeToken 完全一致
var payload = $"{ChildApi}|{Environment.ProcessId}";
using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(KEY)))
{
    var sig = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
    var sigHex = Convert.ToHexString(sig).ToLowerInvariant();
    psi.EnvironmentVariables["DormManage_TRAY_HANDSHAKE"] = $"{payload}|{sigHex}";
}

Console.WriteLine($"=== Spawning Api with real handshake ===");
Console.WriteLine($"  Token: {psi.EnvironmentVariables["DormManage_TRAY_HANDSHAKE"]}");

var p = Process.Start(psi);
var sbOut = new StringBuilder();
var sbErr = new StringBuilder();
p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
p.BeginOutputReadLine();
p.BeginErrorReadLine();

bool exited = p.WaitForExit(20000);
if (!exited) p.Kill();

Console.WriteLine($"\n=== Exit Code: {p.ExitCode} (0=ok/graceful, 0xE0434352=CLR unhandled exception) ===");
Console.WriteLine($"\n=== STDOUT ({sbOut.Length} chars) ===\n{sbOut}");
Console.WriteLine($"\n=== STDERR ({sbErr.Length} chars) ===\n{sbErr}");
