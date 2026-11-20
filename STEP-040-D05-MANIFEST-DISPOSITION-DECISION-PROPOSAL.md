# STEP 040 — D-05 Manifest Disposition Decision Record Preparation

**Mode:** Read-only preparation of a proposed Decision Register entry. No implementation.

**Baseline:** `d54652d4a7734b3116db62bd356a831f9942fc73`. Register blob at that commit: `9065fc9a264f592a19759a6d0d8ef52d95321756`. Governance blob: `823a5df765835638a6db2bd0af9e4211dc8b9345`. Index blob: `7954bd8d6e44947b4e3cdb9e0bfaeca4d87acb5d`.

This file is an untracked report. It is not a Decision Register entry. It does not store the Architect’s disposition in the repository. Conversation approval is not repository authority. `docs/architecture/GOVERNANCE.md` §2: chat is not the architecture source of truth. §6: the Register is where decision status is tracked, and DECIDED is not IMPLEMENTED.

The working tree was not staged, unstaged, edited, deleted, renamed, committed, pushed, merged, reset, cleaned, or stashed, except for the creation of this report. The dirty working copy of `deploy/ubuntu/migration-files.content.txt` was not opened. Migration 376 was not read. Untracked migration files were not opened. No migration was run. No database was accessed. The Decision Register, ADRs, architecture documents, SQL, manifests, application code, and tests were not edited.

## 1. Where the entry belongs

The smallest Level 2 location is one new section appended to `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`, immediately after the synchronization section.

At the baseline that section ends at line 719, the Invariant 1 row. The file separates that section from the closeout above it with a `---` rule (line 700). The proposed text below uses the same rule and the same `#` heading rank.

That slot is the Level 2 document named by Governance §3 and by Step 030 D-02. The new section sits after ADR-01 through ADR-08, after the decision matrix, and after line 231. Those passages stay as they are. The Index stays a pointer to the register file. No Level 3 ADR file is part of this proposal.

The synchronization section remains the current reading of the closeout table. It does not mention these two migrations. The new section does not revise that table.

## 2. What the entry decides

The Architect’s conversation disposition, not yet written into the Register, is manifest ownership of two tracked files only:

1. Include `migrations/383_pack_content_article_series.sql` in `deploy/ubuntu/migration-files.content.txt`, using the tracked filename exactly.
2. Include `migrations/384_pack_content_brand_narrative_dna.sql` in the same manifest, using the tracked filename exactly.
3. Do not rename either SQL file to match its comment label.
4. Treat the `370_` smoke reference as a separate stale filename reference.
5. This disposition does not authorize changes to the smoke test, application contracts, SQL files, or any database.
6. This disposition does not grant D-05 implementation permission.

The supplied disposition does not name a line index. This proposal does not add one. Inclusion and a line number are different facts. The exact manifest position stays unselected.

This ownership ruling does not authorize implementation. Writing the two manifest lines would be that implementation. This entry withholds that write. It also withholds every other write.

## 3. Proposed Register entry

**Proposed text. Not recorded.** The block under this heading is the exact text proposed for insertion after baseline line 719. It is absent from blob `9065fc9a`. Pasting it into the Register, and committing that paste, is a later step. This file does not perform that step. Until that committed text exists, the conversation disposition is not in force.

```
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
```

## 4. Limit of this decision

The decision, once the section above is stored on a commit, is 383/384 manifest ownership and nothing further.

| Subject | State under this decision |
|---|---|
| Ownership of `383_pack_content_article_series.sql` on the content manifest | Included, tracked filename exactly |
| Ownership of `384_pack_content_brand_narrative_dna.sql` on the content manifest | Included, tracked filename exactly |
| Rename of either SQL file to its comment label | Not part of the disposition |
| `370_` smoke reference | Separate stale filename reference; smoke unchanged |
| Edit of the manifest, the smoke, application contracts, SQL, or any database | **NOT GRANTED** |
| D-05 implementation permission | **NOT GRANTED** |
| Line 231 | Unchanged. No exception is added |
| AG-G3-04 | Unchanged. The gate covers the post-367 content set. This section names only 383 and 384 |
| Phase 1.5 | **NOT PASS** |

Step 028 AG-G3-04 remains the broader gate. Recording these two filenames does not pass that row. D-07, as opened in Step 029, stays open with it.

## 5. Prerequisite still required before D-05 implementation permission

D-05 implementation permission stays **NOT GRANTED**. Three further facts have to exist before that permission can be given. They are considered only after the section in §3 is actually stored in the Register on a commit. This file does not store it, so those three facts are not available from this step.

| Prerequisite | What has to be true | State in this step |
|---|---|---|
| Agreed implementation base | A commit agreed as the base for a D-05 migration. Step 030 D-01 names `d54652d4` as the Phase 1.5 acceptance baseline. That naming is not an implementation commit. | **NOT AGREED** |
| Safe D-05 filename and prefix | One new migration filename, chosen from a fresh listing of `migrations/` on that agreed base. A number absent from the tracked tree at `d54652d4` is not reserved. Step 028 recorded untracked on-disk files in prefixes `368`–`382`, `385`, and `386`. Those files were not opened here. | **NOT SELECTED** |
| Exact manifest position | One content-manifest position for that future D-05 line, stated only after the 383/384 ownership section is in the Register. The supplied disposition does not assign a line index for 383, for 384, or for a D-05 line. | **NOT SELECTED** |

The D-05 grant, when a later entry makes it, still has to carry its own implementation sentence, its own filename, and its own manifest line. The 383/384 section does not contain those sentences. Approval of column shape, in any earlier untracked report, is not that grant.

## 6. What this step leaves untouched

No product file, migration, manifest, solution, ADR, Decision Register, Master Architecture, or governance file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

The Register blob remains `9065fc9a264f592a19759a6d0d8ef52d95321756`. The proposed section is not in that blob.

D-05 is not closed. D-06 was not started. No Step 028 row was marked PASS. Phase 2 was not opened.

## Closing state

- 383/384 manifest direction: `ARCHITECT-APPROVED IN CONVERSATION; REGISTER ENTRY NOT RECORDED`
- D-05 implementation permission: `NOT GRANTED`
- Migration prefix: `NOT SELECTED`
- Phase 1.5: `NOT PASS`
