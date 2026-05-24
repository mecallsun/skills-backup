#!/usr/bin/env python3
"""
港股财务数据提取器 v2
使用pdftotext提取，更好的正则匹配
"""

import os
import re
import sqlite3
import subprocess
from collections import defaultdict

# 港股代码与名称映射
HK_STOCKS = {
    '00700': '腾讯控股',
    '09988': '阿里巴巴-SW',
    '03690': '美团-W',
    '09618': '京东集团-SW',
    '09888': '百度集团-SW',
    '01810': '小米集团-W',
    '09999': '网易-S',
    '00941': '中国移动',
    '00388': '香港交易所',
    '02318': '中国平安',
    '00005': '汇丰控股',
    '00939': '建设银行',
    '01398': '工商银行',
    '00883': '中国海洋石油',
    '00857': '中国石油股份',
    '02020': '安踏体育',
    '09992': '泡泡玛特',
    '01211': '比亚迪股份',
    '02333': '长城汽车',
    '00175': '吉利汽车',
    '09866': '蔚来-SW',
    '09868': '小鹏汽车-W',
    '02015': '理想汽车-W',
    '06618': '京东健康',
    '00268': '金蝶国际',
    '03888': '金山软件',
    '00020': '商汤-W',
    '09961': '携程集团-S',
    '01024': '快手-W',
    '02382': '舜宇光学科技',
    '00669': '创科实业',
    '01928': '金沙中国有限公司',
    '00027': '银河娱乐',
    '00981': '中芯国际',
    '00772': '阅文集团',
    '06060': '众安在线',
    '02269': '药明生物',
    '01177': '中国生物制药',
    '03759': '康龙化成',
    '06185': '康希诺生物',
}

def extract_pdf_text(pdf_path):
    """使用pdftotext提取PDF文本"""
    try:
        result = subprocess.run(
            ['pdftotext', '-layout', pdf_path, '-'],
            capture_output=True, text=True, timeout=60
        )
        return result.stdout
    except Exception as e:
        print(f"  pdftotext error: {e}")
        return ''

def parse_number(text):
    """解析数字"""
    if not text:
        return None
    text = text.replace(',', '').replace(' ', '').replace('，', '').strip()
    if text.startswith('(') and text.endswith(')'):
        text = '-' + text[1:-1]
    try:
        return float(text)
    except:
        return None

