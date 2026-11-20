# FAMIXA PRODUCTION OS — TEST SCOPE FOUNDATION CHECKPOINT 1

Date: 2026-09-17  
Mode: schema foundation only (FILM identity)

Official EP01: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Official EP99: `77262ec0-74ad-459d-9128-f09bb553a5dd`

Not implemented: TestRun, TestScope, TestShot, TEST storage, TEST fixture, purge, kind guards, UI, provider, CREATE_VIDEO execution, image generation.

---

## Final verdict

**PASS**

Existing FILM Production OS behavior is unchanged.

- EP01 and EP99 are `production_scope_anchor.kind = FILM` with `scope_id = series_build.id`
- `production_*` `build_id` values are the same UUIDs; they now FK to the anchor
- `series_pilot.active_build_id` is still official EP01
- `GET /series/builds?code=FAMIXA` still returns the same four films
- No `series_build` insert/update/delete
- No Canon / VA / `graph_json` writes
- CREATE_VIDEO Action Contract RequiredInputs unchanged
- Official SH02 CREATE_VIDEO remains unavailable (no OS KEYFRAME)
- Production OS regression suites pass
- Migration 370 re-ran cleanly (`INSERT 0 0` on second apply)
- Paid provider calls = 0

---

## Pre-change inspection (368 / 369 / live DB)

### Migrations

368 creates `production_requests`, `production_tasks`, `production_bindings`, `production_film_assemblies`, `production_film_assembly_shots` with:

`build_id UUID NOT NULL REFERENCES pack_content.series_build (id) ON DELETE RESTRICT`

369 only widens binding roles and snapshot lineage. It does **not** change those FKs.

### Live constraints before 370

| Table | Constraint | Definition |
|---|---|---|
| `production_requests` | `production_requests_build_id_fkey` | `build_id → series_build(id) ON DELETE RESTRICT` |
| `production_tasks` | `production_tasks_build_id_fkey` | same |
| `production_bindings` | `production_bindings_build_id_fkey` | same |
| `production_film_assemblies` | `production_film_assemblies_build_id_fkey` | same |
| `production_film_assembly_shots` | `production_film_assembly_shots_build_id_fkey` | same |

### Before snapshot

| Item | Value |
|---|---|
| `series_build` count | 4 |
| FAMIXA `active_build_id` | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| `production_requests` / `tasks` / `artifacts` | 0 / 0 / 0 |
| `production_bindings` (all / active) | 0 / 0 |
| TEST anchors | none (table did not exist) |

`series_build` rows (unchanged after):

