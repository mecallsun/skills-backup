// v2.12.14 验证：新增/编辑人员表单 BUG 修复 + 布局优化
const { execSync } = require('child_process');

const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const userDataDir = `C:\\Users\\Mecall\\AppData\\Local\\Temp\\chrome-form-${Date.now()}`;

const PAGES = [
    { url: 'http://localhost:8765/personnel/create.html', name: '新增人员' },
    { url: 'http://localhost:8765/personnel/edit.html?id=1', name: '编辑人员' }
];

console.log('='.repeat(70));
console.log('v2.12.14 Verify: 新增/编辑人员表单');
console.log('='.repeat(70));

for (const page of PAGES) {
    const cmd = `"${chromePath}" --headless --disable-gpu --no-sandbox --user-data-dir="${userDataDir}" --virtual-time-budget=10000 --dump-dom "${page.url}" 2>nul`;
    const output = execSync(cmd, { encoding: 'utf-8', timeout: 15000 });

    console.log(`\n--- ${page.name} (${page.url.split('/').pop()}) ---`);

    // 检查下拉选项（关联引用基础资料字典）
    console.log('\n  下拉选项验证：');

    const checks = [
        { name: 'departmentId (DEPARTMENTS.id)',  pattern: /id="departmentId"[\s\S]*?<\/select>/,  dict: 'DEPARTMENTS' },
        { name: 'employeeTypeId (EMPLOYEE_TYPES.id)', pattern: /id="employeeTypeId"[\s\S]*?<\/select>/,  dict: 'EMPLOYEE_TYPES' },
        { name: 'attendanceTypeId (ATTENDANCE_TYPES_FULL.id)', pattern: /id="attendanceTypeId"[\s\S]*?<\/select>/,  dict: 'ATTENDANCE_TYPES_FULL' },
        { name: 'employmentStatusId (EMPLOYMENT_STATUSES.id)', pattern: /id="employmentStatusId"[\s\S]*?<\/select>/,  dict: 'EMPLOYMENT_STATUSES' }
    ];

    for (const check of checks) {
        const selectMatch = output.match(check.pattern);
        if (selectMatch) {
            const selectHtml = selectMatch[0];
            const opts = selectHtml.match(/<option[^>]*value="(\d+)"[^>]*>/g) || [];
            // 检查 value 是否为数字 ID（而非字符串 name）
            const allNumeric = opts.every(o => /value="\d+"/.test(o));
            console.log(`    [${allNumeric ? 'OK' : 'FAIL'}] ${check.name}: ${opts.length} 选项 ${allNumeric ? '(数字 ID)' : '(字符串 value ❌)'}`);
        } else {
            console.log(`    [SKIP] ${check.name}: 未找到 select 元素`);
        }
    }

    // 检查布局：双列布局（col-md-6）
    const colMd6Count = (output.match(/col-md-6/g) || []).length;
    const colMd12Count = (output.match(/col-md-12/g) || []).length;
    const filterItemCount = (output.match(/filter-item/g) || []).length;

    console.log('\n  布局验证：');
    console.log(`    col-md-6 数量：${colMd6Count}（双列布局）`);
    console.log(`    col-md-12 数量：${colMd12Count}（跨整行）`);
    console.log(`    旧 filter-item 数量：${filterItemCount}（应已替换为 col-md-6）`);

    // 检查表单分组
    const formSectionCount = (output.match(/form-section/g) || []).length;
    console.log(`    form-section 分组：${formSectionCount} 个区块`);

    // 检查按钮区
    const hasFormActions = /class="form-actions"/.test(output);
    console.log(`    form-actions 操作按钮区：${hasFormActions ? '✅ 已使用' : '❌ 缺失'}`);

    // 检查字典 hint
    const hasDictHint = /关联引用基础资料/.test(output);
    console.log(`    字典关联提示：${hasDictHint ? '✅ 已显示' : '❌ 缺失'}`);
}

console.log('\n' + '='.repeat(70));