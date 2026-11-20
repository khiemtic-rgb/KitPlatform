# FAMIXA PRODUCTION OS — FULL FILM VERTICAL RUN V1 REPORT

Date: 2026-09-19  
Mode: CONTROLLED END-TO-END EXECUTION  
Scope: One TEST film, one shot, fixture providers only.  
Safety: Runway / Veo / Gemini / Wan / Fal / ElevenLabs = 0. Paid media generation = 0. EP01 / EP99 not written. Canon / Visual Authority / Master / DNA / PRP / CRP / Timing V3 / Motion Contract / provider adapters unchanged.

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

**PASS_WITH_GAPS**

One complete TEST film travelled Production OS from Story to Final:

STORY → SHOT → KEYFRAME → CREATE_VIDEO → VIDEO_ARTIFACT → CREATE_VOICE → VOICE_ARTIFACT → CREATE_LIPSYNC → LIPSYNC_ARTIFACT → MIX → MIX_ARTIFACT → ACCEPT → ASSEMBLE → FilmAssembly → Final (existing contract)

Canonical live lineage on TEST-SC01-SH01 (`bbbbbbbb-0000-4000-8000-000000000371`):

```
FilmAssembly 719c3bbb-2f1e-4fe8-936a-a386bdee58ad
  └── FilmAssemblyShot TEST-SC01-SH01 → MIX 8cebdc3f-53d8-43b7-a1e9-024703cb6362
        └── LIPSYNC 6133e77b-4352-43a2-9197-57a29da24b71
              ├── VIDEO_TAKE 8c04a709-1479-4492-96c7-b594577202fd
              └── VOICE 822be0e9-6657-47d6-9b42-21ccda8a06bf
```

Assembly / Final were already Production OS. They were used as-is. No second orchestrator. No FINAL artifact role invented. Reload after API restart reconstructed CURRENT / ACCEPTED / FilmAssembly from durable Production OS rows. Fixture adapters only. EP01 / EP99 unchanged.

Gaps that keep this from a clean PASS: live Assembly-before-ACCEPT was proven at the GET gate (not a second POST with a valid MIX id before ACCEPT); official-film URL / FinalStore leftovers remain LEGACY display and were not deleted; Final physical bytes are the MIX artifact of this one-shot film, not a new concatenated render (existing “No FINAL claim” contract).

**STOP ARCHITECTURE WORK.** The next decision is whether to run the first REAL FILM with controlled real providers. That decision is not started here.

---

## 2. TEST FILM / SHOT

| Item | Value | Class |
|---|---|---|
| Film | Existing controlled TEST sandbox, treated as TEST-FILM-V1 | FACT / LIVE_VERIFIED |
| Build / scope | `bbbbbbbb-0000-4000-8000-000000000371` | FACT |
| Shot | `TEST-SC01-SH01` only for this run | FACT / LIVE_VERIFIED |
| Episode / scene | TEST / SC01 | FACT |
| Keyframe reused | `33333333-0000-4000-8000-000000000371` (TEST_KEYFRAME_V1, no image generation) | LIVE_VERIFIED |
| PerformanceSnapshot | `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e` `PERFORMANCE_HASH_V1:7715ad4e…b05ab8f1` | LIVE_VERIFIED |
| New test-run row | not created | FACT |

A dedicated `POST /test-runs` film was not inserted. `production_requests.build_id` still references `series_build`. The existing TEST scope already has that row and the valid keyframe. Mission: prefer existing TEST infrastructure; do not regenerate an image. FACT / RECOMMENDED only if a later phase wants a second TEST series_build.

In-process T01 created an isolated `TEST-FILM-V1` foundation (random TEST scope, one shot). TESTED.

SH02 (`TEST-SC01-SH02`) was not used as the vertical-run shot. Its historical CURRENT / ACCEPTED bindings stayed on the previous artifacts. LIVE_VERIFIED.

---

## 3. SAFETY

