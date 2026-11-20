# FAMIXA SHARED PERFORMANCE CONTRACT V1
ARCHITECTURE SPECIFICATION

Date: 2026-09-18  
Mode: READ TRACE → DESIGN → VERIFY INTERNAL CONSISTENCY  
Status: SPEC ONLY — not implemented  

Code changes: none  
Migrations: none  
Database: none  
UI: none  
Provider calls: none  
Media generation: none  
EP01 / EP99: untouched  
Canon / Visual Authority / Timing V3 / Production OS runtime: untouched  

Do not read this as implemented.  
Do not read this as FILM READY.  
Do not read this as REAL PROVIDER READY.

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

Predecessor evidence: `docs/novixa/03-solution/FAMIXA_SHARED_ACTING_PERFORMANCE_CONTRACT_V1_FORENSIC_REPORT.md`. FACT

---

## 1. EXECUTIVE VERDICT

**The Shared Performance Contract is the next WHAT boundary.** RECOMMENDED

Production OS + UI Cutover are closed. The remaining gap is not a provider. It is that CREATE_VIDEO and CREATE_VOICE cannot prove they were generated from the same performance intent. FACT

Current runtime already performs at **three execution grains at once**:

| Grain | Runtime authority today | Consumer |
|---|---|---|
| Shot | `ShotActingBeat` on `episode.shots[]` | I2V / motion / OS `acting_beat` flatten |
| Character-in-shot | `shot.characterIds` + lipsync `speakerCharacterId` | still persons, Fal speaker HOW |
| Dialogue line | `LinePerformance` on `dialogue[]` / `lines[]` | TTS, still face, assemble pause |

FACT

Those grains are **not** one contract. They are parallel writable sources. Shot `during.emotion` and line `performance.emotion` can diverge with no shared snapshot id. FACT

**Target (this spec, not code):**

```
SHOT PERFORMANCE CONTRACT
  ├── shot-level visual performance
  ├── character-in-shot[]
  └── ordered dialogue lines[]
        ↓ freeze
PERFORMANCE SNAPSHOT  (PerformanceSnapshotId + PerformanceHash)
        ↓ referenced by
CREATE_VIDEO  +  CREATE_VOICE  +  CREATE_LIPSYNC (stale-proof only)
```

RECOMMENDED

CREATE_IMAGE may **read** a visual-performance **projection**. It must not become the Performance authority. RECOMMENDED

The contract is WHAT. Provider / model / compiler / adapter remain HOW. FACT (current types already omit vendor fields; compilers add them)

No `PerformanceIntentId` or `ActingSnapshotId` exists today. Repo grep: 0. FACT  
This spec **introduces** `PerformanceSnapshotId` and `PerformanceHash`. They do not exist until an implementation phase. RECOMMENDED

---

## 2. CURRENT ARCHITECTURE

### 2.1 What exists

```
Story / beatText / visual
        ├─ deriveActingBeat()          → ShotActingBeat          WHAT (draft or saved)
        ├─ Director “Diễn xuất”        → persistActingBeat       WHAT (shot)
        ├─ inferActingDirection()      → LinePerformance draft   DERIVED
        ├─ lockLinePerformance()       → LinePerformance locked  WHAT (line)
        └─ deriveVisualSpec()          → VisualSpec.performance  DERIVED
                 ↓
        compileI2vPrompt / actingTtsVoiceSettings / compileVisualContract
                 ↓
        HOW (Runway / ElevenLabs / still prompt)
                 ↓
        Production OS InputSnapshot
          VIDEO: ActingBeat TEXT = before.action + before.body
          VOICE: VoiceHash = lineId|speaker|voiceId|Story
          LIPSYNC: parent artifact IDs only
```

FACT. Functions named in the forensic report §3–§6.

### 2.2 Why a rename is not the design

Do **not** collapse the contract into:

| Rejected collapse | Why it fails current runtime |
|---|---|
| Film-level performance | `FamixaCharacterState.emotion` is story memory, not take execution. `ACTING_LAW_LOCK` is policy text. FACT |
| Scene-level performance | `FamixaSceneNode.performance` / `performances[charId]` are notes. Execution does not freeze them. FACT |
| Line-only performance | Motion/I2V and OS CREATE_VIDEO read **shot** `actingBeat`. Line-only would drop action/body/gaze/room. FACT |
| `ShotActingBeat` rename | Beat has no line identity, no `characterId` ownership of speech, no text identity. CREATE_VOICE would still be unable to prove shared intent. FACT |

**Chosen granularity matches runtime:** shot visual + character participation + ordered lines. RECOMMENDED because those three are exactly what VIDEO, LIPSYNC speaker, and VOICE already consume — they just do not share a snapshot. FACT + RECOMMENDED

### 2.3 Current identity (must not become the new authority)

| Token | Algorithm | Payload | Role after this spec |
|---|---|---|---|
| `ActingHash` | SHA256 of acting **string** | flatten `action + body` | **COMPATIBILITY / DERIVED** |
| `actingBeatFingerprint` | FNV `productionHash` | full `ShotActingBeat` JSON | **COMPATIBILITY / DERIVED** |
| `VoiceHash` | SHA256 | `lineId\|speaker\|voiceId\|Story` | **execution input hash**, not Performance identity |
| `voiceInputFingerprint` | FNV | shotId + line + first-line performance | **COMPATIBILITY / DERIVED** |
| `TimingHash` | SHA256 (server) / FNV (client) | duration strings | **Timing V3**, not Performance |
| `SnapshotHash` | SHA256 join of task hashes + provider + model | per **task** | stays **task** identity |
| `PerformanceIntentId` | — | — | **ABSENT** |
| `ActingSnapshotId` | — | — | **ABSENT** |

FACT

Do **not** copy any of those values into `PerformanceSnapshotId`. RECOMMENDED

