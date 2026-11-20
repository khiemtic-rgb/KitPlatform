# FAMIXA PRODUCTION OS — CK23B LIPSYNC FACE BOX FORENSIC V1

Date: 2026-09-23  
Mode: READ-ONLY. No Fal Start. No CREATE_*. No MIX. No T3 / box / code change.

Overlays: `.tmp/ck23b-face-box-forensic/` (scratch only).

---

## 1. BOX SOURCE

Trace (CK23 official path):

```
ProductionEp01Sh02LipsyncCanon.PinX01/PinY01 = 0.28 / 0.38
  file: ProductionEp01Sh02LipsyncCanon.cs
  comment: “pin-v1 T3 (Minh left)”
→ ProductionCreateVideoService.CreateLipsyncAsync
  new ProductionLipsyncSpeakerPin(CHAR-001, 0.28, 0.38, 1280, 720, 0)
→ ProductionLipsyncFreeze.Token → ShotContract
→ FalLipSyncExecutionAdapter → HttpFalLipsyncTransport (same pin)
→ ContentFalClient.LipsyncPayload
    FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox(0.28, 0.38, 1280, 720)
    RepeatFaceBox(box, 192)
→ options.active_speaker_detection.bounding_boxes
```

**T3** is the regression name in `FamixaFalLipsyncSpeakerPinV1Regression` T3:  
`PixelCoordinates(0.28, 0.38, 1280, 720) == [358, 274]` (“Minh left is integer pixels”).

It is a **hard-coded fixture**, copied into the SH02 canon. It is **not** measured from VIDEO_TAKE `b44e530b-…`, not from a keyframe, not from a Director click on this take.

Client left preset is **different**: `lipsyncSpeakerPickOf('left')` = `(0.3, 0.48)` — not what CK23 sent.

### What are 0.28 and 0.38?

| Question | Answer |
|---|---|
| Normalized x/y in [0, 1]? | **Yes.** `X01`/`Y01`; `PixelCoordinates` does `round(clamp(x01,0,1) * width)` |
| Center of the face box? | **Yes, as constructed.** Box is pin ± half (bw, bh) |
| Top-left of the box? | **No** |
| Percentage string? | No — unit interval |
| Click on this take? | **No** |
| Fixture? | **Yes** — pin-v1 T3 “Minh left” |

---

## 2. BOX FORMULA

`FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox` (`FamixaFalLipsyncSpeakerPinV1.cs`):

```
w, h = 1280, 720
pin = [round(0.28*1280), round(0.38*720)] = [358, 274]
bw = max(140, 1280/8) = 160
bh = max(170, 720/4) = 180
x1 = clamp(358 - 80) = 278
y1 = clamp(274 - 90) = 184
x2 = clamp(358 + 80) = 438
y2 = clamp(274 + 90) = 364
return [x1, y1, x2, y2]
```

Return statement is literally `[x1, y1, x2, y2]`.

Repeated 192 times (`FrameCountFromTake` of the 8s / 24 fps take). Same box every frame. No tracking.

---

## 3. COORDINATE SYSTEM

| Axis | Value |
|---|---|
| Source / provider / encoded resolution | **1280×720** (matches pin Width/Height; ffprobe of VIDEO_TAKE) |
| Display / letterbox / contain-fit | **Not used** on this path (no click, no UI transform) |
| Crop / scale | **None** |
| Origin | Top-left of the encoded frame (x right, y down) |
| Ordering | C# names **x1, y1, x2, y2** (min/max corners) |
| Width/height on the pin | **Frame size** (1280×720), not box size |

Fal OpenAPI in-repo lists `bounding_boxes` as `integer[]` per frame. It does **not** say xyxy vs xywh.  
If Fal read `[278,184,438,364]` as **x,y,w,h**, the rectangle would be `(278,184)–(716,548)`. That is **not** what Famixa computed. Famixa computed xyxy.

---

## 4. SOURCE FRAME / REFERENCE

| Candidate | Used? |
|---|---|
| Keyframe | No |
| VIDEO_TAKE frame 0 **pixels** | **No** — only duration/frame **count** (192) |
| Character master / reference | No |
| Scene / fixture image | No |
| Previous provider output | No |
| Hard-coded T3 pin | **Yes** |
| PinFrame | **0** (token only; box not sampled from frame 0) |

The box is a generic “Minh sits on the left third” fixture, not a measurement of this Veo take.

---

## 5. VIDEO GEOMETRY

VIDEO_TAKE `b44e530b-5b59-422f-9b80-e058a3a639b5`  
`os-veo-out/5a72a533ca30466e99b0357fa47bcdd8.mp4`

