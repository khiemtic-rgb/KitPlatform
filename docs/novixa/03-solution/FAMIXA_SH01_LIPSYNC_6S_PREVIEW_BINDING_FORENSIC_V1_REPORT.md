# FAMIXA_SH01_LIPSYNC_6S_PREVIEW_BINDING_FORENSIC_V1_REPORT

Read-only. 0 Fal / Veo / Runway / ElevenLabs. 0 generate. 0 graph write. 0 migration.

## Verdict

**NEEDS_FIX**

**Letter: A — UI đang thực sự phát Take 56.**

Caption `Video · take 56 · thoại phát cùng Play video (miệng chưa khớp)` matches the bound `<video>` source. It is not a wrong label on a Fal file.

The 6.000s Fal V3 file is on disk and the graph still points at it. The desk preview resolver **clears LipSync / Final on purpose** and plays Veo take 56 instead.

---

## 1. CURRENT UI VIDEO SOURCE

Live BÀN SHOT SH01 (`#fx-shot-studio video`), same origin graph `buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`.

| Field | Value |
|---|---|
| `video.getAttribute('src')` | `/api/content/series/veo-video?taskId=veo_cHJvamVjdHMvZ2VuLWxhbmctY2xpZW50LTA1MDk4NDAxOTUvbG9jYXRpb25zL3VzLWNlbnRyYWwxL3B1Ymxpc2hlcnMvZ29vZ2xlL21vZGVscy92ZW8tMy4xLWdlbmVyYXRlLTAwMS9vcGVyYXRpb25zLzg5ZGQ3NmNhLWY4Y2UtNDcxYy05ODAxLTk0MGM0OTExN2MyZA` |
| absolute URL | `http://localhost:5173/api/content/series/veo-video?taskId=veo_…89dd76ca…` |
| blob / object URL | none on the element at inspect (axios blob not attached; `veoPlaySrc` empty when fetch fails) |
| Fal `/fal-video` | **not** on the element |
| artifact FileId | `7e8e90b4f823be0cb99f9e3e7eac0fea` = SHA256(take-56 taskId)[0:32] |
| taskId | take **56** Veo op `veo_…89dd76ca-f8ce-471c-9801-940c49117c2d` |
| disk file | `App_Data/kit-video-i2v/veo/7e8e90b4f823be0cb99f9e3e7eac0fea.mp4` · 836517 B · **6.000s · H264 only (no audio)** |
| `preview.kind` | `motion` (`resolveShotPreviewKind` after covering cleared lipsync/final) |

```
CURRENT_UI_VIDEO_SOURCE = /api/content/series/veo-video?taskId=<take56 veo_>
CURRENT_UI_VIDEO_SOURCE_KIND = TAKE
CURRENT_UI_VIDEO_DURATION = 6.000s (assigned Veo file). Element loadedmetadata in an unauthenticated <video> tag failed (MEDIA_ERR_SRC_NOT_SUPPORTED). Authenticated axios path would play the same FileId, still mute Take 56 — not Fal 06a032f4.
```

Not inferred from caption. Caption is checked in §6 and agrees.

---

## 2. RESOLVER TRACE

Order on desk remount (`ShotProductionWorkspace`):

1. Hydrate `state` from `kit.famixaSeries.v4` (`loadSeriesPilot`). Graph **keeps** `lipsyncUrl` / `lipsyncTaskId`. No overwrite of LipSync fields.
2. `speechFloorSec = dialogueFloorOf(state, current)` — always a number (`0` if no floor).
3. `resolveCurrentProductionVideoSource({ …, speechFloorSec })`.
4. Because `speechFloorSec != null`, `speechCoveringMotionAttempt` runs.
5. `playbackSixSecondTake` picks attempt **n=56** (`duration: 6`, `SUCCEEDED`, playable pointer).
6. Return `{ kind: 'motion', motionN: 56, usesSpeechCoveringTake: true, lipsyncTaskId: undefined }`.
7. `resolveCurrentLipsyncPreviewBinding`: if `usesSpeechCoveringTake` → **`lipsyncUrl = undefined`, `finalUrl = undefined`**, `takeUrl` = Veo covering URL/blob.
8. Fal hydrate effect: `if (usesSpeechCoveringTake) { setFalPlaySrc(undefined); return; }` — **never** `fetchContentSeriesFalVideo`.
9. `resolveShotPreviewKind({ takeUrl, lipsyncUrl: undefined, finalUrl: undefined })` → `kind: 'motion'`.
10. `voiceSrc` is passed for `motion` → TTS overlay.

