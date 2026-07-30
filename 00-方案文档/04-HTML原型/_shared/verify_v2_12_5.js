// 验证 v2.12.5 变更：住宿状态→在宿状态、新增住宿房号列
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2125-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.5 Verify: 在宿状态 + 住宿（房号）');
console.log('='.repeat(70));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/personnel/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 检查表头
const theadMatch = output.match(/<thead[^>]*>([\s\S]*?)<\/thead>/);
if (theadMatch) {
    console.log('\n--- 表头列名 ---');
    const headers = theadMatch[1].match(/<th[^>]*>([^<]+)<\/th>/g) || [];
    headers.forEach((h, i) => {
        const text = h.replace(/<[^>]+>/g, '').trim();
        console.log(`  [${i + 1}] ${text}`);
    });
}

// 检查 tbody 前 10 行
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    console.log('\n--- 前 10 行数据（含在宿状态+住宿） ---');
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    rows.slice(0, 10).forEach((row, i) => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        // 第 9 列是在职状态，第 10 列是离职日期，第 11 列是在宿状态，第 12 列是住宿
        console.log(`  [${i + 1}] ${cleaned[2]}(${cleaned[1]}) 在职=${cleaned[8]} 在宿=${cleaned[10]} 住宿=${cleaned[11]}`);
    });
}

// 检查筛选条件
console.log('\n--- 筛选条件 ---');
const labels = output.match(/<label[^>]*>([^<]+)<\/label>/g) || [];
labels.forEach(l => {
    const text = l.replace(/<[^>]+>/g, '').trim();
    if (text.includes('姓名') || text.includes('在宿') || text.includes('住宿')) {
        console.log(`  筛选标签：${text}`);
    }
});

// 检查住宿下拉选项数量
const fDormOptions = output.match(/<select[^>]*id="fDorm"[^>]*>([\s\S]*?)<\/select>/);
if (fDormOptions) {
    const opts = fDormOptions[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`\n  住宿（房号）下拉选项数：${opts.length}（含"全部"）`);
    console.log(`  示例房号：${opts.slice(0, 5).map(o => o.match(/value="([^"]+)"/)[1]).join(', ')}...`);
}

// 检查在宿状态 Badge
const in宿Badges = (output.match(/<span class="badge bg-primary">在宿<\/span>/g) || []).length;
const 不在宿Badges = (output.match(/<span class="badge bg-light text-dark">不在宿<\/span>/g) || []).length;
console.log(`\n--- 在宿状态 Badge 统计 ---`);
console.log(`  在宿：${in宿Badges}`);
console.log(`  不在宿：${不在宿Badges}`);

console.log('\n' + '='.repeat(70));