| Rule | Result | Class |
|---|---|---|
| Real Runway / Veo / Gemini / Wan / Fal / ElevenLabs | 0 | FACT / LIVE_VERIFIED |
| Runway HTTP audit after run + reload | start=0 poll=0 download=0 total=0 | LIVE_VERIFIED |
| Paid media generation | 0 — fixture-motion / voice / lipsync / mix / assembly | FACT / LIVE_VERIFIED |
| EP01 writes | 0 | LIVE_VERIFIED |
| EP99 writes | 0 | LIVE_VERIFIED |
| Canon / VA / Master / DNA / PRP / CRP | not modified | FACT |
| Timing V3 / Motion Contract / adapters | not modified | FACT |
| Production OS redesign | none | FACT |
| Schema migration | none | FACT |
| Official Film Preview migration | none | FACT |

---

## 4. FORENSIC ASSEMBLY PATH

Traced **before** any vertical-run code. Assembly / Final were already Production OS. **No Assembly cutover was made.** FACT

| Hop | Route | Class |
|---|---|---|
| CURRENT:MIX | `ProductionBinding` role `CURRENT:MIX` → `ProductionArtifact(MIX)` | AUTHORITY |
| ACCEPT | `AcceptOsAsync` → `ACCEPTED` + `ACCEPTED:MIX` on the same MIX artifactId | AUTHORITY |
| ASSEMBLE evaluate | GET actions available only when ACCEPTED exists | AUTHORITY |
| ASSEMBLE execute | `AssembleOsAsync` requires ACCEPTED binding matching artifactId | AUTHORITY |
| Parent check | `RequireParentOnShot` build + shot + physical SHA/storageKey | AUTHORITY |
| Fixture plan | `FixtureAssemblyAdapter.PlanFrozenAsync` unpaid, forbids URL identity | AUTHORITY HOW |
| Persist | `CreateAssemblyAsync` → `FilmAssembly` + `FilmAssemblyShot(artifactId)` | AUTHORITY |
| Final | Assemble contract: “Assembly lists ordered ACCEPTED artifactIds. **No FINAL claim.**” | AUTHORITY |
| `ProductionArtifactRoles.Final` | does not exist | FACT |
| `ProductionBindingRoles.Final` | unused by AssembleOs | FACT |
| `storageKey` on assembly DTO | `assembly/{assemblyId}` derived label, not media identity | DERIVED |
| MIX `storageKey` / SHA | physical MIX file identity | AUTHORITY |
| `previewUrl` / `finalUrl` / `lipsyncUrl` / `takeUrl` / `acceptedTake.url` | official-film graph / Director | LEGACY |
| `ShotProductionFinalStore` | browser final identity | LEGACY / CACHE |
| IndexedDB / localStorage / blob | official-film display | CACHE / LEGACY |
| Graph media URL | series_build.graph_json | LEGACY |
| Browser WebM recording | not on OS Assemble path | BYPASS if used as authority |

FACT: existing complete-authority chain already did MIX → ACCEPT → ASSEMBLE. This phase used that path.

---

## 5. VIDEO

