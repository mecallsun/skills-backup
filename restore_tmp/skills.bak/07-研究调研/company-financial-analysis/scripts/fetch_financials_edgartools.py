#!/usr/bin/env python3
"""
使用 edgartools 获取美股财务数据
对于不支持的 20-F 文件，回退到 HTM 解析
"""

import json
import sys
import os
from pathlib import Path

def get_financials_edgartools(ticker):
    """使用 edgartools 获取财务数据（适用于 10-K/10-Q）"""
    try:
        from edgar import Company, set_identity
        set_identity("kai@example.com")
        
        company = Company(ticker)
        financials = company.get_financials()
        
        if not financials:
            return None
        
        result = {
            "company": company.name,
            "cik": company.cik,
            "source": "edgartools",
            "data": {}
        }
        
        # 获取关键指标
        try:
            result["data"]["revenue"] = financials.get_revenue()
        except:
            pass
        
        try:
            result["data"]["net_income"] = financials.get_net_income()
        except:
            pass
        
        try:
            result["data"]["total_assets"] = financials.get_total_assets()
        except:
            pass
        
        try:
            result["data"]["total_liabilities"] = financials.get_total_liabilities()
        except:
            pass
        
        try:
            result["data"]["stockholders_equity"] = financials.get_stockholders_equity()
        except:
            pass
        
        try:
            result["data"]["operating_cash_flow"] = financials.get_operating_cash_flow()
        except:
            pass
        
        try:
            result["data"]["free_cash_flow"] = financials.get_free_cash_flow()
        except:
            pass
        
        # 获取完整报表
        try:
            bs = financials.balance_sheet()
            if bs:
                result["balance_sheet"] = str(bs)
        except:
            pass
        
        try:
            income = financials.income_statement()
            if income:
                result["income_statement"] = str(income)
        except:
            pass
        
        try:
            cf = financials.cashflow_statement()
            if cf:
                result["cashflow_statement"] = str(cf)
        except:
            pass
        
        return result
        
    except Exception as e:
        print(f"edgartools 错误: {e}")
        return None


def parse_20f_htm(htm_path):
    """解析 20-F HTM 文件提取财务数据"""
    from bs4 import BeautifulSoup
    import re
    
    with open(htm_path, 'r', encoding='utf-8') as f:
        html = f.read()
    
    soup = BeautifulSoup(html, 'html.parser')
    
    result = {
        "source": "htm_parser",
        "file": htm_path,
        "data": {},
        "tables": []
    }
    
    # 提取 RMB 金额
    rmb_pattern = r'RMB\s*([0-9,]+(?:\.[0-9]+)?)\s*(million|billion)?'
    matches = re.findall(rmb_pattern, html, re.IGNORECASE)
    result["rmb_amounts"] = [(m[0], m[1]) for m in matches[:50]]
    
    # 搜索关键财务表格
    keywords = [
        'Consolidated Statements of Operations',
        'Consolidated Balance Sheet',
        'Consolidated Statements of Cash Flows',
        'Total revenues',
        'Net income',
        'Total assets'
    ]
    
    tables = soup.find_all('table')
    for i, table in enumerate(tables):
        text = table.get_text()[:1000]
        for kw in keywords:
            if kw.lower() in text.lower():
                # 提取表格数据
                rows = []
                for tr in table.find_all('tr')[:30]:
                    cells = [td.get_text(strip=True) for td in tr.find_all(['td', 'th'])]
                    if any(cells):
                        rows.append(cells)
                
                if rows:
                    result["tables"].append({
                        "keyword": kw,
                        "table_index": i,
                        "rows": rows[:20]
                    })
                break
    
    return result


def main():
    if len(sys.argv) < 2:
        print("用法: python3 fetch_financials_edgartools.py <ticker> [output_dir]")
        sys.exit(1)
    
    ticker = sys.argv[1].upper()
    output_dir = sys.argv[2] if len(sys.argv) > 2 else "."
    
    print(f"获取 {ticker} 财务数据...")
    
    # 首先尝试 edgartools
    result = get_financials_edgartools(ticker)
    
    if result:
        print(f"✅ edgartools 成功获取 {ticker} 财务数据")
    else:
        print(f"⚠️ edgartools 不支持 {ticker}，尝试解析本地 HTM 文件...")
        
        # 查找本地 HTM 文件
        possible_dirs = [
            Path(output_dir),
            Path(f"company_analysis_{ticker}_*"),
        ]
        
        htm_files = []
        for pattern in [f"**/20-F*.htm", f"**/{ticker}*.htm", f"**/annual_reports/*.htm"]:
            htm_files.extend(Path(output_dir).glob(pattern))
        
        if htm_files:
            print(f"找到 {len(htm_files)} 个 HTM 文件")
            result = parse_20f_htm(str(htm_files[0]))
        else:
            print(f"❌ 未找到 HTM 文件")
            result = {"error": f"无法获取 {ticker} 财务数据"}
    
    # 保存结果
    output_file = Path(output_dir) / f"financials_{ticker}.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2, default=str)
    
    print(f"结果保存到: {output_file}")
    
    # 打印摘要
    if "data" in result:
        print("\n=== 财务摘要 ===")
        for key, value in result["data"].items():
            if value:
                print(f"  {key}: {value:,.0f}" if isinstance(value, (int, float)) else f"  {key}: {value}")


if __name__ == "__main__":
    main()
