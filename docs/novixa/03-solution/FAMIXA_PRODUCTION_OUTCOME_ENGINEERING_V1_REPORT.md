# FAMIXA PRODUCTION OS — PRODUCTION OUTCOME ENGINEERING V1

Date: 2026-10-17. Local repository and an isolated in-memory tenant only. No provider call, no permit issued, no quota read, no Production deploy, no migration, no change to official SH02 rows, and no new media file.

The timing, artifact-identity, hash-binding, and acting-contract work from the same day is the baseline. This pass does not reopen that audit. It connects the existing Production OS store to one shot verdict, and it runs that verdict on tenant-owned test data.

## Verdict

| Area | Status |
|---|---|
| Approved contract reaches a future request | PASS WITH GAPS |
| Shot outcome from tenant-owned artifacts | PASS WITH GAPS |
| Official SH02 playback through the tenant store | BLOCKED |
| Technical checks separated from visual acceptance | PASS WITH GAPS |
| SH02 ready for an authorized generation | BLOCKED |
| EP01 ready for episode acceptance | BLOCKED |

SH02 is not production-ready. EP01 is not production-ready. A passing isolated-tenant verdict is not a visual accept of any existing film file.

## 1. What shared root cause remained

The baseline already kept the clocks apart, preferred the current mix, and bound a review to an artifact id and a hash. The verdict that used those rules was still a calculator over a hand-built evidence record. The store that actually holds tasks, snapshots, artifacts, and bindings did not produce that verdict.

That one gap explains several symptoms together:

- A unit test could pass while nobody could point at the file the OS would play and say what the shot decision was.
- A review could not be dropped by changing the current mix inside the store, because the review never read the store.
- A future generation had the contract in one place, the clocks in another, and the permit outside both.

Two further causes stay outside that gap. They are not the same defect, and this pass does not fold them into it.

| Id | Class | What it explains | Evidence this pass | Disposition |
|---|---|---|---|---|
| RC-OUTCOME | Shared product cause | No single shot verdict was read from the tenant-owned artifact chain. | New reader `ShotProductionOutcomeV1` over `IProductionWorkStore` and `OfficialVideoAuthorityBridgeV1`. | Fixed for the isolated path. |
| RC-WINDOW | Contract source, left in place | SH02 has no approved dialogue interval. Veo can send 4, 6, or 8 seconds. The locked line is 2.534 seconds. With no declared window, each of those picture lengths fails the existing 0.75 second rule. | `ControlledSh02Attempt` returns `VEO_4`, `VEO_6`, and `VEO_8` clock blockers. A computed lead-in is not treated as a window. | Left blocked. The interval was not invented. |
| RC-TENANT | Environment | Official build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, the SH02 mix, and its bindings have `tenant_id` NULL, and the storage key is not tenant-prefixed. The tenant store will not serve that file. | Not re-queried. The prior closure on 2026-10-17 already read those rows. This pass did not attribute them. | Left blocked. Prerequisite for any live read of that file: the rows already share one non-null tenant with the caller, and the key sits under that tenant prefix. |
| RC-PIXELS | Creative result of the current file | The current mix misses the red-score action, and the open mouth sits on the silent tail. | Not re-watched. Prior frame notes and hash `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b` still name artifact `58297402-e252-45e8-8d33-c7414749a297`. | Left blocked until a person reviews that file. |
| RC-SH03 | Missing source | The locked SH03 line ends at `Điểm thấp thì lần sau gỡ...`. | Unchanged. Voice draft still refuses it. | Left blocked. The missing words were not written. |
| RC-EPISODE | Downstream of the shots | SH03–SH06 are outside Production OS. No shot has a production accept. | `ObserveEp01Episode` is still not production-ready. Confirmed again by the isolated test. | Left blocked. |

The desk action gate previously ignored a director-locked timing as well. A default or measured timing is still not a dialogue window. Only `source = DIRECTOR` is passed into the existing clock check. SH02 has no such lock, so this wiring does not make the current shot pass.

## Dependency map

```text
Approved EP01 contract
  → future request (acting, line, prompt)     [baseline, unchanged]
  → frozen snapshot on the video task         [exact request that was stored]
  → CURRENT keyframe / video / voice / lipsync / mix
  → playback = verified CURRENT:MIX, else the existing authority order
  → byte hash must match the selected artifact
  → technical file check
  → human review of that id and hash
  → shot verdict
  → episode only after every shot verdict is ready and the assembly has its own review
```

A permit and a provider call sit beside the future request. They are not part of the verdict reader. This pass did not issue either.

## 2. What is fixed, and what remains

Fixed in this pass:

