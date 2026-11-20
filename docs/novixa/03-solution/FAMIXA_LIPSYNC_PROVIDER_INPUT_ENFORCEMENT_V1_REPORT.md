# FAMIXA_LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_V1_REPORT

**Mode:** Forensic + deterministic fix + tests. No Fal / ElevenLabs / Veo / Runway. No LipSync retry. No Final. No SH01 graph/media mutation.

---

## 1. Root cause of Take 56 substitution

Historical `lipsync_v3_01a0a327-…` uploaded **Take 56 LATE** (`7e8e90b4…`, 6.000s / 144 frames), not `mot:EP01-SC01-SH01:55`.

Two layers:

**Then (historical send):** provider video was built from mutable graph — `takeUrl` / `previewUrl` / `acceptedTake` / `turboTaskId` of the LATE Veo op. `FetchTakeForLipsyncAsync(takeTaskId)` reads that FileId. Freeze either did not exist or did not snapshot Take 55’s taskId/url.

**Now (remaining hole, fixed this phase):** even after V2 freeze, `startProvider` still:

1. Called `getContentSeriesTurbo(takeTaskId)` and **replaced** `videoUrl` with poll `videoUrl`.
2. Set `performanceDurationSec: performanceDurationOf(state, shot)` — last non-late / latest clock, not `frozen.motionDurationSec`.

So the adapter was not HOW-only. A frozen `mot:…:55` could still be discarded after freeze.

`resolveLipSyncMotionFromFrozenInput` already keyed by `motionArtifactId` n. The leak was **after** that resolve, in SeriesTab.

---

## 2. Root cause of Voice substitution

Locked artifact `vpa-20260915T161911Z-e0b3093f` was created **after** the historical Fal job. That job cannot be a V2 execution of this Voice.

**Remaining hole (fixed):** after freeze, `startProvider` looped `frozen.voiceLineIds` and re-read `stateRef.current.voiceAssets[lineId].artifactId`. A later graph change could load a **different** physical Voice (or fail to `lineId`). It also used `shotTimingOf` for `startSec`.

V2 `loadFrozenVoicePhysicalBytes` was already canonical `FAMIXA:voice:{buildId}:{artifactId}:{voiceId}`. The adapter was not required to use only those bytes.

---

## 3. Frozen → physical → provider chain

```
resolveAuthoritativeLipSync*   // BEFORE freeze only
        ↓
freezeLipSyncExecutionInput    // snapshots motionTaskId, motionUrl, lineId, voiceId, buildId
        ↓
submitLipSyncFrozenExecution
        ↓
resolveLipSyncMotionFromFrozenInput(frozen.motionArtifactId
        + frozen.motionTaskId/url, else attempt n — never acceptedTake)
        ↓
loadFrozenVoicePhysicalBytes(frozen.voiceArtifactId + buildId + voiceId)
        ↓
buildLipSyncProviderHowRequest // HOW: videoUrl/takeTaskId/duration from frozen only
        ↓
startContentSeriesLipsync(how) // adapter cannot pick 56 / current Voice
```

SH01 intended after this contract:

| Frozen field | Resolves to |
|---|---|
| `motionArtifactId` | `mot:EP01-SC01-SH01:55` |
| `motionTaskId` / `motionUrl` | Take 55 task + URL (4.000s mute 1280×720) |
| `voiceArtifactId` | `vpa-20260915T161911Z-e0b3093f` |
| `voiceId` | `kOIUKJ9E0DPndqTtvJm5` |
| canonical key | `FAMIXA:voice:4ef164d6-…:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5` |

Take 56 cannot win if freeze is 55, even when `acceptedTake=56`, `lastSuccessMotionTaskId=56`, current take=56.

---

## 4. Provider boundary

`buildLipSyncProviderHowRequest` is the HOW adapter.

It accepts only:

- frozen identities
- already-resolved `LipSyncProviderPhysicalInput` (url/taskId/bytes from freeze)
- provider/model + optional speaker pin (config)

It **throws** if `motionArtifactId` / `voiceArtifactId` / `motionUrl` / `motionTaskId` drift from freeze.

`performanceDurationSec` = `frozen.motionDurationSec` (not `performanceDurationOf`).

After freeze, SeriesTab `startProvider` no longer calls:

- `getContentSeriesTurbo`
- `resolveTakeUrl`
- `lastSuccessMotionTaskId`
- `performanceDurationOf`
- `voiceAssets[lineId]`
- `resolveLineAudio` / `FAMIXA:tts:`

---

## 5. Provenance

Reused `lipsyncExecutionAttempt` (no second architecture).

Added optional fields:

- `motionTaskId`, `motionUrl`
- `voiceId`, `buildId`, `lineId`
- `motionPhysicalFingerprint` (frozen motion fingerprint)
- `voicePhysicalFingerprint` (SHA-256 of **sent** Voice bytes when available)

`stampLipSyncProviderPhysicalProvenance` writes these **before** `startProvider`. No secrets. No credentialed URLs.

---

## 6. Tests T1–T13

`famixa-lipsync-provider-input-enforcement.smoke.ts` **PASS**

| ID | Result |
|---|---|
| T1 Freeze 55 + Voice → provider Motion 55 | PASS |
| T2 acceptedTake=56 → still 55 | PASS |
| T3 lastSuccessMotionTaskId=56 → still 55 | PASS |
| T4 current take=56 → still 55 | PASS |
| T5 Voice graph change after freeze → frozen bytes | PASS |
| T6 Canonical `FAMIXA:voice:` | PASS |
| T7 `FAMIXA:tts:` not selected | PASS |
| T8 lineId ≠ voiceArtifactId | PASS |
| T9 Exact Voice bytes | PASS |
| T10 Exact Motion 55 | PASS |
| T11 Provenance frozen identities + voice SHA | PASS |
| T12 No mutable selector freeze→provider | PASS |
| T13 Zero provider calls in helper | PASS |

Also: freeze V2 T1–T15 PASS · voice-artifact-binding T1–T10 PASS · identity + contract-v2 PASS.

---

## 7. Historical task treatment

`lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` **unchanged**.

Not deleted. Not reclassified as current V2 execution. Not used as proof that this contract works. File and graph pointer left as historical evidence of the 56 substitution.

---

## 8. Remaining gaps

1. Motion **byte** hash is not computed (browser never loads the mp4 into JS; identity is artifactId + snapped taskId/url).  
2. Speaker pin is still config from the run at freeze time, not a frozen physical artifact.  
3. Server `FetchTakeForLipsyncAsync` still accepts a `takeTaskId` — client now may only send the **frozen** taskId. A hand-rolled API POST could still point at 56.  
4. Old 4-field freezes without `motionTaskId` fall back to `runwayAttempts[n]` (still not acceptedTake).  
5. This phase did not persist a new freeze onto live SH01 (no production graph write).

---

## 9. Provider call counts

| Provider | Calls |
|---|---|
| Fal | **0** |
| ElevenLabs | **0** |
| Veo | **0** |
| Runway | **0** |

Production graph writes: **0**. Production media writes: **0**. Historical LipSync file: **untouched**.

---

## Files

- `client/admin/src/modules/content/famixa-lipsync-execution-input-freeze.ts`
- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx`
- `client/admin/src/modules/content/content-famixa-series.ts`
- `client/admin/src/modules/content/ContentFamixaShotProduction/index.ts`
- `client/admin/src/modules/content/famixa-lipsync-provider-input-enforcement.smoke.ts`

---

## STOP
