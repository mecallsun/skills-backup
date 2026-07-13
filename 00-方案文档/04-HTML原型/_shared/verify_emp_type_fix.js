// v2.12.11 验证：员工类型 undefined 修复
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-emptype-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.11 Verify: 员工类型 undefined 修复');
console.log('='.repeat(70));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/billing/employee-bills.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 提取前 10 行数据
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    console.log('\n--- 前 10 行数据 ---');
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    rows.slice(0, 10).forEach((row, i) => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        if (cleaned.length >= 5) {
            console.log(`  [${i + 1}] 工号=${cleaned[1]} 姓名=${cleaned[2]} 部门=${cleaned[3]} 员工类型=${cleaned[4]} 房号=${cleaned[5]}`);
        }
    });

    // 统计 undefined
    const undefinedCount = rows.filter(row => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        return cells[4] && (cells[4].includes('undefined') || cells[4].trim() === '');
    }).length;
    console.log(`\n--- undefined 统计 ---`);
    console.log(`  undefined 数：${undefinedCount}（应为 0）`);
    console.log(`  ${undefinedCount === 0 ? '✅ 修复成功' : '❌ 仍有 undefined'}`);

    // 统计员工类型分布
    const typeCount = {};
    rows.forEach(row => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const t = cells[4] ? cells[4].replace(/<[^>]+>/g, '').trim() : '-';
        typeCount[t] = (typeCount[t] || 0) + 1;
    });
    console.log(`\n--- 员工类型分布（页内 10 行）---`);
    for (const [k, v] of Object.entries(typeCount)) {
        console.log(`  ${k}: ${v}`);
    }
}

console.log('\n' + '='.repeat(70));