| Field | Value | Class |
|---|---|---|
| Action | CREATE_VIDEO | LIVE_VERIFIED |
| Provider / model | fixture / fixture-motion-v1 | LIVE_VERIFIED |
| Request | `1931b6d2-d1f8-4b55-8568-eda7c028147e` | LIVE_VERIFIED |
| Task | `63b7c526-366d-4c19-96cd-b20bd7dc77b1` | LIVE_VERIFIED |
| Snapshot | `3f38027d-dbcf-47d2-a3dd-ee10547cd928` | LIVE_VERIFIED |
| Attempt | `a79ec98b-f2c4-4239-843a-e3defa5c9888` | LIVE_VERIFIED |
| Artifact | `8c04a709-1479-4492-96c7-b594577202fd` | LIVE_VERIFIED |
| Role | VIDEO_TAKE | LIVE_VERIFIED |
| SHA | `9ecd607e18ee06f3e0df50cf7632ad8c98e0ba5addcf269ab0675dec5d88f4c3` | LIVE_VERIFIED |
| storageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/a79ec98b…` | LIVE_VERIFIED |
| Parent | KEYFRAME `33333333-…0371` | LIVE_VERIFIED |
| CURRENT:VIDEO_TAKE | this artifact | LIVE_VERIFIED |
| Prior runway take | `5872d0a9-…` still on disk, SHA unchanged | LIVE_VERIFIED |

GET `/actions` CREATE_VIDEO available=true before POST. LIVE_VERIFIED

---

## 6. VOICE

| Field | Value | Class |
|---|---|---|
| Action | CREATE_VOICE | LIVE_VERIFIED |
| Provider / model | fixture / fixture-voice-v1 | LIVE_VERIFIED |
| Request | `12504a19-5361-49d0-8cce-ee2d3f15fe69` | LIVE_VERIFIED |
| Task | `71ff8053-7409-4b63-a01d-624f2db0ff8f` | LIVE_VERIFIED |
| Snapshot | `24eaa73d-335e-479f-88ab-87040716cf83` | LIVE_VERIFIED |
| Attempt | `94e3de34-3f5a-453c-a6be-76aee58bbf66` | LIVE_VERIFIED |
| Artifact | `822be0e9-6657-47d6-9b42-21ccda8a06bf` | LIVE_VERIFIED |
| SHA | `6ecd2d7d313e7445bfb79fc81000714fc02e2c6759c98d5d48c991e61daaf191` | LIVE_VERIFIED |
| storageKey | `os-test/…/voice/94e3de34…` | LIVE_VERIFIED |
| Line / performance | L1 / CHAR-001 / “Thì sao?” / snapshot `8231d8a9-…` | LIVE_VERIFIED |
| CURRENT:VOICE | this artifact | LIVE_VERIFIED |

GET `/actions` without line claim returned CREATE_VOICE available=false `MISSING_DEPENDENCY`. That is fail-closed, not a silent remap. POST with Production OS line + snapshot succeeded. LIVE_VERIFIED

---

## 7. LIPSYNC

| Field | Value | Class |
|---|---|---|
| Action | CREATE_LIPSYNC | LIVE_VERIFIED |
| Provider / model | fixture / fixture-lipsync-v1 | LIVE_VERIFIED |
| Request | `fd4b034e-90af-47bf-ac6f-dc485173ca34` | LIVE_VERIFIED |
| Task | `b9ab3909-abeb-44c5-a89e-6f42c4bde24f` | LIVE_VERIFIED |
| Snapshot | `4e304222-4561-49e5-b60f-d59795416906` | LIVE_VERIFIED |
| Attempt | `6bde7fc6-5d57-43a2-81da-a508517571db` | LIVE_VERIFIED |
| Artifact | `6133e77b-4352-43a2-9197-57a29da24b71` | LIVE_VERIFIED |
| SHA | `fc0df5d284cb99f2d7bc789a2b4fd4c40b373781fc8b2907ecbd278d408b4f7a` | LIVE_VERIFIED |
| Parents | VIDEO `8c04a709-…` + VOICE `822be0e9-…` exact | LIVE_VERIFIED |
| CURRENT:LIPSYNC | this artifact | LIVE_VERIFIED |

---

## 8. MIX

| Field | Value | Class |
|---|---|---|
| Action | MIX_VIDEO | LIVE_VERIFIED |
| Provider / model | fixture / fixture-mix-v1 | LIVE_VERIFIED |
| Request | `675f759b-68ee-41a7-878f-09ecb069aa40` | LIVE_VERIFIED |
| Task | `a6ae3fa5-7d74-4371-bd70-878428cb18f5` | LIVE_VERIFIED |
| Snapshot | `07763455-55a8-4aa9-9218-885153bdeb28` | LIVE_VERIFIED |
| Attempt | `27266a23-c5a3-453b-9071-c259fd202346` | LIVE_VERIFIED |
| Artifact | `8cebdc3f-53d8-43b7-a1e9-024703cb6362` | LIVE_VERIFIED |
| SHA | `9b56152d919945e22863c557fe36222cd3f03812235fbc801ddcdfc59441295f` | LIVE_VERIFIED |
| Parent | LIPSYNC `6133e77b-…` exact | LIVE_VERIFIED |
| CURRENT:MIX | this artifact | LIVE_VERIFIED |
| Prior MIX | `ab07ce5a-…` SHA `4722fbac…` unchanged | LIVE_VERIFIED |

---

## 9. ASSEMBLY

Used existing `AssembleOsAsync`. No new engine. FACT

Input is the ACCEPTED MIX artifact (same id as CURRENT:MIX). Contract required `AcceptedArtifactIds`. FACT / LIVE_VERIFIED

| Field | Value | Class |
|---|---|---|
| Action | ASSEMBLE | LIVE_VERIFIED |
| Provider / model | fixture / fixture-assembly-v1 | LIVE_VERIFIED |
| Request | `a9f27cd6-bc2c-4761-845a-67bb2b37f88e` | LIVE_VERIFIED |
| Task | `e6f504ee-4c77-4dad-918d-09e4599d8279` | LIVE_VERIFIED |
| Snapshot | `7e4a6588-c626-44bb-b78c-d55ad867d3ff` | LIVE_VERIFIED |
| Attempt | `04ba2a87-3659-4e6c-a592-8aa3a381782f` | LIVE_VERIFIED |
| FilmAssembly | `719c3bbb-2f1e-4fe8-936a-a386bdee58ad` | LIVE_VERIFIED |
| Title | TEST-ASSEMBLY | LIVE_VERIFIED |
| FilmAssemblyShot | `c8d1c93d-ca9e-434a-b7cb-972700203a2f` | LIVE_VERIFIED |
| Shot code | TEST-SC01-SH01 | LIVE_VERIFIED |
| Referenced artifact | MIX `8cebdc3f-…` (artifactId, not URL) | LIVE_VERIFIED |
| GET before ACCEPT | ASSEMBLE available=false `MISSING_DEPENDENCY` | LIVE_VERIFIED |
| URL shot code | `URL_AUTHORITY_FORBIDDEN` | LIVE_VERIFIED |

ACCEPT request `8d4f9b6b-…` bound ACCEPTED + ACCEPTED:MIX to the MIX artifact. LIVE_VERIFIED

---

## 10. FINAL

Existing Production OS contract: **No FINAL artifact role.** FilmAssembly is the film identity. Physical media for this one-shot film is the MIX artifact. FACT

| Check | Result | Class |
|---|---|---|
| Physical file | `…/video/27266a23c5a3453b9071c259fd202346.mp4` 707 bytes | LIVE_VERIFIED |
| Readable | yes | LIVE_VERIFIED |
| MIME | video/mp4 | LIVE_VERIFIED |
| Duration | 5s | LIVE_VERIFIED |
| SHA | `9b56152d919945e22863c557fe36222cd3f03812235fbc801ddcdfc59441295f` file hash match | LIVE_VERIFIED |
| Build | `bbbbbbbb-…0371` | LIVE_VERIFIED |
| Shot | TEST-SC01-SH01 | LIVE_VERIFIED |
| Browser recording / WebM authority | not used | FACT |
| New FINAL state machine | not created | FACT |

---

## 11. COMPLETE LINEAGE

```
FINAL / FilmAssembly 719c3bbb-…
        ↓
