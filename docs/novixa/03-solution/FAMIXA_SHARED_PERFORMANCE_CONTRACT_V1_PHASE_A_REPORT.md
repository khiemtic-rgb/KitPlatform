# FAMIXA SHARED PERFORMANCE CONTRACT V1
PHASE A IMPLEMENTATION REPORT

Date: 2026-09-18  
Mode: IMPLEMENT + TEST  
Scope: TEST only  

Provider calls: 0  
Media generation: 0  
EP01 / EP99: untouched  
CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC contracts: unchanged  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

Phase A is implemented. **IMPLEMENTED** + **TESTED**

Famixa now has an authoritative, provider-agnostic Performance WHAT:

```
PerformanceDraft → freeze → PerformanceSnapshot
  PerformanceSnapshotId (UUID)
  PerformanceHash = PERFORMANCE_HASH_V1: + SHA256(PERFORMANCE_CANON_V1)
```

VIDEO / VOICE / LIPSYNC do **not** consume this snapshot yet. **FACT**

`ShotActingBeat` and `LinePerformance` remain graph authority. The contract is **projected** from them. **IMPLEMENTED**

STOP. Do not start Phase B automatically.

---

## 2. FILES CHANGED

| File | Role |
|---|---|
| `PerformanceContractIds.cs` | constants / failure codes |
| `PerformanceContractEntities.cs` | draft / snapshot / projection types |
| `PerformanceContractCanon.cs` | PERFORMANCE_CANON_V1 + hash |
| `PerformanceContractRules.cs` | project / validate / emotion inherit |
| `PerformanceContractService.cs` | Create / Validate / Freeze / Get / Bind |
| `IPerformanceSnapshotStore.cs` | store + in-memory TEST store |
| `PerformanceContractV1Regression.cs` | T01–T25 + OS regression |
| `PerformanceSnapshotRepository.cs` | durable SQL insert/get, no UPDATE |
| `migrations/373_pack_content_performance_snapshot.sql` | empty TEST-safe table |
| `deploy/ubuntu/migration-files.content.txt` | manifest +373 |
| `ContentPackDependencyInjection.cs` | DI |
| `VideoGenerationExecutionService.cs` | register suite |
| `ContentProductionController.cs` | 3 TEST endpoints |
| `ProductionApiDtos.cs` | request/response DTOs |
| `ProductionPipelineV1Tests.cs` | xUnit fact |
| `famixa-shared-performance-contract.ts` | client projection + freeze |
| `famixa-shared-performance-contract.smoke.ts` | T01–T25 |

Not changed: `ProductionActionContractRegistry`, Timing V3, Canon, VA, adapters, Director CTA, EP01/EP99, `ShotActingBeat`, `LinePerformance`. **FACT**

---

## 3. DOMAIN TYPES

Root: `PerformanceDraft`, `PerformanceSnapshot`. **IMPLEMENTED**

Fields: `documentId`, `schemaVersion`, `buildId`, `shotCode`, `episodeCode`, `sceneCode`, `shotPerformance`, `characters[]`, `lines[]`, `timingRef`, `visualRef`, `storyRef`. **IMPLEMENTED**

Snapshot-only: `performanceSnapshotId`, `performanceHash`, `frozenAt`, `createdBy`, `source`. **IMPLEMENTED**

Shot slice: `before.{action,body,prop,gaze,room}`, `during.{emotion,intensity}`, `after.{action,gaze}`, `gazeTarget`. No `holdSec` / `speech` / `pace` / `providerDurationSec`. **IMPLEMENTED** + **TESTED** (T01/T19)

Character-in-shot: `characterId`, `participation`, `visualRole`, `speechRole`, `emotionalState?`, `intensity?`, `lineIds[]`. **IMPLEMENTED**

Line: `lineId`, `characterId`, `textIdentity.{text,textHash}`, `delivery.{emotion,intensity,pace,volume,pauseSec,label,emphasis?,locked}`. No VoiceId. **IMPLEMENTED**

---

## 4. CANONICAL SERIALIZATION

`PERFORMANCE_CANON_V1`: UTF-8 compact JSON, specified key order, omit undefined / empty strings, omit empty objects, arrays keep projection order. Instance metadata excluded. **IMPLEMENTED** + **TESTED** (T19)

---

## 5. HASH IMPLEMENTATION

`PerformanceHash = PERFORMANCE_HASH_V1: + lowercase SHA256(canonical bytes)`. **IMPLEMENTED**

Not reused: `ActingHash`, `actingBeatFingerprint`, `VoiceHash`, `voiceInputFingerprint`, `SnapshotHash`. **TESTED** (T20/T21)

`PerformanceSnapshotId` is a new UUID per freeze. **TESTED** (T02/T05)

---

## 6. DRAFT PROJECTION

`CreateDraft` / `projectPerformanceDraft` reads:

- `ShotActingBeat` visual fields only (drops holdSec/speech/pace)
- locked `LinePerformance` as L3
- `characterIds` order
- `dialogueSegmentIds` order

