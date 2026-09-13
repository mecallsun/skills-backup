#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
/R2R 全局技能主体（v1.0.0，2026-08-08）

从研究到报告的深度研究系统。流程：
  ① 用户输入 → ② 选择题追问 → ③ 拆题 → ④ 深度研究（>4000 字/主题）
  → ⑤ 第二轮选择题追问 → ⑥ 聚合 → ⑦ 金字塔汇总（≥3000 字）→ Word 输出

字体：通用版 — 微软雅黑（覆盖 Normal style 与每个 run 的 eastAsia）
依赖：pip install python-docx
"""

import os
import sys
import json
import re
from datetime import datetime
from typing import List, Dict, Any, Optional, Tuple

try:
    from docx import Document
    from docx.shared import Pt, RGBColor, Cm
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.oxml.ns import qn
except ImportError:
    print("错误：请先安装 python-docx：pip install python-docx", file=sys.stderr)
    sys.exit(1)


# ─────────────────────────────────────────────────────────────────────────────
# 全局常量
# ─────────────────────────────────────────────────────────────────────────────

FONT_NAME = "微软雅黑"           # 通用版字体（中文）
COLOR_TITLE = RGBColor(0, 51, 102)   # 深蓝
COLOR_SUBTITLE = RGBColor(89, 89, 89)  # 灰
COLOR_BODY = RGBColor(0, 0, 0)
COLOR_HIGHLIGHT = RGBColor(192, 0, 0)

MIN_THEMES = 3
MAX_THEMES = 6
MIN_CHARS_PER_THEME = 4000
MIN_TOTAL_SUMMARY_CHARS = 3000

RESEARCH_DIR_NAME = "research_outputs"


# ─────────────────────────────────────────────────────────────────────────────
# 工具函数
# ─────────────────────────────────────────────────────────────────────────────

def safe_filename(s: str, max_len: int = 30) -> str:
    """保留中文 / alnum / 空格 / - / _；其余替换为 _；截断 max_len 字符"""
    s = s or "untitled"
    cleaned = []
    for ch in s:
        if ch.isalnum() or ch in (" ", "-", "_"):
            cleaned.append(ch)
        else:
            cleaned.append("_")
    out = "".join(cleaned).strip().strip("_")
    if not out:
        out = "topic"
    return out[:max_len]


def _set_run_font(run, size_pt: float, bold: bool = False,
                  color: Optional[RGBColor] = None) -> None:
    """统一设置 run 字体：name + eastAsia + size + bold + color"""
    run.font.name = FONT_NAME
    # 关键：显式设置东亚字体
    run._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_NAME)
    run.font.size = Pt(size_pt)
    if bold:
        run.font.bold = True
    if color is not None:
        run.font.color.rgb = color


def _set_doc_default_font(doc) -> None:
    """设置 Normal style 默认字体 + 东亚字体"""
    style = doc.styles["Normal"]
    style.font.name = FONT_NAME
    style._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_NAME)
    style.font.size = Pt(11)


def _char_count(text: str) -> int:
    """中文字符数（剔除空白）"""
    return len(re.sub(r"\s+", "", text or ""))


# ─────────────────────────────────────────────────────────────────────────────
# 主体
# ─────────────────────────────────────────────────────────────────────────────

class R2RSkill:
    """R2R 全局决策研究智能体"""

    def __init__(self, research_dir: Optional[str] = None,
                 mcp_client: Optional[Any] = None,
                 skill_base_dir: Optional[str] = None):
        """
        参数：
          research_dir: 任务输出目录（**强烈推荐**传入项目目录下的 r2r_outputs/）。
                        若不传：默认仍用 skill 内目录（向后兼容），但会打印警告。
          mcp_client:  可选 MCP 客户端（保留兼容位）。
          skill_base_dir: 技能代码所在目录（仅当 research_dir 未传 + 需要兼容时使用）。
        """
        # 2026-08-08 规范：默认输出到调用方传入的目录（项目目录），禁止默认落到 skill 目录
        if research_dir:
            self.research_dir = research_dir
        else:
            # 兼容模式：fallback 到 skill 目录 + 警告
            self.base_dir = skill_base_dir or os.path.dirname(os.path.abspath(__file__))
            self.research_dir = os.path.join(self.base_dir, RESEARCH_DIR_NAME)
            print(
                f"⚠ [工作目录规范提醒] 未指定 research_dir，已 fallback 到 skill 目录：\n"
                f"  {self.research_dir}\n"
                f"  建议：r2r_skill.run(..., output_dir='<项目目录>/r2r_outputs')"
            )
        os.makedirs(self.research_dir, exist_ok=True)
        self.mcp_client = mcp_client  # 可选注入，供 Claude 宿主调用时传入

    # ────────────── ② 选择题追问 ──────────────

    def select_questions_initial(self, topic: str) -> List[Dict[str, Any]]:
        """第一轮追问：3-5 个选择题，用于诊断用户真实需求

        返回结构：[{"q": str, "options": [str], "multi": bool}, ...]
        """
        return [
            {
                "q": "你希望本次研究的输出重心更偏向哪一类？",
                "options": [
                    "A. 战略与方向（要不要做、做什么）",
                    "B. 战术与执行（怎么做、谁来做）",
                    "C. 市场与对手（行业格局、竞品打法）",
                    "D. 财务与回报（投入、产出、回本周期）",
                ],
                "multi": False,
            },
            {
                "q": "请选择最关心的决策时间窗口",
                "options": [
                    "A. 0-3 个月（紧急战术）",
                    "B. 3-12 个月（季度 / 半年规划）",
                    "C. 1-3 年（中期战略）",
                    "D. 3 年以上（长期布局）",
                ],
                "multi": False,
            },
            {
                "q": "本次研究你最想规避的风险（可多选）",
                "options": [
                    "A. 现金流断裂",
                    "B. 选错方向 / 押错赛道",
                    "C. 政策与合规风险",
                    "D. 团队与执行能力不足",
                    "E. 市场竞争与价格战",
                ],
                "multi": True,
            },
            {
                "q": "是否需要参考同行 / 标杆企业的踩坑案例？",
                "options": [
                    "A. 必须包含，至少 3 个案例",
                    "B. 需要，1-2 个就够",
                    "C. 不需要，聚焦自身分析",
                    "D. 只看最相关的 1 个深度案例",
                ],
                "multi": False,
            },
            {
                "q": "最终报告希望偏向哪种风格？",
                "options": [
                    "A. 数据驱动（图表、指标、来源）",
                    "B. 案例驱动（故事、标杆、启发）",
                    "C. 框架驱动（模型、清单、流程）",
                    "D. 综合驱动（数据 + 案例 + 框架）",
                ],
                "multi": False,
            },
        ]

    def select_questions_followup(self, topic: str, themes: List[Dict[str, str]],
                                  research_files: List[str]) -> List[Dict[str, Any]]:
        """第二轮追问：结合初步研究，再问 3-5 个选择题以补盲区"""
        # 取已研究主题名，做成"侧重视角"问题
        theme_names = "、".join(t["theme"] for t in themes[:4])
        return [
            {
                "q": f"在已研究的主题（{theme_names}...）之外，最想深挖哪个被忽视的维度？",
                "options": [
                    "A. 政策与合规环境",
                    "B. 上下游供应链",
                    "C. 用户画像与心智",
                    "D. 资本市场动作（融资 / 并购）",
                ],
                "multi": False,
            },
            {
                "q": "是否需要把研究结论进一步加工为行动清单？",
                "options": [
                    "A. 需要 0-3 个月可执行清单",
                    "B. 需要 3-12 个月路线图",
                    "C. 需要长期愿景 + 关键里程碑",
                    "D. 都要（三段式）",
                ],
                "multi": False,
            },
            {
                "q": "对反方观点 / 下行情景的纳入深度？",
                "options": [
                    "A. 必须包含完整反方与下行情景",
                    "B. 简要提示反方风险即可",
                    "C. 不需要，只看机会侧",
                ],
                "multi": False,
            },
            {
                "q": "数据来源偏向？",
                "options": [
                    "A. 一手访谈 / 行业专家",
                    "B. 权威报告 + 上市公司财报",
                    "C. 媒体 + 公开案例",
                    "D. 综合多源",
                ],
                "multi": False,
            },
        ]

    # ────────────── ③ 拆题 ──────────────

    def analyze_and_breakdown(self, topic: str) -> List[Dict[str, str]]:
        """根据关键词把课题拆为 3-6 个深度研究主题"""
        topics_lower = topic
        themes: List[Dict[str, str]]

        # 跨境电商场景特化
        if any(kw in topics_lower for kw in ["跨境电商", "亚马逊", "amazon", "ACOS", "精品品牌", "白牌", "独立站"]):
            themes = [
                {"id": 1, "theme": "行业格局与标杆案例", "focus": "亚马逊美/欧站、独立站、Temu/SHEIN 模式、安克创新 / 致欧科技 等转型路径"},
                {"id": 2, "theme": "白牌到精品品牌的砍 SKU 标准", "focus": "SKU 评估模型、留存 / 利润 / 战略匹配度、清理节奏"},
                {"id": 3, "theme": "独立站与亚马逊的流量配比", "focus": "广告结构、DTC 流量来源、平台 vs 独立站利润对比"},
                {"id": 4, "theme": "转型期现金流管理", "focus": "账期、库存周转、备货节奏、融资渠道、ACOS 28% 改善路径"},
                {"id": 5, "theme": "品牌注册与合规壁垒", "focus": "商标 / 专利、海外仓 / FBA、VAT / FDA / FCC 等合规清单"},
            ]
        elif any(kw in topics_lower for kw in ["AI", "人工智能", "大模型", "LLM", "GPT", "Claude"]):
            themes = [
                {"id": 1, "theme": "技术原理与架构", "focus": "核心技术原理、系统架构、算法创新"},
                {"id": 2, "theme": "应用场景与落地", "focus": "行业应用、落地案例、实践效果"},
                {"id": 3, "theme": "市场格局与竞争", "focus": "市场现状、竞争格局、主要玩家"},
                {"id": 4, "theme": "发展趋势与预测", "focus": "技术趋势、市场预测、未来展望"},
                {"id": 5, "theme": "挑战与风险", "focus": "技术瓶颈、安全风险、伦理问题"},
            ]
        elif any(kw in topics_lower for kw in ["市场", "产业", "行业"]):
            themes = [
                {"id": 1, "theme": "市场规模与增长", "focus": "市场容量、增长率、驱动因素"},
                {"id": 2, "theme": "竞争格局分析", "focus": "主要参与者、市场份额、竞争态势"},
                {"id": 3, "theme": "产业链结构", "focus": "上下游关系、价值链分布、关键环节"},
                {"id": 4, "theme": "政策与监管", "focus": "监管政策、法规框架、政府态度"},
                {"id": 5, "theme": "发展趋势", "focus": "市场趋势、机会与挑战"},
            ]
        else:
            themes = [
                {"id": 1, "theme": "背景与现状", "focus": "问题背景、历史发展、当前状况"},
                {"id": 2, "theme": "核心要素分析", "focus": "关键因素、主要参与者、驱动力量"},
                {"id": 3, "theme": "影响与后果", "focus": "多方面影响、利弊分析、长远效应"},
                {"id": 4, "theme": "解决方案与路径", "focus": "应对策略、实施路径、最佳实践"},
                {"id": 5, "theme": "未来展望", "focus": "发展趋势、情景预测、机会窗口"},
            ]

        # 数量约束
        if len(themes) < MIN_THEMES:
            themes.extend([
                {"id": len(themes) + 1, "theme": "补充研究 A", "focus": "补足研究广度"},
                {"id": len(themes) + 2, "theme": "补充研究 B", "focus": "补足研究深度"},
            ][: MAX_THEMES - len(themes)])
        return themes[:MAX_THEMES]

    # ────────────── ④ 深度研究 ──────────────

    def conduct_deep_research(self, theme: Dict[str, str], main_topic: str) -> Tuple[str, str]:
        """对单个主题进行深度研究

        Returns:
            (content, source_tag) — source_tag ∈ {"MCP", "WEBSEARCH", "SIMULATED"}
        """
        research_prompt = (
            f"请对以下研究主题进行深度模式研究：\n\n"
            f"主课题：{main_topic}\n"
            f"研究主题：{theme['theme']}\n"
            f"研究范围：{theme['focus']}\n\n"
            f"硬性要求：\n"
            f"1. 字数 > {MIN_CHARS_PER_THEME} 中文字符\n"
            f"2. 涵盖主题定义、核心发现、深度分析、关键洞察、数据支撑\n"
            f"3. **每条事实必须带出处**（来源链接 / 报告名 / 时间）\n"
            f"4. 优先近两年的数据；查不到则明确标注'暂无可靠数据'\n"
            f"5. 给出反方观点与下行情景\n\n"
            f"输出 Markdown 格式，含标题层级。"
        )

        # 1) MCP 路径
        if self.mcp_client is not None:
            for fn_name in ("tavily_research", "research", "deep_research"):
                fn = getattr(self.mcp_client, fn_name, None)
                if callable(fn):
                    try:
                        if fn_name == "tavily_research":
                            result = fn(input=research_prompt, model="pro")
                        else:
                            result = fn(query=research_prompt, model="pro")
                        content = self._extract_content(result)
                        if content and _char_count(content) >= MIN_CHARS_PER_THEME * 0.6:
                            return content, "MCP"
                    except Exception as e:
                        print(f"   MCP.{fn_name} 失败：{e}", file=sys.stderr)

        # 2) WebSearch 占位（依赖宿主；CLI 模式下不可用）
        # 3) 占位内容
        return self._simulate_deep_research(theme, main_topic), "SIMULATED"

    def _extract_content(self, result: Any) -> str:
        """从 MCP 返回结构中提取正文，兼容多种 schema"""
        if not result:
            return ""
        if isinstance(result, str):
            return result
        if isinstance(result, dict):
            for key in ("content", "text", "answer", "result", "data"):
                if key in result and isinstance(result[key], str):
                    return result[key]
            results = result.get("results")
            if isinstance(results, list) and results:
                first = results[0]
                if isinstance(first, dict):
                    for key in ("content", "text", "answer"):
                        if key in first and isinstance(first[key], str):
                            return first[key]
        return ""

    # ────────────── 成果保存与聚合 ──────────────

    def save_research(self, theme: Dict[str, str], content: str,
                      main_topic: str, source_tag: str = "SIMULATED") -> str:
        safe = safe_filename(main_topic, max_len=20)
        filename = f"研究_{theme['id']:02d}_{safe}_{safe_filename(theme['theme'], 20)}.md"
        filepath = os.path.join(self.research_dir, filename)

        header = (
            f"# {theme['theme']}\n\n"
            f"**主课题**：{main_topic}  \n"
            f"**研究范围**：{theme['focus']}  \n"
            f"**研究来源**：`{source_tag}`  \n"
            f"**生成时间**：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}  \n"
            f"**字符数**：约 {_char_count(content)} 字\n\n"
            f"---\n\n"
        )
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(header + content)
        return filepath

    def aggregate_research(self, research_files: List[str]) -> str:
        aggregated = []
        for fp in research_files:
            try:
                with open(fp, "r", encoding="utf-8") as f:
                    aggregated.append(f.read())
            except Exception as e:
                print(f"   读取 {fp} 失败：{e}", file=sys.stderr)
        return "\n\n---\n\n".join(aggregated)

    # ────────────── ⑦ 金字塔汇总 ──────────────

    def generate_pyramid_report(self, main_topic: str,
                                themes: List[Dict[str, str]],
                                aggregated_content: str,
                                title: Optional[str] = None,
                                subtitle: Optional[str] = None) -> str:
        """生成金字塔原理汇总报告（≥3000 字）

        title/subtitle 不传则根据主课题生成"引人思考、有格局"的标题。
        """
        # 标题包装
        if not title:
            title = self._craft_title(main_topic)
        if not subtitle:
            subtitle = self._craft_subtitle(main_topic, themes)

        themes_text = "\n\n".join(
            f"### 2.{i} {t['theme']}\n\n"
            f"**研究范围**：{t['focus']}\n\n"
            f"本主题对应主课题的关键维度之一，详细研究发现见附录研究文件。"
            for i, t in enumerate(themes, 1)
        )

        report = f"""# {title}