---

## 3. TARGET ARCHITECTURE

### 3.1 Layers (must stay separate)

| Layer | Owns | Must not own |
|---|---|---|
| Story | spoken text identity, beat text, who speaks | delivery, provider |
| **Performance Contract** | how the shot is performed | identity look, duration bins, provider |
| Canon / Character Identity | who the person is | acting of this take |
| Visual Authority / Visual Contract | how the frame looks | emotion as SoT |
| Timing V3 | how long | how they act |
| Production OS | freeze / task / attempt / artifact / binding | inventing acting |
| Compiler / Adapter | HOW strings | becoming WHAT |

RECOMMENDED

### 3.2 Desired conceptual stack

```
Canon / Character Identity
        ↓
Visual Authority
        ↓
Performance Draft          ← Director edits WHAT
        ↓ freeze
Performance Snapshot       ← immutable WHAT
        ↓ reference
Production Task Input Snapshot
        ↓
Provider / Model Freeze    ← HOW
        ↓
Attempt → Artifact → Binding
```

RECOMMENDED

CREATE_IMAGE sits on Visual Authority + a **derived** visual-performance projection. It is not the Performance SoT. RECOMMENDED

### 3.3 Draft vs Snapshot

| Object | Mutable? | Identity | Stored |
|---|---|---|---|
| `PerformanceDraft` | Yes | none (graph-local) | `graph_json` / current shot+line fields during Phase A |
| `PerformanceSnapshot` | **No** | `PerformanceSnapshotId` + `PerformanceHash` | durable row referenced by OS (Phase B) |

RECOMMENDED

Two tasks that store the **same** `PerformanceSnapshotId` are using the same freeze instance. FACT (by definition of this spec)  
Two tasks that store the **same** `PerformanceHash` are using the same WHAT, even if freeze rows differ. RECOMMENDED  
Two tasks with different `PerformanceHash` are **never** the same intent. RECOMMENDED

---

## 4. PERFORMANCE CONTRACT SCHEMA

Document id: `FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1`. RECOMMENDED  
Schema version integer: `1`. RECOMMENDED

This is a specification type, not an implemented type. RECOMMENDED

### 4.1 Root

```
PerformanceDraft | PerformanceSnapshot
  documentId:        "FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1"
  schemaVersion:     1
  buildId:           uuid          // ProductionScope.BuildId
  shotCode:          string        // ProductionScope.ShotCode / shot.id
  episodeCode:       string
  sceneCode:         string
  shotPerformance:   ShotPerformanceSlice
  characters:        CharacterInShot[]     // order = shot.characterIds order
  lines:             PerformanceLine[]     // order = dialogueSegmentIds order
  timingRef:         PerformanceTimingRef  // reference only
  visualRef:         PerformanceVisualRef  // reference only
  storyRef:          PerformanceStoryRef   // text identity pointers, not delivery
```

RECOMMENDED

Snapshot-only header (absent on draft):

```
PerformanceSnapshot
  + performanceSnapshotId: uuid
  + performanceHash:       "PERFORMANCE_HASH_V1:" + hex
  + frozenAt:              timestamptz
  + createdBy:             string
  + source:                PerformanceSourceRef
```

RECOMMENDED

### 4.2 Shot-level performance fields

Do **not** blindly copy `ShotActingBeat`. Classify each current field:

| Field | Spec home | Class | Why |
|---|---|---|---|
| `action` (before) | `shotPerformance.before.action` | **AUTHORITY** | Director card + I2V locomotion. OS currently freezes only this + body. FACT |
| `body` (before) | `shotPerformance.before.body` | **AUTHORITY** | Hold-body vs walk is a visual-performance fact. FACT |
| `gaze` (before / after) | `shotPerformance.before.gaze` / `after.gaze` | **AUTHORITY** | Consumed by VisualSpec + I2V; **dropped** by OS flatten. FACT |
| `gazeTarget` | `shotPerformance.gazeTarget` | **OPTIONAL AUTHORITY** | Exists on `VisualSpec.gazeTarget`, not on `ShotActingBeat`. Promote if Director-set; else DERIVED from visual. FACT + RECOMMENDED |
| `room` | `shotPerformance.before.room` | **OPTIONAL AUTHORITY** | On beat; not in OS flatten; I2V may use. FACT |
| `prop` | `shotPerformance.before.prop` | **OPTIONAL AUTHORITY** | Same as room. FACT |
| `emotion` (during) | `shotPerformance.context.emotion` | **AUTHORITY** (shot **context**, not line delivery) | Writable on beat; must not silently replace line emotion. FACT |
| `intensity` (during) | `shotPerformance.context.intensity` | **OPTIONAL AUTHORITY** | On beat type, **not** in Director editor. Line intensity is the edited one. FACT |
| `pace` (during) | — | **LEGACY** at shot | Shot `during.pace` is not edited. Line `pace` is the delivery authority. FACT |
| `speech` (during) | — | **DERIVED** | Set today from voice duration > 0.2s. Infer from `lines.length`. FACT |
| `before` slice | `shotPerformance.before` | **AUTHORITY** | Temporal visual beat. FACT |
| `during` slice | `shotPerformance.during` | **AUTHORITY** minus speech/pace | Shared visual play while speech happens. FACT |
| `after` slice | `shotPerformance.after` | **AUTHORITY** | Reaction visual. Lost at OS today. FACT |
| `holdSec` (before/after) | **not in Performance WHAT** | **LEGACY / Timing leak** | Timing V3 already owns lead-in / tail. `afterHoldSecOf` / `leadInSecOf` read beat holds. Keep on Timing V3; reference via `timingRef`. FACT + RECOMMENDED |
| `objective` | — | **ABSENT** | No domain type. Do not invent. FACT |
| `reaction` as named type | encoded as `after.action` + `after.gaze` | **AUTHORITY** (after slice) | No `Reaction` type exists. FACT |

