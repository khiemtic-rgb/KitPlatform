# FAMIXA PRODUCTION OS — CREATE_LIPSYNC INPUT CUTOVER V1

Date: 2026-09-19  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE VERIFY  
Scope: Input authority cutover for CREATE_LIPSYNC on **OS-controlled shots**.  
Safety: Fal calls this phase = 0. Real LipSync generation = 0. Real video / audio generation = 0. ElevenLabs / Runway / Veo / Gemini / Wan = 0. EP01 / EP99 not written.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

**PASS_WITH_GAPS**

CREATE_LIPSYNC on Production OS now consumes exact immutable artifact identities:

CURRENT:VIDEO_TAKE → ProductionArtifact(role=VIDEO_TAKE) → VideoArtifactId + SHA + storageKey  
CURRENT:VOICE → ProductionArtifact(role=VOICE) → VoiceArtifactId + SHA + storageKey

USER INTENT  
→ CREATE_LIPSYNC Action Contract  
→ ProductionRequest  
→ ProductionTask  
→ immutable InputSnapshot (VideoArtifactId + VoiceArtifactId + SHAs + provider/model)  
→ ProductionAttempt  
→ FixtureLipSyncAdapter  
→ physical bytes  
→ Verify  
→ ProductionArtifact(role=LIPSYNC)  
→ CURRENT:LIPSYNC  
→ Shot State

After INPUT_FROZEN, later CURRENT changes do not mutate Task A. A new CREATE_LIPSYNC is a new Task.

This phase rebuilt and restarted `localhost:5290`. Live GET / POST / reload / DB were performed. The previous CREATE_VOICE reporting gap (API down / GET `/voice` UNVERIFIED) is not repeated.

Gaps that keep the verdict from PASS: official-film Director still uses `resolveLineAudio` / `kit-famixa-tts` as a **LEGACY display** plane; historical TEST-SC01-SH02 voice `3a753477-…` still has empty `performance_payload_json` and is fail-closed for line-verified lipsync (not silently repaired). Mix was not migrated.

---

## 2. SAFETY / SCOPE

| Rule | Result | Class |
|---|---|---|
| Real Fal Start / Poll / Download / Upload / LipSync | 0 | FACT / LIVE_VERIFIED |
| ElevenLabs / Runway / Veo / Gemini / Wan this phase | not invoked | FACT |
| Vendor media generation | 0 | FACT |
| Fixture LipSync / fixture Voice through Production OS | yes (TEST-SC01-SH01) | LIVE_VERIFIED |
| FFmpeg / MIX / ASSEMBLY / FINAL | not run | FACT |
| Official film media | not written | LIVE_VERIFIED |
| Schema migration | none | FACT |
| New orchestrator / LipSyncCurrent state machine | none | FACT |
| Fal client / Fal adapter / speaker targeting semantics | not modified | FACT |
| Director layout / labels / Mix / Assembly / Final | not modified | FACT |
| Historical SH02 empty PerformancePayloadJson | not repaired | FACT |

---

## 3. FORENSIC LIPSYNC PATH

Traced before authority code change. Classification is AUTHORITY / CACHE / LEGACY / DERIVED / BYPASS.

| Hop | Route | Class |
|---|---|---|
| UI CTA Khớp môi | SeriesTab `executeProductionOsAction` → CREATE_LIPSYNC | AUTHORITY (OS) |
| Readiness | GET `/production/shots/{build}/{shot}/actions` + `/state` | AUTHORITY (OS) |
| Voice helper | `resolveCurrentVoiceArtifactId` + GET `/voice` | AUTHORITY (OS) |
| POST | `/content/production/requests` action=CREATE_LIPSYNC | AUTHORITY (OS) |
| Evaluate | `EvaluateCreateLipsyncAsync` fills CURRENT:VIDEO_TAKE + CURRENT:VOICE | AUTHORITY |
| Execute | `CreateLipsyncAsync` → RequireParent + RequirePhysicalParent → OpenFrozen → Attempt → FixtureLipSyncAdapter | AUTHORITY |
| Output | Verify → ProductionArtifact(LIPSYNC) → BindNamedProtected CURRENT:LIPSYNC | AUTHORITY |
| Preview | VISIBLE binding (OS) | AUTHORITY preview |
| `kit-famixa-tts` / `loadTtsBlobAny` | IDB | CACHE / RECOVERY |
| `resolveLineAudio` | official-film hydrate / assemble | LEGACY (official film only) |
| `acceptedTake.url` / `takeUrl` / `previewUrl` / graph `videoUrl` | series graph | LEGACY / NON-AUTHORITY for OS |
| `audioUrl` / `voiceUrl` / `lipsyncUrl` | series graph | LEGACY / NON-AUTHORITY for OS |
| voiceId alone / latest video / latest voice | heuristic | BYPASS — now forbidden |
| Fal LipSync adapter | `ContentFalClient.CreateLipsyncAsync` | BYPASS if used as authority; unused this phase |

