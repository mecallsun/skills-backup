#!/usr/bin/env python3
"""
下载公司公告

数据来源：
- A股：巨潮资讯网 (cninfo.com.cn)
- 港股：港交所披露易 (hkexnews.hk)

下载近5年所有公告用于分析
"""
import sys
from pathlib import Path

from utils import save_json, normalize_stock_code

# 导入报告下载模块
try:
    from fetch_reports import download_cninfo_announcements, download_hkex_announcements
except ImportError:
    print("Warning: fetch_reports module not found")
    download_cninfo_announcements = None
    download_hkex_announcements = None


def filter_important_announcements(announcements: list) -> list:
    """筛选重要公告"""
    important_keywords = [
        # 业绩相关
        '业绩', '年报', '季报', '半年报', '预告', '快报', '分红', '派息',
        # 重大事项
        '重大', '重组', '并购', '收购', '增持', '减持', '股权', '定增',
        # 风险相关
        '诉讼', '仲裁', '处罚', '调查', '风险', '警示', '退市',
        # 管理层
        '董事', '监事', '高管', '变更', '辞职', '任命',
        # 其他
        '回购', '质押', '解除', '冻结', '关联交易',
    ]

    important_announcements = []
    for ann in announcements:
        title = str(ann.get('title', ''))
        if any(keyword in title for keyword in important_keywords):
            important_announcements.append(ann)

    return important_announcements


def main(stock_code: str, output_dir: Path, years: int = 5):
    """
    主函数：下载公司公告

    Args:
        stock_code: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的公告
    """
    normalized_code, market, market_type = normalize_stock_code(stock_code)

    print(f"\n{'='*60}")
    print(f"下载公告")
    print(f"股票代码: {normalized_code}")
    print(f"市场类型: {market_type}")
    print(f"时间范围: 最近 {years} 年")
    print(f"{'='*60}")

    announcements = []

    if market_type == 'HK':
        if download_hkex_announcements:
            announcements = download_hkex_announcements(normalized_code, output_dir, years)
        else:
            print("无法下载港股公告：fetch_reports模块未正确导入")
            print("请手动从港交所披露易下载: https://www1.hkexnews.hk/")
    else:
        if download_cninfo_announcements:
            announcements = download_cninfo_announcements(normalized_code, output_dir, years)
        else:
            print("无法下载A股公告：fetch_reports模块未正确导入")
            print("请手动从巨潮资讯网下载: http://www.cninfo.com.cn/")

    # 筛选重要公告
    if announcements:
        important_announcements = filter_important_announcements(announcements)

        # 保存重要公告
        important_path = output_dir / 'processed_data' / 'important_announcements.json'
        save_json(important_announcements, important_path)

        print(f"\n公告下载完成:")
        print(f"  全部公告: {len(announcements)} 条")
        print(f"  重要公告: {len(important_announcements)} 条")
    else:
        important_announcements = []

    print(f"\n{'='*60}")
    print("公告下载完成")
    print(f"{'='*60}\n")

    return announcements


if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: python fetch_announcements.py <stock_code> <output_dir> [years]")
        print("Example: python fetch_announcements.py 600519 ./output 5")
        sys.exit(1)

    stock_code = sys.argv[1]
    output_dir = Path(sys.argv[2])
    years = int(sys.argv[3]) if len(sys.argv) > 3 else 5

    main(stock_code, output_dir, years)
