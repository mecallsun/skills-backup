#!/usr/bin/env python3
"""
公司财务分析主脚本

数据获取方式：
- A股：从巨潮资讯网下载财报和公告（年报+季报）
- 港股：从港交所披露易下载财报和公告（年报+季报）
- 美股：从SEC EDGAR下载财报（10-K、10-Q、8-K等）
- 财务数据只从下载的财报PDF中提取，不从网上搜索
- 市场情绪通过 web_search 采集公开新闻分析

分析流程：
1. 下载近5年年报和季报
2. 下载近5年所有公告
3. 从财报中提取财务数据（带来源标注）
4. 生成核心指标表格
5. 生成新闻采集指引（通过 web_search 执行）
6. 生成分析报告
"""
import sys
import os
from pathlib import Path
import json
import shutil
from datetime import datetime

# Add scripts directory to path
sys.path.insert(0, str(Path(__file__).parent))

# 默认报告输出目录
DEFAULT_REPORTS_DIR = Path.home() / 'Desktop' / 'reports'

from utils import create_output_directory, normalize_stock_code, save_json, load_json


def save_report_to_desktop(source_report: Path, stock_code: str, reports_dir: Path = None) -> Path:
    """
    将分析报告保存到桌面报告目录

    Args:
        source_report: 源报告路径
        stock_code: 股票代码
        reports_dir: 报告目录，默认 ~/Desktop/reports/

    Returns:
        目标报告路径
    """
    if reports_dir is None:
        reports_dir = DEFAULT_REPORTS_DIR

    # 创建报告目录
    reports_dir.mkdir(parents=True, exist_ok=True)

    # 生成报告文件名: {stock_code}_{date}_analysis.md
    date_str = datetime.now().strftime('%Y%m%d')
    report_name = f"{stock_code}_{date_str}_analysis.md"
    target_path = reports_dir / report_name

    # 复制报告
    if source_report.exists():
        shutil.copy2(source_report, target_path)
        print(f"  ✓ 报告已保存到: {target_path}")
        return target_path
    else:
        print(f"  ✗ 源报告不存在: {source_report}")
        return None


def download_reports(stock_code: str, output_dir: Path, years: int = 5) -> dict:
    """
    下载年报、季报和公告

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的数据
    """
    print("\n" + "="*80)
    print("阶段 1: 下载财报和公告（含季报）")
    print("="*80 + "\n")

    try:
        from fetch_reports import download_reports_and_announcements
        result = download_reports_and_announcements(stock_code, output_dir, years)
        return result
    except Exception as e:
        print(f"下载失败: {e}")
        return {
            'stock_code': stock_code,
            'annual_reports': [],
            'quarterly_reports': {},
            'announcements': [],
        }


def extract_financial_data(stock_code: str, output_dir: Path) -> dict:
    """
    从年报PDF中提取财务数据

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
    """
    print("\n" + "="*80)
    print("阶段 2: 从年报中提取财务数据")
    print("="*80 + "\n")

    try:
        from fetch_financial_from_reports import main as extract_from_reports
        financial_data = extract_from_reports(stock_code, output_dir)
        return financial_data
    except Exception as e:
        print(f"提取财务数据失败: {e}")
        return {}


def fetch_industry_data(stock_code: str, output_dir: Path) -> dict:
    """
    获取行业数据

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
    """
    print("\n" + "="*80)
    print("阶段 3: 获取行业数据")
    print("="*80 + "\n")

    try:
        from fetch_industry_data import main as fetch_industry
        industry_data = fetch_industry(stock_code, "", output_dir)
        return industry_data
    except Exception as e:
        print(f"获取行业数据失败: {e}")
        return {}


def generate_metrics_tables(output_dir: Path) -> bool:
    """
    生成核心指标表格

    Args:
        output_dir: 输出目录

    Returns:
        是否成功
    """
    print("\n" + "="*80)
    print("阶段 2.6: 生成核心指标表格")
    print("="*80 + "\n")

    try:
        from generate_metrics_table import main as generate_tables
        return generate_tables(output_dir)
    except Exception as e:
        print(f"生成指标表格失败: {e}")
        return False


