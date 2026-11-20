# FAMIXA SHARED ACTING / PERFORMANCE CONTRACT V1
FORENSIC REPORT

Date: 2026-09-18  
Mode: READ TRACE → COMPARE → CLASSIFY  
Code changes: none  
Migrations: none  
UI changes: none  
Provider calls: none  
Media generation: none  
Film mutation: none

Do not read this as FILM READY.  
Do not read this as REAL PROVIDER READY.  
Do not read this as an implemented Acting Contract.

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

**The next architectural gap is real, and it is not provider integration.** FACT

Famixa already has acting/performance data. It does **not** have one shared, immutable Performance Intent that Motion and Voice can both prove they were generated from. FACT

There are **two parallel WHAT layers** plus several derived/legacy copies:

| Layer | Granularity | Type | Authority today |
|---|---|---|---|
| Shot acting beat | Shot | `ShotActingBeat` | Director card → `episode.shots[].actingBeat` |
| Line performance | Dialogue line | `LinePerformance` | Locked line field, else regex infer |
| Visual performance string | Shot (derived) | `VisualSpec.performance` | `deriveVisualSpec` |
| Production OS acting | Shot (flattened TEXT) | `acting_beat` + `acting_hash` | Client sends `before.action + before.body` only |
| Story-memory emotion | Character / film | `FamixaCharacterState.emotion` | Story Director memory, not shot execution |

FACT. Types and write paths are in §2–§5.

**CREATE_VIDEO and CREATE_VOICE cannot currently prove the same performance intent.** FACT

- VIDEO identity: shot `actingBeatFingerprint` (full JSON) and/or OS `ActingHash` of flattened TEXT.  
- VOICE identity: line `voiceInputFingerprint` (text + voiceId + first-line `LinePerformance`) and/or OS `VoiceHash` of `lineId|speaker|voiceId|Story`.  
- No `PerformanceIntentId`. No `ActingSnapshotId`. Repo-wide grep: 0 matches. FACT

LipSync receives **parent artifact IDs + fingerprints + speaker HOW**, not emotion/action/delivery. FACT

**RECOMMENDED** next implementation phase (not started): a provider-agnostic, snapshot-immutable Shared Acting / Performance Contract owned at **shot + character-in-shot + ordered line** composition, referenced by CREATE_VIDEO and CREATE_VOICE, consumed as parent identity by LIPSYNC, coexisting with Timing V3 and Visual Authority. Do not implement in this report.

---

## 2. CURRENT ACTING SOURCES

Search covered Famixa Content (`client/admin/src/modules/content`, `src/Packs/Content`). Marketing `content_performance` / package performance cards are **out of scope** (metrics, not acting). FACT

Tokens **absent as domain types** in Famixa Content (UNVERIFIED as types; not invented):  
`EmotionalState`, `Mood`, `Objective`, `FacialExpression`, `Gesture`, `Pose`, `EyeDirection`, `Delivery`, `VoiceEmotion`, `SpeechRate`, `Tone`, `CharacterIntent`, `PerformanceIntentId`, `ActingSnapshotId`.

