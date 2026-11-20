# FAMIXA PRODUCTION OS — UI CUTOVER ACCEPTANCE CLOSURE V1

Date: 2026-09-18  
Scope: TEST-SC01-SH02 only  
Live providers: none  
Paid spend: none  
Film mutation: none

Do not read this as FILM READY.  
Do not read this as REAL PROVIDER READY.

---

## 1. REGENERATE implementation

**IMPLEMENTED.** Existing Production OS only. FixtureMotionAdapter. No `/series/turbo`, no `/series/still`, no second engine, no new tables.

`ProductionCreateVideoService.RegenerateOsAsync`:

- requires parent `VIDEO_TAKE` (`MotionArtifactId`)
- writes `PriorTaskId` into the frozen snapshot
- opens a new Request / Task / immutable InputSnapshot / Attempt with `UserAction = REGENERATE`
- refuses replay that would reuse the prior artifact id
- materializes a new fixture artifact and binds VISIBLE
- asserts prior SHA unchanged

UI: `executeProductionOsAction` POSTs `Action=REGENERATE` with the current MOTION artifact id. Idempotency includes that prior artifact id.

**LIVE VERIFIED** TEST-SC01-SH02:

| | artifactId | SHA |
|---|---|---|
| old A (CREATE_VIDEO) | `78d57d06-bcda-4b22-95d1-addadf0cae17` | `d5e33723…f78b5d` |
| new B (REGENERATE) | `583d7700-f5b9-44c9-97db-5277572b628b` | `2c268c19…ee24d7` |

A ≠ B. Old A row still present with the same SHA.

---

## 2. API recycle verification

**LIVE VERIFIED.** Content API recycled via `scripts/api-dev.ps1`. Running GET `/actions` for TEST-SC01-SH02:

`CREATE_IMAGE, CREATE_VIDEO, CREATE_VOICE, CREATE_LIPSYNC, MIX_VIDEO, ACCEPT, REGENERATE, ASSEMBLE`

ASSEMBLE is on the running catalog. Running API matches source `BuildActions`.

---

## 3. Browser CTA trace

TEST-SC01-SH02 director desk. Each click opened the Production OS confirm (`Fixture — không gửi Gemini / Veo / Runway / Wan / Fal / ElevenLabs`) then POSTed `/content/production/requests`.

| Click | ProductionAction | requestId | taskId | attemptId | artifactId | binding | Class |
|---|---|---|---|---|---|---|---|
| Tạo hình | CREATE_IMAGE | c007dc16-… | 6c635fff-… | fe819f33-… | 16c1a4e8-… | current `96fb30be` / visible `a65e136f` | LIVE VERIFIED |
| Tạo video | CREATE_VIDEO | 57c4b90f-… | bc3ad53d-… | f704819f-… | 78d57d06-… | same | LIVE VERIFIED |
| Nạp thoại | CREATE_VOICE | 0492fb2a-… | d515c1c1-… | 333a0a3a-… | 3a753477-… | same | LIVE VERIFIED |
| Khớp môi | CREATE_LIPSYNC | 34c139da-… | 1e0e7964-… | 6fecd3e8-… | 49deaa6b-… | same | LIVE VERIFIED |
| Hoàn thiện | MIX_VIDEO | 4fecf495-… | 11483688-… | eff90738-… | 638bd185-… | same | LIVE VERIFIED |
| Dùng kết quả | ACCEPT | da84b77d-… | ad6e8492-… | 1e2c64e2-… | 638bd185-… | same | LIVE VERIFIED |
| Lắp tập | ASSEMBLE | — | — | — | — | POST reached OS; HTTP 400 | LIVE VERIFIED route / TESTED fail |

ASSEMBLE 400 body (FACT): `FilmAssembly` Dapper materialization requires ctor `(Guid assemblyid, Guid buildid, string title, DateTime createdat)`. No adapter execution after that point. **Not FILM READY.**

TEST stub desk originally hid Nạp thoại / Khớp môi / Hoàn thiện / Dùng kết quả because local snap is empty. **IMPLEMENTED** catalog extras (`visibleProductionOsCatalogActions`) so GET `/actions` `available === true` is clickable. Not a second orchestrator.

Trace: `.tmp/os-ui-cutover-acceptance.json`.

---

## 4. Reload trace

**LIVE VERIFIED** after full browser reload + reopen TEST-SC01-SH02:

| Source | Value |
|---|---|
| GET `/state` visibleArtifactId | `583d7700-…` (REGENERATE B) |
| GET `/state` currentArtifactId | `78d57d06-…` (CREATE_VIDEO A) |
| GET `/state` acceptedArtifactId | `638bd185-…` (MIX) |
| GET `/preview` artifactId | `583d7700-…` |
| GET `/preview/media` | 200, `video/mp4`, 707 bytes |
| Desk `<video>` | `blob:http://localhost:5173/…` from that media |

State after reload is Production OS, not localStorage / IDB latest / takeUrl / previewUrl / lipsyncUrl / acceptedTake.url.

