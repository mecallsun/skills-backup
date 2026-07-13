// v2.12.4 验证：所有筛选区强制单行 + flex-nowrap + filter-card + filter-item
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

const PAGES_WITH_FILTERS = [
  'http://localhost:8765/billing/dorm-bills.html',
  'http://localhost:8765/billing/employee-bills.html',
  'http://localhost:8765/billing/standards.html',
  'http://localhost:8765/booking/index.html',
  'http://localhost:8765/dorms/list.html',
  'http://localhost:8765/meter/index.html'
];

const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-filter-${Date.now()}`;

let pass = 0, fail = 0;

console.log('='.repeat(70));
console.log('v2.12.4 验证：筛选区强制单行排列规则');
console.log('='.repeat(70));

for (const url of PAGES_WITH_FILTERS) {
  const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=5000 --dump-dom "${url}" 2>nul`;
  const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

  // 提取 form class
  const formMatch = output.match(/<form\s+([^>]*)>/);
  const formAttrs = formMatch ? formMatch[1] : '';

  // 检查项
  const hasFlexNowrap = formAttrs.includes('flex-nowrap');
  const hasFilterRow = formAttrs.includes('filter-row');
  const hasFilterCard = /class="filter-card(?:\s+[^"]*)?"/.test(output);
  const noColMd = !/col-md-\d/.test(output);
  const hasFilterBtn = output.includes('filter-btn');
  const hasQueryBtn = output.includes('查询');
  const hasResetBtn = output.includes('重置');
  const noWrap = !/col-md-\d/.test(output);

  const allOk = hasFlexNowrap && hasFilterRow && hasFilterCard && noColMd && hasFilterBtn && hasQueryBtn && hasResetBtn && noWrap;
  if (allOk) pass++; else fail++;

  const status = allOk ? '[PASS]' : '[FAIL]';
  const relUrl = url.replace('http://localhost:8765/', '').padEnd(30);
  console.log(`\n${status} ${relUrl}`);
  console.log(`  form class: ${formAttrs.split('onsubmit')[0].trim()}`);
  console.log(`  flex-nowrap: ${hasFlexNowrap ? 'OK' : 'FAIL'}`);
  console.log(`  filter-row: ${hasFilterRow ? 'OK' : 'FAIL'}`);
  console.log(`  filter-card: ${hasFilterCard ? 'OK' : 'FAIL'}`);
  console.log(`  filter-btn: ${hasFilterBtn ? 'OK' : 'FAIL'}`);
  console.log(`  无 col-md-*: ${noColMd ? 'OK' : 'FAIL'}`);
  console.log(`  强制单行: ${noWrap ? 'OK' : 'FAIL'}`);
}

console.log('\n' + '='.repeat(70));
console.log(`总计：通过 ${pass}/${PAGES_WITH_FILTERS.length}，失败 ${fail}`);
console.log(fail === 0 ? '\n[OK] 全部筛选区符合强制单行规则！' : '\n[FAIL] 存在违规');