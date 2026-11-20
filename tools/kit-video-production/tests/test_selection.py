"""Selector checks against the catalog and the reviewer labels."""

from __future__ import annotations

import unittest
from pathlib import Path

from kit_video.chart import load_candles, ohlcv_errors
from kit_video.library import hamming, load_catalog
from kit_video.selection import (
    PERCEPTUAL_NEAR,
    PERCEPTUAL_SAME,
    evaluate,
    hard_filter,
    interpret,
    plan_beats,
    retrieve,
)
from tests.selection_cases import REVIEW_CRITERION, groups

ROOT = Path(__file__).resolve().parents[3]
CATALOG = ROOT / "proof" / "kit-video-production-v1" / "library" / "catalog.json"
MARKET = ROOT / "proof" / "kit-video-production-v1" / "library" / "market"


class SelectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = load_catalog(CATALOG)
        cls.series = {
            "BTCUSDT": load_candles(MARKET / "BTCUSDT-1h.json"),
            "ETHUSDT": load_candles(MARKET / "ETHUSDT-1h.json"),
        }
        cls.report = evaluate(groups(), cls.rows, cls.series)

    def test_review_set_is_large_enough(self):
        self.assertGreaterEqual(self.report["scenes"], 30)
        self.assertIn("đúng chủ thể", REVIEW_CRITERION)

    def test_hard_filters_and_review_rate(self):
        self.assertEqual(self.report["hardViolations"], [])
        self.assertGreaterEqual(self.report["appropriateRate"], 0.9)
        self.assertEqual(self.report["chartsMet"], self.report["charts"])
        missed = [item for item in self.report["sceneResults"] if not item["appropriate"]]
        self.assertEqual(missed, [], msg=str(missed))

    def test_hash_thresholds_match_this_catalog(self):
        profile = self.report["hashProfile"]
        self.assertGreaterEqual(profile["nearestApproved"]["distance"], PERCEPTUAL_SAME + 1)
        self.assertEqual(profile["nearestApproved"]["distance"], PERCEPTUAL_NEAR)
        self.assertTrue(profile["derived"])
        self.assertTrue(all(item["distance"] <= PERCEPTUAL_SAME for item in profile["derived"]))
        by_id = {row["assetId"]: row for row in self.rows}
        distance = hamming(int(by_id["ref-blister-foil"]["averageHash"]), int(by_id["nvx-doc008-dashboard"]["averageHash"]))
        self.assertGreater(distance, PERCEPTUAL_NEAR)
        self.assertEqual(ohlcv_errors(self.series["BTCUSDT"]["candles"]), [])
        self.assertEqual(ohlcv_errors(self.series["ETHUSDT"]["candles"]), [])

    def test_screenshot_cannot_fill_a_pack_shot(self):
        brief = interpret("Đây là vỉ thuốc tham chiếu.", "NOVIXA", 0, 6)
        found = retrieve(brief, self.rows)
        ids = [item["asset"]["assetId"] for item in found["accepted"]]
        self.assertIn("ref-blister-foil", ids)
        self.assertNotIn("nvx-doc008-stock", ids)
        screen = next(row for row in self.rows if row["assetId"] == "nvx-doc008-stock")
        ok, reason = hard_filter(brief, screen)
        self.assertFalse(ok)
        self.assertIn("loại", reason)

    def test_embedding_was_not_available(self):
        probe = self.report["embedding"]
        self.assertFalse(probe["available"])
        self.assertFalse(probe["used"])
        self.assertLess(self.report["baseline"]["appropriateRate"], self.report["appropriateRate"])

    def test_beats_follow_the_sentence(self):
        brief = interpret("Người đang kiểm một vỉ trên bàn.", "NOVIXA", 0, 18)
        beats = plan_beats(brief, {"assets": [{"assetId": "ref-hands-blister"}]}, None)
        self.assertEqual(len(beats), 1)
        self.assertIn("Một ý", beats[0]["hold_reason"])
        chart_brief = interpret(
            "Bitcoin khung một giờ có một đoạn tăng rõ. Khoanh nến tăng mạnh nhất. Vẽ SMA 20 từ giá đóng cửa.",
            "AgentTrading",
            0,
            22,
        )
        from kit_video.selection import optimize_sequence

        outcome = optimize_sequence([chart_brief], self.rows, self.series)[0]
        spans = [round(item["end"] - item["start"], 3) for item in outcome["beats"]]
        self.assertGreater(len(spans), 1)
        self.assertGreater(max(spans) - min(spans), 0.5)
        self.assertAlmostEqual(sum(spans), 22, places=2)


if __name__ == "__main__":
    unittest.main()