- One outcome is read from the existing store, the existing playback authority, and the existing quality rules. No second engine.
- Picture duration, speech duration, lip-sync duration, and mix duration are reported as four numbers. No interval is derived from them.
- Playback bytes are accepted only when they hash to the selected artifact. A foreign tenant receives neither the rows nor the storage key.
- A review whose hash differs from the playing file does not stay ACCEPT.
- A review that names the right file still fails when the clocks fail, the mouth moves on the silent interval, or a required prop is absent.
- A different isolated shot, whose speech sits inside the picture and whose review names that file, can reach a ready verdict. The verdict records that the motion and mouth checks were attested, and that the pixels were not measured.

Still blocked, each for its own reason:

| Blocker | Reason | Impact |
|---|---|---|
| Official SH02 file | Unattributed tenant and a storage key the tenant store does not own. | The live preview cannot serve `58297402-e252-45e8-8d33-c7414749a297`. |
| Current SH02 picture | Prior frames miss the red score, and the mouth is on the silent tail. | The file stays unaccepted. This pass wrote neither ACCEPT nor REJECT. |
| Next SH02 generation | No approved dialogue window, and Veo 4/6/8 all fail the undeclared-window clock against 2.534 seconds. No issued permit was created here. | A provider request is not ready. |
| SH03 line | The locked source is truncated. | Voice stays refused. |
| SH01, SH04, SH05, SH06 | Prior pictures miss the approved action, or the pixels were never fetched. | They stay out of this shot's pilot. |
| Episode | No production-accepted shot and no reviewed six-shot assembly. | Concat of one file is not an episode accept. |
| Desk REJECT | The shot card still writes ACCEPT when every tick is set against the loaded preview. It has no REJECT control. | A person cannot record REJECT on that card. The outcome reader can. |
| Desk lip-sync length | The click-time clock still sends lip-sync duration 0, because the freeze does not store the lip-sync output length. | The server outcome uses the artifact's own duration. The click does not. |

## 3. Can the OS deliver a shot on valid isolated tenant data?

**PASS WITH GAPS.**

Confirmed on build `cccccccc-3333-4333-8333-0000000000c3`, tenant `aaaaaaaa-1111-4111-8111-0000000000a1`. That build is not official EP01. The planted bytes are local test payloads with a SHA-256, the same kind of fixture the playback authority tests already use. They are not a film, and they were not sent to a provider.

What the run established for the SH02-shaped shot `EP01-SC01-SH02` on that build:

- The approved acting is the contract that contains the red score. The frozen snapshot remains the historical truncated acting, so a replay is not rewritten.
- The current mix is the playback. Its bytes match its hash. The lip-sync names the video take and the voice as parents. The mix names the lip-sync.
- The four durations are 8s picture, 2.534s speech, 2.5s lip-sync, 8s mix. The clock blockers are the speech coverage failure and the split lip-sync. No dialogue window was supplied.
- A review with a different hash fails. A review of the matching hash still fails on the mouth-during-silence flag and the missing red-score prop.
- After the bytes under the storage key were replaced, the hash check failed and the shot stayed unready.
- Tenant `bbbbbbbb-2222-4222-8222-0000000000b2` received no stages and could not read the key.

A second shot on the same tenant, `ISO-SC01-SH01`, has speech 4.0s inside a 4.2s picture and a review that names that mix. The verdict is ready. `AutomatedVisualProof` is false. EP01's episode verdict stays unready, so this shot does not accept the episode.

What this does not establish:

- Browser playback. No session was opened.
- A real MP4 of the official mix. That file was not opened and was not copied into the tenant store.
- Official SQL. The four live mix and assemble tests remain unable to run until the official rows are already tenant-owned. This pass did not give them a caller and did not backfill `tenant_id`.

## 4. What can be checked automatically, and what a person must judge

| Check | Kind | What a pass means |
|---|---|---|
| Playback identity and hash | Automated | The selected artifact's bytes match the stored hash. |
| Technical file | Automated | The task succeeded, the file is verified, duration is positive, and the artifact belongs to this shot. |
| Request against the contract | Automated | The frozen acting is the approved acting, or it is marked as the historical snapshot. |
| Dialogue text | Automated | The spoken line is complete and the leading stage cue is gone. |
| Clocks | Automated | Picture, speech, and lip-sync agree under the existing tolerance. A missing window is not filled in. |
| Review provenance | Automated | The decision names this artifact id and this hash. |
| Action and required props | Human | Someone saw the action and the prop. A missing prop attestation fails only after a review names the file. |
| Character identity and continuity | Human | Someone compared Minh, Linh, wardrobe, and the room with the neighboring shot. |
| Motion, held poses, repeated poses | Human | Someone watched the motion. The classifier has no frame evidence. |
| Mouth during speech and during silence | Human | Someone placed the mouth on the speech interval and on the silent interval. Duration math does not do that. |

