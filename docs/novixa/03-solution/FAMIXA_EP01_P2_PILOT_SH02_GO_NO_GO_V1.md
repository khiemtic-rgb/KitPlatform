# FAMIXA EP01 — P2 Pilot SH02 GO/NO-GO

Date: 2026-10-18. Scope: local readiness for one paid SH02 pilot. This document stops before the first provider call.

Conclusion: **NO-GO**.

A green local test does not prove a live provider. An OS `ACCEPTED:MIX` binding is not a human accept. A catalog price is not an invoice.

## 1. Repository

| Item | State |
|---|---|
| HEAD | `8c59ceb4f3be537898106ec058ea547406f722e5` |
| Branch | `main` |
| Commit | unchanged by this batch |
| Working tree | already dirty before P2. Modified and untracked files were left in place. No reset, clean, stash, checkout, commit, or push |

The production sources edited here were already untracked. P2 edited them in place.

## 2. What P2 implemented

Local execution gates on the existing production path. No second engine, workflow, or pipeline.

| File | Change |
|---|---|
| `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs` | Tenant EP01 paid video requires governance and a resolved credential before a network start. A second attempt on a tenant EP01 paid fingerprint is refused, including an explicit `RetryAttempt` |
| `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs` | Image replay survives a consumed permit. Provider failure on tenant paid image, voice, and lip-sync records a failed attempt. A failed tenant EP01 paid fingerprint cannot be opened again. Tenant lip-sync requires a credential when tenant AI governs |
| `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionVoiceLineIdentity.cs` | Line identity falls back to the CREATE_VOICE freeze token so lip-sync can see the line frozen by voice |
| `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/FakeVeoMotionTransport.cs` | Test transport can report `RequiresNetwork` and can succeed on the first poll. Default behavior of existing tests stays |
| `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionPilotSh02ReadinessV1Regression.cs` | New local chain: still, video, voice, blocked lip-sync, fixture speech window, lip-sync, mix, preview |
| `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs` | Fact `Pilot_sh02_local_readiness_v1` |
| `docs/novixa/03-solution/FAMIXA_EP01_IMPLEMENTATION_PLAN_LOCK_V1.md` | Section 13 records this result |

`IRunwayTransport` remains `DryRunRunwayTransport`.

## 3. Build and tests

Command:

`dotnet test tests/KitPlatform.Content.Tests/KitPlatform.Content.Tests.csproj --filter "FullyQualifiedName~ProductionPipelineV1Tests|FullyQualifiedName~ProductionOfficialSh02|FullyQualifiedName~ProductionCreateVoiceCutover|FullyQualifiedName~ProductionOfficialVeo|FullyQualifiedName~P35CredentialRotation|FullyQualifiedName~FalTenantEp01"`

Result: **55 passed, 0 failed, 0 skipped**. Build succeeded. The prior P1.1 count on this filter was 54. The added fact is `Pilot_sh02_local_readiness_v1`.

Limits of that run:

- Transports are fakes. Create counts are in-memory counters.
- The speech interval in the regression covers the whole take so the test does not shell out to FFmpeg. It is a fixture.
- Browser Preview of a signed-in `FAMIXA_PRODUCTION` session was not opened.
- No wider suite was run.

## 4. Adapters, transports, and credential resolvers

| Stage | Registration | This batch |
|---|---|---|
| Runway | `DryRunRunwayTransport` is `IRunwayTransport` | Left dry-run |
| Gemini still | `HttpGeminiTenantImageTransport` and `TenantGeminiImageExecutionAdapter` are registered | Not called. Regression uses an in-test script |
| Google Veo | `VeoMotionExecutionAdapter` is registered before the fixture motion adapter. Authenticator is `ProductionVeoHttpAuthenticator` and stays closed | Not called. Regression uses `FakeVeoMotionTransport` |
| ElevenLabs | Concrete HTTP transport and adapter are registered. `IVoiceExecutionAdapter` is not registered, so an unpaid call keeps the fixture default | Not called. Regression uses `FakeElevenLabsVoiceTransport` |
| Fal | Concrete HTTP transport and adapter are registered. `ILipSyncExecutionAdapter` is not registered, so an unpaid call keeps the fixture default | Not called. Regression uses `FakeFalLipsyncTransport` |
| Mix | Local FFmpeg concrete type is registered | Regression uses `FakeFfmpegMixTransport` |

Local credential rows were read, not written. The in-test credentials exist only inside the regression process.

## 5. Local proof and live proof

Proven by the local regression on candidate build `30ba4061-a443-4c0d-85aa-1d13ff4fdedb`, episode `EP01`, shot `EP01-SC01-SH02`:

- Missing permit, wrong model, or a null credential does not call Gemini. A credential failure leaves the permit issued.
- Ungoverned video throws `TENANT_AI_CONFIGURATION_REQUIRED`. A Runway resolution against a commanded Veo model throws `PROVIDER_MODEL_MISMATCH`. A null video credential leaves the permit unused. `AuthorizePaidNetwork: false` throws `PaidNetworkForbidden`.
- One still, one Veo 3.1 video-only 720p take with `GenerateAudio` false, one Eleven v3 voice, one Fal v3 lip-sync, and one FFmpeg compose. Each replay returns the same artifact. A second fingerprint is `PaidNetworkForbidden`.
- Lip-sync without a speech interval throws `MISSING_TIMELINE` and does not start Fal.
- Preview artifact role is `CURRENT:MIX`. The authoritative video take remains the video artifact. Video attempt count is 1.
- A provider failure then a same-fingerprint retry is `PaidNetworkForbidden`, with one generation and one attempt.
- Official scope does not govern. Official `CreateVoice` throws `MissingPerformanceSnapshot` with transport counts at zero.
- An empty caller tenant throws `PRODUCTION_TENANT_REQUIRED` before an idempotency read. A null build tenant throws `PRODUCTION_TENANT_UNATTRIBUTED`.

Not proven by a real provider: Gemini still bytes, Veo video bytes, ElevenLabs audio bytes, Fal lip-sync bytes, a real FFmpeg mix of those bytes, provider error payloads, latency, or an invoice.

## 6. Tenant, build, lineage, authorization

Read-only session against local database `kitplatform` on 2026-10-18. Each statement ran inside `BEGIN READ ONLY` and committed that read-only transaction. No row was updated.

| Fact | State |
|---|---|
| Tenant | `4028941b-49b3-4ba2-8815-778daef4a2d3`, code `FAMIXA_PRODUCTION` |
| Candidate build | `30ba4061-a443-4c0d-85aa-1d13ff4fdedb`, `tenant_id` equals that tenant |
| Official build | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, `tenant_id` is null. `series_pilot.active_build_id` still points here. Not written |
| SH02 current mix | `72c6eb46-0c0a-4845-b4d3-d5e573cd67ae` is active `CURRENT:MIX`, `VISIBLE:MIX`, `ACCEPTED`, and `ACCEPTED:MIX` |
| Mix bytes | SHA-256 `78216013541205afea5a131d92d53e166ba9171beddc2111b05b74ffea73538a`, 1,422,990 bytes, 8 s, `VERIFIED` |
| SH02 video | `eb1072d0-aa03-4493-9dd7-db24f2123d27` is `CURRENT` and `CURRENT:VIDEO_TAKE`. SHA-256 equals official Veo `b44e530b-5b59-422f-9b80-e058a3a639b5`: `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34`, 1,437,239 bytes, 8 s |
| SH02 lip-sync | active `CURRENT:LIPSYNC` and `VISIBLE:LIPSYNC` are `a40edfef-db48-44e6-9935-f2ee7febba7c` |
| SH02 voice | active `CURRENT:VOICE` is `e00dbf1a-8347-4b6f-a300-521127ce54a0`, SHA-256 `bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298`, 2.534 s |
| SH02 visible slot | active `VISIBLE` and `VISIBLE:ASSEMBLY` are assembly `4f1e8081-55e5-4d16-b65c-5c3bb8090ead`. The authorized preview order checks a verified `CURRENT:MIX` before that slot |
| SH03 Gemini task | `e280783f-7691-49f3-9aa2-77636c99bac0` remains `CREATE_IMAGE` / `RUNNING`, provider `GEMINI`, model `gemini-2.5-flash-image`, created 2026-09-26 22:24 +07. One attempt, no provider job id, zero artifacts. Not closed. No replacement task was created |
| Authorization | Tenant EP01 paid start requires `AuthorizePaidNetwork` and an issued execution permit. Official film stays on its own permit path. The process-local one-shot permit does not open the tenant EP01 gate |

`VISIBLE` pointing at the assembly does not replace the `CURRENT:MIX` preview rule. This pass did not open a browser session, so live playback of that rule is unproven.

## 7. Pilot SH02 conditions