FilmAssemblyShot artifactId
        ↓
MIX 8cebdc3f-…  SHA 9b56152d…
        ↓
LIPSYNC 6133e77b-…  SHA fc0df5d2…
        ↓
VIDEO 8c04a709-…  SHA 9ecd607e…
VOICE 822be0e9-…  SHA 6ecd2d7d…
        ↓
KEYFRAME 33333333-…  (reused, not regenerated)
```

No URL is required to prove lineage. LIVE_VERIFIED

---

## 12. RELOAD

API was rebuilt, then started, then **restarted again after Assemble** (`SkipBuild`). GET `/state` after restart:

| Binding | Artifact | Class |
|---|---|---|
| CURRENT:VIDEO_TAKE | `8c04a709-…` | LIVE_VERIFIED |
| CURRENT:VOICE | `822be0e9-…` | LIVE_VERIFIED |
| CURRENT:LIPSYNC | `6133e77b-…` | LIVE_VERIFIED |
| ACCEPTED | `8cebdc3f-…` | LIVE_VERIFIED |
| ASSEMBLE request | COMPLETED | LIVE_VERIFIED |
| FilmAssembly row | still `719c3bbb-…` | LIVE_VERIFIED |

Authoritative state came from `production_*` tables + physical files. Not localStorage / sessionStorage / IndexedDB / previewUrl / latest URL. LIVE_VERIFIED

---

## 13. IDEMPOTENCY

Same idempotency keys replayed after success:

| Action | Key | Result | Class |
|---|---|---|---|
| CREATE_VIDEO | `vertical-film-v1-video` | same Request `1931b6d2-…` | LIVE_VERIFIED |
| CREATE_VOICE | `vertical-film-v1-voice` | same Request `12504a19-…` | LIVE_VERIFIED |
| CREATE_LIPSYNC | `vertical-film-v1-lipsync` | same Request `fd4b034e-…` | LIVE_VERIFIED |
| MIX_VIDEO | `vertical-film-v1-mix` | same Request `675f759b-…` | LIVE_VERIFIED |

No second fixture media generated on replay. LIVE_VERIFIED

---

## 14. FAILURE / RECOVERY

| Test | Result | Class |
|---|---|---|
| GET ASSEMBLE before ACCEPTED | available=false `MISSING_DEPENDENCY` | LIVE_VERIFIED |
| AssembleOs without ACCEPTED | `MISSING_DEPENDENCY`, no FilmAssembly | TESTED |
| After ACCEPT | Assemble succeeds | LIVE_VERIFIED |
| LipSync invalidate unbinds CURRENT:MIX; Motion invalidate unbinds ACCEPTED; Assemble fails; re-ACCEPT + Assemble recovers | existing dependency rules | TESTED |
| Live unbind of the proof ACCEPTED | not executed (would destroy the live FilmAssembly proof) | UNVERIFIED live / TESTED in-process |

No fallback to takeUrl / lipsyncUrl / previewUrl / acceptedTake.url / FinalStore / blob. TESTED + LIVE_VERIFIED (URL POST)

---

## 15. CROSS-SCOPE

| Attempt | Result | Class |
|---|---|---|
| SH01 VIDEO → SH02 CREATE_LIPSYNC | `CROSS_SHOT_FORBIDDEN` | LIVE_VERIFIED |
| TEST LIPSYNC → EP01 MIX | `CROSS_SCOPE_FORBIDDEN` | LIVE_VERIFIED |
| TEST MIX → EP99 ASSEMBLE | `CROSS_SCOPE_FORBIDDEN` | LIVE_VERIFIED |
| Other TEST build + same shot code MIX | `CROSS_SCOPE_FORBIDDEN` | TESTED |
| SH02 historical bindings | unchanged (VIDEO `583d7700-…` VOICE `3a753477-…` LIPSYNC `49deaa6b-…` MIX `638bd185-…`) | LIVE_VERIFIED |

---

## 16. LEGACY FALLBACK PROTECTION

| Fallback | Result | Class |
|---|---|---|
| `https://legacy/takeUrl.mp4` as assembly shot code | `URL_AUTHORITY_FORBIDDEN` | LIVE_VERIFIED |
| `idb://` / `blob:` identity | `URL_AUTHORITY_FORBIDDEN` | TESTED |
| `lipsyncUrl` / `previewUrl` / `acceptedTake.url` bind | `LegacySeriesGraphAdapter.MayDriveNewBinding` = false | TESTED |
| AssembleOs source | no lipsyncUrl / takeUrl / previewUrl / FinalStore | TESTED |
| UI OS ASSEMBLE | `assemblyArtifactIds` from `acceptedArtifactId` | TESTED (smoke) |
| Official-film leftover URLs | still present as LEGACY display, not deleted | FACT |

