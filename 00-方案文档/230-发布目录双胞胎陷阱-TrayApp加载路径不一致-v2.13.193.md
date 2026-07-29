# 发布目录双胞胎陷阱 — TrayApp 加载路径不一致问题（v2.13.193 hotfix）

> **版本**：v2.13.193 hotfix
> **日期**：2026-07-27
> **类型**：部署架构陷阱 + 强制同步规范

---

## Context

### 用户报告（v2.13.192 发布后）

> 在宿舍详情 / 当前入住人员 页面，没有显示员工性别列，床号列也没去掉「床位」文字。

### 表面现象

- 源代码 `DormManage.Admin/Pages/Dorms/Details.cshtml` 已正确修改（含 `resident.Gender` 和 `bi-gender-male`）
- 编译后 `release/latest/Admin/DormManage.Admin.dll` 时间戳最新（v2.13.192 16:01）
- 但用户实际访问页面仍显示旧版（HTML 中仍出现「床位 1」「床位 2」）

### 调查路径（5 层深度排查）

| 层 | 验证项 | 结果 |
|----|--------|------|
| 1️⃣ | 源码 `.cshtml` 是否包含 Gender | ✅ 包含（4 处） |
| 2️⃣ | 发布包 `release/latest/Admin/` DLL 是否最新 | ✅ 时间戳 16:01 |
| 3️⃣ | DLL 字符串是否包含 `bi-gender-male`（UTF-16 LE） | ✅ 22 处"性别"+ 9 处"bi-gender-male" |
| 4️⃣ | TrayApp 启动时加载的 DLL 路径 | ❌ **`release/latest/TrayApp/Admin/`**，不是 `release/latest/Admin/`！ |
| 5️⃣ | 子目录中的 DLL 是否同步更新 | ❌ **未更新**（时间戳 09:50，仍是 v2.13.x 旧版） |

---

## 根本原因

### 关键发现

**项目存在两套发布目录！**

```
release/
└── latest/
    ├── Admin/         ← 我修改并发布的目录
    │   └── DormManage.Admin.dll  (16:01)
    └── TrayApp/
        └── Admin/     ← TrayApp 实际加载的子目录
            └── DormManage.Admin.dll  (09:50 ← 旧版！)
```

### TrayApp 配置

`release/latest/TrayApp/appsettings.json`：

```json
{
  "Tray": {
    "ApiExecutable": "..\\Api\\DormManage.Api.exe",       ← 相对路径
    "AdminExecutable": "..\\Admin\\DormManage.Admin.exe"   ← 相对路径
  }
}
```

**当 TrayApp 运行时**：
- 工作目录 = `release/latest/TrayApp/`
- `..\Admin\DormManage.Admin.exe` 解析为 `release/latest/TrayApp/Admin/DormManage.Admin.exe`

**而我之前的发布命令**：

```bash
dotnet publish DormManage.Admin -o release/latest/Admin
```

只更新了 `release/latest/Admin/`，**没有更新** `release/latest/TrayApp/Admin/`。

### 这就是 v2.13.184 强托管规范的双胞胎陷阱

