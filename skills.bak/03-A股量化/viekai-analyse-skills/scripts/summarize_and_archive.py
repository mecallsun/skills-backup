#!/usr/bin/env python3
"""
公司分析知识总结和归档工具

功能：
1. 生成公司分析的知识摘要（用于快速加载）
2. 压缩原始数据文件以节省空间
3. 创建索引文件便于后续检索
"""

import os
import json
import shutil
import tarfile
from datetime import datetime
from pathlib import Path


class CompanyKnowledgeSummarizer:
    def __init__(self, analysis_dir):
        self.analysis_dir = Path(analysis_dir)
        self.company_code = self._extract_company_code()
        self.summary_data = {}

    def _extract_company_code(self):
        """从目录名提取公司代码"""
        dir_name = self.analysis_dir.name
        # company_analysis_09992.HK_20260126
        parts = dir_name.split('_')
        if len(parts) >= 3:
            return parts[2]  # 09992.HK
        return "UNKNOWN"

    def generate_summary(self):
        """生成知识摘要"""
        print(f"\n{'='*60}")
        print(f"生成知识摘要: {self.company_code}")
        print(f"{'='*60}\n")

        # 1. 基本信息
        self.summary_data['basic_info'] = self._summarize_basic_info()

        # 2. 财务数据摘要
        self.summary_data['financial_summary'] = self._summarize_financial_data()

        # 3. 报告摘要
        self.summary_data['report_summary'] = self._summarize_report()

        # 4. 公告摘要
        self.summary_data['announcement_summary'] = self._summarize_announcements()

        # 5. 文件清单
        self.summary_data['file_inventory'] = self._create_file_inventory()

        # 6. 元数据
        self.summary_data['metadata'] = {
            'company_code': self.company_code,
            'analysis_date': datetime.now().isoformat(),
            'summary_version': '1.0',
            'analysis_dir': str(self.analysis_dir)
        }

        # 保存摘要
        summary_file = self.analysis_dir / 'knowledge_summary.json'
        with open(summary_file, 'w', encoding='utf-8') as f:
            json.dump(self.summary_data, f, ensure_ascii=False, indent=2)

        print(f"✓ 知识摘要已保存: {summary_file}")

        # 生成可读的Markdown摘要
        self._generate_markdown_summary()

        return self.summary_data

    def _summarize_basic_info(self):
        """总结基本信息"""
        basic_info = {
            'company_code': self.company_code,
            'analysis_directory': str(self.analysis_dir),
            'created_at': datetime.now().isoformat()
        }

        # 读取公司信息
        company_info_file = self.analysis_dir / 'processed_data' / 'company_info.json'
        if company_info_file.exists():
            with open(company_info_file, 'r', encoding='utf-8') as f:
                company_info = json.load(f)
                basic_info.update(company_info)

        return basic_info

    def _summarize_financial_data(self):
        """总结财务数据"""
        financial_summary = {
            'years_covered': [],
            'key_metrics': {},
            'data_sources': []
        }

        # 检查年报数量
        annual_reports_dir = self.analysis_dir / 'raw_data' / 'annual_reports'
        if annual_reports_dir.exists():
            pdf_files = list(annual_reports_dir.glob('*.pdf'))
            txt_files = list(annual_reports_dir.glob('*.txt'))
            financial_summary['annual_reports_count'] = len(pdf_files)
            financial_summary['text_files_count'] = len(txt_files)

            # 提取年份
            years = set()
            for f in pdf_files:
                # 从文件名提取年份
                if '2020' in f.name:
                    years.add(2020)
                if '2021' in f.name:
                    years.add(2021)
                if '2022' in f.name:
                    years.add(2022)
                if '2023' in f.name:
                    years.add(2023)
                if '2024' in f.name:
                    years.add(2024)
                if '2025' in f.name:
                    years.add(2025)

            financial_summary['years_covered'] = sorted(list(years))

        # 读取财务数据文件
        financial_data_file = self.analysis_dir / 'processed_data' / 'financial_data_with_source.json'
        if financial_data_file.exists():
            with open(financial_data_file, 'r', encoding='utf-8') as f:
                financial_data = json.load(f)
                # 提取关键指标
                if financial_data:
                    financial_summary['key_metrics'] = self._extract_key_metrics(financial_data)

        return financial_summary

    def _extract_key_metrics(self, financial_data):
        """从财务数据中提取关键指标"""
        key_metrics = {}

        # 提取最近年份的关键指标
        if isinstance(financial_data, dict):
            for year, data in financial_data.items():
                if isinstance(data, dict):
                    metrics = {}
                    # 提取常见指标
                    for key in ['营业收入', '净利润', '毛利率', '净利率', 'ROE', '总资产', '净资产']:
                        if key in data:
                            metrics[key] = data[key]

                    if metrics:
                        key_metrics[year] = metrics

        return key_metrics

    def _summarize_report(self):
        """总结分析报告"""
        report_summary = {
            'report_exists': False,
            'report_size': 0,
            'sections': []
        }

        report_file = self.analysis_dir / 'analysis_report.md'
        if report_file.exists():
            report_summary['report_exists'] = True
            report_summary['report_size'] = report_file.stat().st_size

            # 读取报告并提取章节标题
            with open(report_file, 'r', encoding='utf-8') as f:
                content = f.read()
                # 提取一级标题
                import re
                sections = re.findall(r'^## (.+)$', content, re.MULTILINE)
                report_summary['sections'] = sections[:20]  # 最多20个章节

                # 提取执行摘要中的关键信息
                exec_summary_match = re.search(
                    r'## 一、执行摘要.*?### 核心投资逻辑(.*?)###',
                    content,
                    re.DOTALL
                )
                if exec_summary_match:
                    exec_summary = exec_summary_match.group(1).strip()
                    # 提取要点（1. 2. 3. ...）
                    points = re.findall(r'^\d+\.\s*\*\*(.+?)\*\*', exec_summary, re.MULTILINE)
                    report_summary['key_investment_points'] = points[:10]

        return report_summary

    def _summarize_announcements(self):
        """总结公告信息"""
        announcement_summary = {
            'total_count': 0,
            'important_count': 0,
            'date_range': {}
        }

        # 读取所有公告
        all_announcements_file = self.analysis_dir / 'processed_data' / 'all_announcements.json'
        if all_announcements_file.exists():
            with open(all_announcements_file, 'r', encoding='utf-8') as f:
                announcements = json.load(f)
                announcement_summary['total_count'] = len(announcements)

                if announcements:
                    # 提取日期范围
                    dates = [a.get('date', 0) for a in announcements if 'date' in a]
                    if dates:
                        from datetime import datetime
                        min_date = datetime.fromtimestamp(min(dates) / 1000).strftime('%Y-%m-%d')
                        max_date = datetime.fromtimestamp(max(dates) / 1000).strftime('%Y-%m-%d')
                        announcement_summary['date_range'] = {
                            'start': min_date,
                            'end': max_date
                        }

                    # 统计公告类型
                    titles = [a.get('title', '') for a in announcements]
                    announcement_summary['sample_titles'] = titles[:10]

        # 读取重要公告
        important_announcements_file = self.analysis_dir / 'processed_data' / 'important_announcements.json'
        if important_announcements_file.exists():
            with open(important_announcements_file, 'r', encoding='utf-8') as f:
                important = json.load(f)
                announcement_summary['important_count'] = len(important)

        return announcement_summary

    def _create_file_inventory(self):
        """创建文件清单"""
        inventory = {
            'total_files': 0,
            'total_size_mb': 0,
            'by_category': {}
        }

        categories = {
            'raw_data': self.analysis_dir / 'raw_data',
            'processed_data': self.analysis_dir / 'processed_data',
            'reports': self.analysis_dir
        }

        for category, path in categories.items():
            if path.exists():
                files = []
                total_size = 0

                for file_path in path.rglob('*'):
                    if file_path.is_file():
                        size = file_path.stat().st_size
                        files.append({
                            'name': file_path.name,
                            'path': str(file_path.relative_to(self.analysis_dir)),
                            'size_mb': round(size / 1024 / 1024, 2)
                        })
                        total_size += size

                inventory['by_category'][category] = {
                    'file_count': len(files),
                    'total_size_mb': round(total_size / 1024 / 1024, 2),
                    'files': files[:50]  # 最多列出50个文件
                }

                inventory['total_files'] += len(files)
                inventory['total_size_mb'] += round(total_size / 1024 / 1024, 2)

        return inventory

    def _generate_markdown_summary(self):
        """生成Markdown格式的摘要"""
        md_content = f"""# 公司分析知识摘要

**公司代码**: {self.company_code}
**分析日期**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
**摘要版本**: 1.0

---

## 一、基本信息

"""

        basic_info = self.summary_data.get('basic_info', {})
        for key, value in basic_info.items():
            md_content += f"- **{key}**: {value}\n"

        md_content += "\n## 二、财务数据摘要\n\n"

        financial = self.summary_data.get('financial_summary', {})
        md_content += f"- **覆盖年份**: {', '.join(map(str, financial.get('years_covered', [])))}\n"
        md_content += f"- **年报数量**: {financial.get('annual_reports_count', 0)}\n"
        md_content += f"- **文本文件数量**: {financial.get('text_files_count', 0)}\n"

        if financial.get('key_metrics'):
            md_content += "\n### 关键财务指标\n\n"
            for year, metrics in financial['key_metrics'].items():
                md_content += f"\n**{year}年**:\n"
                for metric, value in metrics.items():
                    md_content += f"- {metric}: {value}\n"

        md_content += "\n## 三、分析报告摘要\n\n"

        report = self.summary_data.get('report_summary', {})
        md_content += f"- **报告存在**: {'是' if report.get('report_exists') else '否'}\n"
        md_content += f"- **报告大小**: {report.get('report_size', 0) / 1024:.2f} KB\n"

        if report.get('key_investment_points'):
            md_content += "\n### 核心投资逻辑\n\n"
            for i, point in enumerate(report['key_investment_points'], 1):
                md_content += f"{i}. {point}\n"

        if report.get('sections'):
            md_content += "\n### 报告章节\n\n"
            for section in report['sections']:
                md_content += f"- {section}\n"

        md_content += "\n## 四、公告摘要\n\n"

        announcements = self.summary_data.get('announcement_summary', {})
        md_content += f"- **公告总数**: {announcements.get('total_count', 0)}\n"
        md_content += f"- **重要公告数**: {announcements.get('important_count', 0)}\n"

        if announcements.get('date_range'):
            date_range = announcements['date_range']
            md_content += f"- **日期范围**: {date_range.get('start')} 至 {date_range.get('end')}\n"

        md_content += "\n## 五、文件清单\n\n"

        inventory = self.summary_data.get('file_inventory', {})
        md_content += f"- **文件总数**: {inventory.get('total_files', 0)}\n"
        md_content += f"- **总大小**: {inventory.get('total_size_mb', 0):.2f} MB\n"

        if inventory.get('by_category'):
            md_content += "\n### 分类统计\n\n"
            for category, data in inventory['by_category'].items():
                md_content += f"\n**{category}**:\n"
                md_content += f"- 文件数: {data.get('file_count', 0)}\n"
                md_content += f"- 大小: {data.get('total_size_mb', 0):.2f} MB\n"

        md_content += "\n---\n\n"
        md_content += f"*此摘要由 Company Financial Analysis Skill 自动生成*\n"

        # 保存Markdown摘要
        md_file = self.analysis_dir / 'KNOWLEDGE_SUMMARY.md'
        with open(md_file, 'w', encoding='utf-8') as f:
            f.write(md_content)

        print(f"✓ Markdown摘要已保存: {md_file}")

    def archive_raw_data(self, keep_original=True):
        """压缩原始数据"""
        print(f"\n{'='*60}")
        print(f"压缩原始数据")
        print(f"{'='*60}\n")

        raw_data_dir = self.analysis_dir / 'raw_data'
        if not raw_data_dir.exists():
            print("⚠ 未找到原始数据目录")
            return None

        # 创建压缩文件
        archive_name = f"{self.company_code}_raw_data_{datetime.now().strftime('%Y%m%d')}.tar.gz"
        archive_path = self.analysis_dir / archive_name

        print(f"正在压缩: {raw_data_dir}")
        print(f"目标文件: {archive_path}")

        with tarfile.open(archive_path, 'w:gz') as tar:
            tar.add(raw_data_dir, arcname='raw_data')

        # 计算压缩比
        original_size = sum(f.stat().st_size for f in raw_data_dir.rglob('*') if f.is_file())
        compressed_size = archive_path.stat().st_size
        compression_ratio = (1 - compressed_size / original_size) * 100 if original_size > 0 else 0

        print(f"\n✓ 压缩完成!")
        print(f"  原始大小: {original_size / 1024 / 1024:.2f} MB")
        print(f"  压缩后大小: {compressed_size / 1024 / 1024:.2f} MB")
        print(f"  压缩比: {compression_ratio:.1f}%")

        # 如果不保留原始文件，删除原始目录
        if not keep_original:
            print(f"\n删除原始数据目录...")
            shutil.rmtree(raw_data_dir)
            print(f"✓ 原始数据已删除，仅保留压缩文件")

        return archive_path

    def create_quick_index(self):
        """创建快速索引文件"""
        print(f"\n{'='*60}")
        print(f"创建快速索引")
        print(f"{'='*60}\n")

        index_data = {
            'company_code': self.company_code,
            'company_name': self.summary_data.get('basic_info', {}).get('sec_name', ''),
            'market': self.summary_data.get('basic_info', {}).get('market', ''),
            'analysis_date': datetime.now().isoformat(),
            'analysis_dir': str(self.analysis_dir),
            'summary_file': str(self.analysis_dir / 'knowledge_summary.json'),
            'report_file': str(self.analysis_dir / 'analysis_report.md'),
            'years_covered': self.summary_data.get('financial_summary', {}).get('years_covered', []),
            'key_investment_points': self.summary_data.get('report_summary', {}).get('key_investment_points', []),
            'file_count': self.summary_data.get('file_inventory', {}).get('total_files', 0),
            'total_size_mb': self.summary_data.get('file_inventory', {}).get('total_size_mb', 0)
        }

        # 保存到全局索引
        index_file = self.analysis_dir.parent / 'company_index.json'

        # 读取现有索引
        all_companies = {}
        if index_file.exists():
            with open(index_file, 'r', encoding='utf-8') as f:
                all_companies = json.load(f)

        # 更新索引
        all_companies[self.company_code] = index_data

        # 保存索引
        with open(index_file, 'w', encoding='utf-8') as f:
            json.dump(all_companies, f, ensure_ascii=False, indent=2)

        print(f"✓ 快速索引已更新: {index_file}")
        print(f"  当前索引包含 {len(all_companies)} 家公司")

        return index_file


