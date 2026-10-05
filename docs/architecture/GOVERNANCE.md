# KIT Platform Architecture Governance

**Version:** V1 — INITIAL FOUNDATION  
**Status:** ACTIVE  
**Last Updated:** 2026-10-05  
**Purpose:** Governance model for KIT Platform architecture documentation

---

## 1. Purpose

This document establishes the minimum governance contract for KIT Platform architecture documentation.

**What this governs:**
- Architecture authority hierarchy
- Architecture change control
- Decision lifecycle
- Documentation lifecycle
- Promotion criteria
- Conflict resolution

**What this does NOT govern:**
- Product feature decisions
- Implementation details
- Day-to-day development
- Code review processes

---

## 2. Source of Truth

**Repository documentation is canonical.**

The KIT Platform repository is the Architecture Source of Truth.

**Alternatives REJECTED:**
- Local-only architecture
- External-only architecture (Notion, external wikis)
- Chat history as architecture SoT
- ChatGPT memory as architecture SoT

**Principle:** ChatGPT memory and chat may assist reasoning, but repository documentation is the formal architecture authority.

**Reproducibility:** Architecture must be reproducible from `git clone`.

---

## 3. Authority Levels

Architecture authority is hierarchical:

### Level 1 — Master Architecture

**Document:** `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`

**Authority:** Technical architecture definition; platform structure, layers, boundaries, principles.

**Scope:** Platform-wide.

**Change Control:** Architecture change rule (§4).

### Level 2 — Architecture Decision Register

**Document:** `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`

**Authority:** Decision tracking; ADR-01 through ADR-08 status.

**Scope:** Platform-wide decisions.

**Change Control:** ADR lifecycle (§6).

### Level 3 — ADR / Decision

**Documents:** `docs/KIT-PLATFORM-ADR-XX-DECISION-V1.md`

**Authority:** Specific architecture decision; binding on implementation.

**Scope:** Decision-specific.

**Change Control:** Decision status tracking (§6).

### Level 4 — Supporting Evidence

**Documents:** ADR briefs, forensic reports, checkpoint closeouts, reconciliation reports.

**Authority:** NOT architecture authority; supports decisions.

**Scope:** Decision-specific or implementation-specific.

**Change Control:** Evidence may be created/updated without architecture change control.

### Level 5 — Work / Implementation

**Documents:** Checkpoint documentation, task documentation, implementation prompts.

**Authority:** NOT architecture authority; records implementation.

**Scope:** Task-specific.

**Change Control:** Not governed by architecture governance.

---

## 4. Architecture Change Rule

Changes to KIT Platform architecture require:

```
Evidence
    ↓
Reason
    ↓
Impact
    ↓
Decision
    ↓
ADR / Update
    ↓
Implementation
    ↓
Validation
    ↓
Documentation / Handoff
```

**NOT EVERY CHANGE REQUIRES AN ADR:**
- Trivial documentation typo: NO
- Formatting fix: NO
- New architecture decision: YES
- Change to platform layers: YES
- Change to pack boundaries: YES
- Change to authority model: YES
- Promotion of product type to Core: YES (and likely REJECTED)

**Decision Authority:** Architecture Decision Register identifies decision owners per topic.

**Approval:** Decision must be explicitly recorded as DECIDED before implementation.

---

## 5. Change Classification

Architecture changes are classified by compatibility impact:

### SAFE

**Definition:** Change that does not affect existing implementations.

**Examples:**
- Add new documentation
- Clarify existing documentation
- Add new optional Pack or Module that does not alter existing platform boundaries, authority, ownership, dependency direction, tenant/isolation model, runtime behavior, or existing implementations

**Approval:** Standard architecture change rule.

**Migration Risk:** LOW

**Note:** Adding a Pack or Module is SAFE only when it does not alter existing platform properties. If those properties are affected, the change must be reclassified as COMPATIBLE or BREAKING.

### COMPATIBLE

**Definition:** Change that is backward-compatible with existing implementations.

**Examples:**
- Add new Core primitive that existing packs do NOT depend on
- Extract generic type from proven patterns without changing existing types
- Add new ADR for future work

**Approval:** Standard architecture change rule + impact analysis.

**Migration Risk:** MEDIUM

### BREAKING

**Definition:** Change that requires modification of existing implementations.

**Examples:**
- Change Core identity model
- Change tenant isolation model
- Remove existing primitive
- Promote product type to Core (typically REJECTED)
- Change authority hierarchy

**Approval:** Explicit Architect approval + architectural justification + migration plan.

**Migration Risk:** HIGH

**WARNING:** BREAKING changes should be rare and well-justified.

---

## 6. Open Decision Rule

**Definition:** An OPEN decision is unresolved.

