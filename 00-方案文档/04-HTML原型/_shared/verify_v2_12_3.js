// v2.12.3 验证脚本 — 三层架构 + 无图标导航条 + Tab 正常显示
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

const PAGES = [
  'http://localhost:8765/index.html',
  'http://localhost:8765/booking/index.html',
  'http://localhost:8765/booking/check-in.html',
  'http://localhost:8765/booking/edit.html',
  'http://localhost:8765/dorms/list.html',
  'http://localhost:8765/dorms/create.html',
  'http://localhost:8765/dorms/details.html',
  'http://localhost:8765/dorms/history.html',
  'http://localhost:8765/personnel/list.html',
  'http://localhost:8765/personnel/create.html',
  'http://localhost:8765/billing/standards.html',
  'http://localhost:8765/billing/standard-form.html',
  'http://localhost:8765/billing/dorm-bills.html',
  'http://localhost:8765/billing/employee-bills.html',
  'http://localhost:8765/meter/index.html',
  'http://localhost:8765/meter/detail.html',
  'http://localhost:8765/basics/index.html',
  'http://localhost:8765/settings/index.html'
];

const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2123-${Date.now()}`;

let pass = 0, fail = 0;

console.log('='.repeat(70));
console.log('v2.12.3 验证：三层架构（无图标导航条 + Tab 栏正常）');
console.log('='.repeat(70));

for (const url of PAGES) {
  const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=5000 --dump-dom "${url}" 2>nul`;
  const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

  // 检查项：
  // 1. 不应有 icon-rail（图标导航条）
  const hasIconRail = output.includes('class="icon-rail"') || output.includes('id="icon-rail"');
  // 2. 应有 Tab 栏且包含 10 个 Tab
  const tabCount = (output.match(/<div class="tab-item[^"]*"/g) || []).length;
  // 3. 应有 top-bar
  const hasTopBar = output.includes('class="top-bar"');
  // 4. 应有 page-content
  const hasPageContent = output.includes('class="page-content"');

  const allOk = !hasIconRail && tabCount === 10 && hasTopBar && hasPageContent;

  if (allOk) pass++; else fail++;

  const status = allOk ? '✅' : '❌';
  const relUrl = url.replace('http://localhost:8765/', '').padEnd(30);
  console.log(`${status} ${relUrl} icon-rail=${hasIconRail ? '存在 ❌' : '✅已删除'} tabs=${tabCount}/10 top=${hasTopBar ? '✓' : '✗'} content=${hasPageContent ? '✓' : '✗'}`);
}

console.log('\n' + '='.repeat(70));
console.log(`总计：通过 ${pass}/${PAGES.length}，失败 ${fail}`);
console.log(fail === 0 ? '\n🎉 全部通过！' : '\n❌ 存在失败页面');