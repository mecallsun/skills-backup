#!/usr/bin/env python3
"""
智能财务数据获取器

核心逻辑:
1. 查询最新公告 → 确认最新财报期间
2. 对比东方财富 API 已有数据的最新日期
3. API 已同步 → 直接用结构化数据
4. API 滞后 → 下载 PDF → 解析提取 → 交叉验证
"""

import re
import json
import time
import logging
import hashlib
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional
import requests

logger = logging.getLogger(__name__)

# ── 东方财富 API ──────────────────────────────────────────

def _em_api_latest_report_date(stock_code: str) -> Optional[datetime]:
    """查询东方财富 API 最新财报日期"""
    code = stock_code.split('.')[0]
    market = 'HK' if stock_code.endswith('.HK') else 'A'

    if market == 'HK':
        report_type = 'RPT_HKF10_FN_MAININDICATOR'
        filter_code = f'(SECUCODE="{code}.HK")'
    else:
        suffix = 'SZ' if stock_code.startswith('0') or stock_code.startswith('3') else 'SH'
        report_type = 'RPT_F10_FN_MAININDICATOR'
        filter_code = f'(SECUCODE="{code}.{suffix}")'

    url = 'https://datacenter.eastmoney.com/securities/api/data/get'
    params = {
        'type': report_type,
        'sty': 'REPORT_DATE,REPORT_TYPE',
        'filter': filter_code,
        'p': '1', 'ps': '1',
        'sr': '-1', 'st': 'REPORT_DATE',
        'source': 'SECURITIES', 'client': 'APP'
    }
    try:
        r = requests.get(url, params=params, timeout=10)
        data = r.json()
        items = data.get('result', {}).get('data', [])
        if items:
            d = items[0]['REPORT_DATE'][:10]
            return datetime.strptime(d, '%Y-%m-%d')
    except Exception as e:
        logger.warning(f'EM API date query failed: {e}')
    return None


def fetch_em_financials(stock_code: str, periods: int = 12) -> list[dict]:
    """从东方财富 API 获取结构化财务数据"""
    code = stock_code.split('.')[0]
    market = 'HK' if stock_code.endswith('.HK') else 'A'

    if market == 'HK':
        report_type = 'RPT_HKF10_FN_MAININDICATOR'
        filter_code = f'(SECUCODE="{code}.HK")'
    else:
        suffix = 'SZ' if stock_code.startswith('0') or stock_code.startswith('3') else 'SH'
        report_type = 'RPT_F10_FN_MAININDICATOR'
        filter_code = f'(SECUCODE="{code}.{suffix}")'

    url = 'https://datacenter.eastmoney.com/securities/api/data/get'
    params = {
        'type': report_type,
        'sty': 'ALL',
        'filter': filter_code,
        'p': '1', 'ps': str(periods),
        'sr': '-1', 'st': 'REPORT_DATE',
        'source': 'SECURITIES', 'client': 'APP'
    }
    try:
        r = requests.get(url, params=params, timeout=15)
        data = r.json()
        items = data.get('result', {}).get('data', [])
        results = []
        for item in items:
            results.append({
                'period': item['REPORT_DATE'][:10],
                'report_type': item.get('REPORT_TYPE', ''),
                'revenue': item.get('OPERATE_INCOME'),
                'net_profit': item.get('HOLDER_PROFIT') or item.get('PROFIT_DEDT'),
                'gross_margin': item.get('GROSS_PROFIT_RATIO') or item.get('RATEOFGROSSPROFIT'),
                'net_margin': item.get('PROFITMARGIN') or item.get('XSJLL'),
                'roe': item.get('ROE_AVG') or item.get('ROEJQ'),
                'roa': item.get('TOTAL_PROFIT_RATE'),
                'total_assets': item.get('TOTAL_ASSETS'),
                'total_equity': item.get('TOTAL_EQUITY') or item.get('HOLDER_EQUITY'),
                'debt_ratio': item.get('DEBT_ASSET_RATIO') or item.get('ZCFZL'),
                'operating_cashflow': item.get('OPERATING_CASHFLOW') or item.get('NETOPERATECASHFLOW'),
                'eps': item.get('EPSJB') or item.get('EPS'),
                'source': 'eastmoney_api',
            })
        return results
    except Exception as e:
        logger.error(f'EM API fetch failed: {e}')
        return []

