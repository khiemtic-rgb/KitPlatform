# FAMIXA PRODUCTION OS — CK24B
## MIX TIMELINE POLICY DECISION — DESIGN ONLY V1

Date: 2026-09-23  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shot: `EP01-SC01-SH02`

---

### 1. DESIGN STATUS

**DESIGN ONLY**  
**NO IMPLEMENTATION**  
**NO EXECUTION**

This document proposes a MIX contract shape and names the product decisions that must be approved before any code, freeze, adapter, or SH02 canon change. It does not choose a production rule. It does not run MIX.

No conflicting evidence was found against CK24A. CK24A remains the forensic baseline.

---

### 2. FACTS INHERITED FROM CK24A

1. Four clocks exist on this shot and are not one clock: director / OS `10.0s`; VIDEO_TAKE `8.000s`; VOICE DB `2.534s` / audio composition `≈2.480s`; LIPSYNC `2.500s`.
2. CURRENT MIX is absent. `MIX_VIDEO` requests on this shot = 0.
3. Current MIX implementation resolves CURRENT:LIPSYNC, freezes `lipsyncId|lipsyncSha`, sets snapshot `DurationSec` from LIPSYNC, remuxes LIPSYNC bytes (`-c copy`), and does not read VIDEO or VOICE.
4. No generic OS contract states that LIPSYNC is a full-shot replacement, a speech-window replacement, an overlay, or the new shot duration.
5. No OS MIX contract represents source ranges, remainder, or intra-shot composition.
6. `ProductionTaskInputSnapshot` can hold `ParentArtifactIds`, `DurationSec`, `TimingHash`, `ShotContract`, and hashes. Current `ProductionMixFreeze` binds only `lipsyncId|lipsyncSha`.
7. SH01 MIX canon (`LipsyncDurationSec = 5.042`, locked artifact/SHA) is SH01-scoped and must not become the SH02 rule.
8. Client Timing V3 is not Production OS MIX authority.
9. Fal `cut_off` is a provider input on the SH02 LIPSYNC path. Its output-length meaning is **PROVIDER SEMANTICS = EXTERNAL / NOT AN OS CONTRACT**.
10. VIDEO is mute. CURRENT LIPSYNC has picture + audio. VOICE is a LIPSYNC parent, not a MIX parent.

---

### 3. POLICY QUESTIONS

These are **not** engineering defaults. They are unresolved product decisions.

| ID | Question |
|---|---|
| P1 | What is MIX? Remux of CURRENT:LIPSYNC, fail-closed unless LIPSYNC duration matches a named clock, or an explicit intra-shot composition? |
| P2 | Which clock is the MIX **output** clock: director `10`, VIDEO `8`, LIPSYNC `2.5`, VOICE (`2.534` / `2.480`), or a newly named duration? |
| P3 | Is it acceptable for MIX to discard VIDEO `2.5→8`? If yes, where is that policy written? If no, what is the remainder source? |
| P4 | Is LIPSYNC picture a full-shot replacement, a time-bounded replacement, or something else? |
| P5 | Which audio is MIX audio: LIPSYNC embedded, original VOICE, silence, or composed? |
| P6 | Must VIDEO duration equal LIPSYNC duration before MIX may run? If yes, which measured values and what tolerance? |
| P7 | May the OS treat CK23B `2.500s` as an expected LIPSYNC length, or must expected length stay independent of Fal `cut_off`? |
| P8 | Is the chosen rule generic for all official shots, or SH02-only until a later generic cutover? |

Until P1–P8 are answered, field values below stay `<DECISION_REQUIRED>`.

---

### 4. EXISTING ARCHITECTURE REUSE

