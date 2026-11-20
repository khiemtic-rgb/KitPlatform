# FAMIXA PRODUCTION OS — CK24A
## SH02 COMPOSITE TIMELINE CONTRACT PREFLIGHT V1

Date: 2026-09-23  
Mode: **READ-ONLY**. No MIX_VIDEO. No FFmpeg process. No provider. No permit. No snapshot / artifact / binding / selection / graph write.

Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shot: `EP01-SC01-SH02`

---

### 1. VERDICT

**BLOCKED — contract missing**

Production OS can describe three clocks on this shot (director `10.0s`, VIDEO `8.000s`, LIPSYNC `2.500s`) and can remux CURRENT:LIPSYNC. It cannot express a generic rule for what MIX must be when those clocks disagree. No existing contract states that LIPSYNC replaces the whole take, only a speech interval, or an overlay, or that MIX duration must equal VIDEO, VOICE, LIPSYNC, or director timing.

---

### 2. CURRENT AUTHORITATIVE INPUTS

Live `pack_content` bindings and artifacts (SELECT only, this checkpoint):

| Role | ArtifactId | SHA | Duration | Binding |
|---|---|---|---|---|
| VIDEO_TAKE | `b44e530b-5b59-422f-9b80-e058a3a639b5` | `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34` | DB `8` / mvhd `8.000s` | `ACCEPTED:VIDEO_TAKE` |
| VOICE | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` | `bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298` | DB `2.534` / MPEG-frame parse `≈2.534s` | `CURRENT:VOICE` + `ACCEPTED:VOICE` |
| LIPSYNC (current) | `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e` | `725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e` | DB `2.5` / mvhd `2.500s` | `CURRENT:LIPSYNC` + `ACCEPTED:LIPSYNC` + `VISIBLE:LIPSYNC` + bare `ACCEPTED` + bare `VISIBLE` |
| LIPSYNC (historical CK23) | `32bec770-9706-4aad-bc21-8db3d4f11d4c` | `9d8e6949eb43480a60cee4e6058cff6239beb24191ed3fe06595e30d12b724db` | DB `8` / mvhd `8.000s` | **not** CURRENT / ACCEPTED / VISIBLE |
| MIX | — | — | — | **absent** (`MIX_VIDEO` requests = 0) |

Bare `CURRENT` on this shot is KEYFRAME `91daaf4b-…`, not VIDEO. Official SH02 video authority remains `ACCEPTED:VIDEO_TAKE` (CK21A). Historical LIPSYNC is not current authority.

---

### 3. TIMELINE AUTHORITY

Multiple duration fields exist. They are **not** one clock. MIX execution consumes only the LIPSYNC artifact duration (and then the remuxed bytes).

| Field / method | File | Class | Source of truth | Persisted? | Authoritative for? | Consumed by MIX execution? |
|---|---|---|---|---|---|---|
| `ProductionDirectorTiming.Ep01Sh01DurationSec = 5.0` | `ProductionDirectorTiming.cs` | `ProductionDirectorTiming` | SH01 director lock | In performance `source_json` when frozen | SH01 VIDEO / SH01 voice ingress only | **No** |
| SH02 director `ProductionDurationSec` | `ProductionEp01Sh02VoiceIngressCanon.cs` + `performance_snapshot.source_json` | `ProductionEp01Sh02VoiceIngressCanon.DurationSec = 10.0m` | Latest SH02 snapshots: `10`, `DIRECTOR`, `EXPLICIT_PRODUCTION_DECISION` | Yes | SH02 voice ingress gate (`!= 10` → `TIMING_PRODUCTION_DECISION_REQUIRED`) | **No** |
| CREATE_VIDEO snapshot `DurationSec` | `production_task_input_snapshots` task `9cd27409-…` | `FreezeInputAsync` | `10` (director / OS) | Yes | CREATE_VIDEO identity / Veo cost clock | **No** |
| `VeoMotionDurationMapping.ToProviderDurationSec` | `VeoMotionDurationMapping.cs` | `>= 8 → 8` | Provider-boundary only; comment: does not mutate snapshot | No extra column | Veo request length | **No** |
| VIDEO_TAKE `DurationSec` | `production_artifacts` | `VerifyArtifactCoreAsync` re-reads MP4 `mvhd` | `8` | Yes | VIDEO artifact duration | **No** |
| VOICE `DurationSec` | `production_artifacts` | voice verify / MPEG or claimed | `2.534` | Yes | VOICE artifact duration | **No** |
| CREATE_LIPSYNC snapshot `DurationSec` | `ProductionCreateVideoService.Actions.cs` `CreateLipsyncAsync` | `DurationSec = motion.DurationSec > 0 ? motion.DurationSec : 5` | Live CK23B snap `1125945c-…` = **`8`** | Yes | LIPSYNC **input** clock (video), not output | **No** |
| LIPSYNC artifact `DurationSec` | `FalLipSyncExecutionAdapter` then `VerifyArtifactCoreAsync` | measured from output bytes (`TryReadDuration`) | `2.5` | Yes | LIPSYNC **output** duration | **Yes** — MIX copies this into snapshot, then remuxes the file |
| `ProductionActionContractRegistry.MixVideo` | `ProductionActionContracts.cs` | RequiredInputs `["LipSyncArtifactId","Timing"]`; SuccessCondition parent = exact CURRENT:LIPSYNC | Contract text | Code only | MIX identity, not duration math | Timing **not** evaluated in `EvaluateCreateMixAsync` |
| `ProductionCreateVideoRequestDto.DurationSec` / `Timing` | `ProductionApiDtos.cs` | Client may send shot seconds | Request only | No (until freeze) | CREATE_VIDEO gate; MIX overwrites duration | DTO present; MIX server overwrites `DurationSec` |
| `ShotExecutionInput.DurationSec` / `Timing` | `ShotExecutionInput.cs` | No start/end/range fields | Input to freeze | Timing hashed; duration stored | Compiler `TIMING:` line is a string | MIX: `Timing` hashed only |
| `ProductionTaskInputSnapshot.DurationSec` / `TimingHash` / `ShotContract` / `ParentArtifactIds` | `ProductionEntities.cs` | Single duration. No segment list | Frozen row | Yes | Snapshot identity (`SnapshotHash` includes `DurationSec`) | MIX freeze `ShotContract` = `lipsyncId\|lipsyncSha`. Transport ignores `DurationSec` |
| `ProductionMixFreeze.Token` | `ProductionMixFreeze.cs` | `lipsyncIdN\|lipsyncSha` | Freeze string | In `ShotContract` | MIX parent identity | **Yes** (adapter requires parse) |
| `MixVideoAsync` duration assign | `ProductionCreateVideoService.Actions.cs:528` | `DurationSec = lipsync.DurationSec > 0 ? lipsync.DurationSec : 5` | CURRENT:LIPSYNC row | Written into MIX snapshot **if MIX ran** | MIX snapshot clock | Would be `2.5` now |
| `FfmpegMixExecutionAdapter.ExecuteFrozenAsync` | `FfmpegMixExecutionAdapter.cs` | Passes `snapshot.DurationSec` into request | Snapshot | — | Fake transport uses it; real transport does not | Real: **no** |
| `ProcessFfmpegMixTransport.RemuxOnceAsync` | `ProcessFfmpegMixTransport.cs` | `ffmpeg … -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy` | LIPSYNC bytes only | Temp files only if executed | Output duration = remuxed file | **Would remux LIPSYNC; not invoked this checkpoint** |
| `EvaluateCreateMixAsync` | `ProductionExecutionService.cs:526` | CURRENT:LIPSYNC identity only | Binding + artifact | — | MIX readiness | Does **not** read VIDEO, VOICE, duration mismatch, or Timing |
| `CreateVideoActionContractGate.IsValidTiming` | `CreateVideoActionContractGate.cs` | `durationSec > 0` **or** parse timing string `Ns` | CREATE_VIDEO only | — | CREATE_VIDEO required input `Timing` | **No** (not called on MIX) |
| Client Timing V3 `speechStartSec` / `speechEndSec` / `leadInSec` / `tailSec` / `productionDurationSec` | `famixa-shot-production-timing.ts` | Graph / desk clock | Client state | Graph, not OS MIX tables | Legacy desk / assemble fingerprints | **No** — official MIX path refuses `POST /content/series/assemble` |
| `mixInputFingerprint(lipsync, voice, motion, productionDurationSec)` | `ShotProductionFingerprint.ts` | Client hash | Client | Client stamp | Desk stale detection | **No** — OS MIX freeze is `ProductionMixFreeze` |
| SH01 MIX lock `LipsyncDurationSec = 5.042` | `ProductionEp01Sh01MixCanon.cs` | Locked SH01 lipsync duration | Code constant | — | Official SH01 MIX identity (with artifact id/SHA) | SH02 **not** in this canon |
| `ContentMixAssets.MasterArgs` `filter_complex` / `atrim` | `ContentMixAssets.cs` | Legacy series audio beds | Procedural WAV | Temp / App_Data mix beds | Legacy assemble master | **No** — UI cutover refuses that path for `MIX_VIDEO` |

**Proven MIX duration rule (implementation, not a composite policy):** if `MIX_VIDEO` ran now, snapshot `DurationSec` would be `2.5` and the file duration would be the remux of `b0b4f0f2` (`≈2.500s`). There is no 8.000s shot-clock field on the MIX freeze.

---

### 4. LIPSYNC SEMANTICS

**Proven (generic Production OS):**

- `CREATE_LIPSYNC` required inputs are `MotionArtifactId` + `VoiceArtifactId` (+ frozen provider + performance). Success condition: “Parents are exact VIDEO_TAKE+VOICE artifactIds.”
- Official SH02 parents are locked to VIDEO `b44e530b-…` + VOICE `0fde86c2-…` (`ProductionEp01Sh02LipsyncCanon`).
- Live CURRENT LIPSYNC `b0b4f0f2-…` persists `ParentArtifactIds = [VIDEO, VOICE]`.
- LIPSYNC **input** snapshot duration is the VIDEO clock (`8`). LIPSYNC **output** duration is whatever `TryReadDuration` measures on provider bytes (`2.5`).
- MIX required parent is CURRENT:LIPSYNC only. MIX does not treat LIPSYNC as a segment of VIDEO.

**Proven (this SH02 run, not a generic duration law):**

- CK23B sent `sync_mode=cut_off` and materialized `2.500s` / 60 frames vs VIDEO `8.000s` / 192 frames.
- Historical CK23 LIPSYNC `32bec770-…` is `8.000s` / 192 frames (same parents; different freeze / `silence`).

**SH01-specific (do not treat as generic):**

- `ProductionEp01Sh01LipsyncCanon`: VIDEO `5.042s`, VOICE `4.232s`.
- `ProductionEp01Sh01MixCanon`: MIX parent LIPSYNC duration `5.042s` (equals SH01 VIDEO, not SH01 VOICE).
- SH01 director lock is `5.0s`. That lock is not applied to SH02.

**Absent / UNKNOWN:**

- No OS enum or comment states LIPSYNC is (A) full VIDEO replacement, (B) speech-interval replacement, (C) overlay, or (D) the new shot duration.
- CK23A already listed `VideoStartSec`, `VoiceStartSec`, `SpeechStartSec/End`, `AlignmentMode` as **absent** on the official LIPSYNC path. CK23B added box + `cut_off`; it did **not** add those timing fields.
- `cut_off` **output-duration meaning** is not encoded as an OS rule.  
  **PROVIDER SEMANTICS = EXTERNAL / NOT AN OS CONTRACT.**

Do not infer A–D from the field name `LIPSYNC` or from Fal’s enum name.

---

### 5. REMAINDER / SEGMENT SEMANTICS

**NO EXISTING CONTRACT FOUND** in Production OS MIX / LIPSYNC / VIDEO for a remainder `2.5→8`.

Searched production code, tests, DTOs, persistence, and FFmpeg adapters for: speech window, start/end, offset, segment, source range, trim, pad, silence, tail, remainder, base/overlay video, replacement segment, timeline composition, concat, `filter_complex`, frame range, audio/video timeline.

| Hit | What it actually is | Applies to official SH02 MIX? |
|---|---|---|
| Client Timing V3 `speechStartSec` / `speechEndSec` / `leadInSec` / `tailSec` | Desk graph clock (`famixa-shot-production-timing.ts`) | **No** |
| `FamixaFalLipsyncAudioPrepV1.MergePadDurationSec` | Multi-voice desk pad; single voice keeps full bytes; “no duration trim field” | **No** (not MIX; not official SH02 adapter) |
| `ContentMixAssets` `filter_complex` / `atrim` | Legacy audio beds on an existing cut | **No** (legacy assemble refused) |
| `ContentSeriesAssembleService` `filter_complex` | Legacy episode concat | **No** |
| `ProductionAssemblyFreeze` | Film-level list of whole shot artifacts + each shot’s duration | Not intra-shot remainder; current `FfmpegAssemblyExecutionAdapter` requires **exactly 1** shot and remuxes it |
| `ProcessFfmpegMixTransport` | Single-file `-c copy` remux | No concat / overlay / pad |
| MP4 `elst` on the media files | Container edit list (see §6). Not an OS DTO | Not read by MIX |
| CK23A / CK24 reports | Document the absence | Evidence of gap, not a contract |

There is no persisted speech window, source range, or remainder row for this shot.

---

### 6. MEDIA FACTS

Inspected **file headers only** (Python ISO-BMFF / MPEG frame walk). **FFmpeg / ffprobe were not started.** Files were not rewritten. SHA matched DB.

**VIDEO** `b44e530b-…`  
`App_Data/kit-video-i2v/os-veo-out/5a72a533ca30466e99b0357fa47bcdd8.mp4`

| Item | Value |
|---|---|
| Duration (mvhd) | `8.000s` (timescale 1000, units 8000) |
| Video stream | `vide` / `avc1` present |
| Resolution | `tkhd` **1280×720** |
| Frame count | `stsz` **192** |
| FPS | 192 / 8.000 = **24** |
| Audio stream | **absent** |
| Edit list | 1 entry: `segment_duration=8000`, `media_time=1024` (container priming; not an OS field) |
| Start time (OS) | **not stored**. Container composition starts at movie t=0 |

**VOICE** `0fde86c2-…`  
Stored as `os-voice/9bd531131c244f2b84c25c392b1fb94d.mp4` but bytes are MPEG audio (`audio/mpeg`, 40586 bytes).

| Item | Value |
|---|---|
| DB `DurationSec` | `2.534` |
| This-checkpoint MPEG frame walk | 97 frames × 1152 @ **44100 Hz**, **mono**, 128 kbps → **`2.5339s`** |
| Prior CK23/CK24 ffprobe (same SHA; not re-run) | **≈2.480s** |
| Start/end OS fields | **absent** |
| Prior CK23B RMS (not re-run) | speech **0.10→2.40** on this VOICE |

The 2.534 vs 2.480 gap is a **measurement difference on the same bytes**, not a second file. OS persisted value is `2.534`.

**LIPSYNC** `b0b4f0f2-…`  
`App_Data/kit-video-i2v/os/833e816708624dbbba9d89a2fc3b9a4e.mp4`

| Item | Value |
|---|---|
| Duration (mvhd) | `2.500s` |
| Video | `vide` / `avc1`, `tkhd` **1280×720**, **60** frames, 60/2.5 = **24 fps** |
| Audio | `soun` / `mp4a` present |
| Audio mdhd | `2.503s` @ 44100 |
| Audio elst composition | `segment_duration=2480` → **2.480s** on the movie timescale |
| Video elst composition | `segment_duration=2500` → **2.500s** |
| Start time (OS) | **not stored**. Container `media_time=1024` is encoder delay, not a speech-window contract |

Historical LIPSYNC `32bec770-…` (not current): mvhd `8.000s`, 192 video frames, audio elst/mdhd ≈ `8.02s`. Same VIDEO+VOICE parents.

---

### 7. EXISTING TIMELINE MODELS

Classification only. **No ranking.**

| Model | Existing? | Contract? | Persisted? | Tested? | Current MIX safe? |
|---|---|---|---|---|---|
| **A** — 8s VIDEO remains the picture; LIPSYNC replaces 0→2.5; 2.5→8 from VIDEO | **No** | **No** | **No** | **No** | **No** — MIX never reads VIDEO; `-c copy` cannot splice |
| **B** — LIPSYNC is the entire picture; MIX ≈ 2.5s | **Yes as MIX implementation** | MIX identity = CURRENT:LIPSYNC. **No** written rule that the 8s take / 5.5s tail may be discarded | Would persist MIX ≈ 2.5s **if executed** | Generic remux / SH01 MIX tests. SH01 lock uses a **5.042s** lipsync that matches SH01 VIDEO — not this 2.5-vs-8 case | **Executable, not authorized** for this 8s take. Running it would publish ≈2.5s `CURRENT:MIX` |
| **C** — pad / expand LIPSYNC to 8s before MIX | **No** | **No** | **No** | **No** | **No** — no pad in transport |
| **D** — named timeline of VIDEO + LIPSYNC + VOICE segments | **No** in Production OS MIX | Client Timing V3 names speech windows on the **desk graph only** | No OS segment table | Timing V3 smokes are client-side | **No** |
| **E** — other evidenced model: MIX remuxes whatever LIPSYNC duration the provider wrote | **Yes** | Same as MIX remux contract. SH01 happens to remux a video-length lipsync. Historical SH02 `silence` lipsync is 8s; current `cut_off` lipsync is 2.5s | Artifact `DurationSec` is measured output | MIX regressions assert remux of LIPSYNC, not duration equality to VIDEO | Same as B for current CURRENT:LIPSYNC |

If MODEL A, C, or D were required, a **new** contract would be needed (see §11). That is not an implementation note disguised as a choice.

---

### 8. MIX PARENT CONTRACT

**What MIX freezes today**

```
ProductionMixFreeze.Token = lipsyncId|lipsyncSha
ParentArtifactIds        = [lipsyncId]
DurationSec              = lipsync.DurationSec   // 2.5 for b0b4f0f2
ShotContract             = freeze token
```

Official adapter: `server` / `ffmpeg`. Transport bytes: LIPSYNC only.

**What a true 8s composite would need (capability vs existence)**

| Need | Existing infrastructure can already hold? | Existing MIX freeze already binds it? |
|---|---|---|
| VIDEO_TAKE parent | Yes — `ParentArtifactIds` is a list; LIPSYNC already stores two parents | **No** |
| LIPSYNC parent | Yes | **Yes** |
| VOICE parent | Yes | **No** |
| Exact input hashes | Yes — SHA is on each artifact; freeze strings already carry SHA (lipsync today; lipsync+video+voice on CREATE_LIPSYNC) | LIPSYNC SHA only |
| Exact duration | One `DurationSec` on snapshot / artifact | LIPSYNC duration only; no output-vs-source split |
| Exact source ranges / timeline | **No field** on snapshot, artifact, or MIX freeze | **No** |
| Named remainder 2.5→8 | **No** | **No** |

A composite MIX would **not** need a second artifact system to name three parents and hashes. It **would** need a new freeze/token (or equivalent) for ranges and the chosen output clock. `ProductionMixFreeze` as written cannot bind that.

`ProductionAssemblyFreeze` can list several **whole shots** with duration. It is not an intra-shot segment timeline, and the assembly adapter remuxes one file.

---

### 9. REPLAY / IDEMPOTENCY

**Current MIX identity**

```
Idem(command, MIX_VIDEO, lipsync.ArtifactId.ToString("N"))
```

unless the client supplies `IdempotencyKey`. Official freeze still pins SHA via `ShotContract`.

| Event | Existing gate |
|---|---|
| CURRENT:LIPSYNC ≠ claimed id | `BINDING_CHANGED` (`MixVideoAsync` + `EvaluateCreateMixAsync`) |
| Foreign / historical LIPSYNC `32bec770-…` while CURRENT is `b0b4f0f2-…` | `BINDING_CHANGED` |
| Parent not this BuildId / ShotCode | `CROSS_SCOPE_FORBIDDEN` / `CROSS_SHOT_FORBIDDEN` (`RequireParentAsync`) |
| Wrong role / missing SHA / URL storage / byte SHA mismatch | `FAILED_IDENTITY` |
| Parent not VERIFIED | `FAILED_VERIFICATION` |
| Official SH01 MIX canon id/SHA mismatch | `BINDING_CHANGED` — **SH02 is not in that canon** |
| Official CURRENT:MIX already exists | `BINDING_CHANGED` (blocks a second MIX) |
| Same idem + succeeded MIX | `ReplaySucceededAsync` returns the same artifact; no second remux |
| In-flight / failed retry | `IN_FLIGHT_REPLAY` / reopen frozen snapshot |
| VIDEO_TAKE changes after MIX preflight | **No MIX gate** — VIDEO is not a MIX parent |
| VOICE changes | **No MIX gate** |
| LIPSYNC duration changes | Artifact bytes are immutable; a **new** LIPSYNC id changes freeze + default idem |
| Adapter not `server/ffmpeg` / paid network | `PROVIDER_MODEL_MISMATCH` / `PAID_NETWORK_FORBIDDEN` |
| Duration 8 vs 2.5 vs 2.48 | **No gate** |

Current freeze is **not** sufficient for a multi-parent composite MIX. VIDEO/VOICE/range drift would not invalidate a remux of an unchanged LIPSYNC.

---

### 10. GAPS

Evidence-backed only:

1. No generic Production OS rule for MIX duration when VIDEO ≠ LIPSYNC ≠ VOICE ≠ director clock (`10` / `8` / `2.5` / `2.534`).
2. No remainder / source-range / overlay / pad contract on the official MIX path.
3. `MixVideo` lists `Timing` but `EvaluateCreateMixAsync` does not validate or apply it.
4. `cut_off` is a SH02 canon/idem string and a Fal payload field. It is **not** in the LIPSYNC snapshot `ShotContract`. Its effect on output length is **not** an OS duration contract.
5. CREATE_LIPSYNC freezes `DurationSec=8` (VIDEO) while the CURRENT LIPSYNC artifact is `2.5` (measured). Two clocks, no reconciliation rule.
6. No `ProductionEp01Sh02MixCanon`. Official SH02 MIX would follow CURRENT:LIPSYNC only.
7. Client Timing V3 / `mixInputFingerprint` are not OS MIX authority.
8. CK21A preview still prefers `ACCEPTED:VIDEO_TAKE` over CURRENT:LIPSYNC — preview authority, not a MIX timeline (unchanged).

---

### 11. RECOMMENDED NEXT CHECKPOINT

**CK24B — MIX TIMELINE POLICY DECISION (design only).**

Do **not** implement. Do **not** run MIX. Do **not** treat convenience of `-c copy` as the product rule.

Existing code does **not** prove which model is correct. CK24B must obtain an explicit decision (or an explicit fail-closed) using at least:

1. **Output clock** — MIX duration must equal which of: VIDEO `8.000s`, LIPSYNC `2.500s`, VOICE (DB `2.534` / audio composition `2.480`), director `10.0s`, or a newly named duration.
2. **Picture rule** — LIPSYNC is full replacement, time-bounded replacement, overlay, or something else **written as an OS contract**.
3. If bounded: **exact source ranges** on VIDEO and LIPSYNC (start/end, timescale) and the **remainder source** for any uncovered VIDEO interval.
4. **Audio rule** — LIPSYNC A/V, VOICE only, VIDEO (mute), or a composed mix; whether 2.480 vs 2.534 vs 2.503 is fail-closed or measurement tolerance.
5. Whether `cut_off` length stays **provider-external** or is promoted to an OS expected-duration rule.
6. **Freeze fields** required so replay binds VIDEO id+SHA, LIPSYNC id+SHA, VOICE id+SHA, ranges, and output duration.
7. **Fail-closed vs execute** when those inputs disagree — including “CURRENT:MIX already exists”.

Until that decision exists, MIX remains BLOCKED.

---

### 12. PRODUCTION SAFETY

| Action | This checkpoint |
|---|---|
| POST `MIX_VIDEO` | **0** (`production_requests` for this shot + `MIX_VIDEO` = empty) |
| FFmpeg / ffprobe process | **0** (header parse only) |
| Fal / ElevenLabs / Vertex | **0** |
| MIX permit issued / consumed | **0** (permits on this shot are CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC only; none added) |
| New snapshot | **0** |
| New artifact | **0** |
| Binding / selection / graph / IDB mutation | **0** |

DB session: `readonly` + `autocommit`. Media: `read_bytes` of existing storage keys. No derivative files.

---

### 13. FINAL VERDICT

**BLOCKED — contract missing.** On official SH02 the clocks are director `10s`, VIDEO `8.000s` (mute, 192×24 fps), VOICE `≈2.53s` persisted / `2.480s` audio composition, LIPSYNC `2.500s` (60×24 fps + audio). MIX as coded would remux CURRENT:LIPSYNC only and publish ≈2.5s. That is a determinate remux, not a generic composite-timeline contract, and it does not say what happens to VIDEO `2.5→8`. Models A/C/D are not represented. Do not run MIX. Do not choose the product rule in code.

HARD STOP. No MIX. No FFmpeg. No snapshot. No permit. No implementation.