**Lifecycle:**
```
UNKNOWN (gap) → DECISION REQUIRED → DECIDED → IMPLEMENTED → CLOSED
```

**Rules:**
1. Cursor/developer MUST NOT silently convert OPEN to DECIDED
2. Implementation MUST NOT proceed before DECIDED
3. DECIDED ≠ IMPLEMENTED (status must be synchronized)
4. CLOSED requires validation

**Tracking:** Architecture Decision Register maintains decision status.

**Resolution:** Decision owner responsible for resolution.

---

## 7. Documentation Lifecycle

Architecture documentation follows this lifecycle:

### DRAFT

**Definition:** Document is being written; not yet reviewed.

**Authority:** NONE

**Usage:** DO NOT cite as authority.

**Next State:** ACTIVE or RETIRED

### ACTIVE

**Definition:** Document is current for its assigned authority level or document role.

**Authority:** Per authority level (§3). ACTIVE does not automatically mean "architecture authority" — the document's authority level determines its authority scope.

**Usage:** Cite as authority only if the document is at an authority level (Level 1-3).

**Next State:** SUPERSEDED or RETIRED

### SUPERSEDED

**Definition:** Document was authority but has been replaced.

**Authority:** HISTORICAL (no longer binding)

**Usage:** May cite for historical context; NOT binding on new work.

**Next State:** RETIRED

### RETIRED

**Definition:** Document is archived; not relevant to current architecture.

**Authority:** NONE

**Usage:** DO NOT cite as authority.

**Next State:** (terminal)

**Versioning:** When a document is SUPERSEDED, increment version (V1 → V2).

---

## 8. Promotion Rule

**Principle:** A local/supporting document becomes canonical only when explicitly promoted by Architect governance.

**Promotion Criteria:**
1. Document is required for architecture authority
2. Document is referenced by canonical architecture
3. Document supports architecture decisions
4. Document is stable (not working notes)
5. Architect approves promotion

**Promotion Process:**
1. Identify document for promotion
2. Classify authority level (§3)
3. Review for stability and completeness
4. Add to repository tracking (`.gitignore` exception)
5. Stage and commit with promotion message
6. Update Architecture Index

**Automatic Promotion:** NONE. All promotions require explicit review.

**Demotion:** Documents may be demoted from canonical if no longer required. Follow same process.

---

## 9. Evidence Rule

**Principle:** Forensics support decisions. They do not automatically become architecture authority.

**Forensic Evidence:**
- ADR briefs
- Checkpoint closeouts
- Audit reports
- Reconciliation reports
- Drift documentation

**Authority Status:** Supporting evidence (Level 4).

**Tracking:**
- Critical forensics: Tracked in repository
- Historical forensics: Local-only; selective promotion on-demand

**Promotion:** Evidence may be promoted if required for decision traceability or audit requirements.

---

## 10. Pack / Module Rule

**Principle:** Every future Pack/Module must declare its boundaries and ownership.

**Required Documentation:**

### Pack Requirements

1. **Owner:** Who owns this Pack?
2. **Responsibility / Ownership:** What does this Pack own and remain responsible for?
3. **Architectural Authority Source:** Which canonical architecture document or decision governs this Pack?
4. **Purpose:** What does this Pack do?
5. **Boundaries:** What is inside vs. outside this Pack?
6. **Dependencies:** What does this Pack depend on?
7. **Data Ownership:** What data does this Pack own?
8. **Public Surface:** What APIs/interfaces does this Pack expose?
9. **Documentation Entry:** Where is the Pack documented?
10. **Current State:** What is implemented?
11. **Next Work:** What is planned?

### Module Requirements

Same as Pack, scoped to Module within Pack.

**Conformance:** Pack/Module conformance standard to be defined (see Consolidation Matrix recommendation).

**Template:** Do NOT create templates in this document. Templates are implementation guidance, not governance.

---

## 11. AI / Cursor Rule

**Principle:** User defines business goal. Architect decides technical path. Cursor executes bounded approved tasks.

### Cursor MUST NOT

1. **Redesign architecture** without explicit Architect approval
2. **Resolve OPEN decisions** without decision owner approval
3. **Expand scope** beyond approved task boundary
4. **Invent authority** where none exists
5. **Scan whole repository** when routing is available

### Cursor MUST

1. **Read authority documents** before proposing changes
2. **Respect decision status** (OPEN vs. DECIDED)
3. **Stay within task scope** (bounded implementation)
4. **Report conflicts** rather than silently choosing
5. **Validate work** before handoff

### Task Scoping

Every Cursor task should define:
- **ALLOWED READ:** Which files may be read
- **ALLOWED WRITE:** Which files may be modified
- **FORBIDDEN WRITE:** Which files must NOT be modified
- **STOP CONDITIONS:** When to stop and report