| Existing mechanism | Can represent | Cannot represent | Reusable? | Notes |
|---|---|---|---|---|
| `ProductionTaskInputSnapshot` | One `DurationSec`, `TimingHash`, `ShotContract` string, `ParentArtifactIds` list, provider/model, snapshot hash | Named segments, source ranges, picture vs audio tracks, remainder | **Yes — keep as the freeze row** | Do not add a second snapshot table. `SnapshotHash` already includes `DurationSec`, `TimingHash`, `VoiceHash`, `MotionHash`. Official MIX writes the freeze token into `VoiceRequirements` + `MotionRequirements` → those hashes. `ShotContract` / `ParentArtifactIds` are stored; ranges are not a column. |
| `ShotExecutionInput` | `DurationSec`, `Timing` string, parent ids, lipsync id | Start/end, segments | **Yes as input DTO** | Extending it with ranges would be a later delta, not a new plane. |
| `ProductionMixFreeze` | One LIPSYNC id + SHA | VIDEO/VOICE, ranges, output clock, contract version | **Reusable pattern, not sufficient token** | Comment: “No schema change.” Same pipe-token style as lipsync/voice/assembly. Current token cannot detect range or VIDEO SHA drift. |
| `ProductionLipsyncFreeze` | Multi-field token in `ShotContract` (line, speaker, two parents, pin, optional box) | MIX timeline | **Pattern only** | Proves `ShotContract` can grow a versioned suffix without a migration. Do not reuse lipsync pin fields for MIX. |
| `ProductionVoiceFreeze` | Line/speaker/voice/text in `ShotContract` | MIX | **Pattern only** | |
| `ProductionAssemblyFreeze` | Ordered **whole shots** with role, id, SHA, storage key, duration | Intra-shot segments, source start/end, audio vs picture | **Do not reuse for MIX** | Semantic confusion: assembly is film membership. Adapter already remuxes **one** file. Mixing MIX segments into assembly freeze would collapse shot-mix with episode-assemble. |
| `ProductionActionContractRegistry.MixVideo` | Action, required inputs, forbidden fallbacks, output role `MIX` | Duration math, remainder | **Yes — extend required inputs later** | Success condition today: parent = exact CURRENT:LIPSYNC. `Timing` is listed and unused. |
| `EvaluateCreateMixAsync` / `MixVideoAsync` | CURRENT:LIPSYNC identity, official CURRENT:MIX already-exists, cross-shot/scope, SHA | Duration mismatch, timeline validity | **Yes — add gates later** | |
| `FfmpegMixExecutionAdapter` + `ProcessFfmpegMixTransport` | Single-file remux `-c copy` | Concat, overlay, pad, dual input | **Reusable only for remux policy** | A compose policy needs a **new transport command**, not a second artifact store. Fake transport uses `DurationSec`; real transport ignores it. |
| `ProductionArtifact` + `ParentArtifactIds` | Role, SHA, duration, ancestor list | Ranges | **Yes** | LIPSYNC already has two parents. MIX can list 1–N without a new table. |
| CURRENT / ACCEPTED / VISIBLE | Named bindings per role | Timeline | **Yes — keep** | Do not add a second acceptance plane. |
| `ProductionEp01Sh01MixCanon` | Locked SH01 lipsync id/SHA/duration | Generic MIX / SH02 | **Keep SH01-only** | Must not be copied as SH02 rule. |
| `ProductionEp01Sh02LipsyncCanon` | SH02 LIPSYNC parents, box, `cut_off` | MIX | **Do not extend into MIX canon until P1–P8** | `cut_off` stays a LIPSYNC provider input. |
| `ProductionDirectorTiming` / performance `ProductionDurationSec` | Explicit VIDEO-stage director clock | MIX output, remainder | **Witness only** | SH02 value `10.0` is not consumed by MIX today. Using it as MIX output is P2. |
| Client Timing V3 | Desk speech windows | OS MIX | **No** | Not MIX authority. Do not import `speechStartSec` as an OS field unless P4/P5 explicitly adopt those numbers from a **new** OS source. |
| `ContentMixAssets` / series assemble | Legacy beds / concat | Official MIX | **No** | UI already refuses this path. |
| `VeoMotionDurationMapping` | Provider-boundary 10→8 | MIX | **No** | CREATE_VIDEO only. |

**Reuse rule:** keep one snapshot, one MIX artifact role, one binding model, one freeze-token pipe. Version the token. Do not invent a second production authority plane.

---

### 5. TIMELINE OPTIONS

Do **not** rank. Do **not** treat any row as selected.

