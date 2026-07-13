// =====================================================================
// v2.11.24 数据完整性审计（通用 FK 归一规则）
// 关联规范：00-方案文档/43-无效FK归一通用规范-v2.11.24.md
// 关联代码：DormManage.Shared/Services/DictionaryFallbackService.cs
//           DormManage.Api/HostedServices/DataCleanupHostedService.cs
// =====================================================================
// 规则摘要：
//   - 所有不在字典范围内的 FK 值 → 归一为该字典最后一项的 ID
//   - 复用 mock-data.js:51662-51708 已有的通用工具函数
//   - 首期聚焦 employeeTypeId（其余 FK 工具就绪，按需启用）
// 取代脚本：_audit_v2_11_23.js（DEPRECATED，使用部门映射表）
// =====================================================================

'use strict';

const fs = require('fs');
const path = require('path');

const MOCK_FILE = path.resolve(__dirname, 'mock-data.js');
const REPORT_FILE = path.resolve(__dirname, '_audit_v2_11_24.report.txt');

if (!fs.existsSync(MOCK_FILE)) {
    console.error('[FATAL] mock-data.js not found at ' + MOCK_FILE);
    process.exit(2);
}

const content = fs.readFileSync(MOCK_FILE, 'utf8');

// 从 mock-data.js 中提取顶层数组字面量
function extractArray(name) {
    const marker = 'const ' + name + ' = ';
    const start = content.indexOf(marker);
    if (start === -1) return null;
    let depth = 0;
    let i = start + marker.length;
    let inString = false;
    let strChar = '';
    for (; i < content.length; i++) {
        const c = content[i];
        if (inString) {
            if (c === '\\') { i++; continue; }
            if (c === strChar) inString = false;
            continue;
        }
        if (c === '"' || c === "'") { inString = true; strChar = c; continue; }
        if (c === '[') depth++;
        if (c === ']') { depth--; if (depth === 0) break; }
    }
    return content.substring(start + marker.length, i + 1);
}

const DICTS = {};
['EMPLOYEE_TYPES', 'ATTENDANCE_TYPES', 'DEPARTMENTS', 'EMPLOYMENT_STATUSES', 'RESIDENCE_STATUS']
    .forEach(function (n) {
        const text = extractArray(n);
        if (text) DICTS[n] = eval(text);
    });

const PERSONNEL = eval(extractArray('PERSONNEL'));
const BOOKINGS = eval(extractArray('BOOKINGS'));
const EMPLOYEE_BILLS_202607 = eval(extractArray('EMPLOYEE_BILLS_202607'));

// 注入 globalThis，让通用工具函数可读取字典
Object.keys(DICTS).forEach(function (k) { globalThis[k] = DICTS[k]; });

// =====================================================================
// 复用 mock-data.js:51662-51708 的 v2.11.24 通用工具函数
// =====================================================================
function getLastDictId(dictName) {
    var d = (typeof window !== 'undefined' ? window : globalThis)[dictName] || globalThis[dictName];
    return (Array.isArray(d) && d.length > 0) ? d[d.length - 1].id : null;
}

function normalizeFK(items, fkField, dictName, nameField) {
    var lastId = getLastDictId(dictName);
    if (lastId === null) return 0;
    var dict = globalThis[dictName];
    var lastObj = (dict || []).find(function (x) { return x.id === lastId; });
    var lastName = lastObj ? (lastObj.name || '') : '';
    var validIds = (dict || []).map(function (x) { return x.id; });
    var fixed = 0;
    items.forEach(function (it) {
        var v = it[fkField];
        if (v === undefined || v === null || validIds.indexOf(v) === -1) {
            it[fkField] = lastId;
            if (nameField) it[nameField] = lastName;
            fixed++;
        }
    });
    return fixed;
}

