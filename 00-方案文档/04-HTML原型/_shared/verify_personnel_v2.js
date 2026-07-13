// 验证 personnel/list.html：1) 离职日期列；2) 手机号筛选
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-pp-${Date.now()}`;

console.log('='.repeat(70));
console.log('Verify personnel/list.html');
console.log('='.repeat(70));

// 测试 1：页面直接访问，检查"离职日期"列是否出现
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

// 检查 tbody
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    console.log('\n--- 前 5 行数据（含离职日期） ---');
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    rows.slice(0, 5).forEach((row, i) => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        console.log(`  [${i + 1}] ${cleaned.join(' | ')}`);
    });
}

// 检查筛选条件标签
console.log('\n--- 筛选条件标签 ---');
const fKwLabel = output.match(/<label[^>]*>\s*([^<]*?)\s*<\/label>\s*<input[^>]*id="fKw"/);
if (fKwLabel) {
    console.log(`  姓名/工号 筛选标签：${fKwLabel[1]}`);
} else {
    // 尝试另一种匹配
    const labels = output.match(/<label[^>]*>([^<]*?)<\/label>/g) || [];
    labels.forEach(l => {
        const text = l.replace(/<[^>]+>/g, '').trim();
        if (text.includes('姓名') || text.includes('工号') || text.includes('手机')) {
            console.log(`  找到标签：${text}`);
        }
    });
}

console.log('\n' + '='.repeat(70));