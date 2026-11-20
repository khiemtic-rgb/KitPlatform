# FAMIXA PRODUCTION OS — REAL FILM ASSEMBLY / FINAL BRIDGE CUTOVER V1

Date: 2026-09-21  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`

# Verdict

PASS

# Contract Forensic

Canonical action: `ASSEMBLE`  
Canonical output role: `ASSEMBLY`  
Canonical ACCEPT action: `ACCEPT`  
Existing FilmAssembly semantics: editorial metadata (`FilmAssembly` + `FilmAssemblyShot` with exact ACCEPTED artifactIds). TEST path remains fixture (`fixture` / `fixture-assembly-v1`, title `TEST-ASSEMBLY`), no physical artifact.  
Physical execution semantics: official film cutover of the same `ASSEMBLE` action. Provider `server` / model `ffmpeg`. Reuses `IFfmpegMixTransport` remux (`-c copy -movflags +faststart`). Creates `ProductionArtifact(role=ASSEMBLY)` and binds `CURRENT:ASSEMBLY` + `VISIBLE:ASSEMBLY`. No `ASSEMBLE_FILM`. No `FINAL` artifact role.

CASE A. `ASSEMBLE` already owned MIX → FilmAssembly → physical output. Official path was fixture-only; it now runs the existing server remux engine.

# Acceptance

Accepted binding: `ACCEPTED` + `ACCEPTED:MIX`  
Artifact: `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2`

ACCEPT request `4056d515-ca46-4efb-b823-f7d2485eeafb` COMPLETED. Exact MIX artifactId. No URL.

# FilmAssembly

AssemblyId: `d56a3c63-fbe7-41d6-8355-0af246c092b7`  
BuildId: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Included shots: `EP01-SC01-SH01`  
Editorial order: 0 · MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` · 5.042s  
Title: `EP01` (not `TEST-ASSEMBLY`)

# Production

RequestId: `9146e9cb-79c6-4bc3-8673-540bf8bb47cb` COMPLETED · ASSEMBLE  
TaskId: `2c432d40-b9b7-4d0a-8724-84c4ce6af254` SUCCEEDED  
SnapshotId: `d52cc44c-85e0-4e7e-9d91-622b52f92ea7`  
SnapshotHash: `7edc8c72b5cdbdd189a14e966c91f0fcfd2fdb171ebf29475e1bc25e2cfd1775`  
AttemptId: `b5b954fa-4543-4fc5-856b-edf8476e948c` PROVIDER_SUCCEEDED · attempt 1

Frozen: `server` / `ffmpeg` · parent MIX `2f88e0a0-…` · assembly membership + SHA + storageKey.

# Execution

Engine: server FFmpeg via existing `IFfmpegMixTransport`  
Execution count: 1 persisted remux on the successful process. Replay +0.  
FFmpeg command: `ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4`  
Re-encode or stream-copy: stream-copy  
Reason: SH01 MIX is already H.264 + AAC. Canonicalize container only.

One earlier remux in the previous API process wrote no artifact: SHA-based provider job id collided with the MIX attempt (`uq_prod_attempt_provider_job`). Job ids are now attempt-scoped. Same Request/Task/Attempt resumed; no second request.

# Output

ArtifactId: `28f8de3e-f6d3-4972-934e-703a2be45939`  
Role: ASSEMBLY  
SHA: `3818672c08d716b7d51c3fb12aa3f96ed204e78ef05800f27bec17e87fc28f10`  
StorageKey: `os/b5b954fa45434fc5856bedf8476e948c`  
Duration: 5.042s  
MIME: video/mp4  
Physical bytes: 4,299,195 · H.264 + AAC 44100/mono · SHA match  
Parent: MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2`  
Immutable. MIX not overwritten.

# Binding

CURRENT: `CURRENT:ASSEMBLY` = `28f8de3e-f6d3-4972-934e-703a2be45939`  
VISIBLE: `VISIBLE:ASSEMBLY` + untyped `VISIBLE` = `28f8de3e-f6d3-4972-934e-703a2be45939`

No `FINAL` artifact role. Contract remains “No FINAL claim”.

# Preview

Authority: VISIBLE → ProductionArtifact → storageKey → physical bytes  
Artifact: `28f8de3e-f6d3-4972-934e-703a2be45939`  
Reload: same ASSEMBLY artifact and storageKey. Not a provider URL.

# Idempotency

Replay: same Request / Task / Attempt / Artifact  
Execution count: 1  
Artifact count: 1 ASSEMBLY

# Integrity

EP01 graph: UNCHANGED `415fedf306ed23d1ace94a794400b440`  
EP99 graph: UNCHANGED `c7cd2516627332c2648c1509395e4a64`  
Previous artifacts: UNCHANGED  

VIDEO `b22370a9-6ddd-4b04-a281-c7498cc926ae`  
VOICE `bf8d0827-21ff-4daa-83b6-116bc3324ab0`  
LIPSYNC `837ed989-5e1f-45ad-b77d-cb26f5e808a2`  
MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2`

Cross-shot: none. Only SH01 is in FilmAssembly.

# AI / Provider

AI calls: 0  
Runway: 0  
ElevenLabs: 0  
Fal: 0  
Gemini: 0

# Remaining gaps

Official multi-shot concat is not in this remux engine. Official `ASSEMBLE` accepts exactly one included MIX. SH01-only FilmAssembly does not need concat.

STOP. No SH02. No SH03. No regenerate. No polish. No UI change.
