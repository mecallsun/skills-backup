# 发布同步完整指南

> Skill: release-bugfix-v2.13.193  
> 适用项目：金智住宿管理系统（DormManage）  
> 创建日期：2026-07-27

---

## 概述

v2.13.193 强制要求发布时同步两个目录：
- `release/latest/Admin/`（开发者发布路径）
- `release/latest/TrayApp/Admin/`（TrayApp 实际加载路径）

**❌ 禁止**：仅用 `dotnet publish -o release/latest/Admin/`

---

## 1. 项目发布目录结构

```
release/latest/
├── Admin/                  ← A. 开发者发布
│   ├── DormManage.Admin.dll
│   ├── DormManage.Shared.dll
│   └── Pages/
│       ├── Dorms/
│       │   ├── Index.cshtml
│       │   └── Details.cshtml
│       └── ...
├── Api/                    ← A. 开发者发布
│   ├── DormManage.Api.dll
│   ├── DormManage.Shared.dll
│   └── ...
└── TrayApp/                ← B. TrayApp 主程序（启动加载子目录）
    ├── DormManage.TrayApp.exe
    ├── appsettings.json    ← ApiExecutable=..\Api\, AdminExecutable=..\Admin\
    ├── Admin/             ← 🔴 v2.13.193 必须同步的子目录
    │   ├── DormManage.Admin.dll   ← TrayApp 实际加载这个
    │   ├── DormManage.Shared.dll
    │   └── Pages/
    │       └── Dorms/
    │           ├── Index.cshtml   ← TrayApp 实际加载这个
    │           └── Details.cshtml
    └── Api/                 ← 🔴 v2.13.193 必须同步的子目录
        ├── DormManage.Api.dll     ← TrayApp 实际加载这个
        ├── DormManage.Shared.dll
        └── ...
```

---

## 2. 完整发布流程

### 步骤 1：环境准备

```bash
# 停止所有运行中的 DormManage 进程
taskkill /F /IM DormManage.TrayApp.exe
taskkill /F /IM DormManage.Admin.exe
taskkill /F /IM DormManage.Api.exe

# 清理编译缓存（推荐）
rm -rf DormManage.Shared/bin/Release/net8.0/
rm -rf DormManage.Shared/obj/Release/net8.0/
rm -rf DormManage.Api/bin/Release/net8.0/
rm -rf DormManage.Api/obj/Release/net8.0/
rm -rf DormManage.Admin/bin/Release/net8.0/
rm -rf DormManage.Admin/obj/Release/net8.0/
rm -rf DormManage.TrayApp/bin/Release/net8.0*/
rm -rf DormManage.TrayApp/obj/Release/net8.0*/
```

### 步骤 2：构建

```bash
dotnet build DormManage.sln -c Release
# 必须输出：0 个错误
```

### 步骤 3：发布（publish）

```bash
# Api
dotnet publish DormManage.Api/DormManage.Api.csproj \
    -c Release -r win-x64 --self-contained true \
    -o release/latest/Api

# Admin
dotnet publish DormManage.Admin/DormManage.Admin.csproj \
    -c Release -r win-x64 --self-contained true \
    -o release/latest/Admin

# TrayApp
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj \
    -c Release -r win-x64 --self-contained true \
    -o release/latest/TrayApp
```

### 步骤 4：同步到 TrayApp 兄弟目录（v2.13.193 关键）

```bash
# Admin 子目录同步
cp -f release/latest/Admin/DormManage.Admin.dll    release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Admin.pdb    release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Shared.dll   release/latest/TrayApp/Admin/
cp -rf release/latest/Admin/Pages                  release/latest/TrayApp/Admin/

# Api 子目录同步
cp -f release/latest/Api/DormManage.Api.dll        release/latest/TrayApp/Api/
cp -f release/latest/Api/DormManage.Api.pdb        release/latest/TrayApp/Api/
cp -f release/latest/Api/DormManage.Shared.dll     release/latest/TrayApp/Api/
```

### 步骤 5：验证同步

```bash
# DLL 时间戳一致
echo "=== DLL timestamps ==="
stat -c '%y  %n' release/latest/TrayApp/Admin/DormManage.Admin.dll \
                   release/latest/TrayApp/Admin/DormManage.Shared.dll

# Views 包含最新修改
grep -c "resident.Gender" release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
# 应该 >= 2
```

### 步骤 6：打包为 ZIP

```bash
# 清理旧包
rm -f release/_archive/DormManage-v*.zip

# 创建新包
TS=$(date +%Y%m%d_%H%M%S)
ARCHIVE="release/_archive/DormManage-v{版本号}_${TS}.zip"

powershell -Command "Compress-Archive -Path 'release/latest/*' -DestinationPath '${ARCHIVE}' -Force"
```

---

## 3. 同步脚本（`scripts/sync_publish_to_trayapp.sh`）

### 3.1 脚本完整代码

