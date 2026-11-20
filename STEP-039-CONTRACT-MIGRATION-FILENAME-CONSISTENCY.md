# STEP 039 — Contract & Migration Filename Consistency

**Mode:** Read-only evidence. No implementation. No manifest decision.

**Baseline:** `d54652d4a7734b3116db62bd356a831f9942fc73` (`HEAD` at inspection). Every cited path was read from that commit with `git show` or `git grep`. The working-tree copy of `deploy/ubuntu/migration-files.content.txt` was not opened.

## Scope and queries

Inspected only the STEP 038 references for migration 383, the content migration runner those references identify, and the five Work B paths.

| Query | Result used |
|---|---|
| `git grep -n 370_pack_content_article_series <baseline>` | One file: the article-series smoke. |
| `git grep -n 383_pack_content_article_series <baseline>` | No line. |
| `git grep -n 371_pack_content_brand_narrative <baseline>` | One file: `BrandNarrativeFitTests.cs`. |
| `git grep -n 384_pack_content_brand_narrative <baseline>` | No line. |
| `git grep -n NarrativeJson <baseline> -- *.cs` | One line: `ContentArticleSeriesService.cs:724`. |
| `git grep -n BuildMigrationSql <baseline>` | `BrandNarrativeCatalog.cs:19` and `BrandNarrativeFitTests.cs:111`. |
| `git grep -n migration-files.content.txt` under `*.cs`, `*.ps1`, `*.sh`, `*.ts`, `deploy/ubuntu`, `scripts`, `docs` | Content runner, smoke, catalog header, one repository error string, and docs that name the manifest. |
| `git grep -l migration-files` under `scripts`, `deploy`, `src` | Runner candidates. The content apply script is `deploy/ubuntu/run-kit-migrations-content.sh`. |
| `git cat-file -e` and `git ls-tree --name-only` for `migrations/370_pack_content_article_series.sql`, `migrations/371_pack_content_brand_narrative_dna.sql`, `migrations/371_inventory_stock_review.sql`, `migrations/383_…`, `migrations/384_…` | Presence or absence at the baseline. |
| Static extraction of `BuildMigrationSql()` literals against blob `5aa385dd` | Statement skeleton and decoded JSON values. `BuildMigrationSql()` was not executed. No build, no test, no database. |

Blobs at the baseline:

| Path | Blob |
|---|---|
| `migrations/383_pack_content_article_series.sql` | `1c8f2117f09a936b637ea1024ceb326b2e3f568f` |
| `migrations/384_pack_content_brand_narrative_dna.sql` | `5aa385dd58e4e17ec580487888f12dc4755db1dc` (63352 bytes) |
| `client/admin/src/modules/content/content-article-series.smoke.ts` | `99d8fe1be7308b22e7da105ff88715ca2bf6fc64` |
| `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentArticleSeriesRepository.cs` | `98f7a7c90c16ef28f5799d803ac4b47e63d7f181` |
| `deploy/ubuntu/run-kit-migrations-content.sh` | `4239a6537fd7d8d23ae3920ad0a05528ad479388` |
| `src/Packs/Content/KitPlatform.Packs.Content.Application/BrandNarrativeCatalog.cs` | `538463ac0e6a0486be77e96e82a7b493e0da745e` |
| `src/Packs/Content/KitPlatform.Packs.Content.Application/BrandNarrativeDna.cs` | `d663a0b549c74a9c0fa38441ca85179794d59c62` |
| `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentArticleSeriesService.cs` | `54450d260d8261db53164ffb0cc462d54c786bf3` |
| `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentRepository.cs` | `23a41804858820823db5dae979225c01bf37cb87` |
| `tests/KitPlatform.Platform.Tests/BrandNarrativeFitTests.cs` | `436b794ecf5ead42b0a7a795af092dffba551ca8` |

`migrations/370_pack_content_article_series.sql` is absent. `migrations/371_pack_content_brand_narrative_dna.sql` is absent. The tracked `371_` migration is `migrations/371_inventory_stock_review.sql`.

## Work A — Migration 383 filename contract

### Smoke filename and manifest assertion

**VERIFIED.** `content-article-series.smoke.ts` reads the migration by a relative path whose last segment is `370_pack_content_article_series.sql` (line 19). It then asserts two substrings of that file text: `pack_content.content_series` (line 41) and `UNIQUE (series_id, episode_no)` (line 42). It reads `deploy/ubuntu/migration-files.content.txt` (line 20) and asserts that the manifest text includes `370_pack_content_article_series.sql` (line 44).