`editorialSourceOf` is **not** the desk player. It still prefers `lipsyncUrl` when `lipsynced` + `/fal-video` pointer (`content-famixa-editorial-cut.ts`). Assemble in `ContentFamixaSeriesTab` also calls `resolveCurrentProductionVideoSource` with `speechFloorSec` and would treat covering as mute Veo (`lipsynced = Boolean(lipPlay) && !usesSpeechCoveringTake`).

`lipsyncBindReleasedByNewerMotion` is **false** (take 56 is `LATE_RESULT`, bind `mot:…:55` still CURRENT). Covering does **not** go through release. It is a newer desk-only gate.

Overwrite is **preview assign**, not graph merge:

```
hydrate graph (LipSync CURRENT stays)
→ speechCoveringMotionAttempt → Take 56
→ resolveCurrentLipsyncPreviewBinding clears LipSync
→ <video src=Take56>
```

---

## 3. SH01 GRAPH (as-is)

`buildId`: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` · shot `EP01-SC01-SH01`

| Field | Live value |
|---|---|
| `acceptedTake` | `{ n: 56, url: /veo-video?taskId=veo_…89dd76ca…, acceptedAt: 2026-09-14T15:26:20.102Z }` |
| `currentTake` (graph key) | absent (`null`) — derived only in snapshot |
| `lastSuccessTake` (graph key) | absent — snapshot `lastSuccessTakeOf` = attempt **56** (last SUCCESS in array) |
| `visibleTake` (graph key) | absent |
| `videoApproved` | `true` |
| `status` | `reviewed` |
| `kfApproved` | `true` |
| `motionNeedsRemake` | `false` |
| `lipsynced` | `true` |
| `lipsyncStatus` | `SUCCEEDED` |
| `lipsyncTaskId` | `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` **CURRENT** |
| `lipsyncUrl` | `/api/content/series/fal-video?taskId=lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` |
| `lipsyncExecutionAttempt` | **null** |
| `lipsyncFrozenInput.motionArtifactId` | `mot:EP01-SC01-SH01:55` |
| `lipsyncError` | null |
| `finalSource` | `FAL` |
| `mixFrozenInput.videoArtifactId` | same `/fal-video?taskId=lipsync_v3_01a0a327…` |
| `takeUrl` / `previewUrl` / `turboTaskId` | Take **56** Veo pointer |
| attempt 55 | `CURRENT` · `SUCCEEDED` · duration stamp **5** · FileId `d006770c…` · 4.000s mute |
| attempt 56 | `LATE_RESULT` · `SUCCEEDED` · duration **6** · FileId `7e8e90b4…` · 6.000s mute |

LipSync records: **one** SUCCESS identity (`01a0a327`). No second CURRENT Fal task. Disk also has historical `16940abf…` (4.000s A+V) from an older V2 run — not this graph’s `lipsyncTaskId`.

---

## 4. FAL 6s FILE

`E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v\fal\06a032f474624bca2d0b609572b6d545.mp4`

| Check | Result |
|---|---|
| exists | yes |
| bytes | **3743506** |
| SHA256 | **55515811DB64741B6E4B3CB3AAA2085BE2D8450B118295ADB3064E661AA676BA** |
| duration | **6.000000** |
| video | h264 1280×720 |
| audio | aac 48000 Hz stereo (index 0) |

`FamixaFalLipsyncPlaybackV1.FileId("lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f")` = `06a032f474624bca2d0b609572b6d545`.

Playable application URL **exists**:

`/api/content/series/fal-video?taskId=lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f`

`ReadFalTake(taskId)` maps that FileId. Graph already stores this pointer. Prior bind mission played it as a 3743506-byte blob (`FAMIXA_SH01_LIPSYNC_NEW_OUTPUT_PREVIEW_BINDING_V1_REPORT` §9–10).

The file is **not** missing. The desk covering path **does not call** `fetchContentSeriesFalVideo` and **does not** pass `lipsyncUrl` into `resolveShotPreviewKind`.

---

## 5. OVERWRITE SITES

| Site | Writes graph? | Effect on SH01 desk |
|---|---|---|
| `speechCoveringMotionAttempt` / `resolveCurrentProductionVideoSource` | no | Take 56 wins when `speechFloorSec != null` |
| `resolveCurrentLipsyncPreviewBinding` covering branch | no | `lipsyncUrl`/`finalUrl` forced empty |
| Workspace Fal `useEffect` covering guard | no | skips Fal blob |
| Workspace `coveringTake ? undefined : lipsyncPlayable` | no | drops playable Fal before bind |
| `takeUrl` / `previewUrl` / `acceptedTake` on graph | already Take 56 | unused for LipSync identity |
| `editorialSourceOf` | no | would still return Fal pointer |
| `loadSeriesPilot` / merge | no | does not replace `lipsyncUrl` |

Not: LipSync URL failed → fallback. The Fal pointer is valid. Covering runs **before** LipSync kind is chosen.

---

## 6. UI LABEL

`Video · take 56` =

`directorProductionCaption({ kind: preview.kind, takeN: productionSource.motionN })`

- `kind === 'motion'` → `directorMotionCaption({ current: true, takeN: 56 })` → `Video · take ${n}`
- `takeN` = `productionSource.motionN` from covering take, **not** `acceptedTake` read in the caption helper, **not** editorial, **not** LipSync

Suffix ` · thoại phát cùng Play video (miệng chưa khớp)` =

`ShotProductionPreview` when `muteTakeVoiceOverlay(kind, voiceSrc)` is true (`kind` is `motion`|`video` and TTS `voiceSrc` is set).

If the element were Fal, `kind` would be `lipsync`, caption `Preview Lip-sync`, overlay **off**. Observed caption proves kind is motion + overlay. Matches §1 source.

---

## 7. CONCLUSION

**A. UI đang thực sự phát Take 56.**

Not B (caption matches TAKE).  
Not C (Fal URL resolver can bind; desk covering refuses to use it).  
Not D (file present, hash/duration match).  
Not E (graph LipSync fields unchanged).

### ROOT_CAUSE

Desk always passes `speechFloorSec` (a number) into `resolveCurrentProductionVideoSource`. That opt-in runs `speechCoveringMotionAttempt`, which selects the 6s LATE take 56. Covering then **nulls LipSync preview and skips Fal fetch**, so the hero `<video>` is mute Veo take 56 plus TTS overlay.

This covering gate was added to stop a 4s Final/bind-55 clip. Collateral: it also hides the current 6s Fal SUCCESS (`06a032f4…` / `01a0a327`).

### EVIDENCE

- Live `<video src>` = take-56 `/veo-video?taskId=veo_…89dd76ca…`
- Graph `lipsyncTaskId`/`lipsyncUrl` still CURRENT Fal 6s
- Disk Fal 6s file matches FileId(task)
- Caption + overlay only render for `kind=motion` + `motionN=56`

### EXACT_FILE

`client/admin/src/modules/content/famixa-lipsync-motion-authority.ts`  
`client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionWorkspace.tsx`  
`client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionPreview.tsx`

### EXACT_FUNCTION

`speechCoveringMotionAttempt`  
`resolveCurrentProductionVideoSource`  
`resolveCurrentLipsyncPreviewBinding`  
Fal `useEffect` in `ShotProductionWorkspace`  
`directorProductionCaption` / `muteTakeVoiceOverlay`

### EXACT_ASSIGNMENT

```ts
const cover =
  opts.speechFloorSec != null ? speechCoveringMotionAttempt(run, shotId, opts.speechFloorSec) : undefined;
if (cover) {
  return { kind: 'motion', motionN: cover.n, usesSpeechCoveringTake: true, lipsyncTaskId: undefined, … };
}
```

```ts
if (source.usesSpeechCoveringTake) {
  return { lipsyncUrl: undefined, takeUrl: …, finalUrl: undefined, holdLipsync: false };
}
```

```ts
if (!current?.id || motionPlayback?.usesSpeechCoveringTake) {
  setFalPlaySrc(undefined);
  return;
}
```

---

FAMIXA_SH01_LIPSYNC_6S_PREVIEW_BINDING_FORENSIC_V1_REPORT

Verdict: **NEEDS_FIX**

Provider calls: 0  
Media generation: 0  
Graph writes: 0
