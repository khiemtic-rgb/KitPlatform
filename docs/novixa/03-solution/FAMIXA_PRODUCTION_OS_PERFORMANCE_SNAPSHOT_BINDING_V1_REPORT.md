# FAMIXA PRODUCTION OS — PERFORMANCE SNAPSHOT BINDING V1
PHASE B REPORT

Date: 2026-09-18  
Mode: IMPLEMENT + TEST  
Scope: TEST only  

Provider calls: 0  
Media generation: 0  
EP01 / EP99: untouched  
CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC required identity: unchanged  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

Do not read this as Phase C.  
Do not read this as VIDEO/VOICE consuming PerformanceSnapshot as required identity.

---

## 1. FILES CHANGED

| File | Role |
|---|---|
| `ProductionEntities.cs` | nullable Performance* on InputSnapshot |
| `ShotExecutionInput.cs` | optional claim fields; SnapshotHash extra terms only when bound |
| `PerformanceSnapshotBinding.cs` | fail-closed resolve/verify/copy |
| `ProductionWorkExecutionEngine.cs` | bind at FreezeInput; provider-change keeps WHAT |
| `ProductionExecutionService.cs` | optional performance store |
| `ProductionWorkRepository.cs` | persist/read new columns |
| `migrations/374_pack_content_production_performance_binding.sql` | additive columns |
| `migration-files.content.txt` | +374 |
| `PerformanceSnapshotBindingV1Regression.cs` | T01–T22 |
| `VideoGenerationExecutionService.cs` | register suite |
| `ProductionPipelineV1Tests.cs` | xUnit fact |
| `famixa-performance-snapshot-binding.smoke.ts` | contract/freeze static proof |

Not changed: `ProductionActionContractRegistry`, Director CTA, adapters, Timing V3, Canon, VA, EP01/EP99, Phase A snapshot table as SoT. **FACT**

---

## 2. SCHEMA CHANGES

Additive only on `pack_content.production_task_input_snapshots`:

- `performance_snapshot_id UUID NULL` FK → `pack_content.performance_snapshot`
- `performance_hash VARCHAR(96) NULL` CHECK prefix `PERFORMANCE_HASH_V1:`
- `performance_payload_json TEXT NULL`

**IMPLEMENTED**

No second PerformanceSnapshot table. No drop of `acting_beat` / `acting_hash` / `snapshot_hash`. **FACT**

`ProductionWorkIds.SchemaVersion` remains `372`. **FACT**

Migration 374 not applied to a live TEST database this phase. **UNVERIFIED**

---

## 3. BINDING IMPLEMENTATION

At `FreezeInputAsync`, if the execution input **claims** a snapshot (id / hash / payload):

1. Official film scope → `OFFICIAL_FILM_FORBIDDEN`
2. Claimed hash must be `PERFORMANCE_HASH_V1:` or reject (`INVALID_PERFORMANCE_HASH`)
3. SnapshotId required — no latest / graph fallback
4. Resolve exact row from Phase A store
5. Verify stored hash == SHA256(payload)
6. Claimed hash/payload must match
7. `buildId` + `shotCode` must match task scope
8. Copy immutable payload into InputSnapshot

If no claim: legacy freeze, Performance* stay null. Existing OS tests keep passing. **IMPLEMENTED** + **TESTED**

No re-project of PerformanceDraft after freeze. **FACT**

---

## 4. IDENTITY VERIFICATION

| Check | Fail code |
|---|---|
| Missing / unknown id | `SNAPSHOT_NOT_FOUND` |
| Legacy hash as PerformanceHash | `INVALID_PERFORMANCE_HASH` |
| Hash or payload mismatch | `FAILED_IDENTITY` |
| Cross-shot | `CROSS_SHOT_FORBIDDEN` |
| Cross-build / TEST scope | `CROSS_SCOPE_FORBIDDEN` |
| EP01/EP99 | `OFFICIAL_FILM_FORBIDDEN` |

No silent repair. No ActingHash / VoiceHash / SnapshotHash alias. **IMPLEMENTED** + **TESTED** (T02–T06, T12, T18–T19)

`SnapshotHash` (task) includes Performance* **only when bound**. Unbound hashes unchanged. **IMPLEMENTED** + **TESTED** (T20)

---

## 5. IMMUTABILITY BEHAVIOR

Task A frozen on P001 stays P001 after P002 is created and after draft/graph mutation. **TESTED** (T07, T08)

New task required for P002. **TESTED**

Same P001 may be referenced by two tasks. **TESTED** (T09)

Identical content, two freeze ids: task stores the **exact** SnapshotId it froze. **TESTED** (T10)

Provider / timing / visual HOW changes do not rewrite Performance*. **TESTED** (T14–T16)

`NewTaskForProviderChangeAsync` now forwards frozen PerformanceSnapshotId+Hash so HOW change cannot drop WHAT. **IMPLEMENTED**

---

## 6. CROSS-SHOT / BUILD / SCOPE GUARDS

- SH01 snapshot → SH02 task: FAIL, task remains CREATED (no InputSnapshot). **TESTED** (T05)
- Build A → Build B: FAIL. **TESTED** (T06, T18)
- Official EP01 bind / EP99 snapshot freeze: FAIL. **TESTED** (T19)

---

## 7. TESTS T01–T22

C# `PerformanceSnapshotBindingV1Regression` **PASS**. **TESTED**

TS binding smoke **PASS**. **TESTED**

T17 reload is in-memory store get-after-insert, not live Postgres. **TESTED** / live DB **UNVERIFIED**

---

## 8. PRODUCTION OS REGRESSION

| Suite | Result |
|---|---|
| Phase A Performance Contract | PASS **TESTED** |
| CREATE_VIDEO TEST E2E | PASS **TESTED** |
| Complete Action Authority | PASS **TESTED** |

---

## 9. API TESTS

No CREATE_VIDEO/VOICE/LIPSYNC request DTO cutover. **FACT**

Phase A TEST endpoints unchanged. Binding is exercised through `FreezeInputAsync`. **TESTED**

HTTP freeze-with-snapshot against a running API: **UNVERIFIED**

---

## 10. LIVE DB VERIFICATION

Not executed against live TEST Postgres. **UNVERIFIED**

---

## 11. PROVIDER CALLS

**0**. **FACT** + **TESTED**

---

## 12. MEDIA GENERATION

**0** real generations. **FACT**

---

## 13. OFFICIAL FILM SAFETY

EP01/EP99 scope bind forbidden. EP99 PerformanceSnapshot freeze forbidden. No film backfill in mig 374. **TESTED** + **FACT**

Live official graph not queried. **UNVERIFIED**

---

## 14. REMAINING GAPS

1. CREATE_VIDEO/VOICE still do not **require** PerformanceSnapshot. **FACT**  
2. Acting change still does not stale VOICE. **FACT**  
3. Mig 373/374 not applied live. **UNVERIFIED**  
4. LipSync still has no cross-P001/P002 refuse at action contract. **FACT** (Phase C)

---

## 15. EXACT NEXT PHASE

**Phase C:** CREATE_VIDEO and CREATE_VOICE required identity include the same PerformanceSnapshot; LipSync refuse mismatched parent hashes. Keep Timing V3 / Canon / VA / physical parent artifacts.

Do **not** start Phase C in this increment.

STOP.