`AutomatedVisualProof` is false on every outcome, including the isolated shot whose verdict is ready. A ready verdict there means the clocks matched and a review record named the file. It does not mean the picture was seen.

## 5. Is SH02 ready for a controlled generation?

**BLOCKED.** No permit was issued. No job was submitted. Quota was not read.

The proposed next request, from the approved contract rather than the historical snapshot:

- Shot: `EP01-SC01-SH02`
- Acting: Minh hesitates, sets the bag on the chair, unzips it, takes out the folded test, hands it to Linh with both hands, and lowers his gaze. The prop clause names the bag and the red score.
- Spoken line, on the voice draft only: `Dạ… nay con làm bài không tốt.`
- Video prompt: the story, that acting, and the visual contract joined by the existing Veo prompt. The spoken line is not in that prompt. Confirmed by the gate test.
- Provider and model already selected for this shot: `google` / `veo-3.1`. Catalog quote for the default video-only 720p count: **0.20 USD**. Duration is not a multiplier in that catalog. The quote was read from the local pricing table dated 2026-09-22. It is not an account balance.
- ElevenLabs `eleven_v3` and Fal `sync-lipsync-v3` amounts were not in that quote and were not looked up live.

Duration constraint, unchanged:

| Picture the desk can send | Speech 2.534s, no approved window |
|---|---|
| Veo 4 | Fails. 4 − 2.534 is greater than 0.75. |
| Veo 6 | Fails. |
| Veo 8 | Fails. |

A later attempt still needs all of the following, each from a person, before any provider call:

1. A written decision on the current file: artifact `58297402-e252-45e8-8d33-c7414749a297`, hash `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`. This pass did not write it.
2. An issued `google` / `veo-3.1` permit for that shot. The prior closure found only a consumed permit. This pass did not re-read the permit table and did not create one.
3. A picture length the model can hold that also passes the clock. That requires either a Veo duration within 0.75 seconds of the real speech, which 4, 6, and 8 are not, or a dialogue interval that already exists in an approved source. None is on the contract. This pass did not add one.
4. The quality bar below. A new file does not inherit any earlier tick.

PASS for that future file requires, together:

- The visible action includes the hesitation, the bag on the chair, the unzip, the folded test handed with both hands, and the lowered gaze.
- The red score is visible. The bag is not left on the sofa.
- Minh and Linh, wardrobe, and the room stay continuous with the neighboring shot. Neither body is duplicated.
- The motion performs the beat. A held pose fails.
- The spoken line is the locked sentence, on one interval, with Minh's mouth moving on that interval and still on the intended silence.
- The artifact id and hash of the file that plays are the ones named by the review.

REJECT if any of those fail. The attempt stops there.

## 6. What would show that the next shot is better

The comparison is against the current mix, not against a test count.

| Current file | A better file |
|---|---|
| Artifact `58297402-e252-45e8-8d33-c7414749a297`, hash `e73c85db…e92b` | A new artifact id and a new hash |
| Red score absent. Bag stays on the sofa. | A person attests the red score and the bag on the chair, on that new id |
| Speech 2.534s, lip-sync 2.5s, picture 8s, mouth open on the silent tail | Speech, lip-sync, and the declared speech interval agree within 0.75s, and the mouth is still outside that interval |
| No review names this hash | The decision is ACCEPT or REJECT on the new id and the new hash |
| Identity `ACCEPTED:MIX` | That binding remains a byte lock. It is not the quality decision |

A new hash alone is not the improvement. The improvement is a person's review of the new frames against the rows above, stored on that new id and hash.

## 7. What remains before the episode can be accepted

**BLOCKED.**

1. SH02 has a human decision on the file that plays, and that decision is not ready today.
2. SH01's next picture, when one is later approved, uses the Minh-enters contract. The current mix is not accepted.
3. SH03's locked line is completed by a person. The words are not in the source.
4. SH04, SH05, and SH06 enter Production OS on their own accepted playbacks. SH06's pixels have not been fetched.
5. The episode assembly is a new artifact whose own review names that assembly id and hash. The two existing one-shot assemblies stay as they are.

`FullEpisodeConcatPermitted` stays false until those six playbacks are production-ready. This pass did not assemble them.

## Tests

Passed:

- `ShotProductionOutcomeV1Tests` — 1 passed. The 29 checks inside cover the shaped shot, the stale hash, the tenant fence, the capable isolated shot, the closed generation gate, and the still-blocked episode.
- `famixa-short-quality-pipeline.smoke.ts` — PASS. A default timing produces no dialogue window. A director-locked timing does.

Not run, and why:

- The official SH02 live mix and assemble tests. They still require a caller tenant that already owns those rows. Supplying one would miss the NULL tenant, and attributing the rows would change tenant data.
- Browser playback. No session was opened.
- Provider generation. Not authorized.

Cost this pass: $0 provider.
