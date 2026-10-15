# KIT PLATFORM ARCHITECTURE DECISION REGISTER V1

Status: DECISION PHASE  
Basis: KIT PLATFORM MASTER ARCHITECTURE V1  
Forensic Basis: KPA-01 → KPA-07  
Implementation: NOT STARTED

Later evidence (do not treat the header above as the current implementation state): `docs/forensics/PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01.md` and `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md`. Option tables below stay as the original menus. Each ADR status block now carries the synchronized current state from `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md`. A status of DECIDED is not the same as a status of IMPLEMENTED.

This register converts Master Architecture V1 §20 / §22 unresolved decisions into explicit ADRs.

It does not implement. It does not pick a winner where evidence only describes options. It does not reopen settled KPA findings.

Settled invariants (do not re-decide here) are restated in the final section.

---

# ADR-01 — graph_json

## Status

**DECIDED** (authority split). Full deletion of historical media keys is **not** implemented.

Synchronized from `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` §7. The option menu below is the original question. It is not reopened.

- `series_build.graph_json` is the Famixa script and story document.
- Production OS is the official authority for production execution, artifacts, bindings, and official preview state.
- Server-side media-key cutover is authoritative for new writes. Incoming `acceptedTake`, `takeUrl`, `previewUrl`, `lipsyncUrl`, and turbo or lipsync task fields must not become new graph media authority.
- Historical media keys may remain in stored JSON.
- This is Famixa. It is not a Core Script type.

## Question

Should Famixa `series_build.graph_json` become **script-only authority**, or **continue as dual-write / compatibility state** (including take URLs / `acceptedTake` / runs)?

## Current Evidence

KPA-07 A; Master Architecture §9 A, §20.1.

- Writer: `ContentSeriesPilotService.UpsertBuildAsync` → `ContentRepository.UpsertSeriesBuildAsync`. Client: `ContentFamixaSeriesTab.persistState` → `queueServerSave` persists the full pilot graph (including `runs`).
- OS does **not** write `graph_json`.
- Script/lines remain product state. Voice ingress canons still read `graph_json.lines|episode.shots.dialogueSegmentIds`.
- `LegacySeriesGraphAdapter` classifies `acceptedTake.url`, `takeUrl`, `previewUrl` as **non-authoritative** for CURRENT/VISIBLE/ACCEPTED (`IsAuthoritative() => false`) and `MayDriveNewBinding` is always false.
- Episode assembly **rejects** `graph_json` as timing. CREATE_IMAGE ingress does not read graph.
- `ProductionProgressService` still parses `graph_json`.
- Client still writes take-related fields into graph (ADR-06).

Repository evidence is **sufficient to describe options**. It is **not** sufficient to retire the graph writer: desk save, voice ingress, and progress still depend on it.

## Current Authority

**Split (KPA-07).** Script/lines = graph product state. Execution artifacts/bindings = Production OS. Graph take URLs / `acceptedTake` = LEGACY/COMPATIBILITY, still persisted.

## Options

A. **Script-only** — graph keeps episode/shots/lines/script locks; take/final/URL fields stop being written and stop being read as media authority.

B. **Continue dual-write / compatibility** — keep current split; OS remains execution authority; graph remains the series document including residual take state.

No third option is evidenced (OS does not replace the series document today).

## Consequences

| Option | Architecture | Security | Data | Product | Migration | Operational |
|--------|--------------|----------|------|---------|-----------|-------------|
| A | Single media SoT on OS; graph = script | Fewer URL-as-identity paths | Must stop persisting takes in `runs`; historical URLs remain in old rows | Desk/voice-ingress/progress must switch readers | High — voice canons + progress + persist shape | Operators lose graph-as-take archaeology |
| B | Stage 1 continues; no single SoT | Residual URL confusion | Dual-write remains | Least product change | Low now | Dual worlds remain (KPA-07 A) |

## Dependencies

ADR-06 (remaining UI writes) must be closed or accepted as residual if choosing A. ADR-02 is independent. ADR-03 does not choose A vs B but scopes whichever remains.

## Decision Owner

Famixa Product + Platform Architecture. UI (desk persist). API (`UpsertBuildAsync`).

## Decision

**DECIDED.** Authority split in the Status block. Client-field cleanup stays with ADR-06.

---

# ADR-02 — legacy video_generation_execution

## Status

**DECIDED** for official execution authority. **Retirement of the legacy surface is not implemented.**

Synchronized from `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` §8. `VideoGenerationExecutionService.ExecuteAsync` throws `LEGACY_EXECUTION_CLOSED` before a provider call.

- Production OS is the sole official Famixa production execution authority.
- `video_generation_execution` remains a compatibility surface for read, preflight, approve, and reject.
- The execute path does not submit a provider job, establish an official binding, or create an official artifact.
- Removing routes, UI, provider registration, and the legacy table is still deferred. That deferral is not a second execution authority.

## Question

What is the intended future of `pack_content.video_generation_execution`, `IVideoGenerationProvider`, live video-generation-execution APIs, and `video_generation_execution_event`?

A. RETIRE  
B. COMPATIBILITY ONLY (no new callers; existing may read)  
C. CONTINUE PARALLEL

## Current Evidence

KPA-05; KPA-07 E; Master Architecture §9 E, §20.2.

**Writers**

- `VideoGenerationExecutionService` via `ContentController`: GET/POST preflight, execute, approve, reject, artifact video.
- Routes: `/api/production/shots/{id}/video-generation-execution/*` and `/api/content/video-engine/shots/{id}/video-generation-execution/*`.
- DI: `IVideoGenerationProvider` → `RunwayVideoGenerationProvider`; `IVideoGenerationExecutionService` registered.

**Readers / UI callers (new callers can still be introduced)**

- `client/admin/src/shared/api/content.api.ts` — `preflight/execute/approve/reject/fetch` helpers.
- `ContentKitVideoVideoGenerationExecutionCard.tsx` — execute/preflight buttons.
- `ContentKitVideoDirectorProductionWorkspace.tsx` — full preflight/execute/approve/reject + blob play.
- `ContentFamixaSeriesTab.tsx` mounts the execution card (`<ContentKitVideoVideoGenerationExecutionCard />`).
- `ContentFamixaProductionBoards.tsx`, `ContentFamixaProductionOverview.tsx` — fetch generation + artifact blob.

**Runtime dependency:** independent of Production OS. Different shot id space (`video-engine` shot Guid vs OS `BuildId`+`ShotCode`). File lock: `PersistProductionExecution` (exists → locked). OS uses `PersistNamed` (overwrite). Two journals: `video_generation_execution_event` vs `production_task_events`.

**Retirement evidence:** **insufficient.** Official film uses OS, but legacy UI and APIs are live and wired into Series/Director. No arbitrator if both run (KPA-07 UNKNOWN).

## Current Authority

Official Famixa Production OS film = OS. Legacy path = still executable. **No single winner** if both fire.

## Options

A. RETIRE — remove or fail-closed the execute/approve APIs and UI.  
B. COMPATIBILITY ONLY — keep GET/read; refuse new execute; freeze UI.  
C. CONTINUE PARALLEL — current state, labelled.

## Consequences

| Option | Architecture | Security | Data | Product | Migration | Operational |
|--------|--------------|----------|------|---------|-----------|-------------|
| A | One execution plane | One provider path | Must map or abandon `video_generation_execution` rows | Director/Series cards break until replaced | High | Must cut Runway-via-this-API |
| B | Strangler freeze | Reduces dual execute | Rows remain readable | Progress/overview can still show old executions | Medium | Policy + API guards required (not designed here) |
| C | Dual-live remains | Dual credential/execute risk | Two films possible | No UI break | None now | Permanent confusion |

## Dependencies

ADR-01 does not retire this table. ADR-04 (Runway key is Content-owned). Retirement is not blocked by ADR-03 but tenant-scoping a retired table is wasted work.

## Decision Owner

Famixa Product + API. UI (Director/Series cards). Platform Architecture (strangler classification).

## Decision

**DECIDED** for the fail-closed execute path. Option A (delete the legacy system) is **not** chosen. Option C (parallel execute) is **closed**. The remaining surface is compatibility, which is narrower than the original option B because preflight, approve, and reject still exist.

---

# ADR-03 — Tenant / Workspace model for Production

## Status

**DECIDED.** Tenant-only Production scope. Workspace is not the isolation boundary.

A status of DECIDED is not a status of IMPLEMENTED. Sentences in the CURRENT MODEL below that say production tables have no TenantId stay in this record.

Local OS is a separate dedicated single-tenant park (`docs/forensics/KIT-PLATFORM-LOCALOS-TENANT-BOUNDARY-01.md`). It is not a second universal tenancy rule. TEST_PACK and Pharmacy stay tenant-scoped (`docs/forensics/KIT-PLATFORM-PACK-ONBOARDING-02.md`, `docs/forensics/KIT-PLATFORM-PACK-ONBOARDING-03.md`).

Decision record: `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md`  
Analysis: `docs/KIT-PLATFORM-ADR-03-TENANT-WORKSPACE-DECISION-BRIEF-V1.md`

## Question

How should Production / Famixa execution be scoped so tenant isolation is real before any generic Production Engine serves multiple products?

## Current Evidence

KPA-01, KPA-03, KPA-06, KPA-07; Master Architecture §10–11, §20.3.

### CURRENT MODEL