def extract_financials(text, stock_code):
    """提取财务数据"""
    data = {
        'revenue': None,
        'net_profit': None,
        'total_assets': None,
        'total_equity': None,
        'gross_margin': None,
        'net_margin': None,
        'roe': None,
        'currency': 'CNY',  # 大部分港股用人民币报告
        'unit': 1000000,    # 默认百万
    }
    
    # 检测货币单位
    if '港元' in text or 'HKD' in text or '港幣' in text:
        data['currency'] = 'HKD'
    elif '美元' in text or 'USD' in text or 'US$' in text:
        data['currency'] = 'USD'
    
    # 检测数量单位
    if '千元' in text:
        data['unit'] = 1000
    elif '百萬' in text or '百万' in text:
        data['unit'] = 1000000
    elif '億' in text or '亿' in text:
        data['unit'] = 100000000
    
    # 营收模式 - 年度数据
    revenue_patterns = [
        # 标准年度格式
        r'(?:年度|全年|年)?收入\s+(\d[\d,]*(?:\.\d+)?)\s+(\d[\d,]*(?:\.\d+)?)\s+[\d%\-]+',
        r'收入\s+(\d[\d,]*(?:\.\d+)?)',
        r'營業收入[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'Revenue[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'总收入[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'總收入[^\d]*(\d[\d,]*(?:\.\d+)?)',
    ]
    
    # 净利润模式
    profit_patterns = [
        # 股东应占
        r'(?:本公司)?(?:權益|权益)?(?:持有人|股東|股东)?(?:應|应)?佔?(?:盈利|溢利|利潤|净利润)\s+(\d[\d,]*(?:\.\d+)?)',
        r'(?:年度|全年)?(?:盈利|溢利)\s+(\d[\d,]*(?:\.\d+)?)',
        r'淨?利潤[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'净利润[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'Net profit[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'Profit attributable[^\d]*(\d[\d,]*(?:\.\d+)?)',
    ]
    
    # 总资产模式
    assets_patterns = [
        r'(?:資產|资产)(?:總額|总额|合計|合计)[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'Total\s+[Aa]ssets?[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'總資產[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'总资产[^\d]*(\d[\d,]*(?:\.\d+)?)',
    ]
    
    # 股东权益模式
    equity_patterns = [
        r'(?:股東|股东)?(?:權益|权益)(?:總額|总额|合計|合计)?[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'Total\s+[Ee]quity[^\d]*(\d[\d,]*(?:\.\d+)?)',
        r'所有者权益[^\d]*(\d[\d,]*(?:\.\d+)?)',
    ]
    
    # 毛利模式
    gross_patterns = [
        r'毛利\s+(\d[\d,]*(?:\.\d+)?)',
        r'Gross profit[^\d]*(\d[\d,]*(?:\.\d+)?)',
    ]
    
    # 提取数据
    for pattern in revenue_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match and data['revenue'] is None:
            # 如果有两个数字，取第一个（当年）
            val = parse_number(match.group(1))
            if val and val > 100:
                data['revenue'] = val
                break
    
    for pattern in profit_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match and data['net_profit'] is None:
            val = parse_number(match.group(1))
            if val:
                data['net_profit'] = val
                break
    
    for pattern in assets_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match and data['total_assets'] is None:
            val = parse_number(match.group(1))
            if val and val > 100:
                data['total_assets'] = val
                break
    
    for pattern in equity_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match and data['total_equity'] is None:
            val = parse_number(match.group(1))
            if val and val > 100:
                data['total_equity'] = val
                break
    
    gross = None
    for pattern in gross_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            gross = parse_number(match.group(1))
            break
    
    # 计算比率
    if data['revenue'] and gross:
        data['gross_margin'] = round(gross / data['revenue'] * 100, 2)
    if data['revenue'] and data['net_profit']:
        data['net_margin'] = round(data['net_profit'] / data['revenue'] * 100, 2)
    if data['total_equity'] and data['net_profit']:
        data['roe'] = round(data['net_profit'] / data['total_equity'] * 100, 2)
    
    return data

def extract_year_from_filename(filename):
    """从文件名提取年份"""
    # 优先匹配年度报告/业绩
    match = re.search(r'(20\d{2})年(?:度|財政年度)?(?:業績|业绩|報告|报告)', filename)
    if match:
        return int(match.group(1))
    
    # 匹配截至xxxx年十二月三十一日
    match = re.search(r'截至.*?(20\d{2})年.*?(?:十二月|12月)', filename)
    if match:
        return int(match.group(1))
    
    # 一般年份匹配
    match = re.search(r'(20\d{2})年', filename)
    if match:
        return int(match.group(1))
    
    return None

def find_best_report(data_dir, stock_code):
    """找到最佳年报PDF"""
    # 查找股票目录
    dirs = [d for d in os.listdir(data_dir) if d.startswith(stock_code + '_')]
    if not dirs:
        return None, None
    
    stock_dir = os.path.join(data_dir, dirs[0])
    pdfs = [f for f in os.listdir(stock_dir) if f.endswith('.pdf')]
    
    if not pdfs:
        return None, None
    
    # 优先关键词（年度业绩公告最有价值）
    priority_keywords = [
        '年度業績公', '年度业绩公', '全年業績公', '全年业绩公',
        '年度報告', '年度报告', 'Annual',
        '業績公告', '业绩公告', '業績公布', '业绩公布',
    ]
    
    # 按年份分组
    year_pdfs = defaultdict(list)
    for pdf in pdfs:
        year = extract_year_from_filename(pdf)
        if year and year >= 2023:  # 只要最近两年
            year_pdfs[year].append(pdf)
    
    if not year_pdfs:
        # 没有年份，取最新的
        return os.path.join(stock_dir, pdfs[0]), 2024
    
    # 取最近年份
    latest_year = max(year_pdfs.keys())
    candidates = year_pdfs[latest_year]
    
    # 按优先级找
    for keyword in priority_keywords:
        for pdf in candidates:
            if keyword in pdf:
                return os.path.join(stock_dir, pdf), latest_year
    
    return os.path.join(stock_dir, candidates[0]), latest_year

def save_to_db(db_path, stock_code, stock_name, year, data, source_file):
    """保存到数据库"""
    conn = sqlite3.connect(db_path)
    c = conn.cursor()
    
    # 转换单位为元（从百万）
    revenue = data['revenue'] * data['unit'] if data['revenue'] else None
    net_profit = data['net_profit'] * data['unit'] if data['net_profit'] else None
    total_assets = data['total_assets'] * data['unit'] if data['total_assets'] else None
    total_equity = data['total_equity'] * data['unit'] if data['total_equity'] else None
    total_liabilities = (total_assets - total_equity) if (total_assets and total_equity) else None
    
    c.execute('SELECT id FROM financials WHERE stock_code=? AND year=?', (stock_code, year))
    existing = c.fetchone()
    
    if existing:
        c.execute('''UPDATE financials SET 
            stock_name=?, revenue=?, net_profit=?, total_assets=?, total_equity=?,
            total_liabilities=?, roe=?, gross_margin=?, net_margin=?, 
            source=?, currency=?, market='HK'
            WHERE stock_code=? AND year=?''',
            (stock_name, revenue, net_profit, total_assets, total_equity,
             total_liabilities, data['roe'], data['gross_margin'], data['net_margin'],
             source_file, data['currency'], stock_code, year))
    else:
        c.execute('''INSERT INTO financials 
            (stock_code, stock_name, market, year, revenue, net_profit, total_assets,
             total_equity, total_liabilities, roe, gross_margin, net_margin, 
             currency, source)
            VALUES (?, ?, 'HK', ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)''',
            (stock_code, stock_name, year, revenue, net_profit, total_assets,
             total_equity, total_liabilities, data['roe'], data['gross_margin'],
             data['net_margin'], data['currency'], source_file))
    
    conn.commit()
    conn.close()

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.join(script_dir, 'data')
    db_path = os.path.join(script_dir, 'financials.db')
    
    print(f"目标股票: {len(HK_STOCKS)}")
    print("=" * 60)
    
    results = {'success': [], 'partial': [], 'fail': []}
    
    for stock_code, stock_name in sorted(HK_STOCKS.items()):
        print(f"\n{stock_code} {stock_name}")
        
        pdf_path, year = find_best_report(data_dir, stock_code)
        if not pdf_path:
            print(f"  ❌ 未找到PDF")
            results['fail'].append(stock_code)
            continue
        
        print(f"  📄 {os.path.basename(pdf_path)}")
        print(f"  📅 年份: {year}")
        
        text = extract_pdf_text(pdf_path)
        if not text or len(text) < 500:
            print(f"  ❌ 文本提取失败")
            results['fail'].append(stock_code)
            continue
        
        data = extract_financials(text, stock_code)
        
        # 评估结果
        has_revenue = data['revenue'] is not None
        has_profit = data['net_profit'] is not None
        has_assets = data['total_assets'] is not None
        
        status = '✅' if (has_revenue and has_profit) else ('⚠️' if has_revenue else '❌')
        
        print(f"  {status} 营收: {data['revenue']} | 净利: {data['net_profit']} | 资产: {data['total_assets']}")
        print(f"     货币: {data['currency']} | 毛利率: {data['gross_margin']} | 净利率: {data['net_margin']} | ROE: {data['roe']}")
        
        if has_revenue or has_profit or has_assets:
            save_to_db(db_path, stock_code, stock_name, year, data, pdf_path)
            if has_revenue and has_profit:
                results['success'].append(stock_code)
            else:
                results['partial'].append(stock_code)
        else:
            results['fail'].append(stock_code)
    
    print("\n" + "=" * 60)
    print(f"完整数据: {len(results['success'])} - {results['success']}")
    print(f"部分数据: {len(results['partial'])} - {results['partial']}")
    print(f"失败: {len(results['fail'])} - {results['fail']}")

if __name__ == '__main__':
    main()
