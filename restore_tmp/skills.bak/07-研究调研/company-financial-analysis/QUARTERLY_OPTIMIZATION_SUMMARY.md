# Company Financial Analysis Skill - 季度报表优化总结

## 📋 优化目标

优化 company-financial-analysis skill,使其能够:
1. 自动检测并优先分析最新的季度报表
2. 当年年报未发布时,分析最新的三季报/半年报/一季报
3. 确保分析反映公司最新经营状况

## ✅ 已完成的优化

### 1. 修改 `analyze_company.py` 提示词生成函数

**文件**: `/Users/kai/Code/analyseSystem/company-financial-analysis-skill/scripts/analyze_company.py`

**修改内容**:
- 添加智能检测逻辑,自动识别最新季度报表
- 检测优先级: 当年Q3 > 当年半年报 > 当年Q1 > 去年Q3 > 去年半年报 > 去年Q1
- 检查当年年报是否已发布,如未发布则提示分析最新季度数据

**关键代码**:
```python
# 检查最近两年的季度报表（优先当年，其次去年）
q3_dir = output_dir / 'raw_data' / 'q3_reports'
semi_dir = output_dir / 'raw_data' / 'semi_annual_reports'
q1_dir = output_dir / 'raw_data' / 'q1_reports'
annual_dir = output_dir / 'raw_data' / 'annual_reports'

# 检查当年年报是否已发布
current_year_annual = list(annual_dir.glob(f'*{current_year}*.pdf'))

if not current_year_annual:
    # 当年年报未发布，检查最新季度报表
    # 智能检测逻辑...
```

### 2. 更新数据访问指南

**新增内容**:
- 明确指出当年年报未发布的情况
- 提供最新季度报表的目录路径
- 指导优先分析最新季度数据

**示例输出**:
```
**重要**: 2026年年报尚未发布，最新财报为2025年前三季度。
- 三季报目录: `company_analysis_300274.SZ_20260128/raw_data/q3_reports`
- 请优先分析2025年三季报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
```

### 3. 更新分析提示词

**修改内容**:
```markdown
**重要说明**：
- 所有财务数据均来源于公司年报和季度报表
- **优先分析最新的季度报表**（如当年年报未发布，则分析最新的三季报/半年报/一季报）
- 请使用工具按需搜索和读取数据，不要假设数据内容
- 引用数据时注明来源（年份/季度、页码）
- 结合历年年报和最新季报，分析公司最新经营状况和发展趋势
```

### 4. 更新 SKILL.md 文档

**文件**: `/Users/kai/Code/analyseSystem/company-financial-analysis-skill/SKILL.md`

**更新内容**:
- 在 description 中强调"自动优先分析最新季度报表"
- 更新数据下载说明,包含所有季度报表类型
- 添加"自动检测和优先分析最新季度数据"的说明

## 🎯 优化效果

### 优化前
- 只分析年报数据
- 当年年报未发布时,分析停留在去年年报
- 无法反映公司最新经营状况

### 优化后
- 自动检测最新季度报表
- 优先分析最新的三季报/半年报/一季报
- 确保分析反映公司最新经营状况
- 结合历年年报和最新季报进行趋势分析

## 📊 测试验证

### 测试案例: 阳光电源 (300274.SZ)

**测试时间**: 2026-01-28

**测试结果**:
```
✓ 成功检测到2025年三季报
✓ 提示词正确指示优先分析2025年三季报
✓ 提供了三季报目录路径
✓ 指导结合历年年报进行趋势分析
```

**实际输出**:
```
**重要**: 2026年年报尚未发布，最新财报为2025年前三季度。
- 三季报目录: `company_analysis_300274.SZ_20260128/raw_data/q3_reports`
- 请优先分析2025年三季报数据，了解公司最新经营状况
- 结合历年年报数据进行趋势分析
```

## 📁 修改的文件

1. **analyze_company.py** (line 328-400)
   - 添加智能季度报表检测逻辑
   - 更新数据访问指南生成
   - 更新分析提示词

2. **SKILL.md** (line 1-25)
   - 更新 skill description
   - 更新数据下载说明
   - 添加自动检测说明

## 🔍 技术细节

### 季度报表检测逻辑

```python
# 优先级顺序
1. 当年Q3报告
2. 当年半年报
3. 当年Q1报告
4. 去年Q3报告
5. 去年半年报
6. 去年Q1报告

# 检测方法
- 使用 glob 匹配文件名中的年份
- 检查 PDF 文件是否存在
- 生成相应的提示信息
```

### 数据目录结构

```
company_analysis_<code>_<date>/
├── raw_data/
│   ├── annual_reports/          # 年报
│   ├── q3_reports/              # 三季报
│   ├── semi_annual_reports/     # 半年报
│   ├── q1_reports/              # 一季报
│   └── announcements/           # 公告
└── processed_data/
    └── ...
```

## 🎉 总结

成功优化了 company-financial-analysis skill,实现了:

1. ✅ 自动检测最新季度报表
2. ✅ 智能优先级排序
3. ✅ 明确的分析指导
4. ✅ 完整的文档更新
5. ✅ 测试验证通过

现在,当用户分析公司时:
- 系统会自动检测最新的季度报表
- 如果当年年报未发布,会优先分析最新的季度数据
- 确保分析反映公司最新经营状况
- 结合历年年报进行趋势分析

**下次使用时**,只需运行:
```bash
python3 analyze_company.py <stock_code>
```

系统会自动:
1. 下载所有年报和季度报表
2. 检测最新的季度数据
3. 生成包含最新数据指导的分析提示词
4. 确保分析反映公司最新状况

---

**优化完成时间**: 2026-01-28
**优化者**: Claude Code + Kai
**版本**: 2.1.0

Generated with [Claude Code](https://claude.ai/code) via [Happy](https://happy.engineering)
