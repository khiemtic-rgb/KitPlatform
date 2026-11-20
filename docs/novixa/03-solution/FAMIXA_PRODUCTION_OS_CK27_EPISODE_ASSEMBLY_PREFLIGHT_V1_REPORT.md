# CK27 — EPISODE ASSEMBLY PREFLIGHT

Date: 2026-09-24  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Mode: **READ-ONLY**. No episode assemble. No FFmpeg. No mutation.

## 1. VERDICT

**PASS WITH GAPS**

Shot-level official ASSEMBLE exists and is proven (CK26). Episode-level FINAL concat does not exist. Official `AssembleOsAsync` fail-closes when `resolved.Count != 1`. There is no episode action, no `EvaluateAssemble`, no concat transport, and no episode output clock.

## 2. ENTRY POINT

No episode-level execution path.

Shot-level path only:

Director `ASSEMBLE` / `Lắp tập`
→ `POST` Production OS request (`ContentProductionController`)
→ `AssembleOsAsync`
→ official lock `resolved.Count == 1` + role `MIX`
→ `ProductionAssemblyFreeze` (N-capable token, official parse requires count==1)
→ `FfmpegAssemblyExecutionAdapter.ExecuteFrozenAsync`
→ `RemuxOnceAsync` (`-c copy` +faststart, one input)
→ `ASSEMBLY` artifact + `CURRENT:ASSEMBLY` + `VISIBLE:ASSEMBLY`
→ `FilmAssembly` + `FilmAssemblyShot` membership (member = MIX id)

TEST path: `FixtureAssemblyAdapter.PlanFrozenAsync` + `CreateAssemblyAsync` membership only. No media.

Legacy path (refused by OS UI): `POST /content/series/assemble` → `ContentSeriesAssembleService` (`-f concat`, optional re-encode). Not Production OS authority.

No `EPISODE_ASSEMBLY`. No `FINAL` action. `ProductionActions.All` = CREATE_* / MIX / ACCEPT / BIND / REGENERATE / ASSEMBLE.

## 3. INPUT AUTHORITY

Official shot ASSEMBLE consumes **caller `AssemblyShots` artifact IDs that must equal generic `ACCEPTED` and role `MIX`**.

Bytes remuxed = **MIX bytes**, not `CURRENT:ASSEMBLY` / `VISIBLE:ASSEMBLY` / `ACCEPTED:ASSEMBLY`.

`FilmAssemblyShot.ArtifactId` stores the MIX id. It is membership metadata, not proven episode concat input.

`ACCEPTED:ASSEMBLY` does not exist. Preview/authority bridge ignores `ASSEMBLY`.

Episode concat input is **unset**. Options A–H are all unproven for episode.

## 4. FILM ASSEMBLY SEMANTICS

`FilmAssembly` = build-scoped membership header (`AssemblyId`, `BuildId`, `Title`, `CreatedAt`). Title is episode code (`EP01`), not proof of episode scope.

`FilmAssemblyShot` = one membership row (`ArtifactId`, `Order`, `DurationSec`, `Transition=CUT`, `AudioState=BOUND`).

Two EP01 records exist because official ASSEMBLE is one-shot and `FindAssemblyByMembershipAsync` requires exact `SequenceEqual` of artifact IDs. Different MIX lists → new header. Not extend/merge.

This is shot-level byproduct, not a proven EP01 film container.

## 5. ORDERING AUTHORITY

Production OS has no episode order table. Order = caller `AssemblyShots` index → `FilmAssemblyShot.Order` (`order++`).

Legacy `series_build.graph_json` for this build lists SH01…SH06. That is **legacy**, not OS assemble authority. OS assemble does not read it.

Both live FilmAssembly rows have `sort_order = 0`.

## 6. TIMELINE AUTHORITY

No episode clock. Freeze has per-shot duration, no start/end.

`0.000→5.042` then `5.042→13.042` is arithmetic, **not policy**.

Legacy graph seconds are all `5` — contradicts MIX 5.042 / 8.000. Must not be used as episode clock.

## 7. PICTURE / AUDIO SEMANTICS

Official shot ASSEMBLE: remux one file (`-c copy -movflags +faststart`). No concat, no re-encode, no silence, no FPS/resolution/sample-rate policy.

TEST ASSEMBLE: membership only.

Legacy series assemble: per-clip mix then `-f concat` + `-c copy`, fallback libx264/aac. Refused by OS UI.

Episode concat transport: **does not exist**.

## 8. FREEZE CONTRACT

`ProductionAssemblyFreeze` exists:

`assemblyId|buildId|order|shot|role|artifactId|sha|storageKey|duration`

N-shot capable. Official adapter/OS require `Count == 1`. No episode evaluate. No output clock. No `EvaluateAssemble`. No `ProductionFinalFreeze`.

**CONTRACT GAP** for episode assembly.

## 9. PARENT IDENTITY / SHA

Shot freeze binds parent MIX id + SHA + storage + duration + order. Output `ASSEMBLY` has its own id/SHA. No hash binding from FilmAssembly membership to ASSEMBLY bytes.

Episode: no freeze.

## 10. REPLAY / IDEMPOTENCY

Shot idempotency = joined parent MIX artifact IDs (not SHA). Same MIX ids → replay `ASSEMBLY`. Changed id → new request. SHA change on same id → `HASH_CHANGED` / `FAILED_IDENTITY`. Order/duration are not in the idem token.

