# KIT PLATFORM MASTER ARCHITECTURE V1

Status: ARCHITECTURE DRAFT V1, with factual synchronization in §29  
Forensic Basis: KPA-01 → KPA-07  
Original draft stamp: Implementation Status NOT STARTED  
Architecture Phase: ACTIVE  

Current reading (`docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` and `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md`): Master Architecture V1 remains the target. Operating state is **STATE B**. Core Identity, Tenant, Authorization, Module enablement, and the event/outbox port are in use. Production Engine and Agent Runtime are not implemented. **NO CORE CHANGE** is required for the proven pack patterns. Do not start Work, Context, Contract, Execution, Artifact, Verification, Provenance, Human Approval, or Agent Runtime as a speculative Core subsystem. The "NOT STARTED" stamp above is the original draft, not the current code.

This document converts completed forensic evidence into an architectural reference. It is not an implementation plan. It does not create interfaces, tables, migrations, or services.

North star (architectural reading, not marketing):

> KIT Platform là nền tảng vận hành doanh nghiệp và sản phẩm trong kỷ nguyên AI — nơi con người, AI agents, dữ liệu, workflow và hệ thống bên ngoài cùng thực hiện công việc trong môi trường có context, contract, quyền hạn, provenance và kiểm soát.

That sentence requires: human execution, AI-assisted execution, future AI-agent execution, explicit context, contracts, authorization, provenance, verification, auditability, reproducibility, tenant isolation, product boundaries, and provider independence.

KPA-07 verdict: **PASS WITH GAPS**. Forensic phase is **COMPLETE**. Evidence is sufficient for a descriptive authority map. Evidence is **not** sufficient to claim the current system is already a single source of truth.

This document therefore distinguishes four states on every major claim:

| State | Meaning |
|-------|---------|
| CURRENT | Proven in the repository today |
| TARGET | Intended architecture after decisions and extraction |
| TRANSITION | Allowed coexistence while CURRENT is still live |
| UNRESOLVED / DECISION REQUIRED | KPA-07 UNKNOWN or product choice; this document does not pick a winner |

---

## 0. How to read this document

| Claim type | How it is marked |
|------------|------------------|
| Proven current capability | **Status: CURRENT** + Evidence: KPA-X |
| Intended primitive that does not exist | **Status: TARGET / FUTURE** |
| Live dual-write / dual API | **Status: TRANSITION** |
| Forensic gap or product choice | **Status: DECISION REQUIRED** |

Naming is not authority. Reusability is not genericity. A Famixa type whose name looks generic remains Famixa until a **new** generic type is extracted after prerequisites exist.

**Do not implement this document until it is reviewed and the decisions in §20 are explicitly approved.**

---

## 1. Architectural north star

**Status: TARGET** (intent). **Evidence:** KPA-01–07 describe what exists; they do not claim this north star is implemented.

KIT Platform is an enterprise operating environment in which:

1. A **human** can perform work under tenant, permission, and product context.
2. An **AI-assisted** path can perform work under the same authorization and a frozen contract.
3. A future **AI agent** can perform work only after Context, Contract, Authorization, Execution, and Provenance exist as platform concepts — they do not exist as a generic plane today.
4. Every durable execution can answer, to the extent the layer supports it: who, tenant, product, input, provider, attempt, artifact, hash, verification.

CURRENT platform strength is identity + tenant + JWT + permissions + modules (KPA-01). CURRENT strongest execution/provenance machinery is Famixa Production OS — and it is **product/content-specific** (KPA-06). CURRENT system is **not** a single SoT (KPA-07).

---

## 2. Three-layer boundary

```
KIT PLATFORM CORE          generic enterprise / runtime primitives
        ↓
PRODUCTION ENGINE          generic execution / provenance machinery   [TARGET — does not exist as a pack]
        ↓
PRODUCT PACKS              domain semantics (NOVIXA, FAMIXA, future)
```

**Status: TARGET layering.** CURRENT is Core + product islands + `kit_*` overlays. Production Engine is **not** a current platform component.

### 2.1 KIT Platform Core

Generic enterprise/runtime primitives: tenant, user, JWT, permissions, modules, platform configuration, request tenant context.

Core **must not** absorb Famixa media or Production OS types (KPA-06 §22, KPA-07 contamination risk).

### 2.2 Production Engine

**Production Engine is a future architectural extraction, not a current platform component.**

Famixa Production OS contains reusable **patterns** (work identity, freeze/hash, attempt, artifact split, LATE, journal). Its current types (`ProductionTask`, `ProductionTaskInputSnapshot`, `ProductionArtifact`, `ProductionBinding`, …) remain Famixa-specific (KPA-06).

Do **not** move current Famixa Production types into Core. If an engine is extracted later, it must be **new generic types**, after tenant/workspace and storage/identity foundations exist.

### 2.3 Product packs

NOVIXA (Pharmacy and related enterprise), FAMIXA (Content / Production Domain), Family OS, Local OS, and future products. Product semantics stay in packs.

**Evidence:** KPA-01 (layers), KPA-06 (promotion), KPA-07 (authority).  
**Status:** CURRENT = Core + packs + strangler overlays. TARGET = three-layer boundary. Production Engine = FUTURE.

---

## 3. CURRENT architecture

**Evidence:** KPA-01, KPA-02, KPA-07. **Status: CURRENT.**

```
┌─────────────────────────────────────────────────────────────────┐
│                     CURRENT (as proven)                         │
│                                                                 │
│  public.tenants / public.users / JWT / ITenantContext / RBAC    │
│                         = KIT Core SoT                          │
│                                                                 │
│  kit_accounts · kit_workspace · kit_audit · kit_workflow        │
│  kit_event · kit_notify                                         │
│                         = STRANGLER / dual-write overlays       │
│                                                                 │
│  Packs (isolated by schema / manifest, not a generic engine)    │
│    Pharmacy  → IWorkflowEngine, IAiOrchestrator (health)        │
│    Content   → Famixa Production OS + graph_json + IDB          │
│                + video_generation_execution (legacy, live)      │
│    Family OS / Local OS → separate manifests                    │
│    (later: Family OS pilot is active; Local OS is a            │
│     dedicated single-tenant park, not an unused shelf)         │
│                                                                 │
│  /api/files  → uploads island (not ProductionArtifact)          │
│  App_Data / KitVideoVideoRoot → physical islands                │
└─────────────────────────────────────────────────────────────────┘
```

What CURRENT is **not**:

- A generic Workflow/Task/Approval kernel (KPA-02, KPA-04).
- A generic Storage / Artifact primitive (KPA-03).
- A generic Event bus with proven handler idempotency (KPA-04).
- A generic AI Provider Plane, Model Registry, Agent Runtime, ContextPack, Evaluation, or HumanApproval (KPA-05).
- A reusable Production Engine pack (KPA-06).
- A single source of truth for Famixa production (KPA-07).

---

## 4. TARGET architecture

**Status: TARGET.** Items not proven current are marked FUTURE.

