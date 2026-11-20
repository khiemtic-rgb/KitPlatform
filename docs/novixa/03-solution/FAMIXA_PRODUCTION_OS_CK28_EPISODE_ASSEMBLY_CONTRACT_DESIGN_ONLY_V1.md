# CK28 — EPISODE ASSEMBLY CONTRACT DESIGN V1

Date: 2026-09-24  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Episode: `EP01`  
Mode: **DESIGN ONLY**. No implementation. No execution. No mutation.

---

## 1. VERDICT

**READY FOR CK29 IMPLEMENTATION**

Episode assembly can be represented by the existing Production OS planes:

- artifact role `ASSEMBLY`
- bindings `CURRENT:` / `VISIBLE:`
- `ProductionTaskInputSnapshot`
- `FilmAssembly` / `FilmAssemblyShot` membership
- unpaid server FFmpeg transport

A new artifact/binding/snapshot subsystem is not required. `FINAL` is not activated. Shot-level `ASSEMBLE` stays 1-MIX remux. The two existing EP01 FilmAssembly records stay historical.

Primary design: **`EPISODE_ASSEMBLY_CONTRACT_V1`**.

---

## 2. EXISTING OUTPUT MODEL

| Plane | Existing | Episode use |
|---|---|---|
| `ProductionArtifactRoles` | KEYFRAME, VIDEO_TAKE, VOICE, LIPSYNC, MIX, **ASSEMBLY** | Episode output = `ASSEMBLY` |
| `ProductionOutputTypes` | IMAGE, VIDEO_TAKE, VOICE, LIPSYNC, MIX, BINDING | No ASSEMBLY/FINAL type. ActionContract.Assemble already uses expected output `"ASSEMBLY"` as a string. Reuse that. |
| `ProductionBindingRoles` | CURRENT, ACCEPTED, VISIBLE, EDITORIAL_SOURCE, **FINAL** | FINAL is unused. Do not activate. |
| Typed bindings | `CURRENT:ASSEMBLY`, `VISIBLE:ASSEMBLY` already produced by shot ASSEMBLE | Episode binds the same typed roles on an **episode ShotCode**, not on SH01/SH02 |
| `ACCEPTED:ASSEMBLY` | does not exist | Not required. Shot ASSEMBLY is already the post-ACCEPT remux product |
| `ProductionActions` | ASSEMBLE only (no EPISODE / FINAL action) | New action `ASSEMBLE_EPISODE`. Do not overload shot ASSEMBLE |
| `ProductionActionContract.Assemble` | Target=`Film`, output role ASSEMBLY, **“No FINAL claim.”** | Confirms FINAL artifact is not intended |
| `ProductionFullFilmVerticalRunV1` | “No FINAL artifact role. FilmAssembly is the canonical final representation.” | FilmAssembly remains membership, not the media bytes |
| Bindings key | `(BuildId, ShotCode, Role)` | Episode bindings must use ShotCode = `EP01`, never `EP01-SC01-SH01` / `SH02` |

Answers:

- **A.** Yes. Episode media is an `ASSEMBLY` artifact.  
- **B.** Yes, if bound on episode ShotCode `EP01`. Binding on SH01/SH02 would overwrite shot remux.  
- **C.** No new artifact role.  
- **D.** `FINAL` is a latent **binding** name, not an artifact role. Assemble contract forbids a FINAL claim. Do not activate.  
- **E.** Minimum delta: new action + episode contract/evaluate/freeze + concat transport + new FilmAssembly membership pointing at shot **ASSEMBLY** ids + episode-scoped `CURRENT:ASSEMBLY` / `VISIBLE:ASSEMBLY`.

---

## 3. CANONICAL EPISODE INPUT

**Authority: `CURRENT:ASSEMBLY` per shot, plus explicit caller ArtifactId that must equal that binding.**

Each ordered input freezes:

| Field | Source |
|---|---|
| BuildId | official EP01 build |
| ShotCode | official shot code |
| Order | 0..N-1 contiguous |
| ArtifactId | caller id **and** `CURRENT:ASSEMBLY` |
| Role | `ASSEMBLY` only |
| SHA256 | live artifact `ByteSha256` |
| StorageKey | live artifact `StorageKey` |
| DurationSec | live artifact `DurationSec` (> 0) |

Evaluate must also require:

- `VerificationStatus = VERIFIED`
- artifact task scope BuildId + ShotCode match the row
- `VISIBLE:ASSEMBLY` if present equals `CURRENT:ASSEMBLY` (official shot ASSEMBLE binds both to the same id)
- physical bytes SHA equals frozen SHA

**Rejected input authorities**

| Candidate | Why rejected |
|---|---|
| `VISIBLE:ASSEMBLY` alone | Preview pointer. OfficialVideoAuthorityBridge ignores ASSEMBLY. |
| `ACCEPTED:ASSEMBLY` | Does not exist. Adding ACCEPT after shot ASSEMBLE is an extra product step. Shot ASSEMBLY is already the output of ACCEPT MIX → ASSEMBLE. |
| `FilmAssemblyShot.ArtifactId` | Live rows store **MIX**, not ASSEMBLY. Would concat the wrong bytes. |
| MIX / VIDEO / LIPSYNC | Wrong role. `FAILED_IDENTITY` / `ARTIFACT_ROLE_MISMATCH`. |
| Legacy graph / IDB / take URL | Forbidden fallback. |
| Caller id without binding check | Could pass a foreign ASSEMBLY. |

Official EP01 first concat inputs (witness, resolved again at CK29 evaluate):

1. `EP01-SC01-SH01` → `CURRENT:ASSEMBLY` `28f8de3e-f6d3-4972-934e-703a2be45939` · 5.042s  
2. `EP01-SC01-SH02` → `CURRENT:ASSEMBLY` `92621f9e-57eb-4185-8cea-a962ec399136` · SHA `37127ee5…` · 8.000s  

---

## 4. EPISODE ORDER AUTHORITY

| Source | Kind | Episode authority? |
|---|---|---|
| Caller `OrderedInputs[].Order` frozen in contract | Production OS | **Yes — only authority** |
| `FilmAssemblyShot.Order` on the **new** episode record | OS membership copy of the freeze | After insert only |
| Live SH01/SH02 FilmAssembly `sort_order = 0` | Shot remux byproduct | No |
| `series_build.graph_json` SH01…SH06 | Legacy data | **No** |
| Director TEST hard-code SH01, SH02 | UI-only | No |
| DB insertion time of the two FilmAssemblies | Accident | No |

Official EP01 v1 required set = **exactly the frozen list**. First official list is SH01 then SH02.

SH03–SH06 exist in the legacy graph and are **not** required. Auto-expanding to 6 shots would be a legacy-authority and missing-ASSEMBLY failure.

Order rules:

- unique ShotCode  
- Order = 0..N-1 with no gaps and no collisions  
- N >= 2  
- official film does not read `graph_json`

---

## 5. EPISODE TIMELINE CONTRACT

**Abutting concat is policy.**

```
Start[0] = 0
End[i]   = Start[i] + DurationSec[i]
Start[i] = End[i-1]     (i > 0)
OutputDurationSec = sum(DurationSec)
DurationToleranceSec = 0
```

Duration source = frozen **ASSEMBLY artifact `DurationSec`**.  
Not director timing. Not legacy graph `seconds=5`. Not MIX duration. Not FilmAssemblyShot on the historical MIX rows.

| Rule | Behavior |
|---|---|
| DurationSec <= 0 | FAIL `INVALID_RANGE` |
| Start[0] != 0 | FAIL `INVALID_RANGE` |
| Start[i] < End[i-1] | FAIL `PICTURE_OVERLAP` / `AUDIO_OVERLAP` |
| Start[i] > End[i-1] | FAIL `PICTURE_GAP` / `AUDIO_GAP` |
| Probe output != OutputDurationSec | FAIL `DURATION_MISMATCH` |

Probe after concat **verifies**. It does not invent duration. No new tolerance. Reuse `ProductionMixContract.Dec`.

Official EP01 v1 derived clock if live durations still hold:

- SH01 0.000 → 5.042  
- SH02 5.042 → 13.042  
- Output 13.042  

Those numbers are derived, then frozen. They are not imported from CK27 as constants unless evaluate still sees them.

---

## 6. PICTURE POLICY

Each shot `ASSEMBLY` is the entire shot picture.

- Transition = `CUT`  
- No overlap  
- No implicit trim  
- No implicit pad / loop  
- No intra-shot remainder  
- No NLE  

Composition mode: **`CONCAT_COPY`**.