| FILE | TYPE | FIELD | SOURCE | AUTHORITY | TRANSFORMATION | STORAGE | HASH | SNAPSHOT | CONSUMER | CLASS |
|---|---|---|---|---|---|---|---|---|---|---|
| `famixa-shot-production-timing.ts` | interface `ShotActingBeat` | `before.{action,body,prop,gaze,room,holdSec}` `during.{speech,emotion,intensity,pace}` `after.{action,gaze,holdSec}` | `deriveActingBeat` regex; `patchActingBeat` UI | Saved `shot.actingBeat` wins over derive | Regex from story/visual; UI patch | `graph_json` `episode.shots[].actingBeat` | `actingBeatFingerprintOf` | OS `acting_beat` TEXT (partial) | I2V, Timing V3 holds, OS claim | **WHAT** |
| `ShotProductionActingBeat.ts` | functions | editable: action, body, gaze, room, emotion | Director card | Same shot beat | `patchActingBeat` keeps intensity/pace/hold from prev | graph via `persistActingBeat` | fingerprint after save | — | remake gate | **WHAT** |
| `content-famixa-acting-law.ts` | `ActingEmotion` `ActingDirection` `LinePerformance` | emotion, intensity 1–5, pace, volume, pauseSec, label, emphasis?, locked? | `inferActingDirection`; `lockLinePerformance` | `performance.locked === true` | Regex from text/character | `scenes[].dialogue[].performance`, `lines[].performance` | via `voiceInputFingerprint` | **not** in OS CREATE_VOICE | TTS, still face, assemble pause | **WHAT** |
| `content-famixa-visual-spec.ts` | `VisualSpec` | `performance`, `gaze`, `gazeTarget`, `emotion`, `startState`, `endState`, `shotAction` | `deriveVisualSpec` | Derived from script/action, not actingBeat JSON | Regex / compile | ephemeral → KF `VisualContract` | none as acting id | OS `visual_contract` | still prompt | **DERIVED** |
| `content-famixa-kf-pipeline.ts` | `VisualContract` | `performance`, `gaze`, `action` | `compileVisualContract` | Derived | string compile | prompt text | prompt hash | CREATE_IMAGE visual | Gemini/still | **DERIVED** |
| `SeriesStillVisualIngressV1Rules.cs` | `SceneVisualContract` | `Emotion?` | still ingress request | Server strip of caller style | `StripCallerStyleAuthority` | still request | UNVERIFIED | — | still ingress | **HOW / ADAPTER** |
| `content-famixa-series.ts` | `FamixaSceneNode` | `performance?`, `performances?: Record<charId,string>` | `applyShotLockToGraph` | Scene notes | copy from shot action | graph `scenes[]` | none | — | display / lock | **LEGACY** |
| `content-famixa-series.ts` | `FamixaCharacter` | `performance?`, `voiceNote?`, `voiceBible?` | character edit | Character bible | free text | graph `characters[]` | none | — | voice profile notes | **WHAT / CACHE** |
| `content-famixa-story-memory.ts` | `FamixaCharacterState` | `emotion?`, `goal?`, `fear?`, `behavior?` | Story Memory card | Long-form story, not shot | human write | graph memory | none | — | Story Director | **WHAT** (film/character, not shot execution) |
| `kit-video-continuity.ts` | `KitVideoCharacterState` | kit-video continuity | Kit Video engine | Separate path | — | kit-video state | — | — | Kit Video | **LEGACY** (other engine) |
| `CharacterDesignLanguageV1/V2Rules.cs` | const `BodyLanguage` | look-language string | character design | Visual look, not shot acting | compile into design prompt | design language | — | — | still look | **WHAT** (look) / not shot performance |
| `ProductionEntities.cs` | `ProductionTaskInputSnapshot` | `ActingBeat`, `ActingHash` | FreezeInput | Frozen per task | SHA256 of acting **string** | `production_task_input_snapshots` | `acting_hash` | yes | CREATE_VIDEO compiler / drift | **WHAT** (flattened) |
| `ProductionApiDtos.cs` | DTO | `ActingBeat` string | UI POST | Client serialization | join action+body | request body | hashed on freeze | yes | OS | **WHAT** (lossy) |
| `FamixaExecutionProvenanceV1.cs` | record | `ActingBeatFingerprint?` | client freeze | legacy motion run | FNV `productionHash` | `runs[].frozenInput` | `actingBeatFingerprint` | client freeze | remake | **CACHE / DERIVED** |
| `ShotProductionIntent.ts` | `ShotProductionIntent` | `emotion?`, `acting?` | `shotProductionIntentOf` | **first line** `performance` | copy | ephemeral | into voice/kf/motion fps | no | fingerprints | **DERIVED** |
| `ACTING_LAW_LOCK` | const string | global law | compile-time | Film law | none | source | none | — | copy / UI | **WHAT** (policy) |

FACT for each row’s file/type/fields. STORAGE/HASH from the named functions. LIVE VERIFIED not claimed for every field on official film.

---

## 3. STORY → ACTING TRACE

```
shot.story / beatText / visual
        ├─ deriveActingBeat()          → ShotActingBeat (if none saved)
        ├─ Director “Diễn xuất”        → patchActingBeat → persistActingBeat → graph
        ├─ deriveVisualSpec()          → VisualSpec.performance / emotion / gaze
        └─ dialogue text
                ├─ inferActingDirection()     → draft LinePerformance
                └─ lockLinePerformance()      → locked LinePerformance
```