The same smoke is the only tracked hit for `370_pack_content_article_series`. There is no tracked hit for the filename `383_pack_content_article_series.sql`.

**VERIFIED.** The tracked 383 file contains both substrings the smoke checks: `CREATE TABLE IF NOT EXISTS pack_content.content_series` at line 5, and `CONSTRAINT uq_content_episode_series_no UNIQUE (series_id, episode_no)` at line 58. Its first line is `-- KitPlatform 370: Article Content Series Engine V1`. The comment number and the filename prefix differ. The smoke opens a path, so the comment label does not satisfy line 19.

### Application SQL

**VERIFIED.** `ContentArticleSeriesRepository.cs` is the application repository that names the tables. It does not name a migration file.

Series statements address `pack_content.content_series`:

- Counts and selects from line 87 and line 94.
- `INSERT` at line 179 with `brand_id`, `source_package_id`, `code`, `name`, `description`, `objective`, `audience`, `core_message`, `status`, `episode_count`, `current_episode_no`, `start_date`, `end_date`, `blueprint_json`.
- `UPDATE` at line 222.
- `DELETE` at line 257.

Episode statements address `pack_content.content_episode`:

- `INSERT` at line 299 with `ON CONFLICT (series_id, episode_no)` at line 306.
- Further `SELECT`, `UPDATE`, and `DELETE` at lines 333, 355, 395, 407, 423, 426, 440, and 471.

Those statements are a table contract. They are silent on whether the creating file is named `370_…` or `383_…`.

### Filename resolution

**VERIFIED.** The content apply script is `deploy/ubuntu/run-kit-migrations-content.sh`. It sets `LIST` to `migration-files.content.txt` (line 20). For each manifest line it strips a `#` comment, skips blanks, and sets `path="$MIGRATIONS/$file"` (lines 48–56). A missing path exits 1 (lines 57–60). The script contains no prefix rewrite and no alias from `370_` to `383_`.

`git grep` of `BuildMigrationSql` finds the narrative catalog only. No tracked generator emits `383_pack_content_article_series.sql` or `370_pack_content_article_series.sql`.

### Classification

**VERIFIED.** Nothing in the runner or in a tracked generator maps `370_pack_content_article_series.sql` to `383_pack_content_article_series.sql`. The runtime filename contract is the literal manifest line plus the migrations directory.

**INFERENCE.** The `370_` / `383_` mismatch is an isolated stale test reference. The only tracked consumer of the `370_` filename is the smoke, including its manifest assertion. The SQL text that smoke checks is the text of the tracked `383_` file, whose header comment still says `KitPlatform 370`. Which of those two names a later change should keep is **NOT DECIDED** here.

Deployment status of 383 is a separate question and was not inspected.

## Work B — Migration 384 contract consistency

### `BrandRow` and `GetBrandAsync`

**VERIFIED.** `ContentRepository.BrandRow` (lines 33–49) declares `Code`, `Name`, CTA fields, `OperationalBrief`, `ToneJson`, `VisualKitJson`, and `UpdatedAt`. It does not declare `NarrativeJson`. The class is `sealed` and is not partial. The `*.cs` search for `NarrativeJson` has no other declaration.

**VERIFIED.** `GetBrandAsync` (lines 278–294) selects `code`, `name`, CTA columns, `operational_brief`, `tone_json`, `visual_kit_json`, and `updated_at` from `pack_content.brand`. The select list does not include `narrative_json`. The same column list is what `ListBrandsAsync` selects (lines 260–271). `InsertBrandAsync` (lines 297–310) and `UpdateBrandAsync` (lines 313–333) write `tone_json` and `visual_kit_json` and do not write `narrative_json`.

### How the series service obtains the narrative value

**VERIFIED.** `ContentArticleSeriesService._repo` is a `ContentRepository` (line 21). `CallBlueprintAiAsync` loads the brand with `GetBrandAsync` (lines 691–692) and then calls `BrandNarrativeFit.Parse(brand.NarrativeJson)` (line 724). `Parse` (`BrandNarrativeDna.cs` lines 138–151) deserializes a JSON string with camelCase options and returns null for blank, `{}`, `null`, or a document without territories.

That call is the only `NarrativeJson` use. The service does not call `BrandNarrativeCatalog.ByCode` or `BuildMigrationSql`. `CreateAsync` (lines 75–84) calls `BrandNarrativeFit.SeriesEntryBlock` on the package fit’s territory. That gate reads package JSON, not `pack_content.brand.narrative_json`.

