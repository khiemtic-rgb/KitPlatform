# FAMIXA_FAL_V3_LIPSYNC_CONTRACT_FORENSIC_V1_REPORT

**Suite:** `FAMIXA_FAL_V3_LIPSYNC_CONTRACT_FORENSIC_V1`  
**Date:** 2026-09-14  
**Mode:** READ-ONLY. No payload fix. No new Fal / Veo / Runway / Wan / Gemini / ElevenLabs calls.

---

## 1. VERDICT

**D — Existing evidence is insufficient for a definitive contract sentence.**

Supported lean (not proven):

- Wave 1 (first three request_ids): numeric type mismatch inside `options.active_speaker_detection` is **consistent** with float `coordinates` (two `detail` rows, `input=Number`). The **literal** `type` / `msg` were **not** captured.
- Wave 2–3 (last three request_ids): Fal returned a first-party **`value_error`** (`type` length 11 + `url=docs.fal.ai/errors`) whose `input` is the **entire ASD object**. This still happens **after** integer coordinates and **after** `v3` is removed.
- Published Fal OpenAPI lists `frame_number` + `coordinates` as optional integers and does **not** mark them required or mutually exclusive. Fal **llms.txt** examples never show ASD. Sync.so native docs **do** show manual pin — on `api.sync.so/v2/generate`, not as a Fal playground example.
- Therefore: **`v3=true` is not the sole cause.** **Float coordinates are not the sole cause** of later failures. **Whether this Fal endpoint supports manual ASD at runtime is unproven.** The published schema and the stored Usage `json_output` disagree; the **exact validator `msg` is missing** from local capture.

**INCONCLUSIVE — MISSING FAL VALIDATOR DETAIL** for the key question in §7.

---

## 2. EXACT ROOT CAUSE

**Not established.** What is established:

1. All six jobs were **accepted by Fal Usage** (`items=1`, `json_input` + `json_output` present). None produced `video.url` or Assets.
2. `json_output` is a Pydantic/Fal **validation `detail[]`**, not a lipsync file.
3. `loc` prefix on every captured row decodes as `body` (4) / `options` (7) / `active_speaker_detection` (24).
4. Local peek **redacts** string values to `str:{length}`. **`detail.msg` and `detail.type` literals are not in the repo.**
5. A prior in-repo regression fixture (`int_type` + `loc: ["body"]` + invented `value_error` msg) is **test data**, not a Fal capture. Do not treat it as Usage truth.

---

## 3. EVIDENCE

### 3.1 Sources used (zero provider spend)

| Source | Used |
|---|---|
| `.dev/fal-usage-peek.txt` | Yes — Usage GET **already performed in earlier sessions**; this phase only **read** the file |
| `FamixaFalLipsyncResultV1.RedactShape` | Yes — explains why `msg`/`type` are lengths only |
| Live Fal OpenAPI `fal-ai/sync-lipsync/v3` (public, unauthenticated) | Yes — 2026-09-14 |
| `https://fal.ai/models/fal-ai/sync-lipsync/v3/llms.txt` | Yes |
| `https://docs.fal.ai/errors` (`value_error`) | Yes |
| `https://sync.so/docs/developer-guides/speaker-selection.md` | Yes — upstream Sync, not Fal |
| `ContentFalClient.LipsyncPayload` + `JsonOpts` | Yes — reconstruct serialize |
| `ContentSeriesTurboService.StartLipsyncAsync` | Yes |
| Captured raw `json_input` / `json_output` bodies | **No file in workspace** |
| Fal Dashboard (human) | **Not opened this phase** |
| New Fal GET Usage with API key | **Not called this phase** |

### 3.2 Peek capture limitation (why `msg` is unknown)

`DescribePlatformPeek` → `RedactShape`:

- Strings become `str:{min(len,80)}` (never the text).
- Arrays keep **only the first 3** elements.
- Depth > 4 becomes `…`.

So `loc=[str:4|str:7|str:24]` is **at most** the first three loc segments. Further segments (`coordinates`, `0`, …) would be **dropped**.

