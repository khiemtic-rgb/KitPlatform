# FAMIXA — FIRST REAL SHOT VALIDATION READINESS V1

Date: 2026-10-17. Local repository and an isolated in-memory tenant only. No provider call, no permit issued, no quota read, no Production deploy, no migration, no change to official SH02 rows, and no new media file.

This pass continues from `FAMIXA_PRODUCTION_OUTCOME_ENGINEERING_V1_REPORT.md`. It gives one human a verified way to watch the existing SH02 mix, keeps tenant playback and the click-time clock on measured artifact data, and records one readiness decision for a single controlled attempt.

## Recommendation

**NO-GO** for one controlled media attempt on `EP01-SC01-SH02`.

SH02 is not production-ready. EP01 is not production-ready. The hash check below is a byte identity. It is not a visual acceptance of the file.

## Verdict

| Area | Status |
|---|---|
| Safe review of the existing SH02 mix | READY FOR A PERSON |
| Tenant-owned playback of a correctly attributed artifact | PASS on isolated data |
| Click-time lip-sync clock | PASS WITH GAPS |
| Authoritative readiness record | PASS |
| Visual acceptance stays human-controlled | PASS |
| SH02 timing can support Veo 4, 6, or 8 | BLOCKED |
| One controlled media attempt | NO-GO |
| EP01 episode acceptance | BLOCKED |

## 1. Safe review of the existing SH02 mix

Read-only check on 2026-10-17. The file was not rewritten. The official rows were not read again and were not updated.

| Field | Value |
|---|---|
| Artifact ID | `58297402-e252-45e8-8d33-c7414749a297` |
| Role | `MIX` |
| Storage key | `os/ff28d642765942839894f89bfbeb897c` |
| File | `src/KitPlatform.Api/App_Data/kit-video-i2v/os/ff28d642765942839894f89bfbeb897c.mp4` |
| Size | `1423715` bytes |
| SHA-256 of the file bytes | `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b` |

The size and the hash match the identity already on record. This pass did not open the picture and did not write ACCEPT or REJECT.

The tenant preview cannot serve this file. The build, the mix, and the bindings remain unattributed, and the storage key is not under a tenant prefix. Supplying a caller or backfilling `tenant_id` would change tenant data. This pass did neither.

A person reviews this file as follows:

1. Confirm the path, the size `1423715`, and the SHA-256 above before watching. A different hash is a different file.
2. Open that local mp4 in a player on this machine. Do not use `GET /api/content/production/shots/{buildId}/EP01-SC01-SH02/preview` for this artifact. That route keeps the tenant filter and will not return these bytes.
3. Watch the picture. The earlier frame notes still say the red score is absent and the open mouth sits on the silent tail. Those notes are not a decision from this pass.
4. If a decision is recorded, the person writes it. The record is `reviewedPlaybackId` `os:58297402-e252-45e8-8d33-c7414749a297`, `reviewedContentHash` `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`, and `decision` `ACCEPT` or `REJECT`.
5. The shot card is not the place to record that decision for this file. The preview does not resolve to this artifact, and the card now refuses to stamp ACCEPT from checkboxes. See §5.

## 2. Tenant-owned playback

**PASS** for an artifact that already belongs to the caller tenant. **BLOCKED** for the official SH02 file, for the reason in §1.

The existing store path is unchanged. `ShotProductionOutcomeV1.ReadAsync` asks `OfficialVideoAuthorityBridgeV1` for the playback, then accepts the bytes only when their SHA-256 equals the selected artifact. A second tenant receives no stages and cannot read the storage key. The isolated run on build `cccccccc-3333-4333-8333-0000000000c3`, tenant `aaaaaaaa-1111-4111-8111-0000000000a1`, confirmed that again, including a replaced payload that no longer matched the hash.

What a correctly attributed playback requires, and already does:

- The series build, the artifact, and the binding share the caller's non-null `tenant_id`.
- The storage key is under that tenant prefix. `ProductionTenantBoundary.OwnsStorageKey` stays in front of the file read.
- The playback order remains verified `CURRENT:MIX`, then `CURRENT:LIPSYNC`, then `ACCEPTED:VIDEO_TAKE`.
- The returned bytes hash to the artifact named by that selection.

The planted bytes are local test payloads. They are not the official mix and they are not a visual proof. No browser session was opened. Ports were not started.

## 3. Click-time lip-sync clock

