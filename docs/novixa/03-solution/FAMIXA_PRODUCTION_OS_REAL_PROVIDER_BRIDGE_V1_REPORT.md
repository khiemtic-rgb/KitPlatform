# FAMIXA PRODUCTION OS — REAL PROVIDER BRIDGE V1 REPORT

Date: 2026-09-18  
Mode: FORENSIC → IMPLEMENT + TEST  
Scope: TEST only  
Provider: runway / gen4_turbo (explicit)  
Default TEST provider: fixture / fixture-motion-v1  

**REAL PROVIDER CALLS = 0**  
**REAL MEDIA GENERATION = 0**  
EP01 / EP99: untouched  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

Do not read this as Real Runway Test Execution.  
Do not read this as media generation.  
Do not read this as a Veo bridge.  
Do not start the next phase from this report.

---

## Verdict

**PASS**

A–P acceptance criteria hold in TEST isolation. Production OS can select `runway` / `gen4_turbo` through the same `IMotionExecutionAdapter` boundary as fixture, persist `providerJobId` before poll, recover without browser state, fail-closed on cost/registry/official film, and bind only after physical verification.

No live Runway HTTP. No live DB apply. **LIVE VERIFIED** is not claimed.

---

## 1. Initial forensic findings

Traced paths **FACT**:

```
ProductionCreateVideoService.CreateVideoAsync
  → ProductionMotionSelection.Normalize (explicit pair, or fixture only if BOTH missing)
  → ProductionMotionAdapterResolver.Resolve(ProviderId, ModelId)  fail-closed
  → ProductionMotionCostGuard (before Start)
  → ProductionExecutionService / ProductionWorkExecutionEngine
      StartAttempt (STARTED)
      adapter.StartFrozenAsync
      SubmitProviderJobAsync (persist providerJobId, PROVIDER_SUBMITTED)
      PollFrozenAsync / RecoverFrozenAsync
      FetchOutputAsync → physical bytes
      VerifyArtifactAsync
      BindCurrent / BindVisible
      GetPreview = VISIBLE → ProductionArtifact → storageKey
```

Legacy vendor paths (not Production OS orchestration) **FACT**:

| Question | Owner |
|---|---|
| 1. Who creates a Runway job? | `ContentRunwayClient.CreateImageToVideoAsync` via `HttpRunwayTransport` / `RunwayMotionProvider` / `RunwayVideoGenerationProvider`. OS only calls `IMotionExecutionAdapter.StartFrozenAsync`. |
| 2. Who polls? | `ContentRunwayClient.GetTaskAsync` behind `IRunwayTransport.GetTaskAsync`. OS calls `PollFrozenAsync`. |
| 3. Who recovers? | Same GetTask path via `RecoverFrozenAsync`. No in-memory Runway provider state required. |
| 4. Who downloads output? | `ContentRunwayClient.DownloadOutputAsync` (HTTP URL) or Fake/DryRun bytes. OS never previews the URL. |
| 5. Who maps duration? | `RunwayMotionExecutionAdapter.MapDuration` (`>=8 → 10` else `5`). Also `HttpRunwayTransport` and `FamixaProviderCostCatalog` for cost. Not Timing V3. |
| 6. Who packages keyframe bytes? | Adapter + transport (`data:` URI in HTTP transport). OS passes approved keyframe artifact bytes. |
| 7. Who handles last frame? | `ContentRunwayClient` `lastImage` argument. Not wired in this phase. |
| 8. Who knows vendor task IDs? | Transport/client. OS stores opaque `providerJobId` on `ProductionAttempt`. |
| 9. Who knows vendor model IDs? | Adapter maps OS `gen4_turbo` to vendor `TurboModel`. Catalog is OS HOW, not Shot Contract. |
| 10. Who writes legacy media? | `IVideoGenerationProvider` / `RunwayVideoGenerationProvider` / series turbo. Not this path. |
| 11. Who writes ProductionArtifact? | `ProductionWorkExecutionEngine.VerifyArtifactAsync` only. |
| 12. Who owns cost? | `FamixaProviderCostCatalog` + `ProductionMotionCostGuard`. Client MaxCost is not authority. |
| 13. Who owns authentication? | `ContentRunwayClient.ApplyAuth` (Bearer + `X-Runway-Version`). Tests never invoke it. |

Vendor request JSON, endpoints, and polling format stay behind `IRunwayTransport`. **FACT**

---

## 2. Files changed

