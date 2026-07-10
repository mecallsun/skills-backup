---
name: company-financial-analysis
description: Comprehensive financial analysis system for listed companies (A-shares, Hong Kong stocks, and US stocks). Use when users want to analyze a company's investment value, business model, financial health, or industry position. Supports A-share codes (e.g., 600519, 000001), Hong Kong stock codes (e.g., 00700, 09992), and US tickers (e.g., AAPL, MSFT). Downloads annual reports, quarterly reports, and announcements from official sources. Market sentiment analysis via web_search (news). **Automatically prioritizes the latest quarterly reports** when the current year's annual report is not yet published.
---

# Company Financial Analysis

## Overview

This skill provides comprehensive investment analysis for listed companies, supporting A-shares, Hong Kong stocks, and US stocks. It downloads financial reports (annual + quarterly) and announcements from official sources and extracts data directly from the downloaded files.

**Supported Markets:**
- **A-shares**: Shanghai (6xxxxx), Shenzhen (0xxxxx, 3xxxxx), Beijing (4xxxxx, 8xxxxx)
- **Hong Kong**: HKEx listed stocks (5-digit codes like 00700, 09992)
- **US stocks**: NYSE/NASDAQ tickers (e.g., AAPL, MSFT, GOOGL)

**Data Sources:**
- **A股财报和公告**: 巨潮资讯网 (cninfo.com.cn)
- **港股财报和公告**: 巨潮资讯网港股频道 (cninfo.com.cn/hke)
- **US财报和公告**: SEC EDGAR (sec.gov)
- **US公司财务数据**: edgartools (适用于标准10-K/10-Q)
- **实时行情**: Futu API (`http://39.96.211.212:15000/api/stock/quote/`) + 东方财富备用
- **新闻/市场情绪**: 通过 OpenClaw web_search 工具采集公开新闻

**Key Principles**:
- 财务数据只从下载的财报PDF中提取，不从网上搜索
- **自动优先分析最新季度报表**（当年年报未发布时）
- 市场情绪通过 web_search 采集公开新闻分析（无爬虫依赖）

**⚠️ 美股特别说明**:
- **10-K/10-Q (美国公司如AAPL, MSFT)**: 可用 edgartools 自动提取财务数据
- **20-F/6-K (外国发行人如PDD, BABA, TSM)**: 
  - 6-K 文件仅为 SEC 封面页，不含详细财务数据
  - 季度财务数据需通过 **web_search 搜索业绩公告** 获取
  - 示例搜索: "拼多多 2025 Q3 财报 营收 净利润"

## Workflow

### Step 1: Understand User Request

When a user asks to analyze a company, identify:
- **Stock code**:
  - A-shares: 6-digit code (e.g., 600519, 000001)
  - Hong Kong: 5-digit code (e.g., 00700, 09992)
  - US stocks: 1-5 letter ticker (e.g., AAPL, MSFT)
  - Or company name (e.g., 贵州茅台, 腾讯, Apple)

### Step 2: Run Data Collection

```bash
cd company-financial-analysis/scripts
python3 analyze_company.py <stock_code> [years] [--skip-news]
```

**Examples:**
```bash
# A股分析
python3 analyze_company.py 600519      # 贵州茅台
python3 analyze_company.py 000001      # 平安银行

# 港股分析
python3 analyze_company.py 09992       # 泡泡玛特
python3 analyze_company.py 00700       # 腾讯控股

# 美股分析
python3 analyze_company.py AAPL        # Apple
python3 analyze_company.py MSFT        # Microsoft
```

**What this does:**
1. Creates output directory: `company_analysis_<code>_<date>/`
2. Downloads financial reports (近5年):
   - **年报** (Annual Reports / 10-K)
   - **三季报** (Q3 Reports / 10-Q)
   - **半年报** (Semi-annual Reports)
   - **一季报** (Q1 Reports)
