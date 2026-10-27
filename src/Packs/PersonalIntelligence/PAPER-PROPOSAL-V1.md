# Paper proposal workflow V1

Workflow id: `second-brain.paper-proposal.v1`.

One `pi_task` keeps three ordered steps: `evaluate`, `decide`, `settle`. `decide` depends on `evaluate`. `settle` depends on `decide`. Step state is stored in `last_verification` only. Goal, plan snapshot, evidence, decision id, operation key, and payload hash are not rewritten by verification. A write matches the row `updated_at` it read, so a stale verification does not replace a newer one.

`settle` stays `pending` until the decision is accepted, and it is `completed` only when a re-read shows exactly one `PAPER` execution. Rejection with zero executions leaves `settle` pending and the task `blocked`. A mismatched execution count is `unverified`, not `blocked`. The orchestrator does not call the paper approve or reject API.

No new table and no new capability. This is local task behavior, not a production approval.
