#!/usr/bin/env python3
"""
获取美股季度财务数据

对于外国发行人 (20-F/6-K 如 PDD, BABA, TSM)，SEC 6-K 文件仅为封面页，
不包含详细财务数据。需要通过搜索业绩公告获取季度数据。

用法:
    python3 fetch_us_quarterly.py <ticker> [output_dir]

示例:
    python3 fetch_us_quarterly.py PDD
    python3 fetch_us_quarterly.py BABA company_analysis_BABA_20260131
"""

import json
import sys
import os
from pathlib import Path
from datetime import datetime


def check_edgartools_support(ticker):
    """检查 edgartools 是否支持该公司"""
    try:
        from edgar import Company, set_identity
        set_identity("kai@example.com")
        
        company = Company(ticker)
        financials = company.get_financials()
        
        if financials:
            # 尝试获取数据
            try:
                revenue = financials.get_revenue()
                if revenue:
                    return True, company.name
            except:
                pass
        return False, company.name if company else ticker
    except Exception as e:
        return False, ticker


def get_financials_edgartools(ticker):
    """使用 edgartools 获取财务数据（适用于标准美股 10-K/10-Q）"""
    try:
        from edgar import Company, set_identity
        set_identity("kai@example.com")
        
        company = Company(ticker)
        financials = company.get_financials()
        
        if not financials:
            return None
        
        result = {
            "company": company.name,
            "ticker": ticker,
            "source": "edgartools",
            "fetch_time": datetime.now().isoformat(),
            "data": {}
        }
        
        # 获取关键指标
        metrics = [
            ("revenue", financials.get_revenue),
            ("net_income", financials.get_net_income),
            ("total_assets", financials.get_total_assets),
            ("total_liabilities", financials.get_total_liabilities),
            ("stockholders_equity", financials.get_stockholders_equity),
            ("operating_cash_flow", financials.get_operating_cash_flow),
            ("free_cash_flow", financials.get_free_cash_flow),
        ]
        
        for name, func in metrics:
            try:
                value = func()
                if value is not None:
                    result["data"][name] = value
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


def generate_quarterly_search_prompt(ticker, company_name, output_dir):
    """生成季度数据搜索指引（用于 web_search）"""
    
    current_year = datetime.now().year
    
    prompt = f"""## 美股季度财务数据搜索任务

**公司**: {company_name} ({ticker})
**说明**: 该公司使用 20-F/6-K 格式，SEC 文件不含详细财务数据，需通过搜索业绩公告获取。

### 推荐搜索查询

1. `{company_name} {current_year} Q1 财报 营收 净利润`
2. `{company_name} {current_year} Q2 财报 营收`
3. `{company_name} {current_year} Q3 财报 业绩`
4. `{ticker} quarterly earnings {current_year} revenue`
5. `{ticker} Q3 {current_year} financial results`

### 需要提取的数据

| 季度 | 营收 | 同比增速 | 净利润 | 同比增速 | 来源 |
|------|------|----------|--------|----------|------|
| Q1 {current_year} | | | | | |
| Q2 {current_year} | | | | | |
| Q3 {current_year} | | | | | |

### 其他关键指标
- 毛利率
- 经营利润率
- Non-GAAP 净利润
- 用户/GMV 增长数据
- 管理层指引

### 输出格式

```json
{{
    "company": "{company_name}",
    "ticker": "{ticker}",
    "fiscal_year": {current_year},
    "quarters": [
        {{
            "quarter": "Q1",
            "revenue": "xxx亿元",
            "revenue_yoy": "+xx%",
            "net_income": "xxx亿元",
            "net_income_yoy": "+xx%",
            "source": "新闻链接"
        }}
    ],
    "notes": "其他重要信息"
}}
```

### 保存位置

`{output_dir}/processed_data/quarterly_data.json`
"""
    
    return prompt


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    
    ticker = sys.argv[1].upper()
    output_dir = sys.argv[2] if len(sys.argv) > 2 else f"company_analysis_{ticker}"
    
    print(f"检查 {ticker} 数据获取方式...")
    
    # 检查 edgartools 是否支持
    supported, company_name = check_edgartools_support(ticker)
    
    if supported:
        print(f"✅ {ticker} 支持 edgartools 自动提取")
        result = get_financials_edgartools(ticker)
        
        if result:
            # 保存结果
            os.makedirs(f"{output_dir}/processed_data", exist_ok=True)
            output_file = f"{output_dir}/processed_data/financials_{ticker}.json"
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(result, f, ensure_ascii=False, indent=2, default=str)
            print(f"财务数据已保存: {output_file}")
            
            # 打印摘要
            print("\n=== 财务摘要 ===")
            for key, value in result.get("data", {}).items():
                if isinstance(value, (int, float)):
                    print(f"  {key}: {value:,.0f}")
                else:
                    print(f"  {key}: {value}")
    else:
        print(f"⚠️ {ticker} ({company_name}) 不支持 edgartools 自动提取")
        print("   原因: 外国发行人 (20-F/6-K) 格式不兼容")
        print("   解决: 需要通过 web_search 搜索业绩公告获取季度数据")
        
        # 生成搜索指引
        os.makedirs(f"{output_dir}/processed_data", exist_ok=True)
        prompt = generate_quarterly_search_prompt(ticker, company_name, output_dir)
        
        prompt_file = f"{output_dir}/processed_data/quarterly_search_prompt.md"
        with open(prompt_file, 'w', encoding='utf-8') as f:
            f.write(prompt)
        print(f"\n搜索指引已生成: {prompt_file}")
        
        # 创建空的季度数据模板
        template = {
            "company": company_name,
            "ticker": ticker,
            "source": "web_search",
            "note": "需要通过 web_search 搜索业绩公告获取数据",
            "fiscal_year": datetime.now().year,
            "quarters": [],
            "fetch_time": None
        }
        
        template_file = f"{output_dir}/processed_data/quarterly_data.json"
        with open(template_file, 'w', encoding='utf-8') as f:
            json.dump(template, f, ensure_ascii=False, indent=2)
        print(f"季度数据模板已创建: {template_file}")


if __name__ == "__main__":
    main()