### 3.3 What Usage did expose (shape only)

Every matching Usage row for these ids:

```
itemKeys=request_id,endpoint_id,started_at,sent_at,ended_at,status_code,duration,json_input,json_output
json_output=Object
endpoint query: fal-ai/sync-lipsync/v3
```

`json_input` keys were **not** written to peek (only that the property exists).

---

## 4. REQUEST-BY-REQUEST COMPARISON

Endpoint for all six: **`fal-ai/sync-lipsync/v3`**.  
Task id on desk = `lipsync_v3_` + `request_id`.

Payload columns A–C are **reconstructed from C# at the time of that wave**, not from stored HTTP dumps. Coordinate numbers assume Linh-right preset `{x:0.66,y:0.38}` on **1280×720** (`lipsyncSpeakerSidePreset('right')`). If the click recorded a different `width`/`height`, pixels differ; that is **not** in peek.

| ID | UTC (peek) | Wave | Reconstructed ASD (best effort) | Captured `json_output` shape | What changed vs previous | What stayed |
|---|---|---|---|---|---|---|
| `01a09617-d14e-77d3-abd2-3140caa566c4` | 10:10 / 14:46 | A | `v3:true`, `auto_detect:false`, `frame_number:0`, `coordinates` **float** `[844.8, 273.6]` | **2** detail rows; `type` **len 14**; `msg` **len 68**; `loc` 4/7/24; **`input=Number`** twice; body ~1056 B | — | ASD object present |
| `01a0a0aa-fce2-78e0-af90-f119e8022f8e` | 16:06 | A | Same class as row 1 (still float path) | **Identical shape** (~1056 B) | New paid POST, same validator family | Same |
| `01a0a0ad-4c75-7e52-b4dc-b0631a8296ec` | 16:08 | A | Same class | **Identical shape** (~1055 B) | New paid POST | Same |
| `01a0a0b6-7b51-7700-b8a4-afbfb15eec56` | 16:19 | B | `v3:true`, ints `[845, 274]` via `PixelCoordinates` | **1** detail row; `type` **len 11**; `msg` **len 80**; `url=docs.fal.ai/errors`; **`input={auto_detect=False,v3=True,frame_number=Number,coordinates=[…\|…]}`**; ~1025 B | Integers; error moves from two `Number` items to **whole ASD object**; Fal `value_error` URL appears | `auto_detect:false`, `frame_number`, `coordinates`, ASD nest |
| `01a0a0bf-0f26-7351-98cc-eeaec23bee89` | 16:28 | C | **`v3` omitted**; ints `[845, 274]` | Same family as B; `type` 11; `msg` 80; `url=docs.fal.ai/errors`; **`input={auto_detect=False,frame_number=Number,coordinates=[…\|…]}`** — **no `v3` in input**; ~1006 B | `v3` removed | ASD still rejected as an object |
| `01a0a0bf-d39b-71e0-b9f9-f59ded394ffa` | 16:29 | C / D–E | Same as C | **Same shape as 16:28** (~1006 B) | Another paid POST | Same validator family |

### Hypothesis test

| Claim | Evidence | Result |
|---|---|---|
| Float `coordinates` = **the** cause | Wave A: two `input=Number` under ASD. Wave B/C: `input` is the **object**, not two numbers | **Not sufficient.** Explains A only. |
| `v3=true` = **the** cause | Wave C input has **no `v3`**, same `value_error` envelope | **Falsified as sole cause.** |
| Entire `active_speaker_detection` object = cause | Wave B/C: `loc` ASD + `input` is the object + Fal `value_error` | **Consistent, not proven** (msg unknown). |
| Other field / wrapper mismatch | Queue stored the job; OpenAPI allows this object; runtime `json_output` is validation | **Plausible (C).** Needs literal `msg`. |

---

## 5. EXACT FAL VALIDATOR MESSAGE

**Not available in this workspace.**