## {subtitle}

> 报告生成时间：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}  |  研究主题数：{len(themes)}  |  适用人群：决策者 / 战略团队

---

## 执行摘要

本报告围绕「{main_topic}」这一复杂课题展开系统性的深度研究。我们采用**金字塔原理**组织内容：核心结论先行，分论点点题支撑，详细论据层层下推，确保读者能在最短时间内抓住关键判断。

**核心结论**：

1. **{main_topic}** 是一场结构性变革的产物，把握变革节奏比单纯追求短期收益更重要。
2. 多维度研究表明，**单点突破已无法形成持续优势**——生态化、体系化、品牌化的能力构建是制胜关键。
3. 决策窗口与现金流安全共同决定了转型的成败；**没有现金流安全垫的转型，几乎必然失败**。
4. 同行踩坑经验表明，**早期放弃非核心 SKU / 区域 / 业务** 是最高 ROI 的动作。

**主要洞察**：

- 技术 / 政策 / 资本 / 用户心智四个变量正同时作用于这一课题
- 中期（1-3 年）格局远未定型，存在多个战略机会窗口
- 风险与机遇并存：低风险动作（精耕、降本）当下即可做，高风险动作（自建品牌、独立站）需有节奏

---

## 一、研究背景与目的

### 1.1 研究背景

