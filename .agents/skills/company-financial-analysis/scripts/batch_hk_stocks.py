#!/usr/bin/env python3
"""
批量下载港股年报并提取财务数据入库

用法:
    python3 batch_hk_stocks.py
"""
import sys
import os
import re
import json
import sqlite3
import subprocess
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Optional, Tuple
import shutil
import glob

# 添加当前目录到路径
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# 港股通40只股票
HK_STOCKS = [
    "00700", "09988", "03690", "09618", "09888", "01810", "09999", "00941", "00388", "02318",
    "00005", "00939", "01398", "00883", "00857", "02020", "09992", "01211", "02333", "00175",
    "09866", "09868", "02015", "06618", "00268", "03888", "00020", "09961", "01024", "02382",
    "00669", "01928", "00027", "00981", "00772", "06060", "02269", "01177", "03759", "06185"
]

# 数据库路径
SCRIPT_DIR = Path(__file__).parent
DB_PATH = SCRIPT_DIR / 'financials.db'

# 港股公司名称映射
HK_STOCK_NAMES = {
    "00700": "腾讯控股",
    "09988": "阿里巴巴-SW",
    "03690": "美团-W",
    "09618": "京东集团-SW",
    "09888": "百度集团-SW",
    "01810": "小米集团-W",
    "09999": "网易-S",
    "00941": "中国移动",
    "00388": "香港交易所",
    "02318": "中国平安",
    "00005": "汇丰控股",
    "00939": "建设银行",
    "01398": "工商银行",
    "00883": "中海油",
    "00857": "中国石油股份",
    "02020": "安踏体育",
    "09992": "泡泡玛特",
    "01211": "比亚迪股份",
    "02333": "长城汽车",
    "00175": "吉利汽车",
    "09866": "蔚来-SW",
    "09868": "小鹏汽车-W",
    "02015": "理想汽车-W",
    "06618": "京东健康",
    "00268": "金蝶国际",
    "03888": "金山软件",
    "00020": "商汤-W",
    "09961": "携程集团-S",
    "01024": "快手-W",
    "02382": "舜宇光学科技",
    "00669": "创科实业",
    "01928": "金沙中国有限公司",
    "00027": "银河娱乐",
    "00981": "中芯国际",
    "00772": "阅文集团",
    "06060": "众安在线",
    "02269": "药明生物",
    "01177": "中国生物制药",
    "03759": "康龙化成",
    "06185": "康希诺生物"
}


def init_database():
    """初始化SQLite数据库"""
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    
    # 创建财务数据表
    cursor.execute('''
        CREATE TABLE IF NOT EXISTS financials (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            stock_code TEXT NOT NULL,
            stock_name TEXT,
            market TEXT DEFAULT 'HK',
            year INTEGER NOT NULL,
            revenue REAL,
            net_profit REAL,
            total_assets REAL,
            total_equity REAL,
            total_liabilities REAL,
            roe REAL,
            gross_margin REAL,
            net_margin REAL,
            asset_turnover REAL,
            equity_multiplier REAL,
            current_ratio REAL,
            operating_cash_flow REAL,
            currency TEXT DEFAULT 'HKD',
            source TEXT,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            UNIQUE(stock_code, year)
        )
    ''')
    
    conn.commit()
    conn.close()
    print(f"✓ 数据库初始化完成: {DB_PATH}")


def clear_hk_data():
    """清空港股数据"""
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    cursor.execute("DELETE FROM financials WHERE market = 'HK'")
    deleted = cursor.rowcount
    conn.commit()
    conn.close()
    print(f"✓ 已清空 {deleted} 条港股数据")


