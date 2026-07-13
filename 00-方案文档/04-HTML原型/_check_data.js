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

const datasets = ['PERSONNEL', 'BOOKINGS', 'EMPLOYEE_BILLS_202607', 'DORM_BILLS_202607', 'DORMS', 'METER_RECORDS_202607', 'METER_RECORDS'];
datasets.forEach(name => {
    const arrStr = extractArray(name);
    if (!arrStr) { console.log(name + ': 未找到'); return; }
    try {
        const arr = eval(arrStr);
        console.log(name + ': ' + arr.length + ' 条');
        if (arr.length > 0 && name === 'EMPLOYEE_BILLS_202607') {
            console.log('  示例1:', JSON.stringify(arr[0]));
            console.log('  示例2:', JSON.stringify(arr[1]));
        }
    } catch(e) {
        console.log(name + ': 解析失败 - ' + e.message.substring(0, 120));
    }
});