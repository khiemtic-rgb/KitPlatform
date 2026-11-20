# FAMIXA CURRENT STATE — FORENSIC RECONCILIATION V1

**Date:** 2026-10-17  
**Mode:** Read-only. No source edit except this new file. No migration, no provider call, no POST, no credential or media change.  
**Git HEAD observed:** `8c59ceb4` (`fix(content): treat a video without a file as failed`). The working tree already contained uncommitted Famixa changes before this pass. Those files were not reverted.  
**Runtime read:** one local PostgreSQL database `kitplatform` on `localhost:5432`, inside `BEGIN READ ONLY` … `ROLLBACK`. This is not the VPS database. Queries selected identities, statuses, durations, sizes, hosts, and failure text. Secret columns were not selected.

Evidence grades used below: `CONFIRMED`, `PARTIALLY_CONFIRMED`, `HYPOTHESIS`, `UNKNOWN`, `CONTRADICTORY_EVIDENCE`.

---

## Direct answers

**Famixa has already produced real provider media, and also a large amount of local FFmpeg and imported copies.** On the official NULL-tenant build, Production OS holds a succeeded Runway take, two ElevenLabs voices, a Veo take, Fal lip-sync files, and FFmpeg mixes. Older series graphs hold 11 remote takes each on a 24-shot plan and an 11-shot plan. A later tenant-owned build holds a 13.042 second episode assembly of two shots.

**What still exists on this machine is the local files under `App_Data/kit-video-i2v`, the local rows, and the current source.** Official playback through a tenant-scoped API is a separate question: those official rows have `tenant_id` NULL. The largest local file found is about 6.1 MB. No local multi-minute master file was identified.

**Confirmed defects are asset selection, clock mismatch, stuck tasks, and a local credential gap.** A fixture lip-sync of 808 bytes is the current lip-sync on the tenant SH01. Official SH02 speech is about 2.5 seconds on an 8 second picture, and prior reviews recorded a missed action and an open mouth on the silent tail. Three official Fal lip-sync tasks are still `RUNNING` since 2026-09-23. Local tenant `KIT_MKT` has zero production credentials and zero production tasks. That last fact is local-database evidence. It is not a VPS observation, and it is not by itself the cause of every earlier quality failure.

**Changes that still need investigation** are the uncommitted 2026-10-17 lip-sync trim, the DryRun Runway registration beside historical live Runway HTTP errors, and the difference between this local database and the VPS where credential migrations were still blocked on 2026-10-15.

**Next step:** do not generate and do not rewrite the pipeline. Decide which database and which tenant a later attempt is allowed to use. On this local database the only production credential is `FAMIXA_PRODUCTION` / `runway` / `gen4_turbo`. `KIT_MKT` cannot resolve one here.

---

## Scope and what was not proven

| Looked at | Result |
|---|---|
| `docs/novixa/03-solution/FAMIXA_*.md`, the 2026-10-16 episode report, and the 2026-10-17 pilot, quality, readiness, and lip-sync reports | Read |
| Local `pack_content.series_build`, `production_tasks`, `production_artifacts`, `production_bindings`, `production_credentials`, `production_credential_versions`, `production_tenant_ai_config`, `public.tenants` | Read-only |
| `App_Data/kit-video-i2v` file listing by size and timestamp | Read-only. Largest file 6,389,625 bytes |
| Current DI and the official playback-order comment | Read |
| VPS database, live vendor invoices, and a browser session of the pictures | Not done |
| Whether remote CloudFront or Fal URLs still return bytes | Not fetched. Official SH06 was previously HTTP 401 in the quality report |

An MP4 on disk is not treated as a successful generation. A fixture or offline test is not treated as a provider run. `ACCEPTED` / `ACCEPTED:MIX` in `production_bindings` is an OS binding. It is not the human visual acceptance defined in `FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md`.

---

## 1. Inventory

### 1.1 Series builds in the local database

`CONFIRMED` by `pack_content.series_build` joined to `public.tenants` on 2026-10-17.

