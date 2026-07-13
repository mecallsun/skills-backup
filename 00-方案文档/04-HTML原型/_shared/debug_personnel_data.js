// 检查人员清单列表实际渲染的数据
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-personnel-data-${Date.now()}`;

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=5000 --dump-dom "http://localhost:8765/personnel/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

console.log('='.repeat(70));
console.log('Diagnose: 人员清单列表数据渲染');
console.log('='.repeat(70));

// 提取 tbody 内容
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
  const tbody = tbodyMatch[1];

  // 统计行数
  const rows = tbody.match(/<tr>/g) || [];
  console.log(`\n数据行数：${rows.length}`);

  // 提取每行内容
  const rowMatches = tbody.match(/<tr>([\s\S]*?)<\/tr>/g) || [];
  console.log(`\n前 5 行内容：\n`);
  rowMatches.slice(0, 5).forEach((row, i) => {
    const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
    const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim());
    console.log(`[${i + 1}] ${cleaned.join(' | ')}`);
  });
}

// 检查每列的具体内容（看是否有问题）
console.log('\n\n--- 列数据完整性检查 ---');
const totalCountMatch = output.match(/id="totalCount">(\d+)</);
if (totalCountMatch) {
  console.log(`总数显示：${totalCountMatch[1]}`);
}

// 检查 Badge 渲染
const badges = output.match(/<span class="badge[^"]*">[^<]+<\/span>/g) || [];
console.log(`Badge 数量：${badges.length}`);
const badgeTypes = {};
badges.forEach(b => {
  const m = b.match(/badge\s+([^"]+)">([^<]+)/);
  if (m) {
    const key = m[1].split(' ')[0];
    badgeTypes[key] = (badgeTypes[key] || 0) + 1;
  }
});
console.log('Badge 类型分布：');
for (const [k, v] of Object.entries(badgeTypes)) {
  console.log(`  ${k}: ${v}`);
}

// 检查是否有"undefined"或"null"显示在列表中（数据问题指示器）
const dataIssues = [];
if (output.includes('undefined')) dataIssues.push('"undefined" 出现在页面');
if (output.includes('null,')) dataIssues.push('"null," 出现在列表');
if (output.includes('[object Object]')) dataIssues.push('"[object Object]" 出现');

if (dataIssues.length > 0) {
  console.log('\n❌ 数据问题：');
  dataIssues.forEach(d => console.log(`  - ${d}`));
} else {
  console.log('\n✅ 无明显数据问题');
}

// 检查 Mock 数据加载
const mockLoaded = output.includes('钱鹏') || output.includes('常宇航') || output.includes('EMP-2026-001');
console.log(`\nMock 数据加载：${mockLoaded ? '✅' : '❌'}`);

console.log('\n' + '='.repeat(70));