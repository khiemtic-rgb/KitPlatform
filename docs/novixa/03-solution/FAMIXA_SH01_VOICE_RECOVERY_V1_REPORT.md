# FAMIXA_SH01_VOICE_RECOVERY_V1_REPORT

## 1. VERDICT
PASS

## 2. TARGET
- buildId: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`
- shotId: `EP01-SC01-SH01`
- lineId: `line-SC01-CHAR-003-1`
- speaker: `CHAR-003` (Linh)
- text: `Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?`
- voiceId: `kOIUKJ9E0DPndqTtvJm5`

Forensic before generate: line/text/voiceId exact. `voiceAssets[lineId]` was duration-only (`ready` / `4.24`). No canonical `FAMIXA:voice:` key. No generate skip.

## 3. PROVIDER
- providerId: `elevenlabs`
- modelId: `eleven_v3` (existing adapter — not hardcoded in this phase)
- providerRequestId: not returned by `POST /content/series/tts` (File body only)
- decisionId: `c2fa9db0bc134d9bbb3c17af95e6f204`
- voiceName / accent / publicOwnerId: Anlee / northern / existing library pick

ElevenLabs calls: **1**

## 4. ARTIFACT
- artifactId: `vpa-20260915T161911Z-e0b3093f`
- storageKey: `FAMIXA:voice:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5`
- byteLength: `69007`
- durationSec: `4.24` (ffprobe `4.240000` — matches expected)
- sha256: `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C`
- physicalArtifactResolvable: `true`
- backend: existing `kit-famixa-tts` / `audio`

## 5. VERIFICATION
| Step | Result |
|---|---|
| provider bytes received | PASS (`audio/mpeg`, magic `ID3`) |
| durable save | PASS |
| durable reload | PASS |
| exact byte equality | PASS (`69007` == `69007`, page `exact: true`) |
| graph reference | PASS — only this line |
| post-write physical resolution | PASS — IDB get by canonical key still `69007` / `ID3` |

Graph after write:

```
voiceAssets.line-SC01-CHAR-003-1
  lineId: line-SC01-CHAR-003-1
  status: ready
  duration: 4.24
  characterId: CHAR-003
  artifactId: vpa-20260915T161911Z-e0b3093f
  storageKey: FAMIXA:voice:4ef164d6-…:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5
  physicalArtifactResolvable: true
```

acceptedTake `56`, `videoApproved` true, other voice lines unchanged.

## 6. LEGACY
- legacy `FAMIXA:tts:` used? **NO**
- old 69KB MP3 bound? **NO** (old Chrome blob SHA `741D5543…` ≠ new `E0B3093F…`; same size is coincidence)

## 7. SAFETY
- ElevenLabs calls: 1
- Fal calls: 0
- Motion changes: 0
- Keyframe changes: 0
- Canon changes: 0
- Final video changes: 0

## 8. NEXT ACTION
SH01 Voice artifact is physically verified and ready for LipSync V2.

Do not run LipSync in this phase.

Note for the next phase only: `startLipsync` still freezes `voiceArtifactId = lineId`. It must freeze `voiceAssets[lineId].artifactId` (`vpa-20260915T161911Z-e0b3093f`) or V2 will look up the wrong canonical key.
