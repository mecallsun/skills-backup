// 模拟 mock-data.js normalizeData IIFE 后的数据状态
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
const BOOKINGS = eval(extractArray('BOOKINGS'));
const EMPLOYEE_TYPES = eval(extractArray('EMPLOYEE_TYPES'));
const DEPARTMENTS = eval(extractArray('DEPARTMENTS'));

// 模拟 v2.11.22 修复后的 normalizeData IIFE
(function normalizeDataV2_11_22() {
    let billsFixed = 0;
    EMPLOYEE_BILLS_202607.forEach(b => {
        delete b.attendanceType;
        if (typeof b.employeeId === 'string' && isNaN(parseInt(b.employeeId))) {
            const p = PERSONNEL.find(p => p.employeeCode === b.employeeId);
            if (p) {
                b.employeeId = p.id;
                billsFixed++;
            }
        }
        if (b.employeeId) {
            const p = PERSONNEL.find(emp => emp.id === b.employeeId);
            if (p) {
                if (!b.employeeType) {
                    if (p.employeeTypeName) {
                        b.employeeType = p.employeeTypeName;
                    } else if (p.employeeType) {
                        b.employeeType = p.employeeType;
                    } else if (EMPLOYEE_TYPES) {
                        const t = EMPLOYEE_TYPES.find(t => t.id === p.employeeTypeId);
                        if (t) b.employeeType = t.name;
                    }
                }
                if (!b.attendanceType && (p.attendanceTypeName || p.attendanceType)) {
                    b.attendanceType = p.attendanceTypeName || p.attendanceType;
                }
            }
        }
    });
    console.log('修复后统计:');
    console.log('  employeeId 转换:', billsFixed, '条');
})();

// 测试员工类型分布
const typeStats = {};
EMPLOYEE_BILLS_202607.forEach(b => {
    typeStats[b.employeeType] = (typeStats[b.employeeType] || 0) + 1;
});
console.log('员工类型分布:', typeStats);

// 测试按 employeeTypeId=1 (合同工) 过滤
const CURR_MONTH = '2026-07';
let data = EMPLOYEE_BILLS_202607.filter(b => !CURR_MONTH || b.billingMonth === CURR_MONTH);
console.log('月份过滤后:', data.length, '条');

// 用 employeeTypeId=1 (合同工) 过滤
const typeId = 1;
const filtered = data.filter(function (b) {
    const p = PERSONNEL.find(function (emp) { return emp.id === b.employeeId; });
    return p && p.employeeTypeId === typeId;
});
console.log('员工类型ID=1(合同工) 过滤后:', filtered.length, '条');

// 用 employeeTypeId=4 (实习生) 过滤
const filtered4 = data.filter(function (b) {
    const p = PERSONNEL.find(function (emp) { return emp.id === b.employeeId; });
    return p && p.employeeTypeId === 4;
});
console.log('员工类型ID=4(实习生) 过滤后:', filtered4.length, '条');

// 用部门='生产部' 过滤
const filteredDept = data.filter(b => b.department === '生产部');
console.log('部门=生产部 过滤后:', filteredDept.length, '条');