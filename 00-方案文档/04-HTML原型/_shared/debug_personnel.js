// 深度诊断 personnel/list.html
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-personnel-${Date.now()}`;

function dump(url) {
  const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=5000 --dump-dom "${url}" 2>nul`;
  return execSync(cmd, { encoding: 'utf-8', timeout: 15000 });
}

const divider = '='.repeat(70);
console.log(divider);
console.log('Diagnose: personnel/list.html Tab switch');
console.log(divider);

// 直接访问 personnel/list.html
console.log('\n--- 1. Direct visit personnel/list.html ---');
{
  const output = dump('http://localhost:8765/personnel/list.html');

  const checks = {
    'top-bar': output.includes('class="top-bar"'),
    'tab-bar': output.includes('class="tab-bar"'),
    'page-content': output.includes('class="page-content"'),
    'filter-card': /class="filter-card/.test(output),
    'filter-row': output.includes('filter-row'),
    'filter-btn': output.includes('filter-btn'),
    'query-btn-text': output.includes('查询'),
    'reset-btn-text': output.includes('重置'),
    'content-card': output.includes('class="content-card"'),
    'table': /<table\s+class="table/.test(output),
    'tbody': output.includes('<tbody'),
    'pager': output.includes('pager'),
    'personnel-tab-active': /class="tab-item active[^"]*"[^>]*data-module="personnel"/.test(output)
  };

  for (const [k, v] of Object.entries(checks)) {
    console.log(`  ${v ? '[OK]  ' : '[FAIL]'} ${k}`);
  }

  const formMatch = output.match(/<form\s+([^>]*)>/);
  if (formMatch) {
    console.log(`\n  form attrs: ${formMatch[1].substring(0, 150)}`);
  }

  const contentMatch = output.match(/<div class="page-content">([\s\S]*?)<script/);
  if (contentMatch) {
    const content = contentMatch[1];
    console.log(`\n  page-content chars: ${content.length}`);
    console.log(`  has filter-card: ${content.includes('filter-card')}`);
    console.log(`  has content-card: ${content.includes('content-card')}`);
    console.log(`  has table: ${/<table/.test(content)}`);
  } else {
    console.log('\n  [WARN] page-content not found or empty');
  }
}

// 检查关键 JS 引用是否正确
console.log('\n--- 2. Check JS references ---');
{
  const output = dump('http://localhost:8765/personnel/list.html');
  const refs = output.match(/<script\s+src="[^"]*"/g) || [];
  refs.forEach(r => console.log(`  ${r}`));
}

// 检查 CSS 引用
console.log('\n--- 3. Check CSS references ---');
{
  const output = dump('http://localhost:8765/personnel/list.html');
  const refs = output.match(/<link[^>]*stylesheet[^>]*>/g) || [];
  refs.forEach(r => console.log(`  ${r}`));
}

// 检查 tab-bar 区域
console.log('\n--- 4. Inspect tab-bar ---');
{
  const output = dump('http://localhost:8765/personnel/list.html');
  const tabBarMatch = output.match(/<div class="tab-bar"[^>]*>([\s\S]*?)<\/div>\s*<\/div>/);
  if (tabBarMatch) {
    const items = (tabBarMatch[1].match(/<div class="tab-item[^"]*"[^>]*data-module="([^"]*)"[^>]*data-url="([^"]*)"[^>]*>/g) || []);
    items.forEach((item, i) => {
      const m = item.match(/data-module="([^"]*)"[^>]*data-url="([^"]*)"/);
      const active = item.includes('class="tab-item active');
      console.log(`  [Tab ${i+1}] ${active ? 'ACTIVE' : '      '} module=${m[1]} url=${m[2]}`);
    });
  }
}

console.log('\n' + divider);