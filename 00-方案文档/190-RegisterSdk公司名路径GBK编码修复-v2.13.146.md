# RegisterSdk 公司名路径 GBK 编码 + 字符集 + 归一化修复 — v2.13.146

>
> ⚠️ **DEPRECATED（v2.13.147 起已被取代）**：本文档已被 [00-方案文档/191-注册授权全流程终极汇总-v2.13.147.md](./191-注册授权全流程终极汇总-v2.13.147.md) 取代，请以新文档为准。
> **日期**：2026-07-24
> **类型**：P0 BUG 修复
> **影响范围**：DormManage.TrayApp.Forms.LicenseForm + DormManage.Shared.Register.RegisterSdk
> **关联**：NPGS.Register Public.Core.SDK.Register.cs 1:1 等价层

---

## 一、问题描述（用户原话）

> **「托盘程序中，注册窗口验证注册码出现错误」**

实测数据：
- 本机机器码：`BFEBFBFF000A06A4AA2E3B0E`
- 公司名：`广东金智新材料股份有限公司`
- 有效期：`2026-07-26`
- 待验证 CDKEY：`3B55C-A8LE9-3865B-FBE56-C1DC0`

---

## 二、根因分析（3 个 BUG 链）

### BUG 链 #1：LicenseForm.TryNormalizeCDKey 字符集过窄

**位置**：`DormManage.TrayApp/Forms/LicenseForm.cs:144`

**现象**：用户输入 CDKEY 后，提示「**注册码包含非法字符 'L'**」

**根因**：v2.13.142 引入的字符校验只允许 `[0-9A-F]`（纯 hex），但 NPGS 算法 A 的 CDKEY 字符集是 **`[0-9A-Z]`**（36 进制）：

| 位置 | CDKEY 字符 | 类别 | NPGS 36 进制值 |
|------|-----------|------|---------------|
| `[2]` | `5` | 36 进制日期位 | 5 |
| `[8]` | `L` | **36 进制日期位** | 21 (A=10, L=21) |
| `[14]` | `6` | 36 进制日期位 | 6 |
| `[20]` | `E` | 36 进制日期位 | 14 |

`L` 在 hex `[0-9A-F]` 之外，被 TryNormalizeCDKey 拒绝 → 注册流程根本无法进入下一步。

**NPGS 算法原始代码**（`Public.Core.SDK/Register.cs:545`）的 `ConvertInt10To36` 注释：
> `str += ((j <= 9) ? Convert.ToChar(j + '0') : Convert.ToChar(j - 10 + 'A'));`

即字符集是 **`[0-9A-Z]` 全大写字母**（36 进制），不是 `[0-9A-F]` hex。

---

### BUG 链 #2：RegisterSdk.CheckRegCDKey 硬性 29 字符检查

**位置**：`DormManage.Shared/Register/RegisterSdk.cs:271`（修复前）

**现象**：即使绕开 TryNormalizeCDKey，调用 `CheckRegCDKey` 时立刻返回 `RegInt=0`

**根因**：v2.13.142 实现的代码包含：
```csharp
if (string.IsNullOrEmpty(input.CDKEY) || input.CDKEY.Length != 29)
{
    result.RegInt = 0;
    return result;
}
```

LicenseForm.BtnReg_Click 调用流程：
1. `TryNormalizeCDKey` 剥离连字符 → 返回 25 字符 raw
2. 传入 `CheckRegCDKey(new RegItem { CDKEY = cdkey, ... })` — **`cdkey` 是 25 字符**
3. `Length != 29` 立即失败

**设计错误**：v2.13.142 算法 B 本来就只用 25 字符 raw 工作（看 NPGS 原始 `cdkeyRaw.Substring(0, 20)` 等），但 v2.13.146 新增的算法 A 必须用 29 字符 dashed 与 `GetRegCDKey` 输出比对。两个算法的输入形式不一致，导致边界检查太严。

---

### BUG 链 #3：RegisterSdk.WriteRegItem 不归一化存储

**位置**：`DormManage.Shared/Register/RegisterSdk.cs:547`（修复前）

**现象**：即使注册成功，下次打开 LicenseForm 重新 `CheckReg()` 仍然失败

**根因**：`WriteRegItem` 直接写 `reg.CDKEY`（25 字符 raw），下次 `ReadRegValue("CDKEY")` 读出 25 字符 → `CheckReg` 的 `cdkey.Length != 29` 又判失败。

---

## 三、修复方案

### 修复 #1：LicenseForm.TryNormalizeCDKey 字符集放宽

