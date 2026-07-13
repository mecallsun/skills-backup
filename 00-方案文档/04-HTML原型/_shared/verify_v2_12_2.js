// v2.12.2 全面验证脚本 — 固定 Tab 列表、URL 推断激活、禁止关闭
const { execSync } = require('child_process');
const fs = require('fs');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

// 期望的 10 个固定 Tab（顺序必须一致）
const EXPECTED_TABS = [
  { id: 'tab-home',          title: '首页',       module: 'index',          url: 'index.html' },
  { id: 'tab-booking',       title: '办理登记',   module: 'booking',        url: 'booking/index.html' },
  { id: 'tab-dorms',         title: '宿舍管理',   module: 'dorms',          url: 'dorms/list.html' },
  { id: 'tab-personnel',     title: '人员清单',   module: 'personnel',      url: 'personnel/list.html' },
  { id: 'tab-billing',       title: '费用标准',   module: 'billing',        url: 'billing/standards.html' },
  { id: 'tab-dorm-bills',    title: '宿舍账单',   module: 'dorm-bills',     url: 'billing/dorm-bills.html' },
  { id: 'tab-employee-bills',title: '员工账单',   module: 'employee-bills', url: 'billing/employee-bills.html' },
  { id: 'tab-meter',         title: '抄表记录',   module: 'meter',          url: 'meter/index.html' },
  { id: 'tab-basics',        title: '基础资料',   module: 'basics',         url: 'basics/index.html' },
  { id: 'tab-settings',      title: '系统设置',   module: 'settings',       url: 'settings/index.html' }
];

// 测试页面（涵盖所有 10 个一级菜单 + 部分子页面）
const TEST_PAGES = [
  { url: 'http://localhost:8765/index.html',                       expectedActive: 'tab-home',          expectedActiveTitle: '首页' },
  { url: 'http://localhost:8765/booking/index.html',               expectedActive: 'tab-booking',       expectedActiveTitle: '办理登记' },
  { url: 'http://localhost:8765/booking/check-in.html',            expectedActive: 'tab-booking',       expectedActiveTitle: '办理登记' },
  { url: 'http://localhost:8765/booking/edit.html',                expectedActive: 'tab-booking',       expectedActiveTitle: '办理登记' },
  { url: 'http://localhost:8765/dorms/list.html',                  expectedActive: 'tab-dorms',         expectedActiveTitle: '宿舍管理' },
  { url: 'http://localhost:8765/dorms/create.html',                expectedActive: 'tab-dorms',         expectedActiveTitle: '宿舍管理' },
  { url: 'http://localhost:8765/dorms/details.html',               expectedActive: 'tab-dorms',         expectedActiveTitle: '宿舍管理' },
  { url: 'http://localhost:8765/dorms/history.html',               expectedActive: 'tab-dorms',         expectedActiveTitle: '宿舍管理' },
  { url: 'http://localhost:8765/personnel/list.html',              expectedActive: 'tab-personnel',     expectedActiveTitle: '人员清单' },
  { url: 'http://localhost:8765/personnel/create.html',            expectedActive: 'tab-personnel',     expectedActiveTitle: '人员清单' },
  { url: 'http://localhost:8765/billing/standards.html',           expectedActive: 'tab-billing',       expectedActiveTitle: '费用标准' },
  { url: 'http://localhost:8765/billing/standard-form.html',       expectedActive: 'tab-billing',       expectedActiveTitle: '费用标准' },
  { url: 'http://localhost:8765/billing/dorm-bills.html',          expectedActive: 'tab-dorm-bills',    expectedActiveTitle: '宿舍账单' },
  { url: 'http://localhost:8765/billing/employee-bills.html',      expectedActive: 'tab-employee-bills',expectedActiveTitle: '员工账单' },
  { url: 'http://localhost:8765/meter/index.html',                 expectedActive: 'tab-meter',         expectedActiveTitle: '抄表记录' },
  { url: 'http://localhost:8765/meter/detail.html',                expectedActive: 'tab-meter',         expectedActiveTitle: '抄表记录' },
  { url: 'http://localhost:8765/basics/index.html',                expectedActive: 'tab-basics',        expectedActiveTitle: '基础资料' },
  { url: 'http://localhost:8765/settings/index.html',              expectedActive: 'tab-settings',      expectedActiveTitle: '系统设置' }
];

const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-v2122-${Date.now()}`;

let pass = 0, fail = 0;
const failures = [];

console.log('='.repeat(70));
console.log('v2.12.2 全面验证：Tab 固定菜单切换模式');
console.log('='.repeat(70));

for (const page of TEST_PAGES) {
  const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=5000 --dump-dom "${page.url}" 2>nul`;
  const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

  // 1. 检查 Tab 数量必须是 10
  const tabRegex = /<div class="tab-item[^"]*"[^>]*>/g;
  const tabMatches = output.match(tabRegex) || [];
  const tabCount = tabMatches.length;

  // 2. 提取激活 Tab
  const activeMatch = output.match(/<div class="tab-item active[^"]*"[^>]*data-tab-id="([^"]*)"[^>]*>([\s\S]*?)<\/div>/);
  const activeId = activeMatch ? activeMatch[1] : 'NONE';
  const activeTitleMatch = activeMatch ? activeMatch[2].match(/<span class="tab-title">([^<]+)<\/span>/) : null;
  const activeTitle = activeTitleMatch ? activeTitleMatch[1] : 'NONE';

  // 3. 检查关闭按钮和 + 号（仅检查 Tab 栏内的关闭按钮，不包括业务按钮）
  const tabBarMatch = output.match(/<div class="tab-bar"[^>]*>([\s\S]*?)<\/div>\s*<\/div>/);
  const tabBarContent = tabBarMatch ? tabBarMatch[1] : '';
  const hasClose = /class="tab-close"/.test(tabBarContent);
  const hasTabAdd = /class="tab-add"/.test(tabBarContent);

  // 4. 验证
  const countOk = tabCount === 10;
  const activeOk = activeId === page.expectedActive;
  const closeOk = !hasClose;
  const addOk = !hasTabAdd;
  const allOk = countOk && activeOk && closeOk && addOk;

  if (allOk) pass++; else fail++;
  if (!allOk) failures.push({ page: page.url, tabCount, activeId, activeTitle, expectedActive: page.expectedActive, hasClose, hasTabAdd });

  const status = allOk ? '✅' : '❌';
  console.log(`${status} ${page.url.replace('http://localhost:8765/', '').padEnd(35)} tabs=${tabCount} active=${activeTitle}(${activeId})`);
}

console.log('\n' + '='.repeat(70));
console.log(`总计：通过 ${pass}/${TEST_PAGES.length}，失败 ${fail}`);

if (failures.length > 0) {
  console.log('\n失败详情：');
  failures.forEach(f => {
    console.log(`  ${f.page}`);
    console.log(`    Tab数: ${f.tabCount} (期望 10)`);
    console.log(`    激活: ${f.activeTitle}(${f.activeId}) (期望 ${f.expectedActive})`);
    console.log(`    关闭按钮: ${f.hasClose ? '存在 ❌' : '隐藏 ✅'}`);
    console.log(`    +号按钮: ${f.hasTabAdd ? '存在 ❌' : '隐藏 ✅'}`);
  });
  process.exit(1);
}

console.log('\n🎉 全部通过！');