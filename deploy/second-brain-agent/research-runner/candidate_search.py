"""Score one pre-registered paper variant for each measured parent failure.

The official catalog is not changed. A kept row is still not a selected
strategy and is not confirmed on a later window. No exchange order is sent.
"""

from __future__ import annotations

from decimal import Decimal

import research_review

MAX_ATTEMPTS_PER_PARENT = 1
TREND = "EXP-TREND-SMA-10-30-V1"
BREAKOUT = "EXP-BREAKOUT-DONCHIAN-20-V1"
RANGE = "EXP-REVERSION-RANGE-20-V1"
SLOPE = "SLOW_SLOPE_ALIGN"
CONFIRM = "TWO_CLOSE_CONFIRM"
SLOPE_ID = "EXP-TREND-SMA-10-30-REGIME-V2"
CONFIRM_ID = "EXP-BREAKOUT-DONCHIAN-20-CONFIRM-V2"


def plan_candidates(experiments: list[dict]) -> list[dict]:
    rows = []
    for item in experiments:
        experiment_id = item["experiment_id"]
        development = Decimal(item["development"]["net_pnl"])
        out_of_sample = Decimal(item["out_of_sample"]["net_pnl"])
        base = {
            "parent_experiment_id": experiment_id,
            "parent_development_net_pnl": format(development, "f"),
            "parent_out_of_sample_net_pnl": format(out_of_sample, "f"),
            "attempts": 0,
            "operator": None,
            "experiment_id": None,
            "promoted": False,
            "live_orders": False,
            "exchange_orders_sent": 0,
            "parameters_chosen_on_out_of_sample": False,
        }
        if experiment_id == RANGE and development < 0 and out_of_sample < 0:
            rows.append({**base, "decision": "STOPPED", "reasons": ["NEGATIVE_IN_BOTH_WINDOWS"]})
            continue
        if experiment_id == TREND and development > 0 and out_of_sample < 0:
            rows.append({**base, "decision": "SCORE", "operator": SLOPE, "experiment_id": SLOPE_ID, "attempts": 1})
            continue
        if experiment_id == BREAKOUT and development > 0 and out_of_sample < 0:
            rows.append({**base, "decision": "SCORE", "operator": CONFIRM, "experiment_id": CONFIRM_ID, "attempts": 1})
            continue
        rows.append({**base, "decision": "NO_OPERATOR", "reasons": ["PATTERN_NOT_MATCHED"]})
    return rows


def judge_candidate(parent_out_of_sample: Decimal, result: dict) -> tuple[str, list[str]]:
    reasons = []
    if result.get("parameters_chosen_on_out_of_sample"):
        reasons.append("PARAMETERS_CHOSEN_ON_OUT_OF_SAMPLE")
    if result.get("live_orders") or int(result.get("exchange_orders_sent") or 0) != 0:
        reasons.append("LIVE_ORDERS")
    out_of_sample = Decimal(result["out_of_sample"]["net_pnl"])
    if out_of_sample <= 0:
        reasons.append("OOS_NOT_POSITIVE")
    if out_of_sample <= parent_out_of_sample:
        reasons.append("OOS_NOT_ABOVE_PARENT")
    if reasons:
        return "DROP", reasons
    return "KEEP", []


