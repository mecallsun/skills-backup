#!/usr/bin/env python3
"""
新闻采集模块

使用 OpenClaw web_search 工具采集公司相关新闻
替代原有的雪球爬虫

使用方式：
1. 在 Claude Code 中调用此模块生成搜索查询
2. 使用 web_search 工具执行搜索
3. 将结果保存到 processed_data/news_analysis.json
"""
import json
from pathlib import Path
from datetime import datetime
from typing import Optional


def generate_search_queries(company_name: str, stock_code: str) -> list:
    """
    生成新闻搜索查询列表
    
    Args:
        company_name: 公司名称
        stock_code: 股票代码
        
    Returns:
        搜索查询列表
    """
    queries = [
        f"{company_name} 最新消息 2025",
        f"{company_name} 业绩 财报",
        f"{company_name} 投资 分析",
        f"{stock_code} 股票 行情",
        f"{company_name} 行业 竞争",
    ]
    return queries


def create_news_search_prompt(company_name: str, stock_code: str, output_dir: Path) -> str:
    """
    生成新闻采集的提示词
    
    供 Claude Code 使用 web_search 工具时参考
    """
    queries = generate_search_queries(company_name, stock_code)
    
    prompt = f"""## 新闻采集任务

请使用 web_search 工具为 {company_name} ({stock_code}) 采集相关新闻。

### 推荐搜索查询

"""
    for i, q in enumerate(queries, 1):
        prompt += f"{i}. `{q}`\n"
    
    prompt += f"""
### 采集要求

1. 每个查询获取 5-10 条结果
2. 优先关注：
   - 最新业绩公告和解读
   - 行业动态和政策变化
   - 竞争对手动态
   - 分析师观点
   - 风险事件

3. 结果保存格式：
```json
{{
    "company": "{company_name}",
    "stock_code": "{stock_code}",
    "fetch_time": "ISO时间戳",
    "news": [
        {{
            "title": "新闻标题",
            "url": "链接",
            "snippet": "摘要",
            "source": "来源",
            "query": "搜索查询"
        }}
    ],
    "summary": {{
        "total_count": 数量,
        "key_topics": ["主题1", "主题2"],
        "sentiment": "positive/neutral/negative",
        "risk_signals": ["风险1", "风险2"]
    }}
}}
```

4. 保存位置: `{output_dir}/processed_data/news_analysis.json`

### 分析要点

采集完成后，请分析：
- **市场情绪**: 整体看多/看空/中性
- **关键主题**: 近期讨论热点
- **风险信号**: 潜在风险因素
- **投资观点**: 主流分析师观点
"""
    return prompt


def save_news_analysis(news_data: dict, output_dir: Path) -> Path:
    """
    保存新闻分析结果
    
    Args:
        news_data: 新闻数据字典
        output_dir: 输出目录
        
    Returns:
        保存的文件路径
    """
    processed_dir = output_dir / 'processed_data'
    processed_dir.mkdir(parents=True, exist_ok=True)
    
    output_file = processed_dir / 'news_analysis.json'
    
    # 添加时间戳
    news_data['fetch_time'] = datetime.now().isoformat()
    
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(news_data, f, ensure_ascii=False, indent=2)
    
    print(f"  ✓ 新闻分析已保存: {output_file}")
    return output_file


def format_news_for_analysis(news_data: dict) -> str:
    """
    将新闻数据格式化为分析上下文
    
    Args:
        news_data: 新闻数据字典
        
    Returns:
        格式化的 Markdown 文本
    """
    if not news_data or news_data.get('error'):
        return ""
    
    output = f"""## 新闻与市场情绪分析

**数据采集时间**: {news_data.get('fetch_time', 'N/A')}
**新闻总数**: {news_data.get('summary', {}).get('total_count', 0)}

### 市场情绪
- **整体情绪**: {news_data.get('summary', {}).get('sentiment', 'N/A')}

### 关键主题
"""
    
    topics = news_data.get('summary', {}).get('key_topics', [])
    for topic in topics:
        output += f"- {topic}\n"
    
    output += "\n### 风险信号\n"
    risks = news_data.get('summary', {}).get('risk_signals', [])
    for risk in risks:
        output += f"- {risk}\n"
    
    output += "\n### 近期重要新闻\n"
    news_list = news_data.get('news', [])[:10]  # 最多显示10条
    for news in news_list:
        output += f"- [{news.get('title', 'N/A')}]({news.get('url', '#')})\n"
        if news.get('snippet'):
            output += f"  > {news.get('snippet')[:100]}...\n"
    
    return output


def main(company_name: str, stock_code: str, output_dir: Path) -> dict:
    """
    主函数 - 生成新闻采集指引
    
    注意：实际的新闻采集需要通过 Claude Code 的 web_search 工具完成
    此函数仅生成采集指引和保存模板
    
    Args:
        company_name: 公司名称
        stock_code: 股票代码
        output_dir: 输出目录
        
    Returns:
        包含采集指引的字典
    """
    print(f"  生成新闻采集指引...")
    
    # 生成搜索查询
    queries = generate_search_queries(company_name, stock_code)
    
    # 生成采集提示词
    prompt = create_news_search_prompt(company_name, stock_code, output_dir)
    
    # 保存提示词
    processed_dir = output_dir / 'processed_data'
    processed_dir.mkdir(parents=True, exist_ok=True)
    
    prompt_file = processed_dir / 'news_search_prompt.md'
    with open(prompt_file, 'w', encoding='utf-8') as f:
        f.write(prompt)
    
    print(f"  ✓ 新闻采集指引已保存: {prompt_file}")
    
    return {
        'company_name': company_name,
        'stock_code': stock_code,
        'queries': queries,
        'prompt_file': str(prompt_file),
        'note': '请使用 web_search 工具执行搜索，然后调用 save_news_analysis 保存结果'
    }


if __name__ == '__main__':
    import sys
    if len(sys.argv) < 4:
        print("Usage: python fetch_news.py <company_name> <stock_code> <output_dir>")
        sys.exit(1)
    
    company_name = sys.argv[1]
    stock_code = sys.argv[2]
    output_dir = Path(sys.argv[3])
    
    result = main(company_name, stock_code, output_dir)
    print(json.dumps(result, ensure_ascii=False, indent=2))