| Build | Episode | Title | Shots in row | Videos in row | Tenant | Created | Updated | Class |
|---|---|---|---|---|---|---|---|---|
| `fca0c122-797e-4356-9bd1-2a70daac1ed4` | EP01 | Tập 01 | 24 | 11 | NULL | 2026-08-23 | 2026-09-07 | Older film graph |
| `fd760913-0eb8-46a5-b6eb-ec2990a4b0d0` | EP01 | Tập 01 | 11 | 10 | NULL | 2026-08-23 | 2026-09-07 | Older film graph |
| `77262ec0-74ad-459d-9128-f09bb553a5dd` | EP99 | TEST BATCH 3 SHORT | 3 | 0 | NULL | 2026-08-25 | 2026-09-07 | Short test graph |
| `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | EP01 | Tập 01 | 6 | 6 | NULL | 2026-09-05 | 2026-09-23 | Official 6-shot |
| `30ba4061-a443-4c0d-85aa-1d13ff4fdedb` | EP01 | Tập 01 | 6 | 0 | `FAMIXA_PRODUCTION` | 2026-09-26 | 2026-09-27 | Tenant-owned copy plus later Runway |
| `2615b2da-da9e-486b-9372-8927784df40f` | EP01 | Tập 01 | 6 | 0 | `CONTENT_EP01_SCOPE` | 2026-09-26 | 2026-09-26 | Import / scope test |
| `c1c1c1c1-…` and `e1e1e1e1-…`, `f2`–`f8` | EP01 | race / process-failure titles | 2 | 0 | `CONTENT_EP01_SCOPE` | 2026-09-25 | same day | Harness rows, not a film |

Checkpoint 1 (`FAMIXA_PRODUCTION_OS_TEST_SCOPE_FOUNDATION_CHECKPOINT_1_REPORT.md`) listed four FAMIXA builds and called `fca0c122` a 24-shot build. A later keyframe forensic called the same id an 11-shot build. The local row now says `shot_count = 24`, and `graph_json.episode.shots` has length 24. The 11-shot label for this id is contradicted by the current row. `fd760913` is the 11-shot row.

`KIT_MKT` (`11111111-1111-1111-1111-111111111105`) owns no `series_build` row. `CONFIRMED` locally.

### 1.2 The “several minutes” material

`PARTIALLY_CONFIRMED` as a shot plan and as a set of remote takes. `UNKNOWN` as one playable multi-minute master.

| Build | Graph shots | Sum of shot `seconds` | Takes with a non-empty `takeUrl` | Sum of graph `videoBytes` | Host seen |
|---|---|---|---|---|---|
| `fca0c122` | 24 | **170** (2 min 50 s of planned shot clocks) | 11 | 9,879,492 | `https://dnznrvs05pmza.cloudfront.net` |
| `fd760913` | 11 | **80** | 11 | 8,087,863 | same CloudFront host |
| `77262ec0` | 3 | 30 | 3 | 1,816,561 | counted; host not re-listed |
| `4ef164d6` | 6 | 30 | 6 | 6,510,378 | Fal `v3b.fal.media` on SH03–SH05; CloudFront on SH06; SH01/SH02 URLs were not `https://` |

Those sums are script clocks and byte counters inside `graph_json.runs`. They are not an ffprobe of one concatenated file. No Production OS task exists for `fca0c122`, `fd760913`, or `77262ec0`. The local media folder’s largest file is the 8 second historical SH02 lip-sync (6,389,625 bytes, 2026-09-23), not a multi-minute master.

On `fca0c122`, the 11 takes with URLs are SH01–SH11. Models recorded on those runs: `gen4_turbo` and `sync-lipsync-1.9`. Six of them have `lipsynced = true`. SH04 lip-sync status is `RETRY`. Status values are mostly `turbo_testing`, `keyframe_ready`, or `story_locked`. SH12–SH24 are in the shot array and in `runs`, and they are not in the take-URL set. Their pictures were not found as local OS artifacts.

`HYPOTHESIS`, not used as a finding: a person who played those 11 CloudFront takes in order would have seen on the order of one to two minutes. This pass did not download them and does not know whether the URLs still resolve.

### 1.3 Official EP01 `4ef164d6` — Production OS chain

`CONFIRMED` as rows and, where the path was checked, as files still present. Tenant on the binding and artifact is NULL. Active bindings (`unbound_at` IS NULL):

**SH01**

| Role | Artifact | Provider / model on the task | Duration | Bytes | File checked |
|---|---|---|---|---|---|
| CURRENT:KEYFRAME | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` | import / existing-pixels | still | 93,991 | `os-kf/8f9548b1b0744eb1a5b9295889960b9c.mp4` present |
| CURRENT:VIDEO_TAKE and generic CURRENT | `b22370a9-6ddd-4b04-a281-c7498cc926ae` | runway / gen4_turbo | 5.042 | 1,529,738 | `os/a1673a1e54d344d69031cec3ed6c44b3` present |
| CURRENT:VOICE | `bf8d0827-21ff-4daa-83b6-116bc3324ab0` | elevenlabs / eleven_v3 | 4.232 | 67,753 | voice store |
| CURRENT:LIPSYNC | `837ed989-5e1f-45ad-b77d-cb26f5e808a2` | fal / sync-lipsync-v3 | 5.042 | 4,312,559 | `os/2db32efb4ac84d2c929711d234b135e1` present |
| CURRENT:MIX and ACCEPTED:MIX | `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` | server / ffmpeg | 5.042 | 4,299,195 | `os/58796f8cc6b24578894bda828cc7e2c0` present |
| CURRENT:ASSEMBLY and VISIBLE | `28f8de3e-f6d3-4972-934e-703a2be45939` | server / ffmpeg | 5.042 | 4,299,195 | `os/b5b954fa45434fc5856bedf8476e948c` present |

**SH02**

| Role | Artifact | Provider / model | Duration | Bytes | File checked |
|---|---|---|---|---|---|
| CURRENT and CURRENT:KEYFRAME | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` | import / existing-pixels | still | 113,493 | `os-kf/5a1325ae8a7b4f389f496e24f483ace7.mp4` present. Prior report: JPEG bytes under an `.mp4` name |
| ACCEPTED:VIDEO_TAKE | `b44e530b-5b59-422f-9b80-e058a3a639b5` | google / veo-3.1 | 8 | 1,437,239 | `os-veo-out/5a72a533ca30466e99b0357fa47bcdd8.mp4` present |
| CURRENT:VOICE and ACCEPTED:VOICE | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` | elevenlabs / eleven_v3 | 2.534 | 40,586 | `os-voice/9bd531131c244f2b84c25c392b1fb94d.mp4` present |
| CURRENT:LIPSYNC and ACCEPTED:LIPSYNC | `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e` | fal / sync-lipsync-v3 | 2.5 | 2,073,636 | `os/833e816708624dbbba9d89a2fc3b9a4e.mp4` present |
| Historical lip-sync, not the current binding | `32bec770-9706-4aad-bc21-8db3d4f11d4c` | fal / sync-lipsync-v3 | 8 | 6,389,625 | `fal/9f1aa23b08a16a6dcefc887f4a20fad8.mp4` and `os/c30af2f5…` present. Quality-failed in CK23 / CK24B |
| CURRENT:MIX and ACCEPTED:MIX | `58297402-e252-45e8-8d33-c7414749a297` | server / ffmpeg | 8 | 1,423,715 | `os/ff28d642765942839894f89bfbeb897c.mp4` present. SHA-256 on record `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b` |
| CURRENT:ASSEMBLY and VISIBLE | `92621f9e-57eb-4185-8cea-a962ec399136` | server / ffmpeg | 8 | 1,423,715 | `os/c82d57f568274b78a5af1ba10385715c.mp4` present |

There is no active `CURRENT:VIDEO_TAKE` on official SH02. The Veo take is `ACCEPTED:VIDEO_TAKE` only. Generic `CURRENT` is the keyframe.

**SH03–SH06** have graph runs and no Production OS tasks.

| Shot | Graph model | Graph status | Lipsync | `videoBytes` | Take host |
|---|---|---|---|---|---|
| SH03 | sync-lipsync-1.9 | reviewed | true / SUCCEEDED | 1,377,087 | `https://v3b.fal.media` |
| SH04 | wan-2.1 | reviewed | false | 1,198,375 | Fal host. Graph `finalSource` is `RUNWAY`. Lineage report says that label is a legacy mislabel |
| SH05 | sync-lipsync-1.9 | turbo_testing | true / SUCCEEDED | 1,678,652 | Fal host |
| SH06 | sync-lipsync-1.9 | turbo_testing | false | 491,460 | `https://dnznrvs05pmza.cloudfront.net` |

`FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md` says SH06 pixels were not fetched (HTTP 401). The URL is still in the graph. This pass did not retry the URL. The file’s availability is `UNKNOWN`.

### 1.4 Tenant build `30ba4061` (`FAMIXA_PRODUCTION`)

`CONFIRMED` locally. Storage prefix `4028941b49b34ba28815778daef4a2d3` matches tenant id `4028941b-49b3-4ba2-8815-778daef4a2d3`.

| What | Artifact | Evidence |
|---|---|---|
| SH01 imported video | task `import` / `existing-video`, 1,529,738 bytes, 5.042 s | Same size as official Runway take `b22370a9` |
| SH01 later Runway video, and it is `CURRENT:VIDEO_TAKE` | `81b29b5c-9e5e-492f-8e40-154c43604507`, 646,555 bytes, 5.042 s, 2026-09-27 | Different bytes from the import |
| SH01 Fal lip-sync | `b58e679d-5ae1-470a-8152-efb7161d4fa5`, 4,216,881 bytes, 5.042 s, 2026-09-26 | Not the current binding |
| SH01 current lip-sync and VISIBLE | `7c20d436-cca7-4e4d-b571-345e24037b7b`, **808 bytes**, provider `fixture` / `fixture-lipsync-v1`, 2026-09-27 14:11 | Later than the Fal file |
| SH01 mix, `CURRENT:MIX` and `ACCEPTED:MIX` | `33f22b13-1021-42d3-91af-a8ac3a5d800d`, 1,548,906 bytes, 5.042 s | FFmpeg |
| SH02 imported video, and it is generic `CURRENT` and `CURRENT:VIDEO_TAKE` | `eb1072d0-aa03-4493-9dd7-db24f2123d27`, 1,437,239 bytes, 8 s | Same size as official Veo take |
| SH02 Fal lip-sync, `CURRENT:LIPSYNC` | `a40edfef-db48-44e6-9935-f2ee7febba7c`, 1,925,584 bytes, 2.5 s | Fal task succeeded |
| SH02 mix, `CURRENT:MIX` and `ACCEPTED:MIX` | `72c6eb46-0c0a-4845-b4d3-d5e573cd67ae`, 1,422,990 bytes, 8 s | Close to, not the same size as, official mix 1,423,715 |
| Episode assembly | `b9fed1d7-50c2-42bd-ba92-ecae40726d3f`, role ASSEMBLY, 13.042 s, 2,972,085 bytes, SHA `0dbcdaa0e6f6a299203a769b315f24a2aa2e3331c66d71136ad056e83b9b5002` | File path in `VIDEO-PRODUCTION-E2E-001-REPORT.md` exists: `…/4028941b49b34ba28815778daef4a2d3/os/6378d6d4c9904d679279225982b64bb8.mp4`. Created 2026-10-16 23:37. Parents named there: SH01 assembly `03eae75f-…` (5.042 s) and SH02 assembly `4f1e8081-…` (8 s) |

SH03 on this build has one `CREATE_IMAGE` task, provider `GEMINI` / `gemini-2.5-flash-image`, status `RUNNING` since 2026-09-26 22:24. No image artifact was returned for that task. SH04–SH06 have no OS tasks on this build.

The episode file is 13.042 seconds. It is not a several-minute film.

### 1.5 Other local media

`CONFIRMED` as files. Identity is only where a report or a storage key already ties them.

- Runway TEST sandbox files under `os-test/aaaaaaaa-0000-…`, 1,029,598 bytes (2026-09-18) and 1,249,856 bytes (2026-09-19). Reports: Checkpoint 4A artifact `63bca29a-f899-4008-a1bf-6000787bafd6` and V2 artifact `5872d0a9-0c3f-42b0-9fe5-21f1433ab831`.
- Legacy Veo and Fal folders from 2026-09-11 through 2026-09-17, including Take 55 `veo/d006770ca0f75a00fb….mp4` (541,721 bytes) and Take 56 `veo/7e8e90b4….mp4` (836,517 bytes). The 6 second preview forensic says Fal used Take 56 while the desk claimed Take 55.
- Many duplicate copies under the tenant prefix and under `aaaaaaaaaaaaaaaaaaaaaaaa…` dated 2026-09-25 and 2026-09-26. Several sizes match official artifacts exactly (1,529,738; 1,437,239). Those copies are not a second generation by themselves.

