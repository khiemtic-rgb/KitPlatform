# KIT PLATFORM ARCHITECTURE DECISION REGISTER V1

Status: DECISION PHASE  
Basis: KIT PLATFORM MASTER ARCHITECTURE V1  
Forensic Basis: KPA-01 → KPA-07  
Implementation: the draft stamp `NOT STARTED` is historical. The running system is `docs/architecture/KIT-PLATFORM-CURRENT-STATE-MAP-V1.md`. A status of DECIDED is not a status of IMPLEMENTED. This register still does not implement.

Option tables below stay as the original menus.

This register converts Master Architecture V1 §20 / §22 unresolved decisions into explicit ADRs.

It does not implement. It does not pick a winner where evidence only describes options. It does not reopen settled KPA findings.

Settled invariants (do not re-decide here) are restated in the final section.

---

# ADR-01 — graph_json

## Status

**DECIDED** (authority split). Full deletion of historical media keys is **not** implemented.

The path `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` is not in the history of commit `b64f6772213dd0e095a49ae9a84983b37116f696`. It is not evidence of current implementation state. The status bullets below remain recorded text in this register. They are not re-proved by that path. The option menu below is the original question. It is not reopened.

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

The path `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` is not in the history of commit `b64f6772213dd0e095a49ae9a84983b37116f696`. It is not evidence of current implementation state. Whether `VideoGenerationExecutionService.ExecuteAsync` throws `LEGACY_EXECUTION_CLOSED` before a provider call is not established by a tracked source in that commit. This sentence does not assert the opposite.

- Production OS is the sole official Famixa production execution authority.
- `video_generation_execution` remains a compatibility surface for read, preflight, approve, and reject.
- Whether the execute path submits a provider job, establishes an official binding, or creates an official artifact is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This bullet does not assert the opposite.
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

The paths `docs/forensics/KIT-PLATFORM-LOCALOS-TENANT-BOUNDARY-01.md`, `docs/forensics/KIT-PLATFORM-PACK-ONBOARDING-02.md`, and `docs/forensics/KIT-PLATFORM-PACK-ONBOARDING-03.md` are not in the history of commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not use them as proof that Local OS, TEST_PACK, or Pharmacy is tenant-scoped, and it does not assert the opposite. Local OS is not a second universal tenancy rule.

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

Acceptance claims that unverified migration `376_pack_content_production_tenant_boundary.sql` already added tenant attribution are withdrawn. That migration is not recreated to satisfy a checklist. A stash object, an untracked file, and an untracked forensic report are not implementation evidence for those claims. This sentence does not lift the sentence above.

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

Product-owned secrets in several places. Content Gemini key is **cross-product shared** (proven). Veo permit/credentials are Famixa-specific. Official Production HTTP transports resolve one unscoped `pack_content.org_settings` row (no `tenant_id`) plus process environment. That unscoped row is a structural gap. It is not an observed cross-tenant secret exposure. It is not rewritten as if tenant-owned credentials already exist.

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

Secret storage is **DECIDED. Option A — encrypted database secret storage.** The Phase 1.5 credential boundary is the structural gap that `pack_content.org_settings` does not separate tenants. This record does not state that a cross-tenant secret exposure was observed. Option A is not reopened.

Credential ciphertext is stored in the application database. The data-encryption key is stored outside the database, currently at `/etc/kit-platform/api.env`. An external secret manager is not selected. This register does not name a table, a code type, or a cipher. It does not claim that encryption, a credential domain, or a provider migration already exists.

Frozen executions pin CredentialId and CredentialVersion. Retry resolves that frozen version. Rotation creates a new version and does not rewrite frozen executions. A missing, revoked, invalid, wrong-tenant, or wrong-version credential fails closed. Forbidden fallbacks are legacy `org_settings`, arbitrary environment secrets, another tenant, another version, and another product or provider credential. Provenance may store CredentialId, CredentialVersion, ProviderId, and ModelId, and must not store secret material, the encryption key, or a decrypted payload.

The cipher is AES-256-GCM. The envelope stores a non-secret key id so a later master key can be introduced without rewriting ownership. Credential business rotation remains Gate E. The database dump does not contain the key. Legacy `pack_content.org_settings` stays transitional and is not migrated here.

