"""Claim one research.evaluate task and write a new publication.

The candle archive is the only market input. The locked research-lab engine
scores SOLUSDT 1h. Development stays on the frozen 30 Oct cut. The run writes
a new publication directory and a task file. It does not call an exchange,
does not read an API key, and does not replace the frozen publication.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

import candidate_search
import candle_proposal
import funding_public
import paper_entry
import research_review

INTERVAL_MS = 3_600_000
FROZEN_PUBLICATION = Path("/opt/second-brain-research-publication-v1")
SYMBOL = "SOLUSDT"
INTERVAL = "1h"
TASK_TYPE = "research.evaluate"


class TaskBlocked(RuntimeError):
    def __init__(self, reason: str):
        super().__init__(reason)
        self.reason = reason


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso(moment: datetime) -> str:
    return moment.astimezone(timezone.utc).isoformat()


def open_time_iso(ms: int) -> str:
    return datetime.fromtimestamp(ms / 1000, timezone.utc).strftime("%Y-%m-%dT%H:%M:%S+00:00")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def load_json(path: Path) -> dict:
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        raw = raw[3:]
    data = json.loads(raw.decode("utf-8"))
    if not isinstance(data, dict):
        raise TaskBlocked("JSON_OBJECT_REQUIRED")
    return data


def load_candles(path: Path) -> list[dict]:
    if not path.is_file():
        raise TaskBlocked("CANDLES_MISSING")
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        row = json.loads(line)
        rows.append({
            "open_time": int(row["open_time"]),
            "open": format(Decimal(row["open"]), "f"),
            "high": format(Decimal(row["high"]), "f"),
            "low": format(Decimal(row["low"]), "f"),
            "close": format(Decimal(row["close"]), "f"),
            "close_time": int(row["close_time"]),
            "volume": format(Decimal(row["volume"]), "f"),
        })
    rows.sort(key=lambda item: item["open_time"])
    return rows


def gap_count(rows: list[dict]) -> int:
    return sum(1 for left, right in zip(rows, rows[1:]) if right["open_time"] - left["open_time"] != INTERVAL_MS)


def archive_coverage(archive: Path) -> dict:
    coverage = {}
    for symbol in ("BTCUSDT", "ETHUSDT", "SOLUSDT"):
        path = archive / symbol / INTERVAL / "candles.jsonl"
        if not path.is_file():
            coverage[symbol] = {"present": False}
            continue
        rows = load_candles(path)
        coverage[symbol] = {
            "present": True,
            "rows": len(rows),
            "gaps": gap_count(rows),
            "first_open_time": rows[0]["open_time"] if rows else None,
            "last_open_time": rows[-1]["open_time"] if rows else None,
        }
    return coverage


def frozen_cut_index(frozen: dict) -> int:
    count = int(frozen.get("record_count") or len(frozen["bars"]))
    return int(count * Decimal("0.70"))


def assert_prefix(rows: list[dict], frozen_bars: list[dict]) -> None:
    if len(rows) < len(frozen_bars):
        raise TaskBlocked("ARCHIVE_SHORTER_THAN_FROZEN_SNAPSHOT")
    for index, frozen_bar in enumerate(frozen_bars):
        row = rows[index]
        if row["open_time"] != int(frozen_bar["open_time"]):
            raise TaskBlocked("ARCHIVE_PREFIX_DIFFERS_FROM_FROZEN_SNAPSHOT")
        for field in ("open", "high", "low", "close"):
            if Decimal(row[field]) != Decimal(frozen_bar[field]):
                raise TaskBlocked("ARCHIVE_PREFIX_DIFFERS_FROM_FROZEN_SNAPSHOT")


def build_snapshot(rows: list[dict], frozen: dict, checksum) -> dict:
    if gap_count(rows):
        raise TaskBlocked("ARCHIVE_HAS_GAPS")
    assert_prefix(rows, frozen["bars"])
    cut = frozen_cut_index(frozen)
    if rows[cut]["open_time"] != int(frozen["bars"][cut]["open_time"]):
        raise TaskBlocked("DEVELOPMENT_CUT_MOVED")
    bars = snapshot_bars(rows)
    return {
        "symbol": SYMBOL,
        "interval": INTERVAL,
        "interval_ms": INTERVAL_MS,
        "data_source": "BINANCE_PUBLIC_REST",
        "quality": "PASS",
        "funding_status": "FETCHED",
        "funding": frozen.get("funding") or [],
        "funding_coverage": "FROZEN_EVENTS_THROUGH_SNAPSHOT_NOT_EXTENDED",
        "contract": frozen["contract"],
        "sha256": checksum(bars),
        "first_open_time": rows[0]["open_time"],
        "last_open_time": rows[-1]["open_time"],
        "record_count": len(rows),
        "bars": rows,
    }


def snapshot_bars(rows: list[dict]):
    from trading_agent.market import Bar

    return [
        Bar(
            open_time=row["open_time"],
            open=Decimal(row["open"]),
            high=Decimal(row["high"]),
            low=Decimal(row["low"]),
            close=Decimal(row["close"]),
            close_time=row["close_time"],
            volume=Decimal(row["volume"]),
        )
        for row in rows
    ]


def install_engine(lab_root: Path) -> None:
    for relative in ("research-lab-v1", "trading-agent-v1", "research-validation-v1"):
        path = lab_root / relative
        if not path.is_dir():
            raise TaskBlocked("RESEARCH_ENGINE_MISSING")
        entry = str(path)
        if entry not in sys.path:
            sys.path.insert(0, entry)


def development_anchor(path: Path) -> dict[str, tuple[int, Decimal]]:
    lab = load_json(path)
    anchor = {}
    for item in lab["experiments"]:
        development = item["development"]
        anchor[item["experiment_id"]] = (int(development["trade_count"]), Decimal(development["net_pnl"]))
    return anchor


def assert_reproduces_frozen(experiments: list[dict], anchor: dict[str, tuple[int, Decimal]]) -> None:
    for item in experiments:
        expected = anchor.get(item["experiment_id"])
        if expected is None:
            raise TaskBlocked("DEVELOPMENT_ANCHOR_MISSING")
        actual = (int(item["development"]["trade_count"]), Decimal(item["development"]["net_pnl"]))
        if actual != expected:
            raise TaskBlocked("FROZEN_SNAPSHOT_NOT_REPRODUCED")
        assert_not_selected(item)


def assert_not_selected(item: dict) -> None:
    if item["selected_by_pnl"] or item["parameters_chosen_on_out_of_sample"] or item["live_orders"]:
        raise TaskBlocked("SELECTION_OR_LIVE_ORDER_REFUSED")
    if item["exchange_orders_sent"] != 0:
        raise TaskBlocked("SELECTION_OR_LIVE_ORDER_REFUSED")


def verdict_for(experiments: list[dict]) -> str:
    if not experiments:
        return "INSUFFICIENT_DATA"
    for item in experiments:
        if not item.get("development") or not item.get("out_of_sample"):
            return "INSUFFICIENT_DATA"
        if item.get("approval_status") != "EXPERIMENT_NOT_APPROVED":
            return "INSUFFICIENT_DATA"
        if item.get("performance_claim") != "NOT_A_PERFORMANCE_CLAIM":
            return "INSUFFICIENT_DATA"
    return "NOT_SELECTABLE"


def evaluate(snapshot: dict, cut: int) -> list[dict]:
    from lab import evaluate_experiment, lookahead_ok
    from trading_agent.strategy import research_specs
    import pipeline

    specs = research_specs()
    ids = tuple(spec.strategy_id for spec in specs)
    if ids != research_review.official_catalog():
        raise TaskBlocked("STRATEGY_NOT_APPROVED")
    bars = pipeline.snapshot_to_bars(snapshot)
    stored = []
    for spec in specs:
        if not research_review.admit_for_official_evaluation(spec.strategy_id):
            raise TaskBlocked("STRATEGY_NOT_APPROVED")
        if not lookahead_ok(bars, spec):
            raise TaskBlocked("LOOKAHEAD_FAILED")
        result = evaluate_experiment(snapshot, spec, development_cut=cut)
        result.pop("events", None)
        stored.append(result)
    return stored


def lab_document(snapshot: dict, experiments: list[dict], cut: int, code_sha: str) -> dict:
    return {
        "parent_strategy_id": "EXP-SMA-CROSS-V1",
        "symbol": SYMBOL,
        "snapshot_sha256": snapshot["sha256"],
        "snapshot_selection": "FROZEN_DEVELOPMENT_CUT_EXTENDED_OUT_OF_SAMPLE",
        "funding_coverage": snapshot["funding_coverage"],
        "development_cut_open_time": snapshot["bars"][cut]["open_time"],
        "archive_last_open_time": snapshot["last_open_time"],
        "record_count": snapshot["record_count"],
        "experiments": experiments,
        "selected_strategy": None,
        "evidence_status": "INSUFFICIENT_EVIDENCE",
        "approval_status": "EXPERIMENT_NOT_APPROVED",
        "performance_claim": "NOT_A_PERFORMANCE_CLAIM",
        "live_orders": False,
        "exchange_orders_sent": 0,
        "code_sha256": code_sha,
    }


def paper_documents(lab: dict, generated_at: str, age_ms: int) -> tuple[dict, dict, dict]:
    health_name = "OK" if 0 <= age_ms <= 2 * INTERVAL_MS else "STALE"
    experiment_ids = [item["experiment_id"] for item in lab["experiments"]]
    status = {
        "runner_status": "SUCCEEDED",
        "cycle_outcome": "RESEARCH_EVALUATED",
        "last_run_at": generated_at,
        "last_processed_open_time": lab["archive_last_open_time"],
        "last_error": None,
        "data_gaps": [],
        "rejected": [],
        "strategy_errors": {},
        "extension_count": lab["bars_after_frozen_snapshot"],
        "extension_sha256": lab["snapshot_sha256"],
        "journal_added": {item: 0 for item in experiment_ids},
        "experiments": experiment_ids,
        "capital_usd": "100",
        "risk_limits_approval": "PROPOSED_NOT_APPROVED",
        "fee_status": "ASSUMPTION_NOT_ACCOUNT_SCHEDULE",
        "assumed_fee_rate": "0.0005",
        "slippage_status": "ASSUMPTION",
        "assumed_slippage_rate": "0.0001",
        "fill_model": "NEXT_OPEN_AND_COMPLETED_BAR_STOP",
        "exchange_orders_sent": 0,
        "live_orders": False,
        "effective_live_mode": "LIVE_DISABLED",
        "continuous_schedule": "TASK_CLAIM",
        "frozen_snapshot_sha256": "126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3",
        "strategy_code_sha256": lab["code_sha256"],
        "mode": "ONCE",
        "selected_strategy": None,
    }
    strategies = []
    for item in lab["experiments"]:
        observed = item["observed"]
        oos_signals = item["out_of_sample"]["signals"]
        oos_bars = int(oos_signals["LONG"]) + int(oos_signals["SHORT"]) + int(oos_signals["NO_TRADE"])
        strategies.append({
            "experiment_id": item["experiment_id"],
            "extension_bars_represented": oos_bars,
            "no_signal_cycles": int(oos_signals["NO_TRADE"]),
            "simulated_entries": int(observed["simulated_fills"]),
            "closed_trades": int(observed["trade_count"]),
            "open_trades": 0,
            "risk_blocks": int(observed["blocked_entries"]),
            "no_trade_is_not_a_win_or_loss": True,
            "net_pnl": observed["net_pnl"],
            "profit_factor": observed["profit_factor"],
            "max_drawdown_fraction": observed["max_drawdown_fraction"],
            "expectancy": "SEE_LAB_RESULT",
            "fees": observed["fees"],
            "funding": observed["funding"],
            "slippage": "ASSUMPTION_0.0001_INSIDE_SIMULATED_FILL_PRICE",
            "stand_aside_net_pnl": "0",
            "in_sample_out_of_sample": "SEE_LAB_RESULT_DEVELOPMENT_AND_OUT_OF_SAMPLE",
            "sample_warning": "INSUFFICIENT_SAMPLE_DO_NOT_SELECT",
            "fill_model": observed["fill_model"],
            "signal_time": "CLOSE_OF_DECISION_BAR",
            "assumed_execution_time": "NEXT_BAR_OPEN",
            "exchange_fill": False,
        })
    report = {
        "report_kind": "RESEARCH_EVALUATION_NOT_A_SELECTION",
        "symbol": SYMBOL,
        "timeframe": INTERVAL,
        "extension_bar_count": lab["record_count"],
        "extension_start_utc": open_time_iso(lab["development_cut_open_time"]),
        "extension_end_utc": open_time_iso(lab["archive_last_open_time"]),
        "continuity_breaks": [],
        "strategies": strategies,
        "stand_aside": {"net_pnl": "0", "trade_count": 0},
        "performance_claim": "NOT_A_PERFORMANCE_CLAIM",
        "selected_strategy": None,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "effective_live_mode": "LIVE_DISABLED",
    }
    health = {
        "health": health_name,
        "runner_status": "SUCCEEDED",
        "cycle_outcome": "RESEARCH_EVALUATED",
        "last_run_at": generated_at,
        "last_bar_age_ms": age_ms,
        "last_error": None,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "effective_live_mode": "LIVE_DISABLED",
    }
    return status, report, health


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def assert_destination(destination: Path, frozen_publication: Path) -> None:
    dest = destination.resolve()
    frozen = frozen_publication.resolve()
    if dest == frozen or frozen in dest.parents or dest in frozen.parents:
        raise TaskBlocked("REFUSING_FROZEN_PUBLICATION")


def refuse_exchange_key() -> None:
    for name in ("BINANCE_API_KEY", "BINANCE_API_SECRET", "BINANCE_SECRET"):
        if os.environ.get(name):
            raise TaskBlocked("REFUSING_EXCHANGE_KEY")


def completed_matches(tasks: Path, last_open_time: int, funding_sha: str) -> bool:
    folder = tasks / "completed"
    if not folder.is_dir():
        return False
    for path in folder.glob("*.json"):
        row = load_json(path)
        if row.get("status") != "completed":
            continue
        if int(row.get("archive_last_open_time") or -1) != last_open_time:
            continue
        if row.get("funding_sha256") == funding_sha:
            return True
    return False


def task_record(task_id: str, status: str, coverage: dict, last_open_time: int, extra: dict | None = None) -> dict:
    row = {
        "task_id": task_id,
        "task_type": TASK_TYPE,
        "status": status,
        "symbol": SYMBOL,
        "interval": INTERVAL,
        "archive_last_open_time": last_open_time,
        "coverage": coverage,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "selected_strategy": None,
    }
    if extra:
        row.update(extra)
    return row


def store_task(tasks: Path, status: str, row: dict) -> Path:
    folder = tasks / status
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / (row["task_id"] + ".json")
    write_json(path, row)
    return path


def series_status(rows: list[dict], now_ms: int) -> dict:
    duplicates = sum(1 for left, right in zip(rows, rows[1:]) if right["open_time"] == left["open_time"])
    last = rows[-1]["open_time"] if rows else None
    return {
        "rows": len(rows),
        "gaps": gap_count(rows),
        "duplicate_open_times": duplicates,
        "first_open_time": rows[0]["open_time"] if rows else None,
        "last_open_time": last,
        "age_ms": None if last is None else now_ms - (last + INTERVAL_MS),
        "complete": bool(rows) and gap_count(rows) == 0 and duplicates == 0,
    }


def candle_quality(archive: Path, now_ms: int) -> dict:
    symbols = {}
    for symbol in ("BTCUSDT", "ETHUSDT", "SOLUSDT"):
        path = archive / symbol / INTERVAL / "candles.jsonl"
        rows = load_candles(path) if path.is_file() else []
        symbols[symbol] = series_status(rows, now_ms)
    present = [symbols[name] for name in symbols if symbols[name]["rows"]]
    aligned = len(present) == 3 and len({(item["rows"], item["first_open_time"], item["last_open_time"]) for item in present}) == 1
    return {
        "symbols": symbols,
        "aligned": aligned,
        "complete": aligned and all(item["complete"] for item in present),
    }


def publication_hashes(root: Path) -> dict:
    names = (
        "manifest.json",
        "lab/LAB_RESULT.json",
        "paper/PAPER_OPS_STATUS.json",
        "paper/PAPER_RESEARCH_REPORT.json",
        "paper/PAPER_OPS_HEALTH.json",
    )
    return {name: hashlib.sha256((root / name).read_bytes()).hexdigest() for name in names}


def append_journal(publications: Path, row: dict) -> None:
    path = publications / "journal.jsonl"
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(row, ensure_ascii=False) + "\n")


def write_latest(publications: Path, publication_id: str, publication_path: Path) -> None:
    payload = {
        "publicationId": publication_id,
        "path": str(publication_path),
        "kind": "POINTER_NOT_A_PUBLICATION",
        "live_orders": False,
    }
    temporary = publications / ".latest.json.tmp"
    temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(publications / "latest.json")


def publish_immutable(publications: Path, sources: dict, published_at: str, frozen_publication: Path, task_id: str) -> dict:
    from publish_research_v1 import publish

    staging = publications / ".staging" / task_id
    if staging.exists():
        shutil.rmtree(staging)
    manifest = publish(staging, sources, published_at)
    publication_id = manifest["publicationId"]
    if not publication_id.startswith("pub-") or any(token in publication_id for token in ("/", "\\", ".")):
        raise TaskBlocked("PUBLICATION_ID_UNSAFE")
    final = publications / publication_id
    assert_destination(final, frozen_publication)
    created = not final.exists()
    if final.exists():
        if publication_hashes(final) != publication_hashes(staging):
            raise TaskBlocked("PUBLICATION_ID_COLLISION")
        shutil.rmtree(staging)
    else:
        staging.rename(final)
        append_journal(publications, {
            "publicationId": publication_id,
            "path": str(final),
            "publishedAt": published_at,
            "task_id": task_id,
            "live_orders": False,
            "exchange_orders_sent": 0,
        })
    write_latest(publications, publication_id, final)
    manifest["created"] = created
    manifest["path"] = str(final)
    return manifest


def seal_legacy(publications: Path, frozen_publication: Path) -> dict | None:
    slot = publications / "SOLUSDT-1h"
    if not (slot / "manifest.json").is_file():
        return None
    before = publication_hashes(slot)
    manifest = load_json(slot / "manifest.json")
    publication_id = manifest["publicationId"]
    final = publications / publication_id
    assert_destination(final, frozen_publication)
    if final.exists():
        if publication_hashes(slot) != before:
            raise TaskBlocked("LEGACY_SLOT_CHANGED")
        return {"publicationId": publication_id, "sealed": False}
    staging = publications / ".staging" / ("seal-" + publication_id)
    if staging.exists():
        shutil.rmtree(staging)
    shutil.copytree(slot, staging)
    if publication_hashes(staging) != before or publication_hashes(slot) != before:
        raise TaskBlocked("SEAL_COPY_MISMATCH")
    staging.rename(final)
    append_journal(publications, {
        "publicationId": publication_id,
        "path": str(final),
        "sealed_from": "SOLUSDT-1h",
        "live_orders": False,
        "exchange_orders_sent": 0,
    })
    if not (publications / "latest.json").exists():
        write_latest(publications, publication_id, final)
    return {"publicationId": publication_id, "sealed": True}


def funding_bundle(funding_dir: Path, frozen: dict, first_open: int, last_open: int) -> dict:
    symbols = {}
    complete = True
    for symbol in ("BTCUSDT", "ETHUSDT", "SOLUSDT"):
        events = funding_public.load_funding_file(funding_dir / (symbol + ".json"))
        gaps = funding_public.funding_gaps(events, first_open, last_open) if events else [{"reason": "FILE_MISSING"}]
        symbols[symbol] = {"events": len(events), "gaps": gaps, "complete": events and not gaps}
        if not symbols[symbol]["complete"]:
            complete = False
    merged = []
    if symbols["SOLUSDT"]["complete"]:
        try:
            merged = funding_public.merge_funding(frozen.get("funding") or [], funding_public.load_funding_file(funding_dir / "SOLUSDT.json"))
        except funding_public.FundingError as exc:
            symbols["SOLUSDT"]["complete"] = False
            symbols["SOLUSDT"]["error"] = str(exc)
            complete = False
    encoded = json.dumps(merged, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return {
        "symbols": symbols,
        "complete": complete and bool(merged),
        "merged": merged,
        "sha256": hashlib.sha256(encoded).hexdigest(),
    }


def run_task(archive: Path, publications: Path, tasks: Path, lab_root: Path, frozen_snapshot: Path, anchor_path: Path, frozen_publication: Path, funding_dir: Path, now: datetime | None = None) -> dict:
    refuse_exchange_key()
    assert_destination(publications, frozen_publication)
    backfill_comparison_history(tasks)
    import asset_scan
    asset_scan.scan_and_store(archive, tasks, lab_root)
    moment = now or utc_now()
    now_ms = int(moment.timestamp() * 1000)
    candles = candle_quality(archive, now_ms)
    write_json(tasks / "quality" / "candles.json", candles)
    if not candles["complete"]:
        raise TaskBlocked("CANDLES_INCOMPLETE")
    sol = candles["symbols"]["SOLUSDT"]
    last_open_time = int(sol["last_open_time"])
    frozen = load_json(frozen_snapshot)
    funding = funding_bundle(funding_dir, frozen, int(sol["first_open_time"]), last_open_time)
    write_json(tasks / "quality" / "funding.json", {key: value for key, value in funding.items() if key != "merged"})
    seal = seal_legacy(publications, frozen_publication)
    if not funding["complete"]:
        return {
            "status": "FUNDING_INCOMPLETE",
            "archive_last_open_time": last_open_time,
            "sealed": seal,
            "candles_complete": True,
            "funding": funding["symbols"],
        }
    task_id = "research-evaluate-%s-%s" % (last_open_time, funding["sha256"][:12])
    if completed_matches(tasks, last_open_time, funding["sha256"]):
        return {"status": "NO_NEW_BAR", "task_id": task_id, "archive_last_open_time": last_open_time, "sealed": seal}
    claimed = task_record(task_id, "running", candles, last_open_time, {"claimed_at": iso(moment), "funding_sha256": funding["sha256"]})
    store_task(tasks, "running", claimed)
    install_engine(lab_root)
    import pipeline

    rows = load_candles(archive / SYMBOL / INTERVAL / "candles.jsonl")
    snapshot = build_snapshot(rows, frozen, pipeline.checksum_bars)
    snapshot["funding"] = funding["merged"]
    snapshot["funding_coverage"] = "PUBLIC_FUNDING_COMPLETE"
    snapshot["funding_status"] = "FETCHED"
    cut = frozen_cut_index(frozen)
    frozen_only = evaluate(frozen, cut)
    assert_reproduces_frozen(frozen_only, development_anchor(anchor_path))
    experiments = evaluate(snapshot, cut)
    for item in experiments:
        assert_not_selected(item)
    code_sha = hashlib.sha256((lab_root / "trading-agent-v1" / "trading_agent" / "strategy.py").read_bytes()).hexdigest()
    lab = lab_document(snapshot, experiments, cut, code_sha)
    lab["bars_after_frozen_snapshot"] = snapshot["record_count"] - int(frozen["record_count"])
    lab["funding_event_count"] = len(funding["merged"])
    verdict = verdict_for(experiments)
    if verdict != "NOT_SELECTABLE":
        raise TaskBlocked("VERDICT_" + verdict)
    age_ms = now_ms - (last_open_time + INTERVAL_MS)
    status, report, health = paper_documents(lab, iso(moment), age_ms)
    work = tasks / "work" / task_id
    write_json(work / "LAB_RESULT.json", lab)
    write_json(work / "PAPER_OPS_STATUS.json", status)
    write_json(work / "PAPER_RESEARCH_REPORT.json", report)
    write_json(work / "PAPER_OPS_HEALTH.json", health)
    review = research_review.review_experiments(experiments, "PUBLIC_FUNDING_COMPLETE")
    if any(item.get("enters_official_evaluation") for item in review["proposals"] if item["kind"] == "STRATEGY"):
        raise TaskBlocked("PROPOSAL_ENTERED_OFFICIAL_EVALUATION")
    candidate = _candidate_lane(snapshot, cut, experiments)
    candidate_search.attach_proposal_scores(review, candidate)
    write_json(tasks / "proposals" / (task_id + ".json"), review)
    write_json(tasks / "candidates" / (task_id + ".json"), candidate)
    board = candidate_search.comparison_board(experiments, candidate, last_open_time)
    append_comparison_history(tasks, board)
    manifest = publish_immutable(
        publications,
        {
            "lab": work / "LAB_RESULT.json",
            "status": work / "PAPER_OPS_STATUS.json",
            "report": work / "PAPER_RESEARCH_REPORT.json",
            "health": work / "PAPER_OPS_HEALTH.json",
        },
        iso(moment),
        frozen_publication,
        task_id,
    )
    finished = task_record(task_id, "completed", candles, last_open_time, {
        "claimed_at": claimed["claimed_at"],
        "completed_at": iso(utc_now()),
        "publication_id": manifest["publicationId"],
        "publication_path": manifest["path"],
        "publication_created": manifest["created"],
        "verdict": verdict,
        "funding_sha256": funding["sha256"],
        "funding_coverage": "PUBLIC_FUNDING_COMPLETE",
        "lab_sha256": manifest["labResultSha256"],
        "development_cut_open_time": lab["development_cut_open_time"],
        "sealed": seal,
        "host_read": research_review.host_read_constraints(),
        "candidate_status": candidate["status"],
        "candidate_kept": candidate.get("kept") or [],
        "candidate_promoted": False,
        "experiments": [
            {
                "experiment_id": item["experiment_id"],
                "development_net_pnl": item["development"]["net_pnl"],
                "development_trade_count": item["development"]["trade_count"],
                "out_of_sample_net_pnl": item["out_of_sample"]["net_pnl"],
                "out_of_sample_trade_count": item["out_of_sample"]["trade_count"],
            }
            for item in experiments
        ],
    })
    store_task(tasks, "completed", finished)
    running = tasks / "running" / (task_id + ".json")
    if running.exists():
        running.unlink()
    return finished


def append_comparison_history(tasks: Path, record: dict) -> bool:
    """Append one bar. The same open time is not written twice."""
    history = tasks / "history"
    history.mkdir(parents=True, exist_ok=True)
    ledger = history / "comparison.jsonl"
    key = record["archive_last_open_time"]
    if ledger.exists():
        lines = [line for line in ledger.read_text(encoding="utf-8").splitlines() if line.strip()]
        if lines and json.loads(lines[-1]).get("archive_last_open_time") == key:
            write_json(history / "latest.json", record)
            return False
        if any(json.loads(line).get("archive_last_open_time") == key for line in lines):
            write_json(history / "latest.json", json.loads(lines[-1]))
            return False
    with ledger.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, ensure_ascii=False) + "\n")
    write_json(history / "latest.json", record)
    return True


def backfill_comparison_history(tasks: Path) -> int:
    """Build the ledger from completed runs that already scored proposals."""
    written = 0
    completed = tasks / "completed"
    candidates = tasks / "candidates"
    if not completed.is_dir() or not candidates.is_dir():
        return 0
    for path in sorted(candidates.glob("research-evaluate-*.json")):
        scored = load_json(path)
        finished_path = completed / path.name
        if not finished_path.exists():
            continue
        finished = load_json(finished_path)
        experiments = finished.get("experiments") or []
        if not experiments:
            continue
        board = candidate_search.comparison_board(experiments, scored, int(finished["archive_last_open_time"]))
        if append_comparison_history(tasks, board):
            written += 1
    return written


def _candidate_lane(snapshot: dict, cut: int, experiments: list[dict]) -> dict:
    try:
        return candidate_search.score_candidates(snapshot, cut, experiments)
    except Exception as exc:
        reason = exc.args[0] if exc.args and isinstance(exc.args[0], str) and len(exc.args[0]) < 80 else exc.__class__.__name__
        return {
            "lane": "CANDIDATE",
            "status": "CANDIDATE_FAILED",
            "error": reason,
            "official_catalog": list(research_review.official_catalog()),
            "official_catalog_changed": False,
            "selected_strategy": None,
            "promoted": False,
            "kept": [],
            "live_orders": False,
            "exchange_orders_sent": 0,
            "rows": [],
        }


def _prepared_research(archive: Path, tasks: Path, lab_root: Path, frozen_snapshot: Path, anchor_path: Path, frozen_publication: Path, funding_dir: Path, now: datetime | None) -> dict:
    refuse_exchange_key()
    assert_destination(tasks, frozen_publication)
    moment = now or utc_now()
    now_ms = int(moment.timestamp() * 1000)
    candles = candle_quality(archive, now_ms)
    if not candles["complete"]:
        raise TaskBlocked("CANDLES_INCOMPLETE")
    sol = candles["symbols"]["SOLUSDT"]
    last_open_time = int(sol["last_open_time"])
    frozen = load_json(frozen_snapshot)
    funding = funding_bundle(funding_dir, frozen, int(sol["first_open_time"]), last_open_time)
    if not funding["complete"]:
        raise TaskBlocked("FUNDING_INCOMPLETE")
    install_engine(lab_root)
    import pipeline

    rows = load_candles(archive / SYMBOL / INTERVAL / "candles.jsonl")
    snapshot = build_snapshot(rows, frozen, pipeline.checksum_bars)
    snapshot["funding"] = funding["merged"]
    snapshot["funding_coverage"] = "PUBLIC_FUNDING_COMPLETE"
    snapshot["funding_status"] = "FETCHED"
    cut = frozen_cut_index(frozen)
    frozen_only = evaluate(frozen, cut)
    assert_reproduces_frozen(frozen_only, development_anchor(anchor_path))
    experiments = evaluate(snapshot, cut)
    for item in experiments:
        assert_not_selected(item)
    verdict = verdict_for(experiments)
    if verdict != "NOT_SELECTABLE":
        raise TaskBlocked("VERDICT_" + verdict)
    return {
        "snapshot": snapshot,
        "cut": cut,
        "experiments": experiments,
        "verdict": verdict,
        "last_open_time": last_open_time,
    }


def run_candidates(archive: Path, tasks: Path, lab_root: Path, frozen_snapshot: Path, anchor_path: Path, frozen_publication: Path, funding_dir: Path, now: datetime | None = None) -> dict:
    prepared = _prepared_research(archive, tasks, lab_root, frozen_snapshot, anchor_path, frozen_publication, funding_dir, now)
    snapshot = prepared["snapshot"]
    cut = prepared["cut"]
    experiments = prepared["experiments"]
    verdict = prepared["verdict"]
    last_open_time = prepared["last_open_time"]
    scored = candidate_search.score_candidates(snapshot, cut, experiments)
    scored["archive_last_open_time"] = last_open_time
    scored["snapshot_sha256"] = snapshot["sha256"]
    scored["official_verdict"] = verdict
    path = tasks / "candidates" / ("candidate-%s.json" % last_open_time)
    write_json(path, scored)
    scored["path"] = str(path)
    return scored


def run_propose(archive: Path, tasks: Path, lab_root: Path, frozen_snapshot: Path, anchor_path: Path, frozen_publication: Path, funding_dir: Path, now: datetime | None = None) -> dict:
    prepared = _prepared_research(archive, tasks, lab_root, frozen_snapshot, anchor_path, frozen_publication, funding_dir, now)
    snapshot = prepared["snapshot"]
    cut = prepared["cut"]
    experiments = prepared["experiments"]
    closes = [Decimal(row["close"]) for row in snapshot["bars"]]
    proposal = candle_proposal.propose_from_candles(closes, cut)
    parent = next(item for item in experiments if item["experiment_id"] == candle_proposal.PARENT_ID)
    order = {
        "account": "paper-fixture",
        "source": "paper",
        "status": "NO_SIGNAL",
        "reason": "NO_PROPOSAL",
        "live_orders": False,
        "exchange_orders_sent": 0,
    }
    if proposal["decision"] == "SCORE":
        if proposal["experiment_id"] in research_review.official_catalog():
            raise TaskBlocked("CANDIDATE_ENTERED_OFFICIAL_CATALOG")
        from lab import evaluate_experiment, lookahead_ok
        import pipeline

        spec = candle_proposal.proposal_spec(proposal)
        bars = pipeline.snapshot_to_bars(snapshot)
        if not lookahead_ok(bars, spec):
            proposal["decision"] = "DROP"
            proposal["reasons"] = ["LOOKAHEAD_FAILED"]
        else:
            result = evaluate_experiment(snapshot, spec, development_cut=cut)
            if result.get("live_orders") or int(result.get("exchange_orders_sent") or 0) != 0:
                raise TaskBlocked("LIVE_ORDERS")
            decision, reasons = candidate_search.judge_candidate(Decimal(parent["out_of_sample"]["net_pnl"]), result)
            proposal["decision"] = decision
            proposal["reasons"] = reasons
            proposal["development_net_pnl"] = result["development"]["net_pnl"]
            proposal["development_trade_count"] = result["development"]["trade_count"]
            proposal["out_of_sample_net_pnl"] = result["out_of_sample"]["net_pnl"]
            proposal["out_of_sample_trade_count"] = result["out_of_sample"]["trade_count"]
            proposal["parent_out_of_sample_net_pnl"] = parent["out_of_sample"]["net_pnl"]
        order = paper_entry.paper_order(closes, int(snapshot["bars"][-1]["open_time"]), spec, SYMBOL)
    payload = {
        "lane": "CANDLE_PROPOSAL",
        "official_verdict": prepared["verdict"],
        "official_catalog_changed": False,
        "selected_strategy": None,
        "promoted": False,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "archive_last_open_time": prepared["last_open_time"],
        "snapshot_sha256": snapshot["sha256"],
        "proposal": proposal,
        "paper_order": order,
    }
    path = tasks / "candle-proposals" / ("proposal-%s.json" % prepared["last_open_time"])
    write_json(path, payload)
    if order.get("status") == "PAPER_SIGNAL":
        write_json(tasks / "paper-orders" / ("%s-%s.json" % (order["experiment_id"], order["open_time"])), order)
    payload["path"] = str(path)
    return payload


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Run one research.evaluate task from the local candle archive.")
    parser.add_argument("command", choices=("run", "candidates", "propose"))
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--publications", type=Path)
    parser.add_argument("--tasks", type=Path, required=True)
    parser.add_argument("--lab-root", type=Path, required=True)
    parser.add_argument("--frozen-snapshot", type=Path, required=True)
    parser.add_argument("--anchor", type=Path, required=True)
    parser.add_argument("--frozen-publication", type=Path, default=FROZEN_PUBLICATION)
    parser.add_argument("--funding-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "propose":
            result = run_propose(
                args.archive,
                args.tasks,
                args.lab_root,
                args.frozen_snapshot,
                args.anchor,
                args.frozen_publication,
                args.funding_dir,
            )
        elif args.command == "candidates":
            result = run_candidates(
                args.archive,
                args.tasks,
                args.lab_root,
                args.frozen_snapshot,
                args.anchor,
                args.frozen_publication,
                args.funding_dir,
            )
        else:
            if args.publications is None:
                raise TaskBlocked("PUBLICATIONS_REQUIRED")
            result = run_task(
                args.archive,
                args.publications,
                args.tasks,
                args.lab_root,
                args.frozen_snapshot,
                args.anchor,
                args.frozen_publication,
                args.funding_dir,
            )
    except TaskBlocked as exc:
        print("TASK_BLOCKED %s" % exc.reason)
        return 2
    if args.command == "candidates":
        print("CANDIDATE_%s kept=%s live_orders=%s" % (result["status"], ",".join(result.get("kept") or []) or "none", result["live_orders"]))
        return 0
    if args.command == "propose":
        proposal = result["proposal"]
        order = result["paper_order"]
        print("PROPOSE_%s fraction=%s oos=%s paper=%s live_orders=%s" % (
            proposal["decision"],
            proposal.get("min_break_fraction"),
            proposal.get("out_of_sample_net_pnl"),
            order.get("status"),
            result["live_orders"],
        ))
        return 0
    print("TASK_%s id=%s" % (result["status"], result.get("task_id")))
    if result.get("publication_id"):
        print("publication=%s verdict=%s" % (result["publication_id"], result["verdict"]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