# ── 公告查询（港交所 + 巨潮）─────────────────────────────

def _get_hkex_stock_id(stock_code: str) -> Optional[int]:
    """
    从港交所 activestock JSON 获取内部 stockId
    stock_code: "00700" 或 "00700.HK"
    """
    code = stock_code.split('.')[0]
    cache_file = Path('/tmp/hkex_activestock.json')

    # 缓存 24 小时
    if cache_file.exists():
        age = time.time() - cache_file.stat().st_mtime
        if age < 86400:
            try:
                with open(cache_file) as f:
                    data = json.load(f)
                for item in data:
                    if item.get('c') == code:
                        return item['i']
            except:
                pass

    # 下载
    url = 'https://www1.hkexnews.hk/ncms/script/eds/activestock_sehk_e.json'
    try:
        r = requests.get(url, headers={'User-Agent': 'Mozilla/5.0'}, timeout=15)
        data = r.json()
        with open(cache_file, 'w') as f:
            json.dump(data, f)
        for item in data:
            if item.get('c') == code:
                return item['i']
    except Exception as e:
        logger.warning(f'Failed to fetch HKEx stock list: {e}')

    return None


def _query_hkex_latest_report(stock_code: str) -> Optional[dict]:
    """
    查询港交所披露易最新业绩公告
    流程: 先获取内部 stockId，再查询公告列表
    """
    code = stock_code.split('.')[0]

    # Step 1: 获取港交所内部 ID
    stock_id = _get_hkex_stock_id(stock_code)
    if not stock_id:
        logger.warning(f'Cannot find HKEx stockId for {stock_code}')
        return None

    # Step 2: 查询公告
    url = 'https://www1.hkexnews.hk/search/titleSearchServlet.do'
    params = {
        'sortDir': '0',
        'sortByOptions': 'DateTime',
        'category': '0',
        'market': 'SEHK',
        'stockId': str(stock_id),
        'documentType': '-1',
        'fromDate': (datetime.now() - timedelta(days=365)).strftime('%Y%m%d'),
        'toDate': datetime.now().strftime('%Y%m%d'),
        'title': '',
        'searchType': '1',
        'lang': 'EN',
    }
    headers = {
        'Accept': 'application/json, text/javascript, */*',
        'X-Requested-With': 'XMLHttpRequest',
        'Referer': 'https://www1.hkexnews.hk/search/titlesearch.xhtml',
        'User-Agent': 'Mozilla/5.0',
    }

    # 业绩类关键词（大小写不敏感）
    RESULT_KEYWORDS = ['results', 'annual report', 'interim report',
                       '業績', '业绩', '年報', '年报', '中期報告', '中期报告']

    try:
        r = requests.get(url, params=params, headers=headers, timeout=15)
        outer = r.json()
        result_str = outer.get('result')
        if not result_str or result_str == 'null':
            logger.warning('HKEx returned no results')
            return None

        announcements = json.loads(result_str)

        for ann in announcements:
            title = ann.get('TITLE', '')
            title_lower = title.lower()

            if not any(kw.lower() in title_lower for kw in RESULT_KEYWORDS):
                continue

            # 解析日期 (格式: "DD/MM/YYYY HH:MM")
            dt_str = ann.get('DATE_TIME', '')
            ann_date = None
            try:
                ann_date = datetime.strptime(dt_str[:16], '%d/%m/%Y %H:%M')
            except ValueError:
                try:
                    ann_date = datetime.strptime(dt_str[:10], '%d/%m/%Y')
                except ValueError:
                    pass

            # 判断报告类型
            report_type = 'unknown'
            if 'annual' in title_lower or '年' in title:
                report_type = 'annual'
            elif 'interim' in title_lower or '中期' in title or 'six months' in title_lower:
                report_type = 'interim'
            elif 'nine months' in title_lower or '九' in title:
                report_type = 'q3'
            elif 'three months' in title_lower and 'six' not in title_lower and 'nine' not in title_lower:
                report_type = 'q1'

            file_link = ann.get('FILE_LINK', '')
            full_url = f'https://www1.hkexnews.hk{file_link}' if file_link.startswith('/') else file_link

            return {
                'title': title.replace('&#x3b;', ';'),
                'announcement_date': ann_date.strftime('%Y-%m-%d') if ann_date else dt_str[:10],
                'report_type': report_type,
                'url': full_url,
                'source': 'hkex',
                'stock_id': stock_id,
            }

    except Exception as e:
        logger.warning(f'HKEx query failed: {e}')

    return None


