#!/usr/bin/env python3
"""
Fetch industry data and peer comparison
"""
import sys
from pathlib import Path
import pandas as pd

try:
    import akshare as ak
except ImportError:
    ak = None

from utils import save_json, normalize_stock_code


def fetch_industry_info(industry_name: str, output_dir: Path) -> dict:
    """Fetch industry information and peer companies"""
    results = {
        'industry_name': industry_name,
        'peer_companies': [],
        'industry_index': None
    }

    try:
        print(f"Fetching industry data for: {industry_name}")

        # Get all stocks and filter by industry
        print("  - Fetching stock list...")
        stock_list = ak.stock_info_a_code_name()

        # Get industry classification
        print("  - Fetching industry classification...")
        industry_data = ak.stock_board_industry_name_em()

        # Find stocks in the same industry
        peer_stocks = []
        for _, row in stock_list.iterrows():
            code = row['code']
            try:
                stock_info = ak.stock_individual_info_em(symbol=code)
                info_dict = dict(zip(stock_info['item'], stock_info['value']))
                stock_industry = info_dict.get('行业', '')

                if industry_name in stock_industry or stock_industry in industry_name:
                    peer_stocks.append({
                        'code': code,
                        'name': row['name'],
                        'industry': stock_industry
                    })

                    if len(peer_stocks) >= 20:  # Limit to top 20 peers
                        break
            except:
                continue

        results['peer_companies'] = peer_stocks
        print(f"  ✓ Found {len(peer_stocks)} peer companies")

        # Save results
        save_json(results, output_dir / 'processed_data' / 'industry_info.json')

    except Exception as e:
        print(f"  ✗ Failed to fetch industry data: {e}")

    return results


def fetch_peer_financials(peer_codes: list, output_dir: Path) -> list:
    """Fetch financial data for peer companies"""
    peer_financials = []

    print("Fetching peer company financials...")
    for peer in peer_codes[:10]:  # Limit to top 10 for performance
        try:
            code = peer['code']
            indicators = ak.stock_financial_analysis_indicator(symbol=code)

            if not indicators.empty:
                latest = indicators.iloc[0].to_dict()
                peer_financials.append({
                    'code': code,
                    'name': peer['name'],
                    'financials': latest
                })
                print(f"  ✓ {peer['name']}")
        except Exception as e:
            print(f"  ✗ {peer['name']}: {e}")
            continue

    if peer_financials:
        save_json(peer_financials, output_dir / 'processed_data' / 'peer_financials.json')

    return peer_financials


def main(stock_code: str, industry_name: str, output_dir: Path):
    """Main function"""
    if ak is None:
        print("  ⚠ akshare 未安装，跳过行业数据（已禁用）")
        (output_dir / 'processed_data').mkdir(parents=True, exist_ok=True)
        save_json({'industry_name': industry_name, 'note': 'akshare disabled'}, output_dir / 'processed_data' / 'industry_info.json')
        return
    normalized_code, market, market_type = normalize_stock_code(stock_code)

    print(f"\n{'='*60}")
    print(f"Fetching Industry Data")
    print(f"{'='*60}\n")

    if market_type == 'US':
        print("Note: US stock industry data not supported via akshare, skipping...")
        industry_info = {'industry_name': industry_name, 'peer_companies': [], 'note': 'US stocks not supported'}
        (output_dir / 'processed_data').mkdir(parents=True, exist_ok=True)
        save_json(industry_info, output_dir / 'processed_data' / 'industry_info.json')
        peer_financials = []
    elif market_type == 'HK':
        print("Note: Hong Kong stock industry data requires manual research")
        industry_info = {'industry_name': industry_name, 'peer_companies': [], 'note': 'Manual research required'}
        save_json(industry_info, output_dir / 'processed_data' / 'industry_info.json')
        peer_financials = []
    else:
        # Fetch industry info
        industry_info = fetch_industry_info(industry_name, output_dir)

        # Fetch peer financials
        if industry_info['peer_companies']:
            peer_financials = fetch_peer_financials(industry_info['peer_companies'], output_dir)
        else:
            peer_financials = []

    print(f"\n{'='*60}")
    print("Industry data collection completed!")
    print(f"{'='*60}\n")

    return {
        'industry_info': industry_info,
        'peer_financials': peer_financials
    }


if __name__ == '__main__':
    if len(sys.argv) < 4:
        print("Usage: python fetch_industry_data.py <stock_code> <industry_name> <output_dir>")
        sys.exit(1)

    stock_code = sys.argv[1]
    industry_name = sys.argv[2]
    output_dir = Path(sys.argv[3])

    main(stock_code, industry_name, output_dir)