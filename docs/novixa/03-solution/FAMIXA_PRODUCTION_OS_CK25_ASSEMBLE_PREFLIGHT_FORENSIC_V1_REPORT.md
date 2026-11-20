# CK25 — ASSEMBLE PREFLIGHT

Date: 2026-09-24  
Mode: **READ-ONLY**. No ASSEMBLE. No FFmpeg. No provider. No mutation.

## 1. VERDICT

**PASS WITH GAPS**

## 2. ASSEMBLE ENTRY POINT

Production OS (authoritative for official film):

1. Director CTA `Lắp tập` → `ASSEMBLE` / `os-assemble` (`production-os-action-authority.ts`, `ProductionActionContractRegistry.Assemble`)
2. `ContentFamixaSeriesTab.tsx` `createProductionRequest({ action: 'ASSEMBLE', assemblyArtifactIds, assemblyShotCodes })`
3. `ContentProductionController.CreateVideo` `POST .../requests` when `body.Action == ASSEMBLE`
4. `ActionCommand` maps `AssemblyArtifactIds` + `AssemblyShotCodes` + `DurationSec`
5. `ProductionCreateVideoService.AssembleOsAsync`
6. Official: `AssembleOfficialAsync` — **no** `EvaluateAssemble*`
7. `OpenFrozenAsync` → `FreezeInputAsync` (`ShotContract` = `MotionRequirements` = `ProductionAssemblyFreeze.Token`)
8. Adapter: `FfmpegAssemblyExecutionAdapter.ExecuteFrozenAsync`
9. Transport: `IFfmpegMixTransport.RemuxOnceAsync` / `ProcessFfmpegMixTransport` (`-c copy`)
10. `MaterializeMediaAsync` → `VerifyArtifactAsync` role `ASSEMBLY`
11. Bind `CURRENT:ASSEMBLY` + generic `VISIBLE`. **No** auto `ACCEPTED:ASSEMBLY`

Official Director POST is blocked: `mayMutateProductionOs` is false for build `4ef164d6-…`. CTA assemble also hardcodes `TEST-SC01-SH01` / `TEST-SC01-SH02` only.

Legacy (not OS authority): `POST /content/series/assemble` → `ContentSeriesAssembleService.AssembleAsync` (VideoUrl / still / voice overlay). OS UI refuses this path.

## 3. AUTHORITATIVE INPUT

Official `AssembleOsAsync` precedence:

1. Caller `AssemblyShots[].ArtifactId` (required)
2. Generic `ACCEPTED` on **row** `ShotCode` must equal that ID
3. Official film: `art.Role == MIX` else `FAILED_IDENTITY`
4. Official film: exactly **1** shot else `MISSING_DEPENDENCY`
5. SH01 only: `ProductionEp01Sh01AssemblyCanon` MIX id+SHA else `BINDING_CHANGED`
6. SH02: no assembly canon lock

Not read: `ACCEPTED:MIX`, `CURRENT:MIX`, `VISIBLE:MIX`, graph, IDB, FilmAssembly as input.

If official SH02 is invoked with MIX `58297402-…` and generic `ACCEPTED` still equals that ID → that MIX is assembled.

If invoked with `GetShotState.acceptedArtifactId` → `ACCEPTED:VIDEO_TAKE` `b44e530b-…` → official fail-closed `FAILED_IDENTITY` (not silent VIDEO remux).

No fallback to LIPSYNC / KEYFRAME / historical LIPSYNC unless caller + generic `ACCEPTED` both point there (today they do not).

## 4. BYTE / IDENTITY CONSUMPTION

**BYTES**

Identity is required first (artifact + generic `ACCEPTED` + official MIX role + verified + physical SHA). Official execute then `_files.Read(storageKey)` and remux those bytes. Freeze token stores artifactId + SHA + storageKey + duration.

## 5. TIMELINE AUTHORITY

Resolved duration = `art.DurationSec > 0 ? art.DurationSec : row.DurationSec`.  
`row.DurationSec` from API = `body.DurationSec > 0 ? body.DurationSec : 5`.

Official freeze `DurationSec` = resolved MIX duration.

