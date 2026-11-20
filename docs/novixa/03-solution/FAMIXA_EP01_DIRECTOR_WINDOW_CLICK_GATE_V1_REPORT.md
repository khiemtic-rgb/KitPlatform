# FAMIXA EP01 — director window click gate

**Date:** 2026-10-17  
**Result:** The desk can now use a stored director speech window for the first lip-sync and the first mix. The outcome reader uses that window and the frozen mix spans.  
**Not claimed:** a provider call, a new production asset, a shot accept, or an assembled episode.

Baseline: `FAMIXA_LOCAL_LIPSYNC_TIMELINE_FIX_V1_REPORT.md`. That trim, the tenant EP01 Fal path, and the official SH01/SH02 locks were left as they were.

## Defect

Two gates ignored a window the director had already stored.

1. `CREATE_LIPSYNC` and `MIX_VIDEO` read `shot.timing`. That object is the computed take clock (`source` is not `DIRECTOR`). `approvedDialogueIntervals` correctly rejects it, so an 8 second picture and a 2.5 second line stayed `SPEECH_DOES_NOT_COVER_PLAYBACK` after the director had saved both speech ends on the Production OS snapshot.
2. Passing that stored window straight into `productionClockBlockers` then failed `DIALOGUE_WINDOW_MISMATCH`, because the click required mix spans. Those spans do not exist until the mix runs.

The server lip-sync trim and `ProductionMixContract.Build` already place a valid window. The click never reached them.

## What changed

| File | Why |
|---|---|
| `ContentFamixaSeriesTab.tsx` | The lip-sync and mix click reads `osDirectorTimingByShot`, not the computed take timing. Voice duration falls back to the OS voice artifact when the graph freeze has none. |
| `famixa-short-quality-pipeline.ts` | Before a mix exists, the click checks the window against the voice, the measured lip-sync, and the picture. It does not invent spans. A mix timeline that is actually supplied is still checked. An 8s / 2.5s case with no window still fails. |
| `FamixaDirectorTimingPicker.tsx` | **Lưu khoảng thoại** stores both ends on the already chosen 5 or 10 second picture. The duration buttons still do not create a window by themselves. |
| `ShotProductionOutcomeV1.cs` | A director `PerformanceSourceRef` is the only window. Mix spans are read from the frozen `MIX_CONTRACT_V1` token. A placement with no director decision does not become a window. |
| `ShotProductionOutcomeV1Regression.cs` | Isolated proof of the three cases below. |
| `famixa-short-quality-pipeline.smoke.ts` | Click cases for a legal window, a window past the picture, and speech left at zero. |
| `famixa-director-timing-ui.smoke.ts` | The save control and the click source stay wired. |

`ContentFalClient.cs`, `ProductionMixContract.cs`, the 0.75 second tolerance, `VeoMotionDurationMapping`, official SH01/SH02 constants, and episode assembly callers were not edited.

## Tests

No provider HTTP. No Production DB write.

| Command | Result |
|---|---|
| `dotnet test tests\KitPlatform.Content.Tests\KitPlatform.Content.Tests.csproj --filter FullyQualifiedName~ShotProductionOutcomeV1Tests` | Passed. 1 test, 76 ms. The suite includes T42–T47. |
| `npx tsx src/modules/content/famixa-short-quality-pipeline.smoke.ts` from `client/admin` | `FAMIXA_SHORT_QUALITY_PIPELINE_V1 PASS FAIL=0 (no provider)` |
| `npx tsx src/modules/content/famixa-director-timing-ui.smoke.ts` from `client/admin` | `FAMIXA_DIRECTOR_TIMING_UI_V1 ok` |

Inside the outcome suite, on an isolated build that is not `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`:

- Director 2.0–4.5, voice 2.5, lip-sync 2.5, picture 8, and a frozen mix contract placed at 2.0: no `SPEECH_DOES_NOT_COVER_PLAYBACK`, no `DIALOGUE_WINDOW_MISMATCH`, no `MIX_CLOCK_SPLIT`. `ProductionReady` stays false. `AutomatedVisualProof` stays false.
- The same mix, with no director source, still fails the undeclared 8s / 2.5s clock.
- A computed 0.2–2.7 lead-in is not a window.
- A director window whose mix snapshot has no contract stays `DIALOGUE_WINDOW_MISMATCH`.

The running API was not restarted. Nothing on `api/content/production` calls `ShotProductionOutcomeV1.ReadAsync`. The click change is in the admin client. The lip-sync host from the previous report was left running.

The picker was not opened in a browser session. The smoke asserts the save control and the click source in the series tab.

## Shot matrix

No shot was watched or accepted in this pass. Official SH01 and SH02 rows were not read or written.

| Shot | Class | Evidence used |
|---|---|---|
| EP01-SC01-SH01 | Official asset locked. Awaiting a human review that this pass did not record. | Prior OS mix. Not mutated. |
| EP01-SC01-SH02 | Requires a new tenant-owned pilot and paid-generation approval. The official mix is historical reference, not an accepted deliverable. | Line `Dạ… nay con làm bài không tốt.` Picture clocks 8s / speech about 2.5s fail until a person stores both speech ends. |
| EP01-SC01-SH03 | Blocked by the locked line. | Authoritative text is still `Điểm thấp thì lần sau gỡ...`. Voice stays refused. The missing words were not written. |
| EP01-SC01-SH04 | Legacy file, not an OS accept. | Prior note: action sentence incomplete. Not promoted. |
| EP01-SC01-SH05 | Legacy file, not an OS accept. | Prior note only. Not promoted. |
| EP01-SC01-SH06 | Legacy reference. Pixels were not fetched in this pass. | Not promoted. |

## Boundaries kept

- No Fal, Veo, Gemini, or ElevenLabs call.
- No production database write, migration, or `tenant_id` backfill.
- No change to official SH01/SH02 assets or their acceptance state.
- No episode assembly. ADR-08 caller policy stays open.
- No Core type and no second pipeline.

## Blocking decision

One pilot, before any other shot is generated.

Approve or refuse this exact attempt:

- Tenant: the signed-in Content tenant that already owns Production OS credentials. Not a backfill of official build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`.
- Build: a new tenant-owned EP01 build. Creating it is a `pack_content` write and is part of this approval. Official NULL-tenant rows stay untouched.
- Shot: `EP01-SC01-SH02` only, copied as the approved contract (red score, bag, the locked line above).
- Sources: no official SH02 take, voice, or mix is reused. Those rows are unattributed. This pass has no tenant-owned still, take, or voice id to name.
- Speech window: the director types both `speechStartSec` and `speechEndSec` on the 5 or 10 second picture control, then **Lưu khoảng thoại**. Code will not choose the numbers. The window has to sit inside the generated take, and the measured line has to match that window within the existing 0.75 seconds.
- Operations, one each, no automatic retry: still (Gemini amount unknown in the local catalog), `google` / `veo-3.1` video-only 720p at the catalog **0.20 USD** known count, `elevenlabs` / `eleven_v3` (local amount **unknown**), `fal` / `sync-lipsync-v3` (local estimate **0.67 USD** at the 5 second minimum for a line near 2.5 seconds), then the local FFmpeg mix.
- Known uncertainty: the Veo figure is the 2026-09-22 catalog row, not an invoice. ElevenLabs has no local amount. The Fal figure is an estimate, not a charge.
- Cap: one attempt of each paid stage. A failure stores the failed attempt and does not submit the same fingerprint again.
- Storage: Production OS artifacts for that new build, tenant-prefixed keys.
- Reviewer: the person who approves this attempt watches the mix and writes ACCEPT or REJECT against that artifact id and SHA-256.
- Episode assembly is not included. ADR-08 is still open, and the other five shots are not accepted.

Provider cost of this code pass: $0.
