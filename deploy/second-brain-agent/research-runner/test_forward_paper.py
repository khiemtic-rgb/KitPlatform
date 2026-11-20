import json
import os
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path

import forward_paper as paper


def trade(open_time, pnl="1.5", side="LONG"):
    return {
        "side": side,
        "qty": "1",
        "entry": "10",
        "exit": "11",
        "fees": "0.01",
        "funding": "0",
        "pnl": pnl,
        "reason": "OPPOSITE_CROSS_FLATTEN",
        "decision_open_time": open_time,
    }


class ForwardPaperTests(unittest.TestCase):
    def test_week_is_monday_to_next_monday_in_vietnam(self):
        moment = datetime(2026, 11, 18, 5, 0, tzinfo=timezone.utc)
        start, end = paper.week_bounds(moment)
        self.assertEqual(start.isoformat(), "2026-11-16T00:00:00+07:00")
        self.assertEqual(end - start, timedelta(days=7))
        self.assertEqual(paper.week_title(start), "16/11 – 22/11/2026")

    def test_account_open_time_stays_fixed_when_markets_are_added(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            first = paper.ensure_account(root, {"SOLUSDT": 1_000}, "2026-11-18T05:00:00+00:00")
            second = paper.ensure_account(
                root,
                {"SOLUSDT": 9_000, "BTCUSDT": 9_000, "ETHUSDT": 9_000, "PAXGUSDT": 9_000},
                "2026-11-18T06:00:00+00:00",
            )
            self.assertEqual(second["symbols"]["SOLUSDT"]["book_from_open_time"], first["symbols"]["SOLUSDT"]["book_from_open_time"])
            self.assertEqual(second["symbols"]["PAXGUSDT"]["book_from_open_time"], 9_000)
            self.assertEqual(second["symbols"]["BTCUSDT"]["name"], "Bitcoin")
            self.assertFalse(second["live_orders"])

    def test_old_sol_account_keeps_its_open_when_gold_is_added(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "account.json").write_text(json.dumps({
                "opened_at": "2026-11-18T05:15:00+00:00",
                "book_from_open_time": 1_000,
                "live_orders": False,
                "exchange_orders_sent": 0,
            }), encoding="utf-8")
            account = paper.ensure_account(root, {"SOLUSDT": 2_000, "PAXGUSDT": 2_000}, "2026-11-18T06:00:00+00:00")
            self.assertEqual(account["symbols"]["SOLUSDT"]["book_from_open_time"], 1_000)
            self.assertEqual(account["symbols"]["PAXGUSDT"]["book_from_open_time"], 2_000)

    def test_report_splits_this_week_from_the_whole_book(self):
        moment = datetime(2026, 11, 18, 5, 0, tzinfo=timezone.utc)
        this_week = int(datetime(2026, 11, 17, 3, 0, tzinfo=timezone.utc).timestamp() * 1000)
        older = int(datetime(2026, 11, 10, 3, 0, tzinfo=timezone.utc).timestamp() * 1000)
        books = [{
            "symbol": "BTCUSDT",
            "asset": "Bitcoin",
            "strategy_id": "EXP-TREND-SMA-10-30-V1",
            "title": "SMA 10/30",
            "trades": [trade(older, "-2"), trade(this_week, "1.25")],
            "open_position": {"side": "LONG", "qty": "0.2", "entry": "70"},
            "realized": Decimal("-0.75"),
            "fees": Decimal("0.02"),
            "funding": Decimal("0"),
            "ending_equity": Decimal("99.25"),
        }, {
            "symbol": "PAXGUSDT",
            "asset": "Vàng",
            "strategy_id": "EXP-BREAKOUT-DONCHIAN-20-V1",
            "title": "Donchian 20",
            "trades": [],
            "open_position": None,
            "realized": Decimal("0"),
            "fees": Decimal("0"),
            "funding": Decimal("0"),
            "ending_equity": Decimal("100"),
        }]
        account = {"opened_at": "2026-11-18T05:00:00+00:00"}
        text = paper.render_report(account, books, moment, {"BTCUSDT": "Bitcoin funding ổn.", "PAXGUSDT": "Vàng chưa có mốc funding."})
        self.assertIn("### Bitcoin", text)
        self.assertIn("### Vàng", text)
        self.assertIn("| SMA 10/30 | 1 | 1.2500 | 0.0100 | LONG 0.2 tại 70, chưa đóng |", text)
        self.assertIn("| Bitcoin | SMA 10/30 | 2 | -0.7500 | 0.0200 | 0.0000 | 99.2500 |", text)
        self.assertIn("Không gửi lệnh lên sàn.", text)

    def test_ledger_appends_a_trade_once(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            book = {
                "symbol": "ETHUSDT",
                "strategy_id": "EXP-TREND-SMA-10-30-V1",
                "title": "SMA 10/30",
                "trades": [trade(1000)],
            }
            self.assertEqual(paper.append_ledger(root, [book]), 1)
            self.assertEqual(paper.append_ledger(root, [book]), 0)
            rows = [json.loads(line) for line in (root / "ledger.jsonl").read_text(encoding="utf-8").splitlines()]
            self.assertEqual(len(rows), 1)
            self.assertFalse(rows[0]["live_orders"])
            self.assertEqual(rows[0]["exchange_orders_sent"], 0)

    def test_exchange_key_blocks_the_account(self):
        os.environ["BINANCE_API_KEY"] = "present"
        try:
            with self.assertRaises(paper.PaperBlocked) as caught:
                paper.refuse_exchange_key()
            self.assertEqual(caught.exception.reason, "REFUSING_EXCHANGE_KEY")
        finally:
            del os.environ["BINANCE_API_KEY"]

    def test_runner_rejects_a_trade_from_before_the_account(self):
        def runner(*args, **kwargs):
            return {
                "trades": [trade(kwargs["first_signal_open_time"] - 1)],
                "events": [],
                "realized_pnl": "1",
                "fees": "0",
                "funding": "0",
                "ending_equity": "101",
                "live_orders": False,
                "exchange_orders_sent": 0,
            }

        class Spec:
            strategy_id = "EXP-TREND-SMA-10-30-V1"

        with self.assertRaises(paper.PaperBlocked) as caught:
            paper.settle_books([], None, 5_000, [], None, runner, [Spec()], "BTCUSDT", "Bitcoin")
        self.assertEqual(caught.exception.reason, "HISTORICAL_TRADE_ENTERED_BOOK")


if __name__ == "__main__":
    unittest.main()
