// 验证 v2.12.10 员工分摊筛选区重构
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-emp-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.10 Verify: 员工分摊筛选区重构');
console.log('='.repeat(70));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/billing/employee-bills.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 筛选条件顺序
console.log('\n--- 筛选条件顺序 ---');
const labelRegex = /<label[^>]*>([^<]+)<\/label>/g;
const labels = [];
let m;
while ((m = labelRegex.exec(output)) !== null) {
    labels.push(m[1].trim());
}
labels.forEach((l, i) => {
    console.log(`  [${i + 1}] ${l}`);
});

// 下拉选项数量
console.log('\n--- 各下拉选项数 ---');

const fDeptOpts = output.match(/<select[^>]*id="fDept"[^>]*>([\s\S]*?)<\/select>/);
if (fDeptOpts) {
    const opts = fDeptOpts[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`  部门：${opts.length} 个（含"全部"+ 8 部门）`);
}

const fTypeOpts = output.match(/<select[^>]*id="fType"[^>]*>([\s\S]*?)<\/select>/);
if (fTypeOpts) {
    const opts = fTypeOpts[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`  员工类型：${opts.length} 个（含"全部"+ 5 类型）`);
    opts.slice(1, 6).forEach(o => {
        const m = o.match(/value="([^"]+)">([^<]+)</);
        console.log(`    - value="${m[1]}" → ${m[2]}`);
    });
}

const fResideOpts = output.match(/<select[^>]*id="fReside"[^>]*>([\s\S]*?)<\/select>/);
if (fResideOpts) {
    const opts = fResideOpts[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`  住宿状态：${opts.length} 个（含"全部"+ 3 状态）`);
    opts.slice(1, 4).forEach(o => {
        const m = o.match(/value="([^"]+)">([^<]+)</);
        console.log(`    - value="${m[1]}" → ${m[2]}`);
    });
}

// 员工字段 placeholder
const empInput = output.match(/<input[^>]*id="empKeyword"[^>]*placeholder="([^"]+)"/);
if (empInput) {
    console.log(`\n--- 员工字段 placeholder ---`);
    console.log(`  "${empInput[1]}"`);
}

// 列表数据
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    console.log(`\n--- 列表数据：${rows.length} 行 ---`);
    if (rows.length > 0) {
        const cells = rows[0].match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        console.log(`  [1] 工号=${cleaned[1]} 姓名=${cleaned[2]} 部门=${cleaned[3]} 类型=${cleaned[4]} 住宿=${cleaned[5]}`);
    }
}

console.log('\n' + '='.repeat(70));