def download_hk_reports(stock_code: str) -> bool:
    """
    下载港股年报
    
    Returns:
        是否成功下载
    """
    print(f"\n{'='*60}", flush=True)
    print(f"下载 {stock_code} {HK_STOCK_NAMES.get(stock_code, '')} 年报", flush=True)
    print(f"{'='*60}", flush=True)
    
    # 检查data目录是否有任何PDF（可能已下载但未正确识别）
    data_dir = SCRIPT_DIR / 'data'
    pattern = str(data_dir / f'{stock_code}_*')
    all_pdfs = []
    for dir_path in glob.glob(pattern):
        all_pdfs.extend(list(Path(dir_path).glob('*.pdf')))
    
    # 如果data目录已有3个以上PDF文件，直接跳过下载
    if len(all_pdfs) >= 3:
        print(f"  data目录已有 {len(all_pdfs)} 个PDF文件，跳过下载", flush=True)
        return True
    
    # 先检查是否已有年报PDF文件
    existing_pdfs = find_pdf_files(stock_code)
    if len(existing_pdfs) >= 3:
        print(f"  已有 {len(existing_pdfs)} 份年报PDF，跳过下载", flush=True)
        return True
    
    try:
        result = subprocess.run(
            ['python3', 'analyze_company.py', stock_code, '5', '--skip-news'],
            cwd=SCRIPT_DIR,
            capture_output=True,
            text=True,
            timeout=120  # 缩短超时时间到2分钟
        )
        
        if result.returncode == 0:
            print(f"✓ {stock_code} 下载完成", flush=True)
            return True
        else:
            print(f"✗ {stock_code} 下载失败", flush=True)
            if result.stderr:
                print(result.stderr[-300:], flush=True)
            return False
            
    except subprocess.TimeoutExpired:
        print(f"⚠ {stock_code} 下载超时，尝试使用已有数据", flush=True)
        # 即使超时，如果有数据也返回True
        for dir_path in glob.glob(pattern):
            if list(Path(dir_path).glob('*.pdf')):
                print(f"  发现 {len(list(Path(dir_path).glob('*.pdf')))} 个已下载的PDF", flush=True)
                return True
        return False
    except Exception as e:
        print(f"✗ {stock_code} 下载异常: {e}", flush=True)
        return False


def find_pdf_files(stock_code: str) -> List[Path]:
    """查找已下载的年报PDF文件"""
    data_dir = SCRIPT_DIR / 'data'
    pdfs = []
    
    # 港股年报关键词（扩展版）
    annual_keywords = [
        '全年业绩', '年度业绩', '年度報告', '年報', '年度报告',
        'Annual Report', 'Annual Results', 'Full Year Results',
        '截至.*年度', '年度综合业绩',
        # 阿里巴巴、京东等使用的财务年度格式
        r'\d{4}财务?年度业绩', r'\d{4}财政?年度业绩',
        # 十二月三十一日止年度
        r'12月31日止年度', r'十二月三十一日止年度',
        # 三月底止财务年度（阿里巴巴财年）
        r'三月底止.*财务?年度', r'三月底止.*财政?年度',
    ]
    
    # 排除关键词
    exclude_keywords = ['股息', '派息']  # 不排除中期和季度，让更宽泛匹配先工作
    
    # 查找 data/{code}_* 目录下的PDF
    pattern = str(data_dir / f'{stock_code}_*')
    for dir_path in glob.glob(pattern):
        for pdf in Path(dir_path).glob('*.pdf'):
            fname = pdf.name
            
            # 检查是否匹配年报关键词
            is_annual = False
            for kw in annual_keywords:
                if re.search(kw, fname, re.IGNORECASE):
                    is_annual = True
                    break
            
            # 检查是否需要排除
            if is_annual:
                should_exclude = False
                for ex_kw in exclude_keywords:
                    if ex_kw in fname:
                        should_exclude = True
                        break
                
                if not should_exclude:
                    pdfs.append(pdf)
    
    # 如果没有找到年报，尝试更宽泛的搜索
    if not pdfs:
        for dir_path in glob.glob(pattern):
            for pdf in Path(dir_path).glob('*.pdf'):
                fname = pdf.name
                # 寻找包含"十二月三十一日"或"12月31日"或财务年度的文件
                if ('十二月三十一日' in fname or 'December 31' in fname or 
                    '12月31日' in fname or re.search(r'\d{4}财', fname)):
                    if '股息' not in fname and '派息' not in fname:
                        pdfs.append(pdf)
    
    # 最后兜底：如果还没找到，尝试匹配最新的业绩公告（按文件名排序）
    if not pdfs:
        for dir_path in glob.glob(pattern):
            all_pdfs = list(Path(dir_path).glob('*.pdf'))
            # 筛选包含"业绩"的文件
            annual_pdfs = [p for p in all_pdfs if '业绩' in p.name and '股息' not in p.name]
            if annual_pdfs:
                # 按文件名排序，取包含年度相关信息的文件
                for pdf in annual_pdfs:
                    # 提取年份
                    year_match = re.search(r'20[12][0-9]', pdf.name)
                    if year_match:
                        pdfs.append(pdf)
    
    # 去重并排序
    pdfs = list(set(pdfs))
    return sorted(pdfs, key=lambda x: x.name, reverse=True)


