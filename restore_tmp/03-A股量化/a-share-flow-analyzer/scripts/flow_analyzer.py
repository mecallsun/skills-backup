#!/usr/bin/env python3
"""
A-Share Smart-Money Flow Analyzer (TuShare Pro)

Replaces options flow for A-shares (which mostly have no listed options).
Pulls:
- 融资融券 (margin financing) — leveraged positioning proxy
- 龙虎榜 (dragon-tiger list) — institutional vs hot-money seats
- 主力资金流向 — block/large trade attribution

Usage:
    python3 flow_analyzer.py TICKER [--lookback 60]

Requires: TUSHARE_TOKEN env var.
Output: JSON to stdout.
"""
import json
import os
import sys
import argparse
from datetime import datetime, timedelta


def ts_code_from_ticker(ticker: str) -> str:
    """Convert 6-digit ticker → TuShare ts_code."""
    t = ticker.strip().upper().replace(".SS", "").replace(".SZ", "").replace(".SH", "")
    if t.startswith("6") or t.startswith("9"):
        return f"{t}.SH"
    elif t.startswith(("0", "2", "3")):
        return f"{t}.SZ"
    elif t.startswith(("8", "4")):
        return f"{t}.BJ"  # 北交所
    return f"{t}.SH"


def safe_sum(df, col):
    try:
        return float(df[col].fillna(0).sum())
    except Exception:
        return 0.0