| Field | Wave A (first 3 ids) | Wave B (`01a0a0b6-…`) | Wave C (last 2 ids) |
|---|---|---|---|
| `detail[].type` | unknown string **length 14** (not `int_type` which is length 8; not `value_error` which is 11; **not** `int_from_float` which is 13) | unknown string **length 11** (matches `value_error`) | same as B |
| `detail[].msg` | unknown, **length 68** | unknown, **length 80** | unknown, **length 80** |
| `detail[].loc` | first 3: `body`, `options`, `active_speaker_detection` (further segments unknown) | same first 3 | same first 3 |
| `detail[].input` | `Number` (twice) | ASD object including `v3=True` | ASD object **without** `v3` |
| `detail[].url` | not in peek | `docs.fal.ai/errors` | `docs.fal.ai/errors` |

Fal docs (`value_error`): non-retryable; **`msg` is the reason**. We do not have `msg`.

**Do not use** the strings `Input should be a valid integer` or `v3 cannot mix with manual coordinates` as Usage truth. Those appear only in **regression fixtures**.

To obtain literals: Fal Dashboard → Usage → request → `json_output.detail` (human). This phase did not call Usage again.

---

## 6. CURRENT FAL SCHEMA

Fetched 2026-09-14:  
`https://fal.ai/api/openapi/queue/openapi.json?endpoint_id=fal-ai/sync-lipsync/v3`

No generated Fal SDK / OpenAPI copy lives in this repo.

### `SyncLipsyncV3Input` (queue title `Sync3Input`)

| Field | Type | Required | Notes |
|---|---|---|---|
| `video_url` | string | **yes** | |
| `audio_url` | string | **yes** | |
| `sync_mode` | enum `cut_off\|loop\|bounce\|silence\|remap` | no | default `cut_off` |
| `options` | `Sync3GenerationOptions \| null` | no | “Additional **Sync.so** generation options” |

### `Sync3GenerationOptions`

Optional: `sync_mode`, `model_mode`, `prompt`, `temperature`, `active_speaker_detection`, `occlusion_detection_enabled`.  
`temperature` / `occlusion_detection_enabled` marked **ignored by sync-3**.  
`prompt` / `model_mode` marked **primarily react-1**.  
**No `required` array.**

### `Sync3ActiveSpeakerDetection`

| Field | Schema | Required |
|---|---|---|
| `auto_detect` | boolean \| null | **no** |
| `v3` | boolean \| null — “Whether to use Sync.so's active speaker detection v3” | **no** |
| `use_v2` | boolean \| null — deprecated v2 | **no** |
| `frame_number` | integer, **minimum 0** \| null | **no** |
| `coordinates` | array of **integer**, **minItems 2, maxItems 2** \| null | **no** |
| `bounding_boxes` | array of (`integer[]` \| null) \| null | **no** |
| `bounding_boxes_url` | string \| null | **no** |
| `face_image` | string \| null — “Base64 encoded face image for **manual** speaker selection” | **no** |

OpenAPI does **not** publish:

- `required` on ASD
- `oneOf` / mutual exclusion (`coordinates` vs `bounding_boxes` vs `auto_detect`)
- `additionalProperties: false`

**Field presence in OpenAPI ≠ demonstrated support.** Fal **llms.txt** “Required” and “Full Example” and all cURL/JS/Python samples are **only** `video_url` + `audio_url` (+ optional `sync_mode`). **Zero official Fal v3 examples include `options.active_speaker_detection`.**

---

## 7. FAMIXA SERIALIZED PAYLOAD

Trace (no new call):

```
ContentFamixaSeriesTab.startLipsync
  → POST /api/content/series/lipsync
  → ContentSeriesTurboService.StartLipsyncAsync
       UploadAsync(take bytes, video/mp4)
       UploadAsync(audio bytes, audio/mpeg|wav)
  → FalLipSyncProvider.StartAsync
  → ContentFalClient.CreateLipsyncAsync
  → POST https://queue.fal.run/fal-ai/sync-lipsync/v3
       JsonContent.Create(..., JsonOpts)
```

