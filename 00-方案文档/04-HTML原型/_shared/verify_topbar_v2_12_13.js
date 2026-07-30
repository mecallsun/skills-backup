// v2.12.13 验证：所有页面页头以办理登记旧风格为标准
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v21213-${Date.now()}`;

const PAGES = [
  'index.html',
  'booking/index.html',
  'booking/check-in.html',
  'booking/check-out.html',
  'booking/edit.html',
  'billing/employee-bills.html',
  'billing/dorm-bills.html',
  'billing/standards.html',
  'dorms/list.html',
  'personnel/list.html',
  'meter/index.html',
  'basics/index.html',
  'settings/index.html'
];

console.log('='.repeat(70));
console.log('v2.12.13 Verify: 所有页面页头以办理登记旧风格为标准');
console.log('='.repeat(70));

let pass = 0, fail = 0;
for (const page of PAGES) {
    const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=8000 --dump-dom "http://localhost:8765/${page}" 2>nul`;
    const output = execSync(cmd, { encoding: 'utf-8', timeout: 12000 });

    // 提取 top-bar 区块
    const topBarMatch = output.match(/<div class="top-bar"[^>]*>([\s\S]*?)<\/div>\s*<\/div>\s*<\/div>/);
    if (!topBarMatch) {
        console.log(`  [FAIL] ${page}: 未找到 .top-bar`);
        fail++;
        continue;
    }

    const topBarHtml = topBarMatch[1];

    // 检查关键元素（按办理登记页面结构）
    const checks = {
        'brand-icon': /class="bi bi-droplet-half brand-icon"/.test(topBarHtml),
        'brand-text': /class="brand-text">金智住宿管理系统</.test(topBarHtml),
        'brand-version': /class="brand-version">v2\.12\./.test(topBarHtml),
        'user-pill': /class="user-pill"/.test(topBarHtml),
        'admin-label': />管理员</.test(topBarHtml),
        'btn-exit': /class="btn-exit"/.test(topBarHtml),
        'order-brand-left': (topBarHtml.indexOf('brand-text') < topBarHtml.indexOf('user-pill'))
    };

    const allOk = Object.values(checks).every(v => v);
    if (allOk) {
        pass++;
        console.log(`  [PASS] ${page.padEnd(35)} | 所有 7 项检查通过`);
    } else {
        fail++;
        console.log(`  [FAIL] ${page.padEnd(35)} | ${Object.entries(checks).filter(([_, v]) => !v).map(([k]) => k).join(', ')}`);
    }
}

console.log('\n' + '='.repeat(70));
console.log(`总计：通过 ${pass}/${PAGES.length}，失败 ${fail}`);
console.log(fail === 0 ? '\n所有页面页头一致（以办理登记旧风格为标准）！' : '\n仍有失败页面');