| File | Role |
|---|---|
| `Providers/FakeRunwayTransport.cs` | Deterministic scenarios A–J. `HttpCallCount = 0`. |
| `Providers/RunwayMotionExecutionAdapter.cs` | Preserve `OUTPUT_DOWNLOAD_FAILED`. |
| `ProductionTestMp4.cs` | No-vide-stream + unreadable-duration fixtures. |
| `ProductionWorkIds.cs` | `OUTPUT_DOWNLOAD_FAILED`. |
| `ProductionExecutionService.cs` | GET: official-film + paid blocked; cost guard on paid HOW. |
| `ProductionCreateVideoService.cs` | POST: official-film + paid `IsPaid` fail-closed. |
| `ProductionRunwayBridgeV1Regression.cs` | T01–T56. |
| `ProductionPipelineV1Tests.cs` | xUnit fact renamed to T01–T56. |
| `famixa-production-os-real-provider-bridge.smoke.ts` | Static boundary smoke. |
| this report | Forensic record |

`HttpRunwayTransport` / `ContentRunwayClient` / P06 / Director / Timing V3 / Performance Contract / Veo were not modified. **FACT**

No new SQL migration. **FACT**

---

## 3. Adapter boundary

`IMotionExecutionAdapter` already has Start / Poll / Recover / Fetch. **FACT**

`RunwayMotionExecutionAdapter` implements that boundary. Production OS does not reference Runway endpoints or request schema. **IMPLEMENTED** **TESTED** (T10, T11, T12)

---

## 4. Provider registry

`ProductionMotionAdapterResolver` + `ProductionMotionCatalog`:

- `fixture` / `fixture-motion-v1` → `FixtureMotionAdapter`
- `runway` / `gen4_turbo` → `RunwayMotionExecutionAdapter`

No silent Runway→Fixture fallback. Unknown provider/model fail-closed. **IMPLEMENTED** **TESTED** (T01–T06)

DI default `IMotionExecutionAdapter` remains Fixture. **FACT**

---

## 5. Provider/model freeze

Task + InputSnapshot store explicit `ProviderId` / `ModelId`. One-sided missing → `MISSING_PROVIDER_MODEL`. Both missing still normalize to the injected fixture adapter (TEST default). That is not a Runway silent switch. **FACT** **TESTED** (T05–T07)

Provider mutation on the same idempotency key → `PROVIDER_CHANGED`. **TESTED**

---

## 6. Runway adapter

Owns model id, duration bins, prompt join, transport create/poll/download. **IMPLEMENTED**

OS `gen4_turbo` is the HOW id. Vendor alias stays in client (`ContentRunwayClient.TurboModel`). **FACT**

---

## 7–9. Start lifecycle / Attempt / ProviderJobId

Required sequence is implemented:

1. `StartAttempt` → STARTED  
2. `StartFrozenAsync`  
3. `SubmitProviderJobAsync` persists `providerJobId` immediately  
4. PROVIDER_SUBMITTED  
5. Then Poll / Recover  

OnBeforeGetTask asserts the attempt row already has the job id. **IMPLEMENTED** **TESTED** (T14, T15)

---

## 10–11. Poll / Recover

Poll and Recover both call `IRunwayTransport.GetTaskAsync(providerJobId)`. A fresh adapter + fresh Fake transport recovers the same persisted id with `HttpCallCount = 0`. **IMPLEMENTED** **TESTED** (T20, T21, T49)

Process-kill recovery against a live HTTP job is **UNVERIFIED** (this phase forbids real calls).

---

## 12–13. Idempotency / Retry

SUCCESS replay returns the same request/task/attempt/artifact and does not increment CreateCalls. **TESTED** (T17)

RUNNING / in-flight POST resumes the same attempt; CreateCalls unchanged. **TESTED** (T16, T51)

FAILED attempt stays immutable; same idempotency key does not Start again. **TESTED** (T18)

Explicit retry creates a new Attempt under the same Task. **TESTED** (T19)

---

## 14–15. Cost guard / Network guard

`ProductionMotionCostGuard`: UNKNOWN and OverBudget fail before transport. GET Evaluate now applies the same paid cost decision so GET == POST for budget. **IMPLEMENTED** **TESTED** (T42, T43, T44, T50)

Fake transport: `RequiresNetwork = false`, `HttpCallCount = 0`. Tests never construct `HttpRunwayTransport`. **TESTED** (T13)

---

## 16–18. Output / verification / lineage

Output is downloaded as bytes, written under `os-test/{testRunId}/video/`, then verified. **TESTED** (T22, T27)

