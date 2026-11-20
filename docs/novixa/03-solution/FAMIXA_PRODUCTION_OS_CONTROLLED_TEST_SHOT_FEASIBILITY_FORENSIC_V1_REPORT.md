# FAMIXA PRODUCTION OS — CONTROLLED TEST SHOT FEASIBILITY FORENSIC V1

Date: 2026-09-17  
Mode: read-only architecture / contract forensic  
Official film left untouched: EP01 6-shot `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` (SH01–SH06)

No code changes. No schema changes. No Production OS or series_build writes. No image generation. No provider calls. No import. No fixture run.

---

## Final verdict

**C. TEST SHOT path chưa tồn tại**

Production OS will accept an arbitrary `(buildId, episodeCode, sceneCode, shotCode)` string scope and can ingest pixels via `POST /keyframes` then run unpaid `FixtureMotionAdapter` via `POST /requests`. That is **not** a TEST-ONLY shot.

What is missing:

1. A test-only build/shot type that is **not** a `pack_content.series_build` film.
2. Isolation from Video Studio / active Famixa pilot.
3. Quarantine or delete of `production_*` rows.
4. A repository **test image fixture** (see §3).

Do **not** mint a real series build to “get around” this. `PUT /api/content/series/builds` writes production `series_build` and **sets that id as the active FAMIXA pilot**. That mixes test into the production domain and can displace official EP01 in the studio list.

**Not A/B:** no dedicated test path, and no test image fixture.  
**Not D:** official SH01–SH06 OS bindings are keyed by `(build_id, shot_code)`. A *future* harness with a **new** build UUID would not rewrite official graph, Canon, or VA. Isolation is incomplete, not impossible.

---

## 1. TEST SHOT SUPPORT

### Build scope

| Layer | Fact |
|---|---|
| Domain | `ProductionScope(BuildId, EpisodeCode, SceneCode, ShotCode)` — four fields, no TEST flag |
| Persistence | `production_requests.build_id` / `production_tasks.build_id` **FK** `pack_content.series_build(id) ON DELETE RESTRICT` |
| Implication | OS cannot persist a shot unless `buildId` is already a real `series_build` row |

Existing `series_build` rows (all first-class films, none TEST-ONLY):

