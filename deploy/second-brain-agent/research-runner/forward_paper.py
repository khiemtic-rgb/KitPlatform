"""Forward virtual accounts for BTC, ETH, gold and SOL.

Gold is the Binance token PAXGUSDT. Signals before an account opens are
ignored. Fills stay inside the research engine. Nothing is sent to an exchange.
"""

from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path

import asset_scan
import research_task as task

VN = timezone(timedelta(hours=7))
CAPITAL = Decimal("100")
MARKETS = (
    ("BTCUSDT", "Bitcoin"),
    ("ETHUSDT", "ETH"),
    ("PAXGUSDT", "Vàng"),
    ("SOLUSDT", "SOL"),
)
TITLES = {
    "EXP-TREND-SMA-10-30-V1": "SMA 10/30",
    "EXP-BREAKOUT-DONCHIAN-20-V1": "Donchian 20",
    "EXP-REVERSION-RANGE-20-V1": "Range 20",
}


class PaperBlocked(RuntimeError):
    def __init__(self, reason: str):
        super().__init__(reason)
        self.reason = reason


def refuse_exchange_key() -> None:
    for name in ("BINANCE_API_KEY", "BINANCE_API_SECRET", "BINANCE_SECRET"):
        if os.environ.get(name):
            raise PaperBlocked("REFUSING_EXCHANGE_KEY")


def vn_clock(moment: datetime) -> datetime:
    return moment.astimezone(VN)


def week_bounds(moment: datetime) -> tuple[datetime, datetime]:
    local = vn_clock(moment)
    monday = (local - timedelta(days=local.weekday())).replace(hour=0, minute=0, second=0, microsecond=0)
    return monday, monday + timedelta(days=7)


def week_title(start: datetime) -> str:
    end = start + timedelta(days=6)
    return "%s – %s" % (start.strftime("%d/%m"), end.strftime("%d/%m/%Y"))


def stamp(ms: int) -> str:
    return datetime.fromtimestamp(int(ms) / 1000, VN).strftime("%Y-%m-%d %H:%M +07")


def money(value) -> str:
    quantized = Decimal(str(value)).quantize(Decimal("0.0001"))
    return format(quantized, "f")


def trade_id(symbol: str, strategy_id: str, trade: dict) -> str:
    return "|".join([
        symbol,
        strategy_id,
        str(int(trade["decision_open_time"])),
        str(trade["side"]),
        str(trade["entry"]),
    ])


def in_week(open_time: int, start: datetime, end: datetime) -> bool:
    moment = datetime.fromtimestamp(int(open_time) / 1000, timezone.utc)
    return start <= moment.astimezone(VN) < end


def open_position(events: list[dict]) -> dict | None:
    marks = [event for event in events if event.get("event") == "OPEN_POSITION_MARKED"]
    if not marks:
        return None
    mark = marks[-1]
    return {"side": mark.get("decision"), "qty": mark.get("qty"), "entry": mark.get("entry")}