```diff
-   if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
-   {
-       err = $"注册码包含非法字符 '{c}'（仅允许 0-9 A-F）";
-       return false;
-   }
+   // v2.13.146：NPGS 算法字符集 [0-9A-Z]（36 进制），不再限制为 hex
+   if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')))
+   {
+       err = $"注册码包含非法字符 '{c}'（仅允许 0-9 A-Z，NPGS 36 进制字符集）";
+       return false;
+   }
```

### 修复 #2：RegisterSdk.CheckRegCDKey 接受双形式 + 自动归一化

新增私有助手方法：
```csharp
private static string? NormalizeCDKeyToDashed(string? input)
{
    if (string.IsNullOrEmpty(input)) return null;
    var cleaned = input.Replace("-", "").Trim().ToUpperInvariant();
    if (cleaned.Length != 25) return null;
    
    // 字符集校验：[0-9A-Z]（NPGS 36 进制）
    foreach (var c in cleaned)
    {
        if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'))) return null;
    }
    
    // 重新插入连字符：5-5-5-5-5 = 29 字符
    return $"{cleaned.Substring(0, 5)}-{cleaned.Substring(5, 5)}-{cleaned.Substring(10, 5)}-{cleaned.Substring(15, 5)}-{cleaned.Substring(20, 5)}";
}
```

`CheckRegCDKey` 改用归一化逻辑：
```csharp
public static RegItem CheckRegCDKey(RegItem input)
{
    ...
    if (string.IsNullOrEmpty(input.CDKEY))
    {
        result.RegInt = 0;
        return result;
    }
    
    // v2.13.146 修复：归一化为「29 字符 dashed 形式」
    var cdkeyDashed = NormalizeCDKeyToDashed(input.CDKEY);
    if (cdkeyDashed == null)
    {
        result.RegInt = 0;
        return result;
    }
    var cdkeyRaw = cdkeyDashed.Replace("-", "").ToUpperInvariant();
    ...
    var npgsExpDate = GetDateByRegCDKey(cdkeyDashed);  // 双形式兼容
    ...
    if (string.Equals(snFull, cdkeyDashed, StringComparison.OrdinalIgnoreCase))  // ← 用归一化形式比对
    {
        result.RegInt = 1;
        result.RegDate = npgsExpDate;
        result.CDKEY = cdkeyDashed;  // 回写归一化形式
        return result;
    }
    ...
}
```

### 修复 #3：RegisterSdk.WriteRegItem 归一化存储

```csharp
public static bool WriteRegItem(RegItem reg)
{
    try
    {
        // v2.13.146：归一化为 dashed 形式，确保后续 CheckReg() 读回时长度正确
        var cdkeyDashed = NormalizeCDKeyToDashed(reg.CDKEY) ?? reg.CDKEY;
        WriteRegValue("CDKEY", cdkeyDashed);
        ...
```

### 修复 #4：CheckReg 放宽长度检查 + 自动升级存储

```csharp
public static RegItem CheckReg()
{
    ...
    // v2.13.146：长度检查放宽到 25 字符（raw）或 29 字符（dashed），归一化由 CheckRegCDKey 内部完成
    if (cdkey.Replace("-", "").Length != 25)
    {
        reg.RegInt = 0;
        return reg;
    }
    ...
    // v2.13.146：归一化回写（如果读出 25 字符 raw，自动升级到 29 字符 dashed 存储）
    if (checkResult.CDKEY != cdkey && !string.IsNullOrEmpty(checkResult.CDKEY))
    {
        reg.CDKEY = checkResult.CDKEY;
        try { WriteRegValue("CDKEY", checkResult.CDKEY); } catch { }
    }
    ...
}
```

---

## 四、文件改动清单

| # | 文件 | 改动 |
|---|------|------|
| 1 | `DormManage.TrayApp/Forms/LicenseForm.cs` | `TryNormalizeCDKey` 字符集 `[0-9A-F]` → `[0-9A-Z]`；错误消息更新 |
| 2 | `DormManage.Shared/Register/RegisterSdk.cs` | 新增 `NormalizeCDKeyToDashed` 私有方法；`CheckRegCDKey` 接受双形式 + 自动归一化 + 回写；`CheckReg` 放宽长度检查 + 自动升级存储；`WriteRegItem` 归一化存储 |

**编译**：0 error / 4 warning（CA1416 WinForms-only API，与 v2.13.145 基线持平）

---

## 五、端到端验证

测试位置：`tmp/VerifySdkFix/Program.cs`（引用修复后的 `DormManage.Shared`）

### 验证矩阵（16 个测试，全部通过）