Shot emotional context vocabulary = existing `ActingEmotion` closed set. FACT + RECOMMENDED  
Do not add Mood / FacialExpression / Gesture / Pose / EyeDirection / CharacterIntent. Those tokens are absent. FACT

```
ShotPerformanceSlice
  before:
    action?: string
    body?: string
    prop?: string
    gaze?: string
    room?: string
  during:
    emotion?: ActingEmotion          // shot CONTEXT
    intensity?: 1|2|3|4|5            // optional shot CONTEXT
  after:
    action?: string
    gaze?: string
  gazeTarget?: string                // optional
```

RECOMMENDED

No `holdSec`. No `speech`. No `pace` at shot. No provider duration. RECOMMENDED

### 4.3 Character-in-shot

**Purpose:** name **who is performing in this shot**, who owns visual play, who owns speech, and how they attach to lines. RECOMMENDED

This is **not** Canon. `characterId` is a pointer. Look / wardrobe / era stay on Character Identity. FACT + RECOMMENDED

```
CharacterInShot
  characterId: string                 // Canon / FamixaCharacter.id
  participation: IN_FRAME | OFF_FRAME | VOICE_ONLY
  visualRole: PRIMARY | SECONDARY | NONE
  speechRole: SPEAKER | LISTENER | NONE
  emotionalState?: ActingEmotion      // character state THIS SHOT, optional
  intensity?: 1|2|3|4|5               // optional
  lineIds: string[]                   // subset of lines[].lineId, same characterId
```

RECOMMENDED

| Field | Why it exists | Why it is not invented semantics |
|---|---|---|
| `characterId` | `shot.characterIds`, `cue.characterId`, lipsync speaker | FACT |
| `participation` | `offFrame`, CHAR-VO, CHAR-004 mention-only vs in-frame bodies | FACT |
| `visualRole` | `VisualPerson.role` primary \| secondary | FACT |
| `speechRole` | `dialogueSegmentIds` speakers vs silent bodies | FACT |
| `emotionalState` | needed when two bodies share one shot context but do not share one feeling | RECOMMENDED — optional, not required for one-character shots |
| `lineIds` | `linesForShot` already maps line → character | FACT |

**Is shot-level emotion sufficient?**

| Cast | Sufficient? | Rule |
|---|---|---|
| One in-frame character | **Yes** | `shotPerformance.context.emotion` is the character state unless `characters[0].emotionalState` is set. RECOMMENDED |
| Multiple in-frame, one speaker | **Shot context is not sufficient for both bodies** | Speaker may use line delivery; listener uses own `emotionalState` or inherits shot context. RECOMMENDED |
| Multiple simultaneous speakers | **Not supported as LipSync execution** | Contract may **record** both characters; LipSync must **fail closed**. FACT (`MULTI_SPEAKER_LIPSYNC_SUPPORTED = false`) |

Do not silently treat first line as the shot. `shotProductionIntentOf` does that today. That is DERIVED / first-line collapse, not the target authority. FACT

### 4.4 Ordered dialogue lines

```
PerformanceLine
  lineId: string
  characterId: string
  textIdentity:
    text: string                      // spoken text after stripStageDirection
    textHash: "LINE_TEXT_V1:" + hex   // identity of spoken text, not VoiceId
  delivery:
    emotion: ActingEmotion            // LINE DELIVERY
    intensity: 1|2|3|4|5
    pace: slow | natural | fast
    volume: low | mid | high
    pauseSec: number
    label: string                     // derived display from emotion+intensity
    emphasis?: string
    locked: true                      // snapshot lines are frozen; draft may be unlocked
```

RECOMMENDED

**WHAT (survives from `LinePerformance`):**  
`emotion`, `intensity`, `pace`, `volume`, `pauseSec`, `label`, `emphasis`, `locked`. FACT + RECOMMENDED

**WHAT (must be added):**  
`lineId`, `characterId`, spoken `text` / `textHash`. Today CREATE_VOICE hashes text via Story/LineText separately. The contract must own text **identity**, not ElevenLabs settings. RECOMMENDED

**HOW (must stay out):**

| Field | Home |
|---|---|
| `voiceId` | Voice execution selection (`FamixaCharacter.voiceId` / line `voiceId`) |
| `actingTtsVoiceSettings` stability / similarityBoost / style / speed | ElevenLabs adapter |
| `[sadly]` tags in spoken text | Forbidden by acting law when `language_code=vi` |
| pronunciation / SSML | **ABSENT** — do not add | 

FACT + RECOMMENDED

`label` is DERIVED from emotion+intensity (`labelOf`). Keep as frozen display copy inside the snapshot so UI does not re-infer. RECOMMENDED

### 4.5 References (not copies)

```
PerformanceTimingRef
  timingDocumentId: "FAMIXA_SHOT_TIMING_CONTRACT_V3"
  timingHash?: string                 // existing Timing V3 hash, not PerformanceHash
  // NO providerDurationSec 5|10
  // NO holdSec authority

PerformanceVisualRef
  visualAuthorityId?: string
  visualContractHash?: string
  characterIdentityIds: string[]      // same order as characters[].characterId

PerformanceStoryRef
  storyTextHash?: string              // StoryHash compatibility
  beatId?: string
  dialogueSegmentIds: string[]        // equals lines[].lineId order
```

RECOMMENDED

### 4.6 Source ref (snapshot only)

```
PerformanceSourceRef
  draftFingerprint?: string           // optional Phase A bridge
  actingBeatPresent: boolean
  linePerformanceLocked: boolean[]
  derivedFromInfer: boolean           // true if any line was unlocked infer at freeze — MUST fail closed if freeze requires lock
```