---

## 2. Historical execution map

### 2.1 Real provider calls with a named report

`CONFIRMED` where the report says the call was live and the local task row still says `SUCCEEDED` for the same provider and shot.

| When | Build / shot | Action | Provider / model | Result kept | Report |
|---|---|---|---|---|---|
| 2026-09-18 | TEST `bbbbbbbb-…0371` / `TEST-SC01-SH01` | CREATE_VIDEO | runway / gen4_turbo | SUCCEEDED, 5.042 s, artifact `63bca29a-…` | Checkpoint 4A. “ONE REAL RUNWAY VIDEO” |
| 2026-09-19 | same TEST scope | CREATE_VIDEO | runway / gen4_turbo | SUCCEEDED, artifact `5872d0a9-…` | Real Runway Test Execution V2. Start calls = 1 |
| 2026-09-20 | official SH01 | CREATE_VIDEO | runway / gen4_turbo | SUCCEEDED, artifact `b22370a9-…`, task still SUCCEEDED | Real Film Run V2. “Exactly one real Runway Start” |
| 2026-09-21 | official SH01 | CREATE_VOICE | elevenlabs / eleven_v3 | SUCCEEDED, artifact `bf8d0827-…`, 4.232 s | Real Film Run V2 voice. “exactly one ElevenLabs generation” |
| 2026-09-21 | official SH01 | CREATE_LIPSYNC | fal / sync-lipsync-v3 | Start report FAILED identity; recover PASS, artifact `837ed989-…` | Lip-sync report plus recover report. Fal Start stayed 1 |
| 2026-09-23 | official SH02 | CREATE_VIDEO | google / veo-3.1 | SUCCEEDED, artifact `b44e530b-…` | CK18 Start, CK19 Poll. Catalog cost stated as USD 0.20. Not an invoice |
| 2026-09-23 | official SH02 | CREATE_VOICE | elevenlabs / eleven_v3 | Task SUCCEEDED, artifact `0fde86c2-…` | `PARTIALLY_CONFIRMED` as a live job. The row and the file exist. A dedicated CK22 live report with an ElevenLabs job id was not found. Nested regressions use a fake transport |
| 2026-09-23 | official SH02 | CREATE_LIPSYNC | fal / sync-lipsync-v3 | Two SUCCEEDED artifacts: 8 s `32bec770-…` then 2.5 s `b0b4f0f2-…`. Three other Fal tasks on this shot are still `RUNNING` with an empty failure reason, created 15:29, 15:33, and 15:43 | CK23 and CK23B |
| 2026-09-16 | official SH01, graph/desk, not the OS canon above | CREATE_LIPSYNC | Fal v3 | File `fal/16940abf….mp4` exists. Report: experimental, wrong speaker | SH01 LipSync V2 validated real run |
| 2026-09-26 | `30ba4061` SH01 and SH02 | CREATE_LIPSYNC | fal / sync-lipsync-v3 | Both tasks SUCCEEDED. SH02’s result is still current. SH01’s result is not | Local tasks. This pass did not re-read a Fal receipt beyond `provider_id` |
| 2026-09-27 | `30ba4061` SH01 | CREATE_VIDEO | runway / gen4_turbo | Four FAILED, then one SUCCEEDED (`81b29b5c-…`) | Failure text is in §4 |

### 2.2 Local FFmpeg, not an AI provider

`CONFIRMED`.

| When | What | Result |
|---|---|---|
| 2026-09-21 | Official SH01 MIX and ASSEMBLE | Artifacts `2f88e0a0-…` and `28f8de3e-…` |
| 2026-09-23 / 09-24 | Official SH02 MIX and ASSEMBLE | Artifacts `58297402-…` and `92621f9e-…`. CK24D and CK26 say provider calls on those executes were 0 |
| 2026-09-26 | Tenant SH01 and SH02 mix and assemble | Artifacts listed in §1.4 |
| 2026-10-16 | Tenant `ASSEMBLE_EPISODE` | Artifact `b9fed1d7-…`, 13.042 s. Report says no Runway, Fal, ElevenLabs, Gemini, or Veo on that run |

### 2.3 Explicit non-runs

`CONFIRMED` as “did not call”.

- Real Runway Test Execution V1: Start calls = 0, blocked `MISSING_PERFORMANCE_SNAPSHOT`.
- Real Film V1 SH01 video and voice: reports say Runway and ElevenLabs were not called. V2 is the later paid run.
- Full Film Vertical Run V1: fixture adapters, paid generation = 0.
- 2026-10-17 readiness, quality, outcome, local lip-sync trim, and click-gate reports: no provider HTTP. Fake Fal transport and local FFmpeg fixtures only (`FAMIXA_LOCAL_LIPSYNC_TIMELINE_FIX_V1_REPORT.md`, `FAMIXA_EP01_DIRECTOR_WINDOW_CLICK_GATE_V1_REPORT.md`).

### 2.4 Older films