Whether Gates A–D are implemented, and whether `ProductionCredentialFreezeV1Regression`, `ProductionCredentialRotationV1Regression`, or `ProductionCredentialOfficialPathClosureV1Regression` returned zero failures, is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This paragraph does not assert the opposite. It does not reopen tenant-owned credentials or secret-storage Option A. Do not build a Credential Service, vault, or platform credential plane from this record.

---

# ADR-05 — Shared platform primitives

## Status

**SPLIT. Not one closed decision.**

The paths `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` and `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` are not in the history of commit `b64f6772213dd0e095a49ae9a84983b37116f696`. They are not evidence of current implementation state. The table below remains recorded text in this register. It is not re-proved by those paths.

| Plane | Current status |
|---|---|
| Identity, Tenant, Authorization, Module enablement | **KEEP SHARED.** These are the shared Core in Master Architecture §6.1. Tracked migrations include Pharmacy and Local OS rows in `public.tenants`. Use by TEST_PACK is not established by commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite |
| Event | **OPERATING DECISION, merge not implemented.** `kit_event.event_outbox` is the named target surface. `public.platform_events` stays the compatibility write. Integration outbox stays a separate bus. Famixa journals stay out. Physical merge is deferred. Handler idempotency is **not** fully proven |
| Audit | **OPERATING DECISION, winner not chosen.** Dual-write stays. `kit_audit.activity_log` is the operational read. `public.audit_logs.id` stays the insert identity. Canonical consolidation is deferred |
| Notification | **OPEN**. This round (2026-11-10): not doing Notification SoT. Owner: Platform Architecture + Product Owners |
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

**OPEN** for client and historical cleanup. This round (2026-11-10): not doing that cleanup. Owner: Famixa Product + UI.

ADR-01 now closes new graph media authority. Deleting historical media keys and removing obsolete client fields stays open in this register (ADR-06). `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` is not in the history of commit `b64f6772213dd0e095a49ae9a84983b37116f696` and is not evidence of that deferral. Inventory that hidden product state exists remains sufficient. That inventory is not a decision to delete it.

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
**Fact recorded:** hidden product state **still exists** beside Production OS. New graph media authority is closed by ADR-01. Deleting historical keys and client fields is not decided. The inventory row that calls legacy video execute a parallel live path is historical. Whether `ExecuteAsync` throws `LEGACY_EXECUTION_CLOSED` is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite.

---

# ADR-07 — Preview SHA verification

## Status

**Not established as implemented by commit `b64f6772213dd0e095a49ae9a84983b37116f696`.** Not a Core artifact primitive.

Whether `OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes` hashes physical bytes and compares them to `ProductionArtifact.ByteSha256` is not established by a tracked source in that commit. `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs` is not in that commit. Master Architecture §28.1 does not prove this path. `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` is not in that commit and is not evidence of current implementation state. This paragraph does not assert the opposite.

The evidence block below that says preview does **not** rehash is the original analysis. It is historical. Whether the preview read rehashes is not established by a tracked source in that commit. This paragraph does not assert the opposite. This does not add `IFileStore` or a Core Artifact type.

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

The original options A/B/C were not voted in this register. Whether current official preview follows a rehash path is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This paragraph does not assert the opposite. No Core type was added.

---

# ADR-08 — ASSEMBLE_EPISODE

## Status

**Not a Core capability.** The original letters A/B/C were not voted in an ADR. Further product policy (retire the action, or expand who may call it) stays **OPEN**. This round (2026-11-10): not doing that caller-policy change. Owner: Famixa Product + API.

Whether `migrations/380_pack_content_production_assemble_episode_action.sql` allows `ASSEMBLE_EPISODE`, and whether `ContentProductionController` calls `AssembleEpisodeOsAsync`, is not established by a tracked source in that commit. This sentence does not assert the opposite. The evidence table below is the original draft record. CALLABLE and SQL-ALLOWED in that table are draft records. They are not established as the current schema or implementation.

Shot `ASSEMBLE`, legacy desk assemble, and `FilmAssembly` stay separate Famixa meanings. Episode assembly is not promoted to Core.

## Question

What is the intended product/API status of Famixa episode assembly (`ASSEMBLE_EPISODE`)?