def _write_account(path: Path, account: dict) -> dict:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(account, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return account


def ensure_account(root: Path, markets: dict[str, int], opened_at: str) -> dict:
    path = root / "account.json"
    names = dict(MARKETS)
    if path.is_file():
        account = json.loads(path.read_text(encoding="utf-8"))
        if account.get("live_orders") or int(account.get("exchange_orders_sent") or 0) != 0:
            raise PaperBlocked("LIVE_ORDERS")
        symbols = account.setdefault("symbols", {})
        if "SOLUSDT" not in symbols and account.get("book_from_open_time") is not None:
            symbols["SOLUSDT"] = {
                "name": "SOL",
                "opened_at": account.get("opened_at", opened_at),
                "book_from_open_time": int(account["book_from_open_time"]),
            }
        for symbol, last_open_time in markets.items():
            current = symbols.get(symbol)
            if current is None:
                symbols[symbol] = {
                    "name": names[symbol],
                    "opened_at": opened_at,
                    "book_from_open_time": int(last_open_time),
                }
                continue
            if int(current["book_from_open_time"]) > int(last_open_time):
                raise PaperBlocked("ARCHIVE_REWOUND")
        account["symbols"] = symbols
        return _write_account(path, account)
    account = {
        "schema": "virtual-paper-account/v1",
        "interval": "1h",
        "capital_usd_per_book": format(CAPITAL, "f"),
        "opened_at": opened_at,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "books": list(TITLES),
        "symbols": {
            symbol: {
                "name": names[symbol],
                "opened_at": opened_at,
                "book_from_open_time": int(last_open_time),
            }
            for symbol, last_open_time in markets.items()
        },
    }
    return _write_account(path, account)


def load_funding(funding_dir: Path, symbol: str, asset: str) -> tuple[list[tuple[int, Decimal]], str]:
    path = funding_dir / ("%s.json" % symbol)
    try:
        import funding_public
        rows = funding_public.load_funding_file(path)
    except Exception:
        return [], "%s chưa có funding hợp lệ. Lệnh vẫn khớp." % asset
    events = [(int(item["fundingTime"]), Decimal(item["fundingRate"])) for item in rows]
    if not events:
        return [], "%s chưa có mốc funding." % asset
    return events, "%s funding đến %s." % (asset, stamp(events[-1][0]))


def settle_books(bars, contract, book_from: int, funding: list, limits, runner, specs, symbol: str, asset: str) -> list[dict]:
    books = []
    for spec in specs:
        result = runner(
            bars,
            contract,
            spec,
            limits,
            task.INTERVAL_MS,
            "BINANCE_PUBLIC_REST",
            funding_events=funding,
            first_signal_open_time=book_from,
        )
        if result.get("live_orders") or int(result.get("exchange_orders_sent") or 0) != 0:
            raise PaperBlocked("LIVE_ORDERS")
        trades = []
        for trade in result.get("trades") or []:
            if int(trade["decision_open_time"]) < book_from:
                raise PaperBlocked("HISTORICAL_TRADE_ENTERED_BOOK")
            trades.append(trade)
        books.append({
            "symbol": symbol,
            "asset": asset,
            "strategy_id": spec.strategy_id,
            "title": TITLES.get(spec.strategy_id, spec.strategy_id),
            "trades": trades,
            "open_position": open_position(result.get("events") or []),
            "realized": Decimal(str(result.get("realized_pnl") or "0")),
            "fees": Decimal(str(result.get("fees") or "0")),
            "funding": Decimal(str(result.get("funding") or "0")),
            "ending_equity": Decimal(str(result.get("ending_equity") or CAPITAL)),
        })
    return books


def week_slice(book: dict, start: datetime, end: datetime) -> list[dict]:
    return [trade for trade in book["trades"] if in_week(int(trade["decision_open_time"]), start, end)]


def sum_pnl(trades: list[dict]) -> Decimal:
    return sum((Decimal(str(trade["pnl"])) for trade in trades), Decimal("0"))


def sum_fees(trades: list[dict]) -> Decimal:
    return sum((Decimal(str(trade["fees"])) for trade in trades), Decimal("0"))


def position_line(position: dict | None) -> str:
    if not position:
        return "không"
    return "%s %s tại %s, chưa đóng" % (position["side"], position["qty"], position["entry"])


def render_report(account: dict, books: list[dict], generated_at: datetime, funding_notes: dict[str, str]) -> str:
    start, end = week_bounds(generated_at)
    order = [symbol for symbol, _name in MARKETS if any(book["symbol"] == symbol for book in books)]
    lines = [
        "# Báo cáo tài khoản ảo 1 giờ",
        "",
        "Cập nhật: %s" % vn_clock(generated_at).strftime("%Y-%m-%d %H:%M +07"),
        "Thị trường: Bitcoin, ETH, vàng (PAXGUSDT), SOL. Mỗi chiến lược một sổ %s USD." % money(CAPITAL),
        "Khớp tại giá mở của nến sau. Không gửi lệnh lên sàn.",
        "Lãi lỗ dưới đây là lệnh đã đóng. Vị thế còn mở chưa được tính.",
        "",
        "## Tuần này (%s)" % week_title(start),
        "",
    ]
    for symbol in order:
        chosen_books = [book for book in books if book["symbol"] == symbol]
        lines.append("### %s" % chosen_books[0]["asset"])
        lines.append("")
        lines.append(funding_notes.get(symbol, ""))
        lines.append("")
        lines.append("| Sổ | Lệnh đóng | Lãi/lỗ | Phí | Vị thế đang mở |")
        lines.append("|---|---:|---:|---:|---|")
        for book in chosen_books:
            chosen = week_slice(book, start, end)
            lines.append("| %s | %s | %s | %s | %s |" % (
                book["title"],
                len(chosen),
                money(sum_pnl(chosen)),
                money(sum_fees(chosen)),
                position_line(book["open_position"]),
            ))
        lines.append("")
    lines.extend([
        "## Từ lúc mở sổ",
        "",
        "| Thị trường | Sổ | Lệnh đóng | Lãi/lỗ | Phí | Funding | Vốn còn |",
        "|---|---|---:|---:|---:|---:|---:|",
    ])
    for book in books:
        lines.append("| %s | %s | %s | %s | %s | %s | %s |" % (
            book["asset"],
            book["title"],
            len(book["trades"]),
            money(book["realized"]),
            money(book["fees"]),
            money(book["funding"]),
            money(book["ending_equity"]),
        ))
    lines.extend(["", "## Lệnh đóng trong tuần này", ""])
    any_trade = False
    for book in books:
        for trade in week_slice(book, start, end):
            any_trade = True
            lines.append("- %s %s %s, tín hiệu %s, vào %s, ra %s, lãi/lỗ %s, lý do %s" % (
                book["asset"],
                book["title"],
                trade["side"],
                stamp(trade["decision_open_time"]),
                trade["entry"],
                trade["exit"],
                money(trade["pnl"]),
                trade["reason"],
            ))
    if not any_trade:
        lines.append("Chưa có lệnh nào đóng trong tuần này.")
    lines.append("")
    return "\n".join(lines)


def append_ledger(root: Path, books: list[dict]) -> int:
    path = root / "ledger.jsonl"
    seen = set()
    if path.is_file():
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                seen.add(json.loads(line)["trade_id"])
    added = 0
    with path.open("a", encoding="utf-8") as handle:
        for book in books:
            for trade in book["trades"]:
                identity = trade_id(book["symbol"], book["strategy_id"], trade)
                if identity in seen:
                    continue
                seen.add(identity)
                row = {
                    "trade_id": identity,
                    "symbol": book["symbol"],
                    "strategy_id": book["strategy_id"],
                    "side": trade["side"],
                    "qty": trade["qty"],
                    "entry": trade["entry"],
                    "exit": trade["exit"],
                    "pnl": str(trade["pnl"]),
                    "fees": str(trade["fees"]),
                    "funding": str(trade["funding"]),
                    "reason": trade["reason"],
                    "decision_open_time": int(trade["decision_open_time"]),
                    "live_orders": False,
                    "exchange_orders_sent": 0,
                }
                handle.write(json.dumps(row, ensure_ascii=False) + "\n")
                added += 1
    return added


def contract_for(symbol: str) -> dict:
    contract = dict(asset_scan.CONTRACTS[symbol])
    contract["source"] = "BINANCE_PUBLIC_REST"
    return contract


def run_account(archive: Path, root: Path, lab_root: Path, frozen_snapshot: Path, funding_dir: Path, now: datetime | None = None) -> dict:
    del frozen_snapshot
    refuse_exchange_key()
    moment = now or task.utc_now()
    loaded = {}
    for symbol, _asset in MARKETS:
        path = archive / symbol / "1h" / "candles.jsonl"
        if not path.is_file():
            continue
        candles = task.load_candles(path)
        if len(candles) < 40:
            continue
        loaded[symbol] = candles
    if not loaded:
        raise PaperBlocked("CANDLES_SHORT")
    account = ensure_account(root, {symbol: int(rows[-1]["open_time"]) for symbol, rows in loaded.items()}, task.iso(moment))
    task.install_engine(lab_root)
    from trading_agent.backtest import run_backtest
    from trading_agent.risk import proposed_limits
    from trading_agent.strategy import research_specs
    import pipeline

    limits = proposed_limits()
    specs = research_specs()
    books = []
    funding_notes = {}
    for symbol, asset in MARKETS:
        candles = loaded.get(symbol)
        if candles is None or symbol not in account["symbols"]:
            continue
        snapshot = {
            "symbol": symbol,
            "interval": "1h",
            "interval_ms": task.INTERVAL_MS,
            "contract": contract_for(symbol),
        }
        funding, funding_notes[symbol] = load_funding(funding_dir, symbol, asset)
        books.extend(settle_books(
            task.snapshot_bars(candles),
            pipeline.contract_from_snapshot(snapshot),
            int(account["symbols"][symbol]["book_from_open_time"]),
            funding,
            limits,
            run_backtest,
            specs,
            symbol,
            asset,
        ))
    report = render_report(account, books, moment, funding_notes)
    root.mkdir(parents=True, exist_ok=True)
    (root / "weekly.md").write_text(report, encoding="utf-8")
    added = append_ledger(root, books)
    (root / "latest.json").write_text(json.dumps({
        "generated_at": task.iso(moment),
        "live_orders": False,
        "exchange_orders_sent": 0,
        "books": [{
            "symbol": book["symbol"],
            "strategy_id": book["strategy_id"],
            "trade_count": len(book["trades"]),
            "realized": money(book["realized"]),
            "ending_equity": money(book["ending_equity"]),
            "open_position": book["open_position"],
        } for book in books],
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"added": added, "books": books, "report": report}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Mark the virtual paper accounts and write the weekly report.")
    parser.add_argument("command", choices=("run",))
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--account", type=Path, required=True)
    parser.add_argument("--lab-root", type=Path, required=True)
    parser.add_argument("--frozen-snapshot", type=Path, required=True)
    parser.add_argument("--funding-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        result = run_account(args.archive, args.account, args.lab_root, args.frozen_snapshot, args.funding_dir)
    except PaperBlocked as exc:
        print("PAPER_BLOCKED %s" % exc.reason)
        return 2
    totals = []
    for symbol, asset in MARKETS:
        chosen = [book for book in result["books"] if book["symbol"] == symbol]
        if not chosen:
            continue
        realized = sum((book["realized"] for book in chosen), Decimal("0"))
        totals.append("%s=%s" % (asset, money(realized)))
    print("PAPER_ACCOUNT added=%s %s live_orders=false" % (result["added"], " ".join(totals)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
