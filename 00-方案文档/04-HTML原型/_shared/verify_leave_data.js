// 检查 mock-data.js 中是否有 leaveDate 数据
const fs = require('fs');
const content = fs.readFileSync('E:/AI工作目录/AI编程开发/JINGE开发/宿舍管理系统/00-方案文档/04-HTML原型/mock-data.js', 'utf-8');

// 找到所有有 leaveDate 的记录（不论是 null 还是有日期）
const matchBlock = content.match(/const PERSONNEL = \[([\s\S]*?)\];/);
if (matchBlock) {
    const personData = matchBlock[1];
    // 找到所有 leaveDate 字段
    const leaveMatches = personData.match(/"leaveDate":\s*("[^"]+"|null)/g) || [];
    const stats = { null: 0, withDate: 0, sample: [] };
    leaveMatches.forEach(m => {
        if (m.includes('null')) {
            stats.null++;
        } else {
            stats.withDate++;
            if (stats.sample.length < 3) {
                const dateMatch = m.match(/"([^"]+)"/);
                if (dateMatch) stats.sample.push(dateMatch[1]);
            }
        }
    });
    console.log('leaveDate stats:', stats);
    console.log('Total records:', personData.match(/"id":/g).length);
}