FACT. Functions: `deriveActingBeat` (`famixa-shot-production-timing.ts`), `patchActingBeat` / `persistActingBeat` (`ShotProductionActingBeat.ts`, `ContentFamixaSeriesTab.tsx`), `inferActingDirection` / `resolveLinePerformance` (`content-famixa-acting-law.ts`), `deriveVisualSpec` (`content-famixa-visual-spec.ts`).

**Where created:** auto-derive on timing pass (`applyTimingToShot` does **not** overwrite an existing beat). FACT  
**Where edited:** Director Acting Beat card (shot); Voice Script acting selects (line). IMPLEMENTED  
**Where stored:** `series_build.graph_json` / pilot `episode.shots[].actingBeat` and `scenes[].dialogue[].performance`; localStorage slim pilot keeps `actingBeat`. FACT  
**Where read:** I2V compiler, still mood, TTS, Timing V3 holds, Production OS claim string, remake fingerprints. FACT

**Ownership today is multiple levels — not one.** FACT

| Level | Evidence |
|---|---|
| Film | `ACTING_LAW_LOCK`; `FamixaCharacterState.emotion` in story memory |
| Scene | `FamixaSceneNode.performance` / `performances[charId]` |
| Shot | `shot.actingBeat`, `shot.timing` |
| Character-in-shot | `shot.characterIds`; lipsync `speakerCharacterId`; `actingSpeakerFromDialogue` |
| Dialogue line | `LinePerformance` on `dialogue[]` / `lines[]` |

Do not collapse these in this report. FACT

---

## 4. VISUAL PERFORMANCE TRACE

```
Story
  → ShotActingBeat (Director or deriveActingBeat)
  → deriveVisualSpec / compileVisualContract
  → Keyframe still prompt (PERFORMANCE / GAZE / face from LinePerformance)
  → compileI2vPrompt → Runway HOW prompt
  → freezeMotionInput (actingBeatFingerprint + promptHash + timingHash)
  → Production OS CREATE_VIDEO (ActingBeat TEXT + ActingHash)
```

FACT. Key functions: `compileShotStillMood` / `actingBeatStillAction` / `stillFaceFromPerformance`; `compileVisualContract`; `compileI2vPrompt` → `compileRunwayPromptV1`; `actingBeatFingerprintOf`; `ShotExecutionCompiler.Compile`; `RunwayMotionExecutionAdapter` joins `Story + ActingBeat + VisualContract`.

| Concept | Survives into KF? | Survives into motion prompt? | Survives into motion snapshot? | Survives into OS CREATE_VIDEO? |
|---|---|---|---|---|
| emotion (`actingBeat.during.emotion`) | Partial via `actingPerformanceLine` / still face if line perf used | Yes — `actingPerformanceLine(beat.during.emotion, speaker)` | Yes — inside full JSON fingerprint | **No** — not in client flatten |
| action (`before.action`) | Yes via still action | Yes unless hold-body drops locomotion | Yes (JSON fp) | **Yes** — first part of flatten |
| reaction (`after.*`) | UNVERIFIED in KF | Not in `compileI2vPrompt` | Yes (JSON fp) | **No** |
| expression | Via `stillFaceFromPerformance` (line) | Emotion clause only | JSON if in beat | **No** |
| body language (`before.body`) | Yes | Yes (hold-body logic) | Yes | **Yes** — second part of flatten |
| gaze | Yes (`VisualSpec.gaze`, beat gaze) | Yes | Yes | **No** |
| intensity | Line still face (`intensity/5`); beat intensity **not** in editor | **No** in `compileI2vPrompt` | Yes if present on beat JSON | **No** |
| character objective | **Absent** as a field | — | — | — |
| movement | Encoded as action/body | Transformed (hold vs walk) | Yes | Partial (action+body only) |

FACT for compiler inputs. UNVERIFIED = no call site found.

**OS boundary (lossy).** LIVE VERIFIED in current UI cutover path:

```
acting = [actingBeat.before.action, actingBeat.before.body].join(' ')
```

`ContentFamixaSeriesTab.tsx` `executeProductionOsAction`. Dropped: gaze, room, prop, entire `during.*`, entire `after.*`. FACT

---

## 5. VOICE PERFORMANCE TRACE

```
Story / Script line text
  → DialogueCue (characterId, text, voiceId)
  → resolveLinePerformance (locked LinePerformance or inferActingDirection)
  → actingTtsPerformText (script text only; strips [tags] and stage cues)
  → actingTtsVoiceSettings (HOW: stability, similarityBoost, style, speed)
  → previewContentSeriesTts / voiceAssets
  → Production OS CREATE_VOICE VoiceRequirements = lineId|speaker|voiceId|Story
```