`PARTIALLY_CONFIRMED`. The graph records `gen4_turbo` and `sync-lipsync-1.9`, CloudFront take URLs, `runwayAttempts` / `runwayBilled` keys, and lip-sync statuses. There is no `production_tasks` row, no OS artifact id, and no local file this pass could tie to those URLs. Calling them successful provider jobs would go past the evidence. Calling them absent would also go past the evidence: the URLs and byte counters are still stored.

### 2.5 Gemini

`PARTIALLY_CONFIRMED` as a stuck task only. `CREATE_IMAGE` `GEMINI` / `gemini-2.5-flash-image` on `30ba4061` SH03 is `RUNNING` since 2026-09-26 and has no artifact in the join used here. No Production OS paid Gemini still with a job id and a verified file was found for the official film. Historical keyframes on the official build are `import` / `existing-pixels`.

---

## 3. Current state matrix

### A. Product and media

| Piece | State | Grade | Basis |
|---|---|---|---|
| Official SH01 OS chain through mix and per-shot assembly | Rows and files exist. Generic CURRENT is the Runway take, not the mix | CONFIRMED | Bindings and file listing |
| Official SH02 OS chain through mix and per-shot assembly | Rows and files exist. Generic CURRENT is the keyframe. No active CURRENT:VIDEO_TAKE | CONFIRMED | Bindings |
| Official SH03–SH05 | Legacy graph takes. Not OS tasks | CONFIRMED | Graph hosts; zero tasks |
| Official SH06 | Graph URL remains. Pixels not re-fetched. Prior fetch 401 | PARTIALLY_CONFIRMED | Graph plus quality report |
| Older 24-shot and 11-shot builds | Plans of 170 s and 80 s. 11 remote takes each. No OS chain. Local master not found | PARTIALLY_CONFIRMED | `series_build` and `graph_json` |
| Tenant episode file | 13.042 s assembly, verified, file present, bound CURRENT/VISIBLE on shot `EP01` | CONFIRMED | Artifact, binding, `Test-Path` |
| Human visual acceptance of any shot or of the episode | Not recorded by the 2026-10-17 quality contract. OS `ACCEPTED:MIX` rows exist anyway | CONTRADICTORY_EVIDENCE | Quality report versus bindings. See §6 |
| Official preview for a signed-in tenant | Code refuses NULL-tenant builds (`ProductionBuildTenantGate`, `PRODUCTION_TENANT_REQUIRED` / unattributed). Readiness report says the tenant store will not serve `58297402-…` | CONFIRMED as code and as the prior report. Not re-executed as HTTP this pass | `ProductionTenantBoundary.cs`; readiness §1 |

Playback order in source, if that bridge is the one the desk calls:

```7:8:src/Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialVideoAuthorityBridgeV1.cs
/// Playback order: verified CURRENT:MIX, then verified CURRENT:LIPSYNC, then ACCEPTED:VIDEO_TAKE,
/// then verified CURRENT:ASSEMBLY / VISIBLE:ASSEMBLY, then VISIBLE video, then CURRENT:VIDEO_TAKE.
```

On official SH02 and on tenant SH01/SH02, a verified `CURRENT:MIX` exists, so this order would select the mix and would not select the 808-byte fixture. That is what the code says. This pass did not open the desk. Whether the running UI uses this bridge is `PARTIALLY_CONFIRMED`.

### B. Execution

| Piece | State | Grade | Basis |
|---|---|---|---|
| Production OS kernel (request, task, artifact, binding) | Present and populated | CONFIRMED | 119 requests, 122 tasks, 92 artifacts locally |
| Motion DI | `RunwayMotionExecutionAdapter` then `FixtureMotionAdapter`. Comment says Fixture is the single-injection default | CONFIRMED | `ContentPackDependencyInjection.cs` lines 192–196 |
| Runway transport in that DI | `IRunwayTransport` → `DryRunRunwayTransport` | CONFIRMED | same file, line 193 |
| Veo / ElevenLabs / Fal as `I*ExecutionAdapter` in that DI | Not registered. Classes exist. Official paths are wired in tests and harnesses | CONFIRMED for this composition root | DI file; `ProductionVeoReadinessComposition` sets `ProductionResolverIncludesVeo = false` |
| Legacy HTTP clients | `ContentRunwayClient`, `ContentFalClient`, `ContentVeoClient`, `ContentElevenLabsClient` are still `AddHttpClient` | CONFIRMED | DI lines 28–46 |
| Legacy routes | `POST series/turbo`, `series/lipsync`, `series/assemble`, `series/tts` still exist. `ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob` returns false | CONFIRMED | `ContentController.cs`; authority type |
| Credentials, this local database | One credential row. Owner `FAMIXA_PRODUCTION`. Provider `runway`, model `gen4_turbo`. Versions: 1 ACTIVE, 2 REVOKED. AI config: one enabled VIDEO row pointing at that credential | CONFIRMED | `production_credentials`, `production_credential_versions`, `production_tenant_ai_config` |
| `KIT_MKT` credentials and tasks, this local database | 0 credentials, 0 tasks, 0 builds | CONFIRMED | Same queries |
| Fal, ElevenLabs, Google, Gemini credentials, this local database | 0 version rows | CONFIRMED | Grouped version query |
| Cross-tenant inherit | Resolver comment: caller tenant must own the credential; no legacy settings fallback. Test name `T7_kit_mkt_does_not_see_famixa_runway` | CONFIRMED as code. The test is in-memory, not a live login | `ProductionCredentialResolution.cs`; `ProductionAiCatalogApiV1Tests.cs` |
| VPS credential tables | Repair report 2026-10-15: migrations 377–379 still blocked; no HTTP transport enabled; no live probe | CONFIRMED as that report. Current VPS rows are `UNKNOWN` | `docs/novixa/03-operations/PRODUCTION-CREDENTIAL-REPAIR-003-REPORT.md` |
| Speech window in current source | `speechStartSec` / `speechEndSec` can be stored. Picture duration 5 or 10 does not create them. Official-film Fal still sends the full take. Trim runs only when `IsTenantOwnedEp01Build` | CONFIRMED | `ProductionCreateVideoService.Actions.cs` around lines 450–454; `ProductionScopeAnchorRules.cs` lines 32–35 |
| 2026-10-17 lip-sync and click-gate edits | Reports say tests passed offline and the official assets were not regenerated | CONFIRMED as those reports. Whether the running API process loaded that build is `UNKNOWN` | Local lip-sync report; click-gate report |

