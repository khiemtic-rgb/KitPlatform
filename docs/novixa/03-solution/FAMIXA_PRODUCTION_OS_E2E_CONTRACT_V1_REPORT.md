# FAMIXA PRODUCTION OS — ROOT CAUSE, END-TO-END CONTRACT V1

Date: 2026-10-17. Local repository and local tests only. No provider call, no Production deploy, no migration, no mutation of tenant rows, no new media.

A second pass the same day verified the existing SH02 mix bytes and closed the remaining verification gaps. That evidence is in §12. The root-cause matrix below is unchanged.

## Verdict

**PASS WITH GAPS** for the structural contract. **BLOCKED** for production readiness of EP01.

The existing Production OS can now keep picture duration, dialogue intervals, lip-sync length, audio length, and the mix timeline as separate clocks. A future request for an EP01 shot takes acting from the approved shot contract, not from a match against the historical truncated phrase. Playback prefers the current mix over an accepted video take, and a review does not stick to a file after its id or hash changes.

That does not make any of the six shots visually accepted. The episode is not production-ready. Controlled media validation is not the next automatic step.

## 1. Root-cause matrix

| Id | Class | What failed | Evidence | Disposition |
|---|---|---|---|---|
| RC1 | Confirmed root cause | Future acting was chosen by matching a historical fragment (`đưa hai  `, `Linh entering the room`, `sit on the sofa edge`). A stale sentence that did not contain the fragment was sent on unchanged. | `FutureActingForDesk` / `futureDeskActing` before this pass. Server `PrepareFutureVideo` rewrote SH02 only for the exact historical string and otherwise kept free text. | Fixed. EP01 future requests use `Ep01Contract` by shot code. The exact frozen SH02 story+acting pair stays historical so an old replay still matches its snapshot. |
| RC2 | Confirmed root cause | Speech length, picture length, and lip-sync length were one comparison. The desk passed speech duration in the lip-sync argument (`picture > 0 ? speech : 0`). Any short line was also reported as a split mix. A scene whose dialogue occupies only part of a longer picture could not pass without weakening the 0.75s tolerance. | `SpeechWindowDoesNotCoverPlayback` compared speech to the whole playback. `FutureClockBlockers` treated lip-sync shorter than the picture as `MIX_CLOCK_SPLIT`. Series desk `ContentFamixaSeriesTab` around the CREATE_LIPSYNC / MIX gate. | Fixed. `EvaluateClocks` / `productionClockBlockers`. Tolerance stays 0.75s. An undeclared window still requires speech to cover the picture, so the recorded SH02 file still fails. A declared window is checked against that window, the mix spans, and mouth evidence. |
| RC3 | Confirmed root cause | Preview order returned `ACCEPTED:VIDEO_TAKE` before `CURRENT:MIX`. A mix binding whose artifact was actually a video take could fall through and play the take. Replay of a succeeded CREATE_VIDEO took the first verified artifact of any role. | `OfficialVideoAuthorityBridgeV1.ResolveAsync`. `ReplayOrResumeCreateVideoAsync` used `FirstOrDefault` on verified artifacts. | Fixed. Playback order is verified `CURRENT:MIX`, then `CURRENT:LIPSYNC`, then `ACCEPTED:VIDEO_TAKE`, then assembly, then visible, then current video take. A role mismatch fail-closes. Replay requires exactly one verified `VIDEO_TAKE` on that attempt. |
| RC4 | Confirmed root cause | Review identity was a graph URL or a file name. The preview response already carried `artifactId` and `byteSha256`, but the desk stored only a blob URL. A later file could keep an older ACCEPT. | `playbackReviewTarget` preferred assembly file, then lipsync URL, then take URL. `qualityReviewAccepted` compared that string and did not read a hash. | Fixed when an OS preview identity is present. ACCEPT stamps `os:{artifactId}` and the hash. A different id or hash does not pass. Bytes that do not match the named hash are not shown. |
| RC5 | Confirmed contributing cause | `KitVideoVideoStore.TryReadNamed` always looked for `{stem-with-dots-removed}.mp4`. A tenant-owned key whose last segment already ended in `.mp4` looked up a different file name. Tenant ownership is still decided before that read. | `ProductionArtifactFileStore.Read` refuses a key the caller tenant does not own. `TryReadNamed` then appended `.mp4` after stripping the dot. | Fixed. Candidates keep a known media extension, and still accept the historical bare-id `.mp4`. The tenant prefix check is unchanged. |
| RC6 | Symptom, not a separate cause | SH02 mix `58297402-e252-45e8-8d33-c7414749a297` is 8s. Speech is 2.534s. Lip-sync is 2.5s. Recorded frames show an open mouth on the silent tail and no red score. | `FAMIXA_SHORT_PRODUCTION_READINESS_001.md` and `ObserveEp01`. | Still blocked. The clock rules now name speech coverage, the split mix, and mouth-during-silence separately. No new pixels were judged. |
| RC7 | Symptom | SH01 historical acting says Linh enters. The locked story says Minh enters. | Ingress canon still contains `Linh entering`. The next request uses the Minh-enters contract. | Historical text unchanged. Future payload corrected. The existing mix is not accepted. |
| RC8 | Unknown, left blocked | SH03 spoken line ends at `Điểm thấp thì lần sau gỡ...`. The missing words are not in the recorded script. | `Sh03RecordedLine`, `LineComplete == false`. | Voice draft for SH03 stays refused. The line was not invented. |
| RC9 | Unknown, left blocked | SH06 pixels. The legacy URL returned HTTP 401 in the earlier review. | `ObserveEp01` observation `NOT_FETCHED`. | Not fetched again. Not accepted. |
| RC10 | Not confirmed | Word-level match of the audio to the line. Provider quota. Whether the corrected SH02 sentence will produce the red-score action. | No ASR. No account balance. No provider call. | Not claimed. |

