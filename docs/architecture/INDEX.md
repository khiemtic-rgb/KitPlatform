# KIT Platform Architecture

**Version:** V1  
**Status:** ACTIVE  
**Last Updated:** 2026-10-05  
**Purpose:** Primary entry point for KIT Platform architecture documentation

---

## 1. Architecture Status

KIT Platform architecture is **REPOSITORY-CANONICAL**.

The repository is the Architecture Source of Truth.

**Current Baseline:** V1

**Operating State:** STATE B
- Core is stable enough to operate proven packs
- NOT ready for the first generic Agent-chain primitive
- NO CORE CHANGE required for proven pack patterns

**Architecture Changes:** Changes to KIT Platform architecture require governed decisions per the Architecture Decision Register.

**This Document:** This is the navigation entry point. It does not override the Master Architecture.

---

## 2. Authority Hierarchy

**This INDEX document is a navigation entry point ONLY. It is NOT an authority level.**

The architecture authority hierarchy is:

```
ARCHITECTURE NAVIGATION (INDEX)
        ↓
Level 1 — Master Architecture
        ↓
Level 2 — Architecture Decision Register
        ↓
Level 3 — ADR / Specific Decision
        ↓
Level 4 — Supporting Evidence
        ↓
Level 5 — Work / Implementation
```

**Important:** The Index navigates to authority. It does not override the Master Architecture or any other architecture authority.

---

## 3. Canonical Architecture Documents

### 3.1 Architecture Authority

**Level 1 — Master Architecture**

| Document | Path | Role |
|----------|------|------|
| Master Architecture V1 | `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` | Technical architecture definition; platform structure, layers, boundaries, principles |

**Level 2 — Architecture Decision Register**

| Document | Path | Role |
|----------|------|------|
| Architecture Decision Register V1 | `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` | Decision tracking; ADR-01 through ADR-08 status |

**Level 3 — ADR / Specific Decisions**