`IsTenantOwnedEp01Build` is true for any non-empty EP01 build that is not one of the four official ids. `30ba4061` matches. Official `4ef164d6` does not.

### C. Quality and acceptance

| Item | State | Grade |
|---|---|---|
| Official SH01 picture vs story | Prior review: entrance not performed, Linh duplicated, acting said Linh enters while the story says Minh enters | CONFIRMED as the recorded review in `FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md`. Not re-watched here |
| Official SH02 picture | Prior review: red score absent, open mouth on the silent tail. Clocks 8 s picture, 2.534 s voice, 2.5 s lip-sync | CONFIRMED as that review plus the durations in the current rows |
| SH02 historical 8 s lip-sync `32bec770-…` | Superseded for mix. Reports: wrong face box / silence mode, not mix-ready | CONFIRMED as reports plus the file still on disk |
| SH01 Fal used the late 6 s take | Take 55 versus Take 56 | CONFIRMED as the preview-binding forensic. Files of both sizes are still in `veo/` |
| Word-level match of audio to the line | No ASR gate found | UNKNOWN |
| Episode of six accepted shots | No such accept. Tenant assembly is two shots and 13.042 s | CONFIRMED |
| `AutomatedVisualProof` | Stays false in the outcome reports | CONFIRMED as those reports |

### D. Phase changes

| Phase | What the evidence supports |
|---|---|
| August 23–September 7 | Older 24-shot and 11-shot graphs accumulated remote takes. No OS task table rows for them |
| September 5–24 | Official 6-shot build. Real Runway, ElevenLabs, Veo, Fal, then FFmpeg mix and per-shot assembly. Quality reviews after the files existed |
| September 18 | Forensic said Production OS motion DI was Fixture-only and could not spend on Runway. Later reports and the current file show Runway registered beside Fixture. The September 18 statement is historical, not the current file |
| September 25–27 | Tenant and harness builds. Imports of existing bytes, Fal on the tenant build, then Runway failures and one smaller Runway success, then a fixture lip-sync became current on tenant SH01 |
| October 15 | VPS credential-key backup recorded. DB restore off-machine and migrations 377–379 not passed |
| October 16 | Local episode concat of the tenant SH01 and SH02 assemblies. No new AI call in that report |
| October 17 | Quality gate, readiness NO-GO, pilot plan, speech-window click gate, local Fal trim. Tests are offline. Official bytes were not replaced. Source is ahead of a clean story: the pilot plan’s “picker does not store speech ends” is no longer what the file does |

---

## 4. Known defects

| Defect | Effect | Status | Evidence |
|---|---|---|---|
| Tenant SH01 `CURRENT:LIPSYNC` and `VISIBLE` are the 808-byte fixture `7c20d436-…`, written after the 4.2 MB Fal file `b58e679d-…` | A reader that uses lip-sync or VISIBLE, rather than `CURRENT:MIX`, selects the fixture | Open. Not repaired by the October 17 trim | Bindings and artifact join, this pass |
| Tenant SH01 `CURRENT:VIDEO_TAKE` is the later 646,555-byte Runway file, not the imported 1,529,738-byte file | Two verified takes. Current is the smaller one | Open | Artifacts `81b29b5c-…` vs the import task |
| Official SH02 clocks | 8 s picture, 2.534 s voice, 2.5 s lip-sync. Undeclared window fails the 0.75 s rule | Open for the official rows. Source can now store a window; official Fal does not trim | Rows; `Actions.cs` 450–454 |
| Official SH02 visual notes | Missed red-score action; mouth on the silent tail | Open. No ACCEPT/REJECT of that hash was written by the readiness pass | Readiness report §1 and §6 |
| Official SH01 visual notes | Entrance and duplicate Linh | Open as a review. Next-request text was corrected in the quality pipeline; the existing MP4 was not regenerated | Quality report |
| Three official SH02 Fal tasks still `RUNNING` since 2026-09-23 15:29–15:43 | In-flight rows with no failure reason beside two succeeded Fal artifacts | Open | `production_tasks` |
| Tenant SH03 Gemini `RUNNING` since 2026-09-26 22:24 | No artifact. Episode contract used by the October 16 concat does not include SH03 | Open | Task row; E2E report |
| Official rows `tenant_id` NULL | A tenant-scoped read fails closed. Preview of the official mix through the API is blocked without a backfill | Open. Backfill is not authorized by D-05 or by this pass | `series_build.tenant_id` and artifact `tenant_id` |
| Local `KIT_MKT` has no production credential | A resolver that requires an owned credential cannot start a pinned provider job for that tenant on this database | Open on this database | Credential query, 0 rows |
| Local database has no Fal, ElevenLabs, or Google credential | A pinned Fal / voice / Veo job has nothing to resolve, including on `FAMIXA_PRODUCTION` | Open on this database | Version group query |
| Runway attempts on `30ba4061` SH01 | 2026-09-27 12:29 `PROVIDER_UNAVAILABLE`, text `Runway 401` and “API key … did not start with `key_`”. Then three `Runway 400` “not enough credits”. Then 13:49 one SUCCEEDED take | The failures are stored. One later take exists. The 401 text is a provider error string, not a key | `production_tasks.failure_reason` |
| SH03 locked line truncated; SH04 action sentence incomplete | Voice and a complete action stay refused in the quality contract | Open | Quality report and click-gate matrix |
| SH04 graph `finalSource = RUNWAY` while the file host is Fal and the model is `wan-2.1` | Wrong label on a legacy run | Open as a label. Lineage report already flagged it | Graph row this pass; `FAMIXA_SH04_TAKE4_LINEAGE_VERIFY_V1_REPORT.md` |

