# FAMIXA PRODUCTION OS — TEST CREATE_VIDEO E2E CHECKPOINT 3B

Date: 2026-09-18  
Mode: first unpaid end-to-end CREATE_VIDEO through the real Production OS

Official EP01: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Official EP99: `77262ec0-74ad-459d-9128-f09bb553a5dd`

Existing TEST sandbox (not recreated):

- `test_run_id` = `aaaaaaaa-0000-4000-8000-000000000371`
- `scope_id` = `bbbbbbbb-0000-4000-8000-000000000371`
- `shot_code` = `TEST-SC01-SH01`
- KEYFRAME `artifactId` = `33333333-0000-4000-8000-000000000371` (`CURRENT:KEYFRAME` unchanged)

Adapter: existing `FixtureMotionAdapter` (`IsPaid = false`, `ProviderId = fixture`, `ModelId = fixture-motion-v1`). It now emits a deterministic ISO-BMFF MP4 via `ProductionTestMp4.Encode`. No paid provider was called.

---

## 1. Verdict

**PASS**

One complete CREATE_VIDEO execution ran through the real Production OS on the isolated TEST scope:

USER ACTION → REQUEST → TASK → INPUT SNAPSHOT → PROVIDER/MODEL FREEZE → ATTEMPT → FixtureMotionAdapter → physical MP4 → verification → `VIDEO_TAKE` VERIFIED → `CURRENT:VIDEO_TAKE` + `VISIBLE:VIDEO_TAKE` → preview via VISIBLE → artifactId → bytes.

- Action Contract approved CREATE_VIDEO before POST (same evaluator as execution)
- Existing 3A KEYFRAME reused; no image generation
- Storage: `os-test/{test_run_id}/video/`
- Duplicate idempotent replay did not create a second request, task, attempt, or adapter call
- FILM isolation held
- Paid provider calls = 0
- Fixture video generation = 1

---

## 2. Test scope

| Field | Value |
|---|---|
| test_run_id | `aaaaaaaa-0000-4000-8000-000000000371` |
| scope_id | `bbbbbbbb-0000-4000-8000-000000000371` |
| kind | `TEST` |
| series_build_id | `NULL` |
| shot_code | `TEST-SC01-SH01` |
| second shot (identity only) | `TEST-SC01-SH02` — no video generated |

FILM claims of the TEST KEYFRAME fail `CROSS_SCOPE_FORBIDDEN` (live GET `/actions` on EP01-SC01-SH02 with TEST keyframe id → HTTP 400 `{"message":"CROSS_SCOPE_FORBIDDEN"}`).

---

## 3. Input context

TEST-only typed facts from `ProductionTestCreateVideoContext`. Not copied from EP01/EP99, `graph_json`, `active_build_id`, localStorage, IndexedDB, session, `kfApproved`, `takeUrl`, `previewUrl`, or `acceptedTake`.

| Input | Value | Hash |
|---|---|---|
| Story | `TEST STORY — child walks into a room.` | `de423b2eb8c17b69d369c22a53d95b026fdb890a6b5dbf5743e22cef86204f4e` |
| ActingBeat | `TEST ACTING — walks forward and looks around.` | `d277c174131792e42afc5925257addbc905253c69b0d8c6e59adcab0cd133f19` |
| VisualContract | `TEST VISUAL — 16:9 interior, locked camera, no film graph.` | `9e977078ec355074fd55c7115d1a561fc9e9bbbd72c6f92e81df8c47c2796523` |
| Timing | `5s` / duration 5 | `93e3d8c5b10657d2884f177488b689aadf82a83f962237cb602b3314386ab3b7` |
| ApprovedKeyframe | `33333333-0000-4000-8000-000000000371` | fixture SHA `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` |

These values were frozen on the ProductionTaskInputSnapshot (see §7).

---

## 4. Action Contract before execution

Live `GET /api/content/production/shots/{scope}/TEST-SC01-SH01/actions` with the TEST claim:

| Required input | Result |
|---|---|
| ApprovedKeyframe | satisfied (3A KEYFRAME) |
| Story | satisfied |
| ActingBeat | satisfied |
| VisualContract | satisfied |
| Timing | satisfied |
| FrozenProviderModel | satisfied (`fixture` / `fixture-motion-v1`) |

`CREATE_VIDEO available=True`, `blockReason` empty, `missingInputs` empty.

Same `CreateVideoActionContractGate` / `EvaluateCreateVideoAsync` is used for GET readiness and POST execution. POST with TEST `KeyframeDataUrl` is refused (`URL_AUTHORITY_FORBIDDEN`).

---

## 5. Request

Exactly one `ProductionRequest`:

| Field | Value |
|---|---|
| requestId | `4aec586c-4286-42ee-9649-093e3670a173` |
| action | `CREATE_VIDEO` |
| status | `COMPLETED` |
| scope | TEST `bbbbbbbb-0000-4000-8000-000000000371` |
| shot | `TEST-SC01-SH01` |
| idempotencyKey | `TEST-3B-CREATE_VIDEO-aaaaaaaa-0000-4000-8000-000000000371` |

Durable. Replay of the same key returned this request.

---

## 6. Task

Exactly one `ProductionTask`:

| Field | Value |
|---|---|
| taskId | `9d41a07e-85a5-45bd-b8da-7ad2a305a38f` |
| action | `CREATE_VIDEO` |
| status | `SUCCEEDED` |
| providerId | `fixture` |
| modelId | `fixture-motion-v1` |
| inputSnapshotId | `c671a886-cdf1-4e66-b13a-f1e1670293da` |

---

## 7. Input Snapshot

Immutable row `production_task_input_snapshots`:

| Field | Value |
|---|---|
| snapshotId | `c671a886-cdf1-4e66-b13a-f1e1670293da` |
| snapshotHash | `444baf03cc76c25e166a2367a0af0e475907127cf74a5313268c104133bcc699` |
| story / acting / visual / timing hashes | see §3 |
| keyframeArtifactId | `33333333-0000-4000-8000-000000000371` |
| durationSec | 5 |
| providerId / modelId | `fixture` / `fixture-motion-v1` |

---

## 8. Provider/Model freeze

Frozen on the task and snapshot before the adapter ran:

- `ProviderId` = `fixture`
- `ModelId` = `fixture-motion-v1`
- `IsPaid` = `false`

Not Runway, Veo, Gemini, Fal, or ElevenLabs. Changing provider/model after freeze is a new task or `PROVIDER_CHANGED` / `MODEL_CHANGED` per the existing state machine.

---

## 9. Attempt

Exactly one attempt:

| Field | Value |
|---|---|
| attemptId | `0f3176f1-6e07-4ec8-9ec8-e46abb6aa96d` |
| attempt number | 1 |
| status | `PROVIDER_SUCCEEDED` |
| providerRequestId | `fixture-motion-9d41a07e85a5` (fixture identity, not a vendor job) |

Events on the task: `ATTEMPT_STARTED` → `PROVIDER_SUBMITTED` → `PROVIDER_SUCCEEDED` → `ARTIFACT_VERIFIED`.

CREATE_VIDEO attempts on this TEST scope after replay: **1**.

---

## 10. Fixture output

`FixtureMotionAdapter.ExecuteFrozenAsync` produced a real physical file:

| Field | Value |
|---|---|
| path | `src/KitPlatform.Api/App_Data/kit-video-i2v/os-test/aaaaaaaa-0000-4000-8000-000000000371/video/0f3176f16e074ec89ec8e46abb6aa96d.mp4` |
| storageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/0f3176f16e074ec89ec8e46abb6aa96d` |
| bytes | 707 |
| MIME | `video/mp4` |
| boxes | `ftyp` + `moov` + `mdat` + `vide` handler |
| SHA256 | `a998aa13ba1577f65c175ff91cffa64dc52ab0ebb09d9848096b447af5c35a3b` |

Not written to `os/` or `os-kf/`.

---

## 11. Artifact verification

| Check | Result |
|---|---|
| physical file exists | yes |
| MIME | `video/mp4` |
| duration | 5 |
| byte length | 707 |
| SHA256 | matches file bytes |
| decodable (ftyp/moov/mdat/vide) | yes |
| scope / shot | TEST / `TEST-SC01-SH01` |
| attempt lineage | `0f3176f1-6e07-4ec8-9ec8-e46abb6aa96d` |
| provider/model lineage | `fixture` / `fixture-motion-v1` |
| parent | KEYFRAME `33333333-0000-4000-8000-000000000371` |

| Field | Value |
|---|---|
| artifactId | `fec8ae2f-0dac-4b21-a9c0-9757eca25187` |
| role | `VIDEO_TAKE` |
| status | `VERIFIED` |
| inputSnapshotHash | `444baf03cc76c25e166a2367a0af0e475907127cf74a5313268c104133bcc699` |

Provider SUCCESS without this verified artifact cannot become CURRENT/VISIBLE.

---

## 12. CURRENT binding

| Binding | artifactId | active |
|---|---|---|
| `CURRENT:VIDEO_TAKE` | `fec8ae2f-0dac-4b21-a9c0-9757eca25187` | yes |
| `CURRENT` (alias) | same | yes |
| `CURRENT:KEYFRAME` | `33333333-0000-4000-8000-000000000371` | yes (3A, unchanged) |

Authority is `artifactId`. No URL / `takeUrl` / `previewUrl` / provider job id.

---

## 13. VISIBLE binding

| Binding | artifactId | active |
|---|---|---|
| `VISIBLE:VIDEO_TAKE` | `fec8ae2f-0dac-4b21-a9c0-9757eca25187` | yes |
| `VISIBLE` (alias) | same | yes |

CURRENT artifact == VISIBLE artifact == newly verified `VIDEO_TAKE`.

---

## 14. Preview

Resolved only through VISIBLE → artifactId → physical storage.

| Field | Value |
|---|---|
| visible artifactId | `fec8ae2f-0dac-4b21-a9c0-9757eca25187` |
| preview artifactId | `fec8ae2f-0dac-4b21-a9c0-9757eca25187` |
| preview SHA256 | `a998aa13ba1577f65c175ff91cffa64dc52ab0ebb09d9848096b447af5c35a3b` |
| GET `/preview/media` | HTTP 200, 707 bytes, SHA identical |

The preview resolver does not know about Runway / Veo / Fal.

---

## 15. Reload verification

After a new login (no localStorage / IndexedDB / session authority):

Live `GET /shots/.../state` and `GET /preview`:

- `CURRENT` = `fec8ae2f-0dac-4b21-a9c0-9757eca25187`
- `VISIBLE` = same
- preview = same artifact / SHA

No graph URL required.

---

## 16. Duplicate request

Exact replay of idempotency key `TEST-3B-CREATE_VIDEO-aaaaaaaa-0000-4000-8000-000000000371`:

| Field | First | Replay |
|---|---|---|
| requestId | `4aec586c-…a173` | same |
| taskId | `9d41a07e-…a38f` | same |
| artifactId | `fec8ae2f-…5187` | same |

DB: 1 CREATE_VIDEO request, 1 CREATE_VIDEO attempt on the TEST scope. Source `CountingMotionAdapter.Calls == 1`. FixtureMotionAdapter was not invoked a second time.

---

## 17. Input mutation

**SOURCE VERIFIED.** After the bound video, `InvalidateDownstreamAsync(Story)`:

- `VISIBLE` / `CURRENT:VIDEO_TAKE` / `VISIBLE:VIDEO_TAKE` unbound
- old CREATE_VIDEO task → `SUPERSEDED`
- old `VIDEO_TAKE` artifact remains VERIFIED with the same SHA
- a new CREATE_VIDEO task is required

**Not executed live** (no invalidate API; mission forbids executing the new task). FILM story was not changed.

---

## 18. Late result

**SOURCE VERIFIED** via `ProductionWorkExecutionEngine.ApplyLateResultAsync` on a later simulated attempt (no second adapter call). Older attempt status = `LATE`. CURRENT / VISIBLE stay on the first verified artifact.

**LIVE UNVERIFIED** — no late-result HTTP endpoint. Not a paid/provider call.

---

## 19. Cross-shot isolation

`TEST-SC01-SH02` inserted as identity only (no second video).

| Shot | VISIBLE / preview |
|---|---|
| TEST-SC01-SH01 | artifact `fec8ae2f-…5187` |
| TEST-SC01-SH02 | empty / `VISIBLE_MISSING` |

SH01 artifact cannot appear as SH02 VISIBLE. No global latest-artifact lookup.

---

## 20. FILM isolation

| Check | Before | After |
|---|---|---|
| `series_build` count | 4 | 4 |
| FAMIXA `active_build_id` | official EP01 `4ef164d6-…2c7f` | same |
| EP01 `graph_json` MD5 | `12289be18284fbd4d4b166b82c09eba0` | same |
| EP99 `graph_json` MD5 | `c7cd2516627332c2648c1509395e4a64` | same |
| FILM anchors | 4 | 4 |
| TEST anchors | 1 | 1 |
| FILM artifacts | 0 | 0 |
| FILM bindings | 0 | 0 |
| Canon (`famixa_character`) | 7 | 7 |
| `GET /series/builds?code=FAMIXA` | 4 films | same 4; TEST id absent |

No Canon writes. No Visual Authority writes. No Film Studio mutation.

---

## 21. Regression tests

`dotnet test tests/KitPlatform.Content.Tests --filter ProductionPipelineV1Tests` → **8/8 pass** (includes 3B).

| Test | Result |
|---|---|
| T01 CREATE_VIDEO becomes available | pass |
| T02 Request created | pass |
| T03 Task created | pass |
| T04 immutable InputSnapshot created | pass |
| T05 Provider/model frozen | pass |
| T06 Attempt created (STARTED→SUBMITTED→SUCCEEDED) | pass |
| T07 FixtureMotionAdapter called exactly once | pass |
| T08 physical MP4 exists | pass |
| T09 MP4 decodable | pass |
| T10 MIME verified | pass |
| T11 duration verified | pass |
| T12 SHA256 verified | pass |
| T13 VIDEO_TAKE Artifact created | pass |
| T14 Artifact VERIFIED | pass |
| T15 CURRENT:VIDEO_TAKE bound | pass |
| T16 VISIBLE:VIDEO_TAKE bound | pass |
| T17 CURRENT artifact == VISIBLE artifact | pass |
| T18 preview resolves VISIBLE artifact | pass |
| T19 preview SHA == artifact SHA | pass |
| T20 reload preserves binding | pass |
| T21 duplicate request does not execute twice | pass |
| T22 story mutation invalidates downstream binding | pass (source) |
| T23 old artifact remains immutable | pass (source) |
| T24 cross-scope artifact rejected | pass (source + live) |
| T25 cross-shot artifact rejected | pass (source + live) |
| T26 late result cannot steal VISIBLE | pass (source) / live unverified |
| T27 FILM unchanged | pass |
| T28 existing Production OS suites pass | pass |
| T29 paid providers = 0 | pass |

T28 ran work / pipeline / cutover / contract / checkpoint 1 / checkpoint 2 / checkpoint 3A.

---

## 22. Provider calls

| Call | Count |
|---|---|
| Gemini | 0 |
| Runway | 0 |
| Veo | 0 |
| Fal | 0 |
| ElevenLabs | 0 |
| Paid provider calls | 0 |
| Image generation | 0 |
| Real video generation | 0 |
| Fixture video generation | **1** |

---

## 23. DB writes

TEST-only. Zero FILM mutations.

| Item | Before | After |
|---|---|---|
| TEST CREATE_VIDEO requests | 0 | 1 |
| TEST CREATE_VIDEO tasks / snapshots / attempts | 0 | 1 / 1 / 1 |
| TEST VIDEO_TAKE artifacts | 0 | 1 |
| TEST active video bindings | 0 | `CURRENT:VIDEO_TAKE`, `CURRENT`, `VISIBLE:VIDEO_TAKE`, `VISIBLE` |
| TEST KEYFRAME artifact / `CURRENT:KEYFRAME` | 1 / active | unchanged |
| TEST files under `os-test/{run}/video/` | 0 | 1 |
| TEST shots | SH01 | SH01 + SH02 identity |
| FILM artifacts / bindings | 0 / 0 | 0 / 0 |

---

## 24. Runtime status

| Gate | Status |
|---|---|
| SOURCE VERIFIED | T01–T29 in-memory on the existing Production OS |
| LIVE API VERIFIED | `:5290` rebuilt/restarted; GET actions + one POST `/requests` + GET request/task/preview/media + idempotent replay |
| LIVE DB VERIFIED | request / task / snapshot / attempt / artifact / bindings / events |
| LIVE STORAGE VERIFIED | physical MP4 under `os-test/{test_run_id}/video/`; SHA matches artifact and preview media |

Source tests were not treated as live API proof.

---

## 25. Files changed

Created:

- `ProductionTestMp4.cs`
- `ProductionTestCreateVideoContext.cs`
- `ProductionTestCreateVideoV1Regression.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_TEST_CREATE_VIDEO_E2E_CHECKPOINT_3B_REPORT.md`

Modified:

- `Providers/IMotionExecutionAdapter.cs` — FixtureMotionAdapter emits `ProductionTestMp4` (still unpaid, still `fixture` / `fixture-motion-v1`)
- `ProductionWorkIds.cs` — `VisibleFor`
- `Dependencies/ShotDependencyGraph.cs` — invalidate `VISIBLE:{role}`
- `ProductionExecutionService.cs` — `BindVisible` writes `VISIBLE:{role}` + `VISIBLE`
- `ProductionCreateVideoService.cs` — TEST refuses `KeyframeBytes` / dataUrl
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs`
- `VideoGenerationExecutionService.cs` — register 3B (and prior Production OS suites)

No UI, Director, Canon, Visual Authority, EP01, EP99, Runway, Veo, Gemini, Fal, or ElevenLabs adapter changes.

---

## 26. Remaining blocker for real provider validation

Unpaid CREATE_VIDEO is proven on the Production OS.

A real provider run would reuse this same request → task → frozen snapshot → attempt → verify → bind → preview path, with a paid adapter behind the existing freeze/idempotency/late-result rules.

Not done here: LipSync, Voice, Mix, Assembly, UI, Director, or paid benchmarking.

**STOP.**
