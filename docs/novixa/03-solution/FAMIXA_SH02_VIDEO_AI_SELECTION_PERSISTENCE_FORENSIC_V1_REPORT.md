# FAMIXA SH02 — VIDEO AI SELECTION PERSISTENCE FORENSIC V1

Date: 2026-09-22  
Shot: `EP01-SC01-SH02`  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Stage: VIDEO  
Mode: READ-ONLY. No POST. No snapshot/timing/permit/CREATE_VIDEO. No provider. No UI/graph change.

Classification: FACT / LIVE_VERIFIED.

---

## 1. VERDICT

Canonical VIDEO selection for official SH02 **does not exist**.

| Check | Result | Class |
|---|---|---|
| `GET /api/content/production/ai-selections?buildId=4ef164d6-…&shotCode=EP01-SC01-SH02&stage=VIDEO` | **404** ProblemDetails | LIVE_VERIFIED |
| `pack_content.production_ai_selection` rows for that shot | **0** (any status) | LIVE_VERIFIED |
| Preview `providerId` / `modelId` / `selectionId` | empty / empty / null | LIVE_VERIFIED |
| CREATE_VIDEO block | `MISSING_PRODUCTION_AI_SELECTION` / `FrozenProviderModel` | LIVE_VERIFIED |

UI can still show **Google Veo 3** because Model video is **not** bound to that GET. It is bound to in-memory `osVideoModel.selection` (optimistic persist) and/or the global `engine` React/localStorage clock used on TẠO VIDEO copy.

Root cause is client persist/hydrate, not a missing server endpoint.

---

## 2. EXACT UI SOURCE — “Model video: Google Veo 3”

**Picker (the “Model video” row)**

File: `client/admin/src/modules/content/FamixaMotionEnginePicker.tsx`

- Label: `formatDirectorVideoModelLabel(selectionProviderId, selectionModelId)` → `data-current-video-model`
- Props come from `osVideoModel?.selection` (`ShotProductionWorkspace` / `ContentFamixaStudioView`)
- `engine` prop is **explicitly discarded** (`void _engine`)
- Empty selection → **`Chưa xác định`**, not Veo
- `google` / `veo` / model contains `veo` → **`Google Veo 3`** (`famixa-director-video-model-display.ts`)

**State owner**

`ContentFamixaSeriesTab.tsx` → `osVideoModelByShot[studioShotId].selection`

Writers (only two):

1. `persistEngine` — optimistic local write **before** POST
2. hydrate `useEffect` — GET selection; on 404 **keeps** `osVideoModelByShotRef.current[shotId]?.selection`

**Second Veo string (not the picker, easy to conflate)**

- `engine` = `useState(loadEngine)` ← `localStorage['kit.famixaSeries.engine']`
- `ContentFamixaStudioView.tsx`: CTA `Tạo video · Google Veo 3` / `KF đã đổi · Google Veo 3` when `engine === 'veo'`
- `generateCost('veo')` → `Google Veo 3 · Vertex`

That `engine` is **one value for the whole series session**, not per-shot OS selection.

This conversation’s last official SH02 BÀN SHOT snapshot (before later Director clicks) showed picker **Chưa xác định**. A later session label of Google Veo 3 is therefore either optimistic `osVideoModel.selection` after **Thay đổi**, or the engine CTA — not a canonical row.

---

## 3. EXACT HANDLERS

### Nút “Thay đổi”

`FamixaMotionEnginePicker` `Button` `onClick={() => setEditing((v) => !v)}`  
Toggles local `editing`. **No API.**

### Chọn `google / veo-3.1`

Select `onChange`:

```ts
onEngine(next);  // next === 'veo'
setEditing(false);
```

Option value is **`veo`** (`FAMIXA_MOTION_ENGINE_OPTIONS`), not the string `google/veo-3.1`. Mapping happens in persist.

### persistEngine — **is called** on every Select confirm

`ContentFamixaSeriesTab.tsx` `persistEngine`  
Wired: `onEngine={persistEngine}` on Workspace + StudioView.

Order (FACT):

1. `setEngine(next)` — global session engine
2. If no `studioShotId` → **return** (POST never runs)
3. `mapped = providerModelFromEngine(next)`  
   `veo` → `{ providerId: 'google', modelId: 'veo-3.1' }`
4. **`setOsVideoModelByShot(applyDirectorVideoModelSelection(...))` — local UI already Google Veo 3**
5. `upsertProductionAiSelection({ buildId, shotCode, episodeCode, sceneCode, stage, providerId, modelId, reason })`
6. `.then` writes `selectionId` from server
7. **`.catch(() => { /* selection display stays local; VIDEO_TAKE is not regenerated */ })` — swallow, no toast, no revert**

`persistFamixaMotionEngine` is **never called** from persistEngine (only defined). localStorage engine is not updated by this handler.

---

## 4. EXACT API

| | |
|---|---|
| Client | `upsertProductionAiSelection` / `getProductionAiSelection` in `content.api.ts` |
| POST | `/api/content/production/ai-selections` |
| GET | `/api/content/production/ai-selections?buildId=&shotCode=&stage=VIDEO` |
| Server | `ContentProductionController.UpsertAiSelection` / `GetAiSelection` → `UpsertActiveAsync` / `GetActiveAsync` |
| Official film | **not blocked** on upsert (unlike CREATE_VIDEO / permit) |

