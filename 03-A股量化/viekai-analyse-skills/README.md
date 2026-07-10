# Company Financial Analysis - Claude Code Skill

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Python 3.8+](https://img.shields.io/badge/python-3.8+-blue.svg)](https://www.python.org/downloads/)

一个用于 Claude Code 的专业公司财务分析技能，支持 A 股和港股的全面投资分析。

[English](#english) | [中文](#中文)

---

## 中文

### 📋 简介

这是一个为 Claude Code 开发的专业投资分析技能，能够自动收集公司财务数据、行业信息、公告等，并生成基于杜邦分析框架的专业投资分析报告。

### ✨ 核心功能

- **多市场支持**：A 股（沪深北）+ 港股
- **自动数据收集**：财务报表、财务指标、公司公告、行业数据
- **手动数据输入**：支持手动填写最新财务数据（特别适合港股）
- **杜邦分析框架**：ROE 三因素分解（净利率 × 资产周转率 × 权益乘数）
- **5年对比表格**：自动生成 5 年财务指标对比，智能标注风险
- **商业模式分析**：深入分析公司价值主张、收入模式、竞争壁垒
- **行业分析**：市场空间、竞争格局、公司地位
- **风险评估**：财务风险 + 市场情绪风险
- **投资建议**：基于全面分析的投资建议
- **🆕 知识总结和归档**：自动生成分析摘要并压缩数据（节省70-90%空间）
- **🆕 快速学习**：从之前的分析中快速加载知识，支持公司对比

### 🎯 分析框架

```
行业分析 → 商业模式分析 → 财务分析（杜邦框架）→ 风险分析 → 估值分析 → 投资建议
```

**杜邦分析核心公式**：
```
ROE = 净利率 × 资产周转率 × 权益乘数
```

### 📊 生成报告示例

报告包含以下章节：
1. **执行摘要**：核心投资逻辑、关键指标、风险提示
2. **公司概况**：基本信息、主营业务、发展历程
3. **行业分析**：市场空间、竞争格局、公司地位
4. **商业模式分析**：价值主张、收入模式、竞争壁垒
5. **财务分析**：杜邦分析 + 5年对比表格
6. **风险分析**：财务风险 + 业务风险
7. **估值分析**：PE/PB/PS 估值
8. **投资建议**：操作策略、关注要点

### 🚀 快速开始

#### 安装

1. 确保已安装 Claude Code CLI
2. 克隆本仓库：
```bash
git clone https://github.com/yourusername/company-financial-analysis-skill.git
cd company-financial-analysis-skill
```

3. 安装依赖：
```bash
pip install -r requirements.txt
```

4. 安装 skill 到 Claude Code：
```bash
# 方法 1：复制到 skills 目录
cp -r . ~/.claude/skills/company-financial-analysis

# 方法 2：创建符号链接
ln -s $(pwd) ~/.claude/skills/company-financial-analysis
```

#### 使用方法

**方式一：一键完成分析（推荐）** 🆕

自动完成数据收集、分析、总结、归档：

```bash
cd ~/.claude/skills/company-financial-analysis/scripts
python3 analyze_and_summarize.py 09992  # 泡泡玛特
python3 analyze_and_summarize.py 600519  # 贵州茅台
```

**方式二：快速分析（自动模式）**

适合 A 股公司，使用 AkShare API 自动获取数据：

```bash
# 在 Claude Code 中
帮我分析贵州茅台
# 或
帮我分析 600519
```

**方式三：精确分析（手动模式）**

适合需要最新数据或港股公司：

```bash
# 步骤 1：生成模板
帮我分析腾讯控股（00700）

# 步骤 2：填写数据
# 打开生成的 manual_financial_data.json
# 从官方渠道获取最新财务数据并填写

# 步骤 3：重新分析
帮我重新分析腾讯控股
```

### 📁 输出结构

```
<股票代码>_analysis/
├── raw_data/                      # 原始数据
│   ├── financial_reports/         # 财务报表 CSV
│   ├── announcements/             # 公司公告
│   ├── industry_data/             # 行业数据
│   ├── xueqiu_discussions/        # 雪球讨论模板
│   └── manual_financial_data.json # 手动输入模板 ⭐
├── processed_data/                # 处理后的 JSON 数据
│   ├── company_info.json
│   ├── financial_indicators.json
│   ├── dupont_indicators.json     # 杜邦分析指标 ⭐
│   └── ...
├── analysis_prompt.txt            # AI 分析提示词
└── analysis_report.md             # 最终报告 ⭐⭐⭐
```

### 💡 手动数据输入

对于港股或需要最新数据的情况，系统会生成 JSON 模板：

```json
{
  "stock_code": "00700",
  "years": {
    "2023": {
      "revenue": null,              // 营业收入（亿元）
      "net_profit": null,           // 净利润（亿元）
      "total_assets": null,         // 总资产（亿元）
      "total_equity": null,         // 股东权益（亿元）
      "roe": null,                  // ROE (%)
      // ... 18 个关键指标
    }
  }
}
```

**推荐数据来源**：
- **A 股**：巨潮资讯网 (cninfo.com.cn)、东方财富网
- **港股**：港交所披露易 (hkexnews.hk)、公司官网

### 📊 5年对比表格示例

| 指标 | 2023 | 2022 | 2021 | 2020 | 2019 | 趋势 | 风险提示 |
|------|------|------|------|------|------|------|----------|
| **ROE (%)** | 28.5 | 30.2 | 32.1 | 31.5 | 29.8 | ↓ | - |
| **净利率 (%)** | 52.3 | 51.8 | 52.5 | 51.2 | 50.1 | → | - |
| 资产周转率 | 0.68 | 0.72 | 0.75 | 0.73 | 0.71 | ↓ | - |
| 资产负债率 (%) | 20.1 | 18.5 | 16.8 | 18.9 | 21.9 | ↓ | - |

**自动风险标注**：
- ROE < 15% → ⚠️ 盈利能力不足
- 净利率下降 > 5% → ⚠️ 盈利能力恶化
- 资产负债率 > 70% → ⚠️ 债务风险
- 现金流/净利润 < 80% → ⚠️ 利润质量问题

### 🛠️ 技术架构

**核心脚本**：
- `analyze_company.py`：主流程编排
- `fetch_financial_data.py`：AkShare 数据获取
- `fetch_financial_from_reports.py`：手动数据处理 ⭐
- `fetch_announcements.py`：公司公告获取
- `fetch_industry_data.py`：行业数据获取
- `fetch_xueqiu_discussions.py`：雪球讨论模板
- `utils.py`：工具函数

**参考文档**：
- `references/analysis_framework.md`：投资分析框架
- `references/dupont_analysis.md`：杜邦分析指南
- `assets/report_template.md`：报告模板

### 📦 依赖

```
akshare>=1.11.0
pandas>=1.5.0
requests>=2.28.0
PyPDF2>=3.0.0  # 可选，用于 PDF 解析
```

### 🔧 配置

无需额外配置，开箱即用。

### 🆕 新功能：知识管理

#### 知识总结和归档

分析完成后，自动生成知识摘要并压缩数据：

```bash
cd ~/.claude/skills/company-financial-analysis/scripts

# 手动运行（分析完成后）
python3 summarize_and_archive.py company_analysis_09992.HK_20260126

# 或使用一键命令（推荐）
python3 analyze_and_summarize.py 09992
```

**生成的文件**：
- `knowledge_summary.json` - 结构化摘要
- `KNOWLEDGE_SUMMARY.md` - 可读摘要
- `LEARNING_SUMMARY.md` - 学习摘要（用于Claude上下文）
- `<code>_raw_data_<date>.tar.gz` - 压缩的原始数据（节省70-90%空间）
- `company_index.json` - 全局公司索引

#### 快速学习

从之前的分析中快速加载知识：

```bash
cd ~/.claude/skills/company-financial-analysis/scripts

# 列出所有已分析的公司
python3 quick_learn.py list

# 加载特定公司知识
python3 quick_learn.py load 09992.HK

# 对比多家公司
python3 quick_learn.py compare 09992.HK 00700.HK

# 生成学习摘要
python3 quick_learn.py summary 09992.HK

# 搜索公司
python3 quick_learn.py search 泡泡
```

**详细文档**: 查看 [NEW_FEATURES.md](NEW_FEATURES.md) 了解完整使用指南

### 📝 更新日志

#### v2.2 (2026-01-26) 🆕
- ✅ 新增知识总结和归档功能
- ✅ 新增快速学习功能
- ✅ 支持公司对比分析
- ✅ 自动压缩数据节省空间
- ✅ 全局公司索引

#### v2.1 (2026-01-26)
- ✅ 新增手动数据输入功能
- ✅ 自动计算杜邦分析指标
- ✅ 集成到主分析流程
- ✅ 提供数据来源指引

#### v2.0 (2026-01-25)
- ✅ 数据整理到当前目录
- ✅ 5年杜邦分析对比表格
- ✅ 自动风险标注

#### v1.0 (2026-01-24)
- ✅ 基础分析框架
- ✅ A 股和港股支持
- ✅ 杜邦分析框架

### 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 📄 许可证

MIT License

### 🙏 致谢

- [AkShare](https://github.com/akfamily/akshare)：提供 A 股数据 API
- [Claude Code](https://www.anthropic.com/)：AI 编程助手平台

### 📧 联系方式

如有问题或建议，请提交 Issue。

---

## English

### 📋 Introduction

A professional investment analysis skill for Claude Code that supports comprehensive financial analysis of Chinese listed companies (A-shares and Hong Kong stocks).

### ✨ Key Features

- **Multi-market Support**: A-shares (Shanghai/Shenzhen/Beijing) + Hong Kong stocks
- **Automated Data Collection**: Financial statements, indicators, announcements, industry data
- **Manual Data Input**: Support for manual financial data entry (especially for HK stocks)
- **DuPont Analysis Framework**: ROE decomposition (Net Margin × Asset Turnover × Equity Multiplier)
- **5-Year Comparison Tables**: Auto-generated with intelligent risk flagging
- **Business Model Analysis**: Value proposition, revenue model, competitive moats
- **Industry Analysis**: Market size, competitive landscape, company position
- **Risk Assessment**: Financial risks + market sentiment risks
- **Investment Recommendations**: Based on comprehensive analysis

### 🚀 Quick Start

#### Installation

1. Ensure Claude Code CLI is installed
2. Clone this repository:
```bash
git clone https://github.com/yourusername/company-financial-analysis-skill.git
cd company-financial-analysis-skill
```

3. Install dependencies:
```bash
pip install -r requirements.txt
```

4. Install skill to Claude Code:
```bash
# Method 1: Copy to skills directory
cp -r . ~/.claude/skills/company-financial-analysis

# Method 2: Create symbolic link
ln -s $(pwd) ~/.claude/skills/company-financial-analysis
```

#### Usage

**Method 1: Quick Analysis (Automated)**

For A-share companies using AkShare API:

```bash
# In Claude Code
Analyze Kweichow Moutai
# or
Analyze 600519
```

**Method 2: Precise Analysis (Manual)** ⭐ Recommended

For latest data or HK stocks:

```bash
# Step 1: Generate template
Analyze Tencent (00700)

# Step 2: Fill in data
# Open generated manual_financial_data.json
# Get latest financial data from official sources

# Step 3: Re-analyze
Re-analyze Tencent
```

### 📊 Analysis Framework

```
Industry Analysis → Business Model → Financial Analysis (DuPont) → Risk Analysis → Valuation → Recommendations
```

**DuPont Formula**:
```
ROE = Net Profit Margin × Asset Turnover × Equity Multiplier
```

### 📁 Output Structure

```
<stock_code>_analysis/
├── raw_data/                      # Raw data
│   ├── financial_reports/         # Financial statements (CSV)
│   ├── announcements/             # Company announcements
│   ├── industry_data/             # Industry data
│   └── manual_financial_data.json # Manual input template ⭐
├── processed_data/                # Processed JSON data
│   ├── company_info.json
│   ├── financial_indicators.json
│   ├── dupont_indicators.json     # DuPont indicators ⭐
│   └── ...
├── analysis_prompt.txt            # AI analysis prompt
└── analysis_report.md             # Final report ⭐⭐⭐
```

### 📦 Dependencies

```
akshare>=1.11.0
pandas>=1.5.0
requests>=2.28.0
PyPDF2>=3.0.0  # Optional, for PDF parsing
```

### 🤝 Contributing

Issues and Pull Requests are welcome!

### 📄 License

MIT License

### 🙏 Acknowledgments

- [AkShare](https://github.com/akfamily/akshare): A-share data API
- [Claude Code](https://www.anthropic.com/): AI coding assistant platform

---

**Made with ❤️ for investors and analysts**