Episode identity: **none**.

## 11. FAIL-CLOSED MATRIX

Episode evaluate is **NOT_IMPLEMENTED**. Existing official shot path (`Count != 1` → `MISSING_DEPENDENCY`) would reject a two-shot call before most checks.

| Case | Episode | Existing official shot ASSEMBLE |
|---|---|---|
| A missing SH01 | NOT_IMPLEMENTED | one-shot SH02 would remux; no required-set |
| B missing SH02 | NOT_IMPLEMENTED | same |
| C wrong role | NOT_IMPLEMENTED | FAIL `FAILED_IDENTITY` if not MIX |
| D cross-shot | NOT_IMPLEMENTED | FAIL `CROSS_SHOT_FORBIDDEN` |
| E cross-build | NOT_IMPLEMENTED | FAIL `CROSS_SCOPE_FORBIDDEN` |
| F SHA changed | NOT_IMPLEMENTED | FAIL `HASH_CHANGED` / `FAILED_IDENTITY` |
| G verification failed | NOT_IMPLEMENTED | FAIL |
| H duplicate shot | NOT_IMPLEMENTED | UNKNOWN |
| I ordering conflict | NOT_IMPLEMENTED | caller order wins |
| J duration <= 0 | NOT_IMPLEMENTED | freeze/adapter FAIL |
| K gap | NOT_IMPLEMENTED | no episode clock |
| L overlap | NOT_IMPLEMENTED | no episode clock |
| M legacy graph elsewhere | NOT_IMPLEMENTED | OS does not read graph |
| N FilmAssembly mismatch | NOT_IMPLEMENTED | creates a new header |
| O replay same input | NOT_IMPLEMENTED | PASS shot replay |
| P changed input after freeze | NOT_IMPLEMENTED | FAIL snapshot/binding |

## 12. LEGACY AUTHORITY

Shot ASSEMBLE = Production OS.

Episode = no OS path. Only existing multi-clip concat is legacy `ContentSeriesAssembleService`. OS UI refuses it (`mayMutateProductionOs` official = false; `productionOsLegacyRefuseNote('ASSEMBLE')`).

Legacy graph is the only ordered EP01 shot list. **LEGACY DEPENDENCY** if episode order is taken from it without an OS contract. Not current execution authority.

## 13. OUTPUT MODEL

Shot output: role `ASSEMBLY` + `CURRENT:ASSEMBLY` + `VISIBLE:ASSEMBLY`. No `ACCEPTED:ASSEMBLY`. No `FINAL` artifact role. `ProductionBindingRoles.Final` exists unused.

`FilmAssembly` is membership + shot-remux byproduct, not episode film bytes.

`ProductionFullFilmVerticalRunV1` comment (“FilmAssembly is the canonical final representation”) is TEST membership, not episode concat.

## 14. DIRECTOR PATH

CTA `Lắp tập` / `ASSEMBLE` exists. Official film POST blocked (`mayMutateProductionOs` false). Hard-coded TEST shots `TEST-SC01-SH01` / `TEST-SC01-SH02`. Body uses `acceptedArtifactId` (generic ACCEPTED). No episode CTA. No episode `ActionContract`.

## 15. LIVE EP01 FORENSICS

SH01 FilmAssembly `d56a3c63-fbe7-41d6-8355-0af246c092b7`  
member MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` · 5.042s · order 0 · CUT/BOUND

SH02 FilmAssembly `1c210b73-333e-46ce-9d02-8beab8ef2f5d`  
member MIX `58297402-e252-45e8-8d33-c7414749a297` · 8.000s · order 0 · CUT/BOUND  
FilmAssemblyShot `21678c2e-162b-4f6b-a006-5c3ad3faae36`

SH02 ASSEMBLY `92621f9e-57eb-4185-8cea-a962ec399136` SHA `37127ee5…` 8.000s is **not** the membership row.

No `FINAL` bindings. `series_build` graph: SH01–SH06, seconds=5 each.

## 16. GAPS

1. CONTRACT GAP — no episode evaluate/freeze/output clock/action.
2. FILM ASSEMBLY MODEL GAP — two 1-shot headers; members are MIX; no extend/merge.
3. AUTHORITY GAP — episode bytes unset (MIX vs ASSEMBLY).
4. TIMELINE GAP — no episode clock; graph 5s ≠ MIX durations.
5. LEGACY DEPENDENCY GAP — only ordered shot list is `series_build.graph_json`.

## 17. CK28 READINESS

**CONTRACT GAP**

## 18. RECOMMENDED NEXT IMPLEMENTATION

Not READY. Minimum before any real episode concat:

1. Episode contract: ordered official shots + parent role + artifact id + SHA + duration + order. Decide MIX vs `CURRENT:ASSEMBLY` bytes. Do not invent FINAL unless an existing model is chosen.
2. Evaluate fail-closed for missing shot, wrong role, SHA, cross-scope, duration<=0. Decide gap/overlap policy.
3. One FilmAssembly model for EP01 (do not silently merge/repair the two live headers).
4. Concat transport for N clips. Do not reuse `RemuxOnceAsync`. Do not call legacy `/content/series/assemble`.
5. Do not use graph `seconds=5` as clock.

HARD STOP. No execution. No mutation.
