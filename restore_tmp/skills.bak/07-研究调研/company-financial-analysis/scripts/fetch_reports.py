#!/usr/bin/env python3
"""
从巨潮资讯网下载公司财报和公告

数据来源：
- A股：巨潮资讯网 (cninfo.com.cn)
- 港股：巨潮资讯网港股频道 (cninfo.com.cn/hke)

支持的报告类型：
- 年报 (annual)
- 半年报 (semi)
- 一季报 (q1)
- 三季报 (q3)

使用 CnInfoReports 开源库实现下载功能
"""
import sys
import os
from pathlib import Path
from datetime import datetime
import json
import shutil
import logging
from sys import stdout
from typing import Dict, List, Optional

import httpx


# ============================================================
# 报告类型配置
# ============================================================

# A股报告类型 -> 巨潮分类代码映射
A_SHARE_REPORT_CATEGORIES = {
    'annual': 'category_ndbg_szsh',      # 年报
    'semi': 'category_bndbg_szsh',       # 半年报
    'q1': 'category_yjdbg_szsh',         # 一季报
    'q3': 'category_sjdbg_szsh',         # 三季报
}

# 港股报告类型 -> 标题关键词映射
HK_REPORT_KEYWORDS = {
    'annual': ['年度业绩', '全年业绩', '年報', '年度報告', 'Annual Report', 'Annual Results'],
    'semi': ['中期业绩', '中期報告', '中期报告', '半年報', '六个月业绩', 'Interim Report', 'Interim Results'],
    'q1': ['第一季', '首季', '一季度', '三个月业绩', 'First Quarter', 'Q1'],
    'q3': ['第三季', '三季度', '九个月业绩', 'Third Quarter', 'Q3'],
}

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

# 添加当前目录到路径，以便导入本地模块
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from CnInfoReports import CnInfoReports
from utils import normalize_stock_code, save_json


# 缓存网络获取的日期
_cached_network_date = None


def get_current_date_from_network() -> datetime:
    """
    从网络获取当前日期

    尝试多个时间API，确保获取准确的当前日期
    """
    global _cached_network_date

    # 如果已缓存，直接返回
    if _cached_network_date is not None:
        return _cached_network_date

    time_apis = [
        # WorldTimeAPI
        {
            'url': 'http://worldtimeapi.org/api/timezone/Asia/Shanghai',
            'parser': lambda data: datetime.fromisoformat(data['datetime'].split('.')[0])
        },
        # 巨潮资讯网服务器时间（从响应头获取）
        {
            'url': 'http://www.cninfo.com.cn',
            'parser': lambda data: datetime.strptime(data, '%a, %d %b %Y %H:%M:%S %Z'),
            'use_header': True
        },
    ]

    for api in time_apis:
        try:
            with httpx.Client(timeout=10.0) as client:
                response = client.get(api['url'])
                if response.status_code == 200:
                    if api.get('use_header'):
                        # 从响应头获取日期
                        date_str = response.headers.get('Date')
                        if date_str:
                            current_date = api['parser'](date_str)
                            _cached_network_date = current_date
                            print(f"  从网络获取当前日期: {current_date.strftime('%Y-%m-%d')}")
                            return current_date
                    else:
                        # 从响应体解析
                        data = response.json()
                        current_date = api['parser'](data)
                        _cached_network_date = current_date
                        print(f"  从网络获取当前日期: {current_date.strftime('%Y-%m-%d')}")
                        return current_date
        except Exception as e:
            continue

    # 如果所有API都失败，使用本地时间并警告
    print("  警告: 无法从网络获取日期，使用本地系统时间")
    _cached_network_date = datetime.now()
    return _cached_network_date


def setup_logging():
    """配置日志"""
    logger = logging.getLogger('CnInfoReports')
    logger.setLevel(logging.INFO)
    if not logger.handlers:
        sh = logging.StreamHandler(stdout)
        sh.setFormatter(logging.Formatter(fmt='%(asctime)s %(levelname)s - %(message)s'))
        logger.addHandler(sh)
    return logger