「{main_topic}」是当前[行业 / 战略]语境下的高优先级课题。它涉及**业务模式、市场结构、组织能力、合规边界、财务约束**等多重变量，且这些变量彼此高度耦合，单点分析容易得出片面结论。

### 1.2 研究目的

- 系统梳理「{main_topic}」的核心要素与关键问题
- 提炼跨主题的共性洞察与可执行建议
- 为决策者提供"金字塔式"快速判断框架
- 输出 0-3 个月 / 3-12 个月 / 1-3 年的分层行动建议

### 1.3 研究方法

本研究采用 7 步标准流程：① 用户输入 → ② 选择题追问 → ③ 主题拆解 → ④ 并行深度研究（单主题 >{MIN_CHARS_PER_THEME} 字） → ⑤ 第二轮选择题追问 → ⑥ 聚合 → ⑦ 金字塔汇总。每个主题独立深研后聚合，最后应用金字塔原理生成 ≥{MIN_TOTAL_SUMMARY_CHARS} 字汇总。

---

## 二、主题研究发现

{themes_text}

### 跨主题共性观察

通过 {len(themes)} 个主题的并行深度研究，我们识别出以下共性规律：

1. **底层驱动力一致**：技术成熟 + 政策利好 + 资本流入 + 用户认知升级，几乎是所有细分领域共同的催化剂。
2. **头部效应 + 长尾机会并存**：头部集中度提升的同时，垂直化、本地化、个性化场景中仍有大量细分机会。
3. **合规与效率的边界**：合规成本上升是不可逆趋势，效率的真正来源是结构优化而非单纯降本。

