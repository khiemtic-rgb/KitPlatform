# FAMIXA SH03 — Runway `INTERNAL.BAD_OUTPUT.CODE01` forensic note

No API key, secret, or image data URI is included. Image bytes are identified by SHA-256 only.

## Verdict

Root cause of the missing Shot 03 video is **not proven**.

What is proven: Runway accepted each request, issued a task id, then marked the task `FAILED` with `failureCode=INTERNAL.BAD_OUTPUT.CODE01` and `failure=An unexpected error occurred.` No output URL was returned. KIT Platform did not drop a completed file.

`INTERNAL.BAD_OUTPUT.CODE01` is not given its own entry in the current Runway task-failure docs. It matches the documented family `INTERNAL.BAD_OUTPUT.*` (quality rejection or system error). The docs name `INTERNAL.BAD_OUTPUT.01` as the common member, not `CODE01`.

## What every compared call sent

Source of the wire shape:

- `ContentRunwayClient.ApiVersion` = `2024-11-06` (`ContentRunwayClient.cs` line 14)
- Header `X-Runway-Version: 2024-11-06` plus `Authorization: Bearer` (`ContentRunwayClient.cs` lines 260–263). The bearer value is omitted here.
- `POST https://api.dev.runwayml.com/v1/image_to_video`
- Body fields: `model=gen4_turbo`, `promptImage` as a string data URI `data:image/jpeg;base64,…`, `promptText` after `RunwayI2vPrompt`, `duration=5`, `ratio=1280:720` (`ContentRunwayClient.cs` lines 118–131, `HttpRunwayTransport.cs` lines 64–71 and 126–127)
- No `seed` field
- Same credential id `b394f887-2505-4a5b-b9b8-9b51b3a30eb0`, version `12f22df8-9ce2-4f56-9815-44d757976e99`, on the Shot 01 failure, the Shot 01 success, the Shot 04 success, and all five Shot 03 failures

Current gen4_turbo docs list `1280:720` as a valid ratio and `promptText` max 1000 UTF-16 code units. Data URI limit is 5MB encoded (binary about 3.3MB). These JPEGs are 143–152KB.

## Calls

Times are Asia/Bangkok. `response_payload_hash` is empty on every failure. On success it is the output file SHA-256.

| Task id | Shot | Result | Started | Ended | Image SHA-256 | Bytes | Wire prompt |
|---|---|---|---|---|---|---|---|
| `ef4f8769-11fa-4fdf-88c4-7e31bc94455a` | SH04 | SUCCEEDED | 2026-10-19 23:10:40 | 23:11:07 | `446c840629e941b7fdfe33af4cc836bcf9180672c558269b0f8babe1167ca151` | 142833 | 1000, JSON cut |
| `366df0b9-de31-41d7-b31f-ca60c58af86c` | SH01 | FAILED CODE01 | 2026-10-20 09:36:17 | 09:36:49 | `aa1d46cde16826128397537d119a7343b656a02b16a5423eed75238b4158c1f4` | 148725 | 1000, no JSON |
| `270ac0b8-406a-41db-876c-3876cc393120` | SH01 | SUCCEEDED | 2026-10-20 09:47:28 | 09:48:03 | same SHA as the row above | 148725 | 999, no JSON |
| `ce9d2d25-0f51-4677-a0f4-e03a2cca57c0` | SH03 | FAILED CODE01 | 2026-10-20 11:03:31 | 11:03:59 | `2c939a203ba7b83d2cb1959c6b1269dc0078662538540679ebecffebbc56a13e` | 151869 | 999, JSON cut |
| `77a933d1-322d-4bd9-bf18-489c862e87f1` | SH03 | FAILED CODE01 | 2026-10-20 11:17:16 | 11:17:46 | same SHA as the row above | 151869 | 1000, no JSON |
| `3995e8f7-ac4e-4772-a897-03c1af3b176d` | SH03 | FAILED CODE01 | 2026-10-20 15:13:53 | 15:14:25 | `3ff2f24e3af019dba8e9631fcad5ef91779cd5217d29bd0848f3f2d64e11157d` | 144980 | 999, no JSON |
| `33a9c24c-b78e-4f00-a807-a7dbf8f87f3a` | SH03 | FAILED CODE01 | 2026-10-20 17:04:53 | 17:05:26 | `928548015190d39d142c8750f907242de54b80f64bcf5ed650209adef46d9042` | 147700 | 815, complete |
| `c0e3efae-863c-4068-b0af-69061786aff5` | SH03 | FAILED CODE01 | 2026-10-20 21:49:21 | 21:49:54 | same SHA as the row above | 147700 | 1000, JSON cut |

