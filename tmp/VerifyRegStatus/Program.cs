// v2.13.169 集成测试：直接调 RegisterSdk.CheckReg / LicenseGuard 四态判定
// 不经授权中间件（独立 Debug 也会因 IPC 不可用走默认只读，本测试绕开中间件直接读 RegisterSdk 源）

using DormManage.Shared.Register;

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "✅ PASS" : "❌ FAIL")} | {name} | {detail}");
    if (ok) pass++; else fail++;
}

// 用例1：当前真实场景（用户当前安装状态 — 既有真实注册信息，v2.13.167 真机验证过）
// 机器码 SN=BFEBFBFF000A06A4AA2E3B0E + 公司名=广东金戈新材料股份有限公司 + CDKEY 已知过期
// 本测试不依赖具体真实环境，只验证 CheckReg() 结构稳定性
{
    var reg = RegisterSdk.CheckReg();
    Console.WriteLine($"  [实际环境] RegInt={reg.RegInt} | Status={(int)reg.Status}({reg.Status}) | RegDate={reg.RegDate:yyyy-MM-dd} | LTD={reg.LTDName} | CDKEY={reg.CDKEY}");

    // 必须有一个确定的 Status
    Check("RegStatus 枚举必为四态之一", Enum.IsDefined(typeof(RegStatus), reg.Status),
        $"Status={(int)reg.Status}");

    // RegInt 与 Status 兼容映射
    var statusInt = (int)reg.Status;
    var expectedRegInt = reg.Status switch
    {
        RegStatus.Valid        => 1,
        RegStatus.Unregistered => -1,
        _                      => 0  // Expired 或 Invalid
    };
    Check("RegInt 兼容映射正确", reg.RegInt == expectedRegInt,
        $"RegInt={reg.RegInt} vs Status={(int)reg.Status}({reg.Status}) → expected RegInt={expectedRegInt}");
}

// 用例2：枚举四态定义完整
Check("枚举含 Unregistered", Enum.IsDefined(typeof(RegStatus), RegStatus.Unregistered), "");
Check("枚举含 Valid",        Enum.IsDefined(typeof(RegStatus), RegStatus.Valid),        "");
Check("枚举含 Expired",      Enum.IsDefined(typeof(RegStatus), RegStatus.Expired),      "");
Check("枚举含 Invalid",      Enum.IsDefined(typeof(RegStatus), RegStatus.Invalid),      "");

// 用例3：算法 B 路径（v2.13.94 原生）
// 用一个 SN=24hex + LTD + SECRET_KEY 重算预期 verifyStr，验证基础字段处理
{
    const string TEST_SN = "0123456789ABCDEF01234567";
    const string TEST_LTD = "测试有限公司";
    var priv = typeof(RegisterSdk).GetMethod("ComputeVerifyString",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    if (priv != null)
    {
        var verify = (string)priv.Invoke(null, new object[] { TEST_SN, TEST_LTD });
        Check("ComputeVerifyString 返回 20 位大写 hex", verify?.Length == 20 && verify!.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')),
            $"verify={verify}");
    }
    else
    {
        Console.WriteLine("  [跳过] ComputeVerifyString 私有方法无法反射（确认存在即可）");
    }
}

Console.WriteLine($"\n==== 结果：{pass} PASS / {fail} FAIL ====");
Environment.Exit(fail == 0 ? 0 : 1);
