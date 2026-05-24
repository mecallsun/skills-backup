#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Stock Price Fetcher - 通过 uu_server API 获取实时股票数据
支持A股、港股、美股
"""

import requests
import json
import sys
from typing import Dict, Optional
from decimal import Decimal


class StockPriceFetcher:
    """通过 uu_server API 获取股票实时价格和市值信息"""

    def __init__(self, server_url: str = "http://39.96.211.212:15000"):
        """
        初始化股票价格获取器

        Args:
            server_url: uu_server 服务地址
        """
        self.server_url = server_url
        self.api_base = f"{server_url}/api"

    def get_stock_quote(self, stock_code: str) -> Optional[Dict]:
        """
        获取股票实时行情

        Args:
            stock_code: 股票代码，如 "601127.SH", "00700.HK", "AAPL.US"

        Returns:
            {
                "code": "601127.SH",
                "name": "赛力斯",
                "price": 105.20,
                "prev_close": 104.50,
                "change": 0.70,
                "change_percent": 0.67,
                "market_cap": 183239000000,  # 市值（人民币）
                "update_time": "2026-01-30 13:00:00"
            }
            如果失败返回 None
        """
        try:
            # 标准化股票代码
            normalized_code = self._normalize_code(stock_code)

            # 调用 stock quote API 获取实时行情
            response = requests.get(
                f"{self.api_base}/stock/quote/{normalized_code}",
                timeout=30
            )
            response.raise_for_status()
            data = response.json()

            if data.get("code") != 0:
                print(f"API返回错误: {data.get('message')}", file=sys.stderr)
                return None

            quote_data = data.get("data", {})

            return {
                "code": stock_code,
                "name": quote_data.get("name"),
                "price": quote_data.get("price"),
                "prev_close": quote_data.get("prev_close", 0),
                "change": quote_data.get("change", 0),
                "change_percent": quote_data.get("change_percent", 0),
                "source": quote_data.get("source", "stock_api"),
                "update_time": data.get("timestamp")
            }

        except Exception as e:
            print(f"获取股票 {stock_code} 行情失败: {e}", file=sys.stderr)
            return None

    def calculate_market_cap(self, stock_code: str, total_shares: float) -> Optional[Dict]:
        """
        计算市值（自动获取最新股价）

        Args:
            stock_code: 股票代码
            total_shares: 总股本（亿股）

        Returns:
            {
                "stock_code": "601127.SH",
                "price": 105.01,
                "total_shares": 17.42,
                "market_cap": 183239420000,
                "market_cap_display": "1832.39亿"
            }
            如果失败返回 None
        """
        try:
            # 调用 market-cap API
            normalized_code = self._normalize_code(stock_code)
            response = requests.get(
                f"{self.api_base}/stock/market-cap/{normalized_code}",
                params={"total_shares": total_shares},
                timeout=30
            )
            response.raise_for_status()
            data = response.json()

            if data.get("code") != 0:
                print(f"API返回错误: {data.get('message')}", file=sys.stderr)
                return None

            return data.get("data", {})

        except Exception as e:
            print(f"计算市值失败: {e}", file=sys.stderr)
            return None

    def _normalize_code(self, stock_code: str) -> str:
        """
        标准化股票代码

        Args:
            stock_code: 原始股票代码

        Returns:
            标准化后的代码，如 "601127.SH", "00700.HK", "AAPL.US"
        """
        code = stock_code.upper().strip()

        # 如果已经包含市场后缀，直接返回
        if '.' in code:
            return code

        # A股：6位数字
        if len(code) == 6 and code.isdigit():
            # 沪市：60/68开头
            if code.startswith(('60', '68')):
                return f"{code}.SH"
            # 深市：00/30开头
            elif code.startswith(('00', '30')):
                return f"{code}.SZ"

        # 港股：5位数字
        elif len(code) == 5 and code.isdigit():
            return f"{code}.HK"

        # 美股：字母
        elif code.isalpha():
            return f"{code}.US"

        return code


    def health_check(self) -> bool:
        """
        检查 uu_server 服务是否可用

        Returns:
            True if healthy, False otherwise
        """
        try:
            response = requests.get(
                f"{self.api_base}/health",
                timeout=5
            )
            response.raise_for_status()
            data = response.json()
            return data.get("code") == 0
        except Exception as e:
            print(f"健康检查失败: {e}", file=sys.stderr)
            return False


def main():
    """命令行工具测试"""
    if len(sys.argv) < 2:
        print("用法: python stock_price_fetcher.py <股票代码>")
        print("示例: python stock_price_fetcher.py 601127.SH")
        sys.exit(1)

    stock_code = sys.argv[1]

    fetcher = StockPriceFetcher()

    # 健康检查
    if not fetcher.health_check():
        print("错误: uu_server 服务不可用", file=sys.stderr)
        sys.exit(1)

    # 获取股票行情
    quote = fetcher.get_stock_quote(stock_code)

    if quote:
        print(json.dumps(quote, ensure_ascii=False, indent=2))
    else:
        print(f"错误: 无法获取股票 {stock_code} 的行情", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
