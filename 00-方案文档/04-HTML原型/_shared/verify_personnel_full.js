// 验证手机号筛选 + 离职日期显示
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-pfull-${Date.now()}`;

console.log('='.repeat(70));
console.log('验证: 离职日期 + 手机号筛选');
console.log('='.repeat(70));

// 直接 dump-dom 测试页面
const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "http://localhost:8765/personnel/list.html" 2>nul`;
const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

// 提取筛选函数测试（JS 执行后渲染的 tbody）
const tbodyMatch = output.match(/<tbody[^>]*>([\s\S]*?)<\/tbody>/);
if (tbodyMatch) {
    const rows = tbodyMatch[1].match(/<tr>([\s\S]*?)<\/tr>/g) || [];
    console.log(`\n默认列表行数：${rows.length}`);

    // 统计离职日期分布
    let withLeaveDate = 0, withoutLeaveDate = 0;
    rows.forEach(row => {
        const cells = row.match(/<td[^>]*>([\s\S]*?)<\/td>/g) || [];
        if (cells.length >= 11) {
            const leaveDate = cells[10].replace(/<[^>]+>/g, '').trim();
            if (leaveDate && leaveDate !== '-') {
                withLeaveDate++;
                if (withLeaveDate <= 3) {
                    const cleaned = cells.map(c => c.replace(/<[^>]+>/g, '').trim().replace(/\s+/g, ' '));
                    console.log(`  [离职] ${cleaned[2]} | 工号 ${cleaned[1]} | 离职日期 ${cleaned[10]}`);
                }
            } else {
                withoutLeaveDate++;
            }
        }
    });
    console.log(`\n本页：${withLeaveDate} 条有离职日期，${withoutLeaveDate} 条无离职日期`);
}

// 验证筛选条件标签已更新
console.log('\n--- 筛选条件 ---');
const labelRegex = /<label[^>]*>(姓名\/工号\/手机号)<\/label>/;
const labelMatch = output.match(labelRegex);
if (labelMatch) {
    console.log(`✅ 筛选标签：${labelMatch[1]}`);
} else {
    console.log('❌ 筛选标签未找到');
}

const placeholderRegex = /<input[^>]*id="fKw"[^>]*placeholder="([^"]+)"/;
const placeholderMatch = output.match(placeholderRegex);
if (placeholderMatch) {
    console.log(`✅ 占位符：${placeholderMatch[1]}`);
}

console.log('\n' + '='.repeat(70));