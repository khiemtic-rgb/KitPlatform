# FAMIXA_WORK_CONTROL_AND_EXECUTION_AUDIT_V1_REPORT

Read-only. Provider calls = 0. Media generation = 0. Graph writes = 0. DB writes = 0. Migration = 0. Product code changes = 0.

Discipline used in this document:

| Tag | Meaning |
|---|---|
| **FACT** | Observed in source, in a prior read-only forensic, or as a live desk event already recorded in this thread |
| **IMPLEMENTED** | Code exists in the tree |
| **TESTED** | A smoke/regression in-repo ran and asserted that code path |
| **ASSUMED** | Inference from code, not proven on a live shot this audit |
| **UNVERIFIED** | Patch or claim exists; desk behavior was not re-proven after it landed |
| **RECOMMENDED** | Contract proposal only — not implemented |

This audit does **not** write “đã fix”, “PASS”, “video generated”, or “LipSync successful” for any live SH01/SH02 job.

---

## 1. Executive Summary

FAMIXA does **not** keep a single durable chain:

`USER REQUIREMENT → TASK → ACTION → INPUT → PROVIDER/MODEL → OUTPUT ARTIFACT → BINDING → UI RESULT → REPORT`.

What exists today is a **shot run graph** (JSON) plus **local caches**, plus **provider task IDs**, plus a **client execution snapshot** that *derives* CURRENT / ACCEPTED / VISIBLE from that graph. Those layers do not share one Task/Attempt/Artifact identity.

**FACT (this thread, live desk):**

1. SH01 preview played mute Veo Take 56 while a 6.000s Fal V3 file was already on disk and still referenced on the graph (`FAMIXA_SH01_LIPSYNC_6S_PREVIEW_BINDING_FORENSIC_V1`).
2. SH02 first played SH01 Fal leftover, then the mute SH02 take.
3. After Save diễn xuất on SH02, primary CTA became **Khớp môi / Tạo hình mới** — **Tạo video** disappeared.
4. A later Veo job reached SUCCESS; desk toast was **Take xong nhưng chưa gắn** (`INPUT_CHANGED`); preview stayed on Take 3.
5. User-downloaded `g1hwIKJ2….mp4` / `(1).mp4` were the **same** SHA256 mute 5.03s file — not a new acting-beat take.
6. `compileI2vPrompt` can drop Shot 02 story when the acting beat is classified hold-body.

**IMPLEMENTED / UNVERIFIED:** later attach-fallback patches in `motionCallbackRunPatch` and acting-beat CTA restore. Desk was **not** re-proven after the last loosen (`Sao sửa rồi mà vẫn thế?` remains the last live SH02 motion fact).

**Verdict letter: D** — see §15.

---

## 2. Workflow Inventory

There is **no** formal Workflow table. IDs below are audit labels.

### WF-IMAGE / Keyframe

| Field | Value |
|---|---|
| Purpose | Still for the shot (Gemini) |
| Entry | Desk `Tạo hình` / `Tạo hình mới` → `ShotProductionWorkspace.handleAction('picture')` → `ContentFamixaSeriesTab.confirmCreateShotImage` |
| Input | Canon + studio still + shot WHAT / lock |
| Task | Client-side Gemini still job (not a DB task row) |
| Provider | Gemini (settings `ai.apiKeyConfigured`) |
| Output | `keyframeDataUrl` / IDB KF pixels |
| Artifact | Implicit still; hash on run (`kf.hash` / `kfSourceHash`) |
| Binding | `keyframeApproved`, picture CURRENT on execution snapshot |
| UI action | `approve-picture` (`Duyệt hình`) |
| Authority | Graph run + IDB KF + in-memory `kfPixelsOf` |

### WF-MOTION / Video

| Field | Value |
|---|---|
| Purpose | I2V take from approved still |
| Entry | `handleAction('motion')` → `startShotMotion` → Confirm → `POST /api/content/series/turbo` (`ContentController.StartSeriesTurbo`) |
| Input | Still pixels + `compileI2vPrompt` (may **not** equal on-screen story) |
| Task | Provider `turboTaskId` (`veo_…` / runway) |
| Provider | Veo (desk default) or Runway if routed |
| Output | MP4 under `App_Data/kit-video-i2v/veo/{FileId}.mp4` |
| Artifact | `mot:{shotId}:{n}` + `runwayAttempts[n]` + `takeUrl` / `previewUrl` |
| Binding | `validateMotionCallback` then `motionCallbackRunPatch`; `acceptedTake`; `videoApproved` |
| UI action | `Tạo video` / `Tạo lại video` / `Dùng video này` / `Dùng Take này` |
| Authority | **Split:** provider poll SUCCESS ≠ graph CURRENT ≠ `acceptedTake` ≠ preview URL |