RECOMMENDED

**Freeze rule:** a snapshot used by CREATE_VOICE must have every spoken line `locked: true`. Unlocked infer remains draft only (`resolveLinePerformance` today). FACT + RECOMMENDED

CREATE_VIDEO on a silent shot may freeze with `lines = []`. RECOMMENDED

---

## 5. EMOTION RULE

Mandatory. Do **not** pick `actingBeat.during.emotion` or `LinePerformance.emotion` as the single field. FACT that both are writable today.

### 5.1 Three layers

| Layer | Name | Field | Meaning | Example |
|---|---|---|---|---|
| L1 | Shot emotional **context** | `shotPerformance.during.emotion` | The weather of the shot — what the frame is holding | WORRIED (`uneasy`) |
| L2 | Character emotional **state** | `characters[i].emotionalState` | What this body is holding in this shot | Listener stays `hurt`; speaker may differ |
| L3 | Line **delivery** | `lines[j].delivery.emotion` | How this spoken line is played | DEFENSIVE (`annoyed` / `tense`) |

RECOMMENDED  
Vocabulary remains `ActingEmotion`. The words WORRIED / DEFENSIVE in the prompt are **examples**, mapped onto that closed set by Director, not new enums. RECOMMENDED

### 5.2 Divergence is allowed

Shot `uneasy` + line `annoyed` is **valid** if the Director meant: the room is worried, the line is defensive. RECOMMENDED

That pair is **not** a conflict. It is two layers. FACT that current code already allows both to be written independently.

### 5.3 Precedence (who reads what)

| Consumer | Reads | Must not treat as SoT |
|---|---|---|
| Motion / I2V visual play | L1 + L2 (speaker body) + before/after action | L3-only first-line collapse (`shotProductionIntentOf`) |
| Still face (`stillFaceFromPerformance`) | L3 of the speaker line | L1 alone |
| TTS / CREATE_VOICE | L3 of **that** `lineId` | L1, L2, first-line-of-shot |
| LipSync | **no emotion field** — parents + `PerformanceSnapshotId` | any emotion as Fal input |
| CREATE_IMAGE projection | L1 + speaker L3 for face, L1+L2 for body | becoming Performance authority |

RECOMMENDED  
Current still-face path already uses line performance. FACT  
Current I2V uses beat emotion via `actingPerformanceLine`. FACT  
Current fingerprints use first-line emotion. FACT — that first-line collapse is **not** the target rule.

### 5.4 Inheritance

Applied only when a layer is **unset** on the draft. Never overwrite a set field. RECOMMENDED

```
if line.delivery.emotion unset:
    inherit character.emotionalState
    else inherit shot.during.emotion
    else inferActingDirection()     // draft only
    else 'neutral'

if character.emotionalState unset:
    inherit shot.during.emotion
    else remain unset (silent extra)

if shot.during.emotion unset:
    do not inherit from a line
    remain unset or deriveActingBeat() as DRAFT only
```

RECOMMENDED

**Critical:** shot context does **not** inherit from the first line. That is the current `shotProductionIntentOf` bug-shaped collapse. FACT  
A line does not rewrite shot context. `applyTimingToShot` already refuses to overwrite a saved beat. FACT + RECOMMENDED (keep)

### 5.5 Conflict detection

| Situation | Class | Action |
|---|---|---|
| L1 ≠ L3 | **allowed divergence** | record both; no error |
| L2 ≠ L1 | **allowed** if L2 explicitly set | record both |
| Two L3 values on the **same** `lineId` | **conflict** | fail freeze |
| Line `characterId` not in `characters[]` and not `VOICE_ONLY` | **conflict** | fail freeze |
| Two characters marked PRIMARY | **conflict** | fail freeze |
| `speechRole=SPEAKER` with empty `lineIds` on a spoken shot | **conflict** | fail freeze |
| Two different L1 values | impossible — one shot slice | — |
| Infer vs locked both stored as authority | **conflict** | freeze requires lock for spoken lines |

RECOMMENDED

Do not add a numeric “emotion distance” checker. That would invent semantics. RECOMMENDED

### 5.6 Intensity

Same three-layer rule. Line `intensity` 1–5 is the delivery authority (edited today). Shot `during.intensity` is optional context (not in Director editor). FACT + RECOMMENDED

TTS HOW may map L3 emotion buckets more than the number (`actingTtsVoiceSettings`). That mapping stays HOW. FACT

### 5.7 Multi-character emotion

- Shared L1 = one.  
- Each body may have L2.  
- Each line has L3.  
- LipSync of two speakers remains **unsupported**. The contract can still **describe** both L2 values; execution fail-closes. FACT + RECOMMENDED

---

## 6. SNAPSHOT / HASH IDENTITY

### 6.1 Freeze

```
PerformanceDraft
      ↓  FreezePerformance(buildId, shotCode, createdBy)
PerformanceSnapshot
```

RECOMMENDED

Freeze is a **new object**. It does not mutate the draft. Old snapshots remain. RECOMMENDED (same immutability model as OS InputSnapshot / artifacts)

### 6.2 Identifiers

| Id | Form | Meaning |
|---|---|---|
| `PerformanceSnapshotId` | UUID | This freeze **instance**. Audit / FK. Never a hash. |
| `PerformanceHash` | `PERFORMANCE_HASH_V1:` + lowercase hex | Canonical **content** identity of WHAT |

RECOMMENDED

Do **not** reuse `ActingHash`, `actingBeatFingerprint`, `VoiceHash`, `SnapshotHash`, or FNV `productionHash` as either id. RECOMMENDED  
A new hash **function** may be SHA-256 of canonical bytes. That is a new **namespaced** digest, not the legacy ActingHash value. RECOMMENDED