## Current Evidence

KPA-06, KPA-07 I; Master Architecture §20.8; CK29 tests (not re-run here).

This table is the original draft record in this register. CALLABLE and SQL-ALLOWED below are draft records. Whether any row is the current schema or implementation is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite.

| Axis | Status |
|------|--------|
| CURRENT | Famixa-only. Output role = ASSEMBLY. Episode binds CURRENT/VISIBLE:ASSEMBLY. `FINAL` binding unused. |
| IMPLEMENTED | Not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. |
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

Whether the action is callable and SQL-allowed is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite. In this register the action stays Famixa-scoped and is not a Core capability.

A further product letter (retire, or widen the callers) is **OPEN**. Do not promote the action to Core.

---

# Decision matrix

| ADR | Decision | Status | Blocking | Depends On |
|-----|----------|--------|----------|------------|
| 01 | graph is script/story; Production OS is execution/artifact/binding | **DECIDED** | Historical key deletion still open (ADR-06) | OPEN-AUTHORITY §7 is not in commit `b64f6772213dd0e095a49ae9a84983b37116f696` and is not current-implementation evidence |
| 02 | OS is official execution; legacy execute is fail-closed | **DECIDED**. Surface retirement **not** implemented | Removing routes/UI/table | OPEN-AUTHORITY §8 is not in that commit and is not current-implementation evidence |
| 03 | Tenant-only Production scope. Workspace is not isolation | **DECIDED**. Not IMPLEMENTED | Not a universal tenancy pattern for Local OS | ADR-03 decision file |
| 04 | Tenant-owned Production AI credentials. Secret storage Option A | **DECIDED**. Whether Gates A–D and rotation regressions are implemented is not established by commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite | Not a Core credential plane | Register ADR-04 body. §28.1 does not prove the gates. The post-pack audit path is not in that commit |
| 05 | Identity/Tenant/Authz shared. Event and Audit have an operating dual-write rule. Notification open. Storage and Execution not shared | **SPLIT**. This round (2026-11-10): not doing Notification SoT | Notification; event/audit table merge; handler idempotency | Platform Architecture + Product Owners |
| 06 | Hidden UI state | **OPEN**. This round (2026-11-10): not doing client and historical cleanup | Client and historical cleanup | Famixa Product + UI |
| 07 | Official preview rehashes bytes against `ByteSha256` and fails closed on mismatch | Not established as implemented by commit `b64f6772213dd0e095a49ae9a84983b37116f696`. Not marked as a new Core decision | Not a generic artifact store | `OfficialPreviewIntegrityV1` is not in that commit; OPEN-AUTHORITY §10 is not in that commit; §28.1 does not prove this path |
| 08 | Episode assembly caller policy | Further caller policy **OPEN**. Not Core. This round (2026-11-10): not doing that change | Not a platform assembly primitive | Famixa Product + API |

---

## BLOCKING DECISIONS

| Outcome | Must resolve first |
|---------|-------------------|
| A. Tenant-safe Production Engine | **Still not authorized.** ADR-03 is decided. A status of DECIDED is not a status of IMPLEMENTED. The engine still requires **new types**. GATE A is not executed. |
| B. Generic Storage/Artifact foundation | **Still not authorized.** Famixa preview rehash exists. Do not promote `IProductionArtifactFileStore`. |
| C. Legacy execution retirement | **Not required for authority.** ADR-02 closed official execute. Deleting the compatibility surface is still open. |
| D. AI credential consolidation | **ADR-04 DECIDED.** Whether Production gates A–D and rotation regressions are implemented is not established by commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. Cross-pack credential reuse is not a Core plane. |
| E. Shared platform Event/Audit/Notify | Event and Audit have an operating dual-write rule. **Do not merge** tables or `production_task_events`. Notification stays open. |
| F. Stages 3–6 and Agent Runtime | **Not authorized.** Whether Pharmacy and TEST_PACK are tenant-scoped, and whether Local OS is dedicated single-tenant, is not established by commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. That open question does not unlock a generic primitive. |

Do not invent further dependencies.

---

# IMPLEMENTATION READINESS

**PARTIAL**