### WF-VOICE / TTS

| Field | Value |
|---|---|
| Purpose | Spoken line audio |
| Entry | `Nạp thoại` → `ensureShotVoice` → Confirm → `ensureShotVoiceAssets` |
| Input | Line text + `voiceId` |
| Task | ElevenLabs request (no first-class Task row) |
| Provider | ElevenLabs |
| Output | Blob + optional physical file / IDB (`famixa-voice-physical-*-store`) |
| Artifact | Intended `vpa-…`; **FACT:** TTS persist has used `cue.id` / `lineId` as `artifactId` |
| Binding | `voiceAssets[lineId]`, graph voice ref, session `ttsFiles` / `ttsBlobs` |
| UI action | `Nạp thoại` / `Tạo thoại` |
| Authority | Graph + IDB + session Map — three writers |

### WF-LIPSYNC

| Field | Value |
|---|---|
| Purpose | Fal V3 mouth on a frozen take + voice |
| Entry | `Khớp môi` / `Khớp môi mới` / `Hỏi lại môi` → `startLipsync` → Confirm → `POST /api/content/series/lipsync` |
| Input | `LipSyncExecutionFrozenInput` (motion artifact + voice artifact) |
| Task | `lipsyncTaskId` (`lipsync_v3_{uuid}`) |
| Provider | Fal Sync |
| Output | `App_Data/kit-video-i2v/fal/{FileId}.mp4` (`FileId` = SHA256(taskId)[0:32]) |
| Artifact | `lipsyncUrl` / `lipsyncTaskId` / `lipsynced` |
| Binding | Freeze + `resolveCurrentProductionVideoSource` / `resolveCurrentLipsyncPreviewBinding` |
| UI action | Same handler, different `forceNew` / `resumeOnly` / `remake` flags |
| Authority | Freeze vs latest motion vs covering-take heuristic — **not one bind** |

### WF-MIX / Assemble

| Field | Value |
|---|---|
| Purpose | Cut / overlay / final short file |
| Entry | `Hoàn thiện Shot` → `assembleOneShot` → `POST /api/content/series/assemble` |
| Input | `resolveCurrentProductionVideoSource` (can pick covering take, not Fal) |
| Task | Assemble request (bytes returned; not a durable Task row) |
| Provider | Local assemble service |
| Output | Mix / final URL on run |
| Artifact | Final file identity is weaker than Veo/Fal FileId |
| Binding | `finalReady` / `finalArtifactReady` on snapshot |
| UI action | `mix` |
| Authority | Derived from current video source resolver |

### WF-FINAL / PREVIEW / ACCEPT / RETRY / REGENERATE / PROMOTE

| Workflow | Entry | Authority |
|---|---|---|
| Preview | `<video>` in `ShotProductionPreview` / workspace | Resolver stack (see §9) — **derived** |
| Accept | `Dùng video này` → `acceptExistingTake` | Writes `acceptedTake` on graph only |
| Approve | `Dùng Take này` / `approve-video` → `approveShotVideo` | Sets `videoApproved` — **not** a new artifact |
| Retry / resume | `Hỏi lại môi`, turbo poll | Same or new provider task; UI may keep old bind |
| Regenerate | `Tạo lại video`, `Khớp môi mới`, `forceNew: true` | New provider task; old CURRENT may remain visible |
| Promote / bind | `validateMotionCallback` + `motionCallbackRunPatch` | Can refuse SUCCESS (`INPUT_CHANGED`) |

---

## 3. Requirement → Task Traceability

Exact paths. No generic “the pipeline”.

### “Tạo hình”

```
ShotProductionWorkspace.handleAction('picture')
  → ContentFamixaSeriesTab.confirmCreateShotImage
  → Gemini still (client)
  → patchRun(keyframeDataUrl) + saveKfPixels
  → preview still (directorShowsStillPreview / pending picture)
```

**FACT:** `handleAction('picture')` also calls `actingEditorRef.save()` first. Saving the beat can flip `actingBeatPendingMotion` **in the same click** as image generation.

### “Tạo video”

