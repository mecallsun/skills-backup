# 测试清单（v2.13.193 综合）

> Skill: dorm-bugfix-master
> 创建日期：2026-07-28

---

## 测试总览

| 类别 | 必测 | 文档 |
|------|------|------|
| 编译测试 | 0 错误 0 新增警告 | - |
| 隐私权限测试 | 跨 2 角色 | 案例 1, 5 |
| 账号有效期测试 | 跨日期边界 | 案例 6, 7 |
| 发布同步测试 | 兄弟目录一致 | 案例 4, 11 |
| 业务规则测试 | 真实数据验证 | 案例 8 |

---

## 1. 编译测试

```bash
# 编译整个解决方案
dotnet build DormManage.sln -c Release

# 期望：0 错误
# 警告：与 v2.13.187 基线持平
```

**禁止**：忽略新增警告（可能是新 BUG 信号）

---

## 2. 隐私权限测试（必测 2 角色）

### 2.1 admin 测试

```bash
# 1. Login as admin
curl -s -c cookies.txt -o login.html http://localhost:5001/Account/Login
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
curl -s -b cookies.txt -c cookies.txt -L \
    --data-urlencode "UserName=admin" \
    --data-urlencode "Password=admin123" \
    --data-urlencode "RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$TOKEN" \
    -o /dev/null http://localhost:5001/Account/Login

# 2. Get /Dorms
curl -s -b cookies.txt -o dorms_admin.html http://localhost:5001/Dorms

# 3. 验证：d-none 数量 = 0（admin 看到所有字段）
COUNT=$(grep -o 'd-none' dorms_admin.html | wc -l)
[ "$COUNT" -eq 0 ] && echo "✓ admin sees all" || echo "✗ admin missing d-none count: $COUNT"
```

### 2.2 未授权角色测试（关键！）

```bash
# 1. Login as test (unauthorized role)
curl -s -c cookies_test.txt -o login_t.html http://localhost:5001/Account/Login
TOKEN2=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login_t.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
curl -s -b cookies_test.txt -c cookies_test.txt -L \
    --data-urlencode "UserName=test" \
    --data-urlencode "Password=123456" \
    --data-urlencode "RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$TOKEN2" \
    -o /dev/null http://localhost:5001/Account/Login

# 2. Get /Dorms
curl -s -b cookies_test.txt -o dorms_test.html http://localhost:5001/Dorms

# 3. 验证：d-none 数量 > 0（test 看不到隐私字段）
COUNT=$(grep -o 'd-none' dorms_test.html | wc -l)
[ "$COUNT" -gt 0 ] && echo "✓ test sees privacy fields hidden" || echo "✗ BUG: test should see d-none but saw 0"
```

### 2.3 预期结果

| 用户 | 角色 | 隐私权限 | 期望 |
|------|------|---------|------|
| admin | 系统管理员 | ✅ 勾选 | 显示所有字段 |
| test | 行政助理 | ❌ 未勾选 | 隐藏所有隐私字段 |

---

## 3. 账号有效期测试

### 3.1 过期当天测试（关键！）

```sql
-- 设置 test 用户 ExpiresAt = 今天
UPDATE SysUser SET ExpiresAt = DATEADD(DAY, 0, CAST(GETDATE() AS DATE))
WHERE UserName = 'test'
```

```bash
# 1. Login as test
curl -s -c cookies_test.txt -o login_t.html http://localhost:5001/Account/Login
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login_t.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')

# 2. Try login
HTTP=$(curl -s -b cookies_test.txt -c cookies_test.txt \
    --data-urlencode "UserName=test" \
    --data-urlencode "Password=123456" \
    --data-urlencode "RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$TOKEN" \
    -o /dev/null -w "%{http_code}" \
    http://localhost:5001/Account/Login)

# 3. 期望：HTTP 200 但显示"账号已过期"（不是 302 重定向）
# 如果返回 302 → BUG：v2.13.193 之前的 `>` 严格大于
```

### 3.2 已过期测试

```sql
UPDATE SysUser SET ExpiresAt = DATEADD(DAY, -1, CAST(GETDATE() AS DATE))
WHERE UserName = 'test'
```

期望：登录失败，显示"账号已过期"

### 3.3 未过期测试

```sql
UPDATE SysUser SET ExpiresAt = DATEADD(DAY, 1, CAST(GETDATE() AS DATE))
WHERE UserName = 'test'
```

