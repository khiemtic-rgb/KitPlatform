# FAMIXA REAL FILM RUN V1 — EP01 SC01 SH01 — REAL MIX ONE SHOT

Date: 2026-09-21  
Mode: REAL PRODUCTION ACTION · ONE SERVER FFMPEG · NO AI PROVIDER  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`

## Verdict

PASS

## Production chain

CURRENT:LIPSYNC  
→ MIX_TASK  
→ INPUT_SNAPSHOT  
→ FFmpeg (one remux, stream copy)  
→ MIX_ARTIFACT  
→ CURRENT:MIX  
→ VISIBLE:MIX  
→ PREVIEW

## Forensic (before FFmpeg)

1. CURRENT:LIPSYNC artifact: `837ed989-5e1f-45ad-b77d-cb26f5e808a2` VERIFIED  
2. physical storageKey: `os/2db32efb4ac84d2c929711d234b135e1`  
3. SHA: `8e1bb610070ede431f2b0d0caac9194169145ee468496bf8769155f85b379c3e` (disk match)  
4. duration: 5.042s (container 5.041667s)  
5. video streams: 1 · H.264 High · 1280×720 · 24 fps  
6. audio streams: 1 · AAC LC · 44100 Hz · mono  
7. codec: h264 + aac already mixed  
8. sample rate/channels: 44100 / 1  
9. expected MIX operation: canonicalize container only. Do not add VOICE. Do not double-mix. Do not re-encode picture.  
10. exact FFmpeg command:

`ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4`

## Evidence

Request: `6727a3d1-bf30-4e34-962e-326349953cba` COMPLETED · MIX_VIDEO  
Task: `b4f1fd60-948b-4d86-b701-6ae34ac89bb9` SUCCEEDED  
Snapshot: `827df604-b5de-4e32-96b9-c4d9ce2e9667`  
SnapshotHash: `c54cdc0bd36dcb4e8f6a8eb9729099886e7b734e629d687f8bc0ff06af2f5c55`  
Attempt: `58796f8c-c6b2-4578-894b-da828cc7e2c0` PROVIDER_SUCCEEDED · attempt 1  
InputArtifact: `837ed989-5e1f-45ad-b77d-cb26f5e808a2`  
OutputArtifact: `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` MIX VERIFIED  
OutputSHA: `3818672c08d716b7d51c3fb12aa3f96ed204e78ef05800f27bec17e87fc28f10`  
Duration: 5.042s  
StorageKey: `os/58796f8cc6b24578894bda828cc7e2c0`  
MIME: video/mp4  
Parent: exactly the LIPSYNC artifact  
Provider/model: server / ffmpeg  

ShotContract frozen:  
`837ed9895e1f45adb77dcb26f5e808a2|8e1bb610070ede431f2b0d0caac9194169145ee468496bf8769155f85b379c3e`

## Execution

FFmpeg: 1  
AI calls: 0  
Provider calls: Fal 0 · Runway 0 · ElevenLabs 0  

Replay of the same idempotency key returned the same Artifact and Attempt. FFmpeg count stayed 1. MIX request count 1. MIX attempt count 1.

## Verification

Artifact: VERIFIED · new artifactId · new storageKey · new SHA · physical file 4,299,195 bytes · H.264 + AAC 44100/mono · duration 5.042s  
Binding: CURRENT:MIX and VISIBLE:MIX = `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2`  
Preview: GET preview returns that MIX artifact + storageKey (VISIBLE authority). Not a provider URL.  
Reload: VIDEO `b22370a9-6ddd-4b04-a281-c7498cc926ae` · VOICE `bf8d0827-21ff-4daa-83b6-116bc3324ab0` · LIPSYNC `837ed989-5e1f-45ad-b77d-cb26f5e808a2` · VISIBLE = MIX  
Idempotency: PASS  
Cross-shot: EP99 has no MIX binding  

CURRENT:VIDEO_TAKE, CURRENT:VOICE, CURRENT:LIPSYNC unchanged. No FINAL / FILM_COMPLETE / VIDEO_APPROVED written.

## Graphs

EP01: UNCHANGED `415fedf306ed23d1ace94a794400b440`  
EP99: UNCHANGED `c7cd2516627332c2648c1509395e4a64`

## Remaining gaps

None for this MIX one-shot.

STOP. Do not run FINAL. Do not run Assembly. Do not run another shot.