Production Engine, generic IFileStore, Event/Audit table merge, and Agent Runtime **must not begin** merely because Master Architecture V1 exists. Whether `ASSEMBLE_EPISODE` is a Famixa action through migration 380 is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite. That open question does not make assembly a Core primitive.

**IMPLEMENTATION BLOCKED FOR THOSE AREAS ONLY.**

Work that may continue **without** resolving this register (still must not promote Famixa into Core):

- Famixa Production OS product work **on existing OS routes** (CREATE_VIDEO/VOICE/LIPSYNC/MIX/shot ASSEMBLE) within current Famixa boundary.
- Pharmacy / NOVIXA work under existing RBAC (do not inherit reports from `sales.*`).
- Family OS / Local OS on their own manifests.
- Documentation and further decision records.

A cross-product engine is still **not** authorized. GATE A is not executed. No Production Engine exists.

ADR-03 is **DECIDED**. A status of DECIDED is not a status of IMPLEMENTED.

ADR-04 is **DECIDED** (tenant-owned Production AI credentials). Secret storage is **DECIDED** Option A. The recorded Gate B design stores an AES-256-GCM envelope on the credential version. Whether that design is implemented is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite. It does not reopen Option A.

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

ADR-04 is **DECIDED** (tenant-owned Production AI credentials). Whether Gates A–D are implemented, and whether official freeze pins CredentialId and CredentialVersionId, is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This sentence does not assert the opposite. Gates E–F stay outside this implementation claim. Do not build a Credential Service, vault, or platform credential plane from this record. Whether legacy Content Park and Kit Video clients can still read `org_settings` is not established by a tracked source in that commit. This sentence does not assert the opposite.

ADR-06 is evidence for ADR-01. ADR-08 leaves further caller policy OPEN and does not promote episode assembly to Core.

---

# CLOSEOUT RECONCILIATION — PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01

This section does not replace the ADR text above. It records which later evidence supersedes a stale sentence.

| ADR | Previous status in this file | Later evidence | Current status | Reason |
|-----|------------------------------|----------------|----------------|--------|
| ADR-01 | DECISION REQUIRED | Episode assembly rejects `graph_json` as timing. Desk persist still writes take fields (ADR-06 inventory). | DECISION REQUIRED. DOCUMENTATION DRIFT: none on the decision itself. The split remains. | No later decision file chose script-only vs dual-write. |
| ADR-02 | DECISION REQUIRED | Legacy video-generation routes are still described as live in this register. Episode legacy assemble cannot create official artifacts (`ProductionLegacyExportAuthorityV1` assertions passed inside the episode suite). | DECISION REQUIRED | Retirement was not decided. Export-only is proven for the legacy series assemble flags, not for `video_generation_execution`. |
| ADR-03 | DECIDED, with "GATE A not executed" and "no TenantId" | The decision file. | DECIDED. Not IMPLEMENTED. | The decision (tenant-only, workspace not mandatory) stands. GATE A is not executed. |
| ADR-04 | DECIDED. An earlier body sentence said Gates A–D are implemented. | Whether `ProductionCredentialFreezeV1Regression`, `ProductionCredentialRotationV1Regression`, and `ProductionCredentialOfficialPathClosureV1Regression` returned zero failures is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. | DECIDED. Ownership and secret-storage Option A stand. Gates A–D are not established as implemented by that commit. | Ownership and storage decisions stand. This closeout does not establish that rotation code or its regression exists in that commit. |
| ADR-05 | DECISION REQUIRED for Audit/Event/Notify. Execution PRODUCT-SPECIFIC. | No new shared primitive was extracted. | Unchanged | Famixa Production OS stayed product-specific. |
| ADR-06 | DECISION REQUIRED. Fact: hidden state exists. | No new inventory. | Unchanged | Not re-audited in this closeout. |
| ADR-07 | DECISION REQUIRED. Body says preview does not rehash. | Whether `ReadPreviewMediaAsync` calls `OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes` is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. | DECISION REQUIRED. The policy options were not formally chosen in a decision file. | Do not mark the ADR DECIDED from a type that is not in that commit. |
| ADR-08 | DECISION REQUIRED. Facts say not SQL-allowed, not callable, do not add a CHECK. | Whether migration 380 allows `ASSEMBLE_EPISODE`, and whether a controller calls `AssembleEpisodeOsAsync`, is not established by a tracked source in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite. | Further caller policy OPEN. Not Core. Implementation is not established by that commit. | The action is not a platform primitive. |

