# FAMIXA — Production Standard and Pilot Plan V1

**Date:** 2026-10-17  
**Status:** PLAN ONLY. Awaiting product approval. No implementation is authorized by this file.  
**Owner:** Famixa product. Cursor executes only a package named in §10 after that package is approved.  
**This document is:** the product plan for one publishable Famixa episode, and the failure-to-module map for that work.  
**This document is not:** KIT Platform architecture. It does not decide an open ADR, does not promote Famixa types into Core, and does not replace `docs/architecture/INDEX.md`.

Repository observation at the time of writing, read-only: branch `main`, commit `8c59ceb4`, ahead of `origin/main` by 6 and behind by 142. The working tree already contains Famixa production changes, including untracked server types named in `FAMIXA_VIDEO_PIPELINE_CHANGE_IMPACT_ROOT_CAUSE_V1_REPORT.md`. This plan adds only this file. Later packages must edit in place and must not revert, stash, or overwrite unrelated work.

---

## Authority

| Question | Start here |
|---|---|
| What Famixa may own | `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` §8, §23 |
| Which decisions are closed | `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` |
| How to route a platform question | `docs/architecture/INDEX.md` §5 |
| What a pack entry must declare | `docs/architecture/GOVERNANCE.md` §10 |
| What the official chain must keep true | `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_E2E_CONTRACT_V1_REPORT.md` |
| Why EP01 is not accepted | `docs/novixa/03-solution/FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md` |
| Why the next SH02 picture cannot be requested yet | `docs/novixa/03-solution/FAMIXA_VIDEO_PIPELINE_CHANGE_IMPACT_ROOT_CAUSE_V1_REPORT.md` |
| What a shot verdict can and cannot prove | `docs/novixa/03-solution/FAMIXA_PRODUCTION_OUTCOME_ENGINEERING_V1_REPORT.md` |
| Why one controlled attempt is NO-GO today | `docs/novixa/03-solution/FAMIXA_FIRST_REAL_SHOT_VALIDATION_READINESS_V1_REPORT.md` |

Product reports are evidence. When they disagree with the Master Architecture or the Decision Register, the architecture document wins, and the conflict stops the related code change until the decision owner answers.

There is no second Famixa index. Implementation history stays the dated reports in `docs/novixa/03-solution/FAMIXA_*.md` plus the forensics named by the architecture index. This plan is the entry that points at them.

### Pack declaration (Governance §10, scoped to this pilot)

| Field | Statement |
|---|---|
| Owner | Famixa product, inside the Content pack |
| Architectural authority | Master Architecture §8. Execution stays Famixa Production OS. Not a Production Engine |
| Purpose | Produce one human-accepted animated episode and a file whose hash is the master |
| Inside | Series script, character canon, shot performance, Production OS artifacts and bindings, human review of those artifacts |
| Outside | KIT Core, Pharmacy, Family OS, Local OS, a generic workflow engine, a mesh/rig 3D application |
| Data | `pack_content` only. Migrations, when later approved, go through `deploy/ubuntu/migration-files.content.txt` only |
| Public surface | `api/content/production` on `ContentProductionController` |
| Current state | §3 and §5 |
| Next work | §6 and §10. Not started |

---

## 1. Product goal and final acceptance

The goal is one complete Famixa episode that a person accepts as a coherent animated short, with a master file that can be identified by artifact id and SHA-256.

"3D animated film" in this repository means stylized characters carried by approved stills and image-to-video. It does not mean a mesh, a rig, or a DCC scene. No Blender, Unreal, or equivalent pipeline is present. A later demand for mesh-level consistency is a new production method and needs its own architecture decision. It is not hidden inside this pilot.

### Final acceptance (all of these)

1. The locked script, the shot intents, and the spoken lines are the ones a person approved. The system does not invent missing words, entrances, or resolutions.
2. Every required shot has a playback artifact. A person has watched that file and recorded ACCEPT against that artifact id and that SHA-256, with attestations for action, continuity with the neighbor, motion, and, on a speaking shot, mouth on the speech interval and stillness on the intended silence.
3. Character, wardrobe, room, and required props hold across the episode in that person's judgment. A verified hash is not that judgment.
4. Picture duration, speech duration, lip-sync duration, and mix placement are separate clocks and pass the existing 0.75 second rule. `AutomatedVisualProof` stays false. Technical validity is not visual acceptance. An `ACCEPTED:MIX` binding is a byte lock, not the quality decision.
5. The episode output is one ASSEMBLY artifact of the accepted shots in order. A second person, or the same person in a separate act, accepts that assembly id and hash. Concat of the files is not that act.
6. The master is the bytes of that assembly. Size and SHA-256 are recorded. Legacy `POST api/content/series/assemble` remains export-only and does not create this master.
7. Music and effects are content QC. A procedural bed or silence can be accepted. A score library is not a gate for the master.