---

## 17. ARTIFACT IMMUTABILITY

| Artifact | After later work | Class |
|---|---|---|
| New VIDEO `8c04a709-…` | SHA unchanged after VOICE/LIPSYNC/MIX/ASSEMBLE | LIVE_VERIFIED |
| New VOICE / LIPSYNC / MIX | SHA = file hash; rows not rewritten | LIVE_VERIFIED |
| Prior runway VIDEO `5872d0a9-…` | SHA `e6553595…` still stored | LIVE_VERIFIED |
| Prior MIX `ab07ce5a-…` | SHA `4722fbac…` still stored | LIVE_VERIFIED |
| In-process regenerate / invalidate | prior SHA preserved | TESTED |

---

## 18. TEST MATRIX

| ID | Result | Class |
|---|---|---|
| T01 TEST film isolated | existing TEST scope; in-process new TEST-FILM-V1 foundation | TESTED / LIVE_VERIFIED |
| T02 TEST shot isolated | TEST-SC01-SH01 only | LIVE_VERIFIED |
| T03 Story exists | STORY=AVAILABLE | LIVE_VERIFIED |
| T04 ActingBeat exists | ACTING=AVAILABLE | LIVE_VERIFIED |
| T05 VisualContract exists | CREATE_VIDEO claim / fixture context | TESTED / LIVE_VERIFIED |
| T06 PerformanceSnapshot exists | `8231d8a9-…` | LIVE_VERIFIED |
| T07 Keyframe physical | `33333333-…` SHA `3db4d45f…` reused | LIVE_VERIFIED |
| T08 CREATE_VIDEO | fixture | LIVE_VERIFIED |
| T09 VIDEO verified | artifact / SHA / storageKey | LIVE_VERIFIED |
| T10 CURRENT:VIDEO_TAKE | `8c04a709-…` | LIVE_VERIFIED |
| T11 reload | after API restart | LIVE_VERIFIED |
| T12 CREATE_VOICE | fixture | LIVE_VERIFIED |
| T13 VOICE verified | | LIVE_VERIFIED |
| T14 CURRENT:VOICE | `822be0e9-…` | LIVE_VERIFIED |
| T15 reload | | LIVE_VERIFIED |
| T16 CREATE_LIPSYNC | fixture | LIVE_VERIFIED |
| T17 exact VIDEO parent | `8c04a709-…` | LIVE_VERIFIED |
| T18 exact VOICE parent | `822be0e9-…` | LIVE_VERIFIED |
| T19 LIPSYNC verified | | LIVE_VERIFIED |
| T20 CURRENT:LIPSYNC | `6133e77b-…` | LIVE_VERIFIED |
| T21 reload | | LIVE_VERIFIED |
| T22 MIX | fixture | LIVE_VERIFIED |
| T23 exact LIPSYNC parent | `6133e77b-…` | LIVE_VERIFIED |
| T24 MIX verified | | LIVE_VERIFIED |
| T25 CURRENT:MIX | `8cebdc3f-…` | LIVE_VERIFIED |
| T26 reload | | LIVE_VERIFIED |
| T27 Assembly uses MIX artifact | FilmAssemblyShot.artifactId | LIVE_VERIFIED |
| T28 FilmAssembly exists | `719c3bbb-…` | LIVE_VERIFIED |
| T29 FilmAssemblyShot exists | `c8d1c93d-…` | LIVE_VERIFIED |
| T30 Final generated | FilmAssembly (existing contract) | LIVE_VERIFIED |
| T31 Final physical bytes | MIX 707-byte mp4 | LIVE_VERIFIED |
| T32 Final SHA | file = artifact | LIVE_VERIFIED |
| T33 Final lineage | Assembly → MIX → LIPSYNC → VIDEO+VOICE | LIVE_VERIFIED |
| T34 Full reload | API restart | LIVE_VERIFIED |
| T35 Cross-shot | `CROSS_SHOT_FORBIDDEN` | LIVE_VERIFIED |
| T36 Cross-build | `CROSS_SCOPE_FORBIDDEN` | TESTED |
| T37 Cross-scope | EP01 MIX + EP99 ASSEMBLE | LIVE_VERIFIED |
| T38 Legacy URL | `URL_AUTHORITY_FORBIDDEN` | LIVE_VERIFIED |
| T39 IndexedDB identity | rejected in-process | TESTED |
| T40 browser blob identity | rejected in-process | TESTED |
| T41 upstream invalidation | LipSync change unbinds CURRENT:MIX | TESTED |
| T42 downstream stale | Motion change unbinds ACCEPTED; Assemble fails | TESTED |
| T43 immutability | prior SHA held | TESTED / LIVE_VERIFIED |
| T44–T47 idempotent four actions | same Request ids | LIVE_VERIFIED |
| T48 Assembly fail closed | GET + in-process POST | LIVE_VERIFIED / TESTED |
| T49 recovery | re-ACCEPT + Assemble | TESTED |
| T50 Provider calls | 0 | LIVE_VERIFIED |
| T51 Paid generation | 0 | LIVE_VERIFIED |
| T52 EP01 unchanged | see §20 | LIVE_VERIFIED |
| T53 EP99 unchanged | see §21 | LIVE_VERIFIED |