def fetch_news_guidance(company_name: str, stock_code: str, output_dir: Path) -> dict:
    """
    生成新闻采集指引
    
    实际的新闻采集需要通过 Claude Code 的 web_search 工具完成
    此函数生成采集指引供后续分析使用

    Args:
        company_name: 公司名称
        stock_code: 股票代码
        output_dir: 输出目录

    Returns:
        采集指引
    """
    print("\n" + "="*80)
    print("阶段 4: 生成新闻采集指引")
    print("="*80 + "\n")

    try:
        from fetch_news import main as generate_news_guidance
        return generate_news_guidance(company_name, stock_code, output_dir)
    except Exception as e:
        print(f"生成新闻采集指引失败: {e}")
        return {}


def prepare_analysis_context(output_dir: Path) -> str:
    """
    准备AI分析上下文（精简版）

    优化：不再嵌入大量JSON数据，改为提供文件路径和工具使用说明
    """
    context_parts = []

    # 1. 公司基本信息（保留，很小）
    try:
        company_info = load_json(output_dir / 'processed_data' / 'company_info.json')
        context_parts.append(f"## 公司基本信息\n{json.dumps(company_info, ensure_ascii=False, indent=2)}")

        # 如果有市场数据，单独高亮显示
        if 'market_data' in company_info:
            md = company_info['market_data']
            market_summary = f"""
## 实时市场数据

| 指标 | 数值 |
|------|------|
| 当前股价 | {md.get('price', 'N/A')} 元 |
| 昨日收盘 | {md.get('prev_close', 'N/A')} 元 |
| 涨跌额 | {md.get('change', 'N/A')} 元 |
| 涨跌幅 | {md.get('change_percent', 'N/A')}% |
| 更新时间 | {md.get('update_time', 'N/A')} |

**注意**: 使用此实时价格计算市值、PE等估值指标时，需结合财报中的净利润和总股本数据。
"""
            context_parts.append(market_summary)
    except:
        pass

    # 2. 数据文件位置说明
    reports_dir = output_dir / 'raw_data' / 'annual_reports'
    txt_files = list(reports_dir.glob('*.txt')) if reports_dir.exists() else []

    # 列出所有季度报告目录
    quarterly_dirs = ['semi_annual_reports', 'q1_reports', 'q3_reports']
    quarterly_info = []
    for dir_name in quarterly_dirs:
        dir_path = output_dir / 'raw_data' / dir_name
        if dir_path.exists():
            pdf_count = len(list(dir_path.glob('*.pdf')))
            if pdf_count > 0:
                quarterly_info.append(f"  - {dir_name}: {pdf_count} 份")

    context_parts.append(f"""## 数据文件位置

### 年报文本文件（可使用grep搜索）
目录: `{reports_dir}`
""")

    if txt_files:
        for txt_file in sorted(txt_files):
            size_kb = txt_file.stat().st_size / 1024
            context_parts[-1] += f"- `{txt_file.name}` ({size_kb:.0f} KB)\n"
    else:
        context_parts[-1] += "- 暂无文本文件\n"

    if quarterly_info:
        context_parts[-1] += f"\n### 季度报告\n" + "\n".join(quarterly_info)

    # 3. 核心指标表格（优先展示）
    try:
        metrics_md_path = output_dir / 'processed_data' / 'metrics_tables.md'
        if metrics_md_path.exists():
            with open(metrics_md_path, 'r', encoding='utf-8') as f:
                metrics_content = f.read()
            context_parts.append(metrics_content)
    except:
        pass

    # 4. 索引摘要（如果存在）
    try:
        index_summary_path = output_dir / 'processed_data' / 'financial_index_summary.txt'
        if index_summary_path.exists():
            with open(index_summary_path, 'r', encoding='utf-8') as f:
                index_summary = f.read()
            context_parts.append(f"## 财务指标索引\n{index_summary}")
    except:
        pass

    # 5. 公告统计信息（不嵌入完整数据）
    try:
        all_announcements = load_json(output_dir / 'processed_data' / 'all_announcements.json')
        if all_announcements:
            # 只提供统计信息和文件路径
            announcements_file = output_dir / 'processed_data' / 'all_announcements.json'
            context_parts.append(f"""## 公告数据
- 公告总数: {len(all_announcements)} 条
- 数据文件: `{announcements_file}`
- 使用方法: 读取JSON文件获取公告列表，每条公告包含 title, date, type 等字段""")
    except:
        pass

    # 6. 重要公告列表（只保留标题和日期）
    try:
        important_announcements = load_json(output_dir / 'processed_data' / 'important_announcements.json')
        if important_announcements:
            # 精简为只包含标题和日期
            brief_list = []
            for ann in important_announcements[:20]:  # 最多20条
                brief_list.append(f"- [{ann.get('date', 'N/A')}] {ann.get('title', 'N/A')}")
            context_parts.append(f"## 重要公告摘要（共{len(important_announcements)}条）\n" + "\n".join(brief_list))
    except:
        pass

    # 7. 行业信息（保留，通常很小）
    try:
        industry_info = load_json(output_dir / 'processed_data' / 'industry_info.json')
        if industry_info:
            context_parts.append(f"## 行业信息\n{json.dumps(industry_info, ensure_ascii=False)}")
    except:
        pass

    # 8. 新闻分析（如果已采集）
    try:
        news_analysis = load_json(output_dir / 'processed_data' / 'news_analysis.json')
        if news_analysis and not news_analysis.get('error'):
            from fetch_news import format_news_for_analysis
            news_summary = format_news_for_analysis(news_analysis)
            if news_summary:
                context_parts.append(news_summary)
    except:
        pass
    
    # 8.1 新闻采集指引（如果尚未采集）
    try:
        news_prompt_file = output_dir / 'processed_data' / 'news_search_prompt.md'
        if news_prompt_file.exists():
            news_analysis = load_json(output_dir / 'processed_data' / 'news_analysis.json')
            if not news_analysis:  # 尚未采集
                context_parts.append(f"""## 新闻采集待完成

新闻采集指引已生成: `{news_prompt_file}`

请使用 web_search 工具执行新闻搜索，完成市场情绪分析。
""")
    except:
        pass

    return "\n\n".join(context_parts)


