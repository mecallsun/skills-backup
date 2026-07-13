const fs = require('fs');
const content = fs.readFileSync('mock-data.js', 'utf8');

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

const EMPLOYEE_BILLS_202607 = eval(extractArray('EMPLOYEE_BILLS_202607'));
const BOOKINGS = eval(extractArray('BOOKINGS'));

// 模拟 employee-bills.html 的 currentData() 逻辑
console.log('=== 模拟 employee-bills.html currentData() ===');
const CURR_MONTH = '2026-07';
let data = EMPLOYEE_BILLS_202607.filter(b => !CURR_MONTH || b.billingMonth === CURR_MONTH);
console.log('按月份过滤后:', data.length, '条');

// 检查 billingMonth 输入框的默认值
console.log('billingMonth 输入框默认 value="2026-07"');

// 检查 EMPLOYEE_BILLS_202607 数据的 billingMonth 分布
const monthStats = {};
EMPLOYEE_BILLS_202607.forEach(b => {
    monthStats[b.billingMonth] = (monthStats[b.billingMonth] || 0) + 1;
});
console.log('billingMonth 分布:', monthStats);

console.log('');
console.log('=== 模拟 booking/index.html getFiltered() ===');
// 模拟空 filter
let filtered = BOOKINGS.slice();
console.log('空 filter 全部 BOOKINGS:', filtered.length, '条');

// 检查日期过滤
const bookingDateStats = {};
BOOKINGS.forEach(b => {
    const ym = (b.bookingDate || '').substring(0, 7);
    bookingDateStats[ym] = (bookingDateStats[ym] || 0) + 1;
});
console.log('BOOKING bookingDate 年月分布:', bookingDateStats);

// 检查 type/status 分布
const typeStats = {};
const statusStats = {};
BOOKINGS.forEach(b => {
    typeStats[b.type] = (typeStats[b.type] || 0) + 1;
    statusStats[b.status] = (statusStats[b.status] || 0) + 1;
});
console.log('BOOKING type 分布:', typeStats);
console.log('BOOKING status 分布:', statusStats);

// 检查 employee-bills.html 的 isPublished 字段
const pubStats = {};
EMPLOYEE_BILLS_202607.forEach(b => {
    pubStats[b.isPublished] = (pubStats[b.isPublished] || 0) + 1;
});
console.log('isPublished 分布:', pubStats);