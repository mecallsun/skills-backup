> ⚠️ **本文档已过时**（v2.13.24 文档梳理标注，2026-07-19）
> 
> 内容已被最新文档替代，建议阅读：**`mock-data.js 已废弃，参见 INDEX.md`**
> 
> 本文档原地保留仅作历史版本变更追溯参考，不再更新维护。

# 人员清单列表员工类型 FK 关联补全方案（v2.11.7.B）

> **范围**：`04-HTML原型/mock-data.js` PERSONNEL 数组 + `personnel/list.html` 渲染层
> **日期**：2026-07-12
> **版本**：v2.11.7.B（v2.11.7.BUGFIX 子版本，专注于人员清单的 FK 关联引用展示增强）
> **状态**：⚠️ **已修正为 v2.11.7.CORRECT** — 扩展的 5 种员工类型（顾问/技师/保安/司机/保洁）已回退，EMPLOYEE_TYPES 字典恢复至 5 种基础类型
> **关联文档**：
> - `29-住宿账单详细弹窗-v2.11.4.md` §2.4.2（FK 关联引用规则源头）
> - `33-基础资料模块-v2.11.4.md` §10（基础资料 BUGFIX 主章节）+ §11（v2.11.7.CORRECT 员工类型 FK 关联修正）
> - `15-考勤班次需求-v2.11.2.md` §9（FK 字段定义修正）
> - `16-人员清单筛选条件-v2.11.2.md` §5.5（人员清单筛选修正）

---

## 0. v2.11.7.CORRECT 修正说明（新增）

> **⚠️ 本节原方案将 EMPLOYEE_TYPES 从 5 种扩展至 10 种，但该扩展与基础资料-员工类型列表不一致。**
>
> **修正**：基础资料-员工类型表（EmployeeType）仅定义了 5 种类型。`34c` 文档中扩展的顾问/技师/保安/司机/保洁 5 种类型已回退。
>
> **影响**：
> - `mock-data.js` 中 `EMPLOYEE_TYPES` 字典：10 种 → **5 种**
> - `EMPLOYEE_TYPE_BADGE` 颜色映射：10 种 → **5 种**
> - `normalizeData()` IIFE：新增第 2b 步校正 197 条无效 FK（ID>5 → 映射至 1-5）
> - 人员清单列表：Badge 渲染仅支持 5 种类型

---

---

## 1. 需求背景

### 1.1 期望效果

> 人员清单列表中的"员工类型"列应按照基础资料 (`EmployeeType`) 的对应分类值**主键 ID 关联补全数据**，用于原型展示效果。

具体表现：
- 650 条 `PERSONNEL` 记录中，619 条仅有 `employeeType: "合同工"`（字符串），31 条有 `employeeTypeId: 1`（FK）
- 期望：所有记录统一使用 `employeeTypeId`（FK）关联 `EmployeeType` 表，并按 Code 渲染 Badge（合同工-灰、临时工-橙、外包-青、实习生-绿、驻场-紫、顾问-蓝、技师-灰、保安-红、司机-橙、保洁-浅灰）
- 列表筛选下拉 `value=EmployeeTypeId`（int），`text=Name`
- 列表筛选按 FK 主键 ID 比较

### 1.2 与 v2.11.7.BUGFIX 关系

v2.11.7.BUGFIX 已通过 `normalizeData()` IIFE 把 619 条记录的 `employeeType: "合同工"` 自动补全为 `employeeTypeId: 1` + `employeeTypeName: "合同工"`。本节（v2.11.7.B）在此基础上：

1. **扩展字典**：从 5 种扩展到 10 种员工类型（增加顾问/技师/保安/司机/保洁）
2. **新增渲染助手**：`employeeTypeBadge(emp)` 按 FK 渲染 Badge
3. **更新页面**：人员清单列表 + 筛选下拉 value 全面 FK 化
4. **增强 Badge 颜色**：从原 5 种扩展到 10 种 Badge 颜色

---

## 2. 数据补全方案

### 2.1 EMPLOYEE_TYPES 字典扩展（v2.11.7.CORRECT 已回退至 5 种）

> ⚠️ **本节原方案将 EMPLOYEE_TYPES 从 5 种扩展至 10 种，已被 v2.11.7.CORRECT 修正回退。**

```javascript
// 原 v2.11.4 字典（5 种）← ✅ 此为最终正确版本
const EMPLOYEE_TYPES = [
    { id: 1, code: "CONTRACT",   name: "合同工",     remark: "签订正式劳动合同" },
    { id: 2, code: "TEMPORARY",  name: "临时工",     remark: "短期临时用工" },
    { id: 3, code: "OUTSOURCE",  name: "外包",       remark: "第三方派遣人员" },
    { id: 4, code: "INTERN",     name: "实习生",     remark: "在校实习学生" },
    { id: 5, code: "ONSITE",     name: "驻场",       remark: "客户现场服务人员" }
];
```