```
KIT PLATFORM
  ├── Core Runtime
  │     Identity              CURRENT
  │     Tenant                CURRENT
  │     Authorization         CURRENT (JWT / permissions / roles)
  │     Modules               CURRENT
  │     Organization          CURRENT in Pharmacy as branches; not generic
  │     Workspace             CURRENT table kit_workspace; semantics UNRESOLVED
  │     Config                CURRENT (platform + pack options)
  │     Audit                 CURRENT dual-write; TARGET = one audit plane
  │     Event                 CURRENT dual-write; TARGET = platform Event ≠ journal
  │     Notification          CURRENT kit_notify + pharmacy inbox concepts
  │     Storage primitives    FUTURE (do not promote IProductionArtifactFileStore)
  │
  ├── Production Engine       FUTURE EXTRACTION — new types, not a rename
  │     Work / Request
  │     Contract
  │     Frozen Context / Input
  │     Execution / Attempt
  │     Provider Job
  │     Artifact (id ≠ hash ≠ location)
  │     Verification
  │     Provenance
  │     Replay
  │     optional Cost / Permit ports (not Famixa Veo rules)
  │
  └── Product Packs
        ├── NOVIXA            Pharmacy and related enterprise
        ├── FAMIXA
        │     └── Production Domain   CURRENT (must remain Famixa)
        ├── Family OS         parked / separate manifest
        ├── Local OS          parked / separate manifest
        └── Future Products
```

Human, AI-assisted, and future agent execution all pass:

Context → Contract → Authorization → Execution → Artifact → Verification → Provenance → Evaluation → Human approval where required.

That chain is **TARGET**. CURRENT Famixa Production OS implements a **product-shaped subset** (snapshot, attempt, artifact, SHA, binding, execution journal) without tenant/workspace, without generic approval, and without an agent runtime.

---

## 5. TRANSITION architecture

Architectural sequencing only. Not migrations. Not a backlog of tickets.

| Stage | Name | Intent | Must not do |
|-------|------|--------|-------------|
| 0 | CURRENT | public.* SoT + kit_* overlays + product islands | Pretend a single SoT exists |
| 1 | STABILIZE AUTHORITY | Classify authorities (this document + KPA-07). Keep dual-live paths labelled. | Genericize, rename, “fix” conflicts |
| 2 | ESTABLISH TENANT / WORKSPACE BOUNDARIES | Partial. Pharmacy and TEST_PACK are tenant-scoped. New Famixa Production writes are tenant-scoped. Local OS is dedicated single-tenant on purpose. Historical Famixa NULL `tenant_id` rows remain. Workspace is not the boundary | Extract a Production Engine first |
| 3 | STORAGE / ARTIFACT FOUNDATION | Separate blob store, artifact identity, content hash, location, integrity | Promote `IProductionArtifactFileStore` to `IFileStore` |
| 4 | EXECUTION FOUNDATION | Generic execution **concepts** only when proven outside one product | Move `ProductionTask` into Core |
| 5 | PRODUCTION ENGINE | **New** generic types. Famixa semantics stay in Famixa. | Rename Famixa classes into an “engine” |
| 6 | AI / AGENT PLATFORM | Provider/model/agent only after Context, Contract, Authorization, Execution, Provenance | Build Agent Runtime first; rename `IAiOrchestrator` |

**Status: TRANSITION.** **Evidence:** KPA-06 promotion order; KPA-07 dual-live zones.

Until Stage 2, cross-product generic execution is architecturally unsafe (no tenant/workspace on Production OS or `series_build`).

---

## 6. KIT Platform Core

### 6.1 Proven current Core

**Evidence:** KPA-01, KPA-02. **Status: CURRENT.**

| Capability | Authority |
|------------|-----------|
| Tenant | `public.tenants` |
| User / Identity | `public.users` |
| JWT + request tenant | `ITenantContext` |
| Permissions / roles / policies | existing RBAC (`usePermission`, policy classes). Pharmacy: `reports.*` does not inherit from `sales.*` |
| Modules | module JSON / pack enablement |
| Platform runtime context | API host, connection, pack DI |

Server-side authorization is authoritative. Client ADMIN/UI state is not authority (KPA-01; target principle).

### 6.2 Future Core primitives

**Status: TARGET / FUTURE.** Do not pretend they exist.

| Primitive | Why later | Prerequisite |
|-----------|-----------|--------------|
| Organization (generic) | Pharmacy branches are practical hierarchy, not a platform org model | Decision: org vs tenant vs branch |
| Workspace (resolved) | `kit_workspace` exists; semantics unresolved (KPA-01/KPA-09 concern in KPA-07 §9) | Decision: workspace vs tenant |
| Storage / artifact primitives | No `IFileStore` / `IArtifactStore` (KPA-03) | Tenant-scoped identity; do not promote Famixa store |
| Unified Audit | Dual-write `audit_logs` + `kit_audit` (KPA-04) | Authority decision |
| Unified Event | `platform_events` / outbox + `kit_event` (KPA-04) | Distinguish Event vs journal vs outbox |
| Notification plane | Delivery vs inbox coexist (KPA-04) | Do not merge with Event |
| Context / Contract / Provenance / Evaluation | Absent as generic planes (KPA-05) | After identity + execution foundations |
| Agent Runtime | Absent (KPA-05) | After Context/Contract/Authorization/Execution/Provenance |

### 6.3 Core must not absorb

**Evidence:** KPA-06 §22, KPA-07 P0-4. **Status: CURRENT prohibition / TARGET invariant.**

Shot, Episode, Scene, Character, VisualContract, ActingBeat, MIX, LIPSYNC, VIDEO_TAKE, FilmAssembly, Famixa provider registry, Famixa adapters, Famixa Veo permit, Famixa media roles, `pack_content.production_*`, Famixa Director semantics.

---

## 7. Production Engine (future)

**Explicit statement:** Production Engine is a future architectural extraction, not a current platform component.

**Evidence:** KPA-06 §§19–21. **Status: FUTURE EXTRACTION.**

### 7.1 Patterns that may justify later extraction

These are **patterns**, not current Core types:

- work / execution identity
- request idempotency
- immutable input snapshot / hash
- attempt identity
- provider job identity
- artifact row identity
- content hash
- physical storage location
- verification status
- execution journal
- retry
- LATE handling
- provenance
- optional cost / permit **ports** (empty of Veo/Famixa rules)

### 7.2 What a future engine must not own

Shot, Episode, Scene, Character, VisualContract, ActingBeat, FilmAssembly, MIX, LIPSYNC, VIDEO_TAKE, Famixa provider catalog, Famixa Director UI, product-specific media roles, product-specific cost policy, product-specific permit policy.

### 7.3 Generic execution machinery vs product semantics

| Generic execution (TARGET engine) | Product semantics (stay in Famixa / pack) |
|-----------------------------------|-------------------------------------------|
| Request / task / attempt ids | ProductionScope = Build/Episode/Scene/Shot |
| Frozen input hash | `ShotExecutionInput`, Story, ActingBeat, VisualContract |
| Provider id + model id + job id | `IFamixaProviderRegistry`, Veo/Runway/Fal/ElevenLabs |
| ArtifactId ≠ SHA ≠ StorageKey | Roles KEYFRAME / VIDEO_TAKE / VOICE / LIPSYNC / MIX / ASSEMBLY |
| Verification status | MP4/WAV decode, duration, Famixa quality |
| Execution journal | `shot_code` on `production_task_events` |
| Optional permit/cost port | `OfficialCreateVideoPermitAuthorizationV1`, `FamixaVeo31OfficialPricing` |
| Binding as “selected output” (if extracted later) | CURRENT/VISIBLE/ACCEPTED and ROLE:TYPE on (build, shot) |

