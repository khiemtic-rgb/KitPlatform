# FAMIXA REAL FILM RUN V2 — EP01 SH01 — RUNWAY ONE SHOT

Date: 2026-09-20  
Mode: IMPLEMENT → TEST → LIVE PAID ONE-SHOT → RELOAD VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: Exactly one real Runway Start. No Voice / LipSync / Mix / Assembly / Final. EP01 graph and EP99 untouched.

---

## 1. VERDICT

**PASS**

Official-film CREATE_VIDEO executed once on the already-approved Production OS path. The ISSUED permit was consumed immediately before Start. Runway `gen4_turbo` produced a physical MP4. Production OS created `VIDEO_TAKE`, bound `CURRENT:VIDEO_TAKE`, and preview resolves from that artifact. Replay did not start a second job. Reload after API restart preserved the same Request / Task / Attempt / Artifact / JobId.

STOP. Do not start Voice / LipSync / Mix in this phase.

---

## 2. Locked IDs

| Field | Value |
|---|---|
| RequestId | `5a00690b-0eed-428b-b344-90d2f1517d6c` CREATE_VIDEO COMPLETED |
| TaskId | `43b64309-01fa-46d0-a937-78044aab015b` SUCCEEDED |
| InputSnapshotId | `fea772df-bfa3-41ed-8535-8403a2a9de0e` |
| InputSnapshot hash | `1c59e3d63a5c44586ed7ed0f7467c2c8ff4ad33414ccdd91f56e7fda66172db6` |
| AttemptId | `a1673a1e-54d3-44d6-9031-cec3ed6c44b3` |
| providerJobId | `acc096dd-9fe4-42fd-bbc1-d1db1b6deaf4` |
| ProductionArtifactId | `b22370a9-6ddd-4b04-a281-c7498cc926ae` VIDEO_TAKE VERIFIED |

---

## 3. Provider / model / duration

| Field | Value |
|---|---|
| provider | **runway** |
| model | **gen4_turbo** |
| selection | `9b0dceee-ba53-4cc5-8139-ee9ba58a2011` EXPLICIT ACTIVE (unchanged) |
| duration requested | **5.0** seconds (DIRECTOR PerformanceSnapshot) |
| duration actual | **5.042** seconds |
| PerformanceSnapshot | `f596c3eb-1dd4-4a3e-9077-f7fd40776116` |
| Performance hash | `PERFORMANCE_HASH_V1:863ff9a9fc9d2757f9f02f9e4e48308402d969323953cfe9a197fa38106655f3` |

InputSnapshot froze `duration_sec=5`, `provider_id=runway`, `model_id=gen4_turbo`, the same snapshot id, and keyframe `1f4c26e0-35aa-4395-94e7-09d2b1b764b6`.

---

## 4. Start / Poll / Download

| Field | Count |
|---|---|
| Start | **1** |
| Poll | **12** |
| Download | **1** |
| Replay Start | still **1** (same providerJobId) |

Process-local HTTP audit after the live Start: `startCount = 1`, `pollCount = 12`, `downloadCount = 1`. Replay POST returned the same Request / Task / Attempt / Artifact / JobId and did not increment Start. After API restart the in-process audit reset to 0 (expected). Durable OS rows still hold the same JobId.

No other AI provider was called.

---

## 5. Physical VIDEO_TAKE

| Field | Value |
|---|---|
| MIME | `video/mp4` |
| physical file size | **1529738** bytes |
| SHA256 | `8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296` |
| storageKey | `os/a1673a1e54d344d69031cec3ed6c44b3` |
| path | `E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v\os\a1673a1e54d344d69031cec3ed6c44b3.mp4` |
| container | ftyp isom (MP4) |
| status | VERIFIED |

---

## 6. Binding CURRENT:VIDEO_TAKE

