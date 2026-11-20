# Research Evaluation V1

Read-only comparison of the experiments already inside one research publication. It does not recompute performance and it does not write the paper ledger.

## Use

With `personal_intelligence.research.read` and the personal intelligence module enabled:

- `GET /api/personal-intelligence/research/evaluations`
- `GET /api/personal-intelligence/research/evaluations/{experimentId}`

A comparable result requires availability `FRESH` or `STALE`, matching file checksums, and the same `snapshotSha256` on every experiment. The current publication `pub-e65b6912e02a168d` compares `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, and `EXP-REVERSION-RANGE-20-V1`.

The admin research panel shows the comparison under Research Lab when the publication itself is readable. `NOT_A_PERFORMANCE_CLAIM` stays visible. Sharpe, CAGR, drawdown, expectancy, and the equity curve are `NOT_IN_SOURCE`.

`trading.run` cannot call these routes. A missing publication returns `INSUFFICIENT_DATA` and an empty list. A hash mismatch returns `INCONSISTENT` and no experiment bodies.

This is not `sma-cross-v1` (`fast=5,slow=20`). That strategy is not part of this comparison.