```
handleAction('motion')
  → startShotMotion
  → Confirm modal
  → compileI2vPrompt (content-famixa-series.ts)
  → POST /api/content/series/turbo  (ContentController.StartSeriesTurbo)
  → poll GET /api/content/series/turbo?taskId=
  → validateMotionCallback (ShotProductionExecution.ts)
  → motionCallbackRunPatch
  → preview currentTakeUrl / covering take
```

**VIOLATION (P0):** User requirement = “video of this acting beat / story”. Implementation can send **hold-body** English and **drop story** (`compileI2vPrompt`). Provider SUCCESS can still be **not** bound (`INPUT_CHANGED`).

### “Tạo thoại”

```
Workspace onEnsureVoice
  → ensureShotVoice
  → hydrateSessionTts / resolveLineAudio  OR  Confirm → ensureShotVoiceAssets (ElevenLabs)
  → commitVoicePreview
```

**VIOLATION (P1):** Ready-check can treat `lineId` as voice artifact. Missing real `vpa-…` later blocks Fal (`thiếu Voice artifact` on SH01 remake).

### “Khớp môi”

```
handleAction('lipsync')
  → onEnsureLipsync(shotId, lipsyncActionOpts)
  → startLipsync([shotId], { forceNew?, remake?, resumeOnly?, confirmed? })
  → POST /api/content/series/lipsync  (StartSeriesLipsync)
  → graph lipsyncTaskId / lipsyncUrl
  → resolveCurrentLipsyncPreviewBinding → <video>
```

**FACT:** Labels `Khớp môi` / `Hỏi lại môi` / `Khớp môi mới` are **one action** (`lipsync`) with flags. Resume can poll an old Fal task; remake creates a new one. Preview may still show Veo.

### “Dùng Take này” vs “Dùng video này”

| Label | Handler | Effect |
|---|---|---|
| Dùng Take này / approve-video | `approveShotVideo` | `videoApproved: true` — **no new file** |
| Dùng video này / accept-existing | `acceptExistingTake` | stamps `acceptedTake` from **visible** take |

**VIOLATION (P2):** Two accept notions. Orchestrator `executionAllowsMotionRemake` returns false if `acceptedTake.url` exists — later “Tạo video” can vanish.

### “Tạo lại video”

Same as Tạo video (`action: 'motion'`). Desk `startShotMotion` Confirm uses `forceNew: true` **ASSUMED** from later SeriesTab path. **FACT:** CTA label is remake; orchestrator may still prefer `ENSURE_VOICE` / `ACCEPT_EXISTING`.

### “Chấp nhận”

No single Accept. Split across `acceptExistingTake`, `approveShotVideo`, picture approve, and implicit CURRENT promote on callback.

### “Mix”

```
handleAction('mix') → assembleOneShot
  → resolveCurrentProductionVideoSource
  → POST /api/content/series/assemble
```

**VIOLATION (P0):** Mix input is **whatever the preview resolver calls current**, including a covering mute take after LipSync was cleared.

### “Xem video”

```
handleAction('watch') → onWatch
```

Does **not** start a task. Plays the same derived preview source.

---

## 4. Task / Attempt / Artifact / Binding Storage

**FACT: there is no official Task / Job / Attempt / Artifact / Binding table.**

Persistence that exists:

| Store | What | Writer |
|---|---|---|
| `pack_content` series build JSON | Slim `SeriesPilotState` via `PUT /api/content/series/builds` (`PutSeriesBuild`) | `saveSeriesPilot` + server upsert |
| `localStorage` (`STORAGE_KEY`) | Same slim graph, first write in `saveSeriesPilot` | Client |
| IDB KF / voice physical | Pixels / voice blobs | `saveKfPixels`, voice physical stores |
| Session Maps | `ttsBlobs`, `ttsFiles`, Fal blob URLs | In-memory |
| Disk `veo/` `fal/` | Bytes keyed by FileId | API after provider download |
| Provider | Veo/Fal/ElevenLabs job ids | Provider |

### Identity table

