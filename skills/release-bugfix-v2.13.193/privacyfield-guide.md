# 隐私字段 deny-by-default 实施指南

> Skill: release-bugfix-v2.13.193  
> 适用项目：金戈宿舍管理系统（DormManage）  
> 创建日期：2026-07-27

---

## 核心原则：deny-by-default

> 默认所有角色均看不到隐私字段清单中的字段。
> 只有显式勾选「允许显示隐私字段」后才能查看。

---

## 1. 核心实现（v2.13.193 已修复）

### 1.1 接口方法

**文件**：`DormManage.Shared/Services/PermissionService.cs`

```csharp
public interface IPermissionService
{
    /// <summary>是否允许显示隐私字段</summary>
    Task<bool> AllowDisplayPrivacyFieldsAsync(int userId);
    
    /// <summary>获取应该隐藏的字段集合</summary>
    Task<HashSet<string>> GetHiddenFieldKeysAsync(int userId);
}
```

### 1.2 实现（语义核心）

```csharp
public const string PrivacyFieldPermissionCode = "privacy:field:enable";

public async Task<bool> AllowDisplayPrivacyFieldsAsync(int userId)
{
    if (userId <= 0) return false;
    var codes = await GetUserPermissionCodesAsync(userId);
    return codes.Contains(PrivacyFieldPermissionCode);
}

public async Task<HashSet<string>> GetHiddenFieldKeysAsync(int userId)
{
    // v2.13.176 deny-by-default：不勾选 → 隐藏所有 IsActive 字段
    if (!await AllowDisplayPrivacyFieldsAsync(userId))
    {
        var allActiveKeys = await _db.SysFieldPermissions
            .Where(p => p.IsActive)
            .Select(p => p.FieldKey)
            .ToListAsync();
        return new HashSet<string>(allActiveKeys);
    }
    
    // 勾选了「允许显示隐私字段」 → 不隐藏任何字段
    return new HashSet<string>();
}
```

**关键理解**：
- 旧（v2.13.92）`：if (!HasPrivacyFieldEnabled) return new HashSet();` ← 不勾选 = 不隐藏 ❌ 反向
- 新（v2.13.193）：`if (!AllowDisplayPrivacyFields) return 全部字段;` ← 不勾选 = 隐藏 ✅ 正确

---

## 2. UI 接线（必备 3 件套）

### 2.1 列表 thead 接线

```html
<th class="text-truncate @(await Html.IsFieldHiddenAsync("dorm.capacity") ? "d-none" : "")">
    容量
</th>
```

### 2.2 列表 tbody 接线

```html
<td class="text-center @(await Html.IsFieldHiddenAsync("dorm.capacity") ? "d-none" : "")">
    @if (await Html.IsFieldHiddenAsync("dorm.capacity"))
    {
        <span>***</span>
    }
    else
    {
        @item.Capacity
    }
</td>
```

### 2.3 详情页单点接线

```html
<td class="@(await Html.IsFieldHiddenAsync("dorm.capacity") ? "d-none" : "")">
    @Model.Dorm.Capacity
</td>
```

### 2.4 字段 key 列表（v2.13.180 共 21 个）

| FieldKey | Module |
|----------|--------|
| `employee.realname` | Personnel |
| `employee.phone` | Personnel |
| `employee.employeecode` | Personnel |
| `employee.dormcode` | Personnel |
| `employee.remark` | Personnel |
| `employee.idnumber` | Personnel |
| `employee.hiredate` | Personnel |
| `employee.leavedate` | Personnel |
| `employee.employeetype` | Personnel |
| `booking.realname` | Booking |
| `booking.employeecode` | Booking |
| `booking.dormcode` | Booking |
| `booking.department` | Booking |
| `booking.operator` | Booking |
| `dorm.address` | Dorms |
| **`dorm.capacity`** | **Dorms** |
| **`dorm.currentcount`** | **Dorms** |
| `meter.operator` | Meter |
| `billing.realname` | EmployeeBilling |
| `billing.employeecode` | EmployeeBilling |
| `billing.dormcode` | EmployeeBilling |

---

## 3. 跨权限测试（v2.13.193 强制要求）

### 3.1 admin 测试

- admin 默认拥有 `privacy:field:enable` 权限
- 所有字段可见

### 3.2 未授权角色测试（关键！）

- 创建角色 admin_role_xxx（如行政助理）
- **不勾选** `privacy:field:enable` 权限
- 创建用户 test，分配该角色
- 登录 test，访问 /Dorms
- **预期**：容量、住人数列不显示（d-none）
- **如果看到了**：BUG，存在 deny-by-default 失效