| Model | Definition | Required contract | Compatibility with current artifacts | Implementation consequence | Open policy question |
|---|---|---|---|---|---|
| **A — full 8s LIPSYNC** | LIPSYNC must be the complete picture clock (here: VIDEO `8.000s`). MIX remuxes that LIPSYNC. | Duration-match gate: LIPSYNC duration vs named clock (VIDEO unless P2 says otherwise). MIX freeze can stay remux + match witness. | CURRENT LIPSYNC `2.500s` **fails**. Historical `32bec770` is `8.000s` but is not current and was quality-failed. | No compose FFmpeg. MIX stays remux. Another LIPSYNC strategy would be required before MIX. `cut_off` as used on CK23B is incompatible with this model unless a later LIPSYNC is video-length. Speech-free tail and post-speech audio become **LIPSYNC-provider** problems, not MIX compose. | P1/P6/P7. Is 8s VIDEO the clock, or director 10s? |
| **B — 2.5s LIPSYNC is the complete MIX** | MIX output = LIPSYNC file. VIDEO `2.5→8` is not in the MIX. | Explicit written rule: output clock = LIPSYNC artifact duration; VIDEO remainder discarded. Freeze can stay LIPSYNC id+SHA **if** that discard rule is the approved policy. | Compatible with CURRENT LIPSYNC `b0b4f0f2`. Incompatible with reading this MIX as an 8s take. Director 8/10 clocks become **not** MIX output. | Current adapter can execute this today. CK24/CK24A blocked it because the discard rule is unwritten. | P3: is discarding 5.5s VIDEO acceptable, generic, or SH02-only? |
| **C — composite timeline** | MIX is a named intra-shot composition (example: LIPSYNC picture 0→2.5, VIDEO picture 2.5→8, audio 0→≈2.48 then an explicit remainder). | Segment list: dest start/end, source role/id/SHA, source start/end, picture vs audio, output duration, remainder mode. VIDEO and LIPSYNC become **byte** parents if those bytes are read. | Artifacts exist. Ranges do **not**. Example numbers are not an OS fact. | Current `-c copy` remux **cannot** do this. New transport, not a new artifact system. | P2–P5: exact ranges, audio remainder (silence / mute VIDEO / none), whether 0.000 is assumed. |
| **D — fail closed** | MIX refuses when VIDEO duration ≠ LIPSYNC duration (or ≠ chosen clock). | Named clocks, equality or tolerance, fail reason. MIX does not invent a remainder. | CURRENT SH02 MIX is not executable. Same as A for this shot until another LIPSYNC exists. | Smallest code delta if approved: gate only; no compose. | P6: exact vs tolerant; which clock; does this make CK23B unusable for MIX? |
| **E — other (evidenced)** | MIX remuxes **whatever duration the provider wrote** on LIPSYNC (CK24A model E). SH01 happens to remux a video-length lipsync; SH02 current is 2.5s; historical SH02 is 8s. | Same as current implementation, but **named** as “output = measured LIPSYNC duration, no duration invariant.” | Compatible with any LIPSYNC length. | Already implemented. Still a policy: it silently changes MIX length when LIPSYNC length changes. | Is unnamed remux-length a allowed generic rule, or only an accident of the adapter? |

Engineering consequences are not votes. A and D keep remux and block this LIPSYNC. B and E allow remux of `b0b4f0f2`. C requires composition and a new freeze.

---

### 6. PROPOSED CANONICAL MIX CONTRACT

Minimum generic **shape** so a later approved policy can be written without a second snapshot system.

Do not treat the example numbers in Model C as filled values. Values that choose P1–P8 stay `<DECISION_REQUIRED>`.

