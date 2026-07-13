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

const EMPLOYEE_BILLS_202607 = eval(extractArray('EMPLOYEE_BILLS_202607'));
const PERSONNEL = eval(extractArray('PERSONNEL'));

// 模拟 employee-bills.html 的 currentData() 完整逻辑
const CURR_MONTH = '2026-07';
const billingMonth = CURR_MONTH;
const empKeyword = '';
const deptName = '';
const typeName = '';
const resideCode = '';
const dormKw = '';

let data = EMPLOYEE_BILLS_202607.filter(b => !billingMonth || b.billingMonth === billingMonth);
console.log('1. 月份过滤后:', data.length, '条');

// 模拟 getEmployeeResidenceCode
function getEmployeeResidenceCode(empId) {
    for (var i = 0; i < PERSONNEL.length; i++) {
        if (PERSONNEL[i].id === empId) {
            // 模拟 normalizeData IIFE 后的状态
            const p = PERSONNEL[i];
            // 简化：dormCode 非空 → LODGED
            return (p.dormCode && p.dormCode.trim() !== '') ? 'LODGED' : 'NOT_LODGED';
        }
    }
    return 'NOT_LODGED';
}

// 检查 employeeId 关联性
const billEmpIds = new Set(data.map(b => b.employeeId));
const personnelIds = new Set(PERSONNEL.map(p => p.id));
const matched = [...billEmpIds].filter(id => personnelIds.has(id));
console.log('账单 employeeId 在 PERSONNEL 中的匹配数:', matched.length, '/', billEmpIds.size);

// 检查 dormCode 关联
let noMatch = 0;
data.slice(0, 10).forEach(b => {
    const p = PERSONNEL.find(p => p.id === b.employeeId);
    if (!p) noMatch++;
});
console.log('前 10 条账单关联 PERSONNEL 未匹配:', noMatch);

// 部门分布
const deptStats = {};
data.forEach(b => {
    deptStats[b.department] = (deptStats[b.department] || 0) + 1;
});
console.log('账单部门分布:', deptStats);

// 员工类型分布
const typeStats = {};
data.forEach(b => {
    typeStats[b.employeeType] = (typeStats[b.employeeType] || 0) + 1;
});
console.log('账单员工类型分布:', typeStats);

// dormCode 关联
let noDorm = 0;
data.forEach(b => {
    if (!b.dormCode || b.dormCode.trim() === '') noDorm++;
});
console.log('dormCode 为空的账单:', noDorm);

console.log('');
console.log('=== 测试 deptName=生产部 过滤 ===');
const filtered = data.filter(b => b.department === '生产部');
console.log('生产部账单数:', filtered.length);

console.log('');
console.log('=== 测试 typeName=合同工 过滤 ===');
const filtered2 = data.filter(b => b.employeeType === '合同工');
console.log('合同工账单数:', filtered2.length);