FACT: before this phase CREATE_LIPSYNC already required MotionArtifactId + VoiceArtifactId and used FixtureLipSync. Gaps were generic MISSING_DEPENDENCY, silent CURRENT fill without evaluate-time match, Voice SHA not frozen, no dedicated VideoArtifactId/VoiceArtifactId on GET, no voice line check, fixture seed without MotionHash/VoiceHash.

---

## 4. AUTHORITY BEFORE

| Input | Current Source | Target Source | Risk |
|---|---|---|---|
| Video input | claimed MotionArtifactId or CURRENT alias | CURRENT:VIDEO_TAKE → artifactId | Med if alias/latest |
| Video artifact | RequireParent role VIDEO_TAKE | same + SHA + storageKey + physical bytes | Med |
| Video URL | acceptedTake.url / takeUrl / previewUrl still in graph | must not be identity | High if used |
| Voice input | claimed VoiceArtifactId or CURRENT:VOICE | CURRENT:VOICE → artifactId | Med |
| Voice artifact | RequireParent role VOICE | same + SHA + storage + line | Med |
| Voice URL | graph audioUrl / voiceUrl | forbidden | High |
| VoiceId | CREATE_VOICE identity only | not LipSync input identity | High if used alone |
| IndexedDB audio | official-film resolveLineAudio | CACHE only | High if OS reads it |
| LineId | optional / UI LINE-${shot.id} | freeze when present; match voice snapshot | Med |
| SpeakerCharacterId | existing contract | freeze when present | Low |
| Targeting metadata | existing Fal speaker fields | preserve; freeze resolved form | Low — not redesigned |
| Provider / Model | FixtureLipSync fixture/fixture-lipsync-v1 | freeze on Task + snapshot | Low |
| LipSync output | ProductionArtifact LIPSYNC + CURRENT:LIPSYNC | same; no lipsyncUrl authority | Med if URL leftover |

---

## 5. AUTHORITY AFTER

| Input | Source now | Class |
|---|---|---|
| Video execution input | CURRENT:VIDEO_TAKE at evaluate time → frozen ParentArtifactIds[0] | AUTHORITY |
| Voice execution input | CURRENT:VOICE at evaluate time → frozen ParentArtifactIds[1] | AUTHORITY |
| Video SHA / Voice SHA | MotionHash = Hash(video SHA); VoiceHash = Hash(videoId\|voiceId\|voiceSha\|line\|speaker) | AUTHORITY |
| Provider / Model | Task + InputSnapshot fixture / fixture-lipsync-v1 | AUTHORITY HOW |
| LipSync output | ProductionArtifact role=LIPSYNC + CURRENT:LIPSYNC | AUTHORITY |
| GET actions/state VideoArtifactId / VoiceArtifactId | Production OS verdict | AUTHORITY |
| kit-famixa-tts | CACHE / RECOVERY | not execution input |
| acceptedTake.url / takeUrl / previewUrl | LEGACY | ForbidUrlIdentity |
| Official-film resolveLineAudio | LEGACY display | not OS CREATE_LIPSYNC |

IMPLEMENTED / TESTED / LIVE_VERIFIED on TEST-SC01-SH01 (POST) and TEST-SC01-SH02 (GET IDs).

---

## 6. CREATE_LIPSYNC ACTION CONTRACT

FACT / IMPLEMENTED / TESTED

Action remains `CREATE_LIPSYNC`. CTA remains `Khớp môi`. Output role `LIPSYNC`.

Required production inputs (existing contract, not invented):

- MotionArtifactId (VideoArtifactId)
- VoiceArtifactId
- FrozenProviderModel
- PerformanceSnapshot (TEST scope, existing Phase C)