**VERIFIED.** The service asks `BrandRow` for a member the tracked type does not declare, and `GetBrandAsync` does not select the column migration 384 adds. This step did not run the compiler. A successful compile of that call is **UNKNOWN**. The source contract among `BrandRow`, `GetBrandAsync`, and the service call is already inconsistent.

### What `BuildMigrationSql()` generates

**VERIFIED** from `BrandNarrativeCatalog.cs` lines 19–51. The method does not take a filename. It appends:

1. Header lines `-- KitPlatform 371: Brand Narrative DNA V1`, `-- Manifest: deploy/ubuntu/migration-files.content.txt`, and `-- Adds narrative_json on pack_content.brand. Does not touch series, episodes, or content packages.`
2. `ALTER TABLE pack_content.brand ADD COLUMN IF NOT EXISTS narrative_json JSONB NOT NULL DEFAULT '{}'::jsonb`.
3. The column comment `Authoritative Brand Narrative DNA. Operational brief must not override territory decisions.`
4. One `UPDATE` per `BrandNarrativeCatalog.All` entry, in list order `KitTech`, `Novixa`, `Famixa`, `VanDinhTra`, `XuanHoa`, `ThaiNguyenLife`. Each sets `narrative_json` from `BrandNarrativeFit.Serialize(dna)`, dollar-quoted as `$narrative$`, casts to `jsonb`, sets `updated_at = NOW()`, and filters `WHERE code = '<BrandCode>'`. A literal `$narrative$` inside the JSON would be rewritten to `$ narrative $` (line 34).
5. A final `UPDATE` for `code = 'tnlife'` that sets `default_cta_url = 'https://thainguyenlife.vn'` only when `COALESCE(default_cta_url, '') NOT ILIKE '%thainguyenlife.vn%'`.

`All` order and the six `new(...)` calls fix the codes `kittech`, `novixa`, `famixa`, `vandinhtra`, `xuanhoa`, `tnlife`. `Serialize` (`BrandNarrativeDna.cs` lines 126–155) uses camelCase, `WhenWritingNull`, and `WriteIndented = false`. It does not set `Encoder`. `Guard` is omitted when null. Famixa is the only constructor that passes `new NarrativeGuard(...)` (catalog lines 332–334).

Newlines come from `AppendLine`, so they are `Environment.NewLine` at runtime.

### Generated SQL versus migration 384

`BuildMigrationSql()` was not executed. The comparison extracted the file and the catalog source.

**VERIFIED.** Blob `5aa385dd` is LF-only: 63352 bytes, 45 LF, 0 CR, and it ends with the CTA line’s newline. Replacing each `$narrative$…$narrative$` payload with a marker leaves this skeleton, which is the same text the generator appends, including the `KitPlatform 371` header and the conditional `tnlife` CTA:

- Six updates, codes in catalog order.
- CTA URL `https://thainguyenlife.vn`. The file contains that URL once and does not contain `https://thainguyen.life`.
- The delimiter rewrite did not apply: the file contains `$narrative$` 12 times and `$ narrative $` zero times.

**VERIFIED.** Decoding the six JSON payloads and walking each brand constructor’s string and integer literals in source order produced zero differences:

| Constructor | Tokens | Differences |
|---|---|---|
| `KitTech` | 172 | 0 |
| `Novixa` | 181 | 0 |
| `Famixa` | 186 | 0 |
| `VanDinhTra` | 173 | 0 |
| `XuanHoa` | 180 | 0 |
| `ThaiNguyenLife` | 172 | 0 |

Decoded shape matches the records. Top-level keys are `version`, `brandCode`, `coreStatement`, `primaryAudience`, `secondaryAudience`, `territories`, `hardBoundaries`, `mustNotBecome`, plus `guard` on `famixa` only. Territory counts are 18, 19, 18, 18, 19, 18. Famixa’s guard values are the two arrays at catalog lines 333–334. The file’s JSON stores non-ASCII characters and `&` as `\u` escapes (`\u` appears 4193 times in the payloads, `\u0026` appears 10 times, and a raw `&` appears 0 times in the payloads).

**VERIFIED semantic result.** Statement text, brand order, decoded JSON values, and the `tnlife` CTA in migration 384 match what `BuildMigrationSql()` writes around `Serialize`. No semantic SQL difference was found.

**UNKNOWN byte identity.** The method was not run. Two byte variables remain, and neither changes the SQL meaning:

- The blob is LF. The method uses `AppendLine`.
- The blob’s JSON uses `\u` escapes. `Serialize` does not set `Encoder`, and this step did not observe what net10.0 (`KitPlatform.Packs.Content.Application.csproj` at the baseline) emits for that unset option.

The generator output and the file both carry the header `KitPlatform 371`. Neither body names `384_pack_content_brand_narrative_dna.sql`.

### Tracked test path

**VERIFIED.** `Migration_sql_matches_catalog_and_fixes_tnlife_cta` (`BrandNarrativeFitTests.cs` lines 109–128) calls `BuildMigrationSql()`, then `FindRepoFile("migrations", "371_pack_content_brand_narrative_dna.sql")`, then `File.WriteAllText` of the generated string, then reads that path back. `Assert.Equal(sql, onDisk)` compares the generated string with the bytes just written. The test also asserts `narrative_json`, `https://thainguyenlife.vn`, and the absence of `https://thainguyen.life`. It does not read `migrations/384_pack_content_brand_narrative_dna.sql`.

**VERIFIED.** `migrations/371_pack_content_brand_narrative_dna.sql` is absent at the baseline. `FindRepoFile` (lines 134–140) walks parents until a `migrations` directory exists, then joins that filename. The test expects that path as a write target. It does not require the file to exist before `WriteAllText`. The test was not run.

## Smallest safe dispositions

These are the dispositions the evidence can support. None is selected.

### 383

| Disposition | What the evidence says about it |
|---|---|
| List the tracked name `383_pack_content_article_series.sql` on `migration-files.content.txt` | The runner would open that path literally. The smoke would still require the string `370_pack_content_article_series.sql` in both the filesystem path and the manifest. No tracked generator emits the `383_` name. |
| Leave `383_` unlisted | That matches the baseline manifest silence established in STEP 038, and no runner alias demands the file. `ContentArticleSeriesRepository` would still issue SQL against `pack_content.content_series` and `pack_content.content_episode`, whose only tracked `CREATE TABLE` is this file. |
| List `370_pack_content_article_series.sql` | That path is absent at the baseline. The runner exits when the manifest names a missing file. |

The filename question is settled as a stale smoke reference plus a literal runner. The include-or-leave question is not settled. A rename is not a disposition of this step.

### 384

| Disposition | What the evidence says about it |
|---|---|
| List the tracked name `384_pack_content_brand_narrative_dna.sql` | The file’s statements and decoded JSON match the catalog generator, including the conditional CTA `https://thainguyenlife.vn` for `tnlife`. The header inside the file still says `KitPlatform 371`. Listing `384_` does not create `371_pack_content_brand_narrative_dna.sql`, and it does not make `GetBrandAsync` select `narrative_json`. |
| Leave `384_` unlisted | `BrandNarrativeCatalog` remains the in-memory source. `GetBrandAsync` still does not read `narrative_json`. `ContentArticleSeriesService` line 724 still names `NarrativeJson` on `BrandRow`. Leaving the file unlisted does not remove that call. |
| List `371_pack_content_brand_narrative_dna.sql` | That path is absent. The tracked `371_` file is `371_inventory_stock_review.sql`. |

`BrandRow` / `GetBrandAsync` / the service call disagree with the column regardless of which manifest line is chosen. Fixing that disagreement is outside both dispositions.

## Remaining unknowns that affect the Architect decision

| Unknown | Why it still matters |
|---|---|
| Whether any database has applied 383 or 384 | No database session. A tracked file is not deployment status. |
| Contents of the dirty `deploy/ubuntu/migration-files.content.txt` | Not read. This step cannot say whether a local edit already names either file. |
| Byte identity of a live `BuildMigrationSql()` call with blob `5aa385dd` | Not executed. Semantic SQL matches. Byte equality is still open because of `AppendLine` versus LF and the unset JSON encoder. It matters only if the decision treats regenerator bytes as a condition. |
| Whether `brand.NarrativeJson` compiles | Not built. The member is absent on the sealed `BrandRow`. The source contract is inconsistent either way. |

Manifest ownership, the application’s missing `NarrativeJson` member, and database deployment status stay separate questions.

## Closing state

- Migration 383 manifest disposition: `NOT DECIDED`
- Migration 384 manifest disposition: `NOT DECIDED`
- D-05 implementation permission: `NOT GRANTED`
- Migration prefix: `NOT SELECTED`
- Phase 1.5: `NOT PASS`