| Concept | Current storage | Authority | Persisted? | Mutable? | Who writes | Who reads |
|---|---|---|---|---|---|---|
| User requirement | Shot story, acting beat, CTA label | **None as a task** | Shot fields yes | Yes | Editor / Save | `compileI2vPrompt`, orch |
| Task (Famixa) | **Missing** | — | No | — | — | — |
| Provider job | `turboTaskId`, `lipsyncTaskId` | Provider + poll | On graph | Overwritten on remake | SeriesTab / turbo service | Poll, preview FileId |
| Attempt | `runwayAttempts[]` (`n`, status, url, resultClass) | Client graph | Yes (slim) | Yes | Motion callback | validate / preview / LipSync freeze |
| Artifact id | `mot:shot:n`, Fal FileId, `vpa-…` or **lineId** | Mixed | Partial | Yes | Many | Many |
| Binding CURRENT | Derived `ShotProductionExecution` snapshot | Client function | No (recomputed) | Every hydrate | `snapshotOfShot` | CTA / preview |
| Binding ACCEPTED | `acceptedTake`, `videoApproved` | Graph flags | Yes | Yes | accept / approve | orch remake gate |
| Binding VISIBLE | `visibleTake`, preview URL, blob | UI resolver | Blob no | Yes | Preview / Fal fetch | `<video>` |
| FileId | SHA256(taskId)[0:32] (`FamixaVeoTakePlaybackV1.FileId`, Fal twin) | Disk name | File yes | No (hash) | API | `veo-video` / `fal-video` |
| Input snapshot | `frozenInput` / `lipsyncFrozenInput` | Partial | On run | Overwritten | Freeze helpers | validate / Fal send |

**FACT:** `providerRequestId`, `taskId`, `artifactId`, and `FileId` are used as if interchangeable. FileId is a hash of taskId, not a content hash of the MP4. Two jobs → two FileIds even if bytes match; one FileId does not prove the bytes match the user requirement.

---

## 5. Action Contract Violations

### CREATE_IMAGE

| | Expected | Actual |
|---|---|---|
| Input | Shot lock + canon | Same, plus auto-save acting beat |
| Operation | Gemini still | Gemini still |
| Output | Image artifact | Still on run/IDB |
| Side | — | Can set `actingBeatPendingMotion` |

Status: **mostly OK**. Side-effect on motion CTA = P2.

### CREATE_VIDEO

| | Expected | Actual |
|---|---|---|
| Input | Approved image + **motion contract / story** | Image + `compileI2vPrompt` which may replace story with `He stays in place.` |
| Operation | Motion task | `POST series/turbo` |
| Output | New VideoTake bound CURRENT | Provider SUCCESS **can** stay unbound (`INPUT_CHANGED`) |
| Forbidden | Image gen, voice, other shot | Does not call Gemini/ElevenLabs. **Can** keep showing previous take |

**VIOLATION.** User-visible “Tạo video” ≠ “new bound take of this beat”.

### CREATE_VOICE

Expected: TTS → voice artifact. Actual: may no-op (“Đã nạp thoại đã lưu”) or persist **lineId as artifactId**. **VIOLATION (identity).**

### CREATE_LIPSYNC

Expected: frozen CURRENT motion + voice → Fal → LipSync artifact CURRENT + preview. Actual:

- Can resume dead `lipsync_v3_…`
- Can send auto-detect / pin / bounding_boxes depending on patch era
- SUCCESS file can exist while preview plays Veo
- Quality “both mouths” is provider/quality, but **bind** is a FAMIXA defect when 6s Fal is ignored

**VIOLATION (bind + input).**

### MIX_VIDEO

Expected: current LipSync or accepted take. Actual: `resolveCurrentProductionVideoSource` covering-take path. **VIOLATION.**

### ACCEPT_ARTIFACT / BIND_ARTIFACT

Expected: one bind record (task, artifact, role). Actual: `acceptedTake` **or** callback CURRENT **or** `videoApproved` **or** preview heuristic. **VIOLATION.**

### REGENERATE

Expected: new attempt, old artifact remains addressable, UI binds **new** on SUCCESS. Actual: new job; UI often stays on old n; late SUCCESS can be classified LATE_RESULT or INPUT_CHANGED; late result can also **steal** preview (Take 56 vs bound 55). **VIOLATION.**

---

## 6. Output Identity Violations

Required tuple vs reality:

| Field | Typical presence |
|---|---|
| taskId | Provider id on run — **yes** for last turbo/lipsync |
| attemptId | `runwayAttempts.n` for motion; **no** first-class lipsync attempt |
| providerId / modelId | Implicit (Veo/Fal), not a stable row |
| inputSnapshot | Freeze objects **partial**; I2V prompt not stored as immutable snapshot on every attempt |
| artifactId | `mot:…` / FileId / `vpa-…` / **lineId** |
| fileId / storageKey | SHA256(taskId)[0:32] — **not** SHA256(bytes) |
| sha256 (bytes) | **Not** on graph. Only computed ad-hoc in forensics |
| duration | Sometimes on attempt / voice asset; preview uses media duration |
| mime | Assumed video/mp4 |
| createdAt | `acceptedAt` only on accept; jobs often missing |