def extract_text_from_pdf(pdf_path: Path) -> str:
    """从PDF提取文本"""
    try:
        import PyPDF2
        
        text_parts = []
        with open(pdf_path, 'rb') as f:
            reader = PyPDF2.PdfReader(f)
            for page in reader.pages[:50]:  # 只读取前50页
                try:
                    text = page.extract_text()
                    if text:
                        text_parts.append(text)
                except:
                    continue
        
        return '\n'.join(text_parts)
    except Exception as e:
        print(f"  PDF提取失败: {e}")
        return ""


def parse_number(text: str) -> Optional[float]:
    """解析数字，支持中英文格式"""
    if not text:
        return None
    
    # 清理文本
    text = text.replace(',', '').replace('，', '').replace(' ', '')
    text = text.replace('千元', '').replace('百万', '').replace('亿', '')
    
    # 处理括号表示负数
    if text.startswith('(') and text.endswith(')'):
        text = '-' + text[1:-1]
    
    try:
        return float(text)
    except:
        return None


def extract_year_from_filename(filename: str) -> Optional[int]:
    """从文件名提取年份"""
    # 尝试匹配 "二零二四年" 格式
    cn_year_map = {
        '零': '0', '一': '1', '二': '2', '三': '3', '四': '4',
        '五': '5', '六': '6', '七': '7', '八': '8', '九': '9'
    }
    
    # 匹配"二零二三年"或"二零二四年"格式
    cn_pattern = r'(二零[零一二三四五六七八九]{2})年'
    match = re.search(cn_pattern, filename)
    if match:
        cn_year = match.group(1)
        year_str = ''
        for c in cn_year:
            year_str += cn_year_map.get(c, c)
        try:
            return int(year_str)
        except:
            pass
    
    # 尝试匹配 2023, 2024 等格式
    year_match = re.search(r'20[12][0-9]', filename)
    if year_match:
        return int(year_match.group())
    
    return None


def extract_financials_from_pdf(pdf_path: Path, stock_code: str) -> Optional[Dict]:
    """从PDF提取财务数据"""
    text = extract_text_from_pdf(pdf_path)
    if not text:
        return None
    
    year = extract_year_from_filename(pdf_path.name)
    if not year:
        return None
    
    data = {
        'stock_code': stock_code,
        'stock_name': HK_STOCK_NAMES.get(stock_code, ''),
        'year': year,
        'source': pdf_path.name,
        'currency': 'HKD'
    }
    
    # 检测货币单位（港股可能用港元或人民币）
    if '人民币' in text or 'RMB' in text:
        data['currency'] = 'RMB'
    
    # 检测数字单位
    unit_multiplier = 1
    if '百万' in text or 'million' in text.lower():
        unit_multiplier = 1_000_000
    elif '千' in text:
        unit_multiplier = 1_000
    elif '億' in text or '亿' in text:
        unit_multiplier = 100_000_000
    
    # 提取收入
    revenue_patterns = [
        r'收入[：:\s]+(\d[\d,，\.]+)',
        r'Revenue[：:\s]+(\d[\d,，\.]+)',
        r'營業額[：:\s]+(\d[\d,，\.]+)',
        r'总收入[：:\s]+(\d[\d,，\.]+)',
    ]
    for pattern in revenue_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            val = parse_number(match.group(1))
            if val and val > 1000:  # 至少1000（百万或其他单位）
                data['revenue'] = val * unit_multiplier
                break
    
    # 提取净利润
    profit_patterns = [
        r'(?:归属于|歸屬於).*?(?:股东|股東).*?(?:利润|利潤|溢利)[：:\s]+(-?\d[\d,，\.]+)',
        r'(?:本公司|母公司).*?(?:拥有人|擁有人).*?(?:应占|應佔).*?(?:利润|利潤|溢利)[：:\s]+(-?\d[\d,，\.]+)',
        r'净利润[：:\s]+(-?\d[\d,，\.]+)',
        r'Net\s+(?:profit|income)[：:\s]+(-?\d[\d,，\.]+)',
        r'(?:年度|期内)(?:利润|利潤|溢利)[：:\s]+(-?\d[\d,，\.]+)',
    ]
    for pattern in profit_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            val = parse_number(match.group(1))
            if val:
                data['net_profit'] = val * unit_multiplier
                break
    
    # 提取总资产
    asset_patterns = [
        r'资产总[额计][：:\s]+(\d[\d,，\.]+)',
        r'總資產[：:\s]+(\d[\d,，\.]+)',
        r'Total\s+Assets[：:\s]+(\d[\d,，\.]+)',
    ]
    for pattern in asset_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            val = parse_number(match.group(1))
            if val and val > 1000:
                data['total_assets'] = val * unit_multiplier
                break
    
    # 提取股东权益
    equity_patterns = [
        r'(?:股东|股東)权益(?:合计|總額)?[：:\s]+(\d[\d,，\.]+)',
        r'(?:所有者|擁有人)权益[：:\s]+(\d[\d,，\.]+)',
        r'Total\s+Equity[：:\s]+(\d[\d,，\.]+)',
    ]
    for pattern in equity_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            val = parse_number(match.group(1))
            if val and val > 1000:
                data['total_equity'] = val * unit_multiplier
                break
    
    # 计算派生指标
    if data.get('net_profit') and data.get('revenue'):
        data['net_margin'] = round(data['net_profit'] / data['revenue'] * 100, 2)
    
    if data.get('net_profit') and data.get('total_equity'):
        data['roe'] = round(data['net_profit'] / data['total_equity'] * 100, 2)
    
    if data.get('revenue') and data.get('total_assets'):
        data['asset_turnover'] = round(data['revenue'] / data['total_assets'], 2)
    
    if data.get('total_assets') and data.get('total_equity'):
        data['equity_multiplier'] = round(data['total_assets'] / data['total_equity'], 2)
    
    return data