- Tenant SoT: `public.tenants` + JWT / `ITenantContext`.
- Identity SoT: `public.users`.
- Pharmacy organization hierarchy: **branches** — not proven as generic Organization.
- `kit_workspace` exists; **semantics unresolved**.
- `production_*` and `series_build`: **no TenantId, no WorkspaceId**. Scope = `pack_content` + `series_code` / `BuildId`.
- Disk: `KitVideoVideoRoot/{folder}/{id}.mp4` — not tenant-prefixed.
- Famixa series/build is **product-owned**.

### MISSING BOUNDARY

Tenant and workspace are not on OS rows, not on `series_build`, not on PersistNamed paths. Actor on OS is VARCHAR, not user UUID.

## Current Authority

Platform tenant applies to Core APIs. Production OS isolation is **pack schema**, not tenant RLS.

## Options

(Architectural requirements only — no schema invented.)

1. **Tenant-only on Production** — every OS row and series_build must be attributable to `public.tenants`. Workspace out of scope.
2. **Tenant + Workspace** — after `kit_workspace` semantics are defined.
3. **Pack isolation only** — keep current; **forbids** a cross-product Production Engine.

## Consequences

| Option | Architecture | Security | Data | Product | Migration | Operational |
|--------|--------------|----------|------|---------|-----------|-------------|
| 1 | Enables Stage 2; Engine Stage 5 becomes *possible* later | Cross-tenant OS leak closable | Backfill/attribution of existing builds | Famixa builds become tenant-bound | High (not specified here) | Multi-tenant Content must stop sharing one `series_code` implicitly |
| 2 | Same + workspace | Finer isolation | Needs workspace definition first | Unclear Famixa meaning of workspace | Higher | Blocked on workspace decision |
| 3 | Engine extraction **unsafe** (Master Architecture Stage 5) | Current gap remains P0 | No migration | Least change | None | Do not claim generic engine |

## Dependencies

Must be decided **before** a generic Production Engine can safely serve multiple products.

Also before: generic Storage/Artifact foundation used across products (ADR-07 policy is local; tenant prefix is this ADR).

Does not by itself decide ADR-01/02/08.

## Decision Owner

Platform Architecture + Product Owners (Famixa, NOVIXA). Security (isolation requirement).

## Decision

**DECIDED — OPTION 1 — TENANT-ONLY PRODUCTION SCOPE**

Production execution must be attributable to the platform Tenant represented by `public.tenants`. Workspace is **not** a mandatory Production boundary. Workspace remains a separate KIT primitive and must not be inherited by Production Engine merely because `kit_workspace` exists.

**DECISION CLOSED ≠ IMPLEMENTATION COMPLETE.** Production is **not** tenant-safe today. GATE A is not executed. No TenantId/WorkspaceId/RLS/API change is authorized by this record.

Settled for the future engine (see decision file): Tenant MANDATORY; Workspace OPTIONAL / PRODUCT-DEPENDENT; Famixa Series/Build are not engine types; do not promote ProductionTask / ProductionArtifact / ProductionBinding / Famixa registry / FilmAssembly.

Organization ≠ Tenant ≠ Workspace ≠ Famixa Build ≠ Pharmacy branch. Extraction uses **new** types.

---

---

# ADR-04 — Credential ownership

## Status

**DECIDED** — Tenant-owned Production AI credentials

Secret storage: **DECIDED** — Option A. Ciphertext in the application database. Data-encryption key outside the database (`/etc/kit-platform/api.env` on the current VPS). External secret manager is not selected. Cipher and master-key rotation are Gate B, not an open architecture decision.

Record: `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md`  
Analysis (current-state forensic, not the decision): `docs/KIT-PLATFORM-ADR-04-CREDENTIAL-OWNERSHIP-DECISION-BRIEF-V1.md`  
Violation checkpoint: PRODUCTION-CREDENTIAL-BOUNDARY-01 — VIOLATION FOUND  
Storage closure: PRODUCTION-CREDENTIAL-STORAGE-DECISION-01-CLOSURE  
Implementation: **NOT STARTED**

## Question

Who owns AI provider credentials: product pack, platform, or tenant? May Content credentials be reused by other products?

## Current Evidence

KPA-05; Master Architecture §20.4. Read-only map:

| Credential | Owner (code) | Consumers |
|------------|--------------|-----------|
| Gemini via `pack_content.org_settings` + `Content:GeminiApiKey` / `GEMINI_API_KEY` | Content Park (`ContentParkGeminiKeyResolver`) | Content; **Pharmacy** `PharmacyConsultationAiConfigProvider` (“when tenant has no dedicated key”); **Sales** `KitSalesAiSettingsService`, `KitSalesGeminiVoice`; **Local OS** `LocalOsRewriteService.ReadContentGeminiKeyAsync`; `TenantSettingsService` |
| Vertex / Veo join | Famixa (`ContentVeoClient` — service account, not Gemini) | Official OS CREATE_VIDEO |
| Official Veo permit | Famixa product (`OfficialCreateVideoPermitAuthorizationV1`, hard-locked film) | Official path only |
| Runway / Fal / ElevenLabs / Creatomate | `ContentVideoConfig` / Content options | Famixa adapters; legacy `IVideoGenerationProvider` (Runway) |
| Family billing `ApiKey` | Family OS contract field | Not Content Gemini |

No generic credential plane. Comment on resolver: “Used temporarily by Pharmacy POS consultation.” Temporary is not a decided end state.

## Current Authority (code today — not the target)

Product-owned secrets in several places. Content Gemini key is **cross-product shared** (proven). Veo permit/credentials are Famixa-specific. Official Production HTTP transports resolve one unscoped `pack_content.org_settings` row (no `tenant_id`) plus process environment. That current state is a violation of the decision below. It is not rewritten as if tenant-owned credentials already exist.

## Options

A. **Product-owned, no sharing** — each pack has its own secret; stop Content fallback.  
B. **Tenant-owned** — tenant settings hold keys; packs resolve by tenant (not designed here).  
C. **Platform-owned credential plane** — FUTURE; not to be built in this checkpoint.  
D. **Keep current sharing** — Content key as default for Pharmacy/Sales/Local OS.

## Consequences

| Option | Architecture | Security | Data | Product | Migration | Operational |
|--------|--------------|----------|------|---------|-----------|-------------|
| A | Clear pack boundary | Stops key leak | Duplicate keys | Pharmacy/Sales/Local OS need own config | Medium | More secrets to rotate |
| B | Aligns with tenant SoT | Isolation if done correctly | Tenant settings become SoT | Cross-product UX simpler | High | Ownership on tenant admin |
| C | Stage 6-adjacent | Central control | New plane | All packs change | High | **Do not build now** |
| D | Status quo | P0/P1 credential concern remains | None | Least change | None | Shared blast radius |

## Dependencies

Not required to extract Famixa OS types. **Required** before AI credential consolidation and before treating Gemini as a platform primitive. Interacts with ADR-05 (shared primitives).

## Decision Owner

Security + Platform Architecture + Product Owners (Content/Famixa, NOVIXA/Pharmacy, Sales, Local OS).

## Decision

**DECIDED. Option B — Tenant-owned Production AI credentials.**

Formal statement:

> A credential used by official Production execution is owned by exactly one Tenant.
>
> Product Pack, Provider, ProviderId, ModelId, endpoint, execution permit, Workspace, organization-global settings, and process environment are not credential owners.
>
> The frozen execution retains a non-secret credential identity and version. Retry does not silently switch credentials. Legacy `org_settings` values are unscoped and are not assigned to a Tenant by this decision.

Secret storage is **DECIDED. Option A — encrypted database secret storage.**

Credential ciphertext is stored in the application database. The data-encryption key is stored outside the database, currently at `/etc/kit-platform/api.env`. An external secret manager is not selected. This register does not name a table, a code type, or a cipher. It does not claim that encryption, a credential domain, or a provider migration already exists.

Frozen executions pin CredentialId and CredentialVersion. Retry resolves that frozen version. Rotation creates a new version and does not rewrite frozen executions. A missing, revoked, invalid, wrong-tenant, or wrong-version credential fails closed. Forbidden fallbacks are legacy `org_settings`, arbitrary environment secrets, another tenant, another version, and another product or provider credential. Provenance may store CredentialId, CredentialVersion, ProviderId, and ModelId, and must not store secret material, the encryption key, or a decrypted payload.

The cipher is AES-256-GCM. The envelope stores a non-secret key id so a later master key can be introduced without rewriting ownership. Credential business rotation remains Gate E. The database dump does not contain the key. Legacy `pack_content.org_settings` stays transitional and is not migrated here.

Gates A–D are implemented. Official Production resolution requires an explicit CredentialId and CredentialVersion and does not fall back to `org_settings`. Freeze pins those non-secret ids on the existing input snapshot. Historical snapshots are not backfilled. Credential business rotation is not implemented. The next implementation checkpoint is PRODUCTION-CREDENTIAL-ROTATION-01. Do not build a Credential Service, vault, or platform credential plane from this record.

---

# ADR-05 — Shared platform primitives

## Status

**SPLIT. Not one closed decision.**

Synchronized from `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` §5, §6, and §9, and from `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md`.

