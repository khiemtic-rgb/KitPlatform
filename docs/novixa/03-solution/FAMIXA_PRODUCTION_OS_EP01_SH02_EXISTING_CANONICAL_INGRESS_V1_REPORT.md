# FAMIXA PRODUCTION OS — EP01 SH02 EXISTING CANONICAL INGRESS V1 REPORT

Date: 2026-09-22  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH02`  
Safety: REAL AI = 0. No CREATE_VIDEO. No UI change. No CREATE_VIDEO gate change. Graph not rewritten.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED.

---

## 1. EXECUTIVE VERDICT

**PASS**

Existing SH02 Keyframe pixels were recovered and entered Production OS by the same SH01 path:

`POST /api/content/production/keyframes` → CREATE_IMAGE / `import` / `existing-pixels` → KEYFRAME VERIFIED → `CURRENT:KEYFRAME`.

Minimum goal met: **CREATE_VIDEO is no longer `MISSING_KEYFRAME`.**

CREATE_VIDEO was **not** executed.

---

## 2. SH02 BEFORE INGRESS

GET `/api/content/production/shots/4ef164d6-…/EP01-SC01-SH02/actions|state`

| Item | Value | Class |
|---|---|---|
| currentState | EMPTY | LIVE_VERIFIED |
| Production OS rows | 0 requests, 0 bindings | LIVE_VERIFIED |
| KEYFRAME node | MISSING | LIVE_VERIFIED |
| CREATE_IMAGE | available | LIVE_VERIFIED |
| CREATE_VIDEO | available=false | LIVE_VERIFIED |
| CREATE_VIDEO.blockReason | **MISSING_KEYFRAME** | LIVE_VERIFIED |
| missingInputs | ApprovedKeyframe, Story, ActingBeat, VisualContract, Timing, FrozenProviderModel | LIVE_VERIFIED |
| graph `kfApproved` | true | LIVE_VERIFIED |
| graph `keyframeFileName` | `kf-EP01-SC01-SH02-canon.jpg` | LIVE_VERIFIED |
| graph `pictureRevisionId` | `picture:EP01-SC01-SH02:001` | LIVE_VERIFIED |
| graph `kfSourceHash` | `h5778a617:151347` (data-URL hash, not file SHA) | LIVE_VERIFIED |
| graph `keyframeDataUrl` | empty | LIVE_VERIFIED |
| API disk / repo file | absent | LIVE_VERIFIED |
| Cursor IDB `kit-famixa-kf` | only other-build SH05; no official SH02 | LIVE_VERIFIED |

Graph `takeUrl` / `previewUrl` / `videoApproved` were not used as authority. FACT

---

## 3. SH01 CANONICAL PATTERN REUSED

No new action, no new orchestrator.

| Step | SH01 | SH02 |
|---|---|---|
| Recovery source | Chrome Default IDB `kit-famixa-kf` blob | same origin, blob `3/14/1463` |
| Identity check | `dataUriHash` = graph `kfSourceHash` + filename | `h5778a617:151347` + `kf-EP01-SC01-SH02-canon.jpg` |
| API | `POST /api/content/production/keyframes` | same |
| Provider/model | `import` / `existing-pixels` | same |
| Artifact role | KEYFRAME VERIFIED | same |
| Binding | `CURRENT:KEYFRAME` + CURRENT alias | same |
| Story | `runs.{shot}.shotAction` | same field |
| ActingBeat | visualSpec intent + gaze + performance | same fields |
| VisualContract | shot-level visualSpec subset (no VA) | same subset; no invented universe/mode |
| Timing | not invented | not invented (`duration_sec=0`) |
| AI selection | not copied from graph wan | not copied; SH01 `runway/gen4_turbo` not reused |

---

## 4. PHYSICAL KEYFRAME

Recovery source permitted by SH01 ingress: Chrome `User Data\Default` IndexedDB `http_localhost_5173` blob `3/14/1463`. Structured-clone `{ dataUrl, fileName }`.

| Check | Result | Class |
|---|---|---|
| Filename | `kf-EP01-SC01-SH02-canon.jpg` | LIVE_VERIFIED |
| pictureRevisionId | `picture:EP01-SC01-SH02:001` | LIVE_VERIFIED |
| dataUriHash | `h5778a617:151347` exact graph match | LIVE_VERIFIED |
| MIME | image/jpeg (`FF D8`) | LIVE_VERIFIED |
| Bytes | 113493 | LIVE_VERIFIED |
| Dimensions | 1280×720 | LIVE_VERIFIED |
| Physical SHA256 | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` | LIVE_VERIFIED |
| Replacement pixels | no | LIVE_VERIFIED |
| SH01 pixels reused | no (SH01 remains `7a158fa6…783e9` / 93991 B) | LIVE_VERIFIED |

---

## 5. ARTIFACT + BINDING CREATED

| Field | Value | Class |
|---|---|---|
| requestId | `70dab930-1688-4a1c-a8f9-a8efa5b070c3` | LIVE_VERIFIED |
| action | CREATE_IMAGE COMPLETED | LIVE_VERIFIED |
| provider / model | import / existing-pixels | LIVE_VERIFIED |
| artifactId | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` | LIVE_VERIFIED |
| role | KEYFRAME | LIVE_VERIFIED |
| verification | VERIFIED | LIVE_VERIFIED |
| size | 113493 | LIVE_VERIFIED |
| byteSha256 | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` | LIVE_VERIFIED |
| storageKey | `os-kf/5a1325ae8a7b4f389f496e24f483ace7` | LIVE_VERIFIED |
| physical file | `App_Data/kit-video-i2v/os-kf/5a1325ae….mp4` (JPEG bytes, SHA match, FFD8) | LIVE_VERIFIED |
| CURRENT:KEYFRAME | → `91daaf4b-…556cef` active | LIVE_VERIFIED |
| CURRENT alias | same artifact (existing `BindCurrentAsync`) | LIVE_VERIFIED |
| CURRENT:VIDEO_TAKE | not created | LIVE_VERIFIED |

