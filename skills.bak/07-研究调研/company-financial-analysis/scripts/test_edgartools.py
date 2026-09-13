#!/usr/bin/env python3
"""测试 edgartools 对不同类型公司的支持"""

from edgar import *

set_identity("kai@example.com")

def test_company(ticker, company_type):
    """测试获取公司财务数据"""
    print(f"\n{'='*60}")
    print(f"测试 {ticker} ({company_type})")
    print('='*60)
    
    try:
        company = Company(ticker)
        print(f"公司: {company.name}")
        print(f"CIK: {company.cik}")
        
        # 获取财务数据
        financials = company.get_financials()
        
        if financials:
            print("\n✅ 财务数据可用")
            
            # 资产负债表
            bs = financials.balance_sheet()
            if bs:
                print(f"  - 资产负债表: ✅")
            else:
                print(f"  - 资产负债表: ❌")
            
            # 利润表
            income = financials.income_statement()
            if income:
                print(f"  - 利润表: ✅")
            else:
                print(f"  - 利润表: ❌")
            
            # 现金流量表
            cashflow = financials.cashflow_statement()
            if cashflow:
                print(f"  - 现金流量表: ✅")
            else:
                print(f"  - 现金流量表: ❌")
            
            # 尝试获取关键指标
            try:
                revenue = financials.get_revenue()
                print(f"  - 营收: {revenue}")
            except:
                print(f"  - 营收: 获取失败")
            
            try:
                net_income = financials.get_net_income()
                print(f"  - 净利润: {net_income}")
            except:
                print(f"  - 净利润: 获取失败")
                
            return True
        else:
            print("\n❌ 无法获取财务数据")
            return False
            
    except Exception as e:
        print(f"\n❌ 错误: {e}")
        return False

# 测试不同类型的公司
test_cases = [
    ("AAPL", "美国公司 10-K"),
    ("MSFT", "美国公司 10-K"),
    ("PDD", "中概股 20-F"),
    ("BABA", "中概股 20-F"),
    ("TSM", "外国公司 20-F"),
]

print("EdgarTools 兼容性测试")
print("=" * 60)

results = {}
for ticker, company_type in test_cases:
    results[ticker] = test_company(ticker, company_type)

print("\n" + "=" * 60)
print("测试结果汇总")
print("=" * 60)
for ticker, success in results.items():
    status = "✅ 支持" if success else "❌ 不支持"
    print(f"  {ticker}: {status}")