| Plane | Current status |
|---|---|
| Identity, Tenant, Authorization, Module enablement | **KEEP SHARED.** Already the Core used by TEST_PACK, Local OS, and Pharmacy |
| Event | **OPERATING DECISION, merge not implemented.** `kit_event.event_outbox` is the named target surface. `public.platform_events` stays the compatibility write. Integration outbox stays a separate bus. Famixa journals stay out. Physical merge is deferred. Handler idempotency is **not** fully proven |
| Audit | **OPERATING DECISION, winner not chosen.** Dual-write stays. `kit_audit.activity_log` is the operational read. `public.audit_logs.id` stays the insert identity. Canonical consolidation is deferred |
| Notification | **OPEN** |
| Storage | **SHARE LATER.** No generic `IFileStore`. Do not promote Famixa storage |
| Execution | **PRODUCT-SPECIFIC.** No Production Engine |

The original "DECISION REQUIRED" line applied to the whole list. Identity, tenant, and authorization were already shared. Event and Audit now have an operating rule that is not a table merge.

## Question

How much should NOVIXA, FAMIXA, Family OS, and Local OS share of Identity, Tenant, Authorization, Audit, Event, Notification, Storage, Execution?

## Current Evidence

KPA-01, KPA-02, KPA-04, KPA-05; Master Architecture §6, §14, §20.5. Packs isolated by schema/manifest. Family OS / Local OS separate manifests. `kit_*` overlays already shared as stranglers. Shared primitives without tenant discipline repeat dual-write.

## Current Authority

Identity/Tenant/Authz = Core (`public.*` + JWT). Audit/Event/Notify = dual-write overlays. Storage/Execution = product islands.

## Options

Per primitive (not a single A/B for the whole platform):

| Primitive | Classification | Evidence |
|-----------|----------------|----------|
| Identity | **KEEP SHARED** | `public.users` SoT (KPA-01) |
| Tenant | **KEEP SHARED** | `public.tenants` SoT |
| Authorization | **KEEP SHARED** | JWT/permissions; Pharmacy RBAC must not regress |
| Audit | **DECISION REQUIRED** | Dual-write `audit_logs` + `kit_audit` (KPA-04). SHARE LATER only after one plane is chosen |
| Event | **DECISION REQUIRED** | `platform_events` / outbox + `kit_event`. Execution journals stay **PRODUCT-SPECIFIC** |
| Notification | **DECISION REQUIRED** | `kit_notify` + inbox concepts distinct from Event |
| Storage | **SHARE LATER** | No generic IFileStore (KPA-03). `/api/files` island. Do not share Famixa PersistNamed |
| Execution | **PRODUCT-SPECIFIC** | Famixa OS vs Pharmacy `IWorkflowEngine` vs `work_job`. Engine is FUTURE extraction (KPA-06) |

## Consequences

Sharing Audit/Event/Notify now without a single SoT **repeats** KPA-04 dual-write. Sharing Execution now **contaminates** Core (P0-4). Keeping Identity/Tenant/Authz shared is already proven and should not be split.

## Dependencies

ADR-03 before sharing Storage/Execution across products. ADR-04 before sharing AI credentials (not a primitive in the table; related). Family/Local OS park rules: do not merge auth with Pharmacy.

## Decision Owner

Platform Architecture + Product Owners. Security for Audit/Event. NOVIXA for Pharmacy workflow remaining product-specific.

## Decision

**SPLIT.** Identity, Tenant, and Authorization stay shared. Event and Audit have the operating dual-write rule in the Status block. Notification stays **OPEN**. Storage and Execution are not shared. The original "DECISION REQUIRED on Audit / Event / Notification" line is the first draft.  
Storage: **SHARE LATER**.  
Execution: **PRODUCT-SPECIFIC** until Stage 5.

---

# ADR-06 — Remaining UI writes (acceptedTake / FinalStore / take URLs)

## Status

**OPEN** for client and historical cleanup.

ADR-01 now closes new graph media authority. `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` §7.D still defers deleting historical media keys and removing obsolete client fields. Inventory that hidden product state exists remains sufficient. That inventory is not a decision to delete it.

The original line was: inventory is sufficient (yes, hidden state exists); what to do about it was not decided. The "what to do" part stays open.

## Question

After Production OS preview/execution, do client/API paths still write `acceptedTake`, FinalStore, graph take URLs, or other legacy final/take state?

## Current Evidence

Read-only inventory (production writers/readers; smokes omitted except where they only *forbid* OS writes).

| FILE | METHOD | HTTP/API | WRITER | DATA WRITTEN | AUTHORITY CLASSIFICATION | PRODUCTION OS RELATION | STATUS |
|------|--------|----------|--------|--------------|--------------------------|------------------------|--------|
| `ContentFamixaSeriesTab.tsx` | `persistState` → `queueServerSave` | Series build upsert (`UpsertBuildAsync`) | Director / Series desk | Full `SeriesPilotState` including `runs.*.acceptedTake`, `takeUrl`, `previewUrl`, `lipsyncUrl` | PRODUCT STATE persist / LEGACY take fields | Does **not** write `production_*` | **LIVE** |
| `ContentFamixaSeriesTab.tsx` | `onAcceptExisting` (~7506, ~7639) | via persist above | UI accept-existing | `acceptExistingTake(run)` → `acceptedTake` + remake flags | LEGACY/COMPATIBILITY take accept | Not OS `AcceptAsync` | **LIVE** |
| `ShotProductionArtifacts.ts` | `acceptExistingTake` | none (helper) | Called by SeriesTab | `{ acceptedTake: { url, n, hashes, acceptedAt } }` | Helper for graph run | OS forbids URL identity | **LIVE helper** |
| `content-famixa-series.ts` | merge ~2232 | via persist | Graph merge | `acceptedTake: base \|\| old` | Preserves take on merge | Not OS | **LIVE** |
| `ContentFamixaSeriesTab.tsx` | `assembleOneShot` ~7000 | `assembleContentSeriesCut` (legacy assemble) then IDB | Desk assemble | `saveFinalBlob(shot.id, mp4, identity)` + `stampAssemble` into `runs` | CACHE (IDB) + PRODUCT stamp | OS ASSEMBLE does not use FinalStore (smoke) | **LIVE** |
| `ShotProductionFinalStore.ts` | `saveFinalBlob` | IndexedDB `kit-famixa-shot-final` | SeriesTab assemble | Blob + `FinalArtifactIdentity` | CACHE | Not OS ASSEMBLY artifact | **LIVE** |
| `content-famixa-preview-cut.ts` | readers | none | — | Reads `takeUrl` / `lipsyncUrl` / `acceptedTake.url` / `previewUrl` | LEGACY reader | OS hydrate smokes ignore these | **LIVE read** |
| `ShotProductionDirector.ts` / `Orchestrator.ts` / `Cta.ts` / `State.ts` / `Execution.ts` | readers | none | — | `acceptedTake?.url` for accepted/CTA | Residual product/UI state | OS preview uses bindings | **LIVE read** |
| `content-famixa-final-source.ts` | patch | via persist | final-source helper | `acceptedTake: undefined` (clear) | Compatibility clear | Not OS unbind | **LIVE** |
| `LegacySeriesGraphAdapter` | n/a | n/a | none | lists fields as non-authoritative | LEGACY classification | `MayDriveNewBinding` false | **DECLARED non-authority** |
| OS execute / `ContentProductionController` | CreateVideo / Mix / etc. | `/api/content/production` | OS | artifacts/bindings | EXECUTION STATE | Authoritative for official execute/preview | **LIVE OS** |
| `ContentKitVideoVideoGenerationExecutionCard` / Director workspace | execute/approve | `/video-generation-execution/*` | Legacy UI | `video_generation_execution` | LEGACY execution | Parallel to OS (ADR-02) | **LIVE** |

OS handlers are evidenced **not** to write `acceptedTake` (cutover smokes). Hidden state is **client graph + IDB + legacy assemble**, still saved to server graph.

## Current Authority

Official execute/preview = OS. Graph/IDB takes = residual product/cache. **Both still written.**

## Options

A. Stop writing take/final fields (script-only graph — ADR-01 A).  
B. Keep writing; keep classifying as non-OS authority (ADR-01 B).  
C. Inventory-only freeze: no new writers; existing persist remains — not separately evidenced as an implemented mode.

## Consequences

Same fork as ADR-01. Leaving writers live means “OS is official” is **path-dependent**, not global.

## Dependencies

Decision is a **subset / input** of ADR-01. ADR-02 is a separate live writer (legacy execute). ADR-08 unused FINAL binding is unused; IDB FinalStore is this ADR.

## Decision Owner

Famixa Product + UI. Platform Architecture (classification). API (graph upsert shape).

## Decision

**OPEN** for cleanup.  
**Fact recorded:** hidden product state **still exists** beside Production OS. New graph media authority is closed by ADR-01. Deleting historical keys and client fields is not decided. The inventory row that calls legacy video execute a parallel live path is historical: `ExecuteAsync` now throws `LEGACY_EXECUTION_CLOSED`.

---

# ADR-07 — Preview SHA verification

## Status

**IMPLEMENTED** in Famixa Production OS. Not a Core artifact primitive.

`OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes` hashes the physical bytes and compares them to `ProductionArtifact.ByteSha256`. Mismatch returns null. `ProductionCreateVideoService.ReadPreviewMediaAsync` uses that helper (`src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs`). Master Architecture §28.1 and `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` §10 record the same fact.

The evidence block below that says preview does **not** rehash is the original analysis. It is historical. The implemented path is the rehash. `PersistNamed` can still overwrite bytes; the preview read now fails closed instead of streaming the mismatch. This does not add `IFileStore` or a Core Artifact type.

## Question

Should official media preview re-hash physical bytes against `ProductionArtifact.ByteSha256` before streaming?

## Current Evidence

KPA-03, KPA-07 H; Master Architecture §20.7.

