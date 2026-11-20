# FAMIXA PRODUCTION OS — SHOT IDENTITY ROUTING FORENSIC + FIX V1

Date: 2026-09-23  
Mode: FORENSIC + MINIMAL FIX  
No CK24 / MIX.

## 1. ROOT CAUSE

Director mode prepended TEST fixture shots onto every open film, then remapped API BuildId from `shotCode` alone.

| Point | File | Function | What happened |
|---|---|---|---|
| Inject | `ContentFamixaSeriesTab.tsx` | `deskShots` | `[...productionOsTestShotStubs(), ...officialShots]` put `TEST-SC01-SH01/SH02` first on official EP01 |
| Remap | `production-os-action-authority.ts` | `productionOsBuildIdForShot` | `TEST-SC01-SH02` → `bbbbbbbb-0000-4000-8000-000000000371` even when card BuildId was `4ef164d6-…` |
| Stale | `ContentFamixaSeriesTab.tsx` | `openBuild` | `Tiếp tục →` passed only BuildId; `studioShotId` was not cleared |
| Fallback | `ShotProductionNavigator.tsx` | `resolveLaneAShotId` | missing/scene request → `shots[0]` (TEST stub after inject) |
| Hydrate | `ContentFamixaSeriesTab.tsx` | OS GET effect | fetched TEST sandbox → fixture story + runway / gen4_turbo |

Backend `GET /production/shots/{buildId}/{shotCode}/state` already required both ids. The client sent the wrong pair.

## 2. FIX

- Desk shots are scoped to the open build (`productionOsDeskShots`). Official film no longer lists TEST stubs.
- `productionOsBuildIdForShot` no longer remaps official → TEST.
- `openBuild` clears stale `studioShotId`.
- Shot-code miss is `SHOT_NOT_FOUND`, not first/latest.
- Workspace refuses to render a mismatched pair (`IDENTITY_MISMATCH`).
- URL writes `{ build, shot }` so refresh / new tab keep the same pair.

Minimal: client identity routing only. No snapshot, artifact, provider, or CK20–CK23 change.

## 3. IDENTITY CONTRACT

Canonical identity = `(BuildId, ShotId)`.

`(4ef164d6-…, EP01-SC01-SH02)` ≠ `(bbbbbbbb-…, TEST-SC01-SH02)`.

Card → URL → hydrate → workspace all use that pair. Cross-build shotCode is fail-closed.

## 4. REGRESSION

| Case | Result |
|---|---|
| A official SH02 | PASS |
| B official deep link | PASS |
| C TEST SH02 | PASS |
| D official → TEST | PASS |
| E TEST → official | PASS |
| F missing shot | PASS (`SHOT_NOT_FOUND`, no fallback) |
| G shot on wrong build | PASS (no load) |
| H refresh official SH02 | PASS (URL pair) |
| I new tab official deep link | PASS |

Smoke: `FAMIXA_PRODUCTION_OS_SHOT_IDENTITY_ROUTING_V1 PASS`

## 5. PRODUCTION SAFETY

No provider POST. No CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC / MIX. No permit consume. No artifact / snapshot / selection / graph mutation.

Fal Start remains 1 (CK23). ElevenLabs Start = 0.

## 6. CURRENT CANONICAL STATE

BuildId: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
ShotId: `EP01-SC01-SH02`  
Story: official SH02 story from the film graph (not TEST fixture)  
VIDEO: `b44e530b-5b59-422f-9b80-e058a3a639b5`  
VOICE: `0fde86c2-250f-4303-bda0-4913a6dbd1e4`  
LIPSYNC: `32bec770-9706-4aad-bc21-8db3d4f11d4c`

## 7. VERDICT

PASS

STOP. Do not start CK24 / MIX.
