"""Record one paper order from the latest closed candle.

The order stays on the paper account. It is not sent to an exchange.
"""

from __future__ import annotations

from decimal import Decimal


def paper_order(closes: list[Decimal], open_time: int, spec, symbol: str) -> dict:
    from trading_agent.strategy import signal_at

    index = len(closes) - 1
    side, reason = signal_at(closes, index, spec)
    entered = side in ("LONG", "SHORT")
    return {
        "account": "paper-fixture",
        "source": "paper",
        "symbol": symbol,
        "open_time": open_time,
        "signal_close": format(closes[index], "f"),
        "side": side,
        "reason": reason,
        "experiment_id": spec.strategy_id,
        "status": "PAPER_SIGNAL" if entered else "NO_SIGNAL",
        "fill": "PENDING_NEXT_OPEN" if entered else "NONE",
        "live_orders": False,
        "exchange_orders_sent": 0,
    }
