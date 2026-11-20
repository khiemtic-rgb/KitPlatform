# Roll back this publication

1. Restore `/etc/second-brain-agent/agent.env` from the backup taken before the root was set, or set `PersonalIntelligence__ResearchPublication__Root=` to empty.
2. Restart only `second-brain-agent`.
3. `GET /api/personal-intelligence/research/publication` must return availability `MISSING`.
4. Health must stay HTTP 200 with the same host DLL.

Leave the session-security binary, `auth_session`, and the operator permissions in place. Removing `/opt/second-brain-research-publication-v1` is optional and only after the process is reading an empty root. Do not drop a table and do not revoke refresh tokens.
