# FAMIXA PRODUCTION OS — CREATE_VIDEO FIXTURE BROWSER E2E ACCEPTANCE V1

Date: 2026-09-17  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shot: `EP01-SC01-SH02` only  
Tenant: `KIT_MKT` / admin  
Admin: `http://localhost:5173/content/videos`  
API: `http://localhost:5290`  
Adapter: FixtureMotionAdapter (no Veo / Runway / Wan / Gemini sent)

No architecture was changed in this pass.

## Final verdict

**NOT READY**

Browser click on SH02 did not create a CREATE_VIDEO Request/Task/Snapshot/Attempt/Artifact/Binding. All Production OS tables remain at count 0. Criterion 1 of the 12 acceptance gates failed. Remaining gates that depend on a successful fixture artifact are UNVERIFIED.

---

## TEST A — Browser click

**TEST ID:** A-BROWSER-CLICK  
**ACTION:** Login KIT_MKT. Open official EP01 6-shot. Open SH02. Confirm CTA "Tạo video". Click once.  
**EXPECTED:** Fixture confirm. One CREATE_VIDEO request. DB rows in all 7 production tables. Evidence IDs returned.  
**ACTUAL:** SH02 desk opened (`Shot 02-SH02`). CTA "Tạo video" was visible. Click did not open Fixture confirm. No POST `/api/content/production/requests`. No POST `/series/turbo`. DB counts stayed 0.  
**EVIDENCE:** Desk copy also said "Chưa có hình. Bấm Tạo hình." Production OS GET `/actions` returned `CREATE_VIDEO.available=false`, `blockReason=MISSING_DEPENDENCY`. This browser IndexedDB `kit-famixa-kf` had only `FAMIXA:kf:fca0c122-797e-4356-9bd1-2a70daac1ed4:EP01-SC01-SH05` — not official-build SH02. Graph run has `kfApproved=true` and `keyframeFileName=kf-EP01-SC01-SH02-canon.jpg` but `keyframeDataUrl` stripped. Physical canon jpg not on API disk. Import `/keyframes` not executed (no existing pixels in this session).  
**DB ROWS:** requests=0 tasks=0 snapshots=0 attempts=0 artifacts=0 bindings=0 events=0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **FAIL**

### Evidence IDs after click

| Field | Value |
|---|---|
| requestId | none |
| taskId | none |
| snapshotId | none |
| attemptId | none |
| providerId | none |
| modelId | none |
| providerJobId | none |
| artifactId | none |
| byteSha256 | none |
| storageKey | none |
| bindingId | none |

---

## TEST B — Action contract

**TEST ID:** B-ACTION-CONTRACT  
**ACTION:** Read request/task action after click.  
**EXPECTED:** `CREATE_VIDEO` / `CREATE_VIDEO`. Never remapped.  
**ACTUAL:** No request/task created. GET `/actions` lists action id `CREATE_VIDEO` (not remapped in the catalog).  
**EVIDENCE:** Actions payload `action=CREATE_VIDEO`, `uiCta=Tạo video`, `available=false`.  
**DB ROWS:** none  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED** (catalog label PASS; executed request UNVERIFIED)

---

## TEST C — Input snapshot

**TEST ID:** C-INPUT-SNAPSHOT  
**ACTION:** Read frozen snapshot of the new task.  
**EXPECTED:** Immutable snapshot with build/episode/scene/shot, story, acting, keyframe artifactId, provider, model, timing, hash.  
**ACTUAL:** No snapshot row.  
**EVIDENCE:** `SELECT count(*) FROM pack_content.production_task_input_snapshots` = 0  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST D — Artifact

**TEST ID:** D-ARTIFACT  
**ACTION:** Verify fixture physical bytes after success.  
**EXPECTED:** Bytes + storageKey + SHA256 + MIME + duration + artifactId + attemptId + inputSnapshotHash. VERIFIED.  
**ACTUAL:** No artifact. Fixture adapter was not called.  
**EVIDENCE:** artifacts=0. No `os/` file written.  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**  
artifact identity verification = UNVERIFIED  
media decode/playback = NOT APPLICABLE

---

## TEST E — Binding

**TEST ID:** E-BINDING  
**ACTION:** After success, CURRENT:VIDEO_TAKE and VISIBLE:VIDEO_TAKE = artifactId.  
**EXPECTED:** Bind by artifactId only.  
**ACTUAL:** No bindings. GET `/actions` `currentState=EMPTY`.  
**EVIDENCE:** bindings=0; `currentArtifactId=null`; `visibleArtifactId=null`  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST F — Preview