| Document | Path | Role |
|----------|------|------|
| ADR-03 Decision V1 | `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | DECIDED: Tenant-only Production scope |
| ADR-04 Decision V1 | `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md` | DECIDED: Tenant-owned Production credentials |

### 3.2 Architecture Governance

| Document | Path | Role |
|----------|------|------|
| Architecture Governance | `docs/architecture/GOVERNANCE.md` | Governance model for architecture documentation lifecycle |
| Documentation State V1 | `docs/architecture/KIT-PLATFORM-ARCHITECTURE-DOCUMENTATION-STATE-V1.md` | Current documentation state and tracking model |
| Consolidation Matrix V1 | `docs/architecture/KIT-PLATFORM-ARCHITECTURE-CONSOLIDATION-MATRIX-V1.md` | Gap analysis and conformance matrix |
| Promotion Plan V1 | `docs/architecture/KIT-PLATFORM-ARCHITECTURE-DOCUMENTATION-PROMOTION-PLAN-V1.md` | Documentation promotion and tracking plan |

### 3.3 Level 4 — Supporting Evidence (Decision Briefs)

| Document | Path | Role |
|----------|------|------|
| ADR-01 Brief | `docs/KIT-PLATFORM-ADR-01-GRAPH-JSON-AUTHORITY-DECISION-BRIEF-V1.md` | graph_json vs Production OS authority analysis |
| ADR-02 Brief | `docs/KIT-PLATFORM-ADR-02-LEGACY-VIDEO-EXECUTION-DECISION-BRIEF-V1.md` | Legacy video execution authority analysis |
| ADR-03 Brief | `docs/KIT-PLATFORM-ADR-03-TENANT-WORKSPACE-DECISION-BRIEF-V1.md` | Tenant/Workspace decision analysis |
| ADR-04 Brief | `docs/KIT-PLATFORM-ADR-04-CREDENTIAL-OWNERSHIP-DECISION-BRIEF-V1.md` | Credential ownership decision analysis |
| ADR-05 Brief | `docs/KIT-PLATFORM-ADR-05-SHARED-PRIMITIVES-DECISION-BRIEF-V1.md` | Shared platform primitives analysis |
| ADR-06 Forensic | `docs/KIT-PLATFORM-ADR-06-UI-WRITES-HIDDEN-STATE-FORENSIC-V1.md` | UI write patterns forensic evidence |
| ADR-07 Brief | `docs/KIT-PLATFORM-ADR-07-PREVIEW-SHA-DECISION-BRIEF-V1.md` | Preview SHA handling analysis |
| ADR-08 Brief | `docs/KIT-PLATFORM-ADR-08-EPISODE-ASSEMBLY-DECISION-BRIEF-V1.md` | Episode assembly authority analysis |
| Decision Synthesis V1 | `docs/KIT-PLATFORM-DECISION-SYNTHESIS-V1.md` | Historical aggregate of forensics |
| Authority Stabilization 01C | `docs/KIT-PLATFORM-AUTHORITY-STABILIZATION-01C-IMPLEMENTATION-PROMPT.md` | Implementation guidance |

### 3.4 Level 4 — Supporting Evidence (Critical Forensics)

| Document | Path | Role |
|----------|------|------|
| Architecture Decision Sync Report | `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md` | Current state reconciliation |
| Post-Pack Audit | `docs/forensics/KIT-PLATFORM-ARCHITECTURE-AUDIT-POST-PACK-01.md` | Pack onboarding validation |
| Open Authority Decision Record | `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` | Open authority decision record |
| Production OS Closeout Review | `docs/forensics/PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01.md` | Production OS V1 closeout |
| KPA-01 through KPA-07 | `docs/forensics/KPA-0[1-7]-*.md` | Forensic basis documents |

---

## 4. Supporting Evidence

**Definition:** Documents that support architecture decisions but are not themselves authority.

**Types:**
- ADR briefs (analytical foundation for decisions)
- Forensic reports (audit results, analysis, reconciliation)
- Checkpoint documentation (implementation closeouts)
- Reconciliation reports (drift documentation)

**Authority Status:** Supporting evidence is not architecture authority. It becomes canonical only through the explicit promotion process defined in Architecture Governance §8.

**Traceability:** Critical forensic evidence is tracked for decision traceability and audit requirements.

---

## 5. Architecture Navigation

### 5.1 Routing Table

| Question | Start Here | Section |
|----------|-----------|---------|
| **What is KIT Platform?** | Master Architecture V1 | §1 Architectural north star |
| **What are the platform layers?** | Master Architecture V1 | §2 Three-layer boundary |
| **What is Core vs Pack?** | Master Architecture V1 | §6 KIT Platform Core |
| **Where is tenant authority?** | Master Architecture V1 | §10 Identity model |
| **Where is authorization?** | Master Architecture V1 | §18 Security / authorization |
| **Where is Production scope?** | ADR-03 Decision V1 | Full document |
| **Where is credential ownership?** | ADR-04 Decision V1 | Full document |
| **Which Phase 1.5 tests are named?** | Phase 1.5 Acceptance TestPack V1 | `docs/architecture/KIT-PLATFORM-PHASE-1.5-ACCEPTANCE-TESTPACK-V1.md` |
| **Why was this decision made?** | Architecture Decision Register V1 | Find ADR-XX, read brief |
| **Is this decision open?** | Architecture Decision Register V1 | Decision matrix |
| **Where is Famixa Production OS?** | Master Architecture V1 | §8 Famixa Production Domain |
| **Where is Pharmacy workflow?** | Master Architecture V1 | §13 Work / workflow / job |
| **What does NOT exist?** | Master Architecture V1 | §23 Do not build |
| **Where is implementation history?** | Forensic Evidence | Find relevant closeout |
| **Where should a new Module be documented?** | Architecture Governance | §10 Pack / Module Rule |
| **What is STATE B?** | Post-Pack Audit | §2 STATE B definition |
| **What is CURRENT vs TARGET?** | Master Architecture V1 | §0 How to read this document |

### 5.2 Decision Routing

| Question | Start Here | Next Step |
|----------|-----------|-----------|
| **Is this decision open?** | Architecture Decision Register V1 | Check decision matrix |
| **Why was this decided?** | Architecture Decision Register V1 | Read ADR-XX section |
| **What was the analysis?** | ADR-XX Brief | Full brief document |
| **What is the current state?** | Architecture Decision Sync Report | Find ADR-XX status |
| **When was it decided?** | Architecture Decision Register V1 | ADR-XX status block |
| **Who decides?** | Architecture Decision Register V1 | Decision Owner row |
| **What depends on this?** | Architecture Decision Register V1 | Dependencies row |

### 5.3 Implementation Routing

| Question | Start Here | Next Step |
|----------|-----------|-----------|
| **Is this implemented?** | Architecture Decision Sync Report | ADR-XX current state |
| **Where is the code?** | Master Architecture V1 | §19 Repository / project boundary |
| **What is the migration?** | Master Architecture V1 | §8 Migration rules (not exhaustive) |
| **Where is the closeout?** | Forensic Evidence | Find relevant closeout |
| **What is the drift?** | Master Architecture V1 | §28/§29 Reading after... |
| **What is the gap?** | Post-Pack Audit | §15 Pack boundary debt |

---

## 6. Pack / Module Entry Rule

**Principle:** Every future Pack and Module must have a canonical documentation entry and owner.

**Requirements (for future packs/modules):**
- Pack owner
- Architectural Authority Source
- Purpose statement
- Boundary definition
- Dependencies
- Data ownership model
- Public surface
- Documentation entry point
- Current state
- Next work

**Do NOT invent specific future Pack/Module paths here.**

**Current Packs:**
- NOVIXA (Pharmacy and related enterprise)
- FAMIXA (Content / Production Domain)
- Family OS (active pilot)
- Local OS (dedicated single-tenant park)

**Conformance:** See Architecture Governance for Pack/Module onboarding standard.

---

## 7. Documentation State

**Baseline Documentation State:** `docs/architecture/KIT-PLATFORM-ARCHITECTURE-DOCUMENTATION-STATE-V1.md`

**Current State:**
- Repository documentation is canonical
- Architecture is version-controlled
- Novixa product documentation (`docs/novixa/**`) is separate and product-specific
- Historical forensics are selectively promoted

**Documentation Lifecycle:** See Architecture Governance for lifecycle stages.

---

## 8. Deferred Areas

The following areas are explicitly DEFERRED for later Architect review:

**`docs/architecture/navigation/**`**
- Status: DEFERRED
- Reason: Requires Architect classification
- Not currently part of canonical architecture

**`docs/architecture/second-brain/**`**
- Status: DEFERRED
- Reason: Appears to be working notes
- Not currently part of canonical architecture

**Historical Forensics (95+ documents)**
- Status: Selective promotion
- Baseline includes only critical forensics
- Additional forensics may be promoted on-demand

---

## 9. Open Decisions

**Authority:** Architecture Decision Register V1 maintains the complete list of open decisions.

**Do NOT resolve OPEN decisions** without following the governance process.

**Current Open Decisions:**
- ADR-06: Client hidden state cleanup
- ADR-05: Notification SoT
- ADR-05: Event/Audit physical merge
- ADR-02: Legacy surface retirement (execute authority closed)
- ADR-08: Episode assembly further caller policy
- Code vs documentation authority resolution policy
- Historical NULL tenant row backfill

**For complete status:** See Architecture Decision Register V1, Decision matrix section.

---

## 10. Common Questions

### "Where do I start?"

**You are here.** This INDEX is the entry point.

**Next:** Read Master Architecture V1 §0 (How to read this document) for the orientation.

**Then:** Read Master Architecture V1 §1-2 for the platform structure.

### "What is authoritative for X?"

**Route by topic:**
- Platform structure → Master Architecture V1
- Decision status → Architecture Decision Register V1
- Tenant scope → ADR-03 Decision V1
- Credentials → ADR-04 Decision V1
- Current implementation → Architecture Decision Sync Report
- Pack conformance → Post-Pack Audit

### "What does NOT exist?"

**Production Engine:** Does NOT exist. Future extraction only (Master Architecture §7, §23).

**Agent Runtime:** Does NOT exist. Future only (Master Architecture §16, §23).

**Generic Storage/Artifact:** Does NOT exist (Master Architecture §12, §23).

**Generic Workflow:** Does NOT exist. Pharmacy `IWorkflowEngine` is pack-specific (Master Architecture §13).

### "What is CURRENT?"

**Core Runtime:**
- Identity, Tenant, Authorization, Modules (CURRENT)
- Event/Audit dual-write (CURRENT with operating rule)
- Storage primitives (FUTURE)

**Product Packs:**
- NOVIXA (Pharmacy) (CURRENT)
- FAMIXA Production OS (CURRENT, pack-specific)
- Family OS (CURRENT pilot)
- Local OS (CURRENT park)

**See Master Architecture V1 §3 for complete CURRENT architecture.**

### "What is TARGET?"

**Three-layer model:**
- KIT Platform Core (mostly CURRENT)
- Production Engine (FUTURE)
- Product Packs (CURRENT)

**See Master Architecture V1 §4 for complete TARGET architecture.**

### "What is the current operating state?"

**STATE B (Post-Pack Audit §2):**
- Core stable for proven packs
- NOT ready for Agent-chain primitives
- NO CORE CHANGE required for proven pack patterns

### "Where is the ADR for X?"

**Route:**
1. Go to Architecture Decision Register V1
2. Find ADR-XX in the decision list
3. Read ADR-XX section for status
4. If DECIDED, see dedicated ADR-XX Decision file
5. Read ADR-XX Brief for analysis

### "Can I promote Famixa types to Core?"

**NO.**

**Explicit prohibitions (Master Architecture §6.3, §23):**
- Do NOT move Famixa ProductionTask to Core
- Do NOT promote ProductionArtifact to generic Artifact
- Do NOT promote IProductionArtifactFileStore to IFileStore
- Do NOT promote ProductionBinding to platform approval
- Do NOT promote ACCEPTED to workflow approval
- Do NOT promote Famixa provider registry
- Do NOT rename IAiOrchestrator to generic AI Gateway
- Do NOT rename IWorkflowEngine to generic Workflow Engine

**Reason:** Premature promotion contaminates Core (P0-4 risk).

**Future:** Generic extraction requires new types after tenant/workspace and storage/identity foundations exist.

### "Where do I document a new Pack?"

**See Architecture Governance §10 Pack / Module Rule.**

**Requirements:**
- Owner declaration
- Architectural Authority Source
- Purpose
- Boundaries
- Dependencies
- Data ownership
- Public surface
- Documentation entry
- Current state
- Next work

### "How do I propose an architecture change?"

**See Architecture Governance §4 Architecture Change Rule.**

**Process:**
1. Evidence → Reason → Impact → Decision
2. ADR creation or update
3. Implementation
4. Validation
5. Documentation/Handoff

**Do NOT implement architecture changes without governed decisions.**

---

## 11. Navigation Principles

**Repository Growth ≠ Context Growth**

Future AI execution should route:
```
Task → Step → Workstream → Module → Pack → Authority → Exact Files → Execute → Validate → Handoff
```

**This routing system is NOT implemented here.**

**Principle:** Navigate to authority, then to exact files. Do NOT scan the whole repository when routing is available.

---

## 12. Product Documentation Boundary

**KIT Platform Architecture:**
- Platform structure, layers, boundaries
- Cross-product primitives (Identity, Tenant, Authorization)
- Production Engine (future)
- Pack/Module standards
- Architecture governance

**Product Pack Documentation:**
- Product features
- Product-specific workflows
- Deployment runbooks
- Product operations
- Module catalogs

**Novixa Product Documentation (`docs/novixa/**`):**
- Product-specific
- NOT KIT Platform architecture authority
- Already tracked separately

**Famixa Production OS Types:**
- Remain Famixa-specific
- NOT promoted to Core
- See Master Architecture §8

---

## 13. Glossary

**CANONICAL:** Architecture authority; primary source of truth.

**SUPPORTING EVIDENCE:** Documents that support decisions but are not themselves authority.

**CURRENT:** Proven in the repository today.

**TARGET:** Intended architecture after decisions and extraction.

**FUTURE:** Planned architectural extraction or capability; does not exist today.

**DECIDED:** Architecture decision has been made and recorded.

**OPEN:** Architecture decision is unresolved.

**DEFERRED:** Area explicitly set aside for later Architect review.

**STATE B:** Operating state where Core is stable for proven packs but NOT ready for Agent-chain primitives.

**Pack:** Product boundary (NOVIXA, FAMIXA, Family OS, Local OS).

**Module:** Functional capability within a Pack.

**Core:** Platform primitives shared across packs (Identity, Tenant, Authorization, Modules).

**Production Engine:** Future architectural extraction for generic execution machinery; does NOT exist today.

**Agent Runtime:** Future AI agent platform; does NOT exist today.

**ADR:** Architecture Decision Record.

---

## Document Metadata

**Version:** V1  
**Status:** ACTIVE  
**Created:** 2026-10-05  
**Purpose:** Primary navigation for KIT Platform architecture documentation  
**Governance:** See `docs/architecture/GOVERNANCE.md`

---

**Next Steps:**
- Read Master Architecture V1 for technical architecture
- Read Architecture Decision Register V1 for decision status
- Read Architecture Governance for governance model
- Read relevant ADR for specific decisions
- Read relevant forensics for implementation history

**Questions?** Route using the table in §5 Architecture Navigation.

---

END OF ARCHITECTURE INDEX