3. Downloads announcements/filings
4. Extracts financial data from downloaded PDF reports
5. Calculates DuPont analysis indicators
6. **Generates news search guidance** for web_search
7. Generates analysis prompt with latest data guidance

### Step 3: Execute News Search

After data collection, execute news search using the generated guidance:

1. Read `processed_data/news_search_prompt.md`
2. Use **web_search** tool to execute the recommended queries
3. Save results to `processed_data/news_analysis.json`

**Example news search:**
```python
# 推荐的搜索查询（从 news_search_prompt.md 获取）
queries = [
    "安踏体育 最新消息 2025",
    "安踏体育 业绩 财报",
    "安踏体育 投资 分析",
]
```

### Step 4: Generate Analysis Report

After data collection and news search, generate the analysis report:
1. Read the analysis prompt from `analysis_prompt.txt`
2. Load news analysis from `processed_data/news_analysis.json` (if available)
3. Load the analysis framework: `references/analysis_framework.md`
4. Load DuPont analysis guide: `references/dupont_analysis.md`
5. **Load DCF methodology: `references/dcf_methodology.md`** (for valuation section)
6. **Load Comps methodology: `references/comps_methodology.md`** (for peer comparison)
7. **Load Competitive analysis: `references/competitive_analysis.md`** (for moat/competitive section)
8. **Prioritize analysis of latest quarterly data** if current year's annual report not available
9. Generate comprehensive analysis combining:
   - Historical annual reports
   - Latest quarterly data
   - News and market sentiment
   - Competitive positioning
   - DCF valuation (Bear/Base/Bull scenarios)
   - Comps peer comparison
10. Save report to `analysis_report.md`

### Step 5: Deliver Results

Present the analysis report to the user, highlighting:
- Executive summary with key investment points
- DuPont analysis (ROE decomposition)
- Competitive positioning and moat assessment
- Peer comparison (Comps)
- DCF valuation with scenarios and sensitivity analysis
- Key findings from announcements
- Market sentiment from news
- Critical risks
- Investment recommendation with price target range

### Step 6: Generate Canvas Visualization (v2)

生成 Obsidian Canvas 可视化文件，便于快速查看和分享。

#### 使用方法

```bash
cd ~/.claude/skills/company-financial-analysis/scripts
python3 generate_canvas.py <公司名称> <股票代码> [报告路径] [输出目录]

# 示例
python3 generate_canvas.py 美团 03690.HK ~/ai/obsidian-notes/projects/美团-03690.HK.md
python3 generate_canvas.py 泡泡玛特 09992.HK
```

#### 输出文件

`~/ai/obsidian-notes/canvases/<公司名称>-综合投资分析.canvas`

#### Canvas 结构 (13个节点)

```
┌─────────────────────────────────────────────────────────────┐
│                      标题 (股票代码+当前价)                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                      核心结论 (评级/目标价/预期收益)             │
└────────┬─────────────────┼─────────────────┬────────────────┘
         │                 │                 │
    ┌────▼────┐       ┌────▼────┐       ┌────▼────┐
    │ 业绩数据 │       │ 估值分析 │       │ 风险概览 │
    └────┬────┘       └────┬────┘       └────┬────┘
         │                 │                 │
    ┌────▼────┐       ┌────▼────┐       ┌────▼────┐
    │ 季度趋势 │       │ 现金流  │       │ 竞品对比 │
    └────┬────┘       └────┬────┘       └────┬────┘
         │                 │                 │
    ┌────▼────┐       ┌────▼────┐       ┌────▼────┐
    │ 投资亮点 │       │ 投资策略 │       │ 关键监控 │
    └────┬────┘       └────┬────┘       └────┬────┘
         │                 │                 │
         └────────┬────────┴────────┬────────┘
             ┌────▼────┐       ┌────▼────┐
             │ 风险详情 │       │ 综合评估 │
             └─────────┘       └─────────┘
```

#### 数据提取方式

**优先级 1: JSON 数据块** (推荐)

