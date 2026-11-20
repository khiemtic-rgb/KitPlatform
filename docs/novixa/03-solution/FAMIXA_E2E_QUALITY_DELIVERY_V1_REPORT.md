# FAMIXA END-TO-END QUALITY DELIVERY V1

Date: 2026-10-17. Local only. No provider call, no Production deploy, no rewrite of historical snapshots or existing media.

## Verdict

The production pipeline is implemented on the existing Production OS and `ShortProductionQualityPipelineV1`.

No shot has passed visual acceptance. The full EP01 episode is not production-ready.

## What was implemented

One contract, extended in place. No second acceptance engine.

| Capability | Where | Behavior |
|---|---|---|
| Future contracts for all six shots | `ShortProductionQualityPipelineV1.Ep01ContractRows` and `famixa-short-quality-pipeline.ts` | Next request carries acting, spoken line, required props, and continuity. Historical SH01/SH02 ingress text is unchanged. |
| Propagation | Desk `futureDeskActing` / `futureDeskStory`, server `PrepareFuturePackage`, and `PrepareFutureVideo` | SH01 names Minh as the entrant. SH02 completes the cut-off action and names the red-score paper. SH03/SH04 close the broken parenthesis. SH05 does not start already seated. SH06 pats the cushion. A server replay of the historical SH02 acting string stays historical. |
| Clock | `FutureClockBlockers`, desk `futureClockBlocker` before `CREATE_LIPSYNC` and `MIX_VIDEO` | Speech must cover playback within 0.75s. A lipsync window shorter than the picture by more than 0.75s is `MIX_CLOCK_SPLIT`. |
| Incomplete output | `Classify` | A missing required prop, a truncated spoken line, a stage direction, or a split clock blocks acceptance. Provider success and a verified hash do not. |
| Legacy path | `Ep01Workflow`, `BindLegacyCandidate` | SH03–SH06 are executable as `LEGACY_CANDIDATE` with `BIND_EXISTING_MEDIA`. An accept tick on a legacy file is not production acceptance. SH06 has no playback ref. |
| Episode | `PlanEpisode`, `FullEpisodeConcatPermitted`, `AssembleEpisodeOsAsync` | Ordered provenance for SH01–SH06. Concat of those six shots throws `SHOT_NOT_ACCEPTED` until each playback is production-ready. The historical SH01+SH02 technical concat still runs. Concat permission is not episode acceptance. The assembly file still needs its own ACCEPT. |
| Director | `shotQualityBlockers`, `directorRecoveryOf` | Quality review stays on Kiểm tra. The message names the speech clock, the mouth clock, the split mix, the cut action, and a legacy file that is not in Production OS. |

The recorded SH03 line stays `Điểm thấp thì lần sau gỡ...`. The missing ending was not invented. `LineComplete` is false, so a future voice request for that shot stays blocked.

## EP01 readiness

Recorded evidence was classified again. The mp4 bytes are not in this workspace and were not opened. Nothing below is a new visual review.