def main():
    import sys

    if len(sys.argv) < 2:
        print("用法: python3 summarize_and_archive.py <analysis_directory> [--no-keep-original]")
        print("\n示例:")
        print("  python3 summarize_and_archive.py company_analysis_09992.HK_20260126")
        print("  python3 summarize_and_archive.py company_analysis_09992.HK_20260126 --no-keep-original")
        sys.exit(1)

    analysis_dir = sys.argv[1]
    keep_original = '--no-keep-original' not in sys.argv

    if not os.path.exists(analysis_dir):
        print(f"错误: 目录不存在: {analysis_dir}")
        sys.exit(1)

    print(f"\n{'='*60}")
    print(f"公司分析知识总结和归档工具")
    print(f"{'='*60}\n")
    print(f"分析目录: {analysis_dir}")
    print(f"保留原始文件: {'是' if keep_original else '否'}")
    print()

    # 创建总结器
    summarizer = CompanyKnowledgeSummarizer(analysis_dir)

    # 1. 生成知识摘要
    summary = summarizer.generate_summary()

    # 2. 压缩原始数据
    archive_path = summarizer.archive_raw_data(keep_original=keep_original)

    # 3. 创建快速索引
    index_file = summarizer.create_quick_index()

    print(f"\n{'='*60}")
    print(f"完成!")
    print(f"{'='*60}\n")
    print(f"✓ 知识摘要: {analysis_dir}/knowledge_summary.json")
    print(f"✓ Markdown摘要: {analysis_dir}/KNOWLEDGE_SUMMARY.md")
    if archive_path:
        print(f"✓ 压缩文件: {archive_path}")
    print(f"✓ 全局索引: {index_file}")
    print()


if __name__ == '__main__':
    main()