在分析报告末尾添加结构化数据：

```markdown
## Canvas 数据

\`\`\`json
{
  "current_price": "97.2港元",
  "target_price": "70-116港元",
  "rating": "★★★☆☆ (中性偏谨慎)",
  "market_cap": "~6000亿港元",
  "pe": "14x",
  "revenue": "3376亿",
  "revenue_yoy": "+22%",
  "net_income": "438亿",
  "net_margin": "13%",
  "roe": "~15.6%",
  "quarters": [
    {"quarter": "2025Q1", "revenue": "866亿", "revenue_yoy": "+18.1%", "net_income": "109亿"}
  ],
  "catalysts": ["龙头地位", "业绩强劲", "现金流充沛"],
  "risks": ["竞争加剧", "增长放缓", "利润承压"],
  "suggestion": "70港元以下可建仓"
}
\`\`\`
```

#### 颜色说明

| 颜色 | 含义 |
|------|------|
| 红色 | 标题 |
| 绿色 | 正面（结论/亮点/策略） |
| 青色 | 中性（财务/估值） |
| 黄色 | 重点数据（季度趋势） |
| 橙色 | 风险警示 |
| 紫色 | 监控指标 |

## Analysis Framework

### DuPont Analysis

**Core Formula:**
```
ROE = 净利率 × 资产周转率 × 权益乘数
```

### DCF Valuation (Three-Scenario)

```
Bear / Base / Bull scenarios
WACC = Risk-free rate + Beta × ERP
EV = Σ(UFCF / (1+WACC)^t) + Terminal Value
Price Target = (EV - Net Debt) / Diluted Shares
```

### Comps Peer Comparison

```
Peer universe (3-8 companies)
Key multiples: EV/EBITDA, P/E, EV/Revenue
Statistical benchmarks: median, Q1, Q3
Target company positioning within the distribution
```

### Analysis Flow

```
下载财报/公告 → 提取财务数据 → 新闻采集
→ 行业分析 → 竞争格局(五力+护城河)
→ 商业模式分析 → 财务分析(杜邦)
→ 可比公司分析(Comps) → DCF估值(三情景)
→ 公告分析 → 市场情绪
→ 风险分析 → 估值交叉验证 → 投资建议
```

## Output Structure

```
company_analysis_<code>_<date>/
├── raw_data/
│   ├── annual_reports/           # 年报PDF (A股/港股) 或 10-K/20-F (美股)
│   ├── quarterly_reports/        # 季报 (6-K 封面页)
│   ├── q3_reports/               # 三季报
│   ├── semi_annual_reports/      # 半年报
│   ├── q1_reports/               # 一季报
│   └── announcements/            # 公告
├── processed_data/
│   ├── company_info.json         # 公司信息 + 实时行情
│   ├── financial_data_with_source.json  # 带来源的财务数据
│   ├── financials_<ticker>.json  # edgartools 提取的财务数据 (美股)
│   ├── quarterly_data.json       # 季度财务数据
│   ├── quarterly_search_prompt.md # 季度数据搜索指引 (20-F公司)
│   ├── metrics_tables.md         # 核心指标表格
│   ├── news_search_prompt.md     # 新闻采集指引
│   ├── news_analysis.json        # 新闻分析结果
│   ├── all_announcements.json    # 所有公告
│   └── important_announcements.json  # 重要公告
├── analysis_prompt.txt           # AI分析提示词
└── analysis_report.md            # 最终分析报告（含Canvas数据块）
```

## US Stock Quarterly Data

### 美股财务数据获取方式

| 公司类型 | 文件格式 | 数据获取方式 |
|----------|----------|--------------|
| 美国公司 (AAPL, MSFT) | 10-K/10-Q | ✅ edgartools 自动提取 |
| 外国发行人 (PDD, BABA, TSM) | 20-F/6-K | ⚠️ web_search 搜索业绩公告 |

### 使用方法