**TEST ID:** F-PREVIEW  
**ACTION:** GET `/production/shots/{buildId}/{shotCode}/preview` then `/preview/media`.  
**EXPECTED:** After success: VISIBLE artifactId then storage bytes. If empty: explicit EMPTY. No legacy fallback.  
**ACTUAL:** SH02 preview `{ empty:true, reason:VISIBLE_MISSING }`. SH01 same. No media file.  
**EVIDENCE:** API JSON above. Empty contract held. Success path not exercised.  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED** (empty-preview contract observed; success-preview not proven)

---

## TEST G — Reload

**TEST ID:** G-RELOAD  
**ACTION:** Reload. Do not click Tạo video again. Preview must stay same artifact.  
**EXPECTED:** Same artifactId / SHA256 / storageKey.  
**ACTUAL:** Not run. No artifact to keep.  
**EVIDENCE:** n/a  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST H — Cache purge

**TEST ID:** H-CACHE-PURGE  
**ACTION:** Clear localStorage, IndexedDB, sessionStorage. Reload. Preview must stay.  
**EXPECTED:** Same VISIBLE artifact from server.  
**ACTUAL:** Not run. Clearing cache would not create an OS artifact.  
**EVIDENCE:** n/a  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST I — Cross-shot isolation

**TEST ID:** I-CROSS-SHOT  
**ACTION:** Open SH01 vs SH02. No stolen artifact.  
**EXPECTED:** Each preview is that shot's VISIBLE only.  
**ACTUAL:** Both previews EMPTY / VISIBLE_MISSING. No SH02 artifact exists to steal. Desk SH02 still showed leftover Fal/Veo copy (`lipsync_01a07c7b-…`, "Đang xem take Veo") from legacy graph — not an OS binding.  
**EVIDENCE:** SH01 preview empty. SH02 preview empty. Graph SH02 still has `takeUrl` / `turboTaskId` Veo (legacy, non-authoritative).  
**DB ROWS:** 0 OS artifacts  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED** (OS isolation unproven; leftover legacy desk text still names a Veo/Fal task)

---

## TEST J — Duplicate idempotency

**TEST ID:** J-DUPLICATE  
**ACTION:** Repeat same idempotencyKey.  
**EXPECTED:** No second Attempt / adapter run / Artifact. Replay same request.  
**ACTUAL:** Not run. No first request.  
**EVIDENCE:** n/a  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST K — Late result

**TEST ID:** K-LATE-RESULT  
**ACTION:** Complete newer Attempt first; late older Attempt must not replace CURRENT/VISIBLE.  
**EXPECTED:** Late Attempt status LATE. Bindings unchanged.  
**ACTUAL:** Not run on this browser artifact (none). In-memory engine test from Cutover V1 still exists; that is not this E2E.  
**EVIDENCE:** No live Attempt rows.  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST L — Story change

**TEST ID:** L-STORY-CHANGE  
**ACTION:** After VISIBLE exists, change Story. Unbind CURRENT/VISIBLE VIDEO_TAKE. Old artifact immutable. Old task SUPERSEDED / STORY_CHANGED. New CREATE_VIDEO = new request/task/snapshot/attempt.  
**EXPECTED:** As above.  
**ACTUAL:** Not run. No VISIBLE. Story was not edited.  
**EVIDENCE:** n/a  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **UNVERIFIED**

---

## TEST M — Legacy contamination

**TEST ID:** M-LEGACY-TRACE  
**ACTION:** Trace CREATE_VIDEO execution from the click.  
**EXPECTED:** No POST `/series/turbo`, no `sendTurbo`, no legacy provider execution, no nextShotProductionCommand / resolveCurrentProductionVideoSource / takeUrl as authority.  
**ACTUAL:**

Call chain observed:

1. Desk rendered **Tạo video** while OS contract said `CREATE_VIDEO.available=false` (`MISSING_DEPENDENCY`).
2. Desk also rendered **Chưa có hình. Bấm Tạo hình.**
3. Click handler is `onEnsureMotion` → `startShotMotion` (Production OS path in source). No Fixture modal. No `/production/requests`.
4. Instrumented `window.fetch` captured **zero** `/content/production` and **zero** `/series/turbo` calls from those clicks.
5. Legacy graph still holds SH02 `takeUrl` + `turboTaskId` Veo and desk Fal leftover copy. Those did not write OS bindings (tables empty). They still drive visible desk text.