```
MIX_CONTRACT_V1
{
  ContractVersion: "MIX_CONTRACT_V1"
  CompositionMode: <DECISION_REQUIRED>
    // allowed names only after approval, one of:
    //   REMUX_LIPSYNC
    //   FAIL_CLOSED_DURATION_MATCH
    //   COMPOSE_SEGMENTS

  OutputClock: <DECISION_REQUIRED>
    // one of: VIDEO_TAKE | LIPSYNC | VOICE | DIRECTOR | EXPLICIT

  OutputDurationSec: <DECISION_REQUIRED>
  DurationToleranceSec: <DECISION_REQUIRED>
    // required only if CompositionMode uses a match gate
    // do not invent 2.480 vs 2.534 tolerance here

  Parents: [
    {
      Role: LIPSYNC | VIDEO_TAKE | VOICE
      ArtifactId: <bound>
      Sha256: <bound>
      Binding: CURRENT | ACCEPTED   // which named binding was required
      Consume: BYTES | IDENTITY     // BYTES = transport reads file
    }
  ]

  PictureSegments: <DECISION_REQUIRED>
    // empty / omitted when CompositionMode = REMUX_LIPSYNC
    // otherwise ordered, non-overlapping, covering [0, OutputDurationSec]
    // each: DestStartSec, DestEndSec, SourceRole, SourceArtifactId,
    //       SourceSha256, SourceStartSec, SourceEndSec

  AudioSegments: <DECISION_REQUIRED>
    // same shape; may name SILENCE as a source kind without a new artifact
    // omitted when remux copies LIPSYNC A+V as one file

  TimingHash: <existing snapshot TimingHash; not Timing V3>
  ContractHash: <hash of ContractVersion + mode + clock + duration
                 + parents + segments>
}
```

**Why these fields and not more**

| Field | Necessary because | Ambiguity removed | Already representable? |
|---|---|---|---|
| `ContractVersion` | Current freeze has no version. A later compose must not replay as remux. | Old remux vs new contract | New token prefix in `ShotContract`. No new table. |
| `CompositionMode` | P1 is the missing discriminator. | Remux vs match-gate vs compose | Not present. Encode in freeze token. |
| `OutputClock` | Four clocks exist. | Which duration MIX claims | Not present. `DurationSec` is a number without provenance. |
| `OutputDurationSec` | Snapshot already stores one duration; it must be the **chosen** output clock, not an accidental LIPSYNC copy. | Input snapshot 8 vs artifact 2.5 vs director 10 | `ProductionTaskInputSnapshot.DurationSec` already exists. |
| `DurationToleranceSec` | Only if P6 chooses a match. | Exact vs slop | Not present. Do not fill until measurement policy exists. |
| `Parents[]` Role+Id+SHA+Binding+Consume | CK24A: VIDEO/VOICE can change with no MIX gate. | Ancestor vs consumed input | `ParentArtifactIds` holds ids. SHA/role/binding/consume need the freeze token. |
| `PictureSegments` / `AudioSegments` | Only if P1 = compose. | Remainder 2.5→8, audio vs picture | **Not** representable today. Do **not** add if remux/fail-closed is approved. |
| `TimingHash` | Already on snapshot; MixVideo lists Timing. | Prevents silent Timing string drift **after** Timing is defined for MIX | Exists. Today unused by evaluate. |
| `ContractHash` | Replay must fail if mode/ranges/version change even if LIPSYNC id is unchanged. | Same ids, different composition | New. Can live in freeze token; `MotionHash`/`VoiceHash` already hash that token. |

**Not proposed (would over-engineer)**

- Overlay, filter graphs, multi-track NLE, episode assembly, frame-accurate editor, pad/loop/bounce as first-class MIX modes, Fal `sync_mode` on the MIX contract, client `speechStartSec` imported by name.

`SILENCE` as an audio source kind is allowed only if P5 names an audio remainder. It is not a new artifact role.

---

### 7. PICTURE SEMANTICS

Define sources. Do not assign this shot’s ranges until P1/P4 are approved.

| Source | What it is | What MIX may do with it | What MIX must not infer |
|---|---|---|---|
| VIDEO_TAKE | Verified mute picture, here `8.000s` / 192×24 fps / 1280×720 | Consume bytes **only** if `Consume=BYTES` (compose remainder or full-take picture). Or bind `IDENTITY` if used only as duration witness (A/D). | That LIPSYNC replaced 0→8. That 2.5→8 is kept. That director 10s exists as pixels. |
| LIPSYNC | Verified derived picture (+ audio), here `2.500s` / 60×24 fps / 1280×720 | Current implementation treats it as the **entire** MIX picture. Under A/D it must match the output clock. Under C it is one picture segment. | That `2.5s` means “speech window 0→2.5 on the 8s take.” That is not an OS field. |
| KEYFRAME / historical LIPSYNC | Not MIX picture | Forbidden fallbacks already listed on `MixVideo` | “Latest playable” |

