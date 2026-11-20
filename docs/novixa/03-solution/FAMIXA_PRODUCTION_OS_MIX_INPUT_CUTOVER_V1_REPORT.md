# FAMIXA PRODUCTION OS — MIX INPUT CUTOVER V1

Date: 2026-09-19  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE VERIFY  
Scope: Input authority cutover for MIX_VIDEO on **OS-controlled shots**.  
Safety: Fal / Runway / Veo / Gemini / Wan / ElevenLabs = 0. Real TTS / LipSync / video generation = 0. Fixture Mix only. EP01 / EP99 not written.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

**PASS_WITH_GAPS**

MIX now closes the shot media chain on Production OS:

CURRENT:LIPSYNC → exact ProductionArtifact(role=LIPSYNC) → MIX_VIDEO Action Contract → Request → Task → immutable InputSnapshot → Attempt → FixtureMixAdapter → physical bytes → Verify → ProductionArtifact(role=MIX) → CURRENT:MIX → Shot State

Canonical parent is CURRENT:LIPSYNC only. MIX does not reconstruct Video+Voice from URLs. After INPUT_FROZEN, later CURRENT:LIPSYNC changes do not mutate Task A.

API was rebuilt and restarted on `localhost:5290`. Live GET / POST / reload / DB were performed.

Gaps that keep the verdict from PASS: official-film Director still uses `lipsyncUrl` / `ShotProductionFinalStore` as a **LEGACY display** plane; that leftover was not deleted and was not migrated. Mix quality was not judged.

---

## 2. SAFETY / SCOPE

| Rule | Result | Class |
|---|---|---|
| Real Fal / Runway / Veo / Gemini / Wan / ElevenLabs | 0 | FACT / LIVE_VERIFIED |
| Real TTS / LipSync / video generation | 0 | FACT |
| Mix adapter | FixtureMixAdapter unpaid / no network | FACT / LIVE_VERIFIED |
| FFmpeg redesign | not done | FACT |
| Assembly / Final / Official Film Preview / Director UI | not modified | FACT |
| LipSync / Voice / Motion / Timing / Visual Authority | not modified | FACT |
| Official film writes | 0 | LIVE_VERIFIED |
| Schema migration | none | FACT |
| New Mix orchestrator / MixCurrent state machine | none | FACT |

---

## 3. FORENSIC MIX PATH

Traced before authority code change.

| Hop | Route | Class |
|---|---|---|
| UI CTA Hoàn thiện Shot | SeriesTab `executeProductionOsAction` → MIX_VIDEO | AUTHORITY (OS) |
| Readiness (before) | `Has(lipsync) \|\| Has(motion)` | BYPASS — motion without LipSync could enable MIX |
| POST | `/content/production/requests` action=MIX_VIDEO | AUTHORITY (OS) |
| Execute (before) | `MixVideoAsync` required LipsyncArtifactId, generic `MISSING_DEPENDENCY` | AUTHORITY with gap |
| Parent check | RequireParent role=LIPSYNC | AUTHORITY |
| Physical bytes (before) | not required | GAP |
| Adapter | FixtureMixAdapter via DI | AUTHORITY HOW |
| Output | Verify → MIX artifact → BindNamedProtected CURRENT:MIX | AUTHORITY |
| `lipsyncUrl` / `previewUrl` / `finalUrl` | official-film graph / FinalStore | LEGACY |
| `ShotProductionFinalStore` | browser final identity | LEGACY / CACHE |
| `acceptedTake.url` / takeUrl / videoUrl | series graph | LEGACY |
| IndexedDB / browser blob | official-film display | CACHE / LEGACY |
| Fal Mix / browser WebM | unused on OS MIX path | BYPASS if used as authority |

FACT: MIX_VIDEO already existed. This phase cut over **input authority** and physical parent proof. It did not invent a new action name.

---

## 4. AUTHORITY BEFORE

| Input | Current Source | Target Source | Risk |
|---|---|---|---|
| Mix action | MIX_VIDEO | same | Low |
| LipSync input | claimed LipsyncArtifactId | CURRENT:LIPSYNC → artifactId | Med if latest/URL |
| Readiness | LipSync **or Motion** | CURRENT:LIPSYNC only | High — motion bypass |
| Missing parent | MISSING_DEPENDENCY | MISSING_LIPSYNC_ARTIFACT | Med |
| Physical SHA | not required | required | High |
| lipsyncUrl / previewUrl / FinalStore | official-film | must not override OS | High if used |
| Mix output | ProductionArtifact MIX + CURRENT:MIX | same | Low |

---

## 5. AUTHORITY AFTER

