# FAMIXA_SH01_LIPSYNC_V2_VALIDATED_REAL_RUN_V2_REPORT

## FINAL QUALITY CLASSIFICATION

**PARTIAL**

Frozen Motion 55 + locked Voice `vpa-20260915T161911Z-e0b3093f` were the actual Fal inputs. A new LipSync file exists. Visual review shows **Minh** (boy, left) receiving the mouth motion, not **Linh / CHAR-003**. Audio is present but trimmed to the frozen 4.000s take clock (source voice 4.24s). Experimental artifact only — not accepted.

STOP. No second Fal run.

---

## 1. PREFLIGHT

Live KIT_MKT studio, build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, shot `EP01-SC01-SH01`.

| Check | Result | Evidence |
|---|---|---|
| A. Frozen Motion artifact = `mot:EP01-SC01-SH01:55` | **PASS** | Freeze used `mot:EP01-SC01-SH01:55`. `runwayAttempts[n=55]` `SUCCEEDED` / `CURRENT`. |
| B. Frozen Voice artifact = `vpa-20260915T161911Z-e0b3093f` | **PASS** | Frozen `voiceArtifactId` set to the locked VPA. Graph `voiceAssets[line]` still lacks `artifactId` (hydrate wipe); freeze did **not** use `lineId` as artifactId. |
| C. Motion physical bytes resolve | **PASS** | `App_Data/kit-video-i2v/veo/d006770ca0f75a00fb8442567cad5471.mp4` = SHA256(take-55 taskId)[0:32]. 541721 B, 4.000s, 1280×720, 24fps, mute. SHA256 `834C116A2DDFF1E6DAA09D705D5BCA2A7E6E902B3058AE5395286AA075B9BA50`. |
| D. Voice physical bytes resolve | **PASS** | IDB `kit-famixa-tts`/`audio` key `FAMIXA:voice:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5` → 69007 B. Disk recovery copy matches. |
| E. Voice SHA | **PASS** | `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C` |
| F. Motion duration from frozen input | **PASS** | `performanceDurationSec = frozen.motionDurationSec = 4.000`. Not `performanceDurationOf()`, not attempt-row `duration=5`, not take 56. |
| G. Provider request from frozen input only | **PASS** | `videoUrl` + `takeTaskId` = take 55 only. Audio = IDB locked VPA bytes. `acceptedTake` 56 / `turboTaskId` 56 / `lastSuccessMotionTaskId` unused. |

First HTTP POST on the stale API process returned 400 `Take khớp môi phải là HTTPS hoặc data video` **before Fal** (old allowlist; current source accepts stored Veo). API rebuilt to current `IsStoredTake`. That 400 is not a Fal job. The subsequent POST is the only Fal submission.

---

## 2. FROZEN MOTION IDENTITY

| Field | Value |
|---|---|
| artifactId | `mot:EP01-SC01-SH01:55` |
| n | 55 |
| resultClass | CURRENT |
| taskId | `veo_…operations/ae932ef6-91b1-46bf-be32-a8792e268447` (base64 Vertex path) |
| FileId | `d006770ca0f75a00fb8442567cad5471` |
| url | `/api/content/series/veo-video?taskId=<take55>` |
| duration | **4.000s** (physical + freeze) |
| pixels | 1280×720 · 24fps · 96 frames · mute |

Not used: take 56, `acceptedTake`, `lastSuccessMotionTaskId`, `turboTaskId`, `resolveTakeUrl()`, `getContentSeriesTurbo()`.

---

## 3. FROZEN VOICE IDENTITY

| Field | Value |
|---|---|
| lineId | `line-SC01-CHAR-003-1` |
| speaker | CHAR-003 / Linh |
| voiceId | `kOIUKJ9E0DPndqTtvJm5` |
| artifactId | `vpa-20260915T161911Z-e0b3093f` |
| storageKey | `FAMIXA:voice:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5` |
| bytes | 69007 |
| duration | 4.24s (source) |
| SHA256 | `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C` |
| dialogue | Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia? |

