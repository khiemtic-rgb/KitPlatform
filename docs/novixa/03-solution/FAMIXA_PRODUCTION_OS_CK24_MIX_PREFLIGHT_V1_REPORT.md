# FAMIXA PRODUCTION OS — CK24 MIX PREFLIGHT V1

Date: 2026-09-23  
Mode: **READ-ONLY**. No MIX_VIDEO. No FFmpeg remux. No permit. No snapshot. No binding/selection/graph mutation.

Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shot: `EP01-SC01-SH02`

Live bindings (read-only, this checkpoint):

| Role | Artifact |
|---|---|
| ACCEPTED:VIDEO_TAKE | `b44e530b-5b59-422f-9b80-e058a3a639b5` (8.000s) |
| CURRENT:VOICE / ACCEPTED:VOICE | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` (DB `DurationSec=2.534`) |
| CURRENT:LIPSYNC / ACCEPTED:LIPSYNC / VISIBLE:LIPSYNC | `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e` (2.500s, SHA `725df139…`) |
| Historical LIPSYNC | `32bec770-9706-4aad-bc21-8db3d4f11d4c` (8.000s) — **not** CURRENT |
| CURRENT:MIX | **absent** |
| MIX_VIDEO requests on this shot | **none** |

---

## 1. MIX ENTRY

```
Director CTA "Hoàn thiện"
  PRODUCTION_OS_CTA.MIX_VIDEO
  ShotProductionWorkspace retryOf('mix') / onExecuteOsAction(..., 'MIX_VIDEO')
→ ContentFamixaSeriesTab.executeProductionOsAction
    lipsyncId = osState.lipsyncArtifactId || LIPSYNC node
    refuse if !lipsyncId (MISSING_LIPSYNC)
    POST /api/content/production/requests
      action: MIX_VIDEO
      lipsyncArtifactId: lipsyncId