| Input | Source now | Class |
|---|---|---|
| Mix execution input | CURRENT:LIPSYNC at evaluate time → frozen ParentArtifactIds[0] | AUTHORITY |
| LipSync SHA | MotionHash = Hash(lipsync SHA) | AUTHORITY |
| Provider / Model | fixture / fixture-mix-v1 frozen on Task + snapshot | AUTHORITY HOW |
| Mix output | ProductionArtifact role=MIX + CURRENT:MIX | AUTHORITY |
| GET actions/state lipsyncArtifactId | Production OS mix verdict | AUTHORITY |
| lipsyncUrl / FinalStore / previewUrl | LEGACY official-film | not OS MIX input |

IMPLEMENTED / TESTED / LIVE_VERIFIED on TEST-SC01-SH01.

---

## 6. MIX ACTION CONTRACT

FACT / IMPLEMENTED / TESTED

Action remains `MIX_VIDEO`. CTA remains `Hoàn thiện Shot`. Output role `MIX`.

Required inputs now: `LipSyncArtifactId`, `Timing`.

Forbidden fallbacks now include: latest lipsync, latest take, lipsyncUrl, previewUrl, finalUrl, acceptedTake.url, takeUrl, videoUrl, audioUrl, voiceUrl, provider URL, browser blob, indexedDB.kit-famixa-tts, ShotProductionFinalStore.

Deterministic rejects:

| Condition | Reason |
|---|---|
| LipsyncArtifactId missing | `MISSING_LIPSYNC_ARTIFACT` |
| Claimed ID ≠ CURRENT:LIPSYNC | `BINDING_CHANGED` |
| Cross-shot | `CROSS_SHOT_FORBIDDEN` |
| Cross-build / official film consuming TEST | `CROSS_SCOPE_FORBIDDEN` |
| URL used as identity | `URL_AUTHORITY_FORBIDDEN` |

Live POST without lipsyncId → `MISSING_LIPSYNC_ARTIFACT`. LIVE_VERIFIED.

---

## 7. INPUT SNAPSHOT

IMPLEMENTED / TESTED / LIVE_VERIFIED

Live snapshot `e43da20c-69bb-431f-a5f6-300bfb2c73a6`:

| Field | Value |
|---|---|
| SnapshotHash | `4f79a66d0ef175688e66eac71bc5bff119505fac1e0971065fce4a26626b0a73` |
| Parents | `8eaad200-f6cc-4395-9c14-d8e458ef32bd` |
| MotionHash | Hash of LipSync SHA |
| Provider / Model | fixture / fixture-mix-v1 |

URLs are not identity.

---

## 8. PHYSICAL INPUT

IMPLEMENTED / TESTED / LIVE_VERIFIED

Before Mix: role=LIPSYNC, VERIFIED, storageKey exists and is not URL, SHA exists, physical bytes SHA-match, build/shot match.

Preferred previous-phase LipSync `8eaad200-…` was valid and used. FACT.

---

## 9. MIX EXECUTION

IMPLEMENTED / TESTED / LIVE_VERIFIED

Canonical server path: `MixVideoAsync` → OpenFrozen → `FixtureMixAdapter.ExecuteFrozenAsync` → `MaterializeMediaAsync`.

DI `IMixExecutionAdapter` = FixtureMixAdapter (`IsPaid=false`, `RequiresNetwork=false`). Requires parent ID + MotionHash. Seed = `mix|` + SnapshotHash + MotionHash + parentId.

No browser Mix. No Fal. Existing FFmpeg stack was not redesigned and was not invoked; fixture is the unpaid Production OS Mix boundary already registered.

Live attempt `15891a11-…` provider_job_id = `fixture-mix-84db9e37e8db`.

---

## 10. OUTPUT ARTIFACT

LIVE_VERIFIED TEST-SC01-SH01

