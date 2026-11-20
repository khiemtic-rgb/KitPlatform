# FAMIXA_SH04_TAKE4_LINEAGE_VERIFY_V1_REPORT

**Suite:** `FAMIXA_SH04_TAKE4_LINEAGE_VERIFY_V1`  
**Date:** 2026-09-14  
**Mode:** READ-ONLY. No generate, retry, approve, preview replace, Keyframe / Shot WHAT / Timing edits.  
**Provider calls:** 0. **New media:** 0. **Production mutations:** 0. **Approval mutations:** 0.

---

## 1. VERDICT

**LINEAGE_VERIFIED**

The currently stored **Video · take 4** on this build/shot is frozen to **this build’s SH04 Keyframe**, not another shot’s still.

| Required source | Stored proof | Match |
|---|---|---|
| `buildId` = `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | `pack_content.series_build.id` **and** `graph_json.buildId` | YES |
| `shotId` = `EP01-SC01-SH04` | Run key + `attemptId` + `pictureRevisionId` + `keyframeArtifactId` | YES |
| `kfFp` = `145a711f` | Run stamp + live WHAT recompute + take 4 `frozenInput.baseMotionFingerprint` | YES |

**Provider / model are not the expected Veo pin.**

| Field | Expected | Actual |
|---|---|---|
| `ProviderId` | `google` | **`wan`** |
| `ModelId` | `veo-3.1` | **`wan-2.1`** |

Do not treat take 1–2 (Runway `gen4_turbo`) or Kit Video `video_runway_take` SH01-01 rows as this take.

---

## 2. TARGET

| Field | Value |
|---|---|
| buildId | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| series | `FAMIXA` |
| episode | `EP01` / Tập 01 |
| shotId | `EP01-SC01-SH04` |
| visible take | Take 4 |
| SoT row | `pack_content.series_build` (`status=in_prod`, `updated_at=2026-09-14 23:29:20 +07`) |
| Pilot mirror | `pack_content.series_pilot` `FAMIXA` — same take 4 task, URL, `kfFp`, pixel hash |

---

## 3. ANSWERS (15 fields)

| # | Field | Value |
|---|---|---|
| 1 | VideoTake artifact ID | `mot:EP01-SC01-SH04:4` (`runwayAttempts[n=4].attemptId`) |
| 2 | ProviderId | **`wan`** (`selectionSnapshot.providerId`, `frozenInput.provider`, `graph.aiSelection.providerId`) |
| 3 | ModelId | **`wan-2.1`** (`attempt.model`, `selectionSnapshot.modelId`, `graph.aiSelection.modelId`, run `model`) |
| 4 | ProviderRequestId | `wan_01a08bec-02f4-7730-bc86-688c9bde5ff8` (also run `turboTaskId`) |
| 5 | Source Keyframe revision / fingerprint | revision `picture:EP01-SC01-SH04:001`; pixel `h40d3cef4:152595`; production `kfFp=145a711f`; artifact `pic:EP01-SC01-SH04:h40d3cef4:152595` |
| 6 | Source Keyframe buildId | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| 7 | Source Keyframe shotId | `EP01-SC01-SH04` |
| 8 | PromptHash / Motion Intent hash | prompt `h9b472c49:197`; motion / `baseMotionFingerprint` `bf3e0303`; acting `333b8771`; execution `aedcbda9`; timing `7aa1d348` |
| 9 | Performance duration | **5 s** (last SUCCESS `attempt.duration`; shot `seconds`/`clock` = 5; no `shot.timing` override) |
| 10 | Provider duration | **5 s** (`frozenInput.providerDuration`, `attempt.duration`, `exactRequest.duration`) |
| 11 | Current | **YES** (`resultClass=CURRENT`; live `kfSourceHash` + `pictureRevisionId` match freeze; last SUCCESS; `takeUrl` = this output; `turboTaskId` = this task) |
| 12 | Accepted | **NO** (`acceptedTake` is `null`) |
| 13 | videoApproved | **YES** (run `videoApproved=true`, `videoVerified=true`, `status=reviewed`) — run-level flag, not a per-take field |
| 14 | Exact artifact URL / source | `https://v3b.fal.media/files/b/0aa9e132/cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4` · run `finalSource=RUNWAY` (legacy label; provider is Wan on Fal CDN) |
| 15 | Creation timestamp | `2026-09-10T15:25:08.577Z` |

