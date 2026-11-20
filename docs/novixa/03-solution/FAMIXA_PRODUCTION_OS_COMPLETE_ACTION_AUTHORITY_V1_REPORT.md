# FAMIXA PRODUCTION OS — COMPLETE ACTION AUTHORITY V1

Date: 2026-09-18  
Mode: ARCHITECTURAL CUTOVER — fixture/import only  
Live provider calls: none  
Paid spend: none  
FILM / EP01 / EP99 / Canon / Visual Authority: not modified  

---

## 1. EXECUTIVE VERDICT

**PASS WITH GAPS.** FACT / IMPLEMENTED / TESTED / LIVE VERIFIED

Production OS is now the execution authority for the in-scope TEST actions:

`CREATE_IMAGE` → `CREATE_VIDEO` → `CREATE_VOICE` → `CREATE_LIPSYNC` → `MIX_VIDEO` → `ACCEPT` → `ASSEMBLE` (typed boundary)

CREATE_VIDEO was not rebuilt. Fixture adapters only. Zero Gemini / Runway / Veo / Fal / ElevenLabs calls in this phase.

A complete synthetic TEST shot (`TEST-SC01-SH02`) traversed the chain on the live TEST scope. Preview resolved `VISIBLE → MIX artifactId → physical bytes`. Assembly input was `[accepted MIX artifactId]`, not a URL. FILM isolation held.

Gaps that keep this from an unqualified PASS:

1. Director / Series desk UI still calls legacy `/series/*` endpoints. Classified for later UI Cutover. Not patched. FACT
2. `ASSEMBLE` is a typed OS request/task/snapshot/attempt + `FilmAssembly` row. It does **not** mint a VERIFIED FINAL media artifact and does **not** claim FILM READY. IMPLEMENTED
3. KEYFRAME verification still accepts `length >= 32` without PNG decode (pre-existing verifier gap). FACT
4. Live HTTP did not re-exercise mutation / cross-shot / idempotency / failed-verify (those are TESTED in-process). LIVE VERIFIED only for the happy-path SH02 chain.

This is **not** an end-to-end film. STOP.

---

## 2. CURRENT → TARGET MAP

Completed before coding. FACT

| Action | CURRENT (before) | TARGET (this phase) |
|---|---|---|
| CREATE_IMAGE | OS `RegisterKeyframeAsync` exists; desk still `POST /series/still` | OS `CreateImageAsync` + `FixtureImageAdapter` / `ImportImageAdapter`. Legacy still remains NON-AUTHORITY. |
| CREATE_VIDEO | Proven OS path (3B/4A). Desk still turbo | Unchanged reference. Do not duplicate. |
| CREATE_VOICE | Contract only; IDB / TTS blobs | OS request/task/snapshot/attempt + `FixtureVoiceAdapter` → VOICE + `CURRENT:VOICE` |
| CREATE_LIPSYNC | Contract only; Fal `/series/lipsync` | Parents = VIDEO_TAKE + VOICE artifactIds. `FixtureLipSyncAdapter`. `CURRENT:LIPSYNC` + `VISIBLE:LIPSYNC` |
| MIX_VIDEO | `MixExplicitAsync` engine leftover (old UTF8 bytes) | `FixtureMixAdapter` MP4. Parent = LIPSYNC artifactId. `CURRENT:MIX` + `VISIBLE:MIX` |
| ACCEPT | `AcceptAsync` binding primitive, no HTTP | OS request/task/snapshot/attempt + `ACCEPTED` + `ACCEPTED:{role}`. No new media |
| ASSEMBLY | `AssembleAsync` unused; ASSEMBLE missing from 368 CHECK | Migration 372 + `CreateAssemblyAsync` + `FixtureAssemblyAdapter`. Ordered ACCEPTED artifactIds. No FINAL |

Series graph remains WHAT. Provider/model remains HOW. Adapters own vendor semantics. OS does not know Fal/Runway/ElevenLabs/Gemini payloads.

---

## 3. FILES CHANGED

IMPLEMENTED

**New**

- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionIdentityRules.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionTestWav.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IProductionMediaAdapters.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCompleteActionAuthorityV1Regression.cs`
- `migrations/372_pack_content_production_assemble_action.sql`
- this report

**Modified (shared OS, not a second engine)**

- `ProductionCreateVideoService.cs` — interface + optional fixture adapters; CREATE_VIDEO body untouched
- `ProductionActionContracts.cs` — `Assemble` contract + CTA map
- `ProductionWorkIds.cs` — `SchemaVersion = 372`, `ProductionActions.Assemble`
- `ShotExecutionInput.cs` — optional line/voice/parent artifact fields
- `ProductionWorkExecutionEngine.cs` — VOICE/LIPSYNC/MIX decode gates; `CompleteDecisionAsync`
- `IProductionExecutionService.cs` / `ProductionExecutionService.cs` — `BindNamedAsync`, `CompleteDecisionAsync`; Story/Acting stale includes `CREATE_VOICE`
- `ShotDependencyGraph.cs` — VOICE is downstream of STORY/ACTING
- `IProductionWorkStore.cs` / `InMemoryProductionWorkStore.cs` / `ProductionWorkRepository.cs` — `ListAssembliesByBuildAsync`
- `ProductionApiDtos.cs` / `ContentProductionController.cs` — action dispatch
- `ContentPackDependencyInjection.cs` — fixture adapter singletons
- `deploy/ubuntu/migration-files.content.txt` — 372
- `ProductionScopeGuard.cs` — voice folder (pre-existing in this phase)
- checkpoint schema pins 371 → 372
- `ProductionWorkExecutionV1Regression.cs` — T21 mix bytes are now `ProductionTestMp4` (verifier must not accept UTF8 “mix”)
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs` — new fact

CREATE_VIDEO / 4A files were not rewritten.

---

## 4. DATABASE / MIGRATIONS

IMPLEMENTED / LIVE VERIFIED

Migration `372_pack_content_production_assemble_action.sql` adds `ASSEMBLE` to `production_requests` / `production_tasks` CHECKs.

- Schema-only. No EP01/EP99/Canon/VA writes. FACT
- Applied locally. `assemble_ok = 1`. LIVE VERIFIED
- No new tables. No destructive migration. FACT

---

## 5. ACTION CONTRACTS

IMPLEMENTED / TESTED

Registered actions used by this phase: `CREATE_IMAGE`, `CREATE_VIDEO`, `CREATE_VOICE`, `CREATE_LIPSYNC`, `MIX_VIDEO`, `ACCEPT`, `ASSEMBLE`.

CTA 1:1 (contract / `ProductionUiActionMap`):

| Label | Action |
|---|---|
| Tạo hình | CREATE_IMAGE |
| Tạo video | CREATE_VIDEO |
| Nạp thoại / Tạo giọng nói | CREATE_VOICE |
| Khớp môi | CREATE_LIPSYNC |
| Hoàn thiện | MIX_VIDEO |
| Dùng kết quả này | ACCEPT |
| Lắp tập | ASSEMBLE |

TESTED in `ProductionCompleteActionAuthorityV1Regression`.

Desk helper `production-os-action-authority.ts` still labels voice “Tạo thoại” and only wires CREATE_VIDEO. LEGACY / later UI Cutover. FACT

---

## 6. INPUT SNAPSHOTS

IMPLEMENTED / TESTED / LIVE VERIFIED

Every OS action freezes an immutable `ProductionTaskInputSnapshot` before adapter execution (CREATE_IMAGE was reordered to freeze-then-execute).

Frozen HOW: `providerId` + `modelId` on snapshot and task.

Frozen WHAT (available today, no Acting redesign):

- IMAGE / VIDEO: story, acting, visual, timing, keyframe artifactId
- VOICE: `lineId|speakerCharacterId|voiceId|story` hashed into `VoiceHash`
- LIPSYNC: `ParentArtifactIds = [VIDEO_TAKE, VOICE]`
- MIX: `ParentArtifactIds = [LIPSYNC]`
- ACCEPT: `ParentArtifactIds = [selected artifactId]`
- ASSEMBLE: ordered accepted artifactIds

Same idempotency key + mutated story replays the frozen snapshot. TESTED

Live SH02 snapshot hashes exist on every media artifact. LIVE VERIFIED

---

## 7. ATTEMPTS

IMPLEMENTED / TESTED / LIVE VERIFIED