```bash
#!/bin/bash
# sync_publish_to_trayapp.sh — 发布后必须执行
# 用法：./sync_publish_to_trayapp.sh [--skip-build]

SKIP_BUILD=false
for arg in "$@"; do
    if [ "$arg" == "--skip-build" ]; then SKIP_BUILD=true; fi
done

cd "$(dirname "$0")/.."

if [ "$SKIP_BUILD" != "true" ]; then
    echo "=== Step 1: Build all projects ==="
    dotnet build DormManage.sln -c Release
fi

echo ""
echo "=== Step 2: Publish all projects ==="
dotnet publish DormManage.Api/DormManage.Api.csproj -c Release -r win-x64 --self-contained true -o release/latest/Api
dotnet publish DormManage.Admin/DormManage.Admin.csproj -c Release -r win-x64 --self-contained true -o release/latest/Admin
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o release/latest/TrayApp

echo ""
echo "=== Step 3: Sync DLLs + Pages to TrayApp's sibling dirs ==="

cp -f release/latest/Admin/DormManage.Admin.dll         release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Admin.pdb         release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Shared.dll        release/latest/TrayApp/Admin/
cp -rf release/latest/Admin/Pages                       release/latest/TrayApp/Admin/

cp -f release/latest/Api/DormManage.Api.dll             release/latest/TrayApp/Api/
cp -f release/latest/Api/DormManage.Api.pdb             release/latest/TrayApp/Api/
cp -f release/latest/Api/DormManage.Shared.dll          release/latest/TrayApp/Api/

echo ""
echo "=== Step 4: Verify sync ==="
echo "Admin DLL:"
stat -c '%y  %n' release/latest/TrayApp/Admin/DormManage.Admin.dll
echo ""
echo "Admin Details.cshtml key contents:"
grep -c "resident.Gender" release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
echo "  (should be >= 2)"
echo ""
echo "Shared DLL - RuntimeWindowGuard:"
grep -ao "RuntimeWindowGuard" release/latest/TrayApp/Admin/DormManage.Shared.dll | head -1
```

### 3.2 使用方法

```bash
# 方法 1：完整构建 + 同步
./scripts/sync_publish_to_trayapp.sh

# 方法 2：仅同步（如果已经单独 build 过）
./scripts/sync_publish_to_trayapp.sh --skip-build
```

---

## 4. 发布问题排查

### 问题 1：发布后看不到修改

**5 步排查**（参见 `methodology.md`）：

1. 源码层：`grep -c "新功能" source.cshtml` → 验证源文件
2. 编译层：`dotnet build -c Release` → 0 错误
3. 时间戳：`stat -c '%y' release/latest/Admin/DormManage.Admin.dll` → 最新
4. DLL 内容：`grep -ao "字符串" .dll` → 应能找到
5. **运行环境**：`Get-Process -Name DormManage.Admin` → 检查加载的 DLL 路径

**最常见**：第 5 步发现问题 → TrayApp 加载的 DLL 在兄弟目录，需要 sync。

### 问题 2：DLL 锁定无法复制

**症状**：`cp: cannot create regular file ... being used by another process`

**修复**：先停止所有 DormManage 进程：

```bash
taskkill /F /IM DormManage.TrayApp.exe
taskkill /F /IM DormManage.Admin.exe
taskkill /F /IM DormManage.Api.exe
```

### 问题 3：发布后 ZipArchiveHelper 错误

**症状**：
```
ZipArchiveHelper: 文件被使用...
```

**修复**：先停止 TrayApp（v2.13.193 TrayApp 锁定 release/latest/TrayApp/ 中的 DLL）：

```bash
taskkill /F /IM DormManage.TrayApp.exe
# 然后再执行 powershell Compress-Archive
```

---

## 5. 发布后必做 7 项验证

### 验证 1：源码层
```bash
grep -c "新功能" source.cs
# > 0
```

### 验证 2：编译层
```bash
dotnet build -c Release | grep "0 个错误"
```

### 验证 3：发布时间戳
```bash
stat -c '%y' release/latest/Admin/DormManage.Admin.dll
# 5 分钟内
```

### 验证 4：DLL 内容
```bash
grep -ao "新功能关键字" release/latest/Admin/DormManage.Admin.dll | wc -l
# >= 1
```

### 验证 5：兄弟目录同步
```bash
# 检查兄弟目录 DLL 是否最新
md5sum release/latest/Admin/DormManage.Admin.dll \
       release/latest/TrayApp/Admin/DormManage.Admin.dll
# 应该相同
```

### 验证 6：运行时健康
```bash
# 启动 TrayApp 后
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5001/
# 302
```

### 验证 7：跨权限测试
- admin 用户：所有内容可见
- 未授权角色用户：隐私字段隐藏

---

## 6. 部署流程（用户视角）

### 步骤 1：解压 ZIP

```cmd
Expand-Archive "DormManage-v2.13.193_20260727_171208.zip" -DestinationPath "D:\DormManage"
```

### 步骤 2：启动 TrayApp

```cmd
cd "D:\DormManage\TrayApp"
DormManage.TrayApp.exe
```

**不要**：
- ❌ 双击 DormManage.Admin.exe（强托管拒绝）
- ❌ 双击 DormManage.Api.exe（强托管拒绝）

### 步骤 3：浏览器访问

```
http://localhost:5001
```

---

## 7. v2.13.193 升级版规则总结

| 行为 | v2.13.184 | v2.13.193 |
|------|----------|----------|
| 发布脚本 | `dotnet publish -o release/latest/Admin` | `scripts/sync_publish_to_trayapp.sh` |
| TrayApp 兄弟目录 | 不更新 | **强制同步** |
| Shared DLL 同步 | 不强制 | **强制** |
| 验证清单 | 无 | 7 项验证 |
| 发布检查 | 无 | `scripts/publish_checklist.md` |
| 修改无效 BUG | 隐藏 | **消除** |

---

## 8. 相关文档

- 发布规范：`00-方案文档/99-发布程序包与部署规范-v2.13.193.md`
- 双胞胎陷阱：`00-方案文档/230-发布目录双胞胎陷阱-TrayApp加载路径不一致-v2.13.193.md`
- BUG 综述：`00-方案文档/232-BUG解决经验与防错指南-v2.13.193综述.md`
- 检查清单：`scripts/publish_checklist.md`
- 同步脚本：`scripts/sync_publish_to_trayapp.sh`

---

**使用此 Skill 触发条件**：
- 用户要求"发布""重新发布""构建并发布"
- 修改代码后需要重新生成 ZIP 包
- v2.13.193 hotfix 部署到生产环境