# FAMIXA SH02 — PERFORMANCE SNAPSHOT BLOCKER FORENSIC V1

Date: 2026-09-22  
Shot: `EP01-SC01-SH02`  
Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Mode: READ-ONLY. No code change. No AI/provider. No CREATE_VIDEO. No permit. No new snapshot. No timing/selection write.

Classification: FACT / LIVE_VERIFIED.

---

## 1. VERDICT

**Official SH02 `CREATE_VIDEO` is not `MISSING_PERFORMANCE_SNAPSHOT`.**

Live canonical GET:

| Field | Value | Class |
|---|---|---|
| `CREATE_VIDEO.available` | `false` | LIVE_VERIFIED |
| `CREATE_VIDEO.blockReason` | **`MISSING_PRODUCTION_AI_SELECTION`** | LIVE_VERIFIED |
| `CREATE_VIDEO.missingInputs` | **`FrozenProviderModel` only** | LIVE_VERIFIED |
| PerformanceSnapshot | **exists** — latest `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` | LIVE_VERIFIED |
| Production Timing (OS) | **10s** (`productionDurationSec=10`, `timingSource=DIRECTOR`, `decisionType=EXPLICIT_PRODUCTION_DECISION`) | LIVE_VERIFIED |

`MISSING_PERFORMANCE_SNAPSHOT` is the live `CREATE_VIDEO` block on **TEST** shot `TEST-SC01-SH02` (TEST scope `bbbbbbbb-0000-4000-8000-000000000371`), not official `EP01-SC01-SH02`. LIVE_VERIFIED

The BÀN SHOT string always appends the catalog `requiredInputs` list, which **includes the name** `PerformanceSnapshot` even when that input is **not missing**. UI helper: `formatProductionActionReason` → `CREATE_VIDEO · {blockReason} · ApprovedKeyframe, Story, …, PerformanceSnapshot`. FACT

---

## 2. ENDPOINTS READ

Canonical paths (controller is `shots/{buildId}/{shotCode}/…`, not `/production/actions`):

1. `GET /api/content/production/shots/{buildId}/{shotCode}/actions`
2. `GET /api/content/production/shots/{buildId}/{shotCode}/state`
3. `GET /api/content/production/shots/{buildId}/{shotCode}/create-video-input-preview?episodeCode=EP01&sceneCode=SC01`
4. `GET /api/content/production/performance-snapshots/{id}`
5. `GET /api/content/production/ai-selections?buildId=&shotCode=&stage=VIDEO` → **404** on SH02

Same GET with UI hydrate claim (`story`/`actingBeat`/`visualContract`, no `timing`, no snapshot id): `CREATE_VIDEO` block unchanged (`MISSING_PRODUCTION_AI_SELECTION`). LIVE_VERIFIED

---

## 3. LIVE STATE — OFFICIAL SH02

`GET .../actions` and `GET .../state` (no claim):

| Action | available | blockReason | missingInputs |
|---|---|---|---|
| CREATE_IMAGE | true | — | — |
| **CREATE_VIDEO** | false | **MISSING_PRODUCTION_AI_SELECTION** | **FrozenProviderModel** |
| CREATE_VOICE | false | MISSING_DEPENDENCY | LineText, VoiceId, LineId, CharacterId |
| CREATE_LIPSYNC | false | MISSING_VIDEO_ARTIFACT | MotionArtifactId, VoiceArtifactId |
| MIX_VIDEO | false | MISSING_LIPSYNC_ARTIFACT | LipSyncArtifactId |

Nodes: STORY/ACTING AVAILABLE; KEYFRAME CURRENT `91daaf4b-5550-4cbc-b9f9-0bd0be556cef`; VOICE/LIPSYNC/MIX MISSING. LIVE_VERIFIED

`GET create-video-input-preview`:

