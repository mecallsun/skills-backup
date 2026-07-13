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

const arr = eval(extractArray('EMPLOYEE_BILLS_202607'));
console.log('EMPLOYEE_BILLS_202607 共', arr.length, '条');
console.log('最大 id:', Math.max(...arr.map(b => b.id)));
console.log('最后一条:', JSON.stringify(arr[arr.length - 1]));
console.log('segmentType 分布:', arr.reduce((acc, b) => { acc[b.segmentType] = (acc[b.segmentType] || 0) + 1; return acc; }, {}));

// 查找数组结束位置
const marker = 'const EMPLOYEE_BILLS_202607 = [';
const start = content.indexOf(marker);
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
console.log('数组结束位置:', i + 1);
console.log('数组结束附近内容:');
console.log(content.substring(i - 30, i + 50));