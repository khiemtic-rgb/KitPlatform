# FAMIXA PRODUCTION OS — EP01 SH01 PRODUCTION INGRESS V1 REPORT

Date: 2026-09-20  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: REAL AI = 0. Runway Start = 0. No CREATE_VIDEO execution. Graph not rewritten.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

**PASS_WITH_GAPS**

Production Ingress materialized the recoverable canonical Production OS inputs for EP01 SH01. CREATE_VIDEO was **not** executed.

What is now Production OS authority:

| Input | Status | Class |
|---|---|---|
| Story | AVAILABLE (CREATE_IMAGE snapshot) | LIVE_VERIFIED |
| ActingBeat | AVAILABLE (CREATE_IMAGE snapshot) | LIVE_VERIFIED |
| VisualContract | AVAILABLE (shot-level visualSpec, no new VA) | LIVE_VERIFIED |
| KEYFRAME artifact | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` VERIFIED | LIVE_VERIFIED |
| CURRENT:KEYFRAME | bound to that artifact | LIVE_VERIFIED |
| Physical SHA256 | `7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9` | LIVE_VERIFIED |
| pictureRevisionId | `picture:EP01-SC01-SH01:007` recorded in provenance | LIVE_VERIFIED |

CREATE_VIDEO readiness after ingress:

`available = false`  
`blockReason = MISSING_TIMING`  
`missingInputs = [Timing]`

LIVE_VERIFIED

Remaining gaps this phase was not allowed to close:

1. **Timing** — no approved non-Runway duration exists on the shot. Inventing `5s` or copying Runway diagnostics is forbidden. FACT
2. **PerformanceSnapshot** — freeze on official film is `OFFICIAL_FILM_FORBIDDEN`. Not invented. FACT
3. **AI selection** — no Production AI Selection persist API. Graph remains `wan / wan-2.1 / EXPLICIT`. Silent map to `runway / gen4_turbo` was not done. FACT / LIVE_VERIFIED
4. Paid official-film CREATE_VIDEO remains `OFFICIAL_FILM_FORBIDDEN` / permit TEST-only. Not lifted. FACT

STOP. Do not call Runway. Do not start REAL FILM RUN V2 until Timing is sourced without invention and selection is updated by an explicit authorized API.

---

## 2. SAFETY

| Rule | Result | Class |
|---|---|---|
| Runway Start | 0 | LIVE_VERIFIED |
| Veo / Gemini / Wan / Fal / ElevenLabs | 0 | LIVE_VERIFIED |
| IMAGE / VIDEO / VOICE / LIPSYNC / MIX / ASSEMBLY generation | 0 | LIVE_VERIFIED |
| CREATE_VIDEO executed | no | LIVE_VERIFIED |
| Official-film paid permit | unchanged | FACT |
| Graph rewritten | no | LIVE_VERIFIED |
| Veo take overwritten | no | LIVE_VERIFIED |
| EP99 writes | 0 | LIVE_VERIFIED |
| Canon / Master / DNA / PRP / CRP / VA / Timing V3 / Motion / Assembly / Final | not modified | FACT |
| Second orchestrator / state machine | none | FACT |
| Provider adapters / Runway transport | not modified | FACT |

---

## 3. FORENSIC SOURCE MAP

Read-only before mutation. Official EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`.

