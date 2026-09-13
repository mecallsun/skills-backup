#!/usr/bin/env python3
"""
从 SEC EDGAR HTML 文件中提取财务数据 - 简化版
"""

import sys
import os
import json
import re

def parse_number(text):
    """解析数字，处理千分位和负号"""
    if not text:
        return None
    text = str(text).strip()
    # 移除千分位逗号
    text = text.replace(',', '')
    # 处理括号表示的负数
    if '(' in text and ')' in text:
        text = text.replace('(', '-').replace(')', '')
    # 提取数字
    match = re.search(r'-?\d+\.?\d*', text)
    if match:
        try:
            return float(match.group())
        except:
            return None
    return None

def extract_from_html(html_path):
    """从 HTML 文件中提取财务数据"""
    with open(html_path, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()
    
    financial_data = {
        "revenue": None,
        "net_income": None,
        "total_assets": None,
        "total_liabilities": None,
        "equity": None,
        "gross_profit": None,
        "operating_income": None
    }
    
    # 清理 HTML 标签但保留文本
    text = re.sub(r'<script[^>]*>.*?</script>', ' ', content, flags=re.DOTALL)
    text = re.sub(r'<style[^>]*>.*?</style>', ' ', text, flags=re.DOTALL)
    text = re.sub(r'<[^>]+>', ' ', text)
    text = re.sub(r'\s+', ' ', text)
    
    # 查找财务数据模式
    # Revenue / Sales
    patterns = [
        (r'(?:Total\s+)?Revenue[s]?[\s:]+([\d,]+\.?\d*)', 'revenue'),
        (r'Net\s+Sales[\s:]+([\d,]+\.?\d*)', 'revenue'),
        (r'(?:Total\s+)?revenue[s]?[\s:]+\$?([\d,]+\.?\d*)\s+(?:million|billion)', 'revenue'),
        (r'Net\s+Income[\s:]+([\d,]+\.?\d*)', 'net_income'),
        (r'Net\s+income[\s:]+\$?([\d,]+\.?\d*)', 'net_income'),
        (r'Total\s+Assets[\s:]+([\d,]+\.?\d*)', 'total_assets'),
        (r'total\s+assets[\s:]+\$?([\d,]+\.?\d*)', 'total_assets'),
        (r'Total\s+Liabilit(?:y|ies)[\s:]+([\d,]+\.?\d*)', 'total_liabilities'),
        (r'total\s+liabilit(?:y|ies)[\s:]+\$?([\d,]+\.?\d*)', 'total_liabilities'),
        (r'Stockholders[\'\s]+Equity[\s:]+([\d,]+\.?\d*)', 'equity'),
        (r'total\s+stockholders[\'\s]+equity[\s:]+\$?([\d,]+\.?\d*)', 'equity'),
        (r'Gross\s+Profit[\s:]+([\d,]+\.?\d*)', 'gross_profit'),
        (r'gross\s+profit[\s:]+\$?([\d,]+\.?\d*)', 'gross_profit'),
        (r'Operating\s+Income[\s:]+([\d,]+\.?\d*)', 'operating_income'),
        (r'operating\s+income[\s:]+\$?([\d,]+\.?\d*)', 'operating_income'),
    ]
    
    for pattern, key in patterns:
        matches = re.findall(pattern, text, re.IGNORECASE)
        for match in matches:
            if match:
                value = parse_number(match)
                if value and value > 0:
                    # 如果数值太小，可能是单位问题，需要乘以 1000 或 1000000
                    if key in ['revenue', 'net_income', 'total_assets'] and value < 1000:
                        value *= 1000  # 假设单位是百万
                    if financial_data[key] is None or value > financial_data[key]:
                        financial_data[key] = value
                    break
    
    return financial_data

def main():
    if len(sys.argv) < 2:
        print("Usage: python extract_sec_financial_data.py <html_file>")
        sys.exit(1)
    
    html_path = sys.argv[1]
    if not os.path.exists(html_path):
        print(f"Error: File not found: {html_path}")
        sys.exit(1)
    
    print(f"Extracting financial data from: {html_path}")
    data = extract_from_html(html_path)
    
    print("\nExtracted Financial Data:")
    print("-" * 40)
    for key, value in data.items():
        if value is not None:
            print(f"{key}: {value:,.2f} (单位: 百万美元)")
        else:
            print(f"{key}: Not found")
    
    # Save to JSON
    output_path = html_path.replace('.htm', '_financial_data.json')
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2)
    print(f"\nData saved to: {output_path}")

if __name__ == '__main__':
    main()
