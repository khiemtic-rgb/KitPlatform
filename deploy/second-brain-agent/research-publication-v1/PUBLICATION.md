# Research Publication V1

| Item | Value |
|---|---|
| Publication ID | `pub-e65b6912e02a168d` |
| Schema | `research-publication/v1` |
| Publisher | `research-lab-v1` |
| Published at | `2026-10-30T04:30:00+00:00` |
| Source candidate | `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\output\publication-candidate-20261030` |

The five JSON files in this directory are a byte copy of that candidate. The candidate was not edited.

`manifest.json` names four objects:

| Manifest field | Path |
|---|---|
| `labResultSha256` | `lab/LAB_RESULT.json` |
| `paperStatusSha256` | `paper/PAPER_OPS_STATUS.json` |
| `paperReportSha256` | `paper/PAPER_RESEARCH_REPORT.json` |
| `paperHealthSha256` | `paper/PAPER_OPS_HEALTH.json` |

Health in the file is `OK`. The reader maps that to availability `FRESH`. It does not recompute bar age when the API is called. `last_bar_age_ms` is `242250`, captured at `2026-10-30T04:04:02.086108+00:00`. The last processed bar open time `1793329200000` is `2026-10-30T03:00:00+00:00`, the same instant as `extension_end_utc`. Extension count is 211 in both the status file and the report.

Symbol is `SOLUSDT`. Timeframe is `1h`. `live_orders` is false and `exchange_orders_sent` is 0 in the lab, status, report, and health files. Snapshot `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3` matches `frozen_snapshot_sha256`.

Three experiments: `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, `EXP-REVERSION-RANGE-20-V1`. Approval is `EXPERIMENT_NOT_APPROVED`. Evidence is `INSUFFICIENT_EVIDENCE`. The report claim is `NOT_A_PERFORMANCE_CLAIM`.
