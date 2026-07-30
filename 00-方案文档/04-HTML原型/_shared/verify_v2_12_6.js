// 验证 v2.12.6 住宿状态更正
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2126-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.6 Verify: 住宿状态（更名 + 关联基础资料字典）');
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
        const tag = text === '住宿状态' ? ' 🆕 v2.12.6' : '';
        console.log(`  [${i + 1}] ${text}${tag}`);
    });
}

// 筛选条件检查
console.log('\n--- 筛选条件 ---');
const labels = output.match(/<label[^>]*>([^<]+)<\/label>/g) || [];
labels.forEach(l => {
    const text = l.replace(/<[^>]+>/g, '').trim();
    if (text.includes('姓名') || text.includes('住宿') || text.includes('住宿') || text.includes('在宿')) {
        console.log(`  标签：${text}${text === '住宿状态' ? ' ✅ 已更正' : ''}`);
    }
});

// 住宿状态下拉选项
const fResideOpts = output.match(/<select[^>]*id="fReside"[^>]*>([\s\S]*?)<\/select>/);
if (fResideOpts) {
    const opts = fResideOpts[1].match(/<option[^>]*value="([^"]+)">([^<]+)<\/option>/g) || [];
    console.log(`\n--- 住宿状态下拉选项（关联 RESIDENCE_STATUS 字典） ---`);
    opts.forEach(o => {
        const m = o.match(/value="([^"]+)">([^<]+)</);
        console.log(`  value="${m[1]}" → ${m[2]}`);
    });
}

// Badge 渲染
console.log('\n--- 住宿状态 Badge 渲染 ---');
const badges = {
    '已住宿': (output.match(/<span class="badge bg-primary">已住宿<\/span>/g) || []).length,
    '未住宿': (output.match(/<span class="badge bg-light text-dark">未住宿<\/span>/g) || []).length,
    '待入住': (output.match(/<span class="badge bg-warning text-dark">待入住<\/span>/g) || []).length,
    '在宿(残留)': (output.match(/>在宿</g) || []).length,
    '不在宿(残留)': (output.match(/>不在宿</g) || []).length
};
for (const k of Object.keys(badges)) {
    console.log(`  ${k}: ${badges[k]}`);
}

// 检查 tbody 前 10 行
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    console.log('\n--- 前 10 行数据 ---');
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    rows.slice(0, 10).forEach((row, i) => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        console.log(`  [${i + 1}] ${cleaned[2]}(${cleaned[1]}) 在职=${cleaned[8]} 住宿=${cleaned[10]} 住宿=${cleaned[11]}`);
    });
}

console.log('\n' + '='.repeat(70));