## 2. End-to-end contract

| Stage | Authoritative input | Output | Invalidated when |
|---|---|---|---|
| CREATE_VIDEO | Approved shot contract for an EP01 future request: acting, continuity, props. Exact SH02 historical story+acting is a replay of the frozen job, not a new spec. Provider and model stay the frozen selection. | Frozen story, acting, visual contract. Veo prompt is those three fields joined. Spoken line is not painted into the video prompt. | A different acting text for SH02 is a new request. It does not rewrite the historical snapshot. |
| CREATE_VOICE | Locked spoken line after the stage cue is stripped. | One line, or a refusal. | SH03 stays refused while the locked line is truncated. A different sentence does not replace the locked line. |
| CREATE_LIPSYNC | Current video take and current voice, plus the clocks. | Not started when the undeclared-window clock already fails, or when the voice draft is refused. | This pass does not add a hidden retry or a provider call. |
| MIX | Picture duration, lip-sync duration, and, when the contract declares them, dialogue intervals. | A mix whose speech spans cover each declared interval. Silence outside those intervals must not carry mouth movement. | Lip-sync shorter than a declared dialogue window by more than 0.75s is `MIX_CLOCK_SPLIT`. Lip-sync shorter than the picture is not itself a failure when the declared window is covered. |
| Playback | Verified `CURRENT:MIX`, else verified `CURRENT:LIPSYNC`, else `ACCEPTED:VIDEO_TAKE`, else assembly, else visible, else current video take. | That artifact id, its storage key, and its hash. Bytes are returned only when the hash matches. | A `CURRENT:MIX` binding that points at a video take returns empty. It does not play the take. |
| Review | The playback artifact id and the hash of the bytes that were shown. | ACCEPT only when the ticks, the id, and the hash all name that file. | A new id or a new hash drops the previous ACCEPT. |
| ASSEMBLE_EPISODE | Six production-accepted playbacks, in order, plus a separate ACCEPT on the assembly artifact. | Not produced for EP01 in this pass. | Concat permission is not episode acceptance. |