**Anonymous outputs (FACT):**

- Mix/assemble bytes without a Task row
- Preview blob URLs (`blob:`) with no artifact id on the element
- TTS when `artifactId === lineId`
- SUCCESS turbo job that never lands on `takeUrl`

**ID collision class:** `taskId` used as query for `/veo-video` and `/fal-video`; FileId derived from that string. Graph `artifactId` for motion is `mot:shot:n`, not FileId. LipSync FileId looks like a content hash but is **not**.

---

## 7. Binding Violations

Required distinctions:

| State | Meaning | FAMIXA today |
|---|---|---|
| Task SUCCESS | Provider finished | Poll `SUCCEEDED` |
| Artifact CURRENT | This shot’s current output for that role | Derived; callback may refuse |
| Artifact ACCEPTED | User accepted | `acceptedTake` / `videoApproved` mixed |
| Artifact VISIBLE | What `<video>` plays | Resolver + leftover blobs |

**They are mixed.**

`validateMotionCallback` reasons (**IMPLEMENTED**): `UNKNOWN_ATTEMPT`, `INPUT_CHANGED` (pixel / inFlight / revision / whatChanged), `SUPERSEDED`, `ALREADY_FINAL`, `MISSING_FREEZE`.

**FACT (live SH02):** SUCCESS + toast `INPUT_CHANGED` + VISIBLE still Take 3. Same guard string is used for “pixels changed”, “in-flight mismatch”, “revision unproven”, and “won’t promote”. User cannot tell JOB_FAILED vs ARTIFACT_UNBOUND vs INPUT_CHANGED.

Later **IMPLEMENTED** fallback in `motionCallbackRunPatch`: if current `turboTaskId` SUCCESS, same task, has URL, not superseded, not `pixelClash` → stamp CURRENT even when validate says INPUT_CHANGED.

**UNVERIFIED** on desk after last loosen. Prior tighter fallback **failed live**.

**INPUT_CHANGED vs others — same family of message.** No separate `ARTIFACT_MISSING` / `STALE` / `UNBOUND` user copy on that toast.

LipSync: `lipsyncBindReleasedByNewerMotion` releases Fal when a **newer non-late** motion is CURRENT. Covering-take in `resolveCurrentLipsyncPreviewBinding` can **clear Fal and show Veo** while Fal file is valid (SH01 6s forensic). That is SUCCESS LipSync ≠ VISIBLE LipSync.

---

## 8. UI ↔ Backend Violations

| UI label | Handler | API / task | Output | Next CTA risk |
|---|---|---|---|---|
| Tạo video | `motion` → `startShotMotion` | `POST series/turbo` | Veo take **if** promote | Hidden when orch is ENSURE_VOICE / ACCEPT_EXISTING / LipSync current |
| Tạo hình / Tạo hình mới | `picture` → `confirmCreateShotImage` | Gemini (client) | Still | Can run after beat save |
| Nạp thoại | `ensureShotVoice` | ElevenLabs **or** local hydrate | Voice or no-op | CTA can demand voice **before** remake motion |
| Khớp môi | `lipsync` | `POST series/lipsync` or resume | Fal or old task | Shown when user wanted remake video |
| Hỏi lại môi | same, `resumeOnly` / failed | GET existing Fal | Old file or 404 | Looks like new quality fix |
| Khớp môi mới | same, `forceNew` | New Fal | New file | Preview may still be Veo |
| Xem video | `watch` | none | same preview | — |
| Dùng Take này | `approveShotVideo` | none | flag only | Blocks remake via accepted/approved gates |
| Dùng video này | `acceptExistingTake` | none | `acceptedTake` | Same |
| Hoàn thiện / Mix | `assembleOneShot` | `POST series/assemble` | mix bytes | Uses resolver source |
| Confirm (modal) | various | paid send | — | Confirm stays — **FACT** still required |

**Label ≠ action (P0/P2):**

- Primary **Khớp môi** after beat Save while user requirement was **Tạo video**.
- **Hỏi lại môi** ≠ new speaker-correct Fal.
- Toast **Take xong** (before attach patch) or **chưa gắn** while user hears “video generated”.
- **Xem video** / Preview Lip-sync caption can disagree with `<video>` src (6s forensic: caption and src **agreed** on Take 56 — the bug was **kind**, not a lying caption).