**Context Minimization:** Route to exact files. Do NOT read entire repository.

---

## 12. Change Control

Architecture changes must leave:

1. **Decision Record:** ADR or update to existing ADR
2. **Implementation Task:** Bounded task documentation
3. **Validation:** Evidence of correctness
4. **Documentation Update:** Sync architecture docs with implementation
5. **Handoff:** Current state and next work

**Traceability:** Changes must be traceable from:
- Decision → Implementation
- Implementation → Decision
- Code → Architecture
- Architecture → Code

**Drift Prevention:** When implementation drifts from architecture, document the drift and create a reconciliation task.

---

## 13. Conflict Resolution

**Definition:** Two documents conflict when they make incompatible claims about architecture authority.

### When Conflict Is Discovered

**DO NOT choose silently.**

**Process:**
1. STOP work
2. Document the conflict:
   - Document A claims X
   - Document B claims Y
   - X and Y are incompatible
3. Classify the conflict:
   - Authority conflict (two documents claim to be authority)
   - Drift (implementation diverged from architecture)
   - Historical (one document is superseded)
4. Escalate to Architect
5. Architect decides resolution

### Resolution Outcomes

**Authority Hierarchy Win:**
- Higher authority level wins (§3)
- Example: Master Architecture wins over ADR brief

**Explicit Win:**
- Explicit decision supersedes implicit assumption
- Example: DECIDED ADR supersedes OPEN question

**Classification Win:**
- Canonical document wins over supporting evidence
- Example: ADR Decision wins over ADR Brief

**Temporal Principle:**
- Temporal recency does not override architectural authority
- A later document may report drift, provide newer evidence, propose a change, or supersede historical evidence
- However, it does NOT change architecture authority unless the governed architecture decision process is completed

**Architect Decision:**
- If none of the above resolves it, Architect decides

### Conflict Prevention

**Prevention strategies:**
1. Clear authority hierarchy (§3)
2. Decision status tracking (§6)
3. Drift documentation (reconciliation sections)
4. Promotion governance (§8)

---

## 14. Navigation Principle

**Principle:** Repository growth MUST NOT automatically become context growth.

**Target Routing:**
```
Task
    ↓
Step
    ↓
Workstream
    ↓
Module
    ↓
Pack
    ↓
Authority
    ↓
Exact Files
    ↓
Execute
    ↓
Validate
    ↓
Handoff
```

**This routing system is NOT implemented here.**

**Goal:** Future AI execution should route to exact files, not scan the whole repository.

**Current State:** Routing system is a target capability. Manual navigation via Architecture Index is current.

---

## 15. Deferred Areas

The following areas are explicitly DEFERRED for later Architect review:

### `docs/architecture/navigation/**`

**Status:** DEFERRED

**Reason:** Requires Architect classification

**Not Currently:** Part of canonical architecture

### `docs/architecture/second-brain/**`

**Status:** DEFERRED

**Reason:** Appears to be working notes

**Not Currently:** Part of canonical architecture

### Historical Forensics (95+ documents)

**Status:** Selective promotion

**Baseline:** Includes only critical forensics

**Future:** May be promoted on-demand

---

## 16. Governance Status

**This is:**
- KIT Platform Architecture Governance V1
- INITIAL FOUNDATION
- NOT a complete governance framework

**This governs:**
- Architecture authority
- Decision lifecycle
- Documentation lifecycle
- Promotion criteria
- Conflict resolution

**This does NOT govern:**
- Work breakdown (not defined)
- Task routing (not implemented)
- Context packs (not implemented)
- AI routing (not implemented)
- Conformance enforcement (not implemented)

**Future Governance:**
- Pack/Module onboarding standard (recommended, not yet written)
- Conformance validation process (recommended, not yet written)
- Work routing architecture (deferred)
- Context minimization framework (deferred)

**Do NOT claim all future governance mechanisms are already implemented.**

---

## 17. Governance Principles

### Principle 1: Explicit Over Implicit

Authority must be explicit. Implicit authority is NOT authority.

### Principle 2: Repository Over External

Repository documentation is canonical. External documentation is NOT architecture authority.

### Principle 3: Decision Over Assumption

Explicit decisions are binding. Assumptions are NOT binding.

### Principle 4: Evidence Over Opinion

Decisions must be supported by evidence. Opinions are NOT evidence.

### Principle 5: Navigation Over Search

Route to authority. Do NOT scan the whole repository.

### Principle 6: Bounded Over Open-Ended

Tasks must be bounded. Open-ended work is NOT governed.

### Principle 7: Validation Over Assumption