Technical validity, automated clock and prop checks, and human visual acceptance stay separate. A verified hash is not a visual accept. Identity `ACCEPTED:*` is not a quality review. Provider success is not creative correctness.

## 3. Files changed

| File | Why |
|---|---|
| `ShortProductionQualityPipelineV1.cs` | Approved contract by shot code. Separate clocks. Review hash. Provider prompt projection. |
| `ShortProductionQualityPipelineV1Regression.cs` | Regression for stale acting, partial dialogue, two intervals, truncated audio, mouth during silence, hash rebinding, file-name candidates, attempt selection. |
| `OfficialVideoAuthorityBridgeV1.cs` | Playback precedence. Fail closed on a role mismatch. |
| `ProductionOfficialVideoAuthorityBridgeV1Regression.cs` | Mix beats an accepted video take. A mismatched mix binding does not play the take. Playback hash checked. |
| `ProductionCreateVideoService.cs` | Succeeded replay and materialize select one verified `VIDEO_TAKE` for the attempt. |
| `ProductionPipelineIdentityV1.cs` | Attempt selection and storage-key file names. |
| `KitVideoVideoStore.cs` | Read a tenant folder key that already includes a media extension, without dropping the tenant check. |
| `famixa-short-quality-pipeline.ts` | Same acting and clock rules on the desk. |
| `famixa-short-quality-pipeline.smoke.ts` | Desk regressions for stale text and the three SH02 clocks. |
| `famixa-short-acceptance.ts` | OS playback target is artifact id + hash. Byte check before a blob is treated as that artifact. |
| `famixa-short-acceptance.smoke.ts` | Hash rebinding and a known SHA-256 vector. |
| `content-famixa-series.ts` | `reviewedContentHash` on the desk review record. |
| `ShotProductionWorkspace.tsx` | ACCEPT stamps the OS artifact id and hash when the preview identity is present. |
| `ContentFamixaSeriesTab.tsx` | Stops passing speech duration as the lip-sync duration. Refuses a preview whose bytes do not match the named hash. Does not copy a mix id onto the video-take record. |

## 4. Tests

Passed:

- `dotnet test tests/KitPlatform.Content.Tests --filter FullyQualifiedName~ShortProductionQualityPipelineV1Tests|FullyQualifiedName~ShortProductionAcceptanceV1Tests|FullyQualifiedName~ProductionOfficialVideoAuthorityBridgeV1|FullyQualifiedName~ProductionEpisodeAssemblyV1|FullyQualifiedName~OfficialPreviewIntegrityV1|FullyQualifiedName~ProductionCreateVideoFreezeIntegrityV1Tests|FullyQualifiedName~ProductionOfficialCreateVideoFreezeV1Tests` — 7 passed.
- A wider filter covering create-video, official, preview, mix, full-film, tenant EP01, and episode names — 26 passed.
- `famixa-short-quality-pipeline.smoke.ts` — PASS.
- `famixa-short-acceptance.smoke.ts` — PASS, including SHA-256 of `abc`.
- `famixa-shot-production-orchestration.smoke.ts` — PASS.
- `famixa-official-preview-os-sha.smoke.ts` — PASS.
- `famixa-shot-state-preview-authority.smoke.ts` — PASS.
- `famixa-explicit-mix-action.smoke.ts` — PASS.
- `famixa-final-artifact-identity-binding.smoke.ts` — PASS.
- `famixa-final-artifact-readiness-cta.smoke.ts` — PASS.

Could not complete:

- `ProductionOfficialSh02CreateMixV1LiveTests` (2) and `ProductionOfficialSh02AssembleV1LiveTests` (2). Re-run in the closure pass: each still throws `UnauthorizedAccessException: PRODUCTION_TENANT_REQUIRED` from `ProductionMissingCallerTenant.RequireTenant` inside `GetArtifactAsync`, before SQL, mix, or assemble. The precise reason they cannot be supplied a tenant safely is in §12.
- No browser session was open in either pass. Ports 5290, 7224, 5173, and 5178 were closed during the closure pass. The desk was not clicked.
- The closure pass hashed the existing SH02 mix file and did not open SH01 or SH03–SH06. No provider job was started.

