# FAMIXA PRODUCTION OS — CK23B SHOT-SPECIFIC LIPSYNC FACE BOX + ONE CONTROLLED RE-RUN V1

Date: 2026-09-23  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shot: `EP01-SC01-SH02`  
Fal Start: **exactly 1**. No MIX. No CREATE_VIDEO / CREATE_VOICE.

---

## 1. FORENSIC

**Old box source:** fixture, not this take.

`ProductionEp01Sh02LipsyncCanon` previously locked pin-v1 T3 `(0.28, 0.38)` @ 1280×720 frame 0.  
`FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox` then computed:

```
pin = [round(0.28*1280), round(0.38*720)] = [358, 274]
bw = max(140, 1280/8) = 160
bh = max(170, 720/4) = 180
box = [278, 184, 438, 364]
```

That box sat on the empty wall left of CHAR-001. It was **not** measured from VIDEO_TAKE `b44e530b-…`, frame 0, or character geometry.

**Old formula:** pin ± half of `(bw, bh)`. Repeated 192 times. `sync_mode=silence`.

**Verified Fal box format:** **XYXY**.

Current Fal OpenAPI (fetched 2026-09-23) `fal-ai/sync-lipsync/v3`  
`Sync3ActiveSpeakerDetection.bounding_boxes`:

> Per-frame bounding boxes **[x1, y1, x2, y2]**

`FAL_BOX_FORMAT = XYXY`. Execution was allowed.

---

## 2. NEW BOX

| Field | Value |
|---|---|
| Source frame | VIDEO_TAKE `b44e530b-5b59-422f-9b80-e058a3a639b5` frame 0 and t=2.0s (speech window) |
| Coordinate system | Fal XYXY, integer pixels, 1280×720 |
| Exact coordinates | **`[450, 50, 720, 400]`** |
| Pin (freeze only) | `(0.46, 0.31)` = center of that box |
| Why CHAR-001 | Head ≈ x 460–700 / y 40–340; mouth ≈ x 540–620 / y 270–320. Box fully contains the mouth and face. CHAR-003 stays right (x ≥ ~850); box `x2=720` does not include her. |

Overlays: `.tmp/ck23b-face-box-v2/` and quality frames `.tmp/ck23b-rerun/`.

Legacy T3 `(0.28, 0.38)` / `[278,184,438,364]` remains in canon as **historical evidence** (`LegacyT3*`). It is not sent on this run.

---

## 3. NEW FREEZE

| Field | Value |
|---|---|
| Snapshot ID | `1125945c-0d09-47b1-9d4d-872dc29df46b` |
| Snapshot hash | `abb219e317bf0317956e1d901d0e6a1a3a6deb82e695056d344b70afc0232837` |
| ShotContract | `line-SC01-CHAR-001-2\|CHAR-001\|b44e530b5b59422f9b80e058a3a639b5\|0fde86c2250f4303bda04913a6dbd1e4\|0.46\|0.31\|1280\|720\|0\|450\|50\|720\|400` |
| Parent VIDEO | `b44e530b-5b59-422f-9b80-e058a3a639b5` |
| Parent VOICE | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` |
| CharacterId | `CHAR-001` / `line-SC01-CHAR-001-2` |
| Provider / model | `fal` / `sync-lipsync-v3` |
| sync_mode | **`cut_off`** |

Old CK23 snapshot `71b2bc55-fa5b-4dc1-aec8-99441ecb7b0e` (T3, no box suffix) was **not** mutated.

Idempotency (server, official SH02):  
`4ef164d6…:EP01-SC01-SH02:CREATE_LIPSYNC:b44e530b…0fde86c2…+450\|50\|720\|400\|cut_off`  
This cannot replay `ck23-official-sh02-create-lipsync-v1-d`.

---

## 4. EXECUTION

| Field | Value |
|---|---|
| Start count | **1** (`fal-http-audit` 0 → 1; stayed 1 through recover + ACCEPT) |
| Attempt ID | `833e8167-0862-4dbb-ba9d-89a2fc3b9a4e` |
| Task ID | `d9136964-139f-45a2-811d-aa1c022c212b` |
| Request ID | `0efc1189-7288-4f7a-8d5d-621b8e7197d4` |
| Provider job ID | `lipsync_v3_01a0ce8b-4b50-74e1-9e78-0eae9f25ce71` |
| Request / snapshot hash | `abb219e317bf0317956e1d901d0e6a1a3a6deb82e695056d344b70afc0232837` |
| Box contract | `[450,50,720,400]` XYXY |
| sync_mode | `cut_off` |
| New permit | `87036a18-d187-4605-a0f0-a3664faae697` CONSUMED |
| Poll | Create wait ~75s then `FAILED_IDENTITY` (no bytes yet). Recover poll **1** (4.2s) materialized the same job. |
| Recovery | `POST recover-existing-lipsync` — **no second Start** |

API log: `POST https://queue.fal.run/fal-ai/sync-lipsync/v3` → 200 → `01a0ce8b-4b50-74e1-9e78-0eae9f25ce71`.

---

## 5. OUTPUT