**CURRENT behavior (proven this checkpoint — no contradiction of KPA-07):**

- `ContentProductionController.GetPreviewMedia` → `ReadPreviewMediaAsync`.
- `ProductionCreateVideoService.ReadPreviewMediaAsync`: `GetPreviewAsync` (binding → StorageKey) then `_files.Read(preview.StorageKey)`. **No `Sha256Hex` compare.**
- Same for `ReadCurrentVoiceMediaAsync`.
- Controller returns `File(bytes, mime)` if length ≥ 32.
- Official assemble **does** re-read and compare SHA (`FailedIdentity` on mismatch) — KPA-07 C/H.
- `PersistNamed` = `File.WriteAllBytes` overwrite. Verified **row** cannot change SHA/key. Disk can change under the same key.

KPA-07 said preview rehash was UNKNOWN. Repository now **proves it does not rehash**. That is a **filled evidence gap**, not a contradiction of authority classification (row vs disk still diverge).

## Current Authority

Preview identity = binding → ArtifactId → StorageKey. Integrity at stream time = **trust disk bytes**. Integrity at assemble = **rehash**.

## Options

A. **Re-hash before stream** — compare to `ByteSha256`; fail-closed if mismatch.  
B. **Trust verified storage invariant** — current behavior; assumes PersistNamed is not overwritten.  
C. **No other evidenced policy** (e.g. ETag-only) exists in Production OS preview.

Do not choose from generic best practice.

## Consequences

| Option | Architecture | Security | Data | Product | Migration | Operational |
|--------|--------------|----------|------|---------|-----------|-------------|
| A | Aligns preview with assemble integrity | Detects PersistNamed overwrite (P0-2) | No schema | Preview 404/400 on tamper | Code policy later | CPU on each GET |
| B | Leaves P0-2 as silent playback risk | Wrong bytes under valid ArtifactId | None | Current UX | None | Must prevent overwrite operationally |
| C | — | — | — | — | — | Not evidenced |

## Dependencies

ADR-03 (tenant path) does not choose A vs B. Storage foundation (Stage 3) should not promote PersistNamed regardless. Can be decided independently of ADR-01.

## Decision Owner

Famixa Product + API (preview endpoint). Security (integrity). Platform Architecture (artifact vs disk).

## Decision

**IMPLEMENTED** as Famixa preview integrity. The original options A/B/C are historical. Current official preview follows the rehash path. No Core type was added.

---

# ADR-08 — ASSEMBLE_EPISODE

## Status

**IMPLEMENTED** as a Famixa Production OS action. **Not a Core capability.** The original letters A/B/C were not voted in an ADR. Further product policy (retire the action, or expand who may call it) stays **OPEN**.

`migrations/380_pack_content_production_assemble_episode_action.sql` allows `ASSEMBLE_EPISODE`. `ContentProductionController` calls `AssembleEpisodeOsAsync`. The table below that says the controller throws `HiddenFallbackForbidden` and that migration 372 omits the action is historical.

Shot `ASSEMBLE`, legacy desk assemble, and `FilmAssembly` stay separate Famixa meanings. Episode assembly is not promoted to Core.

## Question

What is the intended product/API status of Famixa episode assembly (`ASSEMBLE_EPISODE`)?

## Current Evidence

KPA-06, KPA-07 I; Master Architecture §20.8; CK29 tests (not re-run here).

| Axis | Status |
|------|--------|
| CURRENT | Famixa-only. Output role = ASSEMBLY. Episode binds CURRENT/VISIBLE:ASSEMBLY. `FINAL` binding unused. |
| IMPLEMENTED | Yes — `AssembleEpisodeOsAsync` in `ProductionCreateVideoService.Actions`; contract/tests exist |
| EXPOSED | `ContentProductionController` POST `requests` accepts `Action`; **throws** `HiddenFallbackForbidden` if `ASSEMBLE_EPISODE` |
| CALLABLE | **No** via Director/production POST |
| TESTED | Yes (isolated regressions; live FFmpeg not authorized by prior checkpoints) |
| SQL-ALLOWED | **No** — mig 372 CHECK has `ASSEMBLE`, not `ASSEMBLE_EPISODE` |
| UI-ALLOWED | **No** — Director POST refused |
| TARGET STATUS | **Not decided** |

Shot `ASSEMBLE` remains live (count==1 MIX remux). Legacy `/content/series/assemble` remains a desk path (ADR-06).

## Current Authority

Episode concat is **not** a live official API. Shot OS ASSEMBLE and client FinalStore assemble are different paths.

## Options

A. Keep refused / test-only until product enables.  
B. Enable later as Famixa-only (still not Core) — would need SQL + API + UI; **not in this phase**.  
C. Abandon episode action; keep shot ASSEMBLE only.

## Consequences

| Option | Architecture | Security | Data | Product | Migration | Operational |
|--------|--------------|----------|------|---------|-----------|-------------|
| A | Matches current refuse | No accidental concat | No SQL change | Episode film stays manual/shot-wise | None | Official EP01 episode concat off |
| B | Still Famixa | Paid/FFmpeg risk if enabled carelessly | SQL CHECK + rows | Product feature | **Do not do now** | Needs approved execute checkpoint |
| C | Simpler OS surface | — | Dead C# vs SQL already diverge | Lose episode contract | Code later | Tests become historical |

## Dependencies

Not required for Tenant Engine. Not Core. Enabling is blocked on product + SQL + live execute policy — **outside this register**. ADR-01 graph timing already rejected for episode.

## Decision Owner

Famixa Product + API. Not Platform Core.

## Decision

**Fact synchronized:** the action is callable and SQL-allowed (migration 380). It is Famixa-only.

The historical sentence "not callable, not SQL-allowed, do not add SQL CHECK" described the register's first draft. It is not the current schema.

A further product letter (retire, or widen the callers) is **OPEN**. Do not promote the action to Core.

---

# Decision matrix

| ADR | Decision | Status | Blocking | Depends On |
|-----|----------|--------|----------|------------|
| 01 | graph is script/story; Production OS is execution/artifact/binding | **DECIDED** | Historical key deletion still open (ADR-06) | OPEN-AUTHORITY §7 |
| 02 | OS is official execution; legacy execute is fail-closed | **DECIDED**. Surface retirement **not** implemented | Removing routes/UI/table | OPEN-AUTHORITY §8 |
| 03 | Tenant-only Production scope. Workspace is not isolation | **DECIDED**. Not IMPLEMENTED | Not a universal tenancy pattern for Local OS | ADR-03 decision file |
| 04 | Tenant-owned Production AI credentials. Secret storage Option A | **DECIDED**. Gates A–D and rotation regressions recorded. Content-key reuse by other packs remains | Not a Core credential plane | Register ADR-04 body; §28.1; post-pack audit |
| 05 | Identity/Tenant/Authz shared. Event and Audit have an operating dual-write rule. Notification open. Storage and Execution not shared | **SPLIT** | Notification; event/audit table merge; handler idempotency | OPEN-AUTHORITY §5–6, §9 |
| 06 | Hidden UI state | **OPEN** | Client and historical cleanup | OPEN-AUTHORITY §7.D |
| 07 | Official preview rehashes bytes against `ByteSha256` and fails closed on mismatch | **IMPLEMENTED** (Famixa). Not marked as a new Core decision | Not a generic artifact store | `OfficialPreviewIntegrityV1`; OPEN-AUTHORITY §10; §28.1 |
| 08 | Episode assembly is a live Famixa action | **IMPLEMENTED** as product behavior. Further caller policy **OPEN**. Not Core | Not a platform assembly primitive | Migration 380; `AssembleEpisodeOsAsync`; §28.1 |

---

## BLOCKING DECISIONS

| Outcome | Must resolve first |
|---------|-------------------|
| A. Tenant-safe Production Engine | **Still not authorized.** ADR-03 is decided. A status of DECIDED is not a status of IMPLEMENTED. The engine still requires **new types**. GATE A is not executed. |
| B. Generic Storage/Artifact foundation | **Still not authorized.** Famixa preview rehash exists. Do not promote `IProductionArtifactFileStore`. |
| C. Legacy execution retirement | **Not required for authority.** ADR-02 closed official execute. Deleting the compatibility surface is still open. |
| D. AI credential consolidation | **ADR-04 DECIDED.** Production gates A–D and rotation regressions are recorded. Cross-pack Content Gemini reuse is still live and is pack boundary debt, not a Core plane. |
| E. Shared platform Event/Audit/Notify | Event and Audit have an operating dual-write rule. **Do not merge** tables or `production_task_events`. Notification stays open. |
| F. Stages 3–6 and Agent Runtime | **Not authorized.** Pharmacy and TEST_PACK are tenant-scoped. Local OS is dedicated single-tenant. That does not unlock a generic primitive. |

Do not invent further dependencies.

---

# IMPLEMENTATION READINESS

**PARTIAL**

Production Engine, generic IFileStore, Event/Audit table merge, and Agent Runtime **must not begin** merely because Master Architecture V1 exists. `ASSEMBLE_EPISODE` is already a Famixa action (migration 380). That fact does not make assembly a Core primitive.

**IMPLEMENTATION BLOCKED FOR THOSE AREAS ONLY.**

Work that may continue **without** resolving this register (still must not promote Famixa into Core):

- Famixa Production OS product work **on existing OS routes** (CREATE_VIDEO/VOICE/LIPSYNC/MIX/shot ASSEMBLE) within current Famixa boundary.
- Pharmacy / NOVIXA work under existing RBAC (do not inherit reports from `sales.*`).
- Family OS / Local OS on their own manifests.
- Documentation and further decision records.

