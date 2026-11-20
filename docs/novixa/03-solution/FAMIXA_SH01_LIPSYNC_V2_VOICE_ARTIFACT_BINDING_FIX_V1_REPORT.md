# FAMIXA_SH01_LIPSYNC_V2_VOICE_ARTIFACT_BINDING_FIX_V1_REPORT

## VERDICT
PASS — code fix + isolated tests. Fal not run.

## SOURCE OF voiceArtifactId
`voiceAssets[lineId].artifactId` via `resolveAuthoritativeLipSyncVoiceSource` / `voicePhysicalArtifactIdFromGraph`.

Resolver refuses:
- missing `artifactId`
- `artifactId === lineId`

`startLipsync` now passes `voiceAssets: stateRef.current.voiceAssets` and freezes `voiceSrc.artifactId`.

## FROZEN IDENTITY (SH01 fixture)
- frozen.voiceArtifactId: `vpa-20260915T161911Z-e0b3093f`
- frozen.voiceId: `kOIUKJ9E0DPndqTtvJm5`
- frozen.buildId: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`
- frozen.voiceLineIds[0]: `line-SC01-CHAR-003-1`
- canonical storageKey: `FAMIXA:voice:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5`

## PHYSICAL BYTE VERIFICATION
Isolated `VoicePhysicalStore`: save → `loadFrozenVoicePhysicalBytes(frozen)` → exact fixture bytes.

Live 69007 B / SHA256 `E0B3093F…` was not re-read this phase (no LipSync, no production IDB open). Recovery identity is the one the resolver now binds.

## MOTION
- selected: `mot:EP01-SC01-SH01:55` / task fixture `veo_fixture_take_55`
- acceptedTake 56 present on fixture — not selected
- `lastSuccessMotionTaskId` = take 56 — not used after freeze

## TESTS
| ID | Result |
|---|---|
| T1 voiceAssets.artifactId → frozen.voiceArtifactId | PASS |
| T2 frozen.voiceArtifactId != lineId | PASS |
| T3 frozen.voiceArtifactId == vpa-20260915T161911Z-e0b3093f | PASS |
| T4 frozen id resolves canonical VoicePhysicalStore | PASS |
| T5 wrong line / lineId-as-id cannot resolve | PASS |
| T6 wrong voiceId cannot resolve | PASS |
| T7 acceptedTake=56 does not replace Motion 55 | PASS |
| T8 lastSuccessMotionTaskId=56 does not replace Motion 55 | PASS |
| T9 freeze/attempt before provider | PASS |
| T10 zero provider calls | PASS |

## SAFETY
- ElevenLabs: 0
- Fal: 0
- Runway: 0
- Veo: 0
- Production graph writes: 0
- SH01 state / videoApproved / acceptedTake / Final: 0

## FILES
- `client/admin/src/modules/content/famixa-lipsync-execution-input-freeze.ts`
- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx`
- `client/admin/src/modules/content/content-famixa-series.ts`
- `client/admin/src/modules/content/ContentFamixaShotProduction/index.ts`
- `client/admin/src/modules/content/famixa-lipsync-execution-input-freeze.smoke.ts`
- `client/admin/src/modules/content/famixa-sh01-lipsync-v2-voice-artifact-binding.smoke.ts`

## REMAINING BLOCKER
None for binding. Next LipSync send still requires the operator browser to have the durable `FAMIXA:voice:…` bytes. This phase did not POST Fal.

STOP.
