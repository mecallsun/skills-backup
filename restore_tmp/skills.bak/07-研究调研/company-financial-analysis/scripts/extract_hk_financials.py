#!/usr/bin/env python3
"""
港股财务数据提取器
从PDF年报中提取财务数据并入库
"""

import os
import re
import sqlite3
import PyPDF2
from datetime import datetime

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

def extract_pdf_text(pdf_path, max_pages=15):
    """提取PDF文本"""
    try:
        with open(pdf_path, 'rb') as f:
            reader = PyPDF2.PdfReader(f)
            text = ''
            for i, page in enumerate(reader.pages[:max_pages]):
                try:
                    text += page.extract_text() + '\n'
                except:
                    continue
            return text
    except Exception as e:
        print(f"  Error reading PDF: {e}")
        return ''

def parse_number(text):
    """解析数字，处理千分位逗号"""
    if not text:
        return None
    # 移除逗号和空格
    text = text.replace(',', '').replace(' ', '').replace('，', '')
    # 处理括号表示负数
    if text.startswith('(') and text.endswith(')'):
        text = '-' + text[1:-1]
    try:
        return float(text)
    except:
        return None

def extract_financials_from_text(text, stock_code):
    """从文本中提取财务数据"""
    data = {
        'revenue': None,
        'net_profit': None,
        'total_assets': None,
        'total_equity': None,
        'total_liabilities': None,
        'roe': None,
        'gross_margin': None,
        'net_margin': None,
    }
    
    # 不同公司可能有不同的格式
    # 繁体中文关键词
    revenue_patterns = [
        r'收入[^\d]*?([\d,]+(?:\.\d+)?)\s*(?:百万|千|億)?',
        r'營業?收入[^\d]*?([\d,]+(?:\.\d+)?)',
        r'收益[^\d]*?([\d,]+(?:\.\d+)?)',
        r'Revenue[^\d]*?([\d,]+(?:\.\d+)?)',
        r'ϗɝ\s*([\d,]+)',  # 腾讯格式
        r'收入\s*(\d[\d,]*)',
        r'總收入[^\d]*([\d,]+)',
        r'营业收入[^\d]*([\d,]+)',
    ]
    
    profit_patterns = [
        r'(?:本公司|母公司)?(?:擁有人|股東|权益持有人)?應?佔?(?:溢利|利潤|淨利潤)[^\d]*?([\d,]+(?:\.\d+)?)',
        r'(?:歸屬於|归属于)?(?:母公司)?(?:所有者|股東)?的?淨?利潤[^\d]*?([\d,]+(?:\.\d+)?)',
        r'Net ?[Pp]rofit[^\d]*?([\d,]+(?:\.\d+)?)',
        r'Profit attributable[^\d]*?([\d,]+(?:\.\d+)?)',
        r'͉ʮ̡ᛆूܵϞɛᏐЦޮл\s*([\d,]+)',  # 腾讯格式
        r'溢利[^\d]*([\d,]+)',
        r'净利润[^\d]*([\d,]+)',
        r'本年溢利[^\d]*([\d,]+)',
    ]
    
    assets_patterns = [
        r'(?:資產|资产)總額?[^\d]*?([\d,]+(?:\.\d+)?)',
        r'Total ?[Aa]ssets?[^\d]*?([\d,]+(?:\.\d+)?)',
        r'總資產[^\d]*?([\d,]+)',
        r'资产合计[^\d]*([\d,]+)',
    ]
    
    equity_patterns = [
        r'(?:股東|股东)?(?:權益|权益)總額?[^\d]*?([\d,]+(?:\.\d+)?)',
        r'Total ?[Ee]quity[^\d]*?([\d,]+(?:\.\d+)?)',
        r'權益合計[^\d]*?([\d,]+)',
        r'所有者权益[^\d]*([\d,]+)',
    ]
    
    # 尝试匹配各模式
    for pattern in revenue_patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match and data['revenue'] is None:
            val = parse_number(match.group(1))
            if val and val > 100:  # 过滤太小的数
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
    
    # 计算衍生指标
    if data['revenue'] and data['net_profit']:
        data['net_margin'] = round(data['net_profit'] / data['revenue'] * 100, 2)
    
    if data['total_equity'] and data['net_profit']:
        data['roe'] = round(data['net_profit'] / data['total_equity'] * 100, 2)
    
    if data['total_assets'] and data['total_equity']:
        data['total_liabilities'] = data['total_assets'] - data['total_equity']
    
    return data

def extract_year_from_filename(filename):
    """从文件名提取年份"""
    # 匹配 2024年 或 二零二四年 等格式
    match = re.search(r'(20\d{2})年', filename)
    if match:
        return int(match.group(1))
    
    # 匹配二零XX年格式
    cn_year_map = {'零': '0', '一': '1', '二': '2', '三': '3', '四': '4',
                   '五': '5', '六': '6', '七': '7', '八': '8', '九': '9',
                   'ɚ': '2', 'ɧ': '3', '̬': '4', 'ʞ': '5'}
    match = re.search(r'二[零〇]([二三四五六七八九])[零一二三四五六七八九]', filename)
    if match:
        return 2020 + int(cn_year_map.get(match.group(1), '0'))
    
    return None

