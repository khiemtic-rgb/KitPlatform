# Publication sample — not a research export

This directory is documentation. It is not a `research-publication/v1` package and must not be set as `PersonalIntelligence__ResearchPublication__Root`.

The running reader returns `MISSING` / `Publication is missing.` when the root is empty or the directory does not exist. That result does not block health, login, paper ledger, or intent.

A real package, produced by the `research-lab-v1` publisher, needs:

- `manifest.json` with `schemaVersion` = `research-publication/v1`, `publicationId`, `publisher`, `publishedAt`
- `lab/LAB_RESULT.json` and manifest field `labResultSha256`
- `paper/PAPER_OPS_STATUS.json` and `paperStatusSha256`
- `paper/PAPER_RESEARCH_REPORT.json` and `paperReportSha256`
- `paper/PAPER_OPS_HEALTH.json` and `paperHealthSha256`
- `health` in the health file equal to `OK` or `STALE`
- each hash equal to the lowercase SHA-256 of the file bytes

No sample JSON is stored here, so this folder cannot be mistaken for a valid publication.
