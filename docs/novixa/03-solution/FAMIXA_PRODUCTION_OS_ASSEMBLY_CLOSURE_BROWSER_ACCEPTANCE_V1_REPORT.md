# FAMIXA PRODUCTION OS — ASSEMBLY CLOSURE + BROWSER ACCEPTANCE V1

Date: 2026-09-18  
Mode: CLOSURE ONLY  
Scope: TEST-SC01-SH02 only  
Live providers: none  
Paid spend: none  
Film mutation: none

Do not read this as FILM READY.  
Do not read this as REAL PROVIDER READY.  
Do not read this as FINAL.

---

## 1. Verdict

**PASS WITH GAPS.** FACT / IMPLEMENTED / TESTED / LIVE VERIFIED

The UI Cutover V1 technical failure is closed: ASSEMBLE no longer returns HTTP 400 on `FilmAssembly` Dapper materialization. Browser CTAs on TEST-SC01-SH02 reach Production OS. Fixture only.

VISIBLE / CURRENT / ACCEPTED remain distinct. The REGENERATE semantic follow-up is **not** patched in this phase.

---

## 2. FilmAssembly materialization fix

**IMPLEMENTED.** Existing `FilmAssembly` domain record + existing `production_film_assemblies` table. No second model, no second table, no second persistence, no lineage change.

Root cause (FACT, prior live 400):

`ListAssembliesByBuildAsync` used `QueryAsync<FilmAssembly>` against TIMESTAMPTZ `created_at`. Dapper required ctor `(Guid assemblyid, Guid buildid, string title, DateTime createdat)`. The domain record only had `DateTimeOffset CreatedAt`.

Fix (compatible mapping only):

1. `FilmAssembly` keeps `DateTimeOffset CreatedAt` and adds the DateTime ctor Dapper asked for (`ProductionEntities.cs`).
2. `ListAssembliesByBuildAsync` maps through existing-style `AssemblyRow` (`DateTime CreatedAt`) then `new FilmAssembly(...)` (`ProductionWorkRepository.cs`). Same pattern as `AsmRow` → `FilmAssemblyShot`.

`QueryAsync<FilmAssembly>` is gone. Artifact lineage / Request / Task / Snapshot / Attempt model unchanged.

**TESTED.** `Complete_action_authority_v1_fixture_chain` PASS (includes DateTime ctor assertion + `AssemblyRow` scan).

---

## 3. ASSEMBLE live TEST

**LIVE VERIFIED.** TEST-SC01-SH02. Idempotent UI key `os-ui:ASSEMBLE:{TEST scope}:{TEST-SC01-SH02}`. HTTP **200** (was 400).

| Field | Value |
|---|---|
| requestId | `e03401ed-302c-4d88-882c-9bbd91b98df1` |
| taskId | `634e2fad-f5df-4cc9-b8c0-3e0cff4f8548` |
| snapshotId | `8e0be97c-b837-45b2-b30e-ec11618596aa` |
| attemptId | `5c3b0369-2d6b-4524-9ecb-bb4715089d50` |
| action | ASSEMBLE |
| adapter | `fixture-assembly-v1` |
| assemblyId / providerJobId | `0ab1d54e-a252-4c52-9575-0951674838a4` |
| acceptedArtifactIds[0] | `638bd185-d2aa-4873-ab23-0ce2ece76839` |
| shotCodes[0] | TEST-SC01-SH02 |

`acceptedArtifactIds[0]` == existing ACCEPTED MIX artifactId `638bd185-…`. FACT.

FilmAssembly row exists:

`0ab1d54e-…` title `TEST-ASSEMBLY` · shot `TEST-SC01-SH02` · artifact `638bd185-…` · sort 0.

Path: ProductionRequest → ProductionTask → InputSnapshot → Attempt → FixtureAssemblyAdapter → FilmAssembly row.

No URL identity. No `POST /content/series/assemble`. No media minted. No FINAL.

---

## 4. Browser CTA acceptance

**LIVE VERIFIED** on existing Production OS UI. TEST-SC01-SH02 desk (`TEST fixture story`). Each catalog button has `data-os-action`. Confirm copy: Production OS / Fixture — no Gemini / Veo / Runway / Wan / Fal / ElevenLabs.

Fixture replay only. No prettier-media regeneration.

| Click | ProductionAction | requestId | taskId | attemptId | artifactId | Class |
|---|---|---|---|---|---|---|
| Tạo hình | CREATE_IMAGE | c007dc16-… | 6c635fff-… | fe819f33-… | 16c1a4e8-… | LIVE VERIFIED |
| Tạo video | CREATE_VIDEO | 57c4b90f-… | bc3ad53d-… | f704819f-… | 78d57d06-… | LIVE VERIFIED |
| Nạp thoại | CREATE_VOICE | 0492fb2a-… | d515c1c1-… | 333a0a3a-… | 3a753477-… | LIVE VERIFIED |
| Khớp môi | CREATE_LIPSYNC | 34c139da-… | 1e0e7964-… | 6fecd3e8-… | 49deaa6b-… | LIVE VERIFIED |
| Hoàn thiện | MIX_VIDEO | 4fecf495-… | 11483688-… | eff90738-… | 638bd185-… | LIVE VERIFIED |
| Dùng kết quả | ACCEPT | da84b77d-… | ad6e8492-… | 1e2c64e2-… | 638bd185-… | LIVE VERIFIED |
| Lắp tập | ASSEMBLE | e03401ed-… | 634e2fad-… | 5c3b0369-… | assembly `0ab1d54e-…` / MIX `638bd185-…` | LIVE VERIFIED |