| Item | Location | Classification | Class |
|---|---|---|---|
| A. Story | `runs.EP01-SC01-SH01.shotAction` Vietnamese stage direction | LEGACY_SOURCE → ingested | LIVE_VERIFIED |
| B. shotAction / story text | same field, 136 chars | LEGACY_SOURCE | LIVE_VERIFIED |
| C. ActingBeat | no shorts/actingBeat row; `visualSpec.intent` + `gaze` + `performance` | LEGACY_SOURCE → ingested | LIVE_VERIFIED |
| D. VisualContract | `runs.*.visualSpec` JSON (Linh/Minh, 35mm, WIDE) | LEGACY_SOURCE → shot-level OS input | LIVE_VERIFIED |
| E. Timing | run `seconds`/`durationSec` empty; shorts `[]`; editorial `shots: {}` | UNUSABLE without invention | LIVE_VERIFIED |
| F. Approved KF metadata | `kfApproved=true`, `kf-EP01-SC01-SH01-canon.jpg`, `picture:EP01-SC01-SH01:007` | LEGACY_SOURCE | LIVE_VERIFIED |
| G. pictureRevisionId | `picture:EP01-SC01-SH01:007` | LEGACY_SOURCE | LIVE_VERIFIED |
| H. Keyframe filename | `kf-EP01-SC01-SH01-canon.jpg` | LEGACY_SOURCE | LIVE_VERIFIED |
| I. API host pixels | not on disk before ingress | UNUSABLE | LIVE_VERIFIED |
| J. Browser IDB | Chrome `localhost:5173` `kit-famixa-kf` blob `3/14/1450` | CACHE / RECOVERY SOURCE | LIVE_VERIFIED |
| K. AI selection | `graph_json.aiSelection` wan/wan-2.1 EXPLICIT | LEGACY_SOURCE (not OS) | LIVE_VERIFIED |
| L. Provider metadata | `pictureSelectionSnapshot` gemini historical HOW; runwayAttempts historical | DISPLAY_ONLY / HOW | LIVE_VERIFIED |
| M. Graph take URL | Veo `veo-video?taskId=veo_…89dd76ca…` | LEGACY display/transport | LIVE_VERIFIED |
| N. Production OS rows before | tasks 0, bindings 0 | — | LIVE_VERIFIED |

`kfSourceHash = hf82234a0:125347` is a **data-URL string hash** (`dataUriHash`), not a physical SHA. Physical JPEG is 93991 bytes, 1280×720. FACT

Runway diagnostics `duration: 5` is **not** used as Timing. FACT

---

## 4. STORY INGRESS

Source: `runs.EP01-SC01-SH01.shotAction` only.

```
(Tiếng cạch cửa. Minh bước vào, đầu hơi cúi, quai cặp kéo lê một bên vai. Người mẹ đang lau dọn bàn ăn, ngẩng lên nhìn dáng đi của con.)
```

Materialized on CREATE_IMAGE input snapshot. GET `/state` Story = AVAILABLE after reload. LIVE_VERIFIED

Provenance: `sourceType=LEGACY_INGRESS`, `sourceBuildId`, `sourceShotId=EP01-SC01-SH01`, `sourceField=runs.EP01-SC01-SH01.shotAction|visualSpec`, `sourceHash=hf82234a0:125347`. IMPLEMENTED / LIVE_VERIFIED

CREATE_VIDEO evaluate reads OS snapshot, not `graph_json.story`. LIVE_VERIFIED

---

## 5. ACTING BEAT INGRESS

No `pack_content` actingBeat / shorts row. Source is existing visualSpec text only:

- intent
- gaze
- performance

Concatenated. No invented emotion labels. Persistent on CREATE_IMAGE snapshot. GET `/state` Acting = AVAILABLE. LIVE_VERIFIED

---

## 6. VISUAL CONTRACT INGRESS

Shot-level VisualContract = existing visualSpec (Linh CHAR-003 primary, Minh CHAR-001 secondary, 35mm, WIDE, FAMIXA / 3D_STYLIZED_REALISM).

Does **not** create Visual Authority. Does **not** modify Master / DNA / PRP / CRP / Universe / Project Visual Mode. Provider/model is not part of this contract. FACT / IMPLEMENTED

---

## 7. PERFORMANCE SNAPSHOT

Required freeze API: `POST /performance-snapshots`.

`PerformanceContractRules.ValidateForFreeze` on official EP01 → `OFFICIAL_FILM_FORBIDDEN`. TESTED

`ActionRequiresSnapshot(CREATE_VIDEO, official film) = false`, so CREATE_VIDEO does not currently demand a snapshot. FACT

No snapshot was frozen. Timing was not invented. T12/T13/T14. TESTED

---

## 8. KEYFRAME RECOVERY

Exact approved artifact recovered from Chrome IndexedDB recovery source permitted by the mission:

- Key: `FAMIXA:kf:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:EP01-SC01-SH01`
- Filename: `kf-EP01-SC01-SH01-canon.jpg`
- `dataUriHash` = `hf82234a0:125347` (exact graph stamp)
- Blob: `...\IndexedDB\http_localhost_5173.indexeddb.blob\3\14\1450`

