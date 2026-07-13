# v2.11.4 (u) mock-data.js 数据转换方案

> 为保持向后兼容，mock-data.js 保留原有数据结构，通过辅助函数实现 ID→名称 映射
>
> **v2.11.7.BUGFIX 修订**：本文档描述的 `transformPersonnelForSubmit()` 等转换函数仅用于「开发期数据转换参考」。生产代码层面，v2.11.7.BUGFIX 通过 `normalizeData()` IIFE 在 mock-data.js 加载时自动补全 FK 字段，**无需手工调用转换函数**。详见 §6 修订章节。

---

## 1. 兼容方案说明

### 1.1 原数据结构（保留）

```javascript
// 部门（保留原有名称数组，用于兼容）
const DEPARTMENTS = ["生产部", "技术部", "行政部", "财务部", "销售部", "后勤部", "仓储部", "其他"];

// 员工类型（保留原有结构）
const EMPLOYEE_TYPES = [
    { code: "CONTRACT", name: "合同工" },
    ...
];

// 人员数据（保留原有字段名）
const PERSONNEL = [
    { department: "行政部", employeeType: "合同工", status: 1, attendanceType: "DEFAULT" },
    ...
];
```

### 1.2 新增 ID 映射表（v2.11.4 u 新增）

```javascript
// ID ↔ 名称 映射表（新增）
const DEPT_MAP = {
    // 名称 → ID
    "生产部": 1, "技术部": 2, "行政部": 3, "财务部": 4,
    "销售部": 5, "后勤部": 6, "仓储部": 7, "其他": 8,
    // ID → 名称
    1: "生产部", 2: "技术部", 3: "行政部", 4: "财务部",
    5: "销售部", 6: "后勤部", 7: "仓储部", 8: "其他"
};

const EMP_TYPE_MAP = {
    // code → id
    "CONTRACT": 1, "TEMPORARY": 2, "OUTSOURCE": 3, "INTERN": 4, "ONSITE": 5,
    // id → code
    1: "CONTRACT", 2: "TEMPORARY", 3: "OUTSOURCE", 4: "INTERN", 5: "ONSITE"
};

const EMP_STATUS_MAP = {
    // id → name
    1: "在职", 2: "待入职", 3: "已离职",
    // name → id
    "在职": 1, "待入职": 2, "已离职": 3
};

const ATTENDANCE_MAP = {
    // code → id
    "DEFAULT": 1, "MORNING": 2, "MIDDLE": 3, "EVENING": 4, "NIGHT": 5, "OTHER": 6,
    // id → code
    1: "DEFAULT", 2: "MORNING", 3: "MIDDLE", 4: "EVENING", 5: "NIGHT", 6: "OTHER"
};
```

---

## 2. 辅助函数（页面中使用）

### 2.1 名称 → ID 转换

```javascript
// 根据部门名称获取ID
function getDeptId(deptName) {
    return DEPT_MAP[deptName] || null;
}

// 根据员工类型 code 获取ID
function getEmpTypeId(typeCode) {
    return EMP_TYPE_MAP[typeCode] || null;
}

// 根据在职状态值获取ID
function getEmpStatusId(statusValue) {
    return statusValue; // 在职状态直接使用值作为ID
}

// 根据考勤班次 code 获取ID
function getAttendanceId(attendanceCode) {
    return ATTENDANCE_MAP[attendanceCode] || null;
}
```

### 2.2 ID → 名称转换

```javascript
// 根据部门ID获取名称
function getDeptName(deptId) {
    return DEPT_MAP[deptId] || '-';
}

// 根据员工类型ID获取名称
function getEmpTypeName(typeId) {
    const type = EMPLOYEE_TYPES.find(t => t.code === EMP_TYPE_MAP[typeId]);
    return type ? type.name : '-';
}

// 根据在职状态ID获取名称
function getEmpStatusName(statusId) {
    return EMP_STATUS_MAP[statusId] || '-';
}

// 根据考勤班次ID获取名称
function getAttendanceName(attendanceId) {
    const type = ATTENDANCE_TYPES.find(t => t.code === ATTENDANCE_MAP[attendanceId]);
    return type ? type.name : '-';
}
```

### 2.3 完整数据转换

```javascript
// 将 PERSONNEL 数据转换为带 ID 的格式
function transformPersonnelForSubmit(person) {
    return {
        id: person.id,
        employeeCode: person.employeeCode,
        realName: person.realName,
        departmentId: getDeptId(person.department),
        departmentName: person.department,
        employeeTypeId: getEmpTypeId(person.employeeType),
        employeeTypeName: person.employeeType,
        employmentStatusId: person.status,
        employmentStatusName: EMP_STATUS_MAP[person.status],
        attendanceTypeId: getAttendanceId(person.attendanceType),
        attendanceTypeName: ATTENDANCE_NAME[person.attendanceType],
        phone: person.phone,
        hireDate: person.hireDate,
        leaveDate: person.leaveDate,
        dormCode: person.dormCode,
        remark: person.remark
    };
}
```