Cost this pass: $0 provider.

## 5. Creative contract to provider payload

`PrepareFutureVideo("EP01-SC01-SH02", historical story, "Minh đứng yên và nhìn tờ giấy trắng.", "")` returns the approved SH02 acting. That sentence does not contain `đưa hai`.

`VideoPromptOf` of that result contains the approved acting, `bag`, and `red score`. It does not contain `(lý nhí)` and it does not contain the truncated `đưa hai  `. The prompt is `VeoMotionPrompt.FromFrozen`, which is the text the Veo adapter sends. The spoken line stays on the voice draft.

The exact historical SH02 story and acting still return `HISTORICAL_INGRESS`, so a replay of that frozen job is not rewritten.

SH03 voice remains blocked. The recorded line is still `Điểm thấp thì lần sau gỡ...`.

## 6. Playback identity and review

In the bridge regression, a verified `CURRENT:MIX` is the preview while `ACCEPTED:VIDEO_TAKE` remains the authoritative video take. The bytes returned for that preview hash to the mix hash. A `CURRENT:MIX` binding that points at the video take yields an empty preview.

`ReviewBindsPlayback` is true only when artifact id and hash both match. Classify of an ACCEPT whose playback hash differs from the reviewed hash is not production-ready (`REVIEW_HASH_MISMATCH`).

On the desk, `qualityReviewAccepted` for `os:mix-1` / `abc` fails for hash `def` and for artifact `mix-2`.

## 7. Timing

Tolerance remains 0.75s.

| Case | Result |
|---|---|
| No declared interval, audio 2.534s, picture 8s, lip-sync 2.5s | `SPEECH_DOES_NOT_COVER_PLAYBACK` and `MIX_CLOCK_SPLIT` |
| Declared interval 0–2.5s inside an 8s picture, audio 2.5s, lip-sync 2.5s, mix speech on 0–2.5s and silence after, mouth quiet | No clock blocker |
| Two intervals, 0–1s and 4–5s, covered by mix speech spans | No clock blocker |
| Same partial window, mouth movement flagged during the silent span | `MOUTH_DURING_INTENDED_SILENCE` only |
| Audio 1s against a 2.5s declared window | `SPOKEN_LINE_INCOMPLETE` |
| Mix speech on 0–2.5s while the declared window is 3–5.5s | `DIALOGUE_WINDOW_MISMATCH` |

The recorded SH02 observation still includes mouth movement on the silent interval. That artifact is not waived.

## 8. Regression protection

Covered by the tests in section 4: stale acting without the truncated phrase, contract text inside the Veo prompt, partial dialogue, multiple intervals, truncated audio, mouth during silence, wrong mix placement, hash rebinding, mix-versus-take playback, mismatched mix binding, and one-verified-take selection.

## 9. SH01–SH06

No file was opened in this pass. "Seen earlier" means the 2026-10-17 frame notes already on record. Those notes are not a new visual acceptance.