The desk click for `CREATE_LIPSYNC` and `MIX_VIDEO` no longer sends lip-sync duration `0`.

| Situation | Clock input |
|---|---|
| The loaded preview is the current `LIPSYNC` artifact and `durationSec` is positive | That duration |
| A current lip-sync artifact exists, and the preview is a mix, a video take, or has no positive duration | `LIPSYNC_DURATION_MISSING`. The click stops. |
| `MIX_VIDEO` and no measured lip-sync duration | `LIPSYNC_DURATION_MISSING`. The click stops. |
| First `CREATE_LIPSYNC`, no current lip-sync artifact | Speech and picture are still checked. No lip-sync length is invented. |

A measured `2.5` against picture `8` and speech `2.534` still returns `SPEECH_DOES_NOT_COVER_PLAYBACK` and `MIX_CLOCK_SPLIT`. The tolerance remains `0.75` seconds. A mix duration of `8` is not reused as the lip-sync duration.

The shot-state payload still has no duration on the lip-sync node. The desk therefore uses the duration already present on the preview response, and only when that preview is the current lip-sync artifact. When the playing file is the mix, the click reports the missing measurement instead of treating it as zero. The server outcome reader continues to use the artifact row's own `DurationSec`.

The official SH02 lip-sync entry still stops on the known `2.534` / `8` / `2.5` clocks before a request. This pass did not submit that request.

## 4. Authoritative readiness

`ShotProductionOutcomeV1.Readiness` is the one record. It keeps these facts apart:

| Field | Meaning |
|---|---|
| Contract present | The approved EP01 acting is on the outcome. |
| Frozen request present | The stored video snapshot is still the historical request. |
| Current artifacts present | The tenant store returned the current stage chain. |
| Clocks pass | Picture, speech, and lip-sync agree under the existing rule. No window is added. |
| Playback hash match | The bytes equal the selected artifact. |
| Human review status | `NOT_RECORDED`, `RECORDED`, or `OTHER_FILE`. |
| Technical valid | The file, the request check, the dialogue text, the clocks, and the hash all pass together. |
| Visual acceptance | Always false from this reader. |
| Controlled attempt ready | The generation gate. It stays false for SH02. |

On the isolated SH02-shaped shot, the hash matches and the clocks fail. Technical validity is false. Review status is `NOT_RECORDED`. Visual acceptance is false. The controlled attempt is false.

A review whose hash differs is `OTHER_FILE`. A review that names the same hash and still fails the mouth and the missing red-score prop is `RECORDED`. That status does not set visual acceptance and does not clear the clock.

The isolated shot `ISO-SC01-SH01` can be technically valid with a review record on its own mix. Visual acceptance on that record stays false. The SH02 attempt stays closed. `AutomatedVisualProof` stays false. EP01 stays unready.

## 5. Visual acceptance

Completed checkboxes no longer write `decision: ACCEPT`.

`bindShotChecklist` stores the playback id and, for an OS file, the hash, and leaves `decision` empty. `qualityReviewAccepted` still requires `decision === 'ACCEPT'` plus the ticks plus the same id and hash, so a finished checklist is not an acceptance.

The card now has two separate actions, **Chấp nhận tệp này** and **Từ chối tệp này**. Each one calls `explicitArtifactDecision`. For a production shot code, the target is the resolved OS artifact only. If that preview has not resolved, the action stops with `REVIEW_TARGET_MISSING` and writes nothing. An OS decision without a hash stops with `REVIEW_HASH_MISSING`.

Those buttons can record a decision only after the tenant preview has resolved to an artifact id and a hash. They cannot review `58297402-e252-45e8-8d33-c7414749a297`, because that preview does not resolve. The procedure in §1 remains the path for that file.

## 6. SH02 timing

The approved sources contain no dialogue interval.

| Source | What it says about time |
|---|---|
| Locked story | The stage direction in parentheses. No start and no end. |
| Approved acting | The red-score action. No interval. |
| Locked spoken line | `Dạ… nay con làm bài không tốt.` The measured voice length on the existing file is `2.534` seconds. The script does not state a window. |
| `computeShotTiming` | A default lead-in of `0.2` seconds plus the measured voice. `source` is not `DIRECTOR`. |
| `approvedDialogueIntervals` | Returns a window only when `source` is `DIRECTOR` and the end is after the start. |

A computed lead-in is not a dialogue window. This pass did not lock one and did not change the `0.75` second tolerance.