Work must be validated. Assumption of correctness is NOT validation.

### Principle 8: Handoff Over Abandonment

Work must be handed off. Abandoned work leaves no trace.

### Principle 9: Promotion Over Automatic

Documents are promoted explicitly. Automatic promotion is NOT governance.

### Principle 10: Conflict Resolution Over Silent Choice

Conflicts must be resolved explicitly. Silent choice is NOT resolution.

---

## 18. Document Versioning

### Version Scheme

**Format:** `V{major}[.{minor}]`

**Examples:**
- V1 (initial)
- V2 (major revision)
- V2.1 (minor update)

### When to Increment

**Major (V1 → V2):**
- Architecture model changes
- Authority hierarchy changes
- Governance rule changes
- Breaking changes

**Minor (V2 → V2.1):**
- Clarifications
- Non-breaking additions
- Typo fixes
- Formatting improvements

**No Increment:**
- Working drafts
- Local-only documents

### Superseded Documents

When a document is superseded:
1. Mark original as SUPERSEDED in status
2. Create new version with incremented number
3. Update Architecture Index
4. Update references

---

## 19. Review and Approval

### Architecture Change Review

**Who Reviews:**
- Architect (required)
- Decision owner (if applicable)
- Affected Pack owners (if applicable)

**What Is Reviewed:**
- Evidence
- Reason
- Impact
- Compatibility classification
- Migration risk

**Approval Criteria:**
- Evidence is sufficient
- Reason is sound
- Impact is acceptable
- Risk is mitigated

### Decision Review

**Who Reviews:**
- Decision owner (required)
- Architect (required for BREAKING changes)

**What Is Reviewed:**
- Decision options
- Evidence
- Consequences
- Dependencies

**Approval Criteria:**
- Options are complete
- Evidence is sufficient
- Consequences are understood
- Dependencies are identified

### Documentation Review

**Who Reviews:**
- Author
- Architect (for canonical documents)

**What Is Reviewed:**
- Accuracy
- Completeness
- Clarity
- Consistency with existing architecture

**Approval Criteria:**
- No conflicts with existing authority
- No invented authority
- No silent resolution of OPEN decisions

---

## 20. Enforcement

### What Is Enforced

1. **Architecture authority hierarchy** (§3)
2. **Open decision rule** (§6)
3. **Documentation lifecycle** (§7)
4. **Promotion rule** (§8)
5. **Pack/Module requirements** (§10)
6. **AI/Cursor rules** (§11)
7. **Conflict resolution** (§13)

### How Enforcement Works

**Pre-Commit:**
- Architecture change review
- Decision review
- Scope firewall (ALLOWED/FORBIDDEN)

**Post-Commit:**
- Validation
- Documentation sync
- Drift detection

**Continuous:**
- Architecture Index maintenance
- Decision status tracking
- Authority conflict detection

### What Is NOT Enforced

1. **Implementation details** (not governed)
2. **Code style** (not governed)
3. **Day-to-day development** (not governed)
4. **Product feature decisions** (not governed)

---

## 21. Glossary

**CANONICAL:** Architecture authority; primary source of truth.

**SUPPORTING EVIDENCE:** Documents that support decisions but are not themselves authority.

**DECIDED:** Architecture decision has been made and recorded.

**OPEN:** Architecture decision is unresolved.

**DEFERRED:** Area explicitly set aside for later Architect review.

**PROMOTED:** Document explicitly added to canonical architecture tracking.

**SUPERSEDED:** Document was authority but has been replaced.

**RETIRED:** Document is archived; not relevant to current architecture.

**BREAKING:** Change that requires modification of existing implementations.

**COMPATIBLE:** Change that is backward-compatible with existing implementations.

**SAFE:** Change that does not affect existing implementations.

**DRIFT:** Implementation diverged from architecture.

---

## Document Metadata

**Version:** V1 — INITIAL FOUNDATION  
**Status:** ACTIVE  
**Created:** 2026-10-05  
**Purpose:** Governance model for KIT Platform architecture documentation  
**Next Review:** After Pack/Module onboarding standard is written

---

## Next Steps

**For Architects:**
- Use this governance model for architecture decisions
- Review and approve changes per §4
- Resolve conflicts per §13

**For Developers:**
- Read Architecture Index for navigation
- Follow open decision rule (§6)
- Do NOT silently resolve OPEN decisions

**For Cursor Agents:**
- Follow AI/Cursor rules (§11)
- Respect task boundaries
- Report conflicts, do not choose silently

**For Future Work:**
- Pack/Module onboarding standard (recommended)
- Conformance validation process (recommended)
- Work routing architecture (deferred)
- Context minimization framework (deferred)

---

END OF ARCHITECTURE GOVERNANCE