---

## 5. Regression candidates

These are related. They are not established as the cause of the quality failures.

| Candidate | Why it is only a candidate |
|---|---|
| `IRunwayTransport` is now `DryRunRunwayTransport`, while 2026-09-27 tasks stored live Runway HTTP 401/400 bodies | The current composition root would not produce those HTTP bodies. The September 27 path did. This pass did not prove which build was running then, or that today’s DryRun registration is what blocked a later click. `ContentRunwayClient` is still registered for the legacy client |
| October 17 speech trim applies only to tenant-owned EP01 | Official SH01/SH02 keep the full-take send. The trim cannot explain the September quality notes, because those files predate it. It can change the next tenant Fal call once a window is stored |
| October 17 click gate reads `osDirectorTimingByShot` instead of computed timing | Offline tests passed. No production asset was created. A live click was not observed |
| September 18 “Fixture only” forensic versus current Runway registration | The code changed after that forensic. Using the September 18 map as the current DI map would be wrong |
| Import plus a later fixture lip-sync on `30ba4061` | Shows a selection change after a real Fal file existed. It does not show that the October 17 source caused it |
| NULL `tenant_id` plus tenant-owned credential rules | Explains why official bytes and a `KIT_MKT` login do not meet on this database. It does not explain mouth, identity, or the 2.5 s versus 8 s cut, which are properties of files that were created before that fence was the issue under discussion |
| VPS migrations 377–379 blocked on 2026-10-15, while this local database already has the credential tables | The two environments are not the same store. A local zero-row result does not prove the VPS result, and the reverse is also true |

---

## 6. Contradictions and unknowns

### Contradictions

1. **`ACCEPTED:MIX` versus “no shot is accepted”.** Bindings on official SH01, official SH02, and tenant SH01/SH02 have active `ACCEPTED` and `ACCEPTED:MIX`. The 2026-10-17 quality and pilot documents say no shot has a production visual accept. Both can be true only if `ACCEPTED:*` means an OS byte binding and the quality report means a human review. The outcome report says visual acceptance from the reader stays false. This pass did not find a stored human review row. Grade: `CONTRADICTORY_EVIDENCE` until the word “accepted” is kept on one meaning.

2. **`fca0c122` shot count.** Checkpoint 1: 24-shot. A keyframe forensic: 11-shot. Current row and `episode.shots` length: 24. The 11-shot film is `fd760913`. Grade: current row `CONFIRMED`; the forensic label is stale.

3. **Episode concat.** `VIDEO-PRODUCTION-E2E-001-REPORT.md` records a 13.042 s `ASSEMBLE_EPISODE` on `30ba4061`. CK27 and the pilot plan say the official six-shot episode assembly does not exist. Those are different builds. The official film still has no six-shot assembly.

4. **SH02 voice duration.** Database `duration_sec` is 2.534. The root-cause report measured the same artifact’s container at 2.480 s. Both refer to `0fde86c2-…`. This pass did not re-probe the file.

5. **Current lip-sync versus the Fal lip-sync on tenant SH01.** Fal artifact `b58e679d-…` succeeded. Current binding is fixture `7c20d436-…`. A report that says “SH01 lip-sync succeeded” without naming the binding is incomplete.

6. **Pilot plan §2 versus the file after the click-gate.** The plan, written the same day, says the timing picker does not store speech ends. The later click-gate report and the current `famixa-director-timing.ts` store them when both ends are supplied. The plan’s gap statement is behind the working tree.

7. **Environment of the credential gap.** Local `KIT_MKT` has no credential row. The VPS repair report did not say `KIT_MKT` was queried, and it said the credential migrations were not applied there. Grade: `CONTRADICTORY_EVIDENCE` across environments, not a single root cause.

### Unknowns

- VPS `production_credentials` and `series_build` contents after 2026-10-15.
- Whether the CloudFront takes on `fca0c122` and `fd760913` still download.
- Whether official SH06’s CloudFront URL still returns 401.
- Account balance and the invoice for the September provider calls. Catalog USD figures are not balances.
- Whether the API process listening on this machine, if any, was started from the current working tree or from an older binary. The lip-sync report names a DLL written 2026-10-17 16:57. This pass did not inspect that process.
- A browser confirmation of which artifact the Director preview resolves for `30ba4061` SH01 and for official SH02.
- Human judgment of the 13.042 s assembly. The E2E report did not watch it in the browser.

---

## 7. Completion gap

A shot that meets the contract in `FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md` and `FAMIXA_PRODUCTION_STANDARD_AND_PILOT_PLAN_V1.md` still needs all of the following. None of them is satisfied for official EP01.