MOTION node displays the KEYFRAME id via the CURRENT alias. That is not a Veo conversion. FACT / LIVE_VERIFIED

---

## 6. OTHER PRODUCTION INPUTS

| Input | Action | Result | Class |
|---|---|---|---|
| Story | Reused SH01 field map: `runs.EP01-SC01-SH02.shotAction` | AVAILABLE on OS snapshot | LIVE_VERIFIED |
| ActingBeat | Reused SH01 concat: intent + gaze + performance | AVAILABLE | LIVE_VERIFIED |
| VisualContract | Reused SH01 shot-level subset from existing visualSpec (40mm, MEDIUM, CHAR-001/CHAR-003). No Master/DNA/PRP/CRP/VA | frozen on CREATE_IMAGE snapshot | LIVE_VERIFIED |
| Timing | No SH02 director timing. `ProductionDirectorTiming.Ep01Sh01DurationSec` is SH01-only. Not copied. | still missing | FACT / LIVE_VERIFIED |
| FrozenProviderModel | Production AI Selection API exists. SH01 has `runway/gen4_turbo`. Graph SH02 is wan/wan-2.1 EXPLICIT. Neither copied. | 0 SH02 selection rows | FACT / LIVE_VERIFIED |
| PerformanceSnapshot | Official-film freeze still `OFFICIAL_FILM_FORBIDDEN`. CREATE_VIDEO does not currently require it. | not frozen | FACT |

Provenance on snapshot: `LEGACY_INGRESS` + `picture:EP01-SC01-SH02:001`. LIVE_VERIFIED

---

## 7. ACTIONS AFTER INGRESS

GET `/actions` SH02:

| Action | Available | Block |
|---|---|---|
| CREATE_IMAGE | true | — |
| **CREATE_VIDEO** | **false** | **TIMING_PRODUCTION_DECISION_REQUIRED** |
| CREATE_VOICE | false | MISSING_PERFORMANCE_SNAPSHOT |
| CREATE_LIPSYNC | false | MISSING_VIDEO_ARTIFACT |
| MIX_VIDEO | false | MISSING_LIPSYNC_ARTIFACT |
| ACCEPT | true (CURRENT alias) | — |
| REGENERATE | true | — |
| ASSEMBLE | false | MISSING_DEPENDENCY |

CREATE_VIDEO.missingInputs after ingress: **`Timing`, `FrozenProviderModel`**.  
`ApprovedKeyframe` / `Story` / `ActingBeat` / `VisualContract` are satisfied. LIVE_VERIFIED

GET `/state`: STORY AVAILABLE, ACTING AVAILABLE, KEYFRAME CURRENT `91daaf4b-…`. LIVE_VERIFIED

---

## 8. REMAINING CANONICAL BLOCKERS

Do not loosen the gate.

1. **TIMING_PRODUCTION_DECISION_REQUIRED** — SH02 has no director timing (`seconds`/`durationSec` empty; SH01 `5.0s` is not a SH02 source). FACT / LIVE_VERIFIED
2. **FrozenProviderModel / MISSING_PRODUCTION_AI_SELECTION** — no `production_ai_selection` row for SH02 VIDEO. Copying graph `wan/wan-2.1` or SH01 `runway/gen4_turbo` would invent a selection. FACT / LIVE_VERIFIED
3. After those exist, official-film paid CREATE_VIDEO still needs an issued permit. FACT

---

## 9. SAFETY

| Rule | Result | Class |
|---|---|---|
| Gemini / Veo / Runway / Fal / ElevenLabs calls | 0 | LIVE_VERIFIED |
| New pixels generated | 0 | LIVE_VERIFIED |
| CREATE_VIDEO executed | no | LIVE_VERIFIED |
| Gate loosened | no | FACT |
| takeUrl / previewUrl / acceptedTake / videoApproved used as authority | no | LIVE_VERIFIED |
| EP01 graph md5 | `415fedf306ed23d1ace94a794400b440` unchanged | LIVE_VERIFIED |
| EP01 `updated_at` | `2026-09-22 00:18:06.907544+07` unchanged | LIVE_VERIFIED |
| EP99 graph md5 | `c7cd2516627332c2648c1509395e4a64` unchanged | LIVE_VERIFIED |
| SH01 CURRENT:KEYFRAME | still `1f4c26e0-…764b6` | LIVE_VERIFIED |
| SH01 physical SHA | still `7a158fa6…783e9` | LIVE_VERIFIED |
| UI | not modified | FACT |

---

## 10. TEST

- Client smoke `famixa-production-os-ep01-sh02-existing-canonical-ingress.smoke.ts` PASS
- C# `ProductionEp01Sh02ExistingCanonicalIngressV1Regression` registered next to SH01 ingress suite; Application project builds

---

## 11. STOP

CREATE_VIDEO.available is **false**. Blocker is **TIMING_PRODUCTION_DECISION_REQUIRED** (then FrozenProviderModel). Keyframe gap is closed.

**Do not call Veo / Runway / Gemini. Do not execute CREATE_VIDEO. Do not invent SH02 timing or selection.**

Live IDs:

- KEYFRAME `91daaf4b-5550-4cbc-b9f9-0bd0be556cef`
- SHA256 `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57`
- storage `os-kf/5a1325ae8a7b4f389f496e24f483ace7`