FACT.

| Control | Authority | Evidence |
|---|---|---|
| text | Script line | `cue.text` → `actingTtsPerformText`; comment forbids ElevenLabs `[sadly]` tags when `language_code=vi` |
| speaker | Line `characterId` / `name` | `DialogueCue`; `actingSpeakerFromDialogue` is for motion, not TTS identity |
| voiceId | Character / line | `FamixaCharacter.voiceId`, `FamixaLine.voiceId`, `characterVoiceProfile` |
| emotion | `LinePerformance.emotion` | `resolveLinePerformance`; maps into TTS settings, **not** into spoken text |
| delivery | `pace` + `volume` + `label` | `actingTtsVoiceSettings` |
| pace | `LinePerformance.pace` | speed clamp |
| intensity | `LinePerformance.intensity` | label + still face; TTS uses emotion buckets more than the number |
| timing | Measured `voiceAssets[id].duration`; Timing V3 `voiceDurationSec` | `measuredVoiceDurations` |
| pauses | `LinePerformance.pauseSec` | assemble (`content-famixa-assemble.ts`) |
| pronunciation | **Absent** | UNVERIFIED — no SSML / pronunciation field |

CREATE_VOICE **required inputs** are `LineText`, `VoiceId` only. `ProductionActionContractRegistry.CreateVoice`. FACT  
CREATE_VOICE snapshot **does not require ActingBeat**. FACT

---

## 6. LIPSYNC TRACE

```
VIDEO_TAKE artifact + VOICE artifact
  → freezeLipSyncExecutionInput
  → CREATE_LIPSYNC VoiceRequirements = motionId|voiceId
  → Fixture / Fal HOW (video URL, voice bytes, speaker region)
```

FACT. Type `LipSyncExecutionFrozenInput` (`famixa-lipsync-execution-input-freeze.ts`):  
`motionArtifactId`, `motionFingerprint`, `motionDurationSec`, `voiceArtifactId`, `voiceFingerprint`, `voiceDurationSec`, speaker HOW fields, optional `motionUrl`, `voiceId`, `lineId`.

**No `emotion`, `intensity`, `actingBeat`, `LinePerformance`, `gaze`, `pace`.** FACT

`lipsyncInputFingerprint` hashes voice fp + motion fp + `performanceDurationSec` (Timing V3 clock, not acting emotion). FACT

LipSync therefore inherits performance **only indirectly** through whatever was already baked into the parent artifacts. FACT

---

## 7. VISUAL vs VOICE COMPARISON

| Concept | Visual source | Voice source | Same authority? | Same identity? | Same snapshot? | Same hash? | Same timing? | Same character? | Classification |
|---|---|---|---|---|---|---|---|---|---|
| Character | `shot.characterIds`, visual persons | `cue.characterId` | No | No | No | No | n/a | Often same CHAR id, not proven | **DUPLICATED** |
| Objective | Absent | Absent (`FamixaCharacterState.goal` is story memory) | — | — | — | — | — | — | **UNKNOWN** / story-only |
| Action | `actingBeat.before.action`, `shot.shotAction` | Not a TTS input | No | No | VIDEO only | `actingBeatFingerprint` vs none | Timing V3 separate | Shot vs line speaker | **VISUAL ONLY** |
| Emotion | `actingBeat.during.emotion` **and** `VisualSpec.emotion` **and** first-line `performance.emotion` | `LinePerformance.emotion` | No | No | No | No shared hash | No | First line vs beat speaker | **DUPLICATED** |
| Intensity | beat `during.intensity` (not in Director editor) | `LinePerformance.intensity` 1–5 | No | No | No | No | No | Line | **DUPLICATED** / **VOICE** stronger |
| Expression | `stillFaceFromPerformance`, `VisualSpec.performance` | TTS settings from emotion | No | No | No | No | No | Line name | **DERIVED** |
| Body language | `actingBeat.before.body` | Absent | No | — | VIDEO only | in beat fp | No | Shot | **VISUAL ONLY** |
| Gaze | `actingBeat.before.gaze`, `VisualSpec.gaze` | Absent | No | — | VIDEO only | in beat fp | No | Shot | **VISUAL ONLY** |
| Delivery | I2V emotion clause | `pace`/`volume`/`label` | No | No | No | No | No | Line | **VOICE ONLY** (plus derived visual) |
| Pace | `actingBeat.during.pace` (not edited) | `LinePerformance.pace` | No | No | No | No | Speed vs holdSec | Line | **DUPLICATED** |
| Timing | Timing V3 `performanceDurationSec` / holds from beat | measured voice duration + `pauseSec` | No | No | TimingHash ≠ voice duration | Client `timingHashOf` ≠ server `Hash(Timing)` | **Not the same clock** | Shot vs line | **DERIVED** / Timing V3 |
| Dialogue | Not in actingBeat | line `text` | No | No | VOICE snapshot Story+LineText | `voiceInputFingerprint` | speech window from Timing V3 | Line | **VOICE ONLY** |
| Reaction | `actingBeat.after` | `pauseSec` / aftertaste emotion | No | No | No | No | holds vs pause | Shot vs line | **DUPLICATED** |