> ~~扩展至 10 种（顾问/技师/保安/司机/保洁）~~ → **v2.11.7.CORRECT 已撤销此扩展**
```

### 2.2 EMPLOYEE_TYPE_BADGE 颜色映射（v2.11.7.CORRECT 回退至 5 种）

```javascript
const EMPLOYEE_TYPE_BADGE = {
    'CONTRACT':   'bg-secondary',                          // 合同工-灰
    'TEMPORARY':  'bg-warning text-dark',                  // 临时工-橙
    'OUTSOURCE':  'bg-info text-dark',                     // 外包-青
    'INTERN':     'bg-success',                            // 实习生-绿
    'ONSITE':     'bg-dark',                               // 驻场-紫
    'OTHER':      'bg-light text-dark border'              // 其他-浅灰（兜底）
};
```

> ~~顾问/技师/保安/司机/保洁 的 5 种颜色映射~~ → **v2.11.7.CORRECT 已撤销**
```

### 2.3 employeeTypeBadge 渲染助手

```javascript
/**
 * 员工类型 Badge 渲染（v2.11.7.B 新增）
 * @param {Object} emp - 人员对象（PERSONNEL 数组中的一条记录）
 * @returns {string} HTML 字符串（包含 Bootstrap Badge）
 */
function employeeTypeBadge(emp) {
    if (!emp) return '<span class="badge bg-light text-muted">-</span>';
    
    // 1. 优先 FK 关联引用（新格式，规范化后的数据）
    var code = null;
    var name = null;
    var empTypeId = (emp.employeeTypeId !== undefined && emp.employeeTypeId !== null) ? emp.employeeTypeId : null;
    
    if (empTypeId !== null && typeof EMPLOYEE_TYPES !== 'undefined') {
        var t = EMPLOYEE_TYPES.find(function(x) { return x.id === empTypeId; });
        if (t) {
            code = t.code;
            name = t.name;
        }
    }
    
    // 2. 回退兼容（旧格式字段）
    if (!code && emp.employeeType) {
        var t2 = EMPLOYEE_TYPES.find(function(x) {
            return x.code === emp.employeeType || x.name === emp.employeeType;
        });
        if (t2) code = t2.code;
        if (!name) name = t2 ? t2.name : emp.employeeType;
    }
    
    if (!code) code = 'OTHER';
    if (!name) name = emp.employeeTypeName || emp.employeeType || '-';
    
    var cls = EMPLOYEE_TYPE_BADGE[code] || 'bg-light text-dark border';
    return '<span class="badge ' + cls + '" title="FK: EmployeeType.Id = ' + (empTypeId || '-') + '">' + name + '</span>';
}

/**
 * 员工类型中文名解析（v2.11.7.B 新增）
 * @param {Object} emp - 人员对象
 * @returns {string} Name 字符串（找不到返回 "-"）
 */
function employeeTypeName(emp) {
    if (!emp) return '-';
    if (emp.employeeTypeId != null && typeof EMPLOYEE_TYPES !== 'undefined') {
        var t = EMPLOYEE_TYPES.find(function(x) { return x.id === emp.employeeTypeId; });
        if (t) return t.name;
    }
    return emp.employeeTypeName || emp.employeeType || '-';
}
```

### 2.4 数据补全覆盖矩阵

| PERSONNEL 字段 | 旧格式样本 | 规范化后（FK） | 字典来源 |
|----------------|-----------|--------------|---------|
| `employeeType: "合同工"` | string | `employeeTypeId: 1` + `employeeTypeName: "合同工"` | EMPLOYEE_TYPES[0] |
| `employeeType: "临时工"` | string | `employeeTypeId: 2` + `employeeTypeName: "临时工"` | EMPLOYEE_TYPES[1] |
| `employeeType: "外包"` | string | `employeeTypeId: 3` + `employeeTypeName: "外包"` | EMPLOYEE_TYPES[2] |
| `employeeType: "实习生"` | string | `employeeTypeId: 4` + `employeeTypeName: "实习生"` | EMPLOYEE_TYPES[3] |
| `employeeType: "驻场"` | string | `employeeTypeId: 5` + `employeeTypeName: "驻场"` | EMPLOYEE_TYPES[4] |
| 字典未覆盖的新值 | string | 显示 fallback `-` | — |

**补全率**：650 条 PERSONNEL 中 619 条可补全（约 95%），剩余 31 条原本已具备 FK。

---

## 3. 页面渲染层更新

### 3.1 personnel/list.html 列表表格

**修改前**（v2.11.6 之前）：

```html
<td><span class="badge bg-secondary">合同工</span></td>
```

员工类型是普通 Badge，没有 FK 关联，且全部用 `bg-secondary` 一种颜色。

**修改后**（v2.11.7.B）：

```javascript
// 渲染时调用 employeeTypeBadge(p) 函数
'<td>' + employeeTypeBadge(p) + '</td>'
// 输出：
// <td><span class="badge bg-secondary" title="FK: EmployeeType.Id = 1">合同工</span></td>
// 或 <td><span class="badge bg-warning text-dark" title="FK: EmployeeType.Id = 2">临时工</span></td>
```

