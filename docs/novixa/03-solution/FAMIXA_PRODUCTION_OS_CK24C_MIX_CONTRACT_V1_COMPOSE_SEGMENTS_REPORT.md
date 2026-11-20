# CK24C — MIX_CONTRACT_V1

Date: 2026-09-23  
Mode: **IMPLEMENT + TEST CONTRACT ONLY**. No provider. No FFmpeg process. No MIX artifact. No MIX binding. No live SH02 mutation.

Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shot: `EP01-SC01-SH02`

---

## 1. VERDICT

**PASS WITH GAPS**

MIX_CONTRACT_V1 COMPOSE_SEGMENTS is implemented, validated, frozen as a versioned token, and covered by T01–T24. Official non-SH01 MIX evaluates the contract and fail-closes before OpenFrozen / remux. SH01 remux canon is unchanged. COMPOSE_SEGMENTS FFmpeg transport is not implemented (CK24D).

---

## 2. POLICY IMPLEMENTED

1. MIX is `COMPOSE_SEGMENTS`.
2. `OutputClock = VIDEO_TAKE`.
3. `OutputDurationSec` = authoritative VIDEO_TAKE duration.
4. VIDEO_TAKE is the shot picture master / timeline clock.
5. LIPSYNC is a bounded picture replacement segment.
6. SH02: VIDEO 8.000s + LIPSYNC 2.500s ⇒ MIX output 8.000s.
7. Picture: `0.0 → 2.5` LIPSYNC; `2.5 → 8.0` VIDEO remainder (explicit).
8. Audio: `0.0 → LIPSYNC.DurationSec` embedded LIPSYNC audio; remainder = `SILENCE`.
9. MIX does not shrink to provider LIPSYNC duration.
10. VIDEO remainder is not discarded.
11. Provider does not decide the shot timeline.
12. Production OS decides timeline / composition.
13. No new artifact / binding / snapshot subsystem.
14. Reuses `ProductionTaskInputSnapshot` + `ProductionMixFreeze`.
15. Generic: `AppliesTo(official film && !SH01 MixCanon)`. No SH02 hard-code in Build.
16. SH01 remux / MixCanon behavior unchanged.

Audio end authority = existing OS `LIPSYNC.DurationSec`. No invented 2.534 vs 2.480 tolerance. If that field is `<= 0`, fail closed (`MISSING_TIMELINE`).

---

## 3. FILES CHANGED

**Added**

- `ProductionMixContract.cs` — `ProductionMixContract`, `ProductionMixContractV1`, `ProductionMixParent`, `ProductionMixSegment`
- `ProductionMixContractV1Regression.cs` — T01–T24 in-memory suite
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_CK24C_MIX_CONTRACT_V1_COMPOSE_SEGMENTS_REPORT.md`

**Modified**

- `ProductionWorkIds.cs` — compose failure reasons
- `ProductionMixFreeze.cs` — `TokenV1` / `TryParseV1` / `IsComposeV1` (v0 remux token unchanged)
- `ProductionExecutionService.cs` — `EvaluateCreateMixAsync` compose branch
- `ProductionCreateVideoService.Actions.cs` — official compose fail-closed before OpenFrozen
- `FfmpegMixExecutionAdapter.cs` — refuse `IsComposeV1` without remux
- `IFfmpegMixTransport.cs` — remux-only boundary comment
- `ProcessFfmpegMixTransport.cs` — remux-only boundary comment
- `ProductionEp01Sh01RealFilmRunV1MixRegression.cs` — official SH02 without VIDEO → `MISSING_VIDEO_ARTIFACT`
- `ProductionEp01Sh01RealFilmRunV1MixTests.cs` — MixContract V1 fact
- `VideoGenerationExecutionService.cs` — catalog `FAMIXA_PRODUCTION_OS_MIX_CONTRACT_V1`

**Reused, not replaced**

- `ProductionTaskInputSnapshot`, `OfficialVideoAuthorityBridgeV1`, `CreateVideoReadinessClaim`, `ProductionActionContractRegistry.MixVideo`, v0 `ProductionMixFreeze.Token`

---

## 4. CONTRACT SHAPE

```
MIX_CONTRACT_V1
  ContractVersion: "MIX_CONTRACT_V1"
  CompositionMode: "COMPOSE_SEGMENTS"
  OutputClock: "VIDEO_TAKE"
  OutputDurationSec: <VIDEO_TAKE.DurationSec>
  DurationToleranceSec: 0
  Parents[]:
    { Role, ArtifactId, Sha256, Binding, Consume=BYTES }
  PictureSegments[]:
    { DestStartSec, DestEndSec, SourceRole, SourceArtifactId, SourceSha256, SourceStartSec, SourceEndSec }
  AudioSegments[]:
    same shape; remainder SourceRole=SILENCE, ArtifactId=empty, Sha256=""
  TimingHash
  ContractHash = SHA256(canonical fields except ContractHash)
