# FAMIXA PRODUCTION OS — CREATE_VIDEO ACTION CONTRACT ENFORCEMENT V1

Date: 2026-09-17  
Mode: contract-hardening only  
Official film left untouched: EP01 6-shot `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`

No test build. No test shot. No fixture image. No Gemini / Veo / Runway / Wan / provider call.  
No `POST /keyframes`. No live `POST /requests`. No ProductionRequest/Task/Artifact on the official build.  
No UI CTA change. No Canon / Visual Authority / Shot Contract / Timing semantics / provider adapter change.  
No new series-graph resolver. No Test Harness.

---

## Final verdict

**PASS**

```
Action Contract RequiredInputs
  = GET /actions readiness
  = POST /requests execution gate
  = Input Snapshot freeze requirements
```

Single evaluator: `CreateVideoActionContractGate.Evaluate`.  
GET and POST both call `ProductionExecutionService.EvaluateCreateVideoAsync`.  
Freeze writes Story / ActingBeat / VisualContract / Timing / Keyframe identity from that verdict, plus server adapter `ProviderId` / `ModelId`.

---

## Current contract (unchanged)

| Field | Value |
|---|---|
| Action | `CREATE_VIDEO` |
| RequiredInputs | `ApprovedKeyframe`, `Story`, `ActingBeat`, `VisualContract`, `Timing`, `FrozenProviderModel` |
| Output | `VIDEO_TAKE` |
| Forbidden fallbacks | `CREATE_IMAGE`, `CREATE_VOICE`, `CREATE_LIPSYNC` |
| CTA | `Tạo video` |

No dependency was added or removed.

---

## Current mismatch (before this phase)

| RequiredInput | GET `/actions` | POST `/requests` | Snapshot freeze | Status |
|---|---|---|---|---|
| ApprovedKeyframe | `Has(CURRENT:KEYFRAME)` only | Verified KEYFRAME + same-scope owner | Client/OS mix | Partial |
| Story | Query default `storyPresent=true` | Non-empty `input.Story` only | Client story | **Mismatch** — silent assume true |
| ActingBeat | Query default `actingPresent=true` | Not gated | Client acting | **Mismatch** — silent assume true |
| VisualContract | Not gated | Not gated | Passed through | **Mismatch** |
| Timing | Not gated | Not gated | Timing hash only | **Mismatch** |
| FrozenProviderModel | Not gated | Adapter used at freeze, replay could silent-reuse | Adapter ids | **Mismatch** — replay ignored provider change |

Generic `MISSING_DEPENDENCY` hid which input failed.  
`ProductionWorkExecutionEngine.ListAvailableActionsAsync(keyframeApproved, storyPresent)` treated legacy flags as readiness.

---

## Exact code paths

```
ProductionActionContractRegistry.CreateVideo.RequiredInputs
        │
        ▼
CreateVideoActionContractGate.Evaluate(scope, osFacts, claim, providerId, modelId)
        │
        ├─ GET  /api/content/production/shots/{buildId}/{shotCode}/actions
        │     ContentProductionController.GetActions
        │       → no storyPresent/actingPresent defaults
        │       → optional explicit query: story, actingBeat, visualContract, timing, keyframeArtifactId
        │       → IProductionCreateVideoService.GetShotStateAsync(scope, claim)
        │       → ProductionExecutionService.EvaluateCreateVideoAsync
        │       → BuildActions(CREATE_VIDEO) uses verdict.Available / BlockReason / MissingInputs
        │
        ├─ POST /api/content/production/requests  (CREATE_VIDEO)
        │     ContentProductionController.CreateVideo
        │       → ShotExecutionInput from body (not trusted as authority)
        │       → ProductionCreateVideoService.CreateVideoAsync
        │       → EvaluateCreateVideoAsync (full revalidation)
        │       → freeze from verdict + _motion.ProviderId/ModelId
        │       → ReplayCreateVideoAsync rechecks provider/model + live vs frozen fields
        │
        └─ FreezeInputAsync
              ProductionExecutionService.RequireCreateVideoDependenciesAsync
                → same EvaluateCreateVideoAsync
              ProductionWorkExecutionEngine.FreezeInputAsync
                → snapshot Story/Acting/Visual/TimingHash/Keyframe + ProviderId/ModelId
```

OS facts loaded for evaluation (not a series-graph resolver):

1. `CURRENT:KEYFRAME` verified artifact, owner task same `BuildId` + `ShotCode`
2. Latest non-superseded `CREATE_VIDEO` then `CREATE_IMAGE` snapshot for Story / ActingBeat / VisualContract / DurationSec
3. Server `IMotionExecutionAdapter.ProviderId` + `ModelId`

Ignored (never satisfy a RequiredInput):

`localStorage`, IndexedDB, session, `kfApproved`, `takeUrl`, `previewUrl`, `acceptedTake`, `turboTaskId`, legacy graph flags, client `available` / `requiredInputs` / provider-model / untrusted artifact id.

If the client omits a text input, the server uses the OS snapshot value or returns unavailable. It does not assume `true`.

---

## Dependency verification matrix