| Action | Live AttemptId | Provider/Model | Job prefix |
|---|---|---|---|
| CREATE_IMAGE | `da2afc90-39f1-410c-87f5-553f6dc41963` | fixture / fixture-image-v1 | `fixture-image-` |
| CREATE_VIDEO | `3438f454-bf69-489c-8552-f84c67ac744c` | fixture / fixture-motion-v1 | `fixture-motion-` |
| CREATE_VOICE | `8dacfd63-65ef-46ca-ad90-39cebe1fd5b2` | fixture / fixture-voice-v1 | `fixture-voice-` |
| CREATE_LIPSYNC | `6f064bc1-e878-4f52-bf0c-d2fa8dead021` | fixture / fixture-lipsync-v1 | `fixture-lipsync-` |
| MIX_VIDEO | `2d7fe878-ef7f-45a0-b4c1-b7b4500a944e` | fixture / fixture-mix-v1 | `fixture-mix-` |
| ACCEPT | (decision attempt on request `a657d0d8-…`) | os / accept-v1 | `accept-` |
| ASSEMBLE | (decision attempt on request `7b801baf-…`) | fixture / fixture-assembly-v1 | assembly id `69670e0d-…` |

Failed verification starts a new attempt on the same request (existing retry semantics). TESTED  
IN_FLIGHT same idempotency does not create a second task. TESTED

---

## 8. ARTIFACT LINEAGE

IMPLEMENTED / TESTED / LIVE VERIFIED

Live `TEST-SC01-SH02` (scope `bbbbbbbb-0000-4000-8000-000000000371`):

```
KEYFRAME   9877cd99-9b1a-44f1-8972-2da382bc5d72   parents []
VIDEO_TAKE 4d919423-fd0f-4fe3-956f-e09251d02ef6   parents [9877cd99]
VOICE      602e48a7-1ab3-48fa-9b68-93359c7e8e9d   parents []
LIPSYNC    909bbc26-73c5-4727-8623-47c72ef8dc8b   parents [4d919423, 602e48a7]
MIX        e7eb55c9-10af-4ce3-a6de-1d5a937340ed   parents [909bbc26]
ACCEPT     binding on e7eb55c9
ASSEMBLY   shot row artifact_id = e7eb55c9
```

Downstream identity is artifactId. No URL lineage. LIVE VERIFIED

VOICE has no parent artifactId because it is a parallel STORY/ACTING dependency, not a child of VIDEO_TAKE. FACT

ASSEMBLE does not create a VERIFIED media artifact. Intentional. IMPLEMENTED

---

## 9. BINDINGS

IMPLEMENTED / LIVE VERIFIED

Active SH02 bindings after the live chain:

| Role | Artifact |
|---|---|
| CURRENT:KEYFRAME | 9877cd99 |
| CURRENT / CURRENT:VIDEO_TAKE | 4d919423 |
| CURRENT:VOICE | 602e48a7 |
| CURRENT:LIPSYNC / VISIBLE:LIPSYNC | 909bbc26 |
| CURRENT:MIX / VISIBLE:MIX / VISIBLE / ACCEPTED / ACCEPTED:MIX | e7eb55c9 |

VOICE does not steal alias `CURRENT` (still VIDEO_TAKE). LIVE VERIFIED

---

## 10. PROVIDER BOUNDARY

IMPLEMENTED / TESTED / LIVE VERIFIED

OS adapters in this phase:

- `FixtureImageAdapter` / `ImportImageAdapter`
- `FixtureMotionAdapter` (existing CREATE_VIDEO)
- `FixtureVoiceAdapter`
- `FixtureLipSyncAdapter`
- `FixtureMixAdapter`
- `FixtureAssemblyAdapter`

All `IsPaid = false`, `RequiresNetwork = false`. TESTED

OS code does not embed Runway / Fal / ElevenLabs / Gemini payload fields. FACT  
Paid `HttpRunwayTransport` remains for the existing CREATE_VIDEO 4A path and was not invoked (`authorizePaidNetwork = false`). LIVE VERIFIED

---

## 11. PREVIEW AUTHORITY

IMPLEMENTED / TESTED / LIVE VERIFIED

OS preview = `VISIBLE` binding → artifactId → storage bytes.

Live GET `/production/shots/{TEST-scope}/TEST-SC01-SH02/preview`:

- `empty = false`
- `artifactId = e7eb55c9-10af-4ce3-a6de-1d5a937340ed` (MIX)
- SHA `ec2b716cbf60f850016e1b0ef9669aefcf5176811e1005ab851cbdd792c79987`

If VISIBLE is absent, resolver returns `VISIBLE_MISSING`. No fallback to `previewUrl` / `takeUrl` / `acceptedTake.url`. FACT

Director desk preview remains LEGACY and was not cut over. FACT

---

## 12. LEGACY BYPASS AUDIT

TESTED (static) / FACT (UI classification)

OS executor sources scanned (`ProductionCreateVideoService.Actions.cs`, `IProductionMediaAdapters.cs`, `ProductionIdentityRules.cs`, CREATE_VIDEO service):

do **not** contain `/series/still`, `/series/turbo`, `/series/lipsync`, `/series/assemble`, `MixExplicitAsync`, `elevenlabs`, `fal.run`, `api.dev.runwayml.com`. TESTED

`LegacySeriesGraphAdapter.IsAuthoritative() = false`. FACT

**UI still bypasses OS (do not patch this phase):**

| Caller | Legacy authority | Classification |
|---|---|---|
| `ContentFamixaSeriesTab.tsx` `generateContentSeriesStill` (~2271) | `POST /series/still` | LEGACY / UI Cutover |
| `ContentFamixaSeriesTab.tsx` `startContentSeriesTurbo` (~3091) | `POST /series/turbo` | LEGACY / UI Cutover |
| `ContentFamixaSeriesTab.tsx` `startContentSeriesLipsync` (~5563) | `POST /series/lipsync` | LEGACY / UI Cutover |
| `ContentFamixaSeriesTab.tsx` `assembleContentSeriesCut` (~6014, 6221, 6419) | assemble cut path | LEGACY / UI Cutover |
| `ShotProductionAssemble.ts` | `POST /content/series/assemble` | LEGACY / UI Cutover |
| `ContentFamixaCharacterWorkspace.tsx` | `generateContentSeriesStill` | LEGACY / UI Cutover |
| `production-os-action-authority.ts` | CREATE_VIDEO only | Partial OS / UI Cutover |

`MixExplicitAsync` remains on the old engine as a leftover. OS `MixVideoAsync` does not call it. FACT

---

## 13. CROSS-SHOT / CROSS-SCOPE TESTS

TESTED

- SH01 VIDEO_TAKE → SH02 CREATE_LIPSYNC → `CROSS_SHOT_FORBIDDEN`. Adapter not called. TESTED
- TEST artifact → FILM-build task (same shot code, different buildId) → `CROSS_SCOPE_FORBIDDEN`. Adapter not called. TESTED
- Existing `ProductionScopeGuard.RequireSameScope` reused. No second guard. FACT

Live HTTP cross-shot/cross-scope: UNVERIFIED (in-process only).

---

## 14. IDEMPOTENCY TESTS

TESTED

- Same VOICE idempotency key returns the same request + artifact. TESTED
- IN_FLIGHT same key throws `IN_FLIGHT_REPLAY` and does not create a second task. TESTED
- ACCEPT same key + same ACCEPTED artifact replays without a new media artifact. IMPLEMENTED
- Live HTTP idempotency: UNVERIFIED

---

## 15. INPUT MUTATION TESTS

TESTED

After VIDEO_TAKE / VOICE / LIPSYNC / MIX, `InvalidateDownstream(STORY)`:

- CREATE_VIDEO / CREATE_LIPSYNC / MIX_VIDEO / CREATE_VOICE tasks become `SUPERSEDED`. TESTED
- Old artifacts remain immutable (same SHA). TESTED
- New LIPSYNC parents the new VIDEO_TAKE + VOICE artifactIds. Old video id is not reused. TESTED
- Same idempotency + mutated story replays the frozen snapshot, does not rewrite it. TESTED

Live HTTP mutation: UNVERIFIED

---

## 16. FULL TEST SHOT TRACE

LIVE VERIFIED on official TEST scope, shot `TEST-SC01-SH02` (SH01 4A left alone).