| Field | Value |
|---|---|
| artifactId | `ab07ce5a-b5b3-4950-911c-0d11009d00f9` |
| role | MIX |
| status | VERIFIED |
| taskId | `84db9e37-e8db-4844-9ee7-2e0c99431364` |
| attemptId | `15891a11-a0cd-462f-a78f-68d53a142e58` |
| inputSnapshotHash | `4f79a66d…` |
| provider / model | fixture / fixture-mix-v1 |
| byteSha256 | `4722fbacb69b64a3a4e11d603d9af2ffdcb990d6669fe6fdc04ade4772b22182` |
| storageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/15891a11a0cd462fa78f68d53a142e58` |
| parents | exact LipSync `8eaad200-…` |

---

## 11. CURRENT:MIX

IMPLEMENTED / TESTED / LIVE_VERIFIED

Existing `CURRENT:MIX` / `VISIBLE:MIX`. No CurrentMixState / LatestMix / MixCurrent.

Live SH01 CURRENT chain:

| Role | Artifact |
|---|---|
| CURRENT:VIDEO_TAKE | `5872d0a9-0c3f-42b0-9fe5-21f1433ab831` |
| CURRENT:VOICE | `84ca5121-1550-49cb-a988-ee42a26ad48b` |
| CURRENT:LIPSYNC | `8eaad200-f6cc-4395-9c14-d8e458ef32bd` |
| CURRENT:MIX | `ab07ce5a-b5b3-4950-911c-0d11009d00f9` |

Reload GET `/state` after POST returns the same IDs. LIVE_VERIFIED.

---

## 12. INVALIDATION

IMPLEMENTED / TESTED

`ShotDependencyGraph`: Lipsync → Mix. `InvalidateDownstreamAsync` unbinds CURRENT:MIX. Old MIX artifact remains immutable.

T23 PASS in-process. Live invalidation after the SH01 proof bind was not re-run. TESTED; live invalidation UNVERIFIED (intentionally not broken after proof).

---

## 13. REGENERATION

IMPLEMENTED / TESTED

New CURRENT:LIPSYNC → new MIX Task. Old snapshot parents stay A. Idempotency keyed by lipsync artifactId.

T24 PASS. One live MIX Task created on SH01. LIVE_VERIFIED for one new Task.

---

## 14. LATE RESULT

IMPLEMENTED / TESTED

`BindNamedProtectedAsync` + `LATE_RESULT_NOT_CURRENT`. Older Mix attempt cannot steal CURRENT:MIX.

T25 PASS. Not replayed live after the SH01 bind. TESTED.

---

## 15. CROSS-SCOPE

IMPLEMENTED / TESTED

| Case | Reason |
|---|---|
| SH01 LipSync → SH02 MIX | CROSS_SHOT_FORBIDDEN; adapter calls = 0 |
| Other TEST build | CROSS_SCOPE_FORBIDDEN |
| Official EP01 / EP99 consuming TEST LipSync | CROSS_SCOPE_FORBIDDEN |

Not posted live against EP01/EP99 (would write film scope). TESTED in-process. EP01/EP99 DB unchanged. LIVE_VERIFIED safety.

---

## 16. LEGACY SOURCE ISOLATION

IMPLEMENTED / TESTED

OS MIX path does not read lipsyncUrl, previewUrl, finalUrl, acceptedTake.url, ShotProductionFinalStore, IndexedDB, or browser blobs.

`ForbidUrlIdentity` rejects URL strings. `LegacySeriesGraphAdapter.MayDriveNewBinding` is false.

Compatibility code remains. Official Film Preview not migrated. FACT.

---

## 17. TEST MATRIX

Suite: `FAMIXA_PRODUCTION_OS_MIX_INPUT_CUTOVER_V1`  
xUnit: `ProductionMixInputCutoverV1Tests` PASS  
Smoke: `famixa-mix-input-cutover.smoke.ts` PASS FAIL=0  
Related: lipsync / voice / work-execution / pipeline PASS  
Live API: GET `/video-engine/video-generation-execution/regression` status=PASS fail=0 generate=false runway=false

| ID | Result | Class |
|---|---|---|
| T01 MIX Action resolves | PASS | TESTED |
| T02 Missing LIPSYNC rejected | PASS | TESTED + LIVE_VERIFIED |
| T03 CURRENT:LIPSYNC exact | PASS | TESTED + LIVE_VERIFIED |
| T04 LipSync role verified | PASS | TESTED + LIVE_VERIFIED |
| T05 LipSync SHA verified | PASS | TESTED + LIVE_VERIFIED |
| T06 Physical storage verified | PASS | TESTED + LIVE_VERIFIED |
| T07 InputSnapshot created | PASS | TESTED + LIVE_VERIFIED |
| T08 LipSyncArtifactId frozen | PASS | TESTED + LIVE_VERIFIED |
| T09 LipSync SHA frozen | PASS | TESTED + LIVE_VERIFIED |
| T10 Build/shot frozen | PASS | TESTED + LIVE_VERIFIED |
| T11 Mix parameters / provider frozen | PASS | TESTED + LIVE_VERIFIED |
| T12 Canonical server path | PASS | TESTED + LIVE_VERIFIED |
| T13 No browser media authority | PASS | TESTED |
| T14 No provider URL authority | PASS | TESTED + LIVE_VERIFIED |
| T15 Output physical bytes | PASS | TESTED + LIVE_VERIFIED |
| T16 Output SHA | PASS | TESTED + LIVE_VERIFIED |
| T17 Output role=MIX | PASS | TESTED + LIVE_VERIFIED |
| T18 Lineage Attempt | PASS | TESTED + LIVE_VERIFIED |
| T19 Lineage InputSnapshot | PASS | TESTED + LIVE_VERIFIED |
| T20 CURRENT:MIX created | PASS | TESTED + LIVE_VERIFIED |
| T21 Reload preserves CURRENT:MIX | PASS | TESTED + LIVE_VERIFIED |
| T22 Old MIX immutable | PASS | TESTED |
| T23 New LipSync invalidates MIX | PASS | TESTED |
| T24 Regeneration new Task | PASS | TESTED |
| T25 Late Mix cannot steal CURRENT:MIX | PASS | TESTED |
| T26 Cross-shot rejected | PASS | TESTED |
| T27 Cross-build rejected | PASS | TESTED |
| T28 Cross-scope rejected | PASS | TESTED |
| T29 Legacy lipsyncUrl cannot override | PASS | TESTED |
| T30 Legacy previewUrl cannot override | PASS | TESTED |
| T31 TEST cannot write EP01 | PASS | TESTED + LIVE_VERIFIED (no EP01 writes) |
| T32 TEST cannot write EP99 | PASS | TESTED + LIVE_VERIFIED (no EP99 writes) |
| T33 Provider calls = 0 | PASS | FACT + LIVE_VERIFIED |
| T34 AI media generation = 0 | PASS | FACT |
| T35 No new orchestrator | PASS | TESTED (source) |
| T36 No competing Mix state machine | PASS | TESTED (source) |

---

## 18. LIVE VERIFICATION

API rebuilt (`dotnet build` succeeded) and restarted. `Now listening on: http://localhost:5290`. Login `KIT_MKT` / `admin` succeeded.

