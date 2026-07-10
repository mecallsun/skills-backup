#!/usr/bin/env python3
"""
财务指标索引生成器

功能：
- 扫描年报文本文件，定位关键财务指标
- 生成轻量级索引（<5KB）
- 记录指标所在行号和上下文，便于快速定位
"""
import sys
import re
from pathlib import Path
from typing import Dict, List, Optional

from utils import save_json, load_json


# 关键财务指标关键词（中英文）
FINANCIAL_KEYWORDS = {
    # 收入相关
    '营业收入': ['营业收入', '营业总收入', 'Revenue', 'Total Revenue'],
    '营业成本': ['营业成本', 'Cost of Revenue', 'Cost of Sales'],

    # 利润相关
    '净利润': ['净利润', '归属于.*净利润', 'Net Profit', 'Profit attributable'],
    '毛利': ['毛利', 'Gross Profit'],
    '营业利润': ['营业利润', 'Operating Profit'],

    # 资产相关
    '总资产': ['总资产', '资产总计', 'Total Assets'],
    '总负债': ['负债合计', '负债总计', 'Total Liabilities'],
    '股东权益': ['股东权益', '所有者权益', 'Total Equity', "Shareholders' Equity"],
    '流动资产': ['流动资产合计', 'Current Assets'],
    '流动负债': ['流动负债合计', 'Current Liabilities'],

    # 现金流相关
    '经营现金流': ['经营活动.*现金流量净额', 'Cash.*operating'],
    '投资现金流': ['投资活动.*现金流量净额', 'Cash.*investing'],
    '筹资现金流': ['筹资活动.*现金流量净额', 'Cash.*financing'],

    # 比率指标
    'ROE': ['净资产收益率', 'ROE', 'Return on Equity'],
    '毛利率': ['毛利率', 'Gross.*margin'],
    '净利率': ['净利率', '销售净利率', 'Net.*margin'],
    '资产负债率': ['资产负债率', 'Debt.*ratio', 'Leverage'],

    # 其他重要指标
    '应收账款': ['应收账款', 'Trade receivables', 'Accounts receivable'],
    '存货': ['存货', 'Inventory', 'Inventories'],
    '每股收益': ['每股收益', 'EPS', 'Earnings per share'],
    '分红': ['分红', '派息', 'Dividend'],
}


def extract_year_from_filename(filename: str) -> Optional[str]:
    """从文件名中提取年份"""
    match = re.search(r'(20\d{2})', filename)
    return match.group(1) if match else None


def scan_text_file(text_path: Path, keywords: Dict[str, List[str]]) -> Dict[str, List[Dict]]:
    """
    扫描文本文件，查找关键词位置

    Args:
        text_path: 文本文件路径
        keywords: 关键词字典 {类别: [关键词列表]}

    Returns:
        索引结果 {类别: [{line, page, context}]}
    """
    index = {}
    current_page = 0

    try:
        with open(text_path, 'r', encoding='utf-8') as f:
            for line_num, line in enumerate(f, 1):
                # 检测页码标记
                page_match = re.match(r'---\s*Page\s+(\d+)\s*---', line)
                if page_match:
                    current_page = int(page_match.group(1))
                    continue

                # 搜索关键词
                for category, kw_list in keywords.items():
                    for kw in kw_list:
                        if re.search(kw, line, re.IGNORECASE):
                            if category not in index:
                                index[category] = []

                            # 避免重复添加同一行
                            if not any(item['line'] == line_num for item in index[category]):
                                index[category].append({
                                    'line': line_num,
                                    'page': current_page,
                                    'context': line.strip()[:150]  # 截取上下文
                                })
                            break  # 一行只匹配一次该类别

    except Exception as e:
        print(f"  扫描文件失败 {text_path}: {e}")

    return index


def generate_index(reports_dir: Path, output_path: Path) -> Dict:
    """
    生成财务指标索引

    Args:
        reports_dir: 年报文本文件目录
        output_path: 索引输出路径

    Returns:
        完整索引
    """
    full_index = {
        'metadata': {
            'source_dir': str(reports_dir),
            'keywords_count': len(FINANCIAL_KEYWORDS)
        },
        'years': {}
    }

    # 查找所有文本文件
    text_files = list(reports_dir.glob('*.txt'))
    if not text_files:
        print(f"未找到文本文件: {reports_dir}")
        return full_index

    print(f"\n扫描 {len(text_files)} 个文本文件...")

    for text_path in sorted(text_files):
        year = extract_year_from_filename(text_path.name)
        if not year:
            continue

        print(f"  索引 {year} 年: {text_path.name}")
        year_index = scan_text_file(text_path, FINANCIAL_KEYWORDS)

        # 添加文件信息
        full_index['years'][year] = {
            'file': text_path.name,
            'indicators': year_index,
            'found_count': len(year_index)
        }

        # 限制每个类别的条目数量（避免索引过大）
        for category in year_index:
            if len(year_index[category]) > 5:
                year_index[category] = year_index[category][:5]

    # 保存索引
    save_json(full_index, output_path)

    # 统计
    total_indicators = sum(
        data['found_count']
        for data in full_index['years'].values()
    )
    print(f"\n索引生成完成: {len(full_index['years'])} 年, {total_indicators} 个指标类别")

    return full_index


def generate_index_summary(index: Dict) -> str:
    """
    生成索引摘要（用于prompt）

    Args:
        index: 完整索引

    Returns:
        摘要文本
    """
    lines = []
    lines.append("### 可用年份和关键指标位置")
    lines.append("")

    for year in sorted(index.get('years', {}).keys(), reverse=True):
        year_data = index['years'][year]
        lines.append(f"**{year}年** ({year_data['file']})")

        indicators = year_data.get('indicators', {})
        if indicators:
            # 只显示最重要的几个指标
            key_indicators = ['营业收入', '净利润', '总资产', 'ROE']
            for ind in key_indicators:
                if ind in indicators and indicators[ind]:
                    first_match = indicators[ind][0]
                    lines.append(f"  - {ind}: Page {first_match['page']}, Line {first_match['line']}")
        lines.append("")

    return '\n'.join(lines)


def main(output_dir: Path):
    """主函数"""
    print("\n" + "=" * 60)
    print("财务指标索引生成器")
    print("=" * 60)

    reports_dir = output_dir / 'raw_data' / 'annual_reports'
    index_path = output_dir / 'processed_data' / 'financial_index.json'

    # 确保目录存在
    index_path.parent.mkdir(parents=True, exist_ok=True)

    # 生成索引
    index = generate_index(reports_dir, index_path)

    print(f"\n索引已保存: {index_path}")

    # 生成摘要
    summary = generate_index_summary(index)
    summary_path = output_dir / 'processed_data' / 'financial_index_summary.txt'
    with open(summary_path, 'w', encoding='utf-8') as f:
        f.write(summary)
    print(f"摘要已保存: {summary_path}")

    return index


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python generate_index.py <output_directory>")
        print("Example: python generate_index.py ./company_analysis_600519_20240126")
        sys.exit(1)

    output_dir = Path(sys.argv[1])
    main(output_dir)