### What the system may check, and what a person must judge

| Check | Who | A pass means |
|---|---|---|
| Playback id, SHA-256, tenant ownership of the key | System | The bytes shown are the selected artifact, and another tenant cannot read the key |
| Task success, mime, positive duration, shot identity | System | The file is a candidate |
| Future request uses the approved acting, not a historical fragment | System | The next job is not a replay of a known-bad sentence |
| Spoken line complete, stage cue stripped | System | Voice may be drafted. SH03 stays refused while the locked line is truncated |
| Clocks within 0.75s | System | Durations and, when present, the director speech window agree. A missing window is not filled in |
| Review names this id and this hash | System | The decision is about the file that played |
| Action, props, identity, wardrobe, room, motion, mouth | Person | Someone saw the picture |
| Story sense, pace, episode assembly | Person | Someone watched the cut |

---

## 2. Chosen method, and the methods set aside

### Chosen

**Controlled stage production on Famixa Production OS.**

One approved keyframe still, then image-to-video, then a separate voice, then lip-sync of that voice onto that picture, then a mix that places the speech on a director-authored interval, then a human decision on the file that plays. Episode concat runs only after every shot decision is ACCEPT. Official motion for this pilot stays `google` / `veo-3.1`, because that is the provider and model already selected for official SH02 and because the server price table marks video-only 720p as Known.

The picture is allowed to be longer than the line. The line occupies a speech interval a person locks (`source = DIRECTOR`, with `speechStartSec` and `speechEndSec`). Lip-sync covers that interval. The rest of the picture is intentional silence, and the mouth must be still there. This is the branch `productionClockBlockers` already implements when a director window exists. It is how a short line can live in a Veo picture of 4, 6, or 8 seconds without weakening the 0.75 second rule.

Why this method:

- Each stage has its own artifact. A bad mouth can be redone without a new picture. A bad picture can be redone without rewriting the script.
- The quality the mission cares about (acting, identity, mouth, continuity) is already defined as a human judgment. Maximizing automation would hide that judgment.
- The chain, the tenant fence, the permit, and the hash check already exist in the Content pack. A new engine is neither allowed nor needed to find out whether the pictures can be accepted.
- Cost stays per stage and per attempt, behind an issued permit.

### What the director UI does today, and what it does not do

`famixa-director-timing.ts` locks a picture length of 5 or 10 seconds (`EXPLICIT_PRODUCTION_DECISION`). That is not a speech window.

`approvedDialogueIntervals` in `famixa-short-quality-pipeline.ts` returns a window only when `source` is `DIRECTOR` and `speechEndSec` is after `speechStartSec`. A computed 0.2 second lead-in is not that window. SH02 has no such lock. With no window, speech must cover the picture within 0.75 seconds. The measured line is about 2.5 seconds. Veo can emit 4, 6, or 8 (`VeoMotionDurationMapping.ToProviderDurationSec`). None of those lengths is within 0.75 seconds of 2.5. Runway and Wan durations of 5 and 10 fail the same comparison.

So the pilot is blocked on a product act, not on a missing video vendor: a person must be allowed to author the speech interval, and the mix must place speech on that interval. The numbers are written by the person. Code must not invent them.

### Set aside

| Method | Why it is not the pilot |
|---|---|
| Weaken 0.75s so an 8 second picture passes against a 2.5 second line with no window | The current SH02 mix fails in exactly that shape: open mouth on the silent tail. The tolerance is the detector. Removing it does not improve the picture |
| Require the picture length to equal the line | No registered model emits a picture within 0.75 seconds of 2.534 seconds. Veo minimum in the desk map is 4. Runway minimum is 5 |
| Switch the default to Runway `gen4_turbo` or Wan `wan-2.1` | Both are registered and remain available per shot. Neither solves the clock, and neither has a Known USD price in the server table. Runway is an estimate in credits. Wan is a Fal-billed estimate with a null amount |
| One-shot video (Kling, Luma, Sora, and anything else not in the catalog) | Not in `FamixaProviderCatalog`. A single generation cannot be repaired per stage. Adding a provider is a Famixa catalog change, not this pilot |
| A mesh/rig 3D pipeline | Not in the repository. Building one would be a new production system beside Production OS. Master Architecture §23 forbids creating that system by renaming Famixa types, and this plan does not request it |
| Promote Production OS, artifacts, bindings, or the provider registry into Core | Forbidden by Master Architecture §6.3 and §23, and by ADR-05 (execution stays product-specific) |
| Keep generating on the official SH02 rows as they stand | Those rows are unattributed (`tenant_id` NULL) and the storage key is not tenant-prefixed. The tenant store will not serve the file. Backfill is a data change and is not part of the pilot until separately approved |

