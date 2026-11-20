# FAMIXA SHORT QUALITY PIPELINE V1

Date: 2026-10-17. Local only. No provider call, no Production deploy, no rewrite of historical snapshots or existing media.

## Readiness verdict

**Not production-ready.**

EP01 still has no shot whose picture was accepted against the file that plays, and it has no reviewed six-shot episode assembly. The pipeline can now reject a short that is only technically valid. It cannot yet show a short that passed the visual contract.

## What this pass implemented

One contract, `ShortProductionQualityPipelineV1`, on the existing Production OS. It does not add a second engine.

| Piece | Where it lives | What it does |
|---|---|---|
| Future shot spec | `ShortProductionQualityPipelineV1` | Corrects the next video request when the text repeats a confirmed contradiction. Historical ingress constants stay as the record of what was already generated. |
| Desk request | `famixa-short-quality-pipeline.ts`, Series desk claims and CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC | The next desk request sends the corrected acting or the completed action sentence, and strips a leading stage cue from the spoken line. |
| Server video freeze | `ProductionCreateVideoService` | A new CREATE_VIDEO whose acting still says Linh enters, while the story says Minh enters, freezes the corrected Minh-enters spec. A replay of the historical SH02 acting is left unchanged. |
| Spoken line | `PerformanceContractRules.SpokenText` | `(lý nhí) Dạ… nay con làm bài không tốt.` becomes `Dạ… nay con làm bài không tốt.` on the next voice draft. The source constant still contains the cue. |
| Shot acceptance | `ShortProductionAcceptanceV1` plus visual attestations | Provider success, a verified hash, and an identity binding do not accept the picture. ACCEPT requires the review id to equal the playback id, and the reviewer must attest action, continuity, motion, and, on a speaking shot, mouth-on-speech. |
| Desk ticks | `ShotProductionQa` and `qualityReviewAccepted` | The motion tick is required. A stored ACCEPT that does not name the current playback, or that skips action, continuity, motion, or voice/face, does not pass. |
| Episode gate | `ClassifyEpisode` | Every required shot must be production-accepted, and the episode assembly artifact must have its own ACCEPT. A verified assembly file is not that decision. |

## Quality contract

| Dimension | Pass | Fail |
|---|---|---|
| Technical validity | Task succeeded, artifact verified, duration > 0, playable mime, artifact shot equals binding shot | Any of those missing, including a cross-shot artifact |
| Script and action | Acting entrant matches the story. The action sentence is complete. The text sent to speech has no leading stage cue | `ACTING_SUBJECT_CONTRADICTS_STORY`, `ACTION_SENTENCE_INCOMPLETE`, `STAGE_DIRECTION_IN_SPOKEN_LINE`, `SPOKEN_LINE_INCOMPLETE` |
| Visual motion and continuity | A person attests `ACTION_MATCHES_BEAT`, `CONTINUITY_WITH_NEIGHBOR`, and `MOTION_PERFORMS_BEAT` on this artifact | Missing attestation, or a held pose / duplicate body recorded by review |
| Dialogue, audio, lip-sync | Speech covers playback within 0.75s, and `MOUTH_ON_SPEECH_INTERVAL` is attested. An explicit mouth/speech mismatch fails | `SPEECH_DOES_NOT_COVER_PLAYBACK`, `MOUTH_CLOCK_MISMATCH` |
| Shot acceptance | Decision ACCEPT, review artifact id equals the playback artifact id, and the blockers above are empty | A changed file does not keep the previous ACCEPT |
| Episode acceptance | Every required shot is shot-accepted, and the assembly artifact has its own matching ACCEPT | `SHOT_NOT_ACCEPTED`, `ASSEMBLY_NOT_REVIEWED`. `ASSEMBLY_TECHNICAL_IS_NOT_ACCEPTANCE` is a note when the file is valid and the review is not |

## EP01 six-shot matrix