Browser resource log after the confirm chain (FACT):

`POST /api/content/production/requests` (repeated)  
`GET …/shots/{TEST}/{TEST-SC01-SH02}/state`  
`GET …/actions`  
`GET …/preview/media`

Zero ` /content/series/assemble`. Zero `/series/turbo`. Zero `/series/still`.

Idempotent replay: no new request rows. Same OS ids as the 09:46 TEST chain. FACT.

---

## 5. Preview authority

**LIVE VERIFIED** after MIX / ASSEMBLE / reload:

| Probe | Value |
|---|---|
| GET `/state` visibleArtifactId | `583d7700-…` (REGENERATE B) |
| GET `/state` currentArtifactId | `78d57d06-…` (CREATE_VIDEO A) |
| GET `/state` acceptedArtifactId | `638bd185-…` (MIX) |
| GET `/preview` artifactId | `583d7700-…` |
| GET `/preview/media` | 200, `video/mp4`, 707 bytes |

Preview contract: **VISIBLE → artifactId → /preview/media → physical bytes.**

Not `takeUrl` / `previewUrl` / `lipsyncUrl` / `acceptedTake.url`. FACT.

---

## 6. STATE SEMANTIC FOLLOW-UP

**NOT PATCHED** (ordered out of this phase).

After REGENERATE:

- VISIBLE = B `583d7700-…`
- CURRENT on GET `/state` still names CREATE_VIDEO A `78d57d06-…`
- ACCEPTED = MIX `638bd185-…`

Preserve VISIBLE ≠ CURRENT ≠ ACCEPTED. Do not merge them.

---

## 7. Reload

**LIVE VERIFIED.** One browser reload of `/content/videos` after the completed TEST chain. Session persisted. GET `/state` + GET `/preview` reconstructed the same Production OS bindings as §5.

No per-action reload testing.

---

## 8. Cross-shot

**LIVE VERIFIED** this turn (self-contained):

CREATE_LIPSYNC on TEST-SC01-SH02 with SH01 4A VIDEO `63bca29a-…` + SH02 VOICE `3a753477-…` → HTTP 400 `{"message":"CROSS_SHOT_FORBIDDEN"}`.

No SH01 adapter artifact. SH01 4A SHA unchanged (§9).

---

## 9. Film safety before / after

| Probe | Expected | After | Class |
|---|---|---|---|
| EP01 `4ef164d6-…` graph md5 | `12289be18284fbd4d4b166b82c09eba0` | same | LIVE VERIFIED |
| EP99 `77262ec0-…` graph md5 | `c7cd2516627332c2648c1509395e4a64` | same | LIVE VERIFIED |
| series_build count | 4 | 4 | LIVE VERIFIED |
| production_bindings on EP01 official build | 0 | 0 | LIVE VERIFIED |
| SH01 4A VIDEO `63bca29a-…` SHA | `f94a9d097e5d90c0…8c8c17` | same | LIVE VERIFIED |

Canon / VA / EP01 / EP99 / SH01 not modified. FACT.

---

## 10. Provider / spend

Fixture adapters only. Zero Gemini / Runway / Veo / Fal / ElevenLabs jobs. Zero paid spend. FACT.

---

## 11. Incidental mount note

Videos Series tab was gated on `requestAnimationFrame`. This automation tab does not schedule rAF (unpainted). **IMPLEMENTED** `useEffect(() => setSeriesMount(true))` so the existing Series / Production OS UI can mount. Not a new engine. Not a CTA redesign.

---

## 12. Remaining / out of scope

Not fixed (as ordered):

1. **STATE SEMANTIC FOLLOW-UP** — VISIBLE B vs CURRENT A after REGENERATE.
2. Character Workspace `/series/still`.
3. Legacy display media / local snap still says “Chưa có hình / chưa có thoại” on the TEST stub desk.
4. Old modals / stacked Ant confirms.
5. Styling.
6. SH01 lip-sync.
7. Real providers / provider selection / spend.
8. Acting / Timing.
9. FINAL / FILM READY.
10. Admin-wide `tsc --noEmit` debt.

---

## 13. Success criteria

| # | Criterion | Result |
|---|---|---|
| 1 | ASSEMBLE no longer FilmAssembly 400 | PASS — HTTP 200 |
| 2 | ASSEMBLE receives accepted artifactIds | PASS — `[0] == MIX 638bd185` |
| 3 | FilmAssembly row created | PASS — `0ab1d54e-…` |
| 4 | Browser CTA path reaches Production OS | PASS |
| 5 | No OS CTA calls legacy execution endpoints | PASS — no `/series/assemble` |
| 6 | Preview VISIBLE → artifact → physical media | PASS |
| 7 | Reload preserves OS state | PASS |
| 8 | Cross-shot intact | PASS — `CROSS_SHOT_FORBIDDEN` |
| 9 | EP01 / EP99 / Canon / SH01 unchanged | PASS |
| 10 | Zero real provider calls | PASS |
| 11 | Zero paid spend | PASS |

---

## 14. Recommended next (not started)

**STATE SEMANTIC FOLLOW-UP** only if operators must read CURRENT as “what is on screen.” Do not merge VISIBLE / CURRENT / ACCEPTED.

Then, only if asked: real-provider UI bridge. No spend in that memo.

STOP. No Runway. No Veo. No Gemini. No Fal. No ElevenLabs. No Acting Contract. No FINAL. No FILM READY. No Provider Benchmark.
