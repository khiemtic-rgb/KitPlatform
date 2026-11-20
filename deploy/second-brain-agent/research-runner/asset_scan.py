"""Score the locked paper strategies on each recorded asset.

Gold is the Binance public token PAXGUSDT. Silver and oil have no public
Binance kline, so they are recorded as unavailable instead of invented.
A positive out-of-sample row becomes a proposal. Nothing is selected and no
exchange order is sent.
"""

from __future__ import annotations

import json
from decimal import Decimal
from pathlib import Path

import research_task as task

SCAN_SYMBOLS = ("BTCUSDT", "ETHUSDT", "SOLUSDT", "PAXGUSDT")
UNAVAILABLE = (
    {"asset": "silver", "reason": "NO_PUBLIC_BINANCE_KLINE"},
    {"asset": "oil", "reason": "NO_PUBLIC_BINANCE_KLINE"},
)
CONTRACTS = {
    "BTCUSDT": {"min_qty": "0.00001", "step_size": "0.00001", "min_notional": "5", "tick_size": "0.01"},
    "ETHUSDT": {"min_qty": "0.0001", "step_size": "0.0001", "min_notional": "5", "tick_size": "0.01"},
    "SOLUSDT": {"min_qty": "0.001", "step_size": "0.001", "min_notional": "5", "tick_size": "0.01"},
    "PAXGUSDT": {"min_qty": "0.0001", "step_size": "0.0001", "min_notional": "5", "tick_size": "0.01"},
}
MIN_ROWS = 40


def proposals_from_measurements(rows: list[dict]) -> list[dict]:
    proposals = []
    for row in rows:
        if row.get("live_orders") or int(row.get("exchange_orders_sent") or 0) != 0:
            continue
        if Decimal(row["out_of_sample_net_pnl"]) <= 0:
            continue
        proposals.append({
            "proposal_id": "PROP-%s-%s" % (row["symbol"], row["experiment_id"]),
            "kind": "ASSET",
            "symbol": row["symbol"],
            "experiment_id": row["experiment_id"],
            "approval_status": "PROPOSED_NOT_APPROVED",
            "development_net_pnl": row["development_net_pnl"],
            "out_of_sample_net_pnl": row["out_of_sample_net_pnl"],
            "enters_official_evaluation": False,
            "live_orders": False,
            "exchange_orders_sent": 0,
            "confirmation_window": "SAME_SPLIT_NOT_A_LATER_WINDOW",
        })
    return proposals


def scan_archive(archive: Path, lab_root: Path) -> dict:
    task.install_engine(lab_root)
    measured = []
    skipped = []
    for symbol in SCAN_SYMBOLS:
        path = archive / symbol / task.INTERVAL / "candles.jsonl"
        if not path.is_file():
            skipped.append({"symbol": symbol, "reason": "NO_CANDLES"})
            continue
        rows = task.load_candles(path)
        if len(rows) < MIN_ROWS:
            skipped.append({"symbol": symbol, "reason": "SHORT_HISTORY", "rows": len(rows)})
            continue
        if task.gap_count(rows):
            skipped.append({"symbol": symbol, "reason": "ARCHIVE_HAS_GAPS", "rows": len(rows)})
            continue
        cut = int(len(rows) * Decimal("0.70"))
        if cut < 32 or cut >= len(rows) - 2:
            skipped.append({"symbol": symbol, "reason": "CUT_NOT_READY", "rows": len(rows)})
            continue
        snapshot = _snapshot(symbol, rows)
        for item in task.evaluate(snapshot, cut):
            if item.get("live_orders") or int(item.get("exchange_orders_sent") or 0) != 0:
                raise task.TaskBlocked("LIVE_ORDERS")
            measured.append({
                "symbol": symbol,
                "experiment_id": item["experiment_id"],
                "development_net_pnl": item["development"]["net_pnl"],
                "out_of_sample_net_pnl": item["out_of_sample"]["net_pnl"],
                "development_trade_count": item["development"]["trade_count"],
                "out_of_sample_trade_count": item["out_of_sample"]["trade_count"],
                "live_orders": False,
                "exchange_orders_sent": 0,
            })
    proposals = proposals_from_measurements(measured)
    return {
        "lane": "ASSET_SCAN",
        "status": "SCORED",
        "official_catalog_changed": False,
        "selected_strategy": None,
        "promoted": False,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "gold_symbol": "PAXGUSDT",
        "unavailable": list(UNAVAILABLE),
        "skipped": skipped,
        "measured": measured,
        "proposals": proposals,
    }


def scan_and_store(archive: Path, tasks: Path, lab_root: Path) -> dict:
    try:
        document = scan_archive(archive, lab_root)
    except task.TaskBlocked as exc:
        document = _blocked(exc.reason)
    except Exception as exc:
        document = _blocked(exc.__class__.__name__)
    folder = tasks / "asset-proposals"
    task.write_json(folder / "latest.json", document)
    ledger = folder / "measurements.jsonl"
    folder.mkdir(parents=True, exist_ok=True)
    with ledger.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(document, ensure_ascii=False) + "\n")
    return document


def _blocked(reason: str) -> dict:
    return {
        "lane": "ASSET_SCAN",
        "status": "ASSET_SCAN_BLOCKED",
        "error": reason,
        "official_catalog_changed": False,
        "selected_strategy": None,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "unavailable": list(UNAVAILABLE),
        "proposals": [],
        "measured": [],
    }


def _snapshot(symbol: str, rows: list[dict]) -> dict:
    contract = dict(CONTRACTS[symbol])
    contract["source"] = "BINANCE_PUBLIC_REST"
    return {
        "symbol": symbol,
        "interval": task.INTERVAL,
        "interval_ms": task.INTERVAL_MS,
        "data_source": "BINANCE_PUBLIC_REST",
        "quality": "PASS",
        "funding_status": "ABSENT",
        "funding": [],
        "contract": contract,
        "sha256": "asset-scan",
        "bars": rows,
    }