def find_annual_report(data_dir, stock_code):
    """找到最新的年报或业绩公告PDF"""
    # 查找股票目录
    dirs = [d for d in os.listdir(data_dir) if d.startswith(stock_code + '_')]
    if not dirs:
        return None, None
    
    stock_dir = os.path.join(data_dir, dirs[0])
    if not os.path.isdir(stock_dir):
        return None, None
    
    # 查找PDF文件，优先年度业绩/年报
    pdfs = [f for f in os.listdir(stock_dir) if f.endswith('.pdf')]
    
    # 优先级：年度业绩公告 > 年报 > 中期业绩
    priority_keywords = ['年度業績', '年度业绩', '全年業績', '全年业绩', '年度報告', '年度报告', 'Annual']
    
    # 按年份分组
    year_pdfs = {}
    for pdf in pdfs:
        year = extract_year_from_filename(pdf)
        if year:
            if year not in year_pdfs:
                year_pdfs[year] = []
            year_pdfs[year].append(pdf)
    
    if not year_pdfs:
        return None, None
    
    # 取最近年份
    latest_year = max(year_pdfs.keys())
    candidates = year_pdfs[latest_year]
    
    # 按优先级排序
    for keyword in priority_keywords:
        for pdf in candidates:
            if keyword in pdf:
                return os.path.join(stock_dir, pdf), latest_year
    
    # 没有匹配，返回第一个
    return os.path.join(stock_dir, candidates[0]), latest_year

def save_to_db(db_path, stock_code, stock_name, year, data, source_file):
    """保存到数据库"""
    conn = sqlite3.connect(db_path)
    c = conn.cursor()
    
    # 检查是否存在
    c.execute('SELECT id FROM financials WHERE stock_code=? AND year=?', (stock_code, year))
    existing = c.fetchone()
    
    if existing:
        # 更新
        c.execute('''UPDATE financials SET 
            stock_name=?, revenue=?, net_profit=?, total_assets=?, total_equity=?,
            total_liabilities=?, roe=?, net_margin=?, source=?, currency='HKD'
            WHERE stock_code=? AND year=?''',
            (stock_name, data['revenue'], data['net_profit'], data['total_assets'],
             data['total_equity'], data['total_liabilities'], data['roe'],
             data['net_margin'], source_file, stock_code, year))
    else:
        # 插入
        c.execute('''INSERT INTO financials 
            (stock_code, stock_name, market, year, revenue, net_profit, total_assets,
             total_equity, total_liabilities, roe, net_margin, currency, source)
            VALUES (?, ?, 'HK', ?, ?, ?, ?, ?, ?, ?, ?, 'HKD', ?)''',
            (stock_code, stock_name, year, data['revenue'], data['net_profit'],
             data['total_assets'], data['total_equity'], data['total_liabilities'],
             data['roe'], data['net_margin'], source_file))
    
    conn.commit()
    conn.close()

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.join(script_dir, 'data')
    db_path = os.path.join(script_dir, 'financials.db')
    
    print(f"数据目录: {data_dir}")
    print(f"数据库: {db_path}")
    print(f"目标股票: {len(HK_STOCKS)}")
    print()
    
    success_count = 0
    fail_count = 0
    
    for stock_code, stock_name in sorted(HK_STOCKS.items()):
        print(f"处理 {stock_code} {stock_name}...")
        
        pdf_path, year = find_annual_report(data_dir, stock_code)
        if not pdf_path:
            print(f"  未找到年报PDF")
            fail_count += 1
            continue
        
        print(f"  文件: {os.path.basename(pdf_path)}")
        print(f"  年份: {year}")
        
        text = extract_pdf_text(pdf_path)
        if not text:
            print(f"  无法提取文本")
            fail_count += 1
            continue
        
        data = extract_financials_from_text(text, stock_code)
        
        # 检查是否提取到数据
        has_data = any(v is not None for v in [data['revenue'], data['net_profit'], data['total_assets']])
        
        if has_data:
            print(f"  营收: {data['revenue']}")
            print(f"  净利润: {data['net_profit']}")
            print(f"  总资产: {data['total_assets']}")
            print(f"  ROE: {data['roe']}")
            
            save_to_db(db_path, stock_code, stock_name, year, data, pdf_path)
            success_count += 1
        else:
            print(f"  未能提取到财务数据")
            fail_count += 1
        
        print()
    
    print("=" * 50)
    print(f"成功: {success_count}")
    print(f"失败: {fail_count}")

if __name__ == '__main__':
    main()