If P1 = remux or fail-closed: picture = LIPSYNC file, one segment implied, no `PictureSegments` array.

If P1 = compose: every output picture interval must name a source artifact and a source range. Unnamed gaps or overlaps fail closed. VIDEO remainder is **not** implied by LIPSYNC being shorter.

---

### 8. AUDIO SEMANTICS

Separate from picture.

| Source | Fact | MIX implication |
|---|---|---|
| VIDEO | Mute. No audio stream. | Cannot supply speech. Can only supply silence-by-absence if picture remainder is VIDEO. |
| VOICE | LIPSYNC parent. DB `2.534s`; composition `≈2.480s`. | Direct MIX parent **only if** MIX reads VOICE bytes or P5 names VOICE as the audio source. Ancestor-of-LIPSYNC is not enough. |
| LIPSYNC embedded audio | Present. `elst` composition `2.480s` on a `2.500s` movie. | Current remux copies this track. Under remux policy, VOICE is **not** a MIX parent. |
| Speech start/end | Not OS fields. Prior RMS 0.10→2.40 is checkpoint observation. | Do not write `0.10` / `2.40` into MIX contract unless a new OS measurement authority is approved. |
| 2.480 vs 2.534 vs 2.503 | Same VOICE bytes, different measures; LIPSYNC audio mdhd `2.503` vs elst `2.480` | **UNKNOWN — DECISION REQUIRED** before any audio tolerance. Need an approved measurement method (which clock: artifact `DurationSec`, `mvhd`, `elst`, MPEG frames). Do not guess ε. |

Audio remainder `2.48→8` is **not** defined. Options (policy, not design default): silence, omit (output ends at speech), or fail closed. VIDEO cannot fill it with original take audio.

---

### 9. DURATION SEMANTICS

| Clock | Role today | Eligible as MIX output? |
|---|---|---|
| Director `10.0` | VIDEO-stage explicit decision; SH02 voice ingress lock | Only if P2 says so. No 10s media exists. Using it implies pad or fail. |
| VIDEO `8.000` | Artifact / `mvhd` | Candidate for A/C/D picture clock. |
| LIPSYNC `2.500` | Artifact / `mvhd`; current MIX snapshot source | Candidate for B/E. |
| VOICE `2.534` / `≈2.480` | Artifact vs composition | Candidate only if P5/P2 name VOICE. Not interchangeable until measurement policy exists. |

**Proposed rule shape (not a chosen number):**

- `OutputDurationSec` is the single MIX claim (already one snapshot field).
- `OutputClock` says which source that number is taken from.
- Source durations stay on their artifacts; they are not overwritten.
- Expected **measured** MIX file duration = `OutputDurationSec` within `DurationToleranceSec` **after** tolerance is approved.
- Do not resolve 2.480 vs 2.534 in this checkpoint.

**Evidence still required before tolerance (P6):**

1. Named measurement function (the same `ProductionTestMp4.TryReadDuration` / MPEG helper the OS already uses, or a new named one).
2. Whether LIPSYNC `elst` 2.480 vs `mvhd` 2.500 is in-tolerance for **audio** vs **movie**.
3. Whether VOICE DB `2.534` is allowed to differ from LIPSYNC audio composition when VOICE is not a MIX parent.

Until then: `DurationToleranceSec = <DECISION_REQUIRED>`.

---

### 10. PARENT / HASH BINDING

Principle: direct MIX parent only if MIX consumes bytes **or** the identity is part of the composition contract.