Forbidden fallbacks now include: `indexedDB.kit-famixa-tts`, latest video, latest voice, voiceId alone, provider URL, acceptedTake.url, takeUrl, previewUrl, audioUrl, voiceUrl, lipsyncUrl.

Deterministic rejects:

| Condition | Reason |
|---|---|
| MotionArtifactId missing | `MISSING_VIDEO_ARTIFACT` |
| VoiceArtifactId missing | `MISSING_VOICE_ARTIFACT` |
| Claimed IDs ≠ CURRENT at evaluate | `BINDING_CHANGED` |
| Cross-shot parents | `CROSS_SHOT_FORBIDDEN` |
| Cross-build / official film consuming TEST | `CROSS_SCOPE_FORBIDDEN` |
| LineId present and voice snapshot line ≠ requested | `PERFORMANCE_LINE_MISMATCH` |
| URL used as identity | `URL_AUTHORITY_FORBIDDEN` |

No fallback to latest / URL / IDB / voiceId.

LIVE_VERIFIED: POST missing video → `MISSING_VIDEO_ARTIFACT`; POST missing voice → `MISSING_VOICE_ARTIFACT`.

---

## 7. EXACT VIDEO INPUT

IMPLEMENTED / TESTED / LIVE_VERIFIED

Resolver: `ResolveCurrentTypedArtifactAsync(VIDEO_TAKE)`  
= binding `CURRENT:VIDEO_TAKE`, or `CURRENT` **only if** that artifact role is VIDEO_TAKE.

ACCEPTED is not automatic input.

Verify: role=VIDEO_TAKE, VERIFIED, SHA, storageKey (no `://`), build match, shot match, physical bytes SHA-match.

Live TEST-SC01-SH01 CURRENT:VIDEO_TAKE = `5872d0a9-0c3f-42b0-9fe5-21f1433ab831` (existing verified TEST VIDEO_TAKE; not newly generated this phase).  
Live TEST-SC01-SH02 CURRENT:VIDEO_TAKE = `583d7700-f5b9-44c9-97db-5277572b628b`.

---

## 8. EXACT VOICE INPUT

IMPLEMENTED / TESTED / LIVE_VERIFIED

Resolver: `ResolveCurrentTypedArtifactAsync(VOICE)` = `CURRENT:VOICE` only. No CURRENT alias for voice.

Verify: role=VOICE, VERIFIED, SHA, storageKey, build, shot, physical bytes. If LineId present: `ProductionVoiceLineIdentity.Matches` against snapshot `PerformancePayloadJson` `"lineId"`.

Live TEST-SC01-SH01 CURRENT:VOICE after fixture CREATE_VOICE = `84ca5121-1550-49cb-a988-ee42a26ad48b` (new OS fixture; payload frozen; no TTS).  
Live TEST-SC01-SH02 CURRENT:VOICE remains `3a753477-fef0-4326-8423-a5d7c316be37` (historical empty payload; not repaired). GET `/voice?lineId=L1` on SH02 → `PERFORMANCE_LINE_MISMATCH`. LIVE_VERIFIED fail-closed.

---

## 9. INPUT SNAPSHOT

IMPLEMENTED / TESTED / LIVE_VERIFIED

CREATE_LIPSYNC InputSnapshot freezes:

- ParentArtifactIds = [VideoArtifactId, VoiceArtifactId]
- VideoArtifactSha256 via MotionHash
- VoiceArtifactId + VoiceArtifactSha256 + LineId + Speaker via VoiceHash
- BuildId / ShotId (task scope)
- ProviderId / ModelId
- existing PerformanceSnapshotId / PerformanceHash / payload when TEST requires it
- existing duration / compiler / story hashes (existing compiler)

Live snapshot `5ebcee46-5d05-4c4b-958f-b4f6946d6126`:

| Field | Value |
|---|---|
| SnapshotHash | `08cc7ce4be90460d18506c7fd1944d8708b3b13fffdb22a82d10380fda859d8d` |
| Parents | `5872d0a9-…` + `84ca5121-…` |
| Provider / Model | fixture / fixture-lipsync-v1 |
| PerformanceSnapshotId | `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e` |
| MotionHash | present (video SHA) |
| VoiceHash | present (ids + voice SHA + L1 + CHAR-001) |

IDs + SHAs are identity. videoUrl / voiceUrl / voiceId are not.