---

# SYNCHRONIZATION — KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01

This section does not delete the closeout table above. `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` is not in the history of commit `b64f6772213dd0e095a49ae9a84983b37116f696`. That path is not evidence that this section is the current implementation state, and it is not evidence that this section wins where it disagrees with the closeout. Rows below keep their recorded decision text. A citation of that path is not proof of current implementation state.

| Topic | Current statement | Evidence | Still open |
|---|---|---|---|
| ADR-01 | Graph is script/story. Production OS is execution, artifact, binding, and official preview | OPEN-AUTHORITY §7 is not in commit `b64f6772213dd0e095a49ae9a84983b37116f696` and is not current-implementation evidence | Deleting historical media keys |
| ADR-02 | Official execute is Production OS. Whether legacy `ExecuteAsync` throws `LEGACY_EXECUTION_CLOSED` is not established by that commit. This cell does not assert the opposite | OPEN-AUTHORITY §8 is not in that commit. `VideoGenerationExecutionService.cs` in that commit does not contain that symbol | Removing the compatibility surface |
| ADR-03 | Tenant is the Production isolation boundary. Workspace is not | ADR-03 decision file in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. `CoreAuthority.WorkspaceRequiredForTenantAuthorization` is not a tracked symbol in that commit and is not evidence for this row | GATE A is not executed |
| ADR-04 | Tenant-owned Production credentials are decided. Whether Production gates are implemented is not established by commit `b64f6772213dd0e095a49ae9a84983b37116f696`. This cell does not assert the opposite | Register ADR-04 decision. §28.1 does not prove the gates | Whether a Content Gemini key is still read by Pharmacy and Kit Sales |
| ADR-05 Event/Audit | Dual-write stays. Outbox is the named event target. No table merge | OPEN-AUTHORITY §5–6 is not in that commit and is not current-implementation evidence | Notification. Handler idempotency. Physical merge |
| ADR-06 | Hidden client state cleanup | OPEN-AUTHORITY §7.D is not in that commit and is not current-implementation evidence | The cleanup itself |
| ADR-07 | Whether official preview rehashes and returns null on SHA mismatch is not established by that commit. This cell does not assert the opposite | `OfficialPreviewIntegrityV1` is not in that commit. OPEN-AUTHORITY §10 is not in that commit | Not a Core store |
| ADR-08 | Whether episode assembly is live Famixa behavior is not established by that commit. This cell does not assert the opposite. Further caller policy stays OPEN. Not Core | Migration 380 and `AssembleEpisodeOsAsync` are not tracked sources in that commit. §28.1 does not prove this path | Wider product policy |
| Tenancy shapes | This row does not establish a tenant-scope fact for Pharmacy, TEST_PACK, or Local OS. Local OS is not a second universal tenancy rule | PACK-ONBOARDING-02, PACK-ONBOARDING-03, and LOCALOS-TENANT-BOUNDARY-01 are not in the history of commit b64f6772213dd0e095a49ae9a84983b37116f696 | Tenant-scope of those three packs |
| Core | Identity, Tenant, Authorization, and Module enablement are the current Core list in Master Architecture §6.1. STATE B, an Event/Outbox port as current Core, and pack-boundary debt are not established by commit `b64f6772213dd0e095a49ae9a84983b37116f696`, because `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` is not in its history. This row does not assert the opposite. No new Agent-chain primitive remains the target in §6.2 | Master Architecture §6.1 and §6.2. The post-pack audit path is not in that commit | Whether STATE B and an Event/Outbox port are current |
| Invariant 1 | The rule "Core must not depend on Pharmacy types" still stands. `KitPlatform.Infrastructure` still references Pharmacy.Application. That is pack boundary debt, not a repeal of the rule | `src/KitPlatform.Infrastructure/KitPlatform.Infrastructure.csproj` in commit `b64f6772213dd0e095a49ae9a84983b37116f696`. Post-pack audit §3 is not in that commit and is not the evidence for this row | The reference itself |
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