def download_annual_reports(stock_code: str, output_dir: Path, years: int = 5) -> list:
    """
    下载年报

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的年报

    Returns:
        下载的年报文件路径列表
    """
    normalized_code, market, market_type = normalize_stock_code(stock_code)
    code = normalized_code.split('.')[0]

    print(f"\n从巨潮资讯网下载年报...")
    print(f"股票代码: {code}")
    print(f"市场类型: {market_type}")

    setup_logging()

    # 确定 stocks.json 路径
    script_dir = Path(__file__).parent
    stocks_json_path = script_dir / 'stocks.json'

    # 切换到脚本目录执行（CnInfoReports 需要在 stocks.json 同目录下运行）
    original_cwd = os.getcwd()
    os.chdir(script_dir)

    try:
        instance = CnInfoReports(skip_download_stock_json=True)

        # 从网络获取当前日期
        end_date = get_current_date_from_network()
        start_year = end_date.year - years
        se_date = f"{start_year}-01-01~{end_date.strftime('%Y-%m-%d')}"

        if market_type == 'HK':
            # 港股 - 使用关键词搜索年度业绩公告
            filter_config = {
                'market': 'hke',
                'tabName': 'fulltext',
                'plate': [],
                'category': [],  # 港股暂无分类筛选
                'industry': [],
                'stock': [code],
                'searchkey': '年度',  # 搜索年度相关公告
                'seDate': se_date,
            }
            # 查询所有公告
            all_announcements = instance.query_announcements_info(filter_config, download_pdf=False)

            # 筛选年报
            annual_reports = []
            for ann in all_announcements:
                title = ann.get('announcementTitle', '')
                # 港股年报标题通常包含"年度"+"业绩"或"年報"
                if ('年度' in title and '业绩' in title) or '年報' in title or '年度報告' in title or '全年业绩' in title:
                    # 排除股息公告
                    if '股息' not in title:
                        annual_reports.append(ann)

            print(f"  从 {len(all_announcements)} 条公告中筛选出 {len(annual_reports)} 份年报")

            # 下载筛选后的年报
            if annual_reports:
                instance.start_download_announcements_pdf(annual_reports)

            announcements = annual_reports
        else:
            # A股 - 只下载年度报告
            filter_config = {
                'market': 'szse',
                'tabName': 'fulltext',
                'plate': [],
                'category': ['category_ndbg_szsh'],  # 年度报告分类
                'industry': [],
                'stock': [code],
                'searchkey': '',
                'seDate': se_date,
            }
            # 查询并下载
            announcements = instance.query_announcements_info(filter_config, download_pdf=True)

        # 移动下载的文件到目标目录
        downloaded_files = []

        # 查找下载的文件
        import glob
        source_dirs = glob.glob(str(script_dir / 'data' / f'{code}_*'))

        if source_dirs:
            reports_dir = output_dir / 'raw_data' / 'annual_reports'
            reports_dir.mkdir(parents=True, exist_ok=True)

            for src_dir in source_dirs:
                for pdf_file in Path(src_dir).glob('*.pdf'):
                    # 筛选年报文件
                    filename = pdf_file.name.lower()
                    if market_type == 'HK':
                        # 港股年报筛选（年度业绩公布）
                        fname = pdf_file.name
                        if (('年度' in fname and '业绩' in fname) or '年報' in fname or '年度報告' in fname or '全年业绩' in fname) and '股息' not in fname:
                            dst_path = reports_dir / pdf_file.name
                            shutil.copy2(pdf_file, dst_path)
                            downloaded_files.append(str(dst_path))
                            print(f"  ✓ 复制年报: {pdf_file.name}")
                    else:
                        # A股年报（已通过category筛选）
                        if '年度报告' in pdf_file.name and '摘要' not in pdf_file.name:
                            dst_path = reports_dir / pdf_file.name
                            shutil.copy2(pdf_file, dst_path)
                            downloaded_files.append(str(dst_path))
                            print(f"  ✓ 复制年报: {pdf_file.name}")

        print(f"\n✓ 共下载 {len(downloaded_files)} 份年报")
        return downloaded_files

    except Exception as e:
        print(f"  下载失败: {e}")
        import traceback
        traceback.print_exc()
        return []
    finally:
        os.chdir(original_cwd)