def save_to_database(data: Dict) -> bool:
    """保存财务数据到数据库"""
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    
    try:
        cursor.execute('''
            INSERT OR REPLACE INTO financials (
                stock_code, stock_name, market, year, revenue, net_profit,
                total_assets, total_equity, total_liabilities, roe,
                gross_margin, net_margin, asset_turnover, equity_multiplier,
                current_ratio, operating_cash_flow, currency, source
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ''', (
            data.get('stock_code'),
            data.get('stock_name'),
            'HK',
            data.get('year'),
            data.get('revenue'),
            data.get('net_profit'),
            data.get('total_assets'),
            data.get('total_equity'),
            data.get('total_liabilities'),
            data.get('roe'),
            data.get('gross_margin'),
            data.get('net_margin'),
            data.get('asset_turnover'),
            data.get('equity_multiplier'),
            data.get('current_ratio'),
            data.get('operating_cash_flow'),
            data.get('currency', 'HKD'),
            data.get('source')
        ))
        conn.commit()
        return True
    except Exception as e:
        print(f"  数据库写入失败: {e}")
        return False
    finally:
        conn.close()


def process_stock(stock_code: str) -> Tuple[bool, List[int]]:
    """
    处理单只股票：下载年报 -> 提取数据 -> 入库
    
    Returns:
        (是否成功, 成功入库的年份列表)
    """
    # 1. 下载年报
    download_hk_reports(stock_code)
    
    # 2. 查找PDF文件
    pdfs = find_pdf_files(stock_code)
    if not pdfs:
        # 列出所有PDF文件供调试
        data_dir = SCRIPT_DIR / 'data'
        pattern = str(data_dir / f'{stock_code}_*')
        all_pdfs = []
        for dir_path in glob.glob(pattern):
            all_pdfs.extend(list(Path(dir_path).glob('*.pdf')))
        
        print(f"  ✗ 未找到 {stock_code} 的年报PDF", flush=True)
        if all_pdfs:
            print(f"    该股票共有 {len(all_pdfs)} 个PDF文件：", flush=True)
            for pdf in all_pdfs[:5]:
                print(f"      - {pdf.name}", flush=True)
            if len(all_pdfs) > 5:
                print(f"      ... 等共 {len(all_pdfs)} 个文件", flush=True)
        return False, []
    
    print(f"  找到 {len(pdfs)} 份年报PDF", flush=True)
    
    # 3. 提取并入库
    success_years = []
    for pdf in pdfs:
        print(f"  处理: {pdf.name}", flush=True)
        data = extract_financials_from_pdf(pdf, stock_code)
        if data and data.get('year'):
            if save_to_database(data):
                success_years.append(data['year'])
                print(f"    ✓ {data['year']}年数据入库成功", flush=True)
                if data.get('revenue'):
                    print(f"      收入: {data['revenue']:,.0f}", flush=True)
                if data.get('net_profit'):
                    print(f"      净利润: {data['net_profit']:,.0f}", flush=True)
        else:
            print(f"    ✗ 提取数据失败", flush=True)
    
    return len(success_years) > 0, success_years


