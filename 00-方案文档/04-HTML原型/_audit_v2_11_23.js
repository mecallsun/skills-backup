// =====================================================================
// ⚠️ DEPRECATED — 请使用 _audit_v2_11_24.js
// =====================================================================
// 本脚本使用 v2.11.23 §2.2 规则 2b 的部门语义映射（已废）：
//   deptId 1 → 1, 2 → 1, 3 → 4, 4 → 1, 5 → 3, 6 → 2, 7 → 3, 8 → 1
// 新规则见：00-方案文档/43-无效FK归一通用规范-v2.11.24.md
// 本脚本仅作为历史归档保留，请勿再用于生产环境审计。
// =====================================================================

// v2.11.23 数据完整性审计
const fs = require('fs');
const content = fs.readFileSync('mock-data.js', 'utf8');

function extractArray(name) {
    const marker = 'const ' + name + ' = ';
    const start = content.indexOf(marker);
    if (start === -1) return null;
    let depth = 0, i = start + marker.length, inString = false, strChar = '';
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

const EMPLOYEE_TYPES = eval(extractArray('EMPLOYEE_TYPES'));
const EMPLOYMENT_STATUSES = eval(extractArray('EMPLOYMENT_STATUSES'));
const ATTENDANCE_TYPES = eval(extractArray('ATTENDANCE_TYPES'));
const DEPARTMENTS = eval(extractArray('DEPARTMENTS'));
const RESIDENCE_STATUS = eval(extractArray('RESIDENCE_STATUS'));
const EMPLOYEE_BILL_SEGMENT_TYPE = eval(extractArray('EMPLOYEE_BILL_SEGMENT_TYPE'));

const PERSONNEL = eval(extractArray('PERSONNEL'));

// v2.11.23 模拟 normalizeData IIFE 完整执行
(function simulateNormalize() {
    // 2b. 员工类型校正
    let corrected = 0;
    PERSONNEL.forEach(p => {
        if (p.employeeTypeId != null && p.employeeTypeId > 5) {
            var deptId = p.departmentId || 1;
            var validId = 1;
            if (deptId === 1) validId = 1;
            else if (deptId === 2) validId = 1;
            else if (deptId === 3) validId = 4;
            else if (deptId === 4) validId = 1;
            else if (deptId === 5) validId = 3;
            else if (deptId === 6) validId = 2;
            else if (deptId === 7) validId = 3;
            else validId = 1;
            p.employeeTypeId = validId;
            corrected++;
        }
    });
    console.log('模拟 IIFE 校正完成: ' + corrected + ' 条');
})();
const BOOKINGS = eval(extractArray('BOOKINGS'));
const EMPLOYEE_BILLS_202607 = eval(extractArray('EMPLOYEE_BILLS_202607'));

console.log('========== v2.11.23 数据完整性审计 ==========\n');

// 1. 员工类型分布
const etStats = {};
PERSONNEL.forEach(p => {
    etStats[p.employeeTypeId] = (etStats[p.employeeTypeId] || 0) + 1;
});
console.log('[PERSONNEL employeeTypeId 分布]');
Object.entries(etStats).sort((a, b) => Number(a[0]) - Number(b[0])).forEach(([k, v]) => {
    const t = EMPLOYEE_TYPES.find(t => t.id === Number(k));
    console.log('  ID=' + k + ' (' + (t ? t.name : '未知类型') + '): ' + v + ' 人');
});
const invalidET = PERSONNEL.filter(p => !EMPLOYEE_TYPES.find(t => t.id === p.employeeTypeId));
console.log('  ❌ employeeTypeId 无效:', invalidET.length, '人');

// 2. 在职状态分布
const esStats = {};
PERSONNEL.forEach(p => {
    esStats[p.employmentStatusId !== undefined ? p.employmentStatusId : p.status] = (esStats[p.employmentStatusId !== undefined ? p.employmentStatusId : p.status] || 0) + 1;
});
console.log('\n[PERSONNEL employmentStatusId 分布]');
Object.entries(esStats).sort((a, b) => Number(a[0]) - Number(b[0])).forEach(([k, v]) => {
    const s = EMPLOYMENT_STATUSES.find(s => s.id === Number(k));
    console.log('  ID=' + k + ' (' + (s ? s.name : '未知状态') + '): ' + v + ' 人');
});

// 3. 考勤班次分布
const atStats = {};
PERSONNEL.forEach(p => {
    const atId = p.attendanceTypeId;
    atStats[atId] = (atStats[atId] || 0) + 1;
});
console.log('\n[PERSONNEL attendanceTypeId 分布]');
Object.entries(atStats).sort((a, b) => Number(a[0]) - Number(b[0])).forEach(([k, v]) => {
    const t = ATTENDANCE_TYPES.find(t => t.id === Number(k));
    console.log('  ID=' + k + ' (' + (t ? t.name : '未知班次') + '): ' + v + ' 人');
});

// 4. 部门分布
const deptStats = {};
PERSONNEL.forEach(p => {
    deptStats[p.departmentId] = (deptStats[p.departmentId] || 0) + 1;
});
console.log('\n[PERSONNEL departmentId 分布]');
Object.entries(deptStats).sort((a, b) => Number(a[0]) - Number(b[0])).forEach(([k, v]) => {
    const d = DEPARTMENTS.find(d => d.id === Number(k));
    console.log('  ID=' + k + ' (' + (d ? d.name : '未知部门') + '): ' + v + ' 人');
});

// 5. BOOKINGS 状态分布
const bkStats = {};
BOOKINGS.forEach(b => {
    bkStats[b.status] = (bkStats[b.status] || 0) + 1;
});
console.log('\n[BOOKINGS status 分布]');
Object.entries(bkStats).sort((a, b) => Number(a[0]) - Number(b[0])).forEach(([k, v]) => {
    console.log('  Status=' + k + ': ' + v + ' 条');
});

// 6. BOOKINGS type 分布
const bktStats = {};
BOOKINGS.forEach(b => {
    bktStats[b.type] = (bktStats[b.type] || 0) + 1;
});
console.log('\n[BOOKINGS type 分布]');
Object.entries(bktStats).sort((a, b) => Number(a[0]) - Number(b[0])).forEach(([k, v]) => {
    console.log('  Type=' + k + ' (' + (k === '1' ? '入住' : '退房') + '): ' + v + ' 条');
});

// 7. EMPLOYEE_BILLS_202607 字段完整性
const ebMissingFields = {};
EMPLOYEE_BILLS_202607.forEach(b => {
    ['employeeId', 'employeeCode', 'employeeName', 'department', 'employeeType', 'dormCode', 'billingMonth', 'stayDays', 'segmentType', 'coldShareAmount', 'hotShareAmount', 'electricityShareAmount', 'totalShareAmount', 'isPublished'].forEach(f => {
        if (b[f] === undefined || b[f] === null || b[f] === '') {
            ebMissingFields[f] = (ebMissingFields[f] || 0) + 1;
        }
    });
});
console.log('\n[EMPLOYEE_BILLS_202607 缺失字段统计]');
Object.entries(ebMissingFields).forEach(([k, v]) => {
    console.log('  ' + k + ': 缺失 ' + v + ' 条');
});

// 8. segmentType 分布
const segStats = {};
EMPLOYEE_BILLS_202607.forEach(b => {
    segStats[b.segmentType] = (segStats[b.segmentType] || 0) + 1;
});
console.log('\n[EMPLOYEE_BILLS_202607 segmentType 分布]');
Object.entries(segStats).forEach(([k, v]) => {
    const s = EMPLOYEE_BILL_SEGMENT_TYPE.find(s => s.code === k);
    console.log('  ' + k + ' (' + (s ? s.name : '未知段类型') + '): ' + v + ' 条');
});

// 9. dormCode 完整性
let noDorm = 0;
EMPLOYEE_BILLS_202607.forEach(b => {
    if (!b.dormCode || String(b.dormCode).trim() === '') noDorm++;
});
console.log('\n[EMPLOYEE_BILLS_202607 dormCode 为空]:', noDorm, '条');

// 10. employeeId 与 PERSONNEL 关联
const empIds = new Set(PERSONNEL.map(p => p.id));
let orphanCount = 0;
EMPLOYEE_BILLS_202607.forEach(b => {
    if (!empIds.has(b.employeeId)) orphanCount++;
});
console.log('\n[EMPLOYEE_BILLS_202607 employeeId 孤儿记录]:', orphanCount, '条');

// 11. 列出具体孤儿记录
if (orphanCount > 0) {
    const orphans = [];
    EMPLOYEE_BILLS_202607.forEach(b => {
        if (!empIds.has(b.employeeId)) orphans.push(b);
    });
    console.log('  示例孤儿:');
    orphans.slice(0, 5).forEach(b => console.log('    ID=' + b.id + ' employeeId=' + b.employeeId + ' code=' + b.employeeCode + ' name=' + b.employeeName));
}