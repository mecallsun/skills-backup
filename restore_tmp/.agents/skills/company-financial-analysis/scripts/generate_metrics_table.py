#!/usr/bin/env python3
"""
核心指标表格生成器

从带来源的财务数据生成核心指标表格，便于分析和展示
"""
import sys
from pathlib import Path
import json
from typing import Dict, List, Optional
from datetime import datetime

from utils import load_json, save_json


class MetricsTableGenerator:
    """核心指标表格生成器"""

    # 报告类型排序优先级（年报优先，然后按时间倒序）
    REPORT_TYPE_ORDER = {'annual': 0, 'q3': 1, 'semi': 2, 'q1': 3}

    # 指标中文名称映射
    INDICATOR_NAMES = {
        'revenue': '营业收入',
        'net_profit': '净利润',
        'gross_margin': '毛利率',
        'net_margin': '净利率',
        'roe': '净资产收益率(ROE)',
        'total_assets': '总资产',
        'total_equity': '净资产',
        'total_liabilities': '总负债',
        'asset_liability_ratio': '资产负债率',
        'asset_turnover': '资产周转率',
        'equity_multiplier': '权益乘数',
        'operating_cash_flow': '经营现金流',
        'current_assets': '流动资产',
        'current_liabilities': '流动负债',
        'inventory': '存货',
        'accounts_receivable': '应收账款',
        'current_ratio': '流动比率',
        'quick_ratio': '速动比率',
    }

    # 报告类型中文名称
    REPORT_TYPE_NAMES = {
        'annual': '年报',
        'semi': '半年报',
        'q1': '一季报',
        'q3': '三季报',
    }

    def __init__(self, financial_data: Dict):
        """
        初始化

        Args:
            financial_data: 从 financial_data_with_source.json 加载的数据
        """
        self.data = financial_data
        self.sorted_keys = self._sort_report_keys(list(financial_data.keys()))

    def _sort_report_keys(self, keys: List[str]) -> List[str]:
        """
        排序报告键名

        排序规则：按年份降序，同一年份按报告类型排序（annual > q3 > semi > q1）
        """
        def sort_key(key):
            parts = key.split('_')
            year = int(parts[0]) if parts[0].isdigit() else 0
            report_type = parts[1] if len(parts) > 1 else 'annual'
            type_order = self.REPORT_TYPE_ORDER.get(report_type, 99)
            return (-year, type_order)

        return sorted(keys, key=sort_key)

    def _get_indicator_value(self, report_key: str, indicator: str) -> Optional[float]:
        """获取指标值"""
        report = self.data.get(report_key, {})
        indicators = report.get('indicators', {})
        if indicator in indicators:
            ind_data = indicators[indicator]
            if isinstance(ind_data, dict):
                return ind_data.get('value')
            return ind_data
        return None

    def _get_indicator_source(self, report_key: str, indicator: str) -> str:
        """获取指标来源描述"""
        report = self.data.get(report_key, {})
        indicators = report.get('indicators', {})
        if indicator in indicators:
            ind_data = indicators[indicator]
            if isinstance(ind_data, dict):
                source = ind_data.get('source', {})
                if source.get('type') == 'calculated':
                    return '计算'
                page = source.get('page')
                if page:
                    report_type = report.get('report_type_name', '')
                    return f"{report_type}P{page}"
        return '-'

    def _format_value(self, value: Optional[float], is_percent: bool = False, decimals: int = 2) -> str:
        """格式化数值"""
        if value is None:
            return '-'
        if is_percent:
            return f"{value:.{decimals}f}%"
        if abs(value) >= 1:
            return f"{value:,.{decimals}f}"
        return f"{value:.{decimals}f}"

    def _calculate_yoy_growth(self, current_key: str, indicator: str) -> Optional[float]:
        """计算同比增长率"""
        current_value = self._get_indicator_value(current_key, indicator)
        if current_value is None:
            return None

        # 解析当前报告的年份和类型
        parts = current_key.split('_')
        if len(parts) != 2:
            return None

        year, report_type = int(parts[0]), parts[1]
        prev_key = f"{year - 1}_{report_type}"

        prev_value = self._get_indicator_value(prev_key, indicator)
        if prev_value is None or prev_value == 0:
            return None

        return ((current_value - prev_value) / abs(prev_value)) * 100

    def generate_revenue_profit_table(self) -> str:
        """生成营收与利润表格"""
        lines = []
        lines.append("### 营收与利润")
        lines.append("")
        lines.append("| 期间 | 营业收入(亿) | 同比增长 | 净利润(亿) | 同比增长 | 净利率 | 来源 |")
        lines.append("|------|-------------|---------|-----------|---------|-------|------|")

        for key in self.sorted_keys[:12]:  # 最多显示12期
            report = self.data.get(key, {})
            year = report.get('year', '')
            report_type_name = report.get('report_type_name', '')
            period = f"{year}{report_type_name}"

            revenue = self._get_indicator_value(key, 'revenue')
            revenue_yoy = self._calculate_yoy_growth(key, 'revenue')
            net_profit = self._get_indicator_value(key, 'net_profit')
            profit_yoy = self._calculate_yoy_growth(key, 'net_profit')
            net_margin = self._get_indicator_value(key, 'net_margin')

            # 获取来源
            source = self._get_indicator_source(key, 'revenue')

            lines.append(
                f"| {period} | {self._format_value(revenue)} | "
                f"{self._format_value(revenue_yoy, is_percent=True) if revenue_yoy else '-'} | "
                f"{self._format_value(net_profit)} | "
                f"{self._format_value(profit_yoy, is_percent=True) if profit_yoy else '-'} | "
                f"{self._format_value(net_margin, is_percent=True)} | {source} |"
            )

        return "\n".join(lines)

    def generate_profitability_table(self) -> str:
        """生成盈利能力表格"""
        lines = []
        lines.append("### 盈利能力")
        lines.append("")
        lines.append("| 期间 | 毛利率 | 净利率 | ROE | 来源 |")
        lines.append("|------|-------|-------|-----|------|")

        for key in self.sorted_keys[:12]:
            report = self.data.get(key, {})
            year = report.get('year', '')
            report_type_name = report.get('report_type_name', '')
            period = f"{year}{report_type_name}"

            gross_margin = self._get_indicator_value(key, 'gross_margin')
            net_margin = self._get_indicator_value(key, 'net_margin')
            roe = self._get_indicator_value(key, 'roe')

            source = self._get_indicator_source(key, 'roe') or self._get_indicator_source(key, 'gross_margin')

            lines.append(
                f"| {period} | {self._format_value(gross_margin, is_percent=True)} | "
                f"{self._format_value(net_margin, is_percent=True)} | "
                f"{self._format_value(roe, is_percent=True)} | {source} |"
            )

        return "\n".join(lines)

    def generate_dupont_table(self) -> str:
        """生成杜邦分析表格"""
        lines = []
        lines.append("### 杜邦分析")
        lines.append("")
        lines.append("**核心公式**: ROE = 净利率 × 资产周转率 × 权益乘数")
        lines.append("")
        lines.append("| 期间 | ROE | 净利率 | 资产周转率 | 权益乘数 | 来源 |")
        lines.append("|------|-----|-------|-----------|---------|------|")

        for key in self.sorted_keys[:12]:
            report = self.data.get(key, {})
            year = report.get('year', '')
            report_type_name = report.get('report_type_name', '')
            period = f"{year}{report_type_name}"

            roe = self._get_indicator_value(key, 'roe')
            net_margin = self._get_indicator_value(key, 'net_margin')
            asset_turnover = self._get_indicator_value(key, 'asset_turnover')
            equity_multiplier = self._get_indicator_value(key, 'equity_multiplier')

            source = self._get_indicator_source(key, 'roe')

            lines.append(
                f"| {period} | {self._format_value(roe, is_percent=True)} | "
                f"{self._format_value(net_margin, is_percent=True)} | "
                f"{self._format_value(asset_turnover)} | "
                f"{self._format_value(equity_multiplier)} | {source} |"
            )

        return "\n".join(lines)

    def generate_balance_sheet_table(self) -> str:
        """生成资产负债表格"""
        lines = []
        lines.append("### 资产负债")
        lines.append("")
        lines.append("| 期间 | 总资产(亿) | 净资产(亿) | 总负债(亿) | 资产负债率 | 来源 |")
        lines.append("|------|-----------|-----------|-----------|-----------|------|")

        for key in self.sorted_keys[:12]:
            report = self.data.get(key, {})
            year = report.get('year', '')
            report_type_name = report.get('report_type_name', '')
            period = f"{year}{report_type_name}"

            total_assets = self._get_indicator_value(key, 'total_assets')
            total_equity = self._get_indicator_value(key, 'total_equity')
            total_liabilities = self._get_indicator_value(key, 'total_liabilities')
            asset_liability_ratio = self._get_indicator_value(key, 'asset_liability_ratio')

            source = self._get_indicator_source(key, 'total_assets')

            lines.append(
                f"| {period} | {self._format_value(total_assets)} | "
                f"{self._format_value(total_equity)} | "
                f"{self._format_value(total_liabilities)} | "
                f"{self._format_value(asset_liability_ratio, is_percent=True)} | {source} |"
            )

        return "\n".join(lines)

    def generate_liquidity_table(self) -> str:
        """生成流动性表格"""
        lines = []
        lines.append("### 流动性指标")
        lines.append("")
        lines.append("| 期间 | 流动资产(亿) | 流动负债(亿) | 流动比率 | 速动比率 | 来源 |")
        lines.append("|------|-------------|-------------|---------|---------|------|")

        for key in self.sorted_keys[:12]:
            report = self.data.get(key, {})
            year = report.get('year', '')
            report_type_name = report.get('report_type_name', '')
            period = f"{year}{report_type_name}"

            current_assets = self._get_indicator_value(key, 'current_assets')
            current_liabilities = self._get_indicator_value(key, 'current_liabilities')
            current_ratio = self._get_indicator_value(key, 'current_ratio')
            quick_ratio = self._get_indicator_value(key, 'quick_ratio')

            source = self._get_indicator_source(key, 'current_assets')

            lines.append(
                f"| {period} | {self._format_value(current_assets)} | "
                f"{self._format_value(current_liabilities)} | "
                f"{self._format_value(current_ratio)} | "
                f"{self._format_value(quick_ratio)} | {source} |"
            )

        return "\n".join(lines)

    def generate_cash_flow_table(self) -> str:
        """生成现金流表格"""
        lines = []
        lines.append("### 现金流")
        lines.append("")
        lines.append("| 期间 | 经营现金流(亿) | 同比增长 | 净利润(亿) | 现金/利润比 | 来源 |")
        lines.append("|------|---------------|---------|-----------|------------|------|")

        for key in self.sorted_keys[:12]:
            report = self.data.get(key, {})
            year = report.get('year', '')
            report_type_name = report.get('report_type_name', '')
            period = f"{year}{report_type_name}"

            ocf = self._get_indicator_value(key, 'operating_cash_flow')
            ocf_yoy = self._calculate_yoy_growth(key, 'operating_cash_flow')
            net_profit = self._get_indicator_value(key, 'net_profit')

            # 计算现金/利润比
            cash_profit_ratio = None
            if ocf is not None and net_profit is not None and net_profit != 0:
                cash_profit_ratio = ocf / net_profit

            source = self._get_indicator_source(key, 'operating_cash_flow')

            lines.append(
                f"| {period} | {self._format_value(ocf)} | "
                f"{self._format_value(ocf_yoy, is_percent=True) if ocf_yoy else '-'} | "
                f"{self._format_value(net_profit)} | "
                f"{self._format_value(cash_profit_ratio)} | {source} |"
            )

        return "\n".join(lines)

    def generate_all_tables(self) -> str:
        """生成所有表格"""
        sections = []
        sections.append("## 核心财务指标")
        sections.append("")
        sections.append(f"数据更新时间: {datetime.now().strftime('%Y-%m-%d')}")
        sections.append("")

        sections.append(self.generate_revenue_profit_table())
        sections.append("")
        sections.append(self.generate_profitability_table())
        sections.append("")
        sections.append(self.generate_dupont_table())
        sections.append("")
        sections.append(self.generate_balance_sheet_table())
        sections.append("")
        sections.append(self.generate_liquidity_table())
        sections.append("")
        sections.append(self.generate_cash_flow_table())

        return "\n".join(sections)

    def export_to_json(self, output_path: Path):
        """导出为JSON格式"""
        export_data = {
            'generated_at': datetime.now().isoformat(),
            'periods': [],
        }

        for key in self.sorted_keys:
            report = self.data.get(key, {})
            period_data = {
                'key': key,
                'year': report.get('year'),
                'report_type': report.get('report_type'),
                'report_type_name': report.get('report_type_name'),
                'filename': report.get('filename'),
                'indicators': {}
            }

            # 提取所有指标
            indicators = report.get('indicators', {})
            for ind_name, ind_data in indicators.items():
                if isinstance(ind_data, dict):
                    period_data['indicators'][ind_name] = {
                        'value': ind_data.get('value'),
                        'source': ind_data.get('source'),
                    }
                else:
                    period_data['indicators'][ind_name] = {'value': ind_data}

                # 计算同比增长
                yoy = self._calculate_yoy_growth(key, ind_name)
                if yoy is not None:
                    period_data['indicators'][ind_name]['yoy_growth'] = yoy

            export_data['periods'].append(period_data)

        save_json(export_data, output_path)
        print(f"  ✓ JSON表格已导出: {output_path}")

    def export_to_markdown(self, output_path: Path):
        """导出为Markdown格式"""
        content = self.generate_all_tables()

        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(content)

        print(f"  ✓ Markdown表格已导出: {output_path}")