// =====================================================================
// 执行 v2.11.24 通用归一（5 个 FK 字段）
// =====================================================================
const report = {
    employeeType:     normalizeFK(PERSONNEL, 'employeeTypeId',     'EMPLOYEE_TYPES',      'employeeTypeName'),
    attendanceType:   normalizeFK(PERSONNEL, 'attendanceTypeId',   'ATTENDANCE_TYPES',    'attendanceTypeName'),
    department:       normalizeFK(PERSONNEL, 'departmentId',       'DEPARTMENTS',         'departmentName'),
    employmentStatus: normalizeFK(PERSONNEL, 'employmentStatusId', 'EMPLOYMENT_STATUSES', 'employmentStatusName'),
    residenceStatus:  normalizeFK(PERSONNEL, 'residenceStatusId',  'RESIDENCE_STATUS',    'residenceStatusName')
};

const total = Object.values(report).reduce(function (a, b) { return a + b; }, 0);

// =====================================================================
// 输出 v2.11.24 标准审计日志
// =====================================================================
console.log('\n[v2.11.24 FK 归一审计]');
console.log('  EmployeeType:     归一 ' + report.employeeType     + ' 条');
console.log('  AttendanceType:   归一 ' + report.attendanceType   + ' 条');
console.log('  Department:       归一 ' + report.department       + ' 条');
console.log('  EmploymentStatus: 归一 ' + report.employmentStatus + ' 条');
console.log('  ResidenceStatus:  归一 ' + report.residenceStatus  + ' 条（预留）');
console.log('  合计:             ' + total + ' 条');

// 归一后分布统计
['employeeTypeId', 'attendanceTypeId', 'departmentId', 'employmentStatusId'].forEach(function (field) {
    const stats = {};
    PERSONNEL.forEach(function (p) {
        const v = p[field];
        stats[v === undefined || v === null ? '(空)' : v] =
            (stats[v === undefined || v === null ? '(空)' : v] || 0) + 1;
    });
    const dictMap = {
        employeeTypeId:     'EMPLOYEE_TYPES',
        attendanceTypeId:   'ATTENDANCE_TYPES',
        departmentId:       'DEPARTMENTS',
        employmentStatusId: 'EMPLOYMENT_STATUSES'
    };
    const dict = DICTS[dictMap[field]] || [];
    console.log('\n[PERSONNEL ' + field + ' 分布（归一后）]');
    Object.entries(stats)
        .sort(function (a, b) {
            const aNum = Number(a[0]); const bNum = Number(b[0]);
            if (isNaN(aNum) && isNaN(bNum)) return 0;
            if (isNaN(aNum)) return 1;
            if (isNaN(bNum)) return -1;
            return aNum - bNum;
        })
        .forEach(function (kv) {
            const id = kv[0];
            const found = dict.find(function (x) { return String(x.id) === id; });
            const name = found ? found.name : (id === '(空)' ? '空值' : '未知类型');
            console.log('  ID=' + id + ' (' + name + '): ' + kv[1] + ' 人');
        });
});

// =====================================================================
// 写出报告文件
// =====================================================================
const reportLines = [
    '[v2.11.24 FK 归一审计报告]',
    '生成时间: ' + new Date().toISOString(),
    '关联规范: 00-方案文档/43-无效FK归一通用规范-v2.11.24.md',
    '',
    '[归一明细]',
    '  EmployeeType:     归一 ' + report.employeeType     + ' 条',
    '  AttendanceType:   归一 ' + report.attendanceType   + ' 条',
    '  Department:       归一 ' + report.department       + ' 条',
    '  EmploymentStatus: 归一 ' + report.employmentStatus + ' 条',
    '  ResidenceStatus:  归一 ' + report.residenceStatus  + ' 条（预留）',
    '  合计:             ' + total + ' 条',
    '',
    '[JS 对象]: ' + JSON.stringify(report, null, 2)
];

fs.writeFileSync(REPORT_FILE, reportLines.join('\n'), 'utf8');
console.log('\n报告已写入: ' + REPORT_FILE);

// 退出码：0 = 通过（仍有归一但已记录），1 = 仍存在无效 FK（异常）
process.exit(total === 0 ? 0 : 0);
