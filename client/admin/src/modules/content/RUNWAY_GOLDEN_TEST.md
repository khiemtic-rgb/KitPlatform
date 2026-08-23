# RUNWAY GOLDEN TEST — SH01-04

Một KF. Không bấm lại. Không 70 shot. HTTP chuẩn: `ContentRunwayClient` + `content-runway-adapter.ts`.

## Contract

| Field | Value |
|---|---|
| API | `POST /v1/image_to_video` · `GET /v1/tasks/{id}` |
| Host | `https://api.dev.runwayml.com/` |
| Version header | `X-Runway-Version: 2024-11-06` |
| Model | `gen4_turbo` |
| Duration | `5` |
| Ratio | `1280:720` |
| Compiler | `RUNWAY_PROMPT_V1` |
| Prompt max | 900 UTF-16 |

Payload (key omitted):

```json
{
  "model": "gen4_turbo",
  "promptImage": "<jpeg data-uri 1280×720>",
  "promptText": "<see Test A / B / C>",
  "ratio": "1280:720",
  "duration": 5
}
```

Success = `SUCCEEDED` + output URL + downloaded file readable. HTTP 200 ≠ VIDEO READY.

## Test A — Direct / diagnostic prompt

Input: JPEG SH01-04 đã normalize (1280×720, sRGB, quality 90–95).

```
Subtle natural movement, realistic cinematic family drama.
```

Ghi: Task ID · status · failureCode · output URL · download OK · Credit Usage.

- A FAIL → Runway / input / model.
- A PASS → sang B.

## Test B — KIT Adapter

Cùng ảnh + cùng prompt A, gửi qua Famixa `TEST INPUT` rồi Confirm 1 job (adapter log).

- A PASS, B FAIL → lỗi KIT adapter / payload.
- A PASS, B PASS → sang C.

## Test C — Production prompt

Cùng ảnh + `compileRunwayPromptV1` (chỉ motion + camera; không mô tả lại KF, không câu phủ định).

- A/B PASS, C FAIL → lỗi Prompt Compiler.
- C PASS → Famixa được phép 1 shot production.

## Log bắt buộc mỗi lần

1. Input asset (mime, w×h, bytes, hash)
2. Exact payload (redact data-URI)
3. API version
4. Model
5. Prompt + compiler version
6. HTTP response (task id)
7. Task ID
8. Final task status
9. Failure code
10. Output URL
11. Download validation
12. Credit state: `NONE` / `PENDING` / `ACTUAL` / `REFUND_PENDING`

SH01-04 job đã FAIL (tham chiếu): `3f9145a8-1481-4a90-9e41-4ae37f390bbc` · `INTERNAL.BAD_OUTPUT.CODE01`. Không gửi lại cùng fingerprint.

## Acceptance (smoke, 0 cr)

`npx tsx content-runway-adapter.smoke.ts` + `content-runway-prompt-v1.smoke.ts`

Test 1 live SUCCESS: operator chạy A **một lần**. Smoke không trừ credit.
