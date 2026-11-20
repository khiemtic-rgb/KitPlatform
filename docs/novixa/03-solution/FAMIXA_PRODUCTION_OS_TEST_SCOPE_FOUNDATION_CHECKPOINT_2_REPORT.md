# FAMIXA PRODUCTION OS — TEST SCOPE FOUNDATION CHECKPOINT 2

Date: 2026-09-17  
Mode: TEST scope foundation only

Official EP01: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Official EP99: `77262ec0-74ad-459d-9128-f09bb553a5dd`

Live TEST sandbox (not a film):

- `test_run_id` = `aaaaaaaa-0000-4000-8000-000000000371`
- `scope_id` = `bbbbbbbb-0000-4000-8000-000000000371`
- `shot_code` = `TEST-SC01-SH01`

Not implemented: fixture keyframe, CREATE_VIDEO E2E, purge, UI, Director CTA, paid providers, image/video generation, FixtureMotionAdapter changes.

---

## 1. Verdict

**PASS**

An isolated TEST scope exists beside FILM without a `series_build`.

- `production_test_run` / `production_test_scope` / `production_test_shot` created
- TEST `production_scope_anchor.kind = TEST`, `series_build_id = NULL`, `test_run_id` set
- TEST `scope_id` is a new UUID, not EP01/EP99/any FILM build
- Authoritative `ProductionScopeGuard` resolves FILM / TEST / UNKNOWN and fail-closes cross-scope access with `CROSS_SCOPE_FORBIDDEN`
- TEST storage namespace is `os-test/{test_run_id}/kf` and `os-test/{test_run_id}/video` on the existing file-store
- FILM storage remains `os-kf/` and `os/`
- Same Production OS entities + `IProductionExecutionService` + `CreateVideoActionContractGate`
- `series_build` count 4, `active_build_id` official EP01, EP01/EP99 `graph_json` MD5 unchanged
- `GET /api/content/series/builds?code=FAMIXA` still returns the same four films
- T01–T16 pass; existing Production OS suites still pass
- Paid provider calls = 0

---

## 2. Migration number

**371** — `migrations/371_pack_content_production_test_scope.sql`  
Manifest: `deploy/ubuntu/migration-files.content.txt`  
Recorded: `kit_schema_migrations` (`371_pack_content_production_test_scope.sql`)

Pre-change inspection of live 370:

| Item | Live value before 371 |
|---|---|
| `production_scope_anchor` columns | `scope_id`, `kind`, `series_build_id`, `created_at` (no `test_run_id`) |
| FILM anchors | 4 |
| TEST anchors | 0 |
| `ck_prod_scope_kind` | `FILM` \| `TEST` already allowed; no TEST rows |
| `ck_prod_scope_test_no_build` | TEST ⇒ `series_build_id IS NULL` |
| `production_*` FKs | `fk_prod_*_scope` → `production_scope_anchor(scope_id)` |

First apply: tables/column/constraints created.  
Second apply: `already exists, skipping`; exit 0. Idempotent. Transactional (`BEGIN`/`COMMIT`).

FILM rows were not rewritten. `test_run_id` is NULL on all four FILM anchors.

---

## 3. Tables created

| Table | Role |
|---|---|
| `pack_content.production_test_run` | Durable TestRun (`test_run_id`, `name`, `status` OPEN\|CLOSED, `created_at`, `created_by`, `metadata`) |
| `pack_content.production_test_scope` | TEST OS scope; PK `scope_id` → `production_scope_anchor` |
| `pack_content.production_test_shot` | Addressable shot (`episode_code` / `scene_code` / `shot_code`); UNIQUE `(scope_id, shot_code)` |

Altered:

- `production_scope_anchor.test_run_id` → `production_test_run` (nullable)
- `ck_prod_scope_test_has_run`: TEST ⇒ `test_run_id IS NOT NULL`
- `ck_prod_scope_film_no_run`: FILM ⇒ `test_run_id IS NULL`

No new Film Episode/Scene/Shot tables. No second execution engine.

---

## 4. TEST anchor structure

