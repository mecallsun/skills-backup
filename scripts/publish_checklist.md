# 发布检查清单（v2.13.193）

## 📋 发布前

- [ ] 源代码修改已完成并通过本地测试
- [ ] `dotnet build DormManage.sln -c Release` 通过 0 error

## 📋 发布中（使用 `scripts/sync_publish_to_trayapp.sh`）

- [ ] 所有项目 publish 到 `release/latest/{Admin,Api,TrayApp}/`
- [ ] DLL 时间戳与 publish 时间一致
- [ ] **TrayApp 兄弟目录**（`release/latest/TrayApp/Admin/` 和 `release/latest/TrayApp/Api/`）已同步
- [ ] ZIP 包生成到 `release/_archive/`

## 📋 发布后验证（关键步骤）

### DLL 同步验证

```bash
# Admin DLL 时间戳（应该是刚刚 publish 的时间）
stat -c '%y  %n' release/latest/TrayApp/Admin/DormManage.Admin.dll

# Shared DLL 时间戳（应该同步到 TrayApp 子目录）
stat -c '%y  %n' release/latest/TrayApp/Admin/DormManage.Shared.dll
stat -c '%y  %n' release/latest/TrayApp/Api/DormManage.Shared.dll
```

### 视图同步验证

```bash
# 检查 Details.cshtml 包含最新修改
grep -c "resident.Gender" release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
# 应该 >= 2（表头 + 数据行）

# 检查「床位」字面量已移除（仅注释中可能存在）
grep -c "床位" release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
# 应该 <= 1（仅注释中）
```

### DLL 内容验证

```bash
# 检查 DLL 中含 UTF-16 LE 编码的"性别"字符串
grep -ao "性别" release/latest/TrayApp/Admin/DormManage.Admin.dll | wc -l
# 应该 >= 5

# 检查 DLL 中含"bi-gender-male"图标
grep -ao "bi-gender-male" release/latest/TrayApp/Admin/DormManage.Admin.dll | wc -l
# 应该 >= 1

# 检查 Shared DLL 含 RuntimeWindowGuard（v2.13.135 暗桩相关）
grep -ao "RuntimeWindowGuard" release/latest/TrayApp/Admin/DormManage.Shared.dll | wc -l
# 应该 >= 1
```

### ZIP 包验证

```bash
ls -la release/_archive/*.zip
# 最新 ZIP 应该约 120MB
```

## 📋 部署验证（运行时验证）

### 步骤 1：停止所有 DormManage 进程

```bash
taskkill /F /IM DormManage.TrayApp.exe
taskkill /F /IM DormManage.Admin.exe
taskkill /F /IM DormManage.Api.exe
```

### 步骤 2：启动 TrayApp

双击 `release/latest/TrayApp/DormManage.TrayApp.exe`

### 步骤 3：检查 TrayApp 日志

```
[LICENSE] 注册有效：LTD=xxx，有效期至 yyyy-MM-dd
Api 进程已启动 PID=xxx
Admin 进程已启动 PID=xxx
Api 健康检查通过 HTTP 200
Admin 健康检查通过 HTTP 200
```

### 步骤 4：HTTP 验证（curl）

```bash
# 登录
curl -c cookies.txt -o login.html http://localhost:5001/Account/Login
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
curl -b cookies.txt -c cookies.txt -L -o /dev/null -w "%{http_code}" \
    --data-urlencode "UserName=admin" \
    --data-urlencode "Password=admin123" \
    --data-urlencode "RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$TOKEN" \
    http://localhost:5001/Account/Login?handler=Login

# 获取详情页
curl -b cookies.txt -o details.html http://localhost:5001/Dorms/Details?id=3

# 验证内容
grep -c "bi-gender-male" details.html    # 应 >= 1
grep -c "bi-gender-female" details.html  # 应 >= 1
grep -o "床位 [0-9]" details.html        # 应为空（不应该有「床位 N」）
```

### 步骤 5：浏览器验证（最终）

访问 `http://localhost:5001/Dorms/Details?id=3`：

- ✅ 性别列应显示「♂ 男」「♀ 女」Badge
- ✅ 床号列应只显示数字（如 `1`、`2`），不显示「床位 N」
- ✅ **务必按 Ctrl+F5 强制刷新**（如果浏览器缓存）

---

## ⚠️ 常见错误排查

| 错误 | 原因 | 解决 |
|------|------|------|
| TypeLoadException: RuntimeWindowGuard | Shared DLL 版本不一致 | 重新 sync Shared DLL |
| HTTP 500: Could not load type | Shared DLL 缺少类 | dotnet build 强制重新编译 |
| 浏览器仍显示旧版 | 浏览器缓存 | Ctrl+F5 强制刷新 |
| Admin 启动后立即退出 | 缺少类型 / 路径错误 | 检查 TrayApp 日志 |
| TrayApp 拉起子进程失败 | 注册码过期或 IPC 异常 | 检查注册表配置 |
| 修改无效（之前 v2.13.192 问题） | TrayApp 加载的是兄弟目录而非发布目录 | 使用 sync 脚本 |

---

## 📌 强制规则（v2.13.193）

1. **禁止**：仅用 `dotnet publish -o release/latest/Admin/`
2. **必须**：使用 `scripts/sync_publish_to_trayapp.sh`
3. **必须**：发布后运行验证脚本
4. **必须**：部署前检查所有 DLL 时间戳一致