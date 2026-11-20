# FAMIXA DIRECTOR VIDEO MODEL DISPLAY FIX V1

Date: 2026-09-20  
Mode: IMPLEMENT → TEST → LIVE VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: Provider calls = 0. Media generation = 0. UI authority/display only.

---

## 1. VERDICT

**PASS**

Director Shot UI for EP01 SH01 now displays the canonical Production OS video model:

**Model video · Runway · Gen-4 Turbo**

not Google Veo 3. The current VIDEO_TAKE provenance is shown separately:

**Video ✓ Đã tạo · Runway · Gen-4 Turbo · 5.04s**

The model list remains. Changing the next production selection does not mutate the existing VIDEO_TAKE.

STOP. Do not continue to Voice.

---

## 2. Provider calls

**0**

Live Runway HTTP audit after Director open + display: `startCount = 0`, `pollCount = 0`, `downloadCount = 0`, `total = 0`.

---

## 3. Media generation

**0**

No CREATE_VIDEO / keyframe / Voice / LipSync / Mix / Assembly / Final.

---

## 4. Display (live Director)

| Surface | Value |
|---|---|
| Shot | Shot 01-SH01 (`EP01-SC01-SH01`) |
| Label | **Model video** (not Máy dựng) |
| Current selection | **Runway · Gen-4 Turbo** |
| VIDEO_TAKE provenance | **Runway · Gen-4 Turbo · 5.04s** |
| Google Veo 3 as current | **false** |
| Change control | Thay đổi (list kept) |

Authority: GET `ProductionAiSelection` + GET preview duration. Not localStorage, not legacy graph `wan`/`wan-2.1`, not hardcoded Veo.

---

## 5. ProductionAiSelection

| Field | Value |
|---|---|
| selectionId | `9b0dceee-ba53-4cc5-8139-ee9ba58a2011` |
| provider / model | runway / gen4_turbo |
| hash | `SELECTION_HASH_V1:e8ce7e47dc326fc57eccbb61d8681ad845fa6c6d418389a746a3d96993fc31bc` |
| status | ACTIVE |

Unchanged after live verify. No upsert on page load.

---

## 6. VIDEO_TAKE

| Field | Value |
|---|---|
| artifactId | `b22370a9-6ddd-4b04-a281-c7498cc926ae` |
| SHA | `8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296` |
| storageKey | `os/a1673a1e54d344d69031cec3ed6c44b3` |
| duration | 5.042 s (shown 5.04s) |
| MIME | video/mp4 |
| binding | CURRENT:VIDEO_TAKE + VISIBLE |

Unchanged. Preview still resolves from Production OS artifact authority.

---

## 7. Selection vs current video

Changing the picker writes `upsertProductionAiSelection` only. It applies `applyDirectorVideoModelSelection`, which keeps the existing take artifact / provider / duration.

A later CREATE_VIDEO would be a new Task. This phase did not create one.

---

## 8. Reload / live

Director opened EP01 SH01 after admin login. Display matched OS selection and preview duration. Audit remained 0. Selection hash unchanged.

---

## 9. EP01 graph

| | |
|---|---|
| hash | `415fedf306ed23d1ace94a794400b440` |
| unchanged | **true** |

Legacy graph `wan` / `wan-2.1` was not rewritten and does not drive Director display.

---

## 10. EP99

| | |
|---|---|
| hash | `c7cd2516627332c2648c1509395e4a64` |
| unchanged | **true** |

---

## 11. T01–T15

| ID | Result |
|---|---|
| T01 SH01 displays Runway / Gen4 Turbo | PASS |
| T02 Google Veo is not displayed as current model | PASS |
| T03 current VIDEO_TAKE remains unchanged | PASS |
| T04 current VIDEO_TAKE artifact unchanged | PASS |
| T05 ProductionAiSelection unchanged | PASS |
| T06 legacy WAN does not override | PASS |
| T07 no hardcoded Veo default | PASS |
| T08 no provider call | PASS |
| T09 no media generation | PASS |
| T10 changing selection does not regenerate video | PASS |
| T11 changing selection does not mutate existing artifact | PASS |
| T12 reload preserves correct display | PASS |
| T13 cross-shot selection cannot affect SH01 | PASS |
| T14 EP01 graph unchanged | PASS |
| T15 EP99 unchanged | PASS |

Smoke `famixa-director-video-model-display.smoke.ts` FAIL=0.

---

## 12. Safety (untouched)

- Production OS contracts
- Provider adapters
- Router
- Timing / PerformanceSnapshot
- Canon / Visual Authority / Visual Universe
- Voice / LipSync / Mix / Assembly / Final
- EP01 graph content
- EP99

---

## 13. NEXT ACTION

**STOP.**

Director model display is correct. Do not start Voice. Do not redesign the Shot page.
