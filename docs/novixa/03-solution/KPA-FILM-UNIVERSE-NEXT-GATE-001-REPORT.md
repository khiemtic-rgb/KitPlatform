# KPA-FILM-UNIVERSE-NEXT-GATE-001

**Date:** 2026-10-17  
**Status:** GATE REPORT. Supporting evidence only. Not architecture authority.  
**This file does not:** decide an open ADR, approve the Famixa pilot, promote a Famixa type into Core, or authorize the next package.

No code, media, Git mutation, database write, migration, or provider call was made while writing this report.

---

## 1. Current checkpoint

There is no human-accepted Famixa episode and no Novixa film canon in the evidence read for this gate.

| Claim | State | Evidence |
|---|---|---|
| Level 1 architecture | Master Architecture V1. Status in the file: ARCHITECTURE DRAFT V1, with factual synchronization in §29 | `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` |
| Decision status | Register is DECISION PHASE. DECIDED is not IMPLEMENTED | `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` |
| Execution charter | `KIT-PLATFORM-EXECUTION-CHARTER-V1.md` was not found in this tree. The task records it as Proposed. It is not authority and was not used to override an ADR | Path search of the repository |
| Control file named by the task | `docs/00-control/KIT-PLATFORM-CONTROL.md` was not found. `docs/00-control/KIT-WORK-INDEX.md` exists and is a routing index. Architect disposition C-02: its “Primary SSOT” label does not create a second Level 1 | `STEP-031-ARCHITECT-DISPOSITION-D03-C01-C04.md` |
| Latest consolidation that still stands | Phase 1.5 physical consolidation is **NOT PASS**. The 2026-10-08 consolidation conclusion does not override that later evidence | `docs/architecture/KIT-PLATFORM-PHASE-1.5-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md`; C-04 in the Step 031 disposition |
| Architecture completion gate | Author conclusion on 2026-10-08 is PASS. The architect-acceptance block in that file is still “Awaiting Architect review.” This gate does not treat that file as an accepted close | `docs/architecture/KIT-PLATFORM-ARCHITECTURE-COMPLETION-GATE-V1.md` §12–§13 |
| Famixa film | EP01 build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` has no production-accepted shot and no accepted master. Official SH02 mix `58297402-e252-45e8-8d33-c7414749a297` remains unaccepted. Pilot plan is PLAN ONLY, awaiting product approval | `FAMIXA_PRODUCTION_STANDARD_AND_PILOT_PLAN_V1.md`; readiness and quality reports dated 2026-10-17 |
| Product work that may continue | Famixa Production OS work on existing OS routes, inside the Content pack, without a new engine and without promoting Famixa types | Decision Register, IMPLEMENTATION READINESS |

Product reports are historical evidence. Where they disagree with the Master Architecture or the Decision Register, the architecture document wins.

---

## 2. Architecture conformance matrix

PASS means the boundary still matches a decided architecture document or the current Content-pack code. It does not mean a film is accepted.

| Boundary | Status | Evidence |
|---|---|---|
| Content operating chain stays Idea → Brand → Brand Angle → Series → Episode → Content → Calendar → Published. Video is a content kind, not a second strategy system | PASS for the composed trail in the Content pack. The eight tokens are not one stored enum | `content-lineage.tsx` trails; `ContentCalendarPage.tsx` groups brand → series → episode and schedules topics; `kit-content-park-v1.md` topic status through Published. Content-park rule: brands are rows inside `KIT_MKT` |
| Canon and story decisions | PASS as a split, not as one store | ADR-01: `series_build.graph_json` is the Famixa script. Marketing voice is `content_brand` (`migrations/293_pack_content_brand_brain.sql`). Article lineage is brand / package angle / article series. These are different documents |
| Series → Episode → Content | PASS in the article lineage | `seriesContentTrail` in `content-lineage.tsx` |
| Production state and artifacts | PASS for official execution | ADR-01 and ADR-02: Production OS holds artifacts, bindings, and official preview. API prefix `api/content/production`. Schema `pack_content` |
| Content, Calendar, Publishing | PASS as a separate plane from Production OS | Master Architecture §13: `ContentWorkWorker` / `work_job` does not consume Production OS. Calendar reads topics and publish jobs |
| One official execution path | FAIL as a single live surface. PASS as authority | ADR-02: Production OS is official execution. Legacy `video_generation_execution` retirement is not implemented. Graph take URLs remain (ADR-06 OPEN). IndexedDB is cache. Kit Video Engine tables remain a compatibility surface. None of these is a second accept authority |
| Review bound to one artifact | PASS on the contract. FAIL on the official EP01 file | E2E contract RC4: ACCEPT names artifact id and hash. Official SH02 preview does not resolve, so the card cannot record that decision (`FAMIXA_FIRST_REAL_SHOT_VALIDATION_READINESS_V1_REPORT.md`) |
| Rebuild one episode and trace cost | FAIL for an accepted episode. Cost of an accept is UNKNOWN | No shot ACCEPT and no episode ASSEMBLY accept. No invoice was read |
| Reuse production capability without mixing canon | PASS as a prohibition. The reuse itself is not built | Master Architecture §6.3, §7, §23: no Production Engine, no Famixa type in Core. A second brand must not share Famixa `graph_json` or `famixa_character` |
| Famixa Production OS stays in the Content pack | PASS | Master Architecture §8. ADR-05: execution is not a shared primitive |
| Tenant-only Production and tenant-owned credentials | PASS as decisions. Implementation of historical rows is not claimed | ADR-03, ADR-04. D-05: adding `tenant_id` to `series_build` and any historical backfill are out of Phase 1.5 and not authorized |
| NULL `tenant_id` backfill | FAIL if attempted. Not done here | D-05. Official EP01 rows remain the unattributed rows named on 2026-10-17 |
| Graph, legacy endpoints, or client cache as the accept source | FAIL if used that way. Not used as authority by ADR-01 | ADR-01, ADR-06 OPEN |
| Brand narrative DNA migrations 383 and 384 | UNKNOWN as applied schema. Manifest ownership is DECIDED and NOT IMPLEMENTED | Decision Register, MANIFEST DISPOSITION. This gate did not apply them |
| ADR-07 preview rehash policy | UNKNOWN as a voted letter. Not a Core store | ADR-07. The pilot plan says the current tree rehashes on one path. That sentence is product evidence, not a decision |
| ADR-08 episode caller policy | OPEN. Not Core | ADR-08. Episode concat is not the next package |
| Charter overriding architecture | FAIL if treated as approved. The file was not found | Task instruction; path search |

Open items that this gate does not close: ADR-06 cleanup, ADR-05 notification and event/audit merge, ADR-08 caller policy, ADR-02 surface retirement, ADR-07 letter, historical NULL tenant rows.

---

## 3. Actual blockers

These are the blockers that stop an accepted film. A green test, a successful build, or an existing MP4 is not one of them.

### Famixa — one acceptable shot

1. **No director-authored speech window.** `ProductionDirectorTiming.ExtraJson` stores `durationSec`, `source`, `decisionType`, and `stage` only. The desk picker offers 5 or 10 seconds. `approvedDialogueIntervals` returns a window only when `source` is `DIRECTOR` and `speechEndSec` is after `speechStartSec`. The computed lead-in in `famixa-shot-production-timing.ts` is not that window. With no window, an 8 second picture and a line near 2.5 seconds fail `SPEECH_DOES_NOT_COVER_PLAYBACK`. Veo 4, 6, and 8 fail the same rule. This blocks a legal next speaking shot.
2. **The mix does not place that window.** `ProductionMixContract.Build` starts picture and audio at 0, runs them to the lip-sync duration, then fills the rest of the video-take clock with silence. It takes no speech start. `MixSpeechSpan` is an input of the clock checker and of its regression. This pass did not find a writer that stores those spans on the mix artifact. A window with no span still fails `DIALOGUE_WINDOW_MISMATCH`.

### Famixa — one accepted episode

These block the episode after a shot can be generated. They are not the next edit.

3. Official EP01 rows are unattributed. Tenant preview will not serve the official mix. Backfill is not authorized.
4. `ShotHumanReview` is an argument of `ShotProductionOutcomeV1.ReadAsync`. It is not a stored Production OS row. Graph `shotQa` is not execution authority.
5. No shot has a production ACCEPT. SH03’s locked line is truncated and voice stays refused. SH03–SH06 are outside Production OS in the 2026-10-17 matrix. SH06 pixels were not fetched.
6. Episode ASSEMBLY plus a separate ACCEPT on that file is absent. ADR-08 caller policy is OPEN.

The current SH02 mix is not accepted. Hash `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b` is byte identity, not a visual accept.

### Novixa — a separate film

7. **No Novixa film canon was found.** Migration 293 stores the Novixa content brand as pharmacy positioning and the Famixa content brand as Family OS copy, each told not to borrow the other’s language. The animated canon (Minh, Linh, `series_build` graph, `famixa_character`) is the Famixa production document. This pass did not query a database and does not claim a hidden Novixa series row is absent from Production data.
8. A Novixa film that reuses the Famixa graph or character registry would mix canon. A new Core production type would violate §23. Neither is authorized.

### KIT_MKT shared gap that actually stops both films

The shared gap is the missing director speech window and the missing mix placement of that window. Both brands would need it before a short spoken line can sit inside a model-length picture. It is a Content-pack Famixa OS gap. It is not a missing Core engine.

What is not a shared blocker: a generic Production Engine, a generic artifact store, episode concat, or a Novixa character model. Those are either forbidden or later.

---

## 4. Shared versus brand-specific

**Shared, already decided**

- KIT Core: identity, tenant, authorization, module enablement.
- Tenant `KIT_MKT`, package `marketing_park`, module `kit_content`, schema `pack_content`.
- The content lineage and calendar/publish plane.
- Production OS patterns may be reused later only as Famixa types inside the Content pack, on a different series document. They are not extracted.

**Must stay separate**

- Famixa film script, shot intent, and spoken lines: that series build’s `graph_json`.
- Famixa character identity: `famixa_character` and the approved stills named by the content-pack rule.
- Novixa brand voice in `content_brand`, and any future Novixa story, characters, and episode text. None of those rows may be copied from the Famixa graph.
- Production artifacts and bindings of one build. An ACCEPT on a Famixa artifact is not an ACCEPT for a Novixa film.
- Credentials stay tenant-owned (ADR-04). They are not a license to share canon.

**Not shared, and not to be built in the next package**

- Production Engine, generic artifact, generic workflow, Agent Runtime.
- Pharmacy, Family OS, and Local OS.

---

## 5. Next execution package

**One package: P1 from `FAMIXA_PRODUCTION_STANDARD_AND_PILOT_PLAN_V1.md` §10.**  
Make a director speech window storable, and record whether the existing mix can place it.

This is the smallest step that can make a later Famixa speaking shot legal. It does not accept SH02, does not generate media, and does not start a Novixa film.

### Objective

A person can store `speechStartSec` and `speechEndSec` with source `DIRECTOR`. The generation gate stays closed when they are absent. When they are present, the clock checks that window and the mix spans. The mix either emits a covering span for that window or the package stops and says it cannot, without a second editor.

### Depends on

Product owner written approval of pilot-plan decisions D1, D2, and D3. Until those three are written, this package does not start.

D1: §2 of the pilot plan is the method.  
D2: a person may type both ends of the speech interval. Code must not invent them.  
D3: the edit may touch only the files listed below and must not rewrite other dirty files.

### Architecture PASS for this package

- Stays in the Content pack and in Famixa Production OS.
- Does not add a Core type, a Production Engine, or a second pipeline.
- Does not resolve ADR-06, ADR-07, or ADR-08.
- Does not add `tenant_id`, backfill NULL rows, apply a migration, or call a provider.
- Does not change the 0.75 second tolerance or `VeoMotionDurationMapping`.
- Does not turn FFmpeg into a second editor. If placement needs a new composition system, stop.

### Files, only if D1–D3 are approved and only where the gap still exists

Already verified absent in this gate:

- Client timing has no speech fields: `client/admin/src/modules/content/famixa-director-timing.ts`, `FamixaDirectorTimingPicker.tsx`, and the Series tab post that sends director timing.
- Server timing has no speech fields: `ProductionDirectorTiming.cs`.
- Desk and server clocks already know a director window: `famixa-short-quality-pipeline.ts`, `ShortProductionQualityPipelineV1.cs`, `ShotProductionOutcomeV1.cs`.
- Mix placement does not take a window: `ProductionMixContract.cs` and the MIX_VIDEO metadata the clock already reads.

Do not edit Pharmacy, Family OS, Local OS, or unrelated dirty files.

### Technical PASS

- No window: an 8 second picture and a 2.5 second line still return `SPEECH_DOES_NOT_COVER_PLAYBACK`.
- A 0.2 second computed lead-in is still not a window.
- A fixture with source `DIRECTOR`, both ends set, and a mix span that covers that interval has no clock blocker.
- Picture locks of 5 and 10 seconds still do not create a speech window by themselves.
- Official SH02 mix id and SHA-256 are unchanged. No ACCEPT is written.
- Provider cost of the package is 0.

### Tests

- `famixa-short-quality-pipeline.smoke.ts`
- `famixa-director-timing-ui.smoke.ts`
- `ShortProductionQualityPipelineV1Regression`
- `ShotProductionOutcomeV1Regression`
- The existing MIX_VIDEO regression, and only if span output changes
- `famixa-short-acceptance.smoke.ts` — checkboxes still do not write ACCEPT

### Stop

Stop and return without expanding scope when any of these is true:

- D1, D2, or D3 is not written.
- The edit would touch a file outside the list, including another dirty file.
- Placement requires a new FFmpeg editor, a new pipeline, a Core type, or an ADR change.
- A migration, a tenant backfill, a permit, or a provider call would be required.
- The undeclared-window SH02 case starts passing.

D4 (a stored review row), D5 (a new tenant-owned build), D6 (one paid attempt), D7 (ADR-08 letter), and D8 (who watches, retake cap 3) stay closed. They are not part of P1.

---

## 6. Approval gates

| Work | May start from decisions already on record | Needs a new decision |
|---|---|---|
| Read Famixa OS and keep official execution on existing routes | Yes. Register implementation-readiness paragraph | No |
| P1 speech-window edit | Architecture allows it inside Famixa. The pilot plan does not | Product owner: D1, D2, D3, in writing |
| Persist `ShotHumanReview` | No | Product owner and Architect: D4. New content migration, not applied to Production, no Core type |
| New tenant-owned pilot build for one shot | No | Product owner: D5. Not a backfill of official rows |
| One paid Veo / voice / lip-sync attempt | No | Product owner: D6, and only after P1 is accepted |
| Episode concat | No | ADR-08 is OPEN. Product letter D7. Architect owns the caller policy. Stays out of Core |
| Historical `tenant_id` backfill | No | Forbidden by D-05 until a separate Architect decision |
| Novixa film canon, characters, and story | No | Product owner must approve a Novixa story that is not the Famixa graph. Architect if that choice needs a new type or a new pack |
| Production Engine or Core promotion | No | Forbidden by Master Architecture §23 |

---

## 7. Cost and evidence gaps

No account balance and no invoice was read. Catalog figures are not measured cost.

| Item | Figure in the repo | Kind |
|---|---|---|
| `google` / `veo-3.1` video-only 720p or 1080p, one count | 0.20 USD | Catalog, Known, in `FamixaVeo31OfficialPricing`. Not an invoice for the existing SH02 file |
| Fal lipsync v3 | 8 USD per minute, 5 second minimum | Catalog estimate |
| ElevenLabs voice | no amount | Unknown |
| Gemini still / keyframe | no amount confirmed for the pilot | Unknown |
| Runway and Wan | credits or a null USD amount | Estimate. Do not invent a USD conversion |
| FFmpeg mix and concat | no provider USD | Local compute |
| Actual charge, attempts, and person-time for one ACCEPT | none | Unknown. Pilot milestone M6 has not run |

The existing SH02 files prove bytes and hashes. They do not prove a spent amount.

---

## 8. Safety statement

This gate did not change implementation code, media, Production data, migrations, or provider usage. Git was not reset, cleaned, stashed, checked out, merged, or rebased. No dirty or untracked file was reverted or deleted. The only file added is this report.

---

## Handoff

Do not start P1 until the product owner writes D1, D2, and D3.

No Architect decision is required to permit those three product answers. An Architect decision is required before any later package that would add a migration, backfill tenant rows, change ADR-08, or create a Novixa production type.

Novixa film work stays closed until a separate story and character canon exist.
