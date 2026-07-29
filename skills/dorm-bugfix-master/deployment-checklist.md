# 部署清单（DormManage v2.13.193+）

> Skill: dorm-bugfix-master
> 创建日期：2026-07-28
> 配合 `scripts/publish_checklist.md` 使用

---

## 部署流程总览

```
[开发] → [本地测试] → [发布构建] → [部署验证] → [用户使用]
                              ↓
                       [发布失败？] → [紧急回滚]
```

---

## 1. 发布前检查（开发机）

### 1.1 源码完整性

```bash
# 1. 工作区无未提交修改
git status --short | head -10
# 期望：只有目标文件被修改

# 2. 编译通过
dotnet build DormManage.sln -c Release
# 期望：0 个错误
```

### 1.2 测试通过

参见 `testing-checklist.md`。

### 1.3 文档更新

- [ ] 00-方案文档/XXX-BUG 修复文档已创建
- [ ] CLAUDE.md 已更新 v2.13.XXX 备注
- [ ] known-bugs.md 已更新案例库
- [ ] 01-DDL-Schema.sql（如果改了 DB）已更新

---

## 2. 发布构建（开发机）

### 2.1 标准发布流程

```bash
# 步骤 1：构建
dotnet build DormManage.sln -c Release

# 步骤 2：发布三个项目
dotnet publish DormManage.Api/DormManage.Api.csproj \
    -c Release -r win-x64 --self-contained true \
    -o release/latest/Api
dotnet publish DormManage.Admin/DormManage.Admin.csproj \
    -c Release -r win-x64 --self-contained true \
    -o release/latest/Admin
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj \
    -c Release -r win-x64 --self-contained true \
    -o release/latest/TrayApp

# 步骤 3：同步兄弟目录（强制！）
bash scripts/sync_publish_to_trayapp.sh --skip-build
```

### 2.2 发布验证（7 项）

参见 `scripts/publish_checklist.md`：
- [ ] DLL 时间戳
- [ ] Views 同步
- [ ] DLL 字符串（UTF-16 LE）
- [ ] Shared DLL 同步
- [ ] ZIP 包生成
- [ ] 运行时健康
- [ ] 跨权限测试

### 2.3 打包 ZIP

```bash
TS=$(date +%Y%m%d_%H%M%S)
ARCHIVE="release/_archive/DormManage-v2.13.193_${TS}.zip"
powershell -Command "Compress-Archive -Path 'release/latest/*' -DestinationPath '${ARCHIVE}' -Force"
```

---

## 3. 部署到生产机

### 3.1 部署前停止所有进程

```bash
# 远程登录或 RDP 到生产机
taskkill /F /IM DormManage.TrayApp.exe
taskkill /F /IM DormManage.Admin.exe
taskkill /F /IM DormManage.Api.exe
sleep 3

# 验证已停止
tasklist | grep -i DormManage
# 期望：空输出
```

### 3.2 备份旧版本

```bash
# 备份当前 release 目录
mv release release.backup.$(date +%Y%m%d_%H%M%S)
```

### 3.3 解压新版本

```cmd
Expand-Archive "DormManage-v2.13.193_20260727_180444.zip" -DestinationPath "D:\DormManage"
```

### 3.4 配置检查

- [ ] 检查 `release/latest/TrayApp/appsettings.json`：
  - `ApiExecutable` 是否正确
  - `AdminExecutable` 是否正确
  - `Database.ConnectionString` 是否正确
- [ ] 检查注册表（HKLM\Software\JINGE\DormManage）：
  - CDKEY / LTDName / RegDate 是否已配置

### 3.5 启动 TrayApp

```cmd
cd D:\DormManage\release\latest\TrayApp
DormManage.TrayApp.exe
```

### 3.6 部署后验证

- [ ] TrayApp 启动成功（日志显示）
- [ ] API 进程已启动（PID 显示）
- [ ] Admin 进程已启动（PID 显示）
- [ ] 健康检查 HTTP 200
- [ ] 浏览器访问 http://localhost:5001

---

## 4. 部署后 7 项验证

### 4.1 进程检查

```powershell
Get-Process -Name DormManage* | Format-Table Id, ProcessName, StartTime, FileName
# 期望：TrayApp + Admin + Api 三个进程都在运行
```

### 4.2 端口检查

```powershell
netstat -ano | findstr ":5001" | findstr "LISTENING"
netstat -ano | findstr ":5100" | findstr "LISTENING"
# 期望：两个端口都在监听
```

### 4.3 健康检查

```bash
curl -s -o /dev/null -w "Admin: %{http_code}\n" http://localhost:5001/
curl -s -o /dev/null -w "Api: %{http_code}\n" http://localhost:5100/api/v1/system/dbhealth/quick
# 期望：200 或 302（重定向到登录页）
```

### 4.4 跨权限测试

参见 `testing-checklist.md` 第 2 节。

### 4.5 隐私字段验证

```bash
# 登录 test:test，访问 /Dorms，验证 d-none 数量 > 0
```

### 4.6 有效期验证

```bash
# 验证过期当天拒绝登录
```

### 4.7 浏览器验证

```
访问 http://localhost:5001
按 Ctrl+F5 强制刷新
验证：
- 性别列显示
- 床号只显示数字
- 隐私字段按权限显示
```

---

## 5. 部署失败应急回滚

### 5.1 何时回滚

- TrayApp 启动失败
- Admin/Api 进程反复重启
- 数据库连接失败
- 跨权限测试失败
- 浏览器验证发现严重 BUG

### 5.2 回滚步骤

```bash
# 1. 停止所有进程
taskkill /F /IM DormManage.TrayApp.exe
taskkill /F /IM DormManage.Admin.exe
taskkill /F /IM DormManage.Api.exe

# 2. 恢复备份
mv release.backup.<timestamp> release

# 3. 启动旧版本
cd release/latest/TrayApp
DormManage.TrayApp.exe

# 4. 验证旧版本工作
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5001/
```

### 5.3 报告 BUG

- 立即记录到 `known-bugs.md`
- 创建 BUG 修复文档
- 不能重复部署有问题的版本

---

## 6. 部署后监控

### 6.1 关键指标

- [ ] API 响应时间 < 200ms
- [ ] 健康检查 HTTP 200
- [ ] DB 连接池无错误
- [ ] 内存使用 < 500MB
- [ ] 进程持续运行（无反复重启）

### 6.2 错误日志

- [ ] 检查 TrayApp 日志：releases/latest/TrayApp/logs
- [ ] 检查 Admin 日志：bin/Release/net8.0/Logs
- [ ] 检查 DB 日志

### 6.3 用户反馈

- [ ] 24h 内无 P0 BUG 报告
- [ ] 72h 内无 P1 BUG 报告
- [ ] 一周内无重大问题

---

## 7. 部署清单总结（速查）

| 步骤 | 操作 | 工具 |
|------|------|------|
| 1 | 编译 0 错误 | `dotnet build` |
| 2 | 发布三个项目 | `dotnet publish` |
| 3 | **同步兄弟目录（强制）** | `sync_publish_to_trayapp.sh` |
| 4 | 验证 md5 一致 | `md5sum` |
| 5 | 打包 ZIP | `Compress-Archive` |
| 6 | 部署到生产 | 复制 ZIP |
| 7 | 启动 TrayApp | `DormManage.TrayApp.exe` |
| 8 | 7 项验证 | 进程 + 端口 + curl + 跨权限 |

---

**使用建议**：每次部署都按此清单执行，避免遗漏关键步骤！