FACT for sources. “Same *” columns are FACT (no shared id found).

---

## 8. DUPLICATION MAP

Do **not** treat every copy as a bug. FACT

| Copy of “emotion” (example) | Role |
|---|---|
| Story / beatText wording | Story WHAT |
| `inferActingDirection` result | **derived** draft |
| Locked `LinePerformance.emotion` | **authority** for that line (voice/still face) |
| `actingBeat.during.emotion` | **authority** for shot visual/motion if Director saved |
| `VisualSpec.emotion` / `VisualContract.performance` | **derived** still compile |
| `ShotProductionIntent.emotion` | **derived** from first line only |
| `actingTtsVoiceSettings` stability/style | **provider compilation** (HOW) |
| `actingPerformanceLine(...)` in Runway prompt | **provider compilation** (HOW) |
| OS `ActingBeat` TEXT | **frozen snapshot** of flatten, not full beat |
| OS `ActingHash` | **hash of that flatten** |
| `actingBeatFingerprint` | **hash of full JSON beat** (legacy motion freeze) |
| `FamixaCharacterState.emotion` | **story memory**, not execution |
| Scene `performance` string | **legacy** note |

FACT. The dangerous pair is **shot `during.emotion` vs line `performance.emotion`**: both are writable authorities, neither points at the other, and OS VIDEO hashes only the flatten of action+body. FACT

---

## 9. PRODUCTION OS SNAPSHOT TRACE

`ProductionTaskInputSnapshot` always has columns `story`, `acting_beat`, hashes, provider/model. FACT (`migrations/368`, `ProductionEntities.cs`)

What each action **requires** and what it **actually freezes as WHAT**:

| Action | Required WHAT (contract) | Frozen WHAT that matters | Acting / performance in snapshot? |
|---|---|---|---|
| CREATE_IMAGE | VisualContract, CharacterIdentity | `visual_contract`, `visual_hash` | **Not required** |
| CREATE_VIDEO | Story, ActingBeat, VisualContract, Timing, ApprovedKeyframe, FrozenProviderModel | story + acting_beat + visual + compiler_output + story/acting/visual/compiler/keyframe/timing/motion hashes | **Yes — flattened ActingBeat string + ActingHash** |
| CREATE_VOICE | LineText, VoiceId | `VoiceRequirements = lineId\|speaker\|voiceId\|Story` → `voice_hash` | **No ActingBeat, no emotion, no pace** |
| CREATE_LIPSYNC | MotionArtifactId, VoiceArtifactId, FrozenProviderModel | parent artifact IDs + byte SHA | **No performance fields** |
| MIX | MotionOrLipsyncArtifactId, Timing | lipsync/motion artifact id + timing | **No shared performance proof** |

FACT. Contracts: `ProductionActionContractRegistry`. Voice/lipsync hashes: `ProductionCreateVideoService.Actions.cs`.

### Can CREATE_VIDEO and CREATE_VOICE prove the same performance intent?

**No.** FACT

Missing boundary (exact):

1. CREATE_VOICE required inputs omit ActingBeat.  
2. CREATE_VOICE `VoiceHash` = SHA256(`lineId|speaker|voiceId|Story`) — Story text, not acting JSON.  
3. CREATE_VIDEO `ActingHash` = SHA256(flattened `before.action + before.body`).  
4. Client motion `actingBeatFingerprint` = FNV of **full** `ShotActingBeat` JSON — different algorithm and different payload than `ActingHash`.  
5. Client voice `voiceInputFingerprint` includes first-line `LinePerformance`, not `actingBeat`.  
6. No foreign key / id linking the two snapshots.

