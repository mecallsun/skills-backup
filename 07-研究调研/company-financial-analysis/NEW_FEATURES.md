# 新功能使用指南

## 概述

公司财务分析技能新增了两个强大功能：

1. **知识总结和归档** - 自动生成分析摘要并压缩数据
2. **快速学习** - 从之前的分析中快速加载知识

## 功能1: 知识总结和归档

### 使用场景

分析完成后，自动：
- 生成结构化的知识摘要
- 压缩原始数据文件（节省70-90%空间）
- 创建全局索引便于检索

### 使用方法

#### 方法1: 手动运行（分析完成后）

```bash
cd company-financial-analysis/scripts
python3 summarize_and_archive.py <analysis_directory>
```

**示例**:
```bash
python3 summarize_and_archive.py company_analysis_09992.HK_20260126
```

**选项**:
- 默认: 保留原始文件 + 生成压缩包
- `--no-keep-original`: 删除原始文件，仅保留压缩包（节省空间）

```bash
python3 summarize_and_archive.py company_analysis_09992.HK_20260126 --no-keep-original
```

#### 方法2: 一键完成（推荐）

使用 `analyze_and_summarize.py` 一次性完成分析、总结、归档：

```bash
cd company-financial-analysis/scripts
python3 analyze_and_summarize.py <stock_code>
```

**示例**:
```bash
# 分析泡泡玛特，保留原始文件
python3 analyze_and_summarize.py 09992

# 分析贵州茅台，删除原始文件节省空间
python3 analyze_and_summarize.py 600519 --no-keep-original

# 仅分析，跳过归档
python3 analyze_and_summarize.py 00700 --skip-archive
```

### 生成的文件

运行后会生成以下文件：

```
company_analysis_09992.HK_20260126/
├── knowledge_summary.json          # 结构化摘要（JSON格式）
├── KNOWLEDGE_SUMMARY.md            # 可读摘要（Markdown格式）
├── LEARNING_SUMMARY.md             # 学习摘要（用于Claude上下文）
└── 09992.HK_raw_data_20260126.tar.gz  # 压缩的原始数据

scripts/
└── company_index.json              # 全局公司索引
```

### 知识摘要内容

`knowledge_summary.json` 包含：
- **基本信息**: 公司代码、名称、市场类型
- **财务数据摘要**: 覆盖年份、关键指标
- **报告摘要**: 核心投资逻辑、章节列表
- **公告摘要**: 公告数量、日期范围
- **文件清单**: 所有文件的详细列表

## 功能2: 快速学习

### 使用场景

- 快速回顾之前分析过的公司
- 对比多家公司
- 为Claude生成优化的学习摘要
- 搜索已分析的公司

### 使用方法

#### 1. 列出所有已分析的公司

```bash
cd company-financial-analysis/scripts
python3 quick_learn.py list
```

**输出示例**:
```
================================================================================
已分析公司列表 (共 3 家)
================================================================================

09992.HK        泡泡玛特              HK         2026-01-26
600519          贵州茅台              A-share    2026-01-25
00700.HK        腾讯控股              HK         2026-01-24
```

#### 2. 加载特定公司的知识

```bash
python3 quick_learn.py load <company_code>
```

**示例**:
```bash
python3 quick_learn.py load 09992.HK
```

**输出**:
- 基本信息
- 财务数据摘要
- 核心投资逻辑
- 公告概况

#### 3. 对比多家公司

```bash
python3 quick_learn.py compare <code1> <code2> [code3...]
```

**示例**:
```bash
# 对比泡泡玛特和腾讯
python3 quick_learn.py compare 09992.HK 00700.HK

# 对比三家公司
python3 quick_learn.py compare 09992.HK 00700.HK 600519
```

**对比内容**:
- 基本信息对比
- 财务指标对比（最新年份）
- 核心投资逻辑对比
- 数据完整性对比

#### 4. 生成学习摘要

为Claude生成优化的学习摘要（精简版，适合快速加载到上下文）：

```bash
python3 quick_learn.py summary <company_code>
```

**示例**:
```bash
python3 quick_learn.py summary 09992.HK
```

生成 `LEARNING_SUMMARY.md`，包含：
- 核心投资逻辑
- 关键财务数据表格
- 报告章节列表
- 公告概况
- 数据文件位置

#### 5. 搜索公司

```bash
python3 quick_learn.py search <keyword>
```

**示例**:
```bash
python3 quick_learn.py search 泡泡
python3 quick_learn.py search 09992
python3 quick_learn.py search 茅台
```

## 工作流程示例

### 场景1: 首次分析公司

```bash
cd company-financial-analysis/scripts

# 一键完成分析、总结、归档
python3 analyze_and_summarize.py 09992

# 查看生成的摘要
cat company_analysis_09992.HK_*/KNOWLEDGE_SUMMARY.md

# 生成学习摘要（用于后续快速加载）
python3 quick_learn.py summary 09992.HK
```

### 场景2: 快速回顾之前的分析