| Shot | Technical | Visual evidence | Acceptance | Production-ready |
|---|---|---|---|---|
| EP01-SC01-SH01 | PASS WITH GAPS. OS mix `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` is the named playback. Task succeeded and the earlier report marked it verified. Blockers remain: historical acting contradicts the story; mouth clock fails. | Seen earlier. Entrance is not performed. Linh is duplicated. Not re-opened here. | Not accepted. | No |
| EP01-SC01-SH02 | PASS WITH GAPS. OS mix `58297402-e252-45e8-8d33-c7414749a297`. Bytes re-hashed in §12; the file was not regenerated. Blockers: speech does not cover the 8s picture because no partial dialogue window was approved for this file; lip-sync 2.5s against that picture is a split; mouth moves on the silent interval; red score is not in the observed props. | Seen earlier. No red score. Open mouth on the silent interval. Not re-opened in either pass. | Not accepted. No quality decision was written. | No |
| EP01-SC01-SH03 | BLOCKED. Not in Production OS. Spoken line is incomplete. | Seen earlier. Held pose. Blank paper. | Not accepted. A legacy file is not an OS accept. | No |
| EP01-SC01-SH04 | BLOCKED. Not in Production OS. Stored action was the broken prefix. The next request completes that sentence. The existing file is unchanged. | Seen earlier. Held pose. Blank card. | Not accepted. | No |
| EP01-SC01-SH05 | BLOCKED. Not in Production OS. Spoken line was not bound on the legacy file. The next request uses the sit-down contract. | Seen earlier. Already seated. | Not accepted. | No |
| EP01-SC01-SH06 | BLOCKED. Not in Production OS. No playback ref. | Not fetched. HTTP 401 on the earlier attempt. Pixels were not judged. | Not accepted. | No |

## 10. Episode assembly

**BLOCKED.**

`ObserveEp01Episode` is not production-ready. Provenance still lists SH01–SH06. SH06 has no artifact id. No shot is production-accepted, so `FullEpisodeConcatPermitted` is false. The existing per-shot technical assemblies were not treated as the episode. Concat of a verified file is not an episode ACCEPT. No assembly bytes were produced in this pass.

## 11. Remaining blockers

| Blocker | Cause | Minimum next action |
|---|---|---|
| SH02 picture | The current mix misses the red-score action, and the mouth is on the silent interval. No approved dialogue window exists for that 8s file, so the short line cannot be excused. | A person watches the file named in §12 and records their own decision on that artifact id and hash. This pass did not record REJECT or ACCEPT. |
| Next SH02 generation | The approved acting reaches a future request. Veo 4s, 6s, and 8s each fail the undeclared-window clock against 2.534s speech. No ISSUED `CREATE_VIDEO` permit exists. | Do not submit. After a human decision on the current file, a later explicit approval still has to choose a picture clock the model can hold before any provider request. |
| SH03 line | The locked line is truncated in the source. | A person supplies the rest of the line. Do not invent it. |
| SH01, SH04, SH05 pictures | Historical or legacy files do not perform the approved action. | Do not regenerate them in the SH02 pilot. |
| SH06 | Pixels were not retrieved. | Fetch only after the URL is authorized. Do not accept it unseen. |
| Live mix/assemble tests | The four tests construct the repository with no caller, so they throw `PRODUCTION_TENANT_REQUIRED` before SQL. The SH02 build, mix row, and active bindings have `tenant_id` NULL, and the storage key is not tenant-prefixed. | Leave the tenant check in place. Do not backfill `tenant_id` and do not inject a caller to force these tests. The required setup is in §12. |
| Episode | No accepted shot and no reviewed six-shot assembly. | Assemble only after each shot's production accept names the current playback id and hash. |

## Conclusion

The pipeline is structurally reliable enough to **prepare** a controlled media validation: the next request carries the approved contract, the clocks no longer collapse into one number, and playback and review can be tied to one artifact id and hash.

It is **not** reliable enough to call EP01 production-ready, and the validation itself is still blocked. No shot has a human visual accept on the file that would play. The episode assembly does not exist as an accepted artifact.

The next decision is a person's own quality record for the file verified in §12. The record names artifact `58297402-e252-45e8-8d33-c7414749a297` and SHA-256 `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`. This pass did not write ACCEPT or REJECT, and it did not submit a provider request.

## 12. SH02 blocker closure

Local read-only checks on 2026-10-17. The mix file was not rewritten. No provider call, no permit issue, no `tenant_id` update, no migration, no Production change.

### 12.1 Artifact identity