def _query_cninfo_latest_report(stock_code: str) -> Optional[dict]:
    """查询巨潮资讯最新业绩公告（A股）"""
    code = stock_code.split('.')[0]
    url = 'https://www.cninfo.com.cn/new/hisAnnouncement/query'
    payload = {
        'stock': code,
        'category': 'category_ndbg_szsh;category_bndbg_szsh;category_yjdbg_szsh;category_sjdbg_szsh',
        'pageNum': 1,
        'pageSize': 5,
        'tabName': 'fulltext',
        'column': 'szse',
        'plate': '',
        'seDate': '',
    }
    headers = {
        'Content-Type': 'application/x-www-form-urlencoded',
        'Referer': 'https://www.cninfo.com.cn/',
        'User-Agent': 'Mozilla/5.0',
    }
    try:
        r = requests.post(url, data=payload, headers=headers, timeout=15)
        data = r.json()
        announcements = data.get('announcements', [])
        if announcements:
            ann = announcements[0]
            return {
                'title': ann.get('announcementTitle', ''),
                'announcement_date': ann.get('announcementTime', '')[:10],
                'url': f"https://static.cninfo.com.cn/{ann.get('adjunctUrl', '')}",
                'source': 'cninfo',
            }
    except Exception as e:
        logger.warning(f'CnInfo query failed: {e}')
    return None


# ── PDF 解析（pdfplumber + fallback to docling）──────────

def _extract_tables_pdfplumber(pdf_path: Path) -> list:
    """用 pdfplumber 提取 PDF 中所有表格"""
    try:
        import pdfplumber
        tables = []
        with pdfplumber.open(str(pdf_path)) as pdf:
            for page_num, page in enumerate(pdf.pages):
                for tbl in page.extract_tables():
                    if tbl and len(tbl) > 1:
                        tables.append({
                            'page': page_num + 1,
                            'rows': tbl,
                        })
        return tables
    except ImportError:
        logger.warning('pdfplumber not installed, trying docling')
        return _extract_tables_docling(pdf_path)
    except Exception as e:
        logger.error(f'pdfplumber failed: {e}')
        return []


def _extract_tables_docling(pdf_path: Path) -> list:
    """用 docling 提取 PDF 表格（pdfplumber 失败时备用）"""
    try:
        from docling.document_converter import DocumentConverter
        converter = DocumentConverter()
        result = converter.convert(str(pdf_path))
        tables = []
        for element, _ in result.document.iterate_items():
            if hasattr(element, 'data') and hasattr(element.data, 'grid'):
                rows = []
                for row in element.data.grid:
                    rows.append([cell.text for cell in row])
                tables.append({'page': 0, 'rows': rows})
        return tables
    except ImportError:
        logger.error('docling not installed')
        return []
    except Exception as e:
        logger.error(f'docling failed: {e}')
        return []