There is **no** shared identity or hash to show. FACT

---

## 10. HASH / IDENTITY MODEL

| Hash | Exists? | Inputs | Location |
|---|---|---|---|
| StoryHash | Yes (server) | SHA256(story string) | OS snapshot |
| ActingHash | Yes (server) | SHA256(acting **string**) | OS snapshot |
| actingBeatFingerprint | Yes (client) | FNV `productionHash(['actingBeat', stableJson(beat)])` | motion freeze / remake |
| VisualHash | Yes (server) | SHA256(visual contract string) | OS snapshot |
| TimingHash | Yes, **two** | Server: SHA256(Timing string). Client: FNV of provider/editorial/performance durations | **Not the same** |
| VoiceHash | Yes (server) | SHA256(VoiceRequirements) — action-specific | OS snapshot |
| voiceInputFingerprint | Yes (client) | shotId + line id/text/voiceId + first-line emotion/label | Director stale |
| promptHash / shotI2vPromptHash | Yes (client) | compiled I2V prompt | motion freeze |
| motionContentFingerprint | Yes (client) | includes `actingBeatFingerprint` | remake |
| lipsyncInputFingerprint | Yes (client) | voice fp + motion fp + performanceDuration | remake |
| SnapshotHash | Yes (server) | join of all snapshot hashes + provider + model + duration | OS |
| **PerformanceIntentId** | **ABSENT** | — | — |
| **ActingSnapshotId** | **ABSENT** | Closest: `SnapshotId` / `InputSnapshotId` per **task**, not per shared intent | OS |

FACT. Algorithms differ (SHA256 vs FNV `productionHash`). UNVERIFIED that any live VIDEO/VOICE pair shares a hash value — the inputs make equality impossible by construction.

---

## 11. MUTATION / INVALIDATION

### Client Director graph (IMPLEMENTED / TESTED in acting-beat smokes)

| Change | VIDEO | VOICE | LIPSYNC | MIX | Implementation |
|---|---|---|---|---|---|
| ACTING beat save | **STALE** (`actingBeatPendingMotion`, fingerprint ≠ frozen take) | **UNCHANGED** (voice fp does not include `actingBeat`) | **UNCHANGED** until motion remakes | **UNCHANGED** until lipsync remakes | `persistActingBeat`, `actingBeatWantsMotionRemake` |
| Line performance / dialogue text | Indirect: `motionInputFingerprint` uses first-line `acting` label | **STALE** (`voiceInputFingerprint`) | **STALE** (depends on voice fp) | **STALE** | `computeInputFingerprints` |
| STORY / shot.story | Motion prompt/hash change → **STALE**; saved beat **not** auto-rewritten | CREATE_VOICE Story is in VoiceRequirements — OS would **SUPERSEDE** if re-posted; client voice fp is line text, not shot.story | UNCHANGED until parents change | UNCHANGED | `applyTimingToShot` preserves beat; `Compile` includes Story |
| TIMING | `timingHashOf` change → motion/lipsync/mix **STALE** | Duration measured separately | `performanceDurationSec` in lipsync fp | mix fp | Timing V3 |

FACT. Labels used in code: `CURRENT`, `STALE`, `ACCEPTED_STALE`, `PENDING`, `FAILED`, `LATE_RESULT`. Not `UNBOUND` as a named enum. FACT

### Production OS (IMPLEMENTED)

`ClassifyInputDrift`: StoryHash → ActingHash → KeyframeHash → VoiceHash → MotionHash → Provider → Model. FACT  
Mismatch → prior task **SUPERSEDED** + `STORY_CHANGED` / `ACTING_CHANGED` / … (`NewTaskForChangedInputAsync`). FACT  
`INPUT_CHANGED` is illegal (`GenericInputChangedIsLegalState` = false). FACT

**If Acting changes, VIDEO and VOICE do not invalidate from the same dependency.** FACT

- VIDEO: `ActingHash` / `actingBeatFingerprint`.  
- VOICE: line text + voiceId (+ first-line performance on the client). Changing `actingBeat.during.emotion` does **not** change CREATE_VOICE `VoiceHash`. FACT

---

## 12. PROVIDER INDEPENDENCE

Acting **types** (`ShotActingBeat`, `LinePerformance`) have **no** Runway / Veo / Gemini / Fal / ElevenLabs / Wan fields. FACT

