// v2.12.9 验证：房号 combobox 模糊筛选
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2129-${Date.now()}`;

console.log('='.repeat(70));
console.log('v2.12.9 Verify: 房号 combobox 模糊筛选');
console.log('='.repeat(70));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/personnel/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

const dormInput = output.match(/<input[^>]*id="fDorm"[^>]*>/);
const datalist = output.match(/<datalist[^>]*id="dormList"[^>]*>/);
const selectOld = output.match(/<select[^>]*id="fDorm"[^>]*>/);

console.log('\n--- 房号输入控件 ---');
if (dormInput) {
    console.log(`  [OK] <input list="dormList"> combobox`);
    console.log('       ' + dormInput[0].substring(0, 150));
}
if (datalist) {
    console.log('  [OK] <datalist id="dormList">');
}
if (selectOld) {
    console.log('  [FAIL] 旧 <select id="fDorm"> 仍存在');
} else {
    console.log('  [OK] 旧 <select id="fDorm"> 已删除');
}

const dlContent = output.match(/<datalist[^>]*id="dormList"[^>]*>([\s\S]*?)<\/datalist>/);
if (dlContent) {
    const opts = dlContent[1].match(/<option[^>]*value="([^"]+)">/g) || [];
    console.log(`\n--- datalist 候选项：${opts.length} 个房号 ---`);
    console.log('  示例：' + opts.slice(0, 5).map(o => o.match(/value="([^"]+)"/)[1]).join(', ') + ' ...');
}

const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    console.log('\n--- 表格行数：' + rows.length + '（应为 10）---');
    if (rows.length > 0) {
        const cells = rows[0].match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
        console.log('  [1] ' + cleaned[2] + '(' + cleaned[1] + ') 住宿=' + cleaned[10]);
    }
}

console.log('\n' + '='.repeat(70));