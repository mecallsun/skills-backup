// 直接输出 dump-dom 到文件，分析 Tab 渲染
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
const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-tab-test5" --virtual-time-budget=5000 --dump-dom "http://localhost:8765/dorms/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 输出 dump 到文件
const dumpFile = 'C:\\Users\\Mecall\\AppData\\Local\\Temp\\tab-dump.html';
fs.writeFileSync(dumpFile, output);

// 恢复原文件
fs.copyFileSync(backupFile, targetFile);
fs.unlinkSync(backupFile);

// 提取 tab-bar 区域
const tabBarMatch = output.match(/<div class="tab-bar"[^>]*>([\s\S]*?)<\/div>\s*<\/div>/);
console.log('=== Tab 栏区域 ===\n');
if (tabBarMatch) {
  const tabBarHtml = tabBarMatch[0];

  // 用更简单的正则：匹配每个 div.tab-item 的开始标签（不一定跨多行）
  const tabStartRegex = /<div class="tab-item[^"]*"[^>]*>/g;
  const starts = [];
  let m;
  while ((m = tabStartRegex.exec(tabBarHtml)) !== null) {
    starts.push({ idx: m.index, html: m[0] });
  }

  console.log(`  Tab 项起始标签数量：${starts.length}\n`);

  starts.forEach((s, i) => {
    const isActive = s.html.includes('class="tab-item active');
    const urlMatch = s.html.match(/data-url="([^"]*)"/);
    const idMatch = s.html.match(/data-tab-id="([^"]*)"/);
    const moduleMatch = s.html.match(/data-module="([^"]*)"/);

    // 提取标题（从该 Tab 的内容中找 tab-title）
    const contentStart = s.idx + s.html.length;
    const contentEnd = i + 1 < starts.length ? starts[i + 1].idx : tabBarHtml.length;
    const tabContent = tabBarHtml.substring(contentStart, contentEnd);
    const titleMatch = tabContent.match(/<span class="tab-title">([^<]+)<\/span>/);
    const iconMatch = tabContent.match(/bi-([\w-]+)/);

    console.log(`  [Tab ${i + 1}] ${isActive ? '🟢 激活' : '⚪ 默认'} | ${(titleMatch ? titleMatch[1] : '?').padEnd(8)} | url="${urlMatch ? urlMatch[1] : '?'}" | 模块=${moduleMatch ? moduleMatch[1] : '?'} | 图标=${iconMatch ? iconMatch[1] : '?'}`);
  });

  console.log(`\n  关闭按钮：${tabBarHtml.includes('tab-close') ? '❌ 存在' : '✅ 已隐藏'}`);
  console.log(`  + 号按钮：${tabBarHtml.includes('class="tab-add"') ? '❌ 存在' : '✅ 已隐藏'}`);
} else {
  console.log('  ❌ 未找到 tab-bar 区域');
}