def download_reports_by_type(
    stock_code: str,
    output_dir: Path,
    report_type: str,
    years: int = 5
) -> List[str]:
    """
    按报告类型下载财报

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        report_type: 报告类型 ('annual', 'semi', 'q1', 'q3')
        years: 下载最近几年的报告

    Returns:
        下载的报告文件路径列表
    """
    if report_type not in A_SHARE_REPORT_CATEGORIES:
        print(f"  不支持的报告类型: {report_type}")
        return []

    normalized_code, market, market_type = normalize_stock_code(stock_code)
    code = normalized_code.split('.')[0]
    report_name = REPORT_TYPE_NAMES.get(report_type, report_type)
    report_dir = REPORT_TYPE_DIRS.get(report_type, f'{report_type}_reports')

    print(f"\n下载{report_name}...")
    print(f"  股票代码: {code}, 市场: {market_type}")

    setup_logging()

    script_dir = Path(__file__).parent
    original_cwd = os.getcwd()
    os.chdir(script_dir)

    try:
        instance = CnInfoReports(skip_download_stock_json=True)

        # 从网络获取当前日期
        end_date = get_current_date_from_network()
        start_year = end_date.year - years
        se_date = f"{start_year}-01-01~{end_date.strftime('%Y-%m-%d')}"

        if market_type == 'HK':
            # 港股 - 先查询所有公告，再用关键词筛选
            filter_config = {
                'market': 'hke',
                'tabName': 'fulltext',
                'plate': [],
                'category': [],
                'industry': [],
                'stock': [code],
                'searchkey': '',
                'seDate': se_date,
            }
            all_announcements = instance.query_announcements_info(filter_config, download_pdf=False)

            # 用关键词筛选对应类型的报告
            keywords = HK_REPORT_KEYWORDS.get(report_type, [])
            # 排斥关键词：避免Q1匹配到半年报/Q3/年报
            EXCLUDE_KEYWORDS = {
                'annual': ['股息'],
                'semi': ['股息', '九个月'],
                'q1': ['股息', '六个月', '九个月', '全年', '年度'],
                'q3': ['股息'],
            }
            exclude_kws = EXCLUDE_KEYWORDS.get(report_type, ['股息'])
            filtered_reports = []
            for ann in all_announcements:
                title = ann.get('announcementTitle', '')
                if any(kw in title for kw in keywords):
                    if not any(ekw in title for ekw in exclude_kws):
                        filtered_reports.append(ann)

            print(f"  从 {len(all_announcements)} 条公告中筛选出 {len(filtered_reports)} 份{report_name}")

            if filtered_reports:
                instance.start_download_announcements_pdf(filtered_reports)
            announcements = filtered_reports
        else:
            # A股 - 使用分类代码直接筛选
            category_code = A_SHARE_REPORT_CATEGORIES[report_type]
            filter_config = {
                'market': 'szse',
                'tabName': 'fulltext',
                'plate': [],
                'category': [category_code],
                'industry': [],
                'stock': [code],
                'searchkey': '',
                'seDate': se_date,
            }
            announcements = instance.query_announcements_info(filter_config, download_pdf=True)

        # 移动下载的文件到目标目录
        downloaded_files = []
        import glob
        source_dirs = glob.glob(str(script_dir / 'data' / f'{code}_*'))

        if source_dirs:
            reports_dir = output_dir / 'raw_data' / report_dir
            reports_dir.mkdir(parents=True, exist_ok=True)

            for src_dir in source_dirs:
                for pdf_file in Path(src_dir).glob('*.pdf'):
                    # 根据报告类型筛选文件
                    filename = pdf_file.name
                    should_copy = False

                    if market_type == 'HK':
                        keywords = HK_REPORT_KEYWORDS.get(report_type, [])
                        should_copy = any(kw in filename for kw in keywords)
                    else:
                        # A股文件名匹配
                        if report_type == 'annual':
                            should_copy = '年度报告' in filename and '摘要' not in filename
                        elif report_type == 'semi':
                            should_copy = '半年度报告' in filename and '摘要' not in filename
                        elif report_type == 'q1':
                            should_copy = '第一季度' in filename or '一季度' in filename
                        elif report_type == 'q3':
                            should_copy = '第三季度' in filename or '三季度' in filename

                    if should_copy:
                        dst_path = reports_dir / pdf_file.name
                        if not dst_path.exists():
                            shutil.copy2(pdf_file, dst_path)
                            downloaded_files.append(str(dst_path))
                            print(f"  ✓ 复制{report_name}: {pdf_file.name}")

        print(f"  共下载 {len(downloaded_files)} 份{report_name}")
        return downloaded_files

    except Exception as e:
        print(f"  下载{report_name}失败: {e}")
        import traceback
        traceback.print_exc()
        return []
    finally:
        os.chdir(original_cwd)


