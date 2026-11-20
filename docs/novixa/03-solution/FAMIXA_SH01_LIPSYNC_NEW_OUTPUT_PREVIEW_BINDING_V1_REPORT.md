# FAMIXA_SH01_LIPSYNC_NEW_OUTPUT_PREVIEW_BINDING_V1_REPORT

## VERDICT
**PASS**

SH01 desk `<video>` now plays the current successful LipSync MP4 (**6.000s**, audio present). No Fal / ElevenLabs / Veo / Runway call. No generate. No Final assemble. `acceptedTake`, Motion, Voice, `videoApproved` unchanged.

---

## 1. New LipSync task

No newer Fal job exists for `EP01-SC01-SH01`.

Search (disk + graph + turbo pointer):

| Candidate | Result |
|---|---|
| `App_Data/kit-video-i2v/fal/*.mp4` | **one file only** |
| `lipsyncExecutionAttempt` | null |
| graph `lipsyncTaskId` | only `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` |
| production-execution / other SH01 takes | older mute takes (5.04s / 4.00s / 6.00s **no audio**) |

The **6.000s LipSync output is the stored Fal file of that same SUCCESS task**. It is not a second historical job. `01a0a327…` stays the current SUCCESS identity. It is treated as historical **only if** a newer CURRENT motion releases the LipSync bind (T7). That release did not happen on live SH01 (take 56 is `LATE_RESULT`).

| Field | Value |
|---|---|
| provider | Fal |
| model | `fal-ai/sync-lipsync/v3` (`sync-lipsync-v3`) |
| providerRequestId / taskId | `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` |
| status | SUCCEEDED |

---

## 2. New artifact

| Field | Value |
|---|---|
| artifactId / FileId | `06a032f474624bca2d0b609572b6d545` = SHA256(taskId)[0:32] |
| lipsyncUrl | `/api/content/series/fal-video?taskId=lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` |
| finalSource | FAL |
| lipsynced | true |
| bound motion | `mot:EP01-SC01-SH01:55` |

No new graph artifact created. Existing SUCCESS pointer reused.

---

## 3. New physical file

`E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v\fal\06a032f474624bca2d0b609572b6d545.mp4`

| Field | Value |
|---|---|
| exists | yes |
| mtime | 2026-09-15 10:43:43 |
| byteLength | **3743506** |
| SHA256 | `55515811DB64741B6E4B3CB3AAA2085BE2D8450B118295ADB3064E661AA676BA` |
| container | mp4 |

---

## 4. Duration

ffprobe format + video stream: **6.000s**. Expected ≈ 6s.

---

## 5. Audio stream

| Field | Value |
|---|---|
| audio stream | yes (index 0) |
| codec | aac |
| sample_rate | 48000 |
| channels | 2 |
| audio duration | **6.000s** |
| video stream | h264 1280×720 6.000s |

Lip-sync quality not scored this phase. File is a real LipSync output (A+V), not mute Motion.

---

## 6. Old artifact currently displayed (before bind)

Desk `<video>` was resolving to **Veo take 55** (and/or a 4s stored Final), not the Fal MP4.

| Field | Value |
|---|---|
| identity | take 55 CURRENT · FileId `d006770ca0f75a00fb8442567cad5471` |
| path | `App_Data/kit-video-i2v/veo/d006770ca0f75a00fb8442567cad5471.mp4` |
| duration | **4.000s** |
| audio | **none** |
| graph leftovers | `takeUrl` / `previewUrl` / `acceptedTake` 56 still point at Veo |

Take 56 LATE file `7e8e90b4…` is 6.000s **video-only** — not what the 4s player was showing.

---

## 7. Root cause of stale preview

UI priority + hydration, not a missing second Fal job.

1. `playableFalLipsyncSrc` treats a bare `/fal-video?taskId=` pointer as **unplayable** until an auth blob exists.
2. `resolveShotPreviewKind` prefers `finalUrl` then `lipsyncUrl` then `takeUrl`.
3. Workspace set `takeUrl = veoPlaySrc \|\| motionUrl` **always**, so Veo 4s was a ready `<video>` src.
4. `playableFinal` (mix/final blob) could also win over LipSync when `mixReady`/`finalReady`.
5. `editorialSourceOf` required `https?://`. The current LipSync URL is a relative fal-video pointer, so cut-edit fell through to **take URL**.
6. Incomplete freeze used to drop `lipsyncPlayableUrl` (`bound ? lip : undefined`), so even a fetched Fal blob could lose to Motion.

Result: after remount, first paint / failed bind showed **4.000s mute Veo** while the 6.000s Fal file was already on disk and graph `lipsyncTaskId` already pointed at it.

---

## 8. Exact binding fix

Minimal preview resolution only. No new artifact. No byte copy. No lineage write.

- `resolveCurrentProductionVideoSource`: proven LipSync blob stays `kind=lipsync` even if freeze is incomplete. If a **newer CURRENT** motion releases the bind, historical LipSync URL is ignored (T7 / T9).
- `resolveCurrentLipsyncPreviewBinding`: when LipSync is current SUCCESS and the Fal file is not missing, **clear `takeUrl` and `finalUrl`**. Do not flash 4s Motion/Final while the Fal blob hydrates.
- Workspace desk uses that bind before `resolveShotPreviewKind`.
- `editorialSourceOf`: current `lipsynced` + fal-video pointer beats take URL.

Files:

- `client/admin/src/modules/content/famixa-lipsync-motion-authority.ts`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionWorkspace.tsx`
- `client/admin/src/modules/content/content-famixa-editorial-cut.ts`

---

## 9. Actual `<video>.currentSrc` after reload

Remount: login KIT_MKT → `/content/videos` → EP01 6-shot → Shot 01 BÀN SHOT.

| Field | Live |
|---|---|
| caption | `Preview Lip-sync` |
| currentSrc | `blob:http://localhost:5173/c37c4f35-ca83-40c4-be9d-fe244be9f814` |
| blob type | `video/mp4` |
| blob size | **3743506** (exact Fal file) |
| readyState | 4 |
| video | 1280×720 |

Not Veo `/veo-video`. Not take 55. Not a 4s Final blob.

---

## 10. Actual displayed duration after reload

`<video>.duration` = **6**

`webkitAudioDecodedByteCount` > 0 (audio present in the playing element).

---

## 11. Tests

| Smoke | Result |
|---|---|
| `famixa-lipsync-new-output-preview-binding.smoke.ts` T1–T10 | **PASS** |
| `famixa-lipsync-bound-source-authority.smoke.ts` T1–T18 + released-bind ignore | **PASS** |
| `content-famixa-editorial-cut.smoke.ts` | **PASS** |
| `famixa-fal-lipsync-playback.smoke.ts` | **PASS** |

---

## 12. Provider calls

| Provider | Calls |
|---|---|
| Fal | **0** |
| ElevenLabs | **0** |
| Veo | **0** |
| Runway | **0** |
| New Motion | **0** |
| New Voice | **0** |
| Final assembly | **0** |
| Keyframe generation | **0** |

Desk only `GET` existing `/api/content/series/fal-video` into a blob (auth playback of the stored file).

---

## 13. Production state changes

| Field | Change |
|---|---|
| acceptedTake | **unchanged** (56 LATE) |
| videoApproved | **unchanged** true |
| Motion / take 55 bind | **unchanged** `mot:EP01-SC01-SH01:55` |
| Voice | **unchanged** |
| lipsyncTaskId / lipsyncUrl | **unchanged** (current SUCCESS pointer) |
| Final / assembleFp | **unchanged** |
| New artifact rows | **0** |

Only current preview resolution/binding.

---

## STOP
