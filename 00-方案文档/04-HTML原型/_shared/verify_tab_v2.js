// 验证 Tab 改造为固定菜单模式
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

(async () => {
  // 测试多页面依次访问，验证 Tab 累积
  const pages = [
    'http://localhost:8765/index.html',
    'http://localhost:8765/dorms/list.html',
    'http://localhost:8765/personnel/list.html',
    'http://localhost:8765/meter/index.html'
  ];

  // 使用同一个 user data dir 模拟持久化会话
  const userDataDir = 'C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-tab-test';

  for (const url of pages) {
    const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=3000 --dump-dom "${url}" 2>nul`;
    const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

    // 检查 Tab 项数量、关闭按钮、+ 号
    const tabCountMatch = output.match(/class="tab-item[^"]*"/g) || [];
    const tabCount = tabCountMatch.length;
    const hasCloseButton = output.includes('tab-close') || output.includes('bi-x');
    const hasTabAdd = output.includes('class="tab-add"') || output.includes('id="tabAdd"');

    console.log(`\n=== ${url.split('/').slice(-2).join('/')} ===`);
    console.log(`  Tab 数量：${tabCount}`);
    console.log(`  关闭按钮：${hasCloseButton ? '❌ 仍存在' : '✅ 已隐藏'}`);
    console.log(`  + 号按钮：${hasTabAdd ? '❌ 仍存在' : '✅ 已隐藏'}`);

    // 列出所有 Tab 的标题
    const tabTitles = [];
    const tabRegex = /<div class="tab-item[^"]*"[^>]*data-url="([^"]*)"[^>]*>[\s\S]*?<span class="tab-title">([^<]+)<\/span>/g;
    let m;
    while ((m = tabRegex.exec(output)) !== null) {
      tabTitles.push(`${m[2]} → ${m[1]}`);
    }
    tabTitles.forEach((t, i) => console.log(`  [Tab ${i + 1}] ${t}`));
  }

  console.log('\n=== 验证完成 ===');
})();