# 财务关键词 → 标准字段映射
FINANCIAL_KEYWORDS = {
    # 营收
    'revenue': ['收入', '营业收入', '总收入', 'Revenue', 'Total Revenue', 'Turnover'],
    # 净利润（归母）
    'net_profit': ['本公司拥有人应占溢利', '归属于母公司股东的净利润', '净利润',
                   'Profit attributable to owners', 'Net profit', 'Net income'],
    # 毛利
    'gross_profit': ['毛利', 'Gross profit', '毛利润'],
    # 经营现金流
    'operating_cashflow': ['经营活动产生的现金', '经营活动现金流', 'Operating activities',
                           'Cash from operations'],
    # 总资产
    'total_assets': ['资产总计', '总资产', 'Total assets'],
    # 股东权益
    'total_equity': ['权益总额', '股东权益', "Total equity", "Shareholders' equity"],
    # EPS
    'eps': ['每股盈利', '基本每股收益', 'Basic EPS', 'Earnings per share'],
}


def _parse_number_from_cell(text: str) -> Optional[float]:
    """从单元格文本解析数值"""
    if not text:
        return None
    text = str(text).strip()
    # 去除千分符、空格
    text = re.sub(r'[,，\s]', '', text)
    # 括号表示负数
    if re.match(r'^\([\d.]+\)$', text):
        text = '-' + text[1:-1]
    try:
        return float(text)
    except ValueError:
        return None


def _extract_financials_from_tables(tables: list, expected_periods: list[str] = None) -> dict:
    """
    从表格列表中提取财务数据
    expected_periods: 期望的报告期列表，如 ['2025-06-30', '2024-06-30']
    """
    extracted = {}

    for tbl in tables:
        rows = tbl['rows']
        if not rows or len(rows) < 2:
            continue

        # 检测表头行（含年份/日期）
        header_row_idx = None
        for i, row in enumerate(rows[:3]):
            row_text = ' '.join(str(c) for c in row if c)
            if re.search(r'20\d{2}', row_text):
                header_row_idx = i
                break

        if header_row_idx is None:
            continue

        headers = rows[header_row_idx]

        # 解析每一行
        for row in rows[header_row_idx + 1:]:
            if not row or not row[0]:
                continue
            label = str(row[0]).strip()

            # 匹配财务字段
            matched_field = None
            for field, keywords in FINANCIAL_KEYWORDS.items():
                if any(kw in label for kw in keywords):
                    matched_field = field
                    break

            if not matched_field:
                continue

            # 提取数值（与表头列对应）
            for col_idx, cell in enumerate(row[1:], 1):
                val = _parse_number_from_cell(cell)
                if val is None:
                    continue
                # 尝试从表头获取期间
                period_key = 'unknown'
                if col_idx < len(headers):
                    header_text = str(headers[col_idx]) if headers[col_idx] else ''
                    date_match = re.search(r'(20\d{2})[\s年/\-](0?[1-9]|1[0-2])[\s月/\-](\d{1,2})', header_text)
                    if date_match:
                        y, m, d = date_match.groups()
                        period_key = f'{y}-{int(m):02d}-{int(d):02d}'
                    else:
                        year_match = re.search(r'20\d{2}', header_text)
                        if year_match:
                            period_key = year_match.group()

                if period_key not in extracted:
                    extracted[period_key] = {}
                # 只取第一个匹配（避免重复行覆盖）
                if matched_field not in extracted[period_key]:
                    extracted[period_key][matched_field] = val

    return extracted


# ── PDF 下载 ──────────────────────────────────────────────

def _download_pdf(url: str, output_dir: Path, filename: str = None) -> Optional[Path]:
    """下载 PDF 到本地"""
    if not filename:
        filename = hashlib.md5(url.encode()).hexdigest()[:8] + '.pdf'
    output_dir.mkdir(parents=True, exist_ok=True)
    pdf_path = output_dir / filename

    if pdf_path.exists():
        logger.info(f'PDF already cached: {pdf_path}')
        return pdf_path

    headers = {'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)'}
    try:
        r = requests.get(url, headers=headers, timeout=60, stream=True)
        r.raise_for_status()
        with open(pdf_path, 'wb') as f:
            for chunk in r.iter_content(chunk_size=8192):
                f.write(chunk)
        size_kb = pdf_path.stat().st_size // 1024
        logger.info(f'Downloaded PDF: {pdf_path} ({size_kb} KB)')
        return pdf_path
    except Exception as e:
        logger.error(f'PDF download failed: {e}')
        return None