### If the chosen method fails the quality bar

Stop after the evidence in milestone M3. Do not add another prompt layer, another provider, or another acceptance flag. Return here with the rejected frames and ask for a method change. The likely change, if identity cannot be held by stills plus image-to-video, is a new production method outside this plan.

---

## 3. Capability map

Status words mean only what was verified for this plan.

- **OPERATING** — the code path exists and a 2026-10-17 report or a named regression shows it doing the job on isolated or official-shaped data. This is not a visual accept.
- **PARTIAL** — the path exists and a named gap still blocks the film.
- **ABSENT** — no owning implementation was found.

| Stage | Status | Evidence | What blocks a finished film |
|---|---|---|---|
| Story and script lock | OPERATING | `ContentFamixaScriptDesk.tsx`, series build `graph_json`. ADR-01: graph is the script document | A person must finish truncated lines. SH03 is still incomplete |
| Series, scene, shot graph | OPERATING | `content-famixa-series.ts`, `ContentFamixaSeriesTab.tsx` | Graph take URLs are residual (ADR-06 OPEN). They must not be treated as the playback |
| Character canon, DNA, wardrobe | PARTIAL | `content-famixa-char-canon.ts`, Minh DNA chain in the Content pack rule, reference stills | Consistency is reference-image and prompt, not a rig. Shot-to-shot drift is a product risk, proven by the earlier SH01 duplicate-Linh note |
| Acting beat and performance snapshot | OPERATING | `PerformanceContractService`, `performance_snapshot`, `ShotProductionActingBeat.ts` | The snapshot freezes the request. It does not prove the pixels match the beat |
| Director picture duration | PARTIAL | `famixa-director-timing.ts` offers 5 or 10 seconds | Does not store `speechStartSec` / `speechEndSec` |
| Dialogue window | PARTIAL | `approvedDialogueIntervals`; server `DialogueInterval` | The checker exists. SH02 has no director window. The mix-placement proof for a short line inside a longer picture was not established |
| Keyframe still | PARTIAL | OS `CREATE_IMAGE` and the Kit Video still chain. SH02 keyframe id `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` was hashed on 2026-10-17 | The still is a JPEG stored under an `.mp4` name. It was not visually accepted in that pass |
| Video generation | PARTIAL | OS `CREATE_VIDEO`, `VeoMotionExecutionAdapter`, `RunwayMotionExecutionAdapter`, permit and freeze | Official SH02 attempt is NO-GO: no window, no new permit, current file not accepted. Desk refuses the post |
| Voice | PARTIAL | OS `CREATE_VOICE`, `ElevenLabsVoiceExecutionAdapter`. Legacy `POST api/content/series/tts` still exists | ElevenLabs price is Unknown. SH03 voice stays refused |
| Lip-sync | PARTIAL | OS `CREATE_LIPSYNC`, Fal `sync-lipsync-v3`. Legacy `POST api/content/series/lipsync` still exists | Current SH02 lip-sync is 2.5 seconds on an 8 second picture and was not accepted |
| Mix | PARTIAL | OS `MIX_VIDEO`, FFmpeg. Procedural beds in the mix helpers | Must place speech on the director window. That placement is a verification item, not a claimed result |
| Shot assembly | OPERATING as a remux | `AssembleOsAsync`, role ASSEMBLY, members are MIX | Not a quality accept |
| Episode assembly | PARTIAL | `AssembleEpisodeOsAsync` is called from `ContentProductionController` (line 417 in the current tree). Migration file `migrations/380_pack_content_production_assemble_episode_action.sql` exists. ADR-08 caller policy stays OPEN and the action is not Core | No six-shot accept exists, so concat is not permitted. `FINAL` binding is unused. Do not enable new callers until the product letter in §9 is answered |
| Human QC record | PARTIAL | Desk buttons in `ShotProductionQa.tsx` call `explicitArtifactDecision`. Server `ShotHumanReview` is an argument of `ShotProductionOutcomeV1.ReadAsync`, not a stored row | The official SH02 preview does not resolve, so the card cannot record the decision. A graph `shotQa` write is residual product state (ADR-06), not OS authority |
| Technical outcome | OPERATING on isolated data | `ShotProductionOutcomeV1` | Official SH02 playback through the tenant store is BLOCKED |
| Publishable master | ABSENT as an OS act | `ProductionLegacyExportAuthorityV1` marks series assemble as export-only | The master has to be the accepted episode ASSEMBLY bytes. That act has no separate publish status |