```
production_test_run
    test_run_id, name, status=OPEN, created_at, metadata
        ↓
production_scope_anchor
    scope_id          = new UUID (not a series_build.id)
    kind              = TEST
    series_build_id   = NULL
    test_run_id       = production_test_run.test_run_id
        ↓
production_test_scope
    scope_id = same TEST anchor
        ↓
production_test_shot
    ProductionScope(scope_id, episode=TEST, scene=SC01, shot=TEST-SC01-SH01)
```

`ProductionTestFoundationService.CreateAsync` allocates a fresh `scope_id` and refuses official FILM UUIDs. It does not insert `series_build`.

Live row:

| Field | Value |
|---|---|
| `kind` | TEST |
| `scope_id` | `bbbbbbbb-0000-4000-8000-000000000371` |
| `series_build_id` | NULL |
| `test_run_id` | `aaaaaaaa-0000-4000-8000-000000000371` |
| in `series_build` | 0 |

---

## 5. Scope resolution

`ProductionScopeGuard.Resolve(anchor, scopeId)`:

| Input | Kind |
|---|---|
| Anchor `kind=FILM` | FILM |
| Anchor `kind=TEST` | TEST |
| No row + official EP01/EP99/known film UUID | FILM |
| No row + unknown UUID | UNKNOWN |

`IProductionTestFoundationService.ResolveKindAsync` reads the anchor store, then the same guard.

---

## 6. Scope guard behavior

Server/domain authoritative (`ProductionExecutionService` when `IProductionScopeFoundationStore` is present). Not a UI check.

| Case | Result |
|---|---|
| FILM request / FILM artifact | allowed (existing OS) |
| TEST request / TEST artifact same `scope_id` | allowed |
| TEST request using FILM artifact | `CROSS_SCOPE_FORBIDDEN` |
| FILM request using TEST artifact | `CROSS_SCOPE_FORBIDDEN` |
| TEST binding/preview resolving FILM artifact | `CROSS_SCOPE_FORBIDDEN` |
| FILM preview resolving TEST artifact | `CROSS_SCOPE_FORBIDDEN` |
| Unknown scope evaluate | `UNKNOWN_SCOPE` (fail closed) |

Legacy in-memory OS tests that omit the scope store still run (guard is a no-op). FILM/Unknown mix stays allowed only in that legacy path so cutover suites do not break.

Artifact lookup is by id, then owner `task.Scope.BuildId` is compared. Bindings and CREATE_VIDEO facts are always `(build_id, shot_code)`. There is no “latest artifact globally” query.

---

## 7. Storage namespace

Derived from authoritative scope kind + `test_run_id` (`ProductionStoragePolicy`). Not a client folder string. Same `IProductionArtifactFileStore`.

| Kind | Keyframe | Video |
|---|---|---|
| FILM / unknown-without-store | `os-kf` | `os` |
| TEST | `os-test/{test_run_id}/kf` | `os-test/{test_run_id}/video` |

TEST without `test_run_id` fail-closes (`UNKNOWN_SCOPE`).

---

## 8. FILM isolation evidence

| Check | Before 371 | After 371 + TEST insert |
|---|---|---|
| `series_build` count | 4 | 4 |
| FAMIXA `active_build_id` | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | same |
| EP01 `graph_json` MD5 | `12289be18284fbd4d4b166b82c09eba0` | same |
| EP99 `graph_json` MD5 | `c7cd2516627332c2648c1509395e4a64` | same |
| FILM anchors | 4 | 4 |
| TEST anchors | 0 | 1 |
| TEST id in `series_build` | — | 0 |
| TEST scope = any FILM scope | — | 0 |
| `production_bindings` | 0 | 0 |
| `production_requests` / `tasks` / `artifacts` | 0 | 0 |
| `famixa_character` | 7 | 7 |
| `GET /series/builds?code=FAMIXA` | 4 films | same 4 ids; TEST UUIDs absent |

`ListSeriesBuildsAsync` still reads only `pack_content.series_build`.

---

## 9. Regression results

`dotnet test tests/KitPlatform.Content.Tests --filter ProductionPipelineV1Tests`

| Suite | Result |
|---|---|
| Work execution T01–T24 | pass |
| Pipeline + fixture slice | pass |
| CREATE_VIDEO cutover | pass |
| Action contract T01–T11 | pass |
| Checkpoint 1 FILM identity | pass |
| **Checkpoint 2 T01–T16** | **pass** |