If inputs are not stream-copy compatible (codec / timebase / size / pix_fmt / audio layout), **FAIL** `UNSUPPORTED_COMPOSITION_MODE`. Do not silent-re-encode. Do not copy legacy `libx264` fallback.

---

## 7. AUDIO POLICY

Shot `ASSEMBLY` already contains authoritative picture + audio (CK26 remux of MIX).

Episode assembly concatenates **complete ASSEMBLY files**.

- No independently sourced episode audio  
- No silence insertion (a gap is a fail, not a pad)  
- No VOICE / LIPSYNC / MIX rebuild at episode level  
- Audio/video duration is the one ASSEMBLY `DurationSec`; probe mismatch is `DURATION_MISMATCH`

---

## 8. EPISODE FREEZE CONTRACT

Reuse **one** `ProductionTaskInputSnapshot` row. Do not add a snapshot table.

Do **not** extend `ProductionAssemblyFreeze`. That token is the shot remux freeze; official adapter requires `shots.Count == 1`. Reusing it for N ASSEMBLY parents would collide with CK26.

Follow `MIX_CONTRACT_V1`: a versioned contract object, hash, stored in `ShotContract`. Parents in `ParentArtifactIds`. `DurationSec` = episode output.

```
EPISODE_ASSEMBLY_CONTRACT_V1
  ContractVersion        = EPISODE_ASSEMBLY_CONTRACT_V1
  CompositionMode        = CONCAT_COPY
  OutputClock            = ASSEMBLY
  BuildId
  EpisodeCode
  OrderedInputs[]        = { Order, ShotCode, Role=ASSEMBLY, ArtifactId, SHA256, StorageKey, DurationSec, StartSec, EndSec }
  OutputDurationSec
  DurationToleranceSec   = 0
  TimelineHash
  ContractHash
```

Snapshot mapping:

- `ShotContract` = canonical contract text  
- `ParentArtifactIds` = ordered ASSEMBLY ids  
- `DurationSec` = OutputDurationSec  
- `ProviderId` / `ModelId` = existing assembly catalog (`server` / ffmpeg)  
- `SnapshotHash` remains the existing snapshot hash (includes DurationSec + ShotContract + parents)

---

## 9. REPLAY / IDEMPOTENCY

```
Idem = ASSEMBLE_EPISODE
     + ContractVersion
     + BuildId
     + EpisodeCode
     + ContractHash
```

`ContractHash` includes ordered `(ShotCode, ArtifactId, SHA256, DurationSec, StartSec, EndSec)` and `OutputDurationSec`.

| Case | Result |
|---|---|
| same frozen inputs | replay same episode ASSEMBLY + same episode FilmAssembly |
| same artifact ids, SHA changed | `HASH_CHANGED` — not replay |
| same shots reordered | new ContractHash — not replay |
| one shot replaced | new ContractHash — not replay |
| duration changed | new ContractHash — not replay |
| CURRENT:ASSEMBLY drifts after freeze | `BINDING_CHANGED` / `HASH_CHANGED` |

---

## 10. FAIL-CLOSED MATRIX

| Case | Result | Reason |
|---|---|---|
| missing required shot / missing CURRENT:ASSEMBLY | FAIL | `MISSING_DEPENDENCY` |
| duplicate ShotCode | FAIL | `FAILED_IDENTITY` |
| missing Order / non-contiguous / collision | FAIL | `INVALID_RANGE` |
| N < 2 | FAIL | `MISSING_DEPENDENCY` |
| wrong BuildId | FAIL | `CROSS_SCOPE_FORBIDDEN` |
| wrong EpisodeCode | FAIL | `CROSS_SCOPE_FORBIDDEN` |
| artifact ShotCode ≠ row ShotCode | FAIL | `CROSS_SHOT_FORBIDDEN` |
| role ≠ ASSEMBLY | FAIL | `ARTIFACT_ROLE_MISMATCH` |
| MIX supplied | FAIL | `ARTIFACT_ROLE_MISMATCH` |
| VIDEO / LIPSYNC / VOICE supplied | FAIL | `ARTIFACT_ROLE_MISMATCH` |
| FilmAssemblyShot MIX used as input | FAIL | `ARTIFACT_ROLE_MISMATCH` |
| not VERIFIED | FAIL | `FAILED_VERIFICATION` |
| SHA ≠ live / ≠ file | FAIL | `HASH_CHANGED` |
| StorageKey changed | FAIL | `FAILED_IDENTITY` |
| DurationSec <= 0 | FAIL | `INVALID_RANGE` |
| timeline gap | FAIL | `PICTURE_GAP` / `AUDIO_GAP` |
| timeline overlap | FAIL | `PICTURE_OVERLAP` / `AUDIO_OVERLAP` |
| contract version mismatch | FAIL | `CONTRACT_VERSION_MISMATCH` |
| mode ≠ CONCAT_COPY | FAIL | `UNSUPPORTED_COMPOSITION_MODE` |
| concat-copy incompatible | FAIL | `UNSUPPORTED_COMPOSITION_MODE` |
| probe duration ≠ output | FAIL | `DURATION_MISMATCH` |
| legacy graph used as order/duration | FAIL | `HIDDEN_FALLBACK_FORBIDDEN` |
| `/content/series/assemble` | FAIL | forbidden fallback |
| `RemuxOnceAsync` | FAIL | wrong transport |
| changed input after freeze | FAIL | `INPUT_SNAPSHOT_MISMATCH` |
| same inputs replay | PASS | existing succeeded replay |
| official Director POST | FAIL | existing `FILM_PROTECTED` (CK29 is in-process) |

