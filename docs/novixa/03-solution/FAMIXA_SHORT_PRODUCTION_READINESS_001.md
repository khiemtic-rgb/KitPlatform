# FAMIXA-SHORT-PRODUCTION-READINESS-001

Date: 2026-10-17. Scope: local `kitplatform`, official build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` (`Tập 01` / `EP01` / `in_prod`). No provider call, no Production deploy, no mutation of live jobs or bindings.

## B. Production readiness verdict

Famixa can generate a playable shot file and lock its bytes. It cannot yet produce a repeatable, production-accepted short.

The official episode has six scripted shots. Only SH01 and SH02 are inside Production OS. Both have technically verified playback files. Frame review of those files shows they miss the approved action. Neither has a quality review bound to the artifact that is playing. Two film assemblies exist, and each contains one shot. There is no episode cut.

This verdict covers local EP01 on this build. It does not cover SH06 pixels (the legacy URL returned 401). It does not claim a phoneme-by-phoneme lip-sync score; speech was judged from frames plus audio energy, not from a transcript.

## A. Short quality matrix

Approved scene: SC01, five beats, six shots, script locked. Dialogue is locked performance `neutral / intensity 2`. Characters in frame: CHAR-001 Minh, CHAR-003 Linh (mẹ).

| Shot | Approved action and line | Current playback | Seen | Acceptance |
|---|---|---|---|---|
| EP01-SC01-SH01 | Minh enters, head down, bag dragging. Linh is already wiping the table and looks up. Line: `Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?` | OS MIX `2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2` = assembly `28f8de3e-f6d3-4972-934e-703a2be45939`. 5.042s, 1280×720, h264+aac. Parents: lipsync `837ed989-5e1f-45ad-b77d-cb26f5e808a2` ← Runway `gen4_turbo` take `b22370a9-6ddd-4b04-a281-c7498cc926ae` + ElevenLabs `eleven_v3` voice `bf8d0827-21ff-4daa-83b6-116bc3324ab0` (4.232s). Keyframe `1f4c26e0-35aa-4395-94e7-09d2b1b764b6`. Selection VIDEO `9b0dceee-ba53-4cc5-8139-ee9ba58a2011`. | Yes. Keyframe is a standing two-shot, not the entrance. 0.2s matches the keyframe. 1.5s both walk away, heavy blur. 3.0s Linh is duplicated. 4.5s is a different wide blocking. Mix frames match the lipsync, not a different shot. Speech energy is present across the 5s file. The speaker’s face is not usable at the 3s break, so mouth sync cannot pass. | OS identity `ACCEPTED` / `ACCEPTED:MIX`. Ledger stage `QUALITY_REVIEWED` is absent. Production accept: no. Classifier blockers: acting subject contradicts story. |
| EP01-SC01-SH02 | Minh hesitates, puts the bag on a chair, unzips, hands a folded test with a red score to Linh, looks down. Line: `(lý nhí) Dạ… nay con làm bài không tốt.` | OS MIX `58297402-e252-45e8-8d33-c7414749a297`, assembly `92621f9e-57eb-4185-8cea-a962ec399136`. 8.0s. Picture contract: 0–2.5s lipsync `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e`, 2.5–8s mute Veo take `b44e530b-5b59-422f-9b80-e058a3a639b5`. Voice `0fde86c2-250f-4303-bda0-4913a6dbd1e4` (2.534s, `eleven_v3`). Selection VIDEO `c52b646f-86b9-4a43-834d-d94ab9f013b8` (`google` / `veo-3.1`). | Yes. Starts on the keyframe: seated, blank folded paper, Linh over the shoulder. 1.5s lipsync opens the mouth slightly while he looks down; a second blank sheet appears. 3.0s and 6.5s he looks up and Linh takes the paper, but that picture is the mute take. Audio energy is speech only on 0–2.5s (about −22 dB) and silence after (−91 dB). The visible open mouth is silent. Bag stays on the sofa. No red score. | OS identity accept on VIDEO, VOICE, LIPSYNC, and MIX. Ledger stage stays below production accept. Blocker: speech does not cover playback. |
| EP01-SC01-SH03 | Same beat as SH04, line on this shot: `Điểm thấp thì lần sau gỡ...` Linh takes the paper, glances at the red score, looks at Minh. | Legacy Fal take `TM75BVxcD4_U4x2XLpXqH_MmBbhFdI.mp4`, 5.031s, video only. Lipsync `9F3XNLOMVOstXuQnDWXSK_kYgN6Z0V.mp4`, 5.031s with audio. Graph model `sync-lipsync-1.9`. No Production OS task, selection, or binding. | Yes, downloaded from the existing Fal URL. 0.3s and 2.0s are almost the same held pose. 4.0s the paper drops with blur. Paper is blank. Lipsync at 1.2s opens Linh’s mouth on that same pose. Phoneme match was not measured. | Graph `shotQa` and `visualQa` say PASS. That checklist is not an OS review. Production accept: no. |
| EP01-SC01-SH04 | Action half of the same beat. `dialogueSegmentIds` is empty. Stored `story` / `shotAction` is a broken prefix: `(Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.` | Legacy Fal `cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4`, 5.031s, no audio. Graph model `wan-2.1`. No OS records. | Yes. Profile two-shot, different framing from SH03. 0.3s and 2.0s barely move. Minh holds a small blank card. No red score, no “takes the paper” action. | Graph `videoApproved` true, `shotQa` empty, `visualQa` PASS at total 64. Production accept: no. |
| EP01-SC01-SH05 | Minh exhales, sits on the sofa edge, glances at Linh. Line: `Lát mẹ rảnh… chỉ lại chỗ này cho con nhé?` | Legacy take `9mHieEfukD7Qlih1RyLPg_G0ko97gA.mp4` and lipsync `i3okll9-JNQfyKFh6b34R_ywaRJ8bt.mp4`, both 5.031s. Graph `sync-lipsync-1.9`. `videoApproved` false. No OS records. | Yes. He is already seated. Expression shifts slightly by 2.0s. Lipsync at 1.2s opens the mouth. He does not sit down in the frames seen. Not the same picture as SH02. Phoneme match was not measured. | Graph checklist PASS. Production accept: no. |
| EP01-SC01-SH06 | Linh smiles and pats the cushion. Line: `Ngồi xích lại đây, lấy bút ra mẹ xem nào.` | Graph take is a CloudFront URL (291 chars) with `motionNeedsRemake: true` and `videoApproved: false`. | No. Fetch returned HTTP 401. Pixels, motion, and mouth were not judged. | Not accepted. |

Cross-shot motion: SH01’s wandering wide shot, SH02’s sofa close-up, SH03’s held over-shoulder, SH04’s profile, and SH05’s seated glance are different pictures. The failure is missed action and held poses, not one file reused as several shots. Wardrobe and room stay in the same stylized family.

Graph `runs.*.shotQa` is true for SH01, SH02, SH03, SH05, and SH06, with `visualQa.status = PASS` and generic notes. Those flags do not name an artifact id. SH01’s own frames contradict the PASS.

## C. Root cause

Confirmed:

1. **Shot spec.** SH01’s frozen acting beat says Linh enters. The approved story says Minh enters and Linh is already cleaning. That text is the legacy ingress canon and is inside the CREATE_VIDEO and CREATE_VOICE snapshots. SH03 and SH04 split one beat so each `story` field is a broken half of the same parenthesis.
2. **Prompt actually sent.** SH01 and SH02 CREATE_VIDEO `shot_contract` length is 0. The continuity clause `[FAMIXA CONTINUITY]` was not in the frozen prompt. SH01 voice prompt also contains `TEST VISUAL`.
3. **Provider limit.** Runway `gen4_turbo` left the SH01 keyframe, walked both characters away, and duplicated Linh. Veo `veo-3.1` held the SH02 keyframe and did not perform the unzip / bag / red-score sequence. A later prompt edit is not evidence that the model will perform that action.
4. **Lip-sync and mix.** Fal `sync-lipsync-v3` on SH02 used `cut_off` and returned 2.5s. MIX_CONTRACT_V1 then plays that segment for 0–2.5s and the mute take afterwards. Speech and the open mouth are on different clocks. SH01 lipsync preserved the broken motion.
5. **Acceptance process.** Task `SUCCEEDED` and artifact `VERIFIED` mean the file exists and the hash matches. Binding role `ACCEPTED` / `ACCEPTED:*` means that exact artifact is the identity lock. `ProductionStateMachines.ProviderSuccessIsAccepted()` is already false. Nothing in Production OS records that a person watched this artifact and accepted the picture. The desk treated `shotQa` ticks as that decision.

Not confirmed:

- Word-level match of the spoken audio to the line. No ASR was run.
- Whether a continuity sentence in a future prompt would stop SH01’s duplicate. The historical job never received that sentence, and gen4_turbo still has to be re-tested.
- SH06 picture.
- Quota remaining on Runway, Veo, Fal, or ElevenLabs. No account balance was queried, and no paid call was made.

## Pipeline

| Step | What works | What is open |
|---|---|---|
| Script | SC01 text and five lines are stored and locked. | SH03/SH04 action text is split. SH01 acting contradicts the story. |
| Shot spec | `episode.shots` lists SH01–SH06 with character and line ids. | Duration is a flat 5s on every shot. SH02’s real video clock is 8s. |
| Reference | SH01/SH02 keyframes are imported, hashed, and parented on the video task. | SH03–SH06 keyframes are file names on the graph only. |
| AI selection | SH01 Runway and SH02 Veo selections are ACTIVE, plus ElevenLabs voice. | No lipsync selection row. SH03–SH06 have no OS selection. |
| Generation | SH01/SH02 tasks are `SUCCEEDED` with provider job ids. | SH03–SH06 are legacy URLs outside OS. |
| Artifact validation | OS files are `VERIFIED` and readable on disk. | Verification is bytes and mime, not picture quality. |
| Quality review | Desk ticks exist. | Ticks did not name the playback. Graph QA is listed as non-authoritative. |
| Shot acceptance | Identity bindings are queryable per role. | Identity accept is not production accept. |
| Episode assembly | Each shot has its own FilmAssembly (`d56a3c63-…` SH01, `1c210b73-…` SH02). | No ordered episode of SH01+SH02, and SH03–SH06 are absent. |
| Final playback | The two assembly mp4 files play locally. | They are the same pictures as the mixes above. |

## Acceptance ledger

Use the labels below as a reading of evidence that already exists. Do not add a second stored status.

| Label | Evidence required |
|---|---|
| `GENERATED` | Task `SUCCEEDED` and an artifact row. |
| `TECHNICALLY_VALID` | `verification_status = VERIFIED`, duration > 0, video or audio mime, artifact shot equals binding shot. |
| `QUALITY_REVIEWED` | A human decision names the playback artifact id. |
| `ACCEPTED` | That decision is ACCEPT, the id matches the playback, and the blockers below are empty. |
| `REJECTED` | The decision for this playback id is REJECT. |

Blockers that keep a reviewed shot out of `ACCEPTED`: cross-shot artifact, acting entrant contradicts the story, speech window shorter than playback by more than 0.75s, review id points at a different artifact.

`ACCEPTED:MIX` in the database remains the identity lock. `ShortProductionAcceptanceV1` reports production readiness separately. On 2026-10-17 every EP01 shot is below production `ACCEPTED`.

Criteria for a future accept, each shot alone:

1. The visible action matches that shot’s approved beat.
2. Minh and Linh, wardrobe, and the room stay continuous with the neighboring accepted shot.
3. Motion performs the beat. A held pose or a duplicated body fails.
4. A speaking shot keeps the line, the energy, and the speaker’s mouth on the same interval.
5. Picture, audio, and duration match the frozen clock.
6. The artifact’s task shot, parents, and review id are the ones being accepted.
7. The decision records reviewer, time, artifact id, and ACCEPT or REJECT.

## D. Implementation order

1. **Data, no provider.** Repair SH01 so the acting entrant is Minh entering, with Linh already in the room. Repair SH03/SH04 so each shot has a whole action sentence. Remove `(lý nhí)` from text that is sent to TTS if it is a stage direction. This does not rewrite historical snapshots; those stay as the record of what was generated.
2. **Desk review, no provider.** Watch the current SH01 and SH02 files and store REJECT against those playback ids. The desk now writes `reviewedPlaybackId` only when the ticks are completed against the file it can name.
3. **One pilot regeneration, after quota is checked.** SH02 is the closer picture. A new lipsync/mix has to put the line on the interval where Minh’s mouth is the speaking mouth, and the test paper has to be the red-score prop if the beat still requires it. Budget one Veo or Fal attempt only after the spec is frozen and a person has rejected the current mix. Do not regenerate SH01 until the acting sentence is corrected; the last prompt asked the wrong person to enter.
4. **Provider capability, not a code promise.** gen4_turbo duplicated a character on this keyframe. Veo held the still and skipped the unzip. Lip-sync `cut_off` shortens the file to the audio. Those are model and mode limits. Re-test one shot and keep the result if the frames pass. Do not switch provider in this step.
5. **Human gate.** Production `ACCEPTED` in the ledger requires the watched decision. Identity `ACCEPTED:*` can remain the byte lock.
6. **Episode.** Assemble SH01–SH06 only after each shot has its own production accept. Today’s two assemblies must stay as they are.

Cost this pass: $0 provider. Risk of the next pass: one paid SH02 lipsync or video retry. Do not start it until quota is read.

## E. Changes delivered

- `ShortProductionAcceptanceV1` classifies a shot from task, verification, shot identity, and an explicit review id. Identity binding and provider success stay out of quality review.
- The classifier is registered beside the Production OS regression list and covered by `ShortProductionAcceptanceV1Tests` (1 passed).
- `SeriesShotRun.shotQa` and `visualQa` are listed as non-authoritative graph fields.
- The desk sets `qualityPassed` only when `decision = ACCEPT` and `reviewedPlaybackId` equals the current playback target (`assembly file`, else lipsync URL, else take URL). Completing the ticks stamps that id. Existing stored ticks without the id no longer count as passed, and the next command is the quality check rather than a paid resend.

Tests run:

- `dotnet test tests/KitPlatform.Content.Tests --filter ShortProductionAcceptanceV1Tests` — Passed 1.
- `famixa-short-acceptance.smoke.ts` — PASS.
- Orchestration, contract-fix, explicit-mix, final-readiness CTA, final-identity, lipsync bound source, late-result — PASS.
- `famixa-shot-production-contract-v2.smoke.ts` stops under plain `tsx` on `import.meta.env` before assertions. That import path was already in the file.
- `famixa-director-video-workspace-restructure.smoke.ts` fails `T6k2` because `ContentFamixaSeriesTab.tsx` does not contain the literal `CREATE_VIDEO trên Production OS`. That file was not edited here.

## F. Next action

Repair the EP01 shot text before any new generation: SH01’s acting beat must name Minh as the person who enters, and SH03/SH04 must each carry one complete action. Then watch the current SH02 mix and record REJECT on artifact `58297402-e252-45e8-8d33-c7414749a297`.
