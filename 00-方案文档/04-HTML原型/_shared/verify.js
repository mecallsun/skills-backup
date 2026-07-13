// Headless Chrome 验证脚本 — 检查所有页面的挂载和链接
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const PAGES = [
  'index.html',
  'basics/index.html',
  'billing/dorm-bills.html',
  'billing/employee-bills.html',
  'billing/standard-form.html',
  'billing/standards.html',
  'booking/check-in.html',
  'booking/check-out.html',
  'booking/edit.html',
  'booking/index.html',
  'dorms/create.html',
  'dorms/details.html',
  'dorms/edit.html',
  'dorms/history.html',
  'dorms/list.html',
  'meter/detail.html',
  'meter/edit.html',
  'meter/entry.html',
  'meter/import.html',
  'meter/index.html',
  'personnel/create.html',
  'personnel/edit.html',
  'personnel/import.html',
  'personnel/list.html',
  'settings/index.html'
];

const BASE_URL = 'http://localhost:8765/';

(async () => {
  const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
  const results = [];

  for (const page of PAGES) {
    const url = BASE_URL + page;
    const tmpFile = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-test-${Date.now()}.json`;

    // 使用 chrome --dump-dom 抓取渲染后的 DOM
    const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --virtual-time-budget=3000 --dump-dom "${url}" 2>nul`;

    try {
      const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

      // 检查关键标识
      const hasIconRail = output.includes('class="icon-rail"') || output.includes('class="icon-rail ');
      const hasTabBar = output.includes('class="tab-bar"') || output.includes('class="tab-bar ');
      const hasTopBar = output.includes('class="top-bar"') || output.includes('class="top-bar ');
      const hasPageContent = output.includes('class="page-content"') || output.includes('class="page-content ');
      const hasErrors = output.includes('JavaScript Error') || output.includes('Uncaught') || output.includes('SyntaxError');

      // 提取 icon-rail 链接（检查链接格式）
      const linkMatch = output.match(/href="([^"]*dorms\/list\.html[^"]*)"/);
      const sampleLink = linkMatch ? linkMatch[1] : '';

      const ok = hasIconRail && hasTabBar && hasTopBar && hasPageContent && !hasErrors;
      const expectedLink = page === 'index.html' ? 'dorms/list.html' : '../dorms/list.html';
      const linkOk = sampleLink === expectedLink;

      const status = ok && linkOk ? 'PASS' : 'FAIL';
      results.push({ page, status, hasIconRail, hasTabBar, hasTopBar, hasPageContent, sampleLink, expectedLink, linkOk });

      console.log(`  [${status}] ${page} | rail=${hasIconRail} tab=${hasTabBar} top=${hasTopBar} content=${hasPageContent} link="${sampleLink}" expect="${expectedLink}"`);
    } catch (e) {
      console.log(`  [ERR ] ${page}: ${e.message.substring(0, 100)}`);
      results.push({ page, status: 'ERROR', error: e.message.substring(0, 100) });
    }
  }

  const pass = results.filter(r => r.status === 'PASS').length;
  const fail = results.filter(r => r.status !== 'PASS').length;

  console.log(`\n=== 总计 ===`);
  console.log(`通过：${pass}/${results.length}`);
  console.log(`失败：${fail}`);

  // 输出失败详情
  if (fail > 0) {
    console.log('\n=== 失败详情 ===');
    results.filter(r => r.status !== 'PASS').forEach(r => {
      console.log(`  ${r.page}: ${JSON.stringify(r)}`);
    });
  }
})();