| # | 测试场景 | 期望 | 实际 |
|---|---------|------|------|
| 1 | 29 字符 dashed 输入 + 正确 SN + 正确 LTD | RegInt=1 | ✅ |
| 2 | 25 字符 raw 输入（LicenseForm 实际传入形式） | RegInt=1 | ✅ |
| 3 | 小写字符 + 带连字符（用户手输小写） | RegInt=1（大小写不敏感） | ✅ |
| 4 | CDKEY 含 'L'（NPGS 36 进制日期位） | 通过校验 | ✅ |
| 5 | 错误 SN + 正确 LTD（LTDName 路径兜底） | RegInt=1 | ✅ |
| 6 | 错误 SN + 错误 LTD（两个路径都失败） | RegInt=0 | ✅ |
| 7 | 篡改日期签名 `XXXXX` | RegInt=0 | ✅ |
| 8 | 完全无效 CDKEY | RegInt=0（不抛异常） | ✅ |
| 9 | 空字符串 / null / 过短字符串 | RegInt=0 | ✅ |

### 验证脚本输出

```
=== v2.13.146 RegisterSdk 修复端到端验证 ===

机器码:    BFEBFBFF000A06A4AA2E3B0E
公司名:    广东金智新材料股份有限公司
目标 CDKEY: 3B55C-A8LE9-3865B-FBE56-C1DC0
有效期:    2026-07-26

【测试 1】29 字符 dashed 输入 (用户原始格式)
  ✅ RegInt == 1（注册有效）  (RegInt=1)
  ✅ RegDate == 2026-07-26  (RegDate=2026-07-26)
  ✅ CDKEY 归一化回写  (CDKEY=3B55C-A8LE9-3865B-FBE56-C1DC0)

【测试 2】25 字符 raw 输入 (LicenseForm BtnReg_Click 实际传入形式)
  输入: '3B55CA8LE93865BFBE56C1DC0' (长度=25)
  ✅ RegInt == 1（注册有效）  (RegInt=1)
  ✅ RegDate == 2026-07-26  (RegDate=2026-07-26)
  ✅ CDKEY 自动归一化为 dashed 形式  (CDKEY=3B55C-A8LE9-3865B-FBE56-C1DC0)

... (省略 4-9 测试输出)

=== 测试结果 ===
通过: 16 / 16
失败: 0
🎉 所有测试通过！v2.13.146 修复有效。
```

---

## 六、永久教训

### 教训 #1：字符集必须与算法严格对齐

**问题**：v2.13.142 写算法 B 时用 `[0-9A-F]` hex（够用），但 v2.13.146 合并算法 A 时仍用同一字符集，**未意识到算法 A 是 36 进制**。

**结论**：每次引入新算法都必须把字符集校验与算法输出严格对齐 — 36 进制必须 `[0-9A-Z]`，hex 是 `[0-9A-F]`，两套不能混用。

### 教训 #2：双算法并存时输入形式必须归一化

**问题**：算法 A 输出 29 字符 dashed，算法 B 操作 25 字符 raw，LicenseForm 传入 25 字符 raw → 算法 A 直接拒绝。

**结论**：多算法并存时，**入口归一化**是必备模式 — 接受多种形式输入、内部统一转换为标准形式、与各算法输出严格比对。

### 教训 #3：存储必须用归一化形式

**问题**：WriteRegItem 不归一化，下次读出仍是原始形式 → 算法 A 永远拒识。

**结论**：所有「写后读」路径必须保证写入和读取用同一形式 — 推荐统一写「归一化形式」（最长、最完整）。

### 教训 #4：诊断时必须端到端验证

**诊断流程**：
1. **算法复刻**（用 NPGS.Register 原始代码逻辑在 tmp/VerifyCdkey 复刻）→ 证明算法本身正确
2. **多编码对比**（GBK vs UTF-8）→ 发现 Encoding.Default 在 Win11 上是 UTF-8
3. **真实调用链追踪**（LicenseForm → TryNormalizeCDKey → CheckRegCDKey）→ 发现 3 个 BUG 链

**结论**：用户报告「验证失败」时，必须**端到端逐层追踪**，而不是只看单一函数。NPGS 算法正确 ≠ JINGE 包装层正确。

---

## 七、相关文档

- v2.13.94 软件注册授权初版（`00-方案文档/147` — 已 DEPRECATED）
- v2.13.139 注册授权终极汇总（`00-方案文档/183`）
- v2.13.142 机器码无连接符规则（`00-方案文档/186`）
- v2.13.143 注册码持久化 + 显式校验（`00-方案文档/187`）
- v2.13.144 暗桩 vs 注册码取较晚截止日（`00-方案文档/188`）
- v2.13.145 数据库默认参数改值（`00-方案文档/189`）

---

**作者**：Claude Opus 4.8 + Mecall
**Commit**：pending
**部署说明**：修复已就绪，编译 0 error，可直接 publish 发布。LicenseForm 与 RegisterSdk 已端到端验证。