A cross-product engine is still **not** authorized. GATE A is not executed. No Production Engine exists.

ADR-03 is **DECIDED**. A status of DECIDED is not a status of IMPLEMENTED.

ADR-04 is **DECIDED** (tenant-owned Production AI credentials). Secret storage is **DECIDED** and Gate B stores AES-256-GCM ciphertext on the credential version. The key is not in the database. Provider resolution is unchanged. Current `org_settings` credentials stay unscoped until a later gate proves ownership.

---

# ARCHITECTURAL INVARIANTS

1. Core must not depend on Famixa or Pharmacy domain types.  
2. Famixa ProductionTask is not generic Core Task.  
3. Famixa ProductionArtifact is not generic Core Artifact.  
4. Production Engine does not currently exist as a platform pack.  
5. Production Engine, if later extracted, uses NEW generic types.  
6. ArtifactId != ByteSha256 != StorageKey.  
7. ACCEPTED != Approval.  
8. Cache != Source of Truth.  
9. Journal != Event.  
10. ACTIVE selection controls pre-freeze HOW.  
11. Frozen ProviderId / ModelId controls post-freeze execution HOW.  
12. Task-bound snapshot controls execution.  
13. LatestSnapshot is evaluation state, not execution replay authority.  
14. Tenant scope is required before cross-product execution.  
15. Naming does not establish authority.  
16. Do not silently resolve KPA-07 UNKNOWN conflicts.  
17. Architecture precedes implementation.  

---

# CHECKPOINT

The lines that originally followed this heading said Implementation NOT STARTED, database unchanged, and listed ADR-01, ADR-02, ADR-05, and ADR-07 as the top unresolved blockers. That stamp is the first decision-phase footer. Current status is the ADR status blocks and the synchronization section at the end of this file.

DECISION PHASE RESULT:  
PARTIAL

IMPLEMENTATION READINESS:  
PARTIAL

Unresolved items that remain after synchronization:

1. ADR-06 — client and historical graph cleanup  
2. ADR-05 Notification, and the deferred Event/Audit table merge  
3. ADR-08 further caller policy  
4. Legacy surface retirement under ADR-02 (authority is already closed)

ADR-03 is **DECIDED**. A status of DECIDED is not a status of IMPLEMENTED. GATE A is not executed.

ADR-04 is **DECIDED** (tenant-owned Production AI credentials). Gates A–D are implemented. Official freeze pins CredentialId and CredentialVersionId on the existing execution snapshot. Retry reads that frozen reference. Gates E–F are not started. The next implementation checkpoint is PRODUCTION-CREDENTIAL-ROTATION-01. Legacy Content Park and Kit Video clients can still read `org_settings`. That is not the official Production transport path.

ADR-06 is evidence for ADR-01. ADR-08 blocks only enabling episode assemble.

---

# CLOSEOUT RECONCILIATION — PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01

This section does not replace the ADR text above. It records which later evidence supersedes a stale sentence.

| ADR | Previous status in this file | Later evidence | Current status | Reason |
|-----|------------------------------|----------------|----------------|--------|
| ADR-01 | DECISION REQUIRED | Episode assembly rejects `graph_json` as timing. Desk persist still writes take fields (ADR-06 inventory). | DECISION REQUIRED. DOCUMENTATION DRIFT: none on the decision itself. The split remains. | No later decision file chose script-only vs dual-write. |
| ADR-02 | DECISION REQUIRED | Legacy video-generation routes are still described as live in this register. Episode legacy assemble cannot create official artifacts (`ProductionLegacyExportAuthorityV1` assertions passed inside the episode suite). | DECISION REQUIRED | Retirement was not decided. Export-only is proven for the legacy series assemble flags, not for `video_generation_execution`. |
| ADR-03 | DECIDED, with "GATE A not executed" and "no TenantId" | The decision file. | DECIDED. Not IMPLEMENTED. | The decision (tenant-only, workspace not mandatory) stands. GATE A is not executed. |
| ADR-04 | DECIDED. Body says Gates A–D implemented and rotation not started. Header of the decision file still says implementation NOT STARTED. | `ProductionCredentialFreezeV1Regression`, `ProductionCredentialRotationV1Regression`, and `ProductionCredentialOfficialPathClosureV1Regression` returned 0 failures in PRODUCTION-EPISODE-ASSEMBLY-REGRESSION-01. Episode assembly pinned no credential because the provider was `server`. | DECIDED. DOCUMENTATION DRIFT: "rotation is not implemented" and "Implementation: NOT STARTED" in `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md`. | Ownership and storage decisions stand. Rotation code and its regression exist. This closeout did not rotate a live credential. |
| ADR-05 | DECISION REQUIRED for Audit/Event/Notify. Execution PRODUCT-SPECIFIC. | No new shared primitive was extracted. | Unchanged | Famixa Production OS stayed product-specific. |
| ADR-06 | DECISION REQUIRED. Fact: hidden state exists. | No new inventory. | Unchanged | Not re-audited in this closeout. |
| ADR-07 | DECISION REQUIRED. Body says preview does not rehash. | `ReadPreviewMediaAsync` calls `OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes`, which compares live SHA to `ByteSha256` and returns null on mismatch. | DECISION REQUIRED. DOCUMENTATION DRIFT in this ADR body and in `docs/KIT-PLATFORM-ADR-07-PREVIEW-SHA-DECISION-BRIEF-V1.md` ("no rehash", "implementation not started"). | The policy options were not formally chosen in a decision file. The code behaves as rehash-before-stream. Do not mark the ADR DECIDED from the code alone. |
| ADR-08 | DECISION REQUIRED. Facts say not SQL-allowed, not callable, do not add a CHECK. | Migration 380 allows `ASSEMBLE_EPISODE`. Controller calls `AssembleEpisodeOsAsync`. | IMPLEMENTED as Famixa behavior. Further caller policy OPEN. Not Core. See the synchronization section. | The action is not a platform primitive. |

---

# SYNCHRONIZATION — KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01

This section is the current reading. It does not delete the closeout table above. Where they disagree, this section wins, because `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` is later than the closeout.

| Topic | Current statement | Evidence | Still open |
|---|---|---|---|
| ADR-01 | Graph is script/story. Production OS is execution, artifact, binding, and official preview | OPEN-AUTHORITY §7 | Deleting historical media keys |
| ADR-02 | Official execute is Production OS. Legacy `ExecuteAsync` throws `LEGACY_EXECUTION_CLOSED` | OPEN-AUTHORITY §8; `VideoGenerationExecutionService.cs` | Removing the compatibility surface |
| ADR-03 | Tenant is the Production isolation boundary. Workspace is not | ADR-03 decision; `CoreAuthority.WorkspaceRequiredForTenantAuthorization == false` | GATE A is not executed |
| ADR-04 | Tenant-owned Production credentials are decided. Production gates are recorded as implemented | Register ADR-04 body; §28.1 | Content Gemini key still read by Pharmacy and Kit Sales |
| ADR-05 Event/Audit | Dual-write stays. Outbox is the named event target. No table merge | OPEN-AUTHORITY §5–6 | Notification. Handler idempotency. Physical merge |
| ADR-06 | Hidden client state cleanup | OPEN-AUTHORITY §7.D | The cleanup itself |
| ADR-07 | Official preview rehashes and returns null on SHA mismatch | `OfficialPreviewIntegrityV1`; OPEN-AUTHORITY §10 | Not a Core store |
| ADR-08 | Episode assembly is live Famixa behavior | Migration 380; controller; §28.1 | Wider product policy |
| Tenancy shapes | Pharmacy and TEST_PACK are tenant-scoped. Local OS is dedicated single-tenant (`KIT_LOCAL`, operator `KIT_MKT`). Do not generalize Local OS | PACK-ONBOARDING-02, PACK-ONBOARDING-03, LOCALOS-TENANT-BOUNDARY-01 | — |
| Core | Identity, Tenant, Authorization, Module enablement, Event/Outbox port. STATE B. No new Agent-chain primitive | ARCHITECTURE-AUDIT-POST-PACK-01 | Pack boundary debt listed in that audit |
| Invariant 1 | The rule "Core must not depend on Pharmacy types" still stands. `KitPlatform.Infrastructure` still references Pharmacy.Application. That is pack boundary debt, not a repeal of the rule | Post-pack audit §3 | The reference itself |
---

# MANIFEST DISPOSITION — CONTENT MIGRATIONS 383 AND 384

## Status

**DECIDED** for manifest ownership of the two files named in this section. **NOT IMPLEMENTED.**

A status of DECIDED is not a status of IMPLEMENTED. This section does not authorize implementation.

## Decision Owner

Architect.

## Decision

This section is limited to manifest ownership of two tracked files.

1. `migrations/383_pack_content_article_series.sql` is included in `deploy/ubuntu/migration-files.content.txt`. The manifest line uses the tracked filename exactly: `383_pack_content_article_series.sql`.
2. `migrations/384_pack_content_brand_narrative_dna.sql` is included in the same manifest. The manifest line uses the tracked filename exactly: `384_pack_content_brand_narrative_dna.sql`.
3. Neither SQL file is renamed to match its comment label. The labels `KitPlatform 370` and `KitPlatform 371` are not manifest lines and are not prefix assignments.
4. The reference `370_pack_content_article_series.sql` is a separate stale filename reference. This section does not change that reference.