### 6.3 Canonical serialization (`PERFORMANCE_CANON_V1`)

Rules (implementation phase must follow exactly; not implemented now):

1. UTF-8 JSON.  
2. Object key order **as specified in this document**, not JS insertion order of extras.  
3. Omit `undefined` / empty optional strings.  
4. Arrays in specified order (`characters` = `shot.characterIds`; `lines` = `dialogueSegmentIds`).  
5. No provider fields. No URLs. No VoiceId. No duration bins.  
6. `label` included as frozen display.  
7. Header ids (`performanceSnapshotId`, `frozenAt`, `createdBy`) are **excluded** from the hash payload so two freezes of the same draft produce the same `PerformanceHash`.  
8. `buildId` + `shotCode` + `schemaVersion` + slices **are** in the hash payload (intent is per shot per build).  

RECOMMENDED

```
hashPayload = {
  documentId, schemaVersion, buildId, shotCode, episodeCode, sceneCode,
  shotPerformance, characters, lines, timingRef, visualRef, storyRef
}
performanceHash = "PERFORMANCE_HASH_V1:" + sha256_hex(stableJson(hashPayload))
```

RECOMMENDED

### 6.4 Proof property

| Claim | Rule |
|---|---|
| Same snapshot instance | equal `PerformanceSnapshotId` |
| Same WHAT | equal `PerformanceHash` |
| Different intent | unequal `PerformanceHash` — even if ActingHash accidentally matches |

RECOMMENDED

Legacy `ActingHash` of `"walks  stands"` can collide across different gaze/emotion. That is why it cannot remain authority. FACT

### 6.5 Production OS InputSnapshot change (design only)

Current columns: `acting_beat` TEXT, `acting_hash`. FACT

Target structurally:

| New | Required on | Notes |
|---|---|---|
| `performance_snapshot_id` | CREATE_VIDEO, CREATE_VOICE, CREATE_LIPSYNC | UUID FK |
| `performance_hash` | same | namespaced digest |
| `performance_payload_json` | VIDEO + VOICE | **store structured snapshot**, not only a pointer |

RECOMMENDED

**Store payload, do not only reference.** Graph draft mutates after freeze. A pointer into `graph_json` would make “immutable snapshot” false. OS already stores `story` / `visual_contract` text for that reason. FACT + RECOMMENDED

`acting_beat` / `acting_hash` remain during Phase A–D as **derived compatibility** (visual-slice flatten or empty). They must not be written as the VIDEO identity after Phase B. RECOMMENDED

`SnapshotHash` (task) must include `performance_hash` once Phase B lands, and must stop treating flatten `ActingHash` as the acting term. RECOMMENDED

---

## 7. VIDEO INTEGRATION

```
CREATE_VIDEO
    required WHAT:
      ApprovedKeyframe
      Story
      PerformanceSnapshotId + PerformanceHash     // replaces ActingBeat as identity
      VisualContract
      Timing
      FrozenProviderModel
    frozen proof:
      inputSnapshot.performanceSnapshotId
      inputSnapshot.performanceHash
      inputSnapshot.performance_payload_json
```

RECOMMENDED

CREATE_VIDEO **must not** flatten `action + body` as authoritative acting identity. FACT that it does this today in `executeProductionOsAction`. RECOMMENDED to stop after Phase C.

`ShotExecutionCompiler` today emits `ACTING: {acting string}`. That string becomes HOW compiler output, not the identity. After cutover, compiler input is the structured snapshot; output may still be a prompt line. RECOMMENDED

Provider adapter receives **compiled HOW only** (current `compileI2vPrompt` / Runway adapter). No Runway field enters the snapshot. FACT + RECOMMENDED

Required-input rename in `ProductionActionContractRegistry.CreateVideo`: `ActingBeat` → `PerformanceSnapshot` (keep `ActingBeat` as alias in Phase B only). RECOMMENDED

---

## 8. VOICE INTEGRATION

```
CREATE_VOICE
    required WHAT:
      PerformanceSnapshotId + PerformanceHash
      lineId
      characterId
      delivery  (from snapshot.lines[lineId])
    required execution inputs (keep):
      LineText          // must equal snapshot textIdentity.text
      VoiceId           // HOW selection — not in PerformanceHash
    frozen proof:
      same performanceSnapshotId / performanceHash as sibling VIDEO
      VoiceHash may remain SHA256(lineId|speaker|voiceId|Story|performanceHash)
```

RECOMMENDED

Voice **must** be able to prove all four: `lineId`, `characterId`, delivery, `performanceSnapshotId`. Today it proves `lineId|speaker|voiceId|Story` only. FACT

Gate: `LineText` ≠ snapshot `textIdentity.text` → reject (`LINE_TEXT_MISMATCH`). RECOMMENDED  
Gate: `characterId` ≠ line.characterId → reject. RECOMMENDED  
Gate: VoiceId missing → reject as today. FACT  
VoiceId change does **not** change `PerformanceHash`. It changes `VoiceHash` / execution HOW. RECOMMENDED

`actingTtsVoiceSettings` remains HOW compiled from L3 delivery. FACT + RECOMMENDED

CREATE_VOICE required inputs today omit ActingBeat. FACT — that is the missing boundary this section closes.

---

## 9. LIPSYNC INTEGRATION

Preserve physical parent lineage. FACT that this is correct today.

```
CREATE_LIPSYNC
    required:
      MotionArtifactId          // VIDEO_TAKE
      VoiceArtifactId           // VOICE
      FrozenProviderModel
      PerformanceSnapshotId     // NEW stale-proof
      PerformanceHash           // NEW
    parents[] = [motionId, voiceId]   // unchanged identity model
```

RECOMMENDED

LipSync **does not** receive emotion / action / delivery as provider inputs. Speaker region / face pin remain HOW (`famixa-fal-v3-lipsync-speaker-how.ts`). FACT + RECOMMENDED