C# suite `FAMIXA_PRODUCTION_OS_FULL_FILM_VERTICAL_RUN_V1` PASS (xUnit). TESTED  
Client smoke `famixa-production-os-full-film-vertical-run.smoke.ts` PASS. TESTED

---

## 19. LIVE VERIFICATION

API rebuilt (`dotnet build` 0 errors) and restarted on `localhost:5290`. Then the TEST film was executed. Then API was restarted again and state was re-read. FACT / LIVE_VERIFIED

Captured IDs are in §5–§10. In-process-only claims are not marked LIVE_VERIFIED.

Quality of picture / voice / lipsync / motion / edit was not judged. FACT

---

## 20. EP01 SAFETY

| Field | Before | After |
|---|---|---|
| series_build id | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | same |
| title | Tập 01 | same |
| updated_at | 2026-09-18 23:01:02.129634+07 | same |
| graph_json md5 | `415fedf306ed23d1ace94a794400b440` | same |
| production tasks | 0 | 0 |
| active bindings | 0 | 0 |

Unchanged. LIVE_VERIFIED

---

## 21. EP99 SAFETY

| Field | Before | After |
|---|---|---|
| series_build id | `77262ec0-74ad-459d-9128-f09bb553a5dd` | same |
| title | TEST BATCH 3 SHORT | same |
| updated_at | 2026-09-07 16:52:54.467199+07 | same |
| graph_json md5 | `c7cd2516627332c2648c1509395e4a64` | same |
| production tasks | 0 | 0 |
| active bindings | 0 | 0 |