Legacy graph **mismatch** is not a gate. The graph is not read.

---

## 11. FILM ASSEMBLY MODEL

Live historical records — **do not mutate, do not merge**:

| Record | Member | Meaning |
|---|---|---|
| `d56a3c63-fbe7-41d6-8355-0af246c092b7` | MIX `2f88e0a0-…` 5.042s order 0 | SH01 shot remux membership |
| `1c210b73-333e-46ce-9d02-8beab8ef2f5d` | MIX `58297402-…` 8.000s order 0 | SH02 shot remux membership |

Neither can represent EP01 episode:

- each has one member  
- members are MIX  
- `FindAssemblyByMembershipAsync` is exact `SequenceEqual`  
- extending SH01 would rewrite history  

**CK29 creates a third FilmAssembly:**

- Title = `EP01`  
- Members = ordered **ASSEMBLY** ids (SH01 ASSEMBLY, SH02 ASSEMBLY)  
- `sort_order` 0, 1  
- Transition `CUT`, AudioState `BOUND`  
- Lookup = exact ASSEMBLY id list  

Existing rows remain historical shot membership. Episode membership does not reuse them.

---

## 12. OUTPUT / BINDING MODEL

| Item | Value |
|---|---|
| Artifact role | `ASSEMBLY` |
| Parents | ordered shot ASSEMBLY ids |
| Bindings | `CURRENT:ASSEMBLY` + `VISIBLE:ASSEMBLY` |
| Binding ShotCode | `EP01` (equals EpisodeCode) |
| EpisodeCode | `EP01` |
| SceneCode | `EP01` |
| `ACCEPTED:ASSEMBLY` | not bound |
| `FINAL` | not bound, not created |
| FilmAssembly | new episode membership of input ASSEMBLY ids |

Scope `(4ef164d6-…, EP01, EP01, EP01)` is the episode slot. It does not collide with `EP01-SC01-SH01` / `SH02`.

---

## 13. CONCAT TRANSPORT CONTRACT

New transport method: **`ConcatCopyOnceAsync`**.

Not `RemuxOnceAsync`. Not `ComposeSegmentsOnceAsync`. Not `ContentSeriesAssembleService`.

```
ffmpeg -y -hide_banner -loglevel error
  -f concat -safe 0 -i list.txt
  -c copy -movflags +faststart
  output.mp4
```

- list.txt order = frozen Order  
- each entry = frozen StorageKey bytes already SHA-checked  
- unpaid, local, no provider, no network  
- output SHA / size / duration materialized through existing `VerifyArtifactAsync`  
- output DurationSec must equal frozen OutputDurationSec  
- copy failure or incompatible inputs → `UNSUPPORTED_COMPOSITION_MODE`  
- no retry, no silent re-encode  

Provider/model: existing `ProductionAssemblyCatalog` server/ffmpeg.

---

## 14. DIRECTOR ACTION CONTRACT