Not used: `FAMIXA:tts:`, session audio, lineId-as-artifactId, latest/current voice, character fallback.

---

## 4. ACTUAL PROVIDER REQUEST PROVENANCE

Logged immediately before the Fal POST (no keys, no signed URLs):

| Field | Submitted |
|---|---|
| motionArtifactId | `mot:EP01-SC01-SH01:55` |
| motionTaskId | take 55 `veo_…ae932ef6-…` |
| motionPhysicalSource | `App_Data/kit-video-i2v/veo/d006770ca0f75a00fb8442567cad5471.mp4` |
| motionDurationSec | 4.000 |
| voiceArtifactId | `vpa-20260915T161911Z-e0b3093f` |
| voiceId | `kOIUKJ9E0DPndqTtvJm5` |
| voicePhysicalSource | canonical `FAMIXA:voice:…` IDB key |
| voiceByteLength | 69007 |
| voiceSHA256 | `E0B3093F…A1001C` |
| voiceDurationSec | 4.24 |
| providerId | fal |
| modelId | `fal-ai/sync-lipsync/v3` |
| syncMode | `silence` (production default) |
| performanceDurationSec | 4.000 (= frozen motion) |
| speakerPin | **false** (validated video+audio payload; no ASD / no coordinates) |

Server `MergeLipsyncVoices` with `performanceDurationSec=4` atrims audio to 4.000s.

---

## 5. FAL TASK ID

`lipsync_v3_01a0a8d3-992d-7973-8f43-e4b8a63ccccd`

New execution. Not `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f`.

Submit HTTP 200, model `sync-lipsync-v3`, turbo `PENDING` at queue accept.

GET turbo poll then reported PENDING with `KIT hỏi sai URL (405)` — known v3 status-path issue. **No second submit.** Physical file for this taskId appeared on disk.

---

## 6. OUTPUT ARTIFACT

| Field | Value |
|---|---|
| taskId | `lipsync_v3_01a0a8d3-992d-7973-8f43-e4b8a63ccccd` |
| artifact / FileId | `16940abfecb603352d2a60e550602b09` = SHA256(taskId)[0:32] |
| path | `src/KitPlatform.Api/App_Data/kit-video-i2v/fal/16940abfecb603352d2a60e550602b09.mp4` |
| mtime | 2026-09-16 13:08:50 |
| experimental | yes — not bound as current LipSync |

---

## 7. PHYSICAL FILE EVIDENCE

| Probe | Value |
|---|---|
| byteLength | 2894490 |
| SHA256 | `5D1EEF2F2914C0F1E6D7E01F428C2A1728136B72EAB1CD5BCB7D8579595B6D14` |
| duration | 4.000s |
| video | h264 · 1280×720 · 24fps · 96 frames |
| audio | aac LC · 48000 Hz · stereo · 4.000s |
| PSNR vs Take 55 | **average 39.69** (y 38.16) |
| PSNR vs Take 56 | **average 24.30** (y 22.59) |

Motion input is Take 55, not Take 56.

---

## 8. AUDIO EVIDENCE

Not “audio exists” only.

| Check | Result |
|---|---|
| Speech present | **Yes** — almost continuous energy across 4.000s |
| Output audio duration | 4.000s (frozen take clock) |
| Source voice duration | 4.24s → last ~0.24s **clipped** by `atrim=0:4` |
| Silence (−30 dB, d=0.15s) | gap **1.110–1.406s** (0.296s). No leading/trailing silence at this threshold |
| Expected line | Locked VPA bytes were the POST body (SHA match). ASR of the 4.000s render was not run. Last words of «…thế kia?» are likely cut |
| Channels / rate | aac stereo 48 kHz |

---

## 9. VISUAL LIP-SYNC ASSESSMENT

