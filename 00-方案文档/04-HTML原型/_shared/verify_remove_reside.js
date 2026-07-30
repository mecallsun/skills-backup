// 验证 v2.12.7 删除"住宿状态"
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2127-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.7 Verify: 删除住宿状态');
console.log('='.repeat(70));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/personnel/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 表头
const theadMatch = output.match(/<thead[^>]*>([\s\S]*?)<\/thead>/);
if (theadMatch) {
    console.log('\n--- 表头列名 ---');
    const headers = theadMatch[1].match(/<th[^>]*>([^<]+)<\/th>/g) || [];
    headers.forEach((h, i) => {
        const text = h.replace(/<[^>]+>/g, '').trim();
        const flag = text === '住宿状态' ? ' ❌ 应删除' : '';
        console.log(`  [${i + 1}] ${text}${flag}`);
    });
}

// 筛选条件
console.log('\n--- 筛选条件 ---');
const labels = output.match(/<label[^>]*>([^<]+)<\/label>/g) || [];
labels.forEach(l => {
    const text = l.replace(/<[^>]+>/g, '').trim();
    const flag = text === '住宿状态' ? ' ❌ 应删除' : ' ✓';
    console.log(`  ${text}${flag}`);
});

// 检查是否有 fReside 下拉
console.log('\n--- 残留检查 ---');
const hasResideDropdown = output.includes('id="fReside"');
console.log(`  fReside 下拉：${hasResideDropdown ? '❌ 仍存在' : '✅ 已删除'}`);
const hasResideBadge = output.includes('已住宿</span>') || output.includes('未住宿</span>');
console.log(`  住宿状态 Badge：${hasResideBadge ? '❌ 仍存在' : '✅ 已删除'}`);
const hasResideFilter = /住宿状态/.test(output);
console.log(`  "住宿状态"文案：${hasResideFilter ? '⚠️ 仍有提及' : '✅ 已清除'}`);

// tbody 数据
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    console.log('\n--- 前 10 行数据 ---');
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    rows.slice(0, 10).forEach((row, i) => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        console.log(`  [${i + 1}] ${cleaned[2]}(${cleaned[1]}) 在职=${cleaned[8]} 住宿=${cleaned[10]}`);
    });
}

// 住宿筛选下拉
const fDormOpts = output.match(/<select[^>]*id="fDorm"[^>]*>([\s\S]*?)<\/select>/);
if (fDormOpts) {
    const opts = fDormOpts[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`\n--- 住宿（房号）下拉选项数：${opts.length}（应含"全部"+ 200 房号）---`);
}

console.log('\n' + '='.repeat(70));