Official API prefix: `api/content/production`. Schema: `pack_content`. Tenant column and filter exist in the current tree (`migrations/376_pack_content_production_tenant_boundary.sql`, `ProductionTenantActionFilter`). ADR-03 remains the authority: tenant-only scope is DECIDED, and a decision is not proof that historical rows are attributed. The 2026-10-17 reports show the official EP01 build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` still has NULL tenant on the mix and its bindings.

### EP01 as of the 2026-10-17 quality matrix

Build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`. No shot is production-accepted. The episode is blocked.

| Shot | In Production OS | Known visual note | Accept |
|---|---|---|---|
| SH01 | Mix `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` | Entrance not performed. Linh duplicated. Acting entrant contradicted the story | No |
| SH02 | Mix `58297402-e252-45e8-8d33-c7414749a297`, SHA-256 `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`, 1423715 bytes | Red score absent. Open mouth on the silent tail. Speech about 2.5s, picture 8s | No |
| SH03 | Legacy file, not an OS task | Held pose. Blank paper. Locked line truncated | No |
| SH04 | Legacy file, not an OS task | Held pose. Blank card. Action sentence incomplete | No |
| SH05 | Legacy file, not an OS task | Already seated | No |
| SH06 | Legacy URL | Pixels not fetched (HTTP 401 on the earlier review) | No |

Those visual notes are prior observations cited by the quality report. This plan did not watch the files and does not convert the notes into ACCEPT or REJECT.

---

## 4. Module map and failure routing

Use this table first. Open the architecture index when the question is about a platform boundary. Search the wider repository only when the row cannot name the owner, then add the owner to this table in the same change.

```text
Failure
  → row in this section
  → owning source
  → contract or report named on the row
  → listed tests
  → one end-to-end check of the shot or the episode
  → a dated note in the FAMIXA report series, not a new index
```

| Failure | Owning module | Contract / history | Consumers that move with it | Tests to run |
|---|---|---|---|---|
| Wrong or stale acting on the next video request | `ShortProductionQualityPipelineV1.cs`, `famixa-short-quality-pipeline.ts` | E2E contract report §2 CREATE_VIDEO | Series desk, create-video freeze | `ShortProductionQualityPipelineV1Regression`, `famixa-short-quality-pipeline.smoke.ts` |
| Speech, picture, and lip-sync collapsed into one clock | `famixa-short-quality-pipeline.ts` `productionClockBlockers`; server pipeline of the same name | E2E contract RC2 | Lip-sync CTA, mix CTA, outcome reader | same smoke; `ShotProductionOutcomeV1Regression` |
| Preview plays a take instead of the mix | `OfficialVideoAuthorityBridgeV1` | E2E contract RC3. Order: verified CURRENT:MIX, CURRENT:LIPSYNC, ACCEPTED:VIDEO_TAKE | `ShotProductionPreview.tsx`, Series tab hydrate | `ProductionOfficialVideoAuthorityBridgeV1Regression`, `famixa-shot-state-preview-authority.smoke.ts` |
| ACCEPT sticks to an old file | `famixa-short-acceptance.ts` `explicitArtifactDecision` | E2E contract RC4. Outcome report §4 | `ShotProductionQa.tsx`, `ShotProductionWorkspace.tsx` | `famixa-short-acceptance.smoke.ts` |
| Official file will not play through the API | `ProductionTenantBoundary`, `ProductionArtifactFileStore` | Readiness report §1. ADR-03 | Preview GET | Do not point the live tests at NULL-tenant rows. Isolated coverage: `ShotProductionOutcomeV1Regression` |
| Bytes under a key no longer match the row | `OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes`, called from `ProductionCreateVideoService` preview and voice reads | ADR-07 letter was not voted. The current tree does rehash on that path | Preview and voice media | `OfficialPreviewIntegrityV1Regression` |
| Director locked 5s or 10s and the speech window is still empty | `famixa-director-timing.ts` versus `approvedDialogueIntervals` | This plan §2 | Timing picker, generation gate | `famixa-director-timing-ui.smoke.ts`, `famixa-short-quality-pipeline.smoke.ts` |
| Voice text is wrong or SH03 is sent anyway | `voiceDraftForRequest`, `PerformanceContractRules.SpokenText` | Quality pipeline report | Create-voice cutover | `content-famixa-voice-action.smoke.ts`, official SH02 voice regression |
| Lip-sync uses the wrong parents or a zero duration | `famixa-lipsync-execution-input-freeze.ts`, Series tab CTA | Readiness report §3 | Fal adapter | `famixa-create-lipsync-input-cutover.smoke.ts`, `famixa-lipsync-execution-input-freeze.smoke.ts` |
| Mix or episode concat claims a master | `ProductionLegacyExportAuthorityV1`, `AssembleEpisodeOsAsync` | ADR-08 OPEN. Legacy export report behavior | Preview cut card, Series tab "Lắp tập phim" | `ProductionLegacyExportAuthorityV1Regression`, `ProductionEpisodeAssemblyV1Regression` |
| Character drift | Canon and reference stills: `content-famixa-char-canon.ts`, Minh DNA / reference-pack services | Content pack rule, character studio smokes | Keyframe prompt, I2V still | `content-famixa-char-canon.smoke.ts`, `famixa-character-studio.smoke.ts`. Pixels remain a human check |
| A second writer creates a take the OS does not know | `ContentFamixaSeriesTab` graph persist; legacy turbo and series TTS/lipsync | ADR-01 split, ADR-02 surface not retired, ADR-06 OPEN | Desk recovery `ACCEPT_EXISTING` | `famixa-accept-existing-authority.smoke.ts`, `famixa-legacy-export-authority.smoke.ts` |