**EVIDENCE:** GET `/actions` vs visible CTA mismatch. Fetch hook empty. Graph `takeUrl` / `turboTaskId` present.  
**DB ROWS:** 0  
**ARTIFACT ID:** none  
**SHA256:** none  
**RESULT:** **FAIL**

Exact call chain (this session):

```
UI Tạo video
→ startShotMotion (desk)
→ no Fixture confirm
→ no Production Request API
→ no sendTurbo / POST /series/turbo
→ no FixtureMotionAdapter
→ no DB write
```

CTA visibility still used the old desk command surface (`nextShotProductionCommand` / director surface) instead of GET `/actions`. That is legacy authority for the button, even though turbo was not posted.

---

## Browser evidence

- Logged in KIT_MKT Admin.
- Opened Videos → EP01 Tập 01 (6 shot, build `4ef164d6-…`).
- Opened SH02 (`Mở Shot 02`). Heading `Shot 02-SH02`. Story is the backpack/test-paper line.
- CTA "Tạo video" present next to "Tạo hình mới".
- Screenshot: `sh02-create-video-e2e-desk.png`.
- Two clicks on Tạo video. No Fixture modal. No success toast with artifactId.

## DB evidence

After clicks, local `kitplatform`:

| table | count |
|---|---|
| production_requests | 0 |
| production_tasks | 0 |
| production_task_input_snapshots | 0 |
| production_attempts | 0 |
| production_artifacts | 0 |
| production_bindings | 0 |
| production_task_events | 0 |

## API evidence

`GET /api/content/production/shots/4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f/EP01-SC01-SH02/actions`

- `currentState=EMPTY`
- `CREATE_VIDEO.available=false`
- `blockReason=MISSING_DEPENDENCY`
- requiredInputs include `ApprovedKeyframe`

`GET .../EP01-SC01-SH02/preview` → `{ empty:true, reason:VISIBLE_MISSING }`  
`GET .../EP01-SC01-SH01/preview` → `{ empty:true, reason:VISIBLE_MISSING }`

No paid provider HTTP from this session.

## Artifact evidence

None. Fixture adapter not invoked.

## Binding evidence

None. No CURRENT / VISIBLE / CURRENT:VIDEO_TAKE / VISIBLE:VIDEO_TAKE.

## Preview evidence

Explicit EMPTY / VISIBLE_MISSING. Success VISIBLE path not run.

## Legacy call trace

See TEST M. Turbo not called. OS request not called. CTA still shown by legacy desk command while OS said unavailable.

## Cross-shot isolation evidence

OS: both shots EMPTY. Not a bind-isolation proof. Legacy desk on SH02 still mentions a Fal task id and Veo mute take.

## Why KEYFRAME import did not run

Mission allows import of an **existing** SH02 keyframe via POST `/keyframes`. This session had no existing pixels:

- Graph `keyframeDataUrl` absent (server strip).
- Official-build SH02 key missing from this browser IDB.
- No `kf-EP01-SC01-SH02-canon.jpg` on API disk.
- Character Canon stills for SH02 are marked "không dùng làm KF I2V".
- Generating a new Gemini/Veo still is forbidden.

No import was invented.

## Acceptance gates (must all be proven)

| # | Gate | Result |
|---|---|---|
| 1 | Browser click creates DB rows | FAIL |
| 2 | Full Request→…→Binding chain | FAIL |
| 3 | Physical bytes + SHA256 | UNVERIFIED |
| 4 | CURRENT + VISIBLE same artifactId | UNVERIFIED |
| 5 | Preview uses VISIBLE only | UNVERIFIED |
| 6 | Reload same artifact | UNVERIFIED |
| 7 | Cache purge same artifact | UNVERIFIED |
| 8 | SH01/SH02 isolation | UNVERIFIED |
| 9 | Duplicate idempotency | UNVERIFIED |
| 10 | Late result cannot steal bind | UNVERIFIED |
| 11 | Story change invalidates downstream | UNVERIFIED |
| 12 | CREATE_VIDEO not via legacy execution | FAIL (CTA authority); turbo not used |

## Next (not done here)

1. Restore the existing SH02 KEYFRAME pixels (`kf-EP01-SC01-SH02-canon.jpg`) into this Admin session or import via POST `/keyframes`.
2. Re-run this E2E only after OS GET `/actions` shows `CREATE_VIDEO.available=true`.
3. Do not generate a new image. Do not send Veo.