Verification for VIDEO_TAKE:

1. bytes exist (≥32)  
2. SHA256  
3. MIME `video/mp4`  
4. ISO-BMFF decodable (`ftyp`/`moov`/`mdat`)  
5. `vide` handler present  
6. duration readable and `> 0`  
7–12. InputSnapshotHash / AttemptId / ProviderJobId / Performance* via frozen snapshot join  

Invalid MIME / undecodable / missing vide stream / unreadable duration → `FAILED_VERIFICATION`, no bind. **TESTED** (T23–T26, T35)

`ProductionArtifact` first-class columns remain: AttemptId, ProviderJobId, InputSnapshotHash, SHA256, storageKey. PerformanceSnapshotId / PerformanceHash live on `ProductionTaskInputSnapshot` and are asserted through that join. No parallel artifact columns were added. **FACT** **TESTED** (T28–T32)

Duration “satisfies contract” in this phase means readable and `> 0`. Editorial Timing V3 is not compared to Runway 5/10 bins. **FACT**

---

## 19–20. Binding / Preview

CURRENT and VISIBLE bind `artifactId` only after VERIFIED. **TESTED** (T33, T34)

Preview = VISIBLE → artifact → `storageKey`. Not a provider URL, not `acceptedTake.url`. **TESTED** (T36, T37, T55)

---

## 21–22. Late result / cross-scope

`ApplyLateResultAsync` marks LATE and does not steal CURRENT/VISIBLE. Superseded-task late results do not rebind. **TESTED** (T38, T39)

Provider change `google` / `veo-3.1` creates a new task/attempt and copies PerformanceSnapshotId/Hash. Veo adapter is not implemented. **TESTED** (T40, T41)

Cross-shot / cross-build / TEST↔FILM fail-closed. **TESTED** (T45–T47)

Official film + paid HOW is blocked on GET and POST. **TESTED** (T48)

---

## 23. Test transport

`FakeRunwayTransport` scenarios:

| Scenario | Behavior |
|---|---|
| A Success | submit → SUCCEEDED → valid MP4 |
| B Failed | submit → FAILED; no second job |
| C Recover | first GetTask PROCESSING, then SUCCEEDED; orphan adapter recovers |
| D DuplicateStart | OS in-flight replay; CreateCalls unchanged |
| E InvalidMp4 | undecodable bytes |
| F WrongDuration | unreadable mvhd duration |
| G Late | OS `ApplyLateResultAsync` |
| H DownloadFail | `OUTPUT_DOWNLOAD_FAILED` |
| I UnknownCost | cost guard, CreateCalls unchanged |
| J OverBudget | cost guard on GET and POST |

No network. No credentials. **IMPLEMENTED** **TESTED**

`DryRunRunwayTransport` remains for the existing 4A HTTP-prefix guard. **FACT**

---

## 24. Tests T01–T56

`ProductionRunwayBridgeV1Regression` **TESTED**. xUnit `Real_provider_bridge_v1_T01_through_T56` **PASS**.

| IDs | Result |
|---|---|
| T01–T09 | PASS |
| T10–T15 | PASS |
| T16–T21 | PASS |
| T22–T27 | PASS |
| T28–T37 | PASS |
| T38–T44 | PASS |
| T45–T51 | PASS |
| T52–T56 | PASS |

TypeScript smoke `famixa-production-os-real-provider-bridge.smoke.ts` **PASS**.

---

## 25. Existing regression suites

`ProductionPipelineV1Tests` 14/14 **PASS** (work, pipeline, cutover, contract, scope, test foundation, keyframe, 3B, bridge, paid permit, action authority, Phase A/B/C). **TESTED**

Infrastructure build succeeded. Pre-existing Newtonsoft.Json NU1903 / CS4014 on unrelated Infrastructure remain. **FACT** (PRE-EXISTING)

---

## 26–27. Database / live DB

No schema change. Existing Production OS tables already store Attempt.ProviderJobId and Artifact.ProviderJobId / InputSnapshotHash. **FACT**

Live DB apply: not performed. **UNVERIFIED**  
Do not claim LIVE VERIFIED.

---

## 28–30. Provider calls / media / EP01–EP99

**REAL PROVIDER CALLS = 0**  
**REAL MEDIA GENERATION = 0**  

Official film UUIDs:

- EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
- EP99 `77262ec0-74ad-459d-9128-f09bb553a5dd`  