Build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`. Visual notes are the 2026-10-17 frame review already on record. This pass did not watch the files again.

| Shot | Playback named by the contract | Stage | Visual | Production accept |
|---|---|---|---|---|
| EP01-SC01-SH01 | MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` | `TECHNICALLY_VALID`. Blockers: acting entrant contradicts the story; mouth clock fails. Identity `ACCEPTED:MIX` is a note, not a review | Seen earlier. Entrance is not performed. Linh is duplicated | No |
| EP01-SC01-SH02 | MIX `58297402-e252-45e8-8d33-c7414749a297` | `TECHNICALLY_VALID`. Blockers: speech 2.534s does not cover 8s; mouth clock fails | Seen earlier. Open mouth is on the silent interval. No red-score action | No |
| EP01-SC01-SH03 | Legacy Fal file, no OS task | `NOT_IN_PRODUCTION_OS`. Spoken line is not bound in OS | Seen earlier. Held pose. Blank paper. Graph QA is not a review | No |
| EP01-SC01-SH04 | Legacy Fal file, no OS task | `NOT_IN_PRODUCTION_OS`. Stored action is the broken prefix and is flagged `ACTION_SENTENCE_INCOMPLETE` | Seen earlier. Held pose. Blank card | No |
| EP01-SC01-SH05 | Legacy Fal file, no OS task | `NOT_IN_PRODUCTION_OS`. Spoken line is not bound in OS | Seen earlier. He is already seated | No |
| EP01-SC01-SH06 | Legacy CloudFront URL | `NOT_IN_PRODUCTION_OS` | **Not fetched.** HTTP 401. Pixels were not judged | No |

Episode: **blocked.** No shot is production-accepted. There is no reviewed ordered assembly of SH01–SH06. The two existing per-shot assemblies stay as they are.

## Status labels

| Label | This pass |
|---|---|
| Implemented | Future spec, spoken-line strip, shot gate, episode gate, desk motion tick |
| Tested | Automated fixtures below. No paid call |
| Visually verified | Nothing new. SH01–SH05 notes are the earlier frame review. SH06 was not seen |
| Accepted | No shot. No episode |
| Blocked | All six shots and the episode, for the reasons in the matrix |
| Not verified | Word-level match of the audio to the line. SH06 pixels. Whether the corrected SH01 sentence will stop a duplicate on the next model run. Remaining provider quota |

## Corrections that propagate, and records that stay

Confirmed repairs, with no new story beat:

- SH01 next video request: Minh enters, head lowered, bag strap dragging. Linh is already wiping the table and looks up. The stored ingress acting still says Linh enters.
- SH02 next desk request: the cut-off “đưa hai” action is completed from the existing story (hesitate, bag on the chair, unzip, folded test, both hands, look down). The server replay path still freezes the historical acting so old jobs keep their snapshot.
- SH03/SH04 next request: the broken parenthesis becomes `Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.`
- Next voice draft: the stage cue `(lý nhí)` is not spoken. The canon line still stores it.

Existing MP4s, hashes, selections, and snapshots were not regenerated.

## Reusability

The same `Classify` and `ClassifyEpisode` functions accept a two-shot fixture that is not EP01 (a person at a door handing over a book) when every shot review names its artifact and the assembly review names the assembly file. A second assembly id does not inherit the first review. No second pipeline was built, and that script was not generated.

## Tests

Passed:

- `ShortProductionQualityPipelineV1Tests`
- `ShortProductionAcceptanceV1Tests`
- `ProductionOfficialSh02CreateVoiceV1Tests`
- `ProductionEp01Sh01RealFilmRunV2Tests`
- `ProductionOfficialCreateVideoFreezeV1Tests`
- `ProductionCreateVideoFreezeIntegrityV1Tests`
- `famixa-short-quality-pipeline.smoke.ts`
- `famixa-short-acceptance.smoke.ts`
- `famixa-video-studio-ux.smoke.ts`
- `famixa-shot-production-orchestration.smoke.ts`
- `famixa-short-production-contract-fix.smoke.ts`
- `famixa-final-artifact-readiness-cta.smoke.ts`
- `famixa-final-artifact-identity-binding.smoke.ts`
- `famixa-explicit-mix-action.smoke.ts`

`famixa-shot-production-contract-v2.smoke.ts` still stops under plain `tsx` on `import.meta.env` before its assertions. That import path was already in the file. This pass only added the motion tick to its accept fixture.

Cost this pass: $0 provider.

## Next action — defined, not run

Experiment **QP-SH02-1**. Do not start it without an explicit approval, and do not switch provider.

1. A person watches the current SH02 mix `58297402-e252-45e8-8d33-c7414749a297` and records REJECT against that artifact id, with action, continuity, motion, and mouth ticks.
2. Read the quota for the current Veo take, ElevenLabs `eleven_v3`, and Fal `sync-lipsync-v3`.
3. After approval, one lipsync/mix on the existing SH02 picture. The spoken line must be `Dạ… nay con làm bài không tốt.` Speech energy and Minh’s speaking mouth must share one interval. The new file gets a new review. It does not inherit any earlier tick.
4. If that file fails the visual contract, stop. Do not open SH01, and do not change model to hide the miss.

SH01’s next video, when one is later approved, must use the Minh-enters spec. That call is not part of QP-SH02-1.
