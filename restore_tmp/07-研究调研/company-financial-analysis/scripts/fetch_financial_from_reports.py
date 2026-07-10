#!/usr/bin/env python3
"""
从下载的年报PDF中提取财务数据

支持：
- A股年报（巨潮资讯网下载）
- 港股年报（港交所下载）

财务数据只从财报中读取，不从网上搜索

优化：
- 流式处理PDF，减少内存占用
- 可选保存文本文件，便于后续grep搜索
"""
import sys
import os
from pathlib import Path
import re
import json
from datetime import datetime
from typing import Dict, List, Optional, Tuple

try:
    import PyPDF2
except ImportError:
    print("Warning: PyPDF2 not installed. Install with: pip install PyPDF2")
    PyPDF2 = None

from utils import save_json, load_json


class FinancialReportParser:
    """财报PDF解析器"""

    def __init__(self):
        # 财务指标正则表达式模式
        self.patterns = {
            # 收入相关
            'revenue': [
                r'营业收入[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'营业总收入[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Revenue[：:]?\s*([\d,，]+\.?\d*)',
                r'Total\s+Revenue[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 净利润相关
            'net_profit': [
                r'归属于.*?净利润[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'净利润[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Profit\s+attributable[：:]?\s*([\d,，]+\.?\d*)',
                r'Net\s+Profit[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 总资产
            'total_assets': [
                r'总资产[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'资产总计[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Total\s+Assets[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 股东权益
            'total_equity': [
                r'股东权益合计[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'归属于.*?股东权益[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'所有者权益[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Total\s+Equity[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 总负债
            'total_liabilities': [
                r'负债合计[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'负债总计[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Total\s+Liabilities[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 经营现金流
            'operating_cash_flow': [
                r'经营活动产生的现金流量净额[：:]\s*(-?[\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'经营活动现金流量净额[：:]\s*(-?[\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Cash\s+flow\s+from\s+operating[：:]?\s*(-?[\d,，]+\.?\d*)',
            ],
            # 流动资产
            'current_assets': [
                r'流动资产合计[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Current\s+Assets[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 流动负债
            'current_liabilities': [
                r'流动负债合计[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Current\s+Liabilities[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 存货
            'inventory': [
                r'存货[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Inventory[：:]?\s*([\d,，]+\.?\d*)',
                r'Inventories[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # 应收账款
            'accounts_receivable': [
                r'应收账款[：:]\s*([\d,，]+\.?\d*)\s*(?:元|万元|百万|亿)',
                r'Trade\s+receivables[：:]?\s*([\d,，]+\.?\d*)',
                r'Accounts\s+receivable[：:]?\s*([\d,，]+\.?\d*)',
            ],
            # ROE
            'roe': [
                r'加权平均净资产收益率[：:]\s*([\d.]+)\s*%',
                r'净资产收益率[：:]\s*([\d.]+)\s*%',
                r'ROE[：:]?\s*([\d.]+)\s*%',
                r'Return\s+on\s+Equity[：:]?\s*([\d.]+)\s*%',
            ],
            # 毛利率
            'gross_margin': [
                r'毛利率[：:]\s*([\d.]+)\s*%',
                r'综合毛利率[：:]\s*([\d.]+)\s*%',
                r'Gross\s+(?:profit\s+)?margin[：:]?\s*([\d.]+)\s*%',
            ],
            # 净利率
            'net_margin': [
                r'净利率[：:]\s*([\d.]+)\s*%',
                r'销售净利率[：:]\s*([\d.]+)\s*%',
                r'Net\s+(?:profit\s+)?margin[：:]?\s*([\d.]+)\s*%',
            ],
        }

    def parse_number(self, value_str: str, unit: str = None) -> Optional[float]:
        """解析数字字符串，转换为亿元"""
        if not value_str:
            return None

        try:
            # 清理数字字符串
            clean_str = value_str.replace(',', '').replace('，', '').replace(' ', '')
            value = float(clean_str)

            # 根据单位转换为亿元
            if unit:
                unit = unit.lower()
                if '万' in unit:
                    value = value / 10000  # 万元转亿元
                elif '百万' in unit:
                    value = value / 100  # 百万转亿元
                elif '元' in unit and '万' not in unit and '亿' not in unit:
                    value = value / 100000000  # 元转亿元

            return value

        except (ValueError, TypeError):
            return None

    def extract_from_text(self, text: str, field: str) -> Optional[float]:
        """从文本中提取指定字段的值"""
        patterns = self.patterns.get(field, [])

        for pattern in patterns:
            matches = re.findall(pattern, text, re.IGNORECASE)
            if matches:
                # 取第一个匹配
                value_str = matches[0]
                return self.parse_number(value_str)

        return None

    def extract_from_text_with_source(
        self,
        text: str,
        field: str,
        page_num: int = None,
        line_offset: int = 0,
        filename: str = None
    ) -> Optional[Dict]:
        """
        从文本中提取指定字段的值，同时记录来源信息

        Args:
            text: 要搜索的文本
            field: 字段名
            page_num: 页码
            line_offset: 行号偏移
            filename: 源文件名

        Returns:
            {
                'value': 123.45,
                'source': {
                    'filename': '...',
                    'page': 45,
                    'line': 123,
                    'context': '营业收入：123.45亿元'
                }
            }
        """
        patterns = self.patterns.get(field, [])

        for pattern in patterns:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                value_str = match.group(1)
                value = self.parse_number(value_str)

                if value is not None:
                    # 提取上下文（匹配位置前后50个字符）
                    start = max(0, match.start() - 30)
                    end = min(len(text), match.end() + 30)
                    context = text[start:end].replace('\n', ' ').strip()

                    # 计算行号
                    line_num = text[:match.start()].count('\n') + 1 + line_offset

                    return {
                        'value': value,
                        'source': {
                            'filename': filename,
                            'page': page_num,
                            'line': line_num,
                            'context': context
                        }
                    }

        return None

    def parse_pdf(self, pdf_path: Path, save_text: bool = True) -> Dict:
        """
        解析PDF文件，提取财务数据（简化版，不含来源信息）

        Args:
            pdf_path: PDF文件路径
            save_text: 是否保存文本文件（便于后续grep搜索）

        Returns:
            提取的财务数据字典
        """
        result = self.parse_pdf_with_source(pdf_path, save_text)
        # 转换为简化格式（只保留值）
        return {k: v['value'] if isinstance(v, dict) else v for k, v in result.get('indicators', {}).items()}

    def parse_pdf_with_source(self, pdf_path: Path, save_text: bool = True) -> Dict:
        """
        解析PDF文件，提取财务数据（带来源信息）

        Args:
            pdf_path: PDF文件路径
            save_text: 是否保存文本文件（便于后续grep搜索）

        Returns:
            {
                'filename': '...',
                'total_pages': 100,
                'indicators': {
                    'revenue': {
                        'value': 123.45,
                        'source': {'page': 45, 'line': 123, 'context': '...'}
                    },
                    ...
                }
            }
        """
        if PyPDF2 is None:
            print("  PyPDF2未安装，无法解析PDF")
            return {'filename': pdf_path.name, 'indicators': {}}

        if not pdf_path.exists():
            print(f"  PDF文件不存在: {pdf_path}")
            return {'filename': pdf_path.name, 'indicators': {}}

        result = {
            'filename': pdf_path.name,
            'total_pages': 0,
            'indicators': {}
        }

        text_path = pdf_path.with_suffix('.txt') if save_text else None
        text_file = None
        
        # 检查是否已有文本文件，跳过 PDF 解析直接读取
        if text_path and text_path.exists() and text_path.stat().st_size > 0:
            print(f"  跳过已有文本: {pdf_path.name}")
            try:
                with open(text_path, 'r', encoding='utf-8') as f:
                    full_text = f.read()
                # 从已有文本中提取指标
                for indicator_name, patterns in self.patterns.items():
                    for pattern in patterns:
                        match = re.search(pattern, full_text, re.IGNORECASE)
                        if match:
                            value_str = match.group(1).replace(',', '').replace('，', '')
                            try:
                                value = float(value_str)
                                result['indicators'][indicator_name] = {
                                    'value': value,
                                    'source': f'{pdf_path.name} (cached txt)',
                                    'page': '-',
                                    'line': '-'
                                }
                            except ValueError:
                                pass
                            break
                return result
            except Exception as e:
                print(f"    读取缓存文本失败: {e}，重新解析PDF")
        
        print(f"  解析PDF: {pdf_path.name}")

        # 用于记录每页的行号偏移
        line_offsets = {}
        current_line = 0

        try:
            with open(pdf_path, 'rb') as file:
                pdf_reader = PyPDF2.PdfReader(file)
                total_pages = len(pdf_reader.pages)
                pages_to_read = min(150, total_pages)
                result['total_pages'] = total_pages

                print(f"    共 {total_pages} 页，读取前 {pages_to_read} 页...")

                # 打开文本输出文件（如果需要）
                if save_text:
                    text_file = open(text_path, 'w', encoding='utf-8')
                    text_file.write(f"# Source: {pdf_path.name}\n")
                    text_file.write(f"# Total Pages: {total_pages}\n")
                    text_file.write("=" * 60 + "\n\n")
                    current_line = 4  # 头部占4行

                # 收集每页文本及其元数据
                page_texts = []  # [(page_num, text, line_offset), ...]

                for page_num in range(pages_to_read):
                    try:
                        page = pdf_reader.pages[page_num]
                        page_text = page.extract_text()

                        if page_text:
                            # 保存到文本文件（带页码标记）
                            if text_file:
                                text_file.write(f"\n--- Page {page_num + 1} ---\n\n")
                                current_line += 3  # 页码标记占3行

                            line_offsets[page_num + 1] = current_line

                            if text_file:
                                text_file.write(page_text)
                                text_file.write("\n")
                                current_line += page_text.count('\n') + 1

                            # 收集用于指标提取
                            page_texts.append((page_num + 1, page_text, line_offsets[page_num + 1]))

                    except Exception as e:
                        if text_file:
                            text_file.write(f"\n--- Page {page_num + 1} ---\n")
                            text_file.write(f"[Error: {e}]\n")
                            current_line += 3
                        continue

                # 关闭文本文件
                if text_file:
                    text_file.close()
                    text_file = None
                    size_kb = text_path.stat().st_size / 1024
                    print(f"    ✓ 文本已保存: {text_path.name} ({size_kb:.1f} KB)")

            # 提取各项财务数据（带来源信息）
            fields = [
                'revenue', 'net_profit', 'total_assets', 'total_equity',
                'total_liabilities', 'operating_cash_flow', 'current_assets',
                'current_liabilities', 'inventory', 'accounts_receivable',
                'roe', 'gross_margin', 'net_margin'
            ]

            # 逐页搜索，记录第一个匹配的位置
            for field in fields:
                for page_num, page_text, line_offset in page_texts:
                    extracted = self.extract_from_text_with_source(
                        page_text,
                        field,
                        page_num=page_num,
                        line_offset=line_offset,
                        filename=pdf_path.name
                    )
                    if extracted:
                        result['indicators'][field] = extracted
                        break  # 找到第一个匹配就停止

            indicator_count = len(result['indicators'])
            print(f"    ✓ 成功提取 {indicator_count} 个指标（带来源信息）")

            return result

        except Exception as e:
            print(f"    ✗ PDF解析失败: {e}")
            return {'filename': pdf_path.name, 'indicators': {}}
        finally:
            # 确保文件关闭
            if text_file:
                text_file.close()


def extract_year_from_filename(filename: str) -> Optional[str]:
    """从文件名中提取年份（匹配 20xx 年份，避免匹配股票代码）"""
    match = re.search(r'(20\d{2})', filename)
    return match.group(1) if match else None


def calculate_derived_indicators(data: Dict) -> Dict:
    """计算衍生指标"""
    result = data.copy()

    # 计算ROE（如果没有直接提取到）
    if result.get('roe') is None and result.get('net_profit') and result.get('total_equity'):
        result['roe'] = (result['net_profit'] / result['total_equity']) * 100

    # 计算净利率
    if result.get('net_margin') is None and result.get('net_profit') and result.get('revenue'):
        result['net_margin'] = (result['net_profit'] / result['revenue']) * 100

    # 计算资产周转率
    if result.get('revenue') and result.get('total_assets'):
        result['asset_turnover'] = result['revenue'] / result['total_assets']

    # 计算权益乘数
    if result.get('total_assets') and result.get('total_equity'):
        result['equity_multiplier'] = result['total_assets'] / result['total_equity']

    # 计算资产负债率
    if result.get('total_liabilities') and result.get('total_assets'):
        result['asset_liability_ratio'] = (result['total_liabilities'] / result['total_assets']) * 100

    # 计算流动比率
    if result.get('current_assets') and result.get('current_liabilities'):
        result['current_ratio'] = result['current_assets'] / result['current_liabilities']

    # 计算速动比率
    if result.get('current_assets') and result.get('inventory') and result.get('current_liabilities'):
        result['quick_ratio'] = (result['current_assets'] - result['inventory']) / result['current_liabilities']

    return result


def parse_all_reports(output_dir: Path) -> Dict[str, Dict]:
    """解析所有下载的年报（简化版，向后兼容）"""
    reports_dir = output_dir / 'raw_data' / 'annual_reports'

    if not reports_dir.exists():
        print(f"年报目录不存在: {reports_dir}")
        return {}

    parser = FinancialReportParser()
    all_data = {}

    # 获取所有PDF文件
    pdf_files = list(reports_dir.glob('*.pdf'))

    if not pdf_files:
        print(f"未找到年报PDF文件")
        return {}

    print(f"\n找到 {len(pdf_files)} 份年报PDF")

    for pdf_path in sorted(pdf_files):
        year = extract_year_from_filename(pdf_path.name)
        if year:
            print(f"\n解析 {year} 年年报...")
            financial_data = parser.parse_pdf(pdf_path)

            if financial_data:
                # 计算衍生指标
                financial_data = calculate_derived_indicators(financial_data)
                all_data[year] = financial_data

    return all_data


# 报告类型 -> 目录名映射
REPORT_TYPE_DIRS = {
    'annual': 'annual_reports',
    'semi': 'semi_annual_reports',
    'q1': 'q1_reports',
    'q3': 'q3_reports',
}

# 报告类型 -> 中文名称映射
REPORT_TYPE_NAMES = {
    'annual': '年报',
    'semi': '半年报',
    'q1': '一季报',
    'q3': '三季报',
}


def parse_all_quarterly_reports(output_dir: Path) -> Dict[str, Dict]:
    """
    解析所有季度报告（带来源信息）

    Returns:
        {
            "2024_annual": {
                "report_type": "annual",
                "year": 2024,
                "filename": "...",
                "indicators": {
                    "revenue": {
                        "value": 123.45,
                        "source": {"page": 45, "line": 123, "context": "..."}
                    },
                    ...
                }
            },
            "2024_semi": {...},
            "2024_q1": {...},
            ...
        }
    """
    parser = FinancialReportParser()
    all_data = {}

    for report_type, dir_name in REPORT_TYPE_DIRS.items():
        reports_dir = output_dir / 'raw_data' / dir_name

        if not reports_dir.exists():
            continue

        pdf_files = list(reports_dir.glob('*.pdf'))
        if not pdf_files:
            continue

        report_name = REPORT_TYPE_NAMES.get(report_type, report_type)
        print(f"\n解析{report_name}...")

        for pdf_path in sorted(pdf_files):
            year = extract_year_from_filename(pdf_path.name)
            if year:
                key = f"{year}_{report_type}"
                print(f"  解析 {year} 年{report_name}...")

                result = parser.parse_pdf_with_source(pdf_path)

                if result.get('indicators'):
                    # 添加报告类型和年份信息
                    all_data[key] = {
                        'report_type': report_type,
                        'report_type_name': report_name,
                        'year': int(year),
                        'filename': result['filename'],
                        'total_pages': result.get('total_pages', 0),
                        'indicators': result['indicators']
                    }

                    # 计算衍生指标
                    simple_data = {k: v['value'] for k, v in result['indicators'].items()}
                    derived = calculate_derived_indicators(simple_data)

                    # 将衍生指标添加到indicators中（无来源信息，标记为计算得出）
                    for field in ['asset_turnover', 'equity_multiplier', 'asset_liability_ratio',
                                  'current_ratio', 'quick_ratio']:
                        if field in derived and field not in all_data[key]['indicators']:
                            all_data[key]['indicators'][field] = {
                                'value': derived[field],
                                'source': {
                                    'type': 'calculated',
                                    'note': '根据其他指标计算得出'
                                }
                            }

    # 按年份和报告类型排序
    sorted_keys = sorted(all_data.keys(), key=lambda x: (x.split('_')[0], x.split('_')[1]), reverse=True)
    sorted_data = {k: all_data[k] for k in sorted_keys}

    return sorted_data


def generate_dupont_summary(financial_data: Dict[str, Dict]) -> Dict:
    """生成杜邦分析摘要"""
    summary = {
        'years': [],
        'roe': [],
        'net_margin': [],
        'asset_turnover': [],
        'equity_multiplier': [],
        'roe_breakdown': {}
    }

    for year in sorted(financial_data.keys(), reverse=True):
        data = financial_data[year]
        summary['years'].append(year)
        summary['roe'].append(data.get('roe'))
        summary['net_margin'].append(data.get('net_margin'))
        summary['asset_turnover'].append(data.get('asset_turnover'))
        summary['equity_multiplier'].append(data.get('equity_multiplier'))

        summary['roe_breakdown'][year] = {
            'roe': data.get('roe'),
            'net_margin': data.get('net_margin'),
            'asset_turnover': data.get('asset_turnover'),
            'equity_multiplier': data.get('equity_multiplier'),
        }

    return summary


def main(stock_code: str, output_dir: Path, years: list = None):
    """
    主函数：从年报中提取财务数据

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 年份列表（可选，用于筛选）
    """
    print(f"\n{'='*60}")
    print(f"从年报中提取财务数据")
    print(f"股票代码: {stock_code}")
    print(f"{'='*60}")

    # 1. 解析所有季度报告（带来源信息）
    quarterly_data = parse_all_quarterly_reports(output_dir)

    # 2. 同时解析年报（向后兼容，简化格式）
    financial_data = parse_all_reports(output_dir)

    if not financial_data and not quarterly_data:
        print("\n未能从年报中提取到财务数据")
        print("请确保已下载年报PDF到: " + str(output_dir / 'raw_data' / 'annual_reports'))
        return None

    # 3. 保存带来源信息的季度数据
    if quarterly_data:
        source_data_path = output_dir / 'processed_data' / 'financial_data_with_source.json'
        save_json(quarterly_data, source_data_path)
        print(f"\n✓ 带来源的财务数据已保存: {source_data_path}")

    # 4. 保存原始财务数据（向后兼容）
    if financial_data:
        raw_data_path = output_dir / 'processed_data' / 'financial_data_from_reports.json'
        save_json(financial_data, raw_data_path)
        print(f"✓ 财务数据已保存: {raw_data_path}")

    # 5. 生成杜邦分析指标
    dupont_indicators = {}
    for year, data in financial_data.items():
        dupont_indicators[year] = {
            'roe': data.get('roe'),
            'net_margin': data.get('net_margin'),
            'asset_turnover': data.get('asset_turnover'),
            'equity_multiplier': data.get('equity_multiplier'),
            'asset_liability_ratio': data.get('asset_liability_ratio'),
            'current_ratio': data.get('current_ratio'),
            'quick_ratio': data.get('quick_ratio'),
            'gross_margin': data.get('gross_margin'),
        }

    dupont_path = output_dir / 'processed_data' / 'dupont_indicators.json'
    save_json(dupont_indicators, dupont_path)
    print(f"✓ 杜邦分析指标已保存: {dupont_path}")

    # 6. 生成杜邦分析摘要
    summary = generate_dupont_summary(financial_data)
    summary_path = output_dir / 'processed_data' / 'dupont_summary.json'
    save_json(summary, summary_path)
    print(f"✓ 杜邦分析摘要已保存: {summary_path}")

    # 7. 打印提取结果摘要
    print(f"\n{'='*60}")
    print("财务数据提取完成")
    print(f"{'='*60}")

    if quarterly_data:
        print("\n季度报告提取结果:")
        for key in sorted(quarterly_data.keys(), reverse=True):
            data = quarterly_data[key]
            indicator_count = len(data.get('indicators', {}))
            print(f"  {key}: {indicator_count} 个指标 ({data.get('filename', 'N/A')})")

    if financial_data:
        print("\n年报提取结果（简化格式）:")
        for year in sorted(financial_data.keys(), reverse=True):
            data = financial_data[year]
            non_null_count = sum(1 for v in data.values() if v is not None)
            print(f"  {year}年: {non_null_count} 个指标")

    return financial_data


if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: python fetch_financial_from_reports.py <stock_code> <output_dir>")
        print("Example: python fetch_financial_from_reports.py 600519 ./600519_analysis")
        sys.exit(1)

    stock_code = sys.argv[1]
    output_dir = Path(sys.argv[2])

    main(stock_code, output_dir)
