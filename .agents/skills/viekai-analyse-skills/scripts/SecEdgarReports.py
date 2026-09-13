#!/usr/bin/env python3
"""
SEC EDGAR 财报下载模块

支持从 SEC EDGAR 下载美股公司的财报：
- 10-K: 年报
- 10-Q: 季报
- 8-K: 重大事项公告
- 20-F: 外国公司年报（如中概股）
- 6-K: 外国公司重大事项公告
"""
import httpx
import json
import time
from pathlib import Path
from datetime import datetime
from typing import Optional


# SEC EDGAR API 基础 URL
EDGAR_API_BASE = "https://data.sec.gov"  # API 数据
EDGAR_ARCHIVES = "https://www.sec.gov"   # 文件下载

# SEC 要求的 User-Agent
HEADERS = {
    "User-Agent": "OpenClaw/1.0 (contact@openclaw.ai)",
    "Accept": "application/json"
}


def get_cik_from_ticker(ticker: str) -> Optional[str]:
    """
    从股票代码获取 CIK（中央索引键）
    
    Args:
        ticker: 股票代码（如 AAPL, PDD）
        
    Returns:
        CIK 编号（10位数字字符串），如找不到则返回 None
    """
    try:
        ticker_upper = ticker.upper()
        
        # 使用公司 ticker-CIK 映射文件（最可靠的方式）
        search_url = "https://www.sec.gov/files/company_tickers.json"
        response = httpx.get(search_url, headers=HEADERS, timeout=30)
        
        if response.status_code == 200:
            companies = response.json()
            
            for key, company in companies.items():
                if company.get('ticker', '').upper() == ticker_upper:
                    cik = company.get('cik_str')
                    if cik:
                        return str(cik).zfill(10)
        
        # 备选：尝试直接访问 submissions API
        # 先尝试用 ticker 作为 CIK 前缀搜索
        url = f"{EDGAR_API_BASE}/submissions/CIK{ticker_upper}.json"
        response = httpx.get(url, headers=HEADERS, timeout=30)
        
        if response.status_code == 200:
            data = response.json()
            cik = data.get('cik')
            if cik:
                return str(cik).zfill(10)
        
        return None
        
    except Exception as e:
        print(f"  获取 CIK 失败: {e}")
        return None


def get_company_filings(cik: str, form_types: list = None, count: int = 40) -> list:
    """
    获取公司的 SEC 提交列表
    
    Args:
        cik: 公司 CIK 编号
        form_types: 筛选的表格类型（如 ['10-K', '10-Q']）
        count: 获取的数量
        
    Returns:
        提交列表
    """
    try:
        url = f"{EDGAR_API_BASE}/submissions/CIK{cik}.json"
        response = httpx.get(url, headers=HEADERS, timeout=30)
        
        if response.status_code != 200:
            print(f"  获取提交列表失败: HTTP {response.status_code}")
            return []
        
        data = response.json()
        filings = data.get('filings', {}).get('recent', {})
        
        if not filings:
            return []
        
        # 整理提交数据
        results = []
        forms = filings.get('form', [])
        dates = filings.get('filingDate', [])
        accessions = filings.get('accessionNumber', [])
        documents = filings.get('primaryDocument', [])
        
        for i in range(min(len(forms), count)):
            form = forms[i]
            
            # 筛选表格类型
            if form_types and form not in form_types:
                continue
            
            results.append({
                'form': form,
                'date': dates[i] if i < len(dates) else '',
                'accession': accessions[i] if i < len(accessions) else '',
                'document': documents[i] if i < len(documents) else '',
                'cik': cik
            })
        
        return results
        
    except Exception as e:
        print(f"  获取提交列表失败: {e}")
        return []


def download_filing(filing: dict, output_dir: Path) -> Optional[Path]:
    """
    下载单个 SEC 提交文件
    
    Args:
        filing: 提交信息字典
        output_dir: 输出目录
        
    Returns:
        下载的文件路径，失败返回 None
    """
    try:
        cik = filing['cik']
        accession = filing['accession'].replace('-', '')
        document = filing['document']
        form = filing['form']
        date = filing['date']
        
        # 构建下载 URL (使用 www.sec.gov 而非 data.sec.gov)
        url = f"{EDGAR_ARCHIVES}/Archives/edgar/data/{int(cik)}/{accession}/{document}"
        
        # 生成文件名
        ext = Path(document).suffix or '.htm'
        filename = f"{form}_{date}_{accession[:10]}{ext}"
        output_path = output_dir / filename
        
        # 检查文件是否已存在
        if output_path.exists():
            print(f"  文件已存在，跳过: {filename}")
            return output_path
        
        # 下载文件
        response = httpx.get(url, headers=HEADERS, timeout=60, follow_redirects=True)
        
        if response.status_code == 200:
            output_path.write_bytes(response.content)
            print(f"  ✓ 下载成功: {filename}")
            return output_path
        else:
            print(f"  下载失败 ({response.status_code}): {filename}")
            return None
            
    except Exception as e:
        print(f"  下载失败: {e}")
        return None