UI entry for a Famixa production failure: `/content/videos` → Series Famixa → `ContentFamixaSeriesTab.tsx` → the stage owner above. `ContentLayout` does not host this chain.

Parallel surfaces that must stay out of the official accept path:

- Kit Video Engine tables (`video_generation_execution` and the Phase 01–06 contracts). ADR-02: official execution is Production OS. The legacy surface is not retired and must not be a second accept.
- `IFamixaProviderRegistry` on `POST api/content/series/turbo` and series lipsync/tts.
- IndexedDB `kit-famixa-tts`, `kit-famixa-kf`, `kit-famixa-shot-final`. Cache and recovery only.

---

## 5. Gap analysis

### Proven, and left in place

- Production OS is the execution authority. The graph is the script. Take URLs in the graph are residual.
- The outcome reader can separate a hash match, a clock failure, a review of a different file, and visual acceptance. Visual acceptance from that reader stays false.
- Isolated tenant data can reach a technically ready shot. That shot is not EP01 and the bytes are not a film.
- Official SH02 bytes on disk match the recorded size and SHA-256. The picture has not been accepted.
- Preview rehash exists on the current `ProductionCreateVideoService` path. ADR-07 still has no voted letter. Do not describe the rehash as a new Core store.

### Proven gaps that can sink the film

1. **No legal picture length for a short line** until a person authors a speech window. This blocks every speaking shot whose line is shorter than the model minimum by more than 0.75 seconds. SH02 is the instance already measured.
2. **Human review is not an OS record.** The card cannot review the official mix, because tenant preview does not resolve it. Graph `shotQa` is not the authority ADR-01 assigned to execution state.
3. **Official EP01 rows are unattributed.** Any live read, permit, or regenerate on that build either fails closed or requires a tenant backfill. Backfill is out of scope until approved.
4. **Four of six shots are outside Production OS.** SH03's line is truncated. SH06's pixels were not fetched. Episode concat of one or two files is not an episode.
5. **Identity across shots is not proven.** The method can only hold identity as well as the stills and the video model hold it. SH01's duplicated Linh is the existing warning.

### Not proven, and not to be assumed

- That FFmpeg mix can place a 2.5 second lip-sync at an arbitrary `speechStartSec` and report mix speech spans. The clock requires those spans once a window exists. This is the first verification inside package P1.
- Word-level match of the audio to the line. No ASR check was found as a gate.
- A browser session of the official file through the admin preview. The readiness report did not open one.
- Account balances and live vendor invoices. Catalog figures in §9 are not balances.
- That the working-tree OS types match what is deployed, or what `origin/main` contains. This plan's code citations are the local tree.

### Hypotheses, labeled as such

- A director window plus mix placement will make Veo 4/6/8 usable for SH02 without a new model. Plausible because the clock's window branch exists. Unproven until a mix span is produced and a person watches the mouth.
- Locked reference stills will keep Minh and Linh stable across six shots. Not supported by an accepted pair of neighboring shots. M3 is the test. Failure there ends the method, it does not start a prompt loop.

