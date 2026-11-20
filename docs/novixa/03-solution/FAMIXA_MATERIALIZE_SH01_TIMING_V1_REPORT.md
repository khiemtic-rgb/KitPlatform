# FAMIXA MATERIALIZE SH01 TIMING V1

Date: 2026-09-20  
Mode: IMPLEMENT → TEST → LIVE VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: Provider calls = 0. CREATE_VIDEO not executed. Permit remains ISSUED.

---

## 1. VERDICT

**PASS**

The explicit Director production decision `performanceDurationSec = 5.0` is now Production OS authority via the existing PerformanceSnapshot + Timing V3 path. CREATE_VIDEO is READY. Runway was not called.

---

## 2. Provider calls

**0**

---

## 3. Media generation

**0**

---

## 4. Timing

| Field | Value |
|---|---|
| durationSec | **5.0** |
| source | **DIRECTOR** |
| decision type | **EXPLICIT_PRODUCTION_DECISION** |
| stage | VIDEO |
| provenance | PerformanceSnapshot `source_json` + `timingRef` (`FAMIXA_SHOT_TIMING_CONTRACT_V3`) |
| timing hash | `TIMING_HASH_V1:92e78482c36ad603a98f3019bb8d6a9baf338f20f0d39fb223a179df2f6b046d` |

Not copied from Runway / Veo / TTS / preview / take / capability.

---

## 5. PerformanceSnapshot

| Field | Value |
|---|---|
| id | `f596c3eb-1dd4-4a3e-9077-f7fd40776116` |
| hash | `PERFORMANCE_HASH_V1:863ff9a9fc9d2757f9f02f9e4e48308402d969323953cfe9a197fa38106655f3` |
| status | FROZEN / immutable |
| keyframe ref | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` |

Story, ActingBeat, VisualContract hashes recorded. Duplicate POST returned the same id.

---

## 6. Keyframe

| Field | Value |
|---|---|
| artifactId | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` |
| SHA | `7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9` |
| storageKey | `os-kf/8f9548b1b0744eb1a5b9295889960b9c` |
| verification | VERIFIED |

No second keyframe. Pixels unchanged.

---

## 7. AI selection

| Field | Value |
|---|---|
| selectionId | `9b0dceee-ba53-4cc5-8139-ee9ba58a2011` |
| provider | runway |
| model | gen4_turbo |

Unchanged. Graph `wan` / `wan-2.1` not rewritten.

---

## 8. Paid permit

| Field | Value |
|---|---|
| permitId | `e18abea2-1d1b-4d2b-b96a-79bec0e6dc16` |
| status | **ISSUED** |

GET / materialize / reload did not consume it.

---

## 9. CREATE_VIDEO

| Field | Value |
|---|---|
| available | **true** |
| blockReason | **none** |

Live GET `/actions` and `/state` after rebuild + restart.

---

## 10. Reload verification

API rebuilt and restarted. After restart: `available = true`, snapshot/selection/permit unchanged, Runway start = 0.

---

## 11. EP01 graph

| | |
|---|---|
| before | `415fedf306ed23d1ace94a794400b440` |
| after | `415fedf306ed23d1ace94a794400b440` |
| unchanged | **true** |

---

## 12. EP99

| | |
|---|---|
| before | `c7cd2516627332c2648c1509395e4a64` |
| after | `c7cd2516627332c2648c1509395e4a64` |
| unchanged | **true** |

---

## 13. T01–T24

| ID | Result |
|---|---|
| T01 duration = 5.0 | PASS |
| T02 source = DIRECTOR | PASS |
| T03 provenance preserved | PASS |
| T04 immutable hash present | PASS |
| T05 PerformanceSnapshot created | PASS |
| T06 PerformanceSnapshot immutable | PASS |
| T07 Story preserved | PASS |
| T08 ActingBeat preserved | PASS |
| T09 VisualContract preserved | PASS |
| T10 Keyframe artifact unchanged | PASS |
| T11 Keyframe SHA unchanged | PASS |
| T12 runway/gen4_turbo selection unchanged | PASS |
| T13 WAN does not override | PASS |
| T14 existing permit preserved | PASS |
| T15 permit not consumed | PASS |
| T16 CREATE_VIDEO becomes available | PASS |
| T17 readiness survives reload | PASS |
| T18 duplicate materialization idempotent | PASS |
| T19 no provider calls | PASS |
| T20 no media generation | PASS |
| T21 EP01 graph unchanged | PASS |
| T22 EP99 unchanged | PASS |
| T23 cross-shot protection | PASS |
| T24 Production OS remains authority | PASS |

Unit + API video-generation-execution regression fail=0. Smoke ok.

---

## Future CREATE_VIDEO input (not executed)

```
SHOT:                 EP01-SC01-SH01
KEYFRAME:             1f4c26e0-35aa-4395-94e7-09d2b1b764b6
KEYFRAME SHA:         7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9
PERFORMANCE DURATION: 5.0 seconds
PERFORMANCE SNAPSHOT: f596c3eb-1dd4-4a3e-9077-f7fd40776116
PROVIDER:             runway
MODEL:                gen4_turbo
SELECTION:            9b0dceee-ba53-4cc5-8139-ee9ba58a2011
EXECUTION PERMIT:     e18abea2-1d1b-4d2b-b96a-79bec0e6dc16  ISSUED
```

---

## 14. Remaining gaps

Paid official-film **execute** remains blocked until the next phase consumes the ISSUED permit on the CREATE_VIDEO Start path. This phase must not do that.

---

## 15. NEXT ACTION

**STOP. CREATE_VIDEO is READY. Do not call Runway. Do not execute CREATE_VIDEO in this phase.**