### 9.1 Cross-snapshot refuse

```
parent VIDEO.performanceHash  must equal  parent VOICE.performanceHash
parent VIDEO.shotCode         must equal  parent VOICE.shotCode
parent VIDEO.buildId          must equal  parent VOICE.buildId
```

RECOMMENDED

If VIDEO was frozen on P001 and VOICE on P002:

- **DENY** `PERFORMANCE_SNAPSHOT_MISMATCH`  
- unless **compatibility rule** below matches.

**Explicit compatibility rule (only):**

1. `PerformanceHash` equal (same WHAT; two freeze UUIDs of identical canon payload), **and** same `buildId` + `shotCode`.  
2. No other rule. No “emotion close”. No “same lineId is enough”. No cross-shot.  

RECOMMENDED

MIX continues to take Motion-or-LipSync artifact + Timing. It inherits Performance proof **through the parent artifact**, plus optional copied `performanceHash` on the MIX snapshot for query convenience. RECOMMENDED  
MIX must not introduce a second performance authority. RECOMMENDED

---

## 10. TIMING RELATIONSHIP

Do **not** redesign Timing V3. FACT (non-goal)

| Concern | Owner |
|---|---|
| How the character performs | Performance Contract |
| How long the performance lasts | Timing V3 (`performanceDurationSec`, `editorialDuration`, `voiceDurationSec`) |
| Provider 5 \| 10 bins | HOW (`mapProviderDuration`, `shot.seconds`) |
| Lead-in / tail / `holdSec` | Timing V3 (today leaked onto `actingBeat.*.holdSec`) |

FACT + RECOMMENDED

Performance Contract **references** Timing V3 via `timingRef.timingHash` / document id. It does not copy `providerDurationSec`. RECOMMENDED

`during.speech` remains derived from lines / measured voice, not a Performance authority. FACT

Assemble `pauseSec` stays on **line delivery** (already `LinePerformance.pauseSec`). That is performance, not Timing V3 clock. FACT + RECOMMENDED  
Timing V3 may still **sum** those pauses when computing duration. That is duration math consuming a performance field, not ownership inversion. RECOMMENDED

---

## 11. INVALIDATION MATRIX

Old artifacts remain immutable. New snapshot = new tasks. Same as current OS SUPERSEDED model. FACT + RECOMMENDED

Example:

```
P001 → VIDEO V001 + VOICE A001 → LIPSYNC L001 → MIX M001
Acting change → freeze P002
V001 STALE, A001 STALE, L001 STALE, M001 STALE
```

RECOMMENDED — this is **new** for VOICE. Today acting-beat save does **not** stale VOICE. FACT

Labels: use existing `CURRENT` / `STALE` / `ACCEPTED_STALE` / `SUPERSEDED`. Do not invent `UNBOUND` as a required enum. FACT

| Change | IMAGE | VIDEO | VOICE | LIPSYNC | MIX | Notes |
|---|---|---|---|---|---|---|
| Story / beatText | STALE if VisualSpec derive changes | STALE (StoryHash + new draft) | STALE (text identity / Story) | STALE via parents | STALE via parent | FACT today for VIDEO StoryHash; VOICE Story in VoiceRequirements |
| Shot visual acting (action/body/gaze/room/prop/before/after) | **Projection STALE** if still used those fields; else UNCHANGED | STALE (new P) | STALE (new P — **new vs today**) | STALE | STALE | Today VOICE UNCHANGED. FACT |
| Shot L1 emotion / intensity | STALE if still-face/projection bound | STALE | STALE (new P) | STALE | STALE | Same |
| Character-in-shot L2 / participation / roles | STALE if persons change | STALE | STALE if speaker/character set changes | STALE | STALE | |
| Line **text** | STALE if still face/text overlay | STALE (new P; text in contract) | STALE | STALE | STALE | FACT voice fp includes text |
| Line **delivery** L3 | STALE if stillFaceFromPerformance | STALE (new P) | STALE | STALE | STALE | FACT voice fp includes first-line emotion only today |
| VoiceId | UNCHANGED | UNCHANGED | STALE (VoiceHash / HOW) | STALE via voice parent | STALE | VoiceId **not** in PerformanceHash |
| Timing V3 duration / editorial / provider bin | UNCHANGED | STALE TimingHash | UNCHANGED unless measured duration gate | STALE (`performanceDurationSec` in lipsync fp) | STALE | FACT |
| Visual Authority / Visual Contract / Canon identity | STALE | STALE VisualHash | UNCHANGED | UNCHANGED until VIDEO remake | STALE via video | PerformanceHash **unchanged** |
| Provider / model | UNCHANGED WHAT | STALE SnapshotHash HOW | STALE HOW | STALE HOW | STALE HOW | FACT |

RECOMMENDED

### CREATE_IMAGE specifically

CREATE_IMAGE required inputs today: VisualContract + CharacterIdentity. **Not** ActingBeat. FACT

Target: IMAGE **may** bind `visualPerformanceProjectionHash` derived from L1 + speaker L3 + action/gaze. If that projection is bound, IMAGE STALE when those fields change. If Phase B does not bind it, IMAGE stays UNCHANGED on acting-only edits (current behavior). RECOMMENDED

IMAGE must **not** require `PerformanceSnapshotId` as its authority. RECOMMENDED

---

## 12. MULTI-CHARACTER RULES

Do not pretend unsupported cases work. FACT

