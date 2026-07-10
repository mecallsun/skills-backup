#!/usr/bin/env python3
"""
Utility functions for company financial analysis
"""
import os
import json
import re
from datetime import datetime
from pathlib import Path


def create_output_directory(stock_code: str) -> Path:
    """Create output directory for analysis results"""
    timestamp = datetime.now().strftime("%Y%m%d")
    dir_name = f"company_analysis_{stock_code}_{timestamp}"
    output_dir = Path(dir_name)

    # Create subdirectories
    (output_dir / "raw_data" / "financial_reports").mkdir(parents=True, exist_ok=True)
    (output_dir / "raw_data" / "announcements").mkdir(parents=True, exist_ok=True)
    (output_dir / "raw_data" / "xueqiu_discussions").mkdir(parents=True, exist_ok=True)
    (output_dir / "raw_data" / "industry_data").mkdir(parents=True, exist_ok=True)
    (output_dir / "processed_data").mkdir(parents=True, exist_ok=True)

    return output_dir


def save_json(data: dict, filepath: Path):
    """Save data as JSON file"""
    with open(filepath, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def load_json(filepath: Path) -> dict:
    """Load JSON file"""
    with open(filepath, 'r', encoding='utf-8') as f:
        return json.load(f)


def normalize_stock_code(code: str) -> tuple:
    """
    Normalize stock code to standard format
    Returns: (normalized_code, market, market_type)
    Examples:
        '600519' -> ('600519.SH', 'SH', 'A')
        '000001' -> ('000001.SZ', 'SZ', 'A')
        '00700' -> ('00700.HK', 'HK', 'HK')
        '600519.SH' -> ('600519.SH', 'SH', 'A')
        'AAPL' -> ('AAPL', 'US', 'US')
        'PDD' -> ('PDD', 'US', 'US')
    """
    code = code.strip().upper()

    # Already has market suffix
    if '.' in code:
        parts = code.split('.')
        market = parts[1]
        if market == 'HK':
            market_type = 'HK'
        elif market == 'US':
            market_type = 'US'
        else:
            market_type = 'A'
        return (code, market, market_type)

    # US stocks: 1-5 letters only (no digits)
    if code.isalpha() and 1 <= len(code) <= 5:
        return (code, 'US', 'US')

    # Hong Kong stocks (5 digits, typically starting with 0)
    if len(code) == 5 and code.isdigit():
        return (f"{code}.HK", 'HK', 'HK')

    # A-share stocks
    # Determine market by code prefix
    if code.startswith('6'):
        return (f"{code}.SH", 'SH', 'A')  # Shanghai
    elif code.startswith(('0', '3')):
        return (f"{code}.SZ", 'SZ', 'A')  # Shenzhen
    elif code.startswith('8') or code.startswith('4'):
        return (f"{code}.BJ", 'BJ', 'A')  # Beijing
    else:
        # Default to Shanghai for numeric codes
        if code.isdigit():
            return (f"{code}.SH", 'SH', 'A')
        # Otherwise treat as US stock
        return (code, 'US', 'US')


def format_number(num, decimal_places=2):
    """Format number with thousands separator"""
    if num is None:
        return "N/A"
    try:
        return f"{float(num):,.{decimal_places}f}"
    except (ValueError, TypeError):
        return str(num)


def calculate_cagr(start_value, end_value, years):
    """Calculate Compound Annual Growth Rate"""
    if start_value <= 0 or end_value <= 0 or years <= 0:
        return None
    try:
        return ((end_value / start_value) ** (1 / years) - 1) * 100
    except:
        return None


def safe_divide(numerator, denominator, default=None):
    """Safe division with default value"""
    try:
        if denominator == 0:
            return default
        return numerator / denominator
    except:
        return default
