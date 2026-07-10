#!/usr/bin/env python3
"""
公司分析快速学习工具

功能：
1. 从之前分析过的公司快速加载知识
2. 提供公司对比功能
3. 生成学习摘要
"""

import os
import json
from pathlib import Path
from datetime import datetime


class CompanyKnowledgeLoader:
    def __init__(self, scripts_dir=None):
        if scripts_dir is None:
            scripts_dir = Path(__file__).parent
        else:
            scripts_dir = Path(scripts_dir)

        self.scripts_dir = scripts_dir
        self.index_file = scripts_dir / 'company_index.json'
        self.companies_index = self._load_index()

    def _load_index(self):
        """加载公司索引"""
        if not self.index_file.exists():
            return {}

        with open(self.index_file, 'r', encoding='utf-8') as f:
            return json.load(f)

    def list_companies(self):
        """列出所有已分析的公司"""
        if not self.companies_index:
            print("暂无已分析的公司")
            return []

        print(f"\n{'='*80}")
        print(f"已分析公司列表 (共 {len(self.companies_index)} 家)")
        print(f"{'='*80}\n")

        companies = []
        for code, info in self.companies_index.items():
            company_name = info.get('company_name', '未知')
            market = info.get('market', '未知')
            analysis_date = info.get('analysis_date', '')
            if analysis_date:
                analysis_date = datetime.fromisoformat(analysis_date).strftime('%Y-%m-%d')

            companies.append({
                'code': code,
                'name': company_name,
                'market': market,
                'date': analysis_date
            })

            print(f"{code:15} {company_name:20} {market:10} {analysis_date}")

        print()
        return companies

    def load_company_knowledge(self, company_code):
        """加载指定公司的知识"""
        if company_code not in self.companies_index:
            print(f"错误: 未找到公司 {company_code} 的分析数据")
            return None

        company_info = self.companies_index[company_code]
        summary_file = company_info.get('summary_file')

        if not summary_file or not Path(summary_file).exists():
            print(f"错误: 知识摘要文件不存在: {summary_file}")
            return None

        print(f"\n{'='*80}")
        print(f"加载公司知识: {company_code} - {company_info.get('company_name', '未知')}")
        print(f"{'='*80}\n")

        with open(summary_file, 'r', encoding='utf-8') as f:
            knowledge = json.load(f)

        # 显示摘要
        self._display_knowledge_summary(knowledge)

        return knowledge

    def _display_knowledge_summary(self, knowledge):
        """显示知识摘要"""
        print("## 基本信息\n")
        basic_info = knowledge.get('basic_info', {})
        print(f"  公司代码: {basic_info.get('stock_code', 'N/A')}")
        print(f"  公司名称: {basic_info.get('sec_name', 'N/A')}")
        print(f"  市场类型: {basic_info.get('market', 'N/A')}")

        print("\n## 财务数据\n")
        financial = knowledge.get('financial_summary', {})
        print(f"  覆盖年份: {', '.join(map(str, financial.get('years_covered', [])))}")
        print(f"  年报数量: {financial.get('annual_reports_count', 0)}")

        if financial.get('key_metrics'):
            print("\n  关键指标:")
            for year, metrics in list(financial['key_metrics'].items())[-3:]:  # 最近3年
                print(f"\n  {year}年:")
                for metric, value in metrics.items():
                    print(f"    - {metric}: {value}")

        print("\n## 分析报告\n")
        report = knowledge.get('report_summary', {})
        print(f"  报告存在: {'是' if report.get('report_exists') else '否'}")
        print(f"  报告大小: {report.get('report_size', 0) / 1024:.2f} KB")

        if report.get('key_investment_points'):
            print("\n  核心投资逻辑:")
            for i, point in enumerate(report['key_investment_points'][:5], 1):
                print(f"    {i}. {point}")

        print("\n## 公告信息\n")
        announcements = knowledge.get('announcement_summary', {})
        print(f"  公告总数: {announcements.get('total_count', 0)}")
        print(f"  重要公告: {announcements.get('important_count', 0)}")

        if announcements.get('date_range'):
            date_range = announcements['date_range']
            print(f"  日期范围: {date_range.get('start')} 至 {date_range.get('end')}")

        print()

    def compare_companies(self, company_codes):
        """对比多家公司"""
        if len(company_codes) < 2:
            print("错误: 至少需要2家公司进行对比")
            return

        print(f"\n{'='*80}")
        print(f"公司对比分析")
        print(f"{'='*80}\n")

        companies_data = {}
        for code in company_codes:
            knowledge = self.load_company_knowledge(code)
            if knowledge:
                companies_data[code] = knowledge

        if len(companies_data) < 2:
            print("错误: 无法加载足够的公司数据进行对比")
            return

        # 生成对比报告
        self._generate_comparison_report(companies_data)

    def _generate_comparison_report(self, companies_data):
        """生成对比报告"""
        print(f"\n{'='*80}")
        print(f"对比报告")
        print(f"{'='*80}\n")

        # 1. 基本信息对比
        print("## 1. 基本信息对比\n")
        print(f"{'公司代码':<15} {'公司名称':<20} {'市场':<10} {'分析年份'}")
        print("-" * 80)

        for code, data in companies_data.items():
            basic = data.get('basic_info', {})
            financial = data.get('financial_summary', {})
            years = financial.get('years_covered', [])
            years_str = f"{min(years)}-{max(years)}" if years else "N/A"

            print(f"{code:<15} {basic.get('sec_name', 'N/A'):<20} "
                  f"{basic.get('market', 'N/A'):<10} {years_str}")

        # 2. 财务指标对比
        print("\n## 2. 财务指标对比\n")

        # 收集所有公司的最新年份数据
        latest_metrics = {}
        for code, data in companies_data.items():
            financial = data.get('financial_summary', {})
            key_metrics = financial.get('key_metrics', {})
            if key_metrics:
                # 获取最新年份
                latest_year = max(key_metrics.keys())
                latest_metrics[code] = {
                    'year': latest_year,
                    'metrics': key_metrics[latest_year]
                }

        if latest_metrics:
            # 找出所有指标
            all_metric_names = set()
            for code, data in latest_metrics.items():
                all_metric_names.update(data['metrics'].keys())

            # 打印对比表
            print(f"{'指标':<15}", end='')
            for code in companies_data.keys():
                year = latest_metrics.get(code, {}).get('year', 'N/A')
                print(f"{code}({year})"[:20].ljust(20), end='')
            print()
            print("-" * (15 + 20 * len(companies_data)))

            for metric in sorted(all_metric_names):
                print(f"{metric:<15}", end='')
                for code in companies_data.keys():
                    value = latest_metrics.get(code, {}).get('metrics', {}).get(metric, 'N/A')
                    print(f"{str(value)[:20]:<20}", end='')
                print()

        # 3. 投资逻辑对比
        print("\n## 3. 核心投资逻辑对比\n")

        for code, data in companies_data.items():
            report = data.get('report_summary', {})
            points = report.get('key_investment_points', [])

            print(f"\n### {code} - {data.get('basic_info', {}).get('sec_name', 'N/A')}\n")
            if points:
                for i, point in enumerate(points[:5], 1):
                    print(f"  {i}. {point}")
            else:
                print("  暂无投资逻辑摘要")

        # 4. 数据完整性对比
        print("\n## 4. 数据完整性对比\n")
        print(f"{'公司代码':<15} {'年报数':<10} {'公告数':<10} {'报告大小(KB)':<15}")
        print("-" * 80)

        for code, data in companies_data.items():
            financial = data.get('financial_summary', {})
            announcements = data.get('announcement_summary', {})
            report = data.get('report_summary', {})

            annual_count = financial.get('annual_reports_count', 0)
            announcement_count = announcements.get('total_count', 0)
            report_size = report.get('report_size', 0) / 1024

            print(f"{code:<15} {annual_count:<10} {announcement_count:<10} {report_size:<15.2f}")

        print()

    def generate_learning_summary(self, company_code, output_file=None):
        """生成学习摘要（用于快速加载到Claude的上下文）"""
        knowledge = self.load_company_knowledge(company_code)
        if not knowledge:
            return None

        # 生成精简的学习摘要
        learning_summary = self._create_learning_summary(knowledge)

        # 保存到文件
        if output_file is None:
            company_info = self.companies_index.get(company_code, {})
            analysis_dir = Path(company_info.get('analysis_dir', '.'))
            output_file = analysis_dir / 'LEARNING_SUMMARY.md'

        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(learning_summary)

        print(f"\n✓ 学习摘要已保存: {output_file}")
        print(f"  文件大小: {Path(output_file).stat().st_size / 1024:.2f} KB")

        return output_file

    def _create_learning_summary(self, knowledge):
        """创建学习摘要"""
        basic = knowledge.get('basic_info', {})
        financial = knowledge.get('financial_summary', {})
        report = knowledge.get('report_summary', {})
        announcements = knowledge.get('announcement_summary', {})

        summary = f"""# {basic.get('sec_name', '未知公司')} ({basic.get('stock_code', 'N/A')}) - 快速学习摘要

**生成时间**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
**市场**: {basic.get('market', 'N/A')}
**覆盖年份**: {', '.join(map(str, financial.get('years_covered', [])))}

---

## 一、核心投资逻辑

"""

        # 添加投资逻辑
        points = report.get('key_investment_points', [])
        if points:
            for i, point in enumerate(points, 1):
                summary += f"{i}. **{point}**\n"
        else:
            summary += "暂无投资逻辑摘要\n"

        summary += "\n## 二、关键财务数据\n\n"

        # 添加财务数据
        key_metrics = financial.get('key_metrics', {})
        if key_metrics:
            # 按年份排序
            sorted_years = sorted(key_metrics.keys(), reverse=True)

            summary += "| 指标 | " + " | ".join(sorted_years) + " |\n"
            summary += "|" + "---|" * (len(sorted_years) + 1) + "\n"

            # 收集所有指标名称
            all_metrics = set()
            for year_data in key_metrics.values():
                all_metrics.update(year_data.keys())

            # 按指标输出
            for metric in sorted(all_metrics):
                row = f"| {metric} |"
                for year in sorted_years:
                    value = key_metrics[year].get(metric, '-')
                    row += f" {value} |"
                summary += row + "\n"
        else:
            summary += "暂无财务数据\n"

        summary += "\n## 三、报告章节\n\n"

        # 添加报告章节
        sections = report.get('sections', [])
        if sections:
            for section in sections:
                summary += f"- {section}\n"
        else:
            summary += "暂无报告章节信息\n"

        summary += "\n## 四、公告概况\n\n"

        # 添加公告信息
        summary += f"- **公告总数**: {announcements.get('total_count', 0)}\n"
        summary += f"- **重要公告**: {announcements.get('important_count', 0)}\n"

        if announcements.get('date_range'):
            date_range = announcements['date_range']
            summary += f"- **日期范围**: {date_range.get('start')} 至 {date_range.get('end')}\n"

        if announcements.get('sample_titles'):
            summary += "\n**公告示例**:\n"
            for title in announcements['sample_titles'][:5]:
                summary += f"- {title}\n"

        summary += "\n## 五、数据文件位置\n\n"

        # 添加文件位置信息
        metadata = knowledge.get('metadata', {})
        summary += f"- **分析目录**: `{metadata.get('analysis_dir', 'N/A')}`\n"
        summary += f"- **知识摘要**: `knowledge_summary.json`\n"
        summary += f"- **分析报告**: `analysis_report.md`\n"

        inventory = knowledge.get('file_inventory', {})
        summary += f"- **文件总数**: {inventory.get('total_files', 0)}\n"
        summary += f"- **总大小**: {inventory.get('total_size_mb', 0):.2f} MB\n"

        summary += "\n---\n\n"
        summary += "*此摘要用于快速加载公司知识到Claude上下文中*\n"

        return summary

    def search_companies(self, keyword):
        """搜索公司"""
        print(f"\n搜索关键词: {keyword}\n")

        results = []
        for code, info in self.companies_index.items():
            company_name = info.get('company_name', '')
            if keyword.lower() in code.lower() or keyword in company_name:
                results.append({
                    'code': code,
                    'name': company_name,
                    'market': info.get('market', ''),
                    'date': info.get('analysis_date', '')
                })

        if results:
            print(f"找到 {len(results)} 个结果:\n")
            for r in results:
                date = datetime.fromisoformat(r['date']).strftime('%Y-%m-%d') if r['date'] else 'N/A'
                print(f"  {r['code']:<15} {r['name']:<20} {r['market']:<10} {date}")
        else:
            print("未找到匹配的公司")

        print()
        return results