| id | episode | title |
|---|---|---|
| `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | EP01 | Tập 01 (official 6-shot) |
| `77262ec0-74ad-459d-9128-f09bb553a5dd` | EP99 | TEST BATCH 3 SHORT |
| `fd760913-0eb8-46a5-b6eb-ec2990a4b0d0` | EP01 | older 11-shot |
| `fca0c122-797e-4356-9bd1-2a70daac1ed4` | EP01 | older 24-shot |

---

## Migration

File: `migrations/370_pack_content_production_scope_anchor.sql`  
Manifest: `deploy/ubuntu/migration-files.content.txt`  
Recorded: `kit_schema_migrations` (`370_pack_content_production_scope_anchor.sql`)

What 370 does:

1. `CREATE TABLE IF NOT EXISTS pack_content.production_scope_anchor`
   - `scope_id` PK
   - `kind` `FILM` | `TEST` (TEST allowed in CHECK only; **no TEST rows inserted**)
   - `series_build_id` → `series_build(id) ON DELETE CASCADE`
   - FILM must have `series_build_id = scope_id`
2. FILM backfill: `INSERT … SELECT id, 'FILM', id FROM series_build ON CONFLICT DO NOTHING`
3. `AFTER INSERT` trigger on `series_build` so a future film still gets a FILM anchor (preserves “any `series_build` can parent OS rows”). Does not write `series_build`.
4. Drop `*_build_id_fkey` to `series_build`; add `fk_prod_*_scope` to `production_scope_anchor(scope_id) ON DELETE RESTRICT`

Second apply: table/index already exist; backfill `INSERT 0 0`; FK drop/add of the new names succeeds. Idempotent.

---

## Backfill count

| Apply | Anchors inserted | Total FILM | TEST |
|---|---|---|---|
| First | **4** | 4 | 0 |
| Second | **0** | 4 | 0 |

Every `series_build.id` has a FILM anchor with the same UUID.

---

## FK changes (after)

| Table | New constraint | Definition |
|---|---|---|
| `production_requests` | `fk_prod_requests_scope` | `build_id → production_scope_anchor(scope_id) ON DELETE RESTRICT` |
| `production_tasks` | `fk_prod_tasks_scope` | same |
| `production_bindings` | `fk_prod_bindings_scope` | same |
| `production_film_assemblies` | `fk_prod_assemblies_scope` | same |
| `production_film_assembly_shots` | `fk_prod_assembly_shots_scope` | same |

Old `*_build_id_fkey` names are gone. `production_*` row UUIDs were not rewritten (there were none).

---

## Official scope resolution

| Film | `scope_id` | `kind` | `series_build_id` | `scope_id = series_build.id` |
|---|---|---|---|---|
| EP01 official | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | FILM | same | yes |
| EP99 official | `77262ec0-74ad-459d-9128-f09bb553a5dd` | FILM | same | yes |

`ProductionScope.BuildId` is still that UUID. Application request/task/binding SQL is unchanged.

---

## `active_build_id`

| | Value |
|---|---|
| Before | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| After | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |

Unchanged.

---

## Production binding counts

| | All | Active |
|---|---|---|
| Before | 0 | 0 |
| After | 0 | 0 |

Unchanged. Official SH01–SH06 still have no Production OS KEYFRAME bindings.

---

## GET `/series/builds`

`GET /api/content/series/builds?code=FAMIXA` after 370 (KIT_MKT admin): same four ids — official EP01, EP99, and the two older EP01 builds. No extra row. No TEST film.

---

## GET `/actions` SH02

`GET /api/content/production/shots/4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f/EP01-SC01-SH02/actions`

| Field | After 370 |
|---|---|
| `currentState` | `EMPTY` |
| CURRENT / VISIBLE / ACCEPTED | null |
| CREATE_VIDEO `available` | `false` |
| CREATE_VIDEO `requiredInputs` | ApprovedKeyframe, Story, ActingBeat, VisualContract, Timing, FrozenProviderModel |

No OS KEYFRAME on official SH02. CREATE_VIDEO stays closed.

The process listening on `:5290` during this probe was a **pre-rebuild** binary and reported `blockReason=MISSING_DEPENDENCY`. Current source + in-memory contract suite use `MISSING_KEYFRAME` for the same missing KEYFRAME case. Checkpoint 1 did not change GET `/actions` code. Unavailable + empty current state is the FILM behavior that must not change.

---

## Regression tests

`dotnet test tests/KitPlatform.Content.Tests --filter ProductionPipelineV1Tests`

| Suite | Result |
|---|---|
| Work execution T01–T24 | pass |
| Pipeline + fixture slice | pass |
| CREATE_VIDEO cutover | pass |
| Action contract T01–T11 | pass |
| **Scope anchor checkpoint 1** (`ProductionScopeAnchorV1Regression`) | pass |

Checkpoint 1 suite locks: schema version `370`, EP01/EP99 FILM identity, contract RequiredInputs, and re-runs the four existing Production OS suites.

---

## Writes / calls / official data

| Check | Result |
|---|---|
| DB writes | `production_scope_anchor` 4 FILM rows; `kit_schema_migrations` 370. No `series_build` / `series_pilot` / Canon / VA / `graph_json` / `production_requests|tasks|bindings` writes |
| Paid provider calls | 0 |
| Image generation | 0 |
| Official film pixels / graph | unchanged |
| UI | unchanged |

---

## Files

Created:

- `migrations/370_pack_content_production_scope_anchor.sql`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionScopeAnchorRules.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionScopeAnchorV1Regression.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_TEST_SCOPE_FOUNDATION_CHECKPOINT_1_REPORT.md`

Modified:

- `deploy/ubuntu/migration-files.content.txt`
- `ProductionWorkIds.SchemaVersion` → `370`
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs`

---

**PASS** — FILM Production OS identity and behavior hold after the FK retarget.