```
Action              = ASSEMBLE_EPISODE
Target              = Episode
RequiredInputs      = OrderedAssemblyInputs
Capability          = Assemble
ExpectedOutput      = ASSEMBLY
OutputArtifactRole  = ASSEMBLY
UiCta               = Lắp tập phim
Success             = Verified ASSEMBLY artifact; parents are exact CURRENT:ASSEMBLY ids; FilmAssembly membership matches freeze.
ForbiddenFallbacks  = latest take, acceptedTake.url, previewUrl, takeUrl, lipsyncUrl,
                      /content/series/assemble, graph_json seconds, FilmAssemblyShot MIX,
                      RemuxOnceAsync, FINAL claim
```

Request (minimum):

- BuildId, EpisodeCode  
- ShotCode = EpisodeCode (`EP01`)  
- OrderedInputs: ShotCode + ArtifactId  
- Story present (existing official OpenFrozen gate)  
- IdempotencyKey derived from ContractHash  

Director official POST stays blocked (`mayMutateProductionOs` = false). CK29 executes in-process, same class as CK26. UI CTA is specified, not shipped.

Do not send `acceptedArtifactId`. GetShotState prefers `ACCEPTED:VIDEO_TAKE`.

---

## 15. MINIMUM CK29 IMPLEMENTATION DELTA

1. **Contract** — `EPISODE_ASSEMBLY_CONTRACT_V1` build/validate/hash (pattern of `ProductionMixContract`).  
2. **Action** — `ProductionActions.AssembleEpisode`, ActionContract, known-action list.  
3. **Evaluate** — resolve CURRENT:ASSEMBLY, role/SHA/scope/order/timeline gates.  
4. **Freeze** — write contract into existing `ProductionTaskInputSnapshot`.  
5. **Membership** — new FilmAssembly whose shots are ASSEMBLY ids; do not touch `d56a3c63-…` / `1c210b73-…`.  
6. **Transport** — `ConcatCopyOnceAsync` only.  
7. **Output** — `ASSEMBLY` artifact + episode `CURRENT:ASSEMBLY` / `VISIBLE:ASSEMBLY`.  
8. **Replay** — idempotency = action + contract hash.  
9. **Tests** — isolated A–P evaluate + official SH01/SH02 regression locks.  
10. **Director** — contract/CTA map only; no official UI POST; no `mayMutateProductionOs` lift.

No migration. No new table. No FINAL role. No generic NLE.

---

## 16. TEST MATRIX

Evaluate (no FFmpeg on fail paths):

- A missing SH01 CURRENT:ASSEMBLY  
- B missing SH02 CURRENT:ASSEMBLY  
- C MIX / VIDEO / LIPSYNC supplied  
- D FilmAssemblyShot MIX supplied  
- E cross-shot ASSEMBLY  
- F cross-build ASSEMBLY  
- G SHA drift  
- H unverified artifact  
- I duplicate shot  
- J order collision / non-contiguous  
- K duration <= 0  
- L forced gap  
- M forced overlap  
- N contract version mismatch  
- O replay same hash  
- P CURRENT:ASSEMBLY changed after freeze  

Execution (official SH01+SH02 only, in-process):

- concat-copy once  
- output role ASSEMBLY  
- duration = 5.042 + 8.000  
- parents = the two shot ASSEMBLY ids  
- new FilmAssembly members = those ASSEMBLY ids  
- SH01 FilmAssembly `d56a3c63-…` unchanged  
- SH02 FilmAssembly `1c210b73-…` unchanged  
- SH01/SH02 MIX + ACCEPTED:MIX + shot CURRENT:ASSEMBLY unchanged  
- remux-once count does not increase  
- compose count does not increase  
- permits ASSEMBLE / ASSEMBLE_EPISODE unpaid  
- replay does not concat again  

---

## 17. REGRESSION GUARANTEES

- Official SH01 production chain unchanged  
- Official SH02 CK26 ASSEMBLY `92621f9e-…` unchanged  
- Shot `ASSEMBLE` remains count==1 MIX remux  
- No ACCEPTED:VIDEO_TAKE / VOICE / LIPSYNC / MIX rewrite  
- No merge/repair of the two EP01 FilmAssembly headers  
- No `FINAL` artifact role  
- No legacy `/content/series/assemble`  
- No graph `seconds=5` as clock  
- No provider / paid network  

---

## 18. CK29 READINESS

**READY FOR CK29 IMPLEMENTATION**

---

## 19. HARD STOP

No code. No migration. No DB write. No FFmpeg. No provider. No legacy assemble.