### 3.3 curl 测试命令

```bash
# Login as test user (无隐私权限)
curl -s -c cookies.txt -o login.html http://localhost:5001/Account/Login
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
curl -s -b cookies.txt -c cookies.txt -L \
    --data-urlencode "UserName=test" \
    --data-urlencode "Password=test" \
    --data-urlencode "RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$TOKEN" \
    -o /dev/null http://localhost:5001/Account/Login?handler=Login

# Get /Dorms
curl -s -b cookies.txt -o dorms.html http://localhost:5001/Dorms

# Verify 隐私字段 should be hidden
grep -c "d-none" dorms.html
# Should be >= 4 (2 hidden headers + 2 hidden cells per row)
```

---

## 4. 常见错误与修复

### 错误 1：admin 用户能正常看到，但未授权用户仍能看到

**诊断**：`IsFieldHiddenAsync()` 在 `FieldPermissionHtmlHelperExtensions.cs` 中：
```csharp
if (perm == null) return false;  // ← ❌ 如果 perm 为 null，永远不隐藏
```

**修复**：确保 DI 注册了 IPermissionService：
```csharp
builder.Services.AddScoped<IPermissionService, PermissionService>();
```

### 错误 2：IsFieldHiddenAsync 总是返回 false

**诊断**：GetUserPermissionCodesAsync 返回 codes 不包含 PrivacyFieldPermissionCode。

**修复**：检查 DB 中：
```sql
SELECT * FROM SysPermission WHERE PermissionCode = 'privacy:field:enable';
SELECT * FROM SysRolePermission WHERE PermissionId = 39;
SELECT * FROM SysUserRole WHERE RoleId IN (...);
```

### 错误 3：所有角色都无法看到（admin 也看不到）

**诊断**：v2.13.176 翻转被错误实施为"全部隐藏"。

**修复**：检查 PermissionService.cs 中 GetHiddenFieldKeysAsync 是否正确：
```csharp
// ✓ 正确
if (!await AllowDisplayPrivacyFieldsAsync(userId))
{
    var activeKeys = await _db.SysFieldPermissions...
    return new HashSet<string>(activeKeys);
}
return new HashSet<string>();
```

---

## 5. 隐私字段 BUG 历史教训

### 5.1 v2.13.92 引入 PrivacyFieldPermissionCode

- 实现：`HasPrivacyFieldEnabledAsync` 返回 `codes.Contains(...)`
- 逻辑：「启用隐私保护」勾选 → 隐藏字段
- ⚠️ **BUG**：「不勾选」=「显示所有」（违反安全原则）

### 5.2 v2.13.176 deny-by-default 设计

- 文档：勾选 = 允许显示（反向语义）
- ❌ **BUG**：代码从未翻转，仍是 v2.13.92 实现

### 5.3 v2.13.193 实质性修复

- 方法重命名：`HasPrivacyFieldEnabledAsync` → `AllowDisplayPrivacyFieldsAsync`
- 逻辑反向：勾选 = 允许显示（正确）
- 不勾选 = 隐藏（deny-by-default，✅ 正确）

---

## 6. 隐私字段 BUG 检查清单

- [ ] `IPermissionService.AllowDisplayPrivacyFieldsAsync` 接口已定义
- [ ] `AllowDisplayPrivacyFieldsAsync` 实现：`return codes.Contains(PrivacyFieldPermissionCode);`
- [ ] `GetHiddenFieldKeysAsync` 实现：未勾选 → 返回所有 IsActive 字段
- [ ] `GetHiddenFieldKeysAsync` 实现：勾选 → 返回空 HashSet
- [ ] `Html.IsFieldHiddenAsync()` 在表格表头和单元格都已调用
- [ ] 跨权限测试：admin + 未授权角色都用过
- [ ] 验证 test 用户的 HTML 中含 `d-none` 类
- [ ] 验证 test 用户的 HTML 中 **不** 含实际数据值

---

## 7. 相关文档

- 实现：`PermissionService.cs` v2.13.193 已修复
- 文档：`00-方案文档/231-隐私字段语义翻转终极修复-v2.13.193.md`
- 历史：`00-方案文档/216-隐私字段保护语义翻转v2.13.176.md`
- 扩展器：`FieldPermissionHtmlHelperExtensions.cs`

---

**使用此 Skill 触发条件**：
- 用户报告「隐私字段不生效」「字段还显示」「权限没阻止」
- 准备为新页面添加隐私字段支持
- 跨权限测试发现 BUG