def download_sec_reports(ticker: str, output_dir: Path, years: int = 5) -> dict:
    """
    下载美股公司的 SEC 财报
    
    Args:
        ticker: 股票代码
        output_dir: 输出目录
        years: 下载最近几年的数据
        
    Returns:
        下载结果摘要
    """
    result = {
        'ticker': ticker,
        'cik': None,
        'annual_reports': [],  # 10-K / 20-F
        'quarterly_reports': [],  # 10-Q / 6-K
        'announcements': [],  # 8-K
        'errors': []
    }
    
    print(f"\n从 SEC EDGAR 下载财报: {ticker}")
    print("=" * 60)
    
    # 1. 获取 CIK
    print(f"  查找 CIK...")
    cik = get_cik_from_ticker(ticker)
    
    if not cik:
        error = f"无法找到 {ticker} 的 CIK 编号"
        print(f"  ✗ {error}")
        result['errors'].append(error)
        return result
    
    result['cik'] = cik
    print(f"  ✓ CIK: {cik}")
    
    # 等待一下，避免请求过快
    time.sleep(0.5)
    
    # 2. 获取提交列表
    # 中概股使用 20-F（年报）和 6-K（季度/重大事项）
    # 美国本土公司使用 10-K（年报）和 10-Q（季报）
    form_types = ['10-K', '10-Q', '8-K', '20-F', '6-K']
    
    print(f"  获取提交列表...")
    filings = get_company_filings(cik, form_types, count=years * 10)
    
    if not filings:
        error = "未找到任何 SEC 提交"
        print(f"  ✗ {error}")
        result['errors'].append(error)
        return result
    
    print(f"  ✓ 找到 {len(filings)} 个提交")
    
    # 3. 创建输出目录
    annual_dir = output_dir / 'annual_reports'
    quarterly_dir = output_dir / 'quarterly_reports'
    announcements_dir = output_dir / 'announcements'
    
    annual_dir.mkdir(parents=True, exist_ok=True)
    quarterly_dir.mkdir(parents=True, exist_ok=True)
    announcements_dir.mkdir(parents=True, exist_ok=True)
    
    # 4. 下载文件
    print(f"\n下载年报 (10-K/20-F)...")
    annual_forms = [f for f in filings if f['form'] in ['10-K', '20-F']]
    for filing in annual_forms[:years]:
        path = download_filing(filing, annual_dir)
        if path:
            result['annual_reports'].append({
                'path': str(path),
                'form': filing['form'],
                'date': filing['date']
            })
        time.sleep(0.3)  # 限速
    
    print(f"\n下载季报 (10-Q/6-K)...")
    quarterly_forms = [f for f in filings if f['form'] in ['10-Q', '6-K']]
    for filing in quarterly_forms[:years * 4]:
        path = download_filing(filing, quarterly_dir)
        if path:
            result['quarterly_reports'].append({
                'path': str(path),
                'form': filing['form'],
                'date': filing['date']
            })
        time.sleep(0.3)
    
    print(f"\n下载重大公告 (8-K)...")
    announcements = [f for f in filings if f['form'] == '8-K']
    for filing in announcements[:20]:  # 最多20个
        path = download_filing(filing, announcements_dir)
        if path:
            result['announcements'].append({
                'path': str(path),
                'form': filing['form'],
                'date': filing['date']
            })
        time.sleep(0.3)
    
    # 5. 打印摘要
    print(f"\n" + "=" * 60)
    print(f"下载完成:")
    print(f"  年报: {len(result['annual_reports'])} 份")
    print(f"  季报: {len(result['quarterly_reports'])} 份")
    print(f"  公告: {len(result['announcements'])} 份")
    
    return result


def main(ticker: str, output_dir: Path, years: int = 5) -> dict:
    """
    主函数
    """
    return download_sec_reports(ticker, output_dir, years)


if __name__ == '__main__':
    import sys
    
    if len(sys.argv) < 3:
        print("Usage: python SecEdgarReports.py <ticker> <output_dir> [years]")
        print("Example: python SecEdgarReports.py PDD ./pdd_data 5")
        sys.exit(1)
    
    ticker = sys.argv[1]
    output_dir = Path(sys.argv[2])
    years = int(sys.argv[3]) if len(sys.argv) > 3 else 5
    
    result = main(ticker, output_dir, years)
    print(json.dumps(result, ensure_ascii=False, indent=2))