With no approved window, speech must cover the picture within `0.75` seconds. The picture would have to be at most `2.534 + 0.75 = 3.284` seconds. The desk map for the selected model sends only Veo `4`, `6`, or `8` (`veoMappedDurationSec`).

| Picture the desk can send | Against speech `2.534` and no approved window |
|---|---|
| Veo 4 | Fails. `4 − 2.534` is greater than `0.75`. |
| Veo 6 | Fails. |
| Veo 8 | Fails. |

Options that stay inside the approved script and the existing provider constraint:

1. **Do not generate.** This is the option the current contract and the Veo duration map support. The attempt stops before a provider request.
2. **A picture length the selected model can hold that is within `0.75` seconds of the measured `2.534` second line.** Veo `4`, `6`, and `8` are not that length. The desk has no other duration for `google` / `veo-3.1`.
3. **A dialogue interval that is already written in an approved source, then locked by a person with `source = DIRECTOR`.** The story, the acting, and the locked line do not contain one. This pass did not add one. Until a person can point at an interval that already exists in an approved source, this option is not available.

Lengthening the locked sentence, or treating the `0.2` second lead-in as the window, is not one of these options.

## 7. Controlled attempt

**NO-GO.**

No permit was issued. No job was submitted. Quota was not read. The generation gate remains closed: the current file has no human decision from this pass, no issued `google` / `veo-3.1` permit was created here, and Veo `4`, `6`, and `8` each fail the undeclared-window clock.

### Costs, from the local catalog only

| Line | Catalog result | What it is |
|---|---|---|
| `google` / `veo-3.1`, video-only 720p, one count | **0.20 USD**, kind `Known` | Official table dated 2026-09-22. Duration is not a multiplier. Not an account balance. |
| `elevenlabs` / `eleven_v3` | Amount **unknown**. Kind `Unknown`. | No row in the local catalog. Not looked up live. |
| `fal` / `sync-lipsync-v3` | **0.67 USD** estimate | Local rate `8` USD per minute, minimum billable `5` seconds, so a `2.5` second output is quoted as `5` seconds. Kind `Estimate`. Not an invoice and not a live price. |

Provider cost this pass: $0.

### What a later file would still have to show

A person would have to see, on the new artifact id and the new hash:

- Minh hesitates, sets the bag on the chair, unzips it, takes out the folded test, hands it to Linh with both hands, and lowers his gaze.
- The red score is visible. The bag is on the chair.
- Minh, Linh, wardrobe, and the room stay continuous with the neighboring shot.
- The motion performs the beat.
- The spoken line is the locked sentence, on one interval, with the mouth moving on that interval and still on the intended silence.
- The review names that new id and that new hash.

This pass did not produce that file and did not judge the current one.

## Human decisions required next

1. Watch `src/KitPlatform.Api/App_Data/kit-video-i2v/os/ff28d642765942839894f89bfbeb897c.mp4` after confirming size `1423715` and SHA-256 `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`. Write `ACCEPT` or `REJECT` for artifact `58297402-e252-45e8-8d33-c7414749a297` and that hash. Do not record it through the checkbox card.
2. Leave the provider request unsent. A later approval still has to choose a picture length the model can hold that passes the `0.75` second rule. Veo `4`, `6`, and `8` do not. No approved dialogue interval is in the script.
3. A later attempt, after those two decisions, still needs its own issued `google` / `veo-3.1` permit. The known video quote is `0.20` USD. The ElevenLabs amount is unknown. The Fal amount in the local table is an estimate of `0.67` USD at the five-second minimum.

## Tests

Passed:

- `ShotProductionOutcomeV1Tests` — 1 passed. The checks cover the shaped shot, the stale hash, the tenant fence, the capable isolated shot, the closed generation gate, the separated readiness record, and the local cost lines.
- `famixa-short-quality-pipeline.smoke.ts` — PASS. A measured lip-sync duration is used. A missing duration is `LIPSYNC_DURATION_MISSING`. A mix duration is not reused. A first lip-sync is not given a zero length.
- `famixa-short-acceptance.smoke.ts` — PASS. Completed ticks do not become ACCEPT. An unresolved OS shot cannot be accepted. ACCEPT and REJECT require an explicit decision on the artifact id and the hash.

Not run, and why:

- The official SH02 live mix and assemble tests. They still require a caller tenant that already owns those rows. This pass did not attribute them.
- Browser playback. No admin session was opened, and the official file was not played.
- Provider generation. Not authorized.

Cost this pass: $0 provider.