---

## 10. PROVIDER / MODEL FREEZE

IMPLEMENTED / TESTED / LIVE_VERIFIED

Frozen on Task + InputSnapshot + Attempt:

- ProviderId = `fixture`
- ModelId = `fixture-lipsync-v1`

No silent Fal model A → B. Changing provider/model requires a new Task.

Live attempt `f13664b4-…` provider_job_id = `fixture-lipsync-d6f0879e8160` (fixture token, not a Fal job).

---

## 11. ATTEMPT

IMPLEMENTED / TESTED / LIVE_VERIFIED

CREATE_LIPSYNC creates ProductionAttempt before adapter execution. Live attempt status `PROVIDER_SUCCEEDED` with fixture token. No vendor job semantics invented.

---

## 12. FIXTURE EXECUTION

IMPLEMENTED / TESTED / LIVE_VERIFIED

`FixtureLipSyncAdapter` is the DI `ILipSyncExecutionAdapter` (`IsPaid=false`, `RequiresNetwork=false`). It requires ParentArtifactIds ≥ 2 and non-empty MotionHash + VoiceHash. Seed = SnapshotHash + MotionHash + VoiceHash + parent IDs. Output is deterministic `ProductionTestMp4` bytes.

Same Request → Task → Snapshot → Attempt → Artifact → Binding path as paid adapters would use. Fal adapter / Fal client not called.

DI still registers FixtureLipSync as the LipSync adapter. FACT.

---

## 13. OUTPUT ARTIFACT

LIVE_VERIFIED TEST-SC01-SH01