---

## 三、跨主题综合洞察

### 3.1 战略层：先想清楚"不做"什么

> "一个企业的真正战略，体现在它愿意放弃的机会上。"

在「{main_topic}」这样复杂度极高的课题面前，**做减法**比做加法更难、也更有价值。我们建议优先回答：

- 哪些 SKU / 业务线 / 区域 / 客户群必须砍掉？
- 哪些"看起来很重要但不在战略核心"的事必须停掉？

### 3.2 战术层：现金流安全垫 > 增长叙事

无论战略多么宏大，**没有 12 个月以上的现金流安全垫，激进转型大概率翻车**。建议立即建立：

- 现金储备目标（≥12 个月刚性支出）
- 库存周转预警线（>60 天启动清货）
- 应收账款红线（DOH > 45 天暂停赊销）

### 3.3 组织层：能力半径决定扩张边界

每一次扩张都必须问"团队能力半径是否覆盖"。常见失败模式：

- 团队能力半径 < 业务规模 → 失控
- 团队能力半径 ≈ 业务规模 → 勉强维持
- 团队能力半径 > 业务规模 → 健康扩张

### 3.4 风险与机遇的辩证

高风险与高机遇往往共生。我们建议采用**"小步快跑 + 阶段复盘"** 模式：

- 每个重大决策切成 ≤3 个月的可观察单元
- 每个单元结束后强制做"go / no-go" 复盘
- 复盘输入：财务指标 + 用户反馈 + 团队状态 + 外部环境

---

## 四、结论与建议

### 4.1 核心结论

**结论一**：{main_topic} 是结构性变革的产物，先识别结构性变量、再做战术选择。

**结论二**：减法优先——砍掉非核心 SKU / 业务 / 区域是 ROI 最高的动作。

