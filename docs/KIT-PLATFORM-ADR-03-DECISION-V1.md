# ADR-03 — TENANT-ONLY PRODUCTION SCOPE

Status: DECIDED

Decision: OPTION 1 — TENANT-ONLY PRODUCTION SCOPE

Record: KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md  
Analysis: KIT-PLATFORM-ADR-03-TENANT-WORKSPACE-DECISION-BRIEF-V1.md  
Forensic: KPA-01 → KPA-07  
Master Architecture: V1

---

## Decision

ADR-03 is **DECIDED**.

**OPTION 1 — TENANT-ONLY PRODUCTION SCOPE**

Formal statement:

> Production execution must be attributable to the platform Tenant represented by `public.tenants`.
>
> Workspace is NOT a mandatory Production boundary.
>
> Workspace remains a separate KIT Platform primitive and may be used by products that have a genuine workspace concept, but Production Engine scope must not inherit Workspace merely because `kit_workspace` exists.

---

## Architectural Rule

1. Tenant is the mandatory isolation boundary for Production.
2. A future generic Production Engine **MUST** be tenant-scoped.
3. A Production work item cannot be authorized merely because the caller knows a BuildId, TaskId, ArtifactId, or StorageKey.
4. Production identifiers such as BuildId, ArtifactId, and TaskId must not become global authorization identities.
5. Workspace is optional and product-dependent.
6. Workspace is **NOT** required for Famixa Production.
7. `kit_workspace` is **NOT** promoted into a Famixa Production Workspace.
8. Pharmacy Branch is **NOT** converted into generic Workspace.
9. Famixa Series is **NOT** converted into Workspace.
10. Famixa Build is **NOT** converted into Workspace.
11. Organization remains distinct from Tenant and Workspace.
12. Product remains distinct from Tenant and Workspace.
13. Product Pack remains an engineering boundary, not a substitute for tenant isolation.

---

## Why

Factual rationale (not a ranking of options):

- `public.tenants` is the only already-proven platform Tenant SoT (KPA-01).
- JWT `tenant_id` and `ITenantContext` are current Core tenant authority.
- Production currently lacks tenant attribution: no TenantId on `production_*` or `series_build`; queries are not tenant-predicated; no proven RLS on those tables; disk paths are not tenant-prefixed (ADR-03 brief).
- Workspace semantics are not proven as Famixa Production semantics. Production OS does not use `kit_workspace`. `kit_workspace` remains broader kernel workspace semantics (tenant-scoped row; multi-solution comment).
- Making Workspace mandatory would introduce a new product meaning that has not been established.
- Tenant-only satisfies the minimum isolation boundary required for future cross-product execution without forcing Workspace into products that do not require it.
- Option 3 (pack isolation only) remains incompatible with a tenant-safe cross-product Production Engine.

---

## What This Does Not Mean

This decision does **NOT** mean:

- TenantId is already present on Production tables.
- Production is already tenant-safe.
- RLS is already implemented.
- Storage is already tenant-isolated.
- Existing Production rows are already attributable to tenants.
- Workspace is removed.
- `kit_workspace` is removed.
- Workspace can never be used by a future product.
- Production Engine now exists.
- `ProductionTask` becomes generic.
- `ProductionArtifact` becomes generic.
- Famixa Production OS is promoted into KIT Core.

**Current implementation remains unchanged.**

---

## Workspace Rule

- Workspace remains a valid KIT Platform primitive.
- Workspace is tenant-scoped (`kit_workspace.workspace_workspace.tenant_id` → `public.tenants`).
- Workspace semantics remain product-dependent.
- Workspace is **not** part of the Production isolation contract.
- A future product may use Workspace internally if its product model requires it.
- Such use must not redefine the global meaning of Workspace.
- No Production code should assume Workspace exists.

---

## Production Engine Implication

Future Production Engine (does **not** exist today):

| Axis | Rule |
|------|------|
| Tenant | **MANDATORY** |
| Workspace | **OPTIONAL / PRODUCT-DEPENDENT** |
| Product | **EXPLICIT** |
| Project | **EXPLICIT IF NEEDED** |
| Famixa Series / Build | **PRODUCT-SPECIFIC AND NOT ENGINE TYPES** |
| ProductionTask | **DO NOT PROMOTE** |
| ProductionArtifact | **DO NOT PROMOTE** |
| ProductionBinding | **DO NOT PROMOTE** |
| Famixa provider registry | **DO NOT PROMOTE** |
| Famixa FilmAssembly | **DO NOT PROMOTE** |

The future Production Engine must use **NEW generic types**.

---

## Required Future Gate

Before implementing tenant-safe Production, **GATE A** must be satisfied.

Minimum architectural requirement:

- Production load/store paths must refuse work that is not attributable to the caller’s `public.tenants` tenant.
- BuildId / TaskId / ArtifactId / StorageKey must not bypass tenant authorization.
- Physical artifact location must not permit one tenant to overwrite another tenant’s logical object.
- Actor provenance should use platform identity rather than the current VARCHAR-only actor where required by the future execution model.

Do not design how this is implemented in this record.

**DECISION CLOSED ≠ IMPLEMENTATION COMPLETE.**

---

## Impact on Other ADRs

Status of other ADRs is **unchanged**.

| ADR | Note |
|-----|------|
| 01 graph_json | **NOT BLOCKED BY ADR-03.** |
| 02 legacy video execution | **NOT BLOCKED BY ADR-03.** |
| 04 credentials | ADR-03 provides the tenant boundary but does **NOT** decide credential ownership. |
| 05 shared primitives | ADR-03 establishes the tenant requirement before cross-product Storage/Execution sharing. |
| 06 UI writes | **NOT BLOCKED BY ADR-03.** |
| 07 preview SHA | **NOT BLOCKED BY ADR-03.** |
| 08 ASSEMBLE_EPISODE | **NOT BLOCKED BY ADR-03.** |

---

## Implementation Status

- **DECIDED:** Tenant is the Production isolation boundary. Workspace is not mandatory and is not a Famixa Production workspace. A status of DECIDED is not a status of IMPLEMENTED.
- Local OS is not covered by this Production decision. It is a dedicated single-tenant park.

Sentences earlier in this file that say Production lacks `tenant_id` remain part of this decision. This section does not replace them.
