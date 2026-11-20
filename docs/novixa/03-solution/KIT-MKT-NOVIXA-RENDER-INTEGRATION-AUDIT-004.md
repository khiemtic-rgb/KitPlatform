# KIT-MKT-NOVIXA-RENDER-INTEGRATION-AUDIT-004

Date: 2026-10-21  
Status: `READY_FOR_IMPLEMENTATION`  
Scope: audit and implementation specification only. No code, database, migration, or pipeline run in this task.

Fixture `NOVIXA-LOCAL-RENDER-FIXTURE-003` proved that `MarketingLocalVideoRenderer` can write a decodable H.264 1080×1920 MP4 with an audio stream from local files. That result is not evidence that the NOVIXA business pipeline produces a playable job.

## Decision

Use the statuses already allowed by `ck_video_job_status` (migration 290). Do not add an enum value, a column, or a migration.

The single NOVIXA render path is the gate that already exists: brand code `novixa`, template code `tiktok_45s_hooks`, template provider not `creatomate` (`IsNovixaTiktokHooks`). Provider stays `storyboard_local`. Do not add a provider, a renderer type, or a second pipeline.

`Ready` on that path means one thing: a new MP4 was decoded and `output_url` is that job's media route. A storyboard is `PreparingRender`. It is not `Ready`.

## A. Current pipeline

### Paths that set `Ready`

| Entry | When | What `Ready` means today |
|---|---|---|
| `PrepareStoryboardAsync` | Every brand and template, including NOVIXA | Storyboard JSON was written. No image file, no voice, no MP4. `rendered_at` is set to now. Existing `output_url` is kept. |
| `RunMvpPipelineAsync` when `Render` is false | After optional Pollinations URLs and optional ElevenLabs | Storyboard plus notes. Missing ElevenLabs becomes `voice=skipped` inside `error_message`, and the status is still `Ready`. |
| `QueueRenderWithStoryboardAsync` | Provider is not Creatomate, or the key or template UUID is missing, and the job is not the NOVIXA local gate | Storyboard or CapCut handoff. Existing `output_url` is kept. |
| `RenderNovixaLocalAsync` | NOVIXA gate only, after `IsPlayableFile` | Local `output.mp4` passed the `ftyp` and 1 KB check. This is not a decode. |
| `QueueRenderWithStoryboardAsync` / `RefreshRenderAsync` / `ApplyCreatomateWebhookAsync` | Creatomate | `ContentVideoRenderStatus.FromProvider` returned `Ready` because the provider status is a success word and the URL is `http(s)` and not an image extension. The file is not downloaded or decoded. |

`CreateFromPackageAsync` inserts `Draft` only. It does not render.

`ApproveAsync` moves `Ready` or `Approved` to `Approved` and does not look at the file. A storyboard-only `Ready` job can be approved.

### Path that actually creates an MP4

Only `RenderNovixaLocalAsync`, reached from:

- `QueueRenderAsync` when `IsNovixaTiktokHooks` is true, via `RenderNovixaLocalFromJobAsync`
- `RunMvpPipelineAsync` when `Render` is true, via `QueueRenderWithStoryboardAsync`

Both HTTP actions are queued (`video_render`, `video_mvp`). The worker calls the service. The UI then reloads the job. Creatomate does not write a local MP4.

`MarketingLocalVideoRenderer` writes `output.mp4` only when every scene image and the voice file already sit inside the job directory. If `output.mp4` already passes `IsPlayableFile`, the renderer returns that file and does not encode again. If the existing file fails that check, it refuses to overwrite.

### What is on disk, and what is only a URL

Job directory: `Content:VideoAssetRoot` (default `App_Data/content-video`) plus the job id in `N` format.

| Resource | Stored as |
|---|---|
| Storyboard text, beats, timeline | `video_job.storyboard_json` only |
| Scene image after MVP `GenerateImages` | `imageUrl` = `https://image.pollinations.ai/prompt/...`. Not a file. |
| Scene image during NOVIXA render | Downloaded to `scene-01-source.bin` and so on, only if the URL host is `image.pollinations.ai`. Checked with a 12-byte image header, then passed to the renderer. |
| Voice | `voice.mp3` in the job directory, and `config_json.voiceLocalPath`, only when `GenerateVoice` is true and ElevenLabs is configured. Otherwise nothing is written. |
| MP4 | `output.mp4` in that directory, plus frames and `frames.txt` |
| `output_url` | Relative `/api/content/video/media/{jobId:D}/output.mp4`, or the same path under `PublicMediaBaseUrl` |

