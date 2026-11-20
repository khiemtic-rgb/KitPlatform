# FAMIXA Production OS — Pipeline replan V1

Phase 1+2 control plane **kept**. This is the replacement map + Vertical Slice 1 foundation. Not a bug list.

## Canonical pipeline (all actions)

```
USER INTENT → ProductionRequest → Action Contract → ProductionTask
→ Input Snapshot → Provider/Model freeze → Attempt → Adapter (HOW only)
→ Verify bytes (byteSha256) → Artifact → explicit Binding → Shot state → UI
```

Shot graph = WHAT. Production OS = WHAT WAS EXECUTED. No second orchestrator.

```
SHOT → STORY → ACTING → KEYFRAME → MOTION ─┐
                     VOICE ──────────────┘→ LIPSYNC → MIX → ACCEPTED → ASSEMBLY → FINAL
```

Downstream names **artifactId**, never “latest take”.

## Re-architecture map

| Action | UI / CTA today | Current execute | State / preview / persist | Replace with | Retain |
|---|---|---|---|---|---|
| CREATE_IMAGE | `Tạo hình` → `confirmCreateShotImage` | Gemini in SeriesTab | `keyframeDataUrl` / IDB / graph | Request/Task/Artifact KEYFRAME + `CURRENT:KEYFRAME` | Gemini adapter, canon |
| CREATE_VIDEO | `Tạo video` → `startShotMotion` → `POST series/turbo` | `compileI2vPrompt` + Veo/Runway | `turboTaskId`, `runwayAttempts`, `takeUrl`, `INPUT_CHANGED` | VS1: Request CREATE_VIDEO, freeze story/acting/KF artifact, fixture then Veo adapter | `IFamixaMotionProvider`, Veo/Runway adapters, disk store |
| CREATE_VOICE | `Nạp thoại` → `ensureShotVoice` | ElevenLabs / session blobs | `voiceAssets`, lineId-as-id | Voice artifact + `CURRENT:VOICE` | ElevenLabs adapter |
| CREATE_LIPSYNC | `Khớp môi*` → `startLipsync` → `POST series/lipsync` | Fal + freeze helpers | `lipsyncTaskId/Url`, covering take | Motion+voice **artifactIds** in snapshot | Fal adapter, speaker pin (HOW) |
| MIX | `Hoàn thiện` → `assembleOneShot` | `resolveCurrentProductionVideoSource` | assemble bytes, graph URL | Explicit parent artifactIds | Assemble service as adapter |
| ACCEPT | `Dùng video này` / `Dùng Take này` | `acceptExistingTake` / `videoApproved` | URL + flags | `AcceptArtifact(artifactId)` | — |
| REGENERATE | `Tạo lại video` → same motion handler | `forceNew` + leftover CURRENT | old take stays VISIBLE | New Task; invalidate downstream; old artifact immutable | — |
| PREVIEW | `<video>` + resolver stack | latest URL / blob / covering | session Fal blob | `GET preview` = VISIBLE only | Auth file GET by storage_key |
| FINAL | none / mix URLs | preview URLs | graph | `FilmAssemblyShot.artifactId[]` | tables 368 |

**Old authority (read-only → cut over later):** `nextShotProductionCommand`, `validateMotionCallback`, `motionCallbackRunPatch`, `acceptedTake.url`, `saveSeriesPilot`, localStorage/IDB, `deskFalPlayableForShot`, `resolveCurrentProductionVideoSource`, `compileI2vPrompt` hold-body drop.

**Keep as HOW:** `IFamixaProvider*`, registry, router, cost, Veo/Fal/Gemini/ElevenLabs clients, `series_build` as Build id, shot codes.

## VS1 foundation (this pass)

`IProductionExecutionService` is the only orchestrator. `FixtureMotionAdapter` (`IsPaid=false`) stands in for Veo. CREATE_VIDEO cannot start without a verified KEYFRAME artifact in shot scope. Preview = VISIBLE. Story change unbinds motion CURRENT/VISIBLE and marks motion tasks SUPERSEDED (`STORY_CHANGED`). Typed bind `CURRENT:KEYFRAME` / `CURRENT:VIDEO_TAKE` so still + take coexist.

UI not wired. Paid Veo not wired.