| Field | Value |
|---|---|
| Artifact ID | `58297402-e252-45e8-8d33-c7414749a297` |
| Role | `MIX` |
| Verification | `VERIFIED` |
| Storage key | `os/ff28d642765942839894f89bfbeb897c` |
| File | `src/KitPlatform.Api/App_Data/kit-video-i2v/os/ff28d642765942839894f89bfbeb897c.mp4` |
| Size | `1423715` bytes, matching `production_artifacts.size_bytes` |
| Duration column | `8` |
| SHA-256 of the file bytes | `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b` |
| Row `byte_sha256` | the same hash |
| `tenant_id` | NULL |
| Attempt | `ff28d642-7659-4283-9894-f89bfbeb897c` |
| Task | `549a3685-1eb5-4381-970b-b87aec953d2a` |

Active bindings on build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / `EP01-SC01-SH02`, all with `tenant_id` NULL:

- `CURRENT:MIX`, `ACCEPTED:MIX`, `VISIBLE:MIX`, and generic `ACCEPTED` point at this mix.
- `ACCEPTED:VIDEO_TAKE` remains `b44e530b-5b59-422f-9b80-e058a3a639b5`.
- `CURRENT:LIPSYNC` remains `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e`.
- Generic `CURRENT` is the keyframe `91daaf4b-5550-4cbc-b9f9-0bd0be556cef`. Generic `VISIBLE` is assembly `92621f9e-57eb-4185-8cea-a962ec399136`.

`ACCEPTED:MIX` is the earlier compose binding. It is not a quality ACCEPT of the picture.

The series build row also has `tenant_id` NULL. The read was `SET default_transaction_read_only = on`.

### 12.2 Preview path

The playback order would choose verified `CURRENT:MIX` ahead of the accepted video take. The local preview API does not reach that row.

`GET /api/content/production/shots/{buildId}/{shotCode}/preview` requires an authenticated ADMIN tenant (`ProductionTenantActionFilter` calls `IProductionCallerTenant.RequireTenant`). `GetArtifactAsync` and `GetActiveBindingAsync` then keep only `tenant_id = caller`. A NULL `tenant_id` matches no caller, so the mix is invisible and the preview is empty. `UnattributedVideoTakeSource` reads a NULL-tenant `VIDEO_TAKE` only. It does not read this MIX.

`ProductionArtifactFileStore.Read` also returns null for storage key `os/ff28d642765942839894f89bfbeb897c`. `ProductionTenantBoundary.OwnsStorageKey` is false for that key. The bytes that hash to the row live at the path above; the tenant file store does not serve them.

Nothing was listening on `localhost:5290`, `7224`, `5173`, or `5178`. No admin session was opened, no credential was created, and no click-through was performed.

### 12.3 Human review of this file

A person watches the file in §12.1. If they record a quality decision, the record is:

- `reviewedPlaybackId`: `os:58297402-e252-45e8-8d33-c7414749a297`
- `reviewedContentHash`: `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`
- `decision`: `ACCEPT` or `REJECT`, written by that person

This pass wrote neither decision and did not call `AcceptOs`.

The shot checkbox card is not a safe way to review this file. When every tick is set, `ShotProductionWorkspace` stamps `decision: ACCEPT` against the OS preview identity that is loaded. That preview does not currently resolve to this artifact and hash, and the card has no REJECT control. The person records the three fields above themselves.

### 12.4 Live tests

Re-run this pass, no caller injected:

| Test | Result |
|---|---|
| `Live_official_sh02_mix_compose_segments_once` | `UnauthorizedAccessException: PRODUCTION_TENANT_REQUIRED` |
| `Live_official_sh02_mix_replay_idempotency_does_not_compose` | same |
| `Live_official_sh02_assemble_remux_once` | same |
| `Live_official_sh02_assemble_replay_does_not_remux` | same |

The throw is `ProductionMissingCallerTenant.RequireTenant` at the start of `GetArtifactAsync`, before a SQL round-trip, compose, remux, or accept. `ShortProductionQualityPipelineV1Tests` passed in the same run (1 passed, 4 failed).

