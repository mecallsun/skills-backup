// v2.12.12 验证：所有页面页头（Tier 1）一致性
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-topbar-${Date.now()}`;

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
console.log('v2.12.12 Verify: 所有页面页头一致性');
console.log('='.repeat(70));

let pass = 0, fail = 0;
for (const page of PAGES) {
    const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=8000 --dump-dom "http://localhost:8765/${page}" 2>nul`;
    const output = execSync(cmd, { encoding: 'utf-8', timeout: 12000 });

    // 提取 .top-bar 区域
    const topBarMatch = output.match(/<div class="top-bar"[^>]*>([\s\S]*?)<\/div>\s*<\/div>\s*<\/div>/);
    if (!topBarMatch) {
        console.log(`  [FAIL] ${page}: 未找到 .top-bar 区域`);
        fail++;
        continue;
    }

    const topBarHtml = topBarMatch[1];

    // 检查关键元素
    const hasBrandIcon = /class="bi bi-droplet-half brand-icon"/.test(topBarHtml);
    const hasBrandText = /class="brand-text">金智住宿管理系统</.test(topBarHtml);
    const hasBrandVersion = /class="brand-version">v2\.12\./.test(topBarHtml);
    const hasUserPill = /class="user-pill"/.test(topBarHtml);
    const hasBtnExit = /class="btn-exit"/.test(topBarHtml);
    const hasAdminLabel = />管理员</.test(topBarHtml);

    // 提取品牌名称位置（应该在左侧 brand-icon 之后）
    const brandTextPos = topBarHtml.indexOf('金智住宿管理系统');
    const exitBtnPos = topBarHtml.indexOf('btn-exit');

    const allOk = hasBrandIcon && hasBrandText && hasBrandVersion && hasUserPill && hasBtnExit && hasAdminLabel;
    const orderOk = brandTextPos > 0 && exitBtnPos > brandTextPos;

    if (allOk && orderOk) {
        pass++;
        console.log(`  [PASS] ${page.padEnd(35)} | brand=✓ user-pill=✓ exit=✓`);
    } else {
        fail++;
        console.log(`  [FAIL] ${page.padEnd(35)} | brand-icon=${hasBrandIcon} brand-text=${hasBrandText} version=${hasBrandVersion} user=${hasUserPill} exit=${hasBtnExit} order=${orderOk}`);
    }
}

console.log('\n' + '='.repeat(70));
console.log(`总计：通过 ${pass}/${PAGES.length}，失败 ${fail}`);
console.log('='.repeat(70));