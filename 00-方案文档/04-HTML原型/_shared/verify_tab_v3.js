// 通过 URL 参数预设 localStorage 后查看 Tab 渲染
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

// 创建一个测试 HTML，在加载前预设 localStorage
function makeTestHtml(targetPage, presetTabs) {
  const html = `<!DOCTYPE html>
<html><head><meta charset="UTF-8"><title>Tab 测试</title></head>
<body>
<script>
const tabs = ${JSON.stringify(presetTabs)};
localStorage.setItem('dormmanage:tabs:v1:admin', JSON.stringify({
  activeTabId: tabs[tabs.length - 1].id,
  tabs: tabs
}));
window.location.href = '/${targetPage}';
</script>
</body></html>`;
  return html;
}

// 测试场景：预设 4 个 Tab，验证渲染
const presetTabs = [
  { id: 'tab-001', title: '首页', url: 'index.html', module: 'index', icon: 'bi-speedometer2' },
  { id: 'tab-002', title: '住宿管理', url: 'dorms/list.html', module: 'dorms', icon: 'bi-building' },
  { id: 'tab-003', title: '人员清单', url: 'personnel/list.html', module: 'personnel', icon: 'bi-people-fill' },
  { id: 'tab-004', title: '智能抄表', url: 'meter/index.html', module: 'meter', icon: 'bi-clipboard-data' }
];

const tmpHtml = 'C:\\Users\\Mecall\\AppData\\Local\\Temp\\tab-test.html';
fs.writeFileSync(tmpHtml, makeTestHtml('dorms/list.html', presetTabs));

// 把测试文件复制到原型目录（用 Python http server 提供）
const targetHtml = 'E:\\AI工作目录\\AI编程开发\\JINGE开发\\住宿管理系统\\00-方案文档\\04-HTML原型\\_shared\\_tab_test.html';
fs.writeFileSync(targetHtml, makeTestHtml('dorms/list.html', presetTabs));

const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-tab-test2" --virtual-time-budget=5000 --dump-dom "http://localhost:8765/_shared/_tab_test.html" 2>nul`;

const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 重定向后查看 dorms/list.html 的 DOM
console.log('=== Tab 渲染验证（预设 4 个 Tab，当前为住宿管理）===\n');

// 解析 Tab 项
const tabRegex = /<div class="tab-item[^"]*"[^>]*data-url="([^"]*)"[^>]*data-tab-id="([^"]*)"[^>]*>([\s\S]*?)<\/div>/g;
let m;
let count = 0;
while ((m = tabRegex.exec(output)) !== null) {
  count++;
  const isActive = m[0].includes('class="tab-item active');
  const iconMatch = m[3].match(/bi-([\w-]+)/);
  const titleMatch = m[3].match(/<span class="tab-title">([^<]+)<\/span>/);
  const hasClose = m[3].includes('tab-close') || m[3].includes('bi-x');

  console.log(`  [Tab ${count}] ${isActive ? '🟢 激活' : '⚪ 静态'} | ${titleMatch ? titleMatch[1] : '?'} | url="${m[1]}" | 图标=${iconMatch ? iconMatch[1] : '?'} | 关闭按钮=${hasClose ? '❌存在' : '✅已隐藏'}`);
}

console.log(`\n  Tab 总数：${count}`);
console.log(`  关闭按钮：${output.includes('tab-close') ? '❌存在' : '✅全部隐藏'}`);
console.log(`  + 号按钮：${output.includes('class="tab-add"') ? '❌存在' : '✅已隐藏'}`);

// 清理临时文件
try { fs.unlinkSync(targetHtml); } catch (e) {}