Every image file is baseline JPEG (`FF D8 FF`, SOF0), 688×384, 8-bit. File SHA-256 matches the stored artifact hash. Aspect 688/384 = 1.792, inside the documented Gen-4 Turbo prompt-image range 0.5–2.358.

Stored provider text on every failure, in both `production_tasks.failure_reason` and `production_attempts.error_code`:

`INTERNAL.BAD_OUTPUT.CODE01: An unexpected error occurred.`

Our task row `failure_code` is `PROVIDER_UNAVAILABLE` because `RecordProviderFailedAsync` writes that label and keeps the provider sentence in `failure_reason` (`ProductionWorkExecutionEngine.cs` lines 217–236). The Runway code itself is the `CODE01` sentence.

`.dev/api.log` records `GET /v1/tasks/{id}` HTTP 200 for `c0e3efae-…` and does not record the response body. The raw task JSON was not persisted.

## Hypotheses

| Hypothesis | Result |
|---|---|
| Request never reached Runway, or the file was lost locally | Disproven. Each failure has a Runway task id. Successes store an output SHA. Failures store an empty response hash and the `CODE01` sentence. |
| Different model, duration, ratio, API version, headers, or credential | Disproven as the difference. All eight calls share `gen4_turbo`, duration 5, `1280:720`, version `2024-11-06`, and the same credential version. |
| Bad MIME, data URI, dimensions, or size | Disproven as the difference. Same JPEG envelope and the same data-URI builder. Sizes are far under the 5MB data-URI limit. An invalid asset is documented as `ASSET.INVALID` or HTTP 400 at submit time. These calls received task ids. |
| Prompt cut at 1000 characters caused `CODE01` | Not sufficient. Shot 04 succeeded with a 1000-character prompt cut inside the visual JSON. Shot 03 `33a9c24c` sent a complete 815-character prompt and failed. Shot 01 used one image for both a cut prompt that failed and a cut prompt that succeeded. The cut is a real KIT defect: `RunwayI2vPrompt` keeps the continuity clause and shortens the body (`VideoVisualIngressV1Rules.cs` lines 155–163, as executed for these calls). A later change stops that cut for visual JSON. That change does not explain `CODE01`. |
| On-image text, logo, or a red score | Not proven. Shot 04 succeeded with a red `5` and the word HOME on the shirt. Shot 01 succeeded with HOME on the shirt. Shot 03 `92854801…` has HOME and a small red mark, no corner logo, and failed twice. Shot 03 `3ff2f24e…` has a corner FAMIXA mark and also failed. The docs list overlaid text as a common `BAD_OUTPUT` cause. These successes contain shirt text and a red digit, so that list does not identify which Shot 03 input was rejected. |
| Prompt asked the model to draw text | Not proven. The Shot 04 success prompt contains “con số đỏ chót” and “a red 5”. Shot 03 `33a9c24c` does not contain “con số” and still failed. |
| A Shot 03-only encoding or integration bug | No evidence. The bytes on disk match the stored SHA-256, and the transport path is shared with the successes. |

## What Runway Support needs to verify

For each Shot 03 task id above, the full Get Task JSON: every `failure` / `failureCode` field and type, whether an output URL ever existed, and whether the quality system flagged the image or the prompt. Also whether `CODE01` is the same condition as documented `INTERNAL.BAD_OUTPUT.01`. The local store does not have that JSON.

## Tests

`Runway_prompt_cap_applies_after_continuity_and_keeps_character_json` and `Visual_continuity_keeps_character_count_inside_runway_prompt`: PASS. They cover prompt assembly. They do not reproduce `CODE01`. No production data was changed and no provider was called for this note.
