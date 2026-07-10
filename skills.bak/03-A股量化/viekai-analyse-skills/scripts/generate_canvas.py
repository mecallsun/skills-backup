#!/usr/bin/env python3
"""
生成投资分析 Canvas 可视化文件 (v2 - 参考中海油模板)

基于 JSON Canvas 规范: https://jsoncanvas.org/spec/1.0/
输出到 Obsidian canvases 目录

特性:
- 16+ 节点，信息密度高
- 支持从报告 JSON 数据块提取
- 行业特定模块（互联网/能源/消费等）
"""

import json
import sys
import os
import re
from pathlib import Path
from datetime import datetime


class InvestmentCanvasGenerator:
    """投资分析 Canvas 生成器 v2"""
    
    # 颜色: 1=红, 2=橙, 3=黄, 4=绿, 5=青, 6=紫
    COLORS = {
        'title': '1',      # 红色 - 标题
        'conclusion': '4', # 绿色 - 核心结论
        'positive': '4',   # 绿色 - 正面
        'neutral': '5',    # 青色 - 中性
        'warning': '2',    # 橙色 - 警示
        'risk': '2',       # 橙色 - 风险
        'highlight': '3',  # 黄色 - 重点数据
        'monitor': '6',    # 紫色 - 监控
    }
    
    def __init__(self, company_name, stock_code):
        self.company_name = company_name
        self.stock_code = stock_code
        self.nodes = []
        self.edges = []
        self.data = {}
    
    def add_node(self, node_id, text, x, y, width, height, color='neutral'):
        """添加节点"""
        self.nodes.append({
            "id": node_id,
            "type": "text",
            "text": text,
            "x": x,
            "y": y,
            "width": width,
            "height": height,
            "color": self.COLORS.get(color, '5')
        })
        return node_id
    
    def add_edge(self, from_node, to_node, from_side='bottom', to_side='top'):
        """添加连接线"""
        self.edges.append({
            "id": f"e{len(self.edges)+1}",
            "fromNode": from_node,
            "fromSide": from_side,
            "toNode": to_node,
            "toSide": to_side,
        })
    
    def load_data(self, report_path=None, data_dir=None):
        """从多个来源加载数据"""
        self.data = {
            # 基础
            "current_price": "-",
            "target_price": "-",
            "rating": "-",
            "expected_return": "-",
            "market_cap": "-",
            # 估值
            "pe": "-",
            "pe_forward": "-",
            "pb": "-",
            "ps": "-",
            "dividend_yield": "-",
            # 业绩
            "revenue": "-",
            "revenue_yoy": "-",
            "net_income": "-",
            "net_income_yoy": "-",
            "gross_margin": "-",
            "net_margin": "-",
            "roe": "-",
            # 现金流
            "operating_cashflow": "-",
            "cash_ratio": "-",
            # 季度
            "quarters": [],
            # 定性
            "catalysts": [],
            "risks": [],
            "suggestion": "-",
            # 竞品对比
            "competitors": [],
            # 监控指标
            "monitor_metrics": [],
        }
        
        # 从报告加载
        if report_path and os.path.exists(report_path):
            self._load_from_report(report_path)
        
        return self.data
    
    def _load_from_report(self, report_path):
        """从分析报告提取数据（改进版正则提取）"""
        try:
            with open(report_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            print(f"读取报告失败: {e}")
            return
        
        # 1. 优先从 JSON 数据块提取
        json_match = re.search(r'```json\s*\n(\{.*?\})\s*\n```', content, re.DOTALL)
        if json_match:
            try:
                json_data = json.loads(json_match.group(1))
                print(f"从 JSON 数据块加载 {len(json_data)} 个字段")
                for key, value in json_data.items():
                    if value and value not in ['-', None, '']:
                        self.data[key] = value
                return
            except json.JSONDecodeError as e:
                print(f"JSON 解析失败，使用正则提取: {e}")
        
        # 2. 正则提取（改进版）
        print("使用正则模式提取数据...")
        extracted = 0
        
        # 基础信息提取模式
        patterns = {
            "current_price": [
                r'当前股?价[：:]\s*\*?\*?([0-9.]+\s*[港美元]+)',
                r'当前价[：:\s]+([0-9.]+\s*[港美元]+)',
                r'股价[：:\s]+([0-9.]+\s*[港美元]+)',
                r'\|\s*当前价\s*\|\s*([^|]+)\s*\|',
            ],
            "target_price": [
                r'目标价[：:]\s*\*?\*?([^\n*]+)',
                r'目标价区间[：:]\s*([^\n]+)',
                r'合理估值[区间]*[：:]\s*([^\n]+)',
            ],
            "rating": [
                r'评级[：:]\s*\*?\*?([★☆⭐]+[^*\n]*)',
                r'综合评级[：:]\s*\*?\*?([^*\n]+)',
                r'投资评级[：:]\s*\*?\*?([^*\n]+)',
                r'\*\*评级\*\*[：:]\s*([^*\n]+)',
            ],
            "expected_return": [
                r'预期收益[：:]\s*([^\n]+)',
                r'潜在回报[：:]\s*([^\n]+)',
                r'上涨空间[：:]\s*([^\n]+)',
            ],
            "market_cap": [
                r'市值[：:\s|]+[约~]?([0-9,.]+\s*[亿万]*[港美元]*)',
                r'\|\s*市值\s*\|\s*([^|]+)\s*\|',
            ],
            "pe": [
                r'PE\s*[\(（]?TTM[\)）]?[：:\s|]+[约~]?([0-9.]+)x?',
                r'PE[：:\s|]+[约~]?([0-9.]+)x?倍?',
                r'\|\s*PE\s*\|\s*([^|]+)\s*\|',
            ],
            "pe_forward": [
                r'PE\s*[\(（]?Forward[\)）]?[：:\s|]+([^\n|]+)',
                r'PE\s*[\(（]?2025E?[\)）]?[：:\s|]+([0-9.]+)',
            ],
            "pb": [
                r'PB[：:\s|]+[约~]?([0-9.]+)x?倍?',
                r'\|\s*PB\s*\|\s*([^|]+)\s*\|',
            ],
            "ps": [
                r'PS[：:\s|]+[约~]?([0-9.]+)x?倍?',
            ],
            "dividend_yield": [
                r'股息率[：:\s|]+([0-9.]+%?)',
                r'分红率[：:\s|]+([0-9.]+%?)',
            ],
            "revenue": [
                r'营收[：:\s|]+([0-9,.]+\s*[亿万]*)',
                r'营业收入[：:\s|]+([0-9,.]+\s*[亿万]*)',
                r'\|\s*营收\s*\|\s*([^|]+)\s*\|',
            ],
            "revenue_yoy": [
                r'营收[^|]*同比[：:\s|]+([+-]?[0-9.]+%?)',
                r'收入增[长速][：:\s|]+([+-]?[0-9.]+%?)',
                r'\|\s*同比\s*\|\s*([^|]+)\s*\|',
            ],
            "net_income": [
                r'净利润[：:\s|]+([0-9,.]+\s*[亿万]*)',
                r'归母净利润[：:\s|]+([0-9,.]+\s*[亿万]*)',
                r'经调整净利[润额]*[：:\s|]+([0-9,.]+\s*[亿万]*)',
            ],
            "net_income_yoy": [
                r'净利润[^|]*同比[：:\s|]+([+-]?[0-9.]+%?)',
                r'利润增[长速][：:\s|]+([+-]?[0-9.]+%?)',
            ],
            "gross_margin": [
                r'毛利率[：:\s|]+[约~]?([0-9.]+%?)',
            ],
            "net_margin": [
                r'净利率[：:\s|]+[约~]?([0-9.]+%?)',
                r'Non-GAAP净利率[：:\s|]+([0-9.]+%?)',
            ],
            "roe": [
                r'ROE[：:\s|]+[约~]?([0-9.]+%?)',
                r'净资产收益率[：:\s|]+([0-9.]+%?)',
            ],
            "operating_cashflow": [
                r'经营现金流[：:\s|]+([0-9,.]+\s*[亿万]*)',
                r'经营活动现金流[：:\s|]+([0-9,.]+\s*[亿万]*)',
            ],
            "cash_ratio": [
                r'现金[储备余额]*[：:\s|]+([0-9,.]+\s*[亿万]*)',
            ],
            "suggestion": [
                r'操作建议[：:]\s*([^\n]+)',
                r'投资建议[：:]\s*([^\n]+)',
                r'建议[：:]\s*([^\n]{10,80})',
            ],
        }
        
        for key, pattern_list in patterns.items():
            if self.data.get(key) not in ['-', None, '']:
                continue
            for pattern in pattern_list:
                match = re.search(pattern, content, re.IGNORECASE)
                if match:
                    value = match.group(1).strip().strip('*').strip()
                    if value and value != '-' and len(value) < 100:
                        self.data[key] = value
                        extracted += 1
                        break
        
        # 3. 提取投资亮点/催化剂
        catalyst_patterns = [
            r'核心投资逻辑.*?\n(.*?)(?=\n##|\n###|\n---|\Z)',
            r'投资亮点.*?\n(.*?)(?=\n##|\n###|\n---|\Z)',
            r'核心逻辑.*?\n(.*?)(?=\n##|\n###|\n---|\Z)',
        ]
        for pattern in catalyst_patterns:
            match = re.search(pattern, content, re.DOTALL)
            if match:
                section = match.group(1)
                items = re.findall(r'\d+\.\s*\*\*([^*]+)\*\*', section)
                if not items:
                    items = re.findall(r'[-•✅]\s*\*?\*?([^*\n]{5,60})\*?\*?', section)
                if items:
                    self.data["catalysts"] = [i.strip() for i in items[:6]]
                    extracted += 1
                    break
        
        # 4. 提取风险
        risk_patterns = [
            r'风险分析.*?\n(.*?)(?=\n##|\n---|\Z)',
            r'风险提示.*?\n(.*?)(?=\n##|\n---|\Z)',
            r'主要风险.*?\n(.*?)(?=\n##|\n---|\Z)',
        ]
        for pattern in risk_patterns:
            match = re.search(pattern, content, re.DOTALL)
            if match:
                section = match.group(1)
                items = re.findall(r'\d+\.\s*\*\*([^*:]+)\*\*', section)
                if not items:
                    items = re.findall(r'[-•⚠️]\s*\*?\*?([^*:\n]{5,50})\*?\*?', section)
                if items:
                    self.data["risks"] = [i.strip() for i in items[:6]]
                    extracted += 1
                    break
        
        # 5. 从表格提取季度数据
        quarter_pattern = r'\|\s*(202[4-6]Q[1-4]|Q[1-4])\s*\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]*)\|'
        quarter_matches = re.findall(quarter_pattern, content)
        if quarter_matches:
            quarters = []
            for q in quarter_matches[:6]:
                quarters.append({
                    "quarter": q[0].strip(),
                    "revenue": q[1].strip(),
                    "revenue_yoy": q[2].strip(),
                    "net_income": q[3].strip() if len(q) > 3 else "-"
                })
            if quarters:
                self.data["quarters"] = quarters
                extracted += 1
        
        print(f"正则提取完成，共提取 {extracted} 个字段")
    
    def generate(self):
        """生成 Canvas 布局 (参考中海油模板)"""
        d = self.data
        
        # ========== 第一层: 标题 ==========
        self.add_node(
            "title",
            f"# {self.company_name}投资分析\n**{self.stock_code} | 当前价: {d['current_price']}**",
            400, -400, 400, 100, 'title'
        )
        
        # ========== 第二层: 核心结论 ==========
        conclusion_text = f"""## 核心结论
- **评级**: {d['rating']}
- **目标价**: {d['target_price']}
- **预期收益**: {d['expected_return']}"""
        
        self.add_node("conclusion", conclusion_text, 400, -250, 400, 140, 'conclusion')
        self.add_edge("title", "conclusion")
        
        # ========== 第三层: 三列 - 业绩/估值/风险概览 ==========
        
        # 左: 业绩数据
        perf_text = f"""## 业绩数据
| 指标 | 数值 |
|------|------|
| 营收 | {d['revenue']} |
| 同比 | {d['revenue_yoy']} |
| 净利润 | {d['net_income']} |
| 同比 | {d['net_income_yoy']} |
| 毛利率 | {d['gross_margin']} |
| 净利率 | {d['net_margin']} |
| ROE | {d['roe']} |"""
        
        self.add_node("financials", perf_text, 0, -50, 350, 220, 'neutral')
        self.add_edge("conclusion", "financials")
        
        # 中: 估值分析
        val_text = f"""## 估值分析
**当前估值**:
- PE (TTM): {d['pe']}
- PE (Forward): {d['pe_forward']}
- PB: {d['pb']}
- PS: {d['ps']}
- 股息率: {d['dividend_yield']}
- 市值: {d['market_cap']}"""
        
        self.add_node("valuation", val_text, 400, -50, 350, 220, 'neutral')
        self.add_edge("conclusion", "valuation")
        
        # 右: 风险概览
        risks = d.get('risks', ['待补充'])[:4]
        risk_text = "## 风险概览\n\n"
        for i, r in enumerate(risks, 1):
            risk_text += f"⚠️ **{r}**\n\n"
        
        self.add_node("risk-overview", risk_text, 800, -50, 350, 220, 'warning')
        self.add_edge("conclusion", "risk-overview")
        
        # ========== 第四层: 季度趋势 / 现金流 ==========
        
        # 左: 季度趋势
        quarters = d.get('quarters', [])
        quarter_text = "## 季度趋势\n\n| 季度 | 营收 | 同比 | 净利润 |\n|------|------|------|--------|\n"
        for q in quarters[:4]:
            quarter_text += f"| {q.get('quarter', '-')} | {q.get('revenue', '-')} | {q.get('revenue_yoy', '-')} | {q.get('net_income', '-')} |\n"
        if not quarters:
            quarter_text += "| - | - | - | - |\n\n*待补充季度数据*"
        
        self.add_node("quarters", quarter_text, 0, 220, 350, 200, 'highlight')
        self.add_edge("financials", "quarters")
        
        # 中: 现金流
        cash_text = f"""## 现金流与财务健康
- 经营现金流: {d['operating_cashflow']}
- 现金储备: {d['cash_ratio']}

**判断**: 
{self._assess_cash_health(d)}"""
        
        self.add_node("cashflow", cash_text, 400, 220, 350, 200, 'neutral')
        self.add_edge("valuation", "cashflow")
        
        # 右: 竞品对比
        competitors = d.get('competitors', [])
        comp_text = "## 竞品对比\n\n| 公司 | PE | 增速 | 市值 |\n|------|-----|------|------|\n"
        if competitors:
            for c in competitors[:4]:
                comp_text += f"| {c.get('name', '-')} | {c.get('pe', '-')} | {c.get('growth', '-')} | {c.get('cap', '-')} |\n"
        else:
            comp_text += f"| **{self.company_name}** | {d['pe']} | {d['revenue_yoy']} | {d['market_cap']} |\n"
            comp_text += "| - | - | - | - |\n\n*待补充竞品数据*"
        
        self.add_node("competitors", comp_text, 800, 220, 350, 200, 'neutral')
        self.add_edge("risk-overview", "competitors")
        
        # ========== 第五层: 投资亮点 / 投资策略 ==========
        
        # 左: 投资亮点
        catalysts = d.get('catalysts', ['待补充'])[:5]
        highlight_text = "## 投资亮点\n\n"
        for c in catalysts:
            highlight_text += f"✅ {c}\n\n"
        
        self.add_node("highlights", highlight_text, 0, 470, 350, 200, 'positive')
        self.add_edge("quarters", "highlights")
        
        # 中: 投资策略
        strategy_text = f"""## 投资策略

**建议**: {d['suggestion']}

**操作参考**:
- 目标价: {d['target_price']}
- 预期收益: {d['expected_return']}

**风险控制**:
- 设置止损
- 关注业绩变化"""
        
        self.add_node("strategy", strategy_text, 400, 470, 350, 200, 'positive')
        self.add_edge("cashflow", "strategy")
        
        # 右: 关键监控指标
        monitors = d.get('monitor_metrics', [
            "季度业绩",
            "竞争格局",
            "管理层动态",
            "行业政策"
        ])[:5]
        monitor_text = "## 关键监控指标\n\n"
        for i, m in enumerate(monitors, 1):
            monitor_text += f"{i}. 📊 {m}\n"
        
        self.add_node("monitor", monitor_text, 800, 470, 350, 200, 'monitor')
        self.add_edge("competitors", "monitor")
        
        # ========== 第六层: 风险详情 / 总结 ==========
        
        # 左: 风险详情
        risk_detail = "## 风险详情\n\n"
        for r in risks:
            risk_detail += f"⚠️ **{r}**\n- 影响: 待评估\n- 缓冲: 待分析\n\n"
        
        self.add_node("risk-detail", risk_detail, 100, 720, 400, 180, 'warning')
        self.add_edge("highlights", "risk-detail")
        self.add_edge("strategy", "risk-detail")
        
        # 右: 综合评估
        summary_text = f"""## 综合评估

**{self.company_name} ({self.stock_code})**

评级: {d['rating']}
目标: {d['target_price']}

---

*分析日期: {datetime.now().strftime('%Y-%m-%d')}*
*以上分析仅供参考*"""
        
        self.add_node("summary", summary_text, 550, 720, 400, 180, 'conclusion')
        self.add_edge("strategy", "summary")
        self.add_edge("monitor", "summary")
        
        return {
            "nodes": self.nodes,
            "edges": self.edges
        }
    
    def _assess_cash_health(self, d):
        """评估现金流健康度"""
        cash = d.get('operating_cashflow', '-')
        if cash != '-' and '亿' in str(cash):
            return "现金流充沛，财务稳健"
        return "待评估"


def find_latest_analysis_dir(stock_code):
    """查找最新的分析目录"""
    scripts_dir = os.path.dirname(os.path.abspath(__file__))
    import glob
    pattern = f"company_analysis_{stock_code}*"
    dirs = glob.glob(os.path.join(scripts_dir, pattern))
    if dirs:
        return max(dirs, key=os.path.getmtime)
    return None


def main():
    if len(sys.argv) < 3:
        print("""用法: python3 generate_canvas.py <公司名称> <股票代码> [报告路径] [输出目录]

示例:
    python3 generate_canvas.py 美团 03690.HK
    python3 generate_canvas.py 美团 03690.HK ~/ai/obsidian-notes/projects/美团-03690.HK.md
""")
        sys.exit(1)
    
    company_name = sys.argv[1]
    stock_code = sys.argv[2]
    report_path = sys.argv[3] if len(sys.argv) > 3 else None
    output_dir = sys.argv[4] if len(sys.argv) > 4 else os.path.expanduser("~/ai/obsidian-notes/canvases")
    
    print(f"生成 Canvas: {company_name} ({stock_code})")
    
    # 查找数据目录
    data_dir = find_latest_analysis_dir(stock_code)
    if data_dir:
        print(f"找到分析目录: {data_dir}")
    
    # 自动查找报告
    if not report_path:
        default_report = os.path.expanduser(f"~/ai/obsidian-notes/projects/{company_name}-{stock_code}.md")
        if os.path.exists(default_report):
            report_path = default_report
            print(f"找到报告: {report_path}")
    
    # 生成 Canvas
    generator = InvestmentCanvasGenerator(company_name, stock_code)
    generator.load_data(report_path, data_dir)
    canvas = generator.generate()
    
    # 保存文件
    os.makedirs(output_dir, exist_ok=True)
    output_file = os.path.join(output_dir, f"{company_name}-综合投资分析.canvas")
    
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(canvas, f, ensure_ascii=False, indent=2)
    
    print(f"\n✅ Canvas 已保存: {output_file}")
    print(f"   节点数: {len(canvas['nodes'])}")
    print(f"   连接数: {len(canvas['edges'])}")


if __name__ == "__main__":
    main()
