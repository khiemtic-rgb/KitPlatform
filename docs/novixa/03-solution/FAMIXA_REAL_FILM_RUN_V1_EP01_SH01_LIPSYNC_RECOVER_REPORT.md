# FAMIXA REAL FILM RUN V1 — SH01 — RECOVER EXISTING FAL JOB

Date: 2026-09-21  
Mode: RECOVERY ONLY · NO NEW PROVIDER START · NO NEW GENERATION  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`

## Verdict

PASS

Existing Fal job recovered. `providerJobId` persisted on the original Attempt. Physical MP4 downloaded into canonical OS storage and verified. ProductionArtifact(LIPSYNC) VERIFIED. CURRENT:LIPSYNC and VISIBLE:LIPSYNC bound. VIDEO and VOICE unchanged. Fal Start count remains 1. EP01 and EP99 graphs unchanged.

## Existing Provider Job

`01a0c347-d35d-7c71-8cf5-12ba5141f0bc`

## Existing Attempt

`2db32efb-4ac8-4d2c-9297-11d234b135e1`

## Provider Start Count

Before:  
1

After:  
1

This API process StartCount after restart = 0 (poll/download only). No `Create` / `Start` / queue POST. Log shows GET `queue.fal.run/fal-ai/sync-lipsync/requests/01a0c347-…` plus GET output bytes only.

## Provider Job Persistence

providerJobId:  
`01a0c347-d35d-7c71-8cf5-12ba5141f0bc`

persisted to Attempt:  
YES

Attempt status: `PROVIDER_SUCCEEDED`  
Same Attempt. No new Attempt. No new Task. No new Request.

## Recovery

Poll:  
GET existing Fal request `01a0c347-d35d-7c71-8cf5-12ba5141f0bc` via `ContentFalClient.GetTaskAsync` / recover endpoints. No Start.

Output:  
Fal CDN `https://v3b.fal.media/files/b/0aab4c03/93c9KoGwGzisE82xpA09w_U0Gjh9ia.mp4`

Download:  
Physical bytes written to canonical storage `os/2db32efb4ac84d2c929711d234b135e1`

Physical bytes:  
YES · 4312559 · MP4 `ftyp isom` · SHA256 match on disk

## Artifact

ArtifactId:  
`837ed989-5e1f-45ad-b77d-cb26f5e808a2`

SHA:  
`8e1bb610070ede431f2b0d0caac9194169145ee468496bf8769155f85b379c3e`

Duration:  
5.042s

StorageKey:  
`os/2db32efb4ac84d2c929711d234b135e1`

Verification:  
VERIFIED

Also referenced:

- attemptId `2db32efb-4ac8-4d2c-9297-11d234b135e1`
- inputSnapshotHash `834c082275914e8a28ee36bc80c819182812bf121b502229cfbe7b75528f3b13`
- providerId `fal`
- modelId `sync-lipsync-v3`
- providerJobId `01a0c347-d35d-7c71-8cf5-12ba5141f0bc`
- mimeType `video/mp4`
- buildId `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`
- shotId `EP01-SC01-SH01`
- parents VIDEO `b22370a9-6ddd-4b04-a281-c7498cc926ae` + VOICE `bf8d0827-21ff-4daa-83b6-116bc3324ab0`

## Binding

CURRENT:LIPSYNC:  
`837ed989-5e1f-45ad-b77d-cb26f5e808a2`

VISIBLE:LIPSYNC:  
`837ed989-5e1f-45ad-b77d-cb26f5e808a2`

CURRENT:VIDEO_TAKE unchanged.  
CURRENT:VOICE unchanged.

## Input Lineage

VIDEO:  
`b22370a9-6ddd-4b04-a281-c7498cc926ae`

VOICE:  
`bf8d0827-21ff-4daa-83b6-116bc3324ab0`

Frozen ShotContract unchanged:

`line-SC01-CHAR-003-1|CHAR-003|b22370a96ddd4b04a281c7498cc926ae|bf8d082721ff4daa83b6116bc3324ab0|0.63|0.25|1280|720|0`

InputSnapshot `26636cbe-e395-4a81-85b7-79e2323dfc9d` is authority.

## Reload

PASS

GET shot state:

- VIDEO = `b22370a9-6ddd-4b04-a281-c7498cc926ae`
- VOICE = `bf8d0827-21ff-4daa-83b6-116bc3324ab0`
- LIPSYNC = `837ed989-5e1f-45ad-b77d-cb26f5e808a2`
- VISIBLE = `837ed989-5e1f-45ad-b77d-cb26f5e808a2`

## Idempotency

PASS

Second recover POST returned the same Artifact `837ed989-5e1f-45ad-b77d-cb26f5e808a2` on the same Attempt. Fal Start stayed 1. No second Artifact. No second binding overwrite cycle that created a new identity.

Empty-story request `famixa-real-film-run-v1-ep01-sh01-lipsync-oneshot` (`30af6953-4ae0-4146-811e-3882652be6e9`): Request FAILED, Task SUPERSEDED, no Attempt, no Fal call. Close replay is idempotent.

## EP01 graph

UNCHANGED

`415fedf306ed23d1ace94a794400b440`

## EP99 graph

UNCHANGED

`c7cd2516627332c2648c1509395e4a64`

## New Provider Calls

Fal:  
0

All other providers:  
0

Permit `255b0c57-dfb5-4034-812c-41e125bed819` remains the only CREATE_LIPSYNC permit for SH01 (CONSUMED). No new permit.

## Stop

Do not continue to MIX.