# ── 交叉验证 ──────────────────────────────────────────────

def _cross_validate(api_data: list[dict], pdf_data: dict) -> dict:
    """
    交叉验证: 比较 API 数据与 PDF 提取数据
    返回验证报告
    """
    report = {
        'status': 'ok',
        'discrepancies': [],
        'warnings': [],
    }

    TOLERANCE = 0.03  # 3% 容忍误差（单位换算等原因）

    for period_key, pdf_values in pdf_data.items():
        # 在 API 数据中找对应期间
        api_record = None
        for rec in api_data:
            if rec['period'] == period_key or rec['period'].startswith(period_key[:7]):
                api_record = rec
                break

        if not api_record:
            continue

        for field in ['revenue', 'net_profit']:
            api_val = api_record.get(field)
            pdf_val = pdf_values.get(field)
            if api_val is None or pdf_val is None:
                continue

            # PDF 数据可能是百万/千/元，需要量级对比
            # 用比例差异判断
            ratio = abs(api_val - pdf_val) / abs(api_val) if api_val != 0 else 1
            # 可能是单位差异 (千 vs 百万 vs 亿)
            for scale in [1, 1000, 10000, 100000000]:
                scaled_pdf = pdf_val * scale
                scaled_ratio = abs(api_val - scaled_pdf) / abs(api_val) if api_val != 0 else 1
                if scaled_ratio < TOLERANCE:
                    ratio = scaled_ratio
                    break

            if ratio > TOLERANCE:
                report['discrepancies'].append({
                    'period': period_key,
                    'field': field,
                    'api_value': api_val,
                    'pdf_value': pdf_val,
                    'diff_pct': f'{ratio*100:.1f}%',
                })
                report['status'] = 'warning'

    if report['discrepancies']:
        report['warnings'].append(f'发现 {len(report["discrepancies"])} 处数据差异，请人工核实')

    return report


# ── 主入口 ────────────────────────────────────────────────