Reviewed extracted frames at 0.20 / 0.80 / 1.40 / 2.00 / 2.60 / 3.20 / 3.70s (`.tmp/sh01-lipsync-v2-validated-real-run-v2/`). Target speaker: **Linh / CHAR-003** (adult, right). Minh = boy, left.

| Check | Result |
|---|---|
| Mouth follows speech (Linh) | **Weak / no** — Linh stays near-closed, close to Take 55 |
| Open/close at speech | **Minh** opens at 0.80, 1.40, 2.00, 3.20, 3.70 |
| Phoneme timing on Linh | **Not demonstrated** |
| Linh visually stable | **Yes** — no identity drift, no face melt |
| Minh not incorrectly lip-synced | **FAIL** — Minh is the face that moves |
| Face deformation / mouth artifacts | None seen |
| Identity drift | None seen |

Validated payload was video+audio only (`speakerPin=false`). Fal v3 without pin attached motion to Minh.

---

## 10. COMPARISON WITH TAKE 55

Same blocking, wardrobe, room, 4.000s / 96 frames. High PSNR to Take 55 proves the **same** mute Veo take was uploaded.

Visible delta is concentrated on **Minh’s mouth**, not Linh’s. Linh’s expression tracks Take 55 more than the dialogue.

---

## 11. CORRECT SPEAKER MODIFIED?

**No.**

Required: Linh / CHAR-003. Observed: Minh’s mouth is the one opening with speech. Linh is not the modified speaker.

---

## 12. QUALITY CLASSIFICATION

**PARTIAL**

| Axis | Grade |
|---|---|
| Frozen input enforcement | PASS |
| New Fal output physical | PASS |
| Audio = locked VPA (trimmed) | PARTIAL |
| Linh lip-sync | FAIL |
| Wrong-speaker (Minh) | FAIL |
| Stability / no artifacts | PASS |

Not PASS (wrong speaker + clipped tail). Not FAIL on the run itself (correct inputs, real new file).

---

## 13. PROVIDER CALL COUNTS

| Provider | Count |
|---|---|
| Fal | **1** (`01a0a8d3-…`) |
| ElevenLabs | 0 |
| Veo | 0 |
| Runway | 0 |
| new Motion | 0 |
| new Voice | 0 |
| Final assembly | 0 |
| Keyframe | 0 |

One pre-Fal HTTP 400 on the stale API process (0 Fal jobs). No retry after the successful Fal accept.

---

## 14. PRODUCTION STATE CHANGES

| Field | After run |
|---|---|
| `videoApproved` | **unchanged** `true` |
| `acceptedTake` | **unchanged** n=56 |
| Motion | **unchanged** bind `mot:…:55` |
| Voice graph `artifactId` | **still null** (not rewritten) |
| Current `lipsyncTaskId` | **unchanged** `lipsync_v3_01a0a327-…` |
| Historical LipSync file | **kept** `06a032f4….mp4` |
| Final | **not assembled / not replaced** |
| New file | experimental only under `App_Data/kit-video-i2v/fal/16940abfecb603352d2a60e550602b09.mp4` |

---

## 15. REMAINING UNCERTAINTY

1. Turbo poll still said PENDING/405 after the mp4 landed. FileId↔taskId match is the persist proof; official COMPLETED JSON was not captured.
2. Graph still has no `voiceAssets.artifactId`; freeze used the locked VPA explicitly. Hydrate can wipe the pointer again.
3. Attempt row `duration=5` is a Runway leftover. This run froze physical **4.000s**. That clip of the 4.24s line is expected under Enforcement HOW.
4. No speaker pin / no ASD — expected for the locked payload; it is also why Minh was synced.
5. No ASR: line identity is byte-SHA of the request, not a transcript of the rendered AAC.
6. Frames are stills. Continuous playback would show the same Minh-open pattern more clearly; it would not make Linh the speaker.

---

**STOP. NO SECOND RUN.**