def download_all_quarterly_reports(
    stock_code: str,
    output_dir: Path,
    years: int = 5
) -> Dict[str, List[str]]:
    """
    下载所有季度报告（年报+半年报+一季报+三季报）

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的报告

    Returns:
        {
            'annual': [文件路径列表],
            'semi': [文件路径列表],
            'q1': [文件路径列表],
            'q3': [文件路径列表]
        }
    """
    result = {
        'annual': [],
        'semi': [],
        'q1': [],
        'q3': [],
    }

    print(f"\n{'='*60}")
    print("下载所有季度报告")
    print(f"{'='*60}")

    for report_type in ['annual', 'semi', 'q1', 'q3']:
        files = download_reports_by_type(stock_code, output_dir, report_type, years)
        result[report_type] = files

    # 汇总统计
    total = sum(len(files) for files in result.values())
    print(f"\n{'='*60}")
    print(f"季度报告下载完成，共 {total} 份")
    for report_type, files in result.items():
        report_name = REPORT_TYPE_NAMES.get(report_type, report_type)
        print(f"  {report_name}: {len(files)} 份")
    print(f"{'='*60}\n")

    return result


def download_announcements(stock_code: str, output_dir: Path, years: int = 5) -> list:
    """
    下载所有公告

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的公告

    Returns:
        公告信息列表
    """
    normalized_code, market, market_type = normalize_stock_code(stock_code)
    code = normalized_code.split('.')[0]

    print(f"\n从巨潮资讯网下载公告列表...")
    print(f"股票代码: {code}")
    print(f"市场类型: {market_type}")

    setup_logging()

    script_dir = Path(__file__).parent
    original_cwd = os.getcwd()
    os.chdir(script_dir)

    try:
        instance = CnInfoReports(skip_download_stock_json=True)

        # 从网络获取当前日期
        end_date = get_current_date_from_network()
        start_year = end_date.year - years
        se_date = f"{start_year}-01-01~{end_date.strftime('%Y-%m-%d')}"

        if market_type == 'HK':
            filter_config = {
                'market': 'hke',
                'tabName': 'fulltext',
                'plate': [],
                'category': [],
                'industry': [],
                'stock': [code],
                'searchkey': '',
                'seDate': se_date,
            }
        else:
            filter_config = {
                'market': 'szse',
                'tabName': 'fulltext',
                'plate': [],
                'category': [],  # 所有类别
                'industry': [],
                'stock': [code],
                'searchkey': '',
                'seDate': se_date,
            }

        # 只查询，不下载PDF
        announcements = instance.query_announcements_info(filter_config, download_pdf=False)

        # 处理公告列表
        processed_announcements = []
        for ann in announcements:
            processed_announcements.append({
                'title': ann.get('announcementTitle', ''),
                'date': ann.get('announcementTime', ''),
                'type': ann.get('announcementTypeName', ''),
                'url': f"http://static.cninfo.com.cn/{ann.get('adjunctUrl', '')}" if ann.get('adjunctUrl') else '',
                'file_name': ann.get('adjunctUrl', '').split('/')[-1] if ann.get('adjunctUrl') else '',
                'sec_code': ann.get('secCode', ''),
                'sec_name': ann.get('secName', ''),
            })

        # 保存公告列表
        if processed_announcements:
            raw_path = output_dir / 'raw_data' / 'announcements' / 'all_announcements.json'
            raw_path.parent.mkdir(parents=True, exist_ok=True)
            save_json(announcements, raw_path)

            processed_path = output_dir / 'processed_data' / 'all_announcements.json'
            processed_path.parent.mkdir(parents=True, exist_ok=True)
            save_json(processed_announcements, processed_path)

            print(f"\n✓ 共获取 {len(processed_announcements)} 条公告")
            print(f"  保存到: {processed_path}")

        return processed_announcements

    except Exception as e:
        print(f"  获取公告失败: {e}")
        import traceback
        traceback.print_exc()
        return []
    finally:
        os.chdir(original_cwd)