def score_candidates(snapshot: dict, cut: int, experiments: list[dict]) -> dict:
    from lab import evaluate_experiment, lookahead_ok
    import pipeline

    official = research_review.official_catalog()
    plans = plan_candidates(experiments)
    bars = pipeline.snapshot_to_bars(snapshot)
    rows = []
    for plan in plans:
        if plan["decision"] != "SCORE":
            rows.append(plan)
            continue
        if plan["attempts"] != MAX_ATTEMPTS_PER_PARENT:
            raise RuntimeError("ATTEMPT_LIMIT")
        if plan["experiment_id"] in official:
            raise RuntimeError("CANDIDATE_ENTERED_OFFICIAL_CATALOG")
        spec = _spec(plan["parent_experiment_id"], plan["operator"])
        if spec.strategy_id != plan["experiment_id"] or spec.strategy_id in official:
            raise RuntimeError("CANDIDATE_ENTERED_OFFICIAL_CATALOG")
        if not lookahead_ok(bars, spec):
            rows.append({**plan, "decision": "DROP", "reasons": ["LOOKAHEAD_FAILED"]})
            continue
        result = evaluate_experiment(snapshot, spec, development_cut=cut)
        if result.get("live_orders") or int(result.get("exchange_orders_sent") or 0) != 0:
            raise RuntimeError("LIVE_ORDERS")
        decision, reasons = judge_candidate(Decimal(plan["parent_out_of_sample_net_pnl"]), result)
        rows.append({
            **plan,
            "decision": decision,
            "reasons": reasons,
            "development_net_pnl": result["development"]["net_pnl"],
            "development_trade_count": result["development"]["trade_count"],
            "out_of_sample_net_pnl": result["out_of_sample"]["net_pnl"],
            "out_of_sample_trade_count": result["out_of_sample"]["trade_count"],
            "parameters_chosen_on_out_of_sample": result["parameters_chosen_on_out_of_sample"],
            "approval_status": "CANDIDATE_NOT_APPROVED",
            "performance_claim": "NOT_A_PERFORMANCE_CLAIM",
            "confirmation_window": "SAME_OOS_NOT_A_LATER_WINDOW",
            "promoted": False,
            "live_orders": False,
            "exchange_orders_sent": 0,
        })
    kept = [row["experiment_id"] for row in rows if row["decision"] == "KEEP"]
    return {
        "lane": "CANDIDATE",
        "status": "SCORED",
        "official_catalog": list(official),
        "official_catalog_changed": False,
        "selected_strategy": None,
        "promoted": False,
        "kept": kept,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "confirmation_window": "SAME_OOS_NOT_A_LATER_WINDOW",
        "attempts_per_parent": MAX_ATTEMPTS_PER_PARENT,
        "rows": rows,
    }


def _net_pnl(item: dict, window: str) -> str | None:
    nested = item.get(window)
    if isinstance(nested, dict) and nested.get("net_pnl") is not None:
        return nested["net_pnl"]
    flat = "development_net_pnl" if window == "development" else "out_of_sample_net_pnl"
    return item.get(flat)


def comparison_board(experiments: list[dict], scored: dict, last_open_time: int) -> dict:
    """One paper row per locked strategy and per scored proposal. Nothing is selected."""
    methods = []
    for item in experiments:
        methods.append({
            "lane": "official",
            "experiment_id": item["experiment_id"],
            "decision": "MEASURED",
            "development_net_pnl": _net_pnl(item, "development"),
            "out_of_sample_net_pnl": _net_pnl(item, "out_of_sample"),
        })
    for row in scored.get("rows") or []:
        if row.get("decision") == "NO_OPERATOR":
            continue
        methods.append({
            "lane": "candidate",
            "experiment_id": row.get("experiment_id"),
            "parent_experiment_id": row.get("parent_experiment_id"),
            "decision": row.get("decision"),
            "reasons": row.get("reasons") or [],
            "development_net_pnl": row.get("development_net_pnl"),
            "out_of_sample_net_pnl": row.get("out_of_sample_net_pnl"),
        })
    return {
        "archive_last_open_time": last_open_time,
        "official_catalog_changed": False,
        "selected_strategy": None,
        "promoted": False,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "methods": methods,
    }


def attach_proposal_scores(review: dict, scored: dict) -> dict:
    """Copy the paper score onto the matching proposal. Approval and catalog stay unchanged."""
    by_id = {
        row.get("experiment_id"): row
        for row in scored.get("rows") or []
        if row.get("experiment_id") and row.get("decision") in ("KEEP", "DROP")
    }
    for item in review.get("proposals") or []:
        row = by_id.get(item.get("experiment_id"))
        if row is None:
            continue
        item["paper_decision"] = row["decision"]
        item["paper_reasons"] = row.get("reasons") or []
        item["development_net_pnl"] = row.get("development_net_pnl")
        item["out_of_sample_net_pnl"] = row.get("out_of_sample_net_pnl")
    return review


def _spec(parent_id: str, operator: str):
    from dataclasses import replace
    from trading_agent.strategy import research_specs

    parent = next(spec for spec in research_specs() if spec.strategy_id == parent_id)
    if operator == SLOPE:
        return replace(
            parent,
            strategy_id=SLOPE_ID,
            strategy_version="2",
            require_slow_slope=True,
            hypothesis_text="Chỉ vào lệnh khi độ dốc đường chậm cùng chiều với tín hiệu cắt.",
            invalidation="Đường chậm đi ngược hướng cắt thì không vào.",
        )
    if operator == CONFIRM:
        return replace(
            parent,
            strategy_id=CONFIRM_ID,
            strategy_version="2",
            entry_confirm_bars=2,
            hypothesis_text="Đòi hai nến đóng liên tiếp ngoài vùng trước khi vào.",
            invalidation="Một nến đóng ngoài vùng chưa đủ.",
        )
    raise RuntimeError("OPERATOR_UNKNOWN")