| Shot | Path | Playback named by the contract | Production accept |
|---|---|---|---|
| EP01-SC01-SH01 | Production OS, next action `REVIEW_PLAYBACK` | MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` | No. Acting entrant contradicts the story. Mouth clock fails. |
| EP01-SC01-SH02 | Production OS, next action `REVIEW_PLAYBACK` | MIX `58297402-e252-45e8-8d33-c7414749a297` | No. Speech 2.534s does not cover 8s. Mouth clock fails. Lipsync 2.5s against an 8s picture is a split clock. Recorded observation has no red score. |
| EP01-SC01-SH03 | Legacy candidate | `9F3XNLOMVOstXuQnDWXSK_kYgN6Z0V.mp4` | No. Not in Production OS. Spoken line is incomplete. Red-score prop is not attested. |
| EP01-SC01-SH04 | Legacy candidate | `cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4` | No. Not in Production OS. Stored action is the broken prefix. Red-score prop is not attested. |
| EP01-SC01-SH05 | Legacy candidate | `i3okll9-JNQfyKFh6b34R_ywaRJ8bt.mp4` | No. Not in Production OS. Spoken line is not bound. |
| EP01-SC01-SH06 | Legacy candidate | none | No. Not in Production OS. Pixels were not fetched. |

Episode plan: not assemblable and not accepted. Provenance keeps SH01–SH06 in order. SH06 has no artifact id. The existing two-shot assemblies stay technical files.

## Status labels

| Label | This pass |
|---|---|
| IMPLEMENTED | Six-shot contracts, future-request propagation, clock gate, prop blocker, legacy candidate path, ordered episode plan, director failure text |
| AUTOMATED-TESTED | Commands below. No paid call |
| EXERCISED WITH EXISTING ARTIFACTS | `ObserveEp01`, `Ep01Workflow`, and `PlanEpisode` on the recorded ids, durations, and legacy file names |
| VISUALLY REVIEWED | No |
| HUMAN-ACCEPTED | No shot. No episode |
| BLOCKED / NOT VERIFIED | All six shots and the episode. SH06 pixels. Word-level match of the audio. Whether the corrected SH02 sentence produces the red-score action on the current model. Provider quota |

## Tests

Passed:

- `dotnet test tests/KitPlatform.Content.Tests --filter FullyQualifiedName~ShortProductionQualityPipelineV1Tests|FullyQualifiedName~ShortProductionAcceptanceV1Tests|FullyQualifiedName~ProductionEpisodeAssemblyV1` — 3 passed
- `dotnet test tests/KitPlatform.Content.Tests --filter FullyQualifiedName~ProductionOfficialCreateVideoFreezeV1Tests|FullyQualifiedName~ProductionCreateVideoFreezeIntegrityV1Tests` — 2 passed
- `npx tsx client/admin/src/modules/content/famixa-short-quality-pipeline.smoke.ts`
- `npx tsx client/admin/src/modules/content/famixa-shot-production-orchestration.smoke.ts`
- `npx tsx client/admin/src/modules/content/famixa-short-acceptance.smoke.ts`

Cost this pass: $0 provider.

## Changed files

- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ShortProductionQualityPipelineV1.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ShortProductionQualityPipelineV1Regression.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEpisodeAssemblyContract.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs`
- `client/admin/src/modules/content/famixa-short-quality-pipeline.ts`
- `client/admin/src/modules/content/famixa-short-quality-pipeline.smoke.ts`
- `client/admin/src/modules/content/famixa-shot-production-orchestration.smoke.ts`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionState.ts`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionDirector.ts`
- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx`

## Highest-value external step — not run

Experiment **QP-SH02-PICTURE-1**. Do not start the paid call without an explicit approval. Do not switch provider or model.

Another lipsync of the current 8s SH02 picture cannot pass. Speech is 2.534s and the lipsync window is 2.5s, so the clock gate blocks that file before a new mix.

1. A person watches mix `58297402-e252-45e8-8d33-c7414749a297` and records REJECT on that artifact id. This step spends no provider quota.
2. Read the quota for the current Veo selection only.
3. After approval, one video generation on that same selection. Input is the future SH02 package: completed handoff, bag on the chair, folded test with a red score, spoken line `Dạ… nay con làm bài không tốt.` The requested picture clock must be within 0.75s of that line. If the current model cannot make a clock that short, stop before submit.
4. Acceptance of the new file requires a new review of that artifact id: action, continuity, motion, and the red-score prop. It does not inherit any earlier tick. Do not lipsync until that review passes.
5. Stop if the frames miss the handoff or the paper is blank. Do not open SH01.

A separate human blocker, not part of this experiment: the rest of the SH03 line after `Điểm thấp thì lần sau gỡ` is not in the recorded script, so that shot cannot get a complete voice draft until someone supplies the locked line.