## Limits

This section does not authorize implementation.

It does not authorize a change to the smoke test, to application contracts, to SQL files, or to any database. It does not authorize an edit of `deploy/ubuntu/migration-files.content.txt`.

It does not grant D-05 implementation permission. It does not select a migration prefix, a D-05 filename, or a manifest line position.

It does not lift line 231. It does not decide any content migration other than the two files named above. It does not mark Phase 1.5 PASS.

---

# D-05 — SCOPE DISPOSITION FOR `pack_content.series_build.tenant_id`

## Status

**DECIDED** — OPTION A — OUT OF SCOPE FOR PHASE 1.5. **NOT IMPLEMENTED.**

A status of DECIDED is not a status of IMPLEMENTED. This section does not authorize implementation.

## Decision Owner

Architect.

## Decision

1. The addition of `tenant_id` to `pack_content.series_build` is outside the scope of Phase 1.5.
2. Decision Register line 231 remains binding. This decision grants no implementation permission for TenantId, WorkspaceId, RLS, API, migration, or deployment changes.
3. No migration is to be created or modified for this purpose in Phase 1.5. No historical backfill is authorized.
4. ADR-03 Option 1 remains DECIDED. This scope decision does not establish that Production tenant isolation has been implemented or verified.
5. Any future proposal to add `tenant_id` must be submitted as a separate Architect decision, with explicit migration and manifest details and a separate implementation authorization.
6. This decision does not close other Phase 1.5 acceptance-gate rows or authorize any unrelated repository changes.

## Limits

**Implementation permission: NOT GRANTED.**

This section does not add `tenant_id` to `pack_content.series_build`. It does not create or modify a migration. It does not authorize a historical backfill. It does not select a migration prefix, a D-05 filename, or a manifest line.

It does not lift line 231. It does not change ADR-03 Option 1. It does not mark Phase 1.5 PASS.

---

# PO-15 — CUSTOMER OWNER FOR PRODUCTION ROWS

## Status

**DECIDED** — narrow implementation grant. **NOT IMPLEMENTED** by this section alone.

This section is a later record. It does not edit Decision Register line 231. It does not edit the D-05 section above. It does not edit the manifest-disposition section for migrations 383 and 384. Those sentences stay as they were written.

## Decision Owner

Product Owner, for the four business grants below. Platform Architecture remains the owner of ADR-03 Option 1. This section does not reopen that option.

## Conflict this section resolves

Line 231, in the ADR-03 record, still says: “No TenantId/WorkspaceId/RLS/API change is authorized by this record.” That sentence remains the limit of the ADR-03 record. D-05 Option A still says that record grants no implementation permission, and it still forbids `tenant_id` on `pack_content.series_build`.

The Product Owner closure package of 2026-10-15 grants a narrower permission than line 231 withholds. Governance §13 does not let a later note replace a Level 2 sentence by silence. This section is the later Level 2 record. It authorizes only the items in “Allowed” and repeats the items in “Still forbidden.”

## Decision

1. Production data of one customer is not the production data of another customer. The same episode code, shot code, or version may be used by two customers. That reuse does not make them one row.
2. An episode (`pack_content.video_production`), a production shot (`pack_content.video_production_shot`), and the two generation records (`pack_content.video_image_generation_execution`, `pack_content.video_generation_execution`) may carry an optional customer owner. The column is `tenant_id UUID NULL`, with no default, referencing `public.tenants(id)`. Old rows stay NULL. No statement in the migration assigns an owner.
3. Inside one customer, the existing business uniqueness rules still apply. Across customers, the same project and episode code are two production identities. The same shot code and version are two shot identities. The same generation fingerprint is two generation identities.
4. Shared Famixa studio material may be inherited: project, universe, visual system, character bible, master reference, and their canon. A customer may create private production rows. Inheritance of shared canon does not authorize a read or write of another customer’s episode, shot, or generation history. A private edit does not overwrite the shared canon row or another customer’s private row. This phase does not add a further asset hierarchy and does not add `tenant_id` to the shared canon tables.
5. A missing row, a row owned by another customer, and a row whose owner is NULL use one not-found result. The body is `Production không tồn tại.` The result does not say which of the three occurred. A rejected request does not change status and does not insert an event.
6. Pharmacy manifest ownership of `228_kit_accounts_memberships.sql` and `229_kit_accounts_prefer_pharmacy.sql`, and Family manifest ownership of `230_pack_family_parent_emotion_push.sql` and `231_pack_family_memory.sql`, is decided. The two pairs do not depend on each other. 228/229 depend on platform identity and pharmacy users. 230 depends on `pack_family.reminder_dispatch`. 231 depends on `pack_family.family`.
7. The already-decided content manifest lines for `383_pack_content_article_series.sql` and `384_pack_content_brand_narrative_dna.sql` may now be written. The earlier section that withheld the edit is not rewritten. This sentence is the implementation grant for those two lines only.
8. A one-time database, built from a copy of the content tables and not listening as the application database, may be counted and used for rehearsal. The application database is not migrated by this decision.

## Allowed

| Change | Limit |
|---|---|
| `tenant_id UUID NULL` on the four tables named in item 2 | No default. No backfill. No update of a NULL owner. No change of an owner that is already set. |
| Read, insert, update, validate, reject, and state transition for those four tables | The predicate is the caller’s tenant. A NULL owner matches nobody. |
| Not-found response for those resources | One body: `Production không tồn tại.` |
| Content manifest | Add the tracked names `383_pack_content_article_series.sql` and `384_pack_content_brand_narrative_dna.sql`, then the new owner migrations after a fresh prefix listing. |
| `migration-files.prod.txt` | Keep 228 and 229. Remove 230 and 231. |
| `migration-files.family-os.txt` | Keep 230 and 231. Remove 228 and 229. |

## Still forbidden

- `tenant_id` on `pack_content.series_build`. D-05 Option A is unchanged.
- `WorkspaceId` on Production rows. RLS. `CREATE POLICY`. Grant changes.
- Migrations 376, 385, and 386, and any reconstruction of them.
- Guessing an owner from project, universe, series build, actor, or canon.
- A customer column on project, universe, visual system, character DNA, master reference, or canon event tables.
- Restoring `IPermissionEngine`, `IPricingEngine`, or `IInventoryEngine`.
- A change to revenue visibility, the sales discount scale, FEFO, or the receipt price formula.
- Applying these migrations to the application database or to a production server.
- Marking Phase 1.5 PASS from this section. PASS still requires the Step 028 rows on an accepted commit.

## Limits

Line 231 is not deleted and is not reworded. ADR-03 Option 1 stays DECIDED. GATE A is not claimed complete. This section does not authorize publication, commit, or deploy. A later acceptance read may still find the working tree dirty. That finding is not removed by this decision.

---

# PO-16 — PHASE 1.5 SOURCE AND RECORDED DISPOSITIONS

## Status

**DECIDED** as a record of decisions already written. This section does not mark a Step 028 row PASS. A PASS is a later read of the accepted commit against the named checklist.

This section does not edit line 231, D-05, or PO-15. Those sentences stay as written.

## Decision Owner

Product Owner execution order of 2026-10-15, for placing already written dispositions onto this Register. Platform Architecture remains the owner of ADR-03 Option 1.

## 1. Checklist approval

`STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md` is the Phase 1.5 acceptance checklist. The Product Owner execution order of 2026-10-15 states that those criteria are the approved criteria. This paragraph cites that filename. Approval of the checklist does not mark its rows PASS.

The inspection triple printed inside Step 028 (`E:/KitPlatform`, branch `main`, HEAD `d54652d4a7734b3116db62bd356a831f9942fc73`) is the triple that file recorded. It is not the inspection identity of this acceptance. The identity in §2 is the named revision of that one row.

## 2. Source identity

| Item | Value |
|---|---|
| Inspection root | `E:/KitPlatform-wt-phase15-wp-bc-01` |
| Branch | `phase15-acceptance` |
| Accepted source | The tip of `phase15-acceptance` whose tree contains this section, `migrations/387_pack_content_production_customer_owner.sql`, and `migrations/388_pack_content_production_customer_uniqueness.sql`. |
| Publication | Not part of this acceptance. `origin/main` is not updated. A local commit is the source. |

Ancestor `b64f6772213dd0e095a49ae9a84983b37116f696` remains the parent that records D-05 Option A and the withdrawal of the Migration 376 claim. It is not, by itself, the tree that contains PO-15.

## 3. Required file set

These paths are the set this acceptance cites. Each one is in the accepted source. A gitignored copy is not a substitute.

- `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`
- `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`
- `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md`
- `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md`
- `docs/architecture/INDEX.md`
- `docs/architecture/KIT-PLATFORM-PHASE-1.5-ACCEPTANCE-TESTPACK-V1.md`
- `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md`
- `STEP-031-ARCHITECT-DISPOSITION-D03-C01-C04.md`
- `deploy/ubuntu/migration-files.content.txt`
- `deploy/ubuntu/migration-files.prod.txt`
- `deploy/ubuntu/migration-files.family-os.txt`
- `migrations/387_pack_content_production_customer_owner.sql`
- `migrations/388_pack_content_production_customer_uniqueness.sql`
- `src/KitPlatform.Api/Controllers/Content/ProductionTenantActionFilter.cs`

`KitPlatform.slnx` plus the project references in the accepted source are the dependency source. `KitPlatform.Api.csproj` does not reference TestPack. Projects that exist on disk and are absent from `KitPlatform.slnx` stay deferred. The NU1903 warning on `Newtonsoft.Json` 10.0.3 is not a separate mandatory failure.

