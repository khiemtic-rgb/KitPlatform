# Paper proposal workflow V1

Workflow id: `second-brain.paper-proposal.v1`.

One `pi_task` keeps three ordered steps: `evaluate`, `decide`, `settle`. `decide` depends on `evaluate`. `settle` depends on `decide`. Step state is stored in `last_verification` only. Goal, plan snapshot, evidence, decision id, operation key, and payload hash are not rewritten by verification. A write matches the row `updated_at` it read, so a stale verification does not replace a newer one.

`settle` stays `pending` until the decision is accepted, and it is `completed` only when a re-read shows exactly one `PAPER` execution. Rejection with zero executions leaves `settle` pending and the task `blocked`. A mismatched execution count is `unverified`, not `blocked`. The orchestrator does not call the paper approve or reject API.

Migration `401_pack_personal_intelligence_paper_task_v1.sql` creates `pi_task` when it is absent. It does not alter migrations 399 or 400, and it is not in the production migration manifest. The paper task API is `api/personal-intelligence/paper-tasks`, separate from the agent task route. No new capability. This package is not a production migration or deploy approval.