```bash
python3 fetch_us_quarterly.py <ticker> [output_dir]
```

## News Collection

The skill generates news search guidance using OpenClaw's `web_search` tool:
1. No external dependencies - uses built-in web_search
2. Public sources only - news, articles, analyst reports
3. Structured JSON output for analysis integration

## Market Support Details

### A-shares (A股)
- **Source**: 巨潮资讯网 (cninfo.com.cn)
- **Reports**: 年报、季报、公告

### Hong Kong (港股)
- **Source**: 巨潮资讯网港股频道
- **Reports**: 年度业绩、中期业绩、季度运营公告

### US Stocks (美股)
- **Source**: SEC EDGAR (sec.gov)
- **Reports**: 10-K, 10-Q, 8-K, 20-F, 6-K
- **Note**: 行业数据获取受限（akshare 不支持美股）

## Reference Files

Load these as needed during analysis:

| 文件 | 内容 | 何时加载 |
|------|------|---------|
| `references/analysis_framework.md` | 完整分析方法论和质量检查 | 每次分析 |
| `references/dupont_analysis.md` | 杜邦分析详细指南 | 财务分析阶段 |
| `references/dcf_methodology.md` | DCF估值完整方法论（WACC/情景/敏感性）| 估值阶段 |
| `references/comps_methodology.md` | 可比公司分析方法论 | 相对估值阶段 |
| `references/competitive_analysis.md` | 竞争格局分析（五力/护城河/定位矩阵）| 竞争分析阶段 |

## Quick Commands

```bash
# 完整分析（含新闻采集指引）
python3 analyze_company.py 09992

# 跳过新闻采集
python3 analyze_company.py 09992 --skip-news

# 指定年限
python3 analyze_company.py 09992 3

# 快速加载已分析公司
python3 quick_learn.py list
python3 quick_learn.py load 09992.HK

# 生成 Canvas 可视化
python3 generate_canvas.py 泡泡玛特 09992.HK
```

## Dependencies

```bash
pip install httpx pandas PyPDF2
# 或
pip install -r requirements.txt
```

## Changelog

### v3.2 (2026-05)

**分析方法论升级（投行级）**:
- 新增 `references/dcf_methodology.md` — DCF完整方法论
  - WACC计算（含A股/港股/美股市场参数）
  - 三情景分析（Bear/Base/Bull）
  - 敏感性分析（WACC × 永续增长率）
  - 常见错误检查清单
- 新增 `references/comps_methodology.md` — 可比公司分析
  - 5-10法则（运营指标 + 估值倍数）
  - 统计基准（中位数/四分位数）
  - A股/港股特殊考虑（AH溢价、流动性折价）
- 新增 `references/competitive_analysis.md` — 竞争格局分析
  - 2×2竞争定位矩阵
  - 波特五力模型（中国市场适配）
  - 竞争壁垒评分（6维度 1-5分）
- 更新 `references/analysis_framework.md` — 整合新方法论
  - 新增竞争格局分析章节
  - 估值部分扩展为Comps+DCF交叉验证
  - 更新质量检查清单
- 更新 `SKILL.md` Step 4 — 加载新参考文件

**数据源更新**:
- 实时行情优先使用 Futu API (`http://39.96.211.212:15000`)

### v3.1 (2026-02-01)

**Canvas 生成 v2**:
- 参考中海油模板，13个节点完整布局
- 支持 JSON 数据块优先提取
- 改进正则提取，支持多种报告格式

**性能优化**:
- **跳过已有文本文件** - 重复分析从 180s+ 降至 8s

### v3.0 (2026-01-31)
- **移除雪球依赖** - 不再使用雪球爬虫
- **新增新闻采集** - 通过 web_search 工具采集公开新闻
- **美股支持** - SEC EDGAR 数据源

### v2.1
- 添加季度报表自动优先分析
- 添加实时股价获取

### v2.0
- 支持A股和港股
- 杜邦分析框架
- 公告分析