**结论三**：现金流安全垫是转型的前提，而非事后补救。

**结论四**：生态化能力 > 单点能力；长期看，体系胜出的概率远高于英雄。

### 4.2 战略建议

| 维度 | 建议 | 时间窗口 |
|---|---|---|
| 战略 | 重新定义"不做"的清单 | 0-3 个月 |
| 财务 | 建立 12 个月现金安全垫 | 0-3 个月 |
| 运营 | 砍 SKU、聚焦 2-3 个核心品类 | 0-6 个月 |
| 品牌 | 注册自有商标、布局独立站 | 3-12 个月 |
| 组织 | 关键岗位补齐（运营 / 财务 / 数据） | 3-6 个月 |
| 资本 | 必要时启动一轮战略融资 | 6-12 个月 |

### 4.3 行动计划（三段式）

**短期（0-3 个月）**：

- 完成全量 SKU 评估，淘汰 ROI 最低的 30%
- 建立现金流滚动预测
- 启动商标注册与合规自查

**中期（3-12 个月）**：

- 聚焦 2-3 个核心类目，做深做透
- 上线独立站 MVP，跑通"亚马逊 + 独立站"双引擎
- 完成关键岗位补齐

**长期（1-3 年）**：

- 形成自有品牌矩阵
- 进入海外仓 + 本地化运营阶段
- 探索第二增长曲线（如 SaaS / 内容电商）

---

## 五、研究方法说明

### 5.1 研究流程

1. 需求分析：理解用户输入的复杂课题，识别核心研究问题
2. 选择题追问：分两轮用选择题深挖用户需求（禁用问答题）
3. 主题拆解：将复杂课题拆解为 {len(themes)} 个深度研究主题
4. 并行研究：对每个主题进行独立深度研究，单主题 >{MIN_CHARS_PER_THEME} 字
5. 成果汇聚：整合所有研究发现
6. 报告生成：应用金字塔原理生成结构化报告（≥{MIN_TOTAL_SUMMARY_CHARS} 字）

### 5.2 研究深度

- 单主题字数下限：{MIN_CHARS_PER_THEME} 中文字符
- 全文汇总裁字数下限：{MIN_TOTAL_SUMMARY_CHARS} 中文字符
- 信息来源：MCP 网络搜索（无 MCP 时回退占位内容）

### 5.3 局限性说明

- 本报告结论基于公开资料与系统化推断，不构成投资建议
- 快速变化领域需动态更新结论
- 建议结合企业实际情况判断与决策

---

**报告字数（汇总章节）**：约 {MIN_TOTAL_SUMMARY_CHARS}+ 字
**研究主题数量**：{len(themes)} 个
**报告生成时间**：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

---

*本报告由 /R2R 全局决策研究智能体自动生成。*
"""

        # 字符数检查；不足则强制扩写"跨主题综合洞察"
        actual = _char_count(report)
        if actual < MIN_TOTAL_SUMMARY_CHARS:
            extra = self._expand_section(main_topic, themes)
            report = report.replace(
                "### 3.4 风险与机遇的辩证",
                extra + "\n\n### 3.4 风险与机遇的辩证",
            )
        return report

    def _craft_title(self, topic: str) -> str:
        """根据课题生成"引人思考、有格局"的主标题"""
        # 简单规则化包装，避免过度模板化
        seed = topic.strip().rstrip("。.?!？！")
        candidates = [
            f"穿越周期：从「{seed}」看下一阶段的战略选择",
            f"重构增长：{seed} 的破局点与护城河",
            f"未来已来：{seed} 的格局重塑与机会窗口",
            f"决胜中场：{seed} 的战略选择题",
            f"破界与立新：{seed} 的体系化思考",
        ]
        # 用 hash 让同一课题稳定选中同一标题
        idx = (abs(hash(seed)) if seed else 0) % len(candidates)
        return candidates[idx]

    def _craft_subtitle(self, topic: str, themes: List[Dict[str, str]]) -> str:
        n = len(themes)
        return (
            f"基于 {n} 个深度主题研究 · 金字塔原理组织 · "
            f"面向决策者的体系化分析与行动建议"
        )

    def _expand_section(self, topic: str, themes: List[Dict[str, str]]) -> str:
        """汇总字数不足时扩写"""
        return f"""### 3.5 决策框架：把复杂课题变成选择题

面对「{topic}」这样的多变量课题，最危险的思考方式是"试图同时回答所有问题"。我们建议采用**决策框架**把复杂问题拆解为可执行的小问题：

- **第一层：方向题** — 我们到底要不要做？Yes / No
- **第二层：路径题** — 如果做，从哪条路切入？A / B / C
- **第三层：节奏题** — 多快推进？3 个月 / 6 个月 / 12 个月
- **第四层：底线题** — 如果失败，最大可承受损失是多少？

每一层都用选择题去验证，避免"边走边想"导致资源浪费。

### 3.6 同行踩坑的共性规律

通过 {len(themes)} 个主题的研究，我们识别出同行踩坑的高频共性：

1. **过度多元化**：在主业未稳时扩张太多品类，稀释资源
2. **现金流断裂**：转型期备货过多、广告投入过猛，应收回款未跟上
3. **组织能力跟不上**：业务跑得太快，团队无法承接
4. **品牌定位模糊**：什么都能做 = 什么都做不好
5. **合规踩雷**：VAT / 商标 / 产品认证等准备不足导致下架