| Field | Value |
|---|---|
| artifactId | `8eaad200-f6cc-4395-9c14-d8e458ef32bd` |
| role | LIPSYNC |
| status | VERIFIED |
| taskId | `d6f0879e-8160-4f75-aea2-262f04d5541d` |
| attemptId | `f13664b4-a6ac-48a6-933d-d2023d4cd895` |
| inputSnapshotHash | `08cc7ce4…` |
| provider / model | fixture / fixture-lipsync-v1 |
| byteSha256 | `2085972ec1f6e561496c38dec4f853a7d2385b8d418275719a24268d66946956` |
| storageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/f13664b4a6ac48a6933dd2023d4cd895` |
| mime / duration | video/mp4 / 5s (fixture) |
| parents | exact Video A + Voice A |

No provider URL as authority.

---

## 14. CURRENT:LIPSYNC

IMPLEMENTED / TESTED / LIVE_VERIFIED

Existing `ProductionBindingRoles.CurrentFor(LIPSYNC)` = `CURRENT:LIPSYNC`. Also VISIBLE:LIPSYNC via existing bindVisible path.

No CurrentLipSyncState / LatestLipSync / LipSyncCurrent machine.

Live SH01 active bindings: CURRENT:VIDEO_TAKE=`5872d0a9-…`, CURRENT:VOICE=`84ca5121-…`, CURRENT:LIPSYNC=`8eaad200-…`.

Reload GET `/state` returns the same three IDs. LIVE_VERIFIED.

---

## 15. INDEXEDDB CUTOVER

IMPLEMENTED / TESTED

OS execution path (`CreateLipsyncAsync`, `EvaluateCreateLipsyncAsync`, SeriesTab `executeProductionOsAction`) does not call `loadTtsBlobAny`, `kit-famixa-tts`, or IDB.

IDB stores remain marked CACHE / RECOVERY. Official-film `resolveLineAudio` remains LEGACY display.

GET `/voice` line mismatch keeps OS CURRENT:VOICE node; never falls back to IDB. IMPLEMENTED.

Live browser IDB presence/absence not exercised. UNVERIFIED in-browser; TESTED in source + regression T18/T42/T43.

---

## 16. LEGACY VIDEO URL CUTOVER

IMPLEMENTED / TESTED

OS path does not read acceptedTake.url / takeUrl / previewUrl / graph video URL. `ForbidUrlIdentity` rejects URL strings. `LegacySeriesGraphAdapter.MayDriveNewBinding` is false for those fields.

Live POST used artifactIds only. LIVE_VERIFIED for the successful SH01 run. Attack 4 (acceptedTake.url) is TESTED in-process.

---

## 17. SPEAKER TARGETING FREEZE

FACT / IMPLEMENTED / TESTED

Speaker targeting semantics were not redesigned. Existing `SpeakerCharacterId` is frozen into VoiceHash / VoiceRequirements when supplied.

Live SH01 POST sent `CHAR-001` (already on the PerformanceSnapshot). Targeting quality was not judged.

---

## 18. INVALIDATION

IMPLEMENTED / TESTED

Existing `ShotDependencyGraph`: Motion → LipSync; Voice → LipSync. `InvalidateDownstreamAsync` unbinds CURRENT:LIPSYNC. Old LipSync artifact remains immutable.

T31 / T32 PASS in-process. Not re-run as a live destructive invalidation after the successful SH01 bind. TESTED; live invalidation UNVERIFIED (intentionally not broken after proof).

---

## 19. REGENERATION

IMPLEMENTED / TESTED

New CREATE_LIPSYNC with new CURRENT pair = new Task. Old snapshot ParentArtifactIds stay A. Idempotency is keyed by videoId+voiceId.

T33 PASS. Live SH01 created new Request `d8578743-…` / Task `d6f0879e-…`. LIVE_VERIFIED for one new Task. Second regeneration not required for this proof.

---

## 20. LATE RESULT

IMPLEMENTED / TESTED

`BindNamedProtectedAsync` + `LATE_RESULT_NOT_CURRENT`. Older LipSync attempt cannot steal CURRENT:LIPSYNC after a newer bind.

T34 PASS. Not replayed live after the SH01 bind. TESTED.

---

## 21. CROSS-SHOT / CROSS-SCOPE

IMPLEMENTED / TESTED

| Case | Reason |
|---|---|
| SH01 video + SH02 voice | CROSS_SHOT_FORBIDDEN; adapter calls = 0 |
| Other TEST build | CROSS_SCOPE_FORBIDDEN |
| Official EP01 / EP99 consuming TEST artifacts | CROSS_SCOPE_FORBIDDEN (same shot code, different build) |
| Line L999 vs frozen L1 | PERFORMANCE_LINE_MISMATCH |

Live SH02 historical pair with `lineId=L1` → `PERFORMANCE_LINE_MISMATCH` (empty payload). Fail-closed. LIVE_VERIFIED.

TEST artifact → EP01/EP99 not posted live (would write film scope). TESTED in-process only. EP01/EP99 DB unchanged. LIVE_VERIFIED safety.

---

## 22. ATTACK TESTS

| Attack | Result | Class |
|---|---|---|
| 1. Freeze Task A (Video A+Voice A), then CURRENT→B, execute/replay A | Snapshot parents stay A; fixture seed still A; stale POST A while CURRENT=B → BINDING_CHANGED; Task B consumes B | TESTED |
| 2. CURRENT:VOICE=A, newer unbound Voice B | GetShotState VoiceArtifactId stays A | TESTED |
| 3. SH01 video + SH02 voice | CROSS_SHOT_FORBIDDEN; CountingLipSyncAdapter.Calls=0 | TESTED |
| 4. acceptedTake.url | ForbidUrlIdentity; legacy fields cannot bind | TESTED |

---

## 23. TEST MATRIX T01–T51

Suite: `FAMIXA_PRODUCTION_OS_CREATE_LIPSYNC_INPUT_CUTOVER_V1`  
xUnit: `ProductionCreateLipsyncInputCutoverV1Tests` PASS  
Smoke: `famixa-create-lipsync-input-cutover.smoke.ts` PASS FAIL=0  
Related: voice cutover / work-execution / pipeline / preview-authority PASS  
Live API in-process: GET `/video-engine/video-generation-execution/regression` status=PASS fail=0 generate=false runway=false

| ID | Result | Class |
|---|---|---|
| T01 Action Contract resolves | PASS | TESTED |
| T02 Missing VideoArtifact rejected | PASS | TESTED + LIVE_VERIFIED |
| T03 Missing VoiceArtifact rejected | PASS | TESTED + LIVE_VERIFIED |
| T04 CURRENT:VIDEO_TAKE exact | PASS | TESTED + LIVE_VERIFIED |
| T05 CURRENT:VOICE exact | PASS | TESTED + LIVE_VERIFIED |
| T06 Video SHA captured | PASS | TESTED + LIVE_VERIFIED |
| T07 Voice SHA captured | PASS | TESTED + LIVE_VERIFIED |
| T08 InputSnapshot created | PASS | TESTED + LIVE_VERIFIED |
| T09 VideoArtifactId frozen | PASS | TESTED + LIVE_VERIFIED |
| T10 VoiceArtifactId frozen | PASS | TESTED + LIVE_VERIFIED |
| T11 Provider/model frozen | PASS | TESTED + LIVE_VERIFIED |
| T12 LineId frozen | PASS | TESTED + LIVE_VERIFIED (L1) |
| T13 Speaker frozen when supplied | PASS | TESTED + LIVE_VERIFIED (CHAR-001) |
| T14 LipSync parameters / video SHA frozen | PASS | TESTED + LIVE_VERIFIED |
| T15 Fixture receives VideoArtifactId | PASS | TESTED + LIVE_VERIFIED |
| T16 Fixture receives VoiceArtifactId | PASS | TESTED + LIVE_VERIFIED |
| T17 Physical storage parents | PASS | TESTED + LIVE_VERIFIED |
| T18 No IndexedDB access | PASS | TESTED |
| T19 No provider URL access | PASS | TESTED |
| T20 No latest-video resolution | PASS | TESTED |
| T21 No latest-voice resolution | PASS | TESTED |
| T22 No voiceId-only resolution | PASS | TESTED |
| T23 ProductionAttempt created | PASS | TESTED + LIVE_VERIFIED |
| T24 Output role=LIPSYNC | PASS | TESTED + LIVE_VERIFIED |
| T25 Output SHA verified | PASS | TESTED + LIVE_VERIFIED |
| T26 Output storageKey verified | PASS | TESTED + LIVE_VERIFIED |
| T27 Lineage Attempt | PASS | TESTED + LIVE_VERIFIED |
| T28 Lineage InputSnapshot | PASS | TESTED + LIVE_VERIFIED |
| T29 CURRENT:LIPSYNC created | PASS | TESTED + LIVE_VERIFIED |
| T30 Old LipSync immutable | PASS | TESTED; SH02 historical `49deaa6b-…` SHA unchanged LIVE_VERIFIED |
| T31 Video change invalidates LipSync | PASS | TESTED |
| T32 Voice change invalidates LipSync | PASS | TESTED |
| T33 Regeneration new Task | PASS | TESTED |
| T34 Late cannot steal CURRENT:LIPSYNC | PASS | TESTED |
| T35 Cross-shot rejected | PASS | TESTED |
| T36 Cross-build rejected | PASS | TESTED |
| T37 Cross-scope rejected | PASS | TESTED |
| T38 Cross-line Voice rejected | PASS | TESTED + LIVE_VERIFIED (SH02 L1 vs empty payload) |
| T39 TEST cannot resolve for EP01 | PASS | TESTED |
| T40 TEST cannot resolve for EP99 | PASS | TESTED |
| T41 Reload preserves bindings | PASS | TESTED + LIVE_VERIFIED |
| T42 IDB absence does not break OS | PASS | TESTED |
| T43 IDB presence cannot override OS | PASS | TESTED |
| T44 Legacy video URL cannot override | PASS | TESTED |
| T45 Legacy voice URL cannot override | PASS | TESTED |
| T46 Provider calls = 0 | PASS | FACT + LIVE_VERIFIED (fixture job id) |
| T47 Media generation = 0 vendor | PASS | FACT (fixture bytes only) |
| T48 EP01 unchanged | PASS | LIVE_VERIFIED |
| T49 EP99 unchanged | PASS | LIVE_VERIFIED |
| T50 No new orchestrator | PASS | TESTED (source) |
| T51 No LipSyncCurrent machine | PASS | TESTED (source) |

---

## 24. LIVE API VERIFICATION

API rebuilt (`dotnet build` 0 errors) and restarted. `Now listening on: http://localhost:5290`. Login `KIT_MKT` / `admin` succeeded.