Remux command does **not** use `snapshot.DurationSec`. It is `-c copy` of the MIX file. Output clock = MIX file duration.

Live MIX `DurationSec=8` + file 8.000s ⇒ expected ASSEMBLY ≈ 8.000s, not 2.5s, **if** parent is this MIX.

No compose/trim/pad/loop in official assemble. Duration vs freeze is not asserted (only `duration > 0`).

## 6. ASSEMBLY CONTRACT

Existing: `ProductionAssemblyFreeze` token on `ProductionTaskInputSnapshot.ShotContract`.

Fields: `assemblyId|buildId|order|shot|role|artifactId|sha|storageKey|duration`.

Missing vs MIX_CONTRACT_V1: no `ContractVersion`, `CompositionMode`, `OutputClock`, `ContractHash`, segment lanes, `HASH_CHANGED`.

`ProductionActionContract.Assemble`: required `AcceptedArtifactIds`; output role `ASSEMBLY`; "No FINAL claim."

No `EvaluateAssemble`.

## 7. FREEZE

Yes: `OpenFrozenAsync` → one snapshot. Token includes parent id+SHA+duration.

Immutability: snapshot insert; no refreeze on success. Resume only for in-flight STARTED attempt.

After freeze, official checks frozen `ArtifactId` only — **not** frozen SHA vs live SHA. No post-freeze `ACCEPTED` re-read (uses in-memory resolved).

`BINDING_CHANGED` / `HASH_CHANGED` after freeze: **not implemented** for SH02.

## 8. REPLAY / IDEMPOTENCY

OS default (empty `IdempotencyKey`): `{build:N}:{shot}:ASSEMBLE:{mixArtifactId:N}`.

Includes build + shot + MIX id. **Not** MIX SHA, freeze hash, or duration.

Same MIX id → `ReplaySucceededAsync` returns existing `ASSEMBLY`; remux stays 1 (tested in-memory).

MIX SHA change, same id → same idem → old ASSEMBLY returned. Not a new identity.

UI key `os-ui:ASSEMBLE:{build}:{shot}` omits MIX id (Director cannot POST official anyway).

## 9. FAIL-CLOSED MATRIX

| Case | Result |
|---|---|
| A Missing MIX | PASS if no shots / no generic `ACCEPTED`. Does not auto-read `ACCEPTED:MIX`. |
| B MIX binding changed | PASS SH01 canon `BINDING_CHANGED`. SH02: generic `ACCEPTED` ≠ id → `MISSING_DEPENDENCY`. `ACCEPTED:MIX` divergence not checked. |
| C MIX SHA changed | FAIL as `HASH_CHANGED`. SH01 canon SHA → `BINDING_CHANGED`. File≠DB SHA → `FAILED_IDENTITY`. Frozen SHA unused after freeze. Replay ignores SHA. |
| D MIX verification failed | PASS `FAILED_VERIFICATION` |
| E Cross-shot MIX | PASS `CROSS_SHOT_FORBIDDEN` |
| F Cross-build MIX | PASS `CROSS_SCOPE_FORBIDDEN` |
| G Wrong role | PASS official `FAILED_IDENTITY` |
| H Legacy graph other video | PASS OS path ignores graph. Legacy series assemble is a different endpoint. |
| I CURRENT:MIX ≠ frozen MIX | UNKNOWN / not compared. Freeze is caller+generic `ACCEPTED`, not `CURRENT:MIX`. |
| J Missing timeline | FAIL-closed only if freeze duration `<= 0`. Duration `0` on artifact falls back to request/`5`. |
| K Invalid duration | Partial. `<= 0` after remux fails. Output is not compared to MIX `8`. |
| L Existing ASSEMBLY | PASS replay (does not reject). |
| M Replay same request | PASS same artifact, remux 1 (in-memory SH02 official suite). |

## 10. LEGACY AUTHORITY

Official `AssembleOsAsync`: **PRODUCTION_OS_AUTHORITY** (artifacts, generic `ACCEPTED`, freeze, remux).

Director assemble CTA for official film: **not reachable** (`mayMutateProductionOs` false) and **TEST-shot hardcoded**.

`POST /content/series/assemble`: **LEGACY_AUTHORITY** (URL/still/voice). OS UI refuses.