**Hidden / wrong-disabled:**

- Tạo video hidden by `executionAllowsMotionRemake` + voice-first orch + `deskLipsyncOutputCurrent`.
- Khớp môi hidden when LipSync CURRENT (quality fail still).
- Khớp môi mới added later as remake secondary — **IMPLEMENTED**; earlier SH01 had **no Fal send button** (**FACT**).

---

## 9. Preview Violations

### Resolver stack (SOURCE OF TRUTH vs DERIVED)

| Layer | Role |
|---|---|
| Disk `veo/{FileId}` / `fal/{FileId}` | Bytes for a **taskId** |
| Graph `takeUrl`, `lipsyncUrl`, `lipsyncTaskId`, `acceptedTake` | Pointers (slim; data URLs stripped) |
| `resolveTakeUrl` / `rewriteVeoTakePointers` / `rewriteFalLipsyncPointers` | Derived URLs |
| `resolveCurrentProductionVideoSource` | **Heuristic** current kind |
| `resolveCurrentLipsyncPreviewBinding` | Can **null** Fal and force take |
| `deskFalPlayableForShot` | Blob only if fetched shotId+taskId match |
| `currentTakeUrl` if motion CURRENT | Workspace |
| `directorShowsStillPreview` / `hideStaleAv` / `picturePixelMismatch` | Can hide take or show still |
| `muteTakeVoiceOverlay` | Overlay on mute Veo — not Fal |
| `<video src>` | **VISIBLE** |

**SOURCE OF TRUTH for bytes:** API disk keyed by FileId(taskId).  
**SOURCE OF TRUTH for “what the shot is”:** **does not exist** as one bind.  
**DERIVED CACHE:** graph URLs, blobs, execution snapshot, overlay.

**FACT violations:**

1. SH01 VISIBLE = Take 56 Veo; Fal 6s file unused (`06a032f4…`).
2. SH02 VISIBLE = SH01 Fal blob until `deskFalPlayableForShot`.
3. Preview kind `motion` while graph still had LipSync pointers.
4. `usesSpeechCoveringTake` **intentionally** prefers a take over LipSync when speech floor > bound duration — can hide a valid longer Fal if bind/duration math is wrong.
5. UI may pick artifact by **latest playable URL**, not by binding authority.

---

## 10. Cross-Workflow Contamination

**CRITICAL.** Shared `SeriesShotRun` + `saveSeriesPilot` merge + one orchestrator + one preview resolver.

| Shared thing | Contamination |
|---|---|
| `SeriesShotRun` | Image, motion, voice, lipsync, mix, flags on **one** object |
| `nextShotProductionCommand` | Voice-first unless `actingBeatPendingMotion`; one command drives all CTAs |
| `directorPictureVideoSurface` | Fallback last branch: LipSync primary, Tạo hình secondary — **hides motion** |
| `actingBeatFingerprint` inside voice fps | Beat edit → voiceStale → ENSURE_VOICE, not CONFIRM_MOTION |
| `acceptedTake.url` | Blocks motion remake after “accept” |
| `validateMotionCallback` / inFlight | Older PENDING can block newer SUCCESS (patched T5b — **TESTED** smoke, live SH02 still failed earlier) |
| `resolveCurrentProductionVideoSource` | LipSync, motion, mix, assemble **all** read it |
| `saveSeriesPilot` / hydrate | localStorage vs PUT build; KF stripped; stale merge |
| Fal blob session | Cross-shot play until scoped |
| `handleAction('picture')` save beat | Image click writes motion-pending |
| `compileI2vPrompt` hold-body | Story / WHAT not sent; video “succeeds” vs wrong contract |
| LATE_RESULT Take 56 | Motion steal vs LipSync freeze on 55 |
| Two-face mix-only leftover | Cleared on open — prior patch made SH01 **mute by policy** (user rejected) |

Generic `run` / `turbo` / `taskId` fields are overloaded. Broad workspace effects recompute snapshot and CTAs from the whole run.

---

## 11. Recent Patch Conflicts

Overlapping phases (docs + this thread). Later patch often **invalidates** the earlier “truth”.