| Condition | Grade | Evidence |
|---|---|---|
| Human viewed mix `72c6eb46` and recorded a REJECT on that id and SHA | Needs the responsible person | OS roles `ACCEPTED` and `ACCEPTED:MIX` exist. No `shot_human_review` table. No human REJECT row |
| Reviewer named | Needs the responsible person | No reviewer assignment was found |
| `speechStartSec` and `speechEndSec` supplied for `30ba4061` | Needs the responsible person | `performance_snapshot` has zero rows for this build |
| Locked line matches the voice that would be sent | Needs the responsible person | Locked line remains `(lý nhí) Dạ… nay con làm bài không tốt.` Voice bytes exist. Word match is unknown |
| Gemini, Veo, ElevenLabs, and Fal credentials registered for this tenant | Not met | Versions: one ACTIVE `runway` / `gen4_turbo`, two REVOKED. AI config: one enabled VIDEO row for that Runway model. No Gemini, Google, ElevenLabs, or Fal version |
| Off-machine backup has real evidence | Needs the responsible person | Local API appsettings do not contain `ProductionCredentials:OffMachineBackupConfirmed`. That flag was not set. A missing local flag is not an attestation, and setting it would not be one either |
| Technical gate and permit path | Met locally | Section 5. A live permit and a live credential pin are not issued |
| Current SH02 video may be the pilot source | Not met | Its SHA equals the official Veo file. A pilot still and a pilot Veo take have to be new artifacts |
| SH03 Gemini task closed | Blocked | D-G has no approved disposition. The task and its empty artifact set were preserved |
| SH03 remaining dialogue | Needs the responsible person | The line still ends at `Điểm thấp thì lần sau gỡ`. Nothing was invented |
| Episode assembly | Blocked | ADR-08 caller letter is still open. Assembly was not run |
| Spend approval | Needs the responsible person | This document is the gate. No provider was called |

## 8. Expected cost

One attempt per stage. No automatic retry of the same fingerprint. These figures are catalog or estimate values from the locked plan. They are not invoices, and this batch spent nothing.

| Stage | Model | Amount | Assumption still unverified |
|---|---|---|---|
| Still | `gemini-2.5-flash-image` | UNKNOWN USD | No current Gemini price was read from a provider |
| Video | `veo-3.1` video-only 720p | 0.20 USD catalog, dated 2026-09-22 | Duration does not multiply it. The 0.40 USD video-plus-audio row is excluded. Account balance was not read |
| Voice | `eleven_v3` | UNKNOWN USD | Character count of the locked line was not priced |
| Lip-sync | `sync-lipsync-v3` | about 0.67 USD | Five-second floor applied to a line of about 2.5 seconds. The human window is missing, so the billed duration can change |
| Mix | local FFmpeg | 0 | No provider charge |

Known catalog sum for one SH02 pass: 0.20 + about 0.67 USD, plus the two UNKNOWN amounts. This is not an authorization to spend the larger examples in the pilot plan.

## 9. Risks, stop conditions, and a failed stage

| Risk | Stop |
|---|---|
| Calling a provider before the human conditions in section 7 | Stay stopped. This batch made zero provider calls |
| Reusing SH02 video `eb1072d0` because it is current | Reject it. Its SHA is the official Veo file |
| A second automatic start after a provider error | Tenant EP01 paid Gemini, Google, ElevenLabs, and Fal now record the failure and refuse the same fingerprint. A later fingerprint still needs a new issued permit and a new human decision |
| Official retry behavior | Unchanged. Official film is outside the tenant EP01 one-attempt rule |
| NULL `tenant_id` on older rows | Left null. No backfill |
| Runway live switch | Not performed. Dry-run remains the Runway transport |
| SH01 paid take, SH04–SH06 dialogue, or episode assembly | Not performed |

If a future approved stage fails, keep the failed attempt, keep the fingerprint, and return to the responsible person. Do not reissue the same fingerprint from an automatic retry.

## 10. Decision

**NO-GO.**

The local path can refuse a paid start that lacks governance, a credential, a permit, a speech interval, or a second attempt on the same fingerprint. It can replay one successful fingerprint and point preview at a valid `CURRENT:MIX`.

The paid pilot is blocked by evidence only a person can supply:

1. A human REJECT of mix `72c6eb46` on artifact id and SHA-256 `78216013541205afea5a131d92d53e166ba9171beddc2111b05b74ffea73538a`, or a human accept that ends the pilot.
2. A named reviewer.
3. `speechStartSec` and `speechEndSec` for build `30ba4061`.
4. Confirmation that the locked SH02 line matches the voice bytes that will be sent.
5. Tenant-owned credentials for Gemini, Google Veo, ElevenLabs, and Fal, registered through the secret mechanism. Runway `gen4_turbo` is the only ACTIVE version.
6. A real off-machine backup attestation.
7. A written spend approval for one attempt of each stage, after a new still and a new Veo take. The current video file is an official byte copy.
8. A separate D-G decision before anyone closes Gemini task `e280783f` or creates a replacement SH03 still.

Until those decisions exist, do not call Gemini, Veo, ElevenLabs, or Fal, do not register or rotate credentials, do not set the backup flag, do not write Production DB, and do not assemble EP01.