```
STORY / ACTING     TEST STORY / TEST ACTING (compiler WHAT, not a media artifact)
CREATE_IMAGE       req 8df53482  art 9877cd99  KEYFRAME VERIFIED
CREATE_VIDEO       req 71c3c657  art 4d919423  parent 9877cd99
CREATE_VOICE       req 0ea2d4db  art 602e48a7  VOICE WAV VERIFIED
CREATE_LIPSYNC     req b9f60843  art 909bbc26  parents 4d919423 + 602e48a7
MIX_VIDEO          req cbe0b71c  art e7eb55c9  parent 909bbc26
ACCEPT             req a657d0d8  ACCEPTED = e7eb55c9
ASSEMBLE           req 7b801baf  assembly shots = [e7eb55c9]
PREVIEW            VISIBLE = e7eb55c9
```

Storage under `os-test/aaaaaaaa-0000-4000-8000-000000000371/`.  
No FINAL. No FILM READY. FACT

---

## 17. TEST RESULTS

TESTED

`dotnet test tests/KitPlatform.Content.Tests --filter FullyQualifiedName~ProductionPipelineV1Tests`

**11 / 11 passed**, including:

- Work execution T01–T24
- Pipeline / CREATE_VIDEO cutover / action contract
- Checkpoints 1, 2, 3A, 3B
- Real provider bridge foundation
- Paid network permit
- **Complete action authority V1** (happy path, missing dep, wrong artifact, cross-shot, cross-scope, idempotency, in-flight, mutation, failed verify + retry, legacy source scan)

---

## 18. BUILD RESULTS

TESTED

- `KitPlatform.Packs.Content.Application` — compile OK
- `KitPlatform.Content.Tests` — compile OK
- `KitPlatform.Api` — compile OK (controller `Length: > i` pattern fixed)

---

## 19. LIVE TEST SCOPE RESULTS

LIVE VERIFIED

Official TEST:

- test_run `aaaaaaaa-0000-4000-8000-000000000371`
- scope `bbbbbbbb-0000-4000-8000-000000000371`
- shot used: `TEST-SC01-SH02`
- SH01 KEYFRAME `33333333-0000-4000-8000-000000000371` unchanged
- SH01 4A VIDEO_TAKE `63bca29a-f899-4008-a1bf-6000787bafd6` SHA `f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17` still CURRENT + VISIBLE

FILM before = after:

| Probe | Value |
|---|---|
| series_build | 4 |
| EP01 graph md5 | `12289be18284fbd4d4b166b82c09eba0` |
| EP99 graph md5 | `c7cd2516627332c2648c1509395e4a64` |
| film_art | 0 |
| film_bind | 0 |

`authorizePaidNetwork` was false on every POST. Fixture job ids only. Zero paid provider calls. LIVE VERIFIED

---

## 20. UNVERIFIED

- Live HTTP idempotency / process-restart recovery
- Live HTTP cross-shot / cross-scope / mutation
- Live HTTP failed-verification retry
- KEYFRAME PNG decodability inside `VerifyArtifact` (bytes exist + SHA; decode not gated)
- Whether every Director CTA on every desk now hits OS (they do not; see §12)
- Film render / FINAL
- Real Runway / Veo / Gemini / Fal / ElevenLabs through the new actions

---

## 21. REMAINING GAPS

RECOMMENDED / FACT

1. **UI Cutover** — Series desk still owns still / turbo / lipsync / assemble HTTP. Recorded, not patched.
2. **Assembly render** — typed boundary only. No FINAL artifact. Do not claim FILM READY.
3. **KEYFRAME decode gate** — report, do not weaken. Existing length/SHA path remains.
4. **VOICE parent list** — empty by design (parallel WHAT). A later Acting contract can add an acting-artifact parent if one exists.
5. **`MixExplicitAsync` leftover** — not OS authority; keep until a cleanup phase.
6. **CREATE_VIDEO paid path** — still the 4A Runway bridge. Out of scope here.

---

## 22. NEXT PHASE

RECOMMENDED

STOP. Do not automatically start:

- Real Runway / Veo / Gemini / ElevenLabs / Fal
- Film rendering
- UI cutover
- Provider benchmarking
- 4A micro-fixes

The only output of this phase is a single Production OS that can execute the in-scope actions on TEST fixtures/imports with deterministic artifact lineage.

**Suggested later phase (not started):** Production OS UI Cutover V1 — map each desk CTA 1:1 to `POST /production/requests` and leave `/series/*` as read-only compatibility.

---

NO SPEND. NO FILM. NO MICRO-FIX LOOP. END.