```bash
cd company-financial-analysis/scripts

# 列出所有公司
python3 quick_learn.py list

# 加载特定公司知识
python3 quick_learn.py load 09992.HK

# 查看完整报告
cat company_analysis_09992.HK_*/analysis_report.md
```

### 场景3: 对比多家公司

```bash
cd company-financial-analysis/scripts

# 对比泡泡玛特和腾讯
python3 quick_learn.py compare 09992.HK 00700.HK

# 查看对比结果（包含财务指标对比表）
```

### 场景4: 为Claude准备上下文

```bash
cd company-financial-analysis/scripts

# 生成学习摘要
python3 quick_learn.py summary 09992.HK

# 将LEARNING_SUMMARY.md内容提供给Claude
# Claude可以快速了解公司情况，无需重新分析
```

## 在Claude Code中使用

### 首次分析

当用户要求分析公司时：

```bash
cd company-financial-analysis/scripts
python3 analyze_and_summarize.py 09992
```

这会自动完成：
1. 下载财报和公告
2. 提取财务数据
3. 生成分析报告
4. 创建知识摘要
5. 压缩原始数据
6. 更新全局索引

### 后续查询

当用户询问之前分析过的公司时：

```bash
cd company-financial-analysis/scripts

# 快速加载知识
python3 quick_learn.py load 09992.HK

# 或生成学习摘要
python3 quick_learn.py summary 09992.HK

# 然后读取LEARNING_SUMMARY.md
cat company_analysis_09992.HK_*/LEARNING_SUMMARY.md
```

这样可以：
- 避免重复下载数据
- 快速回答用户问题
- 节省时间和带宽

## 数据管理

### 空间优化

如果磁盘空间有限，使用 `--no-keep-original` 选项：

```bash
python3 summarize_and_archive.py company_analysis_09992.HK_20260126 --no-keep-original
```

这会：
- 压缩原始数据到 `.tar.gz`（通常节省70-90%空间）
- 删除原始文件
- 保留所有处理后的数据和报告

### 恢复原始数据

如果需要恢复原始数据：

```bash
cd company_analysis_09992.HK_20260126
tar -xzf 09992.HK_raw_data_20260126.tar.gz
```

### 清理旧分析

定期清理不需要的分析目录：

```bash
# 列出所有分析目录
ls -lh company_analysis_*

# 删除旧的分析（保留压缩包和摘要）
rm -rf company_analysis_09992.HK_20260126/raw_data
```

## 最佳实践

1. **首次分析**: 使用 `analyze_and_summarize.py` 一键完成
2. **后续查询**: 使用 `quick_learn.py` 快速加载
3. **空间管理**: 对于不常用的公司，使用 `--no-keep-original`
4. **定期维护**: 每月检查 `company_index.json`，清理不需要的分析
5. **备份**: 定期备份 `company_index.json` 和各公司的 `knowledge_summary.json`

## 故障排除

### 问题1: 找不到公司索引

**症状**: `quick_learn.py list` 显示"暂无已分析的公司"

**解决**:
```bash
# 手动运行总结工具
python3 summarize_and_archive.py company_analysis_09992.HK_20260126
```

### 问题2: 压缩失败

**症状**: 压缩过程出错

**解决**:
- 检查磁盘空间
- 确保有写入权限
- 使用 `--skip-archive` 跳过压缩

### 问题3: 学习摘要内容不完整

**症状**: `LEARNING_SUMMARY.md` 缺少财务数据

**解决**:
- 确保 `knowledge_summary.json` 存在且完整
- 重新运行 `summarize_and_archive.py`
- 检查原始分析是否成功完成

## 技术细节

### 知识摘要结构

```json
{
  "basic_info": {
    "company_code": "09992.HK",
    "company_name": "泡泡玛特",
    "market": "HK"
  },
  "financial_summary": {
    "years_covered": [2020, 2021, 2022, 2023, 2024],
    "key_metrics": {
      "2024": {
        "营业收入": "130.4亿元",
        "净利润": "33.1亿元",
        "净利率": "25.4%"
      }
    }
  },
  "report_summary": {
    "key_investment_points": [
      "爆发式增长",
      "全球化加速",
      "盈利能力优异"
    ]
  }
}
```

### 全局索引结构

```json
{
  "09992.HK": {
    "company_code": "09992.HK",
    "company_name": "泡泡玛特",
    "market": "HK",
    "analysis_date": "2026-01-26T22:00:00",
    "analysis_dir": "company_analysis_09992.HK_20260126",
    "summary_file": "company_analysis_09992.HK_20260126/knowledge_summary.json",
    "report_file": "company_analysis_09992.HK_20260126/analysis_report.md"
  }
}
```

## 未来改进

计划中的功能：
- [ ] 自动更新分析（检测新财报）
- [ ] 多公司组合分析
- [ ] 行业对比分析
- [ ] 导出为Excel/PDF
- [ ] Web界面查看

## 反馈

如有问题或建议，请在GitHub提交Issue。
