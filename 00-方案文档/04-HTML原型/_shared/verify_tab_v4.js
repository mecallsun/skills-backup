// 直接修改 dorms/list.html 临时插入预设 localStorage 的脚本
const { execSync } = require('child_process');
const fs = require('fs');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

// 备份原文件
const targetFile = 'E:\\AI工作目录\\AI编程开发\\JINGE开发\\宿舍管理系统\\00-方案文档\\04-HTML原型\\dorms\\list.html';
const backupFile = targetFile + '.bak';
if (!fs.existsSync(backupFile)) {
  fs.copyFileSync(targetFile, backupFile);
}

// 读取原文件，在 <head> 中注入预设脚本
let content = fs.readFileSync(targetFile, 'utf-8');
const presetScript = `<script>
localStorage.setItem('dormmanage:tabs:v1:admin', JSON.stringify({
  activeTabId: 'tab-004',
  tabs: [
    { id: 'tab-001', title: '首页', url: 'index.html', module: 'index', icon: 'bi-speedometer2' },
    { id: 'tab-002', title: '办理登记', url: 'booking/index.html', module: 'booking', icon: 'bi-clipboard-check' },
    { id: 'tab-003', title: '宿舍管理', url: 'dorms/list.html', module: 'dorms', icon: 'bi-building' },
    { id: 'tab-004', title: '人员清单', url: 'personnel/list.html', module: 'personnel', icon: 'bi-people-fill' },
    { id: 'tab-005', title: '抄表记录', url: 'meter/index.html', module: 'meter', icon: 'bi-clipboard-data' }
  ]
}));
</script>`;

content = content.replace('</head>', presetScript + '\n</head>');
fs.writeFileSync(targetFile, content);

// 测试
const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-tab-test3" --virtual-time-budget=5000 --dump-dom "http://localhost:8765/dorms/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

console.log('=== Tab 渲染验证（预设 5 个 Tab，当前为宿舍管理，激活"人员清单"）===\n');

const tabRegex = /<div class="tab-item[^"]*"[^>]*data-url="([^"]*)"[^>]*data-tab-id="([^"]*)"[^>]*>([\s\S]*?)<\/div>\s*(?=<div class="tab-item|<\/div>\s*<\/div>)/g;
let m;
let count = 0;
while ((m = tabRegex.exec(output)) !== null) {
  count++;
  const isActive = m[0].includes('class="tab-item active');
  const iconMatch = m[3].match(/bi-([\w-]+)/);
  const titleMatch = m[3].match(/<span class="tab-title">([^<]+)<\/span>/);
  const hasClose = m[3].includes('tab-close');

  console.log(`  [Tab ${count}] ${isActive ? '🟢 激活' : '⚪ 默认'} | ${titleMatch ? titleMatch[1].padEnd(8) : '?'} | url="${m[1]}" | 图标=${iconMatch ? iconMatch[1] : '?'} | 关闭按钮=${hasClose ? '❌存在' : '✅已隐藏'}`);
}

console.log(`\n  Tab 总数：${count}`);
console.log(`  关闭按钮：${output.includes('tab-close') ? '❌存在' : '✅全部隐藏'}`);
console.log(`  + 号按钮：${output.includes('class="tab-add"') ? '❌存在' : '✅已隐藏'}`);

// 恢复原文件
fs.copyFileSync(backupFile, targetFile);
fs.unlinkSync(backupFile);
console.log('\n  ✅ 已恢复原 dorms/list.html');