def generate_analysis_prompt(company_name: str, stock_code: str, context: str, output_dir: Path = None) -> str:
    """生成AI分析提示词（优化版：按需读取模式）"""

    # 构建数据访问说明
    data_access_guide = ""
    if output_dir:
        reports_dir = output_dir / 'raw_data' / 'annual_reports'

        # 检查最新季度报表
        current_year = datetime.now().year
        last_year = current_year - 1
        latest_quarterly_info = ""

        # 检查最近两年的季度报表（优先当年，其次去年）
        q3_dir = output_dir / 'raw_data' / 'q3_reports'
        semi_dir = output_dir / 'raw_data' / 'semi_annual_reports'
        q1_dir = output_dir / 'raw_data' / 'q1_reports'
        annual_dir = output_dir / 'raw_data' / 'annual_reports'

        # 检查当年年报是否已发布
        current_year_annual = list(annual_dir.glob(f'*{current_year}*.pdf')) if annual_dir.exists() else []

        if not current_year_annual:
            # 当年年报未发布，检查最新季度报表
            # 优先级: 当年Q3 > 当年半年报 > 当年Q1 > 去年Q3 > 去年半年报
            if q3_dir.exists():
                q3_current = list(q3_dir.glob(f'*{current_year}*.pdf'))
                q3_last = list(q3_dir.glob(f'*{last_year}*.pdf'))

                if q3_current:
                    latest_quarterly_info = f"""
**重要**: {current_year}年年报尚未发布，但已有{current_year}年前三季度财报。
- 三季报目录: `{q3_dir}`
- 请优先分析最新的三季报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
"""
                elif q3_last:
                    latest_quarterly_info = f"""
**重要**: {current_year}年年报尚未发布，最新财报为{last_year}年前三季度。
- 三季报目录: `{q3_dir}`
- 请优先分析{last_year}年三季报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
"""

            if not latest_quarterly_info and semi_dir.exists():
                semi_current = list(semi_dir.glob(f'*{current_year}*.pdf'))
                semi_last = list(semi_dir.glob(f'*{last_year}*.pdf'))

                if semi_current:
                    latest_quarterly_info = f"""
**重要**: {current_year}年年报尚未发布，但已有{current_year}年半年报。
- 半年报目录: `{semi_dir}`
- 请优先分析最新的半年报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
"""
                elif semi_last:
                    latest_quarterly_info = f"""
**重要**: {current_year}年年报尚未发布，最新财报为{last_year}年半年报。
- 半年报目录: `{semi_dir}`
- 请优先分析{last_year}年半年报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
"""

            if not latest_quarterly_info and q1_dir.exists():
                q1_current = list(q1_dir.glob(f'*{current_year}*.pdf'))
                q1_last = list(q1_dir.glob(f'*{last_year}*.pdf'))

                if q1_current:
                    latest_quarterly_info = f"""
**重要**: {current_year}年年报尚未发布，但已有{current_year}年一季报。
- 一季报目录: `{q1_dir}`
- 请优先分析最新的一季报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
"""
                elif q1_last:
                    latest_quarterly_info = f"""
**重要**: {current_year}年年报尚未发布，最新财报为{last_year}年一季报。
- 一季报目录: `{q1_dir}`
- 请优先分析{last_year}年一季报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
"""

        data_access_guide = f"""
## 数据访问方式

财务数据已保存为可搜索的文本格式。请使用工具按需查询，而不是一次性读取所有内容。
{latest_quarterly_info}
### 搜索指南

**查找财务指标**（使用grep搜索年报文本）:
- 营收相关: `grep "营业收入|营业总收入|Revenue" {reports_dir}/*.txt`
- 利润相关: `grep "净利润|归属于.*净利润|Net Profit" {reports_dir}/*.txt`
- 资产相关: `grep "总资产|资产总计|Total Assets" {reports_dir}/*.txt`
- 比率指标: `grep "毛利率|净利率|ROE|净资产收益率" {reports_dir}/*.txt`

**查找季度报表**（使用Read工具直接读取PDF或转换后的文本）:
- 三季报: `{output_dir}/raw_data/q3_reports/`
- 半年报: `{output_dir}/raw_data/semi_annual_reports/`
- 一季报: `{output_dir}/raw_data/q1_reports/`

**读取详细内容**:
- 使用 Read 工具读取特定文本文件或PDF
- 根据索引中的行号定位具体数据
- 每次只读取需要的部分，避免加载过多内容

### 分析流程建议

1. **优先分析最新季度数据**（如果当年年报未发布）
2. 先阅读财务指标索引，了解数据位置
3. 使用 grep 搜索关键指标
4. 读取相关上下文获取详细数据
5. 引用数据时注明来源（年份/季度、页码）
"""

    prompt = f"""# 投资分析任务

请为 {company_name} ({stock_code}) 生成一份专业的投资分析报告。

**重要说明**：
- 所有财务数据均来源于公司年报和季度报表
- **优先分析最新的季度报表**（如当年年报未发布，则分析最新的三季报/半年报/一季报）
- 请使用工具按需搜索和读取数据，不要假设数据内容
- 引用数据时注明来源（年份/季度、页码）
- 结合历年年报和最新季报，分析公司最新经营状况和发展趋势
{data_access_guide}
## 分析框架

### 1. 执行摘要
- 核心投资逻辑（3-5个要点）
- 关键财务指标总览
- 主要风险提示
- 投资建议

### 2. 公司概况
- 基本信息
- 主营业务
- 发展历程

### 3. 行业分析
#### 3.1 行业定位
- 所属行业及细分领域
- 行业分类

#### 3.2 行业空间与增长
- 行业市场规模
- 行业增长趋势
- 行业驱动因素

#### 3.3 竞争格局
- 行业集中度
- 主要竞争对手
- 竞争态势

#### 3.4 公司行业地位
- 市场份额
- 行业排名
- 竞争优势

### 4. 商业模式分析
#### 4.1 价值主张
- 为客户创造的核心价值
- 产品/服务特点

#### 4.2 客户与市场
- 目标客户群体
- 客户类型（B2B/B2C/B2G）

#### 4.3 收入模式
- 收入来源结构
- 收入稳定性

#### 4.4 成本结构
- 主要成本项
- 成本优化空间

#### 4.5 竞争壁垒
- 技术壁垒
- 品牌壁垒
- 规模经济
- 网络效应

### 5. 财务分析（杜邦分析框架）

**核心公式**: ROE = 净利率 × 资产周转率 × 权益乘数

#### 5.1 ROE 总览
- ROE 历史趋势（5年）
- ROE 驱动因素分解

#### 5.2 盈利能力分析（净利率）
- 毛利率趋势
- 净利率趋势
- 净利率与商业模式的关系

#### 5.3 运营效率分析（资产周转率）
- 资产周转率趋势
- 周转率与商业模式的关系

#### 5.4 财务杠杆分析（权益乘数）
- 资产负债率
- 杠杆水平评估

#### 5.5 现金流质量分析
- 经营现金流 vs 净利润
- 现金流稳定性

### 6. 公告分析
基于近5年的公司公告，分析：
- 重大事项（并购、重组、诉讼等）
- 业绩变化趋势
- 管理层变动
- 股东行为（增减持、质押等）
- 潜在风险信号

### 7. 风险分析
#### 7.1 财务风险
- 偿债能力风险
- 现金流风险
- 应收账款风险
- 商誉减值风险

#### 7.2 业务风险
- 基于公告分析的业务风险
- 行业政策风险
- 竞争风险

### 8. 估值分析
- 当前估值水平
- 历史估值区间
- 合理估值区间判断

### 9. 投资建议
- 投资逻辑总结
- 风险收益比评估
- 建议操作策略
- 关注要点

---

## 数据上下文

{context}

---

## 输出要求

1. 使用中文撰写
2. 使用 Markdown 格式
3. 数据引用要准确，注明来源于年报
4. 分析要深入，结合商业模式和财务数据
5. 充分利用公告信息进行风险分析
6. 风险提示要明确
7. 结论要客观，避免过度乐观或悲观
8. 使用表格展示关键财务指标对比
9. 重点分析 ROE 的三因素拆解及其与商业模式的关系

请开始分析。
"""
    return prompt


