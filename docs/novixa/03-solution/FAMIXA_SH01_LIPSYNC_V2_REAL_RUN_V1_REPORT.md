# FAMIXA_SH01_LIPSYNC_V2_REAL_RUN_V1_REPORT

## FINAL VERDICT
**BLOCKED**

Preflight B + F failed on the current production graph. Fal was not called.

---

## 1. PREFLIGHT

Live graph read from the open KIT_MKT studio tab (`/content/videos`, build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, shot `EP01-SC01-SH01`). Physical stores read from browser IDB `kit-famixa-tts` / `audio` and the API take-55 file.

| Check | Result | Evidence |
|---|---|---|
| A. Motion source → Take 55 | **PASS** | `runwayAttempts[n=55]` `SUCCEEDED` / `resultClass=CURRENT` / model `veo-3.1`. Artifact `mot:EP01-SC01-SH01:55` still on the old freeze row. Take 56 exists as `acceptedTake.n=56` and was not used as motion source. `lastSuccessMotionTaskId` is null. |
| B. Voice source → `voiceAssets[lineId].artifactId` | **FAIL** | `voiceAssets['line-SC01-CHAR-003-1']` keys are only `lineId`, `status`, `duration`, `characterId`. `artifactId`, `storageKey`, `voiceId`, `physicalArtifactResolvable` are absent. V2 `voicePhysicalArtifactIdFromGraph` therefore returns undefined. |
| C. Frozen Voice identity → VoicePhysicalStore | **FAIL (graph path)** | Canonical IDB row exists (see D). Graph cannot name `vpa-20260915T161911Z-e0b3093f`, so `resolveAuthoritativeLipSyncVoiceSource` cannot freeze that identity. CHAR-003 `voiceId` is still `kOIUKJ9E0DPndqTtvJm5` on the character record, not on the voice asset. |
| D. Physical Voice bytes readable | **PASS** | IDB key `FAMIXA:voice:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5` → Blob `audio/mpeg` **69007 B**, SHA256 `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C`. No `FAMIXA:tts:` keys in this DB. |
| E. Physical Motion bytes readable | **PASS** | `App_Data/kit-video-i2v/veo/d006770ca0f75a00fb8442567cad5471.mp4` exists, **541721 B**, duration **4.000 s**, h264 1280×720, **no audio stream**, SHA256 `834C116A2DDFF1E6DAA09D705D5BCA2A7E6E902B3058AE5395286AA075B9BA50`. Video-only is expected for Veo LipSync input. |
| F. Frozen input contains actual artifact identities | **FAIL** | Current `lipsyncFrozenInput` is the **old** job freeze: `motionArtifactId`, `motionFingerprint`, `voiceFingerprint`, `fingerprint` only. Missing `voiceArtifactId`, `voiceId`, `voiceDurationSec`, `motionDurationSec`. |

Stop rule: any preflight fail → do not call Fal.

Secondary gates observed (not reached because of B/F):

- Desk hides **Khớp môi** while LipSync artifact is CURRENT (`lipsynced=true`, task `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f`). **Khớp môi mới** is only shown for dead Fal output.
- `startLipsync` `resolveLineAudio` still requires `FAMIXA:tts:` / session blobs. This IDB has **zero** `FAMIXA:tts:` keys. Nạp thoại would open an ElevenLabs confirm if hydrate misses — not clicked.
- A non-`forceNew` desk click would hit `lipsyncResumeTaskId` and **resume** `01a0a327…` (forbidden).

---

## 2. FROZEN INPUT

**Not created.** No new V2 freeze was written.

Would-have-been required fields vs live graph:

| Field | Required | Live |
|---|---|---|
| motionArtifactId | `mot:EP01-SC01-SH01:55` | present only on **old** freeze |
| motion fingerprint | frozen at send | old `f64e90d` (not a new freeze) |
| motion duration | from Take 55 | take row says `5`; physical file is `4.000 s` — not frozen this run |
| voiceArtifactId | `vpa-20260915T161911Z-e0b3093f` | **missing** on `voiceAssets` |
| voiceId | `kOIUKJ9E0DPndqTtvJm5` | on CHAR-003, **not** on voice asset |
| voice fingerprint | frozen at send | old `17dbe460` only |
| voice duration | `4.24` | duration receipt only (`ready` / `4.24`) |

No re-resolve from accepted / latest / lastSuccess was performed.

---

## 3. PROVIDER

Not submitted.

Configured LipSync model on the open build: **`v3`** (Fal). Unused.

---

## 4. PROVIDER REQUEST ID

None. No Fal job created.

Historical task still on the shot (untouched): `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f`.

---

## 5. OUTPUT ARTIFACT

None. No new LipSync artifact.

---

## 6. PHYSICAL FILE

No new Fal MP4.

Locked **input** files only:

- Voice: IDB canonical key above, 69007 B, `audio/mpeg`
- Motion: `d006770ca0f75a00fb8442567cad5471.mp4`, 541721 B

---

## 7. VIDEO/AUDIO FORENSIC

No new output to inspect.

Locked Motion (input) only:

| Field | Value |
|---|---|
| video stream | yes, h264 1280×720 |
| audio stream | **none** |
| duration | 4.000 s |
| readable | yes |

No loudness / output codec / output fingerprint — there is no new LipSync file.

---

## 8. ACTUAL VISUAL LIPSYNC ASSESSMENT

**Not performed.** No new video was produced. The desk still plays the previous Fal LipSync (`01a0a327…`). That file was not re-graded as this run’s result.

Classification: **UNKNOWN** (no new output).

---

## 9. PRODUCTION STATE CHANGES

| Field | Before | After |
|---|---|---|
| videoApproved | true | **unchanged** |
| acceptedTake | 56 | **unchanged** |
| Final | existing (desk: file missing on this machine) | **not assembled / not replaced** |
| Motion | Take 55 CURRENT; Take 56 accepted LATE history | **unchanged** |
| Voice graph | duration-only ready | **unchanged** |
| lipsyncTaskId | `lipsync_v3_01a0a327-…` | **unchanged** |
| lipsynced | true | **unchanged** |
| lipsyncFrozenInput | old 4-field freeze | **unchanged** |

---

## 10. CALL COUNTS

| Provider / action | Count |
|---|---|
| Fal | **0** |
| ElevenLabs | **0** |
| Runway | **0** |
| Veo | **0** |
| New Motion | **0** |
| New Voice | **0** |
| Keyframe generation | **0** |
| Final assembly | **0** |

---

## 11. FINAL VERDICT

**BLOCKED**

Reason: current production `voiceAssets[line-SC01-CHAR-003-1]` does not carry `artifactId = vpa-20260915T161911Z-e0b3093f`. V2 freeze is defined to refuse a missing / lineId artifact. Physical Voice bytes are still in VoicePhysicalStore; they are not graph-bound on this session.

Recovery (`FAMIXA_SH01_VOICE_RECOVERY_V1`) previously wrote that artifactId. It is not present on the live React graph now. This report does not restore it and does not submit Fal.

Stopped after report. No second job. No post-run code fix.