---

## 4. SOURCE KEYFRAME PROOF

### 4.1 Pixel + revision (on the take)

Take 4 `frozenInput`:

| Field | Value |
|---|---|
| `keyframePixelHash` | `h40d3cef4:152595` |
| `pictureRevisionId` | `picture:EP01-SC01-SH04:001` |
| `keyframeArtifactId` | `pic:EP01-SC01-SH04:h40d3cef4:152595` |

Live SH04 run on the same build:

| Field | Value |
|---|---|
| `kfSourceHash` | `h40d3cef4:152595` |
| `pictureRevisionId` | `picture:EP01-SC01-SH04:001` |
| `kfApproved` | `true` |
| `keyframeFileName` | `kf-EP01-SC01-SH04-canon.jpg` |

Take 4 `kf.hash` and `source.hash` are the same pixel stamp. This is **not** take 1 (`hcfa11618:136975` / `h60e97863:143871`).

### 4.2 `kfFp = 145a711f`

`kfFp` is the production WHAT fingerprint (`keyframeInputFingerprint`), not the pixel hash. The take artifact **does not store `kfFp`**.

Proof used (no substitution of another Keyframe):

1. Same-run stamp: `runs.EP01-SC01-SH04.shotProduction.kfFp = 145a711f`.
2. Live recompute from stored Shot WHAT on this graph (`shotId` + `kf` + story + visual + `motionPromptVi` + `CHAR-001,CHAR-003`) = **`145a711f`**.
3. Take 4 `frozenInput.baseMotionFingerprint = bf3e0303`.  
   `motionInputFingerprint(keyframeFp=145a711f, action=story, editorial=5s)` = **`bf3e0303`**.  
   Empty editorial duration hashes to `b82c9ee4` — **not** this take.

That binds take 4’s frozen motion input to **this shot’s** `kfFp`, on **this** `buildId`.

### 4.3 What was not used

| Source | Why unused |
|---|---|
| `pack_content.video_generation_execution` `3d20f567-…` | FAMIXA / CHAR-001 / RUNWAY / 2026-08-29 — different request, not take 4 |
| `pack_content.video_runway_take` | Two rows, both `shot_code=SH01-01`, Aug 2026 Kit Video golden — **other shot** |

---

## 5. TAKE 4 RECORD (as stored)

```
n: 4
attemptId: mot:EP01-SC01-SH04:4
taskId / ProviderRequestId: wan_01a08bec-02f4-7730-bc86-688c9bde5ff8
at: 2026-09-10T15:25:08.577Z
status: SUCCEEDED
resultClass: CURRENT
model: wan-2.1
selectionMode: EXPLICIT
selection reason: explicit provider selected by Director
decisionId: 401372a2e5854d0e81b482cb2207e41a
outputUrl: https://v3b.fal.media/files/b/0aa9e132/cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4
```

`exactRequest` still says `model: gen4_turbo` / `compiler: RUNWAY_PROMPT_V1`. That is a leftover request-shape stamp. **Authority for who ran this take is `selectionSnapshot` + `frozenInput.provider` + `attempt.model` = wan / wan-2.1.**

Graph `aiSelection` (current desk pin): `{ status: ACTIVE, providerId: wan, modelId: wan-2.1, selectionMode: EXPLICIT }`.

---

## 6. CURRENT / ACCEPTED / APPROVED

Desk “Video · take N” is the last same-picture SUCCESS (`playableMotionTakeOf`). Live pixel + revision match take 4. Take 4 is the last attempt (`n=1..4`). Visible `takeUrl` / `previewUrl` equal take 4’s Fal mp4.

| Question | Answer |
|---|---|
| Is take 4 current? | **YES** |
| Is take 4 explicitly accepted? | **NO** — `acceptedTake` is null |
| Is the run video-approved? | **YES** — `videoApproved=true` |

Lipsync is not on this take (`lipsynced=false`, no `lipsyncUrl`).

---

## 7. SAFETY

| Check | Result |
|---|---|
| Veo / Runway / Wan / Fal POST | 0 |
| Graph / approval / KF / WHAT / Timing writes | 0 |
| Method | `SELECT` on `pack_content.series_build` + `series_pilot` + unused execution tables |

---

## 8. STOP

Report only. No remake, no Veo pin change, no approve/accept write.