Paid HOW on those scopes is `OFFICIAL_FILM_FORBIDDEN`. In-memory tests never write official media. **TESTED** (T48) **UNVERIFIED** against live files (no live run).

---

## 31. Remaining gaps

1. Live `HttpRunwayTransport` + paid permit + one TEST keyframe is the next phase, not this one. **RECOMMENDED**
2. Last-frame packaging exists on `ContentRunwayClient` and is not exercised by the OS adapter. **FACT**
3. `ProductionCreateVideoPreflight` still contains 5/10 provider-duration bins for the 4A prepare path. Not used by Fake tests. **FACT**
4. `BindAsync` does not itself reject a superseded task; late authority is `ApplyLateResultAsync`. **FACT**
5. Artifact does not store PerformanceSnapshotId/Hash as first-class columns. Lineage is the snapshot join. **FACT**
6. Browser / Director / Vite were not opened. **UNVERIFIED**
7. Restart-safe recovery against a real Runway job id is **UNVERIFIED**.

---

## 32. Exact next phase

**STOP.**

The next phase, if and only if this report is accepted, is:

**FAMIXA PRODUCTION OS — REAL RUNWAY TEST EXECUTION V1**

That phase may create exactly ONE real TEST video using a frozen PerformanceSnapshot and a verified physical TEST keyframe.

Do not start it from this report.  
Do not call Runway.  
Do not generate voice or lip-sync.  
Do not implement Veo.  
Do not modify Director, Canon, VA, Timing V3, or Performance Contract.

---

## Critical questions

**Q1.** Can Production OS select runway/gen4_turbo without legacy localStorage or DI defaults?  
**Yes.** Explicit `ProviderId`/`ModelId` → resolver. **TESTED** (T02, T07)

**Q2.** Can the Runway provider job ID survive process restart?  
**Yes** as persisted Attempt.ProviderJobId. Live process-kill **UNVERIFIED**. **TESTED** (T15, T21, T49)

**Q3.** Can Production OS recover a RUNNING provider job?  
**Yes** via RecoverFrozenAsync(providerJobId). **TESTED** (T20, T21)

**Q4.** Can duplicate in-flight requests avoid a second job?  
**Yes.** CreateCalls unchanged. **TESTED** (T16, T51)

**Q5.** Can a provider output become a ProductionArtifact only after real verification?  
**Yes.** Invalid MIME/stream/duration/bytes never VERIFIED. **TESTED** (T23–T27, T35)

**Q6.** Can a provider URL become CURRENT/VISIBLE directly?  
**No.** Binding uses artifactId; preview uses storageKey. **TESTED** (T33, T34, T36, T37)

**Q7.** Can a late Runway result overwrite a newer task?  
**No.** LATE does not steal CURRENT/VISIBLE. **TESTED** (T38, T39)

**Q8.** Does PerformanceSnapshot remain identical through the chain?  
**Yes.** SnapshotId/Hash frozen on InputSnapshot; provider change copies them. **TESTED** (T08, T09, T28, T29, T40, T52, T53)

**Q9.** Can UNKNOWN cost reach the network?  
**No.** Guard runs before Start. Fake CreateCalls unchanged. **TESTED** (T42)

**Q10.** Can any TEST execution write to EP01/EP99?  
**No** on this path. Official + paid is forbidden; storage is `os-test/{testRunId}/video/`. **TESTED** (T22, T48)

---

## Acceptance A–P

| | Criterion | Result |
|---|---|---|
| A | Runway adapter implements OS boundary | PASS |
| B | Provider/model explicit and frozen | PASS |
| C | ProviderJobId persisted before polling | PASS |
| D | Poll/Recover restart-safe (persisted id) | PASS (live kill UNVERIFIED) |
| E | In-flight idempotency | PASS |
| F | Cost UNKNOWN / over budget blocks network | PASS |
| G | Artifact only after verification | PASS |
| H | Complete lineage | PASS (snapshot join for Performance*) |
| I | Binding uses artifactId | PASS |
| J | Preview uses VISIBLE artifact | PASS |
| K | Late result cannot overwrite | PASS |
| L | Cross-shot/build/scope fail-closed | PASS |
| M | All tests pass | PASS |
| N | No real provider calls | PASS |
| O | No media generation | PASS |
| P | EP01/EP99 untouched | PASS |

---

## STOP

Do not call Runway.  
Do not generate video, voice, or lip-sync.  
Do not benchmark models.  
Do not modify Director, EP01, EP99, Canon, Visual Authority, Timing V3, or Performance Contract.  
Do not implement Veo bridge.
