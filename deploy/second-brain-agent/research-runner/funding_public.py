"""Public USD-M funding history. GET /fapi/v1/fundingRate only.

No API key, no account, no order path. The research task reads the files
this writer leaves on disk. It does not open a network connection itself.
"""

from __future__ import annotations

import json
import urllib.parse
import urllib.request
from decimal import Decimal
from pathlib import Path

HOST = "fapi.binance.com"
PATH = "/fapi/v1/fundingRate"
INTERVAL_MS = 8 * 60 * 60 * 1000
GAP_SLACK_MS = 60_000
MATCH_SLACK_MS = 120_000
SCHEMA = "research-public-funding/v1"


class FundingError(RuntimeError):
    pass


def assert_public_funding_request(host: str, path: str) -> None:
    if host != HOST or path != PATH:
        raise FundingError("FUNDING_ENDPOINT_ONLY")
    lowered = path.lower()
    for banned in ("order", "account", "leverage", "position", "api_key", "signature"):
        if banned in lowered:
            raise FundingError("FUNDING_ENDPOINT_ONLY")


def fetch_funding(symbol: str, start_ms: int, end_ms: int, getter) -> list[dict]:
    rows = []
    seen = set()
    cursor = start_ms
    pages = 0
    while cursor < end_ms and pages < 20:
        pages += 1
        query = {"symbol": symbol, "startTime": str(cursor), "endTime": str(end_ms), "limit": "1000"}
        raw = getter(HOST, PATH, query)
        if not isinstance(raw, list):
            raise FundingError("FUNDING_SHAPE")
        if not raw:
            break
        for item in raw:
            stamp = int(item["fundingTime"])
            if stamp < start_ms or stamp >= end_ms or stamp in seen:
                continue
            seen.add(stamp)
            rows.append({
                "fundingTime": stamp,
                "fundingRate": format(Decimal(str(item["fundingRate"])), "f"),
                "symbol": symbol,
            })
        next_cursor = int(raw[-1]["fundingTime"]) + 1
        if next_cursor <= cursor:
            raise FundingError("PAGE_STALLED")
        cursor = next_cursor
        if len(raw) < 1000:
            break
    rows.sort(key=lambda item: item["fundingTime"])
    return rows


def urllib_getter(host: str, path: str, query: dict):
    assert_public_funding_request(host, path)
    url = "https://" + host + path + "?" + urllib.parse.urlencode(query)
    if "api_key" in url.lower() or "signature=" in url.lower():
        raise FundingError("FUNDING_ENDPOINT_ONLY")
    request = urllib.request.Request(url, method="GET")
    with urllib.request.urlopen(request, timeout=20) as response:
        payload = json.loads(response.read().decode("utf-8"))
    return payload


def funding_gaps(events: list[dict], start_ms: int, end_ms: int) -> list[dict]:
    times = sorted(int(item["fundingTime"]) for item in events if start_ms <= int(item["fundingTime"]) <= end_ms)
    gaps = []
    if not times:
        return [{"from": start_ms, "to": end_ms, "reason": "NO_EVENTS"}]
    if times[0] - start_ms > INTERVAL_MS + GAP_SLACK_MS:
        gaps.append({"from": start_ms, "to": times[0], "reason": "LATE_FIRST_EVENT"})
    for left, right in zip(times, times[1:]):
        if right - left > INTERVAL_MS + GAP_SLACK_MS:
            gaps.append({"from": left, "to": right, "reason": "INTERVAL_GAP"})
    if end_ms - times[-1] > INTERVAL_MS + GAP_SLACK_MS:
        gaps.append({"from": times[-1], "to": end_ms, "reason": "STALE_TAIL"})
    return gaps


def rates_match(frozen_events: list[dict], fetched: list[dict]) -> bool:
    fetched_times = [(int(item["fundingTime"]), Decimal(item["fundingRate"])) for item in fetched]
    for item in frozen_events:
        stamp = int(item["fundingTime"])
        rate = Decimal(item["fundingRate"])
        nearest = None
        for other_stamp, other_rate in fetched_times:
            distance = abs(other_stamp - stamp)
            if distance <= MATCH_SLACK_MS and (nearest is None or distance < nearest[0]):
                nearest = (distance, other_rate)
        if nearest is None or nearest[1] != rate:
            return False
    return True


def merge_funding(frozen_events: list[dict], fetched: list[dict]) -> list[dict]:
    if not rates_match(frozen_events, fetched):
        raise FundingError("FUNDING_PREFIX_MISMATCH")
    last_frozen = max(int(item["fundingTime"]) for item in frozen_events)
    combined = list(frozen_events)
    seen = {int(item["fundingTime"]) for item in combined}
    for item in fetched:
        stamp = int(item["fundingTime"])
        if stamp <= last_frozen or stamp in seen:
            continue
        combined.append({
            "fundingTime": stamp,
            "fundingRate": format(Decimal(item["fundingRate"]), "f"),
            "symbol": item["symbol"],
        })
        seen.add(stamp)
    combined.sort(key=lambda item: int(item["fundingTime"]))
    return combined


def write_funding_file(path: Path, symbol: str, events: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "schema": SCHEMA,
        "symbol": symbol,
        "source": "BINANCE_PUBLIC_FUNDING_RATE",
        "host": HOST,
        "path": PATH,
        "orders_sent": 0,
        "live_orders": False,
        "events": events,
    }
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def sync_archive(archive: Path, output: Path, getter=None) -> dict:
    fetch = getter or urllib_getter
    written = {}
    for symbol in ("BTCUSDT", "ETHUSDT", "SOLUSDT", "PAXGUSDT"):
        candles = archive / symbol / "1h" / "candles.jsonl"
        rows = [json.loads(line) for line in candles.read_text(encoding="utf-8").splitlines() if line.strip()]
        start = int(rows[0]["open_time"])
        end = int(rows[-1]["open_time"]) + 1
        events = fetch_funding(symbol, start, end, fetch)
        write_funding_file(output / (symbol + ".json"), symbol, events)
        written[symbol] = {"events": len(events), "gaps": funding_gaps(events, start, end), "orders_sent": 0}
    return written


def main(argv: list[str] | None = None) -> int:
    import argparse
    parser = argparse.ArgumentParser(description="Store public funding history. No orders.")
    parser.add_argument("command", choices=("sync",))
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    result = sync_archive(args.archive, args.output)
    for symbol, item in result.items():
        print("%s events=%s gaps=%s orders=%s" % (symbol, item["events"], len(item["gaps"]), item["orders_sent"]))
    return 0 if all(not item["gaps"] for item in result.values()) else 2


def load_funding_file(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("schema") != SCHEMA or payload.get("orders_sent") != 0 or payload.get("live_orders") is not False:
        raise FundingError("FUNDING_FILE_REJECTED")
    if payload.get("path") != PATH:
        raise FundingError("FUNDING_FILE_REJECTED")
    return list(payload.get("events") or [])


if __name__ == "__main__":
    raise SystemExit(main())