| Policy | LIPSYNC | VIDEO_TAKE | VOICE |
|---|---|---|---|
| B / E remux | **BYTES** parent. Freeze id+SHA. Must be CURRENT:LIPSYNC. | Not a parent unless P3 requires a written discard witness (IDENTITY only). Default: not consumed. | Ancestor of LIPSYNC only. Not MIX parent. |
| A / D match | **BYTES** parent (remux). | **IDENTITY** parent if the match clock is VIDEO (id+SHA+duration). Bytes not read. | Not MIX parent unless match clock is VOICE. |
| C compose | **BYTES** if any picture/audio segment cites it. | **BYTES** if any segment cites it. | **BYTES** only if an audio segment cites VOICE; else ancestor. |

If a bound parent SHA changes after freeze: `FAILED_IDENTITY` / `BINDING_CHANGED`.  
If an unbound ancestor changes (e.g. VOICE under remux): MIX identity **does not** change. That is intentional under the principle above.

Official SH02 today would freeze only LIPSYNC `b0b4f0f2` + `725df139…`. VIDEO `b44e530b` + `21700e4f…` and VOICE `0fde86c2` + `bd348ffb…` stay LIPSYNC parents, not MIX parents, until P1 says otherwise.

---

### 11. REPLAY / IDEMPOTENCY

Current token:

```
ProductionMixFreeze.Token = lipsyncId|lipsyncSha
```

**Not sufficient** once P1 is anything other than “unnamed remux of this LIPSYNC,” and not sufficient to detect range or output-clock change even for remux-if-named.

Proposed conceptual identity (not implemented):

```
mixIdem =
  buildId | shotCode | MIX_VIDEO |
  ContractVersion | CompositionMode | OutputClock | OutputDurationSec |
  parentId+sha… |
  pictureSegment… | audioSegment…
```

| Change | Replay? |
|---|---|
| Same token, succeeded MIX | Replay existing artifact |
| Same LIPSYNC id, different SHA | Not replay → `FAILED_IDENTITY` / new idem |
| Same ids, different ranges | Not replay |
| Same ranges, different SHA | Not replay |
| CURRENT:LIPSYNC ≠ frozen LIPSYNC | `BINDING_CHANGED` |
| VIDEO SHA change | `BINDING_CHANGED` only if VIDEO is a MIX parent |
| VOICE SHA change | `BINDING_CHANGED` only if VOICE is a MIX parent |
| Output clock / duration policy change | New contract / new identity |
| `ContractVersion` change | New identity |
| Official CURRENT:MIX already exists | Keep existing `BINDING_CHANGED` (unless a later policy allows replace) |

`SnapshotHash` will move if `DurationSec` or the hashed freeze token moves. That is enough **if** the token contains the contract. Do not add a second hash table.

---

### 12. FAIL-CLOSED RULES

Gates the contract requires. Existing ones stay. New ones only if the approved mode needs them.

**Already exist (keep)**

| Gate | When |
|---|---|
| `MISSING_LIPSYNC` | No CURRENT:LIPSYNC / no claimed id |
| `FAILED_IDENTITY` | Wrong role, URL storage, SHA mismatch, remux/A+V fail |
| `FAILED_VERIFICATION` | Parent not VERIFIED |
| `BINDING_CHANGED` | Claimed ≠ CURRENT:LIPSYNC; official CURRENT:MIX exists; SH01 canon mismatch (SH01 only) |
| `CROSS_SHOT` / `CROSS_SCOPE` | Parent not this build/shot |
| `PROVIDER_MODEL_MISMATCH` / `PAID_NETWORK_FORBIDDEN` | Official adapter not `server/ffmpeg` |
| `INPUT_SNAPSHOT_MISMATCH` | Freeze parse fail / parent list missing required id |
| Forbidden fallbacks | latest URL, IDB, FinalStore, etc. |

**Proposed additions — only after the matching policy**

| Gate | Required by | Why |
|---|---|---|
| `MIX_CONTRACT_VERSION_MISMATCH` | Any V1 token | Old remux token must not execute as V1 compose |
| `DURATION_MISMATCH` | A / D | Named clocks disagree beyond approved tolerance |
| `MISSING_MIX_PARENT` | C, or A/D VIDEO witness | Required parent absent |
| `MISSING_TIMELINE` | C | Compose mode with empty segments |
| `INVALID_SEGMENT_RANGE` | C | end ≤ start, source range outside artifact duration |
| `TIMELINE_OVERLAP` | C | Dest intervals overlap |
| `TIMELINE_GAP` | C | Dest intervals do not cover `[0, OutputDurationSec]` |
| `UNSUPPORTED_COMPOSITION_MODE` | All V1 | Unknown mode or remux adapter asked to compose |
| `OUTPUT_CLOCK_UNRESOLVED` | All V1 | `OutputClock` missing or media missing for that clock |