`GET video/media` opens `App_Data/content-video` under the process current directory. It does not read `VideoAssetRoot`. With the default setting the paths match. A configured root makes `output_url` point at a file the GET handler will not open.

### Missing image, missing voice, FFmpeg

On the NOVIXA render path, `RenderNovixaLocalAsync` first sets `Rendering` and clears `output_url` and `rendered_at`.

- No storyboard: `Failed`, error `Chưa có storyboard NOVIXA...` or `storyboard_json không đọc được.` `output_url` null.
- Empty scene text, non-Pollinations image URL, download failure, header mismatch, or Skia decode failure inside the renderer: `Failed`, `output_url` null, the exception message kept for `InvalidOperationException`.
- Missing or empty `voice.mp3`, or `voiceLocalPath` outside the job directory: `Failed` with the voice message. This happens only if render is actually invoked.
- FFmpeg missing or non-zero exit: renderer returns `Fail` with stderr or `Không tìm thấy ffmpeg trên PATH.` Service stores that as `Failed` and clears `output_url`.
- `IsPlayableFile` false: `Failed`, `Render local không tạo được MP4 hợp lệ.`

Those failures do not remove a bad `output.mp4`. The next call hits the renderer's refusal to overwrite.

Outside that render call, missing voice is not a failure. MVP with `Render=false`, or with ElevenLabs unconfigured, still sets `Ready`. `PrepareStoryboardAsync` sets `Ready` with no media check.

`RunMvpPipelineAsync`'s catch sets `Failed` but writes the original `storyboard_json`, `config_json`, and `output_url` from the row loaded at the start. A failure after the `PreparingRender` update can put back the old storyboard and keep an old `output_url`. `RenderNovixaLocalAsync` itself catches and returns, so that catch does not cover renderer failures.

The work item is marked succeeded when the service returns `Failed` without throwing. The queue does not retry that video. An exception before the render call, such as ElevenLabs throwing, is retried by the work queue up to `WorkerMaxRetries`.

### When `output_url` is set

`UpdateVideoJobAsync` assigns `output_url` directly. Null clears it.

On the NOVIXA render path it is set only after `rendered.Ok`, a non-empty path, `IsPlayableFile`, and `IsJobOutputRoute`. `IsPlayableFile` is size at least 1024 bytes and an `ftyp` header, and it rejects an image header. It does not decode the file. A fake `ftyp` file is enough.

Creatomate sets `output_url` from the provider URL when `FromProvider` says `Ready`. That check is URL shape only (`ContentVideoRenderStatus.IsPlayableVideo`).

`PrepareStoryboardAsync` and the non-Creatomate queue path copy the previous `output_url` while setting `Ready`.

## B. Status meanings, using current values

No new status. Invariant for the NOVIXA gate only:

| Meaning | Status | `output_url` | `rendered_at` | `error_message` |
|---|---|---|---|---|
| Script stored, storyboard may be empty | `Draft` | unchanged by create | null on create | null |
| Storyboard text is saved, media not verified | `PreparingRender` | null | null | null, or a non-success note if voice or images were not produced |
| Render started | `Rendering` | null | null | null |
| Image, voice, FFmpeg, or decode failed | `Failed` | null | null | the specific reason |
| MP4 decoded and bound to this job's media route | `Ready` | that route | now | null |
| Operator approved that verified MP4 | `Approved` | same route | kept | null |

`GeneratingAssets` and `GeneratingVoice` stay transient labels inside MVP. `Queued` stays the Creatomate queue state. `GeneratingScript` is unused; leave it unused.

`PreparingRender` is the storyboard-ready state. The UI label today is `Chuẩn bị render…`. For this gate, the screen must say storyboard, not video. Do not relabel `PreparingRender` globally: other brands pass through it on the way to Creatomate.

Rejected: a new `StoryboardReady` status. The check constraint would need a migration, and the four meanings above fit the current list.

Other brands, other templates, and Creatomate keep today's status writes, including `PrepareStoryboardAsync` setting `Ready`.

## C. Minimal NOVIXA path

One path. Do not add an endpoint.