期望：登录成功

### 3.4 永久有效测试

```sql
UPDATE SysUser SET ExpiresAt = NULL
WHERE UserName = 'test'
```

期望：登录成功（NULL = 永久有效）

---

## 4. 发布同步测试

### 4.1 DLL 时间戳测试

```bash
# 期望：所有 4 个 DLL 时间戳相同（最新）
stat -c '%y  %n' \
    release/latest/Admin/DormManage.Admin.dll \
    release/latest/TrayApp/Admin/DormManage.Admin.dll \
    release/latest/Api/DormManage.Api.dll \
    release/latest/TrayApp/Api/DormManage.Api.dll
```

### 4.2 Views 同步测试

```bash
# 期望：两个目录的 .cshtml 内容相同
diff release/latest/Admin/Pages/Dorms/Index.cshtml \
     release/latest/TrayApp/Admin/Pages/Dorms/Index.cshtml
# 应该无输出

diff release/latest/Admin/Pages/Dorms/Details.cshtml \
     release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
# 应该无输出
```

### 4.3 Shared DLL 测试

```bash
# 检查 Shared DLL 包含 RuntimeWindowGuard
grep -ao "RuntimeWindowGuard" \
    release/latest/TrayApp/Admin/DormManage.Shared.dll | head -1
```

### 4.4 进程加载测试

```bash
# 启动 TrayApp
release/latest/TrayApp/DormManage.TrayApp.exe &

# 等待启动
sleep 10

# 检查 Admin 进程加载的 DLL 路径
powershell -Command "Get-Process -Name DormManage.Admin | ForEach-Object { $_.Modules | Where-Object { $_.FileName -like '*Admin*' } | Select-Object FileName, FileVersion }"
```

期望：路径是 `release/latest/TrayApp/Admin/DormManage.Admin.dll`

---

## 5. 业务规则测试

### 5.1 密码重置测试

```bash
# 1. Login as admin
curl -s -c cookies.txt -o login.html http://localhost:5001/Account/Login
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
curl -s -b cookies.txt -c cookies.txt -L \
    --data-urlencode "UserName=admin" \
    --data-urlencode "Password=admin123" \
    --data-urlencode "RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$TOKEN" \
    -o /dev/null http://localhost:5001/Account/Login

# 2. Get fresh token
curl -s -b cookies.txt -c cookies.txt -o settings.html http://localhost:5001/Settings?tab=users
TOKEN2=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' settings.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')

# 3. Reset test user password
curl -s -b cookies.txt -c cookies.txt \
    --data-urlencode "Id=15" \
    --data-urlencode "NewPassword=NewPass123" \
    --data-urlencode "__RequestVerificationToken=$TOKEN2" \
    -o reset.txt \
    "http://localhost:5001/Settings?handler=UserResetPassword"
cat reset.txt
# 期望：{"success":true,"message":"用户 test 密码已重置"}

# 4. 跨连接查询验证 hash 真的变了
cd tmp/DiagPassword
cat > Program.cs << 'EOF'
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using BCrypt.Net;

class Program {
    static void Main() {
        var opts = new DbContextOptionsBuilder<DormDbContext>();
        opts.UseSqlServer("Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;");
        using var db = new DormDbContext(opts.Options);
        var u = db.SysUsers.FirstOrDefault(x => x.UserName == "test");
        Console.WriteLine($"Hash: {u.PasswordHash}");
        Console.WriteLine($"Verify('NewPass123'): {BCrypt.Net.BCrypt.Verify("NewPass123", u.PasswordHash)}");
    }
}
EOF
dotnet run -c Release
# 期望：Verify('NewPass123'): True
```

### 5.2 有效期编辑测试

```bash
# 1. Login as admin
# 2. Get fresh token
# 3. Update test user with new ExpiresAt
curl -s -b cookies.txt -c cookies.txt \
    --data-urlencode "Id=15" \
    --data-urlencode "DisplayName=测试" \
    --data-urlencode "Email=" \
    --data-urlencode "Phone=" \
    --data-urlencode "IsActive=true" \
    --data-urlencode "SelectedRoleIds=4" \
    --data-urlencode "ExpiresAt=2026-12-31" \
    --data-urlencode "__RequestVerificationToken=$TOKEN2" \
    -o update.txt \
    "http://localhost:5001/Settings?handler=UserUpdate"
# 期望：{"success":true,"message":"用户 test 更新成功"}

# 4. 验证 DB
cd tmp/DiagPassword
# 改 Program.cs 查询 ExpiresAt
dotnet run -c Release
# 期望：ExpiresAt: 2026-12-31 23:59:59
```