Do **not** add a Fal `cut_off` gate on MIX. That is a LIPSYNC provider input.

Do **not** add a Timing V3 speech-window gate unless P4/P5 adopt OS-owned numbers.

`Timing` on `MixVideo` should either be defined (director/output clock string) or removed from required inputs in a later contract edit. Leaving it required-and-unused is the current gap; fixing it is implementation, not this checkpoint.

---

### 13. PROVIDER BOUNDARY

| Belongs to OS | Belongs to provider |
|---|---|
| MIX output clock and claimed `OutputDurationSec` | Fal `sync_mode` (`cut_off`, `silence`, …) |
| Which artifacts MIX reads | Fal output length |
| Segment ranges if compose is approved | Face box / pin (LIPSYNC, already SH02 canon) |
| Fail-closed when claimed vs measured MIX duration disagree **after** OS names both | ElevenLabs / Veo duration mapping |
| Artifact SHA / bindings | How Fal interprets `cut_off` vs video/audio mismatch |

`cut_off` is an **input instruction** on CREATE_LIPSYNC. The OS may **observe** that CK23B output is `2.500s`. The OS may **not** promote “`cut_off` ⇒ speech-length LIPSYNC” to a MIX invariant unless P7 explicitly does that.

If P1 = A or D, CK23B `2.5s` is simply a LIPSYNC that does not match the VIDEO clock. That is an OS duration compare, not a Fal rule.

If P1 = B or E, MIX length follows the LIPSYNC artifact that already exists. That is an OS choice to trust measured LIPSYNC duration, still not a Fal enum law.

---

### 14. SH01 / SH02 COMPATIBILITY

| Item | Generic | SH01-specific | SH02-specific |
|---|---|---|---|
| MIX action, role, remux adapter, CURRENT:LIPSYNC parent | Yes | | |
| `ProductionMixFreeze` v0 `id\|sha` | Yes (current) | | |
| SH01 mix canon id/SHA/`5.042s` | | **Yes** | Must not copy |
| SH01 lipsync VIDEO `5.042` ≈ MIX lock | | Observed lock, not generic “LIPSYNC = VIDEO” | |
| SH02 lipsync canon, box, `cut_off` | | | LIPSYNC only |
| Director `5.0` vs `10.0` | Pattern (explicit director) | `5.0` lock | `10.0` lock |
| Current SH02 8 vs 2.5 mismatch | The generic problem | SH01 remux does not hit it | This shot’s instance |

A later V1 contract should be **generic**. SH01 can keep remux + canon lock if P1 for SH01 remains remux of a video-length LIPSYNC. SH02 must not inherit `5.042` or SH01 artifact ids.

Legacy graph / IDB stay forbidden fallbacks.

---

### 15. MINIMUM IMPLEMENTATION DELTA

**DESIGN ONLY. Do not implement.**

Smallest later change set **after** P1–P8 approval:

1. `ProductionMixFreeze` — versioned token (`V1|mode|…`). Keep `TryParse` for v0 remux if backward compat is required.
2. `ProductionActionContractRegistry.MixVideo` — required inputs match the approved parents; SuccessCondition text; decide `Timing`.
3. `EvaluateCreateMixAsync` / `MixVideoAsync` — bind approved parents; apply approved gates; set `DurationSec` from `OutputClock`.
4. `FfmpegMixExecutionAdapter` — refuse `COMPOSE_SEGMENTS` until a compose transport exists; remux path unchanged for remux modes.
5. `ProcessFfmpegMixTransport` — **only if P1 = compose**: new command. Not a second file store.
6. Optional `ProductionEp01Sh02MixCanon` — **only if** official SH02 needs locked ids after policy. Do not create it as a way to sneak a rule.
7. Tests listed in §16.

