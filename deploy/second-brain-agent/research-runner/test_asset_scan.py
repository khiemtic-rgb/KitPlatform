import json
import unittest
from pathlib import Path

import asset_scan


class AssetScanTests(unittest.TestCase):
    def test_only_a_positive_out_of_sample_row_is_proposed(self):
        proposals = asset_scan.proposals_from_measurements([
            {"symbol": "PAXGUSDT", "experiment_id": "EXP-REVERSION-RANGE-20-V1", "development_net_pnl": "1", "out_of_sample_net_pnl": "0.4", "live_orders": False, "exchange_orders_sent": 0},
            {"symbol": "BTCUSDT", "experiment_id": "EXP-TREND-SMA-10-30-V1", "development_net_pnl": "2", "out_of_sample_net_pnl": "-1", "live_orders": False, "exchange_orders_sent": 0},
            {"symbol": "ETHUSDT", "experiment_id": "EXP-BREAKOUT-DONCHIAN-20-V1", "development_net_pnl": "2", "out_of_sample_net_pnl": "3", "live_orders": True, "exchange_orders_sent": 0},
        ])
        self.assertEqual([item["symbol"] for item in proposals], ["PAXGUSDT"])
        self.assertEqual(proposals[0]["approval_status"], "PROPOSED_NOT_APPROVED")
        self.assertFalse(proposals[0]["enters_official_evaluation"])
        self.assertFalse(proposals[0]["live_orders"])
        self.assertEqual(proposals[0]["exchange_orders_sent"], 0)

    def test_symbol_list_adds_gold_and_records_missing_silver_and_oil(self):
        self.assertIn("PAXGUSDT", asset_scan.SCAN_SYMBOLS)
        self.assertEqual([item["asset"] for item in asset_scan.UNAVAILABLE], ["silver", "oil"])
        symbols = json.loads((Path(__file__).resolve().parents[1] / "market-data-collector" / "symbols.json").read_text(encoding="utf-8"))
        listed = [item["symbol"] for item in symbols["symbols"]]
        self.assertIn("PAXGUSDT", listed)
        self.assertNotIn("XAGUSDT", listed)


if __name__ == "__main__":
    unittest.main()
