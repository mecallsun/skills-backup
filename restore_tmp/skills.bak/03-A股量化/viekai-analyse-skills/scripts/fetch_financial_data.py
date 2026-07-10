#!/usr/bin/env python3
"""
Fetch financial data using AkShare
"""
import sys
from pathlib import Path
import pandas as pd

try:
    import akshare as ak
except ImportError:
    print("Error: akshare not installed. Install with: pip install akshare")
    sys.exit(1)

from utils import save_json, normalize_stock_code, format_number


def fetch_company_info(stock_code: str, market_type: str) -> dict:
    """Fetch basic company information"""
    try:
        code_without_suffix = stock_code.split('.')[0]

        if market_type == 'HK':
            # Hong Kong stock info
            # Note: AkShare has limited support for HK stocks
            # Using basic info structure
            return {
                'stock_code': stock_code,
                'company_name': f'HK Stock {code_without_suffix}',
                'industry': 'N/A (Manual research required)',
                'listing_date': 'N/A',
                'market': 'Hong Kong',
                'note': 'Hong Kong stock data requires manual research or specialized APIs'
            }
        else:
            # A-share stock info
            stock_info = ak.stock_individual_info_em(symbol=code_without_suffix)
            info_dict = dict(zip(stock_info['item'], stock_info['value']))

            return {
                'stock_code': stock_code,
                'company_name': info_dict.get('股票简称', 'N/A'),
                'industry': info_dict.get('行业', 'N/A'),
                'listing_date': info_dict.get('上市时间', 'N/A'),
                'total_share_capital': info_dict.get('总股本', 'N/A'),
                'circulating_share_capital': info_dict.get('流通股', 'N/A'),
                'market': 'A-share'
            }
    except Exception as e:
        print(f"Warning: Failed to fetch company info: {e}")
        return {'stock_code': stock_code, 'error': str(e)}


def fetch_financial_statements(stock_code: str, market_type: str, output_dir: Path) -> dict:
    """Fetch financial statements (balance sheet, income statement, cash flow)"""
    results = {
        'balance_sheet': None,
        'income_statement': None,
        'cash_flow': None,
    }

    code_without_suffix = stock_code.split('.')[0]

    if market_type == 'HK':
        print("  Note: Hong Kong stock financial data requires specialized APIs")
        print("  Creating template for manual data input...")
        # Create template for manual input
        template_file = output_dir / 'raw_data' / 'financial_reports' / 'hk_data_template.txt'
        with open(template_file, 'w', encoding='utf-8') as f:
            f.write("Hong Kong Stock Financial Data Template\n")
            f.write("=" * 50 + "\n\n")
            f.write("Please manually collect financial data from:\n")
            f.write("1. HKEx website (www.hkexnews.hk)\n")
            f.write("2. Company annual reports\n")
            f.write("3. Financial data providers (Bloomberg, Wind, etc.)\n\n")
            f.write("Required data:\n")
            f.write("- Balance Sheet (5 years)\n")
            f.write("- Income Statement (5 years)\n")
            f.write("- Cash Flow Statement (5 years)\n")
        print(f"  ✓ Template created: {template_file}")
        return results

    # A-share data collection
    try:
        # Balance Sheet
        print("Fetching balance sheet...")
        balance_sheet = ak.stock_balance_sheet_by_report_em(symbol=code_without_suffix)
        if not balance_sheet.empty:
            balance_sheet.to_csv(output_dir / 'raw_data' / 'financial_reports' / 'balance_sheet.csv', index=False, encoding='utf-8-sig')
            results['balance_sheet'] = balance_sheet.to_dict('records')
            print(f"  ✓ Balance sheet: {len(balance_sheet)} periods")
    except Exception as e:
        print(f"  ✗ Balance sheet failed: {e}")

    try:
        # Income Statement
        print("Fetching income statement...")
        income_statement = ak.stock_profit_sheet_by_report_em(symbol=code_without_suffix)
        if not income_statement.empty:
            income_statement.to_csv(output_dir / 'raw_data' / 'financial_reports' / 'income_statement.csv', index=False, encoding='utf-8-sig')
            results['income_statement'] = income_statement.to_dict('records')
            print(f"  ✓ Income statement: {len(income_statement)} periods")
    except Exception as e:
        print(f"  ✗ Income statement failed: {e}")

    try:
        # Cash Flow Statement
        print("Fetching cash flow statement...")
        cash_flow = ak.stock_cash_flow_sheet_by_report_em(symbol=code_without_suffix)
        if not cash_flow.empty:
            cash_flow.to_csv(output_dir / 'raw_data' / 'financial_reports' / 'cash_flow.csv', index=False, encoding='utf-8-sig')
            results['cash_flow'] = cash_flow.to_dict('records')
            print(f"  ✓ Cash flow: {len(cash_flow)} periods")
    except Exception as e:
        print(f"  ✗ Cash flow failed: {e}")

    return results


def fetch_financial_indicators(stock_code: str, market_type: str, output_dir: Path) -> dict:
    """Fetch key financial indicators"""
    if market_type == 'HK':
        print("  Note: Hong Kong stock indicators require manual collection")
        return None

    try:
        print("Fetching financial indicators...")
        code_without_suffix = stock_code.split('.')[0]
        indicators = ak.stock_financial_analysis_indicator(symbol=code_without_suffix)

        if not indicators.empty:
            indicators.to_csv(output_dir / 'raw_data' / 'financial_reports' / 'financial_indicators.csv', index=False, encoding='utf-8-sig')
            print(f"  ✓ Financial indicators: {len(indicators)} periods")
            return indicators.to_dict('records')
    except Exception as e:
        print(f"  ✗ Financial indicators failed: {e}")

    return None


def main(stock_code: str, output_dir: Path):
    """Main function to fetch all financial data"""
    normalized_code, market, market_type = normalize_stock_code(stock_code)

    print(f"\n{'='*60}")
    print(f"Fetching Financial Data for {normalized_code}")
    print(f"Market: {market_type} ({market})")
    print(f"{'='*60}\n")

    # Fetch company info
    print("1. Fetching company information...")
    company_info = fetch_company_info(normalized_code, market_type)
    save_json(company_info, output_dir / 'processed_data' / 'company_info.json')
    print(f"  ✓ Company: {company_info.get('company_name', 'N/A')}")
    print(f"  ✓ Industry: {company_info.get('industry', 'N/A')}\n")

    # Fetch financial statements
    print("2. Fetching financial statements...")
    statements = fetch_financial_statements(normalized_code, market_type, output_dir)
    save_json(statements, output_dir / 'processed_data' / 'financial_statements.json')
    print()

    # Fetch financial indicators
    print("3. Fetching financial indicators...")
    indicators = fetch_financial_indicators(normalized_code, market_type, output_dir)
    if indicators:
        save_json(indicators, output_dir / 'processed_data' / 'financial_indicators.json')
    print()

    print(f"{'='*60}")
    print("Financial data collection completed!")
    print(f"{'='*60}\n")

    return {
        'company_info': company_info,
        'statements': statements,
        'indicators': indicators,
        'market_type': market_type
    }


if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: python fetch_financial_data.py <stock_code> <output_dir>")
        sys.exit(1)

    stock_code = sys.argv[1]
    output_dir = Path(sys.argv[2])

    main(stock_code, output_dir)
