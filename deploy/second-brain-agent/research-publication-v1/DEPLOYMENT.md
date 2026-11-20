# Deploy this publication

Do not rebuild the agent. Do not run a migration. Do not enable the model, exchange backfill, or live trading.

Target is VPS B only: service `second-brain-agent`, database `second_brain_agent_v1` on port 5433.

1. From this directory, `sha256sum -c SHA256SUMS.txt` must pass. The host DLL on the server must still be `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`.
2. Copy this directory to `/opt/second-brain-research-publication-v1`, owned by `sbagent:sbagent`. Directories `755`, files `644`. `/opt` is root-owned, so root creates the directory and then changes ownership.
3. Set only this line in `/etc/second-brain-agent/agent.env`:

```text
PersonalIntelligence__ResearchPublication__Root=/opt/second-brain-research-publication-v1
```

4. Restart only `second-brain-agent`.
5. Require HTTP 200 from `/api/health` and `/api/health/db`, `workerEnabled=false`, and no ExchangeBackfill hosted service.
6. With the operator session, the five research GET routes must return `FRESH` and publication id `pub-e65b6912e02a168d`.

The exact command list is in `SECOND-BRAIN-RESEARCH-PUBLICATION-V1-READINESS-REPORT.md`. Those commands were not run while this package was prepared. VPS B still has an empty publication root.