The repository-approved caller is `HttpProductionCallerTenant`: the authenticated `ITenantContext`, not a build id or a storage key. Other live probes resolve a real `public.tenants.id` and pass an `IProductionCallerTenant` whose `RequireTenant` still goes through `ProductionTenantBoundary.Require`. Every SH02 read still filters `tenant_id = caller`. These four tests do not take that caller. They construct `ProductionWorkRepository` and `ProductionArtifactFileStore` with null, which is `ProductionMissingCallerTenant`.

Supplying any real tenant would still miss these rows, because the build, the mix, and the bindings are unattributed. `RequireBuildAsync` then fails with `PRODUCTION_TENANT_UNATTRIBUTED`. Attributing the rows would change tenant data. The mix and assemble entry points also call `MixVideoAsync`, `AcceptOsAsync`, and `AssembleOsAsync` when their early replay path is not taken, so they were not given a caller.

Required setup before those four tests can run: the series build and the SH02 artifact and binding rows already share one non-null `tenant_id` with the authenticated caller, and the storage key is under that tenant prefix. Do not backfill `tenant_id` and do not remove the tenant check to produce a pass.

### 12.5 Next-request gate and timing

No provider request was submitted. Quota was not read. No permit was issued.

`ShortProductionQualityPipelineV1Tests` passed, including the stale-acting and prompt checks. A direct call of the same methods this pass:

- `PrepareFutureVideo` of the historical SH02 story plus `Minh đứng yên và nhìn tờ giấy trắng.` returns `FUTURE_SPEC_V1` and the approved acting. That acting contains `red score` and does not contain `đưa hai`.
- `VideoPromptOf` of that result contains `red score` and does not contain `(lý nhí)` or the truncated fragment `đưa hai  `. The locked story still contains `đưa hai tay cho mẹ`, so a search for `đưa hai` hits the story sentence.
- The exact historical story and acting still return `HISTORICAL_INGRESS`.
- `FutureClockBlockers(true, 2.534, picture, 0)` returns `SPEECH_DOES_NOT_COVER_PLAYBACK` for picture `4`, `6`, and `8`.
- A declared `0–2.534` window inside a 4s picture, with matching speech, lip-sync, mix spans, and a quiet mouth, returns no clock blocker. No such window is approved for the current file or for a future request.

Veo durations used by the desk are 4, 6, and 8 (`veoMappedDurationSec`). The active VIDEO selection is still `google` / `veo-3.1` (`c52b646f-86b9-4a43-834d-d94ab9f013b8`). The only `CREATE_VIDEO` permit for this shot is `CONSUMED` (`97240e2f-e603-495b-863a-ebc9ef27b0a8`). There is no `ISSUED` permit.

The desk `mayMutateProductionOs` is false for build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, so `executeProductionOsAction` stops with `FILM_PROTECTED` before a production request. The server `CreateVideoAsync` requires an issued official permit and throws `OFFICIAL_FILM_FORBIDDEN` when that permit is absent, before a provider start. `ApplyFutureVideoSpec` is on the later part of that method. The known SH02 lip-sync path still returns on `productionClockBlockers` before `createProductionRequest`.

`famixa-short-quality-pipeline.smoke.ts` passed again (`npx tsx`, FAIL=0, no provider).

### 12.6 Exact next decision

A person watches `os/ff28d642765942839894f89bfbeb897c.mp4` and writes their own `ACCEPT` or `REJECT` on artifact `58297402-e252-45e8-8d33-c7414749a297` with hash `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`.

Do not submit a provider request in that step. A later generation still needs a separate explicit approval, an issued `google` / `veo-3.1` permit, and a picture clock that passes. With no approved dialogue window, Veo 4, 6, and 8 all fail against the 2.534s line, so the request stops before submit. SH02 is not production-ready. EP01 is not production-ready.