def main():
    """主函数"""
    if len(sys.argv) < 2:
        print("Usage: python analyze_company.py <stock_code> [years] [--skip-news]")
        print("Example: python analyze_company.py 600519")
        print("         python analyze_company.py 09992 5")
        print("         python analyze_company.py 600519 5 --skip-news")
        sys.exit(1)

    stock_code = sys.argv[1]
    years = int(sys.argv[2]) if len(sys.argv) > 2 and not sys.argv[2].startswith('--') else 5
    skip_news = '--skip-news' in sys.argv

    normalized_code, market, market_type = normalize_stock_code(stock_code)

    print("\n" + "="*80)
    print(f"公司财务分析系统 v3.0")
    print(f"股票代码: {normalized_code}")
    print(f"市场类型: {market_type}")
    print(f"数据范围: 最近 {years} 年")
    print(f"新闻采集: {'否' if skip_news else '是（通过 web_search）'}")
    print("="*80)

    # 创建输出目录
    output_dir = create_output_directory(normalized_code)
    print(f"\n输出目录: {output_dir}\n")

    # 保存基本公司信息（包含实时市场数据）
    company_info = {
        'stock_code': normalized_code,
        'market': market,
        'market_type': market_type,
    }

    # 尝试获取实时股价和市值数据
    try:
        print("\n" + "="*80)
        print("获取实时股价和市值数据")
        print("="*80 + "\n")

        from stock_price_fetcher import StockPriceFetcher
        fetcher = StockPriceFetcher()

        # 检查服务可用性
        if fetcher.health_check():
            # 获取股票行情
            quote = fetcher.get_stock_quote(normalized_code)
            if quote:
                company_info['market_data'] = {
                    'price': quote.get('price'),
                    'prev_close': quote.get('prev_close'),
                    'change': quote.get('change'),
                    'change_percent': quote.get('change_percent'),
                    'update_time': quote.get('update_time'),
                    'source': quote.get('source')
                }
                print(f"  ✓ 当前股价: {quote.get('price')} 元")
                print(f"  ✓ 数据来源: {quote.get('source')}")
                print(f"  ✓ 更新时间: {quote.get('update_time')}")
            else:
                print("  ⚠ 无法获取实时股价（将继续分析）")
        else:
            print("  ⚠ 股价API服务不可用（将继续分析）")
    except Exception as e:
        print(f"  ⚠ 获取实时数据失败: {e}（将继续分析）")

    save_json(company_info, output_dir / 'processed_data' / 'company_info.json')

    # 阶段1: 下载年报、季报和公告
    download_result = download_reports(normalized_code, output_dir, years)

    # 阶段2: 从财报中提取财务数据（同时生成文本文件，带来源标注）
    financial_data = extract_financial_data(normalized_code, output_dir)

    # 阶段2.5: 生成财务指标索引
    print("\n" + "="*80)
    print("阶段 2.5: 生成财务指标索引")
    print("="*80 + "\n")
    try:
        from generate_index import main as generate_index
        generate_index(output_dir)
    except Exception as e:
        print(f"索引生成失败（可选步骤）: {e}")

    # 阶段2.6: 生成核心指标表格
    generate_metrics_tables(output_dir)

    # 阶段3: 获取行业数据（可选）
    industry_data = fetch_industry_data(normalized_code, output_dir)

    # 阶段4: 生成新闻采集指引（可选）
    news_guidance = {}
    if not skip_news:
        # 尝试从年报中提取公司名称
        company_name = normalized_code  # 默认使用股票代码
        try:
            company_info = load_json(output_dir / 'processed_data' / 'company_info.json')
            if company_info and company_info.get('company_name'):
                company_name = company_info.get('company_name')
        except:
            pass
        news_guidance = fetch_news_guidance(company_name, normalized_code, output_dir)
    else:
        print("\n" + "="*80)
        print("阶段 4: 跳过新闻采集")
        print("="*80 + "\n")

    # 阶段5: 准备分析上下文
    print("\n" + "="*80)
    print("阶段 5: 准备分析上下文")
    print("="*80 + "\n")

    context = prepare_analysis_context(output_dir)
    company_name = normalized_code  # 可以从年报中提取公司名称

    # 生成分析提示词（传入output_dir以生成数据访问指南）
    analysis_prompt = generate_analysis_prompt(company_name, normalized_code, context, output_dir)

    # 保存提示词
    prompt_file = output_dir / 'analysis_prompt.txt'
    with open(prompt_file, 'w', encoding='utf-8') as f:
        f.write(analysis_prompt)

    print(f"分析提示词已保存: {prompt_file}")

    # 打印完成信息
    print("\n" + "="*80)
    print("数据收集完成")
    print("="*80)

    # 统计季报数量
    quarterly_counts = {}
    for report_type in ['semi', 'q1', 'q3']:
        quarterly_counts[report_type] = len(download_result.get('quarterly_reports', {}).get(report_type, []))

    print(f"\n数据摘要:")
    print(f"  年报数量: {len(download_result.get('annual_reports', []))}")
    print(f"  半年报数量: {quarterly_counts.get('semi', 0)}")
    print(f"  一季报数量: {quarterly_counts.get('q1', 0)}")
    print(f"  三季报数量: {quarterly_counts.get('q3', 0)}")
    print(f"  公告数量: {len(download_result.get('announcements', []))}")
    print(f"  财务数据年份: {list(financial_data.keys()) if financial_data else '无'}")

    if news_guidance and news_guidance.get('queries'):
        print(f"  新闻采集: 已生成 {len(news_guidance.get('queries', []))} 条搜索查询")
        print(f"  采集指引: {news_guidance.get('prompt_file', 'N/A')}")

    print(f"\n所有结果已保存到: {output_dir}")

    # 列出生成的文件
    print(f"\n生成的文件:")
    files_to_check = [
        ('analysis_prompt.txt', '分析提示词'),
        ('processed_data/financial_data_with_source.json', '带来源的财务数据'),
        ('processed_data/metrics_tables.md', '核心指标表格'),
        ('processed_data/news_search_prompt.md', '新闻采集指引'),
        ('processed_data/news_analysis.json', '新闻分析结果'),
    ]
    for file_path, description in files_to_check:
        full_path = output_dir / file_path
        if full_path.exists():
            size_kb = full_path.stat().st_size / 1024
            print(f"  ✓ {description}: {file_path} ({size_kb:.1f} KB)")

    # 检查是否已有分析报告，如果有则复制到桌面报告目录
    report_file = output_dir / 'analysis_report.md'
    if report_file.exists():
        print("\n" + "="*80)
        print("保存分析报告")
        print("="*80 + "\n")
        saved_report = save_report_to_desktop(report_file, normalized_code)
        if saved_report:
            print(f"\n报告已保存到桌面: {saved_report}")

    print("\n" + "="*80)
    print("下一步: AI 分析")
    print("="*80)

    print(f"""
请使用以下方式完成分析：

1. 读取分析提示词: {prompt_file}
2. 将提示词发送给 Claude 进行分析
3. 将分析结果保存到: {output_dir / 'analysis_report.md'}

分析完成后，报告将自动保存到: {DEFAULT_REPORTS_DIR}

或者直接告诉我"生成分析报告"，我会基于收集的数据生成报告。
""")


if __name__ == '__main__':
    main()
