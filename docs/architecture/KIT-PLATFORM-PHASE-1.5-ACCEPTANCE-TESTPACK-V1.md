# KIT Platform — Phase 1.5 Acceptance TestPack V1

Status: named test list for Step 028 AG-G4-02, AG-G5-02, and AG-G5-03.  
Authority: Decision Register PO-16 §9. Checklist: `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md`.  
This list does not open a production database, does not deploy, and does not call a paid provider.

Run every command from the inspection root in PO-16 §2, on a clean checkout of the accepted source. A dirty worktree result is not a result for this list.

The disposable database, when a command requires `KPA_PHASE15_DISPOSABLE_PG`, is `127.0.0.1` port `54341`, database `kpa_phase15_once`. The connection string must not contain `Port=5432` or `Database=kitplatform`. The tests assert `current_database() = kpa_phase15_once` and `inet_server_port() = 54341`.

NU1903 warnings for `Newtonsoft.Json` 10.0.3 do not fail a row. Two Local OS tests are outside this list. Step 028 does not require a green full `KitPlatform.Platform.Tests` run.

## Commands

| # | Group | Command | Acceptance |
|---|---|---|---|
| 1 | Build | `dotnet build KitPlatform.slnx --nologo` | Exit 0. 0 errors. |
| 2 | Customer isolation | `dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --filter FullyQualifiedName~ProductionCustomerIsolationTests --nologo` with `KPA_PHASE15_DISPOSABLE_PG` set | Exit 0. Cross-customer read and mutation are not found. A NULL owner stays hidden and unchanged. `series_build` row count is unchanged. |
| 3 | HTTP ownership and production DI | `dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --filter FullyQualifiedName~ProductionOwnershipHttpTests --nologo` with the same variable | Exit 0. Host is `WebApplicationFactory<Program>`. Missing, foreign, and NULL-owner video routes return 404 with body `Production không tồn tại.` Content services resolve without a test-host repository registration. |
| 4 | Shared series filter | `dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --filter FullyQualifiedName~ProductionTenantActionFilterSharedSeriesTests --nologo` | Exit 0. The six attributes remain. An active tenant does not establish ownership of a series build id. |
| 5 | Named video read | `dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --filter FullyQualifiedName~ContentSeriesTakeNamedReadTests --nologo` | Exit 0. A named file inside `veo/` or `fal/` is readable. A path outside the video root is not. |
| 6 | Application regression | `dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj --nologo` | Exit 0. |
| 7 | Content regressions already on the source | `dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --filter "FullyQualifiedName~BrandNarrativeFitTests\|FullyQualifiedName~NarrativeFitIntegrityTests\|FullyQualifiedName~ContentQualityGateTests\|FullyQualifiedName~ContentArticleSeriesTests" --nologo` | Exit 0. |
| 8 | Manifest and schema read | Read the three manifests and migrations 387 and 388. Confirm 376, 385, and 386 are absent from the tree and from the three manifests. | 228/229 only in prod. 230/231 only in family-os. 383/384/387/388 only in content, each once. No `ALTER` of `pack_content.series_build`. No backfill `UPDATE`. |

## Evidence

Record the accepted commit hash, each exit code, and the pass/fail counts next to the command. Do not cite stash, an untracked migration, or a gitignored report as proof that schema or API enforcement exists.
