"""Read development candles and propose one breakout threshold.

The threshold is the median excursion of failed breaks inside the development
window. Out of sample is not used to choose it. The official catalog is unchanged.
"""

from __future__ import annotations

from decimal import Decimal
from statistics import median

LOOKBACK = 20
EXPERIMENT_ID = "EXP-BREAKOUT-DONCHIAN-20-EXCURSION-V2"
PARENT_ID = "EXP-BREAKOUT-DONCHIAN-20-V1"


def measure_breaks(closes: list[Decimal], cut: int, lookback: int = LOOKBACK) -> dict:
    false_excursions = []
    held_excursions = []
    last = cut - 1
    for index in range(lookback, last):
        prior = closes[index - lookback:index]
        now = closes[index]
        nxt = closes[index + 1]
        high = max(prior)
        low = min(prior)
        if now > high and high > 0:
            excursion = (now - high) / high
            if nxt > high:
                held_excursions.append(excursion)
            else:
                false_excursions.append(excursion)
        elif now < low and low > 0:
            excursion = (low - now) / low
            if nxt < low:
                held_excursions.append(excursion)
            else:
                false_excursions.append(excursion)
    return {
        "false_breaks": len(false_excursions),
        "held_breaks": len(held_excursions),
        "false_excursions": false_excursions,
    }


def propose_from_candles(closes: list[Decimal], cut: int) -> dict:
    measured = measure_breaks(closes, cut)
    false_count = measured["false_breaks"]
    held_count = measured["held_breaks"]
    base = {
        "source": "DEVELOPMENT_CANDLES",
        "measurement_window": "DEVELOPMENT_ONLY",
        "parent_experiment_id": PARENT_ID,
        "experiment_id": EXPERIMENT_ID,
        "operator": "MIN_BREAK_EXCURSION_FROM_DEVELOPMENT",
        "false_breaks": false_count,
        "held_breaks": held_count,
        "official_catalog_changed": False,
        "parameters_chosen_on_out_of_sample": False,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "promoted": False,
    }
    if false_count < 5:
        return {**base, "decision": "NO_PROPOSAL", "reasons": ["TOO_FEW_DEVELOPMENT_BREAKS"], "min_break_fraction": "0"}
    threshold = median(measured["false_excursions"])
    if threshold <= 0:
        return {**base, "decision": "NO_PROPOSAL", "reasons": ["MEASURED_THRESHOLD_NOT_POSITIVE"], "min_break_fraction": format(threshold, "f")}
    return {
        **base,
        "decision": "SCORE",
        "reasons": [],
        "min_break_fraction": format(threshold, "f"),
    }


def proposal_spec(proposal: dict):
    from dataclasses import replace
    from trading_agent.strategy import research_specs

    parent = next(spec for spec in research_specs() if spec.strategy_id == PARENT_ID)
    return replace(
        parent,
        strategy_id=EXPERIMENT_ID,
        strategy_version="2",
        entry_confirm_bars=1,
        min_break_fraction=Decimal(proposal["min_break_fraction"]),
        hypothesis_text="Phá vùng chỉ được vào khi biên độ vượt mức phá hỏng điển hình đo trong mẫu.",
        invalidation="Phá vùng nhỏ hơn mức đo được thì bỏ.",
    )