| Patch / phase | Original problem | Files (representative) | New behavior | Side effect | Later invalidation |
|---|---|---|---|---|---|
| Shot Production contract / orch | Staff CTA chaos | `ShotProduction*` | Command machine | One command hides others | Acting-beat voice-first hid Tạo video |
| Execution validity CURRENT/STALE | Stale take shown | `ShotProductionExecution.ts` | INPUT_CHANGED refuse | Paid SUCCESS unbound | Attach fallback added, then loosened |
| Motion inFlight | Wrong promote | `validateMotionCallback` | Block if in-flight | Newer SUCCESS blocked by old PENDING | T5b: only newer n |
| hideStaleAv | Wrong take after picture change | Workspace | Hide AV | Same-pixel remake hid take | Gate → `picturePixelMismatch` |
| LipSync freeze / motion authority | Late take steal | `famixa-lipsync-motion-authority.ts` | Bound n wins | Preview can drop Fal | Covering-take still clears Fal |
| Fal-first + blob-only | 4s error file | playback + workspace | Auth blob + shot scope | Empty player if blob fail | SH01 user confirmed 6s **then** quality fail |
| Speaker pin / ASD / boxes | Both mouths | C# Fal client, pin helpers | Coordinates / boxes | Fal reject; 120 vs 96 frames | `FrameCountFromTake` |
| Mix-only mute SH01 | Close short | two-face mix | Mute policy | User rejected — not a quality proof | Cleared on shot open |
| Voice physical / rebind | lineId artifact | voice stores + graph rebind | vpa-… | Hydrate still has lineId paths | SH01 “thiếu Voice” after remake |
| Acting beat pending | Tạo video gone | orch, CTA, series run flag | CONFIRM_MOTION first | Extra flag authority | **IMPLEMENTED**; save-ux smoke still has unrelated fail |
| Callback attach fallback | INPUT_CHANGED drop | `motionCallbackRunPatch`, SeriesTab toast | Attach current turbo SUCCESS | Can attach wrong job if turbo id stale | **UNVERIFIED** live; user said still Take 3 |
| Preview covering | Short take vs speech | motion-authority resolvers | Show longer take | 6s Fal hidden | Forensic A: UI **is** Take 56 |

Patches **stack on the same functions**. There is no frozen contract; each incident adds a branch.

---

## 12. Report Discipline

Prior Cursor reports in `docs/novixa/03-solution/FAMIXA_*` **do not** consistently mark FACT / IMPLEMENTED / TESTED / ASSUMED / UNVERIFIED.

Examples of **discipline failure** (this program, not only docs):

| Claim style | Reality |
|---|---|
| “đã fix” after compile / smoke | Desk still Take 3 + INPUT_CHANGED |
| Smoke **PASS** | Does not prove SH02 bind or Fal mouths |
| “video generated” from provider SUCCESS | Artifact may be unbound |
| “LipSync successful” from Fal SUCCESS | Wrong speaker / both mouths / preview not that file |
| Forensic letter **A** on 6s report | Correct for “src is Take 56”; easy to misread as “preview is the 6s Fal” |
| “Tạo video đã trở lại” | True after Vite/API restart + CTA patch — **not** proof orch is sound |

This audit: SH02 attach patch = **IMPLEMENTED** + **TESTED** (local Execution T21/T22) + **UNVERIFIED** (desk). SH01 6s bind forensic = **FACT**. Hold-body drop = **FACT** in `compileI2vPrompt`.

---

## 13. P0 / P1 / P2 / P3 Findings

### P0 — can execute the **wrong user requirement**

1. **CREATE_VIDEO input ≠ story.** `compileI2vPrompt` hold-body drops Shot 02 backpack/unzip story. User pays for a sit-still take.
2. **SUCCESS ≠ BIND.** Paid Veo can finish; desk keeps old take (`INPUT_CHANGED`).
3. **Preview / mix source ≠ LipSync artifact.** Covering-take / Fal-clear can play mute Veo while Fal exists.
4. **Cross-shot VISIBLE.** SH02 played SH01 Fal blob.
5. **CTA remaps requirement.** After Save diễn xuất, primary action becomes LipSync/image, not motion remake.
6. **One `lipsync` handler** for resume vs remake vs quality retry — user can spend Fal on the wrong job type.

### P1 — wrong **artifact** returned or shown

7. FileId = hash(taskId), not bytes; reports can call a FileId “the 6s file” only after a separate hash.
8. Voice `artifactId === lineId` → Fal freeze miss / wrong bind.
9. Late motion Take 56 vs freeze on 55 — LipSync and preview disagree.
10. Duplicate download SHA `C1D2C31F…` — UI “new video” was old Take 3 bytes.
11. Auto Fal 4.25s treated as if it were the 6s bound file (historical covering).
12. `acceptExistingTake` binds **visible** URL, not the SUCCESS job just finished.