CURRENT `ProductionTask` cannot be created by another KIT product without Famixa (`series_build` FK, Shot scope, media actions) — KPA-06 §3: **NO**.

---

## 8. Famixa Production Domain

**Evidence:** KPA-06 §16–17, §22. **Status: CURRENT product boundary. TARGET: remains Famixa.**

Keep inside Famixa:

- Series, `series_build`, `graph_json`, Build, Episode, Scene, Shot
- Character, VisualContract, ActingBeat, Keyframe / Motion / Voice / LipSync semantics
- MIX, VIDEO_TAKE, ASSEMBLY, FilmAssembly
- ProductionBinding; CURRENT / VISIBLE / ACCEPTED; typed media roles
- Famixa Production OS UI, Director
- `ShotExecutionCompiler`
- `IFamixaProviderRegistry`, Famixa adapters, media catalogs
- Official Veo selection / permit / cost rules (`OfficialCreateVideoPermitAuthorizationV1`, `RealVertexExecutionV1Lock`, `OfficialSelectionId`)

Do not generalize these because names sound generic.

**Related packs (not merged):** Family OS and Local OS keep separate migration manifests and are not mixed with Pharmacy auth or Content Production OS. "Parked" in the original sentence meant separate manifests. Later evidence: Family OS is an active local and pilot deployment (`.cursor/rules/family-os-parked.mdc`). Local OS is a dedicated single-tenant park owned by `KIT_LOCAL`, with `KIT_MKT` as operator (`docs/forensics/KIT-PLATFORM-LOCALOS-TENANT-BOUNDARY-01.md`). That model is not the Pharmacy or TEST_PACK row model.

---

## 9. Authority model (from KPA-07)

KPA-07 is preserved. Conflicts are **not** solved here. Where KPA-07 says UNKNOWN, this document says UNRESOLVED.

### A. `graph_json` vs Production OS

| | |
|--|--|
| CURRENT AUTHORITY | **Split.** Script/lines = product state (`UpsertBuildAsync`). Execution artifacts/bindings = OS. Graph take URLs / `acceptedTake` = LEGACY/COMPATIBILITY per `LegacySeriesGraphAdapter`, **still persisted**. Voice ingress canons still **read** `graph_json.lines`. |
| TARGET AUTHORITY | DECISION REQUIRED: script-only graph vs continued dual-write. OS remains execution/artifact/binding authority. |
| TRANSITION | Dual-write live. Episode timing **rejects** `graph_json`. CREATE_IMAGE ingress does not read graph. Progress still parses graph. |
| UNRESOLVED | Whether desk `runs` after official OS execute is still treated as “the take” in remaining UI paths. |

**Evidence:** KPA-07 A. **Status: TRANSITION + DECISION REQUIRED.**

### B. IndexedDB vs server artifacts

| | |
|--|--|
| CURRENT AUTHORITY | Server `ProductionArtifact` + binding for official execute/preview/voice media. IDB (`kit-famixa-tts`, `kit-famixa-kf`, `kit-famixa-shot-final`) declared CACHE / residual hydrate. |
| TARGET AUTHORITY | Server remains authoritative for official media. IDB stays cache or is retired after hydrate cutover. |
| TRANSITION | Voice hydrate-first IDB still live. |
| UNRESOLVED | Whether every UI playback path has cut over. |

**Evidence:** KPA-07 B. **Status: TRANSITION.**

### C. FilmAssembly vs bindings

| | |
|--|--|
| CURRENT AUTHORITY | FilmAssembly = composition ledger (membership). Bindings = selected artifact. Shot official membership stores **MIX** ids; output artifact is **ASSEMBLY**. |
| TARGET AUTHORITY | Keep both meanings. Do not collapse membership into binding. |
| TRANSITION | Historical MIX membership remains after episode ASSEMBLY. |
| UNRESOLVED | — |

**Evidence:** KPA-07 C. **Status: CURRENT split is the intended product model unless Famixa changes it.**

### D. CURRENT / VISIBLE / ACCEPTED / ROLE:TYPE

| | |
|--|--|
| CURRENT AUTHORITY | Role-string specific. Unique active `(build, shot, role)`. Video preview = `OfficialVideoAuthorityBridgeV1` order: ACCEPTED:VIDEO_TAKE → VISIBLE* → CURRENT:VIDEO_TAKE → bare CURRENT if VIDEO_TAKE. Voice = CURRENT:VOICE only. ACCEPTED is semantic artifact acceptance, **not** platform approval. |
| TARGET AUTHORITY | Remain Famixa execution/selection state. Do not promote to Core approval. |
| TRANSITION | Bare CURRENT is an alias. |
| UNRESOLVED | — |

**Evidence:** KPA-04, KPA-06 §8, KPA-07 D.

### E. Legacy `video_generation_execution` vs Production OS

| | |
|--|--|
| CURRENT AUTHORITY | **Both APIs live.** Official film uses OS. `ContentController` video-generation-execution still executes `IVideoGenerationProvider` (Runway). No arbitrator if both run on one shot. |
| TARGET AUTHORITY | DECISION REQUIRED: retire / compatibility / parallel. |
| TRANSITION | Strangler not closed. Two journals. |
| UNRESOLVED | Winner if both execute — UNKNOWN (KPA-07). |

**Evidence:** KPA-05, KPA-07 E. **Status: DECISION REQUIRED.**

### F. `LatestSnapshotAsync` vs task-bound / `ExpectedSnapshotId`

| | |
|--|--|
| CURRENT AUTHORITY | Execute/verify = `GetSnapshotByTaskAsync`. Official recover/accept = `ExpectedSnapshotId`. Evaluate facts = `LatestSnapshotAsync` (DERIVED). Director duration may use live `IPerformanceSnapshotStore`. |
| TARGET AUTHORITY | Frozen task snapshot remains execute authority. Latest remains evaluation projection unless Famixa changes evaluate. |
| TRANSITION | Eval/exec skew possible; `AssertFrozenMatch` fail-closed on execute. |
| UNRESOLVED | — (execute path does not use Latest; KPA-07 F). |

**Evidence:** KPA-07 F.

### G. ACTIVE selection vs frozen Provider/Model

| | |
|--|--|
| CURRENT AUTHORITY | Pre-freeze HOW = ACTIVE `ProductionAiSelection` (`ProductionCreateVideoProviderBinding.Authoritative`). Post-freeze execute = snapshot/task `ProviderId`/`ModelId`. Official: selection + frozen + `RealVertexExecutionV1Lock` must agree. |
| TARGET AUTHORITY | Same phase split. Product HOW vs frozen execution HOW. |
| TRANSITION | Selection change after freeze requires new task (`NewTaskForProviderChangeAsync`). |
| UNRESOLVED | — |