| Check | Result | Class |
|---|---|---|
| GET SH02 `/actions` VideoArtifactId | `583d7700-f5b9-44c9-97db-5277572b628b` | LIVE_VERIFIED |
| GET SH02 `/actions` VoiceArtifactId | `3a753477-fef0-4326-8423-a5d7c316be37` | LIVE_VERIFIED |
| GET SH02 `/state` same IDs + CURRENT nodes | yes | LIVE_VERIFIED |
| GET SH02 CREATE_LIPSYNC available | false / `PERFORMANCE_HASH_MISMATCH` (empty historical hashes) | LIVE_VERIFIED fail-closed |
| GET SH01 CREATE_LIPSYNC before voice | false / `MISSING_VOICE_ARTIFACT` | LIVE_VERIFIED |
| GET `/voice?lineId=L1` on SH02 | `PERFORMANCE_LINE_MISMATCH` | LIVE_VERIFIED |
| GET video-generation-execution/regression | PASS fail=0 generate=false runway=false | LIVE_VERIFIED |
| POST missing video | `MISSING_VIDEO_ARTIFACT` | LIVE_VERIFIED |
| POST missing voice | `MISSING_VOICE_ARTIFACT` | LIVE_VERIFIED |
| POST SH02 historical pair + L1 | `PERFORMANCE_LINE_MISMATCH` | LIVE_VERIFIED |
| POST CREATE_VOICE SH01 fixture | artifact `84ca5121-…` fixture-voice-v1 | LIVE_VERIFIED |
| POST CREATE_LIPSYNC SH01 fixture | Request/Task/Snapshot/Attempt/Artifact/Binding | LIVE_VERIFIED |
| GET SH01 `/state` after POST | VIDEO `5872d0a9-…` VOICE `84ca5121-…` LIPSYNC `8eaad200-…` | LIVE_VERIFIED |
| Reload | same bindings | LIVE_VERIFIED |
| DB snapshot parents | exact A+A | LIVE_VERIFIED |
| SH02 CURRENT trio | unchanged historical IDs | LIVE_VERIFIED |