### P2 — state / UI inconsistency

13. Task SUCCESS / CURRENT / ACCEPTED / VISIBLE collapsed in toast and CTA.
14. localStorage vs `PUT series/builds` vs IDB vs session blobs.
15. `videoApproved` vs `acceptedTake` vs execution CURRENT.
16. Buttons hidden by unrelated gates (`acceptedTake`, voice fps, LipSync CURRENT).
17. Vite/API down + `/videoos` + `:51733` look like product bugs.
18. Confirm vs in-flight `turboBusy` — second Tạo video blocked while first result unbound.

### P3 — report / observability

19. No Task row; cannot answer “which task produced this file?” without archaeology.
20. No byte sha256 / mime / duration on graph as required evidence.
21. Reports say PASS/fix without desk replay.
22. INPUT_CHANGED is one string for many reasons.
23. Provider model/version not an immutable attempt field.

---

## 14. Proposed Work Execution Contract

**RECOMMENDED only. Not implemented.**

`FAMIXA WORK EXECUTION CONTRACT V1` — every paid or bindable action **must** persist all layers. Skipping a layer is a defect, not a shortcut.

```
REQUEST
  requestId, userAction, shotId, buildId
  requiredOutputType   (IMAGE | VIDEO_TAKE | VOICE | LIPSYNC | MIX)
  forbiddenFallbacks   (no silent task-type change)

TASK
  famixaTaskId         (our id, not provider)
  parentRequestId
  action               (= userAction; no remap without new REQUEST)

INPUT SNAPSHOT
  immutable hash of pixels / prompt / voice bytes / freeze
  story + acting beat + compiler output stored separately
  compilerMustNotDropStory without explicit user flag

ACTION
  exactly one of CREATE_IMAGE | CREATE_VIDEO | CREATE_VOICE |
                 CREATE_LIPSYNC | MIX_VIDEO | ACCEPT | BIND | REGENERATE

PROVIDER / MODEL
  providerId, modelId, frozen at send
  change = new TASK

ATTEMPT
  attemptId, n, startedAt
  providerJobId (separate column)
  status: PENDING | SUCCESS | FAILED | SUPERSEDED

OUTPUT ARTIFACT
  artifactId, role, mime, duration, byteSha256, storageKey
  providerJobId, attemptId, inputSnapshotHash
  SUCCESS with missing artifactId = FAILED_IDENTITY (not SUCCESS)

BINDING
  role: CURRENT | ACCEPTED | VISIBLE
  each is a row: (shotId, role, artifactId, boundAt, boundByTaskId)
  INPUT_CHANGED does not delete artifact; it unbinds CURRENT only

STATE
  task.state ≠ binding.CURRENT ≠ binding.ACCEPTED ≠ binding.VISIBLE
  UI reads VISIBLE from binding, never from “latest URL”

EVIDENCE
  request payload, provider request/response ids, file sha256, duration probe

REPORT
  FACT | IMPLEMENTED | TESTED | ASSUMED | UNVERIFIED | RECOMMENDED
  “video generated” only if VISIBLE.artifactId == that attempt’s artifact
  “LipSync successful” only if input freeze + output sha + preview src match
```

**Non-negotiable:** no generic `run()`. No CTA that calls a different ACTION than its label. No preview heuristic when a VISIBLE bind exists.

---

## 15. Final Verdict

**D — Unsafe for production execution.**

Not A: there is no single requirement→artifact control plane.  
Not B: fragmentation is not “workable” on the live path — paid jobs, wrong preview, missing CTA, dropped story already recurred across SH01 and SH02.  
Not only C: C describes the architecture; **D** matches the operational fact that a human cannot trust SUCCESS, preview, or the next button to mean the action they asked for.

This letter is **not** raised by leaving bugs unfixed in this audit. No code was changed to chase A.

---

### What this audit did not do

- Did not call Gemini, Veo, Runway, Fal, ElevenLabs.
- Did not create media, write graph, PUT production state, or migrate.
- Did not re-open the live SH02 graph this turn; SH02 Take 3 + INPUT_CHANGED is **FACT** from the last desk session, not a new inspect.
- Did not mark attach-fallback or acting-beat CTA as proven.

**End of report.**