Client IDB `ShotProductionFinalStore` / `assembleVideoUrl`: **LEGACY_AUTHORITY**; OS assemble must not call them.

## 11. OUTPUT MODEL

Dual output:

1. Production artifact role `ASSEMBLY` — SHA, MIME `video/mp4`, duration from remux file, `ParentArtifactIds` = MIX id, `CURRENT:ASSEMBLY` + generic `VISIBLE`. No auto `ACCEPTED:ASSEMBLY`. No `FINAL` role.
2. `FilmAssembly` + `FilmAssemblyShot` in `pack_content.production_film_assemblies` / `production_film_assembly_shots` — membership MIX id + duration. Build-scoped title = episode code.

MIX `58297402-…` becomes FilmAssemblyShot.ArtifactId and ASSEMBLY parent.

## 12. SHOT VS EPISODE VS FINAL

Official execute is **shot-level remux of one MIX**. `resolved.Count != 1` fail-closed.

`FilmAssembly` is **build-scoped** but official creates/reuses membership by exact artifact-id list — not episode concat.

Not episode assemble. Not final-film assemble. Contract: "No FINAL claim."

## 13. LIVE SH02

Read-only SELECT. Matches expected:

- `ACCEPTED:VIDEO_TAKE` = `b44e530b-5b59-422f-9b80-e058a3a639b5`
- `ACCEPTED:VOICE` = `0fde86c2-250f-4303-bda0-4913a6dbd1e4`
- `ACCEPTED:LIPSYNC` = `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e`
- `ACCEPTED:MIX` = `58297402-e252-45e8-8d33-c7414749a297` SHA `e73c85db…` 8.000s `VERIFIED`
- Generic `ACCEPTED` = same MIX
- `CURRENT:MIX` / `VISIBLE:MIX` = same MIX

SH02 ASSEMBLE requests: 0. ASSEMBLY artifacts: 0. ASSEMBLE permits: 0.

Existing FilmAssembly `d56a3c63-fbe7-41d6-8355-0af246c092b7` title `EP01` is **SH01 only**: artifact `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` / 5.042s. Not SH02 MIX.

## 14. GAPS

- No `EvaluateAssemble`; no auto `ACCEPTED:MIX` resolve
- `GetShotState.acceptedArtifactId` prefers `ACCEPTED:VIDEO_TAKE`
- Official Director cannot POST; CTA is TEST-shot only
- Freeze SHA / `HASH_CHANGED` not enforced on SH02
- Replay identity is MIX id, not SHA
- Remux duration not pinned to freeze/`8`
- Official is single-shot remux, not episode
- No auto `ACCEPTED:ASSEMBLY`
- SH02 assemble would create a **new** FilmAssembly (membership ≠ SH01 row). Must not rewrite `d56a3c63-…`

## 15. CK26 READINESS

**READY** for shot-level official SH02 remux of the locked MIX through existing `AssembleOsAsync`.

Not ready for episode/final concat without a new contract (do not invent that in CK26).

## 16. RECOMMENDED NEXT IMPLEMENTATION

CK26 Real Assemble (official SH02 only), in-process, no Director POST:

- Preflight `ACCEPTED:MIX` = `58297402-…` SHA `e73c85db…` 8.000s. Fail closed. No latest/fallback.
- `AssembleOsAsync` with `AssemblyShots = [(EP01-SC01-SH02, 58297402-…, 8)]`. Empty idempotency key so OS identity includes MIX id.
- FFmpeg remux exactly 1. Compose 0. Providers 0. No SH01 assembly mutation.
- Verify ASSEMBLY file, SHA, MIME, duration ≈ 8.000s, parent = MIX, not MIX/VIDEO identity.
- Bind `CURRENT:ASSEMBLY` (existing). Do not auto-invent `ACCEPTED:ASSEMBLY` unless using existing `AcceptOsAsync`.
- New FilmAssembly membership SH02=MIX. Leave SH01 `d56a3c63-…` intact.
- Replay same request: same ASSEMBLY, remux stays 1.
- Negative SHA/id evaluation only — no production mutation.

HARD STOP. NO EXECUTION. NO MUTATION.
