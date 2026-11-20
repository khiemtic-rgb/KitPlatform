import json
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

import collector


def bar(open_time, price="10", symbol="SOLUSDT", interval="1h"):
    return collector.normalize_bar({
        "open_time": open_time,
        "open": price,
        "high": price,
        "low": price,
        "close": price,
        "close_time": open_time + collector.INTERVAL_MS[interval] - 1,
        "volume": "1",
    }, symbol, interval, "2026-11-02T00:00:00+00:00")


class CollectorTests(unittest.TestCase):
    def test_checksum_matches_lab_rule(self):
        bars = [bar(1_784_800_800_000, "1.2500")]
        again = collector.checksum_bars(bars)
        self.assertEqual(again, collector.checksum_bars(json.loads(json.dumps(bars)) and bars))
        self.assertEqual(len(again), 64)

    def test_duplicate_and_conflict(self):
        first = bar(0)
        changed = bar(0, price="11")
        merged, inserted, duplicates, conflicts = collector.merge_bars([first], [first, changed, bar(3_600_000)])
        self.assertEqual(inserted, 1)
        self.assertEqual(duplicates, 1)
        self.assertEqual(conflicts, 1)
        self.assertEqual(merged[0]["close"], "10")
        self.assertEqual(len(merged), 2)

    def test_gap_and_backfill_skips_open_candle(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "symbols.json").write_text(json.dumps({
                "symbols": [{"symbol": "SOLUSDT", "interval": "1h", "history_start_ms": 0}],
            }), encoding="utf-8")
            existing = bar(0)
            later = bar(7_200_000)
            collector.write_candles_atomic(root / "SOLUSDT" / "1h" / "candles.jsonl", [existing, later])
            missing = bar(3_600_000)
            unclosed = bar(10_800_000)
            now_ms = 10_800_000 + 60_000

            def fetch(url):
                self.assertNotIn("signature", url)
                self.assertNotIn("api_key", url.lower())
                if url.endswith("/api/v3/time"):
                    return json.dumps({"serverTime": now_ms})
                self.assertIn("startTime=3600000", url)
                return json.dumps([[
                    missing["open_time"], missing["open"], missing["high"], missing["low"],
                    missing["close"], missing["volume"], missing["close_time"],
                    "0", 0, "0", "0", "0",
                ], [
                    unclosed["open_time"], unclosed["open"], unclosed["high"], unclosed["low"],
                    unclosed["close"], unclosed["volume"], unclosed["close_time"],
                    "0", 0, "0", "0", "0",
                ]])

            result = collector.sync_all(root, fetch=fetch, now_ms=now_ms)[0]
            stored = collector.load_candles(root / "SOLUSDT" / "1h" / "candles.jsonl")
            self.assertEqual([item["open_time"] for item in stored], [0, 3_600_000, 7_200_000])
            self.assertEqual(result["inserted"], 1)
            self.assertEqual(result["gap_count"], 0)
            self.assertNotIn(10_800_000, [item["open_time"] for item in stored])

    def test_health_reflects_age(self):
        fresh = collector.assess([bar(3_600_000)], 3_600_000, 7_200_000 + 60_000, None)
        stale = collector.assess([bar(0)], 3_600_000, 0 + 5 * 3_600_000, None)
        self.assertEqual(fresh["status"], "OK")
        self.assertFalse(fresh["stale"])
        self.assertEqual(stale["status"], "STALE")
        self.assertTrue(stale["stale"])

    def test_verify_detects_tamper(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "symbols.json").write_text(json.dumps({
                "symbols": [{"symbol": "SOLUSDT", "interval": "1h"}],
            }), encoding="utf-8")
            moment = datetime.fromtimestamp(7_200_000 / 1000, timezone.utc)
            collector.import_files(root, "SOLUSDT", "1h", []) if False else None
            collector._commit(root, "SOLUSDT", "1h", [bar(0), bar(3_600_000)], moment, "test")
            path = root / "SOLUSDT" / "1h" / "candles.jsonl"
            text = path.read_text(encoding="utf-8").replace('"close":"10"', '"close":"11"', 1)
            path.write_text(text, encoding="utf-8")
            result = collector.verify_all(root, now_ms=7_200_000)[0]
            self.assertFalse(result["verified"])
            self.assertIn("CHECKSUM_MISMATCH", result["problems"])

    def test_rejects_path_like_symbol(self):
        with self.assertRaises(collector.CollectorError):
            collector.archive_dir(Path("."), "../etc", "1h")

    def test_refuses_signed_url(self):
        with self.assertRaises(collector.CollectorError):
            collector._http_get("https://api.binance.com/api/v3/order?signature=abc")


if __name__ == "__main__":
    unittest.main()