API host / App_Data / repo had **no** copy before import. Gemini image-generation executions for shot `66559efe-…` are a different still and were **not** used. LIVE_VERIFIED

---

## 9. KEYFRAME PHYSICAL VERIFICATION

| Check | Result | Class |
|---|---|---|
| Bytes exist | 93991 | LIVE_VERIFIED |
| MIME | image/jpeg (`FF D8`) | LIVE_VERIFIED |
| Readable | yes | LIVE_VERIFIED |
| Dimensions | 1280×720 | LIVE_VERIFIED |
| Build / shot | EP01 / SH01 key + filename | LIVE_VERIFIED |
| pictureRevisionId | `picture:EP01-SC01-SH01:007` | LIVE_VERIFIED |
| Replacement pixels | no — hash match | LIVE_VERIFIED |
| Physical SHA256 | `7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9` | LIVE_VERIFIED |

`kfSourceHash` was not treated as a physical SHA. FACT

---

## 10. KEYFRAME ARTIFACT

CREATE_IMAGE / `import` / `existing-pixels` via existing `POST /api/content/production/keyframes`.

| Field | Value | Class |
|---|---|---|
| artifactId | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` | LIVE_VERIFIED |
| role | KEYFRAME | LIVE_VERIFIED |
| status | VERIFIED | LIVE_VERIFIED |
| mime | image/jpeg | LIVE_VERIFIED |
| size | 93991 | LIVE_VERIFIED |
| byteSha256 | `7a158fa6…783e9` | LIVE_VERIFIED |
| storageKey | `os-kf/8f9548b1b0744eb1a5b9295889960b9c` | LIVE_VERIFIED |
| physical file | `App_Data/kit-video-i2v/os-kf/8f9548b1….mp4` (JPEG bytes, SHA match, `FFD8`) | LIVE_VERIFIED |

No URL identity. IMPLEMENTED / LIVE_VERIFIED

---

## 11. CURRENT:KEYFRAME

Active bindings after ingress (SH01 only):

- `CURRENT:KEYFRAME` → `1f4c26e0-…764b6`
- `CURRENT` alias → same artifact (existing `BindCurrentAsync` behavior)

No prior KEYFRAME existed (0 rows before). No overwrite. LIVE_VERIFIED

`CURRENT:VIDEO_TAKE` was **not** created. The MOTION node showing the KEYFRAME id is the CURRENT alias fallback, not a Veo conversion. FACT / LIVE_VERIFIED

---

## 12. AI SELECTION

Before and after, graph remains:

```
aiSelection.status = ACTIVE
aiSelection.selectionMode = EXPLICIT
aiSelection.providerId = wan
aiSelection.modelId = wan-2.1
```

LIVE_VERIFIED

There is **no** Production AI Selection persist API. CREATE_VIDEO / GET `/actions` accept `providerId`/`modelId` as a **claim**, not durable OS selection. FACT

This phase:

- did not map wan → runway
- did not rewrite `graph_json.aiSelection`
- did not patch the router
- did not use localStorage engine

STOP on §17. Old selection wan/wan-2.1. New selection runway/gen4_turbo was **not** persisted. FACT

A claim `?providerId=runway&modelId=gen4_turbo` still evaluates `MISSING_TIMING` first. LIVE_VERIFIED

---

## 13. PRODUCTION INGRESS REQUEST/TASK

Existing action: **CREATE_IMAGE** (closest to PREPARE_PRODUCTION_SHOT). No second workflow.

| Field | Value | Class |
|---|---|---|
| action | CREATE_IMAGE | LIVE_VERIFIED |
| provider/model | import / existing-pixels | LIVE_VERIFIED |
| EP01 tasks after | 1 | LIVE_VERIFIED |
| EP01 active bindings | 2 | LIVE_VERIFIED |
| snapshot | Story + ActingBeat + VisualContract + provenance | LIVE_VERIFIED |

---

## 14. CREATE_VIDEO READINESS

GET `/api/content/production/shots/{EP01}/EP01-SC01-SH01/state` and `/actions` after ingress and after API restart:

| Dependency | Result | Class |
|---|---|---|
| Story | AVAILABLE | LIVE_VERIFIED |
| ActingBeat | AVAILABLE | LIVE_VERIFIED |
| VisualContract | present on OS snapshot | LIVE_VERIFIED |
| PerformanceSnapshot | not required on official film; not frozen | FACT |
| CURRENT:KEYFRAME | valid | LIVE_VERIFIED |
| Provider persisted | not runway (fixture default on evaluate; graph still wan) | LIVE_VERIFIED |
| Cost guard | not executed | FACT |
| Paid permit | not granted | FACT |
| CREATE_VIDEO.available | **false** | LIVE_VERIFIED |
| Blocker | **MISSING_TIMING** | LIVE_VERIFIED |

CREATE_VIDEO did not call Runway. LIVE_VERIFIED

---

## 15. LEGACY TAKE SAFETY

Veo take URL unchanged on graph. Not deleted, not converted to ProductionArtifact, not bound as CURRENT:VIDEO_TAKE, not used as CREATE_VIDEO input. LIVE_VERIFIED

---

## 16. RELOAD

API stopped and started. GET `/state` and `/actions` again:

- Story AVAILABLE
- Acting AVAILABLE
- KEYFRAME CURRENT `1f4c26e0-…764b6`
- CREATE_VIDEO `MISSING_TIMING`
- Runway audit 0

LIVE_VERIFIED

---

## 17. EP01 SAFETY

| Capture | Before | After | Class |
|---|---|---|---|
| `series_build.updated_at` | 2026-09-18 23:01:02.129634+07 | same | LIVE_VERIFIED |
| graph_json md5 | `415fedf306ed23d1ace94a794400b440` | same | LIVE_VERIFIED |
| production tasks | 0 | 1 (CREATE_IMAGE SH01) | LIVE_VERIFIED |
| active bindings | 0 | 2 (SH01 only) | LIVE_VERIFIED |
| takeUrl | Veo URL | same | LIVE_VERIFIED |
| aiSelection | wan/wan-2.1 EXPLICIT | same | LIVE_VERIFIED |
| SH02+ KEYFRAME | missing | missing | LIVE_VERIFIED |

Graph was not rewritten. LIVE_VERIFIED

---

## 18. EP99 SAFETY

| Capture | Before | After | Class |
|---|---|---|---|
| `updated_at` | 2026-09-07 16:52:54.467199+07 | same | LIVE_VERIFIED |
| graph md5 | `c7cd2516627332c2648c1509395e4a64` | same | LIVE_VERIFIED |
| tasks | 0 | 0 | LIVE_VERIFIED |
| bindings | 0 | 0 | LIVE_VERIFIED |
| GET SH01 KEYFRAME | MISSING | MISSING | LIVE_VERIFIED |

---

## 19. TEST MATRIX

C# `ProductionEp01Sh01ProductionIngressV1Regression` FAIL=0. Client smoke PASS.

| ID | Result | Class |
|---|---|---|
| T01 correct EP01 build | PASS | TESTED / LIVE_VERIFIED |
| T02 correct SH01 | PASS | TESTED / LIVE_VERIFIED |
| T03 Story source located | PASS | LIVE_VERIFIED |
| T04 Story materialized | PASS | TESTED / LIVE_VERIFIED |
| T05 Story provenance | PASS | TESTED |
| T06 ActingBeat source located | PASS | LIVE_VERIFIED |
| T07 ActingBeat materialized | PASS | TESTED / LIVE_VERIFIED |
| T08 ActingBeat provenance | PASS | TESTED |
| T09 VisualContract source | PASS | LIVE_VERIFIED |
| T10 VisualContract materialized | PASS | TESTED / LIVE_VERIFIED |
| T11 no VA change | PASS | TESTED |
| T12 Performance source | no approved non-Runway timing | LIVE_VERIFIED |
| T13 PerformanceSnapshot | not frozen; official film forbidden | TESTED |
| T14 snapshot immutable | n/a — not created | TESTED |
| T15 exact KF located | PASS | LIVE_VERIFIED |
| T16 readable | PASS | LIVE_VERIFIED |
| T17 physical SHA | PASS | LIVE_VERIFIED |
| T18 pictureRevisionId | PASS | LIVE_VERIFIED |
| T19 KEYFRAME artifact | PASS | LIVE_VERIFIED |
| T20 VERIFIED | PASS | LIVE_VERIFIED |
| T21 CURRENT:KEYFRAME | PASS | LIVE_VERIFIED |
| T22 graph KF metadata unchanged | PASS | LIVE_VERIFIED |
| T23 Veo take unchanged | PASS | LIVE_VERIFIED |
| T24 explicit selection changed | **FAIL / STOP** — no API | LIVE_VERIFIED |
| T25 provider=runway | **FAIL / STOP** — not persisted | LIVE_VERIFIED |
| T26 model=gen4_turbo | **FAIL / STOP** — not persisted | LIVE_VERIFIED |
| T27 no silent fallback | PASS | TESTED / LIVE_VERIFIED |
| T28 no provider call | PASS | LIVE_VERIFIED |
| T29 no image generation | PASS | LIVE_VERIFIED |
| T30 no video generation | PASS | LIVE_VERIFIED |
| T31 readiness evaluated | PASS | LIVE_VERIFIED |
| T32 available=true | **false** `MISSING_TIMING` | LIVE_VERIFIED |
| T33 reload | PASS | LIVE_VERIFIED |
| T34 graph not execution authority | PASS | TESTED / LIVE_VERIFIED |
| T35 cross-shot isolation | PASS SH02 | LIVE_VERIFIED |
| T36 cross-scope isolation | PASS | TESTED |
| T37 EP01 graph unchanged | PASS | LIVE_VERIFIED |
| T38 EP99 unchanged | PASS | LIVE_VERIFIED |

---

## 20. LIVE VERIFICATION

API rebuilt and restarted. Login `KIT_MKT` admin. POST `/api/content/production/keyframes` with recovered pixels + canonical Story/ActingBeat/VisualContract. GET state/actions. Restart API. GET again.

Provider calls: 0. Runway Start: 0. LIVE_VERIFIED

---

## 21. BLOCKERS

1. **MISSING_TIMING** — no approved duration on SH01 that is not Runway-derived. LIVE_VERIFIED
2. **No Production AI Selection API** — cannot persist `runway/gen4_turbo` without rewriting graph (forbidden). FACT
3. **OFFICIAL_FILM_FORBIDDEN** on PerformanceSnapshot freeze. FACT
4. After Timing exists, paid CREATE_VIDEO on official film is still `OFFICIAL_FILM_FORBIDDEN` and the one-shot permit is TEST-only. FACT

---

## 22. REMAINING GAPS

- Durable Production AI selection store (explicit director update, not graph rewrite, not router patch). RECOMMENDED
- Approved Timing source that is not Runway diagnostics. RECOMMENDED
- Policy decision if official-film PerformanceSnapshot freeze is allowed for REAL FILM RUN V2. RECOMMENDED
- CURRENT alias on KEYFRAME makes MOTION node display the keyframe id; typed `CURRENT:VIDEO_TAKE` is still empty. FACT

---

## 23. NEXT STEP

**FAMIXA REAL FILM RUN V2 — EP01 SH01 REAL VIDEO** is **not** ready.

Required before one paid Runway Start:

1. Explicit authorized selection `runway / gen4_turbo` (new persist API or user-authorized graph write — this phase forbids both silent map and router patch).
2. Canonical Timing / PerformanceSnapshot without inventing `5s` from Runway.
3. Paid official-film permit policy for that one Start.

Until then: CREATE_VIDEO stays failed closed. Do not call Runway.

---

## 24. STOP

CREATE_VIDEO.available is **false** (`MISSING_TIMING`). Canonical Story / ActingBeat / VisualContract / KEYFRAME / CURRENT:KEYFRAME persist across reload.

**Do not call Runway. Do not create VIDEO. Do not call Gemini. Do not continue to REAL FILM RUN.**

Live IDs:

- KEYFRAME `1f4c26e0-35aa-4395-94e7-09d2b1b764b6`
- SHA256 `7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9`
- storage `os-kf/8f9548b1b0744eb1a5b9295889960b9c`