1. Gate with the existing `IsNovixaTiktokHooks` predicate. Anything else keeps the current method body.
2. Storyboard button and MVP with `Render=false`: persist `storyboard_json`, set `PreparingRender`, clear `output_url` and `rendered_at`. Do not set `Ready`. Do not call the renderer.
3. Render, or MVP with `Render=true`:
   - Require a readable storyboard, non-empty scene text, and `EndSec > StartSec`.
   - For each scene, a Pollinations URL is not an image. Download it in `MaterializeSceneImageAsync`, then decode it with `SKBitmap.Decode`. Header-only is not enough. A file already in the job directory may be reused only after that same decode. Do not fetch any other host. Do not pass the URL to the renderer.
   - Voice must be a non-empty file inside the job directory. Probe it as audio and require its duration to cover the storyboard total. If ElevenLabs is absent or the file is missing or short, set `Failed` and return. Do not throw, so the work queue does not retry a missing key. Do not set `Ready`. Do not synthesize a substitute.
   - Set `Rendering` and clear `output_url`.
   - If `output.mp4` exists, rename it to `output.previous.mp4` before `RenderAsync`. The renderer would otherwise keep the old file or refuse to overwrite.
   - Call the existing `MarketingLocalVideoRenderer` and `ProcessMarketingVideoFfmpeg`.
   - After it returns, a playable job requires all of: `rendered.Ok`, file at the path `GET video/media` will open, `ffprobe` video H.264 at the template frame size (`9:16` is 1080×1920) plus an audio stream, container duration not more than one frame at 30 fps under the storyboard total, and `ffmpeg` decode of video and audio exiting 0. `IsPlayableFile` may stay as a cheap reject. It must not be the success condition.
   - Only then set `Ready`, `rendered_at`, and `output_url` to this job's `output.mp4` route. Delete `output.previous.mp4` after that success.
4. On failure, set `Failed`, `output_url` null, `rendered_at` null, and the reason. Delete a new `output.mp4` that failed the probe so the next attempt is not blocked. Leave `output.previous.mp4` on disk and do not advertise it.
5. `ApproveAsync` for this gate rejects unless the current `output_url` is this job's media route and the file still passes the same probe. Other brands approve as they do now.
6. The MVP catch must not, for this gate, write back an older `output_url`. Keep the storyboard that was just built.

`GET video/media` must resolve the same directory as `ResolveVideoAssetRoot`. Default `App_Data/content-video` stays valid.

UI, this gate only:

- The list tag `Sẵn sàng` is shown only when `playableMarketingVideoUrl` is this job's `output.mp4` route.
- `PreparingRender` with a storyboard and no playable route is shown as storyboard, not as a video.
- The `<video>` element uses `playableMarketingVideoUrl`, not a raw `outputUrl`.
- Approve stays disabled unless status is `Ready` or `Approved`. After this change a storyboard cannot be approved on this gate.
- Other brands keep their current labels and Creatomate player behavior.

Do not pad short audio, do not add a TTS or image provider, and do not post to TikTok.

## D. Acceptance criteria

Tests must not open a production database, must not call Pollinations, ElevenLabs, or Creatomate, and must not update existing NOVIXA jobs. Put the decision in a pure function next to the service so these cases do not need PostgreSQL. The service calls that function. Renderer unit tests that use a fake `ftyp` file stay as renderer tests; they are not this acceptance.

1. Complete input. Five decoded images and a voice at least as long as the storyboard are inside a new job directory. The function calls the renderer once. After a real decode probe, status is `Ready`, `output_url` ends with `/api/content/video/media/{id}/output.mp4`, `error_message` is null. The fixture MP4 under `tests/KitPlatform.Content.Tests/fixtures/novixa-local-render-003/job/output.mp4` can be the probe sample. It must not be the only proof of the pipeline.
2. Missing image. One scene has no verified local image. Status `Failed`, `output_url` null, error names that beat. Renderer is not called. Status is not `Ready`.
3. Missing voice. No `voice.mp3`, or empty file, or audio shorter than the storyboard. Status `Failed`, `output_url` null, error names the voice. Renderer is not called. `voice=skipped` is not a success.
4. FFmpeg failure. Non-zero exit or ffmpeg missing. Status `Failed`, stderr or the missing-binary text is stored, `output_url` null. No `output.mp4` remains that would make the next call refuse to overwrite.
5. Undecodable MP4. A file of at least 1024 bytes with an `ftyp` header and no decodable streams. Status `Failed`, not `Ready`. The bad `output.mp4` is removed.
6. Retry after failure. Case 4 or 5, then a second attempt with the fixture inputs. The renderer is invoked again. Success follows case 1. A previous good file renamed to `output.previous.mp4` is not copied into `output_url` when the new attempt fails.
7. Other brand, other template, or provider `creatomate`. The local renderer is not called. `PrepareStoryboardAsync` still sets `Ready` for those jobs. Creatomate status mapping in `ContentVideoRenderStatus` is unchanged.
8. API and UI. A job with storyboard JSON, status `PreparingRender`, and null `output_url` is not presented as a playable video. A job with `Ready` and this job's `output.mp4` route is. A `Ready` job whose `output_url` is an image or an unrelated URL does not mount a `<video>` for this gate. `GET video/media/{id}/output.mp4` and the renderer directory are the same path.

