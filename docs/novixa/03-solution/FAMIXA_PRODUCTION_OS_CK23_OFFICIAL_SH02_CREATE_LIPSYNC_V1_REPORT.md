# CK23 — OFFICIAL SH02 CREATE_LIPSYNC V1

Date: 2026-09-23  
Mode: FORENSIC IMPLEMENT + REAL PRODUCTION RUN  
Suite: `FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_LIPSYNC_V1`

## 1. STATUS

PASS

## 2. Canonical target

BuildId: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
ShotCode: `EP01-SC01-SH02`

## 3. Input authority

VIDEO ArtifactId: `b44e530b-5b59-422f-9b80-e058a3a639b5`  
VIDEO SHA: `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34`  
VIDEO status: `ACCEPTED:VIDEO_TAKE`  
VOICE ArtifactId: `0fde86c2-250f-4303-bda0-4913a6dbd1e4`  
VOICE SHA: `bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298`  
VOICE status: `ACCEPTED:VOICE` + `CURRENT:VOICE`

## 4. ParentArtifactIds

`b44e530b-5b59-422f-9b80-e058a3a639b5` (VIDEO_TAKE)  
`0fde86c2-250f-4303-bda0-4913a6dbd1e4` (VOICE)

## 5. LipSync contract

CharacterId: `CHAR-001`  
LineId: `line-SC01-CHAR-001-2`  
Provider: `fal`  
Model/config: `sync-lipsync-v3` / pin-v1 T3 `0.28,0.38 @ 1280x720 frame 0`  
SnapshotId: `71b2bc55-fa5b-4dc1-aec8-99441ecb7b0e`  
InputHash: `e1d10cf3525b1b7c0d12e90f5bb1f4091fec4b469baea647c647ea7ca6afcca5`  
ShotContract: `line-SC01-CHAR-001-2|CHAR-001|b44e530b5b59422f9b80e058a3a639b5|0fde86c2250f4303bda04913a6dbd1e4|0.28|0.38|1280|720|0`

## 6. Execution

Provider: Fal `fal-ai/sync-lipsync/v3`  
Start count: 1  
AttemptId: `c30af2f5-b4f7-499a-a66d-c741afac6a99`  
Provider job id: `lipsync_v3_01a0cd8b-934c-7d33-9bb2-c3d9d6d574c2`  
Recovery: yes — first Start queued the job; first poll window returned no bytes (`FAILED_IDENTITY`); existing recover path materialized the same job.  
Replay: yes — same idempotency `ck23-official-sh02-create-lipsync-v1-d` returned the same artifact; Fal Start stayed 1.

## 7. Output

ArtifactId: `32bec770-9706-4aad-bc21-8db3d4f11d4c`  
Role: `LIPSYNC`  
SHA: `9d8e6949eb43480a60cee4e6058cff6239beb24191ed3fe06595e30d12b724db`  
Size: `6389625`  
MIME: `video/mp4`  
Status: `VERIFIED`  
Duration: `8`

## 8. Binding

ACCEPTED:VIDEO_TAKE: `b44e530b-5b59-422f-9b80-e058a3a639b5`  
ACCEPTED:VOICE: `0fde86c2-250f-4303-bda0-4913a6dbd1e4`  
ACCEPTED:LIPSYNC: `32bec770-9706-4aad-bc21-8db3d4f11d4c`  
CURRENT:LIPSYNC: `32bec770-9706-4aad-bc21-8db3d4f11d4c`  
VISIBLE:LIPSYNC: `32bec770-9706-4aad-bc21-8db3d4f11d4c`

## 9. Director/UI

Video: ✓ ACCEPTED:VIDEO_TAKE unchanged  
Voice: ✓ ACCEPTED/CURRENT:VOICE unchanged  
LipSync: ✓ Đã khớp môi — `osLipsyncArtifactId` / `lipsyncArtifactId` = `32bec770-…`  
Preview source: shot state `VISIBLE` + `lipsyncArtifactId` = LIPSYNC `32bec770-…`. Generic `GET /preview` remains ACCEPTED:VIDEO_TAKE by CK21A official video authority. HTTP/read contract proves the LIPSYNC artifact and bindings.

## 10. Provider calls

Vertex: 0  
ElevenLabs: 0  
LipSync provider: Fal Start = 1 (queue `01a0cd8b-934c-7d33-9bb2-c3d9d6d574c2`)  
Other: none

## 11. Mutations

VIDEO: unchanged  
VOICE: unchanged  
LIPSYNC: created + VERIFIED + ACCEPTED/CURRENT/VISIBLE  
Snapshot: frozen once (`71b2bc55-…`); not refrozen  
Permit: `b0b423d5-b4ea-4efe-ab74-95abaf73f6c7` CONSUMED at Start  
Graph: not modified

## 12. Negative tests

A. Wrong VIDEO ArtifactId → FAIL CLOSED (`BINDING_CHANGED`)  
B. Wrong VIDEO SHA → FAIL CLOSED (`BINDING_CHANGED`)  
C. Wrong VOICE ArtifactId → FAIL CLOSED (`BINDING_CHANGED`)  
D. Wrong VOICE SHA → FAIL CLOSED (`BINDING_CHANGED`)  
E. VIDEO not ACCEPTED → FAIL CLOSED (`MISSING_VIDEO_ARTIFACT`)  
F. VOICE not ACCEPTED/CURRENT → FAIL CLOSED (`MISSING_VOICE_ARTIFACT`)  
G. Existing LIPSYNC for different input → not authoritative  
H. Exact frozen input replay → same artifact, Fal Start = 1  
I. Graph IDs do not override Production OS parents

## 13. Regression

CK20: PASS (nested)  
CK21A: PASS (nested)  
CK22: PASS (nested)  
CK23 tests: PASS (`Official_sh02_create_lipsync_is_offline_safe`)  
CK23 smoke: PASS (`FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_LIPSYNC_V1`)

## 14. Remaining gaps

- Generic Director `GET /preview` still resolves ACCEPTED:VIDEO_TAKE (CK21A). LIPSYNC preview is via `VISIBLE:LIPSYNC` / `lipsyncArtifactId`, not the video-authority overlay.
- `Evaluate CREATE_LIPSYNC` can still report available after success; a new idempotency hits `BINDING_CHANGED` because `CURRENT:LIPSYNC` exists.
- Media quality is not judged here. Inspect the real SH02 LipSync file before MIX.

## 15. FINAL

CK23 PASS

STOP. Do not proceed to MIX.
