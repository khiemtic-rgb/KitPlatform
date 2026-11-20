# FAMIXA PRODUCTION OS — CREATE_VIDEO ACTION AUTHORITY CUTOVER FIX V1

Date: 2026-09-17  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Shots: `EP01-SC01-SH01`, `EP01-SC01-SH02`  
Tenant: `KIT_MKT` / admin  
Admin: `http://localhost:5173/content/videos`  
API: `http://localhost:5290`

No AI / fixture / Veo / Runway / Wan / Gemini generation. No keyframe import. No Production DB schema, Action Contract, Shot Contract, Canon, Visual Authority, Timing, Provider, Artifact, Attempt, or Binding changes. No new resolver. No fallback that invents CREATE_VIDEO.

---

## Final verdict

**PASS**

UI CREATE_VIDEO availability is decided only by `GET /api/content/production/shots/{buildId}/{shotCode}/actions`. When `CREATE_VIDEO.available !== true`, the desk and the shot card do not render "Tạo video", and `executeProductionOsAction` returns without posting.

Fixture execution path: **UNVERIFIED** — repository has no fixture-ready shot (`CREATE_VIDEO.available=true` with OS KEYFRAME). No shot was created.

---

## Files changed

Created:

- `client/admin/src/modules/content/production-os-action-authority.ts`
- `client/admin/src/modules/content/production-os-action-authority.smoke.ts`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_AUTHORITY_CUTOVER_FIX_V1_REPORT.md`

Modified:

- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionWorkspace.tsx`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionCta.ts`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionCard.tsx`
- `client/admin/src/modules/content/famixa-shot-stale-remake-gate.smoke.ts`
- `client/admin/src/modules/content/famixa-director-video-workspace-restructure.smoke.ts`
- `client/admin/src/modules/content/famixa-video-studio-ux.smoke.ts`

Not changed: backend Action Contract, Production schema, Canon, VA, Timing, Provider, Artifact, Attempt, Binding.

---

## Exact old authority

CREATE_VIDEO CTA was derived from legacy Director / staff graph:

- `nextShotProductionCommand` → `CONFIRM_MOTION` / `RETRY_LOCKED`
- `directorPictureVideoSurface` / `directorPrimaryCta` → `{ label: 'Tạo video', action: 'motion' }`
- `applyOsCreateVideoCta` previously re-enabled "Tạo video" from local graph even when GET `/actions` said `available=false`
- Click path: `onEnsureMotion` → `startShotMotion` → `createProductionVideo` (and historically `sendTurbo`)
- Local cues that could keep the CTA alive: `kfApproved`, `takeUrl`, `previewUrl`, `acceptedTake`, `turboTaskId`, `videoApproved`, IndexedDB / session pixels

E2E 2026-09-17: SH02 showed "Tạo video" while GET `/actions` returned `CREATE_VIDEO.available=false`, `blockReason=MISSING_DEPENDENCY`.

---

## Exact new authority

Single source:

`GET /api/content/production/shots/{buildId}/{shotCode}/actions`

Rules:

1. Catalog missing, empty, unknown action, or `available !== true` → fail closed. No CREATE_VIDEO CTA. No execute.
2. CTA rendered only when `action.available === true`. No disabled CREATE_VIDEO button to hide a mismatch.
3. Click → `executeProductionOsAction(shotId, 'CREATE_VIDEO')` only after `mayExecuteProductionAction(catalog, 'CREATE_VIDEO')`.
4. `startShotMotion` / `onEnsureMotion` / `sendTurbo` are not the CREATE_VIDEO handler.
5. Desk "Tạo hình" is remapped to `os-create-image` only if `CREATE_IMAGE.available === true`.

---

## Action payload

Official build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, probed 2026-09-17 after login `KIT_MKT` / admin.

### SH02 `EP01-SC01-SH02`

| Field | Value |
|---|---|
| currentState | EMPTY |
| currentArtifactId | null |
| visibleArtifactId | null |
| acceptedArtifactId | null |
| CREATE_VIDEO.available | **false** |
| CREATE_VIDEO.blockReason | MISSING_DEPENDENCY |
| CREATE_VIDEO.requiredInputs | ApprovedKeyframe, Story, ActingBeat, VisualContract, Timing, FrozenProviderModel |
| CREATE_IMAGE.available | true |
| CREATE_VOICE.available | true |
| CREATE_LIPSYNC.available | false |
| MIX_VIDEO.available | false |
| ACCEPT.available | false |

### SH01 `EP01-SC01-SH01`

Same CREATE_VIDEO row: `available=false`, `MISSING_DEPENDENCY`. Catalog is a separate GET per `shotCode` (`osActionsByShot[shotId]`). SH01 and SH02 do not share one authority object.

No shot in this build has `CREATE_VIDEO.available=true`. Production tables are empty (see DB evidence). No fixture-ready shot exists. None was created.

---

## CTA rendering rule

Map (label used only when that action is available):

| Action | Label |
|---|---|
| CREATE_VIDEO | Tạo video |
| CREATE_IMAGE | Tạo hình |
| CREATE_VOICE | Tạo thoại |
| CREATE_LIPSYNC | Khớp môi |
| MIX_VIDEO | Hoàn thiện |
| ACCEPT | Dùng video này |

`applyProductionOsActionAuthority`:

1. Strip every desk CTA whose action is `motion` / `os-create-video` or label is "Tạo video" / "Tạo lại video".
2. Keep "Tạo hình" only as `os-create-image` when `CREATE_IMAGE.available === true`.
3. Append `CREATE_VIDEO · {blockReason} · {requiredInputs}` when CREATE_VIDEO is present and unavailable.
4. Inject `{ label: 'Tạo video', action: 'os-create-video' }` only if `CREATE_VIDEO.available === true`.

`authorizeOsDeskCta` applies the same hide/remap on `ShotProductionCard` (legacy `directorPrimaryCta` / recovery `CONFIRM_MOTION`).

Unavailable CREATE_VIDEO is not rendered as a disabled button.

---

## Click handler

When `CREATE_VIDEO.available === true`:

"Tạo video" → `onExecuteOsAction(shotId, 'CREATE_VIDEO')` → `executeProductionOsAction` → `mayExecuteProductionAction` → confirm → `createProductionVideo` with `result.action === 'CREATE_VIDEO'` (never remapped).

When `CREATE_VIDEO.available === false`:

- No "Tạo video" CTA.
- `executeProductionOsAction` returns immediately. No POST `/api/content/production/requests`.
- `handleAction('motion')` is a no-op.
- `produceShot.ensureMotion` is `() => Promise.resolve()`.
- `startShotMotion` is a no-op (legacy symbol kept for other workflows).

`onEnsurePicture` / `os-create-image` is CREATE_IMAGE, not CREATE_VIDEO. Not clicked in this pass (generation forbidden).

---

## Legacy call trace

Still present, **not** CREATE_VIDEO authority:

| Symbol | Role now |
|---|---|
| `nextShotProductionCommand` | Other stages only. Output is stripped before desk render. |
| `directorPictureVideoSurface` / `directorPrimaryCta` | Legacy labels. Filtered by `applyProductionOsActionAuthority` / `authorizeOsDeskCta`. |
| `startShotMotion` | Empty function. `retryMotionWithNewCamera` still calls it; it posts nothing. |
| `onEnsureMotion` | Required prop, unused on the desk (`void _onEnsureMotion`). |
| `sendTurbo` / `startContentSeriesTurbo` | Marked LEGACY / NON-AUTHORITATIVE. Not bound to Tạo video. |
| `takeUrl` / `previewUrl` / `acceptedTake` / `turboTaskId` / `videoApproved` / IDB | Preview / other workflows. Not read to enable CREATE_VIDEO. |
| `applyOsCreateVideoCta` | No-op. Does not enable Tạo video. |

---

## Browser evidence

Session: KIT_MKT admin, official EP01 6-shot, Director desk.

### SH02

- Desk heading: `Shot 02-SH02`.
- GET `/actions` (XHR): `/api/content/production/shots/4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f/EP01-SC01-SH02/actions`
- Note (`data-os-create-video-unavailable=1`): `Chưa có hình. Bấm Tạo hình. CREATE_VIDEO · MISSING_DEPENDENCY · ApprovedKeyframe, Story, ActingBeat, VisualContract, Timing, FrozenProviderModel`
- Buttons matching Tạo video / Tạo lại video: **none**
- "Tạo hình" present with `data-os-create-image=1` (from CREATE_IMAGE, not invented from `kfApproved`)
- Instrumented `window.fetch` + `XMLHttpRequest.open` after open SH02 / SH01:

  - POST `/api/content/production/requests` = **0**
  - POST `/series/turbo` = **0**
  - GET `/actions` only

Tạo hình / Tạo hình mới were **not** clicked (would be CREATE_IMAGE / picture, forbidden this mission). With no CREATE_VIDEO CTA, the CREATE_VIDEO handler cannot be invoked from the desk.

### SH01

- Desk heading: `Shot 01-SH01`
- Own GET `/actions` for `EP01-SC01-SH01`
- CREATE_VIDEO also unavailable → no "Tạo video"; "Tạo hình" is `os-create-image`
- Catalogs do not share one in-memory row; switching shots re-GETs that shot’s actions

---

## Tests

`npx tsx src/modules/content/production-os-action-authority.smoke.ts`

**PASS FAIL=0**

| Id | Case | Result |
|---|---|---|
| A | unavailable action → no CTA | PASS |
| B | unavailable / missing / empty / unknown → no execution | PASS |
| C | available action → exact `os-create-video` / `mayExecuteProductionAction` | PASS (unit). Browser execute **UNVERIFIED** (no fixture-ready shot) |
| D | CREATE_VIDEO never remaps; CTA map 1:1 | PASS |
| E | `startShotMotion` has no `createProductionVideo` / turbo; click is `executeAction` | PASS |
| F | SH01 / SH02 catalogs independent | PASS |

Related smokes updated and green: `famixa-shot-stale-remake-gate`, `famixa-director-video-workspace-restructure`, `famixa-video-studio-ux`.

---

## DB evidence

```
pack_content.production_requests                 0
pack_content.production_tasks                    0
pack_content.production_task_input_snapshots     0
pack_content.production_attempts                 0
pack_content.production_artifacts                0
pack_content.production_bindings                 0
pack_content.production_task_events              0
```

Counts unchanged after the SH02 / SH01 browser pass. No fixture-ready OS KEYFRAME / VIDEO_TAKE row exists.

---

## Paid calls

**0**

No Veo, Runway, Wan, Gemini, Fal paid submit, fixture adapter run, or keyframe import.

---

## UNVERIFIED

C-browser (available → execute exact CREATE_VIDEO): no shot in this repository currently returns `CREATE_VIDEO.available=true`. Do not invent a shot. Re-test execution only after OS GET `/actions` shows available on an existing shot.