Does not rewrite the graph. **IMPLEMENTED** + **TESTED** (T01)

Ambiguous missing line ids → `UNRESOLVED_PROJECTION`. Spoken line with no L1/L2/L3 after inherit → unresolved (C#) or existing infer marked unlocked (TS draft only). **IMPLEMENTED**

---

## 7. FREEZE IMPLEMENTATION

Freeze copies payload, assigns new id + hash, inserts; does not mutate the caller's later drafts. **TESTED** (T02–T06)

Two identical drafts: same hash, different ids. **TESTED** (T04/T05)

---

## 8. VALIDATION RULES

| Rule | Code | Test |
|---|---|---|
| L1 ≠ L3 allowed | — | T09 |
| L1 does not inherit first line | — | T10 |
| Unlocked spoken line not voice-ready; freeze rejected | `SPOKEN_LINE_UNLOCKED` | T11 |
| Unknown line character | `INVALID_CHARACTER` | T12 |
| Two PRIMARY | `DUPLICATE_PRIMARY` | T13 |
| Cross-shot bind | `CROSS_SHOT_FORBIDDEN` | T14 |
| Cross-build / official film | `CROSS_SCOPE_FORBIDDEN` / `OFFICIAL_FILM_FORBIDDEN` | T15 |
| Provider / VoiceId / duration injected | matching codes | T16–T18 |
| Multi-character one speaker | valid | T24 |
| Multi-speaker snapshot allowed; no LipSync cutover | valid + registry unchanged | T25 |

Emotion inherit: L3 ← L2 ← L1; never overwrite explicit; no emotion-distance checker. **IMPLEMENTED**

---

## 9. PERSISTENCE

In-memory store used by T01–T25. **TESTED**

SQL table `pack_content.performance_snapshot` (mig 373): insert + get, no UPDATE. Payload stored, not a `graph_json` pointer. **IMPLEMENTED**

Live DB apply / HTTP reload against Postgres: **UNVERIFIED** (migration written, not applied this phase).

Official film rows: none inserted. **FACT**

---

## 10. API

TEST-only, no provider execution:

- `POST /api/content/production/performance-drafts`
- `POST /api/content/production/performance-snapshots`
- `GET /api/content/production/performance-snapshots/{id}`

**IMPLEMENTED**. HTTP live call: **UNVERIFIED** (compiled, not browser-hit).

---

## 11. TEST SCOPE RESULTS

| Id | Result |
|---|---|
| T01–T25 C# | PASS |
| T01–T25 TS | PASS |
| Nested CREATE_VIDEO TEST | PASS |
| Nested Complete Action Authority | PASS |

**TESTED**

---

## 12. REGRESSION RESULTS

| Suite | Result |
|---|---|
| `ProductionTestCreateVideoV1Regression` | PASS **TESTED** |
| `ProductionCompleteActionAuthorityV1Regression` | PASS **TESTED** |
| `famixa-production-os-ui-cutover.smoke.ts` | PASS FAIL=0 **TESTED** |
| CREATE_VIDEO required inputs still include `ActingBeat` | **FACT** |
| CREATE_VOICE still `LineText` + `VoiceId` | **FACT** |
| CREATE_LIPSYNC still parent artifact ids | **FACT** |

---

## 13. OFFICIAL FILM SAFETY EVIDENCE

- Freeze with `OfficialEp01` (`4ef164d6-…`) throws `OFFICIAL_FILM_FORBIDDEN`. **TESTED**
- No writes to `series_build` / EP99. **FACT**
- TEST build ids are dedicated UUIDs, not official film scope ids. **FACT**
- Live EP01/EP99 graph values: **UNVERIFIED** (not queried)

---

## 14. PROVIDER CALL COUNT

**0** paid / real provider calls. **FACT**

Nested OS suites use existing unpaid fixture adapters only. **TESTED**

---

## 15. MEDIA GENERATION COUNT

**0** real media generations. **FACT**

---

## 16. REMAINING GAPS

1. CREATE_VIDEO / CREATE_VOICE still freeze flatten / VoiceHash, not `PerformanceSnapshotId`. **FACT**  
2. Acting change still does not stale VOICE. **FACT**  
3. Mig 373 not applied on a live TEST database this phase. **UNVERIFIED**  
4. HTTP endpoints not live-hit. **UNVERIFIED**  
5. Client TS hash uses `node:crypto` (smoke/node). Not imported by UI. **ASSUMED** safe until a UI import.  
6. Director UI still edits `ShotActingBeat` / `LinePerformance` only. **FACT**

---

## 17. EXACT NEXT PHASE

**Phase B — Production OS references snapshot.** **RECOMMENDED**

- Nullable `performance_snapshot_id` / `performance_hash` / payload on task input snapshots.  
- Keep `acting_beat` / `ActingHash` as compatibility.  
- Do not change CREATE_VIDEO/VOICE required identity to the snapshot until Phase C.

Do **not** start Phase B in this increment.

STOP.
