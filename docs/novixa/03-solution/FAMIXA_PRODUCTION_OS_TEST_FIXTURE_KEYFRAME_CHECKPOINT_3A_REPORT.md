# FAMIXA PRODUCTION OS — TEST FIXTURE KEYFRAME CHECKPOINT 3A

Date: 2026-09-17  
Mode: TEST fixture KEYFRAME only. CREATE_VIDEO was not executed.

## 1. Verdict

**PASS**

A real, deterministic, verified TEST KEYFRAME exists as a `ProductionArtifact`, is bound `CURRENT:KEYFRAME`, and has matching physical bytes + SHA256. FILM is untouched. CREATE_VIDEO was not executed.

## 2. Fixture

| Field | Value |
|---|---|
| FixtureId | `TEST_KEYFRAME_V1` |
| Path | `testdata/famixa-production-os/TEST_ONLY_keyframe_v1.png` |
| SHA256 | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` |
| MIME | `image/png` |
| Dimensions | 160 × 90 |
| Bytes | 232 |

SHA256 was computed from the repository file bytes and written into `testdata/famixa-production-os/manifest.json`. Not hardcoded first.

## 3. TEST scope

| Field | Value |
|---|---|
| test_run_id | `aaaaaaaa-0000-4000-8000-000000000371` |
| scope_id | `bbbbbbbb-0000-4000-8000-000000000371` |
| shot_code | `TEST-SC01-SH01` |

`production_scope_anchor.kind = TEST`, `series_build_id = NULL`.

## 4. Artifact

Live TEST sandbox:

| Field | Value |
|---|---|
| artifactId | `33333333-0000-4000-8000-000000000371` |
| role | `KEYFRAME` |
| scopeId | `bbbbbbbb-0000-4000-8000-000000000371` |
| byteSha256 | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` |
| storageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/kf/22222222000040008000000000000371` |
| verificationStatus | `VERIFIED` |
| source | `TEST_FIXTURE` |
| fixtureId | `TEST_KEYFRAME_V1` (not the artifact id) |

In-memory registration (T11–T15) creates a distinct Production OS `artifactId` per run; identity rules are the same.

## 5. Binding

Live:

| Binding | artifactId |
|---|---|
| `CURRENT:KEYFRAME` | `33333333-0000-4000-8000-000000000371` |
| `CURRENT` (alias) | same |

Authority is `artifactId`. No URL / dataUrl / base64 / filename.

## 6. CREATE_VIDEO readiness

Before: `MISSING_KEYFRAME`

After (source evaluator, same TEST scope / `TEST-SC01-SH01`):

- `ApprovedKeyframe` satisfied (artifact `VERIFIED`, owner scope matches)
- **not** `MISSING_KEYFRAME`
- remaining block: **`MISSING_STORY`**

Story, ActingBeat, VisualContract, and Timing were left empty on purpose. FrozenProviderModel is supplied by the existing unpaid `FixtureMotionAdapter` identity (`fixture` / `fixture-motion-v1`) at evaluate time. No TEST-specific readiness flags.

CREATE_VIDEO was **not** executed.

## 7. Physical verification

Repository fixture:

- exists: `testdata/famixa-production-os/TEST_ONLY_keyframe_v1.png`
- 232 bytes, PNG signature, 160×90
- SHA256 `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` = manifest

TEST storage file:

- exists: `src/KitPlatform.Api/App_Data/kit-video-i2v/os-test/aaaaaaaa-0000-4000-8000-000000000371/kf/22222222000040008000000000000371.mp4`
- 232 bytes
- SHA256 matches fixture and `production_artifacts.byte_sha256`
- MIME `image/png` (PNG bytes; filename follows existing file-store `.mp4` convention)

Hash comparison: fixture = stored file = artifact row.

## 8. FILM isolation

| Check | Before | After |
|---|---|---|
| EP01 `graph_json` MD5 | `12289be18284fbd4d4b166b82c09eba0` | same |
| EP99 `graph_json` MD5 | `c7cd2516627332c2648c1509395e4a64` | same |
| `active_build_id` | official EP01 | same |
| `series_build` count | 4 | 4 |
| FILM artifacts | 0 | 0 |
| FILM bindings | 0 | 0 |
| FILM anchors | 4 | 4 |
| `GET /series/builds?code=FAMIXA` | 4 films | same 4; TEST id absent |
| Canon (`famixa_character`) | 7 | 7 |
| Visual Authority / UI | no writes | no writes |

## 9. Regression

`dotnet test tests/KitPlatform.Content.Tests --filter ProductionPipelineV1Tests` → **7/7 pass**

| Test | Result |
|---|---|
| T01 fixture manifest exists | pass |
| T02 fixture SHA256 == manifest | pass |
| T03 MIME image/png | pass |
| T04 dimensions 160×90 | pass |
| T05 TEST scope required | pass |
| T06 FILM scope rejected | pass |
| T07 unknown scope rejected | pass |
| T08 testRunId mismatch rejected | pass |
| T09 TEST shot required | pass |
| T10 bytes stored under `os-test/{test_run_id}/kf/` | pass |
| T11 ProductionArtifact created | pass |
| T12 verification = VERIFIED | pass |
| T13 artifact hash = physical bytes | pass |
| T14 storageKey TEST namespace | pass |
| T15 CURRENT:KEYFRAME → artifactId | pass |
| T16 duplicate registration idempotent | pass |
| T17 TEST cannot cross into FILM | pass |
| T18 FILM cannot cross into TEST | pass |
| T19 CREATE_VIDEO no longer MISSING_KEYFRAME | pass |
| T20 existing Production OS suites still pass | pass |
| T21 EP01/EP99 unchanged | pass |
| T22 active_build identity unchanged | pass |
| T23 series_build count / TEST not a film | pass |
| T24 provider calls = 0 | pass |

## 10. Provider calls

| Call | Count |
|---|---|
| Gemini | 0 |
| Runway | 0 |
| Veo | 0 |
| Fal | 0 |
| ElevenLabs | 0 |
| Image generation | 0 |
| Video generation | 0 |
| CREATE_VIDEO execution | 0 |

Registration uses `import` / `existing-pixels` only. `FixtureMotionAdapter.ExecuteFrozenAsync` was not called. No paid adapter was initialized for generation.

## 11. DB writes

**TEST-only writes; zero FILM mutations.**

Before → after TEST sandbox:

| Item | Before | After |
|---|---|---|
| TEST anchors | 1 | 1 |
| TEST artifacts | 0 | 1 |
| TEST bindings (active) | 0 | 2 (`CURRENT:KEYFRAME`, `CURRENT`) |
| TEST files | 0 | 1 under `os-test/{test_run_id}/kf/` |
| FILM artifacts | 0 | 0 |
| FILM bindings | 0 | 0 |

Also wrote: TEST request / task / empty-input snapshot / attempt / `TEST_FIXTURE_REGISTERED` event.

## 12. Runtime status

- **SOURCE VERIFIED** — T01–T24, including `EvaluateCreateVideoAsync` → not `MISSING_KEYFRAME`, remaining `MISSING_STORY`.
- **LIVE DB / STORAGE VERIFIED** — artifact, bindings, and physical TEST file on disk.
- **LIVE API NOT VERIFIED** — `:5290` is still a pre-3A binary (file-locked). Do not treat in-memory evaluate as a live `GET /actions` result. Rebuild/restart API is an explicit remaining runtime step, not part of 3A success.

## 13. Files changed

Created:

- `testdata/famixa-production-os/TEST_ONLY_keyframe_v1.png`
- `testdata/famixa-production-os/manifest.json`
- `ProductionTestKeyframePng.cs`
- `ProductionTestKeyframeCatalog.cs`
- `ProductionTestKeyframeService.cs` (`RegisterAsync`)
- `ProductionTestKeyframeV1Regression.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_TEST_FIXTURE_KEYFRAME_CHECKPOINT_3A_REPORT.md`

Modified:

- `ProductionWorkIds.cs` (fixture failure codes + `TEST_FIXTURE_REGISTERED`)
- `KitVideoVideoStore.cs` (nested `os-test/{id}/kf` segments; same store)
- `ContentPackDependencyInjection.cs`
- `ContentProductionController.cs` (`POST /api/content/production/test-runs/{id}/keyframes`)
- `ProductionApiDtos.cs`
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs`

No UI, Director CTA, FixtureMotionAdapter, provider adapters, Canon, VA, `graph_json`, or `series_build` changes.

## 14. Remaining blocker for Checkpoint 3B

`EvaluateCreateVideoAsync` on `TEST-SC01-SH01` is no longer `MISSING_KEYFRAME`.

The next required input on the **same** `CreateVideoActionContractGate` is **`MISSING_STORY`**.

Checkpoint 3B must supply TEST-scoped Story (then ActingBeat, VisualContract, Timing) without inventing TEST readiness flags, without executing CREATE_VIDEO until those inputs are present, and without FILM / provider / UI work.

**STOP.**