Unchanged. LIVE_VERIFIED

---

## 22. BLOCKERS

None that stop the vertical run.

Assembly consumed `ProductionArtifact(MIX)` via ACCEPTED. Final persisted as FilmAssembly + MIX bytes. Reload kept authority. Legacy URL fallback was rejected. No cross-scope write. No second authority invented.

---

## 23. REMAINING GAPS

1. Final is FilmAssembly + MIX physical bytes, not a new FINAL artifact role or concatenated multi-shot file. Existing contract. FACT
2. Live Assembly-before-ACCEPT POST with a valid MIX id was not repeated after ACCEPT (would require unbinding the proof). GET gate + in-process POST cover it. UNVERIFIED live POST / TESTED
3. Official-film `lipsyncUrl` / `ShotProductionFinalStore` / graph URLs remain LEGACY display. Not deleted. FACT
4. Browser IndexedDB / blob leftovers not live-clicked. TESTED in source. UNVERIFIED in browser
5. A brand-new `production_test_run` named TEST-FILM-V1 was not inserted because requests still FK to `series_build`. Existing TEST scope used. FACT
6. Quality of the fixture media was not evaluated. FACT — out of scope

---

## 24. FINAL VERDICT

**PASS_WITH_GAPS**

The first end-to-end Production OS film proof is complete on TEST-SC01-SH01. Lineage is durable. Reload works. Fixtures only. EP01 / EP99 untouched.

This is not another architecture hardening phase. Assembly / Final did not need a cutover.

---

## 25. STOP / NEXT STEP

**STOP ARCHITECTURE WORK.**

Do not start another forensic micro-phase.  
Do not invent a FINAL artifact role.  
Do not migrate Official Film Preview.  
Do not run a real paid provider from this report.

Next decision (human): whether to run the first **REAL FILM** with controlled real providers.

END.