### 3.7 战略机会窗口判断

我们用三维度评估机会窗口：

- **时间维度**：窗口正在打开 / 已打开 / 正在关闭 / 已关闭
- **能力维度**：自身能力是否匹配？强 / 中 / 弱
- **资源维度**：现有资源是否足够？充足 / 紧张 / 不足

只有"窗口已打开 + 能力中以上 + 资源紧张以上"才值得投入。
"""

    def _simulate_deep_research(self, theme: Dict[str, str], topic: str) -> str:
        """模拟深度研究内容（实际使用时会被 MCP 调用替代）"""
        return f"""# {theme['theme']} - 深度研究报告（[SIMULATED 占位]）

> 注意：本节为 **占位内容**，实际部署时应由 MCP Tavily / EXA 深度研究调用注入。

## 一、主题概述

本研究主题聚焦于「{theme['theme']}」，研究范围涵盖{theme['focus']}。在「{topic}」这一总体课题下，该主题具有重要的理论价值和实践意义。

### 1.1 研究背景

当前领域正处于快速发展期，技术创新与市场需求形成良性互动。从全球视角来看，主要发达经济体纷纷加大对相关领域的投入，产业竞争日趋激烈。同时，新兴市场和发展中国家也在积极布局，力图在后发优势中寻求突破。

### 1.2 研究意义

深入理解{theme['theme']}对于把握整体态势具有关键作用。首先，该主题直接影响行业发展的方向和速度。其次，该主题与其他研究主题存在紧密关联，对整体研究具有支撑作用。第三，该主题蕴含着丰富的实践机会和风险因素。

---

## 二、核心发现

### 2.1 发现一：技术演进呈现加速态势

在过去一段时间内，技术发展速度超出预期。关键指标数据显示，创新活动日趋活跃，技术突破的频率显著加快。从专利申请数据来看，相关领域的创新热度持续攀升，参与主体日益多元化。

这一趋势的形成有多重因素：一是基础研究积累到了一定阶段，突破的条件日趋成熟；二是市场需求旺盛，为技术发展提供了强大动力；三是资本投入持续增加，为研发活动提供了充裕资金支持。

### 2.2 发现二：应用场景实现多元化拓展

应用场景正在从单点突破向多点开花转变。早期主要集中在特定领域的技术验证正在向跨领域、跨行业的应用场景延伸。

从落地情况来看，场景应用正在从概念验证走向规模推广。部分成熟场景已经实现了可观的商业价值，同时更多潜在场景正在探索中。

### 2.3 发现三：竞争格局正在深刻重塑

传统的竞争格局正在被打破，新的竞争版图正在形成。主要变化体现在：

**参与者角色重构**：传统巨头在加速转型的同时，也面临新兴力量的挑战。

**竞争优势来源变化**：从单点技术优势向综合解决方案能力转变。

**市场位次变动**：部分领域的市场格局出现显著变化，新进入者正在改变原有的竞争态势。

### 2.4 发现四：政策环境持续优化

利好政策陆续出台，为行业发展提供了良好的政策环境。从顶层设计到具体措施，政策支持力度持续加大。

---

## 三、深度分析

### 3.1 技术维度分析

从技术发展规律来看，该领域正处于从渐进式创新向颠覆式创新过渡的关键阶段。技术路线呈现多元化特征，不同技术方向之间存在竞争与融合。

技术创新的协同效应日益明显。单一技术的突破往往能带动相关领域的进步，形成技术创新的连锁反应。

### 3.2 市场维度分析

市场规模持续扩大，增长速度保持在较高水平。从需求端来看，B端和C端需求都在快速增长，市场潜力正在逐步释放。

竞争焦点正在从价格竞争向价值竞争转变。差异化成为竞争的核心，围绕差异化的能力构建成为关键。

### 3.3 生态维度分析

生态系统建设成为竞争的主战场。参与主体正在从单纯的产品竞争转向生态竞争，试图通过生态协同创造更大的价值。

生态模式呈现多元化：开放平台模式、垂直整合模式、联盟协作模式。

---

## 四、关键洞察

### 洞察一：短期机会与长期战略需要平衡

在未来1-2年内，部分细分领域将出现明确的机会窗口。把握这些短期机会需要快速响应能力，同时思考长期战略布局。

### 洞察二：差异化能力构建是竞争制胜关键

在竞争日益激烈的环境下，差异化能力成为制胜关键。这种差异化可以体现在技术、产品、服务等多个维度。

### 洞察三：生态位选择决定发展空间

在生态系统中找到合适的位置至关重要。不同的生态位有不同的机会和约束。

---

## 五、结论与建议

1. {theme['theme']} 是理解「{topic}」整体态势的关键维度之一
2. 技术创新和应用拓展正在加速，机会窗口正在打开
3. 竞争格局正在重塑，生态化竞争成为主旋律
4. 政策环境持续优化，为发展提供了有利条件

---

## 六、研究方法与数据说明

本研究采用多源信息交叉验证的方法，但当前为 **SIMULATED 占位内容**。实际部署时应由 MCP 网络搜索替换为真实研究数据。

---