def analyze(ticker: str, lookback: int = 60) -> dict:
    token = os.environ.get("TUSHARE_TOKEN", "")
    if not token:
        return {"ticker": ticker, "error": "TUSHARE_TOKEN env var required", "has_data": False}

    try:
        import tushare as ts
    except ImportError:
        return {"ticker": ticker, "error": "pip install tushare required", "has_data": False}

    ts.set_token(token)
    pro = ts.pro_api()

    ts_code = ts_code_from_ticker(ticker)
    today = datetime.now()
    end_date = today.strftime("%Y%m%d")
    start_date = (today - timedelta(days=lookback + 30)).strftime("%Y%m%d")  # buffer for non-trading days

    out = {
        "ticker": ticker,
        "ts_code": ts_code,
        "as_of": today.strftime("%Y-%m-%d"),
        "lookback_days": lookback,
        "has_data": True,
    }

    # === 1. 融资融券 ===
    try:
        margin = pro.margin_detail(ts_code=ts_code, start_date=start_date, end_date=end_date)
        if not margin.empty:
            margin = margin.sort_values("trade_date", ascending=False).reset_index(drop=True)
            latest = margin.iloc[0]
            m5 = margin.head(5)
            m20 = margin.head(20)
            m60 = margin.head(min(60, len(margin)))

            rzye_latest = float(latest.get("rzye", 0)) / 1e8   # 亿元
            rqye_latest = float(latest.get("rqye", 0)) / 1e8
            rzmre_5d = safe_sum(m5, "rzmre") / 1e8  # 5日融资净买入
            rzmre_20d = safe_sum(m20, "rzmre") / 1e8

            rzye_oldest = float(m60.iloc[-1].get("rzye", 0)) / 1e8 if len(m60) > 1 else rzye_latest
            rzye_chg_pct = ((rzye_latest - rzye_oldest) / rzye_oldest * 100) if rzye_oldest else 0

            # 52-week rzye percentile
            try:
                year_data = pro.margin_detail(
                    ts_code=ts_code,
                    start_date=(today - timedelta(days=365)).strftime("%Y%m%d"),
                    end_date=end_date,
                )
                if not year_data.empty:
                    year_rzye = year_data["rzye"].dropna().astype(float)
                    rzye_rank = round(float((year_rzye < latest["rzye"]).mean()) * 100, 1)
                else:
                    rzye_rank = None
            except Exception:
                rzye_rank = None

            # Trend interpretation
            if rzye_chg_pct > 10 and rzmre_20d > 0:
                trend = "leveraged_accumulation"
            elif rzye_chg_pct < -10 and rzmre_20d < 0:
                trend = "deleveraging"
            elif rzmre_20d > 0:
                trend = "mild_accumulation"
            elif rzmre_20d < 0:
                trend = "mild_distribution"
            else:
                trend = "neutral"

            out["margin"] = {
                "rzye_latest_yi": round(rzye_latest, 2),
                "rqye_latest_yi": round(rqye_latest, 2),
                "rzmre_5d_sum_yi": round(rzmre_5d, 2),
                "rzmre_20d_sum_yi": round(rzmre_20d, 2),
                "rzye_60d_chg_pct": round(rzye_chg_pct, 2),
                "rzye_rank_pct_52w": rzye_rank,
                "trend": trend,
            }
        else:
            out["margin"] = {"available": False, "reason": "non-margin-eligible ticker"}
    except Exception as e:
        out["margin"] = {"error": str(e)[:200]}

    # === 2. 龙虎榜 (TuShare requires trade_date; iterate last N trading days) ===
    try:
        # Loop trading days and collect top_list hits
        tl_rows = []
        try:
            # Get all trading dates in range
            trade_cal = pro.trade_cal(start_date=start_date, end_date=end_date, is_open='1')
            dates_list = trade_cal['cal_date'].tolist()[-min(30, len(trade_cal)):]  # last 30 trading days
        except Exception:
            dates_list = []

        import pandas as pd
        all_hits = []
        for td in dates_list:
            try:
                hit = pro.top_list(trade_date=td, ts_code=ts_code)
                if hit is not None and not hit.empty:
                    all_hits.append(hit)
            except Exception:
                continue

        tl = pd.concat(all_hits, ignore_index=True) if all_hits else pd.DataFrame()
        if not tl.empty:
            tl = tl.sort_values("trade_date", ascending=False).reset_index(drop=True)
            events = []

            # Get top_inst detail for each top_list entry
            inst_net_30d = 0.0
            hotm_net_30d = 0.0

            for _, row in tl.head(10).iterrows():
                trade_date = row["trade_date"]
                reason = row.get("reason", "")
                event = {
                    "date": trade_date,
                    "reason": reason,
                    "net_buy_amount_yi": round(float(row.get("net_amount", 0)) / 1e8, 2),
                    "turnover_ratio": round(float(row.get("turnover_ratio", 0)), 2),
                }

                # Top institutional seats detail
                try:
                    inst = pro.top_inst(ts_code=ts_code, trade_date=trade_date)
                    if not inst.empty:
                        inst_buy = float(inst[inst["side"] == 0]["buy"].sum()) / 1e8 if "side" in inst.columns else 0
                        inst_sell = float(inst[inst["side"] == 1]["sell"].sum()) / 1e8 if "side" in inst.columns else 0
                        # 机构席位（exalter 含 "机构" 或 "证券"）
                        inst_rows = inst[inst["exalter"].str.contains("机构|资管|社保|保险", na=False)]
                        hotm_rows = inst[~inst["exalter"].str.contains("机构|资管|社保|保险", na=False)]
                        net_inst = (float(inst_rows["buy"].sum()) - float(inst_rows["sell"].sum())) / 1e8
                        net_hotm = (float(hotm_rows["buy"].sum()) - float(hotm_rows["sell"].sum())) / 1e8
                        event["net_inst_yi"] = round(net_inst, 2)
                        event["net_hotm_yi"] = round(net_hotm, 2)
                        inst_net_30d += net_inst
                        hotm_net_30d += net_hotm
                except Exception:
                    pass

                events.append(event)

            # Signal
            if inst_net_30d > 0 and hotm_net_30d < 0:
                signal = "institutional_accumulation_hotm_distribution"
            elif inst_net_30d > 0 and hotm_net_30d > 0:
                signal = "broad_buying"
            elif inst_net_30d < 0:
                signal = "institutional_distribution"
            else:
                signal = "mixed"

            out["dragon_tiger"] = {
                "recent_count": len(tl),
                "events": events[:5],  # top 5 recent
                "net_inst_recent_yi": round(inst_net_30d, 2),
                "net_hotm_recent_yi": round(hotm_net_30d, 2),
                "signal": signal,
            }
        else:
            out["dragon_tiger"] = {"recent_count": 0, "events": [], "signal": "no_activity"}
    except Exception as e:
        out["dragon_tiger"] = {"error": str(e)[:200]}

    # === 3. 主力资金 ===
    try:
        mf = pro.moneyflow(ts_code=ts_code, start_date=start_date, end_date=end_date)
        if not mf.empty:
            mf = mf.sort_values("trade_date", ascending=False).reset_index(drop=True)
            # 主力 = 大单 + 特大单
            mf["main_net"] = (
                mf["buy_lg_amount"].fillna(0) + mf["buy_elg_amount"].fillna(0)
                - mf["sell_lg_amount"].fillna(0) - mf["sell_elg_amount"].fillna(0)
            )
            main_5d = float(mf.head(5)["main_net"].sum()) / 1e4  # 单位 千元→亿元（除以1e4）
            main_20d = float(mf.head(20)["main_net"].sum()) / 1e4

            total_turnover = float(
                (mf["buy_lg_amount"] + mf["buy_md_amount"] + mf["buy_sm_amount"] +
                 mf["sell_lg_amount"] + mf["sell_md_amount"] + mf["sell_sm_amount"]).fillna(0).sum()
            )
            big_order_amount = float(
                (mf["buy_lg_amount"] + mf["buy_elg_amount"] +
                 mf["sell_lg_amount"] + mf["sell_elg_amount"]).fillna(0).sum()
            )
            big_ratio = (big_order_amount / total_turnover) if total_turnover else 0

            if main_20d > 0 and main_5d > 0:
                mf_signal = "main_force_inflow"
            elif main_20d < 0 and main_5d < 0:
                mf_signal = "main_force_outflow"
            elif main_5d > 0 and main_20d < 0:
                mf_signal = "recent_reversal_bullish"
            else:
                mf_signal = "mixed"

            out["money_flow"] = {
                "main_net_5d_yi": round(main_5d, 2),
                "main_net_20d_yi": round(main_20d, 2),
                "big_order_ratio": round(big_ratio, 3),
                "signal": mf_signal,
            }
        else:
            out["money_flow"] = {"available": False}
    except Exception as e:
        out["money_flow"] = {"error": str(e)[:200]}

    # === Qualitative interpretation ===
    interp = []
    m = out.get("margin", {})
    if isinstance(m, dict) and m.get("rzye_60d_chg_pct") is not None:
        chg = m["rzye_60d_chg_pct"]
        if chg > 15:
            interp.append(f"融资余额 60日 +{chg}% — 杠杆多头大幅增仓 (leveraged long buildup)")
        elif chg < -15:
            interp.append(f"融资余额 60日 {chg}% — 杠杆资金撤离 (deleveraging)")
        elif abs(chg) > 5:
            direction = "增仓" if chg > 0 else "减仓"
            interp.append(f"融资余额 60日 {chg}% — 杠杆温和{direction}")

        n20 = m.get("rzmre_20d_sum_yi", 0)
        if n20 > 5:
            interp.append(f"20日融资净买入 +{n20}亿 — 杠杆资金持续流入")
        elif n20 < -5:
            interp.append(f"20日融资净买入 {n20}亿 — 杠杆资金持续流出")

        if m.get("rzye_rank_pct_52w") and m["rzye_rank_pct_52w"] > 80:
            interp.append(f"融资余额处于 52周 {m['rzye_rank_pct_52w']}% 分位 — 高位警惕 (contrarian caution at crowded level)")
        elif m.get("rzye_rank_pct_52w") and m["rzye_rank_pct_52w"] < 20:
            interp.append(f"融资余额处于 52周 {m['rzye_rank_pct_52w']}% 分位 — 低位，反弹空间潜在")

    dt = out.get("dragon_tiger", {})
    if isinstance(dt, dict) and dt.get("recent_count", 0) > 0:
        net_inst = dt.get("net_inst_recent_yi", 0)
        net_hotm = dt.get("net_hotm_recent_yi", 0)
        if net_inst > 0 and net_hotm < 0:
            interp.append(f"龙虎榜近期机构净买入 +{net_inst}亿 / 游资净卖出 {net_hotm}亿 — smart money 吸筹，游资派发")
        elif abs(net_inst) > 1:
            direction = "净买入" if net_inst > 0 else "净卖出"
            interp.append(f"龙虎榜机构{direction} {net_inst}亿")

    mfy = out.get("money_flow", {})
    if isinstance(mfy, dict) and mfy.get("main_net_20d_yi") is not None:
        n20 = mfy["main_net_20d_yi"]
        if n20 > 5:
            interp.append(f"主力资金 20日净流入 +{n20}亿 — 持续机构买入")
        elif n20 < -5:
            interp.append(f"主力资金 20日净流出 {n20}亿 — 持续机构卖出")

    out["interpretation"] = interp or ["flow signals neutral or insufficient data"]
    return out


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("ticker")
    parser.add_argument("--lookback", type=int, default=60)
    args = parser.parse_args()
    result = analyze(args.ticker, args.lookback)
    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