v2.13.184 强托管规则规定：
- TrayApp 启动子进程时使用相对路径 `..\Admin\`、`..\Api\`
- TrayApp 实际加载的是兄弟目录式布局（兄弟目录），不是单层 Admin/Api 目录

但很多开发习惯中 `dotnet publish -o release/latest/Admin` 是默认发布路径，与 TrayApp 的运行路径不一致！

---

## 影响范围

### 受影响的所有文件

| 类型 | 路径 1（被修改） | 路径 2（被加载） | 必须同步 |
|------|-----------------|-----------------|---------|
| Admin DLL | `release/latest/Admin/DormManage.Admin.dll` | `release/latest/TrayApp/Admin/DormManage.Admin.dll` | ✅ |
| Api DLL | `release/latest/Api/DormManage.Api.exe` | `release/latest/TrayApp/Api/DormManage.Api.exe` | ✅ |
| Shared DLL | `release/latest/Admin/DormManage.Shared.dll` | `release/latest/TrayApp/Admin/` + `release/latest/TrayApp/Api/` | ✅ |
| .cshtml 视图 | `release/latest/Admin/Pages/*.cshtml` | `release/latest/TrayApp/Admin/Pages/*.cshtml` | ✅ |

### 潜在 BUG 风险

如果只更新 `release/latest/Admin/` 而忘记同步 `release/latest/TrayApp/Admin/`：
- 编译检查通过（0 error）
- DLL 文件校验通过（时间戳最新）
- **但用户访问的还是旧版**（因为 TrayApp 加载的是兄弟目录）
- 表现为"修改无效"

---

## 实施方案（v2.13.193 强同步规则）

### 修复 1：创建统一的发布同步脚本

**新文件**：`scripts/sync_publish_to_trayapp.sh`（新增脚本）

```bash
#!/bin/bash
# sync_publish_to_trayapp.sh — 发布后必须执行，强同步到 TrayApp 兄弟目录
# 用法：./sync_publish_to_trayapp.sh [--skip-build]
# v2.13.193 hotfix：解决 v2.13.184 强托管规则下 TrayApp 加载路径与发布路径不一致的陷阱

set -e

ROOT=$(pwd)
SKIP_BUILD=false

for arg in "$@"; do
    if [ "$arg" == "--skip-build" ]; then
        SKIP_BUILD=true
    fi
done

if [ "$SKIP_BUILD" != "true" ]; then
    echo "=== Step 1: Build all projects (Release) ==="
    dotnet build DormManage.sln -c Release
fi

echo ""
echo "=== Step 2: Publish all projects to release/latest/ ==="
dotnet publish DormManage.Api/DormManage.Api.csproj -c Release -r win-x64 --self-contained true -o release/latest/Api
dotnet publish DormManage.Admin/DormManage.Admin.csproj -c Release -r win-x64 --self-contained true -o release/latest/Admin
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o release/latest/TrayApp

echo ""
echo "=== Step 3: Sync DLLs + Pages to TrayApp's sibling dirs ==="
# TrayApp 使用相对路径 ..\Admin\ ..\Api\，实际加载 release/latest/TrayApp/Admin/ 和 release/latest/TrayApp/Api/
# 必须同步这些 DLL 和 Pages，否则 TrayApp 加载的仍是旧版

# Admin 子目录同步
cp -f release/latest/Admin/DormManage.Admin.dll         release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Admin.pdb         release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Shared.dll        release/latest/TrayApp/Admin/
cp -rf release/latest/Admin/Pages                       release/latest/TrayApp/Admin/

# Api 子目录同步
cp -f release/latest/Api/DormManage.Api.dll             release/latest/TrayApp/Api/
cp -f release/latest/Api/DormManage.Api.pdb             release/latest/TrayApp/Api/
cp -f release/latest/Api/DormManage.Shared.dll          release/latest/TrayApp/Api/

echo ""
echo "=== Step 4: Verify sync (timestamp + key content) ==="
echo "Admin DLL:"
stat -c '%y  %n' release/latest/TrayApp/Admin/DormManage.Admin.dll
echo "Admin Pages/Dorms/Details.cshtml:"
stat -c '%y  %n' release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
grep -c "resident.Gender" release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
echo "Should be >= 2 (table header + data row)"

echo ""
echo "=== Step 5: Package as ZIP ==="
TS=$(date +%Y%m%d_%H%M%S)
ARCHIVE="release/_archive/DormManage-v2.13.193_${TS}.zip"
rm -f release/_archive/*.zip 2>/dev/null
powershell -Command "Compress-Archive -Path 'release/latest/*' -DestinationPath '${ARCHIVE}' -Force"
echo "Archive: ${ARCHIVE}"

echo ""
echo "=== Sync complete ==="
```

### 修复 2：创建发布检查清单

**新文件**：`scripts/publish_checklist.md`（新增）

```markdown
# 发布检查清单（v2.13.193）

## 发布前
- [ ] 源代码修改已完成
- [ ] `dotnet build DormManage.sln -c Release` 通过 0 error

## 发布中（使用 sync_publish_to_trayapp.sh）
- [ ] 所有项目 publish 到 `release/latest/{Admin,Api,TrayApp}/`
- [ ] DLL 时间戳与 publish 时间一致
- [ ] TrayApp 兄弟目录（`release/latest/TrayApp/Admin/` 和 `release/latest/TrayApp/Api/`）已同步

## 发布后验证
- [ ] `release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml` 含 `resident.Gender` ≥ 2 次
- [ ] `release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml` 不含「床位」字面量（在数据行）
- [ ] `release/latest/TrayApp/Admin/DormManage.Admin.dll` 含 UTF-16 LE "性别" ≥ 5 次
- [ ] `release/latest/TrayApp/Admin/DormManage.Shared.dll` 含 "RuntimeWindowGuard" ≥ 1 次
- [ ] ZIP 包生成到 `release/_archive/`

## 部署验证（关键步骤）
- [ ] **停止所有 DormManage 进程**（`taskkill /F /IM DormManage.*.exe`）
- [ ] 启动 TrayApp
- [ ] 检查 TrayApp 日志：注册校验通过 + Admin 进程已启动 + 健康检查 HTTP 200
- [ ] curl 登录后访问 `/Dorms/Details?id=3` → grep 验证
- [ ] 检查 HTML 中无「床位 N」模式（应为 `'\d+'` 数字）
- [ ] 检查 HTML 中含 `bi-gender-male` 和 `bi-gender-female`
```

---

## 强制规则（v2.13.193）

### 规则 1：发布后必须执行 sync 脚本

❌ **禁止**：仅用 `dotnet publish -o release/latest/Admin/`

✅ **必须**：使用 `scripts/sync_publish_to_trayapp.sh`（含兄弟目录同步）

### 规则 2：检查验证不可省略

发布后必须验证以下内容：

```bash
# 检查 TrayApp 实际加载的目录
grep -c "resident.Gender" release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
# 应该 >= 2
```

如果输出 0 → 发布未生效，必须重新 sync。

### 规则 3：TrayApp 进程启动失败时检查 DLL 版本一致性

如果 Admin 启动报错 `Could not load type 'Xxx'`：
1. 检查 `release/latest/TrayApp/Admin/DormManage.Shared.dll` 是否与 `release/latest/Admin/` 一致
2. 如果不一致 → 同步 Shared DLL
3. 同步完成后重启 TrayApp

---

## 永久教训

1. **「发布路径」≠「运行路径」**：开发者的发布习惯（`release/latest/Admin/`）与 TrayApp 运行路径（`release/latest/TrayApp/Admin/`）不一致，是隐藏陷阱。
2. **目录双胞胎同步必须自动化**：手动 cp 容易遗漏，必须脚本化并加入发布清单。
3. **DLL 错误必查依赖链**：`Could not load type 'Xxx'` 错误说明 Shared DLL 版本不一致，需要同时同步。
4. **"修改无效"先查 TrayApp 兄弟目录**：发布后修改无效，先检查 `release/latest/TrayApp/{Admin,Api}/` 的 DLL 和 .cshtml 是否同步。
5. **ASP.NET Core Razor RuntimeCompilation 不会自动发现兄弟目录**：运行时编译从 `AppContext.BaseDirectory` 加载 views，兄弟目录的 views 不会被自动发现。

---

## 变更影响范围

### 文件变更
- **新建**：`scripts/sync_publish_to_trayapp.sh`
- **新建**：`scripts/publish_checklist.md`
- **更新**：`99-发布程序包与部署规范-v2.13.184.md`（添加 v2.13.193 同步规则章节）

### 行为变更
- 发布流程必须使用 `sync_publish_to_trayapp.sh`，禁止单独 `dotnet publish`
- 部署验证流程增加 5 项 DLL/Views 同步检查