1. A tenant-owned build whose preview can read the bytes. Official `4ef164d6` is NULL-tenant. `30ba4061` is tenant-owned and is the closest existing build, and its SH01 current lip-sync is a fixture.
2. One picture, one voice, one lip-sync, and one mix whose bindings agree, each with a hash a person can open.
3. A director speech window on a speaking shot whose line is shorter than the model duration by more than 0.75 s. The checker exists. Official SH02 has the failing clocks. This pass did not read a stored window off the official snapshot.
4. A person writes ACCEPT or REJECT against that artifact id and that hash, including action, continuity, motion, and mouth. Checkbox completion is not that act.
5. For a film: the other shots in OS, SH03’s line completed by a person, SH06’s picture actually fetched, then one episode assembly and a separate accept of that file. ADR-08 caller policy is still open in the pilot plan. The 13.042 s file is two shots.

Owning code for the next failure class is already listed in the pilot plan §4. This pass does not add a second map.

---

## 8. Evidence-based next step

**Do not generate media. Do not backfill `tenant_id`. Do not rewrite the pipeline.**

The smallest step that changes the next decision:

**Name the database and the tenant before any execution plan.**

Reason: on the local database just read, `KIT_MKT` has no production credential and no production task. The only credential is `FAMIXA_PRODUCTION` / `runway` / `gen4_turbo`, one ACTIVE version. There is no local Fal, ElevenLabs, or Google credential to pin. The OS Runway transport registered in source is `DryRunRunwayTransport`. A plan that says “log in as `KIT_MKT` and run Veo, ElevenLabs, and Fal” does not match this database. The September files were created anyway, so this gap is not the historical cause of those pictures. It is the gate in front of the next call on this machine.

Scope of that step: one read-only comparison, already started here, finished by stating which environment is allowed to run. If the allowed environment is the VPS, the missing evidence is a read-only count of `production_credential_versions` by tenant code and provider, with secret columns excluded, after the operator confirms migrations 377–379 are or are not applied. If the allowed environment is this local database, the next product choice is whether `FAMIXA_PRODUCTION` build `30ba4061` is the pilot, and whether its current SH01 lip-sync may remain the fixture `7c20d436-…`.

A second read, still without a provider call, is a preview resolve for two shots only: official SH02 and tenant SH01. The code order says `CURRENT:MIX` wins when it verifies. The bindings also contain a fixture lip-sync and a keyframe sitting on generic `CURRENT`. Those two results should be written down as artifact id plus size before anyone treats the desk as showing the mix.

---

## 9. Approval boundary

These actions are not authorized by this report.

| Action | Why it waits |
|---|---|
| Register or rotate a credential, including giving `KIT_MKT` a key | Writes secret material. Local evidence says the row is absent. It does not say which key, or that a key should be created |
| Backfill `tenant_id` on `4ef164d6` or copy official bytes into a tenant prefix | D-05 and the pilot plan leave historical backfill unauthorized. A prefix copy already exists on `30ba4061` and then diverged |
| Call Runway, Veo, ElevenLabs, Fal, or Gemini | Spend and new media. Official SH02 is still NO-GO in the readiness report. Tenant Runway already recorded a 401 and an insufficient-credit failure |
| Rebind tenant SH01 off the fixture lip-sync | Changes the current artifact. Needs an explicit choice of `b58e679d-…` or a new generation |
| Treat `ACCEPTED:MIX` as a human accept, or click ACCEPT in the desk | Mixes two meanings. The readiness report says the official card cannot record the official file |
| Apply migrations 377–379 on the VPS | Blocked in the 2026-10-15 repair report until the off-machine restore evidence exists |
| Start package P1 as if speech fields did not exist | The working tree already stores `speechStartSec` and `speechEndSec`. A new edit has to start from that file, not from the earlier sentence in the pilot plan |
| Assemble official SH01–SH06, or add SH03 to `b9fed1d7-…` | SH03–SH06 are not OS-accepted. SH03’s Gemini task is still `RUNNING`. ADR-08 is open |
| Deploy, commit, or clean the working tree | Not asked. The tree was already dirty |

---

## Evidence index

| Claim | Where |
|---|---|
| Four historical film ids | `FAMIXA_PRODUCTION_OS_TEST_SCOPE_FOUNDATION_CHECKPOINT_1_REPORT.md`; re-read in `series_build` this pass |
| Official artifact ids and durations | `production_artifacts` join `production_tasks` for `4ef164d6-…`; files under `src/KitPlatform.Api/App_Data/kit-video-i2v/` |
| 13.042 s episode | `docs/novixa/03-solution/VIDEO-PRODUCTION-E2E-001-REPORT.md`; artifact `b9fed1d7-…`; path exists |
| Quality matrix and NO-GO | `FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md`; `FAMIXA_FIRST_REAL_SHOT_VALIDATION_READINESS_V1_REPORT.md` |
| Speech trim scope | `FAMIXA_LOCAL_LIPSYNC_TIMELINE_FIX_V1_REPORT.md`; `ProductionCreateVideoService.Actions.cs` |
| Click-gate behavior | `FAMIXA_EP01_DIRECTOR_WINDOW_CLICK_GATE_V1_REPORT.md` |
| DryRun registration | `ContentPackDependencyInjection.cs` line 193 |
| Local credential counts | `production_credentials` / `production_credential_versions` / `production_tenant_ai_config`, 2026-10-17, read-only |
| VPS migration block | `docs/novixa/03-operations/PRODUCTION-CREDENTIAL-REPAIR-003-REPORT.md` |
| Playback order | `OfficialVideoAuthorityBridgeV1.cs` lines 7–8 |