| RequiredInput | GET `/actions` verification | POST `/requests` verification | Snapshot source | Status |
|---|---|---|---|---|
| ApprovedKeyframe | Verified KEYFRAME on `CURRENT:KEYFRAME`, same shot scope. Client `keyframeArtifactId` if sent must match OS id. | Same gate. Bytes-import path registers KF first, then revalidates. | `verdict.KeyframeArtifactId` / `ByteSha256` | Aligned |
| Story | Non-empty claim, else non-empty OS snapshot Story | Same. Empty + empty OS → `MISSING_STORY` | Frozen `snap.Story` from verdict | Aligned |
| ActingBeat | Non-empty claim, else OS snapshot ActingBeat | Same → `MISSING_ACTING_BEAT` | Frozen `snap.ActingBeat` | Aligned |
| VisualContract | Non-empty claim, else OS snapshot VisualContract | Same → `MISSING_VISUAL_CONTRACT` | Frozen `snap.VisualContract` | Aligned |
| Timing | Valid timing string or `DurationSec > 0` (not `"0"` / 0s) | Same → `MISSING_TIMING` | Frozen `TimingHash` + `DurationSec` from verdict | Aligned |
| FrozenProviderModel | Server adapter ids non-empty. Client cannot supply. | Same. Freeze always `_motion.ProviderId/ModelId`. Replay fails on change. | Frozen `snap.ProviderId` / `ModelId` | Aligned |

GET `blockReason` is the first missing input in contract order. Multiple misses also return `missingInputs` as the contract names.

Reason codes:

- `MISSING_KEYFRAME`
- `MISSING_STORY`
- `MISSING_ACTING_BEAT`
- `MISSING_VISUAL_CONTRACT`
- `MISSING_TIMING`
- `MISSING_PROVIDER_MODEL`
- `INPUT_MUTATED` — client artifact id ≠ OS `CURRENT:KEYFRAME`

Generic `MISSING_DEPENDENCY` is no longer used for CREATE_VIDEO readiness.

---

## Changes

Created:

- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/CreateVideoActionContractGate.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoActionContractV1Regression.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_CONTRACT_ENFORCEMENT_V1_REPORT.md`

Modified:

- `ProductionWorkIds.cs` — specific CREATE_VIDEO missing reasons + `INPUT_MUTATED`
- `ProductionEntities.cs` — `ProductionActionAvailability.MissingInputs`
- `IProductionExecutionService.cs` / `ProductionExecutionService.cs` — shared evaluate; GET no longer takes boolean presence flags
- `ProductionCreateVideoService.cs` — POST revalidates all six; freeze from verdict; replay rejects provider/model or input mutation
- `ContentProductionController.cs` — removed `storyPresent=true` / `actingPresent=true`; optional explicit query strings only
- `ProductionWorkExecutionEngine.ListAvailableActionsAsync` — `kfApproved` / `storyPresent` are not authority
- Existing pipeline / cutover / work-execution regressions updated to the new signatures
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs` — T01–T11 fact

Not changed: UI CTA, Canon, VA, Shot WHAT, Timing V3 rules, provider adapters, official EP01 data, schema.

---

## Tests

In-memory only (`InMemoryProductionWorkStore`). Scope `EP99-SC01-SH99` / synthetic build UUID. Fixture adapter. Paid calls = 0.

| ID | Case | Result |
|---|---|---|
| T01 | Missing keyframe | GET + POST `MISSING_KEYFRAME`, `missingInputs` includes `ApprovedKeyframe` |
| T02 | Missing story | GET + POST `MISSING_STORY` |
| T03 | Missing acting beat | GET + POST `MISSING_ACTING_BEAT` |
| T04 | Missing visual contract | GET + POST `MISSING_VISUAL_CONTRACT` |
| T05 | Missing / zero timing | GET + POST `MISSING_TIMING` |
| T06 | Blank provider/model | GET + POST `MISSING_PROVIDER_MODEL` |
| T07 | All six valid | `available=true`; POST freezes `fixture` / `fixture-motion-v1`; output `VIDEO_TAKE` |
| T08 | GET available, POST mutated KF id | `INPUT_MUTATED`. GET available via acting claim, POST omits acting → `MISSING_ACTING_BEAT` |
| T09 | `kfApproved=true`, no OS KEYFRAME | unavailable `MISSING_KEYFRAME` |
| T10 | `takeUrl` / `previewUrl` / `acceptedTake` / `turboTaskId` | do not satisfy; do not flip a valid OS verdict |
| T11 | Provider/model change after catalog | same idempotency → `PROVIDER_CHANGED` / `MODEL_CHANGED`; new auto key → new Task with new freeze |

xUnit: `Create_video_action_contract_enforcement_T01_through_T11` **passed** (2026-09-17).  
Existing work-execution / pipeline / cutover suites **passed**.

---

## Safety

| Check | Result |
|---|---|
| DB writes (Postgres / official build) | 0 |
| Paid provider calls | 0 |
| UI CTA / workspace changes | 0 |
| Production data / official SH01–SH06 | unchanged |
| Action ID | still `CREATE_VIDEO` |

GET `/actions` is readiness, not execution authority. POST revalidates server-side. Client `available`, `requiredInputs`, provider/model, and foreign artifact ids are not trusted.