---

## 3. 前端页面使用示例

### 3.1 人员清单列表（显示名称）

```javascript
// 渲染列表（显示名称）
function renderPersonnelTable(data) {
    let html = '';
    data.forEach(p => {
        html += `<tr>
            <td>${p.employeeCode}</td>
            <td>${p.realName}</td>
            <td>${p.department}</td>           <!-- 显示名称 -->
            <td>${p.employeeType}</td>         <!-- 显示名称 -->
            <td>${EMP_STATUS_MAP[p.status]}</td>  <!-- 显示名称 -->
            <td>${p.dormCode || '-'}</td>
            <td>
                <button onclick="editPerson(${p.id})">编辑</button>
            </td>
        </tr>`;
    });
    document.getElementById('personnelTable').innerHTML = html;
}
```

### 3.2 表单下拉选项（使用 ID 作为 value）

```javascript
// 渲染部门下拉（value 是 ID）
function renderDepartmentSelect(targetId, selectedValue) {
    let html = '<option value="">全部</option>';
    DEPARTMENTS.forEach((name, idx) => {
        const id = idx + 1;  // 1-based ID
        html += `<option value="${id}" ${selectedValue === id ? 'selected' : ''}>${name}</option>`;
    });
    document.getElementById(targetId).innerHTML = html;
}

// 渲染员工类型下拉（value 是 ID）
function renderEmployeeTypeSelect(targetId, selectedValue) {
    let html = '<option value="">全部</option>';
    EMPLOYEE_TYPES.forEach((t, idx) => {
        const id = idx + 1;
        html += `<option value="${id}" ${selectedValue === id ? 'selected' : ''}>${t.name}</option>`;
    });
    document.getElementById(targetId).innerHTML = html;
}

// 渲染在职状态下拉（value 是 ID）
function renderEmploymentStatusSelect(targetId, selectedValue) {
    const statuses = [
        { id: 1, name: "在职" },
        { id: 2, name: "待入职" },
        { id: 3, name: "已离职" }
    ];
    let html = '<option value="">全部</option>';
    statuses.forEach(s => {
        html += `<option value="${s.id}" ${selectedValue === s.id ? 'selected' : ''}>${s.name}</option>`;
    });
    document.getElementById(targetId).innerHTML = html;
}
```

### 3.3 表单提交（提交 ID）

```javascript
// 提交表单（提交 ID 而非名称）
async function submitPersonForm() {
    const formData = {
        departmentId: parseInt(document.getElementById('departmentId').value),    // ✅ 提交 ID
        employeeTypeId: parseInt(document.getElementById('employeeTypeId').value),  // ✅ 提交 ID
        employmentStatusId: parseInt(document.getElementById('statusId').value),     // ✅ 提交 ID
        attendanceTypeId: parseInt(document.getElementById('attendanceTypeId').value)  // ✅ 提交 ID
    };
    
    // 调用 API...
}
```

---

## 4. 数据流向图