| Field | Value |
|---|---|
| New ArtifactId | `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e` |
| SHA | `725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e` |
| Duration | video **2.500s** (60 frames @ 24 fps); audio **2.480s** |
| Size | 2,073,636 bytes |
| MIME | `video/mp4` |
| Storage | `os/833e816708624dbbba9d89a2fc3b9a4e` |
| Parents | VIDEO `b44e530b-…` + VOICE `0fde86c2-…` |
| Bindings after quality PASS | `CURRENT:LIPSYNC` + `VISIBLE:LIPSYNC` + `ACCEPTED:LIPSYNC` = new artifact |

`cut_off` trimmed the 8.000s take to the 2.48s voice. That is the intended A/B vs CK23 `silence` (8.01s + 1.84s lead).

---

## 6. QUALITY

| Check | Result |
|---|---|
| Voice start / end | **0.10s → 2.40s** (−45 dB), same as VOICE parent |
| Mouth start / end | First meaningful open at **t≈0.00–0.25**; elevated mouth ROI through **0.10–2.40s** |
| Measured offset | **0.00s** (CK23 was +1.74s) |
| Mouth ROI MAE vs VIDEO | mean **4.39**, peak **8.03** at t=0.25 (CK23A was ~0.3–0.6 = encode noise) |
| Face ROI MAE | mean **1.95** |
| Old wall box MAE | mean **0.26** (untouched) |
| CHAR-003 / Linh MAE | mean **0.43** (encode only; back of head unchanged) |
| Character continuity | Eyes, nose, hair, proportions, paper/hands match the take |
| Facial deformation | None material |
| Speaker targeting | Only CHAR-001 mouth changes |

Frame pairs (`.tmp/ck23b-rerun/lip_*.jpg` vs `vid_*.jpg`):

- t=0: take mouth closed; output slightly open  
- t=0.25 / 1.0 / 1.5: take mouth closed; output clearly open (viseme)  
- Linh silhouette unchanged at all sampled times  

This is **not** the original Veo gasp / paper handoff counted as lipsync.

**NEW LIPSYNC QUALITY = PASS.**

---

## 7. OLD CK23

`32bec770-9706-4aad-bc21-8db3d4f11d4c`  
SHA `9d8e6949eb43480a60cee4e6058cff6239beb24191ed3fe06595e30d12b724db`  
6,389,625 bytes, VERIFIED, file bytes unchanged.

Not deleted. Not overwritten. Not rebound as success.  
Superseded only by the existing binding model: CURRENT / VISIBLE / ACCEPTED:LIPSYNC now point at `b0b4f0f2-…`.

---

## 8. REGRESSION

| Gate | Status |
|---|---|
| CK20 ACCEPTED:VIDEO_TAKE | Intact `b44e530b-…` / SHA `21700e4f…` |
| CK21A video authority | Intact. GetPreview still prefers ACCEPTED:VIDEO_TAKE (existing preview-authority gap — not fixed here) |
| CK22 VOICE | Intact `0fde86c2-…` / SHA `bd348ffb…` CURRENT+ACCEPTED |
| Official SH02 C# regression | PASS FAIL=0 (includes nested CK20 / CK21A / CK22) |
| Official SH02 TS smoke | PASS FAIL=0 |
| VIDEO mutation | None |
| VOICE mutation | None |
| Graph / other shots | None |
| Fal Start after ACCEPT | still **1** |

---

## 9. VERDICT

**PASS WITH GAPS**

Quality PASS and provenance PASS. MIX was **not** started.

Gaps (do not treat as quality failure):

1. Create HTTP returned `FAILED_IDENTITY` after the 75s in-process wait; the same Fal job was recovered. Start count stayed 1.  
2. Recover bound CURRENT/VISIBLE before the quality inspect. Quality then passed; ACCEPT ran only after that.  
3. `cut_off` output is **2.500s / 60 frames**, not the 8.000s shot clock. MIX/CK24 must not assume an 8s lipsync picture.  
4. CK21A GetPreview may still say “miệng chưa khớp” because it prefers ACCEPTED:VIDEO_TAKE. Inspect the new LIPSYNC artifact directly.

**Do not start MIX / CK24 from this checkpoint as a silent next step.** MIX is unblocked on quality, but it is a separate authorization.

---

## PREFLIGHT (compact, as printed before Start)

```
1  BuildId              PASS  4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
2  ShotId               PASS  EP01-SC01-SH02
3  VIDEO artifact       PASS  b44e530b-5b59-422f-9b80-e058a3a639b5
4  VIDEO SHA            PASS  21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34
5  VOICE artifact       PASS  0fde86c2-250f-4303-bda0-4913a6dbd1e4
6  VOICE SHA            PASS  bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298
7  CharacterId          PASS  CHAR-001
8  Provider             PASS  fal
9  Model                PASS  sync-lipsync-v3
10 Fal box format       PASS  XYXY
11 Box contains mouth   PASS  [450,50,720,400]
12 Not CHAR-003         PASS  x2=720
13 New snapshot frozen  PASS  1125945c-… after OpenFrozen
14 Snapshot hash        PASS  abb219e3…
15 Parents immutable    PASS  VIDEO + VOICE + old 32bec
16 Permit               PASS  87036a18-… issued then consumed
17 One execution armed  PASS  CREATE_LIPSYNC only
```