## E. Change scope

### Functions

- `ContentVideoService.PrepareStoryboardAsync` — NOVIXA gate writes `PreparingRender` and clears `output_url` and `rendered_at`.
- `ContentVideoService.RunMvpPipelineAsync` — NOVIXA gate does not set `Ready` when render did not produce a probed MP4. Missing voice on a render request returns `Failed` without throwing. The catch must not restore an old `output_url` for this gate.
- `ContentVideoService.RenderNovixaLocalAsync` — rename old MP4, probe after render, set `Ready` only after decode, clean a bad new file.
- `ContentVideoService.MaterializeSceneImageAsync` — Skia-decode after the header check. Reuse a file in the job directory only if that decode succeeds.
- `ContentVideoService.ResolveJobVoicePath` — require a real audio duration that covers the storyboard before render.
- `ContentVideoService.ApproveAsync` — NOVIXA gate requires the same probe.
- `ContentVideoService.IsJobOutputRoute` and `GET video/media` — one shared directory with `ResolveVideoAssetRoot`.
- `ContentVideosPage.tsx` — player and the NOVIXA list tag follow section C. No change to Famixa or Video Product screens.
- A pure decision helper in the Content infrastructure assembly, called by the service. Not a new renderer and not a new `IContentVideoService` method unless the controller needs one function to resolve the media file.

Comments on `IContentVideoService.PrepareStoryboardAsync` and `QueueRenderAsync` currently say local preparation is `Ready`. Update those two comments so they match the gate. Do not change Creatomate contracts.

### Tests to add

- `tests/KitPlatform.Content.Tests/NovixaLocalRenderIntegrationTests.cs` for cases 1–7, using temp directories and the existing fixture files.
- A small admin smoke for `playableMarketingVideoUrl` and the NOVIXA label rule, case 8. Do not drive a live pipeline.

Existing `MarketingLocalVideoRendererTests` and `ContentVideoRenderStatusTests` stay. `NovixaLocalRenderFixture003Tests` stays an opted-in renderer smoke. It does not satisfy cases 1–8.

### Migration

None. `Draft`, `GeneratingScript`, `GeneratingAssets`, `GeneratingVoice`, `PreparingRender`, `Queued`, `Rendering`, `Ready`, `Failed`, and `Approved` are already in `ck_video_job_status`. `output_url`, `error_message`, `storyboard_json`, `config_json`, and `rendered_at` already exist. Do not backfill existing rows.

### Data and status risks

- Existing NOVIXA `Ready` rows that have no media stay `Ready` until an operator opens them. The UI rule hides a playable player when the route is absent. Do not rewrite those rows in this change.
- A new render renames a previous `output.mp4` to `output.previous.mp4` and clears `output_url` at start. A failed retry leaves the previous file on disk but the API no longer points at it.
- A process kill after `Rendering` is written can leave `Rendering`. The next explicit render is allowed. There is no new sweeper.
- Template `tiktok_45s_hooks` is 45 seconds. A voice shorter than that storyboard fails closed. Do not pad it in this change.
- Renderer concat can be one frame short of the requested duration. The probe allows that one frame and no more.
- Client render wait is 180 seconds and MVP wait is 300 seconds. The renderer timeout for a 45 second storyboard is 90 seconds, plus image downloads. A slow run can still finish after the UI stops waiting. The job row is the source of truth, not the toast.

### Local verification, without production

- `dotnet test` filtered to `NovixaLocalRenderIntegrationTests` and the existing renderer tests. No connection string.
- Do not call `RunMvpPipelineAsync` or `QueueRenderAsync` against a shared database in that test.
- Do not render the four existing NOVIXA jobs. Use a new temp directory.
- Do not deploy, do not run migrations, and do not post the video.

## Out of scope

Famixa, Video Product, Creatomate behavior for other templates, new image or voice providers, audio padding, automatic publish, and any rewrite of historical `video_job` rows.