def main(output_dir: Path) -> bool:
    """
    主函数

    Args:
        output_dir: 分析输出目录

    Returns:
        是否成功生成表格
    """
    print("\n" + "=" * 60)
    print("生成核心指标表格")
    print("=" * 60 + "\n")

    # 加载带来源的财务数据
    source_data_path = output_dir / 'processed_data' / 'financial_data_with_source.json'

    if not source_data_path.exists():
        print(f"未找到财务数据文件: {source_data_path}")
        print("请先运行 fetch_financial_from_reports.py 提取财务数据")
        return False

    financial_data = load_json(source_data_path)

    if not financial_data:
        print("财务数据为空")
        return False

    print(f"加载了 {len(financial_data)} 期财务数据")

    # 生成表格
    generator = MetricsTableGenerator(financial_data)

    # 导出JSON
    json_path = output_dir / 'processed_data' / 'metrics_tables.json'
    generator.export_to_json(json_path)

    # 导出Markdown
    md_path = output_dir / 'processed_data' / 'metrics_tables.md'
    generator.export_to_markdown(md_path)

    print("\n✓ 核心指标表格生成完成")

    return True


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python generate_metrics_table.py <output_dir>")
        print("Example: python generate_metrics_table.py ./company_analysis_600519_20260126")
        sys.exit(1)

    output_dir = Path(sys.argv[1])
    success = main(output_dir)
    sys.exit(0 if success else 1)