## 4. Authority rank, C-01 through C-04

Recorded from `STEP-031-ARCHITECT-DISPOSITION-D03-C01-C04.md`. The sentences are not rewritten. Rank is unchanged.

- **C-01.** Work lists and reading order are navigation aids. They do not alter the authority hierarchy. Level 1 remains `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`. Level 2 remains this Register. Level 3 remains the ADR decision files. `docs/architecture/INDEX.md` is navigation, not an authority rank.
- **C-02.** The label “Primary SSOT” in `KIT-WORK-INDEX.md` does not create another Level 1 architecture authority. Level 1 remains `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`.
- **C-03.** Do not declare `Documentation State V1` wholly invalid. Mark an individual claim stale only where Git evidence supports that exact claim. That document is not edited here.
- **C-04.** The 2026-10-08 consolidation report does not override the later 2026-10-11 evidence. It is not Phase 1.5 PASS. Phase 1.5 is PASS only when every mandatory Step 028 row is PASS on the accepted source.

## 5. D-09 — reset execution order

No path for `RESET EXECUTION ORDER V1` is in this source. Searches under `docs/`, `docs/architecture/`, `docs/forensics/`, and `docs/00-control/` did not find it. The disposition, using the sentence already written for this gap, is: reset-order text is out of Phase 1.5 unless a path appears on the source commit. This is a scope note. It does not claim that a missing document passed, and it does not invent the document’s text.

## 6. D-06 — six `ProductionTenantActionFilter` attributes

The six attributes stay on `ContentController`. They are not removed.

| Action | Route |
|---|---|
| `ListSeriesBuilds` | `GET series/builds` |
| `GetSeriesBuild` | `GET series/builds/{id}` |
| `PutSeriesBuild` | `PUT series/builds` |
| `DeleteSeriesBuild` | `DELETE series/builds/{id}` |
| `ListProductionProgress` | `GET video-engine/production-progress` |
| `GetProductionProgress` | `GET video-engine/series/builds/{id}/production-progress` |

The implementing type is `src/KitPlatform.Api/Controllers/Content/ProductionTenantActionFilter.cs`, registered in `Program.cs` so the attributes resolve. `EstablishesSeriesResourceOwnership` is `false`. `OnActionExecutionAsync` calls `next()`. An active tenant is not ownership of a series build id.

These six routes are a shared series surface. The filter has no ownership duty. It is not the mechanism that hides another customer’s episode, shot, or generation row. That duty is `ProductionCustomerOwner` on the four tables named in PO-15, with the not-found body `Production không tồn tại.`

Resource refusal for a series build is not in this phase. D-05 Option A forbids `tenant_id` on `pack_content.series_build`. Adding that column, or treating this filter as a tenant guard, is outside this decision.

## 7. Manifest ownership

PO-15 items 6 and 7 are the ownership table. This paragraph does not change them.

| Filename | Owner manifest |
|---|---|
| `228_kit_accounts_memberships.sql` | `migration-files.prod.txt` only |
| `229_kit_accounts_prefer_pharmacy.sql` | `migration-files.prod.txt` only |
| `230_pack_family_parent_emotion_push.sql` | `migration-files.family-os.txt` only |
| `231_pack_family_memory.sql` | `migration-files.family-os.txt` only |
| `383_pack_content_article_series.sql` | `migration-files.content.txt` only |
| `384_pack_content_brand_narrative_dna.sql` | `migration-files.content.txt` only |
| `387_pack_content_production_customer_owner.sql` | `migration-files.content.txt` only |
| `388_pack_content_production_customer_uniqueness.sql` | `migration-files.content.txt` only |

`migrations/386_pack_content_famixa_canon_administer.sql` is not edited to satisfy this gate. Prefixes 376, 385, and 386 are not added. No manifest is merged into another.

## 8. Line 231 and the implementation citation

Line 231 remains the limit of the ADR-03 record. PO-15 is the later Level 2 grant for the four nullable `tenant_id` columns, the not-found body, and the manifest pairs. The implementation commit that adds migrations 387 and 388 cites PO-15. That citation is newer than Step 028. It does not cite Migration 376, a forensic file absent from history, or stash.

## 9. Named tests

`docs/architecture/KIT-PLATFORM-PHASE-1.5-ACCEPTANCE-TESTPACK-V1.md` is the tracked Phase 1.5 test list. The commands in that file do not open the application database on port 5432 and do not open a database named `kitplatform`. A dirty-tree result is not a result for this list. The list is run on the accepted source.

## Limits

This section does not add `tenant_id` to `pack_content.series_build`. It does not create migration 385 or 386. It does not authorize a historical backfill, RLS, a WorkspaceId change, publication, or deploy. It does not restore `IPermissionEngine`, `IPricingEngine`, or `IInventoryEngine`. It does not open Phase 2.

---

# PHASE 4 — ARCHITECTURE FREEZE

## Status

**DECIDED** as a freeze of decisions already recorded on the accepted baseline. **NOT IMPLEMENTED** by this section. A frozen sentence is not a status of IMPLEMENTED and is not a status of CLOSED.

This section is the Phase 4 record. It does not edit the ADR sections above, line 231, D-05, PO-15, or PO-16. Those sentences stay as written.

## Decision Owner

Architect authorization for Phase 4 on 2026-10-15. Platform Architecture remains the owner of ADR-03 Option 1 and of the authority hierarchy. Product Owner remains the owner of the PO-15 grants. This section does not reopen those owners.

## Classification

**SAFE** under Governance §5. This section records decisions that the baseline already states. It does not change platform layers, pack boundaries, the authority hierarchy, tenant isolation, or any implementation.

Governance §4 asks for Evidence, Reason, Impact, and Decision before an update. Those four are this section. A new ADR is not required: Governance §4 requires an ADR for a new architecture decision, and this section does not make one. Implementation is not entered. This section grants no implementation permission.

## Baseline

| Item | Value |
|---|---|
| Accepted Phase 1.5 / Phase 2 source | `37ffb3f61b53e040b17d3acb0d2a424a0e20a967` |
| Parent branch left unchanged | `phase15-acceptance` |
| Recorded on | `phase4-architecture-freeze`, parent of this commit is the accepted source |
| Candidate list | Phase 2 checkpoint section 5.1, file `KPA-PHASE2-ARCHITECTURE-COMPLETION-GATE.md` on inspection root `E:\KitPlatform-wt-phase15-wp-bc-01`. Level 5. Not promoted. |
| Open register used | That checkpoint’s section 5.2, checked against this Register |
| Dirty tree | `E:\KitPlatform` is not the source and is not evidence |

Phase 1.5 and Phase 2 stay PASS on the parent commit. This section does not rescore them.

## 1. Frozen decisions

Each row is frozen only because the cited baseline text states it. Master Architecture §22 stays the original question list and is not edited. A §22 line that still says “not decided” is not used to repeal a current Level 1 sentence that states the same rule.

| Frozen rule | Authoritative support on `37ffb3f6` |
|---|---|
| Authority order. Level 1 is `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`. Level 2 is this Register. Level 3 is the ADR decision files. `docs/architecture/INDEX.md` is navigation. | Governance §3. PO-16 §4, recording STEP-031 C-01. INDEX §2 prose states the same rank and states that the Index is not an authority level. |
| STATE B. No new Core primitive for Work, Context, Contract, Execution, Artifact, Verification, Provenance, Human Approval, or Agent Runtime. The §23 prohibitions stay in force, including no Production Engine, no generic `IFileStore`, and no promotion of Famixa types. | Master Architecture header, §23, and §29. Register invariants and the synchronization row “Core”. |
| ADR-03 Option 1. Tenant is the Production isolation boundary. Workspace is not that boundary. | Master Architecture §11. Register ADR-03 decision. `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` formal statement. |
| ADR-04 ownership and secret storage. A credential used by official Production execution is owned by exactly one Tenant. Secret storage is Option A: ciphertext in the application database, data-encryption key outside the database, external secret manager not selected. | Register ADR-04 formal statement. ADR-04 decision file, Decision and Secret storage sections. |
| ADR-01 authority split. `series_build.graph_json` is the Famixa script and story document. Production OS is the authority for execution, artifacts, bindings, and official preview. The split is Famixa, not a Core Script type. | Master Architecture §9 A, current authority. Register ADR-01 status. |
| ADR-02, limited. Official Famixa film execution authority is Production OS. | Master Architecture §9 E (“Official film uses OS”). Register ADR-02. The fail-closed sentence is not in this row. See §3. |
| ADR-05 operating rule. Identity, Tenant, and Authorization stay shared. Event and Audit stay dual-write. Storage is share-later. Execution stays product-specific. | Master Architecture §6.1, §14, and §19. Register ADR-05 status. |
| PO-15. Four nullable `tenant_id` columns, on `pack_content.video_production`, `pack_content.video_production_shot`, `pack_content.video_image_generation_execution`, and `pack_content.video_generation_execution`. No default. No backfill. No owner inferred from canon. One not-found body: `Production không tồn tại.` | PO-15 items 2 and 5 and the Still forbidden list. Migration 387 states that it does not backfill and does not alter `series_build`. `ProductionNotFoundException.FixedMessage` is that body. |
| D-05 Option A and line 231. `tenant_id` on `pack_content.series_build` is outside Phase 1.5 and is not granted by the ADR-03 record. A later column needs its own Architect decision, migration, and manifest. | D-05. Register line 231. PO-15 Still forbidden. ADR-03 decision file, “What This Does Not Mean”. |
| D-06. The six series routes on `ContentController` stay a shared series surface. `EstablishesSeriesResourceOwnership` is false. The filter does not establish ownership of a series build. | PO-16 §6. `ProductionTenantActionFilter.EstablishesSeriesResourceOwnership`. The six routes named in PO-16 §6 carry the filter on this commit. |
| PO-16 §7 manifest ownership. One owner manifest for 228, 229, 230, 231, 383, 384, 387, and 388, as that table states. | PO-16 §7. On this commit those eight names sit only on the owner manifest named in that table. |
| Pharmacy RBAC. `reports.*` does not inherit from `sales.*`. | Master Architecture §6.1 and §18. `ReportsAuthorizationExtensions` grants report policies from `reports.read`, `reports.write`, and `reports.export`. |
| Family OS and Local OS stay on their own manifests and tenants. Local OS is the dedicated single-tenant park. Its manifest is `deploy/ubuntu/migration-files.local-os.txt` and is not merged into the prod, Family OS, or Content manifests. | Master Architecture §19 and §29. Register synchronization row “Tenancy shapes” (`KIT_LOCAL`). The local-os manifest header. PO-16 §7 for the Family and Content pairs. |
| Second Brain stays deferred. `docs/architecture/second-brain/**` is not canonical architecture. Phase 8 is not opened. | Governance §15. INDEX §8. |