Provider names appear at **compile / HOW** time:

| Occurrence | Class |
|---|---|
| `compileI2vPrompt` → `compileRunwayPromptV1` | **HOW** (adapter compile) |
| `actingTtsVoiceSettings` → ElevenLabs `stability/style/speed` | **HOW** |
| Fal lipsync speaker region / frame | **HOW** |
| `RunwayMotionExecutionAdapter` prompt join | **ADAPTER** |
| `FrozenProviderModel` on CREATE_VIDEO / LIPSYNC | **HOW** (OS) |
| `motionLegacyExecutionFingerprintOf` mixing provider into content hash | **LEGACY** |

Target: Performance Contract must not know provider/model. Current shot/line types already do not; compilers do. FACT  
`ACTING_LAW_LOCK` and emotion enums are provider-agnostic. FACT

---

## 13. TIMING V3 RELATIONSHIP

Timing V3 (`DOCUMENT_ID = FAMIXA_SHOT_TIMING_CONTRACT_V3`) is a **duration contract**, not an acting contract. FACT

| Clock | Field | Authority | Link to Acting |
|---|---|---|---|
| performanceDuration | `timing.performanceDurationSec` / `performanceDurationOf` | Measured take, else stored, else provider 5\|10. **Voice is not authority.** | Not from emotion |
| editorialDuration | `productionDurationSec` when `source='DIRECTOR'` | Director trim | Assemble keep-in-film |
| dialogue / voice duration | `voiceDurationSec`, `voiceAssets[id].duration` | Measured WAV | `during.speech` set if voice > 0.2s in `deriveActingBeat` |
| lead-in / tail | `actingBeat.before.holdSec` / `after.holdSec` | Beat holds, else `SPEECH_LEAD_IN` / `SPEECH_TAIL` (0.2 / 0.22) | Acting **references** Timing constants |
| providerDuration | 5 \| 10 map | `mapProviderDuration` | HOW |

FACT. Future Performance Contract should **reference** Timing V3 ids/durations, not replace them. RECOMMENDED  
Do not redesign Timing V3. FACT (non-goal)

---

## 14. CONTRACT GRANULARITY

Evidence does **not** support a single film-level execution contract. FACT  
Evidence does **not** support scene-level as the execution authority (`scenes[].performance` is notes). FACT

What execution actually reads:

- Motion/I2V: **shot** `actingBeat`  
- TTS: **line** `LinePerformance` + character `voiceId`  
- Still face: **line** performance  
- LipSync speaker: **character-in-shot**  
- Timing holds: **shot** beat `holdSec`  
- OS CREATE_VIDEO: **shot** flattened string  

FACT

**RECOMMENDED ownership (not implemented):** a **composed hierarchy**

```
Shot Performance Contract
  ├── shot-level visual performance (action, body, gaze, room, shared emotion/intensity)
  ├── character-in-shot[] (who is performing)
  └── lines[] in dialogue order (text identity + delivery: emotion, pace, volume, pause)
```

Not film-only. Not line-only. Not “pick one existing field and rename it.” RECOMMENDED from repository evidence.

---

## 15. MINIMUM TARGET CONTRACT

**RECOMMENDED. Not implemented. No schema proposed as existing.**

Minimum shared WHAT so VIDEO and VOICE can prove one intent:

**ACTING WHAT (provider-agnostic, immutable after snapshot)**

- `shotCode`  
- ordered `characterIds`  
- visual performance: action, body, gaze, room, prop (optional)  
- shared emotion + intensity (one shot-level value, or explicit per-character if multi-speaker — evidence today is first-line vs beat, so the contract must name the rule)  
- ordered lines: `lineId`, `characterId`, spoken text identity (not TTS settings), delivery (emotion, pace, volume, pauseSec)  
- references: `timingHash` or Timing V3 fields by reference (do not copy provider 5\|10 into WHAT)  
- references: Visual Authority / Canon by id, not by prompt  

**VISUAL EXECUTION HOW**

- keyframe compiler, I2V prompt compiler, provider/model, duration map  

**VOICE EXECUTION HOW**

- `actingTtsVoiceSettings`, voiceId selection, ElevenLabs/Fal/etc.  

**LIPSYNC**

- continues to take **physical parent artifacts**; additionally **must bind** to the same Performance snapshot id so stale VIDEO+VOICE from different intents cannot mix without detection  