---

## 6. Milestones

Each milestone is a capability. A green unit test is not the milestone.

Work inside an approved milestone continues across ordinary defects. Stop and return to this plan when the evidence says the method cannot meet the milestone, when an ADR would have to change, when data or a paid call is required and was not in the approval, or when the edit would overwrite existing uncommitted work.

### M0 — Plan decision

- **Input:** this file.
- **Output:** an approval or a written adjustment of §2, §9, and §10.
- **Accept:** the product owner names the method, the speech-window rule, and whether P1 may start.
- **Reject:** a request to weaken 0.75 seconds, to add a Core type, or to treat a test pass as a finished film.

### M1 — A speaking shot is legal to generate

- **Input:** M0 approval of director-authored speech intervals. No provider call.
- **Scope:** Famixa timing and the existing clock. Veo 4/6/8 map stays. Tolerance stays 0.75 seconds.
- **Output:** a director can store `speechStartSec` and `speechEndSec` on the shot; the generation gate stays closed when they are absent; when they are present, the clock checks the window and the mix spans, not the whole picture. Evidence that the mix either emits those spans or cannot, stated as a result.
- **Accept:** a fixture shot with a director window and a covering mix span has no clock blocker, and the same shot with no window still returns `SPEECH_DOES_NOT_COVER_PLAYBACK` for an 8 second picture and a 2.5 second line.
- **Reject:** the gate opens because a lead-in was computed, or because the picture duration was set to 5 or 10.

### M2 — One shot a person accepts

- **Input:** M1 accepted. A tenant-owned build. An issued permit. A person available to watch.
- **Scope:** one shot copied from the approved SH02 contract onto that build. Do not update the NULL-tenant official rows.
- **Output:** keyframe, video, voice, lip-sync, and mix, each bound in OS. The person watches the mix and records ACCEPT or REJECT on that id and hash.
- **Accept:** ACCEPT, clocks clean, required action and the red-score prop attested, mouth attested on the speech interval and still outside it, hash equal to the played bytes.
- **Reject:** any missed action, wrong identity, mouth on the silence, or a review of a different hash. The attempt stops. A retry is a new permit, not an automatic one.

### M3 — The next shot matches the first

- **Input:** M2 ACCEPT.
- **Output:** a neighboring shot accepted by the same person against the M2 frames (face, wardrobe, room, props, screen direction).
- **Accept:** both reviews name their own files, and the person records continuity.
- **Reject:** identity drift the stills cannot correct after the agreed retake count (default 3 video attempts, then stop). Method returns to §2.

### M4 — The episode's shots are in OS and accepted

- **Input:** M3 ACCEPT. A person completes the SH03 line in the script. SH06's picture is actually fetched and watched.
- **Output:** SH01–SH06 each have an OS playback and an ACCEPT on that playback.
- **Accept:** `ClassifyEpisode` no longer reports a missing shot accept. The assembly review is still a separate act.
- **Reject:** a legacy Fal URL or a graph QA tick counted as the playback.

### M5 — Master

- **Input:** M4. The ADR-08 product letter in §9 answered as "Famixa callers may assemble an episode; the action stays out of Core."
- **Output:** one episode ASSEMBLY whose own review is ACCEPT, plus recorded size and SHA-256 of those bytes.
- **Accept:** the file plays the six accepted shots in order, and the hash matches.
- **Reject:** a per-shot assembly, a legacy series export, or an unused `FINAL` binding presented as the master.

### M6 — The cost of a real accept

- **Input:** at least one M2 accept and its permits.
- **Output:** actual provider charges, attempts per accept, and the manual time, written back into §9. Catalog numbers stay labeled as catalog numbers.
- **Accept:** the next episode can be estimated from those charges.
- **Reject:** a new catalog lookup presented as the measured cost.

---

## 7. Dependencies, in the order that avoids another repair loop

```text
Script line complete
  → director speech window          (M1, product-authored)
  → mix can place that window       (M1 verification, before any paid call)
  → tenant-owned build              (not a backfill of official rows)
  → issued permit                   (one action, one provider, one model)
  → still → video → voice → lipsync → mix
  → human ACCEPT on that mix        (M2)
  → neighbor shot ACCEPT            (M3)
  → remaining shots                 (M4)
  → episode ASSEMBLY + its ACCEPT   (M5, needs the ADR-08 letter)
```