def verify_core_stocks():
    """验证核心股票数据"""
    core_stocks = ['00700', '09988', '03690']
    
    print(f"\n{'='*60}")
    print("验证核心股票")
    print(f"{'='*60}")
    
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    
    for code in core_stocks:
        cursor.execute('''
            SELECT year, revenue, net_profit, roe, net_margin
            FROM financials
            WHERE stock_code = ?
            ORDER BY year DESC
        ''', (code,))
        rows = cursor.fetchall()
        
        name = HK_STOCK_NAMES.get(code, code)
        print(f"\n{code} {name}:")
        if rows:
            for row in rows:
                year, rev, profit, roe, margin = row
                rev_str = f"{rev/1e9:.1f}B" if rev else "N/A"
                profit_str = f"{profit/1e9:.1f}B" if profit else "N/A"
                roe_str = f"{roe:.1f}%" if roe else "N/A"
                margin_str = f"{margin:.1f}%" if margin else "N/A"
                print(f"  {year}: 收入={rev_str}, 净利润={profit_str}, ROE={roe_str}, 净利率={margin_str}")
        else:
            print(f"  ✗ 无数据")
    
    conn.close()


def get_processed_stocks():
    """获取已处理的股票列表"""
    if not DB_PATH.exists():
        return set()
    
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    cursor.execute('SELECT DISTINCT stock_code FROM financials WHERE market = "HK"')
    stocks = set(row[0] for row in cursor.fetchall())
    conn.close()
    return stocks


def main(resume=True, clear_data=False):
    print("="*60, flush=True)
    print("港股通40只股票年报下载与入库", flush=True)
    print(f"开始时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}", flush=True)
    print("="*60, flush=True)
    
    # 1. 初始化数据库
    init_database()
    
    # 2. 根据参数决定是否清空数据
    if clear_data:
        clear_hk_data()
        processed = set()
    elif resume:
        processed = get_processed_stocks()
        print(f"断点续传：已处理 {len(processed)} 只股票", flush=True)
    else:
        processed = set()
    
    # 3. 批量处理
    success_count = len(processed)
    failed_stocks = []
    total_years = 0
    
    for i, code in enumerate(HK_STOCKS, 1):
        if code in processed:
            print(f"\n[{i}/{len(HK_STOCKS)}] 跳过 {code} {HK_STOCK_NAMES.get(code, '')} (已处理)", flush=True)
            continue
            
        print(f"\n[{i}/{len(HK_STOCKS)}] 处理 {code} {HK_STOCK_NAMES.get(code, '')}", flush=True)
        
        try:
            success, years = process_stock(code)
            if success:
                success_count += 1
                total_years += len(years)
            else:
                failed_stocks.append(code)
        except Exception as e:
            print(f"  ✗ 处理失败: {e}", flush=True)
            failed_stocks.append(code)
    
    # 4. 验证核心股票
    verify_core_stocks()
    
    # 5. 打印汇总
    print(f"\n{'='*60}", flush=True)
    print("汇总", flush=True)
    print(f"{'='*60}", flush=True)
    print(f"成功: {success_count}/{len(HK_STOCKS)} 只股票", flush=True)
    print(f"入库: {total_years} 条年度数据", flush=True)
    
    if failed_stocks:
        print(f"\n失败的股票 ({len(failed_stocks)}):", flush=True)
        for code in failed_stocks:
            print(f"  - {code} {HK_STOCK_NAMES.get(code, '')}", flush=True)
    
    print(f"\n完成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}", flush=True)
    print(f"数据库: {DB_PATH}", flush=True)


if __name__ == '__main__':
    main()