---

## 6. UI 渲染测试

### 6.1 性别列测试

```bash
# 验证 /Dorms/Details?id=X HTML 含 bi-gender-male/female
curl -s -b cookies.txt -o details.html "http://localhost:5001/Dorms/Details?id=3"
grep -c "bi-gender-male\|bi-gender-female" details.html
# 期望：>= 1
```

### 6.2 床位简化测试

```bash
# 验证 HTML 不含 "床位 N" 模式
grep "床位 [0-9]" details.html
# 期望：空输出
```

### 6.3 字段截断测试

```bash
# 验证长字段有 text-truncate 类
grep -c "text-truncate" details.html
# 期望：>= 4（每个超长字段列各 1 个）
```

### 6.4 操作按钮文字化测试

```bash
# 验证操作按钮是 .op-btn 而不是 .btn-outline-*
grep -c "op-btn" details.html
# 期望：>= 1
grep -c "btn-outline-primary" details.html
# 期望：0
```

---

## 7. 部署验证

### 7.1 ZIP 包验证

```bash
ls -la release/_archive/DormManage-v*.zip
unzip -l release/_archive/DormManage-v*.zip | tail -5
# 期望：~3000+ 文件
```

### 7.2 md5 一致性

```bash
md5sum release/latest/Admin/DormManage.Admin.dll \
       release/latest/TrayApp/Admin/DormManage.Admin.dll
# 期望：相同
```

### 7.3 .cshtml 一致性

```bash
md5sum release/latest/Admin/Pages/Dorms/Details.cshtml \
       release/latest/TrayApp/Admin/Pages/Dorms/Details.cshtml
# 期望：相同
```

---

## 8. 自动化测试（CI/CD）

```bash
#!/bin/bash
# ci_test.sh - 自动化测试脚本

set -e
echo "=== 编译测试 ==="
dotnet build DormManage.sln -c Release || exit 1

echo "=== 发布测试 ==="
bash scripts/sync_publish_to_trayapp.sh || exit 1

echo "=== DLL 时间戳 ==="
LATEST_TS=$(stat -c '%Y' release/latest/Admin/DormManage.Admin.dll)
TRAYAPP_TS=$(stat -c '%Y' release/latest/TrayApp/Admin/DormManage.Admin.dll)
[ "$LATEST_TS" -eq "$TRAYAPP_TS" ] || { echo "DLL 时间戳不一致"; exit 1; }

echo "=== DLL 内容 ==="
GENDER_COUNT=$(grep -ao "性别" release/latest/TrayApp/Admin/DormManage.Admin.dll | wc -l)
[ "$GENDER_COUNT" -gt 0 ] || { echo "Gender 不在 DLL 中"; exit 1; }

echo "=== md5 一致性 ==="
ADMIN_MD5=$(md5sum release/latest/Admin/DormManage.Admin.dll | cut -d' ' -f1)
TRAYAPP_MD5=$(md5sum release/latest/TrayApp/Admin/DormManage.Admin.dll | cut -d' ' -f1)
[ "$ADMIN_MD5" == "$TRAYAPP_MD5" ] || { echo "md5 不一致"; exit 1; }

echo "=== 所有测试通过！ ==="
```

---

## 9. 测试结果判定

| 测试 | 通过 | 失败 |
|------|------|------|
| 编译 0 错误 | ✓ | ✗ 修复编译错误 |
| 跨权限测试 admin OK | ✓ | ✗ 检查 Dll/Permission 同步 |
| 跨权限测试 test 隐藏 | ✓ | ✗ 修复 deny-by-default 翻转 |
| 过期当天拒绝 | ✓ | ✗ 修复 `>` → `>=` |
| DLL 时间戳一致 | ✓ | ✗ 重新运行 sync 脚本 |
| md5 一致 | ✓ | ✗ 重新发布 |
| .cshtml 一致 | ✓ | ✗ cp Pages 到兄弟目录 |

**任何一项失败 = 发布不通过！**

---

**使用建议**：每次发布前必须执行所有测试，自动化脚本可集成到 CI/CD！