T01 create TEST run  
T02 create TEST scope  
T03 create TEST anchor (`kind=TEST`, `series_build_id=NULL`)  
T04 create TEST shot `TEST-SC01-SH01`  
T05 TEST resolves as TEST  
T06 existing FILM (EP01/EP99) resolves as FILM  
T07 unknown scope → `UNKNOWN_SCOPE`  
T08 TEST cannot access FILM artifact / claim / preview  
T09 FILM cannot access TEST artifact / claim / preview  
T10 TEST evaluate on FILM `shot_code` stays `MISSING_KEYFRAME` (no FILM facts)  
T11 TEST storage `os-test/{test_run_id}/kf`  
T12 FILM storage remains `os-kf`  
T13 TEST absent from series-build queries  
T14 `active_build` identity / official EP01 constant unchanged  
T15 EP01/EP99 FILM anchors unchanged  
T16 existing Production OS suites still pass  

CREATE_VIDEO on a bare TEST shot is correctly unavailable (`MISSING_KEYFRAME`). The evaluator uses the TEST `scope_id` and does not fall back to FILM data.

---

## 10. DB writes

| Write | Count / note |
|---|---|
| `production_test_run` | 1 live sandbox row |
| `production_scope_anchor` TEST | 1 new row |
| `production_test_scope` | 1 |
| `production_test_shot` | 1 |
| `kit_schema_migrations` | 371 recorded |
| `series_build` | **none** |
| `series_pilot.active_build_id` | **unchanged** |
| `graph_json` | **unchanged** |
| Canon / Visual Authority | **no writes** |
| FILM `production_*` work rows | **none** |
| FILM artifact ids | **not rewritten** |

---

## 11. Provider calls

| Call | Count |
|---|---|
| Gemini | 0 |
| Runway | 0 |
| Veo | 0 |
| Fal | 0 |
| ElevenLabs | 0 |
| Image generation | 0 |
| Video generation | 0 |
| CREATE_VIDEO execution | 0 |

T11/T12 in-memory tests import existing dummy pixels through `RegisterKeyframe` (`import` / `existing-pixels`) only to prove the folder prefix. No live media write. No paid adapter.

---

## 12. Files changed

Created:

- `migrations/371_pack_content_production_test_scope.sql`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionScopeGuard.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/IProductionScopeFoundationStore.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/InMemoryProductionScopeFoundationStore.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionTestFoundationService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionTestScopeFoundationV1Regression.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionScopeFoundationRepository.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_TEST_SCOPE_FOUNDATION_CHECKPOINT_2_REPORT.md`

Modified:

- `deploy/ubuntu/migration-files.content.txt`
- `ProductionWorkIds.SchemaVersion` → `371` (`CROSS_SCOPE_FORBIDDEN`, `UNKNOWN_SCOPE`)
- `ProductionScopeAnchorRules.cs` (TEST kind)
- `ProductionScopeAnchorV1Regression.cs` (schema 371)
- `ProductionExecutionService.cs` (scope store + guard)
- `ProductionCreateVideoService.cs` (storage folder from scope kind)
- `ContentPackDependencyInjection.cs`
- `ContentProductionController.cs` (`POST/GET /api/content/production/test-runs`)
- `ProductionApiDtos.cs`
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs`

No UI / Director CTA / provider adapter / FixtureMotionAdapter / Canon / VA / graph changes.

The running `:5290` process still holds pre-checkpoint-2 binaries (file lock). Domain + tests are the authority for the new guard. `GET /series/builds` isolation was proven against live data.

---

## 13. Exact remaining blocker for Checkpoint 3

TEST scope is addressable and isolated, but it has **no OS KEYFRAME**.

`CreateVideoActionContractGate` / `EvaluateCreateVideoAsync` therefore stay **`MISSING_KEYFRAME`** on `TEST-SC01-SH01`.

Checkpoint 3 must add a **TEST-scoped existing-pixel KEYFRAME** (or equivalent fixture import) inside `os-test/{test_run_id}/kf/`, then re-evaluate CREATE_VIDEO on that same TEST `scope_id`.

Still out of scope until explicitly requested: paid providers, image/video generation, CREATE_VIDEO E2E, FixtureMotionAdapter changes, purge, UI, FILM data.

---

**PASS** — TEST Scope Foundation is proven. Stop.