| Check | Result | Class |
|---|---|---|
| GET SH01 `/state` before MIX | VIDEO `5872d0a9-…` VOICE `84ca5121-…` LIPSYNC `8eaad200-…` MIX missing | LIVE_VERIFIED |
| GET SH01 `/actions` MIX_VIDEO | available=true; lipsyncArtifactId=`8eaad200-…` | LIVE_VERIFIED |
| POST missing LipSync | `MISSING_LIPSYNC_ARTIFACT` | LIVE_VERIFIED |
| POST MIX_VIDEO fixture | Request `b2fb774a-…` Task `84db9e37-…` Snapshot `e43da20c-…` Attempt `15891a11-…` Artifact `ab07ce5a-…` | LIVE_VERIFIED |
| Parent frozen | `8eaad200-…` | LIVE_VERIFIED |
| GET `/state` after | CURRENT:MIX `ab07ce5a-…`; prior VIDEO/VOICE/LIPSYNC unchanged | LIVE_VERIFIED |
| In-process regression on running API | PASS fail=0 | LIVE_VERIFIED |
| SH02 historical CURRENT:* | unchanged | LIVE_VERIFIED |

Never claim LIVE_VERIFIED for a Fal/FFmpeg quality run. There was none.

---

## 19. EP01 SAFETY

| Field | Value |
|---|---|
| series_build id | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| title | Tập 01 |
| updated_at | 2026-09-18 23:01:02.129634+07 |
| graph_json md5 | `415fedf306ed23d1ace94a794400b440` |
| active bindings | 0 |

Unchanged this phase. LIVE_VERIFIED.

---

## 20. EP99 SAFETY

| Field | Value |
|---|---|
| series_build id | `77262ec0-74ad-459d-9128-f09bb553a5dd` |
| title | TEST BATCH 3 SHORT |
| updated_at | 2026-09-07 16:52:54.467199+07 |
| graph_json md5 | `c7cd2516627332c2648c1509395e4a64` |
| active bindings | 0 |

Unchanged this phase. LIVE_VERIFIED.

---

## 21. REMAINING GAPS

1. Official-film `lipsyncUrl` / `ShotProductionFinalStore` / assemble-from-graph remain LEGACY display. Not deleted. OS MIX does not use them. RECOMMENDED only if a later phase migrates official-film **display**.
2. In-browser FinalStore presence/absence not live-clicked. TESTED in source. UNVERIFIED in browser.
3. Live late-result / invalidation after the SH01 bind were not re-executed (would destroy the proof bind). TESTED in-process.
4. Mix quality / FFmpeg audio-video polish not evaluated. FACT — out of scope.
5. Assembly / Final still downstream and not cut over. FACT.

---

## 22. NEXT STEP

Not another forensic micro-phase.

**FAMIXA PRODUCTION OS — FULL FILM VERTICAL RUN V1**

Prove one coherent Production OS chain:

Story → Shot → Video → Voice → LipSync → Mix → Assembly → Final

Still fixture / 0 paid providers unless that phase explicitly authorizes a controlled TEST run.

Do not start that phase here.

---

## 23. STOP

This phase is closed.

The shot media dependency chain on Production OS is now:

CURRENT:VIDEO_TAKE → CURRENT:VOICE → CURRENT:LIPSYNC → CURRENT:MIX

each an exact immutable ProductionArtifact.

No commit. No deploy. No Fal. No official film writes. No Assembly / Final / Official Film Preview work started.