`JsonOpts`: `PropertyNameCaseInsensitive`, `DefaultIgnoreCondition = WhenWritingNull`.  
**No** `JsonSerializerDefaults.Web`. Anonymous property names are already snake_case.  
`bool` → JSON `true`/`false`. `int[]` → JSON integers. `double[]` (pre-pin helper) → JSON numbers with fraction.

**No raw request body is in logs.** Below is the serialize **contract** of current `LipsyncPayload` when `kind=="v3"` and speaker 0–1:

```json
{
  "video_url": "https://<fal-upload-host>/…",
  "audio_url": "https://<fal-upload-host>/…",
  "sync_mode": "<cut_off|loop|bounce|silence|remap>",
  "options": {
    "active_speaker_detection": {
      "auto_detect": false,
      "frame_number": 0,
      "coordinates": [845, 274]
    }
  }
}
```

(`v3` is **not** in current C#. It **was** serialized on waves A–B.)

Wave A coordinates (same helper math, before `int` rounding):

```json
"coordinates": [844.8, 273.6]
```

`sync_mode`: C# `NormalizeSyncMode` default **`silence`** if client omits/unknown. Fal default is **`cut_off`**. Enum-valid either way — **not** an explanation for ASD `value_error`.

URLs: Fal CDN after `UploadAsync`, not localhost. Hosts not copied here (no secrets, no full CDN paths).

**Auth headers / API keys: not logged, not reproduced.**

---

## 8. SCHEMA DIFFERENCES

```
FAMIXA JSON
    ↓
Fal queue OpenAPI (SyncLipsyncV3Input)     ← fields allowed; ASD optional
    ↓
Fal Usage json_output.detail               ← validation object, no video
    ↓
? Fal custom validator and/or Sync.so      ← msg unknown
```

| Topic | FAMIXA (waves) | Fal OpenAPI | Fal llms examples | Sync.so `/v2/generate` docs |
|---|---|---|---|---|
| Envelope | `{video_url,audio_url,sync_mode,options}` | Same top-level | `{video_url,audio_url}` only | `{model,input[],options}` — **different** |
| ASD nest | `options.active_speaker_detection` | Same | **Absent** | Same nest on Sync HTTP |
| `coordinates` type | float then int[2] | **integer[2]** | n/a | `[x,y]` (examples are ints) |
| `frame_number` | `0` | integer ≥ 0 | n/a | Video example **240**; image **0** |
| `v3` | true, then removed | optional bool | n/a | optional |
| `auto_detect` | `false` | optional bool | n/a | `false` for manual |
| `face_image` | never sent | optional | n/a | not in Sync cURL examples |
| `bounding_boxes` | never sent | optional | n/a | alternative to point; **one box per frame** |

Mismatches that are **facts**:

1. Wave A `coordinates` **floats** vs OpenAPI **integers**.
2. Fal **marketing/examples** never show ASD; OpenAPI **does**.
3. Sync.so examples use a **different root body** (`model` + `input[]`). Fal wraps that as `video_url`/`audio_url` + `options`.
4. Runtime `json_output` rejects ASD **after** (1) is fixed — **not explained by OpenAPI**.

---

## 9. MANUAL SPEAKER SUPPORT

| Question | Answer from **published** material only |
|---|---|
| Are `coordinates` in Fal OpenAPI? | Yes, `integer[2]` |
| Cardinality | exactly 2 when present |
| `frame_number` type | integer ≥ 0 |
| `auto_detect` type | boolean \| null |
| Does `v3` exist on Fal ASD? | Yes, optional boolean |
| Are `frame_number` + `coordinates` jointly marked valid? | **Not stated.** Both optional; no `dependentRequired` |
| Are bounding boxes required? | **Not in OpenAPI** |
| Does `bounding_boxes_url` exist? | Yes, optional |
| Is `face_image` required? | **No** (optional; described as manual) |
| Is manual selection **supported** on this Fal endpoint? | **Cannot conclude.** Schema lists fields. Examples omit them. Runtime rejected FAMIXA ASD. Sync.so documents manual pin on **their** API. |

Do **not** infer support from field presence alone.

---

## 10. MINIMAL DOCUMENTED PAYLOAD

Fal llms.txt + OpenAPI `required`:

```json
{
  "video_url": "https://…",
  "audio_url": "https://…"
}
```

**BASELINE_PAYLOAD_DOCUMENTED_VALID = YES**

Documented speaker form on **Fal**: OpenAPI ASD fields only; **no Fal example**.  
Documented speaker form on **Sync.so**: `options.active_speaker_detection: { auto_detect: false, frame_number, coordinates }` **or** per-frame `bounding_boxes`.

**Not executed.**

---

## 11. RECOMMENDED ADAPTER CHANGE

**None in this phase** (forensic stop).

When a later phase is authorized, **evidence-first** (still no speculative POST):

1. Human copies Usage `json_output.detail` for `01a0a0bf-0f26-7351-98cc-eeaec23bee89` (wave C, no `v3`).
2. Persist **unredacted** `type` / `loc` / `msg` in peek (no URLs, no keys) so this gap cannot recur.
3. Only then decide adapter: drop ASD, switch to `auto_detect: true`, `bounding_boxes_url`, or `face_image` — **after** the literal `msg` names the rule.

Do not treat current `skipSpeakerPin` (client, unconfirmed) as a verified contract.

---

## 12. CONFIDENCE LEVEL

| Statement | Confidence |
|---|---|
| All six jobs have Usage rows and no output video | **High** (peek `match=yes`, `json_output=Object`, no Assets) |
| Failures sit under `options.active_speaker_detection` | **High** (loc lengths 4/7/24) |
| Wave B/C are Fal `value_error` | **Medium-high** (`type` len 11 + `docs.fal.ai/errors`) |
| Wave A is float-coordinate `int_*` | **Medium** (two `Number` inputs; `type` len 14 ≠ `int_type`) |
| `v3=true` is not the only failure | **High** (wave C input lacks `v3`) |
| Exact `msg` / full `loc` / exact `type` | **None** |
| Manual pin is unsupported on Fal v3 | **Unproven** |
| Key question (§7) | **INCONCLUSIVE** |

---

## 13. REMAINING UNKNOWN

1. Literal `detail.type` and `detail.msg` for all six ids.  
2. Full `loc` after the third segment.  
3. Full `json_input` (CDN URLs, exact `sync_mode`, exact pixel ints, whether `width`/`height` were 1280×720).  
4. Whether queue HTTP was 200 then runner-validated, or 422 stored as Usage output.  
5. Whether Fal forwards ASD unchanged to Sync.so.  
6. Whether `face_image` or per-frame boxes are **runtime**-required despite optional OpenAPI.  
7. Whether `frame_number: 0` fails a Fal custom rule on **video** (Sync.so video example uses 240; image uses 0). **Not claimed** — would be guessing without `msg`.

---

## 14. NEXT SAFE TEST

**This phase: stop. No POST.**

Safe next **human** step (0 generation if they only open Dashboard):

1. Fal Dashboard → Usage → open  
   `01a0a0bf-0f26-7351-98cc-eeaec23bee89`  
   and  
   `01a09617-d14e-77d3-abd2-3140caa566c4`  
2. Paste `json_output.detail` (and ASD subset of `json_input`) into the next forensic note.  
3. **Do not** Confirm another FAMIXA job until that `msg` is on file.

If a later **paid** probe is approved (not this phase): playground **baseline** `{video_url, audio_url}` only, one clip, to see if the endpoint produces video **without** ASD. That spends credit; **not run here**.

---

## 15. SAFETY REPORT

```
FAL POST CALLS:              0
FAL CREDITS SPENT:           0
VEO CALLS:                   0
RUNWAY CALLS:                0
WAN CALLS:                   0
GEMINI CALLS:                0
ELEVENLABS CALLS:            0

NEW MEDIA:                   0
PRODUCTION ARTIFACT CHANGES: 0
APPROVAL CHANGES:            0
CANON CHANGES:               0
VA CHANGES:                  0
SHOT WHAT CHANGES:           0
TIMING CHANGES:              0
```

This phase: read local peek + public docs + source; wrote this file only.

**STOP.**