| Field | Value |
|---|---|
| timing | `10.0s` |
| durationSec | `10` |
| timingStatus | `AUTHORITATIVE` |
| performanceSnapshotId | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` |
| providerId / modelId | empty |
| selectionId | null |
| createVideoAvailable | false |
| blockReason | `MISSING_PRODUCTION_AI_SELECTION` |

VIDEO AI selection: 404. FACT

---

## 4. EXACT CREATE_VIDEO RULE

File: `ProductionExecutionService.EvaluateCreateVideoAsync` + `CreateVideoActionContractGate.Evaluate` + `ProductionPerformanceConsumption`.

Order that matters for official film:

1. Load OS facts (CURRENT:KEYFRAME, Story/Acting/Visual from last CREATE_IMAGE/CREATE_VIDEO input snap, **Director timing from latest PerformanceSnapshot**).
2. Official + no ACTIVE `production_ai_selection` VIDEO → provider/model stay empty → gate adds `FrozenProviderModel`.
3. Official + `MissingTiming` remaps to `TIMING_PRODUCTION_DECISION_REQUIRED`. **Not this shot.**
4. Official + WhatInputsPresent (KF + Story + Acting + Visual + Timing) + selection null → **`MISSING_PRODUCTION_AI_SELECTION`**. **This is the live SH02 path.**
5. `ProductionPerformanceConsumption.ActionRequiresSnapshot` is **`false` on official film**. CollectMissing does **not** add `PerformanceSnapshot` for official `CREATE_VIDEO`.
6. `BindCreateVideoSnapshotAsync`: no claim id → `GetByScopeAsync` + `HasDirectorTiming` → attach snapshot id/hash. Does **not** fail when snapshot exists.

`MISSING_PERFORMANCE_SNAPSHOT` is emitted for `CREATE_VIDEO` only when `ActionRequiresSnapshot` is true (non-official / TEST build) and the GET/POST claim has no `performanceSnapshotId`. FACT

TEST `TEST-SC01-SH02` on TEST scope: `blockReason=MISSING_PERFORMANCE_SNAPSHOT`, `missingInputs=[PerformanceSnapshot]`. LIVE_VERIFIED

---

## 5. SH02 PERFORMANCE SNAPSHOT — EXISTS

Latest by `GetByScopeAsync` (`ORDER BY frozen_at DESC LIMIT 1`):

`GET /api/content/production/performance-snapshots/6505b8a2-8a71-4d74-8d93-f1fae8319b6b`

| Field | Value | Class |
|---|---|---|
| shotCode | EP01-SC01-SH02 | LIVE_VERIFIED |
| frozenAt | 2026-09-22T03:49:14.958Z | LIVE_VERIFIED |
| productionDurationSec | **10** | LIVE_VERIFIED |
| timingSource | DIRECTOR | LIVE_VERIFIED |
| decisionType | EXPLICIT_PRODUCTION_DECISION | LIVE_VERIFIED |
| keyframeArtifactId | 91daaf4b-…556cef | LIVE_VERIFIED |
| lines[] | empty | LIVE_VERIFIED |

Prior freeze (same second, 5s): `cf1bdd40-0533-4452-a363-4135afc4e037`, `productionDurationSec=5`. Not current authority. LIVE_VERIFIED

GET DTO `issues: [OFFICIAL_FILM_FORBIDDEN]` is `ValidateDraft` without `extraJson` — display-only. It is **not** the CREATE_VIDEO blockReason. FACT

### Canonical freeze endpoint (already used; do not call again this mission)

`POST /api/content/production/director-timing`

Body: `{ buildId, shotCode, episodeCode, sceneCode, durationSec }`

Generic `POST /api/content/production/performance-snapshots` exists (raw draft freeze). Official CREATE_VIDEO timing authority is the director-timing extra (`DIRECTOR` + `EXPLICIT_PRODUCTION_DECISION` + duration > 0), not a raw freeze. FACT

### Conditions to freeze (director-timing)

- CURRENT VERIFIED KEYFRAME on that shot
- EvaluateCreateVideo Story, ActingBeat, VisualContract all present
- `durationSec > 0` (Director 5 or 10)
- Does **not** require FrozenProviderModel / AI selection
- Does **not** require spoken lines

### Where snapshot data comes from

`MaterializeDirectorTimingAsync` copies **this shot’s** evaluate facts:

- Story / ActingBeat / VisualContract from OS evaluate (CREATE_IMAGE ingress facts — not SH01)
- Keyframe artifact id + SHA
- Duration from Director POST
- ActingBeatSource before-action = that ActingBeat; gaze hardcoded `"Linh looks toward Minh."` in the materializer
- Character ids CHAR-003 / CHAR-001
- Hashes: timing / story / visual

Depends on ActingBeat + VisualContract + Story + Keyframe + Director duration. Does **not** depend on VIDEO AI selection. FACT

---

## 6. PRODUCTION TIMING — CANONICAL 10s

Authority = latest PerformanceSnapshot for (build, SH02), not graph.

| Source | Value | Used as SoT? |
|---|---|---|
| Snap `6505b8a2` `productionDurationSec` | **10** | YES |
| Preview `durationSec` / `timing` | 10 / `10.0s` | derived from that snap |
| Snap `cf1bdd40` | 5 | superseded |
| SH01 snap 5s | 5 | other shot — not SH02 |
| shot.seconds / UI 5s·10s / provider duration | — | NOT evidence |

---

## 7. SH01 STRUCTURE ONLY (no copy)

| | SH01 | SH02 |
|---|---|---|
| Latest snap | `7dbaa094-55ff-4eea-8413-8e8ce2cbf254` | `6505b8a2-…` |
| Freeze family | `POST /director-timing` (same command). SH01 later also has `POST /voice-production-ingress` (**SH01-only**; CrossShotForbidden on SH02). | `POST /director-timing` only |
| Required for director-timing | KF + Story + ActingBeat + VisualContract + durationSec>0 | same fields, SH02 values |
| Duration | 5 | **10** |
| timingSource / decisionType | DIRECTOR / EXPLICIT | same |
| lines[] | locked spoken line `line-SC01-CHAR-003-1` | empty |
| VIDEO selection | runway / gen4_turbo | **none** (404) |
| CREATE_VIDEO block | `OFFICIAL_FILM_FORBIDDEN` (paid + no issued permit) | `MISSING_PRODUCTION_AI_SELECTION` |

Do not copy SH01 duration, lines, selection, or permit onto SH02. FACT

---

## 8. CANONICAL NEXT ACTION (not executed)

Do **not** create another PerformanceSnapshot. Timing is already AUTHORITATIVE 10s.

Next CREATE_VIDEO input on official SH02:

`POST /api/content/production/ai-selections`  
`stage=VIDEO`, this shot’s `buildId`/`shotCode`, Director-chosen `providerId`/`modelId`.

That fills `FrozenProviderModel`. After that, paid Runway/Veo still needs an issued permit (`OFFICIAL_FILM_FORBIDDEN`) before CREATE_VIDEO can become available. No auto-run.

STOP.