| Case | Current support | Contract | Execution |
|---|---|---|---|
| One character / one line | SUPPORTED | 1 character SPEAKER, 1 line | VIDEO + VOICE + LIPSYNC |
| One character / multiple lines | SUPPORTED (`mergeSameSpeakerCues`) | 1 character, N lines | VOICE per line; LipSync one speaker |
| Two faces / **one** speaker | SUPPORTED (speaker pick HOW) | 2 IN_FRAME; 1 SPEAKER + 1 LISTENER | LipSync HOW pin; not a contract vendor field |
| Multiple characters / multiple **speakers** | BLOCKED (`MULTI_SPEAKER_LIPSYNC_SUPPORTED = false`, `MULTI_SPEAKER_BLOCKED`) | May **describe**; freeze allowed | LipSync **FAIL CLOSED** |
| Multiple characters / no dialogue | SUPPORTED silent VIDEO | characters[], `lines=[]` | No CREATE_VOICE; LipSync not required |
| Overlapping simultaneous speech | NOT SUPPORTED | fail freeze if two lines claim overlapping speech without split | FAIL CLOSED |
| CHAR-VO / off-frame voice | SUPPORTED as voice-only | `participation=VOICE_ONLY` | No body in still |
| CHAR-004 mention-only in frame | Forbidden by parse (`offFrame`) | must not appear IN_FRAME | FAIL CLOSED if listed IN_FRAME |

FACT + RECOMMENDED

`actingOfLines` / `shotProductionIntentOf` first-line collapse is **illegal** as authority under this spec. It may remain a Phase A derived display. FACT + RECOMMENDED

---

## 13. LEGACY MAPPING

No deletes in the implementation of this spec (this phase writes no code). RECOMMENDED for the later implementation phases:

| Current source | Class |
|---|---|
| `ShotActingBeat` visual fields (action, body, gaze, room, prop, before/during/after emotion) | **MIGRATE** → `shotPerformance` |
| `ShotActingBeat.holdSec` | **LEGACY** → Timing V3 |
| `ShotActingBeat.during.speech` / `during.pace` | **DERIVED** / **LEGACY** |
| `LinePerformance` delivery fields | **MIGRATE** → `lines[].delivery` |
| `LinePerformance.locked` | **KEEP** as freeze gate |
| `inferActingDirection` | **DERIVED** draft only |
| `VisualSpec.performance` / gaze / emotion | **DERIVED** from contract projection |
| `VisualContract.performance` | **DERIVED** |
| `ShotProductionIntent.emotion` / `acting` | **DERIVED** / first-line **LEGACY** |
| `FamixaCharacterState.emotion/goal/fear/behavior` | **KEEP** story memory — **never** snapshot authority |
| Scene `performance` / `performances` | **LEGACY** / **REMOVE LATER** |
| `actingBeatFingerprint` | **DERIVED** compatibility |
| `voiceInputFingerprint` | **DERIVED** compatibility |
| OS `acting_beat` TEXT | **LEGACY** flatten — **REMOVE LATER** |
| OS `ActingHash` | **LEGACY** / **DERIVED** compatibility |
| `ACTING_LAW_LOCK` | **KEEP** film policy, not a field |
| Character `performance` / `voiceNote` / `voiceBible` | **KEEP** character bible — not shot execution |
| `KitVideoCharacterState` | **LEGACY** other engine |
| Character Design `BodyLanguage` | **KEEP** look language — not shot acting |

RECOMMENDED

### Story memory (explicit ban)

`FamixaCharacterState.{emotion,goal,fear,behavior}` is **film/character arc** written on the Story Memory card. FACT

It may **inform a human** when drafting L1/L2. It must **not**:

- enter `PerformanceHash`
- satisfy CREATE_VIDEO / CREATE_VOICE required inputs
- stale or un-stale artifacts by itself

RECOMMENDED

---

## 14. MIGRATION PLAN

Design only. No migration execution. No big-bang drop.

### Phase A — contract alongside old data

- Specify + persist `PerformanceDraft` projected from existing `actingBeat` + locked lines + `characterIds`.  
- Do not require OS columns yet.  
- Do not change CTAs.  
- Draft is dual-written or projected.  
- Tests: projection determinism, emotion-layer split, no provider calls.

RECOMMENDED

### Phase B — Production OS references snapshot

- Add nullable `performance_snapshot_id`, `performance_hash`, `performance_payload_json`.  
- Keep `acting_beat` / `acting_hash` populated as derived compatibility.  
- Task `SnapshotHash` **gains** `performance_hash` when present.  
- TEST scope only.

RECOMMENDED

### Phase C — Video / Voice / LipSync use snapshot

- CREATE_VIDEO requires snapshot id+hash. Flatten is not identity.  
- CREATE_VOICE requires same snapshot + lineId + LineText + VoiceId.  
- CREATE_LIPSYNC keeps parent artifacts; adds mismatch refuse.  
- Invalidation: acting change stales VIDEO **and** VOICE.

RECOMMENDED

### Phase D — legacy acting fields become derived

- Director card writes draft slices; `ShotActingBeat` becomes a view.  
- `actingBeatFingerprint` / `voiceInputFingerprint` hash the snapshot or are labeled compatibility.  
- `ActingHash` computed from payload for old readers only.

RECOMMENDED

### Phase E — remove later

- Drop flatten as required input.  
- Remove first-line `shotProductionIntentOf` emotion authority.  
- Scene performance strings unused.  
- Not this program increment.

RECOMMENDED

Official EP01 / EP99 are not migrated by this spec. FACT (non-goal)

---

## 15. TEST PLAN

Unpaid TEST-scope only. No Gemini / Runway / Veo / Fal / ElevenLabs. No official film mutation. RECOMMENDED