| Role | ArtifactId | Active |
|---|---|---|
| CURRENT | `b22370a9-6ddd-4b04-a281-c7498cc926ae` | true |
| CURRENT:VIDEO_TAKE | `b22370a9-6ddd-4b04-a281-c7498cc926ae` | true |
| VISIBLE | `b22370a9-6ddd-4b04-a281-c7498cc926ae` | true |
| VISIBLE:VIDEO_TAKE | `b22370a9-6ddd-4b04-a281-c7498cc926ae` | true |
| CURRENT:KEYFRAME | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` | true |

Preview source: Production OS `GET /api/content/production/shots/{shotId}/preview/media` role `VISIBLE`. Same SHA / bytes as the VIDEO_TAKE artifact. Legacy URL is not authority.

---

## 7. Permit consume

| Field | Value |
|---|---|
| permitId | `e18abea2-1d1b-4d2b-b96a-79bec0e6dc16` |
| action | CREATE_VIDEO |
| provider / model | runway / gen4_turbo |
| status | **CONSUMED** |
| consumed at | 2026-09-20 22:20:47 +07 |
| permit hash | `PERMIT_HASH_V1:57fc0a0204426466ee21c04fc19827bedce53f5f420bfdcd9602704b57e23bf9` |

Consumed exactly once, immediately before the single Runway Start. Replay after CONSUMED did not Start again.

---

## 8. Keyframe (unchanged)

| Field | Value |
|---|---|
| artifactId | `1f4c26e0-35aa-4395-94e7-09d2b1b764b6` |
| SHA | `7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9` |
| storageKey | `os-kf/8f9548b1b0744eb1a5b9295889960b9c` |
| size | 93991 bytes |
| verification | VERIFIED |

Physical bytes re-hashed before Start. No second keyframe.

---

## 9. Reload verification

API rebuilt and restarted after success.

| Check | Result |
|---|---|
| GET Production OS state | CURRENT:VIDEO_TAKE present |
| Artifact SHA / storageKey | same `8fa96f9b…e66296` / `os/a1673a1e54d344d69031cec3ed6c44b3` |
| Physical bytes | same 1529738 MP4 on disk |
| Preview from ProductionArtifact | same SHA / MIME / role VISIBLE |
| Legacy URL authority | none |
| Permit | still CONSUMED |
| Selection / keyframe / snapshot | unchanged |

---

## 10. EP01 graph

| | |
|---|---|
| before | `415fedf306ed23d1ace94a794400b440` |
| after | `415fedf306ed23d1ace94a794400b440` |
| unchanged | **true** |

Graph `wan` / `wan-2.1` not rewritten. Official selection remains Production OS only.

---

## 11. EP99

| | |
|---|---|
| before | `c7cd2516627332c2648c1509395e4a64` |
| after | `c7cd2516627332c2648c1509395e4a64` |
| unchanged | **true** |

No EP99 rows written. Other shots empty.

---

## 12. All provider calls

| Provider | Endpoint class | Count | Notes |
|---|---|---|---|
| runway | Start | 1 | job `acc096dd-9fe4-42fd-bbc1-d1db1b6deaf4` |
| runway | Poll | 12 | same job / same model |
| runway | Download | 1 | physical MP4 |
| any other AI | — | **0** | no Veo / Fal / TTS / Mix |

Regression suite after restart: `video-generation-execution` fail=0, `runwayCalled=false` (suite does not live-Start).

---

## 13. Failure evidence

**Client encoding only — not a Runway retry.**

First live POST used PowerShell `ConvertTo-Json` and corrupted Vietnamese story → HTTP 400 `$.story`. Start count remained 0. Permit remained ISSUED. No provider job was created.

Second POST used Node UTF-8 JSON (`famixa-real-film-run-v2.mjs`). That is the single Start recorded above.

No Start failure. No Poll failure. No output-verification failure. No artifact/binding failure. No automatic retry. No provider/model switch. No keyframe regeneration.

---

## 14. Safety (untouched)

- EP01 legacy graph
- EP99
- other shots
- Canon
- Visual Authority
- Visual Universe
- Voice
- LipSync
- Mix
- Assembly
- Final

---

## 15. NEXT ACTION

**STOP.**

CREATE_VIDEO one-shot is complete. Do not start Voice / LipSync / Mix in this phase. Do not call Runway again.
