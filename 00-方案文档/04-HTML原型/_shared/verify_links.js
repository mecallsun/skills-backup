// 验证所有 icon-rail 链接是否正确（首页 vs 子页面）
const { execSync } = require('child_process');

const SAMPLE_PAGES = [
  { url: 'http://localhost:8765/index.html', isRoot: true, name: '首页' },
  { url: 'http://localhost:8765/dorms/list.html', isRoot: false, name: '住宿管理' },
  { url: 'http://localhost:8765/billing/dorm-bills.html', isRoot: false, name: '住宿账单' },
  { url: 'http://localhost:8765/personnel/list.html', isRoot: false, name: '人员清单' }
];

const EXPECTED_LINKS = [
  { key: 'index', url: 'index.html' },
  { key: 'booking', url: 'booking/index.html' },
  { key: 'dorms', url: 'dorms/list.html' },
  { key: 'personnel', url: 'personnel/list.html' },
  { key: 'billing', url: 'billing/standards.html' },
  { key: 'dorm-bills', url: 'billing/dorm-bills.html' },
  { key: 'employee-bills', url: 'billing/employee-bills.html' },
  { key: 'meter', url: 'meter/index.html' },
  { key: 'basics', url: 'basics/index.html' },
  { key: 'settings', url: 'settings/index.html' }
];

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

(async () => {
  for (const sample of SAMPLE_PAGES) {
    const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --virtual-time-budget=3000 --dump-dom "${sample.url}" 2>nul`;
    const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

    console.log(`\n=== ${sample.name} (${sample.isRoot ? '首页' : '子页面'}) ===`);
    let allOk = true;

    for (const expected of EXPECTED_LINKS) {
      const expectedHref = sample.isRoot ? expected.url : '../' + expected.url;
      // 匹配 href 包含目标 URL 的 a 标签
      const pattern = new RegExp(`data-module="${expected.key}"[^>]*href="([^"]*)"`, 'i');
      const match = output.match(pattern);
      const actualHref = match ? match[1] : 'NOT_FOUND';

      const ok = actualHref === expectedHref;
      if (!ok) allOk = false;

      const status = ok ? '✓' : '✗';
      console.log(`  ${status} ${expected.key.padEnd(15)} href="${actualHref}" (期望: "${expectedHref}")`);
    }

    console.log(`  ${allOk ? '✅' : '❌'} ${allOk ? '所有链接正确' : '存在错误链接'}`);
  }
})();