Never claim LIVE_VERIFIED for a Fal run. There was none.

---

## 25. EP01 SAFETY

| Field | Value |
|---|---|
| series_build id | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| title | Tập 01 |
| updated_at | 2026-09-18 23:01:02.129634+07 |
| graph_json md5 | `415fedf306ed23d1ace94a794400b440` |
| production_tasks | 0 |
| active bindings | 0 |

Unchanged this phase. LIVE_VERIFIED.

---

## 26. EP99 SAFETY

| Field | Value |
|---|---|
| series_build id | `77262ec0-74ad-459d-9128-f09bb553a5dd` |
| title | TEST BATCH 3 SHORT |
| updated_at | 2026-09-07 16:52:54.467199+07 |
| graph_json md5 | `c7cd2516627332c2648c1509395e4a64` |
| production_tasks | 0 |
| active bindings | 0 |

Unchanged this phase. LIVE_VERIFIED.

---

## 27. REMAINING GAPS

1. Official-film Director still uses `resolveLineAudio` → `kit-famixa-tts` for display/assemble. LEGACY plane. Not deleted. OS CREATE_LIPSYNC does not use it. RECOMMENDED only if a later phase migrates official-film **display**.
2. Historical TEST-SC01-SH02 VOICE `3a753477-…` still has empty `performance_payload_json` / empty PerformanceHash. Line-verified GET `/voice` and CREATE_LIPSYNC fail-close. Not repaired. FACT. New SH01 fixture voice proves the good path.
3. UI OS execute still sends `LINE-${shot.id}` while frozen TEST performance uses `L1`. Live proof used `L1` to match the snapshot. RECOMMENDED: align OS UI lineId with frozen Performance line — separate phase.
4. Mix still conceptually downstream; this phase only ensures LipSync ProductionArtifactId exists. Mix implementation not changed. FACT.
5. In-browser IDB presence/absence not live-clicked. TESTED in source. UNVERIFIED in browser.
6. Live late-result / invalidation after the SH01 bind were not re-executed (would destroy the proof bind). TESTED in-process.

---

## 28. NEXT SINGLE PHASE

**MIX input cutover to exact LIPSYNC ProductionArtifactId** (still fixture / 0 Fal / 0 FFmpeg vendor), using CURRENT:LIPSYNC the same way this phase used CURRENT:VIDEO_TAKE + CURRENT:VOICE.

Do not start: real Fal, LipSync quality, Assembly, Final, Official Film Preview, vendor TTS, provider benchmarking.

---

## 29. STOP

This phase is closed.

Production OS CREATE_LIPSYNC now receives the exact intended VIDEO artifact and exact intended VOICE artifact. Inputs are frozen. IndexedDB and legacy URLs are not execution authority.

No commit. No deploy. No Fal. No official film writes.