def main():
    import sys

    loader = CompanyKnowledgeLoader()

    if len(sys.argv) < 2:
        print("\n公司分析快速学习工具\n")
        print("用法:")
        print("  python3 quick_learn.py list                    # 列出所有公司")
        print("  python3 quick_learn.py load <code>             # 加载公司知识")
        print("  python3 quick_learn.py compare <code1> <code2> [code3...]  # 对比公司")
        print("  python3 quick_learn.py summary <code>          # 生成学习摘要")
        print("  python3 quick_learn.py search <keyword>        # 搜索公司")
        print("\n示例:")
        print("  python3 quick_learn.py list")
        print("  python3 quick_learn.py load 09992.HK")
        print("  python3 quick_learn.py compare 09992.HK 00700.HK")
        print("  python3 quick_learn.py summary 09992.HK")
        print("  python3 quick_learn.py search 泡泡")
        print()
        sys.exit(0)

    command = sys.argv[1]

    if command == 'list':
        loader.list_companies()

    elif command == 'load':
        if len(sys.argv) < 3:
            print("错误: 请指定公司代码")
            sys.exit(1)
        company_code = sys.argv[2]
        loader.load_company_knowledge(company_code)

    elif command == 'compare':
        if len(sys.argv) < 4:
            print("错误: 至少需要2个公司代码")
            sys.exit(1)
        company_codes = sys.argv[2:]
        loader.compare_companies(company_codes)

    elif command == 'summary':
        if len(sys.argv) < 3:
            print("错误: 请指定公司代码")
            sys.exit(1)
        company_code = sys.argv[2]
        loader.generate_learning_summary(company_code)

    elif command == 'search':
        if len(sys.argv) < 3:
            print("错误: 请指定搜索关键词")
            sys.exit(1)
        keyword = sys.argv[2]
        loader.search_companies(keyword)

    else:
        print(f"错误: 未知命令 '{command}'")
        sys.exit(1)


if __name__ == '__main__':
    main()