## 2. What a frozen rule does not do

A frozen rule preserves the decision already written. It does not assert that the decision is implemented, and it does not authorize the next build.

These confirmations are part of the freeze:

- ADR-03 remains DECIDED and not IMPLEMENTED. GATE A is not executed. Production OS rows are not claimed tenant-safe. PO-15’s four columns are not GATE A.
- ADR-04’s ownership and Option A storage are frozen. Gate completion, live rotation, and cross-pack Gemini reuse are not frozen. See §3 and §4.
- ADR-01 does not delete historical media keys. That cleanup stays with ADR-06.
- ADR-05 dual-write is the operating rule. Notification source of truth, handler idempotency, and a physical Event/Audit merge stay open.
- D-05 and line 231 stay in force. This freeze does not add `tenant_id` to `series_build`.
- The manifest table freezes ownership. It does not authorize another manifest edit.
- The Pharmacy rule freezes the reports boundary. It does not add `RequirePlatformModule` to ungated Pharmacy controllers.
- Family OS and Local OS boundaries stay as written in §19 and §29. This freeze does not change the Master Architecture “parked” label and does not decide a deployment state.
- Second Brain deferral does not promote those notes and does not grant Phase 8.

## 3. Conflicts preserved, not resolved

Higher authority is kept. This section does not rewrite a conflicting sentence and does not pick a winner.

### 3.1 ADR-02 fail-closed execute path — not frozen

The Phase 2 candidate list included “ADR-02 fail-closed execute path.” That sentence is not frozen.

- Level 1, Master Architecture §9 E, current authority: both APIs are live, and `ContentController` video-generation-execution still executes `IVideoGenerationProvider`. The target is still marked DECISION REQUIRED.
- Level 2, ADR-02 status and the synchronization row: `VideoGenerationExecutionService.ExecuteAsync` throws `LEGACY_EXECUTION_CLOSED` before a provider call, and parallel execute is closed.
- On `37ffb3f6`, `ExecuteAsync` in `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs` calls `_provider.GenerateAsync` when confirmation and preflight pass. The identifier `LEGACY_EXECUTION_CLOSED` appears in this Register and does not appear in a `.cs` file on that commit.

Level 1 and the cited source file agree with each other. The Level 2 fail-closed sentence does not. This freeze keeps the Level 1 statement. Official film authority remains Production OS, which §9 E and ADR-02 both state. Legacy-surface retirement stays open. The existing ADR-02 section is not rewritten.

INDEX §9 repeats “execute authority closed.” INDEX is navigation and does not settle this conflict.

### 3.2 ADR-04 implementation sentences — not frozen

Ownership and Option A storage are frozen in §1. The implementation-completeness sentences disagree, so none of them is frozen.

- Register ADR-04 status block: `Implementation: NOT STARTED`.
- Register ADR-04 decision body: Gates A–D are implemented, and official Production resolution does not fall back to `org_settings`. Business rotation is not implemented. The next checkpoint named there is `PRODUCTION-CREDENTIAL-ROTATION-01`.
- ADR-04 decision file: the decision records the target and “does not claim the running code already behaves this way.” The current-state table says official transports still use the unscoped row. The same file records Gates A–D and rotation regressions as implemented, and leaves credential business rotation at Gate E.
- Master Architecture §28.1: tenant-owned credential identity, encrypted storage, and credential rotation are “proven by their regressions.”

This freeze does not decide which of those implementation sentences is current.

### 3.3 Master Architecture §22 — original question list, not edited

§22.1 through §22.5 still say this document has not decided graph authority, legacy video execution, the tenant/workspace model, credential ownership, and shared primitives. This freeze does not edit those lines and does not mark them superseded.

A §1 row is frozen only where a current Level 1 section already states the rule: §9 A for the authority split, §9 E for official film authority, §11 for Workspace not being the Production isolation boundary, §14 for Event/Audit dual-write, and §6.1 with §18 for the reports boundary. §22 is not used to repeal those current sections. The one place a current Level 1 section conflicts with a later Register sentence is §3.1.

## 4. Open decisions

Silence in this section closes nothing. These stay open.

| Open item | Why it stays open | What may reopen or close it |
|---|---|---|
| GATE A implementation | ADR-03 required future gate. Register: GATE A is not executed. PO-15 does not claim it. | A separate implementation grant. The tenant-only rule stays frozen. |
| ADR-06 cleanup | Register status OPEN. | An ADR-06 decision. |
| Notification source of truth | Register ADR-05: Notification stays OPEN. | An ADR-05 decision. Dual-write stays until that decision. |
| Event/Audit physical merge and handler idempotency | Register: merge deferred, idempotency not fully proven. Master Architecture §14 records the same gap. | An ADR-05 decision. Dual-write stays until that decision. |
| Legacy-surface retirement | Master Architecture §9 E target is DECISION REQUIRED. Register: retirement is not implemented. The fail-closed claim is the §3.1 conflict. | An ADR-02 retirement decision. |
| ADR-08 caller policy | Register: further caller policy OPEN. The action stays Famixa-only. | An ADR-08 decision. |
| Future `series_build.tenant_id` | D-05 Option A. Line 231. | A new Architect decision with migration and manifest detail, and a separate implementation authorization. |
| NULL-owner backfill | PO-15 forbids assigning an owner. Migration 387 does not backfill. | A new Architect decision with proof of ownership. None is authorized by this freeze. |
| Credential rotation | Register names `PRODUCTION-CREDENTIAL-ROTATION-01`. §3.2 leaves the implementation sentences unresolved. | That checkpoint, through a later decision. This freeze does not close it. |
| Cross-pack Gemini reuse | Register synchronization row for ADR-04. Master Architecture §15 records the shared Content key as current. | A later pack-boundary decision. It is not a Core credential plane. |
| Ungated Pharmacy controllers | Master Architecture §29 records Pharmacy controllers that lack `RequirePlatformModule` as pack boundary debt. | A named controller and a separate implementation grant. |
| Level 4 promotion | Governance §8. P2-D2 limited later citation to files on `37ffb3f6` and did not promote gitignored files. | Governance §8: Architect approval, a `.gitignore` exception, a commit, and an Index update. |
| C-03 Documentation State V1 | PO-16 §4. One claim is marked stale only where Git evidence supports that claim. | That disposition. This freeze does not edit the document. |

Also still outside this freeze, and not started here: Phase 3 routing, Phase 8 Second Brain, and any later product-proof phase.

## 5. Reopening a frozen decision

A frozen row in §1 changes only when Governance §4 is followed again: evidence, reason, impact, and a new decision recorded in this Register before implementation. A checkpoint, a chat, a code edit, or a later document that has not completed that process does not reopen a frozen row and does not close an open row.

Breaking changes still require the Governance §5 approval. This freeze is not that approval.

## 6. Exclusions

This section does not grant, and this Phase 4 record does not perform:

- Routing runtime, routing API, navigation UI, or a new routing primitive. Phase 3 stays closed.
- A blanket module gate on Pharmacy controllers.
- A source, migration, manifest, or database change.
- A new Core abstraction, Production Engine, generic `IFileStore`, or Agent Runtime.
- A reopen of Phase 1.5 or Phase 2.
- Promotion of a gitignored Level 4 file.
- Publication, deploy, push, or merge.
- Implementation permission for any deferred or open item in §4.
- An edit of the Master Architecture, an ADR decision file, `GOVERNANCE.md`, or `INDEX.md`.

## Limits

The parent commit `37ffb3f61b53e040b17d3acb0d2a424a0e20a967` stays the Phase 1.5 and Phase 2 source. This section does not amend it.

Line 231 is not deleted. ADR-03 Option 1 stays DECIDED and not IMPLEMENTED. D-05 Option A stays DECIDED. PO-15 and PO-16 stay as written.

This freeze is not evidence that GATE A, ADR-06, Notification, an Event/Audit merge, legacy-surface retirement, ADR-08 caller policy, `series_build.tenant_id`, NULL-owner backfill, credential rotation, cross-pack Gemini reuse, or ungated Pharmacy controllers are implemented or resolved.
