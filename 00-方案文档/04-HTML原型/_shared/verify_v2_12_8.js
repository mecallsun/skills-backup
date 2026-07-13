// v2.12.8 深度统一验证：住宿状态单一数据源
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2128-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.8 Verify: 住宿状态深度统一');
console.log('='.repeat(70));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/personnel/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    console.log('\n--- 人员清单列表数据（v2.12.8 应无住宿状态列） ---');
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    rows.slice(0, 5).forEach((row, i) => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        console.log(`  [${i + 1}] ${cleaned[2]}(${cleaned[1]}) 在职=${cleaned[8]} 宿舍=${cleaned[10]}`);
    });
}

// 检查没有"住宿状态"列
const hasResideCol = output.includes('住宿状态');
console.log(`\n--- 残留检查 ---`);
console.log(`  "住宿状态"列/筛选：${hasResideCol ? '❌ 仍存在' : '✅ 已删除'}`);

// 验证宿舍房号列保留
const hasDormCol = output.includes('宿舍（房号）');
console.log(`  "宿舍（房号）"列：${hasDormCol ? '✅ 保留' : '❌ 缺失'}`);

// 统计表头列数
const theadMatch = output.match(/<thead[^>]*>([\s\S]*?)<\/thead>/);
if (theadMatch) {
    const headers = theadMatch[1].match(/<th[^>]*>([^<]+)<\/th>/g) || [];
    console.log(`\n--- 表头（应 12 列） ---`);
    headers.forEach((h, i) => {
        const text = h.replace(/<[^>]+>/g, '').trim();
        console.log(`  [${i + 1}] ${text}`);
    });
    console.log(`  总列数：${headers.length}`);
}

// 宿舍筛选下拉
const fDormOpts = output.match(/<select[^>]*id="fDorm"[^>]*>([\s\S]*?)<\/select>/);
if (fDormOpts) {
    const opts = fDormOpts[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`\n--- 宿舍（房号）下拉：${opts.length} 个选项 ---`);
}

console.log('\n' + '='.repeat(70));