| id | title | note |
|---|---|---|
| `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | Tập 01 | official EP01 6-shot — leave alone |
| `fd760913-0eb8-46a5-b6eb-ec2990a4b0d0` | Tập 01 | older 11-shot |
| `fca0c122-797e-4356-9bd1-2a70daac1ed4` | Tập 01 | older 11/25-shot |
| `77262ec0-74ad-459d-9128-f09bb553a5dd` | TEST BATCH 3 SHORT | official EP99 — leave alone |

### Shot identity

OS `shot_code` is a free `VARCHAR(80)` (e.g. `EP01-SC01-SH02`). There is no OS table of shots and no “create shot” command. Identity is the pair `(build_id, shot_code)`.

Cross-shot steal is forbidden at execute time: keyframe artifact’s owning task must match the same `BuildId` + `ShotCode`.

### Required fields (API)

`POST /api/content/production/keyframes`:

- `buildId`, `episodeCode`, `sceneCode`, `shotCode`, `story`, `keyframeDataUrl`

`POST /api/content/production/requests` (CREATE_VIDEO):

- `buildId`, `episodeCode`, `sceneCode`, `shotCode`
- `story`, `actingBeat`, `visualContract`, `timing`, `aspectRatio`, `durationSec`
- optional `keyframeArtifactId` and/or `keyframeDataUrl` (if bytes ≥ 32, service auto-registers KEYFRAME first)

### Action Contract

`CREATE_VIDEO` (`ProductionActionContractRegistry.CreateVideo`):

| Field | Value |
|---|---|
| Target | Shot |
| RequiredInputs | ApprovedKeyframe, Story, ActingBeat, VisualContract, Timing, FrozenProviderModel |
| Capability | Motion |
| Output | VIDEO_TAKE / VideoTake |
| UiCta | Tạo video |
| Forbidden fallbacks | CREATE_IMAGE, CREATE_VOICE, CREATE_LIPSYNC |

`CREATE_IMAGE` (used by keyframe ingest): VisualContract, CharacterIdentity → IMAGE / Keyframe, CTA Tạo hình, provider stamp `import` / `existing-pixels`.

### Story / ActingBeat / VisualContract / Timing / Provider

See §6. Availability and execute gates **do not all implement** the contract list.

### Keyframe dependency

CREATE_VIDEO requires a **verified** `production_artifacts` row with role `KEYFRAME` bound as `CURRENT:KEYFRAME` on that `(buildId, shotCode)`. Graph `kfApproved` / IDB / filename are not OS authority.

---

## 2. KEYFRAME INGESTION (contract only — no POST)

`POST /api/content/production/keyframes`

| Check | Contract |
|---|---|
| Auth | ADMIN + `kit_content` |
| Body | `ProductionRegisterKeyframeRequestDto` |
| Pixels | `keyframeDataUrl` must contain `base64,`; decoded length **≥ 32**; MIME from data-URL header |
| Story | required string (frozen on CREATE_IMAGE snapshot) |
| Scope | any `buildId` that **already exists** in `series_build` |
| Effect | CREATE_IMAGE Request → Task → Snapshot (`import` / `existing-pixels`) → Attempt → Artifact(KEYFRAME) → `CURRENT` + `CURRENT:KEYFRAME` |
| Idempotency | `{buildId:N}:{shotCode}:CREATE_IMAGE:{sha256}` |
| File store | `IProductionArtifactFileStore.Write("os-kf", attemptId)` → `KitVideoVideoStore.PersistNamed` writes **`{id}.mp4`** under `App_Data/kit-video-i2v/os-kf/` even for JPEG/PNG bytes |
| Canon / VA | not written |
| Official graph | not written |

The endpoint **can** accept an existing test image **if** a caller supplies a valid data URL and a **pre-existing** `series_build` id. This forensic pass did not POST.

In-memory regression “seeds” KEYFRAME as UTF-8 `"FAMIXA-KF-…"`, not a repo file.

---

## 3. TEST IMAGE SOURCE

Searched: `tests/`, `testdata/`, Content pack, `App_Data` OS folders, filename `*fixture*`, `fixture-image`.

Docs PNG under `docs/novixa/**` are Pharmacy / brand screenshots. They are **not** a Famixa TEST KEYFRAME fixture and must not be used as one.

**NO TEST IMAGE FIXTURE AVAILABLE**

No path / mime / bytes / SHA256 / width / height to record for a sanctioned test-only still.

---

## 4. SHOT CREATION

| Mechanism | Exists? | TEST-ONLY? |
|---|---|---|
| Production OS “create shot” API | **No** | — |
| OS accepts new `shotCode` on first POST | Yes (string) | No type |
| `PUT /api/content/series/builds` | Yes — upserts `series_build`, mints UUID if empty | **No** — production film graph; **sets active FAMIXA pilot** |
| `DELETE /api/content/series/builds/{id}` | Yes | Blocked after any `production_*` row (`ON DELETE RESTRICT`) |
| Production OS delete / quarantine | **No** | — |

**Exact missing capability:** a harness that can mint an OS scope **without** inserting or activating a user-facing `series_build`, and that can drop `production_*` + `os-kf` / `os` files after the run.

Using official EP01 + a new `shotCode` (e.g. SH99) would attach OS rows to the official build id. Forbidden for this mission and not TEST-ONLY.

---

## 5. ISOLATION

| Requirement | If a future new `series_build` UUID were used | Today |
|---|---|---|
| Not official EP01 | Possible (different `build_id`) | No TEST build exists |
| No Canon write | POST keyframes does not touch Canon | OK |
| No Visual Authority change | OS does not write VA | OK |
| No SH01–SH06 graph / OS bind change | Bindings are per `shot_code` | OK **only** if official `build_id` is never used |
| No user-facing film impact | **Fails** — `ListBuilds(FAMIXA)` shows the row; upsert sets **active pilot** | No clean hide |
| Delete / quarantine after test | **Fails** — no OS purge; `DELETE series_build` RESTRICT | Cannot quarantine |

---

## 6. CREATE_VIDEO READINESS (`GET /actions`)

`GET /api/content/production/shots/{buildId}/{shotCode}/actions`  
Query defaults: `storyPresent=true`, `actingPresent=true` (UI client does not send them).

Availability implementation (`BuildActions`):

```
CREATE_VIDEO.available = Has(Keyframe) && storyPresent && actingPresent
```

`Has(Keyframe)` = node status Current / Available / Accepted = active binding `CURRENT:KEYFRAME` (or missing → `MISSING_DEPENDENCY`).

| Contract input | How GET /actions verifies | How execute verifies |
|---|---|---|
| **ApprovedKeyframe** | Verified KEYFRAME artifact + current typed binding for this `(buildId, shotCode)` | Same; plus role, `Verified`, owner task scope match. Else `MISSING_DEPENDENCY` / `CROSS_SHOT_FORBIDDEN` |
| **Story** | Query `storyPresent` (default **true**), **not** series graph | `input.Story` non-empty |
| **ActingBeat** | Query `actingPresent` (default **true**), **not** graph | **Not** checked in `RequireCreateVideoDependenciesAsync` |
| **VisualContract** | Listed on contract `RequiredInputs` only | Passed through snapshot; **not** gated |
| **Timing** | Listed only | Body `timing` / `durationSec` frozen; **not** gated for availability |
| **FrozenProviderModel** | Listed only | Set at freeze from adapter: `fixture` / `fixture-motion-v1` |

So `CREATE_VIDEO.available=true` on a live GET (defaults) means **only**: OS KEYFRAME current on that shot. Official SH02 is `available=false` because that binding does not exist.

---

## 7. FIXTURE EXECUTION (not run)

If a scope had KEYFRAME + story string + unpaid adapter:

`CREATE_VIDEO` → Request → Task → Snapshot → Attempt → `FixtureMotionAdapter` → Artifact(VIDEO_TAKE) → CURRENT + VISIBLE → GET preview.

| Gate | Status |
|---|---|
| Paid adapters | Throw until fixture path is proven (`IsPaid`) |
| Fixture adapter | Unpaid. Bytes = UTF-8 `"FAMIXA-FIXTURE-MP4-" + snapshotHash + taskId` (padded ≥ 32). MIME `video/mp4`. **Not a real MP4** |
| VerifyArtifact | Length ≥ 32 only — would **accept** those bytes |
| File write | `os/{attemptId}.mp4` under kit-video-i2v |
| Browser preview | `GET .../preview/media` serves `video/mp4`; fake UTF-8 **will not play** |
| In-memory regression | Already proves this slice **without** Postgres FK (`InMemoryProductionWorkStore`) |

Live Postgres + HTTP can run the same slice **only after** a real `series_build` id + ≥32 image bytes. That is not a TEST-ONLY harness. This pass did not execute it.

---

## 8. Missing capability (for a later harness — do not build here)

A controlled fixture E2E that meets §5 needs something **outside** production `series_build` / studio list, for example:

- OS scope not FK’d to a user film, **or** a quarantined test build that `ListBuilds` / active-pilot never surfaces
- Explicit TEST shotCode namespace
- Delete/purge of `production_*` + artifact files
- A **repo** test still (≥32 B, known SHA, not Canon / not official SH01–SH06)

Until that exists: do not generate, do not import onto official EP01, do not PUT a decoy film.

---

## Writes / paid calls this pass

0.