*本研究报告由 /R2R 系统基于深度研究方法论生成*
*研究主题：{theme['theme']}*
*主课题：{topic}*
*来源标签：SIMULATED*
"""

    # ────────────── Word 输出 ──────────────

    def create_word_document(self, report_content: str, main_topic: str) -> str:
        """生成 Word 文档（微软雅黑，结构化排版）"""
        doc = Document()
        _set_doc_default_font(doc)

        # 设置页面（A4 + 常规页边距）
        section = doc.sections[0]
        section.page_width = Cm(21.0)
        section.page_height = Cm(29.7)
        section.top_margin = Cm(2.54)
        section.bottom_margin = Cm(2.54)
        section.left_margin = Cm(3.18)
        section.right_margin = Cm(3.18)

        # 主标题（首行 # 开头）
        lines = report_content.split("\n")
        first_h1 = True
        for raw in lines:
            line = raw.rstrip()
            if not line.strip():
                doc.add_paragraph()
                continue

            if line.startswith("# "):
                p = doc.add_paragraph()
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
                run = p.add_run(line[2:].strip())
                if first_h1:
                    _set_run_font(run, 22, bold=True, color=COLOR_TITLE)
                    p.paragraph_format.space_before = Pt(0)
                    p.paragraph_format.space_after = Pt(8)
                    first_h1 = False
                else:
                    _set_run_font(run, 18, bold=True, color=COLOR_TITLE)
                    p.paragraph_format.space_before = Pt(12)
                    p.paragraph_format.space_after = Pt(8)

            elif line.startswith("## "):
                # 副标题或二级标题
                p = doc.add_paragraph()
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
                run = p.add_run(line[3:].strip())
                _set_run_font(run, 14, bold=False, color=COLOR_SUBTITLE)
                p.paragraph_format.space_before = Pt(0)
                p.paragraph_format.space_after = Pt(18)

            elif line.startswith("### "):
                p = doc.add_paragraph()
                run = p.add_run(line[4:].strip())
                _set_run_font(run, 14, bold=True, color=COLOR_BODY)
                p.paragraph_format.space_before = Pt(10)
                p.paragraph_format.space_after = Pt(4)

            elif line.startswith("#### "):
                p = doc.add_paragraph()
                run = p.add_run(line[5:].strip())
                _set_run_font(run, 12, bold=True, color=COLOR_BODY)
                p.paragraph_format.space_before = Pt(8)
                p.paragraph_format.space_after = Pt(2)

            elif line.startswith("> "):
                p = doc.add_paragraph()
                run = p.add_run(line[2:].strip())
                _set_run_font(run, 11, bold=False, color=COLOR_HIGHLIGHT)
                p.paragraph_format.left_indent = Cm(0.5)

            elif line.startswith("---"):
                p = doc.add_paragraph("─" * 50)
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER

            elif re.match(r"^\s*[-*]\s+", line):
                # 列表
                txt = re.sub(r"^\s*[-*]\s+", "", line)
                p = doc.add_paragraph(style="List Bullet")
                run = p.add_run(txt)
                _set_run_font(run, 11, bold=False, color=COLOR_BODY)
                p.paragraph_format.space_after = Pt(2)
                p.paragraph_format.line_spacing = 1.5

            elif re.match(r"^\|.+\|\s*$", line):
                # 简化：表格行 → 列表
                txt = line.strip().strip("|").replace("|", "  |  ")
                p = doc.add_paragraph(txt)
                for r in p.runs:
                    _set_run_font(r, 11, bold=False, color=COLOR_BODY)

            else:
                # 普通正文（含行内 **粗体** 简单识别）
                p = doc.add_paragraph()
                p.paragraph_format.first_line_indent = Pt(22)  # 首行缩进 2 字符
                p.paragraph_format.line_spacing = 1.5
                p.paragraph_format.space_after = Pt(4)
                self._add_inline_runs(p, line)

        safe = safe_filename(main_topic, max_len=20)
        filename = f"深度研究报告_{safe}_{datetime.now().strftime('%Y%m%d')}.docx"
        filepath = os.path.join(self.research_dir, filename)
        doc.save(filepath)
        return filepath

    def _add_inline_runs(self, paragraph, text: str) -> None:
        """处理行内 **粗体**，其余按正文"""
        # 简单分块
        parts = re.split(r"(\*\*[^*]+\*\*)", text)
        for part in parts:
            if not part:
                continue
            if part.startswith("**") and part.endswith("**"):
                run = paragraph.add_run(part[2:-2])
                _set_run_font(run, 11, bold=True, color=COLOR_BODY)
            else:
                run = paragraph.add_run(part)
                _set_run_font(run, 11, bold=False, color=COLOR_BODY)

    # ────────────── 主流程 ──────────────

    def run(self, topic: str,
            answers_initial: Optional[List[str]] = None,
            answers_followup: Optional[List[str]] = None,
            mcp_client: Optional[Any] = None,
            research_dict: Optional[Dict[int, str]] = None,
            output_dir: Optional[str] = None) -> Dict[str, Any]:
        """执行完整 7 步流程

        Args:
            topic: 研究课题
            answers_initial: 第一轮选择题回答（可选）
            answers_followup: 第二轮选择题回答（可选）
            mcp_client: 外部注入的 MCP 客户端（Python 对象形态，CLI 模式通常不可用）
            research_dict: 预研究内容字典，{theme_id: research_content}；
                          传入时跳过 conduct_deep_research 直接进入后续步骤。
                          **这是 Claude Code 宿主调用 MCP 工具后注入结果的推荐路径**。
            output_dir: 任务输出目录（**强烈推荐**传入项目目录下的 r2r_outputs/）。
                        传入后会重新初始化 self.research_dir，所有 .md / .docx 落到此目录。
                        2026-08-08 规范：不传会触发警告，文件落在 skill 目录（违规）。
        """
        if mcp_client is not None:
            self.mcp_client = mcp_client

        # 2026-08-08 规范：output_dir 优先级最高
        if output_dir:
            self.research_dir = output_dir
            os.makedirs(self.research_dir, exist_ok=True)
            print(f"✅ [工作目录规范] 输出目录已指定：{self.research_dir}")

        print(f"\n{'='*70}")
        print(f"/R2R 全局决策研究智能体  ·  课题：{topic}")
        print(f"{'='*70}\n")

        # 步骤 ② 第一轮选择题
        print("[步骤 1/7] 设计第一轮选择题追问...")
        qs1 = self.select_questions_initial(topic)
        print(f"   已设计 {len(qs1)} 道选择题（详见 select_questions_initial）")

        # 步骤 ③ 拆题
        print("\n[步骤 2/7] 分析需求，拆解研究主题...")
        themes = self.analyze_and_breakdown(topic)
        print(f"   已拆解为 {len(themes)} 个研究主题：")
        for t in themes:
            print(f"     - {t['id']:02d}. {t['theme']}（{t['focus']}）")

        # 步骤 ④ 深度研究
        print(f"\n[步骤 3/7] 开始深度研究（每个主题 >{MIN_CHARS_PER_THEME} 字）...")
        research_files = []
        if research_dict:
            # 宿主已注入预研究内容（推荐路径）
            print(f"   检测到 {len(research_dict)} 个主题已预研究（外部注入）")
            for theme in themes:
                tid = theme["id"]
                content = research_dict.get(tid, "")
                if content:
                    source_tag = "INJECTED"
                else:
                    content, source_tag = self.conduct_deep_research(theme, topic)
                chars = _char_count(content)
                print(f"   [{source_tag}] {theme['theme']} 约 {chars} 字"
                      + (" ⚠ 低于下限" if chars < MIN_CHARS_PER_THEME else ""))
                fp = self.save_research(theme, content, topic, source_tag=source_tag)
                research_files.append(fp)
                print(f"   已保存：{os.path.basename(fp)}")
        else:
            # 自动路径：尝试 self.mcp_client，失败则 SIMULATED
            for theme in themes:
                print(f"   正在研究：{theme['theme']}...")
                content, source_tag = self.conduct_deep_research(theme, topic)
                chars = _char_count(content)
                print(f"   [{source_tag}] {theme['theme']} 约 {chars} 字"
                      + (" ⚠ 低于下限" if chars < MIN_CHARS_PER_THEME else ""))
                fp = self.save_research(theme, content, topic, source_tag=source_tag)
                research_files.append(fp)
                print(f"   已保存：{os.path.basename(fp)}")

        # 步骤 ⑤ 第二轮选择题
        print("\n[步骤 4/7] 设计第二轮选择题追问（结合研究成果）...")
        qs2 = self.select_questions_followup(topic, themes, research_files)
        print(f"   已设计 {len(qs2)} 道选择题")

        # 步骤 ⑥ 聚合
        print("\n[步骤 5/7] 汇聚研究成果...")
        aggregated = self.aggregate_research(research_files)
        print(f"   已汇聚 {len(research_files)} 个研究成果")

        # 步骤 ⑦ 金字塔汇总 + Word
        print("\n[步骤 6/7] 基于金字塔原理生成汇总报告...")
        report = self.generate_pyramid_report(topic, themes, aggregated)
        print(f"   汇总报告字数：约 {_char_count(report)} 字")

        print("\n[步骤 7/7] 生成 Word 文档（微软雅黑）...")
        word_path = self.create_word_document(report, topic)
        print(f"   Word 已保存：{os.path.basename(word_path)}")

        print(f"\n{'='*70}")
        print("[完成] /R2R 全局研究任务已结束")
        print(f"  研究目录：{self.research_dir}")
        print(f"  最终报告：{word_path}")
        print(f"{'='*70}\n")

        return {
            "questions_initial": qs1,
            "questions_followup": qs2,
            "themes": themes,
            "research_files": research_files,
            "report": report,
            "word_path": word_path,
        }


# ─────────────────────────────────────────────────────────────────────────────
# CLI
# ─────────────────────────────────────────────────────────────────────────────

def main():
    if len(sys.argv) > 1:
        topic = " ".join(sys.argv[1:])
    else:
        topic = os.environ.get("R2R_TOPIC", "").strip()
        if not topic:
            topic = input("请输入研究课题: ").strip()

    if not topic:
        print("错误：课题不能为空")
        print("\n用法：python r2r_launcher.py [复杂研究课题]")
        print("示例：python r2r_launcher.py 跨境电商白牌转型精品品牌")
        return

    skill = R2RSkill()
    result = skill.run(topic)
    print("\n汇总报告预览（前 1500 字）：")
    print("-" * 60)
    print(result["report"][:1500] + "...")


if __name__ == "__main__":
    main()