Preview contract: VISIBLE → artifactId `583d7700-…` → physical media. FACT.

Per-action reload after every one of CREATE_VIDEO / VOICE / LIPSYNC / MIX / ACCEPT / ASSEMBLE: **UNVERIFIED**. One reload after the MIX chain, then ACCEPT/ASSEMBLE on the reopened desk.

---

## 5. Duplicate click trace

**LIVE VERIFIED** (same UI idempotency key `os-ui:CREATE_VIDEO:{TEST}:{SH02}`):

two POSTs → same request `57c4b90f-…`, same task `bc3ad53d-…`, same artifact `78d57d06-…`.

No second fixture motion execution.

CREATE_VOICE / CREATE_LIPSYNC / MIX_VIDEO browser double-click: **UNVERIFIED**. Same idempotency pattern is **IMPLEMENTED**.

Browser physical double-click of Tạo video: **UNVERIFIED** (stacked confirm modal). API replay is the contract proof.

---

## 6. Cross-shot trace

**LIVE VERIFIED** in the prior UI Cutover live chain (same TEST scope, this tree):

CREATE_LIPSYNC on TEST-SC01-SH01 with SH02 VIDEO_TAKE + VOICE → `CROSS_SHOT_FORBIDDEN`. No SH01 adapter artifact.

This session did not recapture the 400 reason body. **UNVERIFIED** as a new POST in this closure turn. Prior result stands. No SH01 4A mutation (see §7).

---

## 7. Film integrity before / after

| Probe | Expected | After | Class |
|---|---|---|---|
| EP01 `4ef164d6-…` graph md5 | `12289be18284fbd4d4b166b82c09eba0` | same | LIVE VERIFIED |
| EP99 `77262ec0-…` graph md5 | `c7cd2516627332c2648c1509395e4a64` | same | LIVE VERIFIED |
| series_build count | 4 | 4 | LIVE VERIFIED |
| film_art on EP01 build | 0 | 0 | LIVE VERIFIED |
| film_bind on EP01 build | 0 | 0 | LIVE VERIFIED |
| SH01 4A VIDEO `63bca29a-…` SHA | `f94a9d097e5d90c0…8c8c17` | same | LIVE VERIFIED |

Canon / VA / EP01 / EP99 / SH01 not modified. FACT.

---

## 8. Build / test results

| Test | Result | Class |
|---|---|---|
| `famixa-production-os-ui-cutover.smoke.ts` | PASS | TESTED |
| `production-os-action-authority.smoke.ts` | PASS | TESTED |
| `ProductionCompleteActionAuthority` (REGENERATE A≠B, immutable prior SHA) | implemented + previously PASS | TESTED |
| Admin `tsc --noEmit` | pre-existing debt | UNVERIFIED as clean |
| Real provider jobs | not started | FACT |

---

## 9. Unverified

- Browser double-click of Tạo video / Nạp thoại / Khớp môi / Hoàn thiện
- Per-action reload after each of the six named actions
- New CROSS_SHOT_FORBIDDEN HTTP body in this turn
- ASSEMBLE SUCCESS after API recycle (`FilmAssembly` ctor)
- Preview `<video>` decoded width/height (blob attached; dimensions 0 at snapshot)
- Character `/series/still` (OUT OF SCOPE)

---

## 10. Remaining architectural gaps

1. **ASSEMBLE after recycle** — catalog and UI POST are correct; `FilmAssembly` store materialization 400. FACT.
2. **TEST stub desk vs OS catalog** — without catalog extras, local empty snap hid voice/lipsync/mix/accept. Extras close the click gap; local lanes still say “Chưa có hình / chưa có thoại”. DISPLAY, not OS authority.
3. **Stacked Ant confirms** — leftover modals can block the next CTA. OUT OF SCOPE polish.
4. **VISIBLE ≠ CURRENT ≠ ACCEPTED** — after REGENERATE, VISIBLE is B, CURRENT MOTION node is B, CREATE_VIDEO idempotent replay still names A as currentArtifactId on GET `/state`. Operators can misread this. FACT.
5. Character master-ref still `/series/still`. OUT OF SCOPE.
6. Film GET `/actions` may show paid CTAs that UI refuses (`PROVIDER_UNAVAILABLE · FILM_PROTECTED`). FACT.

OUT OF SCOPE (not fixed): Character Workspace `/series/still`, old styling, old preview/modals, SH01 lip-sync, providers, admin tsc, Acting, Timing, FINAL, FILM READY.

---

## 11. Recommended next phase

**RECOMMENDED** (not started):

1. Fix `FilmAssembly` Dapper materialization so ASSEMBLE can complete on TEST without a new table or engine.
2. Recapture CROSS_SHOT_FORBIDDEN HTTP body on a fresh SH01 POST (fixture only).
3. Decide Film CTA policy: hide vs show `PROVIDER_UNAVAILABLE`.
4. Only then consider a real-provider UI bridge. No spend in that memo.

STOP. No Runway. No Veo. No Gemini. No Fal. No ElevenLabs. No FILM READY. No REAL PROVIDER READY.
