# VIDEO-PRODUCTION-E2E-001 — Local Final video

Date: 2026-10-16  
Scope: FAMIXA_PRODUCTION tenant, local build `30ba4061-a443-4c0d-85aa-1d13ff4fdedb` (EP01).  
Historical official film `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` was not written. No paid provider calls.

## Blocker

Shot assemblies for EP01-SC01-SH01 and EP01-SC01-SH02 already existed and were verified. Episode concat did not run.

Root cause: `ASSEMBLE_EPISODE` was implemented on `ProductionCreateVideoService`, but the API composition root never registered the local FFmpeg adapters, the shot catalog never offered the action, and the Director refused every non-TEST post. The desk therefore could not produce the episode file.

## Delivery

- Register unpaid `IFfmpegMixTransport`, mix, shot assembly, and episode assembly adapters in `ContentPackDependencyInjection`.
- Offer `ASSEMBLE_EPISODE` on a tenant-owned EP01 desk when SH01 and SH02 `CURRENT:ASSEMBLY` equals `VISIBLE:ASSEMBLY` and both artifacts are verified. Official film builds stay closed.
- Director `Lắp tập phim` posts `ASSEMBLE_EPISODE` with those artifact ids, in that order, on scope shot `EP01`.
- Client production request/state helpers the desk already called are now on `content.api.ts`.

## Produced file

Request `80bc6c6f-fb4a-4935-a45a-04954d95599f` COMPLETED.  
Task `6decf5b6-dbe7-41b0-bcfb-9dddb74aff92` SUCCEEDED.  
Attempt `6378d6d4-c990-4d67-9279-225982b64bb8`. Provider `server` / `ffmpeg`.  
Artifact `b9fed1d7-50c2-42bd-ba92-ecae40726d3f` role `ASSEMBLY`, VERIFIED, 13.042s.  
SHA `0dbcdaa0e6f6a299203a769b315f24a2aa2e3331c66d71136ad056e83b9b5002`.  
File `src/KitPlatform.Api/App_Data/kit-video-i2v/4028941b49b34ba28815778daef4a2d3/os/6378d6d4c9904d679279225982b64bb8.mp4` (2,972,085 bytes).  
ffprobe: H.264 1280x720, AAC 48000 stereo, start 0, container 13.042s. Full decode succeeded.  
Parents, in order: SH01 assembly `03eae75f-8320-4e9c-9eaf-1442a5661f3d` (5.042s), SH02 assembly `4f1e8081-55e5-4d16-b65c-5c3bb8090ead` (8s).  
Bindings on shot `EP01`: `CURRENT:ASSEMBLY`, `VISIBLE`, `VISIBLE:ASSEMBLY` = this artifact.  
Replay of the same POST returned the same request, task, attempt, and artifact.

Preview: `GET /api/content/production/shots/30ba4061-a443-4c0d-85aa-1d13ff4fdedb/EP01/preview/media` returned `video/mp4` with the same SHA.

## Checks

- `dotnet build` KitPlatform.Api: succeeded.
- `famixa-production-os-ui-cutover.smoke.ts`: PASS.
- Browser click of `Lắp tập phim` was not exercised. The catalog GET and the POST that button sends were exercised on the local API.
- Full admin `tsc` was not run.

## Limits

The episode contract accepts exactly SH01 then SH02. SH03 still has a running `CREATE_IMAGE` and is not in this file. There is no separate `FINAL` artifact role; the playable output is the episode `ASSEMBLY` artifact. Official film ids remain blocked. This run did not call Runway, Fal, ElevenLabs, Gemini, or Veo.

## Next action

Open the local Director on this build and play `Lắp tập phim` preview for shot `EP01`. Do not regenerate SH01/SH02. Decide separately whether SH03 joins a later episode contract.