```

Parents are structured. No opaque `ParentArtifactIds`-only contract. VIDEO_TAKE + LIPSYNC consume `BYTES`. VOICE is rejected as a MIX parent.

---

## 5. SH02 CONTRACT PROOF

In-memory representation of the locked official parents (no MIX artifact written):

| Field | Value |
|---|---|
| Shot | EP01-SC01-SH02 |
| Build | 4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f |
| OutputClock | VIDEO_TAKE |
| OutputDurationSec | 8 |
| VIDEO parent | b44e530b-5b59-422f-9b80-e058a3a639b5 / SHA 21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34 / 8.0s / BYTES |
| LIPSYNC parent | b0b4f0f2-95b5-4839-b9bc-22c48028bb9e / SHA 725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e / 2.5s / BYTES |

Picture:

1. dest `0 → 2.5` ← LIPSYNC source `0 → 2.5`
2. dest `2.5 → 8` ← VIDEO_TAKE source `2.5 → 8`

Audio:

1. dest `0 → 2.5` ← LIPSYNC embedded audio `0 → 2.5`
2. dest `2.5 → 8` ← SILENCE

---

## 6. FREEZE

`ProductionMixFreeze.TokenV1` is a pipe token:

`V1|version|mode|clock|output|tol|timing|contractHash|P|n|parents…|PIC|n|segs…|AUD|n|segs…`

Immutable fields bound by the token + `ContractHash`:

- ContractVersion, CompositionMode, OutputClock, OutputDurationSec, DurationToleranceSec
- ordered parent Role / ArtifactId / SHA256 / Binding / Consume
- ordered picture + audio destination/source ranges
- TimingHash, ContractHash
- scope/build/shot stay on the existing request/task (`ProductionTaskInputSnapshot` / identity)

v0 `lipsyncId|lipsyncSha` still parses only remux tokens (`TryParse` rejects `V1` prefix). `TryParseV1` re-hashes and rejects silently-repaired tokens.

CK24C does not call `OpenFrozenAsync` on the compose path, so no live snapshot is written.

After a V1 token exists: no refreeze, no latest-artifact repair, no current-binding swap if SHA/id changed (`MatchesFrozen` → `HASH_CHANGED` / `BINDING_CHANGED`).

---

## 7. EVALUATION GATES

`EvaluateCreateMixAsync` (official non-SH01) + `ProductionMixContract.Validate` / `MatchesFrozen`:

| Gate | Code | Source |
|---|---|---|
| MISSING_VIDEO | `MISSING_VIDEO_ARTIFACT` | compose VIDEO resolve |
| MISSING_LIPSYNC | `MISSING_LIPSYNC_ARTIFACT` | existing |
| BINDING_CHANGED | `BINDING_CHANGED` | claim ≠ CURRENT; `MatchesFrozen` id |
| HASH_CHANGED | `HASH_CHANGED` | `MatchesFrozen` SHA |
| CROSS_SHOT | `CROSS_SHOT_FORBIDDEN` | existing parent identity |
| CROSS_SCOPE | `CROSS_SCOPE_FORBIDDEN` | existing parent identity |
| OUTPUT_CLOCK_UNRESOLVED | `OUTPUT_CLOCK_UNRESOLVED` | VIDEO duration ≤ 0 or clock ≠ VIDEO_TAKE |
| INVALID_RANGE | `INVALID_RANGE` | negative / start≥end / LIPSYNC > VIDEO / source beyond parent |
| PICTURE_GAP / PICTURE_OVERLAP | same | picture lane |
| AUDIO_GAP / AUDIO_OVERLAP | same | audio lane |
| DURATION_MISMATCH | `DURATION_MISMATCH` | last dest ≠ OutputDuration or Output ≠ VIDEO duration |
| UNSUPPORTED_COMPOSITION_MODE | `UNSUPPORTED_COMPOSITION_MODE` | mode ≠ COMPOSE_SEGMENTS; MixVideo compose halt; adapter V1 refuse |
| CONTRACT_VERSION_MISMATCH | `CONTRACT_VERSION_MISMATCH` | version ≠ MIX_CONTRACT_V1 |
| MISSING_TIMELINE | `MISSING_TIMELINE` | empty lanes or LIPSYNC duration ≤ 0 |
| FAILED_VERIFICATION | `FAILED_IDENTITY` / existing verify | parent identity |
| PAID_NETWORK_FORBIDDEN | existing | remux official path only (compose never reaches it) |
| PROVIDER_MODEL_MISMATCH | existing | remux official path + adapter |

LIPSYNC 2.5s ≠ VIDEO 8s is **not** an error.

---

## 8. REPLAY / IDEMPOTENCY

Canonical identity:

`buildId|shotCode|MIX_VIDEO|ContractVersion|CompositionMode|OutputClock|OutputDurationSec|ContractHash|TimingHash`

`ContractHash` covers ordered parent IDs + SHA256 + picture/audio segments.

Same canonical contract ⇒ same hash / same V1 token (T14). Change of parent SHA, artifact id, segment range, output duration, composition mode, or contract version ⇒ new identity / no replay (T10, T11, T13, T15).

No provider call is made to prove replay.

---

## 9. TEST RESULTS

| ID | Result |
|---|---|
| T01 VIDEO 8 + LIPSYNC 2.5 ⇒ output 8 | PASS |
| T02 picture 0→2.5 LIPSYNC / 2.5→8 VIDEO | PASS |
| T03 no picture gap | PASS |
| T04 no picture overlap | PASS |
| T05 output starts at 0 | PASS |
| T06 output ends at 8 | PASS |
| T07 shorter LIPSYNC does not fail | PASS |
| T08 missing VIDEO ⇒ fail | PASS |
| T09 missing LIPSYNC ⇒ fail | PASS |
| T10 VIDEO SHA changed after freeze ⇒ fail | PASS |
| T11 LIPSYNC SHA changed after freeze ⇒ fail | PASS |
| T12 historical LIPSYNC ⇒ BINDING_CHANGED | PASS |
| T13 different segment range ⇒ new identity | PASS |
| T14 same contract ⇒ same identity | PASS |
| T15 different contract version ⇒ no replay | PASS |
| T16 OutputClock ≠ VIDEO_TAKE rejected | PASS |
| T17 unsupported composition mode | PASS |
| T18 picture gap ⇒ fail | PASS |
| T19 picture overlap ⇒ fail | PASS |
| T20 audio remainder SILENCE | PASS |
| T21 MIX does not parent VOICE | PASS |
| T22 SH01 existing mix suite | PASS |
| T23 no real provider / FFmpeg invocation | PASS |
| T24 no live SH02 binding/artifact mutation | PASS |

Also PASS: Mix input cutover remux suite; official SH02 CREATE_LIPSYNC; official SH02 CREATE_VOICE; SH01 CREATE_LIPSYNC; CK20 VIDEO acceptance.

---

## 10. SAFETY

Confirmed on CK24C compose paths and T01–T24 harness (in-memory only):

| Check | Count |
|---|---|
| provider calls (Veo / Fal / ElevenLabs / Runway Start) | 0 |
| FFmpeg executions (`RemuxOnceAsync` / `Process.Start`) | 0 |
| permits consumed | 0 |
| MIX artifacts created | 0 |
| MIX bindings created | 0 |
| live graph / IDB mutations | 0 |

Official compose `MixVideoAsync` throws `UNSUPPORTED_COMPOSITION_MODE` after evaluate, before `OpenFrozenAsync`. Adapter throws the same if a V1 token is presented. `ProcessFfmpegMixTransport` remains remux-only and is not invoked.

---

## 11. GAPS

1. COMPOSE_SEGMENTS FFmpeg transport is not implemented. `RemuxOnceAsync` is still one-input `-c copy`.
2. MixVideo compose never reaches `OpenFrozenAsync`, so `TokenV1` is not yet persisted on a live `ProductionTaskInputSnapshot`.
3. `ProductionActionContractRegistry.MixVideo` still lists LipSync-only required inputs (existing contract reused; evaluate is stricter).
4. Audio end is OS `LIPSYNC.DurationSec` only. Media-probed 2.480 vs DB 2.534 is not resolved (fail-closed: no invented tolerance).

---

## 12. NEXT CHECKPOINT

**CK24D Real MIX Execution V1 is ready to be authorized. Do not execute it now.**

When authorized later, CK24D must:

- persist the frozen MIX_CONTRACT_V1 on the existing snapshot/freeze
- execute that frozen COMPOSE_SEGMENTS contract
- produce an 8.000s MIX artifact for SH02
- not remux LIPSYNC as the whole shot
- not call Veo / Fal / ElevenLabs

HARD STOP after CK24C.