→ ContentProductionController.ExecuteOsActionAsync
→ ProductionCreateVideoService.MixVideoAsync
→ EvaluateCreateMixAsync
→ OpenFrozenAsync (server / ffmpeg)
→ FfmpegMixExecutionAdapter.ExecuteFrozenAsync
→ ProcessFfmpegMixTransport.RemuxOnceAsync
```

| Item | Exact |
|---|---|
| Action | `MIX_VIDEO` |
| Class / method | `ProductionCreateVideoService.MixVideoAsync` |
| Input DTO | `ProductionActionApiCommand` / `ProductionCreateVideoRequestDto` (`LipsyncArtifactId`, `DurationSec`, `Timing` present on DTO) |
| Official adapter | `FfmpegMixExecutionAdapter` |
| Transport | `ProcessFfmpegMixTransport` — **not** Fal / ElevenLabs / Vertex |
| Provider / model | `server` / `ffmpeg` (`ProductionMixCatalog`) |
| Paid / network | `IsPaid=false`, `RequiresNetwork=false` |
| Freeze | `ProductionMixFreeze.Token` = `lipsyncIdN\|lipsyncSha` |
| Output role | `MIX` (`ProductionActionContractRegistry.MixVideo`) |
| Official command | `ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4` |

There is **no** `ProductionEp01Sh02MixCanon`. SH01-only lock is `ProductionEp01Sh01MixCanon` (does not apply to SH02).

No official SH02 “prepareOfficialMix” path. Official film uses the same `executeProductionOsAction` MIX branch as the desk.

---

## 2. EXACT PARENT ARTIFACTS

MIX parent resolution in `MixVideoAsync` (official film):

```
currentLipsync = CURRENT:LIPSYNC
lipsyncId     = command.Input.LipsyncArtifactId ?? currentLipsync.ArtifactId
RequireParentAsync(..., LIPSYNC)
if claimed != CURRENT → BINDING_CHANGED
```

`EvaluateCreateMixAsync` uses the same CURRENT:LIPSYNC rule. It does **not** read VIDEO or VOICE.

| Expected parent | Used by MIX today? |
|---|---|
| VIDEO `b44e530b-…` | **No.** Not a MIX parent. Not in freeze. Not passed to FFmpeg. |
| VOICE `0fde86c2-…` | **No.** |
| LIPSYNC `b0b4f0f2-…` | **Yes**, if CURRENT:LIPSYNC stays this id (it does now). |
| Historical `32bec770-…` | **Not used** while CURRENT is `b0b4f0f2`. Sending `32bec` while CURRENT is `b0b4f0f2` → `BINDING_CHANGED`. |

Freeze / snapshot parents = **`[lipsyncId]` only**.

Not used: latest arbitrary artifact, KEYFRAME, TEST, graph URL, IDB. Action contract forbids those identity surfaces.

---

## 3. TIMELINE CONTRACT

**A. MIX timeline authority**

The MIX snapshot duration is:

```
DurationSec = lipsync.DurationSec > 0 ? lipsync.DurationSec : 5
```

For `b0b4f0f2-…` that is **2.5**.

`ProcessFfmpegMixTransport` does **not** read `request.DurationSec`. It remuxes the LIPSYNC file (`-c copy`). Output duration is then **measured from the remuxed bytes** (`TryReadDuration`).

There is no 8.000s shot-clock field on the MIX freeze.

**B. Same-duration required?**  
**No.** No comparison of VIDEO / VOICE / LIPSYNC durations exists on the MIX path.

**C. LIPSYNC shorter than VIDEO (2.5 vs 8.0)**

| Behavior | In current MIX code? |
|---|---|
| Pad | **No** (`-c copy` remux of LIPSYNC only) |
| Loop | **No** |
| Trim VIDEO | **No** — VIDEO is not an input |
| Keep VIDEO_TAKE 2.5→8.0 | **No** |
| Fail closed on mismatch | **No** |

**D. Voice 2.480s**

MIX does not ingest the VOICE artifact. Audio is whatever is already on the LIPSYNC file (CK23B: audio starts **0.000**, speech **0.10→2.40**, file audio **2.480s**).

On a MIX remux of `b0b4f0f2`:

- Voice start: **0.000** of the MIX file (copied from LIPSYNC)
- Voice end: **≈2.480s** of the MIX file
- Interval 2.480→8.000: **does not exist** on the MIX output (output ≈ 2.500s). It is not silence-padded to 8s.

**E. Named mix fields**

| Field | On MIX freeze / FFmpeg command? |
|---|---|
| start / end / offset / padding | **No** |
| base video / overlay video | **No** |
| timeline segment | **No** |
| duration | Snapshot `DurationSec` copied from LIPSYNC; transport ignores it |
| Timing DTO | Listed on the action contract; **not** applied as pad/trim/offset in `MixVideoAsync` |

Do not infer a composite timeline. It is not in the code.

---

## 4. DURATION CONTRACT

| Input | Artifact duration | MIX uses it? |
|---|---|---|
| VIDEO | 8.000s | No |
| VOICE | DB 2.534s / ffprobe ≈ 2.480s | No |
| LIPSYNC | 2.500s | **Yes — sole duration source** |

If MIX ran today, expected MIX file duration ≈ **2.500s** (copy of LIPSYNC), not 8.000s.

---

## 5. LIPSYNC SEMANTICS

For MIX, LIPSYNC is **not** modeled as:

- A. 8s whole-shot replacement  
- B. overlay segment onto VIDEO  
- C. speech-window segment with a named remainder

It is **D, scoped to the LIPSYNC file itself:** the LIPSYNC bytes are the entire MIX picture **and** audio. MIX remuxes them. The system does **not** treat `LIPSYNC = 8s`. `b0b4f0f2` stays **2.500s**.

SH01 mix lock (`LipsyncDurationSec = 5.042`) is documentation on SH01 canon only. SH02 has no such lock.

---

## 6. VOICE SEMANTICS

VOICE is a LIPSYNC parent (CK22/CK23), not a MIX parent. MIX does not place VOICE on a timeline, does not offset it, and does not pad it to 8s.

---

## 7. EXPECTED MIX OUTPUT (current contract)

If MIX_VIDEO were executed on this shot **as coded today**:

| Output | Value |
|---|---|
| Duration | ≈ **2.500s** (LIPSYNC remux) |
| Video source | LIPSYNC `b0b4f0f2` video track (`-map 0:v:0 -c copy`) |
| Audio source | LIPSYNC `b0b4f0f2` audio track (`-map 0:a:0 -c copy`) |
| VIDEO_TAKE 2.5→8s | **Dropped** (never read) |
| Composite VIDEO 0→8 + LIPSYNC 0→2.5 + VOICE 0→2.48 | **Not implemented** |
| Role | `MIX`, then `CURRENT:MIX` + `VISIBLE` |

Character / framing: `-c copy` does not re-encode. It does not alter CHAR-001 identity or the CK23B mouth result. It also does not restore the 8s take.

---

## 8. CURRENT FAILURE GATES

Fail closed **before** remux:

| Gate | Trigger |
|---|---|
| `MISSING_LIPSYNC` | no `LipsyncArtifactId` and no CURRENT:LIPSYNC |
| `FAILED_IDENTITY` | wrong role, URL storage, missing SHA, byte SHA mismatch, ffmpeg missing / remux fail / no A+V |
| `FAILED_VERIFICATION` | parent not VERIFIED |
| `BINDING_CHANGED` | official: claimed LIPSYNC ≠ CURRENT:LIPSYNC |
| `BINDING_CHANGED` | official SH01 canon id/SHA mismatch (SH02 **not** in this canon) |
| `BINDING_CHANGED` | official: CURRENT:MIX already exists |
| `CROSS_SHOT` / `CROSS_SCOPE` | parent task not this (BuildId, ShotId) |
| `PAID_NETWORK_FORBIDDEN` | official adapter paid/network (current ffmpeg is not) |
| `PROVIDER_MODEL_MISMATCH` | official adapter not `server/ffmpeg` |

**Not** fail-closed:

- VIDEO missing  
- VOICE missing  
- duration mismatch (8 vs 2.5 vs 2.48)  
- unsupported 8s shot clock  
- leftover VIDEO 2.5→8s  

MIX evaluate is available **now** because CURRENT:LIPSYNC exists.

---

## 9. GAPS

1. MIX is a **LIPSYNC remux**, not an 8s film mix. Running it would publish a 2.5s MIX as CURRENT:MIX.  
2. No SH02 mix canon locking `b0b4f0f2` + SHA (SH01-only). Official SH02 relies on CURRENT:LIPSYNC.  
3. No duration / remainder contract for `cut_off` lipsync vs 8s VIDEO_TAKE.  
4. Action contract lists `Timing`; MIX does not apply it.  
5. UI may send `durationSec` from shot seconds; server overwrites with LIPSYNC duration.  
6. CK21A preview still prefers ACCEPTED:VIDEO_TAKE — unchanged; not a MIX contract.

---

## 10. RECOMMENDED NEXT STEP

Do **not** run MIX_VIDEO until a written SH02 mix rule chooses one of:

1. **Fail closed** — MIX refuses unless LIPSYNC duration matches VIDEO (8.000s), or  
2. **Named remux** — MIX output is authorized as 2.500s (shot clock is no longer 8s), or  
3. **New composite contract** — VIDEO 0→8 + LIPSYNC picture 0→2.5 + audio 0→2.48, with explicit remainder 2.5→8.

Option 3 is **not** in the current code. Inventing it here would be a new checkpoint.

---

## PRODUCTION SAFETY (this checkpoint)

| Action | Done? |
|---|---|
| POST MIX / FFmpeg remux | No |
| Provider Start | No |
| New artifact | No |
| Permit consume | No |
| New snapshot | No |
| Binding / selection / graph mutate | No |

`pack_content.production_requests` for this shot + `MIX_VIDEO` = empty.

---

## VERDICT

**BLOCKED**

The MIX **implementation** is determinate: remux CURRENT:LIPSYNC `b0b4f0f2-…` → ≈2.500s MIX, ignore VIDEO and VOICE.

That is **not** a safe contract for **8.000s VIDEO + 2.500s LIPSYNC**. There is no pad, no remainder, and no fail-closed duration gate. Executing MIX now would drop 5.5s of ACCEPTED:VIDEO_TAKE without a named rule.

STOP. Do not run MIX.
