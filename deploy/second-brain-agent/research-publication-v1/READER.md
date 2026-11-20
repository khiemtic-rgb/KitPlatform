# Reader and API

The accepted agent host already contains `FileResearchPublicationReader` and `ResearchPublicationController`. This package does not replace that host. Host DLL SHA-256 remains `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`.

Configuration key: `PersonalIntelligence__ResearchPublication__Root`

The value is the directory that contains `manifest.json`. An empty value returns HTTP 200 with availability `MISSING`.

The reader accepts only `research-publication/v1`, requires the four SHA-256 fields, and reads only `manifest.json`, `lab/LAB_RESULT.json`, `paper/PAPER_OPS_STATUS.json`, `paper/PAPER_RESEARCH_REPORT.json`, and `paper/PAPER_OPS_HEALTH.json`. A hash mismatch is `INCONSISTENT`. A missing required file is `MISSING`. Health `OK` is `FRESH`. Health `STALE` is `STALE`.

Local read of this package returned `FRESH`, id `pub-e65b6912e02a168d`, 3 experiments, health `OK`, 3 strategies, `live_orders=false`, `exchange_orders_sent=0`.

All routes require an authenticated session and `personal_intelligence.research.read`. A platform `ADMIN` role also passes. `trading.run`, `trading.approve`, `exchange.read`, and `exchange.connect` do not. The tenant must have `personal_intelligence` in `settings.platform.enabled_modules`.

| Method and path | HTTP 200 body when the publication is fresh |
|---|---|
| `GET /api/personal-intelligence/research/publication` | `availability`, `schemaVersion`, `publicationId`, `publishedAt`, `publisher`, `objects`, `readError` |
| `GET /api/personal-intelligence/research/experiments` | `availability`, `experiments`, `readError` |
| `GET /api/personal-intelligence/research/experiments/{experimentId}` | `availability`, `experiment`, `readError`. An id outside the catalog or absent from the file is HTTP 404 `NOT_IN_PUBLICATION`. |
| `GET /api/personal-intelligence/research/paper/status` | `availability`, `status`, `readError` |
| `GET /api/personal-intelligence/research/paper/report` | `availability`, `report`, `readError`. `report.labResult` is the literal `SEE_LAB_RESULT_ENDPOINT`. |

When availability is not `FRESH` or `STALE`, experiment, status, and report bodies are empty or null. The publication route still returns the error code.