def get_financial_data(stock_code: str, output_dir: Path = None, periods: int = 8) -> dict:
    """
    智能财务数据获取主函数

    流程:
    1. 查询最新财报公告日期
    2. 对比 API 数据日期
    3. API 已同步 → 返回 API 数据
    4. API 滞后 → 下载 PDF 解析 → 交叉验证

    Returns:
        {
          'stock_code': str,
          'data_source': 'api' | 'pdf' | 'api+pdf',
          'latest_period': str,
          'financials': [...],       # 标准化财务数据
          'pdf_raw': {...},          # PDF 提取原始数据（如有）
          'validation': {...},       # 交叉验证报告
          'latest_announcement': {}, # 最新公告信息
        }
    """
    result = {
        'stock_code': stock_code,
        'data_source': 'unknown',
        'latest_period': None,
        'financials': [],
        'pdf_raw': None,
        'validation': None,
        'latest_announcement': None,
        'fetched_at': datetime.now().isoformat(),
    }

    is_hk = stock_code.endswith('.HK') or (len(stock_code.split('.')) == 1 and len(stock_code) == 5)
    is_us = stock_code.endswith('.US') or (stock_code.isalpha() and len(stock_code) <= 5)
    # 修正：纯5位数字是港股
    if stock_code.isdigit():
        is_hk = True
        is_us = False

    if output_dir is None:
        output_dir = Path('/tmp') / f'financial_{stock_code}_{datetime.now().strftime("%Y%m%d")}'
    output_dir = Path(output_dir)

    print(f'\n{"="*60}')
    print(f'  智能财务数据获取: {stock_code}')
    print(f'{"="*60}')

    # Step 1: 查询最新公告
    print('\n[1/4] 查询最新财报公告...')
    latest_ann = None
    if is_hk:
        latest_ann = _query_hkex_latest_report(stock_code)
    else:
        latest_ann = _query_cninfo_latest_report(stock_code)

    if latest_ann:
        result['latest_announcement'] = latest_ann
        print(f'  ✓ 最新公告: {latest_ann.get("title", "N/A")}')
        print(f'    发布日期: {latest_ann.get("announcement_date", "N/A")}')
    else:
        print('  ⚠ 未找到近期公告')

    # Step 2: 查询 API 最新数据日期
    print('\n[2/4] 检查东方财富 API 数据同步状态...')
    api_latest_date = _em_api_latest_report_date(stock_code)
    if api_latest_date:
        print(f'  ✓ API 最新数据: {api_latest_date.strftime("%Y-%m-%d")}')
    else:
        print('  ⚠ API 无数据')

    # Step 3: 判断是否需要 PDF 解析
    need_pdf = False
    ann_date = None
    if latest_ann and latest_ann.get('announcement_date'):
        ann_date_str = latest_ann['announcement_date']
        try:
            ann_date = datetime.strptime(ann_date_str, '%Y-%m-%d')
        except ValueError:
            pass

    # 从公告标题推断报告截止日期
    ann_report_period = None
    if latest_ann:
        title = latest_ann.get('title', '')
        # 匹配 "ENDED 30 SEPTEMBER 2025" 或 "截至2025年9月30日"
        import calendar
        month_map = {m.lower(): i for i, m in enumerate(calendar.month_name) if m}
        month_map.update({m.lower(): i for i, m in enumerate(calendar.month_abbr) if m})
        # English pattern: "DD MONTH YYYY" or "MONTH DD, YYYY"
        m = re.search(r'(\d{1,2})\s+(january|february|march|april|may|june|july|august|september|october|november|december)\s+(\d{4})', title.lower())
        if m:
            day, month_name, year = m.groups()
            month_num = month_map.get(month_name, 0)
            if month_num:
                ann_report_period = datetime(int(year), month_num, int(day))
        if not ann_report_period:
            # Chinese pattern: "2025年9月30日"
            m = re.search(r'(20\d{2})年(\d{1,2})月(\d{1,2})日', title)
            if m:
                ann_report_period = datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)))

    if ann_report_period:
        print(f'  公告对应报告期: {ann_report_period.strftime("%Y-%m-%d")}')

    # 判断 API 是否已覆盖该报告期
    if ann_report_period and api_latest_date:
        if ann_report_period > api_latest_date:
            days_lag = (ann_report_period - api_latest_date).days
            need_pdf = True
            print(f'  ⚠ API 尚未覆盖此报告期（滞后 {days_lag} 天），需要从 PDF 提取')
        else:
            print(f'  ✓ API 已覆盖至 {api_latest_date.strftime("%Y-%m-%d")}，包含该报告期')
    elif ann_date and api_latest_date:
        # fallback: 用公告日期粗略判断（公告日通常比报告期晚 1-3 个月）
        days_lag = (ann_date - api_latest_date).days
        if days_lag > 90:
            need_pdf = True
            print(f'  ⚠ 公告日与 API 最新期间相差 {days_lag} 天，可能有新数据')
        else:
            print(f'  ✓ API 数据应已同步（公告日与API期间差 {days_lag} 天）')
    elif not api_latest_date:
        need_pdf = True
        print('  ⚠ API 无数据，将尝试 PDF 解析')

    # Step 4a: 获取 API 数据
    print('\n[3/4] 获取东方财富 API 结构化数据...')
    api_financials = fetch_em_financials(stock_code, periods=periods)
    if api_financials:
        result['financials'] = api_financials
        result['latest_period'] = api_financials[0]['period']
        result['data_source'] = 'api'
        print(f'  ✓ 获取 {len(api_financials)} 期数据，最新: {api_financials[0]["period"]}')
        for rec in api_financials[:3]:
            rev = rec.get('revenue')
            np_ = rec.get('net_profit')
            roe = rec.get('roe')
            rev_str = f'{rev/1e8:.0f}亿' if rev else 'N/A'
            np_str = f'{np_/1e8:.0f}亿' if np_ else 'N/A'
            roe_str = f'{roe:.1f}%' if roe else 'N/A'
            print(f'    {rec["period"]}  营收: {rev_str}  净利润: {np_str}  ROE: {roe_str}')
    else:
        print('  ✗ API 无数据')

    # Step 4b: PDF 解析（API 滞后时）
    if need_pdf and latest_ann and latest_ann.get('url'):
        print('\n[4/4] 从最新财报 PDF 提取数据...')
        pdf_url = latest_ann['url']
        print(f'  下载: {pdf_url[:80]}...' if len(pdf_url) > 80 else f'  下载: {pdf_url}')

        pdf_cache_dir = output_dir / 'pdf_cache'
        pdf_path = _download_pdf(pdf_url, pdf_cache_dir)

        if pdf_path:
            print(f'  ✓ PDF 下载成功 ({pdf_path.stat().st_size // 1024} KB)')
            print('  解析表格...')
            tables = _extract_tables_pdfplumber(pdf_path)
            print(f'  ✓ 提取到 {len(tables)} 个表格')

            pdf_financials = _extract_financials_from_tables(tables)
            result['pdf_raw'] = pdf_financials

            if pdf_financials:
                periods_found = list(pdf_financials.keys())
                print(f'  ✓ 提取到期间: {periods_found}')

                # 将 PDF 数据合并为标准格式
                pdf_records = []
                for period, vals in pdf_financials.items():
                    rec = {'period': period, 'source': 'pdf', **vals}
                    pdf_records.append(rec)

                if need_pdf and not api_financials:
                    # API 完全没数据，用 PDF 作为主数据
                    result['financials'] = pdf_records
                    result['data_source'] = 'pdf'
                    result['latest_period'] = pdf_records[0]['period'] if pdf_records else None
                else:
                    # API 有数据但滞后，合并最新期间
                    result['data_source'] = 'api+pdf'
                    # 将 PDF 中 API 未覆盖的期间追加
                    api_periods = {r['period'] for r in api_financials}
                    for rec in pdf_records:
                        if rec['period'] not in api_periods:
                            result['financials'].insert(0, rec)
                            print(f'  + 从 PDF 补充期间: {rec["period"]}')
                    if pdf_records:
                        result['latest_period'] = result['financials'][0]['period']
            else:
                print('  ⚠ PDF 表格解析未提取到财务数据')
        else:
            print('  ✗ PDF 下载失败')

    # 交叉验证
    if result['pdf_raw'] and result['data_source'] == 'api+pdf':
        print('\n  交叉验证 API vs PDF...')
        validation = _cross_validate(api_financials, result['pdf_raw'])
        result['validation'] = validation
        if validation['status'] == 'ok':
            print('  ✓ 数据一致，验证通过')
        else:
            print(f'  ⚠ 发现差异: {validation["warnings"]}')
            for d in validation['discrepancies']:
                print(f'    [{d["period"]}] {d["field"]}: API={d["api_value"]}, PDF={d["pdf_value"]}, 差异={d["diff_pct"]}')

    print(f'\n{"="*60}')
    print(f'  数据来源: {result["data_source"]}')
    print(f'  最新期间: {result["latest_period"]}')
    print(f'  数据条数: {len(result["financials"])}')
    print(f'{"="*60}\n')

    return result


# ── CLI ───────────────────────────────────────────────────

if __name__ == '__main__':
    import sys
    logging.basicConfig(level=logging.WARNING, format='%(levelname)s: %(message)s')

    stock = sys.argv[1] if len(sys.argv) > 1 else '00700.HK'
    if '.' not in stock and len(stock) == 5 and stock.isdigit():
        stock = stock + '.HK'

    result = get_financial_data(stock)

    # 输出 JSON 结果
    output_file = f'/tmp/financials_{stock.replace(".", "_")}.json'
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2, default=str)
    print(f'完整数据已保存: {output_file}')