**Do not:** new artifact table, new binding roles, new snapshot table, migrations for segments, Timing V3 import, assembly-freeze reuse, SH01 canon copy.

---

### 16. TEST PLAN FOR FUTURE CK24C

Design-only. Deterministic. No live Fal/FFmpeg required for contract tests.

1. 8s VIDEO + 2.5s LIPSYNC — evaluate under approved mode (expect fail-closed **or** remux 2.5 **or** compose; one approved expectation).
2. 8s VIDEO + 8s LIPSYNC — remux/match modes succeed; compose with remainder unused.
3. Matching durations — match gate passes.
4. Shorter LIPSYNC — match gate fails; remux mode uses LIPSYNC length **only if** B/E approved; compose requires remainder segments.
5. Longer LIPSYNC than VIDEO — fail or trim **only if** a later policy names trim (not proposed now) → default fail-closed.
6. Missing VOICE — remux still available if VOICE is not MIX parent; compose-from-VOICE fails `MISSING_MIX_PARENT`.
7. Changed VIDEO SHA — fail iff VIDEO is MIX parent.
8. Changed LIPSYNC SHA — always fail / new identity.
9. Changed source range — not replay (compose only).
10. Foreign shot artifact — `CROSS_SHOT` / `CROSS_SCOPE`.
11. Replay same contract — same MIX artifact, no second remux.
12. Existing CURRENT:MIX — `BINDING_CHANGED`.
13. Malformed timeline / unknown mode — `UNSUPPORTED_COMPOSITION_MODE` / parse fail.
14. Gap in dest coverage — `TIMELINE_GAP` (compose).
15. Overlapping dest — `TIMELINE_OVERLAP` (compose).
16. Contract version change — new identity; v0 token must not execute as v1 compose.

Also: official SH01 canon still locks its own lipsync; SH02 must not accept SH01 mix canon ids.

---

### 17. OPEN DECISIONS FOR HUMAN APPROVAL

1. **P1 — What is MIX?** `REMUX_LIPSYNC` / `FAIL_CLOSED_DURATION_MATCH` / `COMPOSE_SEGMENTS` (or named remux-length E).
2. **P2 — Output clock** for this shot and for the generic rule.
3. **P3 — May MIX drop VIDEO `2.5→8`?** If no, remainder source.
4. **P4 — LIPSYNC picture meaning** (full replacement vs bounded vs other).
5. **P5 — MIX audio source** and audio remainder.
6. **P6 — Duration match + measurement method + tolerance.** Do not approve ε until the method is named.
7. **P7 — May OS treat `cut_off` output length as expected?** Default from CK24A: **no**, unless explicitly overridden.
8. **P8 — Generic now vs SH02-only exception.**
9. **Binding required for VIDEO** if it is only an IDENTITY witness: `ACCEPTED:VIDEO_TAKE` vs `CURRENT:VIDEO_TAKE` (this shot has ACCEPTED, not CURRENT:VIDEO_TAKE).
10. **Whether official CURRENT:MIX remains one-shot** (`BINDING_CHANGED` if exists).

Do not silently decide these in CK24C.

---

### 18. RECOMMENDED NEXT CHECKPOINT

**CK24C — MIX CONTRACT IMPLEMENTATION**

**ONLY after CK24B receives explicit approval** of P1–P8 (and P9–P10 if they affect the first cut).

Until approval: no code, no freeze change, no MIX, no FFmpeg, no SH02 mix canon.

---

### 19. PRODUCTION SAFETY

| Action | This checkpoint |
|---|---|
| MIX executions | **0** |
| FFmpeg executions | **0** |
| Provider calls | **0** |
| Permits issued / consumed | **0** |
| Snapshots created | **0** |
| Artifacts created | **0** |
| Binding mutations | **0** |
| Selection mutations | **0** |
| Graph / IDB mutations | **0** |
| Source / migration changes | **0** |

Output = this design proposal only.

---

HARD STOP. No implementation. No MIX. No FFmpeg. No snapshot. No permit. No contract change in code.
