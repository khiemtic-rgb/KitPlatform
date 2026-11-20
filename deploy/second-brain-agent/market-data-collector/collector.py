"""Public market-data collector for Second Brain research.

Binance public klines only. No API key, no account, no orders.
The archive is the research record. It is not a strategy publication
and it is not a paper-trading or live-trading ledger.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

SCHEMA = "research-market-archive/v1"
SOURCE = "BINANCE_PUBLIC_REST"
PUBLIC_KLINES = "https://api.binance.com/api/v3/klines"
PUBLIC_TIME = "https://api.binance.com/api/v3/time"
INTERVAL_MS = {
    "1m": 60_000,
    "5m": 300_000,
    "15m": 900_000,
    "1h": 3_600_000,
    "4h": 14_400_000,
    "1d": 86_400_000,
}
STALE_INTERVALS = 2
BAR_FIELDS = ("open_time", "open", "high", "low", "close", "close_time", "volume")


class CollectorError(RuntimeError):
    pass


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso(moment: datetime) -> str:
    return moment.astimezone(timezone.utc).isoformat()


def canonical_decimal(value) -> str:
    return format(Decimal(str(value)), "f")


def checksum_bars(bars: list[dict]) -> str:
    lines = []
    for bar in bars:
        lines.append("|".join([
            str(int(bar["open_time"])),
            canonical_decimal(bar["open"]),
            canonical_decimal(bar["high"]),
            canonical_decimal(bar["low"]),
            canonical_decimal(bar["close"]),
            str(int(bar["close_time"])),
            canonical_decimal(bar["volume"]),
        ]))
    return hashlib.sha256("\n".join(lines).encode("utf-8")).hexdigest()


def normalize_bar(raw: dict, symbol: str, interval: str, ingested_at: str) -> dict:
    bar = {
        "symbol": symbol,
        "interval": interval,
        "source": SOURCE,
        "open_time": int(raw["open_time"]),
        "open": canonical_decimal(raw["open"]),
        "high": canonical_decimal(raw["high"]),
        "low": canonical_decimal(raw["low"]),
        "close": canonical_decimal(raw["close"]),
        "close_time": int(raw["close_time"]),
        "volume": canonical_decimal(raw["volume"]),
        "ingested_at": ingested_at,
    }
    invalid = (
        Decimal(bar["high"]) < max(Decimal(bar["open"]), Decimal(bar["close"]))
        or Decimal(bar["low"]) > min(Decimal(bar["open"]), Decimal(bar["close"]))
        or Decimal(bar["high"]) < Decimal(bar["low"])
        or bar["close_time"] <= bar["open_time"]
    )
    if invalid:
        raise CollectorError(f"invalid candle at {bar['open_time']}")
    return bar


def bar_identity(bar: dict) -> tuple:
    return tuple(bar[field] if field in ("open_time", "close_time") else canonical_decimal(bar[field]) for field in BAR_FIELDS)


def gap_ranges(bars: list[dict], interval_ms: int) -> list[tuple[int, int]]:
    ranges = []
    for left, right in zip(bars, bars[1:]):
        start = int(left["open_time"]) + interval_ms
        end = int(right["open_time"]) - interval_ms
        if start <= end:
            ranges.append((start, end))
    return ranges


def closed_only(bars: list[dict], now_ms: int, interval_ms: int) -> list[dict]:
    return [bar for bar in bars if int(bar["open_time"]) + interval_ms <= now_ms]


def merge_bars(existing: list[dict], incoming: list[dict]) -> tuple[list[dict], int, int, int]:
    by_open = {int(bar["open_time"]): bar for bar in existing}
    inserted = 0
    duplicates = 0
    conflicts = 0
    for bar in incoming:
        open_time = int(bar["open_time"])
        current = by_open.get(open_time)
        if current is None:
            by_open[open_time] = bar
            inserted += 1
            continue
        if bar_identity(current) == bar_identity(bar):
            duplicates += 1
            continue
        conflicts += 1
    merged = [by_open[key] for key in sorted(by_open)]
    return merged, inserted, duplicates, conflicts


def assess(bars: list[dict], interval_ms: int, now_ms: int, last_error: str | None) -> dict:
    gaps = gap_ranges(bars, interval_ms)
    if not bars:
        status = "ERROR" if last_error else "EMPTY"
        return {
            "status": status,
            "stale": True,
            "gap_count": 0,
            "age_ms": None,
            "expected_last_open_time": None,
            "last_open_time": None,
        }
    last_open = int(bars[-1]["open_time"])
    expected = (now_ms // interval_ms) * interval_ms - interval_ms
    age = expected - last_open
    stale = age >= STALE_INTERVALS * interval_ms
    if last_error:
        status = "ERROR"
    elif gaps:
        status = "GAP"
    elif stale:
        status = "STALE"
    else:
        status = "OK"
    return {
        "status": status,
        "stale": stale,
        "gap_count": len(gaps),
        "age_ms": age,
        "expected_last_open_time": expected,
        "last_open_time": last_open,
    }


def kline_to_raw(row: list) -> dict:
    return {
        "open_time": int(row[0]),
        "open": row[1],
        "high": row[2],
        "low": row[3],
        "close": row[4],
        "volume": row[5],
        "close_time": int(row[6]),
    }


def fetch_public_klines(symbol: str, interval: str, start_ms: int, end_ms: int, fetch=None, max_pages: int = 50) -> list[dict]:
    if start_ms > end_ms:
        return []
    fetch = fetch or _http_get
    interval_ms = INTERVAL_MS[interval]
    cursor = start_ms
    pages = 0
    found = []
    while cursor <= end_ms and pages < max_pages:
        query = urllib.parse.urlencode({
            "symbol": symbol,
            "interval": interval,
            "startTime": cursor,
            "endTime": end_ms,
            "limit": 1000,
        })
        payload = fetch(f"{PUBLIC_KLINES}?{query}")
        rows = json.loads(payload)
        pages += 1
        if not rows:
            break
        for row in rows:
            raw = kline_to_raw(row)
            if start_ms <= raw["open_time"] <= end_ms:
                found.append(raw)
        last_open = int(rows[-1][0])
        next_cursor = last_open + interval_ms
        if next_cursor <= cursor or len(rows) < 1000:
            break
        cursor = next_cursor
    return found


def _http_get(url: str) -> str:
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https" or parsed.netloc != "api.binance.com":
        raise CollectorError("refusing non-public Binance host")
    if "api_key" in parsed.query.lower() or "signature" in parsed.query.lower():
        raise CollectorError("refusing signed request")
    request = urllib.request.Request(url, headers={"User-Agent": "second-brain-market-data/1"}, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return response.read().decode("utf-8")
    except urllib.error.HTTPError as exc:
        if exc.code == 429:
            time.sleep(2)
            with urllib.request.urlopen(request, timeout=20) as response:
                return response.read().decode("utf-8")
        raise CollectorError(f"public kline http {exc.code}") from exc


def server_now_ms(fetch=None) -> int:
    fetch = fetch or _http_get
    try:
        payload = json.loads(fetch(PUBLIC_TIME))
        return int(payload["serverTime"])
    except Exception:
        return int(utc_now().timestamp() * 1000)


def archive_dir(root: Path, symbol: str, interval: str) -> Path:
    _require_symbol(symbol)
    if interval not in INTERVAL_MS:
        raise CollectorError(f"unsupported interval {interval}")
    return root / symbol / interval


def _require_symbol(symbol: str) -> None:
    if not symbol.isalnum() or not symbol.isupper() or not 5 <= len(symbol) <= 20:
        raise CollectorError(f"unsupported symbol {symbol}")


def load_candles(path: Path) -> list[dict]:
    if not path.exists():
        return []
    bars = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            bars.append(json.loads(line))
    bars.sort(key=lambda bar: int(bar["open_time"]))
    return bars


def write_json_atomic(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(dir=path.parent, prefix=path.name, suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, separators=(",", ":"), sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.remove(temporary)


def write_candles_atomic(path: Path, bars: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(dir=path.parent, prefix=path.name, suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8", newline="\n") as stream:
            for bar in bars:
                stream.write(json.dumps(bar, separators=(",", ":"), sort_keys=True))
                stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.remove(temporary)


def _acquire_lock(root: Path):
    root.mkdir(parents=True, exist_ok=True)
    lock_path = root / ".lock"
    if lock_path.exists():
        try:
            pid = int(lock_path.read_text(encoding="utf-8").strip() or "0")
        except ValueError:
            pid = 0
        if _pid_alive(pid):
            return None
        lock_path.unlink(missing_ok=True)
    try:
        fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_RDWR)
    except FileExistsError:
        return None
    os.write(fd, str(os.getpid()).encode("ascii"))
    return fd, lock_path


def _pid_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    try:
        os.kill(pid, 0)
    except OSError:
        return False
    except SystemError:
        return False
    return True


def _release_lock(lock) -> None:
    if lock is None:
        return
    fd, path = lock
    os.close(fd)
    path.unlink(missing_ok=True)


def load_symbols(root: Path) -> list[dict]:
    path = root / "symbols.json"
    if not path.exists():
        return [{"symbol": "SOLUSDT", "interval": "1h", "history_start_ms": 1784800800000}]
    document = json.loads(path.read_text(encoding="utf-8"))
    symbols = document.get("symbols") or []
    if not symbols:
        raise CollectorError("symbols.json has no symbols")
    return symbols


def import_files(root: Path, symbol: str, interval: str, paths: list[Path], now: datetime | None = None) -> dict:
    moment = now or utc_now()
    incoming = []
    for path in paths:
        incoming.extend(_bars_from_export(path, symbol, interval, iso(moment)))
    return _commit(root, symbol, interval, incoming, moment, origin="import")


def _bars_from_export(path: Path, symbol: str, interval: str, ingested_at: str) -> list[dict]:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    rows = document["bars"] if isinstance(document, dict) and "bars" in document else document
    if not isinstance(rows, list):
        raise CollectorError(f"{path} is not a candle export")
    file_symbol = document.get("symbol", symbol) if isinstance(document, dict) else symbol
    file_interval = document.get("interval", interval) if isinstance(document, dict) else interval
    if file_symbol != symbol or file_interval != interval:
        raise CollectorError(f"{path} is {file_symbol} {file_interval}, not {symbol} {interval}")
    return [normalize_bar(row, symbol, interval, ingested_at) for row in rows]


def sync_symbol(root: Path, spec: dict, fetch=None, now_ms: int | None = None) -> dict:
    symbol = spec["symbol"]
    interval = spec["interval"]
    interval_ms = INTERVAL_MS[interval]
    moment_ms = now_ms if now_ms is not None else server_now_ms(fetch)
    folder = archive_dir(root, symbol, interval)
    existing = load_candles(folder / "candles.jsonl")
    ranges = []
    if existing:
        ranges.extend(gap_ranges(existing, interval_ms))
        tail = int(existing[-1]["open_time"]) + interval_ms
        if tail <= moment_ms - interval_ms:
            ranges.append((tail, moment_ms - interval_ms))
    else:
        start = int(spec.get("history_start_ms") or moment_ms - interval_ms)
        ranges.append((start, moment_ms - interval_ms))
    raw = []
    for start, end in ranges:
        raw.extend(fetch_public_klines(symbol, interval, start, end, fetch=fetch))
    ingested_at = iso(datetime.fromtimestamp(moment_ms / 1000, timezone.utc))
    incoming = [normalize_bar(row, symbol, interval, ingested_at) for row in raw]
    incoming = closed_only(incoming, moment_ms, interval_ms)
    moment = datetime.fromtimestamp(moment_ms / 1000, timezone.utc)
    return _commit(root, symbol, interval, incoming, moment, origin="sync")


def _commit(root: Path, symbol: str, interval: str, incoming: list[dict], moment: datetime, origin: str) -> dict:
    folder = archive_dir(root, symbol, interval)
    existing = load_candles(folder / "candles.jsonl")
    merged, inserted, duplicates, conflicts = merge_bars(existing, incoming)
    digest = checksum_bars(merged) if merged else ""
    health = assess(merged, INTERVAL_MS[interval], int(moment.timestamp() * 1000), None)
    health.update({
        "schema": SCHEMA,
        "symbol": symbol,
        "interval": interval,
        "source": SOURCE,
        "candle_count": len(merged),
        "duplicate_count": duplicates,
        "inserted": inserted,
        "conflicts": conflicts,
        "checksum_sha256": digest,
        "first_open_time": int(merged[0]["open_time"]) if merged else None,
        "checked_at": iso(moment),
        "live_orders": False,
        "exchange_orders_sent": 0,
        "origin": origin,
    })
    manifest = {
        "schema": SCHEMA,
        "symbol": symbol,
        "interval": interval,
        "source": SOURCE,
        "candle_count": len(merged),
        "checksum_sha256": digest,
        "first_open_time": health["first_open_time"],
        "last_open_time": health["last_open_time"],
        "updated_at": iso(moment),
        "live_orders": False,
    }
    write_candles_atomic(folder / "candles.jsonl", merged)
    write_json_atomic(folder / "manifest.json", manifest)
    write_json_atomic(folder / "health.json", health)
    with (folder / "runs.jsonl").open("a", encoding="utf-8", newline="\n") as stream:
        stream.write(json.dumps(health, separators=(",", ":"), sort_keys=True) + "\n")
    return health


def verify_symbol(root: Path, symbol: str, interval: str, now_ms: int | None = None) -> dict:
    folder = archive_dir(root, symbol, interval)
    bars = load_candles(folder / "candles.jsonl")
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    digest = checksum_bars(bars) if bars else ""
    problems = []
    if manifest.get("checksum_sha256") != digest:
        problems.append("CHECKSUM_MISMATCH")
    if manifest.get("candle_count") != len(bars):
        problems.append("COUNT_MISMATCH")
    opens = [int(bar["open_time"]) for bar in bars]
    if opens != sorted(opens) or len(opens) != len(set(opens)):
        problems.append("ORDER_OR_DUPLICATE")
    moment = now_ms if now_ms is not None else int(utc_now().timestamp() * 1000)
    health = assess(bars, INTERVAL_MS[interval], moment, None)
    if health["gap_count"]:
        problems.append("GAP")
    health.update({
        "schema": SCHEMA,
        "symbol": symbol,
        "interval": interval,
        "candle_count": len(bars),
        "checksum_sha256": digest,
        "problems": problems,
        "verified": not problems,
        "live_orders": False,
        "exchange_orders_sent": 0,
    })
    write_json_atomic(folder / "health.json", health)
    return health


def sync_all(root: Path, fetch=None, now_ms: int | None = None) -> list[dict]:
    lock = _acquire_lock(root)
    if lock is None:
        return [{"status": "SKIPPED_LOCKED", "live_orders": False}]
    try:
        results = [sync_symbol(root, spec, fetch=fetch, now_ms=now_ms) for spec in load_symbols(root)]
        rollup = {
            "schema": SCHEMA,
            "checked_at": iso(utc_now()),
            "status": _rollup_status(results),
            "symbols": results,
            "live_orders": False,
            "exchange_orders_sent": 0,
        }
        write_json_atomic(root / "health.json", rollup)
        return results
    finally:
        _release_lock(lock)


def verify_all(root: Path, now_ms: int | None = None) -> list[dict]:
    results = [verify_symbol(root, spec["symbol"], spec["interval"], now_ms=now_ms) for spec in load_symbols(root)]
    write_json_atomic(root / "health.json", {
        "schema": SCHEMA,
        "checked_at": iso(utc_now()),
        "status": _rollup_status(results),
        "symbols": results,
        "live_orders": False,
        "exchange_orders_sent": 0,
    })
    return results


def _rollup_status(results: list[dict]) -> str:
    statuses = [item.get("status", "ERROR") for item in results]
    for status in ("ERROR", "GAP", "STALE", "EMPTY"):
        if status in statuses:
            return status
    if statuses and all(status == "OK" for status in statuses):
        return "OK"
    if "SKIPPED_LOCKED" in statuses:
        return "SKIPPED_LOCKED"
    return "ERROR"


def _print_results(results: list[dict]) -> int:
    exit_code = 0
    for item in results:
        print(json.dumps(item, separators=(",", ":"), sort_keys=True))
        if item.get("status") in {"ERROR", "GAP", "EMPTY"} or item.get("verified") is False:
            exit_code = 2
        if item.get("conflicts"):
            exit_code = 2
    return exit_code


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Second Brain public market-data collector")
    parser.add_argument("--archive", required=True)
    sub = parser.add_subparsers(dest="command", required=True)
    sync = sub.add_parser("sync")
    sync.set_defaults(func=lambda args: sync_all(Path(args.archive)))
    verify = sub.add_parser("verify")
    verify.set_defaults(func=lambda args: verify_all(Path(args.archive)))
    import_command = sub.add_parser("import-files")
    import_command.add_argument("--symbol", required=True)
    import_command.add_argument("--interval", required=True)
    import_command.add_argument("--file", action="append", required=True, dest="files")
    import_command.set_defaults(func=_import_main)
    args = parser.parse_args(argv)
    try:
        results = args.func(args)
    except CollectorError as exc:
        print(json.dumps({"status": "ERROR", "error": str(exc), "live_orders": False}))
        return 1
    return _print_results(results)


def _import_main(args) -> list[dict]:
    result = import_files(Path(args.archive), args.symbol, args.interval, [Path(path) for path in args.files])
    root = Path(args.archive)
    write_json_atomic(root / "health.json", {
        "schema": SCHEMA,
        "checked_at": result["checked_at"],
        "status": result["status"],
        "symbols": [result],
        "live_orders": False,
        "exchange_orders_sent": 0,
    })
    return [result]


if __name__ == "__main__":
    sys.exit(main())
