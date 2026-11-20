import json
import os
import sys
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
import candidate_search
import candle_proposal
import funding_public
import paper_entry
import research_review
import research_task as task


def candle(open_time, price="10"):
    return {
        "open_time": open_time,
        "open": price,
        "high": price,
        "low": price,
        "close": price,
        "close_time": open_time + task.INTERVAL_MS - 1,
        "volume": "1",
    }


class ResearchTaskTests(unittest.TestCase):
    def test_destination_cannot_be_the_frozen_publication(self):
        with tempfile.TemporaryDirectory() as folder:
            frozen = Path(folder) / "publication"
            frozen.mkdir()
            with self.assertRaises(task.TaskBlocked) as caught:
                task.assert_destination(frozen, frozen)
            self.assertEqual(caught.exception.reason, "REFUSING_FROZEN_PUBLICATION")
            nested = frozen / "next"
            with self.assertRaises(task.TaskBlocked):
                task.assert_destination(nested, frozen)

    def test_prefix_and_gap_block_before_any_publication(self):
        frozen_bars = [candle(0, "10"), candle(task.INTERVAL_MS, "11")]
        changed = [candle(0, "10"), candle(task.INTERVAL_MS, "12"), candle(2 * task.INTERVAL_MS, "13")]
        with self.assertRaises(task.TaskBlocked) as caught:
            task.assert_prefix(changed, frozen_bars)
        self.assertEqual(caught.exception.reason, "ARCHIVE_PREFIX_DIFFERS_FROM_FROZEN_SNAPSHOT")
        gapped = [candle(0), candle(2 * task.INTERVAL_MS)]
        self.assertEqual(task.gap_count(gapped), 1)

    def test_verdict_refuses_selection(self):
        experiment = {
            "development": {"trade_count": 1},
            "out_of_sample": {"trade_count": 1},
            "approval_status": "EXPERIMENT_NOT_APPROVED",
            "performance_claim": "NOT_A_PERFORMANCE_CLAIM",
        }
        self.assertEqual(task.verdict_for([experiment]), "NOT_SELECTABLE")
        experiment["selected_by_pnl"] = True
        self.assertEqual(task.verdict_for([experiment]), "NOT_SELECTABLE")
        approved = dict(experiment)
        approved["approval_status"] = "APPROVED"
        self.assertEqual(task.verdict_for([approved]), "INSUFFICIENT_DATA")

    def test_exchange_key_is_refused_without_reading_the_archive(self):
        os.environ["BINANCE_API_KEY"] = "present"
        try:
            with self.assertRaises(task.TaskBlocked) as caught:
                task.refuse_exchange_key()
            self.assertEqual(caught.exception.reason, "REFUSING_EXCHANGE_KEY")
        finally:
            del os.environ["BINANCE_API_KEY"]

    def test_same_last_bar_does_not_create_another_task(self):
        with tempfile.TemporaryDirectory() as folder:
            tasks = Path(folder)
            row = task.task_record("research-evaluate-10", "completed", {}, 10)
            task.store_task(tasks, "completed", row)
            self.assertFalse(task.completed_matches(tasks, 10, "funding"))
            row["funding_sha256"] = "funding"
            task.store_task(tasks, "completed", row)
            self.assertTrue(task.completed_matches(tasks, 10, "funding"))
            self.assertFalse(task.completed_matches(tasks, 11, "funding"))
            stored = json.loads((tasks / "completed" / "research-evaluate-10.json").read_text(encoding="utf-8"))
            self.assertEqual(stored["task_type"], "research.evaluate")
            self.assertIsNone(stored["selected_strategy"])
            self.assertEqual(stored["exchange_orders_sent"], 0)

    def test_publications_stay_immutable(self):
        lab_root = Path(r"E:\Kit-Brain\second-brain-trading-source-inventory")
        task.install_engine(lab_root)
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            publications = root / "publications"
            frozen = root / "frozen-publication"
            publications.mkdir()
            frozen.mkdir()
            first = _sources(root / "a", "one")
            second = _sources(root / "b", "two")
            first_manifest = task.publish_immutable(publications, first, "2026-11-09T00:00:00+00:00", frozen, "task-a")
            first_hash = task.publication_hashes(publications / first_manifest["publicationId"])
            second_manifest = task.publish_immutable(publications, second, "2026-11-09T01:00:00+00:00", frozen, "task-b")
            self.assertNotEqual(first_manifest["publicationId"], second_manifest["publicationId"])
            self.assertEqual(task.publication_hashes(publications / first_manifest["publicationId"]), first_hash)
            self.assertTrue((publications / first_manifest["publicationId"] / "manifest.json").is_file())
            self.assertTrue((publications / second_manifest["publicationId"] / "manifest.json").is_file())
            again = task.publish_immutable(publications, first, "2026-11-09T00:00:00+00:00", frozen, "task-a-repeat")
            self.assertFalse(again["created"])
            self.assertEqual(task.publication_hashes(publications / first_manifest["publicationId"]), first_hash)
            legacy = publications / "SOLUSDT-1h"
            legacy.mkdir()
            for name, path in {
                "manifest.json": publications / first_manifest["publicationId"] / "manifest.json",
                "lab/LAB_RESULT.json": publications / first_manifest["publicationId"] / "lab" / "LAB_RESULT.json",
                "paper/PAPER_OPS_STATUS.json": publications / first_manifest["publicationId"] / "paper" / "PAPER_OPS_STATUS.json",
                "paper/PAPER_RESEARCH_REPORT.json": publications / first_manifest["publicationId"] / "paper" / "PAPER_RESEARCH_REPORT.json",
                "paper/PAPER_OPS_HEALTH.json": publications / first_manifest["publicationId"] / "paper" / "PAPER_OPS_HEALTH.json",
            }.items():
                target = legacy / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(path.read_bytes())
            sealed = task.seal_legacy(publications, frozen)
            self.assertFalse(sealed["sealed"])
            self.assertEqual(task.publication_hashes(legacy), first_hash)

    def test_funding_gap_and_prefix(self):
        start = 0
        end = funding_public.INTERVAL_MS * 2
        events = [
            {"fundingTime": 0, "fundingRate": "0.0001"},
            {"fundingTime": funding_public.INTERVAL_MS, "fundingRate": "0.0001"},
            {"fundingTime": funding_public.INTERVAL_MS * 2, "fundingRate": "0.0001"},
        ]
        self.assertEqual(funding_public.funding_gaps(events, start, end), [])
        missing = [events[0], events[2]]
        self.assertEqual(funding_public.funding_gaps(missing, start, end)[0]["reason"], "INTERVAL_GAP")
        self.assertTrue(funding_public.rates_match(events, events))
        changed = [dict(events[0], fundingRate="0.0002"), events[1], events[2]]
        self.assertFalse(funding_public.rates_match(events, changed))
        with self.assertRaises(funding_public.FundingError):
            funding_public.assert_public_funding_request("fapi.binance.com", "/fapi/v1/order")

    def test_proposal_cannot_enter_official_evaluation(self):
        experiments = [
            _experiment("EXP-TREND-SMA-10-30-V1", "7", "-6"),
            _experiment("EXP-BREAKOUT-DONCHIAN-20-V1", "6", "-2"),
            _experiment("EXP-REVERSION-RANGE-20-V1", "-3", "-1"),
        ]
        review = research_review.review_experiments(experiments, "PUBLIC_FUNDING_COMPLETE")
        self.assertEqual(review["approval_status"], "PROPOSED_NOT_APPROVED")
        self.assertIsNone(review["selected_strategy"])
        self.assertFalse(review["official_catalog_changed"])
        strategy_proposals = [item for item in review["proposals"] if item["kind"] == "STRATEGY"]
        self.assertEqual(len(strategy_proposals), 3)
        self.assertTrue(all(item["approval_status"] == "PROPOSED_NOT_APPROVED" for item in strategy_proposals))
        self.assertTrue(all(item["enters_official_evaluation"] is False for item in strategy_proposals))
        self.assertFalse(review["host_read"]["switch_without_restart"])
        self.assertFalse(review["host_read"]["performed"])

    def test_comparison_history_records_official_and_new_methods_once(self):
        experiments = [
            _experiment("EXP-TREND-SMA-10-30-V1", "7", "-6"),
            _experiment("EXP-BREAKOUT-DONCHIAN-20-V1", "6", "-2"),
            _experiment("EXP-REVERSION-RANGE-20-V1", "1", "0.08"),
        ]
        scored = {
            "rows": [
                {
                    "experiment_id": "EXP-TREND-SMA-10-30-REGIME-V2",
                    "parent_experiment_id": "EXP-TREND-SMA-10-30-V1",
                    "decision": "DROP",
                    "reasons": ["OOS_NOT_POSITIVE"],
                    "development_net_pnl": "4",
                    "out_of_sample_net_pnl": "-1",
                },
                {
                    "experiment_id": "EXP-BREAKOUT-DONCHIAN-20-CONFIRM-V2",
                    "parent_experiment_id": "EXP-BREAKOUT-DONCHIAN-20-V1",
                    "decision": "KEEP",
                    "reasons": [],
                    "development_net_pnl": "3",
                    "out_of_sample_net_pnl": "1",
                },
            ]
        }
        board = candidate_search.comparison_board(experiments, scored, 1794528000000)
        lanes = [row["lane"] for row in board["methods"]]
        self.assertEqual(lanes.count("official"), 3)
        self.assertEqual(lanes.count("candidate"), 2)
        self.assertIsNone(board["selected_strategy"])
        self.assertFalse(board["official_catalog_changed"])
        self.assertFalse(board["live_orders"])
        review = research_review.review_experiments(experiments, "PUBLIC_FUNDING_COMPLETE")
        candidate_search.attach_proposal_scores(review, scored)
        trend = next(item for item in review["proposals"] if item["proposal_id"] == "PROP-TREND-REGIME-FILTER")
        self.assertEqual(trend["paper_decision"], "DROP")
        self.assertFalse(trend["enters_official_evaluation"])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.assertTrue(task.append_comparison_history(root, board))
            self.assertFalse(task.append_comparison_history(root, board))
            lines = (root / "history" / "comparison.jsonl").read_text(encoding="utf-8").splitlines()
            self.assertEqual(len(lines), 1)
            self.assertEqual(json.loads((root / "history" / "latest.json").read_text(encoding="utf-8"))["methods"][0]["lane"], "official")

    def test_candidate_plan_follows_measured_losses_and_stays_out_of_catalog(self):
        experiments = [
            _experiment("EXP-TREND-SMA-10-30-V1", "7", "-6"),
            _experiment("EXP-BREAKOUT-DONCHIAN-20-V1", "6", "-2"),
            _experiment("EXP-REVERSION-RANGE-20-V1", "-3", "-1"),
        ]
        plans = candidate_search.plan_candidates(experiments)
        by_parent = {row["parent_experiment_id"]: row for row in plans}
        self.assertEqual(by_parent["EXP-TREND-SMA-10-30-V1"]["operator"], "SLOW_SLOPE_ALIGN")
        self.assertEqual(by_parent["EXP-BREAKOUT-DONCHIAN-20-V1"]["operator"], "TWO_CLOSE_CONFIRM")
        self.assertEqual(by_parent["EXP-REVERSION-RANGE-20-V1"]["decision"], "STOPPED")
        self.assertEqual(by_parent["EXP-REVERSION-RANGE-20-V1"]["attempts"], 0)
        scored = [row for row in plans if row["decision"] == "SCORE"]
        self.assertEqual(len(scored), 2)
        self.assertTrue(all(row["experiment_id"] not in research_review.official_catalog() for row in scored))
        self.assertTrue(all(row["live_orders"] is False and row["exchange_orders_sent"] == 0 for row in plans))
        kept = candidate_search.judge_candidate(task.Decimal("-6"), {
            "out_of_sample": {"net_pnl": "1"},
            "parameters_chosen_on_out_of_sample": False,
            "live_orders": False,
            "exchange_orders_sent": 0,
        })
        self.assertEqual(kept, ("KEEP", []))
        dropped = candidate_search.judge_candidate(task.Decimal("-6"), {
            "out_of_sample": {"net_pnl": "-1"},
            "parameters_chosen_on_out_of_sample": False,
            "live_orders": False,
            "exchange_orders_sent": 0,
        })
        self.assertEqual(dropped[0], "DROP")
        self.assertIn("OOS_NOT_POSITIVE", dropped[1])

    def test_candidate_signal_is_narrower_than_the_parent_and_can_be_scored(self):
        lab_root = Path(r"E:\Kit-Brain\second-brain-trading-source-inventory")
        task.install_engine(lab_root)
        from dataclasses import replace
        from decimal import Decimal
        from trading_agent.strategy import experiment_spec, research_specs, signal_at

        official = research_specs()
        self.assertEqual(tuple(spec.strategy_id for spec in official), research_review.official_catalog())
        self.assertTrue(all(spec.require_slow_slope is False and spec.entry_confirm_bars == 1 and spec.min_break_fraction == 0 for spec in official))
        closes = [Decimal(value) for value in ("1", "20", "10", "10", "11")]
        trend = replace(experiment_spec(), strategy_id="EXP-TREND-TEST", family="TREND", trend_fast=2, trend_slow=3, require_slow_slope=False)
        self.assertEqual(signal_at(closes, 4, trend)[0], "LONG")
        self.assertEqual(signal_at(closes, 4, replace(trend, require_slow_slope=True))[1], "SLOW_SLOPE_OPPOSES_LONG")
        breakout_closes = [Decimal(value) for value in ("1", "5", "1", "1", "6")]
        breakout = replace(experiment_spec(), strategy_id="EXP-BREAKOUT-TEST", family="BREAKOUT", breakout_lookback=2, entry_confirm_bars=1)
        self.assertEqual(signal_at(breakout_closes, 4, breakout)[0], "LONG")
        self.assertEqual(signal_at(breakout_closes, 4, replace(breakout, entry_confirm_bars=2))[1], "CONFIRM_CLOSE_MISSING")
        confirmed = [Decimal(value) for value in ("1", "2", "3", "4", "5")]
        self.assertEqual(signal_at(confirmed, 4, replace(breakout, entry_confirm_bars=2))[0], "LONG")

        rows = []
        for index in range(40):
            price = Decimal(100 + index)
            rows.append({
                "open_time": index * task.INTERVAL_MS,
                "open": format(price, "f"),
                "high": format(price, "f"),
                "low": format(price, "f"),
                "close": format(price, "f"),
                "close_time": index * task.INTERVAL_MS + task.INTERVAL_MS - 1,
                "volume": "1",
            })
        snapshot = {
            "symbol": "SOLUSDT",
            "interval": "1h",
            "interval_ms": task.INTERVAL_MS,
            "data_source": "BINANCE_PUBLIC_REST",
            "quality": "PASS",
            "funding_status": "FETCHED",
            "funding": [],
            "sha256": "abc",
            "contract": {"min_qty": "0.01", "step_size": "0.01", "min_notional": "5", "tick_size": "0.01", "source": "TEST"},
            "bars": rows,
        }
        scored = candidate_search.score_candidates(snapshot, 32, [
            _experiment("EXP-TREND-SMA-10-30-V1", "7", "-6"),
            _experiment("EXP-BREAKOUT-DONCHIAN-20-V1", "6", "-2"),
            _experiment("EXP-REVERSION-RANGE-20-V1", "-3", "-1"),
        ])
        self.assertEqual(scored["status"], "SCORED")
        self.assertFalse(scored["official_catalog_changed"])
        self.assertIsNone(scored["selected_strategy"])
        self.assertFalse(scored["promoted"])
        self.assertFalse(scored["live_orders"])
        self.assertEqual(scored["exchange_orders_sent"], 0)
        self.assertEqual(scored["official_catalog"], list(research_review.OFFICIAL_EXPERIMENTS))
        decisions = {row["parent_experiment_id"]: row["decision"] for row in scored["rows"]}
        self.assertEqual(decisions["EXP-REVERSION-RANGE-20-V1"], "STOPPED")
        self.assertIn(decisions["EXP-TREND-SMA-10-30-V1"], ("KEEP", "DROP"))
        self.assertIn(decisions["EXP-BREAKOUT-DONCHIAN-20-V1"], ("KEEP", "DROP"))
        for row in scored["rows"]:
            self.assertNotIn(row.get("experiment_id"), research_review.official_catalog())
            self.assertFalse(row["live_orders"])
            self.assertEqual(row["exchange_orders_sent"], 0)

    def test_candle_proposal_uses_development_failures_and_paper_order_stays_off_exchange(self):
        closes = [Decimal(100)] * 40
        for offset, index in enumerate((20, 22, 24, 26, 28)):
            closes[index] = Decimal(101 + offset)
            closes[index + 1] = Decimal(100)
        proposal = candle_proposal.propose_from_candles(closes, 35)
        self.assertEqual(proposal["decision"], "SCORE")
        self.assertEqual(proposal["source"], "DEVELOPMENT_CANDLES")
        self.assertFalse(proposal["parameters_chosen_on_out_of_sample"])
        self.assertGreater(Decimal(proposal["min_break_fraction"]), 0)
        self.assertNotIn(proposal["experiment_id"], research_review.official_catalog())
        self.assertEqual(proposal["false_breaks"], 5)
        lab_root = Path(r"E:\Kit-Brain\second-brain-trading-source-inventory")
        task.install_engine(lab_root)
        from dataclasses import replace
        from trading_agent.strategy import experiment_spec, signal_at

        spec = replace(experiment_spec(), strategy_id="EXP-BREAKOUT-TEST", family="BREAKOUT", breakout_lookback=2, min_break_fraction=Decimal("0.10"))
        small = [Decimal(1), Decimal(1), Decimal("1.05")]
        self.assertEqual(signal_at(small, 2, spec)[1], "BREAK_BELOW_MEASURED_EXCURSION")
        large = [Decimal(1), Decimal(1), Decimal("1.20")]
        self.assertEqual(signal_at(large, 2, spec)[0], "LONG")
        entered = paper_entry.paper_order(large, 123, spec, "SOLUSDT")
        self.assertEqual(entered["status"], "PAPER_SIGNAL")
        self.assertEqual(entered["source"], "paper")
        self.assertEqual(entered["exchange_orders_sent"], 0)
        self.assertFalse(entered["live_orders"])
        quiet = paper_entry.paper_order([Decimal(1), Decimal(1), Decimal(1)], 123, spec, "SOLUSDT")
        self.assertEqual(quiet["status"], "NO_SIGNAL")

    def test_three_candle_series_must_align(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder)
            for symbol in ("BTCUSDT", "ETHUSDT", "SOLUSDT"):
                path = archive / symbol / "1h"
                path.mkdir(parents=True)
                rows = [candle(index * task.INTERVAL_MS, "10") for index in range(3)]
                path.joinpath("candles.jsonl").write_text("\n".join(json.dumps(row) for row in rows), encoding="utf-8")
            quality = task.candle_quality(archive, 3 * task.INTERVAL_MS)
            self.assertTrue(quality["complete"])
            broken = archive / "ETHUSDT" / "1h" / "candles.jsonl"
            broken.write_text(json.dumps(candle(0)) + "\n" + json.dumps(candle(2 * task.INTERVAL_MS)), encoding="utf-8")
            self.assertFalse(task.candle_quality(archive, 3 * task.INTERVAL_MS)["complete"])


def _sources(folder: Path, marker: str) -> dict:
    folder.mkdir(parents=True)
    paths = {}
    for kind, name in (("lab", "LAB_RESULT.json"), ("status", "STATUS.json"), ("report", "REPORT.json"), ("health", "HEALTH.json")):
        path = folder / name
        path.write_text(json.dumps({"marker": marker, "kind": kind}) + "\n", encoding="utf-8")
        paths[kind] = path
    return paths


def _experiment(experiment_id: str, development: str, out_of_sample: str) -> dict:
    return {
        "experiment_id": experiment_id,
        "development": {"net_pnl": development},
        "out_of_sample": {"net_pnl": out_of_sample},
    }


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(ResearchTaskTests)
    outcome = unittest.TextTestRunner(verbosity=1).run(suite)
    if not outcome.wasSuccessful():
        raise SystemExit(1)
    print("RESULT PASS")