```
┌─────────────────────────────────────────────────────────────────┐
│                        数据存储层                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
│  │ Department │    │EmployeeType│    │EmploymentSt │         │
│  │     ID=2   │    │    ID=1     │    │    ID=1     │         │
│  └─────┬─────┘    └──────┬──────┘    └──────┬──────┘         │
│        │                 │                   │                 │
└────────┼─────────────────┼───────────────────┼─────────────────┘
         │                 │                   │
         ▼                 ▼                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                      业务表（SysEmployee）                       │
│  departmentId=2, employeeTypeId=1, employmentStatusId=1, ...  │
│  ✅ 存储的是 ID（外键），不是名称                                │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                      API 返回（JOIN 查询）                        │
│  departmentId: 2, departmentName: "技术部"                     │
│  employeeTypeId: 1, employeeTypeName: "合同工"                │
│  ✅ 同时返回 ID 和 Name                                          │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                      前端页面渲染                                 │
│  显示：技术部（名称）    操作：departmentId=2                    │
│  ✅ 下拉选项 value=ID    表格显示 Name                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. 实施检查清单

- [ ] mock-data.js 新增 DEPT_MAP、EMP_TYPE_MAP 等映射表
- [ ] 页面 JS 新增 `getDeptId()`、`getDeptName()` 等辅助函数
- [ ] 人员清单页面下拉选项 value 属性使用 ID
- [ ] 人员清单表格显示名称
- [ ] 表单提交时转换名称为 ID
- [ ] API 层正确实现 JOIN 查询

---

## 6. v2.11.7.BUGFIX 关联关系错误更正（强制补充）

> ⚠️ **重要**：本文档原稿（v2.11.4 u）的数据转换方案是**过渡期方案**，已由 v2.11.7 修订为更彻底的方案。

### 6.1 原稿方案局限性

v2.11.4 u 原稿提到：
> "为保持向后兼容，mock-data.js 保留原有数据结构，通过辅助函数实现 ID→名称 映射"

但实际**真实数据问题**比"转换函数缺失"更严重：
- PERSONNEL 数组中**619 条记录仍使用旧字符串字段**（如 `attendanceType: "MIDDLE"`），并未使用 `transformPersonnelForSubmit` 转换
- 仅 31 条记录同时具备新旧两种字段
- 原转换函数 `transformPersonnelForSubmit()` 仅在 PERSONNEL 对象读取后被显式调用时才生效，但绝大多数页面代码直接读取 `p.attendanceType`（旧字段）而非 `p.attendanceTypeName`（新字段）

**后果**：v2.11.7 引入"FK 关联引用"规则后，95% 员工记录关联失效。

### 6.2 v2.11.7 替代方案（推荐）

**不再依赖手工转换函数**，改为在 mock-data.js 加载时**自动规范化**：

```javascript
// 文件末尾追加
(function normalizeData() {
    if (typeof PERSONNEL === 'undefined') return;
    
    PERSONNEL.forEach(p => {
        // 考勤班次：旧字符串 → FK + Name
        if ((p.attendanceTypeId === undefined || p.attendanceTypeId === null) && p.attendanceType) {
            const t = ATTENDANCE_TYPES.find(x => x.code === p.attendanceType);
            if (t) { p.attendanceTypeId = t.id; p.attendanceTypeName = t.name; }
        }
        // 员工类型：旧字符串 → FK + Name
        if ((p.employeeTypeId === undefined || p.employeeTypeId === null) && p.employeeType) {
            const t = EMPLOYEE_TYPES.find(x => x.code === p.employeeType || x.name === p.employeeType);
            if (t) { p.employeeTypeId = t.id; p.employeeTypeName = t.name; }
        }
        // 部门：旧字符串 → FK
        if ((p.departmentId === undefined || p.departmentId === null) && p.department) {
            const d = DEPARTMENTS.find(x => x.name === p.department || x.code === p.department);
            if (d) p.departmentId = d.id;
        }
        // 在职状态：旧 int → FK
        if ((p.employmentStatusId === undefined || p.employmentStatusId === null) && p.status) {
            p.employmentStatusId = p.status;
            const s = EMPLOYMENT_STATUSES.find(x => x.id === p.status);
            if (s) p.employmentStatusName = s.name;
        }
    });
})();
```

### 6.3 转换函数何时仍有用

| 场景 | 是否仍使用 `transformPersonnelForSubmit()` |
|------|----------|
| mock-data.js 加载时自动规范化 | ❌ 由 IIFE 替代 |
| 页面读取 PERSONNEL 后立即标准化 | ❌ 由 IIFE 替代 |
| 从外部 API 获取原始数据后转换为带 FK 字段 | ✅ 仍有用 |
| 提交表单数据时序列化 | ✅ 仍有用 |
| **数据库迁移脚本**（真实数据库初始化） | ✅ 仍有用（生产场景） |

### 6.4 与其他文档联动

| 文档 | 关联点 |
|------|--------|
| `33-基础资料模块-v2.11.4.md` §10 | 完整 BUGFIX 修复方案 |
| `15-考勤班次需求-v2.11.2.md` §9 | 数据库字段定义修正 |
| `29-宿舍账单详细弹窗-v2.11.4.md` §2.4.2 | 触发 BUG 的具体场景 |
| `30-宿舍详情页面调整-v2.11.4.md` §3.2.1 | 员工类型 FK 关联引用 |
| `16-人员清单筛选条件-v2.11.2.md` §5.5 | 筛选下拉 value 修正 |

### 6.5 实施建议（v2.11.7 起）

1. ✅ **优先** 在 mock-data.js 末尾加 `normalizeData()` IIFE（一次性投入）
2. ❌ **不再需要** 在页面代码中显式调用 `transformPersonnelForSubmit()`
3. ✅ **保留** 转换函数作为公共工具，供生产数据库迁移使用

### 6.6 验收清单

- [x] mock-data.js 加 `normalizeData()` IIFE
- [x] `findEmployee()` 增强为双向查找
- [x] 控制台输出规范化统计日志
- [x] 所有原型 JS 增加 fallback 兼容
- [x] EMPLOYEE_BILLS 删除冗余字段
- [x] 本文档补充 §6 修订章节

### 6.7 提交记录

| 版本 | 日期 | 变更 | 状态 |
|------|------|------|------|
| v2.11.4 (u) | 2026-07-11 | 初始版本：通过手工转换函数实现 ID↔名称 映射 | ⚠️ 实际未执行 |
| **v2.11.7.BUGFIX** | **2026-07-12** | **替代方案**：mock-data.js 加 `normalizeData()` IIFE 自动规范化，**不再依赖**手工转换函数；保留 `transformPersonnelForSubmit()` 作为生产数据库迁移工具 | ✅ 自动生效 |
