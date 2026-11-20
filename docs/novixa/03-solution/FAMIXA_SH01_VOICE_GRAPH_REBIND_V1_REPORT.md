# FAMIXA_SH01_VOICE_GRAPH_REBIND_V1_REPORT

## VERDICT
**PASS**

Existing physical Voice artifact rebound on SH01. No Voice generation. No LipSync. No provider media calls.

---

## ROOT CAUSE

Recovery (`FAMIXA_SH01_VOICE_RECOVERY_V1`) wrote `voiceAssets[lineId].artifactId` into **`pack_content.series_build`**.

The studio remount path does **not** load that row as the hydrate source:

1. `ContentFamixaSeriesTab` rAF-loads `localStorage` (`kit.famixaSeries.v4`).
2. Then `fetchContentSeriesPilot` reads **`pack_content.series_pilot`**.
3. `persistState(mergeRemotePilot(remote, local))` applies the merge.

`mergeRemotePilot` returned `{ ...graph }` where `graph` is the **pilot** object. It merged runs / voicePreview / sceneMasters, but **did not merge `voiceAssets`**. Remote duration-only rows replaced the Recovery bind.

`persistState` then `PUT /content/series/builds`, so the build row was overwritten back to duration-only. Physical IDB bytes were never deleted.

Serialization (`slimPilotForStorage` / `FamixaVoiceAsset`) does **not** strip `artifactId` / `storageKey`. The type already has those fields. The disappearance was hydrate merge, not schema strip.

**Fix (minimal):** `mergeVoiceAssets(remote, local)` on same-episode merge. Keep a real physical `artifactId` (never `lineId`) from whichever side still has it.

---

## PHYSICAL ARTIFACT VERIFICATION

Canonical identity loaded from `kit-famixa-tts` / `audio`:

| Field | Expected | Live |
|---|---|---|
| storageKey | `FAMIXA:voice:4ef164d6-…:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5` | match |
| byteLength | 69007 | **69007** |
| SHA256 | `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C` | **match** |
| MIME | `audio/mpeg` | **audio/mpeg** (ID3) |
| duration | 4.24s | **4.24** (`Audio` metadata) |

No generate. Same artifactId. No `FAMIXA:tts:` bind.

---

## GRAPH BEFORE / AFTER

`voiceAssets['line-SC01-CHAR-003-1']`

**Before**

```
lineId: line-SC01-CHAR-003-1
status: ready
duration: 4.24
characterId: CHAR-003
```

**After**

```
lineId: line-SC01-CHAR-003-1
status: ready
duration: 4.24
characterId: CHAR-003
artifactId: vpa-20260915T161911Z-e0b3093f
storageKey: FAMIXA:voice:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:vpa-20260915T161911Z-e0b3093f:kOIUKJ9E0DPndqTtvJm5
voiceId: kOIUKJ9E0DPndqTtvJm5
physicalArtifactResolvable: true
```

Other lines unchanged (`line-SC01-CHAR-001-2` still duration-only 2.48).

Persist: React state + `kit.famixaSeries.v4` + `PUT /content/series/builds` **200**.

---

## LIPSYNC RESOLVER (NO SUBMIT)

`resolveAuthoritativeLipSyncVoiceSource(current voiceAssets, lineId)`:

- artifactId = `vpa-20260915T161911Z-e0b3093f`
- voiceId = `kOIUKJ9E0DPndqTtvJm5`

`loadFrozenVoicePhysicalBytes` (isolated smoke + live IDB reload): **69007 B**, SHA `E0B3093F…`.

No LipSync attempt created.

---

## MOTION

- source = Take 55
- artifact = `mot:EP01-SC01-SH01:55`
- `resultClass` = CURRENT
- acceptedTake **56** untouched
- `videoApproved` **true** untouched

---

## TESTS T1–T15

Isolated smoke `famixa-sh01-voice-graph-rebind.smoke.ts` **PASS**. Live checks noted.

| ID | Result |
|---|---|
| T1 physical artifact exists | PASS (live + smoke) |
| T2 exact bytes | PASS (69007 / SHA live + smoke) |
| T3 graph artifactId restored | PASS |
| T4 graph storageKey restored | PASS |
| T5 physicalArtifactResolvable true | PASS |
| T6 resolver returns real artifactId, never lineId | PASS |
| T7 frozen Voice resolution returns real artifact | PASS |
| T8 wrong line cannot resolve | PASS (smoke) |
| T9 wrong voiceId cannot resolve | PASS (smoke) |
| T10 Motion remains Take 55 | PASS |
| T11 zero provider calls | PASS (no ElevenLabs / Fal / Runway / Veo) |
| T12 no new Voice artifact | PASS (same `vpa-20260915T161911Z-e0b3093f`) |
| T13 no Final changes | PASS |
| T14 no videoApproved changes | PASS |
| T15 no acceptedTake changes | PASS |

`content-famixa-kf-keep.smoke.ts` still PASS after merge change.

---

## PROVIDER CALL COUNTS

| Call | Count |
|---|---|
| ElevenLabs | **0** |
| Fal | **0** |
| Runway | **0** |
| Veo | **0** |
| New Voice generate | **0** |
| New artifactId | **0** |
| LipSync | **0** |
| Graph persist PUT builds | 1 (not a media provider) |

---

## PRODUCTION STATE CHANGES

| Field | Change |
|---|---|
| `voiceAssets[line-SC01-CHAR-003-1]` physical ref | **restored** |
| other voice lines | none |
| Motion / Take 55 | none |
| acceptedTake | none (56) |
| videoApproved | none (true) |
| Final | none |
| Keyframe | none |

Stopped. LipSync not run.