| Id | Assertion |
|---|---|
| T01 | Same `PerformanceSnapshotId` on TEST VIDEO + VOICE → accept pairing |
| T02 | VIDEO P001 + VOICE P002 (`PerformanceHash` differs) → LipSync reject `PERFORMANCE_SNAPSHOT_MISMATCH` |
| T03 | Acting visual field change → new hash → VIDEO+VOICE+LIPSYNC+MIX STALE; old artifacts immutable |
| T04 | Line delivery change → new hash → VOICE+downstream STALE; VoiceId unchanged |
| T05 | Story / line **text** change → new `textHash` + new `PerformanceHash` → VOICE+VIDEO STALE |
| T06 | Timing-only change → `TimingHash` changes; `PerformanceHash` **unchanged**; VIDEO timing-stale; VOICE WHAT unchanged |
| T07 | Visual Authority / VisualContract change → VisualHash STALE; `PerformanceHash` **unchanged** |
| T08 | Two IN_FRAME, one SPEAKER → snapshot valid; LipSync speaker HOW not in payload |
| T09 | One SPEAKER, N lines → order = `dialogueSegmentIds`; each line delivery distinct |
| T10 | Reload TEST task → same `performanceSnapshotId` / `performanceHash` / payload bytes |
| T11 | Two freezes of identical draft → same `PerformanceHash`, different `PerformanceSnapshotId` allowed |
| T12 | Mutating draft after freeze must not change stored payload / hash |
| T13 | Cross-shot: SH01 snapshot cannot attach to SH02 task |
| T14 | Cross-scope: TEST snapshot cannot satisfy official / other buildId |
| T15 | `ActingHash` or `actingBeatFingerprint` presented as `PerformanceSnapshotId` → reject; legacy hash is not authority |

RECOMMENDED

T08 multi-**speaker** LipSync must assert FAIL CLOSED, not a fake pass. FACT + RECOMMENDED

---

## 16. RISKS

1. **Silent single-field pick** (beat vs line emotion) would look like a data bug. The three-layer rule is mandatory. FACT + RECOMMENDED  
2. Making VOICE stale on acting visual change is a **behavior change**. Operators must see one snapshot id, not a surprise remake. FACT  
3. Pointer-only OS storage would lie about immutability because `graph_json` mutates. RECOMMENDED store payload  
4. Reusing SHA256/FNV **values** as the new id would fake equality between VIDEO flatten and VOICE line hash. FACT  
5. Putting `VoiceId` or Runway duration into the snapshot would bind HOW into WHAT. RECOMMENDED forbid  
6. First-line `shotProductionIntentOf` will keep lying until Phase D. FACT  
7. `holdSec` on the beat will keep looking like performance if the Director card is not split from Timing. FACT  
8. Multi-speaker description without fail-closed LipSync would over-claim. FACT  
9. CREATE_IMAGE over-binding would stale stills on every line-delivery tweak; under-binding leaves still face stale-proof missing. RECOMMENDED optional projection hash  
10. Official EP01/EP99 graphs were not queried this phase. UNVERIFIED live emotion divergence on those films  

---

## 17. NON-GOALS

This specification phase, and the first implementation increment of this contract, do **not** include:

- real AI / paid provider calls  
- provider bridge or benchmark  
- visual quality / lip-sync quality  
- final rendering / FILM READY  
- assembly redesign (ASSEMBLE already closed separately)  
- UI redesign  
- Canon changes  
- Visual Authority changes  
- Timing V3 redesign  
- pronunciation / SSML  
- new emotion vocabulary  
- film-level or scene-level execution contract  
- `ShotActingBeat` rename-as-done  
- Pharmacy / Family OS / Kit Sales  

FACT (mission) + RECOMMENDED

---

## 18. FINAL CANONICAL DIAGRAM

```
                         STORY WHAT
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
   Character Identity   Performance Draft    Timing V3
   + Visual Authority         │              (DURATION)
          │                   ▼
          │           Performance Snapshot
          │           Id + PERFORMANCE_HASH_V1
          │                   │
          │         ┌─────────┼─────────┐
          │         ▼         ▼         ▼
          │   CREATE_VIDEO  CREATE_VOICE  CREATE_IMAGE
          │   reads snapshot reads same   reads projection
          │         │         │           (not authority)
          │         ▼         ▼
          │      VIDEO_TAKE  VOICE
          │      artifactId  artifactId
          │         │         │
          │         └────┬────┘
          │              ▼
          │        CREATE_LIPSYNC
          │        parents = artifacts
          │        + same PerformanceHash
          │              ▼
          │             MIX
          │              ▼
          │           BINDING
          │
SHOT WHAT = Story + Performance Snapshot + Visual Authority refs + Timing refs
HOW       = Provider + Model + Compiler + Adapter
EXECUTION = Production OS (Request → Task → InputSnapshot → Attempt → Artifact → Binding)
IDENTITY  = Artifact IDs + PerformanceSnapshotId + PerformanceHash
```

RECOMMENDED

**Never** production authority:

- latest URL  
- latest take  
- `acceptedTake.url`  
- `previewUrl`  
- localStorage  
- IndexedDB  
- provider URL  

FACT (Production OS already forbids those as MIX fallbacks)

---

## PROVIDER INDEPENDENCE PROOF

The schema in §4 contains **no** Runway, Veo, Gemini, ElevenLabs, Fal, Wan, model ids, provider payloads, vendor duration bins, or vendor prompt syntax. RECOMMENDED

Those remain below the contract:

| HOW site | Class |
|---|---|
| `compileI2vPrompt` / `compileRunwayPromptV1` | ADAPTER |
| `actingTtsVoiceSettings` | ADAPTER |
| Fal speaker region / frame | ADAPTER |
| `FrozenProviderModel` | OS HOW |
| `mapProviderDuration` 5 \| 10 | HOW |
| `motionLegacyExecutionFingerprintOf` mixing provider into content hash | LEGACY |

FACT

---

## IMPLEMENTATION PHASE GATE

Next increment (not this document): Phase A projection + unpaid T01–T15 harness design, still no official film, still no providers. RECOMMENDED

STOP.