def download_reports_and_announcements(stock_code: str, output_dir: Path, years: int = 5) -> dict:
    """
    下载财报和公告的统一入口

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的数据

    Returns:
        包含下载结果的字典
    """
    normalized_code, market, market_type = normalize_stock_code(stock_code)

    result = {
        'stock_code': normalized_code,
        'market_type': market_type,
        'annual_reports': [],
        'semi_annual_reports': [],
        'q1_reports': [],
        'q3_reports': [],
        'announcements': [],
    }

    print(f"\n{'='*60}")
    print(f"下载财报和公告")
    print(f"股票代码: {normalized_code}")
    print(f"市场类型: {market_type}")
    print(f"时间范围: 最近 {years} 年")
    print(f"{'='*60}")

    # 美股使用 SEC EDGAR
    if market_type == 'US':
        try:
            from SecEdgarReports import download_sec_reports
            raw_data_dir = output_dir / 'raw_data'
            raw_data_dir.mkdir(parents=True, exist_ok=True)
            
            sec_result = download_sec_reports(normalized_code, raw_data_dir, years)
            
            # 转换结果格式
            result['annual_reports'] = [r['path'] for r in sec_result.get('annual_reports', [])]
            result['q3_reports'] = [r['path'] for r in sec_result.get('quarterly_reports', [])]
            result['announcements'] = [{'title': f"8-K {r['date']}", 'path': r['path']} 
                                       for r in sec_result.get('announcements', [])]
            
            # 保存下载结果摘要
            summary_path = output_dir / 'processed_data' / 'download_summary.json'
            summary_path.parent.mkdir(parents=True, exist_ok=True)
            
            summary = {
                'stock_code': normalized_code,
                'market_type': market_type,
                'download_time': datetime.now().strftime('%Y-%m-%d %H:%M:%S'),
                'reports': {
                    'annual': {'count': len(result['annual_reports']), 'files': result['annual_reports']},
                    'semi': {'count': 0, 'files': []},
                    'q1': {'count': 0, 'files': []},
                    'q3': {'count': len(result['q3_reports']), 'files': result['q3_reports']},
                },
                'total_reports_count': len(result['annual_reports']) + len(result['q3_reports']),
                'announcements_count': len(result['announcements']),
                'annual_reports_count': len(result['annual_reports']),
                'annual_report_files': result['annual_reports'],
                'sec_cik': sec_result.get('cik'),
            }
            save_json(summary, summary_path)
            
            print(f"\n{'='*60}")
            print(f"SEC EDGAR 下载完成")
            print(f"年报 (10-K/20-F): {len(result['annual_reports'])} 份")
            print(f"季报 (10-Q/6-K): {len(result['q3_reports'])} 份")
            print(f"公告 (8-K): {len(result['announcements'])} 条")
            print(f"{'='*60}\n")
            
            return result
            
        except Exception as e:
            print(f"SEC EDGAR 下载失败: {e}")
            return result

    # A股和港股使用巨潮资讯网
    # 下载所有季度报告（年报+半年报+一季报+三季报）
    quarterly_result = download_all_quarterly_reports(stock_code, output_dir, years)
    result['annual_reports'] = quarterly_result.get('annual', [])
    result['semi_annual_reports'] = quarterly_result.get('semi', [])
    result['q1_reports'] = quarterly_result.get('q1', [])
    result['q3_reports'] = quarterly_result.get('q3', [])

    # 下载公告列表
    result['announcements'] = download_announcements(stock_code, output_dir, years)

    # 保存下载结果摘要
    summary_path = output_dir / 'processed_data' / 'download_summary.json'
    summary_path.parent.mkdir(parents=True, exist_ok=True)

    total_reports = (
        len(result['annual_reports']) +
        len(result['semi_annual_reports']) +
        len(result['q1_reports']) +
        len(result['q3_reports'])
    )

    summary = {
        'stock_code': normalized_code,
        'market_type': market_type,
        'download_time': datetime.now().strftime('%Y-%m-%d %H:%M:%S'),
        'reports': {
            'annual': {'count': len(result['annual_reports']), 'files': result['annual_reports']},
            'semi': {'count': len(result['semi_annual_reports']), 'files': result['semi_annual_reports']},
            'q1': {'count': len(result['q1_reports']), 'files': result['q1_reports']},
            'q3': {'count': len(result['q3_reports']), 'files': result['q3_reports']},
        },
        'total_reports_count': total_reports,
        'announcements_count': len(result['announcements']),
        # 向后兼容
        'annual_reports_count': len(result['annual_reports']),
        'annual_report_files': result['annual_reports'],
    }
    save_json(summary, summary_path)

    print(f"\n{'='*60}")
    print(f"下载完成")
    print(f"年报: {len(result['annual_reports'])} 份")
    print(f"半年报: {len(result['semi_annual_reports'])} 份")
    print(f"一季报: {len(result['q1_reports'])} 份")
    print(f"三季报: {len(result['q3_reports'])} 份")
    print(f"公告: {len(result['announcements'])} 条")
    print(f"{'='*60}\n")

    return result


def main(stock_code: str, output_dir: Path, years: int = 5):
    """主函数"""
    return download_reports_and_announcements(stock_code, output_dir, years)


if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: python fetch_reports.py <stock_code> <output_dir> [years]")
        print("Example: python fetch_reports.py 600519 ./output 5")
        sys.exit(1)

    stock_code = sys.argv[1]
    output_dir = Path(sys.argv[2])
    years = int(sys.argv[3]) if len(sys.argv) > 3 else 5

    main(stock_code, output_dir, years)