Must: provider/model agnostic; immutable after snapshot; shared by Motion and Voice; coexist with Timing V3, Canon / Visual Authority, Production OS (`InputSnapshot` can **reference** the contract hash instead of flattening action+body). RECOMMENDED

Separate from: FINAL, FILM READY, provider benchmark. FACT (non-goals)

---

## 16. MIGRATION IMPACT

Estimate only. No modifications.

| Component | Class |
|---|---|
| `ShotActingBeat` / Director card | **READ FROM NEW CONTRACT** (or become the visual slice of it) |
| `LinePerformance` / Voice Script | **READ FROM NEW CONTRACT** (line slice) |
| `deriveActingBeat` / `inferActingDirection` | **DERIVED** (draft only, as today) |
| `VisualSpec.performance` / KF pipeline | **DERIVED** from contract |
| `compileI2vPrompt` / Runway compile | **DERIVED** / HOW |
| `actingTtsVoiceSettings` | **DERIVED** / HOW |
| Production OS `acting_beat` TEXT flatten | **MIGRATE AUTHORITY** — freeze contract hash + structured payload, not action+body join |
| CREATE_VIDEO required ActingBeat | **READ FROM NEW CONTRACT** |
| CREATE_VOICE required LineText+VoiceId | **READ FROM NEW CONTRACT** (add performance snapshot id; keep LineText/VoiceId) |
| CREATE_LIPSYNC parent artifacts | **NO CHANGE** to artifact identity; **READ** contract id for stale proof |
| Timing V3 | **NO CHANGE** — reference only |
| Canon / Visual Authority | **NO CHANGE** |
| `FamixaCharacterState` story memory | **NO CHANGE** (different layer) |
| Scene `performance` strings | **LEGACY** / **REMOVE LATER** |
| Kit Video `KitVideoCharacterState` | **LEGACY** (other engine) |
| `actingBeatFingerprint` / `voiceInputFingerprint` | **DERIVED** until they hash the same contract |
| Fixture adapters | **NO CHANGE** (HOW) |
| Real providers | **NO CHANGE** in this contract phase |

RECOMMENDED classification. UNVERIFIED exact row counts on official EP01/EP99 graphs for every field.

---

## 17. RISKS

1. **Two writable emotions** (shot beat vs line performance) already diverge; a contract that “picks one silently” will look like a data bug. FACT + RECOMMENDED explicit rule.  
2. OS flatten already **drops** gaze/emotion; operators can believe VIDEO was frozen with the full Director card. FACT  
3. Changing Acting today **does not** stale VOICE — a shared contract will change that behavior; must be intentional. FACT  
4. Hash algorithms differ (SHA256 vs FNV). Merging without a new id will fake equality. FACT  
5. Multi-speaker shots: `shotProductionIntentOf` uses **first line only**. FACT  
6. Treating story-memory `FamixaCharacterState.emotion` as shot performance would mix film arc with take intent. FACT  
7. Compiling the contract into Runway/ElevenLabs strings is HOW; putting those strings back into the contract would violate provider independence. RECOMMENDED

---

## 18. UNVERIFIED

- Live official EP01/EP99 graph values for `actingBeat.during.emotion` vs each line `performance.emotion` (not queried; film left untouched).  
- Whether `visualContractHash` is always populated on every motion freeze.  
- Pronunciation / SSML anywhere in Famixa voice path (none found).  
- `KitVideoVisualContract.performance` (field absent).  
- Whether any production_task_input_snapshot on TEST has a non-empty `acting_beat` that includes gaze/emotion (UI flatten says no).  
- Character Design `BodyLanguage` consumption by shot I2V (look language, not traced as shot acting).  

---

## 19. RECOMMENDED IMPLEMENTATION PHASE

**RECOMMENDED. Not started. No code in this phase.**

1. Specify the composed Shot Performance Contract (WHAT only) and its snapshot hash.  
2. Define the single rule for emotion/intensity when shot beat ≠ line performance.  
3. Point CREATE_VIDEO and CREATE_VOICE snapshots at that hash.  
4. Keep Timing V3, Canon, Visual Authority, Production OS Request/Task/Attempt.  
5. Keep LipSync on physical parents; add contract-hash stale proof.  
6. Do **not** start Runway / Veo / Gemini / Fal / ElevenLabs / FINAL / FILM READY.

STOP.