| Field | Value |
|---|---|
| Resolution | 1280×720 |
| Frames | 192 |
| fps | 24 |
| Duration | 8.000s |
| Aspect | 16:9 |
| Audio | none |

---

## 6. ACTUAL FACE REGION

No in-repo face detector. Visual read of encoded frames (no AI):

CHAR-001 = boy, left-of-center, large head. CHAR-003 = mother, right edge, mostly hair/back.

**Frame 0** (looking down at paper):

- Head ≈ **x 460–700, y 40–340**
- Mouth ≈ **x 540–620, y 270–320**
- Face center ≈ **(0.45, 0.26)** — not (0.28, 0.38)

**Frame 3.5 / 4.0** (gasp, open mouth):

- Face still center-left, similar band
- Open mouth ≈ **x 530–620, y 250–330**

T3 pin `(358, 274)` = **empty wall** to the left of the hair.

---

## 7. CURRENT BOX INTERSECTION

Overlay of Famixa xyxy `[278,184,438,364]` on the real take:

| Frame | Intersection with CHAR-001 face |
|---|---|
| 0 | **None.** Box is wall. Right edge 438 is left of the hair. Mouth not inside. |
| 3.5 | **None.** Same wall. Open mouth far to the right of the box. |
| 4.0 | **None.** |

CHAR-003 is not in the box either.

Normalized T3 box: **[0.217, 0.256]–[0.342, 0.506]**.  
CHAR-001 face ≈ **[0.36, 0.06]–[0.55, 0.47]**. Overlap ≈ **empty**.

If someone mis-read the 4-int as xywh `(278,184,716,548)`, that larger rectangle **would** cover much of the head (mouth near the right edge at 3.5). That is **not** Famixa’s formula. Fal’s 4-int layout remains **UNKNOWN FROM REPOSITORY**.

---

## 8. EXPECTED BOX

Not applied. Visual estimate on **this** VIDEO_TAKE only:

| | Pixels (xyxy) | Pin (normalized center) |
|---|---|---|
| Face (hair–chin, frame 0 / 3.5) | **[460, 70, 700, 350]** | **≈ (0.45, 0.29)** |
| Mouth-tight | **[520, 240, 640, 340]** | **≈ (0.45, 0.40)** |

Why: T3 assumes a small left-third face. This take is a close two-shot; Minh’s head is large and near frame center. Formula `max(140, w/8)` × `max(170, h/4)` is also **smaller than this head**.

Do not write this into canon in this checkpoint.

---

## 9. CASE A / B / C

**CASE A.**

The box Famixa built and (per C#) sent as xyxy **does not cover CHAR-001’s face** on VIDEO_TAKE `b44e530b-…`. Root-cause candidate: **face-box mapping** (fixture T3 + fixed 160×180 box, not measured on this take).

Not CASE B for the xyxy overlay: the red rectangle is on wall, not a mis-drawn face.

CASE B remains only for **Fal’s unpublished 4-int layout** (xywh would look different). That does not make the T3 pin a face measurement.

Not CASE C as the primary finding: we cannot say “box is correct and Fal still failed.” The constructed xyxy box is not on the face. Playground `cut_off` + same 192 boxes + no viseme is **consistent with A** (wrong target) and does **not** prove C. C can still be true *after* a correct box — not proven here.

---

## 10. ROOT CAUSE CONFIDENCE

| Claim | Confidence |
|---|---|
| T3 0.28, 0.38 is a hard-coded pin, not this take | **High** |
| Formula produces `[278,184,438,364]` as xyxy | **High** |
| That rectangle misses CHAR-001 face on frames 0 / 3.5 / 4.0 | **High** |
| Fal interprets the 4-int as xyxy | **Unknown** (OpenAPI silent) |
| Wrong box is why Fal added no visemes | **Medium** — sufficient to disqualify this box; not sufficient to prove the model would viseme with a correct box |

---

## 11. NEXT ACTION

Do **not** MIX. Do **not** Start Fal. Do **not** change T3 in this checkpoint.

Later (only if authorized):

1. Measure a pin/box from **this** VIDEO_TAKE frame 0 (not pin-v1 T3).
2. Confirm Fal `bounding_boxes` element layout from current provider docs (xyxy vs xywh).
3. Treat silence-offset (CK23A) and viseme failure as **separate**. A wall box can explain no mouth change; it does not explain +1.74s audio by itself.

---

## SAFETY

Provider POST = 0. Artifact / snapshot / binding / T3 unchanged.

---

## VERDICT

**FAIL**

Famixa’s constructed box `[278,184,438,364]` is proven, on this VIDEO_TAKE, to sit on empty wall — not on CHAR-001’s face.

CK23 EXECUTION = PASS · QUALITY = FAIL · MIX = BLOCKED.

STOP.