**Evidence:** KPA-07 G.

### H. Artifact row vs disk

| | |
|--|--|
| CURRENT AUTHORITY | ArtifactId = row identity. ByteSha256 = content identity. StorageKey = location. Disk via `PersistNamed` can overwrite bytes; verified **row** cannot change SHA/key. |
| TARGET AUTHORITY | Same three-way split. Physical write must not silently replace bytes under a verified key (policy DECISION for preview rehash). |
| TRANSITION | `PersistNamed` overwrite remains. |
| CURRENT FACT (later than this table's first draft) | Official preview calls `OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes` and fails closed when the live SHA does not match `ByteSha256`. The "whether preview rehashes" question is answered for that path. `PersistNamed` can still overwrite bytes. |

**Evidence:** KPA-03, KPA-07 H, Master Architecture §28.1, `OfficialPreviewIntegrityV1`. **Status: Famixa behavior implemented. Not a Core store.**

### I. FINAL vs ASSEMBLY / MIX

| | |
|--|--|
| CURRENT AUTHORITY | Shot assemble output = ASSEMBLY; members = MIX. Episode output = ASSEMBLY + CURRENT/VISIBLE:ASSEMBLY. `ProductionBindingRoles.Final` unused. Engine `FINAL` and IDB FinalStore are other meanings of “final.” |
| TARGET AUTHORITY | Do not invent OS episode FINAL. ASSEMBLY remains the OS episode/shot assemble output unless Famixa decides otherwise. |
| CURRENT FACT (later than the refused-POST draft) | Migration 380 allows `ASSEMBLE_EPISODE`. `ContentProductionController` calls `AssembleEpisodeOsAsync`. The action is Famixa-only. The "POST refused / SQL lacks the action" sentence is historical. |
| UNRESOLVED | ASSEMBLE_EPISODE product status; unused FINAL binding future meaning. |

**Evidence:** KPA-07 I. **Status: DECISION REQUIRED for episode assemble API.**

### J. UI/API bypasses

| | |
|--|--|
| CURRENT AUTHORITY | OS is authoritative **only** on `ContentProductionController` `/api/content/production`. Graph upsert, legacy video-execution, video-engine shot/contract APIs, FilesController, ContentWorkWorker do **not** write OS. |
| TARGET AUTHORITY | Bypass routes remain product/legacy/platform islands until decided. FilesController is **not** an OS bypass; it is a separate file island. |
| TRANSITION | All listed routes live. |
| UNRESOLVED | Full remaining client writes of `acceptedTake` / FinalStore after OS preview. |

**Evidence:** KPA-07 J.

---

## 10. Identity model

**Principle:** ArtifactId ≠ ByteSha256 ≠ StorageKey. Do not collapse identities.

| Layer | CURRENT | TARGET | Gap |
|-------|---------|--------|-----|
| User | `public.users` | Keep | OS actor is VARCHAR `created_by` / `selected_by` — PARTIAL (KPA-06 §12) |
| Tenant | `public.tenants` | Keep; required on product execution rows | Historical Production and `series_build` rows may have NULL `tenant_id`. Migration 376 added the nullable column and did not backfill. New official Production writes carry `tenant_id` and fail closed (§28.1). The older "no TenantId" sentence is the pre-376 state |
| Workspace | `kit_workspace` exists | Not the isolation boundary (ADR-03; `CoreAuthority.WorkspaceRequiredForTenantAuthorization` is false). Generic workspace meaning stays deferred | OS has **no WorkspaceId** |
| Product | Implicit pack / module | Explicit product identity if cross-product execution appears | PARTIAL (schema name, not a column) |
| Project / domain | Famixa: `series_code` / BuildId | Product-owned | Not a platform project |
| Work / execution | `ProductionRequest` / `ProductionTask` | Future engine: generic work id | Famixa-scoped today |
| Attempt | `ProductionAttempt` | Future engine pattern | Famixa-hosted |
| Artifact | `ArtifactId` | Future engine pattern + tenant/workspace | No tenant on row |
| Content | `ByteSha256` | Keep as content identity | SHA index not UNIQUE |
| Physical storage | `StorageKey` / PersistNamed path | Location only; tenant-prefixed store FUTURE | Path lacks complete tenant/workspace isolation |

**Architectural requirement (not a schema):** any future generic execution or artifact primitive **must** carry tenant scope before it is used across products. Do not invent columns in this document.

**Evidence:** KPA-01, KPA-03, KPA-06 §6, KPA-07 H. **Status: CURRENT gaps. TARGET requirement.**

---

## 11. Tenant / organization / workspace / product / project

**Evidence:** KPA-01, KPA-07 §5 (missing items). **Status: mixed.**

| Concept | CURRENT | Do not merge |
|---------|---------|--------------|
| Tenant | `public.tenants` — platform SoT | Not a series, not a workspace |
| Organization | Pharmacy **branches** are the practical hierarchy | Not proven as a generic org primitive |
| Workspace | `kit_workspace` is a tenant-scoped package slot. It is not the isolation boundary (ADR-03, OPEN-AUTHORITY §4). A future generic Workspace meaning is still deferred | Not tenant |
| Product | Pack / module (Content, Pharmacy, Family, Local) | Not a project |
| Project | Famixa series/build; Pharmacy store operations | Product-owned |

**TARGET DESIGN / DECISION REQUIRED:** generic Organization and resolved Workspace. This document does not pick the model.

Production and `series_build` isolation today is **pack schema** (`pack_content`) + `series_code` string, not tenant RLS.

---

## 12. Storage / artifact architecture

Three separate concepts:

| | CURRENT | TARGET |
|--|---------|--------|
| A. FILE / BLOB STORAGE | Islands: `/api/files` uploads; KitVideoVideoRoot; App_Data; IDB (KPA-03) | Tenant-aware blob location. **Do NOT promote `IProductionArtifactFileStore` as `IFileStore`.** |
| B. ARTIFACT IDENTITY | Famixa `ProductionArtifact.ArtifactId` (product-specific) | Future generic artifact id only after tenant scope + new type |
| C. CONTENT INTEGRITY | `ByteSha256`; row trigger on verified SHA; disk not locked by `PersistNamed` | Integrity must survive relocation; overwrite of verified location forbidden |

Generic `IFileStore` / `IArtifactStore` **does not exist**. No generic GC. Production artifacts lack TenantId/WorkspaceId.

Target **may** introduce generic storage/artifact primitives later. This document does **not** prescribe interfaces.

**Evidence:** KPA-03, KPA-06 §7, KPA-07 H. **Status: CURRENT islands. TARGET foundation at Stage 3.**

---

## 13. Work / workflow / job / execution

Do not create implementation abstractions. Conceptual boundaries only.

| Concept | CURRENT | TARGET |
|---------|---------|--------|
| Workflow | `IWorkflowEngine` is **Pharmacy-specific** (KPA-02, KPA-04) | Platform workflow only if extracted from proven multi-product need — not by renaming Pharmacy |
| Task | Famixa `ProductionTask` = media action on Shot/Build | Future engine work item ≠ Famixa task |
| Job | `ContentWorkWorker` → `work_job` / publish; **does not consume Production OS** | Keep separate from OS execution |
| Execution / Attempt | Famixa `ProductionAttempt` + provider job | Future engine attempt |
| Approval | Platform approval kernel **absent**. Director approve on **legacy** video-execution is product/legacy | Approval ≠ binding |
| Binding | Famixa selection of artifact by role | Stay in product unless a generic “selected output” is later extracted |
| Artifact acceptance | ACCEPTED binding | **Not** workflow approval (KPA-04, KPA-07 D) |

**Evidence:** KPA-02, KPA-04, KPA-06, KPA-07. **Status: CURRENT product-shaped. TARGET: do not collapse.**

---

## 14. Event / audit / notification

**Evidence:** KPA-04, KPA-07 E. **Status: CURRENT dual-write. TARGET: five distinct planes.**

| Plane | CURRENT | TARGET |
|-------|---------|--------|
| EVENT | `platform_events` / `integration_outbox` and `kit_event` coexist | Platform Event = integration/domain fact with proven handler idempotency |
| AUDIT | `public.audit_logs` and `kit_audit` activity_log dual-write; `kit_audit` read-oriented | One audit plane; not the Event bus |
| NOTIFICATION | `kit_notify` + separate delivery/inbox concepts | Delivery ≠ Event ≠ Audit |
| EXECUTION JOURNAL | `production_task_events` (immutable, empty payload, no actor). `video_generation_execution_event` = second legacy journal | Stay product/execution; **do not** become platform Event |
| OUTBOX | `integration_outbox` present | Outbox = reliable publish, not the journal |

Do not merge them because all contain “events.” Handler idempotency is **not fully proven** (KPA-04). `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` §6 records the same gap after pack onboarding. Local CSDL uniqueness and remote CSDL idempotency are different questions and are not this bus.

---

## 15. AI architecture

**Evidence:** KPA-05, KPA-06 §10–11. **Status: CURRENT = no generic AI plane. TARGET = conceptual planes below.**

CURRENT:

- No generic AI Provider Plane, `IAiGateway`, Model Registry, Agent Runtime.
- `IAiOrchestrator` = Pharmacy health-copilot rules — **not** Famixa execution and **not** a gateway.
- Real model calls are product-owned. Multiple Gemini clients exist. Content key shared to other products (credential concern).
- Famixa has provider/model/attempt/artifact machinery — Content-specific.
- AgentRun / AgentTool / AgentPolicy / ContextPack / Evaluation / HumanApproval: **absent**.

TARGET / FUTURE conceptual planes (do not create now):

| Plane | Role |
|-------|------|
| AI Provider / Model | Identity of provider and model; not Famixa catalog |
| AI Execution | Calls bound to frozen contract + attempt |
| Context | What the model/agent is allowed to see — not data dumping |
| Contract | What must hold — not merely a DTO |
| Evaluation | Did the result satisfy the contract |
| Agent | Goal-directed tool use under policy |
| Human Approval | Explicit approval contract — not ACCEPTED binding |

---

## 16. Agent architecture

**Status: FUTURE. Does not exist.**

Long-term model (not implemented):

Agent → Agent Run → Agent Tool → Agent Policy → Context → Contract → Authorization → Execution → Provenance → Evaluation → Human Approval.

**Why Agent Runtime belongs at platform level:** tools, policy, tenant authorization, provenance, and evaluation are cross-product. **Why product semantics stay in packs:** a Famixa tool that creates VIDEO_TAKE is still Famixa; a Pharmacy tool that posts a sale is still Pharmacy.

Do not implement. Do not claim these types exist. Agent Runtime before Context/Contract/Authorization/Execution/Provenance would recreate today’s product islands with a new name.

---

## 17. Context / contract / provenance

**Status: TARGET.**

| Concept | Is not | CURRENT Famixa analogue (product-shaped) |
|---------|--------|------------------------------------------|
| Context | Data dumping | Scope + performance snapshot + selection — not a platform ContextPack |
| Contract | Merely a DTO | `ShotExecutionCompiler` / action contracts — Famixa media |
| Provenance | Merely audit log | Task → snapshot → attempt → job → artifact → binding; tenant/workspace/actor **gaps** |

TARGET chain:

```
Context → Contract → Authorization → Execution → Artifact
       → Verification → Provenance → Evaluation → Human approval (where required)
```

**Evidence:** KPA-05 (absent planes), KPA-06 §12 (provenance PARTIAL/ABSENT).

---

## 18. Security / authorization

**Evidence:** KPA-01. **Status: CURRENT Core. TARGET extensions labelled.**

CURRENT authoritative: JWT, permissions, roles, `ITenantContext`. Server-side authorization is authoritative. Client UI is not.

TARGET requirements (do not invent unstated policies):

| Concern | Status |
|---------|--------|
| Tenant isolation | CURRENT for Core APIs; **ABSENT** on Production OS rows/disk (requirement) |
| Workspace isolation | TARGET where workspace is defined |
| Product/module authorization | CURRENT modules |
| Execution authorization | CURRENT = Famixa permit/cost (product). TARGET = tenant + module + optional engine port |
| Provider authorization | CURRENT = product credentials. TARGET = credential ownership decision |
| Artifact access | CURRENT = pack API. TARGET = tenant-scoped artifact |
| Agent tool authorization | FUTURE |

Pharmacy RBAC invariants remain (staff must not inherit financial reports from `sales.*`). Do not regress them when discussing “generic authorization.”

---

## 19. Repository / project boundary

**Do not rename or move projects.** Conceptual mapping only.

| Current project / area | Conceptual layer | Alignment |
|------------------------|------------------|-----------|
| `src/KitPlatform.Api` + Application/Infrastructure platform hosts | Core runtime + composition | CURRENT |
| Identity / tenant / permission services | Core | CURRENT |
| `kit_*` tables and dual-write adapters | Strangler overlay | CURRENT MISALIGNMENT vs TARGET unified Audit/Event/Notify |
| `src/Packs/Content` Application + Infrastructure | FAMIXA product pack | CURRENT. Contains Production OS **and** legacy video-execution |
| `client/admin` Content / Director | FAMIXA UI | CURRENT. Also IDB + graph writers |
| Pharmacy pack + `IWorkflowEngine` | NOVIXA / Pharmacy product | `IWorkflowEngine` is in the Pharmacy pack (`KIT-PLATFORM-CORE-BOUNDARY-06`). `IAiOrchestrator` still sits in Application and returns customer-health types. `KitPlatform.Infrastructure` still references Pharmacy.Application. Those two are pack boundary debt, not Core workflow |
| Family OS / Local OS packs | Product packs (parked) | Separate manifests — keep |
| Veo / Runway / Fal / ElevenLabs adapters | Provider implementations (product-owned) | CURRENT in Content Infrastructure |
| `IProductionArtifactFileStore` / `KitVideoVideoStore` | Product storage adapter | CURRENT MISALIGNMENT vs future Core storage |
| Production Engine project | — | **Does not exist** |

TARGET: Core | (future) Production Engine | Product Packs | Infrastructure adapters | Provider implementations | UI.

**CURRENT MISALIGNMENT:** Content pack owns official OS, legacy video-execution, graph, and file store. Pharmacy owns workflow/AI names that look like Core. `kit_*` overlays sit beside `public.*`. Do not fix in this phase.

---

## 20. Promotion / deprecation matrix

| Component / Concept | Current Location | Current Authority | Target Layer | Promotion Status | Reason | Dependency | Migration Risk |
|---------------------|------------------|-------------------|--------------|------------------|--------|------------|----------------|
| `public.users` | public.* | Identity SoT | Core | **KEEP** | Proven Core | — | Low if left |
| `public.tenants` | public.* | Tenant SoT | Core | **KEEP** | Proven Core | — | Low |
| JWT / `ITenantContext` | API | Request tenant | Core | **KEEP** | Proven Core | — | Low |
| permissions | Core RBAC | Authz SoT | Core | **KEEP** | Proven Core | — | Low (do not regress Pharmacy) |
| `kit_accounts` | kit_* | Overlay | Strangler | **STRANGLER** | Not identity SoT (KPA-01/02) | users | Medium |
| `kit_workspace` | kit_* | Unresolved | Core or overlay | **DECISION REQUIRED** | Semantics not proven | Tenant model | High |
| `kit_audit` | kit_* | Read-oriented dual-write | Core Audit (later) | **STRANGLER** | Dual-write with `audit_logs` (KPA-04) | Audit decision | High |
| `kit_workflow` | kit_* | Pharmacy-shaped | Product | **DO NOT PROMOTE** | Not generic workflow (KPA-02) | — | High if promoted |
| `kit_event` | kit_* | Dual-write Event | Core Event (later) | **STRANGLER** | Coexists with platform_events (KPA-04) | Event decision | High |
| `kit_notify` | kit_* | Notify/inbox | Core Notify (later) | **STRANGLER** / **KEEP** as overlay until decision | Distinct from Event | Notify decision | Medium |
| `/api/files` | Api FilesController | Upload island | Core storage later | **KEEP** as island; **DO NOT PROMOTE** as artifact | Not ProductionArtifact (KPA-03, KPA-07 J) | Storage foundation | Medium |
| ProductionRequest | pack_content | Famixa request | Famixa | **DO NOT PROMOTE** | Media actions; FK series_build | — | High |
| ProductionTask | pack_content | Famixa media task | Famixa | **DO NOT PROMOTE** | Shot scope (KPA-06) | — | High |
| ProductionTaskInputSnapshot | pack_content | Famixa freeze | Famixa; pattern **FUTURE EXTRACTION** | **DO NOT PROMOTE** type | Story/Acting/Visual (KPA-06) | Tenant first | High |
| ProductionAttempt | pack_content | Famixa attempt | Famixa; pattern **FUTURE EXTRACTION** | **DO NOT PROMOTE** yet | Concept reusable; table not (KPA-06) | New types | Medium |
| ProductionArtifact | pack_content | Famixa artifact | Famixa; pattern **FUTURE EXTRACTION** | **DO NOT PROMOTE** | Roles/duration; no tenant | Storage + tenant | High |
| ProductionBinding | pack_content | Shot selection | Famixa | **DO NOT PROMOTE** | Not approval (KPA-04/07) | — | High |
| ProductionAiSelection | pack_content | Famixa HOW | Famixa | **DO NOT PROMOTE** | Media stages (KPA-05/06) | — | Medium |
| Famixa provider registry | Content Application | Famixa catalog | Famixa | **DO NOT PROMOTE** | Named Famixa; media ports | — | High |
| `IProductionArtifactFileStore` | Content | PersistNamed .mp4 | Famixa adapter | **DO NOT PROMOTE** | Not IFileStore (KPA-03/06) | — | High |
| FilmAssembly | pack_content | Composition ledger | Famixa | **DO NOT PROMOTE** | Shot/MIX/ASSEMBLY (KPA-06/07) | — | High |
| `graph_json` | series_build | Split product/legacy | Famixa | **COMPATIBILITY** + **DECISION REQUIRED** | Still written (KPA-07 A) | Graph decision | High |
| `video_generation_execution` | pack_content | Legacy live API | Famixa legacy | **COMPATIBILITY** + **DECISION REQUIRED** | No arbitrator (KPA-07 E) | Retirement decision | High |
| `production_task_events` | pack_content | Execution journal | Famixa | **DO NOT PROMOTE** | Not platform Event (KPA-04/06) | — | Medium |
| `IAiOrchestrator` | Pharmacy | Health copilot | Pharmacy | **DO NOT PROMOTE** | Not AI plane (KPA-05) | — | High |
| `IWorkflowEngine` | Pharmacy pack (`Workflow/IWorkflowEngine.cs`) | Pharmacy workflow | Pharmacy | **DO NOT PROMOTE** | Moved out of Application in BOUNDARY-06. The older "contaminates Application" sentence is historical | — | High |

Do not label **DEPRECATE LATER** unless a replacement authority is decided. Graph take URLs and legacy video-execution are **COMPATIBILITY** while still writable.

---

## 21. P0 / P1 architectural risks

Carry-forward only. **Evidence:** KPA-03–07. No new invented P0/P1.

**P0**

1. Production OS and `series_build` lack TenantId / WorkspaceId.
2. `PersistNamed` can overwrite physical bytes under the same StorageKey.
3. Famixa-owned permit/credential ownership and cross-product credential concerns (Content Gemini key shared — KPA-05).
4. Promoting Famixa Production OS into Core would contaminate Core.

**P1**

5. `LatestSnapshotAsync` vs task-bound snapshot authority.
6. ACTIVE selection vs frozen provider/model.
7. ACCEPTED ≠ approval.
8. `ContentWorkWorker` ≠ Production OS worker.
9. `ASSEMBLE_EPISODE` in C# vs SQL CHECK (mig 372).
10. Partial actor provenance / empty `production_task_events` payload.
11. `ParentArtifactIds` omitted from SnapshotHash.
12. `IAiOrchestrator` / `IWorkflowEngine` are non-authoritative for Production OS.
13. Dual-live legacy video-generation-execution.
14. `graph_json` remains live.
15. Preview SHA recheck not proven.

---

## 22. Architectural decisions required

This document does **not** choose product options unless already proven.

### 1. `graph_json`: script-only vs continued dual-write

- **Evidence:** KPA-07 A. Script/lines still authoritative for desk and voice ingress. Take URLs classified legacy but persisted. OS does not write graph.
- **Why architecture cannot decide:** Product still uses graph as the series document. Forensic cannot retire a live writer.
- **Owner:** Famixa product + platform architecture.
- **Consequences:** Script-only → desk must stop persisting takes; OS preview becomes the only take SoT. Dual-write → Stage 1 continues; single SoT remains false.

### 2. Legacy `video_generation_execution`

- **Evidence:** KPA-07 E. Both APIs live. UNKNOWN winner if both run.
- **Why not decided:** Live `ContentController` routes; progress still reads `_vge`.
- **Owner:** Famixa + API owners.
- **Consequences:** Retire → engine UI/progress must move. Compatibility-only → mark and freeze new callers. Parallel → permanent dual film risk.

### 3. Tenant / workspace model for Production

- **Evidence:** KPA-03, KPA-06, KPA-07. No tenant on OS or `series_build`. `kit_workspace` semantics unresolved.
- **Why not decided:** Requires org/workspace product model (Pharmacy branches ≠ Famixa series).
- **Owner:** Platform architecture + product owners.
- **Consequences:** Without tenant scope, Stage 5 engine extraction is unsafe across products.

### 4. Credential ownership

- **Evidence:** KPA-05. Multiple Gemini clients; Content key leak to other products; Famixa Veo permit hard-locked.
- **Why not decided:** Security/product ownership, not a naming issue.
- **Owner:** Platform security + pack owners.
- **Consequences:** Shared keys remain a cross-product risk. Split ownership requires a credential plane (FUTURE).

### 5. Pharmacy / Family OS / Local OS vs shared primitives

- **Evidence:** KPA-01; park rules; separate manifests. Forensic did not re-audit those packs in KPA-07.
- **Why not decided:** Isolation is a policy already; sharing Core primitives (audit/event/storage) needs an explicit allow-list.
- **Owner:** Platform + those product owners.
- **Consequences:** Shared Event/Audit without tenant discipline repeats dual-write. Isolation forever → Core stays thin (acceptable).

### 6. Remaining UI writes to `acceptedTake` / FinalStore

- **Evidence:** KPA-07 A/B/J UNKNOWN.
- **Why not decided:** Full client write inventory not closed.
- **Owner:** Famixa UI.
- **Consequences:** Hidden product state continues beside OS.

### 7. Preview SHA verification policy

- **Evidence:** KPA-07 H. Assemble rehashes; preview GET not proven.
- **Why not decided:** Runtime path not fully proven; policy choice (fail-closed vs stream).
- **Owner:** Famixa Production OS.
- **Consequences:** Without rehash, overwrite yields silent wrong playback.

### 8. `ASSEMBLE_EPISODE` status

- **Evidence:** KPA-07 I. C# contract/tests exist; Director POST refused; SQL CHECK lacks action.
- **Why not decided:** Product/API readiness, not architecture layering.
- **Owner:** Famixa.
- **Consequences:** Leave refused → episode concat stays off live API. Enable later → still Famixa, not Core.

---

## 23. Do not build

**Status: CURRENT prohibition.**

Do not:

- move Famixa `ProductionTask` into KIT Core
- promote Famixa `ProductionArtifact` directly into generic Artifact
- promote `IProductionArtifactFileStore` into generic `IFileStore`
- promote `ProductionBinding` into platform approval
- promote ACCEPTED into workflow approval
- promote Famixa provider registry
- create a generic AI Gateway by renaming `IAiOrchestrator`
- create a generic Workflow Engine from Pharmacy `IWorkflowEngine`
- create generic Event from `production_task_events`
- create Agent Runtime before Context / Contract / Authorization / Execution / Provenance foundations
- create a generic Production Engine by renaming Famixa classes
- resolve authority conflicts by naming conventions
- implement this architecture before §20 decisions are explicitly approved
- add Production OS to `migration-files.prod.txt` / Pharmacy / Family OS manifests as a “platform” migrate
- silently pick a winner where KPA-07 says UNKNOWN

---

## 24. Target architecture diagrams

### A. Platform boundary

```
                    ┌──────────────────────────┐
                    │   External providers     │
                    │  (product-owned today)   │
                    └────────────▲─────────────┘
                                 │ FUTURE: engine port / product adapter
┌────────────────────────────────┼────────────────────────────────┐
│                         KIT PLATFORM                            │
│  ┌─────────────────────┐  ┌──────────────────┐  ┌────────────┐  │
│  │ CORE (CURRENT)      │  │ PRODUCTION       │  │ PRODUCT    │  │
│  │ tenant user JWT     │  │ ENGINE           │  │ PACKS      │  │
│  │ permissions modules │  │ [FUTURE]         │  │ NOVIXA     │  │
│  │                     │  │ new types only   │  │ FAMIXA     │  │
│  │ kit_* STRANGLER     │  │                  │  │ Family/Local│ │
│  └─────────────────────┘  └──────────────────┘  └────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### B. Runtime dependency direction (TARGET)

```
UI  →  Product API  →  Product domain  →  (optional) Production Engine
                 ↘  Core (tenant, user, authz)
Provider adapters ← Product or Engine port
        ✗  Product must not depend on another product's domain types
        ✗  Core must not depend on Famixa or Pharmacy domain types
```

**CURRENT MISALIGNMENT:** Content Infrastructure hosts Famixa adapters and the KitVideo store. Pharmacy `IWorkflowEngine` no longer lives in Application. The remaining shared-to-product compile edge is `KitPlatform.Infrastructure` referencing Pharmacy.Application, recorded as pack boundary debt in §29.

### C. Core / Production Engine / Product

```
        CORE                     ENGINE [FUTURE]              FAMIXA [CURRENT]
 tenant, user, JWT          work, freeze hash,          Shot, graph_json,
 permissions, modules       attempt, artifact id≠hash,  MIX/LIPSYNC/ASSEMBLY,
                            job id, verify status,      FilmAssembly, Director,
                            journal, LATE               IFamixaProviderRegistry,
                                                        Veo permit/cost
```

### D. AI future architecture

```
FUTURE:
  ContextPack → Contract → Authz → Provider/Model → Execution/Attempt
       → Artifact → Verify → Provenance → Evaluation → HumanApproval
       → Agent Runtime (tools + policy)

CURRENT:
  IAiOrchestrator = health copilot
  Famixa registry + OS execute = Content product
  no IAiGateway / Model Registry / Agent
```

### E. Artifact identity chain

```
ArtifactId  (row)     ≠    ByteSha256  (bytes)    ≠    StorageKey  (path)
     │                         │                          │
  production_artifacts      SHA256(file)            PersistNamed *.mp4
  [no TenantId today]       index, not UNIQUE       overwrite possible
```

### F. Authority model (Famixa production, CURRENT)

```
PRODUCT STATE          EXECUTION STATE              CACHE / LEGACY
graph script/lines     snapshot (task-bound)        IDB TTS/KF/final
ACTIVE selection       frozen provider/model        graph acceptedTake URLs
FilmAssembly members   bindings CURRENT/VISIBLE/    video_generation_execution
                       ACCEPTED / ROLE:TYPE
                       ArtifactId + SHA claim
                       production_task_events
                                              DISK = physical, not identity
```

### G. Current → Target transition

```
S0 CURRENT ─► S1 classify authority ─► S2 tenant/workspace
         ─► S3 storage/artifact ─► S4 execution concepts
         ─► S5 new Production Engine types ─► S6 AI/Agent
         ✗ skip S2/S3 and extract engine
         ✗ rename Famixa into Core
```

---

## 25. Architectural principles

Supported by KPA-01–07 unless marked target.

1. **Authority must be explicit.** (KPA-07)
2. **Naming is not authority.** (KPA-06, KPA-07)
3. **Product semantics do not become platform primitives merely because they are reusable.** (KPA-06)
4. **Execution state is different from product state.** (KPA-07 A/C)
5. **Artifact identity is different from content identity.** (KPA-03, KPA-06)
6. **Physical storage location is not identity.** (KPA-03, KPA-07 H)
7. **Frozen execution input is different from latest mutable state.** (KPA-07 F)
8. **AI provider identity must be frozen for reproducible execution.** (KPA-07 G)
9. **ACCEPTED is not approval unless an approval contract explicitly exists.** (KPA-04, KPA-07 D)
10. **Cache is not authority.** (KPA-07 B)
11. **Journal is not state.** (KPA-04, KPA-06 §15)
12. **Legacy compatibility must be explicitly classified.** (KPA-07)
13. **Tenant scope must exist before generic cross-product execution.** (KPA-03, KPA-06)
14. **Platform abstractions must be extracted from proven patterns, not from names.** (KPA-06)
15. **Architecture must precede implementation.** (this checkpoint)
16. **Server-side authorization is authoritative; client UI is not.** (KPA-01) — target principle aligned with current Core
17. **Do not silently resolve UNKNOWN.** (KPA-07) — target principle for this program

---

## 26. Final target state (concise)

```
KIT PLATFORM
  ├── Core Runtime                         mostly CURRENT; storage/event/audit unification FUTURE
  │     Identity · Tenant · Authorization · Modules
  │     Organization / Workspace           DECISION REQUIRED
  │     Config
  │     Audit · Event · Notification       TARGET unified planes; CURRENT dual-write
  │     Storage primitives                 FUTURE
  │
  ├── Production Engine                    FUTURE — new types, not Famixa moved
  │     Work · Contract · Frozen input
  │     Execution · Attempt · Provider Job
  │     Artifact · Verification · Provenance · Replay
  │
  └── Product Packs
        ├── NOVIXA
        ├── FAMIXA
        │     └── Production Domain        CURRENT and remaining
        └── Future Products
```

CURRENT strongest execution system = Famixa Production OS. TARGET strongest **generic** execution system = a Production Engine that does not yet exist. TARGET AI/Agent platform = after Stages 2–5.

---

## 27. Consistency checklist

| Rule | How this draft complies |
|------|-------------------------|
| Preserve KPA terminology | Production OS, ACCEPTED, LatestSnapshotAsync, PersistNamed, ROLE:TYPE, strangler, journal |
| Current vs target | Every major section labelled |
| Proven vs future | Production Engine, Agent, IFileStore, ContextPack marked FUTURE |
| Authority vs compatibility | KPA-07 map preserved; UNKNOWN kept |
| No implementation | No code, SQL, interfaces |
| No fake generics | Engine = extraction later, not a rename |
| No silent UNKNOWN resolution | §9, §20, §22 |

---

## ARCHITECTURE CHECKPOINT

Forensic Phase: COMPLETE  
Architecture Phase: V1 DRAFT  
Implementation: NOT STARTED  
Migrations: NOT STARTED  
Production Changes: NONE

Architecture must be reviewed and approved before implementation begins.

The checkpoint lines above are the original V1 draft footer. They are not the current implementation state. See §28.

---

## 28. Proven current state after Production OS V1 closeout

Status: CURRENT FACTS. This section does not retarget the architecture and does not promote Famixa types into Core.

Source: `docs/forensics/PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01.md`.

### 28.1 Proven Famixa Production OS capabilities

These are product-specific. They are not a Production Engine.

- Tenant-scoped Production writes and reads for a caller tenant, with historical NULL-tenant rows left unmapped.
- Frozen `ProductionTaskInputSnapshot` for official execution, including provider and model.
- Tenant-owned credential identity and version freeze, encrypted credential storage, and credential rotation, proven by their regressions. Not promoted to a platform credential plane.
- Artifact identity distinct from SHA and storage key. Official preview re-reads bytes and compares SHA.
- Binding is the official state pointer. Episode output for the tested EP01 uses `CURRENT:ASSEMBLY` and `VISIBLE:ASSEMBLY`.
- One tenant-owned EP01 episode assembly executed, replayed, and isolated. See the closeout for ids and hashes.

### 28.2 Current platform gaps

- `graph_json` is still both script state and a residual take document (ADR-01, ADR-06).
- Legacy `video_generation_execution` is not retired (ADR-02).
- Historical Production rows with NULL `tenant_id` are fail-closed, not owned.
- First-insert concurrency for a new episode identity is not proven.
- CK23B historical lipsync regression throws `PRODUCTION_CREDENTIAL_UNRESOLVED`. Cause not classified here.
- The original ADR-07 analysis said preview does not rehash. The code does, through `OfficialPreviewIntegrityV1`. The Decision Register and the ADR-07 brief status line now record that implemented Famixa behavior. It is not a Core artifact primitive.

### 28.3 Future Production Engine candidates

A later extraction could consider execution, attempt, artifact verification, and provenance only after Famixa types stay behind a new generic type. This section does not name those types and does not authorize the extraction.

### 28.4 Future Core primitives

Identity, tenant, and authorization stay shared. Storage, workflow, and a credential plane are not current Core primitives.

### 28.5 Forbidden premature promotions

Do not move ProductionTask, ProductionArtifact, ProductionBinding, Famixa provider adapters, or the credential domain into KIT Core or into a generic Workflow, Storage, Artifact, Execution, or Agent Runtime in the name of this closeout.

---

## 29. Reading after pack onboarding

Status: CURRENT READING. This section does not replace the target in §4 or §26. It records how to read this document after TEST_PACK, Local OS, and Pharmacy onboarding.

Source: `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` and `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md`.

- Master Architecture V1 remains the target architecture.
- Current operating state is **STATE B**: Core is stable enough to run the proven packs. It is not ready for the first generic Agent-chain primitive.
- **NO CORE CHANGE** is required for those pack patterns.
- Core that is actually reusable: Identity, Tenant, Authorization, Module enablement, and the Event/Outbox port.
- Product behavior stays in packs: Pharmacy / Novixa, Local OS, the TEST_PACK canary, Famixa Production OS, Pharmacy customer-health orchestration, the Famixa provider/model/attempt/artifact stack, and Content-specific storage.
- Pack boundary debt stays debt. It includes the Infrastructure reference to Pharmacy.Application, Application-hosted Pharmacy and healthcare contracts, `IAiOrchestrator` returning customer-health types, `ContentParkGeminiKeyResolver` shared across packs, `FilesController` using Pharmacy authorization, and Pharmacy catalog/sales/stock controllers that lack `RequirePlatformModule`.
- Tenancy is not one shape. Pharmacy and TEST_PACK are tenant-scoped. Local OS is dedicated single-tenant. Do not copy the Local OS operator model onto Pharmacy, and do not add `tenant_id` to `pack_local` from this sentence.
- Future-agent capability remains a target. Do not start Work, Context, Contract, Execution, Artifact, Verification, Provenance, Human Approval, or Agent Runtime as a speculative Core subsystem.
- Provider and model catalogs stay in packs. `CoreAuthority` does not name a model. The shared Gemini resolver is pack boundary debt, not a Core AI type.