### Expected request if Director confirms Veo on official SH02

```json
{
  "buildId": "4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f",
  "shotCode": "EP01-SC01-SH02",
  "episodeCode": "EP01",
  "sceneCode": "SC01",
  "stage": "VIDEO",
  "providerId": "google",
  "modelId": "veo-3.1",
  "reason": "DIRECTOR_SELECTION"
}
```

`episodeCode`/`sceneCode` from `productionScopeFromShotCode` (`EP01` / `SC01`). FACT

### Live GET response (this forensic)

`GET` official SH02 VIDEO:

- **status 404**
- body: ASP.NET ProblemDetails `{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Not Found", "status": 404, "traceId": "00-a6cfa59b0357f8b1f5bbcfe3b98ce1e9-1c59e53ed367247d-00" }`

No successful POST for official SH02 exists in DB, so there is **no POST status/body** to quote for this shot. A 200 would have inserted `status=ACTIVE`.

GET 404 on hydrate is caught: `getProductionAiSelection(...).catch(() => null)`.

---

## 5. CANONICAL DB

`pack_content.production_ai_selection` for build `4ef164d6-…`:

| shot | stage | provider/model | status | when |
|---|---|---|---|---|
| EP01-SC01-SH01 | VIDEO | runway / gen4_turbo | ACTIVE | 2026-09-20 |
| EP01-SC01-SH01 | VOICE | elevenlabs / eleven_v3 | ACTIVE | 2026-09-21 |
| **EP01-SC01-SH02** | — | — | **no row** | — |

Other VIDEO row (not official SH02):

| TEST-SC01-SH02 | VIDEO | runway / gen4_turbo | ACTIVE | 2026-09-22 11:38:50 +07 |

That TEST row proves `persistEngine` → POST **can succeed**. It landed on TEST scope `bbbbbbbb-0000-4000-8000-000000000371` (prepended TEST shots on BÀN SHOT), **not** official `EP01-SC01-SH02`. Provider is runway, not google.

Zero `google` / `veo` rows in the table. LIVE_VERIFIED

---

## 6. WHY UI ≠ CANONICAL

Exact conditions:

1. **`persistEngine` updates local selection before POST**  
   `ContentFamixaSeriesTab.tsx` `persistEngine` lines: `setOsVideoModelByShot` then `void upsertProductionAiSelection`.

2. **POST failure is swallowed**  
   `.catch(() => { /* selection display stays local */ })`  
   UI stays Google Veo 3; OS stays empty.

3. **Hydrate treats 404 as “keep local”**  
   `nextSelection = selection ? api : osVideoModelByShotRef.current[shotId]?.selection`  
   GET 404 → `selection` null → **does not clear** optimistic Veo.

4. **Picker does not re-read GET as SoT on render**  
   It only reads `osVideoModel.selection`.

5. **Optional mix-up:** global `engine === 'veo'` paints TẠO VIDEO as Google Veo 3 even when picker is `Chưa xác định`.

6. **TEST shot trap:** first “Mở Shot” can be `TEST-SC01-SH0x`. persistEngine then writes TEST scope. Official SH02 GET stays 404.

After a full page reload with no local `osVideoModel`, official SH02 picker **must** be `Chưa xác định` (GET 404 + empty state). If the user still sees Veo after reload, they are reading the `engine` CTA or they clicked Thay đổi again in that session.

---

## 7. SH01 STRUCTURE ONLY (do not copy)

| | SH01 | SH02 |
|---|---|---|
| Persist path | Same table + `POST /ai-selections` | same endpoint available |
| How SH01 row was created | Server readiness: `UpsertSelectionAsync` reason `REAL_FILM_READINESS_V1 explicit Production OS VIDEO selection` (2026-09-20). **Not** Director UI `DIRECTOR_SELECTION`. | no row |
| GET | 200 runway / gen4_turbo `9b0dceee-…` | 404 |
| UI persistEngine | same function | same function |

Do not copy SH01 `runway/gen4_turbo` onto SH02.

---

## 8. BUG (report only)

| File | Function | Condition |
|---|---|---|
| `ContentFamixaSeriesTab.tsx` | `persistEngine` | Local `setOsVideoModelByShot` **before** POST; `.catch` empty |
| `ContentFamixaSeriesTab.tsx` | hydrate `useEffect` (`studioShotId`, `state.buildId`) | `getProductionAiSelection.catch(() => null)` then fallback to prior local selection |
| `FamixaMotionEnginePicker.tsx` | display | Label from local props only; `engine` ignored (correct for SoT) but session `engine` still labels TẠO VIDEO |

Server GET/POST contract is consistent. Official upsert is allowed. The hole is client optimistic + fail-open + 404 retain.

---

## 9. RECOMMENDED NEXT IMPLEMENTATION SCOPE

Do **not** do it in this forensic.

1. Official SH02 only: Director confirm → `POST /ai-selections` VIDEO `{ google, veo-3.1 }` (or whatever Director picks) → require 200 → then GET actions/state/preview.
2. Fail closed: no local Google Veo 3 until POST 200; toast on error; revert selection.
3. Hydrate: GET 404 ⇒ `selection: undefined` (Chưa xác định). Never keep optimistic row.
4. Do not write graph seconds, do not issue permit, do not CREATE_VIDEO, do not copy SH01.
5. Keep TEST persist isolated (`productionOsBuildIdForShot`).

STOP.