### 3.2 筛选下拉 value（强制 ID）

**修改前**：

```html
<select id="employeeType">
    <option value="">全部</option>
    <option value="合同工">合同工</option>   <!-- value=字符串 -->
    <option value="临时工">临时工</option>
</select>
```

**修改后**（通过 initSelects 自动处理）：

```javascript
function initSelects() {
    EMPLOYEE_TYPES.forEach(function(t) {
        typeSel.add(new Option(t.name, t.id));  // value=ID, text=Name
    });
}
```

输出：
```html
<select id="employeeType">
    <option value="">全部</option>
    <option value="1">合同工</option>      <!-- value=1 (FK) -->
    <option value="2">临时工</option>
    <option value="3">外包</option>
    ...
</select>
```

### 3.3 筛选逻辑

**修改后**：

```javascript
function getFiltered() {
    var typeId = document.getElementById('employeeType').value;  // "1"、"2" 等字符串
    return PERSONNEL.filter(function(p) {
        // 优先按 FK 比较（新格式）
        var tid = (p.employeeTypeId !== undefined && p.employeeTypeId !== null) 
                  ? String(p.employeeTypeId) : '';
        if (typeId && tid !== String(typeId)) return false;
        return true;
    });
}
```

---

## 4. 验证测试清单

### 4.1 数据补全验证

打开 `mock-data.js` 在浏览器控制台验证：

```javascript
// 619 条记录补全后
console.log(PERSONNEL.filter(p => !p.employeeTypeId).length); 
// → 0（应有 0 条缺失 employeeTypeId）

// 抽样 EMP-2026-228
const p = PERSONNEL.find(p => p.employeeCode === 'EMP-2026-228');
console.log({
    id: p.id,
    employeeType: p.employeeType,        // "MIDDLE"（旧）
    employeeTypeId: p.employeeTypeId,    // 3（新，补全后）
    employeeTypeName: p.employeeTypeName // "中班"（新，补全后）
});
```

### 4.2 页面渲染验证

打开 `personnel/list.html`：
- ✅ 列表"员工类型"列显示彩色 Badge（不同类型不同颜色）
- ✅ 鼠标悬停显示 `title` 提示：`FK: EmployeeType.Id = 1`
- ✅ 部门下拉选项 value 为 ID（如 `value="1"`）
- ✅ 选择"合同工"下拉，列表只显示员工类型为合同工的员工
- ✅ 切换员工类型筛选，按 FK ID 匹配

### 4.3 兼容性验证

模拟"如果 FK 字段缺失"的情况：

```javascript
// 临时移除 FK 字段
const p = PERSONNEL.find(p => p.employeeCode === 'EMP-2026-228');
delete p.employeeTypeId;
delete p.employeeTypeName;

employeeTypeBadge(p);
// 输出：'<span class="badge bg-warning text-dark" title="FK: EmployeeType.Id = -">中班</span>'
// ↑ 通过 p.employeeType 字符串回退仍能正确显示
```

---

## 5. 影响范围

| 文件 | 变更 |
|------|------|
| `04-HTML原型/mock-data.js` | ✏️ **EMPLOYEE_TYPES 字典扩展 5 → 10 种 + 备注**；✏️ 新增 `employeeTypeBadge` / `employeeTypeName` 渲染助手；✏️ 新增 `EMPLOYEE_TYPE_BADGE` 颜色映射 |
| `04-HTML原型/personnel/list.html` | ✏️ `initSelects` 改为 value=ID；✏️ `getFiltered` 改为按 FK ID 比较；✏️ `renderTable` 第 6 列（员工类型）改为 `employeeTypeBadge(p)` 渲染 |
| `DormManage.Shared/Services/PersonnelService.cs`（开发阶段） | 📝 `GetPersonnelListAsync` 关联 Include `EmployeeType` 返回 employeeTypeId + employeeTypeName |
| `DormManage.Admin/Pages/Personnel/Index.cshtml`（开发阶段） | 📝 列表第 6 列用 Badge 渲染，引用 Badge Class 对应 EmployeeType.Code |

---

## 6. 提交记录

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v2.11.4 (u) | 2026-07-11 | 基础资料模块初始版本（5 种员工类型） |
| v2.11.7.BUGFIX | 2026-07-12 | 通过 normalizeData() IIFE 补全 619 → 650 条 FK 字段 |
| v2.11.7.B | 2026-07-12 | ~~扩展 EMPLOYEE_TYPES 字典 5→10 种；新增 Badge 渲染助手~~ |
| **v2.11.7.CORRECT** | **2026-07-12** | **撤销 v2.11.7.B 的扩展，EMPLOYEE_TYPES 回退至 5 种；normalizeData() 新增第 2b 步校正** |

---

> **变更路径**：v2.11.4 → **v2.11.7.BUGFIX（数据规范化 IIFE）** → **v2.11.7.B（字典扩展 + Badge 渲染助手 + 页面 FK 化）**
