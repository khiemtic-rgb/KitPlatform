# FAMIXA SH02 — VIDEO AI SELECTION FAIL-CLOSED V1

Date: 2026-09-22  
Scope: client persist/hydrate only. No OS contract, server API, gate, timing, permit, or CREATE_VIDEO execution.

---

## 1. WHAT CHANGED

`persistEngine` no longer writes `osVideoModel.selection` before POST.

POST ` /ai-selections` → commit only if 2xx body has `selectionId` + `providerId` + `modelId`.

POST fail → revert to prior **confirmed** selection (`selectionId` present) or `undefined` (`Chưa xác định`) + toast `Không lưu được Model video.`

Hydrate GET 200 → server row. GET 404 → `undefined`. Other errors → keep prior only if confirmed.

`FamixaMotionEnginePicker` still paints from shot-scoped `osVideoModel.selection`. Global `engine` unchanged for editorial CTA (not SoT).

---

## 2. FILES

- `famixa-director-video-model-display.ts` — `directorVideoSelectionFromRow`, `hydrateDirectorVideoSelection`, `revertDirectorVideoSelectionAfterPostFailure`, `isConfirmedDirectorVideoSelection`
- `ContentFamixaSeriesTab.tsx` — persist + hydrate
- `FamixaMotionEnginePicker.tsx` — `busy` disable while POST in flight
- `ShotProductionWorkspace.tsx`, `ContentFamixaStudioView.tsx` — pass `directorSelectionBusy`
- `famixa-video-ai-selection-fail-closed.smoke.ts`

---

## 3. TESTS

Smoke: `FAMIXA_VIDEO_AI_SELECTION_FAIL_CLOSED_V1 ok` (+ display + timing smokes).

Live official `4ef164d6-…` / `EP01-SC01-SH02`:

| Step | Result |
|---|---|
| GET VIDEO before | **404** |
| POST empty provider | **400** `MISSING_PRODUCTION_AI_SELECTION`; GET still 404 |
| POST `{ google, veo-3.1, DIRECTOR_SELECTION }` | **200** `selectionId=c52b646f-86b9-4a43-834d-d94ab9f013b8` ACTIVE |
| GET after | **200** google / veo-3.1 |
| SH01 GET | unchanged `9b0dceee-…` runway / gen4_turbo |
| TEST-SC01-SH02 | still TEST scope `bbbbbbbb-…` runway (not official) |

CREATE_VIDEO was **not** POSTed. After selection, evaluate `available=true` (Veo path; no permit issued). Do not click Tạo video.

Browser login for Model video label was not completed (password fill blocked). After reload, hydrate GET 200 must show **Google Veo 3**; a 404 hydrate must show **Chưa xác định**.

---

## 4. INVARIANT

UI Model video == canonical `production_ai_selection` after GET/POST settle. Pending POST does not write selection. POST fail / GET 404 → `Chưa xác định` unless a prior confirmed `selectionId` exists.

STOP.
