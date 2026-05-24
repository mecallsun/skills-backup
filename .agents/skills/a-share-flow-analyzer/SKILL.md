---
name: a-share-flow-analyzer
description: A-share institutional/retail flow intelligence via TuShare. Pulls margin financing (融资融券), dragon-tiger list (龙虎榜), and main-force money flow. Use when analyzing Chinese A-share tickers (6-digit codes) and need smart-money positioning signals. This is the A-share equivalent of options flow analysis — since most A-share stocks have no listed options, margin + 龙虎榜 + 主力资金 serve as the primary institutional footprint indicators.
---

# A-Share Flow Analyzer

## Overview

A-share stocks (Shanghai/Shenzhen) mostly do NOT have listed individual options — only ~60-100 blue-chips (平安/茅台/招行 etc.) via SSE options program. For the vast majority of A-shares, **options flow is not an available signal**.

This skill replaces options flow with A-share-native smart-money signals:

1. **融资融券 (Margin Financing)** — leveraged retail/semi-pro directional bets
   - 融资余额: long leverage positioning
   - 融券余额: short positioning (very small in A-shares, notable when spikes)
   - 融资净买入趋势 5/20/60 day
2. **龙虎榜 (Dragon-Tiger List)** — institutional/游资 (hot money) seat tracking
   - Only populated when stock moves >7% daily or hits turnover threshold
   - Shows 机构席位 (institutional seats) vs 游资席位 (prop trader seats) buy/sell
3. **主力资金流向 (Main-force money flow)** — block/large trade attribution

## When to Use

Triggered by `/analyze` Module 六C when ticker is A-share (6-digit numeric).

Also standalone:
- User asks about 融资融券 / 龙虎榜 / 主力资金 for a Chinese stock
- User wants "smart money positioning" on an A-share

## Data Source

**TuShare Pro** (requires `TUSHARE_TOKEN` env var). No alternatives — this is A-share canonical.

APIs used:
- `pro.margin_detail(ts_code=..., start_date=..., end_date=...)` — margin daily
- `pro.top_list(ts_code=..., start_date=..., end_date=...)` — dragon-tiger
- `pro.moneyflow(ts_code=..., start_date=..., end_date=...)` — money flow
- `pro.moneyflow_dc(ts_code=..., start_date=..., end_date=...)` — Eastmoney alt

## Usage

```python
import subprocess
result = subprocess.run(
    ["python3", "/Users/deepwish/.claude/skills/a-share-flow-analyzer/scripts/flow_analyzer.py", "600519"],
    capture_output=True, text=True
)
import json
data = json.loads(result.stdout)
```

Or direct:
```bash
python3 ~/.claude/skills/a-share-flow-analyzer/scripts/flow_analyzer.py 600519
python3 ~/.claude/skills/a-share-flow-analyzer/scripts/flow_analyzer.py 000001 --lookback 60
```

## Output Schema

```json
{
  "ticker": "600519",
  "ts_code": "600519.SH",
  "as_of": "2026-04-23",
  "margin": {
    "rzye_latest": 12345.67,          // 融资余额 (亿元)
    "rqye_latest": 123.45,            // 融券余额 (亿元)
    "rzmre_5d_sum": 567.89,           // 5日融资净买入
    "rzmre_20d_sum": 2345.67,         // 20日融资净买入
    "rzye_60d_chg_pct": 12.5,         // 60日融资余额变化%
    "rzye_rank_pct": 85,              // 融资余额所处52周分位
    "trend": "increasing_leverage"    // 趋势解读
  },
  "dragon_tiger": {
    "recent_count": 3,                // 近30日上榜次数
    "events": [
      {"date": "2026-04-15", "reason": "日涨幅偏离值达7%",
       "inst_buy": 123.45, "inst_sell": 0,
       "hotm_buy": 0, "hotm_sell": 89.01,
       "net_inst": 123.45, "net_hotm": -89.01}
    ],
    "net_inst_30d": 345.67,           // 30日机构净买入
    "net_hotm_30d": -89.01,           // 30日游资净买入
    "signal": "institutional_accumulation"
  },
  "money_flow": {
    "main_net_5d": 234.56,            // 5日主力净买入 (亿元)
    "main_net_20d": 567.89,
    "big_order_ratio": 0.35,           // 大单占比
    "signal": "main_force_inflow"
  },
  "interpretation": [
    "融资余额60日+12.5% — leveraged long demand increasing",
    "近30日3次上榜，机构净买入+345亿，游资净卖出-89亿 — smart money accumulating, hot money distributing",
    "20日主力净流入+567亿 — sustained institutional buying"
  ]
}
```

## Degradation

- TUSHARE_TOKEN missing: return `{"ticker": ..., "error": "TUSHARE_TOKEN required", "has_data": false}`
- ts_code not in TuShare universe (e.g. 新股/退市股): return `{"has_data": false, "reason": "..."}`
- 龙虎榜 无上榜记录: `recent_count: 0, events: []` — normal, most stocks don't appear

## Integration Points

- `/analyze` Module 六C (A-share branch)
- `/canslim-screener` 的 L 维度（流动性/机构关注度）补充
- `/theme-detector` A股板块资金流向