Doing a later box first recreates the current loop: a file exists, the clock rejects it, the preview cannot show it, and the accept has nowhere legal to live.

| If this is skipped | What breaks later |
|---|---|
| Speech window | Every Veo, Runway, and Wan duration fails the undeclared-window clock. Paid calls buy files that cannot pass |
| Mix placement proof | The window branch then fails `DIALOGUE_WINDOW_MISMATCH` because spans are missing. Lip-sync spend is wasted |
| Tenant-owned build | Official preview and live mix tests throw `PRODUCTION_TENANT_REQUIRED` or refuse the key |
| Separate human review from `ACCEPTED:*` | Identity bindings get used as quality decisions, which the outcome reader already rejects |
| M3 before more shots | Six shots can be technically valid and still be six different faces |
| ADR-08 letter before episode concat | A callable concat becomes a product policy change made inside a task |

Contracts that later packages may touch, all Famixa-local:

- Director timing shape: add speech interval fields beside the existing picture duration. Do not replace `EXPLICIT_PRODUCTION_DECISION`.
- `ShotHumanReview`: persist it only under the migration approval in §9. Do not overload `ProductionBinding` ACCEPTED.
- Mix contract: record speech spans on the mix artifact metadata the clock already reads. Do not change FFmpeg into a second editor.
- Episode: keep output role ASSEMBLY. Do not start writing `FINAL`.

Consumers to retest when those contracts move: Series tab OS actions, `ShotProductionWorkspace`, preview authority, outcome reader, episode assembly regression. Pharmacy, Family OS, and Local OS stay out of the test set.

---

## 8. Quality strategy

The pilot optimizes for a person being able to reject a shot without regenerating the episode.

1. Lock the script and the acting before any image spend.
2. Accept the keyframe still before video. A wrong still is cheaper than a wrong take.
3. Generate video with the spoken line absent from the picture prompt (current Veo prompt rule).
4. Generate voice from the locked line only.
5. Lip-sync only after the window and the voice duration agree within 0.75 seconds.
6. Mix places that lip-sync on the window and leaves the rest silent.
7. The person watches that mix, in a player when the tenant preview cannot serve it, and writes ACCEPT or REJECT against the id and the hash.
8. Continuity is judged on the next shot, not by a score in the prompt.
9. The episode is judged on the assembly file, after the shots.

Retake rule: three video attempts for one shot, each with a new permit and a written reason the previous file failed. After the third REJECT for the same identity or action miss, stop the shot and return to §2. Do not raise the attempt count inside the task.

QC of music, pace, and story is the same human pass as the assembly review. The procedural mix bed can be switched off. Silence is a valid accept.

---

## 9. Cost and the approvals required before anything is spent

Prices below were read from the repository on 2026-10-17. They are not invoices and not account balances.

| Item | Figure | Kind | Source |
|---|---|---|---|
| `google` / `veo-3.1` video-only 720p or 1080p, one count | 0.20 USD | Known | `FamixaVeo31OfficialPricing`, retrieved 2026-09-22. Duration is not a multiplier |
| Same, video+audio 720p/1080p, or video-only 4K | 0.40 USD | Known | same table. This pilot does not use these rows |
| `fal` lipsync `v3` | 8 USD per minute, 5 second minimum, so a 2.5 second output is quoted as 0.67 USD | Estimate | `FamixaProviderCostCatalog` |
| `fal` `1.9` | 0.70 USD per minute | Estimate | same |
| `elevenlabs` / `eleven_v3` | no amount | Unknown | client and server catalogs |
| Gemini still / keyframe | no amount confirmed for this plan | Unknown | not quoted here |
| Runway `gen4_turbo` | 5 credits per second, billed 5 or 10 seconds (25 or 50 credits) | Estimate | server catalog. No USD conversion in that table. Do not invent one |
| Wan `wan-2.1` | amount null | Fal-billed estimate | server catalog |
| FFmpeg mix and concat | 0 provider USD | Local compute | adapters are not paid transports |

Scenario arithmetic, not a budget: one SH02-shaped attempt at the Known video row plus the Fal estimate is 0.20 + 0.67 USD, voice unknown, still unknown. Three video attempts and two lip-sync attempts are 3 × 0.20 + 2 × 0.67 = 1.94 USD plus unknown voice and still. Six shots at three video attempts and one lip-sync each are 6 × 3 × 0.20 + 6 × 0.67 = 7.62 USD plus unknown voice and stills. Real M6 numbers replace this paragraph. They do not replace it before a permit is consumed.

### Approvals required

