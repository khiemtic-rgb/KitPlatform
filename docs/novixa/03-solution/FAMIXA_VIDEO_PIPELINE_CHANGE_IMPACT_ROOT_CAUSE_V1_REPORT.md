# FAMIXA — VIDEO PIPELINE CHANGE IMPACT & ROOT CAUSE V1

Date: 2026-10-17. Read-only. No file was patched except this report. No provider call, no permit, no quota read, no media file created, no Production row updated, no migration, no deploy, no git reset, checkout, merge, or stash.

The file under review is still mix `58297402-e252-45e8-8d33-c7414749a297`. This pass re-hashed it. It did not regenerate it and does not accept it.

## Decision

**BLOCKED**

A later video cannot be both legal under the current clock and long enough for `veo-3.1` to emit. The desk also refuses a CREATE_VIDEO post on the official film. The prompt and keyframe that produced the current picture are known. Changing them is not a change that can be executed or checked until a person supplies a picture length the model can hold and the existing 0.75 second rule can accept. No such length is in the desk map, and no approved dialogue interval is in the locked SH02 sources.

## What this pass verified on disk

| Stage | Artifact | Role | File checked this pass | Bytes | SHA-256 |
|---|---|---|---|---|---|
| Frozen still | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` | KEYFRAME | `App_Data/kit-video-i2v/os-kf/5a1325ae8a7b4f389f496e24f483ace7.mp4` | 113493 | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` |
| Video take | `b44e530b-5b59-422f-9b80-e058a3a639b5` | VIDEO_TAKE | `App_Data/kit-video-i2v/os-veo-out/5a72a533ca30466e99b0357fa47bcdd8.mp4` | 1437239 | `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34` |
| Voice | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` | VOICE | `App_Data/kit-video-i2v/os-voice/9bd531131c244f2b84c25c392b1fb94d.mp4` | 40586 | `bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298` |
| Lip-sync | `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e` | LIPSYNC | `App_Data/kit-video-i2v/os/833e816708624dbbba9d89a2fc3b9a4e.mp4` | 2073636 | `725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e` |
| Mix / playback | `58297402-e252-45e8-8d33-c7414749a297` | MIX | `App_Data/kit-video-i2v/os/ff28d642765942839894f89bfbeb897c.mp4` | 1423715 | `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b` |

ffprobe this pass, no frames written:

| File | Picture | Audio |
|---|---|---|
| Video take | h264, 1280×720, 8.000 s, 192 frames | no audio stream in the probe |
| Lip-sync | h264, 2.500 s, 60 frames | AAC 2.480 s |
| Mix | h264, 1280×720, 8.000 s, 192 frames | AAC 7.984 s |
| Voice | — | MP3 2.480 s |

The voice container duration measured here is 2.480 seconds. Earlier OS notes record 2.534 seconds for the same artifact id and the same SHA. This pass did not re-read the database row. The gap does not change the clock: both numbers leave more than 0.75 seconds of picture after the line on an 8 second take.

The keyframe bytes are a JPEG (`FF D8 FF E0`) stored under an `.mp4` name. This pass confirmed the hash and did not decode the picture.

Repository, recorded and left as found:

- Branch `main`, ahead of `origin/main` by 6 commits and behind by 142. No `MERGE_HEAD`, no `REBASE_HEAD`, no unmerged paths.
- Those 6 commits are pharmacy, architecture docs, and editorial workflow. They are not the SH02 video, lip-sync, or mix path.
- The SH02 execution types used below are untracked in the working tree, including `ProductionCreateVideoService.cs`, `ProductionMixContract.cs`, `VeoMotionDurationMapping.cs`, `ProductionEp01Sh02IngressCanon.cs`, `ShortProductionQualityPipelineV1.cs`, and `ShotProductionOutcomeV1.cs`. `ContentFamixaSeriesTab.tsx` is a tracked modification. This pass did not commit or reconcile them with `origin/main`.

## 1. What the recent changes actually reach

The two source reports (`FAMIXA_PRODUCTION_OUTCOME_ENGINEERING_V1_REPORT.md`, `FAMIXA_FIRST_REAL_SHOT_VALIDATION_READINESS_V1_REPORT.md`) add a verdict reader, a click-time lip-sync clock, and an explicit ACCEPT/REJECT. None of them is on the path that wrote `58297402-…`.

| Change | Where it runs | Class | Reached the existing MP4 |
|---|---|---|---|
| `ShotProductionOutcomeV1.ReadAsync` / `Readiness` / `ControlledSh02Attempt` | A reader over the store and the playback bridge. `ControlledSh02Attempt` is called from its regression, not from `ProductionCreateVideoService`. | 1. Readiness | No. It does not call Veo, Fal, or ffmpeg. |
| `futureDeskActing` / `ApplyFutureVideoSpec` | Desk claim and `ProductionCreateVideoService` plan/execute, before a new freeze. Official film `4ef164d6-…` is refused earlier by `mayMutateProductionOs` (`FILM_PROTECTED`) in `executeProductionOsAction`. | 2. Would affect the next acting string, on a path the official shot cannot post | No new take exists. Historical canon is unchanged. |
| `clickTimeLipsyncClock` | `executeProductionOsAction` only when the action is `CREATE_LIPSYNC` or `MIX_VIDEO`. `prepareOfficialLipsync` separately refuses the known SH02 parents before `createProductionRequest`. | 1. Blocks a click | No. The current lip-sync and mix files were not rewritten. |
| Explicit ACCEPT / REJECT (`explicitArtifactDecision`, `bindShotChecklist`) | Review record. The official mix is not a tenant-resolved preview, so the card cannot stamp this artifact. | 1. Acceptance | No. |
| `ProductionMixContract.Build` | The live mix composer. Still sets the output clock to the video take and, when lip-sync is shorter, copies video-take picture from the lip-sync end to the take end and fills that span with silence. | 4. Mix, unchanged behavior | The existing mix was built by this rule on 2026-09-23. The rule was not re-executed. |
| `VeoMotionPrompt.FromFrozen` | Still joins story, acting, and visual with a space. Still the prompt sent by `VeoMotionExecutionAdapter`. | 2. Prompt shape, text of the historical freeze unchanged | The historical snapshot `fde661bb-a33f-418d-ac68-5c6f4751f81f` is still `FromEp01Sh02Canon`, which stores the truncated acting. |
| `content-famixa-visual-spec.ts` performance line `Mouth mid-speech` | Source of the performance clause baked into the ingress acting. File is not in the uncommitted pipeline set above. | 2. Already inside the frozen acting | Already consumed by the take. Not re-sent. |

`futureClockBlocker` is only referenced by its smoke test. It is not called from the CREATE_VIDEO click.

No change in this set was traced into `predictLongRunning`, Fal `sync-lipsync/v3`, or `ProcessFfmpegMixTransport.ComposeSegmentsOnceAsync` for these artifact ids. The bytes above still match the CK18 / CK19 / CK20 take, the CK23B lip-sync, the CK22 voice, and the CK24D mix.

## 2. Why those changes have not shown a better video

There is no newer SH02 picture. The playback file is the CK24D compose of the CK18 take and the CK23B lip-sync. A readiness record, a click that returns before the request, and a future acting string that the official desk is not allowed to post do not replace those bytes.

A passing isolated-tenant verdict in the outcome report uses planted payloads. It is not this file.

## 3. Chain that produced the current file

Build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, shot `EP01-SC01-SH02`.

### Frozen request

| Field | Value |
|---|---|
| Snapshot | `fde661bb-a33f-418d-ac68-5c6f4751f81f` |
| Snapshot hash | `56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb` |
| Task | `9cd27409-41fe-4194-b5a3-1b90f4d7b2a2` |
| Attempt | `5a72a533-ca30-466e-99b0-357fa47bcdd8` |
| Selection | `c52b646f-86b9-4a43-834d-d94ab9f013b8` |
| Provider / model | `google` / `veo-3.1` (`veo-3.1-generate-001`) |
| OS duration / provider duration | 10 / 8 |
| generateAudio | false |
| Keyframe | `91daaf4b-…`, hash `9fe2bd76…` |
| Prompt function | `VeoMotionPrompt.FromFrozen`: story + acting + visual |

The frozen strings are `ProductionEp01Sh02IngressCanon`, via `OfficialCreateVideoFreezeV1.FromEp01Sh02Canon`.

- Story is the full stage direction: hesitate, put the bag on the chair, unzip, take out the folded test, hand it with both hands, lower the gaze. It does not say the score is red.
- Acting repeats that direction and is cut at `đưa hai`. It then adds gaze lines and `Mouth mid-speech` with no end time.
- Visual contract intent is the same cut sentence. It has no red score and no closed-mouth tail.

The outbound prompt text was not stored in the CK18 report (`prompt | from frozen Story + ActingBeat + VisualContract (not logged)`). The three fields that the prompt function joins are in the canon and were the freeze inputs. Request hash on that start: `3938ce57a6b6efa78454000eeb74712ec93233e6a96d87f924c731cf4c222c10`.

### Video take

`b44e530b-…`, SHA `21700e4f…`, 8.000 seconds, 192 frames, mute. Operation `633ea037-d719-4e31-9492-e36442bd3e20`. One Start, then one Poll. CK20 bound `ACCEPTED:VIDEO_TAKE`. This pass did not call Vertex.

CK23A measured this same SHA, not the later lip-sync:

| t | Mouth on the take |
|---|---|
| 0.0–2.5 | closed, reading the paper |
| 3.00 | first opening |
| 3.25–4.0 | open gasp |
| 6.5–7.5 | closed; paper taken. Largest face motion at 6.75 is not a viseme |

This pass did not re-sample those frames. The file hash still matches the file CK23A measured.

### Lip-sync

Current artifact `b0b4f0f2-…`, not the historical 8 second job `32bec770-9706-4aad-bc21-8db3d4f11d4c`.

| Field | Value |
|---|---|
| Provider / model | `fal` / `sync-lipsync-v3` |
| sync_mode | `cut_off` |
| Face box | `[450,50,720,400]` |
| Parents | take `b44e530b-…` + voice `0fde86c2-…` |
| Line | `line-SC01-CHAR-001-2`, text `(lý nhí) Dạ… nay con làm bài không tốt.` |
| Output | 2.500 s picture, 2.480 s audio, 60 frames |

CK23B, on this artifact: during 0.10–2.40 s the take mouth is closed and the lip-sync mouth is open. The gasp and the paper handoff are outside this file. This pass confirmed the duration and the hash, and did not re-open the frames.

### Mix

`ProductionMixContract.Build` output clock is the video take. For this pair the frozen contract is:

1. Picture 0–2.5 from lip-sync `b0b4f0f2-…`.
2. Picture 2.5–8 from video take `b44e530b-…`.
3. Audio 0–2.5 from the lip-sync.
4. Audio 2.5–8 silence, 5.5 seconds.

CK24D executed that graph once (`ffmpeg-compose-ff28d642765942839894f89bfbeb897c`) into `58297402-…`. Frame MSE in that report: mix at 0.10 s matches the lip-sync (open mouth) and not the take (closed mouth); mix at 2.60, 5.00, and 7.90 matches the take. Mix audio 2.50–8.00 WAV RMS was 0.023. This pass confirmed the output hash, 8.000 s picture, and 7.984 s AAC. It did not recompute MSE.

### Playback

The file a person can open locally is that mix. The tenant preview still does not serve it: the rows are unattributed and the storage key is not under a tenant prefix. This pass did not attribute them.

## 4. Mouth still moving after the line

**Stage: the video take, copied into the mix from 2.5 s to 8 s. Confidence: high for the stage. The 3.0–4.0 s gasp itself is the CK23A measurement of this same take hash, not a new frame read.**

The current lip-sync ends at 2.5 s. It cannot be the picture at 3–4 s. The mix does not invent that picture: `ProductionMixContract.Build` copies the take for every second after the lip-sync. CK24D already showed the mix tail matches the take and the tail audio is silence.

Cutting or muting the tail therefore does not remove the mouth. The mix already mutes it. The open mouth at 3.0–4.0 s is in the picture of the take.

What in the take's inputs can explain that:

- The frozen acting says `Mouth mid-speech` and gives that mouth no end.
- The picture is 8 s. The line is about 2.5 s. The take's own mouth stays closed through the speech window and opens after it.
- Lip-sync `cut_off` then opens the mouth only inside 0–2.5 s, which is the part where the take was closed. The gasp remains in the untouched tail.

Not established: that the four words `Mouth mid-speech` are the only reason Veo opened the mouth at 3 s. The instruction is in the frozen acting. A controlled comparison would need a new take. This pass did not make one.

The older 8 second Fal job (`sync_mode=silence`, artifact `32bec770-…`) also did not create that gasp. CK23A found the open mouth already identical on the mute take. That job is not the lip-sync inside the current mix.

## 5. Action and props

**Stage: the video take, conditioned on keyframe `91daaf4b-…`. Confidence: high that lip-sync and mix did not add or remove the props. Medium on the exact pose of the still, because this pass did not decode the JPEG.**

The locked story does ask for the bag on the chair, the unzip, the folded paper, both hands, and the lowered gaze. The frozen acting contains that text only until `đưa hai`, so the both-hands handoff and the lowered gaze are cut out of the acting line. Neither the story, the acting, nor the visual contract says the score is red. `Sh02FutureActing` does name a red score and the bag on the chair. That string is not what `FromEp01Sh02Canon` froze, and the official desk cannot post it (`mayMutateProductionOs` is false for `4ef164d6-…`).

Prior frame notes on this mix (`FAMIXA_SHORT_PRODUCTION_READINESS_001.md`), not repeated by watching the file here:

- The picture starts on the keyframe: seated, blank folded paper, Linh over the shoulder.
- The bag stays on the sofa.
- No red score.
- At 3.0 s and 6.5 s he looks up and Linh takes the paper, and that picture is the mute take.

Lip-sync and mix have no prop input. They cannot draw a score or move the bag. CK23B also says the paper and hands in the speech window match the take. The missing action is already in the take the mix copies.

Not established this pass: a new description of the keyframe pixels. The still's hash matches the I2V parent of the take. The readiness note that the take starts on that still is the evidence that the start pose was already the seated paper pose. This pass did not look at the JPEG.

## 6. The change that has to be made

One change, at the video-take inputs, with the mix refused when it would paste an ungoverned tail. Lip-sync of the current take cannot remove the gasp, and another silence pad cannot either.

### Stage

`ProductionCreateVideoService` before `FreezeInputAsync`, using the prompt already built by `VeoMotionPrompt.FromFrozen`. Then `ProductionMixContract.Build`, which today always appends the take after the lip-sync.

The official click must not stay on the TEST-only gate if this shot is the one to be regenerated. `mayMutateProductionOs` currently returns false for build `4ef164d6-…`, so `futureDeskActing` in `executeProductionOsAction` never runs for this CREATE_VIDEO. A server-side freeze that still calls `FromEp01Sh02Canon` would store the truncated acting again.

### Inputs that are allowed to freeze

- Story: the locked SH02 direction, including both hands and the lowered gaze. Not the acting string that stops at `đưa hai`.
- Acting: `ShortProductionQualityPipelineV1.Sh02FutureActing`, plus one clause the current future string does not have: after the locked line, Minh's mouth is closed and still.
- Visual: the same beat. It must not carry the truncated intent, and it must not carry an unbounded `Mouth mid-speech`.
- Keyframe: a still of the start of the beat. The hash `9fe2bd76…` is the parent of the take that held the seated paper pose. It is not a legal parent for the next take.
- The red-score clause is already in `Sh02FutureActing`. The locked story does not contain it. Keep it only as that existing contract clause. Do not add a score number the script does not state.
- Duration: whatever a person has made legal under `FutureClockBlockers`. Veo `ToProviderDurationSec` only returns 4, 6, or 8. Against speech 2.480 s or 2.534 s and no director window, each of those fails the 0.75 s rule. This pass does not invent a window and does not add a new Veo duration.

### Fail closed

Stop before `predictLongRunning` when any of these is true:

- Acting equals `ProductionEp01Sh02IngressCanon.ActingBeat`.
- The joined prompt contains `Mouth mid-speech` and does not contain a closed mouth after the line.
- Keyframe SHA-256 is `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57`.
- `FutureClockBlockers` is non-empty for the provider duration and the measured voice. A computed 0.2 s lead-in is not a window. `approvedDialogueIntervals` stays empty unless `source` is `DIRECTOR`.
- The caller is the official film and the post is still the TEST fixture path. Do not bypass `FILM_PROTECTED` inside this report.

Stop before ffmpeg when `ProductionMixContract.Build` would copy video-take picture past the lip-sync end and that tail has no director window whose picture source is a closed mouth. Returning `MIX_CLOCK_SPLIT` is the existing code. Composing silence over the gasp is the current behavior and is the behavior to stop.

The historical snapshot `fde661bb-…` stays immutable. A replay of that request is not rewritten.

### How to check the mouth

On the new take id and the new hash, before lip-sync:

- Sample inside the speech interval and outside it. The take's mouth may move inside the line. It is still outside the line.
- Then lip-sync with the existing `cut_off` path, parents limited to that new take and the existing voice if the voice text is still the locked line.
- The lip-sync file must end at the voice. It must not be an 8 second `silence` mode job.
- The mix may play lip-sync picture only where the line is. If a tail exists, its frames are not the pre-lipsync gasp. Audio silence is checked separately and is not the mouth check.

### How to accept action, props, and continuity

A person, on that new id and hash:

- Minh hesitates, the bag leaves him onto the chair, the bag is unzipped, the folded paper is handed with both hands, and his gaze drops.
- The paper is not the blank sheet of the current take. If the future contract's red-score clause is in the prompt that was frozen, the score is visible.
- Minh, Linh, wardrobe, and the room match the neighboring shot. Neither body is duplicated.
- The motion is the beat, not a hold of the first frame.

REJECT if any of those fail. The attempt stops. No second provider call in the same attempt.

### Cost of one later attempt

Local catalog only. Not a balance and not an invoice.

| Call | Catalog | Amount |
|---|---|---|
| `google` / `veo-3.1`, video-only 720p, one count | `FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd` | 0.20 USD, kind Known. Duration is not a multiplier. |
| New still, if the current keyframe is refused | No amount read for a Gemini still in this pass | Unknown |
| `elevenlabs` / `eleven_v3` | `FamixaProviderCostCatalog` returns Unknown when the provider is not google, runway, wan, or fal | Unknown. Reusing voice `0fde86c2-…` avoids this call. The spoken line must still be the locked sentence. |
| `fal` / `sync-lipsync-v3` | 8 USD per minute, billed seconds `max(5, quantity)` | 0.67 USD estimate for a 2.5 s output quoted as 5 s. Kind Estimate. |
| ffmpeg mix | local | 0 |

This pass: 0 USD.

## 7. When a controlled generation can be proposed

All of the following, each still undone:

1. A person has chosen a picture length the selected model can emit that `FutureClockBlockers` accepts for the measured voice, or has pointed at a dialogue interval that already exists in an approved SH02 source and locked it with `source = DIRECTOR`. Veo 4, 6, and 8 do not pass today. No approved interval is in the story, the acting, or the locked line.
2. The next freeze is not `FromEp01Sh02Canon` and is not keyframe `9fe2bd76…`.
3. A new `google` / `veo-3.1` permit is issued for that shot. This pass did not read the permit table and did not issue one.
4. The official post path is an explicit authorization for this one shot. The current desk function will not send it.
5. The new file is judged by a person on its own id and hash. The current mix is not that file.

Until those exist, the generation stays unsent.

## Tests not run

No provider test, no live SQL, no browser session, no frame extract. The hash and ffprobe checks above are the media evidence added in this pass.

Cost this pass: $0 provider.
