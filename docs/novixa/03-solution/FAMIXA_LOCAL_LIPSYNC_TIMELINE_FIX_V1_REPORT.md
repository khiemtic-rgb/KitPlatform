# FAMIXA local lip-sync and timeline fix

**Date:** 2026-10-17  
**Result:** PASS for the tenant EP01 Fal path, the regression suite, the isolated FFmpeg timeline, and the rebuilt local API.  
**Not claimed:** an official-film lipsync rerun, a provider call, a new production asset, or an accepted shot.

## What changed

Tenant-owned EP01 `CreateLipsyncAsync` now reads `ProductionDirectorTiming.ApprovedSpeech` before Fal.

- Both `speechStartSec` and `speechEndSec` are required. `ApprovedSpeech` already rejects a missing end, a negative start, and `end <= start`. Decimal values have no NaN or infinity.
- The interval must sit inside the source take duration. A take duration of 0, or an end past that duration, is `INVALID_RANGE`.
- A missing interval is `MISSING_TIMELINE`. The method returns before `OpenFrozen`, before permit consume, and before the Fal transport.
- A valid partial interval is trimmed with local FFmpeg into a temp directory (`famixa-lipsync-src-{guid}`). The temp directory is deleted after the bytes are in memory. The stored take is read twice and its SHA-256 must match; the method does not write that storage key.
- An interval that is exactly the whole take returns the original bytes and does not re-encode.
- The trimmed bytes are the `video_url` payload. The voice bytes and the adapter `sync_mode` are unchanged. `ContentFalClient` has no new start offset.
- The transport duration passed with those bytes is `end - start`. The frozen snapshot in the store still records the take duration.
- Official film scopes are unchanged. They keep the existing full-take send. A speech window is not invented for SH01, SH02, or the official SH99 regression.

## Files

| File | Why |
|---|---|
| `ProductionCreateVideoService.Actions.cs` | Gate, trim, and the bytes handed to `FalLipSyncExecutionAdapter` |
| `ProductionLipsyncSpeechSource.cs` | Interval check and the local FFmpeg trim |
| `ProductionLipsyncSpeechSourceV1Regression.cs` | Fake-provider regressions and the fixture timeline |
| `ProductionLipsyncSpeechSourceV1Tests.cs` | xUnit entry |
| `ProductionTenantEp01LipsyncV1Regression.cs` | Existing success path now stores speech `0..take` so the whole-take send stays explicit |
| `VideoGenerationExecutionService.cs` | Registers the new suite beside the mix-contract suite |

`ContentFalClient.cs` and `ProductionMixContract.cs` were not edited. The mix already places a lipsync clip at `speechStart`.

## Local API

| | Before | After |
|---|---|---|
| Launch | `dotnet run --project src\KitPlatform.Api\KitPlatform.Api.csproj --no-build --launch-profile https` | Same command, after a successful build |
| dotnet PID | 14904, started 13:37:12 | 20528 |
| API PID | 22936, `bin\Debug\net10.0\KitPlatform.Api.exe` | 11404, same executable path |
| Listen | 127.0.0.1 and ::1 on 5290 and 7224 | Same |

The dev watcher `scripts\watch-api-dev.ps1` (PID 11060) restarted the old `--no-build` process as dotnet 14408 / API 1188 after the first stop. That pair, and the watcher, were stopped after their command lines were checked. `Ensure-ApiDev` then built and started the host. Watcher PID is now 13448.

Build, from `scripts\api-dev.ps1` → `dotnet build src\KitPlatform.Api\KitPlatform.Api.csproj`: **0 errors**, 66 existing warnings, 12.60s.

Host DLL:

`E:\KitPlatform\src\KitPlatform.Api\bin\Debug\net10.0\KitPlatform.Packs.Content.Application.dll`  
Length 7,977,472. Written 2026-10-17 16:57:19. Same length and timestamp as the project output. UTF-16 token `famixa-lipsync-src-` is at byte offset 7,386,669.

Startup log (`.dev\api.log`): `Now listening on: https://localhost:7224` and `http://localhost:5290`. `GET http://localhost:5290/api/health` returned `{"status":"ok"}`.

The previous host DLL timestamp was 13:37:02, length 7,917,568.

## Tests

`dotnet test tests\KitPlatform.Content.Tests\KitPlatform.Content.Tests.csproj` with the filter below. Fake `FakeFalLipsyncTransport` only. No Fal HTTP.

| Test | Result |
|---|---|
| `ProductionLipsyncSpeechSourceV1Tests.Lipsync_uses_the_approved_speech_interval_and_the_mix_places_it` | PASS (947 ms) |
| `ProductionPipelineV1Tests.Tenant_ep01_lipsync_v1` | PASS |
| `ProductionOfficialSh02CreateLipsyncV1Tests.Official_sh02_create_lipsync_is_offline_safe` | PASS |
| `ProductionEp01Sh01RealFilmRunV1LipsyncTests.Real_film_run_v1_lipsync_recover_does_not_start_again` | PASS |

Inside the new suite:

1. No speech interval, and a start without an end → `MISSING_TIMELINE`, provider start count 0.
2. Speech past the take → `INVALID_RANGE`, provider start count 0.
3. Approved 2.0–4.5 on an 8.0s take → one provider call. Sent video SHA `8df7d9ed58ea8850453373c4de3f9bf9236bcda59f51b01aa533f073501adfa9`, different from the take SHA `ba6d8ea3a8f0170551f46038fb797f2d02fa18b73c254a1f3d51e734de52b56e`. Voice SHA unchanged. `SyncMode` stayed null, which is the adapter contract outside official SH02.
4. Stored take SHA unchanged after the call.
5. A container whose sample bytes are corrupted → `EXECUTION_PROCESS_FAILED`, provider start count 0.
6. Mix contract places the lipsync picture at 2.0–4.5 with source 0–2.5. The 0–2.0 picture is the video take, not the lipsync.

## FFmpeg timeline

Fixture only, under `.dev\famixa-lipsync-timeline-proof\`. Not an SH02 asset.

Take: 0–2s red, 2–4.5s green, 4.5–8s blue, silent stereo AAC, 160×90, 10 fps.

Independent probe after the test:

| File | ffprobe duration | Frame samples |
|---|---|---|
| `take.mp4` | 8.000000 | 0.3s red (253,0,0); 2.3s green (0,127,0); 6.0s blue (0,0,254) |
| `speech.mp4` (bytes given to the fake provider) | 2.500000 | 0.2s, 1.2s, and 2.2s all green (0,127,0). The file does not start on red |
| `mix.mp4` | 8.000000 | 0.3s red; 2.3s green; 6.0s blue |

The mix command is the existing `FfmpegMixComposeGraph` for that contract: take head 0–2, trimmed clip 0–2.5 placed at 2–4.5, take tail 4.5–8, silence outside the speech, lipsync audio on the speech. Contract output is 8. The speech slot is green, which is the approved window, not the red head of the take.

## Boundaries kept

- No Fal or other paid provider call.
- No production database write, migration, or `tenant_id` backfill.
- No production deploy.
- No edit to official SH02 mix, source, review, or acceptance constants. Official lipsync regressions stayed green.
- No shot or episode acceptance.
- No Core type, second pipeline, tolerance change, or credential-policy change.
- No episode assembly.

## Next smallest step

Store a director speech window on the tenant EP01 shot that will be the pilot, then run `CreateLipsync` on that shot. This tree will trim that window before Fal. A human still has to accept the result. Official film builds are outside this gate until their locked tests are given an explicit window of their own.