| Id | Decision | Needed before | Default if unanswered |
|---|---|---|---|
| D1 | Adopt §2 as the pilot method | Any code in P1 | No code |
| D2 | A director may author `speechStartSec` / `speechEndSec` even though the script text has no timecodes. The person writes the numbers. Code must not | M1 | Generation stays NO-GO |
| D3 | Package P1 may edit the Famixa files listed in §10 and may not touch other dirty files | P1 | No code |
| D4 | A Famixa review record may be added in `pack_content` through `migration-files.content.txt`, without applying it to Production and without a Core type | The persistence half of P1 | Review stays a written note plus residual graph `shotQa`, labeled as not OS authority |
| D5 | One tenant-owned pilot build may be created for M2. Official NULL-tenant rows are not backfilled | M2 | No live media on the official build |
| D6 | One paid attempt: Veo video-only 720p, one ElevenLabs voice, one Fal v3 lip-sync, on that pilot build, after M1 is accepted | M2 spend | No provider call |
| D7 | ADR-08 letter: Famixa episode assembly may be called for an accepted six-shot episode and stays out of Core | M5 | No episode concat |
| D8 | Who watches, and the retake cap of 3 | M2 | No ACCEPT is valid |

D1–D3 are the gate for the next package. D4 can be deferred inside P1. D5–D8 are later gates. None of them is granted by this file.

---

## 10. Next package, after D1, D2, and D3

**Package P1 — Make the speech window real, and prove whether the mix can place it.**

No provider call. No permit. No new media. No migration apply. No tenant backfill. No deploy. No git commit unless a later message asks for one.

### Verify first, then edit only the gap

1. Trace a director speech interval from the timing API into `approvedDialogueIntervals` and into `ShotProductionOutcomeV1`. Record whether `speechStartSec` and `speechEndSec` are stored anywhere today. The picture-duration picker is not that store.
2. Trace a mix artifact into the spans `productionClockBlockers` reads. Record whether a short lip-sync on a longer picture produces a covering span or an empty span.
3. Edit only if step 1 or step 2 is missing. The edit stays inside Famixa:
   - Client timing: `client/admin/src/modules/content/famixa-director-timing.ts`, `FamixaDirectorTimingPicker.tsx`, and the Series tab call that posts director timing.
   - Desk clock: `client/admin/src/modules/content/famixa-short-quality-pipeline.ts`.
   - Server clock and outcome: `ShortProductionQualityPipelineV1.cs`, `ShotProductionOutcomeV1.cs`, `ProductionDirectorTiming.cs`.
   - Mix span output, only if step 2 shows the span is absent: the FFmpeg mix contract types already used by `MIX_VIDEO`.
4. If D4 is also approved, add the review record in a new content-pack migration and read it from `ShotProductionOutcomeV1.ReadAsync`. If D4 is not approved, skip this step and say so in the P1 report.
5. Do not change `VeoMotionDurationMapping`, the 0.75 second constant, `ProductionBinding` roles, episode `FINAL`, or any Pharmacy / Family / Local file.

### Tests

- `famixa-short-quality-pipeline.smoke.ts` — no window still fails the 8s / 2.5s case; a director window checks the window; a 0.2s lead-in is still not a window.
- `famixa-director-timing-ui.smoke.ts` — 5 and 10 second picture locks still work, and they still do not create a speech window by themselves.
- `ShotProductionOutcomeV1Regression` and `ShortProductionQualityPipelineV1Regression` — same clock facts on the server.
- Mix regression already covering `MIX_VIDEO`, extended only if the span output changes.
- `famixa-short-acceptance.smoke.ts` — checkboxes still do not write ACCEPT.

### Accept P1

- The verification note states, with paths, whether the interval is stored and whether the mix emits spans.
- Any code change is limited to the gap found.
- The SH02 official file is untouched: same id, same hash, no ACCEPT written.
- Provider cost of the package is 0.

### Reject P1

- A window appears without a person having a place to type both ends.
- The undeclared-window SH02 case starts passing.
- A file outside the list above is rewritten to make a test pass.

### What P1 does not do

P1 does not accept SH02, does not generate the next file, and does not assemble EP01. Those wait for M2 and for D5–D8.

The report for P1 belongs in `docs/novixa/03-solution/` under the existing FAMIXA report naming, and it links back to this plan. It does not add an index.

---

## What this planning pass did not do

No code was edited. No contract was edited. No migration was applied. No provider was called. No media file was created. No Production row was written. The official SH02 mix was not opened and was not accepted. Git was read (`status`, branch, latest commit) and was not otherwise used.
