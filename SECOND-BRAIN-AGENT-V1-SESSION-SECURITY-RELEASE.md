# SECOND BRAIN AGENT V1 — SESSION SECURITY RELEASE

Verdict: **RELEASE_READY**. The packaged host DLL is the binary accepted on VPS B. It was not rebuilt for this release.

## Release identity

| Item | Value |
|---|---|
| Release | Session Security V1 |
| Package | `E:\sb-agent-v1-session-security\package` |
| Host DLL | `host/KitPlatform.SecondBrainAgent.Host.dll` |
| Host SHA-256 | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| Host size | 99840 bytes |
| Policy DLL | `host/KitPlatform.Packs.PersonalIntelligence.Api.dll` |
| Policy SHA-256 | `44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0` |
| Migration | `001_auth_session.sql` |
| Migration SHA-256 | `7aa9fcc61788720350f7ee36fda190a1249354a4c5d1fd5763014c1cb454b268` |
| Manifest | `SHA256SUMS.txt`, 39 files, checked with 0 mismatches |
| Running host | VPS B `157.10.53.182`, service `second-brain-agent`, PID 68847 since 2026-10-30 12:33:46 +07 |

## Source

The binary was published from a `git archive` of `2c1ea353c58fcb1cf74d996bade25df28f34f8af` plus the session overlay, then copied to this package. `HEAD` is still that commit. The session changes were not committed, and this packaging did not create a commit. There is no newer source build. A rebuild would be a different artifact and is not this release.

`001_auth_session.sql` in the package matches `deploy/second-brain-agent/001_auth_session.sql` in the repository. The file applied on VPS B was not edited.

## Package contents

- `host/` — accepted publish output, including dependencies. `host/appsettings.json` has no secret. SHA-256 `23a3c08109507c2d8a8a58c7a60a57b4ac7a4418a606b50cf0821b7c74d1d10f`.
- `001_auth_session.sql` — session table and nullable `refresh_token.session_id`. No grant and no backfill.
- `DEPLOYMENT.md` and `ROLLBACK.md`.
- `SHA256SUMS.txt`.

The same manifest is `E:\KitPlatform\SHA256SUMS.txt`.

## Database prerequisites

Database `second_brain_agent_v1` on port 5433, system identifier `7702079316921501630`. Schema `second_brain_host` from `000_identity_minimum.sql` must already exist.

`001_auth_session.sql` creates `auth_session` and adds nullable `session_id`. Existing refresh rows stay null. Re-running the file does not fill them.

The migration does not grant rights. The host role needs only:

```sql
GRANT SELECT, INSERT, UPDATE ON TABLE second_brain_host.auth_session TO sb_agent;
```

That statement was applied on VPS B at 2026-10-30 12:33:45 +07. Verified again at 12:41:04 +07: `sb_agent` has `INSERT`, `SELECT`, and `UPDATE` on `auth_session`, and nothing else on that table.

## Runtime configuration

`/etc/second-brain-agent/agent.env` supplies the connection string and JWT secret. This release adds no key.

Required flags, confirmed on VPS B:

- `ASPNETCORE_URLS=http://127.0.0.1:5095`
- `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`
- `PersonalIntelligence__BusinessIntent__Enabled=true` is absent
- No `5432`, `novixa_prod`, or `103.200.23.229`

The host also forces the backfill worker off and refuses to start when `auth_session` or `refresh_token.session_id` is missing.

## Security acceptance

Accepted 2026-10-30 12:35 +07 on this DLL. Details are in `SECOND-BRAIN-AGENT-V1-SESSION-SECURITY-FINAL-ACCEPTANCE-REPORT.md`.

- Login JWT contains `sid` and does not contain `role`.
- Refresh keeps `sid`. The pre-rotation refresh token returns 401.
- Logout returns 204 and that session's access JWT returns 401.
- A second session kept working until it was logged out.
- A JWT with no `sid`, or with another user or tenant, returns 401.
- Exchange connect returned 403 and a live loop returned 400, with no new rows.
- Research publication returned `MISSING`.
- The three legacy refresh rows were not revoked.

## Deploy and roll back

Follow `SECOND-BRAIN-AGENT-V1-DEPLOYMENT-RUNBOOK.md`. VPS B already runs this package. Installing it again is not required.

Process rollback restores `/opt/second-brain-agent-v1-2c1ea353/host` and DLL `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285`. Leave the session table and the three grants in place.
