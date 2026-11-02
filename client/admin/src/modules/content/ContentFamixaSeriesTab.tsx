import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Collapse,
  Drawer,
  Input,
  InputNumber,
  Modal,
  Segmented,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  CopyOutlined,
  DeleteOutlined,
  DownloadOutlined,
  FolderOpenOutlined,
  LinkOutlined,
  LockOutlined,
  PlusOutlined,
  ReloadOutlined,
  SoundOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { Link, useSearchParams } from 'react-router-dom';
import './content-famixa-studio.css';
import { apiErrorMessage, deskToastContent, isUnauthorizedError } from '@/shared/api/api-error';
import { ContentKitVideoGoldenShotCard } from './ContentKitVideoGoldenShotCard';
import { ContentFamixaMinhDnaCard } from './ContentFamixaMinhDnaCard';
import { ContentKitVideoMasterReferenceCard } from './ContentKitVideoMasterReferenceCard';
import { ContentKitVideoIdentityTestCard } from './ContentKitVideoIdentityTestCard';
import { ContentKitVideoIdentityStressCard } from './ContentKitVideoIdentityStressCard';
import { ContentKitVideoMasterReviewCard } from './ContentKitVideoMasterReviewCard';
import { ContentKitVideoCharacterDnaCard } from './ContentKitVideoCharacterDnaCard';
import { ContentKitVideoProductionReferencePackCard } from './ContentKitVideoProductionReferencePackCard';
import { ContentKitVideoIdentityGovernanceCard } from './ContentKitVideoIdentityGovernanceCard';
import { ContentKitVideoProductionShotCard } from './ContentKitVideoProductionShotCard';
import { ContentKitVideoProductionShotContractCard } from './ContentKitVideoProductionShotContractCard';
import { ContentKitVideoProductionPromptCompilerCard } from './ContentKitVideoProductionPromptCompilerCard';
import { ContentKitVideoImageGenerationContractCard } from './ContentKitVideoImageGenerationContractCard';
import { ContentKitVideoImageGenerationExecutionCard } from './ContentKitVideoImageGenerationExecutionCard';
import { ContentKitVideoImageDirectorReviewCard } from './ContentKitVideoImageDirectorReviewCard';
import { ContentKitVideoProductionVideoContractCard } from './ContentKitVideoProductionVideoContractCard';
import { ContentKitVideoVideoGenerationExecutionCard } from './ContentKitVideoVideoGenerationExecutionCard';
import { ContentFamixaProductionOverview } from './ContentFamixaProductionOverview';
import {
  famixaMotionEngineReady,
  loadFamixaMotionEngine,
  motionProviderFromEngine,
  type FamixaMotionEngine,
} from './famixa-motion-engine';
import {
  applyDirectorVideoModelSelection,
  bindVideoTakeProvenance,
  directorVideoSelectionFromRow,
  hydrateDirectorVideoSelection,
  motionEngineFromProviderModel,
  productionAiSelectionHttpStatus,
  productionScopeFromShotCode,
  providerModelFromEngine,
  revertDirectorVideoSelectionAfterPostFailure,
  type DirectorVideoModelDisplay,
} from './famixa-director-video-model-display';
import {
  directorTimingFromPreview,
  directorTimingFromSnapshot,
  explicitDirectorSpeechInterval,
  isAuthoritativeDirectorTiming,
  type DirectorProductionDurationSec,
  type DirectorProductionTiming,
  type DirectorSpeechInterval,
} from './famixa-director-timing';
import { qualityTierOf, readQualityTier } from './famixa-quality-tier';
import {
  formatProductionActionReason,
  EPISODE_ASSEMBLY_ORDER,
  EPISODE_ASSEMBLY_SHOT_CODE,
  mayExecuteProductionAction,
  mayAuthorizeTenantEp01Image,
  mayMutateProductionOs,
  mayPostProductionEpisode,
  productionNodeArtifactId,
  resolveCurrentVoiceArtifactId,
  productionOsBuildIdForShot,
  productionOsDeskShots,
  productionOsIdempotencyKey,
  productionOsLegacyPathClosed,
  productionOsLegacyRefuseNote,
  isProductionOsTestShot,
  resolveShotWorkspaceIdentity,
  formatShotIdentityError,
  looksLikeProductionShotCode,
  prepareOfficialVoiceIngress,
  prepareOfficialLipsyncIngress,
  VIDEO_TAKE_CURRENT_INPUT,
  VOICE_PRODUCTION_OS_NOT_READY,
  LIPSYNC_PRODUCTION_OS_NOT_READY,
  OFFICIAL_SH02_LINE_ID,
  OFFICIAL_SH02_SHOT_CODE,
  OFFICIAL_SH02_VIDEO_TAKE_ID,
  OFFICIAL_SH02_VOICE_ARTIFACT_ID,
  PRODUCTION_OS_CTA,
  PRODUCTION_OS_TEST_SCOPE_ID,
} from './production-os-action-authority';
import { ACCEPT_EXISTING_NON_AUTHORITY_NOTE, acceptExistingAuthorityEffect } from './famixa-accept-existing-authority';
import { approvedDialogueIntervals, clickTimeLipsyncClock, currentLipsyncMeasurement, episodeAssemblyNotice, futureDeskActing, futureDeskStory, directorBlockerLines, productionClockBlockers, voiceDraftForRequest } from './famixa-short-quality-pipeline';
import { osPreviewIdentityFromMeta, previewBytesMatch, type OsPlaybackIdentity } from './famixa-short-acceptance';
import { isVeoMotionTaskId, veoAssembleDurationSec, veoMappedDurationSec, veoSpeechOverflowReason } from './famixa-veo-take-playback';
import {
  canStampFalLipsyncFinal,
  isFalLipsyncTaskId,
  isFalPlaybackSrc,
  playableFalLipsyncSrc,
} from './famixa-fal-lipsync-playback';
import {
  ContentFamixaShotProductionPanel,
  applyProductionStamps,
  computeInputFingerprints,
  ensureShotVoiceAssets,
  asPlayableVideoBlob,
  finalArtifactIdentityOf,
  loadFinalArtifact,
  loadFinalBlob,
  playableCurrentFinalBlob,
  resolveCurrentProductionVideoSource,
  oneShotAssembleBody,
  saveFinalBlob,
  snapshotOfShot,
  nextShotProductionCommand,
  episodeFinishReady,
  stampAssemble,
  studioLipsyncSendOpts,
  detachAvAfterPictureChange,
  actingBeatFingerprintOf,
  adaptMotionAttempts,
  currentPictureInputOf,
  createLipSyncAttemptProvenance,
  createIdbVoicePhysicalStore,
  freezeLipSyncExecutionInput,
  freezeLipSyncInput,
  buildLipSyncProviderHowRequest,
  loadFrozenVoicePhysicalBytes,
  mintVoicePhysicalArtifactId,
  resolveLipSyncRemakeSources,
  resolveStoredVoicePhysicalSource,
  saveVoicePhysicalArtifact,
  voicePhysicalStorageKey,
  stampLipSyncAttemptProviderRequest,
  submitLipSyncFrozenExecution,
  voicePhysicalIdentityOf,
  freezeMixInput,
  freezeMotionInput,
  hasProductionTakeLineage,
  motionAttemptId,
  motionCallbackRunPatch,
  nextPictureRevisionId,
  validateMotionCallback,
} from './ContentFamixaShotProduction';
import {
  ContentFamixaBuildFinishBoard,
  ContentFamixaBuildImageBoard,
  ContentFamixaBuildPublishBoard,
  ContentFamixaBuildSceneList,
  ContentFamixaBuildVideoBoard,
  ContentFamixaBuildVoiceBoard,
  ContentFamixaSharedProductionNote,
} from './ContentFamixaBuildBoards';
import {
  ContentFamixaCharacterBoard,
  ContentFamixaImageBoard,
  ContentFamixaSceneList,
  ContentFamixaVideoBoard,
} from './ContentFamixaProductionBoards';
import { ContentFamixaDirectorShortDesk } from './ContentFamixaDirectorShortDesk';
import { ContentFamixaDirectorStoryDesk } from './ContentFamixaDirectorStoryDesk';
import { ContentFamixaScriptDesk } from './ContentFamixaScriptDesk';
import { ContentFamixaSeriesDesk } from './ContentFamixaSeriesDesk';
import { ContentFamixaSeriesWorkspace } from './ContentFamixaSeriesWorkspace';
import {
  directorShotAttention,
  directorShotLaneMarks,
  directorShotOpenLabel,
  directorTabOf,
  nextDirectorStoryAction,
} from './kit-video-director-nav';
import { ContentKitVideoDirectorProductionWorkspace } from './ContentKitVideoDirectorProductionWorkspace';
import {
  deriveSeriesTrack,
  directorPictureHoldCopy,
  mergeCanonicalShotQa,
  nextWorkCopy,
  type ProductionUserMode,
  type SeriesStaffTab,
} from './kit-video-production-ux';
import {
  applySceneCharacterRefs,
  countScenesAndShots,
  deriveProductionProgress,
  episodeStoryLine,
  resolveSceneCharacterRefs,
} from './kit-video-production-workflow';
import { buildSceneViews } from './kit-video-scene-workspace';
import { ContentFilmProductionDesk } from './ContentFilmProductionDesk';
import { FILM_SERIES_DESK_STEPS } from './film-production-orchestrator';
import { characterDeskForFilm, reopenFilmSurface, usesFilmScriptDesk } from './video-pipeline';
import { ContentKitVideoEngineCard } from './ContentKitVideoEngineCard';
import { ContentKitVideoVisualSystemCard } from './ContentKitVideoVisualSystemCard';
import {
  draftContentSeriesScript,
  fetchContentBrands,
  fetchContentSeriesPilot,
  fetchContentSeriesVoices,
  fetchContentSettings,
  generateContentSeriesStill,
  qaContentSeriesStill,
  fetchContentSeriesFalVideo,
  getContentSeriesTurbo,
  startContentSeriesLipsync,
  previewContentSeriesTts,
  putContentSeriesPilot,
  fetchContentSeriesBuilds,
  fetchProductionProgressList,
  fetchProductionProgress,
  type ProductionProgressRow,
  fetchContentSeriesBuild,
  putContentSeriesBuild,
  deleteContentSeriesBuild,
  fetchCharacterLibrary,
  fetchFamixaCharacters,
  fetchCharacterStudioList,
  fetchProjectVisualStyle,
  characterStudioReferenceImageUrl,
  characterStudioMasterImageUrl,
  guardFamixaCharacters,
  approveFamixaCharacter,
  lockFamixaCharacter,
  unlockFamixaCharacter,
  createFamixaCharacter,
  type ContentSeriesBuildSummary,
  assembleContentSeriesCut,
  createProductionRequest,
  fetchProductionPreviewMedia,
  fetchProductionKeyframeMedia,
  getProductionKeyframe,
  resolveVisualShot,
  readVisualShotBinding,
  fetchVisualAssetImage,
  approveVisualShotBinding,
  fetchProductionVoiceMedia,
  getProductionCurrentVoice,
  materializeVoiceProductionIngress,
  materializeLipsyncProductionIngress,
  getProductionPreview,
  getAuthoritativeProductionVideo,
  getCreateVideoInputPreview,
  getProductionAiSelection,
  getProductionShotActions,
  getProductionShotState,
  materializeProductionDirectorTiming,
  upsertProductionAiSelection,
  type ProductionShotActions,
  type ProductionShotState,
  type AuthoritativeVideoTakeMeta,
  type ProductionVoiceArtifactMeta,
  startContentSeriesTurbo,
  probeContentSeriesTake,
  type CharacterLibraryViewRow,
  type CharacterStudioRow,
  type ProjectVisualStyleRow,
  type ContentBrand,
  type ContentSeriesVoice,
  fetchKitVideoProductionShots,
} from '@/shared/api/content.api';
import {
  afterVisualSearch,
  buildShotVisualDemand,
  interpretVisualResolve,
  normalizeFilmBrand,
  type VisualDeskView,
} from './visual-studio-still';
import { fetchTenantAiConfig } from '@/shared/api/tenant-ai-config.api';
import { providerKeyFlagsFromSources } from './famixa-tenant-provider-keys';
import { productionKeyframeIsDisplayable, productionPictureBlockCopy } from './famixa-production-still';
import { NovixaShortStrategyDesk } from './NovixaShortStrategyDesk';
import {
  backendAcceptedStrategy,
  isConfirmedStaticVoiceOver,
  strategyClaim,
  strategyEngaged,
} from './novixa-short-strategy';
import {
  CONTINUITY_GATES,
  FAMIXA_PACK_SKELETON,
  SERIES_STATUS_LABEL,
  previousLockedShot,
  previousKeyframeShot,
  shotHasValidAction,
  bindShotToMemory,
  pruneEmptyShots,
  shotPlanDensity,
  groupShotsByBeat,
  primeLongShotsOnScriptLock,
  applyShotLockToGraph,
  compileI2vPrompt,
  outputAspectOf,
  sceneHasKeyframe,
  formatSeriesVideoContext,
  pickFamixaBrand,
  lockFromGraph,
  voiceCuesForShot,
  scriptListenCues,
  ensurePilotGraph,
  ensureScriptFollowsVoice,
  PILOT_SCHEMA,
  studioI2vPrecheck,
  i2vActionOf,
  studioShotCode,
  removeSceneShots,
  insertShortClip,
  allLongShotsLocked,
  approvedShortCount,
  canProduceShot,
  canLockScript,
  canConfirmStaffScript,
  canLockCast,
  lockCast,
  canOpenStudio,
  canWorkScene,
  canWorkShorts,
  sceneBlockReason,
  studioFallbackPane,
  creditSum,
  runwaySpentSum,
  episodeCodeOf,
  episodeShots,
  FAMIXA_SERIES_CODE,
  hasSeriesGraph,
  emptyPilot,
  loadSeriesPilot,
  localFileRef,
  mergeRemotePilot,
  linesForScene,
  rebindVoicePhysicalGraphRef,
  replaceStoryFromParse,
  refreshSpokenLinesFromPack,
  newRoleRow,
  newStillRow,
  packForShotEdit,
  parseFamixaPack,
  wrapStaffScriptForParse,
  preflightTurboSend,
  reviewComplete,
  rolesReady,
  applyStillFromCanon,
  characterOfRole,
  characterCanonReady,
  canonDisplayOf,
  canonImageOf,
  hydratePilotCanon,
  hydratePilotKeyframes,
  bindPictureHashOnRuns,
  withRunPixels,
  seriesSceneStillPrompt,
  effectiveShotAction,
  looksLikePackHeading,
  roleCanonReady,
  roleVoiceReady,
  isVoiceOnlyRole,
  syncVoiceOnlyRoles,
  voiceLaneForRole,
  voicesForLane,
  voiceSoundsNorthern,
  isSouthernOrCentralVoice,
  isChildVoiceLane,
  isKidLibraryVoice,
  setCharacterCanon,
  setCharacterVoice,
  seriesCanonHint,
  saveSeriesPilot,
  slimPilotForServer,
  slimPilotForStorage,
  shortRunOf,
  stillsForShort,
  shotRunOf,
  type FamixaSeriesShot,
  type FamixaListenCue,
  type SceneContinuityLock,
  type SeriesPilotState,
  type SeriesReviewAxis,
  type SeriesShotRun,
  type SeriesCanonRef,
} from './content-famixa-series';
import {
  applySceneKfReuses,
  buildSceneKfPlan,
  continuityPlaceHint,
  firstNewKfShot,
  kfIsApproved,
  sceneKfToGenerate,
} from './content-famixa-batch-plan';
import { mapPreviewCut, shotsInInclusiveRange } from './content-famixa-preview-cut';
import { productionShorts, setShortSeconds, canWorkV2Scene, v2SceneBlockReason, readyV2VideoShots, isLockedTemplateKf, isOperatorSuppliedKf, visualLockShot, canRetryTurboStart, shouldResumeTurboPoll, isTurboRateLimit, isTurboDailyQuota, parseRetryAfterSec, loadRunwayQuietUntil, persistRunwayQuietUntil, clearRunwayQuietUntil, nextRunwayQuietUntil, runwayQuietRemainMin, shouldResumeLipsync, lipsyncFalHasNoOutput, estimateFalLipsyncUsdForShots, lipsyncVideoUrl, parseFalLipsyncRef, takeVideoUrl, lipsyncQaReady, lipsyncSendEligible, normalizeLipsyncModel, normalizeLipsyncSyncMode, lipsyncTierOf, lipsyncTaskPrefix, lipsyncResumeTaskId, parseFalJobIdFromError, shotI2vPromptHash } from './content-famixa-prod-v2';
import { assembleVideoUrl, finalSourceBlockReason, resolveTakeUrl, stampFalFinal, stampMuteTake } from './content-famixa-final-source';
import {
  attemptToDiagRow,
  canManualRetry,
  capRunwayBatch,
  classifyRunwayFailure,
  classifyVideoPipe,
  dataUriHash,
  explainPipeError,
  inspectKfDataUri,
  inspectRunwayPayload,
  isKitPrecheckError,
  lastGenerationFail,
  kfCheckFromMeasure,
  latestAttempt,
  parseFailureCode,
  patchRunwayAttempt,
  promptHashOf,
  sameFailedInput,
  sanitizeKitPrecheck,
  stampFailedInput,
  startRunwayAttempt,
  summarizeAbDiagnostic,
} from './content-famixa-runway-pipe';
import { buildRunwayJob, promptUsedOnCurrentKf, sameRequestBlocked } from './content-runway-adapter';
import { compileRunwayPromptV1 } from './content-runway-prompt-v1';
import {
  assembleFileStem,
  buildAssembleTimeline,
  formatSrt,
  looksLikeVideoUrl,
  assembleConfirmCopy,
  assembleNeedTtsOverlay,
  completeCutBlocked,
  completeCutHolds,
  planCompleteCut,
  takeDownloadName,
  lipsyncDownloadName,
} from './content-famixa-assemble';
import { assembleMixPayload, compileMixCueSheet, normalizeMixPrefs } from './content-famixa-mix';
import {
  editorialAssembleError,
  editorialCutOf,
  editorialFileStem,
  editorialMixClips,
  editorialSourceOf,
  editorialSourceReady,
  estimateEditorialTotalSec,
  patchEditorialCut,
  patchEditorialShotTrim,
  resolveEditorialWindow,
  type EditorialCutMode,
} from './content-famixa-editorial-cut';
import { blobToBase64, recordAssembledCut, recordHardCutConcat, takeBlobFromUrl, triggerDownload } from './content-famixa-assemble-render';
import { deleteTtsScope, loadTtsBlob, loadTtsBlobAny, measureAudioSec, saveTtsBlob, ttsHydrateKeys, ttsLineKey, ttsTextKey } from './content-famixa-tts-store';
import { kfPixelsOf, loadKfPixels, rememberKfFromRuns, saveKfPixels } from './content-famixa-kf-store';
import { applyContinuityChain, buildContinuityChain } from './content-famixa-continuity-chain';
import {
  applyEditDurations,
  applyVisibleCast,
  compileShotSceneCard,
  compileShotStillMood,
  deriveSceneMaster,
  lockSceneMaster,
  peopleCountLock,
  pickShots,
  previousSceneKf,
  sceneIdOfShot,
  sceneMasterOf,
  sequentialKfIds,
  unlockSceneMaster,
  upsertSceneMaster,
  visibleFrameCast,
} from './content-famixa-scene-first';
import {
  approveBlockReason,
  parseVisionQa,
  peopleCountForSpec,
  seedQaChecks,
  remakeStillCorrection,
  shouldAttachPrevKf,
  visualQaAllowsApprove,
  type VisualSpec,
} from './content-famixa-visual-spec';
import {
  applyPictureApproveGuard,
  applyPictureGenerationGuard,
  PICTURE_DUPLICATE_COPY,
  type PictureGenerationKind,
} from './content-famixa-picture-pixel-invariant';
import {
  applySoloCast,
  compileCorrectionPrompt,
  soloCastFromNote,
  identityCanonIds,
  mergeReferencePack,
  nextShotNeedingKf,
} from './content-famixa-kf-pipeline';
import { applyDialogueMap, coverageOf, dialogueMapNeedsHeal, linesForShot, lipsyncNeedsSpeakerPick, lipsyncSpeakerMismatch, multiSpeakerBlock } from './content-famixa-dialogue-map';
import { clearTwoFaceLipsyncMixOnly, leftoverTwoFaceMixOnly } from './famixa-lipsync-two-face-mix';
import {
  assertLipSyncSpeakerTargetReady,
  canMutateLiveSpeakerTarget,
  isSpeakerFaceBandMark,
  lipsyncSpeakerCharacterIdFromFaceMark,
  lipsyncSpeakerPickOf,
  lipsyncSpeakerSideOfFaceMark,
  shouldOmitRejectedSpeakerPin,
  LIPSYNC_SPEAKER_MARK_FRAME_NUMBER,
  stampLipsyncSpeakerPick,
} from './famixa-lipsync-speaker-target';
import { mapFrozenSpeakerToFalV3How } from './famixa-fal-v3-lipsync-speaker-how';
import { ACTING_EMOTIONS, actingTtsPerformText, actingTtsVoiceSettings, resolveLinePerformance } from './content-famixa-acting-law';
import { isChildFromBible, linePerformanceOf, patchDialoguePerformance, patchVoiceBible, stampDialoguePerformances } from './content-famixa-performance';
import {
  measureKfImage,
  prepareRunwayKf,
  shrinkStillDataUrl,
} from './content-famixa-still-ref';
import {
  SERIES_BUILD_STATUS_VI,
  bindBuildMedia,
  ensureBuildId,
  newSeriesBuild,
  type SeriesBuildStatus,
} from './content-famixa-build';
import { ContentFamixaCharacterUniverseCard } from './ContentFamixaCharacterUniverseCard';
import { ContentFamixaCharacterWorkspace } from './ContentFamixaCharacterWorkspace';
import {
  characterAuditOf,
  compileCharacterPromptSubset,
  parseFamixaCanon,
  resolveFamixaCharacters,
  type FamixaCharacterRecord,
} from './content-famixa-character-memory';
import { findUnregisteredNames } from './content-famixa-character-create';
import { clipOf } from './content-famixa-shot-catalog';
import {
  MIX_STAFF_EPISODE_ROLE,
  characterVoiceProfile,
  mergeSameSpeakerCues,
  shotProductionInputOf,
  videoProductionPreflight,
} from './famixa-video-audio-lipsync-pipeline';
import { dialogueEndOf, dialogueFloorOf, editorialDurationOf, performanceDurationOf, providerDurationOf } from './famixa-shot-production-timing';
import { famixaMotionBatchCost, famixaMotionGenerateCost, quoteFamixaProviderCost } from './famixa-ai-provider-cost';
import { selectFamixaProvider } from './famixa-ai-provider-routing-foundation';
import { lastFrameFromUrlOf, smoothnessOf } from './famixa-shot-smoothness-contract';
import {
  canGenerateScene,
  findStudioLockRow,
  sceneCharacterName,
  type SceneImageActionId,
  type SceneImageBlocker,
} from './kit-video-scene-image-preflight';
import {
  FAMIXA_VISUAL_MODE,
  existingScenePipelineStatus,
  lockedStudioClaim,
  resolveSceneVisualReferences,
} from './kit-video-visual-mode';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import { http } from '@/shared/api/http';
import { deleteKfScope } from './content-famixa-kf-store';
import { saveCanonPixels } from './content-famixa-canon-store';
import { famixaCanonSeedFor } from './content-famixa-canon-seed';
import { ContentFamixaStudioView, FamixaTimelinePane } from './ContentFamixaStudioView';
import { ContentFamixaStoryMemoryCard } from './ContentFamixaStoryMemoryCard';
import { inheritanceReview, needsInheritanceReview } from './content-famixa-story-memory';
import {
  canLockVoice,
  deriveVoiceScript,
  emptyVoicePreview,
  formatVoiceScriptPreview,
  looksLikeScreenplayDump,
  mergeVoiceGenerated,
  verifyVoiceGeneration,
  voiceLockBlockReason,
  voicePreviewCoversScript,
  voiceProductionReady,
} from './content-famixa-voice-script';

const REVIEW_AXES: { id: SeriesReviewAxis; label: string }[] = [
  { id: 'character', label: 'Character OK' },
  { id: 'motion', label: 'Motion OK' },
  { id: 'emotion', label: 'Emotion OK' },
  { id: 'canon', label: 'Canon OK' },
];

function shCode(st: SeriesPilotState, shot?: FamixaSeriesShot) {
  if (!shot) return 'SH';
  return studioShotCode(shot, episodeShots(st));
}

function ContinuityMemoryCard({
  lock,
  prevShot,
  onChange,
}: {
  lock: SceneContinuityLock;
  prevShot?: FamixaSeriesShot;
  onChange: (lock: SceneContinuityLock) => void;
}) {
  const patch = (p: Partial<SceneContinuityLock>) => onChange({ ...lock, ...p });
  const ro = lock.locked;
  return (
    <Card
      size="small"
      title="FAMIXA MEMORY — Continuity"
      extra={ro ? <Tag color="green">Scene lock</Tag> : <Tag>Chưa khóa</Tag>}
      style={{ marginBottom: 16 }}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="Khóa một lần cho cảnh. Shot sau chỉ nhập Action. Không chạy V03 để 'thử prompt'."
        description={
          prevShot
            ? `Baseline: ${prevShot.id} đã khóa. Shot sau kế thừa KF cảnh + wardrobe / phòng / vị trí — không viết lại.`
            : 'Khóa take shot đầu cảnh để Memory nhớ áo / ghế / phòng. Shot sau chỉ nhập Action.'
        }
      />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {lock.id}
      </Typography.Paragraph>
      {(
        [
          ['episode', 'Episode'],
          ['scene', 'Scene'],
          ['characters', 'Characters'],
          ['wardrobe', 'Wardrobe'],
          ['position', 'Position'],
          ['environment', 'Environment'],
          ['camera', 'Camera'],
          ['performance', 'Performance'],
        ] as const
      ).map(([key, label]) => (
        <div key={key} style={{ marginBottom: 8 }}>
          <Typography.Text type="secondary">{label}</Typography.Text>
          <Input.TextArea
            rows={key === 'environment' || key === 'performance' ? 2 : 1}
            disabled={ro}
            value={lock[key]}
            onChange={(e) => patch({ [key]: e.target.value })}
          />
        </div>
      ))}
      <Space wrap>
        <Button
          type="primary"
          icon={<LockOutlined />}
          disabled={ro}
          onClick={() => onChange({ ...lock, locked: true })}
        >
          Khóa Continuity
        </Button>
        {ro ? (
          <Button onClick={() => onChange({ ...lock, locked: false })}>Mở để sửa</Button>
        ) : null}
      </Space>
    </Card>
  );
}

let persistCache: { json: string; state: SeriesPilotState } | undefined;

function persist(next: SeriesPilotState, setState: (s: SeriesPilotState) => void, rememberLocal = true) {
  const seeded =
    next.buildId ||
    (next.packDraft ?? '').trim() ||
    (next.episode?.shots?.length ?? 0) > 0 ||
    (next.scenes?.length ?? 0) > 0
      ? ensureBuildId(next)
      : next;
  if (seeded.buildId) bindBuildMedia(seeded);
  const graph = ensurePilotGraph({ ...seeded, schemaVersion: PILOT_SCHEMA });
  rememberKfFromRuns(graph.runs ?? {});
  const bound = bindPictureHashOnRuns(graph);
  const slim = slimPilotForStorage(bound);
  const json = JSON.stringify(slim);
  if (persistCache?.json === json) {
    rememberKfFromRuns(bound.runs ?? {});
    const hit = withRunPixels(bound);
    persistCache = { json, state: hit };
    setState(hit);
    return hit;
  }
  if (rememberLocal) saveSeriesPilot(bound, json);
  const cloned: SeriesPilotState = {
    ...bound,
    roles: [...bound.roles],
    runs: { ...bound.runs },
    stills: [...(bound.stills ?? [])],
    episode: bound.episode ? { ...bound.episode, shots: [...bound.episode.shots] } : undefined,
    continuity: bound.continuity ? { ...bound.continuity } : undefined,
    characters: [...(bound.characters ?? [])],
    scenes: [...(bound.scenes ?? [])],
    lines: [...(bound.lines ?? [])],
    shorts: [...(bound.shorts ?? [])],
    storyMemory: bound.storyMemory ? { ...bound.storyMemory } : undefined,
    voicePreview: bound.voicePreview ? { ...bound.voicePreview } : undefined,
  };
  const withPx = withRunPixels(cloned);
  persistCache = { json, state: withPx };
  setState(withPx);
  return withPx;
}

function packSummary(parsed: ReturnType<typeof parseFamixaPack>) {
  const shotN = parsed.episode?.shots.length ?? 0;
  const shortN = parsed.shorts.length;
  const stillN = parsed.stills.length;
  const charN = parsed.characters.length;
  const ids = [
    ...parsed.shorts.map((s) => s.id),
    ...(parsed.episode?.shots ?? []).map((s) => s.id),
  ].filter(Boolean);
  const sceneN = parsed.scenes.length;
  const lineN = parsed.lines.length;
  const parts = [
    parsed.episode?.episode ? parsed.episode.episode : null,
    shortN ? `${shortN} short` : null,
    sceneN ? `${sceneN} cảnh` : null,
    shotN ? `${shotN} shot` : null,
    stillN ? `${stillN} hàng ảnh CHAR` : null,
    charN ? `${charN} CHAR` : null,
    lineN ? `${lineN} câu thoại` : null,
  ].filter(Boolean);
  return `${parts.join(', ') || 'đã nhận'}${ids.length ? ` · ${ids.slice(0, 4).join(', ')}` : ''}`;
}

function stripLeadingId3(buf: Uint8Array) {
  if (buf.length >= 10 && buf[0] === 0x49 && buf[1] === 0x44 && buf[2] === 0x33) {
    const size =
      ((buf[6] & 0x7f) << 21) | ((buf[7] & 0x7f) << 14) | ((buf[8] & 0x7f) << 7) | (buf[9] & 0x7f);
    return buf.subarray(Math.min(10 + size, buf.length));
  }
  return buf;
}

async function concatMp3Blobs(blobs: Blob[]) {
  const parts: BlobPart[] = [];
  for (let i = 0; i < blobs.length; i++) {
    const buf = new Uint8Array(await blobs[i]!.arrayBuffer());
    parts.push(i === 0 ? buf : stripLeadingId3(buf));
  }
  return new Blob(parts, { type: 'audio/mpeg' });
}

function slugFilePart(raw: string) {
  return (
    raw
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd')
      .replace(/Đ/g, 'D')
      .replace(/[^\w]+/g, '-')
      .replace(/^-|-$/g, '')
      .toLowerCase()
      .slice(0, 48) || 'tap'
  );
}

function fullScriptMp3Name(state: SeriesPilotState) {
  const ep = episodeCodeOf(state.episode?.episode || state.episode?.title) || 'EP01';
  const title = slugFilePart(state.episode?.title || 'bo-dung-hua-nua');
  return `famixa-${ep}-${title}.mp3`;
}

function explainTurboError(raw?: string | null) {
  const t = (raw ?? '').trim();
  if (isTurboDailyQuota(t)) {
    return 'Hết số job/ngày (không phải hết cr). Mua credit không mở khóa. Đợi cửa sổ 24h hoặc nâng tier trên dev.runwayml.com → Usage.';
  }
  if (isTurboRateLimit(t)) {
    return 'Runway 429: khóa số job/ngày hoặc đang chạy quá số song song — không phải hết cr. Mua credit không gỡ. Đợi rồi Hỏi lại · 0 cr.';
  }
  if (/insufficient|quota|payment|billing|credits?/i.test(t) && !/INTERNAL/i.test(t)) {
    return 'Runway hết credit hoặc Dev chưa thanh toán. Kiểm tra tài khoản, rồi gửi nốt clip thiếu.';
  }
  return explainPipeError(t);
}

function runwayCredits(seconds: number) {
  const quote = quoteFamixaProviderCost({ providerId: 'runway', modelId: 'gen4_turbo', quantity: seconds });
  return { sec: quote.billedQuantity, credits: quote.estimatedAmount ?? 0 };
}

const VOICE_KEY = 'kit.famixaSeries.voice';

function loadEngine(): FamixaMotionEngine {
  return loadFamixaMotionEngine();
}

function loadVoice(): 'elevenlabs' | 'f5' {
  try {
    return localStorage.getItem(VOICE_KEY) === 'f5' ? 'f5' : 'elevenlabs';
  } catch {
    return 'elevenlabs';
  }
}

function generateCost(engine: FamixaMotionEngine, seconds: number) {
  if (engine === 'veo') return { credits: null as number | null, label: 'Google Veo 3 · Vertex' };
  return famixaMotionGenerateCost(engine, seconds);
}

function clipPlaySrc(url?: string, sessionSrc?: string) {
  if (sessionSrc) return sessionSrc;
  return looksLikeVideoUrl(url) ? (url ?? '').trim() : '';
}

function clipOpenHref(url?: string) {
  const href = (url ?? '').trim();
  return /^https?:\/\//i.test(href) ? href : '';
}

function suggestedTakeName(clipId: string) {
  let compact = clipId.trim().replace(/-/g, '_');
  compact = compact.replace(/^FAMIXA_/i, '').replace(/^EP/i, 'E').replace(/_V\d+$/i, '');
  return `FAMIXA_${compact}_V01.mp4`;
}

function ClipWatch({
  clipId,
  url,
  path,
  sessionSrc,
  history = [],
  onUrl,
  onPath,
  onFile,
}: {
  clipId: string;
  url?: string;
  path?: string;
  sessionSrc?: string;
  history?: { url: string; taskId?: string }[];
  onUrl: (value: string) => void;
  onPath: (value: string) => void;
  onFile: (file: File) => void;
}) {
  const pick = useRef<HTMLInputElement>(null);
  const play = clipPlaySrc(url, sessionSrc);
  const open = clipOpenHref(url);
  const expected = suggestedTakeName(clipId);
  return (
    <Card size="small" title="Xem thử take" style={{ marginBottom: 12 }}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="KIT không có file video. Take nằm ở Runway hoặc thư mục bạn tải về."
        description={
          <>
            Tên gợi ý từ pack: <Typography.Text copyable>{expected}</Typography.Text>
            . Take mới ghi đè link trên KIT — take trước nằm ở Downloads / Runway (thường là file V01). Bấm{' '}
            <strong>Chọn video trên máy</strong>.
          </>
        }
      />
      <input
        ref={pick}
        type="file"
        accept="video/*,.mp4,.webm,.mov,.m4v"
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onFile(file);
          e.target.value = '';
        }}
      />
      <Space wrap style={{ marginBottom: 8 }}>
        <Button type="primary" icon={<FolderOpenOutlined />} onClick={() => pick.current?.click()}>
          Chọn video trên máy
        </Button>
        {open ? (
          <Typography.Link href={open} target="_blank" rel="noreferrer">
            Mở / tải take
          </Typography.Link>
        ) : null}
      </Space>
      <Input
        size="small"
        prefix={<LinkOutlined />}
        value={url ?? ''}
        placeholder="Hoặc dán https://… (Runway share / mp4)"
        onChange={(e) => onUrl(e.target.value)}
        style={{ marginBottom: 8 }}
      />
      <Input
        size="small"
        value={path ?? ''}
        placeholder={`Đường dẫn máy — ${expected}`}
        onChange={(e) => onPath(e.target.value)}
        style={{ marginBottom: 8 }}
      />
      {history.length > 0 ? (
        <div style={{ marginBottom: 8 }}>
          <Typography.Text type="secondary">Take trước (link còn trên máy): </Typography.Text>
          <Space wrap>
            {history.map((h, i) => (
              <Button key={`${h.url}-${i}`} size="small" onClick={() => onUrl(h.url)}>
                Dùng lại take {history.length - i}
              </Button>
            ))}
          </Space>
        </div>
      ) : null}
      {play ? (
        <video src={play} controls playsInline style={{ width: '100%', maxWidth: 560, borderRadius: 8 }} />
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Chưa có take để phát. Download từ Runway rồi chọn file — không gõ path rồi Enter.
        </Typography.Paragraph>
      )}
    </Card>
  );
}

function readImageFile(file: File) {
  return new Promise<string>((resolve, reject) => {
    if (file.size > 4_000_000) {
      reject(new Error('Ảnh ≤ 4MB — lưu trên máy, không lên server.'));
      return;
    }
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.onerror = () => reject(new Error('Không đọc được ảnh'));
    reader.readAsDataURL(file);
  });
}

export function ContentFamixaSeriesTab({
  catalogCode,
  seriesLine,
  pipelineSteps,
  workflowKey,
  brandCode,
}: {
  catalogCode?: string;
  seriesLine?: string;
  pipelineSteps?: string[] | null;
  workflowKey?: string | null;
  brandCode?: string | null;
} = {}) {
  const catalog = (catalogCode || '').trim() || FAMIXA_SERIES_CODE;
  const filmLed = usesFilmScriptDesk(workflowKey);
  const characterScope = { seriesCode: catalog, workflowKey };
  const characterDesk = characterDeskForFilm(characterScope);
  const famixaCharacters = characterDesk === 'famixa';
  const deskTitle = (seriesLine || '').trim() || 'Famixa – Video giáo dục';
  const episodeSeriesTitle = (seriesLine || '').trim() || 'Famixa';
  const [searchParams, setSearchParams] = useSearchParams();
  const deepLinkDone = useRef('');
  const app = App.useApp();
  const modal = app.modal;
  const message = useMemo(() => {
    const inner = app.message;
    const error = ((content: unknown, ...rest: unknown[]) => {
      const text = deskToastContent(content);
      if (content && typeof content === 'object' && 'content' in (content as object)) {
        return inner.error({
          ...(content as { content?: unknown; key?: string; duration?: number }),
          content: text,
          key: (content as { key?: string }).key || 'famixa-desk-err',
        });
      }
      return inner.error({ content: text, key: 'famixa-desk-err' }, ...(rest as []));
    }) as typeof inner.error;
    return { ...inner, error };
  }, [app.message]);
  const [state, setState] = useState<SeriesPilotState>(emptyPilot);
  const [packText, setPackText] = useState('');
  const [activeId, setActiveId] = useState<string | undefined>();
  const [activeShortId, setActiveShortId] = useState<string | undefined>();
  const [stillOnlyActive, setStillOnlyActive] = useState(true);
  const [packResult, setPackResult] = useState<{ ok: boolean; text: string } | null>(null);
  const [builds, setBuilds] = useState<ContentSeriesBuildSummary[]>([]);
  const [catalogAuthRequired, setCatalogAuthRequired] = useState(false);
  const [catalogSettled, setCatalogSettled] = useState(false);
  const [progressById, setProgressById] = useState<Record<string, ProductionProgressRow>>({});
  const [buildBusy, setBuildBusy] = useState(false);
  const [sessionClips, setSessionClips] = useState<Record<string, string>>({});
  const [turboBusy, setTurboBusy] = useState<string | undefined>();
  const [osPreviewByShot, setOsPreviewByShot] = useState<Record<string, string>>({});
  const [osKeyframeByShot, setOsKeyframeByShot] = useState<Record<string, string>>({});
  const [osPictureBlockByShot, setOsPictureBlockByShot] = useState<Record<string, string>>({});
  const [osPreviewIdentityByShot, setOsPreviewIdentityByShot] = useState<Record<string, OsPlaybackIdentity>>({});
  const [osActionsByShot, setOsActionsByShot] = useState<Record<string, ProductionShotActions>>({});
  const [strategyAcceptedByShot, setStrategyAcceptedByShot] = useState<Record<string, boolean>>({});
  const [osStateByShot, setOsStateByShot] = useState<Record<string, ProductionShotState>>({});
  const [osAuthoritativeByShot, setOsAuthoritativeByShot] = useState<Record<string, AuthoritativeVideoTakeMeta>>({});
  const [osVoiceByShot, setOsVoiceByShot] = useState<Record<string, ProductionVoiceArtifactMeta>>({});
  const [osVoiceUrlByShot, setOsVoiceUrlByShot] = useState<Record<string, string>>({});
  const [osVideoModelByShot, setOsVideoModelByShot] = useState<Record<string, DirectorVideoModelDisplay>>({});
  const [osDirectorTimingByShot, setOsDirectorTimingByShot] = useState<Record<string, DirectorProductionTiming>>({});
  const [directorTimingBusy, setDirectorTimingBusy] = useState(false);
  const [directorSelectionBusy, setDirectorSelectionBusy] = useState(false);
  const osActionsByShotRef = useRef(osActionsByShot);
  osActionsByShotRef.current = osActionsByShot;
  const osStateByShotRef = useRef(osStateByShot);
  osStateByShotRef.current = osStateByShot;
  const osPreviewIdentityByShotRef = useRef(osPreviewIdentityByShot);
  osPreviewIdentityByShotRef.current = osPreviewIdentityByShot;
  const osVideoModelByShotRef = useRef(osVideoModelByShot);
  osVideoModelByShotRef.current = osVideoModelByShot;
  const osInflightRef = useRef<Record<string, string>>({});
  const turboBusyRef = useRef<string | undefined>(undefined);
  const [lipsyncBusy, setLipsyncBusy] = useState<string | undefined>();
  const turboLockRef = useRef(Promise.resolve());
  const turboLaunchRef = useRef(new Set<string>());
  const [runwayQuietUntil, setRunwayQuietUntil] = useState(loadRunwayQuietUntil);
  const [, setQuietTick] = useState(0);
  const [assembleBusy, setAssembleBusy] = useState(false);
  const assembleAspect = outputAspectOf(state);
  const [stillBusy, setStillBusy] = useState<string | undefined>();
  const kfInflightRef = useRef<Record<string, true>>({});
  const [memOpen, setMemOpen] = useState(false);
  const [prodView, setProdView] = useState<'overview' | 'shot'>('overview');
  const [seriesDesk, setSeriesDesk] = useState<'list' | 'workspace'>('list');
  const [openShotId, setOpenShotId] = useState<string>();
  const [prodMode, setProdMode] = useState<ProductionUserMode>('director');
  const [directorBench, setDirectorBench] = useState(false);
  const [studioShotId, setStudioShotId] = useState<string>();
  const [studioFocusTick, setStudioFocusTick] = useState(0);
  const [prodTab, setProdTab] = useState<SeriesStaffTab>('overview');
  const [imageShotId, setImageShotId] = useState<string>();
  const [videoShotId, setVideoShotId] = useState<string>();
  const [videoRestoreTick, setVideoRestoreTick] = useState(0);
  const [studioRows, setStudioRows] = useState<CharacterStudioRow[]>([]);
  const [projectStyle, setProjectStyle] = useState<ProjectVisualStyleRow>();
  const [studioFocus, setStudioFocus] = useState<{
    characterId: string;
    panel?: 'studio' | 'universe';
    returnTo?: { shotId: string; sceneLabel: string; tab: SeriesStaffTab };
  }>();
  const [gateReturn, setGateReturn] = useState<{ shotId: string; sceneLabel: string }>();
  const [prodLive, setProdLive] = useState({
    shotTotal: 0,
    imagesReviewed: 0,
    videosReviewed: 0,
    videosDone: 0,
    characterLocked: false,
    firstShotId: '',
  });
  const [proposeNote, setProposeNote] = useState<string>();
  const [studioPane, setStudioPane] = useState<'script' | 'voice' | 'shorts' | 'studio' | 'timeline' | 'advanced'>(
    'script',
  );
  const [watchClip, setWatchClip] = useState<{ title: string; url: string }>();
  const [engine, setEngine] = useState<FamixaMotionEngine>(loadEngine);
  const [voiceProvider, setVoiceProvider] = useState<'elevenlabs' | 'f5'>(loadVoice);
  const [keys, setKeys] = useState({ runway: false, fal: false, elevenLabs: false, gemini: false, veo: false });
  const keysRef = useRef(keys);
  keysRef.current = keys;
  const [settingsHydrated, setSettingsHydrated] = useState(false);
  const [voices, setVoices] = useState<ContentSeriesVoice[]>([]);
  const [voicesLoading, setVoicesLoading] = useState(true);
  const [universe, setUniverse] = useState<FamixaCharacterRecord[]>([]);
  const [universeBusy, setUniverseBusy] = useState<string | undefined>();
  const [workspaceCode, setWorkspaceCode] = useState<string | undefined>();
  const [packMode, setPackMode] = useState<'paste' | 'ai'>('paste');
  const [packSeed, setPackSeed] = useState('');
  const [draftBusy, setDraftBusy] = useState(false);
  const [famixaBrand, setFamixaBrand] = useState<ContentBrand | undefined>();
  const [filmBrandCode, setFilmBrandCode] = useState(() => normalizeFilmBrand(brandCode));
  const filmBrandRef = useRef(filmBrandCode);
  filmBrandRef.current = filmBrandCode;
  const [visualByShot, setVisualByShot] = useState<Record<string, VisualDeskView & { url?: string }>>({});
  const [visualBrandChoice, setVisualBrandChoice] = useState('');
  const visualBrandChoiceRef = useRef('');
  visualBrandChoiceRef.current = visualBrandChoice;
  const visualBoundBrandRef = useRef<Record<string, string>>({});
  const [visualBrandChoices, setVisualBrandChoices] = useState<{ code: string; name: string }[]>([]);
  const videoContext = useMemo(
    () => formatSeriesVideoContext(famixaBrand?.knowledge),
    [famixaBrand],
  );
  const stateRef = useRef(state);
  stateRef.current = state;
  const ttsUrls = useRef(new Map<string, string>());
  const ttsBlobs = useRef(new Map<string, Blob>());
  const voicePhysicalStoreRef = useRef(createIdbVoicePhysicalStore());
  const ttsSent = useRef(new Map<string, string>());
  const ttsAudio = useRef<HTMLAudioElement | null>(null);
  const ttsStop = useRef(false);
  const [ttsBusy, setTtsBusy] = useState(false);
  const [playingLineId, setPlayingLineId] = useState<string>();
  const [ttsNote, setTtsNote] = useState<string>();
  const [ttsFiles, setTtsFiles] = useState<Record<string, { url: string; fileName: string }>>({});
  const ttsFilesRef = useRef(ttsFiles);
  ttsFilesRef.current = ttsFiles;
  const [orchMixId, setOrchMixId] = useState<string>();
  const [library, setLibrary] = useState<CharacterLibraryViewRow[]>([]);
  const [cutFrom, setCutFrom] = useState<string>();
  const [cutTo, setCutTo] = useState<string>();
  const [cutPick, setCutPick] = useState<string[]>([]);
  const [ttsFull, setTtsFull] = useState<{ url: string; fileName: string }>();
  const serverSaveTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const refreshBuilds = () =>
    Promise.all([
      fetchContentSeriesBuilds(catalog),
      fetchProductionProgressList(catalog).catch(() => ({ items: [] as ProductionProgressRow[] })),
    ])
      .then(([rows, list]) => {
        setBuilds(rows);
        const map: Record<string, ProductionProgressRow> = {};
        for (const item of list.items ?? []) {
          if (item.buildId) map[item.buildId] = item;
        }
        setProgressById(map);
        setCatalogAuthRequired(false);
        setCatalogSettled(true);
      })
      .catch((error) => {
        if (isUnauthorizedError(error)) setCatalogAuthRequired(true);
        setCatalogSettled(true);
      });

  const lastSlimJson = useRef('');
  const explicitBuildRef = useRef<string | null>(null);
  const flushServerSaveRef = useRef<() => void>(() => undefined);
  const writeServerGraph = (next: SeriesPilotState) => {
    const slim = slimPilotForServer(next) as unknown as Record<string, unknown>;
    const json = JSON.stringify(slim);
    if (json === lastSlimJson.current) return;
    lastSlimJson.current = json;
    const openId = explicitBuildRef.current;
    if (openId) {
      void putContentSeriesBuild({
        id: openId,
        seriesCode: catalog,
        graph: slim,
      })
        .then(() => fetchProductionProgress(openId))
        .then((item) => setProgressById((cur) => ({ ...cur, [item.buildId]: item })))
        .catch(() => {
          if (lastSlimJson.current === json) lastSlimJson.current = '';
          message.error('Không lưu được nội dung phim.');
        });
      return;
    }
    if (next.buildId) {
      void putContentSeriesBuild({
        id: next.buildId,
        seriesCode: catalog,
        graph: slim,
      })
        .then(() => fetchProductionProgress(next.buildId!))
        .then((item) => setProgressById((cur) => ({ ...cur, [item.buildId]: item })))
        .catch(() => undefined);
    }
    void putContentSeriesPilot({
      seriesCode: catalog,
      graph: slim,
    }).catch(() => undefined);
  };
  const queueServerSave = (next: SeriesPilotState) => {
    if (serverSaveTimer.current) clearTimeout(serverSaveTimer.current);
    const delay = explicitBuildRef.current ? 400 : 2400;
    serverSaveTimer.current = setTimeout(() => writeServerGraph(next), delay);
  };
  flushServerSaveRef.current = () => {
    if (serverSaveTimer.current) clearTimeout(serverSaveTimer.current);
    writeServerGraph(stateRef.current);
  };
  const persistState = (next: SeriesPilotState, server = true) => {
    const stamped = applyProductionStamps(stateRef.current, next);
    const selectedSession = Boolean(explicitBuildRef.current);
    const cloned = persist(ensureScriptFollowsVoice(syncVoiceOnlyRoles(stamped)), setState, !selectedSession);
    stateRef.current = cloned;
    if (server) queueServerSave(cloned);
    return cloned;
  };
  const commitFilmGraph = (next: SeriesPilotState) => {
    const id = next.buildId || stateRef.current.buildId || explicitBuildRef.current;
    const saved = persistState(id ? { ...next, buildId: id } : next, false);
    if (!id) {
      persistState(saved, true);
      return;
    }
    const slim = slimPilotForServer(saved) as unknown as Record<string, unknown>;
    void putContentSeriesBuild({ id, seriesCode: catalog, graph: slim }).catch(() => {
      message.error('Không lưu được tiến độ phim.');
    });
  };
  const filmSteps = pipelineSteps?.length ? pipelineSteps : [...FILM_SERIES_DESK_STEPS];

  const setOutputAspect = (aspect: '16:9' | '9:16') => {
    const live = stateRef.current;
    if (aspect === live.outputAspect) return;
    if (sceneHasKeyframe(live)) {
      modal.confirm({
        title: `Đổi khung ${outputAspectOf(live)} → ${aspect}`,
        content:
          'Đã có KF. Đổi khung lúc này crop/pad lệch ý. Tạo lại KF + I2V theo khung mới. Take cũ không tự sửa.',
        okText: `Khóa ${aspect}`,
        cancelText: 'Giữ khung cũ',
        onOk: () => persistState({ ...stateRef.current, outputAspect: aspect }),
      });
      return;
    }
    persistState({ ...live, outputAspect: aspect });
  };

  const clearSessionTts = () => {
    ttsUrls.current.forEach((url) => URL.revokeObjectURL(url));
    ttsUrls.current.clear();
    ttsBlobs.current.clear();
    ttsSent.current.clear();
    setTtsFiles({});
  };

  const voiceIdsForLine = (line: { characterId?: string; voiceId?: string }) => {
    const ch = (stateRef.current.characters ?? []).find((c) => c.id === line.characterId);
    const role = stateRef.current.roles.find((r) => r.characterId === line.characterId);
    return [line.voiceId, ch?.voiceId, role?.voiceId].filter(Boolean) as string[];
  };

  const hydrateSessionTts = async (graph: SeriesPilotState) => {
    const seen = new Set<string>();
    const lines = [
      ...deriveVoiceScript(graph).lines,
      ...episodeShots(graph).flatMap((shot) => linesForShot(graph, shot)),
    ].filter((line) => {
      if (!line.id || seen.has(line.id)) return false;
      seen.add(line.id);
      return true;
    });
    for (const line of lines) {
      const haveBlob = ttsBlobs.current.has(line.id);
      const haveUrl = Boolean(ttsFilesRef.current[line.id]?.url || ttsUrls.current.get(line.id));
      const wantVoice = (line.voiceId || '').trim();
      const haveVoice =
        !wantVoice ||
        ttsBlobs.current.has(ttsLineKey(line.id, wantVoice)) ||
        ttsBlobs.current.has(ttsTextKey(line.text, wantVoice));
      if (haveBlob && haveUrl && haveVoice) continue;
      const blob =
        (haveBlob ? ttsBlobs.current.get(line.id) : undefined) ||
        (await loadTtsBlobAny(ttsHydrateKeys(line, voiceIdsForLine(line))));
      if (!blob) continue;
      ttsBlobs.current.set(line.id, blob);
      ttsBlobs.current.set(ttsTextKey(line.text, line.voiceId), blob);
      if (line.voiceId) ttsBlobs.current.set(ttsLineKey(line.id, line.voiceId), blob);
      let url = ttsUrls.current.get(line.id);
      if (!url) {
        url = URL.createObjectURL(blob);
        ttsUrls.current.set(line.id, url);
      }
      ttsFilesRef.current = { ...ttsFilesRef.current, [line.id]: { url, fileName: `${line.id}.mp3` } };
      setTtsFiles((m) => (m[line.id]?.url ? m : { ...m, [line.id]: { url, fileName: `${line.id}.mp3` } }));
    }
  };

  const openBuild = (id: string) => {
    setStudioShotId(undefined);
    if (id === state.buildId && explicitBuildRef.current === id) {
      setSeriesDesk('workspace');
      return Promise.resolve();
    }
    setBuildBusy(true);
    return fetchContentSeriesBuild(id)
      .then(async (row) => {
        explicitBuildRef.current = row.id;
        const remote = (row.graph && typeof row.graph === 'object' ? row.graph : {}) as SeriesPilotState;
        const live = stateRef.current;
        const liveText = live.buildId === row.id ? (live.packDraft || live.scriptDraft || '').trim() : '';
        const remoteText = (remote.packDraft || remote.scriptDraft || '').trim();
        const text = liveText || remoteText;
        clearSessionTts();
        const next = persistState(
          ensurePilotGraph({
            ...remote,
            buildId: row.id,
            schemaVersion: PILOT_SCHEMA,
            ...(text ? { packDraft: text, scriptDraft: text } : {}),
          }),
        );
        setPackText(next.packDraft || next.scriptDraft || '');
        await hydratePilotKeyframes(await hydratePilotCanon(next));
        setState((cur) => ({ ...cur, runs: { ...cur.runs } }));
        await hydrateSessionTts(stateRef.current);
        setStudioPane('script');
        setProdTab('overview');
        setProdView('overview');
        setSeriesDesk('workspace');
        void fetchProductionProgress(row.id)
          .then((item) => setProgressById((cur) => ({ ...cur, [item.buildId]: item })))
          .catch(() => undefined);
        message.success(`Đã mở ${row.episodeCode || row.title || 'bản dựng'}.`);
      })
      .catch((e) => message.error(apiErrorMessage(e, 'Không mở được bản dựng.')))
      .finally(() => setBuildBusy(false));
  };

  const createBuild = () => {
    persistState(stateRef.current);
    clearSessionTts();
    const next = persistState(newSeriesBuild(stateRef.current));
    setPackText('');
    setPackResult(null);
    setStudioPane('script');
    message.success('Bản dựng mới. Canon giữ. Dán kịch bản rồi Nhận pack.');
    void next;
  };

  const createVideoFromDesk = (input: { title: string; premise: string; note: string; episode: string; script?: string }) => {
    persistState(stateRef.current);
    clearSessionTts();
    persistState({
      ...newSeriesBuild(stateRef.current),
      packDraft: input.script || '',
      episode: {
        seriesCode: catalog,
        seriesTitle: episodeSeriesTitle,
        episode: input.episode,
        title: input.title,
        premise: input.premise,
        moral: input.note,
        ctaRule: '',
        shots: [],
      },
    });
    setPackText('');
    setPackResult(null);
    setStudioPane('script');
    setProdTab('script');
    setProdView('overview');
    setSeriesDesk('workspace');
    void refreshBuilds();
    message.success('Đã tạo video. Bắt đầu từ kịch bản.');
  };

  const removeBuild = (id: string, title: string) => {
    modal.confirm({
      title: `Xóa video «${title || 'không tên'}»?`,
      content: 'Xóa video này khỏi danh sách. Không lấy lại được. Credit đã dùng (nếu có) không hoàn.',
      okText: 'Xóa hẳn',
      okButtonProps: { danger: true },
      cancelText: 'Giữ',
      onOk: async () => {
        await deleteContentSeriesBuild(id);
        await deleteKfScope(id);
        await deleteTtsScope(id);
        if (stateRef.current.buildId === id) {
          clearSessionTts();
          persistState(newSeriesBuild(stateRef.current));
          setPackText('');
        }
        await refreshBuilds();
        message.success('Đã xóa bản dựng.');
      },
    });
  };

  const stopScriptListen = () => {
    ttsStop.current = true;
    ttsAudio.current?.pause();
    setPlayingLineId(undefined);
    setTtsBusy(false);
    setTtsNote(undefined);
  };

  const loadCueAudio = async (cue: {
    id: string;
    voiceId?: string;
    text: string;
    name: string;
    characterId?: string;
    performance?: import('./content-famixa-acting-law').LinePerformance;
  }) => {
    if (looksLikeScreenplayDump(cue.text)) {
      throw new Error('TTS chỉ nhận thoại CHAR — không gửi heading/cảnh/action.');
    }
    const voiceId = (cue.voiceId ?? '').trim();
    if (!voiceId) throw new Error(`Chưa gán Voice Canon cho ${cue.name}.`);
    if (voices.some((v) => v.voiceId === voiceId && v.cloned)) {
      throw new Error(
        `${cue.name}: giọng Instant Clone — gói ElevenLabs hiện tại không TTS được. Đổi sang giọng thư viện tiếng Việt miền Bắc.`,
      );
    }
    const ch = cue.characterId
      ? (stateRef.current.characters ?? []).find((c) => c.id === cue.characterId)
      : undefined;
    const dir = resolveLinePerformance({
      text: cue.text,
      characterId: cue.characterId,
      name: cue.name,
      performance: cue.performance ?? linePerformanceOf(stateRef.current, cue.id),
    });
    const child =
      isChildVoiceLane(voiceLaneForRole({ characterId: cue.characterId, name: cue.name, title: '' }, ch)) ||
      isChildFromBible(ch);
    const pick = voices.find((v) => v.voiceId === voiceId);
    const northern = voiceSoundsNorthern(pick) || /hanoi|ha noi|northern|mien bac/i.test(`${pick?.accent || ''} ${pick?.name || ''} ${ch?.voiceName || ''}`);
    const vs = actingTtsVoiceSettings(
      dir,
      {
        stability: ch?.voiceStability,
        similarity: ch?.voiceSimilarity,
        style: ch?.voiceStyle,
        speed: ch?.voiceSpeed,
      },
      { child, northern: child || northern },
    );
    const perform = actingTtsPerformText(cue.text, dir);
    const key = `vi-north|${voiceId}|${perform}|${dir.emotion}|${vs.stability}|${vs.speed}|${vs.style}`;
    const hit = ttsUrls.current.get(key);
    if (hit) {
      ttsSent.current.set(cue.id, cue.text);
      return hit;
    }
    const preview = await previewContentSeriesTts({
      voiceId,
      text: perform,
      publicOwnerId: pick?.publicOwnerId || undefined,
      voiceName: pick?.name || ch?.voiceName,
      accent: pick?.accent || (child ? 'northern' : undefined),
      stability: vs.stability,
      similarityBoost: vs.similarityBoost,
      style: vs.style,
      speed: vs.speed,
    });
    const blob = preview.blob;
    const url = URL.createObjectURL(blob);
    ttsUrls.current.set(key, url);
    ttsBlobs.current.set(key, blob);
    ttsBlobs.current.set(cue.id, blob);
    ttsSent.current.set(cue.id, cue.text);
    void saveTtsBlob(ttsLineKey(cue.id, voiceId), blob);
    void saveTtsBlob(ttsTextKey(cue.text, voiceId), blob);
    const physicalId = voicePhysicalIdentityOf({
      artifactId: mintVoicePhysicalArtifactId(),
      buildId: stateRef.current.buildId,
      lineId: cue.id,
      voiceId,
    });
    if (physicalId) {
      const buf = await blob.arrayBuffer();
      await saveVoicePhysicalArtifact(physicalId, new Uint8Array(buf), voicePhysicalStoreRef.current);
    }
    void measureAudioSec(blob).then((sec) => {
      if (sec <= 0) return;
      const cur = stateRef.current;
      persistState({
        ...cur,
        voiceAssets: {
          ...(cur.voiceAssets ?? {}),
          [cue.id]: {
            lineId: cue.id,
            characterId: cue.characterId,
            duration: Number(sec.toFixed(2)),
            status: 'ready',
            ...(physicalId
              ? {
                  artifactId: physicalId.artifactId,
                  storageKey: voicePhysicalStorageKey(physicalId),
                  voiceId,
                  physicalArtifactResolvable: true,
                }
              : {}),
            selectionSnapshot: preview.selectionSnapshot,
          },
        },
      });
    });
    const idx = scriptListenCues(stateRef.current).findIndex((c) => c.id === cue.id);
    const who = (cue.name || 'thoai').replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '') || 'line';
    const fileName = `famixa-${String((idx >= 0 ? idx : 0) + 1).padStart(2, '0')}-${who}.mp3`;
    setTtsFiles((m) => ({ ...m, [cue.id]: { url, fileName } }));
    return url;
  };

  const commitVoicePreview = (st = stateRef.current) => {
    const script = deriveVoiceScript(st);
    const now = [...ttsSent.current.entries()].map(([id, text]) => ({ id, text }));
    const merged = mergeVoiceGenerated(st.voicePreview?.generated, now);
    if (merged.length === 0 && voicePreviewCoversScript(script, st.voicePreview)) {
      return st.voicePreview!;
    }
    const report = merged.length ? verifyVoiceGeneration(script, merged) : emptyVoicePreview(script);
    const keep =
      report.status !== 'complete' && voicePreviewCoversScript(script, st.voicePreview)
        ? {
            ...st.voicePreview!,
            ...report,
            status: 'complete' as const,
            issues: [] as string[],
            generated: merged.length ? merged : st.voicePreview?.generated,
          }
        : report;
    persistState({
      ...st,
      voicePreview: keep,
      voiceLocked: keep.status === 'complete' ? Boolean(st.voiceLocked) : false,
    });
    return keep;
  };

  const playCueAudio = (url: string) =>
    new Promise<void>((resolve, reject) => {
      ttsAudio.current?.pause();
      const audio = new Audio(url);
      ttsAudio.current = audio;
      audio.onended = () => resolve();
      audio.onerror = () => reject(new Error('Không phát được tiếng.'));
      void audio.play().catch(reject);
    });

  const playListenCue = async (cue: FamixaListenCue) => {
    ttsStop.current = false;
    setTtsBusy(true);
    setPlayingLineId(cue.id);
    try {
      const url = await loadCueAudio(cue);
      if (ttsStop.current) return;
      commitVoicePreview();
      await playCueAudio(url);
    } catch (e) {
      message.error({ content: apiErrorMessage(e, 'Không nghe được thoại.'), key: 'famixa-desk-err' });
    } finally {
      if (!ttsStop.current) setPlayingLineId(undefined);
      setTtsBusy(false);
    }
  };

  const playWholeScript = () => {
    const script = deriveVoiceScript(stateRef.current);
    const cues = script.lines;
    const ready = cues.filter((c) => c.voiceId);
    const missing = cues.length - ready.length;
    const speakers = new Set(cues.map((c) => c.characterId));
    if (cues.length === 0) {
      message.warning('Nhận pack kịch bản rồi duyệt Parsed Story — KIT tự tách Voice Script (chỉ thoại).');
      return;
    }
    if (ready.length === 0 || missing > 0) {
      const names = [...new Set(cues.filter((c) => !c.voiceId).map((c) => c.name || c.characterId))].slice(0, 4).join(', ');
      message.warning(
        names
          ? `Gán Voice Canon cho: ${names}. Lời bình không cần ảnh — mở khóa Cast nếu đang khóa, chọn giọng nam miền Bắc, rồi Tạo Full Voice.`
          : 'Gán Voice Canon cho từng vai rồi mới Full Voice.',
      );
      return;
    }
    if (!keys.elevenLabs) {
      message.warning('Chưa có key ElevenLabs — Cấu hình AI.');
      return;
    }
    modal.confirm({
      title: 'Full Voice — chỉ thoại CHAR',
      content: `Voice Script ${script.sourceLineCount} câu · ${script.sourceCharCount} ký tự · ~${script.estimatedSec}s · ${speakers.size} giọng. Không đọc heading/action. Thiếu voice: ${missing}. ElevenLabs tính ký tự — không trừ Runway.`,
      okText: 'Tạo Full Voice',
      onOk: () => {
        ttsStop.current = false;
        setTtsBusy(true);
        void (async () => {
          const urls: (string | null)[] = [];
          const cloneSkip = new Set<string>();
          try {
            for (let i = 0; i < cues.length; i++) {
              if (ttsStop.current) return;
              const cue = cues[i]!;
              setPlayingLineId(cue.id);
              setTtsNote(`Voice Script ${i + 1}/${cues.length} · ${cue.name}`);
              if (!cue.voiceId || voices.some((v) => v.voiceId === cue.voiceId && v.cloned)) {
                if (cue.voiceId) cloneSkip.add(cue.name);
                urls.push(null);
                continue;
              }
              try {
                urls.push(await loadCueAudio(cue));
              } catch (e) {
                urls.push(null);
                message.warning(`${cue.name}: ${e instanceof Error ? e.message : 'bỏ câu này.'}`);
              }
            }
            const report = commitVoicePreview();
            if (cloneSkip.size > 0) {
              message.warning(`Bỏ giọng Instant Clone của ${[...cloneSkip].join(', ')}.`);
            }
            if (ttsStop.current) return;
            if (report.status !== 'complete') {
              message.error(`VOICE INCOMPLETE — ${report.issues[0] || 'thiếu thoại'}. Chưa được khóa, chưa I2V.`);
              return;
            }
            const playable = urls.filter(Boolean).length;
            setTtsNote(`COMPLETE · đọc ${playable} câu`);
            for (let i = 0; i < cues.length; i++) {
              if (ttsStop.current) break;
              const url = urls[i];
              if (!url) continue;
              setPlayingLineId(cues[i]!.id);
              await playCueAudio(url);
              if (ttsStop.current) break;
              await new Promise((r) => setTimeout(r, 180));
            }
            message.success(`Voice COMPLETE · ${report.generatedLineCount} câu · ${report.generatedCharCount} ký tự.`);
          } catch (e) {
            message.error({ content: apiErrorMessage(e, 'Không nghe được thoại.'), key: 'famixa-desk-err' });
          } finally {
            setPlayingLineId(undefined);
            setTtsBusy(false);
            setTtsNote(undefined);
          }
        })();
      },
    });
  };

  const downloadFullScriptMp3 = () => {
    const script = deriveVoiceScript(stateRef.current);
    const cues = script.lines;
    if (cues.length === 0) {
      message.warning('Chưa có Voice Script — Nhận pack kịch bản.');
      return;
    }
    if (!keys.elevenLabs) {
      message.warning('Chưa có key ElevenLabs — Cấu hình AI.');
      return;
    }
    modal.confirm({
      title: 'Tải MP3 Voice Script',
      content: `Ghép ${script.sourceLineCount} câu thoại (không gồm heading/action) · ${script.sourceCharCount} ký tự.`,
      okText: 'Ghép và tải',
      onOk: () => {
        ttsStop.current = false;
        setTtsBusy(true);
        void (async () => {
          try {
            const blobs: Blob[] = [];
            for (let i = 0; i < cues.length; i++) {
              if (ttsStop.current) return;
              const cue = cues[i]!;
              setPlayingLineId(cue.id);
              setTtsNote(`Voice Script ${i + 1}/${cues.length} · ${cue.name}`);
              if (!cue.voiceId || voices.some((v) => v.voiceId === cue.voiceId && v.cloned)) continue;
              await loadCueAudio(cue);
              const blob = ttsBlobs.current.get(cue.id);
              if (blob) blobs.push(blob);
            }
            const report = commitVoicePreview();
            if (ttsStop.current) return;
            if (report.status !== 'complete') {
              message.error(`VOICE INCOMPLETE — không tải file full khi còn thiếu thoại.`);
              return;
            }
            setTtsNote(`Đang ghép ${blobs.length} câu thành 1 MP3`);
            const full = await concatMp3Blobs(blobs);
            const fileName = fullScriptMp3Name(stateRef.current).replace('.mp3', '-voice-script.mp3');
            const url = URL.createObjectURL(full);
            setTtsFull((prev) => {
              if (prev?.url) URL.revokeObjectURL(prev.url);
              return { url, fileName };
            });
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            a.rel = 'noopener';
            a.click();
            message.success(`COMPLETE · ${fileName}`);
          } catch (e) {
            message.error({ content: apiErrorMessage(e, 'Không ghép được MP3 cả tập.'), key: 'famixa-desk-err' });
          } finally {
            setPlayingLineId(undefined);
            setTtsBusy(false);
            setTtsNote(undefined);
          }
        })();
      },
    });
  };

  const voiceOptionsForRole = (role: (typeof state.roles)[number], selectedId: string) => {
    const lane = voiceLaneForRole(role, characterOfRole(state, role));
    const ch = characterOfRole(state, role);
    let rows = voicesForLane(voices, lane, selectedId);
    if (!rows.length) {
      rows = voices.filter((v) => !v.cloned && !isSouthernOrCentralVoice(v));
    }
    if (!rows.length) rows = voices.filter((v) => !v.cloned);
    const options = rows.map((v) => ({
      value: v.voiceId,
      label: v.cloned ? `${v.name} · clone (gói chưa TTS được)` : v.name,
    }));
    if (selectedId && !options.some((o) => o.value === selectedId)) {
      options.unshift({
        value: selectedId,
        label: (ch?.voiceName || role.voiceName || selectedId).trim(),
      });
    }
    const filterOption = (input: string, option?: { label?: string }) => {
      const q = input.trim().toLowerCase();
      if (!q || /lời bình|loi binh|miền bắc|mien bac|giọng /.test(q)) return true;
      return String(option?.label ?? '').toLowerCase().includes(q);
    };
    return { lane, options, filterOption };
  };
  const castFrozen = Boolean(state.castLocked || state.scriptLocked);
  const ready = rolesReady(state.roles);
  const shorts = state.shorts ?? [];
  const shots = episodeShots(state);
  const deskShots = prodMode === 'director' ? productionOsDeskShots(state.buildId, shots) : shots;
  const ep = state.episode;
  const activeShort = shorts.find((s) => s.id === activeShortId) ?? shorts[0];
  const shortRun = activeShort ? shortRunOf(state, activeShort.id) : undefined;
  const active = shots.find((s) => s.id === activeId) ?? shots[0];
  const run = active ? shotRunOf(state, active) : undefined;
  const unlocked = active ? canProduceShot(state, active) : false;
  const sceneReady = allLongShotsLocked(state);
  const credits = creditSum(state);
  const spentOnRunway = runwaySpentSum(state);
  const voiceScript = useMemo(() => deriveVoiceScript(state), [state]);
  const listenCues = voiceScript.lines;
  const staffVoiceLines = useMemo(
    () =>
      shots.flatMap((s, index) =>
        linesForShot(state, s, voiceScript.lines).map((line) => ({
          shotId: s.id,
          label: `Cảnh ${String(index + 1).padStart(2, '0')}`,
          speaker: line.name || '—',
          text: line.text,
        })),
      ),
    [state, shots, voiceScript.lines],
  );

  useEffect(() => {
    if (!famixaCharacters) {
      setLibrary([]);
      return;
    }
    void fetchCharacterLibrary(characterScope)
      .then((row) => setLibrary(row.items ?? []))
      .catch(() => setLibrary([]));
  }, [famixaCharacters, catalog, workflowKey]);

  useEffect(() => {
    const loadProd = () => {
      void fetchKitVideoProductionShots()
        .then(async (bundle) => {
          const list = bundle.shots || [];
          const locked =
            Boolean(bundle.master?.lockedAt) &&
            (bundle.dna?.status || '').toUpperCase() === 'LOCKED' &&
            (bundle.pack?.status || '').toUpperCase() === 'LOCKED';
          setProdLive({
            shotTotal: list.length,
            imagesReviewed: list.filter((shot) => Boolean(shot.approvedAt)).length,
            videosReviewed: 0,
            videosDone: 0,
            characterLocked: locked,
            firstShotId: list[0]?.id || '',
          });
        })
        .catch(() => undefined);
    };
    loadProd();
    window.addEventListener('famixa-look-changed', loadProd);
    return () => window.removeEventListener('famixa-look-changed', loadProd);
  }, []);

  useEffect(() => {
    if (voiceScript.lines.length > 0) return;
    const next = refreshSpokenLinesFromPack(stateRef.current);
    if (deriveVoiceScript(next).lines.length <= voiceScript.lines.length) return;
    persistState(next, false);
  }, [state.packDraft, voiceScript.lines.length]);

  useEffect(() => {
    if (!state.shotGraphLocked) return;
    if (!dialogueMapNeedsHeal(stateRef.current)) return;
    persistState(applyDialogueMap(stateRef.current), false);
  }, [state.shotGraphLocked, voiceScript.lines.length, state.episode?.shots?.length]);
  const voiceLockHint =
    !keys.elevenLabs && state.voicePreview?.status !== 'complete'
      ? 'Chưa có key ElevenLabs — Cấu hình AI, rồi bấm Tạo Full Voice.'
      : voiceLockBlockReason(state) ?? '';

  useEffect(() => {
    const selected = (new URLSearchParams(window.location.search).get('build') || '').trim();
    if (selected) return;
    const t = window.requestAnimationFrame(() => {
      const s = loadSeriesPilot();
      bindBuildMedia(s);
      setState(s);
      setPackText(s.packDraft ?? '');
      setActiveShortId(s.shorts?.[0]?.id);
    });
    return () => window.cancelAnimationFrame(t);
  }, []);

  useEffect(() => {
    if (runwayQuietUntil <= Date.now()) return;
    const t = window.setInterval(() => setQuietTick((n) => n + 1), 15_000);
    return () => window.clearInterval(t);
  }, [runwayQuietUntil]);

  useEffect(() => {
    if (studioPane !== 'shorts') return;
    if ((state.shorts?.length ?? 0) > 0) return;
    if (!canOpenStudio(state)) return;
    setStudioPane('studio');
  }, [studioPane, state.shorts?.length, state.scriptLocked]);

  useEffect(() => {
    let cancel = false;
    const load = () => {
      void Promise.all([
        fetchContentSettings().catch(() => null),
        fetchTenantAiConfig().catch(() => []),
      ]).then(([settings, tenant]) => {
        if (cancel) return;
        const next = providerKeyFlagsFromSources(settings, tenant);
        keysRef.current = next;
        setKeys(next);
        setSettingsHydrated(true);
      });
    };
    load();
    const onVisible = () => {
      if (document.visibilityState === 'visible') load();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      cancel = true;
      document.removeEventListener('visibilitychange', onVisible);
    };
    void fetchContentBrands(true)
      .then((rows) => setFamixaBrand(pickFamixaBrand(rows)))
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    const given = normalizeFilmBrand(brandCode);
    if (given) {
      setFilmBrandCode(given);
      return;
    }
    const seriesBrand = normalizeFilmBrand(catalog);
    const seriesIsVisualBrand = seriesBrand === 'famixa' || seriesBrand === 'novixa' || seriesBrand === 'trade';
    const build = (searchParams.get('build') || state.buildId || '').trim();
    if (!build) {
      if (seriesIsVisualBrand) setFilmBrandCode(seriesBrand);
      return;
    }
    let cancel = false;
    void fetchContentSeriesBuild(build)
      .then(async (row) => {
        const brandId = (row.brandId || '').trim();
        if (!brandId) {
          if (!cancel && seriesIsVisualBrand) setFilmBrandCode(seriesBrand);
          return;
        }
        const brands = await fetchContentBrands(true);
        if (cancel) return;
        const code = normalizeFilmBrand(brands.find((brand) => brand.id === brandId)?.code);
        setFilmBrandCode(code || (seriesIsVisualBrand ? seriesBrand : ''));
      })
      .catch(() => undefined);
    return () => {
      cancel = true;
    };
  }, [brandCode, catalog, searchParams, state.buildId]);

  useEffect(() => {
    let cancel = false;
    void fetchContentBrands(true)
      .then((rows) => {
        if (cancel) return;
        const seen = new Set<string>();
        setVisualBrandChoices(rows.flatMap((row) => {
          const code = normalizeFilmBrand(row.code);
          if (!code || seen.has(code)) return [];
          seen.add(code);
          return [{ code, name: row.name || row.code }];
        }));
      })
      .catch(() => undefined);
    return () => {
      cancel = true;
    };
  }, []);

  const loadVoiceLibrary = (opts?: { force?: boolean }) => {
    if (!opts?.force) {
      try {
        const cached = sessionStorage.getItem('kit.famixa.voices.v4');
        if (cached) {
          const rows = JSON.parse(cached) as ContentSeriesVoice[];
          if (Array.isArray(rows) && rows.length && rows.some((v) => (v.previewUrl || '').trim())) {
            setVoices(rows);
            setVoicesLoading(false);
          }
        }
      } catch {
        /* ignore */
      }
    }
    setVoicesLoading(true);
    return fetchContentSeriesVoices()
      .then((rows) => {
        const next = rows ?? [];
        if (next.length) {
          setVoices(next);
          try {
            sessionStorage.setItem('kit.famixa.voices.v4', JSON.stringify(next));
          } catch {
            /* quota */
          }
        } else if (opts?.force) {
          setVoices([]);
          message.warning('API không trả giọng — restart API :5290 hoặc dán Voice ID.');
        }
        return next;
      })
      .catch(() => {
        message.warning('Không tải được thư viện giọng — dán Voice ID nam Hà Nội.');
        return [] as ContentSeriesVoice[];
      })
      .finally(() => setVoicesLoading(false));
  };

  const toUniverseRow = (row: {
    id: string;
    characterCode: string;
    name: string;
    role: string;
    universe: string;
    visual: string;
    lifecycle: string;
    currentVersionId?: string | null;
    currentEra: string;
    version: string;
    isCurrentCanon: boolean;
    approvedAt?: string | null;
    approvedBy?: string | null;
    canon: Record<string, unknown>;
    references: { kind: string; path: string; label?: string | null }[];
    updatedAt: string;
  }): FamixaCharacterRecord => ({
    id: row.id,
    characterCode: row.characterCode,
    name: row.name,
    role: row.role,
    universe: row.universe,
    visual: row.visual,
    lifecycle: row.lifecycle,
    currentVersionId: row.currentVersionId ?? undefined,
    currentEra: row.currentEra,
    version: row.version,
    isCurrentCanon: row.isCurrentCanon,
    approvedAt: row.approvedAt ?? undefined,
    approvedBy: row.approvedBy ?? undefined,
    canon: parseFamixaCanon(row.canon),
    references: (row.references ?? []).map((r) => ({ kind: r.kind, path: r.path, label: r.label ?? undefined })),
    updatedAt: row.updatedAt,
  });

  const loadUniverse = () => {
    if (!famixaCharacters) {
      setUniverse([]);
      return Promise.resolve([] as FamixaCharacterRecord[]);
    }
    return fetchFamixaCharacters(characterScope)
      .then((rows) => {
        const next = (rows ?? []).map(toUniverseRow);
        setUniverse(next);
        return next;
      })
      .catch(() => {
        setUniverse([]);
        return [] as FamixaCharacterRecord[];
      });
  };

  const loadStudioPreflight = async () => {
    if (!famixaCharacters) {
      setStudioRows([]);
      return { items: [] as CharacterStudioRow[], style: projectStyle };
    }
    try {
      const [list, style] = await Promise.all([
        fetchCharacterStudioList(characterScope),
        fetchProjectVisualStyle('FAMIXA'),
      ]);
      const items = list.items ?? [];
      setStudioRows(items);
      setProjectStyle(style);
      return { items, style };
    } catch {
      setStudioRows([]);
      return { items: [] as CharacterStudioRow[], style: projectStyle };
    }
  };

  useEffect(() => {
    void loadVoiceLibrary();
    void loadUniverse();
    loadStudioPreflight();
  }, []);

  useEffect(
    () => () => {
      flushServerSaveRef.current();
      ttsStop.current = true;
      ttsAudio.current?.pause();
      ttsUrls.current.forEach((url) => URL.revokeObjectURL(url));
      ttsUrls.current.clear();
      ttsBlobs.current.clear();
    },
    [],
  );

  useEffect(() => {
    void refreshBuilds();
    const retry = window.setTimeout(() => void refreshBuilds(), 1500);
    const selected = (searchParams.get('build') || new URLSearchParams(window.location.search).get('build') || '').trim();
    if (selected) {
      void openBuild(selected);
      return () => window.clearTimeout(retry);
    }
    let cancelled = false;
    void (async () => {
      try {
        const row = await fetchContentSeriesPilot(catalog);
        if (cancelled) return;
        const remote = row.graph && typeof row.graph === 'object' ? (row.graph as SeriesPilotState) : null;
        const local = stateRef.current;
        void refreshBuilds();
        if (remote && hasSeriesGraph(remote as SeriesPilotState)) {
          persistState(bindPictureHashOnRuns(slimPilotForStorage(mergeRemotePilot(remote, local))), false);
        }
      } catch {
        void refreshBuilds();
      }
      if (cancelled) return;
      await hydratePilotCanon(stateRef.current);
      await hydratePilotKeyframes(stateRef.current);
      if (!cancelled) {
        setState((cur) => {
          const next = bindPictureHashOnRuns(withRunPixels(cur));
          saveSeriesPilot(next);
          return next;
        });
      }
    })();
    return () => {
      cancelled = true;
      window.clearTimeout(retry);
    };
    // hydrate once
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!filmLed || prodTab !== 'script') return;
    setProdTab('overview');
  }, [filmLed, prodTab]);

  useEffect(() => {
    if (seriesDesk !== 'workspace') return;
    const opened = reopenFilmSurface({
      workflowKey,
      seriesCode: catalog,
      shot: searchParams.get('shot'),
    });
    if (opened.autoOpenShot !== 'first') return;
    if ((studioShotId || '').trim()) return;
    const first = state.episode?.shots?.[0]?.id;
    if (first) setStudioShotId(first);
  }, [seriesDesk, studioShotId, state.buildId, state.episode?.shots, searchParams, workflowKey, catalog]);

  useEffect(() => {
    const next = syncVoiceOnlyRoles(stateRef.current);
    if (next !== stateRef.current) persistState(next, false);
  }, [state.roles.length, state.characters, state.castLocked]);

  useEffect(() => {
    if (prodMode !== 'director' && studioPane !== 'studio' && prodTab !== 'images') return;
    let cancelled = false;
    void (async () => {
      await hydratePilotKeyframes(stateRef.current);
      if (!cancelled) {
        setState((cur) => {
          const next = bindPictureHashOnRuns(withRunPixels(cur));
          if (!explicitBuildRef.current) saveSeriesPilot(next);
          return next;
        });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [studioPane, prodTab, state.buildId, prodMode]);

  useEffect(() => {
    const staffVoice =
      prodTab === 'voice' || prodTab === 'overview' || prodTab === 'video' || prodTab === 'finish';
    const directorVoice = prodMode === 'director' && (prodTab === 'overview' || directorBench);
    if (studioPane !== 'voice' && studioPane !== 'studio' && !staffVoice && !directorVoice) return;
    let cancelled = false;
    void hydrateSessionTts(stateRef.current).then(() => {
      if (cancelled) return;
      setState((cur) => ({ ...cur }));
    });
    return () => {
      cancelled = true;
    };
  }, [studioPane, prodTab, prodMode, directorBench, studioShotId, state.buildId]);

  useEffect(() => {
    const shot =
      productionShorts(stateRef.current).find((s) => s.id === studioShotId) ||
      (stateRef.current.episode?.shots ?? []).find((s) => s.id === studioShotId);
    if (!shot) return;
    const run = shotRunOf(stateRef.current, shot);
    if (!leftoverTwoFaceMixOnly(run)) return;
    patchRun(shot.id, clearTwoFaceLipsyncMixOnly());
  }, [studioShotId, state.buildId]);

  const rememberOsKeyframe = (shotId: string, url?: string) => {
    setOsKeyframeByShot((current) => {
      const prior = current[shotId];
      if (prior && prior !== url) URL.revokeObjectURL(prior);
      if (!url) {
        if (!prior) return current;
        const next = { ...current };
        delete next[shotId];
        return next;
      }
      return { ...current, [shotId]: url };
    });
  };

  const loadOsKeyframe = async (buildId: string, shotId: string, isCancelled?: () => boolean) => {
    try {
      const meta = await getProductionKeyframe(buildId, shotId);
      if (isCancelled?.()) return false;
      const block = productionPictureBlockCopy(meta.reason);
      setOsPictureBlockByShot((current) => ({ ...current, [shotId]: block }));
      if (block || !productionKeyframeIsDisplayable(meta) || !meta.byteSha256) {
        rememberOsKeyframe(shotId);
        return false;
      }
      const blob = await fetchProductionKeyframeMedia(buildId, shotId);
      if (isCancelled?.()) return false;
      if (!(await previewBytesMatch(blob, meta.byteSha256))) {
        rememberOsKeyframe(shotId);
        return false;
      }
      const mime = (meta.mimeType || 'image/png').trim();
      const typed = blob.type.startsWith('image/') ? blob : new Blob([await blob.arrayBuffer()], { type: mime });
      rememberOsKeyframe(shotId, URL.createObjectURL(typed));
      return true;
    } catch {
      setOsPictureBlockByShot((current) => ({ ...current, [shotId]: '' }));
      rememberOsKeyframe(shotId);
      return false;
    }
  };

  useEffect(() => {
    const filmBuildId = state.buildId;
    const shotId = studioShotId;
    if (!shotId) return;
    const identity = resolveShotWorkspaceIdentity({ requestedBuildId: filmBuildId, requestedShotId: shotId });
    const buildId = identity.ok ? identity.requestedBuildId : productionOsBuildIdForShot(filmBuildId, shotId);
    if (!identity.ok || !buildId) return;
    let cancelled = false;
    const shot = productionOsDeskShots(buildId, [
      ...productionShorts(stateRef.current),
      ...(stateRef.current.episode?.shots ?? []),
    ]).find((s) => s.id === shotId);
    const claim = shot
      ? {
          episodeCode: isProductionOsTestShot(shotId) ? 'TEST' : shotId.startsWith('EP01-') ? 'EP01' : undefined,
          sceneCode: isProductionOsTestShot(shotId) ? 'TEST-SC01' : undefined,
          story: futureDeskStory(shotId, (shot.story || '').trim()) || undefined,
          actingBeat: futureDeskActing(shotId, [shot.actingBeat?.before?.action, shot.actingBeat?.before?.body].map((x) => (x || '').trim()).filter(Boolean).join(' ')) || undefined,
          visualContract: (shot.visual || '').trim() || undefined,
          ...strategyClaim(shot),
        }
      : undefined;
    const scope = productionScopeFromShotCode(shotId);
    void (async () => {
      const shown = await loadOsKeyframe(buildId, shotId, () => cancelled);
      if (cancelled) return;
      await hydrateVisualShot(buildId, shotId, () => cancelled, !shown);
    })();
    void (async () => {
      try {
        const [actions, osState, selectionHydrate, preview, authoritative] = await Promise.all([
          getProductionShotActions(buildId, shotId, claim),
          getProductionShotState(buildId, shotId, claim),
          getProductionAiSelection(buildId, shotId, 'VIDEO').then(
            (row) => ({ status: 'ok' as const, row }),
            (error) => ({
              status: (productionAiSelectionHttpStatus(error) === 404 ? 'missing' : 'error') as 'missing' | 'error',
              row: null,
            }),
          ),
          getCreateVideoInputPreview(buildId, shotId, scope.episodeCode || undefined, scope.sceneCode || undefined).catch(() => null),
          getAuthoritativeProductionVideo(buildId, shotId).catch(() => null),
        ]);
        if (cancelled) return;
        setOsActionsByShot((m) => ({ ...m, [shotId]: actions }));
        noteStrategyCatalog(shotId, actions);
        setOsStateByShot((m) => ({ ...m, [shotId]: osState }));
        if (authoritative) setOsAuthoritativeByShot((m) => ({ ...m, [shotId]: authoritative }));
        const voiceLineId = shotId === OFFICIAL_SH02_SHOT_CODE
          ? OFFICIAL_SH02_LINE_ID
          : shotId === 'EP01-SC01-SH01'
            ? 'line-SC01-CHAR-003-1'
            : '';
        if (voiceLineId) {
          try {
            const voice = await getProductionCurrentVoice(buildId, shotId, voiceLineId);
            if (!cancelled && voice && !voice.empty && voice.artifactId) {
              setOsVoiceByShot((m) => ({ ...m, [shotId]: voice }));
              try {
                const blob = await fetchProductionVoiceMedia(buildId, shotId, voiceLineId);
                if (!cancelled) {
                  const url = URL.createObjectURL(blob);
                  setOsVoiceUrlByShot((m) => {
                    const prior = m[shotId];
                    if (prior) URL.revokeObjectURL(prior);
                    return { ...m, [shotId]: url };
                  });
                }
              } catch {
                if (!cancelled) {
                  setOsVoiceUrlByShot((m) => {
                    const next = { ...m };
                    const prior = next[shotId];
                    if (prior) URL.revokeObjectURL(prior);
                    delete next[shotId];
                    return next;
                  });
                }
              }
            }
          } catch {
            /* voice hydrate is optional until CREATE_VOICE succeeds */
          }
        }
        if (preview) {
          setOsDirectorTimingByShot((m) => ({ ...m, [shotId]: directorTimingFromPreview(preview) }));
        }
        const nextSelection = hydrateDirectorVideoSelection({
          status: selectionHydrate.status,
          row: selectionHydrate.row,
          previous: osVideoModelByShotRef.current[shotId]?.selection,
        });
        const artifactId =
          (authoritative && !authoritative.empty && (authoritative.artifactId || '').trim()) ||
          (osState.videoArtifactId || '').trim() ||
          undefined;
        setOsVideoModelByShot((m) => ({
          ...m,
          [shotId]: {
            selection: nextSelection,
            take: bindVideoTakeProvenance(m[shotId]?.take, { artifactId, selection: nextSelection }),
          },
        }));
        const osEngine = motionEngineFromProviderModel(nextSelection?.providerId, nextSelection?.modelId);
        if (osEngine) setEngine(osEngine);
      } catch {
        /* actions stay empty until API/mig is up */
      }
      try {
        const meta = await getProductionPreview(buildId, shotId);
        if (cancelled || meta.empty || !meta.artifactId || !meta.byteSha256) {
          if (!cancelled) {
            setOsPreviewIdentityByShot((m) => {
              const next = { ...m };
              delete next[shotId];
              return next;
            });
            setOsPreviewByShot((m) => {
              const next = { ...m };
              const prior = next[shotId];
              if (prior) URL.revokeObjectURL(prior);
              delete next[shotId];
              return next;
            });
          }
          return;
        }
        try {
          const blob = await fetchProductionPreviewMedia(buildId, shotId);
          if (cancelled) return;
          if (!(await previewBytesMatch(blob, meta.byteSha256))) {
            setOsPreviewIdentityByShot((m) => {
              const next = { ...m };
              delete next[shotId];
              return next;
            });
            setOsPreviewByShot((m) => {
              const next = { ...m };
              const prior = next[shotId];
              if (prior) URL.revokeObjectURL(prior);
              delete next[shotId];
              return next;
            });
            return;
          }
          const url = URL.createObjectURL(asPlayableVideoBlob(blob));
          setOsPreviewIdentityByShot((m) => ({
            ...m,
            [shotId]: osPreviewIdentityFromMeta(meta),
          }));
          setOsPreviewByShot((m) => {
            const prior = m[shotId];
            if (prior) URL.revokeObjectURL(prior);
            return { ...m, [shotId]: url };
          });
        } catch {
          if (cancelled) return;
          setOsPreviewIdentityByShot((m) => {
            const next = { ...m };
            delete next[shotId];
            return next;
          });
          setOsPreviewByShot((m) => {
            const next = { ...m };
            const prior = next[shotId];
            if (prior) URL.revokeObjectURL(prior);
            delete next[shotId];
            return next;
          });
          return;
        }
        setOsVideoModelByShot((m) => ({
          ...m,
          [shotId]: {
            selection: m[shotId]?.selection,
            take: bindVideoTakeProvenance(m[shotId]?.take, {
              artifactId: (meta.role || '').includes('VIDEO_TAKE') ? meta.artifactId : m[shotId]?.take?.artifactId,
              selection: m[shotId]?.selection,
              durationSec: (meta.role || '').includes('VIDEO_TAKE') ? meta.durationSec : m[shotId]?.take?.durationSec,
            }),
          },
        }));
      } catch {
        /* VISIBLE empty is explicit EMPTY, not a fallback */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [studioShotId, state.buildId]);

  const persistEngine = (next: FamixaMotionEngine) => {
    setEngine(next);
    const shotId = studioShotId;
    if (!shotId) return;
    const mapped = providerModelFromEngine(next);
    const identity = resolveShotWorkspaceIdentity({
      requestedBuildId: stateRef.current.buildId,
      requestedShotId: shotId,
    });
    const buildId = identity.ok ? identity.requestedBuildId : undefined;
    if (!buildId) {
      message.error({ content: formatShotIdentityError(identity), key: 'famixa-desk-err' });
      return;
    }
    const previous = osVideoModelByShotRef.current[shotId]?.selection;
    const scope = productionScopeFromShotCode(shotId);
    setDirectorSelectionBusy(true);
    void upsertProductionAiSelection({
      buildId,
      shotCode: shotId,
      episodeCode: scope.episodeCode || 'EP01',
      sceneCode: scope.sceneCode || 'SC01',
      stage: 'VIDEO',
      providerId: mapped.providerId,
      modelId: mapped.modelId,
      reason: 'DIRECTOR_SELECTION',
    })
      .then((row) => {
        const committed = directorVideoSelectionFromRow(row);
        if (!committed) throw new Error('INVALID_PRODUCTION_AI_SELECTION');
        setOsVideoModelByShot((m) => ({
          ...m,
          [shotId]: applyDirectorVideoModelSelection(m[shotId], committed),
        }));
      })
      .catch((e) => {
        const reverted = revertDirectorVideoSelectionAfterPostFailure(previous);
        setOsVideoModelByShot((m) => ({
          ...m,
          [shotId]: { selection: reverted, take: m[shotId]?.take },
        }));
        message.error({ content: apiErrorMessage(e, 'Không lưu được Model video.'), key: 'famixa-desk-err' });
      })
      .finally(() => {
        setDirectorSelectionBusy(false);
      });
  };

  const persistDirectorTiming = (
    durationSec: DirectorProductionDurationSec,
    speech?: DirectorSpeechInterval | null,
  ) => {
    const shotId = studioShotId;
    if (!shotId) return;
    const current = osDirectorTimingByShot[shotId];
    const currentSpeech = explicitDirectorSpeechInterval(current);
    const sameDuration = isAuthoritativeDirectorTiming(current) && Number(current.durationSec) === durationSec;
    const sameSpeech = (currentSpeech?.speechStartSec ?? null) === (speech?.speechStartSec ?? null)
      && (currentSpeech?.speechEndSec ?? null) === (speech?.speechEndSec ?? null);
    if (sameDuration && sameSpeech) return;
    const identity = resolveShotWorkspaceIdentity({
      requestedBuildId: stateRef.current.buildId,
      requestedShotId: shotId,
    });
    const buildId = identity.ok ? identity.requestedBuildId : undefined;
    if (!buildId) return;
    const scope = productionScopeFromShotCode(shotId);
    setDirectorTimingBusy(true);
    void materializeProductionDirectorTiming({
      buildId,
      shotCode: shotId,
      episodeCode: scope.episodeCode || 'EP01',
      sceneCode: scope.sceneCode || 'SC01',
      durationSec,
      speechStartSec: speech?.speechStartSec,
      speechEndSec: speech?.speechEndSec,
    })
      .then(async (row) => {
        setOsDirectorTimingByShot((m) => ({ ...m, [shotId]: directorTimingFromSnapshot(row) }));
        const claimShot = productionOsDeskShots(buildId, [
          ...productionShorts(stateRef.current),
          ...(stateRef.current.episode?.shots ?? []),
        ]).find((s) => s.id === shotId);
        const claim = claimShot
          ? {
              episodeCode: isProductionOsTestShot(shotId) ? 'TEST' : shotId.startsWith('EP01-') ? 'EP01' : undefined,
              sceneCode: isProductionOsTestShot(shotId) ? 'TEST-SC01' : undefined,
              story: futureDeskStory(shotId, (claimShot.story || '').trim()) || undefined,
              actingBeat: futureDeskActing(shotId, [claimShot.actingBeat?.before?.action, claimShot.actingBeat?.before?.body].map((x) => (x || '').trim()).filter(Boolean).join(' ')) || undefined,
              visualContract: (claimShot.visual || '').trim() || undefined,
              ...strategyClaim(claimShot),
            }
          : undefined;
        try {
          const [actions, osState, preview] = await Promise.all([
            getProductionShotActions(buildId, shotId, claim),
            getProductionShotState(buildId, shotId, claim),
            getCreateVideoInputPreview(buildId, shotId, scope.episodeCode || undefined, scope.sceneCode || undefined).catch(() => null),
          ]);
          setOsActionsByShot((m) => ({ ...m, [shotId]: actions }));
          noteStrategyCatalog(shotId, actions);
          setOsStateByShot((m) => ({ ...m, [shotId]: osState }));
          if (preview) {
            const fromPreview = directorTimingFromPreview(preview);
            const saved = directorTimingFromSnapshot(row);
            const kept = explicitDirectorSpeechInterval(fromPreview) ?? explicitDirectorSpeechInterval(saved);
            setOsDirectorTimingByShot((m) => ({
              ...m,
              [shotId]: {
                ...fromPreview,
                speechStartSec: kept?.speechStartSec ?? null,
                speechEndSec: kept?.speechEndSec ?? null,
              },
            }));
          }
        } catch {
          /* POST already froze OS timing; next open reloads actions */
        }
      })
      .catch((e) => {
        message.error({ content: apiErrorMessage(e, 'Không lưu được Production duration.'), key: 'famixa-desk-err' });
      })
      .finally(() => {
        setDirectorTimingBusy(false);
      });
  };

  const refuseUnwiredVeo = () => {
    if (engine !== 'veo' || keys.veo) return false;
    message.warning(
      'Đã chọn Google Veo 3. Lưu file Join ở Cấu hình AI → Video. Không gửi Runway / Wan thay thế.',
    );
    return true;
  };

  const persistVoice = (next: 'elevenlabs' | 'f5') => {
    setVoiceProvider(next);
    try {
      localStorage.setItem(VOICE_KEY, next);
    } catch {
      /* quota */
    }
  };

  const patchRole = (id: string, patch: {
    title?: string;
    name?: string;
    line?: string;
    voiceNote?: string;
    voiceId?: string;
    voiceName?: string;
    performance?: string;
  }) => {
    let next: SeriesPilotState = {
      ...state,
      roles: state.roles.map((r) => (r.id === id ? { ...r, ...patch } : r)),
    };
    const role = next.roles.find((r) => r.id === id);
    if (role?.characterId && (patch.voiceId !== undefined || patch.voiceName !== undefined)) {
      next = setCharacterVoice(next, role.characterId, {
        voiceId: patch.voiceId ?? role.voiceId,
        voiceName: patch.voiceName ?? role.voiceName,
      });
    }
    persistState(next);
  };

  const addRole = () => {
    if (state.roles.length >= 8) {
      message.warning('Tối đa 8 vai.');
      return;
    }
    persistState({ ...state, roles: [...state.roles, newRoleRow()] });
  };

  const onRoleCanon = async (roleId: string, file: File | undefined) => {
    if (!file) return;
    const role = state.roles.find((r) => r.id === roleId);
    if (!role) return;
    if (!role.name.trim()) {
      message.warning('Điền tên người rồi gắn Canon.');
      return;
    }
    try {
      const imageDataUrl = await readImageFile(file);
      const ref = localFileRef(file);
      let next = { ...state, roles: state.roles.map((r) => (r.id === roleId ? { ...r } : r)) };
      next = ensurePilotGraph({ ...next, schemaVersion: PILOT_SCHEMA });
      const charId = next.roles.find((r) => r.id === roleId)?.characterId;
      if (!charId) {
        message.warning('Chưa gắn CHAR — điền tên vai + người.');
        return;
      }
      persistState(
        setCharacterCanon(next, charId, {
          canonFileName: ref.fileName,
          canonLocalPath: ref.localPath,
          canonImageDataUrl: imageDataUrl,
        }),
      );
      void saveCanonPixels(charId, imageDataUrl, ref.fileName);
    } catch (e) {
      message.error({ content: apiErrorMessage(e, 'Không gắn được Canon.'), key: 'famixa-desk-err' });
    }
  };

  const removeRole = (id: string) => {
    persistState({ ...state, roles: state.roles.filter((r) => r.id !== id) });
  };

  const stills = state.stills ?? [];
  const stillFilterId = activeShort?.id ?? active?.id;
  const visibleStills =
    stillOnlyActive && stillFilterId ? stillsForShort(stills, stillFilterId) : stills;

  const patchStill = (id: string, patch: Partial<(typeof stills)[number]>) => {
    persistState(
      { ...state, stills: stills.map((s) => (s.id === id ? { ...s, ...patch } : s)) },
    );
  };

  const addStill = () => {
    persistState(
      {
        ...state,
        stills: [
          ...stills,
          newStillRow({
            shortId: activeShort?.id ?? shorts[0]?.id ?? '',
            scene: activeShort?.scene ?? '',
          }),
        ],
      },
    );
  };

  const patchRun = (id: string, patch: Partial<SeriesShotRun>) => {
    const cur = stateRef.current;
    const shot = episodeShots(cur).find((s) => s.id === id);
    const prev = cur.runs[id] ?? { status: shot?.status ?? 'keyframe_ready' };
    const nextPatch = patch.shotQa
      ? { ...patch, shotQa: mergeCanonicalShotQa(prev.shotQa, patch.shotQa) }
      : patch;
    persistState({ ...cur, runs: { ...cur.runs, [id]: { ...prev, ...nextPatch } } });
  };

  const noteStrategyCatalog = (shotId: string, actions: ProductionShotActions) => {
    const shot = (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
    setStrategyAcceptedByShot((current) => ({
      ...current,
      [shotId]: backendAcceptedStrategy(actions, strategyEngaged(shot) && shot?.strategyConfirmed === true),
    }));
  };

  const persistShotStrategy = (
    shotId: string,
    patch: Pick<FamixaSeriesShot, 'pictureStrategy' | 'speechDelivery' | 'strategyConfirmed'>,
  ) => {
    const live = stateRef.current;
    if (!live.episode) return;
    setStrategyAcceptedByShot((current) => ({ ...current, [shotId]: false }));
    persistState({
      ...live,
      episode: {
        ...live.episode,
        shots: live.episode.shots.map((s) => {
          if (s.id !== shotId) return s;
          const picture = (patch.pictureStrategy || '').trim();
          const speech = (patch.speechDelivery || '').trim();
          if (!picture && !speech) {
            const { pictureStrategy: _picture, speechDelivery: _speech, strategyConfirmed: _confirmed, ...rest } = s;
            return rest;
          }
          return {
            ...s,
            pictureStrategy: picture,
            speechDelivery: speech,
            strategyConfirmed: patch.strategyConfirmed === true,
          };
        }),
      },
    });
  };

  const persistActingBeat = (shotId: string, actingBeat: NonNullable<FamixaSeriesShot['actingBeat']>) => {
    const live = stateRef.current;
    if (!live.episode) return;
    const shot = live.episode.shots.find((s) => s.id === shotId);
    const prevFp = shot ? actingBeatFingerprintOf(shot) : '';
    persistState({
      ...live,
      episode: {
        ...live.episode,
        shots: live.episode.shots.map((s) => (s.id === shotId ? { ...s, actingBeat } : s)),
      },
    });
    if (prevFp !== actingBeatFingerprintOf({ ...(shot || { id: shotId }), actingBeat } as FamixaSeriesShot)) {
      patchRun(shotId, { actingBeatPendingMotion: true });
    }
  };

  const applyLipsyncSpeakerPick = (
    shotId: string,
    pt: { x: number; y: number; width: number; height: number; characterId?: string; frameNumber?: number },
  ) => {
    const run = shotRunOf(stateRef.current, { id: shotId, status: 'story_locked' } as FamixaSeriesShot);
    if (!canMutateLiveSpeakerTarget(run.lipsyncFrozenInput, run)) {
      message.warning('Speaker đã freeze — không đổi target Fal.');
      return;
    }
    patchRun(shotId, { lipsyncSpeaker: stampLipsyncSpeakerPick(pt, stateRef.current.characters) });
  };

  const onKeyframeFile = async (id: string, file: File | undefined) => {
    if (!file) return;
    try {
      const keyframeDataUrl = await readImageFile(file);
      const ref = localFileRef(file);
      const st = stateRef.current;
      const shot = episodeShots(st).find((s) => s.id === id);
      const card = shot ? compileShotSceneCard(st, shot, previousSceneKf(st, shot, productionShorts(st))?.shot) : undefined;
      patchRun(id, {
        keyframeDataUrl,
        keyframeFileName: ref.fileName,
        keyframePath: ref.localPath,
        kfBoundAction: (shot?.story || shot?.visual || '').trim() || undefined,
        keyframeInheritedFrom: undefined,
        kfApproved: false,
        kfRetryOk: false,
        kfSourceHash: dataUriHash(keyframeDataUrl),
        ...detachAvAfterPictureChange(shortRunOf(st, id)),
        visualSpec: card?.visualSpec,
        visualQa: card?.visualSpec ? seedQaChecks(card.visualSpec) : undefined,
        status: 'keyframe_ready',
      });
      const cur = stateRef.current;
      const pack = productionShorts(cur);
      persistState(applySceneKfReuses(cur, pack, buildSceneKfPlan(cur, pack)));
    } catch (e) {
      message.error({ content: apiErrorMessage(e, 'Không gắn được KF01.'), key: 'famixa-desk-err' });
    }
  };

  const generateKfFromCanon = async (opts: {
    clipId: string;
    aspect: '9:16' | '16:9';
    visual: string;
    action?: string;
    location?: string;
    characterIds?: string[];
    prevKfUrl?: string;
    inheritFromId?: string;
    continuityNote?: string;
    peopleCount?: number;
    peopleNames?: string;
    atmosphere?: string;
    lightingLock?: string;
    speakers?: string;
    visualSpec?: VisualSpec;
    sceneMasterUrl?: string;
    correction?: string;
    failedKfUrl?: string;
  }): Promise<PictureGenerationKind> => {
    message.warning(productionOsLegacyRefuseNote('CREATE_IMAGE', 'POST /series/still'));
    if (productionOsLegacyPathClosed()) return 'GENERATION_FAILED';
    if (!keysRef.current.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon mặt.');
      return 'GENERATION_FAILED';
    }
    if (kfInflightRef.current[opts.clipId]) {
      message.warning('Đang vẽ 1 KF — chờ xong, không bấm lần 2.');
      return 'GENERATION_FAILED';
    }
    kfInflightRef.current[opts.clipId] = true;
    try {
    const failedRun = shortRunOf(stateRef.current, opts.clipId);
    if (lastGenerationFail(failedRun) && !failedRun.failedKfHash) {
      patchRun(opts.clipId, stampFailedInput(failedRun, failedRun.keyframeDataUrl));
    }
    const st = await hydratePilotCanon(stateRef.current);
    if (st !== stateRef.current) persistState(st);
    const faces: SeriesCanonRef[] = [];
    const visualIds = (opts.characterIds ?? []).filter((id) => !/^CHAR-VO$/i.test(id) && !/loi binh|narrator/i.test(id));
    const shotRow = episodeShots(st).find((s) => s.id === opts.clipId);
    const spoken = shotRow ? linesForShot(st, shotRow) : [];
    const hasDialogue = Object.fromEntries(
      visualIds.map((id) => [id, spoken.some((l) => (l.characterId || '').toUpperCase() === id)]),
    );
    const voiceIds = Object.fromEntries(
      visualIds.map((id) => [id, (st.characters ?? []).find((c) => c.id === id)?.voiceId ?? '']),
    );
    if (!famixaCharacters) {
      message.warning('Phim này không dùng bộ nhân vật Famixa.');
      return 'GENERATION_FAILED';
    }
    let studioLockRows = studioRows;
    if (!studioLockRows.length) {
      try {
        const list = await fetchCharacterStudioList(characterScope);
        studioLockRows = list.items ?? [];
        setStudioRows(studioLockRows);
      } catch {
        studioLockRows = studioRows;
      }
    }
    let registry = universe;
    if (!registry.length) {
      try {
        registry = (await fetchFamixaCharacters(characterScope)).map(toUniverseRow);
        setUniverse(registry);
      } catch {
        message.error('Character Registry chưa sẵn (mig 330). Không đoán Canon — không vẽ KF.');
        return 'GENERATION_FAILED';
      }
    }
    let serverGuard: Awaited<ReturnType<typeof guardFamixaCharacters>>;
    try {
      const unknownNames = visualIds.length
        ? []
        : findUnregisteredNames(`${shotRow?.story || ''} ${shotRow?.visual || ''} ${shotRow?.beatText || ''}`, registry);
      serverGuard = await guardFamixaCharacters({ characterIds: visualIds, hasDialogue, voiceIds, unknownNames });
    } catch {
      message.error('Không gọi được Character Guard. Không vẽ KF.');
      return 'GENERATION_FAILED';
    }
    if (!serverGuard.ok) {
      const remaining = serverGuard.blocked.filter((text) => {
        if (!/chưa LOCK|APPROVE\/LOCK|FRONT|REQUEST CREATION/i.test(text)) return true;
        const code = text.match(/CHAR-\d+/i)?.[0];
        return !findStudioLockRow(code || '', studioLockRows)?.officialLocked;
      });
      if (remaining.length) {
        message.error(remaining[0] || 'Character Resolution BLOCK.');
        return 'GENERATION_FAILED';
      }
    }
    const audit = characterAuditOf({
      ok: serverGuard.ok,
      blocked: serverGuard.blocked,
      resolved: serverGuard.resolved.map((r) => ({
        characterCode: r.characterCode,
        lifecycle: r.lifecycle,
        era: r.era,
        version: r.version,
        wardrobeId: r.wardrobeId ?? undefined,
        refIds: r.refIds,
        frontOk: r.frontOk,
      })),
      compilerVersion: serverGuard.compilerVersion,
    });
    const subset = resolveFamixaCharacters(registry, visualIds)
      .map((row) => compileCharacterPromptSubset(row.canon))
      .join(' ');
    const unlockedId = visualIds.find((id) => !findStudioLockRow(id, studioLockRows)?.officialLocked);
    if (unlockedId) {
      const studio = findStudioLockRow(unlockedId, studioLockRows);
      const name = studio?.characterName || unlockedId;
      const copy = directorPictureHoldCopy({
        code: 'CHARACTER_NOT_LOCKED',
        type: 'character_lock',
        title: `${name} chưa LOCK`,
        message: `Để khuôn mặt và tạo hình không đổi giữa các shot, ${name} phải được Director duyệt và khóa Master trước khi vào production. APPROVED chưa đủ.`,
      });
      message.error({ content: `${copy.reason} — ${copy.hint}`, key: 'fx-kf-gen' });
      return 'GENERATION_FAILED';
    }
    const visualClaims = visualIds.map((id) => {
      const studio = findStudioLockRow(id, studioLockRows);
      const name = studio?.characterName || id;
      return lockedStudioClaim(id, name, studio?.masterSha256);
    });
    const visualResolved = resolveSceneVisualReferences(visualClaims, FAMIXA_VISUAL_MODE.visualMode);
    if (visualIds.length && !visualResolved.allowed) {
      message.error(visualResolved.code === 'LEGACY_VISUAL_MODE_CONFLICT'
        ? 'Canon photoreal cũ không được dùng. Khóa Character Studio 3D rồi tạo lại.'
        : 'VISUAL_MODE_CONFLICT — reference không cùng Visual Mode với Project.');
      return 'GENERATION_FAILED';
    }
    for (const claim of visualResolved.attachable.slice(0, Math.max(1, visualIds.length))) {
      const studio = findStudioLockRow(claim.characterId, studioLockRows);
      try {
        const path = (studio?.masterSha256
          ? characterStudioMasterImageUrl(claim.characterId, studio.masterSha256)
          : characterStudioReferenceImageUrl(claim.characterId, 'FRONT', studio?.masterSha256)).replace(/^\/api/, '');
        const { data } = await http.get<Blob>(path, { responseType: 'blob' });
        const dataUrl = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => resolve(String(reader.result ?? ''));
          reader.onerror = () => reject(new Error('studio-front'));
          reader.readAsDataURL(data);
        });
        const slim = await shrinkStillDataUrl(dataUrl);
        if (slim) {
          faces.push({
            name: claim.name,
            role: 'identity',
            imageDataUrl: slim,
            visualMode: claim.visualMode,
            referenceStatus: 'ACTIVE',
            authorityStatus: 'LOCKED',
            characterId: claim.characterId,
            referenceRole: 'IDENTITY_ANCHOR',
          });
        }
      } catch {
        message.error(`Không đọc được Character Studio FRONT của ${claim.name}.`);
        return 'GENERATION_FAILED';
      }
    }
    let prevUrl: string | undefined;
    if (opts.prevKfUrl?.startsWith('data:image')) {
      prevUrl = await shrinkStillDataUrl(opts.prevKfUrl);
      if (!prevUrl) {
        message.warning('KF shot trước quá nặng — vẽ từ Canon + lệnh continuity, không gắn pixel shot trước.');
      }
    }
    let sceneUrl: string | undefined;
    if (opts.sceneMasterUrl?.startsWith('data:image') && opts.sceneMasterUrl !== opts.prevKfUrl) {
      sceneUrl = await shrinkStillDataUrl(opts.sceneMasterUrl);
    }
    let failedUrl: string | undefined;
    if (
      opts.failedKfUrl?.startsWith('data:image') &&
      opts.correction &&
      !soloCastFromNote(opts.correction) &&
      !/Remove every extra person|Only Linh|CAST: Linh only/i.test(opts.correction)
    ) {
      failedUrl = await shrinkStillDataUrl(opts.failedKfUrl);
    }
    const refs = mergeReferencePack({
      scene: sceneUrl ? { name: 'Scene Master', role: 'scene', imageDataUrl: sceneUrl } : undefined,
      prev: prevUrl ? { name: 'Previous KF', role: 'continuity', imageDataUrl: prevUrl } : undefined,
      identities: faces.map((f, i) => ({
        name: f.name,
        role: i === 0 ? 'identity' : 'identity-secondary',
        imageDataUrl: f.imageDataUrl,
        visualMode: f.visualMode,
        referenceStatus: f.referenceStatus,
        authorityStatus: f.authorityStatus,
        characterId: f.characterId,
        referenceRole: f.referenceRole,
      })),
    });
    if (failedUrl) {
      refs.unshift({ name: 'FAILED-STILL', role: 'continuity', imageDataUrl: failedUrl });
      refs.splice(4);
    }
    if (refs.length === 0) {
      const named = (st.characters ?? []).filter((c) => characterCanonReady(c));
      message.warning(
        named.length
          ? 'Có tên file Canon nhưng mất pixels (F5). Bấm Thay Canon một lần — KIT nhớ ảnh trên máy, không gửi lên server.'
          : 'Ảnh Canon mặt chưa có trong session. Gắn lại ảnh vai (mặt/tóc) rồi tạo KF cảnh.',
      );
      return 'GENERATION_FAILED';
    }
    for (const c of st.characters ?? []) {
      const url = canonImageOf(st, c.id);
      if (url) void saveCanonPixels(c.id, url, c.canonFileName);
    }
    setStillBusy(opts.clipId);
    message.loading({ content: 'Đang vẽ 1 KF…', key: 'fx-kf-gen', duration: 0 });
      const res = await generateContentSeriesStill({
        prompt: seriesSceneStillPrompt({
          aspect: opts.aspect,
          visual: looksLikePackHeading(opts.visual) ? '' : opts.visual,
          action: looksLikePackHeading(opts.action) ? '' : opts.action,
          location: opts.location,
          refs,
          continuityNote: opts.continuityNote,
          peopleCount: opts.peopleCount ?? visualIds.length,
          peopleNames: opts.peopleNames,
          atmosphere: opts.atmosphere,
          lightingLock: opts.lightingLock,
          speakers: opts.speakers,
          visualSpec: opts.visualSpec,
          correction: opts.correction,
          characterSubset: subset,
        }),
        aspect: opts.aspect,
        references: refs.map((r) => ({
          ...r,
          visualMode: r.visualMode || FAMIXA_VISUAL_MODE.visualMode,
          referenceStatus: r.referenceStatus || 'ACTIVE',
          authorityStatus: r.authorityStatus || 'LOCKED',
          referenceRole: r.referenceRole
            || (r.role === 'identity' || r.role === 'identity-secondary'
              ? 'IDENTITY_ANCHOR'
              : r.role),
        })),
      });
      const fitted = res.imageDataUrl.startsWith('data:image')
        ? await prepareRunwayKf(res.imageDataUrl, opts.aspect, {
            people: opts.peopleCount ?? visualIds.length,
            faceSafe: true,
          })
        : res.imageDataUrl;
      const marked = fitted;
      const before = shortRunOf(stateRef.current, opts.clipId);
      const generation = applyPictureGenerationGuard(before, marked.startsWith('data:image') ? marked : undefined);
      if (generation.kind === 'GENERATION_FAILED') {
        message.error({ content: 'Không vẽ được KF từ Canon.', key: 'fx-kf-gen' });
        return 'GENERATION_FAILED';
      }
      if (generation.kind === 'DUPLICATE_PIXEL') {
        message.error({ content: PICTURE_DUPLICATE_COPY, key: 'fx-kf-gen' });
        return 'DUPLICATE_PIXEL';
      }
      let visualQa = opts.visualSpec ? seedQaChecks(opts.visualSpec) : undefined;
      if (opts.visualSpec && marked.startsWith('data:image')) {
        message.loading({ content: 'Đang chấm QA (không vẽ ảnh mới)…', key: 'fx-kf-gen', duration: 0 });
        try {
          const raw = await qaContentSeriesStill({
            imageDataUrl: marked,
            specJson: JSON.stringify(opts.visualSpec),
          });
          visualQa = parseVisionQa(raw, opts.visualSpec);
        } catch (e) {
          visualQa = {
            ...(visualQa ?? seedQaChecks(opts.visualSpec)),
            status: 'PENDING',
            notes: e instanceof Error ? e.message : 'Image QA lỗi — Chấm lại, đừng tạo ảnh mới.',
          };
          message.warning('Image QA chưa chấm được. Chấm lại trên hàng — đừng tạo 10 KF mới.');
        }
      }
      patchRun(opts.clipId, {
        ...generation.run,
        pictureSelectionSnapshot: res.decisionId && res.providerId
          ? {
              decisionId: res.decisionId,
              providerId: res.providerId,
              modelId: res.model,
              selectionMode: '',
            }
          : undefined,
        keyframeFileName: `kf-${opts.clipId}-canon.jpg`,
        kfBoundAction: (opts.action || shotRow?.story || '').trim() || undefined,
        keyframeInheritedFrom: undefined,
        visualSpec: opts.visualSpec,
        visualQa,
        characterAudit: audit,
        kfTechNote: opts.continuityNote?.trim() || undefined,
        status: 'keyframe_ready',
        visualMode: FAMIXA_VISUAL_MODE.visualMode,
        visualUniverse: FAMIXA_VISUAL_MODE.visualUniverse,
        visualStyle: FAMIXA_VISUAL_MODE.visualStyle,
        visualPipelineStatus: 'ACTIVE',
        visualReferenceIds: visualResolved.attachable.map((c) => c.characterId),
      });
      if (marked.startsWith('data:image')) {
        await saveKfPixels(opts.clipId, marked, `kf-${opts.clipId}-canon.jpg`);
      }
      if (visualQa?.status === 'REJECT') {
        message.warning(`Image QA REJECT ${opts.clipId}: ${(visualQa.hardFails.join(', ') || visualQa.notes || 'hard fail').slice(0, 160)}`);
      } else if (visualQa && visualQaAllowsApprove(visualQa)) {
        message.success(`Image QA ${visualQa.total} PASS — có thể Duyệt KF.`);
      }
      const cur = stateRef.current;
      persistState({
        ...cur,
        episode: cur.episode
          ? {
              ...cur.episode,
              shots: cur.episode.shots.map((s) =>
                s.id === opts.clipId ? { ...s, inheritFromShotId: opts.inheritFromId || s.inheritFromShotId } : s,
              ),
            }
          : cur.episode,
      });
      message.success({ content: `Đã vẽ 1 KF (${res.model}) — duyệt rồi mới I2V.`, key: 'fx-kf-gen' });
      return 'NEW_PENDING_PICTURE';
    } catch (e) {
      message.error({ content: apiErrorMessage(e, 'Không vẽ được KF từ Canon.'), key: 'fx-kf-gen' });
      return 'GENERATION_FAILED';
    } finally {
      delete kfInflightRef.current[opts.clipId];
      setStillBusy(undefined);
    }
  };

  const rerunVisualQa = async (shotId: string) => {
    const pack = productionShorts(stateRef.current);
    const shot = pack.find((s) => s.id === shotId);
    if (!shot) return;
    const run = shotRunOf(stateRef.current, shot);
    if (!run.keyframeDataUrl?.startsWith('data:image')) {
      message.warning('Chưa có KF để chấm. Tạo 1 ảnh — đừng chấm không.');
      return;
    }
    const spec = compileShotSceneCard(stateRef.current, shot, previousSceneKf(stateRef.current, shot, pack)?.shot).visualSpec;
    const locked = isLockedTemplateKf(run);
    setStillBusy(shotId);
    try {
      const raw = await qaContentSeriesStill({
        imageDataUrl: run.keyframeDataUrl,
        specJson: JSON.stringify(spec),
      });
      let visualQa = parseVisionQa(raw, spec, { sceneMaster: locked });
      if (locked && run.kfApproved && visualQa.hardFails.length) {
        visualQa = {
          ...visualQa,
          hardFails: [],
          status: 'PASS',
          notes: 'Scene Master đã KHÓA — không BLOCK vì action/count/place. Ảnh này là chỗ/áo cho shot sau.',
        };
      }
      patchRun(shotId, { visualSpec: spec, visualQa });
      if (visualQaAllowsApprove(visualQa)) {
        message.success(
          locked
            ? `KF khóa: QA ${visualQa.total ?? '—'} — giữ KHÓA, không BLOCK Scene Master.`
            : `QA ${visualQa.total} PASS — Duyệt KF được.`,
        );
      } else {
        message.info(`QA ${visualQa.total ?? '—'} ${visualQa.status}${visualQa.hardFails.length ? ` · ${visualQa.hardFails.join(', ')}` : ''}`);
      }
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chấm lại QA thất bại. Ảnh cũ còn — đừng tạo KF mới.'));
    } finally {
      setStillBusy(undefined);
    }
  };

  const onClipFile = (id: string, file: File) => {
    if (file.size > 80_000_000) {
      message.error('Video xem thử ≤ 80MB — chỉ trên máy, không lên server.');
      return;
    }
    const prev = sessionClips[id];
    if (prev) URL.revokeObjectURL(prev);
    setSessionClips((m) => ({ ...m, [id]: URL.createObjectURL(file) }));
    patchRun(id, { localVideoPath: localFileRef(file).localPath });
  };

  const applyPack = () => {
    try {
      const parsed = parseFamixaPack(packText);
      if (parsed.error) {
        setPackResult({ ok: false, text: parsed.error });
        message.error(parsed.error);
        return;
      }
      persistState(
        replaceStoryFromParse(state, parsed, packText),
      );
      setActiveShortId(parsed.shorts[0]?.id);
      setActiveId(parsed.episode?.shots[0]?.id);
      setStudioPane('script');
      const text = `Đã nhận: ${packSummary(parsed)}. Chưa gửi Runway.`;
      setPackResult({ ok: true, text });
      message.success(text);
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Không nhận được pack.';
      setPackResult({ ok: false, text });
      message.error(text);
    }
  };

  const requestDraft = () => {
    const seed = packSeed.trim();
    if (seed.length < 12) {
      message.warning('Nhập hạt giống ít nhất một câu (12 ký tự).');
      return;
    }
    if (state.scriptLocked) {
      message.warning('Mở khóa kịch bản rồi mới đề xuất lại.');
      return;
    }
    const run = () => {
      setDraftBusy(true);
      void draftContentSeriesScript({
        seed,
        charactersHint: seriesCanonHint(state) || undefined,
        episodeHint: ep?.episode || ep?.title || undefined,
        brandId: famixaBrand?.id,
      })
        .then((res) => {
          setPackText(res.pack);
          setPackResult({ ok: true, text: `${res.costNote} Đọc lại rồi bấm Nhận pack — chưa khóa, chưa gửi I2V.` });
          message.success('Đã điền nháp vào ô. Sửa rồi Nhận pack.');
        })
        .catch((e) => {
          const text = apiErrorMessage(e, 'Không đề xuất được pack.');
          setPackResult({ ok: false, text });
          message.error(text);
        })
        .finally(() => setDraftBusy(false));
    };
    if (packText.trim()) {
      modal.confirm({
        title: 'Ghi đè ô dán?',
        content: 'Bản đề xuất sẽ thay chữ đang có. Nhận pack vẫn do bạn bấm.',
        okText: 'Đề xuất',
        cancelText: 'Giữ ô hiện tại',
        onOk: run,
      });
      return;
    }
    run();
  };

  const shotPackText = (shot: FamixaSeriesShot) =>
    packForShotEdit(shot, ep, state.packDraft, packText, stills);

  const putShotPackToPaste = (shot: FamixaSeriesShot) => {
    const text = shotPackText(shot);
    setPackText(text);
    const note = `Khối ${shot.id} trên ô dán — sửa rồi Nhận pack. Chưa sang shot sau.`;
    setPackResult({ ok: true, text: note });
    message.success(note);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const copyShotPack = (shot: FamixaSeriesShot) => {
    void navigator.clipboard.writeText(shotPackText(shot));
    message.success(`Đã copy khối ${shot.id} (không gồm shot sau)`);
  };

  const lockShotGraph = () => {
    const pruned = pruneEmptyShots(state);
    const mapped = applyDialogueMap(pruned);
    let next = applyContinuityChain(mapped, productionShorts(mapped));
    next = applyEditDurations(next, productionShorts(next));
    for (const sc of [...new Set(productionShorts(next).map(sceneIdOfShot))]) {
      if (!next.sceneMasters?.[sc]) next = upsertSceneMaster(next, deriveSceneMaster(next, sc));
    }
    const n = next.episode?.shots.length ?? 0;
    persistState(next);
    const remain = next.episode?.shots ?? [];
    if (active && !remain.some((s) => s.id === active.id)) setActiveId(remain[0]?.id);
    if (n === 0) {
      message.warning('Không còn shot có Script Beat. Sửa kịch bản rồi Nhận pack.');
      return;
    }
    const cov = coverageOf(next, remain);
    const dens = shotPlanDensity(remain);
    const densNote = dens.warn
      ? ` · mật độ ${dens.ratio.toFixed(1)} shot/beat — xem lại`
      : '';
    message.success(
      `SHOT GRAPH LOCKED — ${n} Short · thoại ${cov.spoken} · câm ${cov.silent}` +
        densNote +
        (cov.extraUnmapped.length ? ` · ${cov.extraUnmapped.length} câu chưa gắn` : '') +
        (cov.message ? ` · ${cov.message}` : ''),
    );
  };

  const removeShots = (ids: string[]) => {
    if (!ids.length) return;
    if (state.sceneLocked) {
      message.warning('Scene đã Final. Mở khóa cảnh trước khi xóa shot.');
      return;
    }
    modal.confirm({
      title: `Xóa ${ids.length} shot khỏi plan?`,
      content: 'Chỉ gỡ khỏi bảng dựng. KF trên máy giữ nếu thêm lại cùng id. Không xóa thoại kịch bản.',
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Giữ',
      onOk: () => {
        const next = removeSceneShots(stateRef.current, ids);
        persistState(next);
        const remain = episodeShots(next);
        if (active && ids.includes(active.id)) setActiveId(remain[0]?.id);
        message.success(`Đã xóa ${ids.length} shot.`);
      },
    });
  };

  const selectShot = (shot: FamixaSeriesShot) => {
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản ở bước 1 rồi mới dựng cảnh.');
      setStudioPane(studioFallbackPane(state));
      return;
    }
    const bound = bindShotToMemory(state, shot);
    if (bound !== state) persistState(bound);
    setActiveId(shot.id);
  };

  const markKeyframe = () => {
    if (!ready || !active) return;
    patchRun(active.id, {
      status: 'keyframe_ready',
      kfApproved: true,
      kfRetryOk: true,
      continuity: Object.fromEntries(CONTINUITY_GATES.map((g) => [g.id, true])),
    });
  };

  /** LEGACY / NON-AUTHORITATIVE for CREATE_VIDEO. LEGACY / NON-AUTHORITY. Desk Tạo video uses Production OS (createProductionVideo → createProductionRequest). Do not bind CURRENT / VISIBLE from this path. */
  const sendTurbo = async (opts: {
    clipId: string;
    prompt: string;
    negative?: string;
    seconds: number;
    ratio: '16:9' | '9:16';
    engine: 'turbo' | 'wan' | 'veo';
    silent?: boolean;
    resume?: boolean;
    forceNew?: boolean;
    diagnostic?: boolean;
    lastFrameFromUrl?: string;
    cameraShift?: number;
    /** Per-shot Director cap. Not monthly ContentBudget / localStorage. */
    maxCost?: number | null;
    currency?: string | null;
  }) => {
    message.warning(productionOsLegacyRefuseNote('CREATE_VIDEO', 'POST /series/turbo'));
    if (productionOsLegacyPathClosed()) return false;
    const existing = shortRunOf(stateRef.current, opts.clipId);
    const resume = Boolean(opts.resume || (!opts.forceNew && shouldResumeTurboPoll(existing)));
    const keyframe = existing.keyframeDataUrl;
    if (!resume && !keyframe) {
      message.warning('Gắn KF cảnh (cả khung hình), đừng gửi ảnh mặt CHAR.');
      return false;
    }
    const imageDataUrl = keyframe;
    const prev = turboLockRef.current;
    let release = () => {};
    turboLockRef.current = new Promise<void>((resolve) => {
      release = resolve;
    });
    await prev;
    const quietMin = runwayQuietRemainMin(runwayQuietUntil);
    if (quietMin && !opts.resume) {
      message.warning(`KIT còn khóa gửi job mới (429 cũ). Bấm «Đã lên tier — mở gửi» rồi Gửi lại từng hàng. Hỏi lại · 0 cr vẫn dùng được.`);
      release();
      return false;
    }
    turboBusyRef.current = opts.clipId;
    setTurboBusy(opts.clipId);

    const finishOk = (task: {
      videoUrl?: string | null;
      seconds?: number;
      model?: string;
      taskId?: string;
      videoBytes?: number | null;
      videoMime?: string | null;
      videoVerified?: boolean;
    }, wan: boolean) => {
      const current = shortRunOf(stateRef.current, opts.clipId);
      const sameLip = Boolean(
        task.videoUrl &&
          (current.lipsyncUrl === task.videoUrl || (current.lipsynced && current.previewUrl === task.videoUrl)),
      );
      const shotRow = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
      const silentTake = shotRow ? linesForShot(stateRef.current, shotRow).length === 0 : false;
      const attempts = adaptMotionAttempts(opts.clipId, current.runwayAttempts);
      const wanProvider = motionProviderFromEngine(
        wan || (task.model ?? '').startsWith('wan')
          ? 'wan'
          : opts.engine === 'veo' || (task.model ?? '').startsWith('veo')
            ? 'veo'
            : 'turbo',
      );
      const currentMotion = shotRow
        ? {
            ...freezeMotionInput(stateRef.current, shotRow, { provider: wanProvider }),
            picture: currentPictureInputOf(stateRef.current, shotRow),
          }
        : undefined;
      const decision = currentMotion
        ? validateMotionCallback({
            attempts,
            current: currentMotion,
            providerTaskId: task.taskId || current.turboTaskId,
          })
        : { promote: false as const, resultClass: 'LATE_RESULT' as const, keepHistory: true as const, reason: 'NO_SHOT' };
      const callbackPatch = motionCallbackRunPatch({
        run: current,
        decision,
        outputUrl: task.videoUrl ?? undefined,
        taskId: task.taskId || current.turboTaskId,
        videoBytes: task.videoBytes ?? undefined,
        videoMime: task.videoMime ?? undefined,
        silentTake,
      });
      patchRun(opts.clipId, {
        ...(decision.promote
          ? {
              status: 'turbo_testing' as const,
              ...(sameLip
                ? { finalSource: 'FAL' as const, takeUrl: current.takeUrl || current.previewUrl }
                : { ...stampMuteTake(task.videoUrl || '', silentTake), lipsyncStatus: undefined }),
              runwaySpent: wan ? 0 : runwayCredits(task.seconds || opts.seconds).credits,
              model: task.model,
              turboStatus: 'SUCCEEDED' as const,
              turboError: undefined,
              videoPipe: 'VIDEO_READY' as const,
              motionNeedsRemake: false,
              actingBeatPendingMotion: false,
              videoVerified: true,
              videoBytes: task.videoBytes ?? undefined,
              videoMime: task.videoMime ?? undefined,
            }
          : {}),
        ...callbackPatch,
      });
      if (!opts.silent) {
        const attached = Boolean(
          decision.promote || callbackPatch.takeUrl || callbackPatch.previewUrl || callbackPatch.videoPipe === 'VIDEO_READY',
        );
        if (attached) {
          message.success(
            wan
              ? `Take Wan sẵn. Fal đã trừ ~1 đơn vị 720p — sổ KIT không ghi Runway cr.`
              : opts.engine === 'veo'
                ? 'Take Veo sẵn. Không gửi Runway / Wan.'
                : resume
                  ? `Lấy được take từ task cũ (0 cr thêm).`
                  : `Take sẵn. Runway đã trừ ~${runwayCredits(task.seconds || opts.seconds).credits} cr — sổ KIT chỉ ghi khi khóa take đạt.`,
          );
        } else {
          message.warning(
            wan
              ? `Wan xong nhưng chưa gắn vào shot này (${decision.promote ? '' : decision.reason}). Không coi là video hiện tại.`
              : `Take xong nhưng chưa gắn vào shot này (${decision.promote ? '' : decision.reason}).`,
          );
        }
      }
      return true;
    };

    const markQuiet = (optsQuiet: { daily?: boolean; hits?: number; retryAfterSec?: number }) => {
      const until = nextRunwayQuietUntil({ ...optsQuiet, prev: runwayQuietUntil });
      persistRunwayQuietUntil(until);
      setRunwayQuietUntil(until);
    };

    const pollTask = async (taskId: string) => {
      const t0 = Date.now();
      let waitMs = 6000;
      let hits429 = 0;
      while (Date.now() - t0 < 240_000) {
        try {
          const task = await getContentSeriesTurbo(taskId);
          hits429 = 0;
          const live = shortRunOf(stateRef.current, opts.clipId);
          const pipeStatus =
            task.status === 'SUCCEEDED'
              ? task.videoUrl
                ? 'VIDEO_SUCCEEDED'
                : 'FILE_INVALID'
              : task.status === 'RUNNING' || task.status === 'PROCESSING'
                ? 'VIDEO_PROCESSING'
                : task.status === 'PENDING'
                  ? 'VIDEO_SUBMITTED'
                  : classifyVideoPipe({ ...live, turboStatus: task.status, turboTaskId: task.taskId });
          patchRun(opts.clipId, {
            turboStatus: task.status,
            turboTaskId: task.taskId,
            turboError: undefined,
            videoPipe: pipeStatus,
            runwayAttempts: patchRunwayAttempt(live.runwayAttempts, task.taskId, {
              status: task.status,
              outputUrl: task.videoUrl ?? undefined,
              failureCode: task.failureCode ?? parseFailureCode(task.error || ''),
            }),
          });
          if (task.status === 'SUCCEEDED' && task.videoUrl) {
            const wan = opts.engine === 'wan' || (task.model ?? '').startsWith('wan');
            if (task.videoVerified || task.videoUrl.startsWith('data:video')) return finishOk(task, wan);
            patchRun(opts.clipId, { videoPipe: 'VIDEO_DOWNLOADING' });
            try {
              const probed = await probeContentSeriesTake(task.videoUrl);
              if (probed.ok) {
                return finishOk(
                  { ...task, videoBytes: probed.bytes, videoMime: probed.mime },
                  wan,
                );
              }
              const lastText = explainTurboError(probed.error || 'Có URL nhưng file không đọc được.');
              const after = shortRunOf(stateRef.current, opts.clipId);
              patchRun(opts.clipId, {
                turboStatus: 'SUCCEEDED',
                turboError: lastText,
                videoPipe: 'DOWNLOAD_FAILED',
                videoVerified: false,
                runwayAttempts: patchRunwayAttempt(after.runwayAttempts, task.taskId, {
                  status: 'SUCCEEDED',
                  outputUrl: task.videoUrl,
                  downloadOk: false,
                  error: lastText,
                }),
              });
              if (!opts.silent) message.error(lastText);
              return false;
            } catch (probeErr) {
              if (looksLikeVideoUrl(task.videoUrl)) return finishOk(task, wan);
              const lastText = explainTurboError(apiErrorMessage(probeErr, 'Không xác được file take.'));
              patchRun(opts.clipId, { turboError: lastText, videoPipe: 'DOWNLOAD_FAILED', videoVerified: false });
              if (!opts.silent) message.error(lastText);
              return false;
            }
          }
          if (task.status === 'SUCCEEDED' && !task.videoUrl) {
            waitMs = 4000;
            continue;
          }
          if (task.status === 'FAILED' || task.status === 'CANCELLED') {
            const lastText = explainTurboError(task.error || `Turbo ${task.status}. Chưa có file.`);
            const after = shortRunOf(stateRef.current, opts.clipId);
            const failShot = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
            const failAttempts = adaptMotionAttempts(opts.clipId, after.runwayAttempts);
            const failCurrent = failShot
              ? {
                  ...freezeMotionInput(stateRef.current, failShot, {
                    provider: motionProviderFromEngine(
                      opts.engine === 'wan' || (task.model ?? '').startsWith('wan')
                        ? 'wan'
                        : opts.engine === 'veo' || (task.model ?? '').startsWith('veo')
                          ? 'veo'
                          : 'turbo',
                    ),
                  }),
                  picture: currentPictureInputOf(stateRef.current, failShot),
                }
              : undefined;
            const failDecision = failCurrent
              ? validateMotionCallback({
                  attempts: failAttempts,
                  current: failCurrent,
                  providerTaskId: task.taskId,
                })
              : { promote: false as const, resultClass: 'LATE_RESULT' as const, keepHistory: true as const, reason: 'NO_SHOT' };
            const failPatch = motionCallbackRunPatch({
              run: after,
              decision: failDecision,
              taskId: task.taskId,
              failed: true,
            });
            patchRun(opts.clipId, {
              ...(failDecision.promote
                ? {
                    turboStatus: task.status,
                    turboError: lastText,
                    turboTaskId: task.taskId,
                    videoPipe: 'RUNWAY_FAILED' as const,
                    videoVerified: false,
                  }
                : {}),
              ...failPatch,
              runwayAttempts: patchRunwayAttempt(failPatch.runwayAttempts || after.runwayAttempts, task.taskId, {
                status: task.status,
                failureCode: task.failureCode ?? parseFailureCode(task.error || lastText),
                error: lastText,
                resultClass: failDecision.resultClass,
              }),
            });
            if (!opts.silent) message.error(lastText);
            return false;
          }
          waitMs = 6000;
        } catch (e) {
          const raw = apiErrorMessage(e, 'Không hỏi được task Runway.');
          if (isTurboDailyQuota(raw)) {
            markQuiet({ daily: true });
            const lastText = explainTurboError(raw);
            patchRun(opts.clipId, { turboStatus: 'RETRY', turboError: lastText, turboTaskId: taskId });
            if (!opts.silent) message.error(lastText);
            return false;
          }
          if (isTurboRateLimit(raw)) {
            hits429 += 1;
            const sec = parseRetryAfterSec(raw, 30);
            if (hits429 >= 3) {
              markQuiet({ hits: hits429, retryAfterSec: sec });
              const lastText = `Runway chặn liên tục (429). Đợi ~${runwayQuietRemainMin(nextRunwayQuietUntil({ hits: hits429, retryAfterSec: sec, prev: runwayQuietUntil }))} phút. Không bấm thêm.`;
              patchRun(opts.clipId, { turboStatus: 'RETRY', turboError: lastText, turboTaskId: taskId });
              if (!opts.silent) message.error(lastText);
              return false;
            }
            patchRun(opts.clipId, {
              turboStatus: 'RETRY',
              turboError: `Runway 429 — hỏi lại task sau ${sec}s (0 cr).`,
              turboTaskId: taskId,
            });
            await new Promise((r) => setTimeout(r, sec * 1000));
            waitMs = Math.min(Math.max(waitMs * 2, sec * 1000), 30_000);
            continue;
          }
          const lastText = explainTurboError(raw);
          patchRun(opts.clipId, { turboError: lastText, turboStatus: 'FAILED', turboTaskId: taskId });
          if (!opts.silent) message.error(lastText);
          return false;
        }
        await new Promise((r) => setTimeout(r, waitMs));
      }
      const lastText = 'Runway chưa trả take. Đợi hết hạn mức rồi Hỏi lại · 0 cr — đừng gửi job mới.';
      patchRun(opts.clipId, {
        turboError: lastText,
        turboStatus: 'RETRY',
        turboTaskId: taskId,
        videoPipe: 'TIMEOUT',
      });
      if (!opts.silent) message.error(lastText);
      return false;
    };

    try {
      const latest = shortRunOf(stateRef.current, opts.clipId);
      if (resume && latest.turboTaskId?.trim()) {
        if (!opts.silent) message.info('Hỏi lại task Runway đã gửi — 0 cr.');
        return await pollTask(latest.turboTaskId.trim());
      }
      if (!imageDataUrl) {
        message.warning('Gắn KF cảnh (cả khung hình), đừng gửi ảnh mặt CHAR.');
        return false;
      }
      let sourceCheck = inspectKfDataUri(imageDataUrl);
      if (imageDataUrl.startsWith('data:image')) {
        try {
          const dim = await measureKfImage(imageDataUrl);
          sourceCheck = kfCheckFromMeasure(sourceCheck, dim.width, dim.height);
        } catch {
          sourceCheck = {
            ...sourceCheck,
            ok: false,
            reasons: [...sourceCheck.reasons, 'Không đọc được ảnh KF.'],
          };
        }
      }
      if (!sourceCheck.ok) {
        patchRun(opts.clipId, {
          kfCheck: sourceCheck,
          videoPipe: 'INPUT_INVALID',
          turboStatus: 'BLOCKED',
          turboError: sourceCheck.reasons.join(' '),
        });
        message.error(`Chưa gửi (0 cr): ${sourceCheck.reasons.join(' ')}`);
        return false;
      }
      const cleaned = sanitizeKitPrecheck(latest);
      if (cleaned) patchRun(opts.clipId, cleaned);
      const gated = { ...latest, ...cleaned };
      const shotForPrompt = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
      const actionForPrompt = shotForPrompt
        ? i2vActionOf(stateRef.current, shotForPrompt) || opts.prompt
        : opts.prompt;
      const compileAt = (retry: number) =>
        shotForPrompt
          ? compileI2vPrompt(stateRef.current, shotForPrompt, actionForPrompt, videoContext, retry)
          : compileRunwayPromptV1({ action: opts.prompt, retry }).text;
      const sourceHash = dataUriHash(imageDataUrl);
      const promptLocked = (prompt: string) => sameFailedInput(gated, sourceHash, promptHashOf(prompt));
      const usedPrompt = (prompt: string) =>
        promptLocked(prompt) || promptUsedOnCurrentKf(gated, promptHashOf(prompt), sourceHash);
      const maxCameraShift = 6;
      let cameraShift = opts.cameraShift ?? 0;
      if (!opts.diagnostic && lastGenerationFail(gated) && cameraShift === 0 && usedPrompt(compileAt(gated.i2vRetry ?? 0))) {
        cameraShift = 1;
      }
      let retryN = (gated.i2vRetry ?? 0) + cameraShift;
      let compiledPrompt = compileAt(retryN);
      while (!opts.diagnostic && usedPrompt(compiledPrompt) && cameraShift < maxCameraShift) {
        cameraShift += 1;
        retryN = (gated.i2vRetry ?? 0) + cameraShift;
        compiledPrompt = compileAt(retryN);
      }
      if (!opts.diagnostic && usedPrompt(compiledPrompt)) {
        for (let holdRetry = 0; holdRetry <= maxCameraShift; holdRetry++) {
          const holdFallback = compileRunwayPromptV1({
            action: 'He stays in place.',
            retry: holdRetry,
            directed: false,
            motion: {
              body: 'He stays in place.',
              gaze: 'Looks toward the other person, not the camera.',
            },
          }).text;
          if (!usedPrompt(holdFallback)) {
            compiledPrompt = holdFallback;
            retryN = holdRetry;
            break;
          }
        }
      }
      if (!opts.diagnostic && usedPrompt(compiledPrompt)) {
        message.warning('Hết lệch camera trên ảnh này. Tạo hình mới rồi duyệt — đừng Confirm cùng ảnh.');
        return false;
      }
      if (retryN !== (gated.i2vRetry ?? 0)) {
        patchRun(opts.clipId, { i2vRetry: retryN });
      }
      let lastText = '';
      for (let attempt = 0; attempt < 2; attempt++) {
        const gate = preflightTurboSend({ prompt: compiledPrompt, imageDataUrl });
        if (!gate.ok) {
          message.error(`Chưa gửi (0 cr): ${gate.reasons.join(' ')}`);
          return false;
        }
        const prompt = gate.prompt;
        if (!opts.diagnostic && usedPrompt(prompt)) {
          message.warning('Hết lệch camera trên ảnh này. Tạo hình mới rồi duyệt — đừng Confirm cùng ảnh.');
          return false;
        }
        let createdTask = false;
        try {
          const current = shortRunOf(stateRef.current, opts.clipId);
          const takeHistory = [...(current.takeHistory ?? [])];
          if (!opts.diagnostic && current.previewUrl?.trim()) {
            takeHistory.unshift({ url: current.previewUrl.trim(), taskId: current.turboTaskId });
          }
          if (opts.engine !== 'wan' && opts.engine !== 'veo' && !imageDataUrl.startsWith('data:image')) {
            throw new Error('KF phải là data-URI — không gửi URL gốc vào Runway (0 cr).');
          }
          const shotRow = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
          const peopleN = shotRow ? visibleFrameCast(stateRef.current, shotRow).count : 0;
          const kf =
            opts.engine === 'wan' || opts.engine === 'veo'
              ? imageDataUrl
              : await prepareRunwayKf(imageDataUrl, opts.ratio, { people: peopleN });
          const sentDim = kf.startsWith('data:image')
            ? await measureKfImage(kf).catch(() => ({ width: 0, height: 0 }))
            : { width: 0, height: 0 };
          let sentCheck = inspectRunwayPayload(kf, opts.ratio);
          sentCheck = kfCheckFromMeasure(sentCheck, sentDim.width, sentDim.height);
          const exactPx =
            (sentDim.width === 1280 && sentDim.height === 720) ||
            (sentDim.width === 720 && sentDim.height === 1280);
          sentCheck = {
            ...sentCheck,
            width: sentDim.width || sentCheck.width,
            height: sentDim.height || sentCheck.height,
            reasons: exactPx
              ? sentCheck.reasons.filter((r) => !/pixel|1280|720|chưa đo/i.test(r))
              : sentCheck.reasons,
            ok:
              (exactPx || sentCheck.reasons.filter((r) => !/pixel|1280|720|chưa đo/i.test(r)).length === 0) &&
              exactPx,
            checks: [
              ...sentCheck.checks.filter((c) => c.id !== 'pixels' && c.id !== 'res'),
              {
                id: 'pixels',
                ok: exactPx,
                label:
                  sentDim.width && sentDim.height
                    ? `${sentDim.width}×${sentDim.height}`
                    : 'chưa đo pixel',
              },
            ],
          };
          if (opts.engine !== 'wan' && opts.engine !== 'veo' && !sentCheck.ok) {
            patchRun(opts.clipId, {
              kfCheck: sourceCheck,
              sentKfCheck: sentCheck,
              videoPipe: 'INPUT_INVALID',
              turboStatus: 'BLOCKED',
              turboError: sentCheck.reasons.join(' ') || 'Normalize JPEG 1280×720 thất bại.',
            });
            message.error(`Chưa gửi (0 cr): ${sentCheck.reasons.join(' ') || 'JPEG 1280×720 bắt buộc.'}`);
            return false;
          }
          const job = buildRunwayJob({
            shotId: opts.clipId,
            image: kf,
            prompt,
            duration: opts.seconds,
            ratio: opts.ratio,
            width: sentDim.width || sentCheck.width,
            height: sentDim.height || sentCheck.height,
          });
          if (opts.engine !== 'wan' && opts.engine !== 'veo' && !job.ok) {
            patchRun(opts.clipId, {
              kfCheck: sourceCheck,
              sentKfCheck: sentCheck,
              videoPipe: 'INPUT_INVALID',
              turboStatus: 'BLOCKED',
              turboError: job.blocked.reasons.join(' ') || 'RUNWAY BLOCKED',
            });
            message.error(`RUNWAY BLOCKED (0 cr): ${job.blocked.reasons.join(' · ')}`);
            return false;
          }
          if (!opts.diagnostic && sameRequestBlocked(current, job.fingerprint)) {
            if (cameraShift < maxCameraShift) {
              patchRun(opts.clipId, { i2vRetry: retryN + 1 });
              return sendTurbo({ ...opts, cameraShift: cameraShift + 1 });
            }
            message.warning('Hết lệch camera trên ảnh này. Tạo hình mới rồi duyệt — đừng Confirm cùng ảnh.');
            return false;
          }
          const started = await startContentSeriesTurbo({
            clipId: opts.clipId,
            prompt: job.exact.promptText,
            negativePrompt: opts.negative,
            imageDataUrl: kf,
            seconds: opts.seconds,
            ratio: opts.ratio,
            engine: opts.engine,
            confirm: true,
            maxCost: opts.maxCost,
            currency: opts.currency,
            lastFrameFromUrl:
              opts.engine === 'veo'
                ? undefined
                : opts.forceNew || opts.resume || opts.diagnostic
                  ? opts.lastFrameFromUrl
                  : opts.lastFrameFromUrl ||
                    lastFrameFromUrlOf(
                      stateRef.current,
                      productionShorts(stateRef.current).find((s) => s.id === opts.clipId) ||
                        ({ id: opts.clipId } as FamixaSeriesShot),
                    ),
          });
          createdTask = true;
          const estimatedCost = generateCost(opts.engine, opts.seconds).credits ?? undefined;
          const sentBytes = kf.startsWith('data:') ? Math.max(0, Math.floor((kf.length * 3) / 4) - 32) : sourceCheck.bytes;
          const keepTake = Boolean(opts.diagnostic && current.previewUrl?.trim());
          const frozenMotion = shotRow
            ? (() => {
                const liveRun = shortRunOf(stateRef.current, opts.clipId);
                const frozen = freezeMotionInput(stateRef.current, shotRow, {
                  provider: (() => {
                    const picked = selectFamixaProvider({
                      capability: 'MOTION',
                      engine: opts.engine,
                    }).providerId;
                    return picked === 'wan' ? 'wan' : picked === 'google' ? 'google' : 'runway';
                  })(),
                });
                const revisionId =
                  (frozen.pictureRevisionId || liveRun.pictureRevisionId || '').trim() ||
                  nextPictureRevisionId(opts.clipId, liveRun.pictureRevisionId);
                if (!liveRun.pictureRevisionId) {
                  patchRun(opts.clipId, { pictureRevisionId: revisionId });
                }
                return { ...frozen, pictureRevisionId: revisionId };
              })()
            : undefined;
          const nextN = (current.runwayAttempts?.at(-1)?.n ?? 0) + 1;
          const attempts = startRunwayAttempt(current, {
            attemptId: motionAttemptId(opts.clipId, nextN),
            taskId: started.taskId,
            selectionSnapshot: started.decisionId
              ? {
                  decisionId: started.decisionId,
                  providerId: started.providerId || motionProviderFromEngine(opts.engine),
                  modelId: started.model,
                  selectionMode: started.selectionMode || '',
                  reason: started.selectionReason || undefined,
                  estimatedCost: started.estimatedCost,
                  costKind: started.costKind || undefined,
                }
              : undefined,
            submitOk: true,
            status: started.status,
            estimatedCost,
            refundStatus: 'PENDING',
            billed: undefined,
            httpStatus: 200,
            model: started.model || 'gen4_turbo',
            duration: opts.seconds,
            ratio: opts.ratio,
            promptHash: job.exact.promptHash,
            fingerprint: job.fingerprint,
            frozenInput: frozenMotion,
            inputFingerprint: frozenMotion?.executionFingerprint,
            exactRequest: {
              promptText: job.exact.promptText,
              promptHash: job.exact.promptHash,
              kfHash: frozenMotion?.keyframePixelHash || job.exact.kfHash,
              model: job.exact.model,
              duration: job.exact.duration,
              ratio: job.exact.ratio,
              apiVersion: job.exact.apiVersion,
              compiler: job.exact.promptCompiler,
            },
            diagnostic: opts.diagnostic,
            source: {
              mime: sourceCheck.mime,
              bytes: sourceCheck.bytes,
              width: sourceCheck.width,
              height: sourceCheck.height,
              hash: frozenMotion?.keyframePixelHash || dataUriHash(imageDataUrl),
            },
            kf: {
              mime: 'image/jpeg',
              bytes: sentBytes,
              width: sentDim.width,
              height: sentDim.height,
              hash: frozenMotion?.keyframePixelHash || dataUriHash(kf),
            },
          });
          const diagRow = attemptToDiagRow(opts.clipId, attempts.at(-1)!, {
            kfId: current.keyframeFileName || opts.clipId,
          });
          patchRun(opts.clipId, {
            status: keepTake ? current.status : 'turbo_testing',
            model: started.model,
            previewUrl: keepTake ? current.previewUrl : undefined,
            takeUrl: keepTake ? current.takeUrl : current.takeUrl,
            takeHistory: takeHistory.slice(0, 6),
            turboTaskId: started.taskId,
            turboStatus: started.status,
            turboError: undefined,
            videoPipe: keepTake ? current.videoPipe || 'VIDEO_READY' : 'VIDEO_SUBMITTED',
            videoVerified: keepTake ? current.videoVerified : undefined,
            kfCheck: sourceCheck,
            sentKfCheck: sentCheck,
            kfRetryOk: false,
            runwayEstimated: estimatedCost,
            runwayRefund: 'PENDING',
            runwayAttempts: attempts,
            runwayDiagnostics: [...(current.runwayDiagnostics ?? []), diagRow].slice(-12),
          });
          if (started.usedPlaceholderImage) {
            message.warning('Không nhận được KF01 — gửi lại sau khi gắn ảnh cảnh.');
          }
          const ok = await pollTask(started.taskId);
          const after = shortRunOf(stateRef.current, opts.clipId);
          const last = latestAttempt(after);
          if (last) {
            const klass = classifyRunwayFailure({
              sentOk: true,
              httpStatus: last.httpStatus ?? 200,
              runwayStatus: after.turboStatus,
              failureCode: last.failureCode,
              error: after.turboError,
            });
            const ready = Boolean(ok || (keepTake && current.previewUrl));
            const failedStamp = ready
              ? { failedKfHash: undefined, failedPromptHash: undefined, runwayRefund: 'NONE' as const, renderFailure: false }
              : stampFailedInput(after, imageDataUrl, prompt);
            patchRun(opts.clipId, {
              previewUrl: keepTake ? current.previewUrl : after.previewUrl,
              takeUrl: keepTake ? current.takeUrl : after.takeUrl,
              videoPipe: keepTake && current.previewUrl ? 'VIDEO_READY' : after.videoPipe,
              videoVerified: keepTake ? current.videoVerified : after.videoVerified,
              runwayBilled: ready ? (current.runwayBilled ?? current.runwaySpent ?? 0) + (estimatedCost ?? 0) : current.runwayBilled,
              ...failedStamp,
              runwayAttempts: patchRunwayAttempt(after.runwayAttempts, started.taskId, {
                classification: klass,
                billed: ready ? estimatedCost : undefined,
                refundStatus: ready ? 'NONE' : 'REFUND_PENDING',
              }),
              runwayDiagnostics: [...(after.runwayDiagnostics ?? [])].map((row) =>
                row.runwayJobId === started.taskId
                  ? {
                      ...row,
                      runwayStatus: after.turboStatus,
                      outputUrl: last.outputUrl || after.previewUrl,
                      downloadOk: last.downloadOk,
                      videoBytes: last.videoBytes ?? after.videoBytes,
                      errorCode: last.failureCode,
                      errorMessage: after.turboError,
                      classification: klass,
                    }
                  : row,
              ),
            });
          }
          return ok;
        } catch (e) {
          lastText = explainTurboError(
            apiErrorMessage(e, 'Không gửi được I2V. Kiểm tra key Runway / Fal (Model AI → Video).'),
          );
          if (isTurboDailyQuota(lastText)) {
            const until = nextRunwayQuietUntil({ daily: true, prev: runwayQuietUntil });
            persistRunwayQuietUntil(until);
            setRunwayQuietUntil(until);
            patchRun(opts.clipId, { turboError: lastText, turboStatus: 'FAILED' });
            if (!opts.silent) message.error(lastText);
            return false;
          }
          if (canRetryTurboStart(lastText, createdTask) && attempt === 0) {
            const sec = parseRetryAfterSec(lastText, 30);
            patchRun(opts.clipId, {
              turboStatus: 'RETRY',
              turboError: `Runway 429 — đợi ${sec}s rồi gửi lại (chưa tạo job, 0 cr).`,
            });
            await new Promise((r) => setTimeout(r, sec * 1000));
            continue;
          }
          patchRun(opts.clipId, { turboError: lastText, turboStatus: 'FAILED' });
          if (!opts.silent) message.error(lastText);
          return false;
        }
      }
      patchRun(opts.clipId, { turboError: lastText || 'Runway chưa trả take.', turboStatus: 'FAILED' });
      return false;
    } finally {
      turboBusyRef.current = undefined;
      setTurboBusy(undefined);
      release();
    }
  };

  const resumePollBoot = useRef(false);
  useEffect(() => {
    if (resumePollBoot.current) return;
    const hung = productionShorts(stateRef.current).find((s) =>
      shouldResumeTurboPoll(shotRunOf(stateRef.current, s)),
    );
    if (!hung) return;
    resumePollBoot.current = true;
    void sendTurbo({
      clipId: hung.id,
      prompt: '',
      seconds: hung.seconds || 10,
      ratio: outputAspectOf(stateRef.current),
      engine: 'turbo',
      silent: true,
      resume: true,
    });
  }, [state.episode?.shots?.length]);

  const startTurbo = () => {
    if (refuseUnwiredVeo()) return;
    if (!active || !run) {
      message.warning('Chọn shot đang làm.');
      return;
    }
    const lock = lockFromGraph(state, active);
    const pre = studioI2vPrecheck({
      lock,
      action: run.shotAction,
      keyframeDataUrl: run.keyframeDataUrl,
      status: run.status,
      unlocked,
      sceneLocked: state.sceneLocked,
      scriptLocked: state.scriptLocked,
      shortsReady: canWorkScene(state),
      engine,
      hasEngineKey: engine === 'wan' ? keys.fal : engine === 'veo' ? keys.veo : keys.runway,
      state,
      shot: active,
      videoContext,
      run,
    });
    if (!pre.ok) {
      modal.warning({
        title: 'Pre-check chưa đạt — 0 cr',
        content: pre.items
          .filter((i) => !i.ok)
          .map((i) => i.label)
          .join(' · '),
      });
      return;
    }
    const cost = generateCost(engine, active.seconds);
    modal.confirm({
      title:
        engine === 'wan'
          ? 'Pre-check đạt · gửi Wan (Fal)'
          : engine === 'veo'
            ? 'Pre-check đạt · gửi Google Veo 3'
            : `Confirm credit · ước ${cost.credits} cr (5 cr/s)`,
      content: `${pre.warnings.length ? `${pre.warnings.join(' ')} ` : ''}${
        engine === 'wan'
          ? `${cost.label}. Status: PENDING đến khi SUCCEEDED + file đọc được. Không gửi lại cùng KF.`
          : engine === 'veo'
            ? `${cost.label}. 1 shot / 1 lần. Không gửi Runway / Wan thay thế.`
            : `RUNWAY COST — Estimated: ${cost.credits} cr · Status: PENDING đến khi SUCCEEDED + file đọc được. Lỗi generation = REFUND PENDING (Runway), KIT không ghi «đã trừ». Không gửi lại cùng KF.`
      }`,
      okText: engine === 'wan' ? 'Gửi Wan' : engine === 'veo' ? 'Gửi Veo 3' : `Confirm · ước ${cost.credits} cr`,
      cancelText: 'Không gửi',
      onOk: () =>
        sendTurbo({
          clipId: active.id,
          prompt: pre.prompt,
          negative: undefined,
          seconds: active.seconds,
          ratio: outputAspectOf(state),
          engine,
        }),
    });
  };

  const checkTurbo = (prompt: string, imageDataUrl?: string) => {
    const gate = preflightTurboSend({ prompt, imageDataUrl });
    if (!gate.ok) {
      message.error(`Check máy: chưa đạt (0 cr). ${gate.reasons.join(' ')}`);
      return;
    }
    if (gate.warnings.length) {
      message.warning(`Check máy: cảnh báo (0 cr). ${gate.warnings.join(' ')} Vẫn gửi được — moderation có thể trừ cr.`);
      return;
    }
    message.success(`Check máy: đạt (0 cr). Prompt ${gate.prompt.length} ký tự — có thể gửi Turbo.`);
  };

  const startShortTurbo = () => {
    if (!canWorkShorts(state)) {
      message.warning(
        !voiceProductionReady(state)
          ? 'Khóa Full Voice (thoại COMPLETE) trước khi tốn credit hình/video.'
          : 'Khóa kịch bản ở bước 1 rồi mới làm short.',
      );
      setStudioPane('script');
      return;
    }
    if (!ready || !activeShort) {
      message.warning('Điền vai rồi dán pack.');
      return;
    }
    const cost = runwayCredits(activeShort.seconds);
    const prompt = compileRunwayPromptV1({ action: activeShort.motionPrompt }).text;
    const gate = preflightTurboSend({ prompt, imageDataUrl: shortRun?.keyframeDataUrl });
    if (!gate.ok) {
      modal.warning({
        title: 'Check máy: chưa đạt — chưa trừ credit',
        content: gate.reasons.join(' '),
      });
      return;
    }
    modal.confirm({
      title: `Check đạt · gửi Turbo dọc ~${cost.credits} cr`,
      content: `Check trên máy: đạt (0 cr). Runway không check hộ trước. Sẽ gửi: ${gate.prompt}`,
      okText: `Gửi (~${cost.credits} cr)`,
      cancelText: 'Không gửi',
      onOk: () =>
        sendTurbo({
          clipId: activeShort.id,
          prompt,
          seconds: activeShort.seconds,
          ratio: '9:16',
          engine,
        }),
    });
  };

  const lockShort = () => {
    if (!activeShort || !shortRun) return;
    if (!reviewComplete(shortRun)) {
      message.warning('Tick đủ 4 câu rồi khóa short.');
      return;
    }
    const next: SeriesPilotState = {
      ...state,
      runs: {
        ...state.runs,
        [activeShort.id]: {
          ...shortRun,
          status: 'approved',
          credits: shortRun.runwaySpent ?? shortRun.credits ?? runwayCredits(activeShort.seconds).credits,
        },
      },
    };
    persistState(next);
    if (canWorkScene(next)) {
      message.success('Đã khóa hết short. Sang dựng cảnh 16:9.');
      setStudioPane('studio');
    }
  };

  const lockShot = () => {
    if (active) lockShotOf(active);
  };

  const lockShotOf = (shot: FamixaSeriesShot, opts?: { stay?: boolean; autoReview?: boolean }) => {
    const cur = stateRef.current;
    const take = shotRunOf(cur, shot);
    const reviewed = opts?.autoReview
      ? {
          ...take,
          review: { character: true, motion: true, emotion: true, canon: true },
        }
      : take;
    if (!reviewComplete(reviewed)) {
      message.warning('Tick đủ 4 câu review — hoặc mở Shot Detail.');
      setActiveId(shot.id);
      return;
    }
    const lockedRun = {
      ...reviewed,
      status: 'approved' as const,
      credits: take.runwaySpent ?? take.credits ?? runwayCredits(shot.seconds).credits,
    };
    let nextState: SeriesPilotState = applyShotLockToGraph(
      {
        ...cur,
        runs: {
          ...cur.runs,
          [shot.id]: lockedRun,
        },
      },
      shot,
      lockedRun.shotAction ?? take.shotAction,
    );
    const pack = episodeShots(nextState);
    const i = pack.findIndex((s) => s.id === shot.id);
    const next = pack[i + 1];
    if (next) nextState = bindShotToMemory(nextState, next);
    persistState(nextState);
    if (opts?.stay) {
      message.success(`PASS ${shCode(state, shot)}.`);
      return;
    }
    if (next) {
      setActiveId(next.id);
      message.success(`Đã khóa ${shCode(state, shot)}. Shot sau kế thừa Memory + KF cảnh.`);
    } else {
      setStudioPane('timeline');
      message.success(`Đã khóa ${shCode(state, shot)}. Timeline · Final — ghi chú ghép rồi khóa cảnh.`);
    }
  };

  const failShot = () => {
    if (!active) return;
    patchRun(active.id, { status: 'rejected' });
  };

  const setReview = (axis: SeriesReviewAxis, on: boolean) => {
    if (!active || !run) return;
    patchRun(active.id, {
      status: run.status === 'turbo_testing' || run.status === 'rejected' ? 'reviewed' : run.status,
      review: { ...run.review, [axis]: on },
    });
  };

  const lockScene = () => {
    if (!sceneReady) return;
    persistState({ ...state, sceneLocked: true });
    message.success('Đã khóa cảnh (Final). I2V tắt đến khi mở khóa trên Timeline.');
  };

  const receiveStaffPack = (): SeriesPilotState | undefined => {
    const live = stateRef.current;
    if (live.scriptLocked) {
      message.warning('Mở khóa kịch bản rồi mới nhận pack lại.');
      return undefined;
    }
    const raw = (live.packDraft || live.scriptDraft || '').trim();
    if (!raw) {
      message.warning('Dán kịch bản vào Nội dung chi tiết (hành động + thoại) trước.');
      return undefined;
    }
    let parsed = parseFamixaPack(raw);
    if (parsed.error) {
      const wrapped = wrapStaffScriptForParse({
        title: live.episode?.title || 'Tập mới',
        episode: live.episode?.episode || live.episode?.title || '100',
        body: raw,
      });
      if (wrapped !== raw) parsed = parseFamixaPack(wrapped);
    }
    if (parsed.error) {
      flushServerSaveRef.current();
      message.error(parsed.error);
      return undefined;
    }
    const n = (parsed.episode?.shots?.length ?? 0) + (parsed.shorts?.length ?? 0);
    if (n === 0) {
      flushServerSaveRef.current();
      message.warning('Chưa tách được shot từ nội dung. Thêm hành động + thoại rồi thử lại.');
      return undefined;
    }
    const next = persistState({ ...replaceStoryFromParse(live, parsed, raw), scriptDraft: raw });
    flushServerSaveRef.current();
    setPackText(raw);
    setProposeNote(
      `Đã tách ${parsed.scenes?.length || 0} cảnh · ${parsed.episode?.shots?.length || 0} shot. Duyệt rồi Hoàn tất. Chưa tạo ảnh/video.`,
    );
    message.success(`Đã nhận: ${packSummary(parsed)}. Duyệt nếu đúng — chưa khóa, chưa tạo ảnh.`);
    return next;
  };

  const confirmStaffScript = () => {
    let live = stateRef.current;
    const shotN =
      (live.shorts?.length ?? 0) +
      episodeShots(live).filter((s) => shotHasValidAction(s, shotRunOf(live, s))).length;
    if (shotN === 0) {
      const next = receiveStaffPack();
      if (!next) return;
      live = next;
      if ((live.scenes?.length ?? 0) > 0 && !live.storyReviewed) return;
    }
    if ((live.scenes?.length ?? 0) > 0 && !live.storyReviewed) {
      persistState({ ...live, storyReviewed: true });
      message.success('Đã duyệt kịch bản đã tách. Bấm Hoàn tất kịch bản lần nữa.');
      return;
    }
    if (!canConfirmStaffScript(live)) {
      message.warning(
        needsInheritanceReview(live)
          ? 'Duyệt kế thừa trạng thái tập trước trước khi hoàn tất.'
          : 'Chưa có shot/cảnh từ nội dung. Dán kịch bản vào Nội dung chi tiết (hành động + thoại) rồi Nhận pack.',
      );
      return;
    }
    persistState(primeLongShotsOnScriptLock({ ...live, storyReviewed: true, scriptLocked: true }));
    if (prodMode === 'director') {
      setProdTab('characters');
      message.success('Đã khóa chuyện. Sang Người — khóa mặt và giọng trước khi tạo hình.');
      return;
    }
    if (!live.castLocked) {
      setProdTab('characters');
      message.success('Đã khóa kịch bản. Sang Nhân vật — khóa Canon/giọng trước khi tạo hình.');
      return;
    }
    setProdTab('voice');
    message.success('Đã khóa kịch bản. Duyệt thoại — chưa tạo hình/video.');
  };

  const lockDirectorStory = () => {
    const live = stateRef.current;
    const shotCount =
      (live.shorts?.length ?? 0) +
      episodeShots(live).filter((s) => shotHasValidAction(s, shotRunOf(live, s))).length;
    const action = nextDirectorStoryAction({
      body: live.packDraft || live.scriptDraft || '',
      sceneCount: live.scenes?.length ?? 0,
      shotCount,
      storyReviewed: Boolean(live.storyReviewed),
      needsInheritReview: needsInheritanceReview(live) && !live.scriptLocked,
      scriptLocked: Boolean(live.scriptLocked),
      shotGraphLocked: live.shotGraphLocked !== false && shotCount > 0,
      aspectOk: live.outputAspect === '16:9' || live.outputAspect === '9:16' || !live.outputAspect,
    });
    if (action.kind === 'LOCKED') return;
    if (action.kind === 'PARSE') {
      receiveStaffPack();
      return;
    }
    if (action.kind === 'REVIEW_STORY') {
      persistState({ ...live, storyReviewed: true });
      message.success('Đã duyệt chuyện đã tách.');
      return;
    }
    if (action.kind === 'REVIEW_INHERIT') {
      const mem = live.storyMemory;
      if (!mem) return;
      persistState({ ...live, storyMemory: { ...mem, inheritReviewed: true } });
      message.success('Đã duyệt kế thừa.');
      return;
    }
    if (action.kind === 'LOCK_GRAPH') {
      lockShotGraph();
      return;
    }
    if (action.kind === 'ASPECT') {
      persistState({ ...live, outputAspect: live.outputAspect === '9:16' ? '9:16' : '16:9' });
      return;
    }
    confirmStaffScript();
    const after = stateRef.current;
    if (after.scriptLocked && after.shotGraphLocked === false) lockShotGraph();
  };

  const lockScript = () => {
    if (!canLockScript(state)) {
      message.warning(
        (state.scenes?.length ?? 0) > 0 && !state.storyReviewed
          ? 'Duyệt kịch bản đã tách (nút Duyệt trên tab Kịch bản) trước khi hoàn tất.'
          : needsInheritanceReview(state)
          ? 'Duyệt kế thừa trạng thái tập trước (card Trạng thái chuyện) trước khi khóa kịch bản.'
          : state.roles.length > 0 && !state.castLocked
          ? 'Khóa Cast & Canon (ảnh + giọng từng vai) trước.'
          : state.roles.length > 0 && !state.roles.every((r) => roleCanonReady(state, r))
            ? 'Gắn ảnh Character Canon cho đủ vai (mặt/tóc — không phải ảnh bàn ăn).'
            : 'Nhận pack (có short hoặc shot) và điền đủ vai nếu đã thêm vai.',
      );
      return;
    }
    persistState(primeLongShotsOnScriptLock({ ...state, scriptLocked: true }));
    setStudioPane('voice');
    message.success('Đã khóa kịch bản. Duyệt Full Voice — chưa tạo hình/video.');
  };

  const lockVoice = (opts?: { skipRegen?: boolean }) => {
    const why = voiceLockBlockReason(state);
    if (why && !opts?.skipRegen) {
      message.warning(
        !keys.elevenLabs && state.voicePreview?.status !== 'complete'
          ? 'Chưa có key ElevenLabs — Cấu hình AI, rồi bấm Tạo Full Voice.'
          : why,
      );
      return;
    }
    const script = deriveVoiceScript(state);
    if (opts?.skipRegen) {
      if (script.lines.length === 0 || script.lines.some((l) => !l.voiceId)) {
        message.warning(why ?? 'Gán Voice Canon đủ vai trước khi khóa.');
        return;
      }
    }
    persistState({
      ...stampDialoguePerformances(state),
      scriptLocked: true,
      voiceLocked: true,
      voicePreview: {
        ...(state.voicePreview ?? emptyVoicePreview(script)),
        status: 'complete',
        issues: [],
        operatorConfirmed: Boolean(opts?.skipRegen) || Boolean(state.voicePreview?.operatorConfirmed),
      },
    });
    setStudioPane('studio');
    message.success('VOICE LOCKED. Duyệt cách chia Short từ kịch bản — chưa tạo hình.');
  };

  const isStudio = studioPane === 'studio';
  const prodQueue = isStudio ? productionShorts(state) : [];
  const sceneShotKey = prodQueue.map((s) => s.id).join('|');
  useEffect(() => {
    if (!prodQueue.length) return;
    const ids = new Set(prodQueue.map((s) => s.id));
    const pick = prodQueue;
    setCutFrom((cur) => (cur && ids.has(cur) ? cur : pick[0]!.id));
    setCutTo((cur) => (cur && ids.has(cur) ? cur : pick[Math.min(9, pick.length - 1)]!.id));
  }, [sceneShotKey]);
  const cutRange = isStudio
    ? cutPick.length
      ? pickShots(prodQueue, cutPick)
      : shotsInInclusiveRange(prodQueue, cutFrom, cutTo)
    : [];
  const sceneKfPlan = isStudio ? buildSceneKfPlan(state, prodQueue) : [];
  const sceneVideo = isStudio ? readyV2VideoShots(state, cutRange.length ? cutRange : prodQueue) : { ready: [], blocked: [] };
  const sceneKfNew = isStudio ? sceneKfToGenerate(prodQueue, sceneKfPlan, state) : [];
  const sceneKfReuse = isStudio ? sceneKfPlan.filter((p) => p.lane === 'reuse').length : 0;
  const scenePlaceHint =
    active && lockFromGraph(state, active).locked
      ? continuityPlaceHint(lockFromGraph(state, active), active, shotRunOf(state, active).shotAction)
      : undefined;
  const hasVideoKey = famixaMotionEngineReady(engine, keys);
  const sceneBatchCredits =
    engine === 'veo'
      ? 0
      : famixaMotionBatchCost(
          engine,
          sceneVideo.ready.map((s) => s.seconds),
        ).credits ?? 0;
  const sceneBatchLabel =
    !hasVideoKey
      ? engine === 'wan'
        ? 'Cần key Fal (Cấu hình AI → Video)'
        : engine === 'veo'
          ? 'Cần file Join Veo (Cấu hình AI → Video)'
          : 'Cần key Runway (Cấu hình AI → Video)'
      : sceneVideo.ready.length === 0
        ? prodQueue.length === 0
          ? 'Chưa có Short từ kịch bản'
          : v2SceneBlockReason(state) ||
            (sceneVideo.blocked[0]
              ? `${sceneVideo.blocked.length} Short: ${sceneVideo.blocked[0].reason}`
              : 'Chưa có Short để gửi')
        : engine === 'veo'
          ? 'Google Veo 3 — Tạo video 1 shot trên bàn'
          : engine === 'wan'
          ? `Tạo video ${sceneVideo.ready.length} Short · Wan`
          : state.i2vProductionMode
            ? `BATCH · tối đa 3 · ước −${sceneBatchCredits} cr`
            : `SAFE MODE · 1 shot · Confirm credit`;

  const pictureIdentityIds = (
    st: SeriesPilotState,
    s: FamixaSeriesShot,
    pack: FamixaSeriesShot[],
    specOverride?: VisualSpec,
  ) => {
    const prev = previousSceneKf(st, s, pack);
    const card = compileShotSceneCard(st, s, prev?.shot);
    const spec = specOverride ?? card.visualSpec;
    const soloFrame = !spec.secondary.length && spec.subjectKind !== 'prop';
    return identityCanonIds(spec, soloFrame && spec.primary?.id ? [spec.primary.id] : card.cast.ids)
      .filter((id) => !/^CHAR-VO$/i.test(id) && !/loi binh|narrator/i.test(id));
  };

  const stillArgsFor = (
    s: FamixaSeriesShot,
    pack: FamixaSeriesShot[],
    continuityNote?: string,
    edit?: { correction?: string; failedKfUrl?: string },
  ) => {
    const st = stateRef.current;
    const loc = lockFromGraph(st, s);
    const master = sceneMasterOf(st, sceneIdOfShot(s));
    const prev = previousSceneKf(st, s, pack);
    const lock = visualLockShot(st, pack);
    const remake = Boolean(shotRunOf(st, s).kfForceNew);
    const card = compileShotSceneCard(st, s, prev?.shot);
    persistState(applyVisibleCast(st, s.id, card.cast.ids));
    const solo = soloCastFromNote(edit?.correction);
    const spec = solo ? applySoloCast(card.visualSpec, solo) : card.visualSpec;
    const soloFrame = Boolean(solo) || (!spec.secondary.length && spec.subjectKind !== 'prop');
    const count = peopleCountForSpec(spec);
    const wide =
      spec.framing === 'WIDE' || spec.framing === 'MEDIUM' || spec.framing === 'ESTABLISHING';
    const lockUrl = remake ? undefined : lock ? shotRunOf(st, lock).keyframeDataUrl : undefined;
    const prevUrl = remake ? undefined : prev?.run.keyframeDataUrl;
    const attachPrev =
      !remake &&
      Boolean(prevUrl?.startsWith('data:image')) &&
      !soloFrame &&
      spec.framing !== 'INSERT' &&
      spec.subjectKind !== 'prop' &&
      shouldAttachPrevKf(prev?.run.visualSpec?.framing, spec.framing);
    const placeLock = [
      master.location || loc.environment || s.location,
      master.lighting || card.lighting,
      prev ? `Same locked room as ${shCode(st, prev.shot)}. Do not copy that crop.` : '',
    ]
      .filter(Boolean)
      .join('. ');
    return {
      clipId: s.id,
      aspect: outputAspectOf(st),
      visual: placeLock,
      action: spec.shotAction || card.stillAction,
      location: [master.location || loc.environment || s.location, card.lighting].filter(Boolean).join('. '),
      characterIds: pictureIdentityIds(st, s, pack, spec),
      prevKfUrl: attachPrev ? prevUrl : undefined,
      sceneMasterUrl: lockUrl?.startsWith('data:image') ? lockUrl : undefined,
      inheritFromId: prev?.shot.id,
      peopleCount: count,
      peopleNames:
        count >= 2
          ? [spec.primary?.name, ...spec.secondary.filter((p) => p.face === 'full').map((p) => p.name)]
              .filter(Boolean)
              .join(', ') || card.cast.names.join(', ')
          : spec.primary?.name || card.cast.names[0] || '',
      atmosphere: compileShotStillMood(st, s, spec.shotAction || card.stillAction),
      lightingLock: card.lighting,
      speakers: spec.framing === 'INSERT' ? '' : card.speakerNames.join(', '),
      visualSpec: spec,
      continuityNote: [
        continuityNote?.trim(),
        wide ? peopleCountLock(card.cast, prev ? visibleFrameCast(st, prev.shot).count : undefined) : '',
      ]
        .filter(Boolean)
        .join(' '),
      correction: edit?.correction || (remake ? remakeStillCorrection(spec) : undefined),
      failedKfUrl: remake || soloFrame ? undefined : edit?.failedKfUrl,
    };
  };

  const sceneLabelOfShot = (shotId: string) => {
    const idx = shots.findIndex((s) => s.id === shotId);
    return `Cảnh ${String((idx >= 0 ? idx : 0) + 1).padStart(2, '0')}`;
  };

  const preflightForShot = (
    shotId: string,
    live?: { universe?: FamixaCharacterRecord[]; studioRows?: CharacterStudioRow[]; gemini?: boolean },
  ) => {
    const shot = shots.find((s) => s.id === shotId) || episodeShots(state).find((s) => s.id === shotId);
    const sceneId = shot ? sceneIdOfShot(shot) : '';
    const characterIds = shot
      ? pictureIdentityIds(state, shot, productionShorts(state))
      : [];
    const spoken = shot ? linesForShot(state, shot) : [];
    const shotVoiceOk =
      spoken.length === 0 ||
      spoken.every((l) => {
        const dur = Number(state.voiceAssets?.[l.id]?.duration || 0);
        return dur > 0.2 || Boolean(ttsFiles[l.id]?.url || ttsBlobs.current.has(l.id));
      });
    const directorCopyOk = prodMode === 'director' && directorShotHasSpokenCopy(shotId);
    const registry = live?.universe ?? universe;
    const studio = live?.studioRows ?? studioRows;
    const geminiOk = live?.gemini ?? (settingsHydrated ? keys.gemini : undefined);
    return canGenerateScene({
      characterIds,
      registry,
      studioRows: studio,
      scriptLocked: canOpenStudio(state),
      voiceReady: voiceProductionReady(state) || directorCopyOk || (prodMode === 'director' && shotVoiceOk),
      shotGraphLocked: state.shotGraphLocked !== false,
      sceneMasterLocked: sceneId ? sceneMasterOf(state, sceneId).locked : true,
      visualStyleKnown: projectStyle != null,
      visualStyleReady: Boolean(projectStyle?.ready),
      aspectOk: state.outputAspect === '16:9' || state.outputAspect === '9:16',
      hasGemini: geminiOk,
      shotAllowsGeneration: shot ? shotHasValidAction(shot, shotRunOf(state, shot)) : false,
      sceneLabel: sceneLabelOfShot(shotId),
      projectVisualMode: FAMIXA_VISUAL_MODE.visualMode,
      visualReferences: characterIds
        .filter((id) => !/^CHAR-VO$/i.test(id) && !/loi binh|narrator/i.test(id))
        .flatMap((id) => {
          const lock = findStudioLockRow(id, studio);
          if (!lock?.officialLocked) return [];
          const name = lock.characterName || sceneCharacterName(id, registry, studio);
          return [lockedStudioClaim(id, name, lock.masterSha256)];
        }),
    });
  };

  const openCharacterFromScene = (
    shotId: string,
    action: SceneImageBlocker['action'],
  ) => {
    const characterId = action.characterId || '';
    setStudioFocus({
      characterId,
      panel: action.id === 'open_character_universe' ? 'universe' : 'studio',
      returnTo: { shotId, sceneLabel: sceneLabelOfShot(shotId), tab: 'images' },
    });
    setProdTab('characters');
  };

  const handleSceneImageAction = (shotId: string, action: SceneImageBlocker['action']) => {
    const leave = (tab: SeriesStaffTab) => {
      setImageShotId(shotId);
      setGateReturn({ shotId, sceneLabel: sceneLabelOfShot(shotId) });
      setProdTab(tab);
    };
    const map: Record<SceneImageActionId, () => void> = {
      open_character_studio: () => openCharacterFromScene(shotId, action),
      open_character_view: () => openCharacterFromScene(shotId, action),
      open_character_universe: () => openCharacterFromScene(shotId, action),
      open_script: () => leave('script'),
      open_voice: () => leave('voice'),
      open_scenes: () => leave('scenes'),
      lock_voice: () => {
        const live = stateRef.current;
        if (canLockVoice(live) || live.voicePreview?.status === 'complete') {
          lockVoice(live.voicePreview?.status === 'complete' && !canLockVoice(live) ? { skipRegen: true } : undefined);
          return;
        }
        leave('voice');
      },
      approve_shot_graph: () => lockShotGraph(),
      set_aspect: () => {
        const aspect = action.aspect;
        if (aspect !== '16:9' && aspect !== '9:16') return;
        const live = stateRef.current;
        if (live.outputAspect === aspect) return;
        persistState({ ...live, outputAspect: aspect });
      },
      open_visual_style: () => {
        setStudioFocus({
          characterId: action.characterId || '',
          panel: 'universe',
          returnTo: { shotId, sceneLabel: sceneLabelOfShot(shotId), tab: 'images' },
        });
        setGateReturn({ shotId, sceneLabel: sceneLabelOfShot(shotId) });
        setProdTab('characters');
      },
      open_config: () => leave('overview'),
    };
    map[action.id]?.();
  };

  const returnToSceneFromStudio = () => {
    const back = studioFocus?.returnTo;
    setStudioFocus(undefined);
    void loadUniverse();
    loadStudioPreflight();
    if (!back) return;
    setProdTab(back.tab);
    setImageShotId(back.shotId);
  };

  const shotVoiceReadyForPicture = (shotId: string) => {
    const shot =
      episodeShots(stateRef.current).find((s) => s.id === shotId) ||
      (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
    if (!shot) return false;
    const snap = snapshotOfShot(stateRef.current, shot, ttsFilesRef.current, { hasVoiceFile });
    return snap.isSilent || snap.voiceReady;
  };

  const directorShotHasSpokenCopy = (shotId: string) => {
    const shot =
      episodeShots(stateRef.current).find((s) => s.id === shotId) ||
      (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
    if (!shot) return false;
    const spoken = linesForShot(stateRef.current, shot);
    if (spoken.length === 0) return true;
    return spoken.some((l) => (l.text || '').replace(/\s+/g, ' ').trim().length >= 4);
  };

  const usableRemakeStill = (run?: SeriesShotRun) =>
    Boolean(run?.keyframeDataUrl?.startsWith('data:image')) &&
    existingScenePipelineStatus(run) !== 'INVALID_REFERENCE_PIPELINE';

  const directorPictureHold = (shotId: string) => {
    const shot =
      episodeShots(stateRef.current).find((s) => s.id === shotId) ||
      (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
    const run = shot ? shotRunOf(stateRef.current, shot) : undefined;
    const remakeStill = usableRemakeStill(run);
    if (prodMode === 'director') {
      if (!shotVoiceReadyForPicture(shotId) && !remakeStill && !directorShotHasSpokenCopy(shotId)) {
        return {
          allowed: false,
          reason: 'Shot chưa có thoại.',
          hint: 'Gắn thoại trên Người rồi tạo hình.',
        };
      }
    } else if (!voiceProductionReady(stateRef.current)) {
      return {
        allowed: false,
        reason: 'Thoại của Episode chưa được khóa.',
        hint: 'Hoàn tất và khóa thoại trước khi tạo hình.',
      };
    }
    const pf = preflightForShot(shotId);
    if (!pf.allowed) return { allowed: false, ...directorPictureHoldCopy(pf.blockers[0]) };
    return { allowed: true };
  };

  const confirmCreateShotImage = (shotId: string) => {
    message.warning(productionOsLegacyRefuseNote('CREATE_IMAGE', 'POST /series/still'));
    if (productionOsLegacyPathClosed()) return;
    if (kfInflightRef.current[shotId]) {
      message.warning('Đang vẽ/chấm hình — chờ xong, không bấm lần 2.');
      return;
    }
    if (turboBusyRef.current) {
      message.warning('Đang tạo video. Không vẽ hình mới.');
      return;
    }
    void (async () => {
      let gemini = keys.gemini;
      let chars = universe;
      let studio = studioRows;
      if (!settingsHydrated || !gemini || !chars.length || !studio.length) {
        message.loading({ content: 'Đang nạp cấu hình vẽ hình…', key: 'fx-kf-pre', duration: 0 });
        try {
          if (!settingsHydrated || !gemini) {
            const [s, tenant] = await Promise.all([
              fetchContentSettings().catch(() => null),
              fetchTenantAiConfig().catch(() => []),
            ]);
            const nextKeys = providerKeyFlagsFromSources(s, tenant);
            gemini = nextKeys.gemini;
            keysRef.current = nextKeys;
            setKeys(nextKeys);
            setSettingsHydrated(true);
          }
          if (!chars.length) chars = await loadUniverse();
          if (!studio.length) studio = (await loadStudioPreflight()).items;
        } catch {
          /* preflight below */
        }
        message.destroy('fx-kf-pre');
      }
      const preflight = preflightForShot(shotId, { universe: chars, studioRows: studio, gemini });
      if (!preflight.allowed) {
        const copy = directorPictureHoldCopy(preflight.blockers[0]);
        if (prodMode !== 'director') {
          setProdTab('images');
          setImageShotId(shotId);
        }
        modal.warning({
          title: copy.reason,
          content: copy.hint,
          okText: 'Đóng',
          zIndex: 3100,
          getContainer: () => document.body,
        });
        return;
      }
      modal.confirm({
        title: 'Tạo ảnh cảnh này?',
        content: 'Gemini vẽ 1 khung hình theo kịch bản + diễn xuất đã lưu + Canon. Không trừ Runway.',
        okText: 'Tạo ảnh',
        cancelText: 'Hủy',
        zIndex: 3100,
        getContainer: () => document.body,
        onOk: () => {
          void generateSceneKf([shotId]);
        },
      });
    })();
  };

  const generateSceneKf = (onlyIds?: string[]) => {
    message.warning(productionOsLegacyRefuseNote('CREATE_IMAGE', 'POST /series/still'));
    if (productionOsLegacyPathClosed()) return;
    if (onlyIds && onlyIds.length === 1 && kfInflightRef.current[onlyIds[0]!]) {
      message.warning('Đang vẽ/chấm hình — chờ xong, không bấm lần 2.');
      return;
    }
    if (turboBusyRef.current) {
      message.warning('Đang tạo video. Không vẽ hình mới.');
      return;
    }
    if (onlyIds && onlyIds.length === 1) {
      const pf = preflightForShot(onlyIds[0]);
      if (!pf.allowed) {
        setImageShotId(onlyIds[0]);
        if (prodMode !== 'director') setProdTab('images');
        return;
      }
    }
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản rồi mới dựng cảnh.');
      return;
    }
    if (!voiceProductionReady(state)) {
      const oneId = onlyIds?.[0];
      const oneShot = oneId
        ? episodeShots(stateRef.current).find((s) => s.id === oneId) ||
          (stateRef.current.episode?.shots ?? []).find((s) => s.id === oneId)
        : undefined;
      const remakeStill = usableRemakeStill(oneShot ? shotRunOf(stateRef.current, oneShot) : undefined);
      const shotOk =
        prodMode === 'director' &&
        onlyIds && onlyIds.length === 1 &&
        (shotVoiceReadyForPicture(onlyIds[0]!) || remakeStill || directorShotHasSpokenCopy(onlyIds[0]!));
      if (!shotOk) {
        message.warning('Duyệt thoại (VOICE LOCKED) trước khi tạo hình.');
        if (prodMode !== 'director') setStudioPane('voice');
        return;
      }
    }
    if (state.shotGraphLocked === false) {
      message.warning('Duyệt cách chia Short rồi mới tạo hình. KIT không vẽ Short rỗng.');
      return;
    }
    const packNow = productionShorts(stateRef.current);
    const unlocked = [...new Set(packNow.map(sceneIdOfShot))].filter((sc) => !sceneMasterOf(stateRef.current, sc).locked);
    if (onlyIds && onlyIds.length === 1) {
      const oneShot =
        packNow.find((s) => s.id === onlyIds[0]) ||
        episodeShots(stateRef.current).find((s) => s.id === onlyIds[0]);
      const oneScene = oneShot ? sceneIdOfShot(oneShot) : '';
      if (oneScene && !sceneMasterOf(stateRef.current, oneScene).locked) {
        message.warning(`Khóa Scene Master (${oneScene}) trước khi tạo hình.`);
        return;
      }
    } else if (unlocked.length) {
      message.warning(`Khóa Scene Master (${unlocked.join(', ')}) trước khi tạo hình.`);
      return;
    }
    if (state.outputAspect !== '16:9' && state.outputAspect !== '9:16') {
      message.warning('Chọn khung xuất 16:9 hoặc 9:16 trên Scene Master trước khi vẽ KF.');
      return;
    }
    if (!keysRef.current.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon.');
      return;
    }
    return (async () => {
      message.loading({ content: 'Đang tạo hình…', key: 'fx-kf-gen', duration: 0 });
      try {
      let st = await hydratePilotCanon(stateRef.current);
      if (st !== stateRef.current) st = persistState(st);
      st = persistState(applyContinuityChain(st, productionShorts(st)));
      let pack = productionShorts(st);
      let plan = buildSceneKfPlan(st, pack);
      let todo = sceneKfToGenerate(pack, plan, st);
      const lock = visualLockShot(st, pack);
      if (onlyIds?.length) {
        todo = pack.filter((s) => onlyIds.includes(s.id) && shotHasValidAction(s, shotRunOf(st, s)) && !shotRunOf(st, s).prodSkip);
        for (const s of todo) patchRun(s.id, { kfForceNew: true, i2vRetry: 0 });
        st = stateRef.current;
        pack = productionShorts(st);
        plan = buildSceneKfPlan(st, pack);
        todo = pack.filter((s) => onlyIds.includes(s.id) && shotHasValidAction(s, shotRunOf(st, s)) && !shotRunOf(st, s).prodSkip);
      } else {
        const seq = new Set(sequentialKfIds(st, pack));
        todo = todo.filter((s) => seq.has(s.id) && shotHasValidAction(s, shotRunOf(st, s)));
        todo = todo.filter((s) => {
          if (lock && s.id === lock.id) return false;
          return !isLockedTemplateKf(shotRunOf(st, s)) && !isOperatorSuppliedKf(shotRunOf(st, s));
        });
      }
      if (todo.length === 0) {
        message.warning({
          content: onlyIds?.length
            ? 'Short này HOLD/SKIP hoặc không có Action — không vẽ KF.'
            : `Không có Short nào cần tạo hình (${plan.filter((p) => p.eligible).length} Short hợp lệ).`,
          key: 'fx-kf-gen',
        });
        return;
      }
      const one = nextShotNeedingKf(todo, todo.map((s) => s.id), lock?.id) || todo[0]!;
      todo = [one];
      const reuseIds = new Set(
        onlyIds?.length ? [] : plan.filter((p) => p.lane === 'reuse' && p.eligible).map((p) => p.shotId),
      );
      const reuseN = todo.filter((s) => reuseIds.has(s.id)).length;
      const draw = todo.filter((s) => !reuseIds.has(s.id));
      persistState(applySceneKfReuses(stateRef.current, pack, plan));
      if (reuseN && !draw.length) {
        persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
        message.success({ content: `REUSE ${shCode(st, one)} — 0 Gemini. Duyệt KF rồi mới shot tiếp.`, key: 'fx-kf-gen' });
        return;
      }
      if (draw.some((s) => kfInflightRef.current[s.id])) {
        message.warning({ content: 'Đang vẽ 1 KF — chờ xong.', key: 'fx-kf-gen' });
        return;
      }
      for (const s of draw) {
        if (!shotHasValidAction(s, shotRunOf(stateRef.current, s))) continue;
        const kind = await generateKfFromCanon(stillArgsFor(s, pack));
        if (kind === 'DUPLICATE_PIXEL') return;
        if (kind !== 'NEW_PENDING_PICTURE') return;
        const qa = shotRunOf(stateRef.current, s).visualQa;
        if (qa && !visualQaAllowsApprove(qa)) {
          message.warning({
            content: `${shCode(stateRef.current, s)} chưa PASS — sửa đúng lỗi, đừng tạo shot tiếp.`,
            key: 'fx-kf-gen',
          });
          return;
        }
      }
      persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
      } catch (e) {
        message.error({
          content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không tạo được hình.'),
          key: 'fx-kf-gen',
        });
      }
    })();
  };

  const regenerateSelectedKf = (ids: string[], continuityNote?: string) => {
    if (!ids.length) {
      message.warning('Chọn shot trên KF Review.');
      return;
    }
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon.');
      return;
    }
    void (async () => {
      let st = await hydratePilotCanon(stateRef.current);
      if (st !== stateRef.current) st = persistState(st);
      const pack = productionShorts(st);
      const requested = pack.filter((s) => ids.includes(s.id));
      const targets = requested.filter((s) => shotHasValidAction(s, shotRunOf(st, s)) && !shotRunOf(st, s).prodSkip);
      if (!targets.length) {
        message.warning(
          requested.length
            ? 'Shot đã chọn đang HOLD/SKIP — không vẽ KF.'
            : 'Không tìm thấy Short để tạo lại.',
        );
        return;
      }
      const one = targets[0]!;
      const failed = shotRunOf(st, one).keyframeDataUrl;
      if (kfInflightRef.current[one.id]) {
        message.warning('Đang vẽ 1 KF — chờ xong.');
        return;
      }
      const run = shotRunOf(st, one);
      const spec = run.visualSpec ?? stillArgsFor(one, pack).visualSpec;
      const correction =
        continuityNote?.trim() ||
        (spec && run.visualQa?.hardFails.length
          ? compileCorrectionPrompt(spec, [], run.visualQa.evidence || run.visualQa.notes)
          : undefined);
      const solo = soloCastFromNote(correction);
      const nextSpec = spec && solo ? applySoloCast(spec, solo) : spec;
      patchRun(one.id, {
        kfForceNew: true,
        i2vRetry: 0,
        kfTechNote: correction,
        visualSpec: nextSpec,
      });
      const kind = await generateKfFromCanon(
        stillArgsFor(one, pack, undefined, {
          correction,
          failedKfUrl: failed,
        }),
      );
      if (kind === 'DUPLICATE_PIXEL') return;
      if (kind !== 'NEW_PENDING_PICTURE') return;
      persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
    })();
  };

  const approveSceneKf = (ids?: string[]) => {
    const gates = Object.fromEntries(CONTINUITY_GATES.map((g) => [g.id, true]));
    const range = shotsInInclusiveRange(prodQueue, cutFrom, cutTo);
    const pack = ids?.length ? prodQueue.filter((s) => ids.includes(s.id)) : range;
    const chained = applyContinuityChain(state, productionShorts(state));
    const links = buildContinuityChain(chained, productionShorts(chained));
    const runs = { ...chained.runs };
    let n = 0;
    let blocked = 0;
    const reasons: string[] = [];
    for (const s of pack) {
      const run = shotRunOf(chained, s);
      if (!run.keyframeDataUrl || run.status === 'approved' || run.kfApproved) continue;
      const spec = run.visualSpec ?? compileShotSceneCard(chained, s, previousSceneKf(chained, s, productionShorts(chained))?.shot).visualSpec;
      const qa = run.visualQa ?? seedQaChecks(spec);
      const why = approveBlockReason(qa);
      if (why) {
        blocked += 1;
        if (reasons.length < 3) reasons.push(`${shCode(chained, s)}: ${why}`);
        runs[s.id] = { ...run, visualSpec: spec, visualQa: qa };
        continue;
      }
      const decided = applyPictureApproveGuard(run, s.id, run.keyframeDataUrl);
      if (decided.kind === 'DUPLICATE_PIXEL') {
        blocked += 1;
        if (reasons.length < 3) reasons.push(`${shCode(chained, s)}: ${PICTURE_DUPLICATE_COPY}`);
        continue;
      }
      if (decided.kind !== 'APPROVE_NEW') continue;
      const link = links.find((l) => l.shotId === s.id);
      runs[s.id] = {
        ...decided.run,
        status: run.status === 'turbo_testing' || run.status === 'reviewed' ? run.status : 'keyframe_ready',
        continuity: run.continuity ?? gates,
        startState: run.startState ?? link?.start,
        endState: run.endState ?? link?.end,
        transitionType: run.transitionType ?? link?.transitionType,
        stateLocked: true,
        visualSpec: spec,
        visualQa: qa,
      };
      n += 1;
    }
    persistState({ ...chained, runs });
    if (blocked) {
      message.warning(
        n
          ? `Duyệt ${n} KF. ${blocked} bị chặn Image QA — ${reasons.join(' · ')}`
          : `Không duyệt KF: Image QA chưa PASS. ${reasons.join(' · ')}`,
      );
      return;
    }
    if (n) {
      message.success(`Đã duyệt ${n} KF — Start/End đã khóa.`);
      return;
    }
    const already = pack.filter((s) => {
      const run = shotRunOf(chained, s);
      return Boolean(run.keyframeDataUrl) && (run.kfApproved || run.status === 'approved');
    });
    if (already.length) {
      message.info(
        already.length === 1
          ? `${shCode(chained, already[0]!)} đã duyệt rồi. Sang Video nếu cần take / khớp môi.`
          : `${already.length} KF đã duyệt rồi. Sang Video nếu cần take / khớp môi.`,
      );
      return;
    }
    message.warning('Chưa có KF để duyệt.');
  };

  const recoverTake = async (clipId: string) => {
    const run = shortRunOf(stateRef.current, clipId);
    const url = (resolveTakeUrl(run) || latestAttempt(run)?.outputUrl || '').trim();
    if (!url) {
      message.warning('Không có output URL để đọc lại. Mở Nhật ký — đừng Gửi lại.');
      return;
    }
    patchRun(clipId, { videoPipe: 'VIDEO_DOWNLOADING', turboError: undefined });
    try {
      const probed = await probeContentSeriesTake(url);
      if (!probed.ok) {
        const lastText = explainTurboError(probed.error || 'URL cũ không đọc được.');
        patchRun(clipId, { videoPipe: 'DOWNLOAD_FAILED', videoVerified: false, turboError: lastText });
        message.error(lastText);
        return;
      }
      patchRun(clipId, {
        status: 'turbo_testing',
        previewUrl: url,
        turboStatus: 'SUCCEEDED',
        turboError: undefined,
        videoPipe: 'VIDEO_READY',
        motionNeedsRemake: false,
        actingBeatPendingMotion: false,
        videoVerified: true,
        videoBytes: probed.bytes ?? undefined,
        videoMime: probed.mime ?? undefined,
        runwayAttempts: patchRunwayAttempt(run.runwayAttempts, run.turboTaskId, {
          outputUrl: url,
          downloadOk: true,
          videoBytes: probed.bytes ?? undefined,
          videoMime: probed.mime ?? undefined,
        }),
      });
      message.success('Đã gắn file từ URL cũ — 0 cr.');
    } catch (e) {
      const lastText = explainTurboError(apiErrorMessage(e, 'Không đọc được URL cũ.'));
      patchRun(clipId, { videoPipe: 'DOWNLOAD_FAILED', videoVerified: false, turboError: lastText });
      message.error(lastText);
    }
  };

  const startAbDiagnostic = (successId: string, failId: string) => {
    const st = stateRef.current;
    const pack = productionShorts(st);
    const success = pack.find((s) => s.id === successId);
    const fail = pack.find((s) => s.id === failId);
    if (!success || !fail) {
      message.warning('Chọn 1 shot READY và 1 shot INTERNAL.');
      return;
    }
    const cr =
      (generateCost('turbo', success.seconds).credits ?? 0) + (generateCost('turbo', fail.seconds).credits ?? 0);
    const total = cr * 3;
    modal.confirm({
      title: 'A/B Runway · 6 job · JPEG 1280×720',
      content: `3× ${shCode(st, success)} (READY) + 3× ${shCode(st, fail)} (lỗi). Cùng normalize JPEG, model gen4_turbo, duration, prompt từng KF. Tuần tự, không batch, không xóa take READY. Ước tính −${total} cr.`,
      okText: `CONFIRM 6 job · −${total} cr`,
      cancelText: 'Không gửi',
      onOk: async () => {
        const jobs = [success, success, success, fail, fail, fail];
        for (let i = 0; i < jobs.length; i += 1) {
          const shot = jobs[i]!;
          const after = stateRef.current;
          const run = shotRunOf(after, shot);
          const loc = lockFromGraph(after, shot);
          const lock = { ...loc, locked: loc.locked || Boolean(loc.environment) };
          const pre = studioI2vPrecheck({
            lock,
            action: i2vActionOf(after, shot, run),
            keyframeDataUrl: run.keyframeDataUrl,
            status: run.status === 'story_locked' ? 'keyframe_ready' : run.status,
            unlocked: true,
            sceneLocked: after.sceneLocked,
            scriptLocked: after.scriptLocked,
            shortsReady: canWorkV2Scene(after),
            engine: 'turbo',
            hasEngineKey: keys.runway,
            state: after,
            shot,
            videoContext,
            run,
          });
          if (!pre.ok) {
            message.error(`Dừng A/B tại ${shCode(after, shot)}: ${pre.items.filter((x) => !x.ok).map((x) => x.label).join(' · ')}`);
            break;
          }
          message.loading({ content: `A/B ${i + 1}/6 · ${shCode(after, shot)}`, key: 'ab-runway', duration: 0 });
          await sendTurbo({
            clipId: shot.id,
            prompt: pre.prompt,
            seconds: providerDurationOf(after, shot),
            ratio: outputAspectOf(after),
            engine: 'turbo',
            silent: true,
            forceNew: true,
            diagnostic: true,
          });
          if (i < jobs.length - 1) await new Promise((r) => window.setTimeout(r, 8000));
        }
        const rows = productionShorts(stateRef.current).flatMap((s) => shotRunOf(stateRef.current, s).runwayDiagnostics ?? []).filter((r) => r.diagnostic);
        const sum = summarizeAbDiagnostic(rows.slice(-6));
        message.destroy('ab-runway');
        modal.info({
          title: sum.verdict === 'INTERMITTENT' ? 'Kết luận: Runway ngẫu nhiên' : sum.verdict === 'INPUT' ? 'Kết luận: lỗi KF/input' : 'A/B chưa đủ dữ liệu',
          content: sum.lines.join('\n'),
        });
      },
    });
  };

  const retryMotionWithNewCamera = (shotId: string) => {
    startShotMotion(shotId);
  };

  const startSceneTurbo = (onlyIds?: string[], opts?: { remake?: boolean; cameraShift?: number }) => {
    if (refuseUnwiredVeo()) return;
    if (engine === 'veo' && (onlyIds?.length ?? 0) !== 1) {
      message.warning('Google Veo 3: gửi 1 shot / 1 lần. Dùng Tạo video trên bàn shot. Không gửi hàng loạt.');
      return;
    }
    const quietMin = runwayQuietRemainMin(runwayQuietUntil);
    if (opts?.remake && (onlyIds?.length ?? 0) !== 1) {
      const pack = productionShorts(stateRef.current);
      const ids = onlyIds ?? [];
      const shots = (ids.length ? pack.filter((s) => ids.includes(s.id)) : pack).filter((s) => {
        const run = shotRunOf(stateRef.current, s);
        if (!run.keyframeDataUrl || !kfIsApproved(run) || run.prodSkip || !shotHasValidAction(s, run)) return false;
        const promptHash = shotI2vPromptHash(stateRef.current, s, run);
        if (sameFailedInput(run, dataUriHash(run.keyframeDataUrl), promptHash)) return false;
        if (resolveTakeUrl(run)) return true;
        return Boolean(lastGenerationFail(run));
      });
      if (!shots.length) {
        message.warning('Không gửi lại shot FAIL cùng KF. Chỉ Tạo lại take đã READY — hoặc sửa KF rồi Confirm 1 job.');
        return;
      }
      if (quietMin) {
        message.warning('KIT còn khóa gửi job mới vì 429 cũ. Bấm «Đã lên tier — mở gửi» rồi Tạo lại từng hàng.');
        return;
      }
      const capped = capRunwayBatch(shots, stateRef.current.i2vProductionMode === true);
      const batch = famixaMotionBatchCost(
        engine,
        capped.map((s) => providerDurationOf(stateRef.current, s)),
      );
      const total = batch.credits ?? 0;
      modal.confirm({
        title: engine === 'wan' ? `Tạo lại ${capped.length} take · ${batch.label}` : `Tạo lại ${capped.length} take · ước ${total} cr`,
        content:
          'Chỉ take READY. Shot INTERNAL.BAD_OUTPUT không nằm trong list. Estimated — Status PENDING đến SUCCEEDED + file. Fail → BATCH PAUSED.',
        okText: engine === 'wan' ? `Confirm · ${batch.label}` : `Confirm · ước ${total} cr`,
        cancelText: 'Hủy — 0 cr',
        onOk: async () => {
          for (const shot of capped) {
            const run = shotRunOf(stateRef.current, shot);
            const ok = await sendTurbo({
              clipId: shot.id,
              prompt: i2vActionOf(stateRef.current, shot, run) || run.shotAction || shot.story || '',
              seconds: providerDurationOf(stateRef.current, shot),
              ratio: outputAspectOf(stateRef.current),
              engine,
              silent: true,
              forceNew: true,
            });
            if (!ok) {
              message.error(`BATCH PAUSED tại ${shCode(stateRef.current, shot)} — không gửi shot sau.`);
              break;
            }
          }
        },
      });
      return;
    }
    if (onlyIds && onlyIds.length === 1) {
      const one =
        productionShorts(stateRef.current).find((s) => s.id === onlyIds[0]) ||
        (stateRef.current.episode?.shots ?? []).find((s) => s.id === onlyIds[0]);
      const replaceTake = Boolean(opts?.remake || (one && shotRunOf(stateRef.current, one).motionNeedsRemake));
      if (one && (opts?.cameraShift ?? 0) === 0) {
        const lockedRun = shotRunOf(stateRef.current, one);
        const lockedHash = shotI2vPromptHash(stateRef.current, one, lockedRun);
        if (sameFailedInput(lockedRun, dataUriHash(lockedRun.keyframeDataUrl), lockedHash)) {
          startSceneTurbo(onlyIds, { ...opts, cameraShift: 1 });
          return;
        }
      }
      const retry = one
        ? canManualRetry(shotRunOf(stateRef.current, one), shotI2vPromptHash(stateRef.current, one, shotRunOf(stateRef.current, one)))
        : { ok: false, kind: 'none' as const };
      if (retry.kind === 'recover' && one && !replaceTake && (onlyIds?.length ?? 0) !== 1) {
        void recoverTake(one.id);
        return;
      }
    }
    if (quietMin) {
      const one = onlyIds && onlyIds.length === 1 ? productionShorts(stateRef.current).find((s) => s.id === onlyIds[0]) : undefined;
      const resumeOne = Boolean(one && shouldResumeTurboPoll(shotRunOf(stateRef.current, one)));
      if (!resumeOne) {
        message.warning('KIT còn khóa gửi job mới vì 429 cũ. Bấm «Đã lên tier — mở gửi» trên bước Video. Hỏi lại · 0 cr không bị khóa.');
        return;
      }
    }
    const explicitOne = onlyIds && onlyIds.length === 1;
    if (!canWorkV2Scene(state)) {
      const muteShot = onlyIds?.[0]
        ? episodeShots(stateRef.current).find((s) => s.id === onlyIds[0]) ||
          (stateRef.current.episode?.shots ?? []).find((s) => s.id === onlyIds[0])
        : undefined;
      const muteRun = muteShot ? shotRunOf(stateRef.current, muteShot) : undefined;
      const directorMuteKf = Boolean(
        muteRun?.keyframeDataUrl?.startsWith('data:image') && kfIsApproved(muteRun),
      );
      const directorI2v =
        prodMode === 'director' &&
        explicitOne &&
        Boolean(onlyIds?.[0] && (shotVoiceReadyForPicture(onlyIds[0]!) || directorMuteKf));
      const staffI2v = explicitOne && Boolean(state.scriptLocked) && !needsInheritanceReview(state);
      if (!directorI2v && !staffI2v) {
        message.warning(v2SceneBlockReason(state) ?? 'Chưa mở dựng cảnh.');
        if (prodMode !== 'director') setStudioPane(studioFallbackPane(state));
        return;
      }
    }
    if (state.sceneLocked) {
      message.warning('Scene đã Final. Mở khóa cảnh trên Timeline trước khi tạo hết shot.');
      return;
    }
    persistState(applyContinuityChain(stateRef.current, productionShorts(stateRef.current)));
    const pack = productionShorts(stateRef.current);
    let { ready, blocked } = readyV2VideoShots(stateRef.current, pack);
    if (onlyIds?.length) {
      ready = ready.filter((s) => onlyIds.includes(s.id));
      blocked = blocked.filter((b) => onlyIds.includes(b.shot.id));
    }
    if (explicitOne && ready.length === 0) {
      const one =
        pack.find((s) => s.id === onlyIds![0]) ||
        (stateRef.current.episode?.shots ?? []).find((s) => s.id === onlyIds![0]);
      const run = one ? shotRunOf(stateRef.current, one) : undefined;
      if (one && run?.keyframeDataUrl?.startsWith('data:image') && kfIsApproved(run) && !run.prodSkip) {
        ready = [one];
        blocked = [];
      }
    }
    if (onlyIds && onlyIds.length === 1 && turboLaunchRef.current.has(onlyIds[0]!)) {
      turboLaunchRef.current.delete(onlyIds[0]!);
    }
    if ((onlyIds?.length ?? 0) !== 1 && turboLaunchRef.current.size) {
      message.warning('Đang gửi hàng loạt. Gửi lại từng hàng lỗi — KIT xếp sau clip hiện tại.');
      return;
    }
    const unlockedMaster = [...new Set(ready.map(sceneIdOfShot))].filter(
      (sc) => !sceneMasterOf(stateRef.current, sc).locked,
    );
    if (unlockedMaster.length && (onlyIds?.length ?? 0) !== 1) {
      message.warning(`Khóa Scene Master (${unlockedMaster.join(', ')}) trước khi gửi I2V.`);
      return;
    }
    if (ready.length === 0) {
      message.warning(
        blocked.length
          ? `Chưa gửi được. ${blocked.map((b) => `${shCode(stateRef.current, b.shot)}: ${b.reason}`).join(' · ')}`
          : 'Không còn Short cần I2V.',
      );
      return;
    }
    const resumePack = ready.filter((s) => shouldResumeTurboPoll(shotRunOf(stateRef.current, s)));
    const blockedInternal = ready.filter((s) =>
      sameFailedInput(
        shotRunOf(stateRef.current, s),
        dataUriHash(shotRunOf(stateRef.current, s).keyframeDataUrl),
        shotI2vPromptHash(stateRef.current, s, shotRunOf(stateRef.current, s)),
      ),
    );
    const newPack = ready.filter((s) => {
      const run = shotRunOf(stateRef.current, s);
      return (
        !shouldResumeTurboPoll(run) &&
        !sameFailedInput(run, dataUriHash(run.keyframeDataUrl), shotI2vPromptHash(stateRef.current, s, run))
      );
    });
    if (blockedInternal.length && !newPack.length && !resumePack.length && !explicitOne) {
      message.warning(
        `INTERNAL.BAD_OUTPUT trên ${blockedInternal.map((s) => shCode(stateRef.current, s)).join(', ')} — circuit breaker. Không gửi lại cùng KF. Sửa KF → duyệt → Confirm 1 job.`,
      );
      return;
    }
    if (onlyIds && onlyIds.length === 1 && resumePack.length === 1 && newPack.length === 0) {
      const shot = resumePack[0]!;
      const run = shotRunOf(stateRef.current, shot);
      const replaceTake = Boolean(opts?.remake || run.motionNeedsRemake || (opts?.cameraShift ?? 0) > 0);
      if (!replaceTake) {
        void sendTurbo({
          clipId: shot.id,
          prompt: i2vActionOf(stateRef.current, shot, run) || run.shotAction || shot.story || '',
          seconds:
            engine === 'veo'
              ? veoMappedDurationSec(providerDurationOf(stateRef.current, shot), dialogueFloorOf(stateRef.current, shot))
              : providerDurationOf(stateRef.current, shot),
          ratio: outputAspectOf(stateRef.current),
          engine,
          resume: true,
        });
        return;
      }
    }
    const production = stateRef.current.i2vProductionMode === true;
    const sendNew = explicitOne
      ? newPack.length || resumePack.length
        ? newPack
        : ready
      : capRunwayBatch(newPack, production);
    const queue = explicitOne ? ready : [...resumePack, ...sendNew.filter((s) => !resumePack.some((r) => r.id === s.id))];
    const batch = famixaMotionBatchCost(
      engine,
      sendNew.map((s) => s.seconds),
    );
    const total = batch.credits ?? 0;
    const wan = engine === 'wan';
    const readyIds = queue.map((s) => s.id);
    for (const id of readyIds) turboLaunchRef.current.add(id);
    modal.confirm({
      title: wan
        ? `Tạo video ${queue.length} Short · Wan`
        : resumePack.length && !sendNew.length
          ? `Hỏi lại ${resumePack.length} task cũ · 0 cr`
          : production
            ? `BATCH ${sendNew.length}/3 · ước ${total} cr`
            : `SAFE MODE · 1 shot · ước ${total} cr`,
      content: blocked.length
        ? `Chỉ gửi Short đã duyệt. Bỏ qua: ${blocked.map((b) => shCode(stateRef.current, b.shot)).join(', ')}. Estimated — PENDING đến file hợp lệ. 1 FAIL = BATCH PAUSED.`
        : resumePack.length && !sendNew.length
          ? 'Task cũ còn trên Runway. KIT hỏi lại file — không POST job mới, không trừ cr.'
          : wan
            ? `SAFE mặc định 1 shot. Production tối đa 3. Fail → dừng, không gửi shot sau. ${batch.label} · chưa phải đã trừ.`
            : `SAFE mặc định 1 shot. Production tối đa 3. Fail → dừng, không gửi shot sau. Estimated ${total} cr · chưa phải đã trừ.`,
      okText: 'CONFIRM & GENERATE',
      cancelText: 'Hủy',
      onCancel: () => {
        for (const id of readyIds) turboLaunchRef.current.delete(id);
      },
      onOk: () => {
        void (async () => {
        let sent = 0;
        const failed: string[] = [];
        let lastFailErr = '';
        try {
          for (const shot of queue) {
            try {
              const remakeTake =
                explicitOne ||
                Boolean(opts?.remake) ||
                Boolean(shotRunOf(stateRef.current, shot).motionNeedsRemake);
              if (
                sameFailedInput(
                  shotRunOf(stateRef.current, shot),
                  dataUriHash(shotRunOf(stateRef.current, shot).keyframeDataUrl),
                  shotI2vPromptHash(stateRef.current, shot, shotRunOf(stateRef.current, shot)),
                ) &&
                !shouldResumeTurboPoll(shotRunOf(stateRef.current, shot)) &&
                !remakeTake
              ) {
                message.warning(`${shCode(stateRef.current, shot)} circuit — bỏ qua.`);
                continue;
              }
              if (resolveTakeUrl(shotRunOf(stateRef.current, shot)) && !remakeTake) continue;
              setActiveId(shot.id);
              const after = stateRef.current;
              const run = shotRunOf(after, shot);
              if (run.keyframeDataUrl && run.status === 'story_locked') {
                patchRun(shot.id, { status: 'keyframe_ready' });
              }
              const loc = lockFromGraph(stateRef.current, shot);
              const lock = { ...loc, locked: Boolean(loc.locked) || Boolean(loc.environment) || Boolean(explicitOne) };
              const pre = studioI2vPrecheck({
                lock,
                action: i2vActionOf(after, shot, run),
                keyframeDataUrl: run.keyframeDataUrl,
                status: run.status === 'story_locked' ? 'keyframe_ready' : run.status,
                unlocked: true,
                sceneLocked: after.sceneLocked,
                scriptLocked: Boolean(after.scriptLocked) || (prodMode === 'director' && explicitOne),
                shortsReady: explicitOne || canWorkV2Scene(after),
                engine,
                hasEngineKey: engine === 'wan' ? keys.fal : engine === 'veo' ? keys.veo : keys.runway,
                state: after,
                shot,
                videoContext,
                run,
                ignoreCircuit: remakeTake,
              });
              if (!pre.ok) {
                failed.push(shCode(after, shot));
                message.error(
                  `Bỏ ${shCode(after, shot)} (0 cr): ${pre.items
                    .filter((i) => !i.ok)
                    .map((i) => i.label)
                    .join(' · ')}`,
                );
                continue;
              }
              const resumeShot = shouldResumeTurboPoll(run);
              const spokenEnd = linesForShot(stateRef.current, shot).reduce((n, line, idx, all) => {
                const dur = Number(stateRef.current.voiceAssets?.[line.id]?.duration || 0);
                return n + (dur || 0) + (idx + 1 < all.length ? 0.1 : 0);
              }, 0.2);
              const askedSec = spokenEnd > 0.3 ? providerDurationOf(after, shot) : shot.seconds;
              const ok = await sendTurbo({
                clipId: shot.id,
                prompt: pre.prompt,
                seconds:
                  engine === 'veo' ? veoMappedDurationSec(askedSec, dialogueFloorOf(after, shot)) : askedSec,
                ratio: outputAspectOf(stateRef.current),
                engine,
                silent: true,
                resume: remakeTake ? false : resumeShot,
                forceNew: remakeTake,
                cameraShift: remakeTake ? Math.max(opts?.cameraShift ?? 0, 0) : opts?.cameraShift,
              });
              if (!ok) {
                failed.push(shCode(stateRef.current, shot));
                const err = shotRunOf(stateRef.current, shot).turboError || '';
                lastFailErr = err;
                message.error(
                  isTurboDailyQuota(err)
                    ? 'Hết hạn mức ngày Runway — BATCH PAUSED. Hỏi lại · 0 cr.'
                    : isKitPrecheckError(err)
                      ? `BATCH PAUSED tại ${shCode(stateRef.current, shot)} — KIT PRECHECK · 0 cr. Không phải 429. TEST INPUT rồi Confirm 1 shot.`
                      : err
                        ? `BATCH PAUSED tại ${shCode(stateRef.current, shot)}: ${err.slice(0, 140)}`
                        : `BATCH PAUSED tại ${shCode(stateRef.current, shot)} — không gửi được. Xem lỗi trong drawer.`,
                );
                break;
              }
              sent += 1;
              await new Promise((r) => window.setTimeout(r, 8000));
            } finally {
              turboLaunchRef.current.delete(shot.id);
            }
          }
          if (sent && failed.length) {
            message.warning(
              `Được ${sent} take. Còn ${failed.join(', ')} — Hỏi lại · 0 cr từng hàng. Đừng gửi job mới khi còn 429.`,
            );
          } else if (sent) {
            message.success(`Đã gửi ${sent} take. QC từng shot — không tự Final.`);
          } else if (failed.length) {
            message.warning(
              isKitPrecheckError(lastFailErr)
                ? `Chưa gửi (${failed.join(', ')}). KIT PRECHECK · 0 cr — không phải 429. F5 rồi TEST INPUT.`
                : isTurboRateLimit(lastFailErr) || isTurboDailyQuota(lastFailErr)
                  ? `Chưa lấy được take (${failed.join(', ')}). Đang 429 / hạn mức — đợi rồi Hỏi lại · 0 cr từng hàng. Không CONFIRM job mới.`
                  : `Chưa lấy được take (${failed.join(', ')}). Xem lỗi từng hàng. Không CONFIRM cùng input.`,
            );
          } else {
            message.warning('Không gửi clip nào (0 cr). Sửa prompt/KF rồi CONFIRM lại.');
          }
        } finally {
          for (const id of readyIds) turboLaunchRef.current.delete(id);
        }
        })();
      },
    });
  };

  const startShotMotion = (_shotId: string, _liveStill?: string) => {
    /* LEGACY. CREATE_VIDEO CTA never calls this. No production requests. No turbo send. */
    return;
  };

  const putVisual = (shotId: string, next: VisualDeskView & { url?: string }) => {
    setVisualByShot((current) => {
      const prior = current[shotId];
      const url = next.keepPicture ? prior?.url : next.url;
      if (prior?.url && prior.url !== url) URL.revokeObjectURL(prior.url);
      return {
        ...current,
        [shotId]: {
          phase: next.phase,
          detail: next.detail,
          assetId: next.keepPicture ? prior?.assetId : next.assetId,
          approved: next.approved,
          undrawable: next.undrawable,
          decision: next.decision ?? prior?.decision,
          searchDecision: next.searchDecision ?? prior?.searchDecision,
          variantUrl: next.variantUrl ?? prior?.variantUrl,
          url,
        },
      };
    });
  };

  const showVisualAsset = async (assetId: string) => {
    const blob = await fetchVisualAssetImage(assetId);
    const typed = blob.type.startsWith('image/') ? blob : new Blob([await blob.arrayBuffer()], { type: 'image/png' });
    return URL.createObjectURL(typed);
  };

  const applyVisualCreate = async (
    buildId: string,
    shot: FamixaSeriesShot,
    opts: { regenerate: boolean; medium?: 'auto' | 'photo' },
  ) => {
    const brand = normalizeFilmBrand(filmBrandRef.current)
      || normalizeFilmBrand(visualBrandChoiceRef.current)
      || normalizeFilmBrand(visualBoundBrandRef.current[shot.id]);
    if (!brand) {
      putVisual(shot.id, {
        phase: 'missing',
        detail: 'Phim chưa có brand. Không dùng tạm hình của brand khác.',
        keepPicture: true,
      });
      return false;
    }
    visualBoundBrandRef.current[shot.id] = brand;
    const demand = buildShotVisualDemand({ filmId: buildId, brandCode: brand, shot, medium: opts.medium });
    if (!opts.regenerate) {
      putVisual(shot.id, {
        phase: 'searching',
        detail: `Đang tìm tài nguyên cho brand ${brand}.`,
        keepPicture: true,
        decision: '',
        searchDecision: '',
      });
      const found = await resolveVisualShot({ demand, searchOnly: true });
      const step = afterVisualSearch(found);
      if (step === 'bind' && found.assetId) {
        const view = interpretVisualResolve(found, 'search');
        const url = await showVisualAsset(found.assetId);
        putVisual(shot.id, { ...view, detail: `${view.detail} Brand ${brand}.`, url });
        return false;
      }
      if (step !== 'create') {
        putVisual(shot.id, { ...interpretVisualResolve(found, 'search'), keepPicture: true, searchDecision: found.decision || '' });
        return false;
      }
      putVisual(shot.id, {
        phase: 'missing',
        detail: found.reason || 'Chưa có tài nguyên đúng cảnh. Không lấy ảnh khác chủ đề.',
        keepPicture: true,
        decision: found.decision || 'NO_FIT',
        searchDecision: found.decision || 'NO_FIT',
      });
    }
    putVisual(shot.id, {
      phase: 'generating',
      detail: opts.regenerate
        ? 'Đang lưu biến thể mới. Ảnh đã duyệt không bị ghi đè.'
        : `Đang tạo hình cục bộ cho brand ${brand}.`,
      keepPicture: true,
    });
    const made = await resolveVisualShot({ demand, regenerate: opts.regenerate });
    const view = interpretVisualResolve(made, opts.regenerate ? 'variant' : 'create');
    const url = !view.keepPicture && view.assetId && made.hasImage ? await showVisualAsset(view.assetId) : undefined;
    const priorGap = !opts.regenerate && view.decision !== 'REUSE' && view.decision !== 'BOUND'
      ? 'Không có ảnh đúng cảnh (NO_FIT). '
      : '';
    const paidNote = view.decision === 'GENERATION_REQUIRED' ? ` Provider calls ${made.paidProviderCalls ?? 0}.` : '';
    const variantUrl = view.decision === 'VARIANT_STORED' && made.variantAssetId && made.hasImage
      ? await showVisualAsset(made.variantAssetId)
      : undefined;
    putVisual(shot.id, {
      ...view,
      detail: `${priorGap}${view.undrawable ? `${view.detail} Chưa vẽ được chữ có dấu: ${view.undrawable}.` : view.detail} Brand ${brand}.${paidNote}`,
      variantUrl,
      searchDecision: opts.regenerate ? undefined : 'NO_FIT',
      url,
    });
    return view.offerPaid === true;
  };

  const hydrateVisualShot = async (
    buildId: string,
    shotId: string,
    isCancelled: () => boolean,
    replacePicture: boolean,
  ) => {
    try {
      const row = await readVisualShotBinding(buildId, shotId);
      if (isCancelled()) return;
      if (!row.found || !row.assetId) return;
      if (row.brandCode) visualBoundBrandRef.current[shotId] = row.brandCode;
      let url: string | undefined;
      if (row.hasImage) {
        url = await showVisualAsset(row.assetId);
        if (isCancelled()) {
          URL.revokeObjectURL(url);
          return;
        }
      }
      putVisual(shotId, {
        phase: row.approved ? 'approved' : 'ready',
        detail: row.approved
          ? `Giữ ảnh đã duyệt. Brand ${row.brandCode || ''}. Mở lại phim không tạo ảnh mới.${replacePicture ? '' : ' Ảnh production của shot không bị thay.'}`
          : `Ảnh đã lưu cho shot. Brand ${row.brandCode || ''}. Mở lại phim không tạo ảnh mới.${replacePicture ? '' : ' Ảnh production của shot không bị thay.'}`,
        assetId: row.assetId,
        approved: row.approved,
        decision: row.decision || (row.approved ? 'BOUND' : 'BOUND'),
        keepPicture: false,
        url,
      });
    } catch {
      /* A failed read must not resolve or generate. */
    }
  };

  const approveVisualStill = (shotId: string) => {
    const filmBuildId = stateRef.current.buildId;
    const identity = resolveShotWorkspaceIdentity({ requestedBuildId: filmBuildId, requestedShotId: shotId });
    if (!identity.ok) return;
    void (async () => {
      try {
        const row = await approveVisualShotBinding(identity.requestedBuildId, shotId);
        putVisual(shotId, {
          phase: 'approved',
          detail: 'Đã duyệt ảnh này. Tạo biến thể sẽ lưu file mới và giữ nguyên ảnh này.',
          assetId: row.assetId || undefined,
          approved: true,
          keepPicture: true,
        });
      } catch (e) {
        message.error(apiErrorMessage(e, 'Chưa duyệt được ảnh.'));
      }
    })();
  };

  const requestVisualLibrary = (shotId: string, medium?: 'auto' | 'photo') => {
    if (osInflightRef.current[shotId]) return;
    const filmBuildId = stateRef.current.buildId;
    const identity = resolveShotWorkspaceIdentity({ requestedBuildId: filmBuildId, requestedShotId: shotId });
    if (!identity.ok) return;
    const shot = productionOsDeskShots(identity.requestedBuildId, [
      ...productionShorts(stateRef.current),
      ...(stateRef.current.episode?.shots ?? []),
    ]).find((s) => s.id === shotId);
    if (!shot) return;
    const mark = medium === 'photo' ? 'VISUAL_PHOTO' : 'VISUAL_LIBRARY';
    osInflightRef.current[shotId] = mark;
    setStillBusy(shotId);
    void (async () => {
      try {
        await applyVisualCreate(identity.requestedBuildId, shot, { regenerate: false, medium });
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tìm được hình dùng chung.'));
      } finally {
        if (osInflightRef.current[shotId] === mark) delete osInflightRef.current[shotId];
        setStillBusy((cur) => (cur === shotId ? undefined : cur));
      }
    })();
  };

  const requestVisualVariant = (shotId: string) => {
    if (osInflightRef.current[shotId]) return;
    const filmBuildId = stateRef.current.buildId;
    const identity = resolveShotWorkspaceIdentity({ requestedBuildId: filmBuildId, requestedShotId: shotId });
    if (!identity.ok) return;
    const shot = productionOsDeskShots(identity.requestedBuildId, [
      ...productionShorts(stateRef.current),
      ...(stateRef.current.episode?.shots ?? []),
    ]).find((s) => s.id === shotId);
    if (!shot) return;
    osInflightRef.current[shotId] = 'VISUAL_VARIANT';
    setStillBusy(shotId);
    void (async () => {
      try {
        await applyVisualCreate(identity.requestedBuildId, shot, { regenerate: true });
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không lưu được biến thể.'));
      } finally {
        if (osInflightRef.current[shotId] === 'VISUAL_VARIANT') delete osInflightRef.current[shotId];
        setStillBusy((cur) => (cur === shotId ? undefined : cur));
      }
    })();
  };

  const executeProductionOsAction = (shotId: string, action: string) => {
    if (action === 'CREATE_LIPSYNC' && shotId === OFFICIAL_SH02_SHOT_CODE) {
      void prepareOfficialLipsync(shotId);
      return;
    }
    void (async () => {
      try {
        const catalog = osActionsByShotRef.current[shotId];
        const blocked = formatProductionActionReason(catalog, action);
        if (!mayExecuteProductionAction(catalog, action)) {
          message.warning(blocked || `${action} · UNAVAILABLE`);
          return;
        }
        const filmBuildId = stateRef.current.buildId;
        const identity = resolveShotWorkspaceIdentity({ requestedBuildId: filmBuildId, requestedShotId: shotId });
        const buildId = identity.ok ? identity.requestedBuildId : undefined;
        if (!identity.ok || !buildId) {
          message.error(formatShotIdentityError(identity));
          return;
        }
        const shot = productionOsDeskShots(buildId, [
          ...productionShorts(stateRef.current),
          ...(stateRef.current.episode?.shots ?? []),
        ]).find((s) => s.id === shotId);
        if (!shot) {
          message.error(formatShotIdentityError({
            reason: 'SHOT_NOT_FOUND',
            requestedBuildId: filmBuildId,
            requestedShotId: shotId,
          }));
          return;
        }
        const episodePost = action === 'ASSEMBLE_EPISODE';
        const tenantImage = action === 'CREATE_IMAGE' && mayAuthorizeTenantEp01Image(buildId, shotId);
        if (action === 'CREATE_IMAGE' && shotId.toUpperCase() === 'EP01-SC01-SH03') {
          message.warning('EP01-SC01-SH03 · SPOKEN_LINE_INCOMPLETE');
          return;
        }
        if (action === 'CREATE_IMAGE') {
          if (osInflightRef.current[shotId] === 'CREATE_IMAGE') return;
          osInflightRef.current[shotId] = 'CREATE_IMAGE';
          setStillBusy(shotId);
          let offerPaid = false;
          try {
            offerPaid = await applyVisualCreate(buildId, shot, { regenerate: false });
          } catch (e) {
            message.error(apiErrorMessage(e, 'Không chọn được hình.'));
            return;
          } finally {
            if (osInflightRef.current[shotId] === 'CREATE_IMAGE') delete osInflightRef.current[shotId];
            setStillBusy((cur) => (cur === shotId ? undefined : cur));
          }
          if (!offerPaid || !tenantImage) return;
        }
        if (episodePost) {
          if (!mayPostProductionEpisode(buildId)) {
            message.warning(`${action} · PROVIDER_UNAVAILABLE · FILM_PROTECTED`);
            return;
          }
        } else if (!mayMutateProductionOs(buildId, shotId) && !tenantImage) {
          message.warning(`${action} · PROVIDER_UNAVAILABLE · FILM_PROTECTED`);
          return;
        }
        const story = futureDeskStory(shot.id, (shot.story || '').trim() || 'TEST fixture story');
        const voiceDraft = action === 'CREATE_VOICE' ? voiceDraftForRequest(shot.id, '') : null;
        if (voiceDraft && !voiceDraft.permitted) {
          message.warning(directorBlockerLines([voiceDraft.blocker]).join(' ') || voiceDraft.blocker);
          return;
        }
        if (action === 'CREATE_LIPSYNC' || action === 'MIX_VIDEO') {
          const run = shotRunOf(stateRef.current, shot);
          const director = osDirectorTimingByShot[shotId];
          const speech = run.lipsyncFrozenInput?.voiceDurationSec
            || osVoiceByShot[shotId]?.durationSec
            || 0;
          const picture = run.lipsyncFrozenInput?.motionDurationSec ?? 0;
          const os = osStateByShotRef.current[shotId];
          const preview = osPreviewIdentityByShotRef.current[shotId];
          const measured = currentLipsyncMeasurement({
            lipsyncArtifactId: os?.lipsyncArtifactId || productionNodeArtifactId(os, 'LIPSYNC'),
            previewArtifactId: preview?.artifactId,
            previewRole: preview?.role,
            previewDurationSec: preview?.durationSec,
          });
          const clocks = clickTimeLipsyncClock({
            action,
            hasCurrentLipsyncArtifact: measured.hasArtifact,
            currentLipsyncDurationSec: measured.durationSec,
            directDialogue: (shot.dialogueSegmentIds?.length ?? 0) > 0,
            audioSec: speech,
            pictureSec: picture,
            dialogueIntervals: approvedDialogueIntervals({
              source: director?.timingSource ?? undefined,
              speechStartSec: director?.speechStartSec ?? undefined,
              speechEndSec: director?.speechEndSec ?? undefined,
            }),
          });
          if (clocks.length) {
            message.warning(directorBlockerLines(clocks).join(' ') || clocks[0]);
            return;
          }
        }
        const acting = futureDeskActing(
          shot.id,
          [shot.actingBeat?.before?.action, shot.actingBeat?.before?.body]
            .map((x) => (x || '').trim())
            .filter(Boolean)
            .join(' ') || 'TEST fixture acting',
        );
        const label = PRODUCTION_OS_CTA[action] || action;
        const imageTier = tenantImage ? qualityTierOf('IMAGE', readQualityTier(shot.id, 'IMAGE')) : null;
        const tenantVideo = action === 'CREATE_VIDEO' && shot.id.startsWith('EP01-') && !isProductionOsTestShot(shot.id);
        const videoTier = tenantVideo ? qualityTierOf('VIDEO', readQualityTier(shot.id, 'VIDEO')) : null;
        modal.confirm({
          title: label,
          content: episodePost
            ? `ASSEMBLE_EPISODE trên Production OS. Server ffmpeg ghép CURRENT:ASSEMBLY đã có. Không gọi Gemini / Veo / Runway / Wan / Fal / ElevenLabs. ${episodeAssemblyNotice(EPISODE_ASSEMBLY_ORDER)}`
            : tenantImage
              ? `Tạo ảnh Gemini 3.1 Flash Image, ${imageTier?.label}. ${imageTier?.detail} theo bảng giá đã công bố, chưa phải hóa đơn. Công đoạn này tối đa 3 lượt có phí, tính cả lượt này.`
            : videoTier
              ? `Tạo video Runway Gen-4 Turbo, ${videoTier.label}. ${videoTier.detail}.`
            : `${action} trên Production OS. Fixture — không gửi Gemini / Veo / Runway / Wan / Fal / ElevenLabs.`,
          okText: label,
          cancelText: 'Hủy',
          zIndex: 3100,
          getContainer: () => document.body,
          onOk: async () => {
            if (!mayExecuteProductionAction(osActionsByShotRef.current[shotId], action)) return;
            if (osInflightRef.current[shotId] === action) return;
            osInflightRef.current[shotId] = action;
            if (action === 'CREATE_VIDEO' || action === 'REGENERATE') setTurboBusy(shotId);
            else if (action === 'CREATE_IMAGE') setStillBusy(shotId);
            else if (action === 'CREATE_LIPSYNC' || action === 'CREATE_VOICE') setLipsyncBusy(shotId);
            else setOrchMixId(shotId);
            try {
              const osState = osStateByShotRef.current[shotId]
                || await getProductionShotState(buildId, shotId, {
                  episodeCode: 'TEST',
                  sceneCode: 'TEST-SC01',
                  story,
                  actingBeat: acting,
                  visualContract: shot.visual || 'TEST fixture visual',
                  timing: '5',
                  ...strategyClaim(shot),
                });
              const motionId = productionNodeArtifactId(osState, 'MOTION');
              let voiceArtId = resolveCurrentVoiceArtifactId(osState);
              if (action === 'CREATE_LIPSYNC') {
                try {
                  const resolved = await getProductionCurrentVoice(buildId, shot.id, `LINE-${shot.id}`);
                  if (resolved.artifactId) voiceArtId = resolved.artifactId;
                } catch {
                  /* GET /voice 404/mismatch: keep OS CURRENT:VOICE node. Never kit-famixa-tts. */
                }
              }
              const lipsyncId = (osState.lipsyncArtifactId || '').trim() || productionNodeArtifactId(osState, 'LIPSYNC');
              const mixId = productionNodeArtifactId(osState, 'MIX');
              const acceptId = mixId || lipsyncId || osState.visibleArtifactId || motionId || osState.currentArtifactId;
              if (action === 'CREATE_LIPSYNC' && (!motionId || !voiceArtId)) {
                throw new Error(formatProductionActionReason(catalog, action) || 'MISSING_DEPENDENCY · VIDEO_TAKE+VOICE');
              }
              if (action === 'REGENERATE' && !motionId) {
                throw new Error('MISSING_DEPENDENCY');
              }
              if (action === 'MIX_VIDEO' && !lipsyncId && !isConfirmedStaticVoiceOver(shot)) {
                throw new Error('MISSING_LIPSYNC');
              }
              if (action === 'ACCEPT' && !acceptId) {
                throw new Error('MISSING_DEPENDENCY');
              }
              let assemblyArtifactIds: string[] | undefined;
              let assemblyShotCodes: string[] | undefined;
              if (action === 'ASSEMBLE') {
                const accepted: { shotCode: string; artifactId: string }[] = [];
                for (const code of ['TEST-SC01-SH01', 'TEST-SC01-SH02']) {
                  const row = code === shotId
                    ? osState
                    : osStateByShotRef.current[code] || await getProductionShotState(PRODUCTION_OS_TEST_SCOPE_ID, code);
                  const id = (row.acceptedArtifactId || '').trim();
                  if (id) accepted.push({ shotCode: code, artifactId: id });
                }
                if (!accepted.length) throw new Error('MISSING_DEPENDENCY');
                assemblyArtifactIds = accepted.map((row) => row.artifactId);
                assemblyShotCodes = accepted.map((row) => row.shotCode);
              }
              if (action === 'ASSEMBLE_EPISODE') {
                const ordered: { shotCode: string; artifactId: string }[] = [];
                for (const code of EPISODE_ASSEMBLY_ORDER) {
                  const row = code === shotId
                    ? osState
                    : osStateByShotRef.current[code] || await getProductionShotState(buildId, code, {
                      episodeCode: 'EP01',
                      sceneCode: 'SC01',
                    });
                  const id = productionNodeArtifactId(row, 'ASSEMBLY');
                  if (!id) throw new Error('MISSING_DEPENDENCY');
                  ordered.push({ shotCode: code, artifactId: id });
                }
                assemblyArtifactIds = ordered.map((row) => row.artifactId);
                assemblyShotCodes = ordered.map((row) => row.shotCode);
              }
              const result = await createProductionRequest({
                buildId,
                episodeCode: action === 'ASSEMBLE_EPISODE'
                  ? 'EP01'
                  : isProductionOsTestShot(shotId)
                  ? 'TEST'
                  : episodeCodeOf(stateRef.current.episode?.episode || stateRef.current.episode?.title) || 'EP01',
                sceneCode: action === 'ASSEMBLE_EPISODE'
                  ? 'EP01'
                  : isProductionOsTestShot(shotId) ? 'TEST-SC01' : sceneIdOfShot(shot) || shot.scene || 'SC01',
                shotCode: action === 'ASSEMBLE_EPISODE' ? EPISODE_ASSEMBLY_SHOT_CODE : shot.id,
                story: voiceDraft?.text || story,
                actingBeat: acting,
                visualContract: shot.visual || '',
                timing: String(dialogueFloorOf(stateRef.current, shot) || shot.seconds || 5),
                ...strategyClaim(shot),
                aspectRatio: outputAspectOf(stateRef.current),
                durationSec: action === 'CREATE_VIDEO'
                  ? Number(readQualityTier(shot.id, 'VIDEO')) || 5
                  : Number(shot.seconds) || 5,
                action,
                imageSize: action === 'CREATE_IMAGE' ? readQualityTier(shot.id, 'IMAGE') : undefined,
                idempotencyKey: productionOsIdempotencyKey(
                  action,
                  buildId,
                  shot.id,
                  action === 'CREATE_IMAGE'
                    ? `${readQualityTier(shot.id, 'IMAGE')}:${Date.now()}`
                    : action === 'REGENERATE' ? motionId : undefined,
                ),
                lineId: action === 'CREATE_VOICE' || action === 'CREATE_LIPSYNC' ? `LINE-${shot.id}` : undefined,
                speakerCharacterId: action === 'CREATE_VOICE' ? 'CHAR-TEST' : undefined,
                voiceId: action === 'CREATE_VOICE' ? 'VOICE-TEST' : undefined,
                motionArtifactId: action === 'CREATE_LIPSYNC' || action === 'REGENERATE' ? motionId : undefined,
                voiceArtifactId: action === 'CREATE_LIPSYNC' ? voiceArtId : undefined,
                lipsyncArtifactId: action === 'MIX_VIDEO' ? lipsyncId : undefined,
                acceptArtifactId: action === 'ACCEPT' ? acceptId || undefined : undefined,
                assemblyArtifactIds,
                assemblyShotCodes,
                authorizePaidNetwork: tenantImage,
              });
              if (result.action !== action) throw new Error('Request bị đổi action.');
              const nextState = await getProductionShotState(buildId, shot.id, strategyClaim(shot));
              setOsStateByShot((m) => ({ ...m, [shot.id]: nextState }));
              const actions = await getProductionShotActions(buildId, shot.id, strategyClaim(shot));
              setOsActionsByShot((m) => ({ ...m, [shot.id]: actions }));
              noteStrategyCatalog(shot.id, actions);
              try {
                const meta = await getProductionPreview(buildId, shot.id);
                if (meta.empty || !meta.artifactId || !meta.byteSha256) throw new Error('preview identity missing');
                const blob = await fetchProductionPreviewMedia(buildId, shot.id);
                if (!(await previewBytesMatch(blob, meta.byteSha256))) throw new Error('preview hash mismatch');
                const url = URL.createObjectURL(asPlayableVideoBlob(blob));
                setOsPreviewIdentityByShot((m) => ({
                  ...m,
                  [shot.id]: osPreviewIdentityFromMeta(meta),
                }));
                setOsPreviewByShot((m) => ({ ...m, [shot.id]: url }));
              } catch {
                /* VISIBLE absent or hash mismatch is empty — not a URL fallback */
              }
              if (action === 'CREATE_IMAGE') void loadOsKeyframe(buildId, shot.id);
              message.success(`${result.action} · ${result.artifactId.slice(0, 8)}`);
            } catch (e) {
              message.error(apiErrorMessage(e, 'Tạo không xong.'));
            } finally {
              if (osInflightRef.current[shotId] === action) delete osInflightRef.current[shotId];
              setTurboBusy((cur) => (cur === shotId ? undefined : cur));
              setStillBusy((cur) => (cur === shotId ? undefined : cur));
              setLipsyncBusy((cur) => (cur === shotId ? undefined : cur));
              setOrchMixId((cur) => (cur === shotId ? undefined : cur));
            }
          },
        });
      } catch (e) {
        message.error({ content: apiErrorMessage(e, formatProductionActionReason(osActionsByShotRef.current[shotId], action) || 'Không mở được Production OS.'), key: 'famixa-desk-err' });
      }
    })();
  };

  /** Accept Existing click. Non-authority. Does not POST ACCEPT and does not stamp the accepted record. */
  const acceptExistingOfficial = (_shotId: string) => {
    const effect = acceptExistingAuthorityEffect();
    if (effect.createsProductionArtifact || effect.createsProductionBinding || effect.refreezesInput) return;
    message.warning(ACCEPT_EXISTING_NON_AUTHORITY_NOTE);
  };

  const rememberLineAudio = (lineId: string, blob: Blob) => {
    ttsBlobs.current.set(lineId, blob);
    let url = ttsUrls.current.get(lineId);
    if (!url) {
      url = URL.createObjectURL(blob);
      ttsUrls.current.set(lineId, url);
    }
    if (!ttsFilesRef.current[lineId]?.url) {
      ttsFilesRef.current = { ...ttsFilesRef.current, [lineId]: { url, fileName: `${lineId}.mp3` } };
    }
    setTtsFiles((m) => (m[lineId] ? m : { ...m, [lineId]: { url, fileName: `${lineId}.mp3` } }));
    return url;
  };

  /** LEGACY / CACHE / RECOVERY. Not Production OS CURRENT:VOICE authority. OS execution uses GET /voice. */
  const resolveLineAudio = async (line: { id: string; text: string; voiceId?: string; characterId?: string }) => {
    const hit = ttsBlobs.current.get(line.id);
    if (hit) {
      rememberLineAudio(line.id, hit);
      return hit;
    }
    const same = (a: string, b: string) => a.replace(/\s+/g, ' ').trim() === b.replace(/\s+/g, ' ').trim();
    const voices = voiceIdsForLine(line);
    const alts = ttsHydrateKeys(line, voices);
    for (const g of stateRef.current.voicePreview?.generated ?? []) {
      if (g.id !== line.id && same(g.text, line.text)) {
        alts.push(...ttsHydrateKeys({ id: g.id, text: g.text, voiceId: line.voiceId }, voices));
      }
    }
    const blob = await loadTtsBlobAny(alts);
    if (!blob) return undefined;
    rememberLineAudio(line.id, blob);
    rememberLineAudio(ttsTextKey(line.text, line.voiceId), blob);
    return blob;
  };

  const cutPlan = mapPreviewCut(state, cutRange, {
    hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id)),
  });

  const spokenLinesOf = (shot: FamixaSeriesShot) => {
    const item = cutPlan?.items.find((i) => i.shotId === shot.id);
    if (item?.silent) return [];
    return linesForShot(stateRef.current, shot);
  };

  const proveLocalFalFile = async (taskId?: string) => {
    const id = (taskId || '').trim();
    if (!isFalLipsyncTaskId(id)) return false;
    try {
      const blob = await fetchContentSeriesFalVideo(id);
      return blob.size >= 80;
    } catch {
      return false;
    }
  };

  const startLipsync = async (
    onlyIds?: string[],
    opts?: { confirmed?: boolean; remake?: boolean; forceNew?: boolean; resumeOnly?: boolean },
  ) => {
    message.warning(productionOsLegacyRefuseNote('CREATE_LIPSYNC', 'POST /series/lipsync'));
    if (productionOsLegacyPathClosed()) return;
    const pack = productionShorts(stateRef.current);
    const ids = onlyIds ?? [];
    const range = ids.length ? pack.filter((s) => ids.includes(s.id)) : cutRange;
    const falModel = normalizeLipsyncModel(stateRef.current.lipsyncModel);
    const falSync = normalizeLipsyncSyncMode(stateRef.current.lipsyncSyncMode);
    const keepLipsyncError = (ids: string[], text: string) => {
      const why = text.trim();
      if (!why) return;
      for (const id of ids) patchRun(id, { lipsyncError: why, lipsyncStatus: 'FAILED' });
      message.error(why);
    };
    const pollExistingLipsync = async (shot: FamixaSeriesShot, taskId: string) => {
      const t0 = Date.now();
      while (Date.now() - t0 < 90_000) {
        const task = await getContentSeriesTurbo(taskId);
        const st = (task.status || '').toUpperCase();
        const missingFile = /chưa thấy URL|chưa lấy file|không có file|keys:\s*status/i.test(task.error || '');
        const noOutput = lipsyncFalHasNoOutput(task.error);
        const waiting = !task.videoUrl && !missingFile && !noOutput && (st === 'PENDING' || st === 'RUNNING' || st === 'PROCESSING' || st === 'IN_PROGRESS' || st === 'RETRY');
        patchRun(shot.id, {
          lipsyncStatus: task.status,
          lipsyncTaskId: task.taskId || taskId,
          lipsyncError: waiting
            ? task.error || `Đang hỏi lại Fal (${st || 'PENDING'}). Task ${taskId}. 0$ — đợi file.`
            : task.error ?? undefined,
        });
        if ((missingFile || noOutput) && !task.videoUrl) {
          const why = noOutput
            ? task.error || `Fal không có mp4 cho job này. Task ${taskId}. Gắn Fal · 0$ nếu fal.ai có link. Hỏi lại sẽ không ra file.`
            : task.error || `Fal xong — KIT chưa lấy file. Task ${taskId}. Gắn Fal · 0$ từ Usage.`;
          patchRun(shot.id, {
            lipsyncStatus: noOutput ? 'FAILED' : 'RETRY',
            lipsyncTaskId: task.taskId || taskId,
            lipsyncError: why,
          });
          message.warning(
            noOutput
              ? `${studioShotCode(shot, pack)}: job Fal này không có file môi. Hỏi lại không ra. Take Veo + thoại vẫn xem được.`
              : `${studioShotCode(shot, pack)}: Fal xong, chưa lấy được file. Take Veo + thoại vẫn xem được.`,
          );
          return false;
        }
        if (task.status === 'SUCCEEDED' && task.videoUrl) {
          const live = shotRunOf(stateRef.current, shot);
          const localReady =
            isFalPlaybackSrc(task.videoUrl) &&
            (await proveLocalFalFile(task.taskId || taskId));
          if (!canStampFalLipsyncFinal(task.videoUrl, localReady)) {
            patchRun(shot.id, {
              lipsyncStatus: 'SUCCEEDED',
              lipsynced: false,
              lipsyncTaskId: task.taskId || taskId,
              lipsyncError:
                'Fal xong — KIT chưa có file playable trên máy API. Gắn Fal · 0$ (dán video.url). Không Confirm job mới.',
            });
            return false;
          }
          const frozen = live.lipsyncFrozenInput;
          const now = freezeLipSyncInput(stateRef.current, shot, {
            motionArtifactId: frozen?.motionArtifactId,
            usedValidity: frozen?.usedValidity as import('./ContentFamixaShotProduction/ShotProductionExecution').ArtifactValidity,
          });
          const late = Boolean(frozen?.fingerprint && frozen.fingerprint !== now.fingerprint);
          if (late) {
            patchRun(shot.id, {
              lipsyncStatus: 'SUCCEEDED',
              lipsyncError: undefined,
            });
            return true;
          }
          patchRun(shot.id, {
            ...stampFalFinal(live, task.videoUrl, localReady),
            lipsyncStatus: 'SUCCEEDED',
            lipsyncError: undefined,
            model: task.model || 'sync-lipsync-1.9',
            shotQa: { ...live.shotQa, lipsync: true },
          });
          return true;
        }
        if (task.status === 'FAILED' || task.status === 'CANCELLED') {
          patchRun(shot.id, {
            lipsyncStatus: task.status,
            lipsyncError: task.error || 'Fal lipsync lỗi. Take cũ còn trên hàng.',
          });
          message.error(`${studioShotCode(shot, pack)}: ${task.error || 'Fal khớp môi lỗi.'}`);
          return false;
        }
        await new Promise((r) => setTimeout(r, 2000));
      }
      patchRun(shot.id, {
        lipsyncStatus: 'RETRY',
        lipsyncTaskId: taskId,
        lipsyncError: `Fal chưa trả take khớp môi. Task ${taskId}. Hỏi lại môi · 0$ — đừng Confirm job mới.`,
      });
      message.warning(`${studioShotCode(shot, pack)}: Fal chưa trả file. Hỏi lại môi · 0$ — đừng Confirm job mới.`);
      return false;
    };
    if (opts?.resumeOnly) {
      opts = { ...opts, confirmed: true, remake: false, forceNew: false };
    }
    const resumeShots = range.filter((s) => {
      if (opts?.forceNew) return false;
      const run = shotRunOf(stateRef.current, s);
      if (!lipsyncResumeTaskId(run, falModel)) return false;
      if (opts?.resumeOnly || shouldResumeLipsync(run)) return true;
      if (opts?.remake) return false;
      return true;
    });
    if (opts?.resumeOnly && !resumeShots.length) {
      keepLipsyncError(
        range.map((s) => s.id),
        'Hết task Fal để hỏi lại. Gắn Fal · 0$ từ Usage — đừng Confirm job mới.',
      );
      return;
    }
    if (resumeShots.length) {
      if (!opts?.confirmed) {
        modal.confirm({
          title: `Hỏi lại khớp môi ${resumeShots.length} Short · 0$ Fal`,
          content: `${resumeShots.map((s) => studioShotCode(s, pack)).join(', ')}. Task cũ còn trên Fal — KIT chỉ hỏi lại file. Không gửi job mới, không trừ thêm.`,
          okText: 'Hỏi lại · 0$',
          cancelText: 'Hủy',
          onOk: () => void startLipsync(resumeShots.map((s) => s.id), { confirmed: true }),
        });
        return;
      }
      let resumed = 0;
      for (let i = 0; i < resumeShots.length; i++) {
        const shot = resumeShots[i]!;
        const run = shotRunOf(stateRef.current, shot);
        const resumeId = lipsyncResumeTaskId(run, falModel);
        if (!resumeId) {
          keepLipsyncError([shot.id], `${studioShotCode(shot, pack)}: hết task id. Dán URL từ fal.ai → Usage (Gắn Fal · 0$).`);
          continue;
        }
        const prev = turboLockRef.current;
        let release = () => {};
        turboLockRef.current = new Promise<void>((resolve) => {
          release = resolve;
        });
        await prev;
        setLipsyncBusy(shot.id);
        patchRun(shot.id, {
          lipsyncTaskId: resumeId,
          lipsyncStatus: 'RETRY',
          lipsyncError: `Đang hỏi lại Fal. Task ${resumeId}. 0$ — đợi file.`,
        });
        try {
          message.loading({
            content: `Hỏi lại khớp môi ${studioShotCode(shot, pack)} (${i + 1}/${resumeShots.length}) · 0 Fal mới`,
            key: 'lipsync',
            duration: 0,
          });
          if (await pollExistingLipsync(shot, resumeId)) resumed += 1;
        } catch (e) {
          const why = apiErrorMessage(e, 'Không hỏi lại được Fal.');
          patchRun(shot.id, {
            lipsyncStatus: 'RETRY',
            lipsyncTaskId: resumeId,
            lipsyncError: `${why} Task ${resumeId}. Hỏi lại môi · 0$ — đừng Confirm job mới.`,
          });
          message.error(`${studioShotCode(shot, pack)}: ${why}`);
        } finally {
          setLipsyncBusy(undefined);
          release();
        }
      }
      if (resumed) {
        message.success({ content: `Đã khớp môi ${resumed}/${resumeShots.length} Short. Ghép sẽ giữ tiếng trong video.`, key: 'lipsync' });
      } else {
        message.destroy('lipsync');
      }
      return;
    }
    const need = range.filter((s) => {
      const run = shotRunOf(stateRef.current, s);
      const snap = snapshotOfShot(stateRef.current, s, ttsFilesRef.current, {
        hasVoiceFile: (id) => Boolean(ttsFilesRef.current[id]?.url || ttsBlobs.current.has(id)),
      });
      return lipsyncSendEligible({
        run,
        spoken: spokenLinesOf(s).length > 0,
        remake: opts?.remake || opts?.forceNew,
        motionStale: snap.motionStale,
        accepted: snap.motionAccepted,
      });
    });
    if (!need.length) {
      const why = 'Chưa có take + thoại cần khớp môi (hoặc đã khớp). Không gửi Fal.';
      keepLipsyncError(onlyIds && onlyIds.length ? onlyIds : [], why);
      if (!onlyIds?.length) message.warning(why);
      return;
    }
    const overflow = need
      .map((s) => {
        const why = veoSpeechOverflowReason({
          takeSec: performanceDurationOf(stateRef.current, s),
          dialogueEndSec: dialogueEndOf(stateRef.current, s),
          floorSec: dialogueFloorOf(stateRef.current, s),
        });
        return why && isVeoMotionTaskId(shotRunOf(stateRef.current, s).turboTaskId)
          ? `${studioShotCode(s, pack)}: ${why}`
          : '';
      })
      .filter(Boolean);
    if (overflow.length) {
      keepLipsyncError(need.map((s) => s.id), `${overflow.join(' ')} Không gửi Fal trên take ngắn.`);
      return;
    }
    const facePick = need.filter((s) =>
      lipsyncNeedsSpeakerPick(s, spokenLinesOf(s), stateRef.current.characters),
    );
    if (facePick.length > 0 && falModel !== 'v3') {
      keepLipsyncError(
        facePick.map((s) => s.id),
        `${facePick.map((s) => studioShotCode(s, pack)).join(', ')} có 2 mặt. Chọn Fal v3 (Chuẩn) — 1.9 kéo môi cả Minh lẫn Linh. Không gửi.`,
      );
      return;
    }
    if (facePick.some((s) => {
      const pt = shotRunOf(stateRef.current, s).lipsyncSpeaker;
      return !(pt && pt.x >= 0 && pt.x <= 1 && pt.y >= 0 && pt.y <= 1);
    })) {
      keepLipsyncError(
        facePick.map((s) => s.id),
        `${facePick.map((s) => studioShotCode(s, pack)).join(', ')}: chấm mặt người nói (Linh) trên video rồi mới khớp môi. Không gửi Fal.`,
      );
      return;
    }
    const qaBlock = need.filter((s) => !lipsyncQaReady(shotRunOf(stateRef.current, s)));
    if (qaBlock.length) {
      keepLipsyncError(
        qaBlock.map((s) => s.id),
        `${qaBlock.map((s) => studioShotCode(s, pack)).join(', ')} chưa tick ACTION + CONTINUITY + VOICE/FACE. Sai mặt/giọng thì sửa KF — không gửi Fal.`,
      );
      return;
    }
    const speakerBlock = need.filter((s) => multiSpeakerBlock(spokenLinesOf(s)));
    if (speakerBlock.length) {
      keepLipsyncError(
        speakerBlock.map((s) => s.id),
        speakerBlock.map((s) => `${studioShotCode(s, pack)}: ${multiSpeakerBlock(spokenLinesOf(s))}`).join(' '),
      );
      return;
    }
    const faceBlock = need
      .map((s) => {
        const line = spokenLinesOf(s)[0];
        if (!line) return '';
        const why = lipsyncSpeakerMismatch(s, line, shotRunOf(stateRef.current, s), stateRef.current.characters);
        return why ? `${studioShotCode(s, pack)}: ${why}` : '';
      })
      .filter(Boolean);
    if (faceBlock.length) {
      keepLipsyncError(need.map((s) => s.id), `${faceBlock.join(' · ')} — không gửi Fal. Sửa map thoại hoặc KF.`);
      return;
    }
    if (!keys.fal) {
      keepLipsyncError(need.map((s) => s.id), 'Chưa có Fal API key (Model AI → Video). Khớp môi không trừ Runway.');
      return;
    }
    await hydrateSessionTts(stateRef.current);
    const missingVoice: string[] = [];
    for (const s of need) {
      for (const line of spokenLinesOf(s)) {
        if (!(await resolveLineAudio(line))) {
          missingVoice.push(`${studioShotCode(s, pack)} «${(line.text || line.id).slice(0, 42)}»`);
        }
      }
    }
    if (missingVoice.length) {
      keepLipsyncError(
        need.map((s) => s.id),
        `Thiếu Voice master: ${missingVoice.join(' · ')}. Mở tab Voice → phát lại đúng câu (KIT nhớ file trên máy). Không tạo TTS lúc Fal.`,
      );
      return;
    }
    const resumeIds = new Set(
      need
        .filter((s) => !opts?.forceNew && shouldResumeLipsync(shotRunOf(stateRef.current, s)))
        .map((s) => s.id),
    );
    if (!opts?.confirmed) {
      const attachInstead = opts?.forceNew
        ? []
        : need.filter((s) => shouldResumeLipsync(shotRunOf(stateRef.current, s)));
      if (attachInstead.length) {
        keepLipsyncError(
          attachInstead.map((s) => s.id),
          'Job Fal cũ còn — Hỏi lại môi · 0$ hoặc Gắn Fal · 0$. Không mở Confirm trừ tiền.',
        );
        return;
      }
      const falUsd = estimateFalLipsyncUsdForShots(
        need.map((s) => ({
          seconds: performanceDurationOf(stateRef.current, s),
          performanceDurationSec: performanceDurationOf(stateRef.current, s),
        })),
        falModel,
      );
      const codes = need.map((s) => studioShotCode(s, pack)).join(', ');
      if (resumeIds.size) {
        modal.confirm({
          title: `Hỏi lại khớp môi ${resumeIds.size} Short · 0$ Fal`,
          content: `${codes}. Task cũ còn trên Fal — KIT chỉ hỏi lại file. Không gửi job mới, không trừ thêm.`,
          okText: 'Hỏi lại · 0$',
          cancelText: 'Hủy',
          onOk: () => void startLipsync(need.map((s) => s.id), { confirmed: true }),
        });
        return;
      }
      const alreadyTried = need.some((s) => Boolean(shotRunOf(stateRef.current, s).lipsyncError));
      const deadJob =
        Boolean(opts?.forceNew) ||
        need.some((s) => lipsyncFalHasNoOutput(shotRunOf(stateRef.current, s).lipsyncError));
      const tier = lipsyncTierOf(falModel);
      modal.confirm({
        title: deadJob
          ? `Khớp môi mới ${need.length} Short · job Fal mới · ~$${falUsd.toFixed(2)}`
          : `Khớp môi ${need.length} Short · ${tier.title} · ~$${falUsd.toFixed(2)} · 0 cr Runway`,
        content: deadJob
          ? `${codes}. Job mới. ${
              need.some((s) =>
                shouldOmitRejectedSpeakerPin({
                  remake: true,
                  error: shotRunOf(stateRef.current, s).lipsyncError,
                }),
              )
                ? 'Thoại Linh — Confirm gửi khung mặt Linh, đúng số khung file take. Không điểm, không auto, không để câm.'
                : 'Thoại Linh — Confirm gửi khung mặt Linh, đúng số khung file take. Không để câm.'
            } Confirm = trừ ~$${falUsd.toFixed(2)}. Hủy = dừng, take Veo vẫn còn.`
          : alreadyTried
            ? `${codes}. Lần trước Fal có thể đã trừ. Confirm = job MỚI, trừ thêm ~$${falUsd.toFixed(2)}. Xem fal.ai → Usage trước — file có thể đã xong.`
            : `${codes}. ${tier.title} — ${tier.hint} · ${tier.rate}. Sync ${falSync}. Nhận job là trừ. KIT chỉ hiện KHỚP MÔI khi có file. Take cũ giữ lịch sử.`,
        okText: `Confirm · trừ ~$${falUsd.toFixed(2)} Fal`,
        cancelText: 'Hủy — 0 Fal',
        onOk: () =>
          void startLipsync(need.map((s) => s.id), {
            confirmed: true,
            remake: opts?.remake || opts?.forceNew,
            forceNew: opts?.forceNew,
          }),
      });
      return;
    }
    for (const s of need) {
      const spoken = spokenLinesOf(s);
      if (!lipsyncNeedsSpeakerPick(s, spoken, stateRef.current.characters)) continue;
      const live = shotRunOf(stateRef.current, s);
      if (isSpeakerFaceBandMark(live.lipsyncSpeaker)) continue;
      const side = lipsyncSpeakerSideOfFaceMark(live.lipsyncSpeaker) || 'right';
      patchRun(s.id, { lipsyncSpeaker: lipsyncSpeakerPickOf(side, stateRef.current.characters) });
    }
    const omitSpeakerByShot = new Map(
      need.map((s) => {
        const live = shotRunOf(stateRef.current, s);
        return [
          s.id,
          shouldOmitRejectedSpeakerPin({
            remake: Boolean(opts?.forceNew || opts?.remake),
            error: live.lipsyncError,
            frozenRegion: live.lipsyncFrozenInput?.speakerFaceRegion,
            liveMark: live.lipsyncSpeaker,
          }),
        ] as const;
      }),
    );
    for (const s of need) {
      if (resumeIds.has(s.id)) continue;
      patchRun(s.id, { lipsyncError: undefined, lipsyncStatus: undefined });
    }
    let ok = 0;
    for (let i = 0; i < need.length; i++) {
      const shot = need[i]!;
      const run = shotRunOf(stateRef.current, shot);
      const lines = spokenLinesOf(shot);
      if (!lines.length) continue;
      if (multiSpeakerBlock(lines)) {
        message.error(`${studioShotCode(shot, pack)}: ${multiSpeakerBlock(lines) || 'MULTI_SPEAKER_LIPSYNC_UNSUPPORTED'}`);
        continue;
      }
      const merged = mergeSameSpeakerCues(
        lines.map((line, idx) => ({
          dialogueId: line.id,
          shotId: shot.id,
          speakerCharacterId: line.characterId || '',
          text: line.text,
          voiceId: line.voiceId,
          startSec: idx * 1,
          endSec: (idx + 1) * 1,
          audioAssetId: line.id,
          audioDurationSec: Number(stateRef.current.voiceAssets?.[line.id]?.duration || 0),
          status: (Number(stateRef.current.voiceAssets?.[line.id]?.duration || 0) > 0.2 ? 'READY' : 'MISSING') as 'READY' | 'MISSING',
        })),
      );
      if (merged.discarded.length) {
        message.error(`${studioShotCode(shot, pack)}: không được bỏ câu thoại.`);
        continue;
      }
      const prev = turboLockRef.current;
      let release = () => {};
      turboLockRef.current = new Promise<void>((resolve) => {
        release = resolve;
      });
      await prev;
        setLipsyncBusy(shot.id);
        try {
        if (resumeIds.has(shot.id) || (shouldResumeLipsync(run) && !opts?.forceNew)) {
          const recovered = parseFalJobIdFromError(run.lipsyncError);
          const resumeId =
            run.lipsyncTaskId?.trim() ||
            (recovered ? `${lipsyncTaskPrefix(falModel)}${recovered}` : '');
          if (!resumeId) {
            message.error(`${studioShotCode(shot, pack)}: hết task id. Dán URL từ fal.ai → Usage (Gắn Fal · 0$).`);
            continue;
          }
          if (!run.lipsyncTaskId?.trim()) patchRun(shot.id, { lipsyncTaskId: resumeId, lipsyncStatus: 'RETRY' });
          message.loading({
            content: `Hỏi lại khớp môi ${studioShotCode(shot, pack)} (${i + 1}/${need.length}) · 0 Fal mới`,
            key: 'lipsync',
            duration: 0,
          });
          if (await pollExistingLipsync(shot, resumeId)) ok += 1;
          continue;
        }

        const fps = computeInputFingerprints(stateRef.current, shot);
        const liveRun = shotRunOf(stateRef.current, shot);
        const remakeLineIds = lines.map((line) => line.id);
        const remakeDurationByLine = Object.fromEntries(
          lines.map((line) => [line.id, Number(stateRef.current.voiceAssets?.[line.id]?.duration || 0)]),
        );
        const remakeVoiceId = voiceIdsForLine(lines[0]!).find(Boolean);
        let remakeSrc = resolveLipSyncRemakeSources({
          run: liveRun,
          shotId: shot.id,
          motionFingerprint: fps.motion,
          lineIds: remakeLineIds,
          voiceFingerprint: fps.voice,
          durationByLineId: remakeDurationByLine,
          voiceAssets: stateRef.current.voiceAssets,
          voiceId: remakeVoiceId,
        });
        if (!remakeSrc.voice) {
          const storedVoice = await resolveStoredVoicePhysicalSource({
            store: voicePhysicalStoreRef.current,
            buildId: stateRef.current.buildId,
            lineId: lines[0]!.id,
            lineIds: remakeLineIds,
            voiceId: remakeVoiceId,
            voiceFingerprint: fps.voice,
            durationSec:
              remakeDurationByLine[lines[0]!.id] || Number(liveRun.lipsyncFrozenInput?.voiceDurationSec) || 0,
            durationByLineId: remakeDurationByLine,
            preferredArtifactId: liveRun.lipsyncFrozenInput?.voiceArtifactId,
          });
          if (storedVoice) {
            remakeSrc = { ...remakeSrc, voice: storedVoice };
            if (
              storedVoice.artifactId !== storedVoice.lineId &&
              storedVoice.storageKey &&
              storedVoice.voiceId
            ) {
              persistState(
                rebindVoicePhysicalGraphRef(stateRef.current, storedVoice.lineId, {
                  artifactId: storedVoice.artifactId,
                  storageKey: storedVoice.storageKey,
                  voiceId: storedVoice.voiceId,
                }),
              );
            }
          }
        }
        if (!remakeSrc.voice && remakeVoiceId && stateRef.current.buildId) {
          const line = lines[0]!;
          const blob = await resolveLineAudio(line);
          if (blob) {
            const durationSec =
              remakeDurationByLine[line.id] ||
              Number(liveRun.lipsyncFrozenInput?.voiceDurationSec) ||
              (await measureAudioSec(blob));
            const physicalId = voicePhysicalIdentityOf({
              artifactId: mintVoicePhysicalArtifactId(),
              buildId: stateRef.current.buildId,
              lineId: line.id,
              voiceId: remakeVoiceId,
              durationSec,
            });
            if (physicalId && durationSec > 0) {
              const saved = await saveVoicePhysicalArtifact(
                physicalId,
                new Uint8Array(await blob.arrayBuffer()),
                voicePhysicalStoreRef.current,
              );
              const prev = stateRef.current.voiceAssets?.[line.id];
              persistState(
                prev
                  ? rebindVoicePhysicalGraphRef(stateRef.current, line.id, {
                      artifactId: saved.artifactId,
                      storageKey: saved.storageKey,
                      voiceId: saved.voiceId,
                    })
                  : {
                      ...stateRef.current,
                      voiceAssets: {
                        ...(stateRef.current.voiceAssets ?? {}),
                        [line.id]: {
                          lineId: line.id,
                          characterId: line.characterId,
                          duration: Number(durationSec.toFixed(2)),
                          status: 'ready',
                          artifactId: saved.artifactId,
                          storageKey: saved.storageKey,
                          voiceId: saved.voiceId,
                          physicalArtifactResolvable: true,
                        },
                      },
                    },
              );
              remakeSrc = {
                ...remakeSrc,
                voice: {
                  artifactId: saved.artifactId,
                  lineId: line.id,
                  lineIds: remakeLineIds,
                  storageKey: saved.storageKey,
                  voiceId: saved.voiceId,
                  fingerprint: fps.voice,
                  durationSec,
                  durationByLineId: { ...remakeDurationByLine, [line.id]: durationSec },
                },
              };
            }
          }
        }
        const motionSrc = remakeSrc.motion;
        const voiceSrc = remakeSrc.voice;
        if (!motionSrc || !voiceSrc) {
          message.error({
            content: !motionSrc
              ? `${studioShotCode(shot, pack)}: thiếu Motion CURRENT (bound take). Không gửi Fal.`
              : `${studioShotCode(shot, pack)}: thiếu Voice artifact. Mở tab Người — phát lại câu Linh. Không gửi Fal.`,
            key: 'famixa-desk-err',
          });
          continue;
        }
        const execNow = snapshotOfShot(stateRef.current, shot, ttsFilesRef.current).execution;
        const speakerMark = shotRunOf(stateRef.current, shot).lipsyncSpeaker;
        const multiFace = lipsyncNeedsSpeakerPick(shot, lines, stateRef.current.characters);
        const omitSpeakerPin = multiFace ? false : omitSpeakerByShot.get(shot.id) === true;
        const frozen = freezeLipSyncExecutionInput({
          motionArtifactId: motionSrc.artifactId,
          motionFingerprint: motionSrc.fingerprint || fps.motion,
          motionDurationSec: motionSrc.durationSec,
          voiceArtifactId: voiceSrc.artifactId,
          voiceFingerprint: voiceSrc.fingerprint,
          voiceDurationSec: voiceSrc.durationSec,
          fingerprint: fps.lipsync,
          voiceLineIds: voiceSrc.lineIds,
          voiceDurationByLineId: voiceSrc.durationByLineId,
          usedValidity: execNow?.currentArtifacts.motion?.validity,
          providerId: 'fal',
          modelId: falModel,
          buildId: stateRef.current.buildId,
          voiceId: voiceSrc.voiceId || voiceIdsForLine(lines[0]!).find(Boolean),
          lineId: voiceSrc.lineId,
          motionTaskId: motionSrc.taskId,
          motionUrl: motionSrc.url,
          speakerCharacterId:
            lipsyncSpeakerCharacterIdFromFaceMark(speakerMark, stateRef.current.characters) ||
            lines[0]?.characterId,
          speakerFaceRegion:
            omitSpeakerPin || !speakerMark
              ? undefined
              : {
                  x: speakerMark.x,
                  y: speakerMark.y,
                  width: speakerMark.width,
                  height: speakerMark.height,
                },
          speakerFrameNumber: speakerMark?.frameNumber ?? LIPSYNC_SPEAKER_MARK_FRAME_NUMBER,
        });
        try {
          if (!omitSpeakerPin) {
            assertLipSyncSpeakerTargetReady({
              multiFace: lipsyncNeedsSpeakerPick(shot, lines, stateRef.current.characters),
              speakerCharacterId: frozen.speakerCharacterId,
              speakerFaceRegion: frozen.speakerFaceRegion,
              requiredCharacterId: lines[0]?.characterId,
            });
          }
        } catch (err) {
          message.error(`${studioShotCode(shot, pack)}: ${(err as Error).message || 'thiếu speaker target. Không gửi Fal.'}`);
          continue;
        }
        patchRun(shot.id, {
          lipsyncFrozenInput: frozen,
          lipsyncExecutionAttempt: createLipSyncAttemptProvenance(frozen),
          lipsyncError: undefined,
          lipsynced: false,
          lipsyncStatus: 'PENDING',
        });
        const takeHistory = [...(run.takeHistory ?? [])];
        if (motionSrc.url) takeHistory.unshift({ url: motionSrc.url, taskId: motionSrc.taskId });
        message.loading({
          content: `Khớp môi ${studioShotCode(shot, pack)} (${i + 1}/${need.length}) · Fal`,
          key: 'lipsync',
          duration: 0,
        });
        const submitted = await submitLipSyncFrozenExecution({
          frozen,
          run: shotRunOf(stateRef.current, shot),
          shotId: shot.id,
          persistAttempt: (attempt) => {
            patchRun(shot.id, { lipsyncExecutionAttempt: attempt, lipsyncStatus: attempt.status });
          },
          loadVoiceBytes: async () => {
            const bytes = await loadFrozenVoicePhysicalBytes(frozen, voicePhysicalStoreRef.current);
            return new Blob([bytes], { type: 'audio/mpeg' });
          },
          startProvider: async (input) => {
            const how = buildLipSyncProviderHowRequest({
              frozen,
              input,
              clipId: shot.id,
              syncMode: falSync,
              model: falModel,
            });
            if (frozen.speakerFaceRegion) {
              mapFrozenSpeakerToFalV3How({
                videoUrl: how.videoUrl,
                takeTaskId: how.takeTaskId,
                syncMode: how.syncMode,
                speakerCharacterId: how.speakerCharacterId,
                speakerFaceRegion: frozen.speakerFaceRegion,
                speakerFrameNumber: frozen.speakerFrameNumber,
              });
            }
            const audioBase64 = await blobToBase64(input.voiceBytes);
            return startContentSeriesLipsync({
              clipId: how.clipId,
              videoUrl: how.videoUrl,
              audioBase64,
              mime: input.voiceBytes.type || 'audio/mpeg',
              syncMode: falSync,
              model: falModel,
              performanceDurationSec: how.performanceDurationSec,
              takeTaskId: how.takeTaskId,
              speakerX: omitSpeakerPin ? undefined : how.speakerX,
              speakerY: omitSpeakerPin ? undefined : how.speakerY,
              speakerVideoWidth: omitSpeakerPin ? undefined : how.speakerVideoWidth,
              speakerVideoHeight: omitSpeakerPin ? undefined : how.speakerVideoHeight,
              speakerFrameNumber: omitSpeakerPin ? undefined : how.speakerFrameNumber,
              voices: [{ audioBase64, startSec: 0, mime: input.voiceBytes.type || 'audio/mpeg' }],
            });
          },
        });
        const started = submitted.providerResult;
        patchRun(shot.id, {
          lipsyncTaskId: started.taskId,
          lipsyncStatus: started.status,
          lipsyncError: undefined,
          lipsynced: false,
          takeHistory: takeHistory.slice(0, 6),
          lipsyncExecutionAttempt: stampLipSyncAttemptProviderRequest(
            submitted.attempt,
            started.taskId,
            started.status === 'SUCCEEDED' ? 'SUCCEEDED' : 'PENDING',
          ),
          lipsyncSelectionSnapshot: started.decisionId
            ? {
                decisionId: started.decisionId,
                providerId: started.providerId || 'fal',
                modelId: started.model,
                selectionMode: started.selectionMode || '',
                reason: started.selectionReason || undefined,
                estimatedCost: started.estimatedCost,
                costKind: started.costKind || undefined,
              }
            : undefined,
        });
        if (started.status === 'SUCCEEDED' && started.videoUrl) {
          const live = shotRunOf(stateRef.current, shot);
          const localReady =
            isFalPlaybackSrc(started.videoUrl) && (await proveLocalFalFile(started.taskId));
          if (!canStampFalLipsyncFinal(started.videoUrl, localReady)) {
            patchRun(shot.id, {
              lipsyncStatus: 'SUCCEEDED',
              lipsynced: false,
              lipsyncTaskId: started.taskId,
              lipsyncError:
                'Fal xong — KIT chưa có file playable trên máy API. Gắn Fal · 0$ (dán video.url). Không Confirm job mới.',
            });
          } else {
            patchRun(shot.id, {
              ...stampFalFinal(live, started.videoUrl, localReady),
              lipsyncStatus: 'SUCCEEDED',
              lipsyncError: undefined,
              model: started.model || 'sync-lipsync-1.9',
              shotQa: { ...live.shotQa, lipsync: true },
            });
            ok += 1;
          }
        } else if (await pollExistingLipsync(shot, started.taskId)) {
          ok += 1;
        }
      } catch (e) {
        const raw = apiErrorMessage(e, 'Không gửi được Fal khớp môi.');
        const status = typeof e === 'object' && e && 'response' in e
          ? Number((e as { response?: { status?: number } }).response?.status)
          : 0;
        const why =
          status === 404
            ? 'API :5290 chưa có khớp môi (process cũ). Restart API rồi F5. Đừng Confirm job mới nếu Fal Usage đã có file.'
            : /401|403|hết hạn|Không tải được take/i.test(raw)
              ? `${raw.replace(/gửi lại clip\.?/i, '').trim()} Hỏi lại task cũ · 0 cr. Không tạo video chuyển động mới.`
              : raw;
        const recovered = parseFalJobIdFromError(why);
        patchRun(shot.id, {
          lipsyncStatus: recovered ? 'RETRY' : 'FAILED',
          lipsyncError: why,
          ...(recovered ? { lipsyncTaskId: `${lipsyncTaskPrefix(falModel)}${recovered}` } : {}),
        });
        message.error(`${studioShotCode(shot, pack)}: ${why}`);
      } finally {
        setLipsyncBusy(undefined);
        release();
      }
    }
    if (ok) {
      message.success({ content: `Đã khớp môi ${ok}/${need.length} Short. Ghép sẽ giữ tiếng trong video.`, key: 'lipsync' });
    } else {
      message.destroy('lipsync');
    }
  };

  /** EDITORIAL. Pastes a Fal file onto the desk run. Does not create ProductionArtifact or ProductionBinding. */
  const attachLipsync = (shotId: string) => {
    let raw = '';
    const shot = productionShorts(stateRef.current).find((s) => s.id === shotId);
    const code = shot ? studioShotCode(shot, productionShorts(stateRef.current)) : shotId;
    modal.confirm({
      title: `Gắn file Fal ${code} · 0$`,
      content: (
        <div>
          <p style={{ marginTop: 0 }}>
            Chỉ dán khi bạn đã có link mp4 trên tay (https://…fal.media/….mp4). Không có link thì bấm Hủy — Play video trên bàn để xem + nghe thoại. KIT không gửi job mới, không trừ tiền.
          </p>
          <Input.TextArea
            rows={3}
            placeholder="https://v3b.fal.media/…/output.mp4 hoặc request id"
            onChange={(e) => {
              raw = e.target.value;
            }}
          />
        </div>
      ),
      okText: 'Gắn · 0$',
      cancelText: 'Hủy',
      async onOk() {
        if (!shot) {
          message.error('Không tìm thấy Short.');
          return Promise.reject();
        }
        const ref = parseFalLipsyncRef(raw);
        if (ref.url) {
          const live = shotRunOf(stateRef.current, shot);
          patchRun(shotId, {
            ...stampFalFinal(live, ref.url),
            lipsyncStatus: 'SUCCEEDED',
            lipsyncError: undefined,
            model: 'sync-lipsync-v3',
            shotQa: { ...live.shotQa, lipsync: true },
          });
          message.success(`${code}: đã gắn file Fal · 0$.`);
          return;
        }
        if (!ref.taskId) {
          message.error('Dán URL mp4 hoặc Request ID trên Fal Usage.');
          return Promise.reject();
        }
        const task = await getContentSeriesTurbo(ref.taskId);
        if (!task.videoUrl) {
          message.error(task.error || 'Fal chưa trả file cho id này. Thử dòng Quantity lớn hơn (0.17).');
          return Promise.reject();
        }
        const localReady =
          isFalPlaybackSrc(task.videoUrl) && (await proveLocalFalFile(task.taskId || ref.taskId));
        if (!canStampFalLipsyncFinal(task.videoUrl, localReady)) {
          message.error('Fal chưa có file playable trên máy này. Dán URL mp4 fal.media — đừng Confirm job mới.');
          return Promise.reject();
        }
        const live = shotRunOf(stateRef.current, shot);
        patchRun(shotId, {
          ...stampFalFinal(live, task.videoUrl, localReady),
          lipsyncTaskId: task.taskId || ref.taskId,
          lipsyncStatus: 'SUCCEEDED',
          lipsyncError: undefined,
          model: task.model || 'sync-lipsync-v3',
          shotQa: { ...live.shotQa, lipsync: true },
        });
        message.success(`${code}: lấy lại file Fal · 0$.`);
      },
    });
  };

  const hydrateCutVoice = async () => {
    const lines = cutPlan.items.flatMap((i) => (i.lines.length ? i.lines : i.line ? [i.line] : []));
    let n = 0;
    for (const line of lines) {
      if (await resolveLineAudio(line)) n += 1;
    }
    return n;
  };
  const fillPreviewCut = (kind: 'story' | 'motion') => {
    const needVoice = cutPlan.items.filter((i) => !i.silent && !i.hasVoiceFile);
    if (kind === 'story') {
      if (cutPlan.storyMissingKf.length) {
        generateSceneKf(cutPlan.storyMissingKf);
        return;
      }
      if (needVoice.length) {
        message.info(
          `Đã đủ hình. Thiếu ${needVoice.length} file TTS session (F5 mất MP3) — GHÉP PREVIEW vẫn chạy hình. Phát lại ở bước Thoại, không gọi ElevenLabs nếu đã khóa.`,
        );
        return;
      }
      message.success('Đoạn đã chọn đã đủ hình.');
      return;
    }
    if (cutPlan.durationBlocked) {
      message.error('Thoại dài hơn shot — không gửi I2V. Sửa thoại hoặc tăng seconds.');
      return;
    }
    if (cutPlan.storyMissingKf.length) {
      message.warning('Còn thiếu KF. Tạo và duyệt KF đoạn trước, rồi mới video.');
      generateSceneKf(cutPlan.storyMissingKf);
      return;
    }
    startSceneTurbo(cutRange.map((s) => s.id));
  };

  const downloadTakes = async (ids?: string[], kind?: 'take' | 'lipsync') => {
    const pack = productionShorts(stateRef.current);
    const range = ids?.length ? pack.filter((s) => ids.includes(s.id)) : cutRange;
    const wantLip = kind === 'lipsync';
    const ready = range.filter((s) => {
      const run = shotRunOf(stateRef.current, s);
      return wantLip ? Boolean(lipsyncVideoUrl(run)) : Boolean(resolveTakeUrl(run));
    });
    if (!ready.length) {
      message.warning(
        wantLip
          ? 'Chưa có file khớp môi trên dải. Confirm Khớp môi trước — rồi mới tải.'
          : 'Chưa có take trên dải đã chọn. Gửi Runway ở bước 5 Video trước.',
      );
      return;
    }
    setAssembleBusy(true);
    try {
        let saved = 0;
        for (let i = 0; i < ready.length; i++) {
          const s = ready[i]!;
          const run = shotRunOf(stateRef.current, s);
          const url = (wantLip ? lipsyncVideoUrl(run) : resolveTakeUrl(run)) || '';
          const name = wantLip ? lipsyncDownloadName(s, pack) : takeDownloadName(s, pack);
          message.loading({
            content: `Tải ${wantLip ? 'khớp môi ' : ''}${studioShotCode(s, pack)} (${i + 1}/${ready.length})`,
            key: 'take-dl',
            duration: 0,
          });
          try {
            const blob = await takeBlobFromUrl(url);
            triggerDownload(blob, name);
            saved += 1;
          } catch (e) {
            window.open(url, '_blank', 'noopener,noreferrer');
            message.warning(
              `${studioShotCode(s, pack)}: ${e instanceof Error ? e.message : 'Không tải qua API'} — đã mở link, lưu MP4 từ tab đó.`,
            );
          }
          await new Promise((r) => window.setTimeout(r, 400));
        }
        message.success({
          content: saved === ready.length
            ? `Đã tải ${saved} ${wantLip ? 'khớp môi' : 'take'}.`
            : `Tải ${saved}/${ready.length}. Phần còn lại mở tab.`,
          key: 'take-dl',
        });
    } catch (e) {
      message.error({
        content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không tải được take.'),
        key: 'take-dl',
      });
    } finally {
      setAssembleBusy(false);
    }
  };

  const assembleCut = async (opts?: { resynth?: boolean; confirmed?: boolean }) => {
    persistState(applyContinuityChain(stateRef.current, productionShorts(stateRef.current)));
    if ((stateRef.current.episode?.shots ?? []).some((s) => !Array.isArray(s.dialogueSegmentIds))) {
      persistState(applyDialogueMap(stateRef.current));
    }
    await hydrateCutVoice();
    const hole = completeCutBlocked(cutPlan);
    if (hole.length) {
      message.error(`Chưa ghép đủ tập — thiếu KF: ${hole.join(', ')}. Tạo / duyệt KF rồi ghép. Không bỏ shot.`);
      return;
    }
    const readyPlan = planCompleteCut(cutPlan);
    if (!readyPlan.items.length) {
      message.warning('Chưa có KF trên dải. Tạo hình rồi ghép — không cắt nhịp kịch bản.');
      return;
    }
    const srcBlock = finalSourceBlockReason(
      readyPlan.items.map((i) => ({
        code: i.code,
        silent: i.silent,
        finalSource: i.finalSource,
        lipsynced: i.lipsynced,
      })),
    );
    if (srcBlock) {
      message.error(`LIPSYNC_REQUIRED. ${srcBlock} Overlay TTS không phải Final.`);
      return;
    }
    let mixSheet = compileMixCueSheet(
      buildAssembleTimeline(readyPlan, { hasVoiceFile: () => true, fit: 'speech' }),
      productionShorts(stateRef.current),
      stateRef.current,
    );
    const confirm = assembleConfirmCopy(readyPlan);
    const holdN = completeCutHolds(readyPlan).length;
    if (!opts?.confirmed) {
      modal.confirm({
        title: `Xuất tương thích · ${readyPlan.items.length} shot + thoại · 0 cr Runway`,
        content: `${confirm.detail} Câu chưa gắn Short không đưa vào file. Không gửi job mới.`,
        okText: confirm.okText,
        cancelText: 'Hủy',
        onOk: () => void assembleCut({ ...opts, confirmed: true }),
      });
      return;
    }
    const script = deriveVoiceScript(stateRef.current);
    const cov = coverageOf(stateRef.current, cutRange, {
      hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id)),
    });
    if (cov.extraUnmapped.length) {
      message.warning(
        `${cov.extraUnmapped.length} câu chưa gắn Short — bỏ khỏi file ghép. Không đoán map, không trừ Runway.`,
      );
    }
    const spoken = assembleNeedTtsOverlay(readyPlan).flatMap((i) => (i.lines.length ? i.lines : i.line ? [i.line] : []));
    const missing = [];
    for (const line of spoken) {
      if (!(await resolveLineAudio(line))) missing.push(line);
    }
    if (missing.length && !opts?.resynth) {
      modal.confirm({
        title: 'Thiếu file thoại trên máy',
        content: `File vừa tải câm vì không gửi được MP3 (F5 mất file). ${missing.length} câu trên dải này. Tạo lại rồi mix — ElevenLabs tính ký tự, không trừ Runway, không tạo ảnh mới.`,
        okText: `Tạo lại ${missing.length} câu rồi ghép MP4`,
        cancelText: 'Hủy — không tải file câm',
        onOk: () => void assembleCut({ ...opts, resynth: true, confirmed: true }),
      });
      return;
    }
    if (missing.length && opts?.resynth) {
      if (!keys.elevenLabs) {
        message.error('Chưa có key ElevenLabs — không tạo được thoại, không ghép file câm.');
        return;
      }
      setAssembleBusy(true);
      try {
        for (let i = 0; i < missing.length; i++) {
          const line = missing[i]!;
          const full =
            script.lines.find((l) => l.id === line.id) ||
            script.lines.find((l) => l.text.replace(/\s+/g, ' ').trim() === line.text.replace(/\s+/g, ' ').trim());
          if (!full?.voiceId) throw new Error(`Chưa gán Voice Canon cho “${line.text.slice(0, 40)}”.`);
          message.loading({ content: `TTS ${i + 1}/${missing.length} · ${full.name}`, key: 'assemble', duration: 0 });
          await loadCueAudio(full);
        }
      } catch (e) {
        message.error({
          content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không tạo lại được thoại.'),
          key: 'assemble',
        });
        setAssembleBusy(false);
        return;
      }
    }
    const stillMissing = [];
    for (const line of spoken) {
      if (!(await resolveLineAudio(line))) stillMissing.push(line);
    }
    if (stillMissing.length) {
      message.error(`Chưa ghép — vẫn thiếu TTS (${stillMissing.length} câu). Không tải file câm.`);
      setAssembleBusy(false);
      return;
    }
    const assets = stateRef.current.voiceAssets ?? {};
    const measured: Record<string, number> = {};
    for (const line of spoken) {
      const full = script.lines.find((l) => l.id === line.id);
      const blob = await resolveLineAudio({
        id: line.id,
        text: line.text,
        voiceId: full?.voiceId ?? line.voiceId,
        characterId: full?.characterId ?? line.characterId,
      });
      if (!blob) continue;
      const sec = (await measureAudioSec(blob)) || assets[line.id]?.duration || 0;
      if (sec > 0.2) measured[line.id] = Number(sec.toFixed(2));
    }
    const tl = buildAssembleTimeline(readyPlan, {
      hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id) || measured[id]),
      voiceSecOf: (id) => measured[id] || assets[id]?.duration,
      fit: 'speech',
    });
    mixSheet = compileMixCueSheet(tl, productionShorts(stateRef.current), stateRef.current);
    const stem = assembleFileStem(readyPlan, ep?.episode, ep?.title);
    triggerDownload(new Blob([formatSrt(tl.cues)], { type: 'text/plain;charset=utf-8' }), `${stem}.srt`);
    setAssembleBusy(true);
    try {
      message.loading({
        content: mixSheet.music
          ? 'FFmpeg: phòng + Foley + nhạc (duck) + thoại → −14 LUFS…'
          : 'FFmpeg: phòng + Foley + thoại → −14 LUFS…',
        key: 'assemble',
        duration: 0,
      });
      const pack = productionShorts(stateRef.current);
      const clips = [];
      for (const clip of tl.clips) {
        const shot = pack.find((s) => s.id === clip.shotId);
        const run = shot ? shotRunOf(stateRef.current, shot) : undefined;
        const videoUrl = assembleVideoUrl(run) || takeVideoUrl(run) || '';
        let stillBase64: string | undefined;
        if (!videoUrl) {
          const kf =
            run?.keyframeDataUrl?.startsWith('data:image')
              ? run.keyframeDataUrl
              : (await loadKfPixels(clip.shotId))?.dataUrl;
          if (!kf?.startsWith('data:image')) {
            throw new Error(`${clip.code}: thiếu KF để giữ khung. Không bỏ shot — tạo KF rồi ghép.`);
          }
          stillBase64 = kf.includes(',') ? kf.slice(kf.indexOf(',') + 1) : kf;
        }
        const keepLip = Boolean(videoUrl && run?.lipsyncUrl?.trim());
        const voices = [];
        for (const cue of clip.cues) {
          const full = script.lines.find((l) => l.id === cue.lineId);
          const blob = await resolveLineAudio({
            id: cue.lineId,
            text: cue.text,
            voiceId: full?.voiceId,
            characterId: full?.characterId,
          });
          if (!blob) {
            if (!keepLip) throw new Error(`Thiếu thoại ${cue.code}: “${cue.text.slice(0, 40)}”. Không ghép file câm.`);
            continue;
          }
          voices.push({
            lineId: cue.lineId,
            startSec: Math.max(0, cue.startSec - clip.startSec),
            audioBase64: await blobToBase64(blob),
            mime: blob.type || 'audio/mpeg',
          });
        }
        const requireVoice = clip.cues.length > 0;
        if (requireVoice && !keepLip && !voices.length) {
          throw new Error(`${clip.code}: thiếu thoại — không ghép file câm.`);
        }
        clips.push({
          code: clip.code,
          videoUrl: videoUrl || undefined,
          seconds: shot ? editorialDurationOf(stateRef.current, shot) || clip.seconds : clip.seconds,
          voices,
          useVideoAudio: keepLip,
          requireVoice,
          stillBase64,
        });
      }
      const voiceCount = clips.reduce((n, c) => n + c.voices.length, 0);
      const lipN = clips.filter((c) => c.useVideoAudio).length;
      if (clips.some((c) => c.requireVoice && !c.useVideoAudio && !c.voices.length)) {
        throw new Error('Không gửi được file thoại lên API. Không tải file câm.');
      }
      try {
        const mp4 = await assembleContentSeriesCut({
          fileStem: stem,
          aspect: assembleAspect,
          clips,
          mix: assembleMixPayload(mixSheet, smoothnessOf(stateRef.current)),
        });
        triggerDownload(mp4, `${stem}.mp4`);
        persistState({ ...stateRef.current, previewApproved: true });
        message.success({
          content: `Đã xuất file tương thích ${tl.clips.length} shot${
            holdN ? ` · ${holdN} HOLD KF` : ''
          }${lipN ? ` · ${lipN} khớp môi` : ''}${voiceCount ? ` · ${voiceCount} câu thoại` : ''}${
            mixSheet.room ? ' · phòng' : ''
          }${mixSheet.sfx.length ? ` · Foley ${mixSheet.sfx.length}` : ''}${
            mixSheet.music ? ' · nhạc duck' : ''
          }${mixSheet.loudnorm ? ' · −14 LUFS' : ''} → ${stem}.mp4`,
          key: 'assemble',
        });
        return;
      } catch (e) {
        const why = e instanceof Error ? e.message : apiErrorMessage(e, 'FFmpeg lỗi.');
        message.warning({ content: `${why} — ghép tạm trên trình duyệt (WebM).`, key: 'assemble', duration: 2 });
      }
      const blob = await recordAssembledCut({
        clips: tl.clips,
        videoOf: async (shotId) => {
          const shot = pack.find((s) => s.id === shotId);
          const run = shot ? shotRunOf(stateRef.current, shot) : undefined;
          const url = run ? assembleVideoUrl(run) || takeVideoUrl(run) : '';
          if (url) return takeBlobFromUrl(url);
          const kf =
            run?.keyframeDataUrl?.startsWith('data:image')
              ? run.keyframeDataUrl
              : (await loadKfPixels(shotId))?.dataUrl;
          if (!kf?.startsWith('data:image')) throw new Error('Thiếu KF để giữ khung.');
          const res = await fetch(kf);
          return res.blob();
        },
        audioOf: async (lineId) => {
          const line = script.lines.find((l) => l.id === lineId);
          return line ? resolveLineAudio(line) : ttsBlobs.current.get(lineId) || loadTtsBlob(lineId);
        },
        onProgress: (msg) => message.loading({ content: `Ghép ${msg}`, key: 'assemble', duration: 0 }),
      });
      triggerDownload(blob, `${stem}.webm`);
      persistState({ ...stateRef.current, previewApproved: true });
      message.success({ content: `Đã ghép hoàn thiện ${tl.clips.length} take (WebM) + SRT.`, key: 'assemble' });
    } catch (e) {
      message.error({
        content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không ghép được file.'),
        key: 'assemble',
      });
    } finally {
      setAssembleBusy(false);
    }
  };

  const mixStaffEpisode = () => {
    persistState(applyDialogueMap(stateRef.current));
    const pack = episodeShots(stateRef.current);
    const script = deriveVoiceScript(stateRef.current);
    const rows = pack
      .map((s, index) => ({
        shot: s,
        label: `Cảnh ${String(index + 1).padStart(2, '0')}`,
        src: clipOf(shotRunOf(stateRef.current, s)),
        lines: linesForShot(stateRef.current, s, script.lines),
      }))
      .filter((r) => r.src);
    if (!rows.length) {
      message.warning('Chưa có clip để ghép tiếng.');
      return;
    }
    const spoken = rows.flatMap((r) => r.lines);
    if (!spoken.length) {
      message.warning('Kịch bản chưa gắn thoại vào các cảnh này. Take Runway vốn câm — thêm lời ở tab Kịch bản. Máy không tự viết thoại.');
      return;
    }
    modal.confirm({
      title: `Preview TTS ${rows.length} cảnh + ${spoken.length} câu (${MIX_STAFF_EPISODE_ROLE})`,
      content: 'Không phải Final. Take đang câm. Overlay TTS trên trình duyệt — miệng chưa khớp. Shot có thoại phải Fal rồi mix server.',
      okText: 'Tải preview',
      cancelText: 'Hủy',
      onOk: async () => {
        setAssembleBusy(true);
        try {
          let t = 0;
          const clips = [];
          for (const row of rows) {
            const cues = [];
            let local = 0;
            for (const line of row.lines) {
              let blob = await resolveLineAudio(line);
              if (!blob && keys.elevenLabs && line.voiceId) {
                message.loading({ content: `TTS · ${line.name}`, key: 'staff-mix', duration: 0 });
                await loadCueAudio(line);
                blob = await resolveLineAudio(line);
              }
              if (!blob) {
                throw new Error(
                  line.voiceId
                    ? `Thiếu tiếng «${line.text.slice(0, 40)}». Cần key ElevenLabs để tạo TTS.`
                    : `Chưa gán giọng cho ${line.name}. Gán Voice Canon rồi ghép lại.`,
                );
              }
              const sec = Math.max(
                0.8,
                (await measureAudioSec(blob)) || Number(stateRef.current.voiceAssets?.[line.id]?.duration || 0) || 2,
              );
              cues.push({
                lineId: line.id,
                shotId: row.shot.id,
                code: row.label,
                name: line.name || '',
                text: line.text,
                startSec: t + local,
                endSec: t + local + sec,
              });
              local += sec;
            }
            const seconds = Math.max(row.shot.seconds || 5, local);
            clips.push({
              shotId: row.shot.id,
              code: row.label,
              startSec: t,
              seconds,
              videoUrl: row.src,
              cues,
            });
            t += seconds;
          }
          message.loading({ content: 'Ghép take + TTS…', key: 'staff-mix', duration: 0 });
          const blob = await recordAssembledCut({
            clips,
            videoOf: async (id) => {
              const src = rows.find((r) => r.shot.id === id)?.src;
              if (!src) throw new Error('Thiếu clip.');
              return takeBlobFromUrl(src);
            },
            audioOf: async (lineId) => {
              const line = spoken.find((l) => l.id === lineId);
              return line ? resolveLineAudio(line) : undefined;
            },
          });
          triggerDownload(blob, `${epCode}-preview-tts.webm`);
          message.success({ content: `Preview TTS ${rows.length} cảnh (${MIX_STAFF_EPISODE_ROLE}). Không phải Final.`, key: 'staff-mix' });
        } catch (e) {
          message.error({
            content: e instanceof Error ? e.message : 'Không ghép được tiếng.',
            key: 'staff-mix',
          });
        } finally {
          setAssembleBusy(false);
        }
      },
    });
  };

  /** Editorial V1. Preview = ready shots. Final = all shots. Overlay only — 0 provider calls. */
  const exportEditorialCut = (kind: 'preview' | 'final' | 'watch', mode?: EditorialCutMode) => {
    message.warning(productionOsLegacyRefuseNote('ASSEMBLE', 'POST /content/series/assemble'));
    if (productionOsLegacyPathClosed()) return;
    const live = stateRef.current;
    const pack = episodeShots(live);
    const ready = pack.filter((s) => editorialSourceReady(shotRunOf(live, s)));
    const selected = kind === 'final' ? pack : ready;
    if (kind === 'final' && ready.length !== pack.length) {
      message.warning(
        ready.length
          ? `Chưa đủ video để dựng (${ready.length}/${pack.length}). Preview các shot đã có URL — Xuất khi đủ nguồn.`
          : 'Chưa có video nguồn để dựng. Take / lipsync không còn trên graph.',
      );
      return;
    }
    if (!selected.length) {
      message.warning('Chưa có video nguồn để dựng preview.');
      return;
    }
    const cut = { ...editorialCutOf(live), ...(mode ? { mode } : {}) };
    const voiceSecOf = (id: string) => Number(live.voiceAssets?.[id]?.duration || 0);
    const windows = selected.map((shot) => resolveEditorialWindow(live, shot, cut, voiceSecOf));
    const totalSec = windows.reduce((n, w) => n + w.seconds, 0);
    const stem = editorialFileStem(epCode, kind === 'final' ? 'final' : 'preview');
    const mixTl = { clips: editorialMixClips(selected, windows, (shot) => linesForShot(live, shot)) };
    const mixSheet = compileMixCueSheet(mixTl, selected, live, cut);
    const mix = assembleMixPayload(mixSheet, { grade: cut.grade, colorMatch: cut.grade, interpolate: false });
    const runExport = async () => {
      setAssembleBusy(true);
      try {
        const httpClips = selected.map((shot, i) => {
          const run = shotRunOf(live, shot);
          const win = windows[i]!;
          return {
            code: studioShotCode(shot, pack),
            videoUrl: editorialSourceOf(run),
            seconds: win.seconds,
            usableStart: win.usableStart,
            usableEnd: win.usableEnd,
            voices: [] as { lineId: string; startSec: number; audioBase64: string; mime?: string }[],
            useVideoAudio: true,
            requireVoice: false,
          };
        });
        if (httpClips.every((c) => looksLikeVideoUrl(c.videoUrl))) {
          message.loading({
            content: `FFmpeg: ${kind === 'final' ? 'Final' : 'preview'} ${httpClips.length} shot · ~${totalSec.toFixed(1)}s…`,
            key: 'final-cut',
            duration: 0,
          });
          const mp4 = await assembleContentSeriesCut({
            fileStem: stem,
            aspect: assembleAspect,
            clips: httpClips,
            mix,
          });
          if (kind === 'watch') {
            openWatchClip(`${stem} preview`, mp4);
            message.success({ content: `Preview ${httpClips.length} shot · ~${totalSec.toFixed(1)}s`, key: 'final-cut' });
            return;
          }
          triggerDownload(mp4, `${stem}.mp4`);
          message.success({ content: `Đã ghép ${httpClips.length} shot → ${stem}.mp4`, key: 'final-cut' });
          return;
        }
        const clips = [];
        const missing: string[] = [];
        for (const shot of selected) {
          const blob = await loadFinalBlob(shot.id);
          if (!blob || blob.size < 80) {
            missing.push(studioShotCode(shot, pack));
            continue;
          }
          clips.push({
            shotId: shot.id,
            code: studioShotCode(shot, pack),
            capSec: windows.find((w) => w.shotId === shot.id)?.seconds ?? 5,
          });
        }
        if (missing.length) {
          throw new Error(`Thiếu file hoàn thiện: ${missing.join(', ')}. Mở shot → Hoàn thiện Shot.`);
        }
        message.loading({
          content: `Ghép ${clips.length} bản trên máy (không trim server)…`,
          key: 'final-cut',
          duration: 0,
        });
        const blob = await recordHardCutConcat({
          clips,
          videoOf: async (id) => {
            const row = await loadFinalBlob(id);
            if (!row || row.size < 80) throw new Error('Thiếu file hoàn thiện.');
            return row;
          },
          onProgress: (msg) => message.loading({ content: `Ghép ${msg}`, key: 'final-cut', duration: 0 }),
        });
        if (kind === 'watch') {
          openWatchClip(`${stem} preview`, blob);
          message.success({ content: `Preview ${clips.length} shot (máy)`, key: 'final-cut' });
          return;
        }
        triggerDownload(blob, `${stem}.webm`);
        message.success({ content: `Đã ghép ${clips.length} shot → ${stem}.webm`, key: 'final-cut' });
      } catch (e) {
        message.error({
          content: editorialAssembleError(e),
          key: 'final-cut',
        });
      } finally {
        setAssembleBusy(false);
      }
    };
    if (kind === 'watch') {
      void runExport();
      return;
    }
    modal.confirm({
      title:
        kind === 'final'
          ? `Xuất Final · ${selected.length} shot · ~${totalSec.toFixed(1)}s · 0 cr`
          : `Xuất preview · ${selected.length}/${pack.length} shot · ~${totalSec.toFixed(1)}s · 0 cr`,
      content:
        kind === 'final'
          ? 'Đủ tập. Trim + mix overlay. Không sửa take / lipsync / KF. Không gửi Runway / Gemini / Fal.'
          : 'Chỉ shot đã có video nguồn (lipsync / take). Không sửa production artifact. 0 AI.',
      okText: 'Tải file',
      cancelText: 'Hủy',
      onOk: () => runExport(),
    });
  };

  const prepareOfficialVoice = async (shotId: string) => {
    const buildId = productionOsBuildIdForShot(stateRef.current.buildId, shotId);
    if (!buildId) {
      message.warning(VOICE_PRODUCTION_OS_NOT_READY);
      return;
    }
    try {
      const take = await getAuthoritativeProductionVideo(buildId, shotId);
      const verdict = prepareOfficialVoiceIngress({ buildId, shotCode: shotId, take });
      if (take && !take.empty) setOsAuthoritativeByShot((m) => ({ ...m, [shotId]: take }));
      if (verdict.reason === VIDEO_TAKE_CURRENT_INPUT) return;
      if (!verdict.execute || !verdict.artifactId || !verdict.lineId || !verdict.voiceId) {
        message.warning(verdict.reason || VOICE_PRODUCTION_OS_NOT_READY);
        return;
      }
      const shot =
        episodeShots(stateRef.current).find((s) => s.id === shotId) ||
        (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
      const episodeCode = episodeCodeOf(stateRef.current.episode?.episode || stateRef.current.episode?.title) || 'EP01';
      const sceneCode = shot ? sceneIdOfShot(shot) || shot.scene || 'SC01' : 'SC01';
      const ingress = await materializeVoiceProductionIngress({
        buildId,
        shotCode: shotId,
        episodeCode,
        sceneCode,
      });
      const voiceDraft = voiceDraftForRequest(shotId, ingress.lineText || '');
      if (!voiceDraft.permitted) {
        message.warning(directorBlockerLines([voiceDraft.blocker]).join(' ') || voiceDraft.blocker);
        return;
      }
      const result = await createProductionRequest({
        buildId,
        episodeCode,
        sceneCode,
        shotCode: shotId,
        story: voiceDraft.text,
        actingBeat: futureDeskActing(shotId, shot?.actingBeat?.before?.action || ''),
        visualContract: shot?.visual || '',
        ...strategyClaim(shot),
        timing: String(verdict.durationSec || 10),
        aspectRatio: outputAspectOf(stateRef.current),
        durationSec: verdict.durationSec || 10,
        action: 'CREATE_VOICE',
        idempotencyKey: productionOsIdempotencyKey('CREATE_VOICE', buildId, shotId, verdict.artifactId),
        lineId: ingress.lineId,
        speakerCharacterId: ingress.speakerCharacterId,
        voiceId: ingress.voiceId,
        motionArtifactId: verdict.artifactId,
        authorizePaidNetwork: true,
        performanceSnapshotId: ingress.performanceSnapshotId,
        performanceHash: ingress.performanceHash,
      });
      if (result.action !== 'CREATE_VOICE') throw new Error('Request bị đổi action.');
      await createProductionRequest({
        buildId,
        episodeCode,
        sceneCode,
        shotCode: shotId,
        story: voiceDraft.text,
        actingBeat: futureDeskActing(shotId, shot?.actingBeat?.before?.action || ''),
        visualContract: shot?.visual || '',
        ...strategyClaim(shot),
        timing: String(verdict.durationSec || 10),
        aspectRatio: outputAspectOf(stateRef.current),
        durationSec: verdict.durationSec || 10,
        action: 'ACCEPT',
        idempotencyKey: productionOsIdempotencyKey('ACCEPT', buildId, shotId, result.artifactId),
        acceptArtifactId: result.artifactId,
      });
      const [nextState, actions, voice] = await Promise.all([
        getProductionShotState(buildId, shotId, strategyClaim(shot)),
        getProductionShotActions(buildId, shotId, strategyClaim(shot)),
        getProductionCurrentVoice(buildId, shotId, ingress.lineId),
      ]);
      setOsStateByShot((m) => ({ ...m, [shotId]: nextState }));
      setOsActionsByShot((m) => ({ ...m, [shotId]: actions }));
      noteStrategyCatalog(shotId, actions);
      if (voice && !voice.empty) setOsVoiceByShot((m) => ({ ...m, [shotId]: voice }));
      try {
        const blob = await fetchProductionVoiceMedia(buildId, shotId, ingress.lineId);
        const url = URL.createObjectURL(blob);
        setOsVoiceUrlByShot((m) => {
          const prior = m[shotId];
          if (prior) URL.revokeObjectURL(prior);
          return { ...m, [shotId]: url };
        });
      } catch {
        /* meta bind is enough */
      }
      message.success(`VOICE · ${result.artifactId.slice(0, 8)}`);
    } catch {
      message.warning(VOICE_PRODUCTION_OS_NOT_READY);
    }
  };

  const prepareOfficialLipsync = async (shotId: string) => {
    const buildId = productionOsBuildIdForShot(stateRef.current.buildId, shotId);
    if (!buildId) {
      message.warning(LIPSYNC_PRODUCTION_OS_NOT_READY);
      return;
    }
    setLipsyncBusy(shotId);
    try {
      const [take, voice] = await Promise.all([
        getAuthoritativeProductionVideo(buildId, shotId),
        getProductionCurrentVoice(buildId, shotId, OFFICIAL_SH02_LINE_ID),
      ]);
      const verdict = prepareOfficialLipsyncIngress({ buildId, shotCode: shotId, video: take, voice });
      if (take && !take.empty) setOsAuthoritativeByShot((m) => ({ ...m, [shotId]: take }));
      if (voice && !voice.empty) setOsVoiceByShot((m) => ({ ...m, [shotId]: voice }));
      if (verdict.reason === VIDEO_TAKE_CURRENT_INPUT) return;
      if (!verdict.execute || !verdict.videoArtifactId || !verdict.voiceArtifactId || !verdict.lineId) {
        message.warning(verdict.reason || LIPSYNC_PRODUCTION_OS_NOT_READY);
        return;
      }
      const shot =
        episodeShots(stateRef.current).find((s) => s.id === shotId) ||
        (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
      const episodeCode = episodeCodeOf(stateRef.current.episode?.episode || stateRef.current.episode?.title) || 'EP01';
      const sceneCode = shot ? sceneIdOfShot(shot) || shot.scene || 'SC01' : 'SC01';
      await materializeLipsyncProductionIngress({
        buildId,
        shotCode: shotId,
        episodeCode,
        sceneCode,
      });
      const lipsyncLine = voiceDraftForRequest(shotId, verdict.lineText || '');
      if (!lipsyncLine.permitted) {
        message.warning(directorBlockerLines([lipsyncLine.blocker]).join(' ') || lipsyncLine.blocker);
        return;
      }
      const knownSplit =
        (verdict.videoArtifactId || '').toLowerCase() === OFFICIAL_SH02_VIDEO_TAKE_ID
        && (verdict.voiceArtifactId || '').toLowerCase() === OFFICIAL_SH02_VOICE_ARTIFACT_ID;
      const clocks = knownSplit
        ? productionClockBlockers({
            directDialogue: true,
            audioSec: 2.534,
            pictureSec: 8,
            lipsyncSec: 2.5,
            mouthDuringSilence: true,
            mouthDuringSpeech: false,
          })
        : [];
      if (clocks.length) {
        message.warning(directorBlockerLines(clocks).join(' '));
        return;
      }
      const result = await createProductionRequest({
        buildId,
        episodeCode,
        sceneCode,
        shotCode: shotId,
        story: lipsyncLine.text,
        actingBeat: futureDeskActing(shotId, shot?.actingBeat?.before?.action || ''),
        visualContract: shot?.visual || '',
        ...strategyClaim(shot),
        timing: '10',
        aspectRatio: outputAspectOf(stateRef.current),
        durationSec: 10,
        action: 'CREATE_LIPSYNC',
        idempotencyKey: productionOsIdempotencyKey(
          'CREATE_LIPSYNC',
          buildId,
          shotId,
          `${verdict.videoArtifactId}${verdict.voiceArtifactId}`,
        ),
        lineId: verdict.lineId,
        speakerCharacterId: verdict.speakerCharacterId,
        motionArtifactId: verdict.videoArtifactId,
        voiceArtifactId: verdict.voiceArtifactId,
        authorizePaidNetwork: true,
      });
      if (result.action !== 'CREATE_LIPSYNC') throw new Error('Request bị đổi action.');
      await createProductionRequest({
        buildId,
        episodeCode,
        sceneCode,
        shotCode: shotId,
        story: lipsyncLine.text,
        actingBeat: futureDeskActing(shotId, shot?.actingBeat?.before?.action || ''),
        visualContract: shot?.visual || '',
        ...strategyClaim(shot),
        timing: '10',
        aspectRatio: outputAspectOf(stateRef.current),
        durationSec: 10,
        action: 'ACCEPT',
        idempotencyKey: productionOsIdempotencyKey('ACCEPT', buildId, shotId, result.artifactId),
        acceptArtifactId: result.artifactId,
      });
      const [nextState, actions, preview] = await Promise.all([
        getProductionShotState(buildId, shotId, strategyClaim(shot)),
        getProductionShotActions(buildId, shotId, strategyClaim(shot)),
        getProductionPreview(buildId, shotId).catch(() => null),
      ]);
      setOsStateByShot((m) => ({ ...m, [shotId]: nextState }));
      setOsActionsByShot((m) => ({ ...m, [shotId]: actions }));
      noteStrategyCatalog(shotId, actions);
      if (preview && !preview.empty && preview.artifactId) {
        try {
          if (!preview.byteSha256) throw new Error('preview hash missing');
          const blob = await fetchProductionPreviewMedia(buildId, shotId);
          if (!(await previewBytesMatch(blob, preview.byteSha256))) throw new Error('preview hash mismatch');
          const url = URL.createObjectURL(asPlayableVideoBlob(blob));
          setOsPreviewIdentityByShot((m) => ({
            ...m,
            [shotId]: osPreviewIdentityFromMeta(preview),
          }));
          setOsPreviewByShot((m) => {
            const prior = m[shotId];
            if (prior) URL.revokeObjectURL(prior);
            return { ...m, [shotId]: url };
          });
        } catch {
          /* HTTP contract still proves the artifact */
        }
      }
      message.success(`LIPSYNC · ${result.artifactId.slice(0, 8)}`);
    } catch {
      message.warning(LIPSYNC_PRODUCTION_OS_NOT_READY);
    } finally {
      setLipsyncBusy(undefined);
    }
  };

  const ensureShotVoice = async (shotId: string) => {
    message.warning(productionOsLegacyRefuseNote('CREATE_VOICE', 'ElevenLabs / IDB TTS'));
    if (productionOsLegacyPathClosed()) return;
    const shot = episodeShots(stateRef.current).find((s) => s.id === shotId);
    if (!shot) return;
    const lines = linesForShot(stateRef.current, shot);
    const alreadyReady = (lineId: string) => {
      const line = lines.find((l) => l.id === lineId);
      const voiceId = (line?.voiceId || '').trim();
      if (voiceId) {
        return Boolean(
          ttsBlobs.current.has(ttsLineKey(lineId, voiceId)) ||
            ttsBlobs.current.has(ttsTextKey(line?.text || '', voiceId)),
        );
      }
      const dur = Number(stateRef.current.voiceAssets?.[lineId]?.duration || 0);
      return dur > 0.2 && Boolean(ttsFilesRef.current[lineId]?.url || ttsBlobs.current.has(lineId));
    };
    try {
      await hydrateSessionTts(stateRef.current);
      for (const line of lines) {
        if (alreadyReady(line.id)) continue;
        const blob = await resolveLineAudio(line);
        if (blob) rememberLineAudio(line.id, blob);
      }
      const need = lines.filter((l) => !alreadyReady(l.id));
      if (need.length === 0) {
        commitVoicePreview();
        message.success('Đã nạp thoại đã lưu. Không tạo TTS mới.');
        return;
      }
      if (!keys.elevenLabs) {
        message.warning('Chưa có key ElevenLabs — Cấu hình AI.');
        return;
      }
      const chars = need.reduce((n, l) => n + (l.text || '').replace(/\s+/g, ' ').trim().length, 0);
      await new Promise<void>((resolve, reject) => {
        modal.confirm({
          title: need.length === 1 ? 'Tạo thoại câu này?' : `Tạo thoại ${need.length} câu?`,
          content: `ElevenLabs tính ~${chars} ký tự. Không trừ Runway. Câu đã có file thì giữ.`,
          okText: 'Tạo thoại',
          cancelText: 'Hủy',
          onOk: async () => {
            await ensureShotVoiceAssets({
              lines: need,
              loadCueAudio,
              alreadyReady: () => false,
            });
            commitVoicePreview();
            message.success(need.length === 1 ? 'Đã tạo thoại.' : `Đã tạo ${need.length} câu thoại.`);
            resolve();
          },
          onCancel: () => reject(new Error('Đã hủy tạo thoại.')),
        });
      });
    } catch (e) {
      if (e instanceof Error && e.message === 'Đã hủy tạo thoại.') return;
      message.error({ content: apiErrorMessage(e, 'Không nạp được thoại.'), key: 'famixa-desk-err' });
      throw e;
    }
  };

  void ensureShotVoice;

  const assembleOneShot = async (shotId: string) => {
    message.warning(productionOsLegacyRefuseNote('MIX_VIDEO', 'POST /content/series/assemble'));
    if (productionOsLegacyPathClosed()) return;
    const pack = productionShorts(stateRef.current);
    const shot = pack.find((s) => s.id === shotId);
    if (!shot) return;
    const run = shotRunOf(stateRef.current, shot);
    const localReady =
      isFalPlaybackSrc(run.lipsyncUrl) && (await proveLocalFalFile(run.lipsyncTaskId));
    const lipPlay = playableFalLipsyncSrc({ lipsyncUrl: run.lipsyncUrl, localFileReady: localReady });
    const fallbackMotionUrl = [resolveTakeUrl(run), run.takeUrl, run.previewUrl]
      .map((u) => (u || '').trim())
      .find((u) => u && !isFalPlaybackSrc(u));
    const mixFloor = dialogueFloorOf(stateRef.current, shot);
    const mixSource = resolveCurrentProductionVideoSource({
      run,
      shotId: shot.id,
      lipsyncPlayableUrl: lipPlay,
      fallbackMotionUrl,
      speechFloorSec: mixFloor,
    });
    const videoUrl = mixSource.url || '';
    if (!videoUrl) {
      message.warning('Chưa có video để hoàn thiện.');
      return;
    }
    const lines = linesForShot(stateRef.current, shot);
    const voices = [];
    let t = 0;
    for (const line of lines) {
      const blob = await resolveLineAudio(line);
      if (!blob) continue;
      voices.push({
        lineId: line.id,
        startSec: t,
        audioBase64: await blobToBase64(blob),
        mime: blob.type || 'audio/mpeg',
      });
      t += Number(stateRef.current.voiceAssets?.[line.id]?.duration || 1);
    }
    const spoken = lines.length > 0;
    const lipsynced = Boolean(lipPlay) && !mixSource.usesSpeechCoveringTake;
    if (spoken && !lipsynced && !lipsyncFalHasNoOutput(run.lipsyncError)) {
      message.error('Chưa khớp môi. Không ghép thoại đè lên video.');
      return;
    }
    const asked = editorialDurationOf(stateRef.current, shot) || shot.editSeconds || 5;
    const seconds = isVeoMotionTaskId(run.turboTaskId)
      ? veoAssembleDurationSec(Number(asked) || 5, dialogueFloorOf(stateRef.current, shot))
      : asked;
    setOrchMixId(shotId);
    setAssembleBusy(true);
    patchRun(shotId, {
      mixFrozenInput: freezeMixInput(stateRef.current, shot, {
        videoArtifactId: run.lipsyncUrl || run.takeUrl,
      }),
    });
    try {
      const fileName = `famixa-${studioShotCode(shot, pack).replace(/\s+/g, '')}.mp4`;
      const mp4 = await assembleContentSeriesCut(
        oneShotAssembleBody({
          shotCode: studioShotCode(shot, pack),
          seconds,
          videoUrl,
          spoken,
          lipsynced,
          voices,
          aspect: assembleAspect,
          grade: stateRef.current.smoothness?.grade !== false,
          interpolate: stateRef.current.smoothness?.interpolate === true,
        }),
      );
      const live = stateRef.current;
      const liveShot = episodeShots(live).find((s) => s.id === shot.id) ?? shot;
      const identity = finalArtifactIdentityOf(live, liveShot);
      await saveFinalBlob(shot.id, mp4, identity);
      const stamped = stampAssemble(shotRunOf(live, liveShot), identity.assembleFp, fileName);
      persistState({ ...live, runs: { ...live.runs, [shot.id]: stamped } });
      message.success('Đã hoàn thiện cảnh. Xem trên màn hình — không tải file.');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không ghép được cảnh.'));
    } finally {
      setAssembleBusy(false);
      setOrchMixId(undefined);
    }
  };

  const approveShotPicture = (shotId: string, liveStill?: string) => {
    const shot = episodeShots(stateRef.current).find((s) => s.id === shotId);
    if (!shot) {
      message.warning('Không tìm thấy cảnh để duyệt.');
      return;
    }
    void (async () => {
      const shown =
        (liveStill?.startsWith('data:image') ? liveStill : undefined) ||
        (shotRunOf(stateRef.current, shot).keyframeDataUrl?.startsWith('data:image')
          ? shotRunOf(stateRef.current, shot).keyframeDataUrl
          : undefined) ||
        kfPixelsOf(shotId);
      if (!shown?.startsWith('data:image')) {
        const cached = await loadKfPixels(shotId);
        const dataUrl = cached?.dataUrl?.startsWith('data:image') ? cached.dataUrl : kfPixelsOf(shotId);
        if (dataUrl?.startsWith('data:image')) {
          patchRun(shotId, { keyframeDataUrl: dataUrl, keyframeFileName: cached?.fileName });
        }
      } else if (shown !== shotRunOf(stateRef.current, shot).keyframeDataUrl) {
        patchRun(shotId, { keyframeDataUrl: shown });
      }
      const run = shotRunOf(stateRef.current, shot);
      if (existingScenePipelineStatus(run) === 'INVALID_REFERENCE_PIPELINE') {
        message.error('Ảnh này thuộc pipeline reference cũ. Không duyệt vào production.');
        return;
      }
      const decided = applyPictureApproveGuard(run, shotId, run.keyframeDataUrl);
      if (decided.kind === 'DUPLICATE_PIXEL') {
        message.error(PICTURE_DUPLICATE_COPY);
        return;
      }
      if (decided.kind !== 'APPROVE_NEW' || !decided.run.keyframeDataUrl?.startsWith('data:image')) {
        confirmCreateShotImage(shotId);
        return;
      }
      await saveKfPixels(shotId, decided.run.keyframeDataUrl, decided.run.keyframeFileName);
      patchRun(shotId, {
        ...decided.run,
        i2vRetry: 0,
        status:
          run.status === 'turbo_testing' || run.status === 'reviewed' || run.status === 'approved'
            ? run.status
            : 'keyframe_ready',
      });
      const after = shotRunOf(stateRef.current, shot);
      const snap = snapshotOfShot(stateRef.current, shot, ttsFilesRef.current);
      const exec = snap.execution;
      const take44 = [...(after.runwayAttempts ?? [])].reverse().find((row) => row.n === 44);
      console.info('[FAMIXA_APPROVE_BOUNDARY]', {
        shotId,
        currentPictureRevision: after.pictureRevisionId,
        currentPictureHash: (after.kfSourceHash || '').slice(0, 24),
        currentMotion: exec?.currentArtifacts.motion?.validity ?? 'NONE',
        visibleMotion: exec?.visibleArtifacts.motion?.validity ?? 'NONE',
        acceptedMotion: exec?.acceptedArtifacts.motion?.validity ?? 'NONE',
        nextAction: snap.nextAction,
        remake: after.motionNeedsRemake,
        take44Class: take44?.resultClass || take44?.status,
        take44Revision: (take44 as { frozenInput?: { pictureRevisionId?: string } } | undefined)?.frozenInput?.pictureRevisionId || '',
        take44Picture: (take44?.kf?.hash || '').slice(0, 24),
        pictureMatchTake44: Boolean(after.kfSourceHash && take44?.kf?.hash && after.kfSourceHash === take44.kf.hash),
      });
      message.success('Đã duyệt ảnh.');
    })();
  };

  const openWatchClip = (title: string, blob: Blob) => {
    const url = URL.createObjectURL(asPlayableVideoBlob(blob));
    setWatchClip((prev) => {
      if (prev?.url) URL.revokeObjectURL(prev.url);
      return { title, url };
    });
  };

  const watchShot = (shotId: string) => {
    void (async () => {
      const pack = productionShorts(stateRef.current);
      const shot = pack.find((s) => s.id === shotId) || episodeShots(stateRef.current).find((s) => s.id === shotId);
      const run = shot ? shotRunOf(stateRef.current, shot) : undefined;
      if (run?.motionNeedsRemake || run?.kfApproved === false) {
        message.warning('Video cũ không khớp ảnh mới. Bấm Tạo video → Confirm.');
        return;
      }
      if (!shot) {
        message.warning('Chưa có bản hoàn thiện. Take và Lip-sync không phải Final.');
        return;
      }
      const stored = await loadFinalArtifact(shotId);
      const blob = playableCurrentFinalBlob(stored, finalArtifactIdentityOf(stateRef.current, shot));
      if (blob) {
        openWatchClip(studioShotCode(shot, pack), blob);
        return;
      }
      if (run?.shotProduction?.assembleFp || stored.kind === 'legacy' || stored.kind === 'incomplete' || stored.kind === 'record') {
        message.warning('File hoàn thiện không còn trên máy này. Bấm Hoàn thiện để ghép lại. Không mở take/lipsync như Final.');
        return;
      }
      message.warning('Chưa có bản hoàn thiện. Take và Lip-sync không phải Final.');
    })();
  };

  const approveShotVideo = (shotId: string) => {
    const run = shotRunOf(stateRef.current, episodeShots(stateRef.current).find((s) => s.id === shotId) ?? { id: shotId, status: 'story_locked' } as FamixaSeriesShot);
    if (!hasProductionTakeLineage(run)) {
      message.warning('Chưa có video để duyệt.');
      return;
    }
    patchRun(shotId, { videoApproved: true, status: run.status === 'approved' ? run.status : 'reviewed' });
    message.success('Đã duyệt chuyển động.');
  };

  const lockMem = lockFromGraph(state, active);
  const prevLocked = previousLockedShot(state, active) ?? previousKeyframeShot(state, active);
  const prevRun = prevLocked ? shotRunOf(state, prevLocked) : undefined;
  const epCode = episodeCodeOf(state.episode?.episode || state.episode?.title) || 'EP01';
  const openScene = (id: string) => {
    setOpenShotId(id);
    setProdView('shot');
  };
  const focusStudioShot = (id: string) => {
    const pool = prodMode === 'director' ? deskShots : shots;
    const hit =
      pool.find((s) => s.id === id) ||
      (!looksLikeProductionShotCode(id)
        ? pool.find((s) => sceneIdOfShot(s) === id || (s.sceneId || '') === id)
        : undefined);
    const nextId = hit?.id || id;
    setStudioShotId(nextId);
    setStudioFocusTick((n) => n + 1);
  };
  const openShotStudio = (id: string) => {
    setProdTab('overview');
    setProdView('overview');
    focusStudioShot(id);
    if (prodMode === 'director') setDirectorBench(true);
  };

  useEffect(() => {
    const build = (searchParams.get('build') || '').trim();
    const shot = (searchParams.get('shot') || '').trim();
    const key = `${build}|${shot}`;
    if (!build || !shot || deepLinkDone.current === key) return;
    if (!resolveShotWorkspaceIdentity({ requestedBuildId: build, requestedShotId: shot }).ok) return;
    deepLinkDone.current = key;
    void openBuild(build).then(() => {
      openShotStudio(shot);
    });
  }, [searchParams]);

  useEffect(() => {
    const build = (state.buildId || '').trim();
    const shot = (studioShotId || '').trim();
    if (!build || !shot) return;
    if (!resolveShotWorkspaceIdentity({ requestedBuildId: build, requestedShotId: shot }).ok) return;
    if ((searchParams.get('build') || '').trim() === build && (searchParams.get('shot') || '').trim() === shot) return;
    setSearchParams({ build, shot }, { replace: true });
  }, [state.buildId, studioShotId]);
  const proposeScenes = () => {
    receiveStaffPack();
  };
  const runById = (id: string) => {
    const shot = shots.find((s) => s.id === id);
    return shot ? shotRunOf(state, shot) : shotRunOf(state, { id, status: 'story_locked' } as (typeof shots)[0]);
  };
  const hasVoiceFile = (id: string) => Boolean(ttsFiles[id]?.url || ttsBlobs.current.has(id));
  const staffPipeline = shots.map((s, index) => {
    const input = shotProductionInputOf(state, s, ttsFiles, hasVoiceFile);
    return {
      id: s.id,
      label: `Cảnh ${String(index + 1).padStart(2, '0')}`,
      input,
      preflight: videoProductionPreflight(input),
    };
  });
  const staffFinalize = episodeFinishReady(
    shots.map((s) => snapshotOfShot(state, s, ttsFiles, { hasVoiceFile })),
  );
  const voiceProfiles = (state.characters ?? []).map((ch) => {
    const profile = characterVoiceProfile(ch.id, ch.voiceId);
    return { characterId: ch.id, name: ch.name || ch.id, status: profile.status, voiceId: profile.voiceId };
  });
  const voiceCueRows = staffVoiceLines.map((line) => {
    const dur = Number(state.voiceAssets?.[line.shotId]?.duration || 0);
    const hit = listenCues.find((c) => c.text === line.text && (c.name || '') === line.speaker);
    const lineDur = Number((hit && state.voiceAssets?.[hit.id]?.duration) || dur || 0);
    return {
      shotLabel: line.label,
      speaker: line.speaker,
      text: line.text,
      ready: lineDur > 0.2,
      durationSec: lineDur || undefined,
    };
  });
  const voiceReadyAll = staffPipeline.every((r) => r.preflight.voiceReady);
  const lipsyncPending = staffPipeline.some((r) => r.preflight.blockers.includes('LIPSYNC_REQUIRED'));
  const sceneViews = buildSceneViews(shots, runById, state.scenes);
  const units = countScenesAndShots(sceneViews);
  const workflow = state.buildId ? progressById[state.buildId] : undefined;
  const sceneCharacterIds = [...new Set(sceneViews.flatMap((scene) => scene.characterIds))];
  const blockedCast = sceneCharacterIds
    .map((id) => library.find((row) => row.characterId.toUpperCase() === id.toUpperCase()))
    .find((row) => row && !row.canUse);
  const characterReady = workflow
    ? !workflow.blockingReason && workflow.stages.some((s) => s.id === 'cast' && s.done)
    : sceneCharacterIds.length > 0 &&
      sceneCharacterIds.every((id) => library.find((row) => row.characterId.toUpperCase() === id.toUpperCase())?.canUse);
  const productionProgress = {
    nodes:
      workflow?.stages.map((s) => ({ id: s.id as never, label: s.label, done: s.done, detail: s.detail })) ??
      deriveProductionProgress({
        scriptLocked: Boolean(state.scriptLocked),
        sceneCount: units.sceneCount,
        shotCount: units.shotCount,
        characterCount: sceneCharacterIds.length,
        characterReady,
        characterNeed: blockedCast ? `Cần hoàn thiện` : undefined,
        imageMade: 0,
        imageApproved: 0,
        videoMade: 0,
        videoApproved: 0,
        complete: 0,
      }).nodes,
    current: workflow?.currentStep ?? 'cast',
    finishReady: workflow ? workflow.finalization !== 'Chưa sẵn sàng' : false,
  };
  const nextProd = {
    step: workflow?.currentStep ?? 'cast',
    label: workflow?.nextAction || (blockedCast ? `Hoàn thiện bộ ảnh chuẩn cho ${blockedCast.displayName || blockedCast.name}` : 'Hoàn thiện bộ ảnh chuẩn'),
    tab: 'characters' as const,
  };
  const shotTotal = Math.max(workflow?.shotCount ?? 0, units.shotCount, shots.length);
  const imagesMade = workflow?.imageProgress ?? 0;
  const videosMade = workflow?.videoProgress ?? 0;
  const imagesReviewed = workflow?.imageApprovalProgress ?? 0;
  const videosReviewed = workflow?.videoApprovalProgress ?? 0;
  const seriesTrack = deriveSeriesTrack({
    scriptLocked: Boolean(state.scriptLocked),
    sceneCount: workflow?.sceneCount ?? units.sceneCount,
    shotCount: shotTotal,
    shotGraphLocked: Boolean(state.shotGraphLocked) || units.sceneCount > 0,
    characterLocked: characterReady,
    characterNeed: workflow?.blockingReason ? 'Cần hoàn thiện' : undefined,
    prepDone: workflow?.sceneCount ?? units.sceneCount,
    shotTotal,
    imagesMade,
    imagesReviewed,
    videosMade,
    videosReviewed,
    videosDone: !workflow || workflow.finalization === 'Chưa sẵn sàng' ? 0 : (workflow.shotCount ?? 0),
    voiceReady: voiceReadyAll,
    finalReady: staffFinalize.allowed,
  });
  const storyLine = workflow?.storyLine
    || episodeStoryLine({
      sceneCount: units.sceneCount,
      shotCount: units.shotCount,
      characterCount: sceneCharacterIds.length,
    });
  const buildCards = (workflow?.scenes ?? []).map((scene, index) => {
    const firstShot = shots.find(
      (s) => sceneIdOfShot(s) === scene.sceneId || (s.sceneId || '') === scene.sceneId,
    );
    return {
      id: scene.sceneId,
      firstShotId: firstShot?.id,
      scene: `Cảnh ${String(index + 1).padStart(2, '0')}`,
      title: scene.sceneName,
      mark: scene.imageProgress >= scene.shotCount && scene.shotCount > 0 ? '●' : '○',
      label: scene.nextAction || `${scene.shotCount} shot`,
    };
  });
  const saveSceneCharacters = (sceneId: string, ids: string[]) => {
    const known = [
      ...library.map((row) => row.characterId),
      ...(state.characters ?? []).map((row) => row.id),
    ];
    const resolved = resolveSceneCharacterRefs(ids, known);
    if (!resolved.ok) {
      message.warning(`${resolved.error}. ${resolved.hint}`);
      return;
    }
    persistState(applySceneCharacterRefs(stateRef.current, sceneId, resolved.characterIds));
  };

  return (
    <div>
      {seriesDesk === 'list' ? (
        <ContentFamixaSeriesDesk
          seriesTitle={deskTitle}
          builds={builds}
          progressById={progressById}
          busy={buildBusy}
          authRequired={catalogAuthRequired}
          catalogSettled={catalogSettled}
          onOpen={(id) => void openBuild(id)}
          onCreate={createVideoFromDesk}
          onDelete={removeBuild}
        />
      ) : (
      <>
      <ContentFamixaVisualModeBadge compact />
      {filmLed ? (
        <ContentFilmProductionDesk
          key={state.buildId || catalog}
          state={state}
          steps={filmSteps}
          episodeCode={epCode}
          seriesCode={catalog}
          onCommit={commitFilmGraph}
        />
      ) : null}
      <ContentFamixaSeriesWorkspace
        showFamixaCastLock={famixaCharacters}
        hideStoryForm={filmLed}
        episodeCode={epCode}
        episodeTitle={state.episode?.title || ''}
        seriesTitle={state.episode?.seriesTitle || episodeSeriesTitle}
        pipelineLine={seriesLine}
        episodeDesc={state.episode?.premise || ''}
        onBack={() => {
          setSeriesDesk('list');
          setProdView('overview');
          void refreshBuilds();
        }}
        mode={prodMode}
        tab={prodTab}
        onMode={(next) => {
          setProdMode(next);
          if (next === 'director') {
            setProdTab(directorTabOf(prodTab));
            setProdView('overview');
          }
        }}
        onTab={(next) => {
          setProdTab(next);
          setProdView('overview');
          if (next === 'images' || next === 'characters') {
            void loadUniverse();
            loadStudioPreflight();
          }
          if (next === 'voice' || next === 'characters') {
            void loadVoiceLibrary();
          }
          if (next === 'video') {
            const first = shots.find((s) => {
              const run = runById(s.id);
              return Boolean(run.keyframeDataUrl && (run.kfApproved || run.status === 'approved'))
                && !run.lipsyncUrl && !run.previewUrl && !run.takeUrl;
            });
            if (first) setVideoShotId(first.id);
            setVideoRestoreTick((n) => n + 1);
          }
        }}
        track={seriesTrack.nodes}
        currentTrack={seriesTrack.current}
        currentTask={
          prodMode === 'director'
            ? undefined
            : nextWorkCopy({
          scriptLocked: Boolean(state.scriptLocked),
          sceneCount: workflow?.sceneCount ?? units.sceneCount,
          characterLocked: characterReady,
          characterNeed: nextProd.label,
          sceneLabel: sceneViews[0]?.title || 'Cảnh',
          imagesPending: shotTotal > 0 && imagesMade < shotTotal,
          videosPending: shotTotal > 0 && videosMade < shotTotal,
          videosReviewPending: shotTotal > 0 && videosReviewed < videosMade,
          voicePending: shotTotal > 0 && videosReviewed >= shotTotal && !voiceReadyAll,
          lipsyncPending: shotTotal > 0 && videosReviewed >= shotTotal && lipsyncPending,
          finishReady: staffFinalize.allowed,
          done: staffFinalize.allowed,
        })
        }
        returnBanner={
          gateReturn && prodTab !== 'images'
            ? {
                label: `${gateReturn.sceneLabel} · xong việc trên tab này`,
                onBack: () => {
                  const back = gateReturn;
                  setGateReturn(undefined);
                  if (!back) return;
                  setProdTab('images');
                  setImageShotId(back.shotId);
                },
              }
            : undefined
        }
        sceneOpen={prodMode === 'staff' && prodView === 'shot'}
        overview={
          prodMode === 'director' ? (
            <ContentFamixaDirectorShortDesk
              title={`${epCode} · ${state.episode?.title?.trim() || 'Chưa đặt tên tập'}`}
              rows={deskShots.map((shot, index) => {
                const snap = snapshotOfShot(state, shot, ttsFiles, { hasVoiceFile });
                const cmd = nextShotProductionCommand(snap);
                const lanes = directorShotLaneMarks(snap);
                const attn = directorShotAttention(cmd, snap);
                const voiceSecOf = (id: string) => Number(state.voiceAssets?.[id]?.duration || 0);
                const win = resolveEditorialWindow(state, shot, editorialCutOf(state), voiceSecOf);
                return {
                  id: shot.id,
                  index,
                  picture: lanes.picture,
                  video: lanes.video,
                  voice: lanes.voice,
                  mark: attn.mark,
                  status: attn.label,
                  openLabel: directorShotOpenLabel(index),
                  finalReady: snap.finalReady,
                  sourceReady: editorialSourceReady(shotRunOf(state, shot)),
                  inSec: win.usableStart,
                  outSec: win.usableEnd,
                  keepSec: win.seconds,
                  capSec: win.cap,
                };
              })}
              allFinal={staffFinalize.allowed}
              readyCount={deskShots.filter((shot) => editorialSourceReady(shotRunOf(state, shot))).length}
              allSourceReady={deskShots.length > 0 && deskShots.every((shot) => editorialSourceReady(shotRunOf(state, shot)))}
              previewSec={estimateEditorialTotalSec(
                state,
                deskShots.filter((shot) => editorialSourceReady(shotRunOf(state, shot))),
                editorialCutOf(state),
                (id) => Number(state.voiceAssets?.[id]?.duration || 0),
              )}
              editorial={editorialCutOf(state)}
              assembleBusy={assembleBusy}
              benchOpen={directorBench}
              bench={
                <ContentFamixaShotProductionPanel
                  shots={deskShots}
                  state={state}
                  ttsFiles={ttsFiles}
                  busyShotId={stillBusy || turboBusy || lipsyncBusy}
                  motionBusyId={turboBusy}
                  lipsyncBusyId={lipsyncBusy}
                  mixBusyId={orchMixId}
                  episodeLabel={`${epCode} · ${state.episode?.title?.trim() || 'Chưa đặt tên tập'}`}
                  mode="director"
                  selectedShotId={studioShotId}
                  focusTick={studioFocusTick}
                  onSelectShot={setStudioShotId}
                  pictureGate={directorPictureHold}
                  onOpenVoice={() => setProdTab('characters')}
                  hasVoiceFile={hasVoiceFile}
                  readLive={() => ({ state: stateRef.current, ttsFiles: ttsFilesRef.current })}
                  onEnsureVoice={(shotId) => prepareOfficialVoice(shotId)}
                  onEnsurePicture={(shotId) => {
                    confirmCreateShotImage(shotId);
                  }}
                  onApprovePicture={approveShotPicture}
                  onApproveVideo={approveShotVideo}
                  onEnsureMotion={startShotMotion}
                  osPreviewUrl={studioShotId ? osPreviewByShot[studioShotId] : undefined}
                  osKeyframeUrl={studioShotId ? osKeyframeByShot[studioShotId] : undefined}
                  osPictureBlock={studioShotId ? osPictureBlockByShot[studioShotId] : undefined}
                  osPreviewIdentity={studioShotId ? osPreviewIdentityByShot[studioShotId] : undefined}
                  osAuthoritativeVideo={studioShotId ? osAuthoritativeByShot[studioShotId] : undefined}
                  osAuthoritativeVoice={studioShotId ? osVoiceByShot[studioShotId] : undefined}
                  osLipsyncArtifactId={studioShotId ? osStateByShot[studioShotId]?.lipsyncArtifactId || undefined : undefined}
                  osPaidStageLedger={studioShotId ? osStateByShot[studioShotId]?.paidStageLedger || undefined : undefined}
                  osVoiceUrl={studioShotId ? osVoiceUrlByShot[studioShotId] : undefined}
                  osActions={studioShotId ? osActionsByShot[studioShotId] : undefined}
                  onExecuteOsAction={executeProductionOsAction}
                  visualStill={studioShotId ? visualByShot[studioShotId] : undefined}
                  onVisualApprove={approveVisualStill}
                  onVisualVariant={requestVisualVariant}
                  onVisualLibrary={requestVisualLibrary}
                  onVisualPhoto={(shotId) => requestVisualLibrary(shotId, 'photo')}
                  visualBrandChoices={filmBrandCode ? undefined : visualBrandChoices}
                  visualBrandChoice={visualBrandChoice}
                  onVisualBrandChoice={filmBrandCode ? undefined : setVisualBrandChoice}
                  onPrepareOsVoice={prepareOfficialVoice}
                  onRetryCamera={retryMotionWithNewCamera}
                  onPickLipsyncSpeaker={applyLipsyncSpeakerPick}
                  onEnsureLipsync={(shotId, send) => {
                    if (shotId === OFFICIAL_SH02_SHOT_CODE) {
                      void prepareOfficialLipsync(shotId);
                      return;
                    }
                    if (send?.resume) {
                      void startLipsync([shotId], { resumeOnly: true });
                      return;
                    }
                    if (send?.forceNew) {
                      void startLipsync([shotId], { forceNew: true, remake: true });
                      return;
                    }
                    const shot =
                      productionShorts(stateRef.current).find((s) => s.id === shotId) ||
                      (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
                    const run = shotRunOf(stateRef.current, { id: shotId, status: 'story_locked' } as FamixaSeriesShot);
                    if (shouldResumeLipsync(run) || lipsyncResumeTaskId(run, normalizeLipsyncModel(stateRef.current.lipsyncModel))) {
                      void startLipsync([shotId], { resumeOnly: true });
                      return;
                    }
                    const opts = shot
                      ? studioLipsyncSendOpts(
                          snapshotOfShot(stateRef.current, shot, ttsFilesRef.current, {
                            hasVoiceFile: (id) => Boolean(ttsFilesRef.current[id]?.url || ttsBlobs.current.has(id)),
                          }),
                        )
                      : {};
                    void startLipsync([shotId], opts);
                  }}
                  onAttachLipsync={attachLipsync}
                  onAcceptExisting={acceptExistingOfficial}
                  engine={engine}
                  onEngine={persistEngine}
                  engineKeys={keys}
                  osVideoModel={studioShotId ? osVideoModelByShot[studioShotId] : undefined}
                  osDirectorTiming={studioShotId ? osDirectorTimingByShot[studioShotId] : undefined}
                  onDirectorTiming={persistDirectorTiming}
                  directorTimingBusy={directorTimingBusy}
                  directorSelectionBusy={directorSelectionBusy}
                  lipsyncModel={normalizeLipsyncModel(state.lipsyncModel)}
                  onLipsyncModel={(lipsyncModel) => persistState({ ...stateRef.current, lipsyncModel })}
                  onEnsureMix={(shotId) => void assembleOneShot(shotId)}
                  onWatch={watchShot}
                  onQa={(shotId, next) => {
                    const live = shotRunOf(stateRef.current, { id: shotId, status: 'story_locked' } as FamixaSeriesShot);
                    patchRun(shotId, { shotQa: mergeCanonicalShotQa(live.shotQa, next) });
                  }}
                  onActingBeat={(shotId, actingBeat) => persistActingBeat(shotId, actingBeat)}
                />
              }
              onOpenShot={(id) => {
                setStudioShotId(id);
                setDirectorBench(true);
                setStudioFocusTick((n) => n + 1);
              }}
              onBack={() => setDirectorBench(false)}
              onEditorialMode={(mode) => persistState(patchEditorialCut(stateRef.current, { mode }))}
              onEditorialMix={(prefs) => persistState(patchEditorialCut(stateRef.current, prefs))}
              onEditorialTrim={(shotId, trim) => persistState(patchEditorialShotTrim(stateRef.current, shotId, trim))}
              onResetTrim={(shotId) => persistState(patchEditorialShotTrim(stateRef.current, shotId, null))}
              onPreviewWatch={() => exportEditorialCut('watch')}
              onPreviewExport={() => exportEditorialCut('preview')}
              onExportFinal={() => exportEditorialCut('final')}
              onExportSpeech={() => exportEditorialCut('final', 'SPEECH_CUT')}
            />
          ) : (
          <>
            <ContentFamixaProductionOverview
              embedded
              episodeCode={epCode}
              episodeTitle={state.episode?.title || ''}
              scriptLocked={Boolean(state.scriptLocked)}
              sceneCount={workflow?.sceneCount ?? units.sceneCount}
              shotCount={workflow?.shotCount ?? units.shotCount}
              characterCount={workflow?.characterCount ?? sceneCharacterIds.length}
              storyLine={storyLine}
              shotGraphLocked={Boolean(state.shotGraphLocked) || units.sceneCount > 0}
              onOpenShot={openScene}
              onFocusShot={focusStudioShot}
              onFinish={() => setProdTab('publish')}
              onOpenTab={(tab) => setProdTab(tab)}
              buildScope={{
                characterLocked: characterReady,
                cards: buildCards,
                imagesMade: workflow?.imageProgress ?? 0,
                videosMade: workflow?.videoProgress ?? 0,
                progress: productionProgress.nodes,
                stageLine: workflow ? `${workflow.completedStages}/${workflow.totalStages} bước` : undefined,
                nextAction: workflow?.nextAction,
              }}
            />
            <ContentFamixaShotProductionPanel
              shots={shots}
              state={state}
              ttsFiles={ttsFiles}
              busyShotId={stillBusy || turboBusy || lipsyncBusy}
              motionBusyId={turboBusy}
              lipsyncBusyId={lipsyncBusy}
              mixBusyId={orchMixId}
              episodeLabel={`${epCode} · ${state.episode?.title?.trim() || 'Chưa đặt tên tập'}`}
              mode={prodMode}
              selectedShotId={studioShotId}
              focusTick={studioFocusTick}
              onSelectShot={setStudioShotId}
              pictureGate={directorPictureHold}
              onOpenVoice={() => setProdTab('voice')}
              hasVoiceFile={hasVoiceFile}
              readLive={() => ({ state: stateRef.current, ttsFiles: ttsFilesRef.current })}
              onEnsureVoice={(shotId) => prepareOfficialVoice(shotId)}
              onEnsurePicture={(shotId) => {
                confirmCreateShotImage(shotId);
              }}
              onApprovePicture={approveShotPicture}
              onApproveVideo={approveShotVideo}
              onEnsureMotion={startShotMotion}
              osPreviewUrl={studioShotId ? osPreviewByShot[studioShotId] : undefined}
              osKeyframeUrl={studioShotId ? osKeyframeByShot[studioShotId] : undefined}
              osPictureBlock={studioShotId ? osPictureBlockByShot[studioShotId] : undefined}
              osPreviewIdentity={studioShotId ? osPreviewIdentityByShot[studioShotId] : undefined}
              osAuthoritativeVideo={studioShotId ? osAuthoritativeByShot[studioShotId] : undefined}
              osAuthoritativeVoice={studioShotId ? osVoiceByShot[studioShotId] : undefined}
              osLipsyncArtifactId={studioShotId ? osStateByShot[studioShotId]?.lipsyncArtifactId || undefined : undefined}
              osPaidStageLedger={studioShotId ? osStateByShot[studioShotId]?.paidStageLedger || undefined : undefined}
              osVoiceUrl={studioShotId ? osVoiceUrlByShot[studioShotId] : undefined}
              osActions={studioShotId ? osActionsByShot[studioShotId] : undefined}
              onExecuteOsAction={executeProductionOsAction}
              visualStill={studioShotId ? visualByShot[studioShotId] : undefined}
              onVisualApprove={approveVisualStill}
              onVisualVariant={requestVisualVariant}
              onVisualLibrary={requestVisualLibrary}
              onVisualPhoto={(shotId) => requestVisualLibrary(shotId, 'photo')}
              visualBrandChoices={filmBrandCode ? undefined : visualBrandChoices}
              visualBrandChoice={visualBrandChoice}
              onVisualBrandChoice={filmBrandCode ? undefined : setVisualBrandChoice}
              onPrepareOsVoice={prepareOfficialVoice}
              onRetryCamera={retryMotionWithNewCamera}
              onPickLipsyncSpeaker={applyLipsyncSpeakerPick}
              onEnsureLipsync={(shotId, send) => {
                if (shotId === OFFICIAL_SH02_SHOT_CODE) {
                  void prepareOfficialLipsync(shotId);
                  return;
                }
                if (send?.resume) {
                  void startLipsync([shotId], { resumeOnly: true });
                  return;
                }
                if (send?.forceNew) {
                  void startLipsync([shotId], { forceNew: true, remake: true });
                  return;
                }
                const shot =
                  productionShorts(stateRef.current).find((s) => s.id === shotId) ||
                  (stateRef.current.episode?.shots ?? []).find((s) => s.id === shotId);
                const run = shotRunOf(stateRef.current, { id: shotId, status: 'story_locked' } as FamixaSeriesShot);
                if (shouldResumeLipsync(run) || lipsyncResumeTaskId(run, normalizeLipsyncModel(stateRef.current.lipsyncModel))) {
                  void startLipsync([shotId], { resumeOnly: true });
                  return;
                }
                const opts = shot
                  ? studioLipsyncSendOpts(
                      snapshotOfShot(stateRef.current, shot, ttsFilesRef.current, {
                        hasVoiceFile: (id) => Boolean(ttsFilesRef.current[id]?.url || ttsBlobs.current.has(id)),
                      }),
                    )
                  : {};
                void startLipsync([shotId], opts);
              }}
              onAttachLipsync={attachLipsync}
              onAcceptExisting={acceptExistingOfficial}
              engine={engine}
              onEngine={persistEngine}
              engineKeys={keys}
              osVideoModel={studioShotId ? osVideoModelByShot[studioShotId] : undefined}
              osDirectorTiming={studioShotId ? osDirectorTimingByShot[studioShotId] : undefined}
              onDirectorTiming={persistDirectorTiming}
              directorTimingBusy={directorTimingBusy}
              directorSelectionBusy={directorSelectionBusy}
              lipsyncModel={normalizeLipsyncModel(state.lipsyncModel)}
              onLipsyncModel={(lipsyncModel) => persistState({ ...stateRef.current, lipsyncModel })}
              onEnsureMix={(shotId) => void assembleOneShot(shotId)}
              onWatch={watchShot}
              onQa={(shotId, next) => {
                const live = shotRunOf(stateRef.current, { id: shotId, status: 'story_locked' } as FamixaSeriesShot);
                patchRun(shotId, { shotQa: mergeCanonicalShotQa(live.shotQa, next) });
              }}
              onActingBeat={(shotId, actingBeat) => persistActingBeat(shotId, actingBeat)}
              onCoverage={(shotId, coverage) => {
                const live = stateRef.current;
                if (!live.episode) return;
                persistState({
                  ...live,
                  episode: {
                    ...live.episode,
                    shots: live.episode.shots.map((s) => (s.id === shotId ? { ...s, coverage } : s)),
                  },
                });
              }}
              onEditorial={(shotId, timing) => {
                const live = stateRef.current;
                if (!live.episode) return;
                persistState({
                  ...live,
                  episode: {
                    ...live.episode,
                    shots: live.episode.shots.map((s) =>
                      s.id === shotId && s.timing ? { ...s, timing: { ...s.timing, ...timing } } : s,
                    ),
                  },
                });
              }}
              onSmoothness={(smoothness) => persistState({ ...stateRef.current, smoothness })}
            />
          </>
          )
        }
        script={
          filmLed ? null : prodMode === 'director' ? (
          <ContentFamixaDirectorStoryDesk
            shotOrder={shots.map((s, i) => ({
              id: s.id,
              label: `${String(i + 1).padStart(2, '0')} · ${s.story || s.visual || s.id}`,
            }))}
            aspect={assembleAspect === '9:16' ? '9:16' : '16:9'}
            onAspect={(next) => persistState({ ...stateRef.current, outputAspect: next })}
            storyAction={nextDirectorStoryAction({
              body: state.packDraft || state.scriptDraft || '',
              sceneCount: state.scenes?.length ?? 0,
              shotCount: shots.length,
              storyReviewed: Boolean(state.storyReviewed),
              needsInheritReview: needsInheritanceReview(state) && !state.scriptLocked,
              scriptLocked: Boolean(state.scriptLocked),
              shotGraphLocked: state.shotGraphLocked !== false && shots.length > 0,
              aspectOk: assembleAspect === '16:9' || assembleAspect === '9:16',
            })}
            onPrimary={lockDirectorStory}
            script={
          <ContentFamixaScriptDesk
            hideActions
            title={state.episode?.title || ''}
            goal={state.episode?.premise || ''}
            moral={state.episode?.moral || ''}
            cta={state.episode?.ctaRule || ''}
            body={state.packDraft || state.scriptDraft || ''}
            dialogue={voiceScript.lines.map((l) => l.text).filter(Boolean).join('\n')}
            locked={Boolean(state.scriptLocked)}
            onTitle={(title) =>
              persistState({
                ...stateRef.current,
                episode: stateRef.current.episode
                  ? { ...stateRef.current.episode, title }
                  : {
                      seriesCode: catalog,
                      seriesTitle: episodeSeriesTitle,
                      episode: epCode,
                      title,
                      premise: '',
                      moral: '',
                      ctaRule: '',
                      shots: [],
                    },
              })
            }
            onGoal={(premise) =>
              persistState({
                ...stateRef.current,
                episode: stateRef.current.episode
                  ? { ...stateRef.current.episode, premise }
                  : {
                      seriesCode: catalog,
                      seriesTitle: episodeSeriesTitle,
                      episode: epCode,
                      title: '',
                      premise,
                      moral: '',
                      ctaRule: '',
                      shots: [],
                    },
              })
            }
            onBody={(packDraft) => persistState({ ...stateRef.current, packDraft, scriptDraft: packDraft })}
            onBodyCommit={() => flushServerSaveRef.current()}
            onSave={() => persistState(stateRef.current)}
            onConfirm={confirmStaffScript}
            onProposeScenes={proposeScenes}
            onReceivePack={receiveStaffPack}
            onGoScenes={() => setProdTab('scenes')}
            proposeNote={proposeNote}
            needsStoryReview={(state.scenes?.length ?? 0) > 0 && !state.storyReviewed && !state.scriptLocked}
            onReviewStory={() => persistState({ ...state, storyReviewed: true })}
            sceneLabels={(state.scenes ?? []).map((sc) =>
              [sc.id, sc.title].filter(Boolean).join(' — '),
            )}
            needsInheritReview={needsInheritanceReview(state) && !state.scriptLocked}
            inheritFrom={inheritanceReview(state).fromEpisode || state.storyMemory?.inheritFromEpisode}
            inheritTo={inheritanceReview(state).toEpisode || epCode}
            inheritThreads={inheritanceReview(state).openThreads.map((t) =>
              [t.id, t.name, t.createdEpisode ? `tạo ${t.createdEpisode}` : ''].filter(Boolean).join(' — '),
            )}
            onReviewInherit={() => {
              const live = stateRef.current;
              const mem = live.storyMemory;
              if (!mem) return;
              persistState({ ...live, storyMemory: { ...mem, inheritReviewed: true } });
            }}
          />
            }
          />
          ) : (
          <ContentFamixaScriptDesk
            title={state.episode?.title || ''}
            goal={state.episode?.premise || ''}
            moral={state.episode?.moral || ''}
            cta={state.episode?.ctaRule || ''}
            body={state.packDraft || state.scriptDraft || ''}
            dialogue={voiceScript.lines.map((l) => l.text).filter(Boolean).join('\n')}
            locked={Boolean(state.scriptLocked)}
            onTitle={(title) =>
              persistState({
                ...stateRef.current,
                episode: stateRef.current.episode
                  ? { ...stateRef.current.episode, title }
                  : {
                      seriesCode: catalog,
                      seriesTitle: episodeSeriesTitle,
                      episode: epCode,
                      title,
                      premise: '',
                      moral: '',
                      ctaRule: '',
                      shots: [],
                    },
              })
            }
            onGoal={(premise) =>
              persistState({
                ...stateRef.current,
                episode: stateRef.current.episode
                  ? { ...stateRef.current.episode, premise }
                  : {
                      seriesCode: catalog,
                      seriesTitle: episodeSeriesTitle,
                      episode: epCode,
                      title: '',
                      premise,
                      moral: '',
                      ctaRule: '',
                      shots: [],
                    },
              })
            }
            onBody={(packDraft) => persistState({ ...stateRef.current, packDraft, scriptDraft: packDraft })}
            onBodyCommit={() => flushServerSaveRef.current()}
            onSave={() => persistState(stateRef.current)}
            onConfirm={confirmStaffScript}
            onProposeScenes={proposeScenes}
            onReceivePack={receiveStaffPack}
            onGoScenes={() => setProdTab('scenes')}
            proposeNote={proposeNote}
            needsStoryReview={(state.scenes?.length ?? 0) > 0 && !state.storyReviewed && !state.scriptLocked}
            onReviewStory={() => persistState({ ...state, storyReviewed: true })}
            sceneLabels={(state.scenes ?? []).map((sc) =>
              [sc.id, sc.title].filter(Boolean).join(' — '),
            )}
            needsInheritReview={needsInheritanceReview(state) && !state.scriptLocked}
            inheritFrom={inheritanceReview(state).fromEpisode || state.storyMemory?.inheritFromEpisode}
            inheritTo={inheritanceReview(state).toEpisode || epCode}
            inheritThreads={inheritanceReview(state).openThreads.map((t) =>
              [t.id, t.name, t.createdEpisode ? `tạo ${t.createdEpisode}` : ''].filter(Boolean).join(' — '),
            )}
            onReviewInherit={() => {
              const live = stateRef.current;
              const mem = live.storyMemory;
              if (!mem) return;
              persistState({ ...live, storyMemory: { ...mem, inheritReviewed: true } });
            }}
          />
          )
        }
        scenes={
          <ContentFamixaBuildSceneList
            useFamixaSet={famixaCharacters}
            characterScope={characterScope}
            shots={shots}
            runOf={runById}
            scenes={state.scenes}
            characters={state.characters}
            scriptLocked={Boolean(state.scriptLocked)}
            mode={prodMode}
            onEditScript={() => setProdTab('script')}
            onSaveCharacters={saveSceneCharacters}
            creating={Boolean(stillBusy)}
            onCreateImage={confirmCreateShotImage}
          />
        }
        characters={
          famixaCharacters ? (
          <ContentFamixaCharacterBoard
            scope={characterScope}
            mode={prodMode}
            shots={shots}
            onBuildEpisode={() => setProdTab('script')}
            focusCharacterId={studioFocus?.characterId}
            focusPanel={studioFocus?.panel}
            returnToScene={studioFocus?.returnTo}
            onReturnToScene={studioFocus?.returnTo ? returnToSceneFromStudio : undefined}
            voices={voices.map((v) => ({ value: v.voiceId, label: v.name || v.voiceId, previewUrl: v.previewUrl }))}
            voiceById={Object.fromEntries([
              ...(state.characters ?? [])
                .filter((c) => c.voiceId)
                .map((c) => [c.id, c.voiceId as string] as const),
              ...state.roles
                .filter((r) => r.characterId && r.voiceId)
                .map((r) => [r.characterId as string, r.voiceId as string] as const),
            ])}
            onAssignVoice={(characterId, voiceId, voiceName) => {
              const role = state.roles.find((r) => r.characterId === characterId);
              if (role) {
                patchRole(role.id, { voiceId, voiceName });
                return;
              }
              persistState(setCharacterVoice(state, characterId, { voiceId, voiceName }));
            }}
          />
          ) : (
            <section className="fx-desk" data-character-desk={characterDesk}>
              <h2>Người</h2>
              <p className="fx-desk__lead">
                {characterDesk === 'unavailable' ? 'Chưa khả dụng.' : 'Chưa cấu hình.'} Phim này không dùng Character Studio.
              </p>
            </section>
          )
        }
        voice={
          <ContentFamixaBuildVoiceBoard
            profiles={voiceProfiles}
            cues={voiceCueRows}
            voiceOptions={voices.map((v) => ({ value: v.voiceId, label: v.name || v.voiceId, previewUrl: v.previewUrl }))}
            locked={Boolean(state.voiceLocked)}
            canLock={canLockVoice(state)}
            lockHint={voiceLockHint}
            ttsBusy={ttsBusy}
            onAssignVoice={(characterId, voiceId, voiceName) => {
              const role = state.roles.find((r) => r.characterId === characterId);
              if (role) {
                patchRole(role.id, { voiceId, voiceName });
                return;
              }
              persistState(setCharacterVoice(state, characterId, { voiceId, voiceName }));
            }}
            onLockVoice={() => lockVoice()}
            onLockWithoutTts={() => lockVoice({ skipRegen: true })}
            onCreateVoice={playWholeScript}
          />
        }
        images={
          <>
            {prodMode === 'staff' ? <ContentKitVideoImageGenerationExecutionCard /> : null}
            <p className="fx-desk__note">
              Lưới Cảnh 01–03 là ảnh của tập này. Ảnh Shot 01 engine (nếu có) nằm riêng — không thay thế Cảnh.
            </p>
            <ContentFamixaBuildImageBoard
              shots={shots}
              runOf={runById}
              creating={Boolean(stillBusy)}
              restoreShotId={imageShotId}
              preflightOf={preflightForShot}
              onPreflightAction={handleSceneImageAction}
              onCreate={(shotId) => {
                if (!preflightForShot(shotId).allowed) {
                  setImageShotId(shotId);
                  return;
                }
                generateSceneKf([shotId]);
              }}
              onApprove={(shotId) => {
                approveShotPicture(shotId);
              }}
              onReject={(shotId, reason) => {
                patchRun(shotId, {
                  kfApproved: false,
                  notes: reason || undefined,
                });
                message.info('Đã ghi yêu cầu chỉnh sửa. Máy không tự tạo lại ảnh.');
              }}
            />
          </>
        }
        video={
          <>
            <ContentFamixaBuildVideoBoard
              shots={shots}
              runOf={runById}
              creating={Boolean(turboBusy)}
              restoreShotId={videoShotId}
              restoreNonce={videoRestoreTick}
              onCreate={(shotId) => {
                setVideoShotId(shotId);
                startSceneTurbo([shotId]);
              }}
              onRetry={(shotId) => {
                const run = runById(shotId);
                patchRun(shotId, { i2vRetry: (run.i2vRetry ?? 0) + 1 });
                setVideoShotId(shotId);
                startSceneTurbo([shotId]);
              }}
              onApprove={(shotId) => {
                const run = runById(shotId);
                if (!run.lipsyncUrl && !run.previewUrl && !run.takeUrl) {
                  message.warning('Chưa có video để duyệt.');
                  return;
                }
                patchRun(shotId, { videoApproved: true, status: 'approved' });
                message.success('Đã duyệt video.');
              }}
              onReject={(shotId, reason) => {
                patchRun(shotId, { videoApproved: false, notes: reason || undefined });
                message.info('Đã ghi yêu cầu chỉnh sửa. Máy không tự tạo lại video.');
              }}
              onProduceShot={openShotStudio}
            />
            {prodMode === 'staff' ? (
              <>
                <ContentFamixaSharedProductionNote
                  onOpen={() => {
                    if (prodLive.firstShotId) openScene(prodLive.firstShotId);
                  }}
                />
                <ContentFamixaVideoBoard onOpen={openScene} />
                <ContentFamixaImageBoard onOpen={openScene} />
                <ContentFamixaSceneList onOpen={openScene} />
              </>
            ) : (
              <p className="fx-desk__note">Cảnh production Minh (Video Engine) không thuộc bản dựng này. Mở Kỹ thuật nếu cần xem.</p>
            )}
          </>
        }
        finish={
          <ContentFamixaBuildFinishBoard
            episodeCode={epCode}
            episodeTitle={state.episode?.title || ''}
            shots={shots}
            runOf={runById}
            voiceLines={staffVoiceLines}
            onMixVoice={mixStaffEpisode}
            mixing={assembleBusy}
            pipeline={staffPipeline}
            canFinalize={staffFinalize.allowed}
            onCreateLipsync={(shotId) => void startLipsync([shotId])}
            onBack={() => {
              setSeriesDesk('list');
              setProdView('overview');
              void refreshBuilds();
            }}
            onPublish={() => {
              if (!staffFinalize.allowed) {
                message.warning(staffFinalize.blockers[0] || 'Chưa Final — chờ Lip-sync.');
                return;
              }
              setProdTab('publish');
            }}
          />
        }
        publish={
          <ContentFamixaBuildPublishBoard
            episodeCode={epCode}
            episodeTitle={state.episode?.title || ''}
            shots={shots}
            runOf={runById}
            voiceLines={staffVoiceLines}
            onMixVoice={mixStaffEpisode}
            mixing={assembleBusy}
            canFinalize={staffFinalize.allowed}
          />
        }
        sceneWorkspace={
          prodView === 'shot' ? (
            <ContentKitVideoDirectorProductionWorkspace
              shotId={openShotId}
              showTechnical={prodMode === 'staff'}
              onBack={() => setProdView('overview')}
              onContinue={() => {
                setProdView('overview');
                setProdTab('finish');
              }}
            >
              {null}
            </ContentKitVideoDirectorProductionWorkspace>
          ) : null
        }
        lookBuild={
          famixaCharacters ? (
          <>
            <p className="fx-desk__note">Đường khóa mặt Minh. Đã LOCK. Không sửa Master / DNA / PRP từ đây.</p>
            <ContentKitVideoVisualSystemCard />
            <ContentFamixaMinhDnaCard />
            <ContentKitVideoMasterReferenceCard />
            <ContentKitVideoIdentityTestCard />
            <ContentKitVideoIdentityStressCard />
            <ContentKitVideoMasterReviewCard />
          </>
          ) : (
            <p className="fx-desk__note">Chưa cấu hình bộ nhân vật cho phim này.</p>
          )
        }
        productionSystem={
          <>
          <ContentKitVideoEngineCard />
          {famixaCharacters ? (
            <>
          <ContentKitVideoCharacterDnaCard />
          <ContentKitVideoProductionReferencePackCard />
          <ContentKitVideoIdentityGovernanceCard />
            </>
          ) : null}
          <ContentKitVideoProductionShotCard />
          <ContentKitVideoProductionShotContractCard />
          <ContentKitVideoProductionPromptCompilerCard />
          <ContentKitVideoImageGenerationContractCard />
          <ContentKitVideoImageGenerationExecutionCard />
          <ContentKitVideoImageDirectorReviewCard />
          <ContentKitVideoProductionVideoContractCard />
          <ContentKitVideoVideoGenerationExecutionCard />
          <ContentKitVideoGoldenShotCard />
      <ContentFamixaStudioView
        shots={shots}
        episode={ep}
        active={active}
        run={run}
        lock={lockMem}
        pilot={state}
        videoContext={videoContext}
        prevLocked={prevLocked}
        prevRun={prevRun}
        unlocked={unlocked}
        sceneLocked={state.sceneLocked}
        scriptLocked={state.scriptLocked}
        shortsReady={canWorkScene(state)}
        shortsCount={shorts.length}
        shortsLockedCount={approvedShortCount(state)}
        sessionSrc={active ? sessionClips[active.id] : undefined}
        turboBusy={turboBusy}
        generateKfBusy={stillBusy === active?.id}
        kitCredits={credits}
        runwaySpent={spentOnRunway}
        expectedCost={generateCost(engine, active?.seconds ?? 5).credits}
        costLabel={generateCost(engine, active?.seconds ?? 5).label}
        engine={engine}
        onEngine={persistEngine}
        keys={keys}
        osVideoModel={(studioShotId && osVideoModelByShot[studioShotId]) || (active?.id ? osVideoModelByShot[active.id] : undefined)}
        osDirectorTiming={(studioShotId && osDirectorTimingByShot[studioShotId]) || (active?.id ? osDirectorTimingByShot[active.id] : undefined)}
        onDirectorTiming={persistDirectorTiming}
        directorTimingBusy={directorTimingBusy}
        directorSelectionBusy={directorSelectionBusy}
        voiceProvider={voiceProvider}
        onVoiceProvider={persistVoice}
        statusOf={(s) => shotRunOf(state, s).status}
        runOf={(s) => shotRunOf(state, s)}
        actionOf={(s) => effectiveShotAction(s, shotRunOf(state, s))}
        onSelectShot={selectShot}
        onLockShotGraph={lockShotGraph}
        onRemoveShots={removeShots}
        onAction={(shotAction) => {
          if (active) patchRun(active.id, { shotAction });
        }}
        onPickKeyframe={(file) => {
          if (active) void onKeyframeFile(active.id, file);
        }}
        onGenerateKeyframe={() => {
          if (!active) return;
          void generateKfFromCanon(stillArgsFor(active, productionShorts(stateRef.current)));
        }}
        onInheritKeyframe={() => {
          if (!active) return;
          const src = previousKeyframeShot(state, active);
          const srcRun = src ? shotRunOf(state, src) : undefined;
          if (!src || !srcRun?.keyframeDataUrl) {
            message.warning('Chưa có ảnh cảnh trên shot nào. Gắn KF01 (bàn ăn) trên shot đầu, không dùng ảnh mặt CHAR.');
            return;
          }
          patchRun(active.id, {
            keyframeDataUrl: srcRun.keyframeDataUrl,
            keyframeFileName: srcRun.keyframeFileName,
            keyframePath: srcRun.keyframePath,
            keyframeInheritedFrom: src.id,
          });
        }}
        onApproveKeyframe={() => {
          markKeyframe();
        }}
        onCreateVideo={startTurbo}
        onCreateSceneVideo={startSceneTurbo}
        onAbDiagnostic={startAbDiagnostic}
        onLipsync={startLipsync}
        onLipsyncPrefs={(prefs) => persistState({ ...stateRef.current, ...prefs })}
        onMixPrefs={(prefs) =>
          persistState({
            ...stateRef.current,
            mixPrefs: { ...normalizeMixPrefs(stateRef.current.mixPrefs), ...prefs },
          })
        }
        onAttachLipsync={attachLipsync}
        lipsyncBusy={lipsyncBusy}
        sceneBatchLabel={sceneBatchLabel}
        sceneBatchDisabled={
          !hasVideoKey ||
          sceneVideo.ready.length === 0 ||
          !canWorkV2Scene(state) ||
          Boolean(state.sceneLocked) ||
          Boolean(turboBusy) ||
          runwayQuietRemainMin(runwayQuietUntil) > 0
        }
        batchPlan={sceneKfPlan}
        batchSceneShots={prodQueue}
        batchKfNew={sceneKfNew.length}
        batchKfReuse={sceneKfReuse}
        onGenerateSceneKf={generateSceneKf}
        onPickSceneKeyframe={(file, shotId) => {
          const pack = productionShorts(stateRef.current);
          const plan = buildSceneKfPlan(stateRef.current, pack);
          const target =
            (shotId ? pack.find((s) => s.id === shotId) : undefined) ||
            firstNewKfShot(pack, plan) ||
            active;
          if (!target) {
            message.warning('Chưa có Short để gắn hình.');
            return;
          }
          void (async () => {
            await onKeyframeFile(target.id, file);
            const cur = stateRef.current;
            persistState(applySceneKfReuses(cur, pack, buildSceneKfPlan(cur, pack)));
            message.success(`Đã gắn ảnh SK cho ${shCode(cur, target)}. 0 Gemini.`);
          })();
        }}
        onApproveSceneKf={approveSceneKf}
        onQaKf={rerunVisualQa}
        onRegenerateSelectedKf={regenerateSelectedKf}
        generateSceneKfBusy={Boolean(stillBusy)}
        sceneVideoReady={sceneVideo.ready.length}
        sceneVideoBlocked={sceneVideo.blocked.length}
        sceneBatchCredits={sceneBatchCredits}
        runwayQuietMin={runwayQuietRemainMin(runwayQuietUntil)}
        onClearRunwayQuiet={() => {
          clearRunwayQuietUntil();
          setRunwayQuietUntil(0);
          message.success('Đã mở gửi. Hỏi lại · 0 cr trước; Gửi lại từng hàng (Confirm). Không gửi hàng loạt.');
        }}
        sessionSrcOf={(id) => sessionClips[id]}
        onPassTake={(s) => lockShotOf(s, { stay: true, autoReview: true })}
        onFailTake={(s) => {
          setActiveId(s.id);
          patchRun(s.id, { status: 'rejected' });
        }}
        cutFrom={cutFrom}
        cutTo={cutTo}
        cutPick={cutPick}
        onCutRange={(from, to) => {
          setCutFrom(from);
          setCutTo(to);
        }}
        onCutPick={setCutPick}
        onLockSceneMaster={(sceneId, locked) => {
          if (locked && state.outputAspect !== '16:9' && state.outputAspect !== '9:16') {
            message.warning('Chọn khung xuất 16:9 hoặc 9:16 trước khi khóa Scene Master.');
            return;
          }
          persistState(locked ? lockSceneMaster(state, sceneId) : unlockSceneMaster(state, sceneId));
        }}
        onOutputAspect={setOutputAspect}
        onPatchSceneMaster={(sceneId, patch) => {
          persistState(upsertSceneMaster(state, { sceneId, ...patch }));
        }}
        cutPlan={cutPlan}
        ttsUrlOf={(id) => {
          if (ttsFiles[id]?.url) return ttsFiles[id].url;
          const cached = ttsUrls.current.get(id);
          if (cached) return cached;
          const blob = ttsBlobs.current.get(id);
          if (!blob) return undefined;
          const url = URL.createObjectURL(blob);
          ttsUrls.current.set(id, url);
          return url;
        }}
        onFillPreviewCut={fillPreviewCut}
        onDownloadTakes={downloadTakes}
        onAssembleCut={assembleCut}
        onEnsureTts={hydrateCutVoice}
        assembleBusy={assembleBusy}
        assembleAspect={assembleAspect}
        onAssembleAspect={setOutputAspect}
        placeHint={scenePlaceHint}
        onAcceptPlaceChange={() => {
          if (active) patchRun(active.id, { kfForceNew: true });
        }}
        onKeepPlaceBaseline={() => {
          if (active) patchRun(active.id, { kfForceNew: false });
        }}
        onForceKfNew={(s) => patchRun(s.id, { kfForceNew: true })}
        onReview={setReview}
        onLockShot={lockShot}
        onFailShot={() => {
          failShot();
        }}
        onPickVideo={(file) => {
          if (active) onClipFile(active.id, file);
        }}
        pane={studioPane}
        onOpenScript={() => setStudioPane('script')}
        onOpenVoice={() => setStudioPane('voice')}
        onSeconds={(id, sec) => persistState(setShortSeconds(state, id, sec))}
        onLockScene={lockScene}
        onApprovePreview={() => persistState({ ...state, previewApproved: true })}
        onPatchRun={(id, patch) => patchRun(id, patch)}
        onApproveScene={(sceneId) =>
          persistState({ ...state, sceneApproved: { ...state.sceneApproved, [sceneId]: true } })
        }
        onI2vProductionMode={(on) => persistState({ ...stateRef.current, i2vProductionMode: on })}
        onOpenShorts={() => setStudioPane('advanced')}
        onOpenStudio={() => {
          if (!state.scriptLocked && !state.voiceLocked && !voiceProductionReady(state)) {
            message.warning('Khóa kịch bản rồi mới chia Short.');
            setStudioPane('script');
            return;
          }
          if (!state.scriptLocked) persistState({ ...state, scriptLocked: true });
          if (!voiceProductionReady(state)) {
            message.warning('Duyệt thoại (VOICE LOCKED) trước khi tạo hình/video.');
            setStudioPane('voice');
            return;
          }
          setStudioPane('studio');
        }}
        onOpenTimeline={() => {
          if (!canOpenStudio(state)) {
            message.warning(sceneBlockReason(state) ?? 'Dựng cảnh (shot dài) chưa mở.');
            setStudioPane('script');
            return;
          }
          setStudioPane('timeline');
        }}
        onOpenAdvanced={() => setStudioPane('advanced')}
        onOpenMemory={() => setMemOpen(true)}
      >
        {studioPane === 'timeline' ? (
          <FamixaTimelinePane
            shots={shots}
            episode={ep}
            lock={lockMem}
            sceneNotes={state.sceneNotes}
            sceneReady={sceneReady}
            sceneLocked={state.sceneLocked}
            statusOf={(s) => shotRunOf(state, s).status}
            actionOf={(s) => effectiveShotAction(s, shotRunOf(state, s))}
            runOf={(s) => shotRunOf(state, s)}
            cuesOf={(s) => voiceCuesForShot(state, s)}
            kitCredits={credits}
            runwaySpent={spentOnRunway}
            onNotes={(sceneNotes) => persistState({ ...state, sceneNotes })}
            onLockScene={lockScene}
            onUnlockScene={() => persistState({ ...state, sceneLocked: false })}
            onOpenShot={(s) => {
              selectShot(s);
              setStudioPane('studio');
            }}
          />
        ) : null}
        {studioPane === 'script' || studioPane === 'voice' ? (
          <Card size="small" title="Bản dựng" style={{ marginBottom: 16 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              Mỗi dòng là một lần dựng (kịch bản + thoại + hình + video). Graph trên server. Tệp KF/TTS/take ở máy này.
              Không giới hạn số tập. Nhận pack ghi đè bản đang mở.
            </Typography.Paragraph>
            <Space wrap style={{ marginBottom: 8 }}>
              <Button onClick={createBuild} disabled={buildBusy}>
                Bản dựng mới
              </Button>
              <Button onClick={() => void refreshBuilds()} disabled={buildBusy}>
                Tải lại danh sách
              </Button>
              {state.buildId ? <Tag color="blue">Đang mở</Tag> : <Tag>Chưa lưu hàng</Tag>}
            </Space>
            {builds.length === 0 ? (
              <Typography.Text type="secondary">Chưa có hàng — Nhận pack sẽ tạo bản Nháp.</Typography.Text>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="fx-build-table">
                  <thead>
                    <tr>
                      <th>Tập</th>
                      <th>Tên</th>
                      <th>Trạng thái</th>
                      <th>Short</th>
                      <th>Thoại</th>
                      <th>KF</th>
                      <th>Video</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {builds.map((b) => {
                      const on = b.id === state.buildId;
                      const st = (b.status in SERIES_BUILD_STATUS_VI
                        ? b.status
                        : 'draft') as SeriesBuildStatus;
                      return (
                        <tr key={b.id} className={on ? 'fx-build-table__on' : undefined}>
                          <td>
                            <Button type="link" size="small" disabled={buildBusy} onClick={() => openBuild(b.id)}>
                              {b.episodeCode || '—'}
                            </Button>
                          </td>
                          <td>
                            <Button type="link" size="small" disabled={buildBusy} onClick={() => openBuild(b.id)}>
                              {b.title || 'Bản dựng'}
                            </Button>
                          </td>
                          <td>
                            <Tag color={on ? 'blue' : undefined}>{SERIES_BUILD_STATUS_VI[st]}</Tag>
                          </td>
                          <td>{b.shotCount}</td>
                          <td>{b.voiceLines}</td>
                          <td>{b.kfCount}</td>
                          <td>{b.videoCount}</td>
                          <td>
                            <Button
                              type="link"
                              size="small"
                              danger
                              disabled={buildBusy}
                              onClick={() => removeBuild(b.id, b.title || b.episodeCode)}
                            >
                              Xóa
                            </Button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        ) : null}
        {studioPane === 'script' || studioPane === 'voice' || studioPane === 'shorts' || studioPane === 'advanced' ? (
          <Collapse
            destroyOnHidden
            defaultActiveKey={
              studioPane === 'voice'
                ? ['cast']
                : studioPane === 'script'
                  ? []
                  : studioPane === 'shorts'
                    ? ['shorts']
                    : ['long']
            }
            items={[
              ...(studioPane === 'script' || studioPane === 'voice'
                ? [
              {
                key: 'pack',
                label: 'Nhận pack Story',
                children: (
                  <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={
          packMode === 'ai'
            ? 'AI đề xuất nháp vào ô dưới. Bạn sửa rồi Nhận pack — không tự khóa, không gửi I2V.'
            : 'Dán kịch bản tập. KIT bung EP → cảnh → shot → thoại. Không tự viết chuyện, chưa trừ Runway.'
        }
        description={
          shorts.length || shots.length
            ? `Sổ KIT (đã khóa): ${credits} cr. Runway đã trừ: ${spentOnRunway} cr. Short khóa ${approvedShortCount(state)}/${shorts.length}.`
            : packMode === 'ai'
              ? 'Một câu hạt giống. Gemini (Cấu hình AI) — không trừ credit Runway.'
              : 'Không cần VIDEO ID hay --- SHOT ---. Dán cả tập rồi bấm Nhận pack — chưa generate.'
        }
      />

      <Card size="small" title="Pack kịch bản" style={{ marginBottom: 0 }}>
        <Space wrap style={{ marginBottom: 12 }}>
          {famixaBrand ? (
            <Tag color={famixaBrand.brainReady ? 'success' : 'gold'}>
              Brand Brain {famixaBrand.code}
              {famixaBrand.brainReady ? '' : ' — thiếu kiến thức'}
            </Tag>
          ) : (
            <Tag color="warning">Chưa có brand Famixa</Tag>
          )}
          <Typography.Text type="secondary">
            48 tài liệu chưng vào{' '}
            <Link to="/content/brands">Thương hiệu</Link>
            {' — '}Studio chỉ lấy lát DNA/cấm + Memory + Action. Không dán docs vào I2V.
          </Typography.Text>
        </Space>
        <Segmented
          value={packMode}
          disabled={state.scriptLocked}
          onChange={(v) => setPackMode(v === 'ai' ? 'ai' : 'paste')}
          options={[
            { label: 'Tự dán', value: 'paste' },
            { label: 'AI đề xuất', value: 'ai' },
          ]}
          style={{ marginBottom: 12 }}
        />
        {packMode === 'ai' ? (
          <div style={{ marginBottom: 12 }}>
            <Space wrap style={{ marginBottom: 8 }}>
              <Tag color={keys.gemini ? 'success' : 'warning'}>{keys.gemini ? 'Đã có key Gemini' : 'Chưa có key Gemini'}</Tag>
              <Typography.Text type="secondary">0 cr Runway · phí Gemini theo Model AI</Typography.Text>
            </Space>
            <Input.TextArea
              rows={3}
              value={packSeed}
              disabled={state.scriptLocked}
              onChange={(e) => setPackSeed(e.target.value)}
              placeholder="Hạt giống — vd. Minh thất vọng vì bố không đến xem buổi thi. Giữ CHAR-001 Minh / Nam / Linh bàn cơm tối."
            />
            <Button
              type="primary"
              icon={<ThunderboltOutlined />}
              style={{ marginTop: 8 }}
              loading={draftBusy}
              disabled={state.scriptLocked || packSeed.trim().length < 12 || !keys.gemini}
              onClick={requestDraft}
            >
              Đề xuất kịch bản
            </Button>
          </div>
        ) : (
          <Typography.Paragraph type="secondary">
            Nhận <strong>kịch bản tập</strong> (FAMIXA / EPISODE / SCENE / thoại) — KIT bung cảnh và shot,
            không tự viết chuyện. Không cần 4 dòng VIDEO ID. Khối <code>--- SHORT ---</code> / shot master
            <code>VIDEO ID …SC01-SH01</code> vẫn nhận được.
          </Typography.Paragraph>
        )}
        <Input.TextArea
          rows={10}
          value={packText}
          onChange={(e) => setPackText(e.target.value)}
          spellCheck={false}
          placeholder={
            packMode === 'ai'
              ? 'Nháp sẽ hiện ở đây sau khi đề xuất. Sửa rồi bấm Nhận pack.'
              : 'Dán kịch bản tập Famixa (không cần --- SHOT ---):\n\nVIDEO TITLE: BỐ ĐỪNG HỨA NỮA\nSC01 — LỜI HỨA\nMinh:\nBố.'
          }
          style={{ fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
        />
        <Space wrap style={{ marginTop: 12 }}>
          <Button onClick={() => setPackText(FAMIXA_PACK_SKELETON)}>Chèn khung trống</Button>
          <Button
            onClick={() => {
              void import('./content-famixa-ep01-golden.txt?raw').then((m) =>
                setPackText(String(m.default ?? '').trim()),
              );
            }}
          >
            Kịch bản EP01 đã gửi
          </Button>
          <Button type="primary" disabled={!packText.trim()} onClick={applyPack}>
            Nhận pack
          </Button>
          {active ? (
            <>
              <Button icon={<CopyOutlined />} onClick={() => copyShotPack(active)}>
                Copy khối {active.id}
              </Button>
              <Button onClick={() => putShotPackToPaste(active)}>
                Đưa khối {active.id} lên ô dán để sửa
              </Button>
            </>
          ) : null}
        </Space>
        {packResult ? (
          <Alert
            type={packResult.ok ? 'success' : 'error'}
            showIcon
            style={{ marginTop: 12 }}
            message={packResult.text}
          />
        ) : null}
        {packResult?.ok && (state.scenes?.length || state.lines?.length) ? (
          <Card size="small" title="Parsed Story — Script Beat → Shot" style={{ marginTop: 12 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {[ep?.episode, ep?.title].filter(Boolean).join(' · ') || 'EP'} — kịch bản quyết định nội dung. Shot
              Decomposition chỉ cách quay. KIT không tạo SH rỗng để đủ số lượng. 70 Short là năng lực, không phải
              requirement.
            </Typography.Paragraph>
            {(state.parseWarnings ?? []).map((w) => (
              <Alert key={w} type="warning" showIcon style={{ marginBottom: 8 }} message={w} />
            ))}
            {(state.characters ?? []).map((c) => (
              <Tag key={c.id}>
                {c.id} {c.name}
                {c.role ? ` · ${c.role}` : ''}
                {c.voiceName || c.voiceId ? ` · voice ${c.voiceName || c.voiceId}` : ' · chưa voice'}
              </Tag>
            ))}
            {(state.scenes ?? []).map((sc) => {
              const sceneShots = episodeShots(state).filter(
                (s) =>
                  (s.sceneId || s.scene) === sc.id ||
                  (s.id || '').includes(`-${sc.id}-`) ||
                  (s.id || '').startsWith(`${sc.id}-`),
              );
              const beats = sc.scriptBeats?.length
                ? sc.scriptBeats
                : groupShotsByBeat(sceneShots).map((g, i) => ({
                    id: g.beatId || `${sc.id}-BEAT${String(i + 1).padStart(2, '0')}`,
                    text: g.label,
                    shotIds: g.shots.map((s) => s.id),
                  }));
              const sceneLines = linesForScene(state, sc.id);
              return (
                <div key={sc.id} style={{ marginTop: 12 }}>
                  <Typography.Text strong>
                    {sc.id}
                    {sc.title ? ` — ${sc.title}` : ''}
                  </Typography.Text>
                  <Typography.Paragraph type="secondary" style={{ margin: '2px 0 6px', fontSize: 12 }}>
                    Kịch bản: {beats.length} beats · Đề xuất {sceneShots.filter((s) => shotHasValidAction(s)).length}{' '}
                    shots
                    {(() => {
                      const d = shotPlanDensity(sceneShots);
                      return d.warn ? ` · mật độ ${d.ratio.toFixed(1)} — xem lại` : '';
                    })()}
                    {sc.environment ? ` · ${sc.environment}` : ''}
                  </Typography.Paragraph>
                  {beats.map((b, i) => {
                    const kids = sceneShots.filter((s) => b.shotIds.includes(s.id));
                    return (
                      <div key={b.id} style={{ margin: '6px 0 8px 8px', fontSize: 12 }}>
                        <div>
                          <Typography.Text strong>BEAT {String(i + 1).padStart(2, '0')}</Typography.Text>
                          <span style={{ color: '#64748b' }}> · {b.text}</span>
                        </div>
                        {kids.map((s, si) => {
                          const hold = !shotHasValidAction(s);
                          return (
                            <div key={s.id} style={{ marginLeft: 16, color: hold ? '#94a3b8' : '#334155' }}>
                              {si === kids.length - 1 ? '└─' : '├─'} {s.shot} — {s.story}
                              {s.splitReason ? ` · ${s.splitReason}` : ''}
                              {hold ? ' · HOLD' : ` · ${s.seconds || 5}s`}
                              <NovixaShortStrategyDesk
                                shot={s}
                                accepted={strategyAcceptedByShot[s.id] === true}
                                catalog={osActionsByShot[s.id]}
                                onCommit={async (patch) => {
                                  persistShotStrategy(s.id, patch);
                                  const live = (stateRef.current.episode?.shots ?? []).find((row) => row.id === s.id) || {
                                    ...s,
                                    ...patch,
                                  };
                                  const buildId = stateRef.current.buildId;
                                  if (!buildId) {
                                    if (patch.strategyConfirmed) throw new Error('Chưa có build. Server chưa nhận xác nhận.');
                                    return;
                                  }
                                  const actions = await getProductionShotActions(buildId, s.id, {
                                    story: (live.story || '').trim() || undefined,
                                    ...strategyClaim(live),
                                  });
                                  setOsActionsByShot((current) => ({ ...current, [s.id]: actions }));
                                  noteStrategyCatalog(s.id, actions);
                                }}
                              />
                            </div>
                          );
                        })}
                      </div>
                    );
                  })}
                  {sceneLines.map((l) => {
                    const who = (state.characters ?? []).find((c) => c.id === l.characterId);
                    return (
                      <Typography.Paragraph key={l.id} style={{ margin: '2px 0 0 8px', fontSize: 12 }}>
                        {who?.name || l.characterId}: {l.text}
                      </Typography.Paragraph>
                    );
                  })}
                </div>
              );
            })}
            <Space wrap style={{ marginTop: 12 }}>
              {state.storyReviewed ? (
                <Tag color="green">Đã duyệt Parsed Story</Tag>
              ) : (
                <Button
                  type="primary"
                  onClick={() => persistState({ ...state, storyReviewed: true })}
                >
                  Parsed Story đúng
                </Button>
              )}
              {state.shotGraphLocked ? (
                <Tag color="green">SHOT GRAPH LOCKED</Tag>
              ) : (
                <Button onClick={lockShotGraph} disabled={!state.storyReviewed}>
                  Duyệt cách chia shot
                </Button>
              )}
            </Space>
          </Card>
        ) : null}
        <ContentFamixaStoryMemoryCard state={state} onChange={persistState} />
      </Card>
                  </>
                ),
              },
              {
                key: 'cast',
                label: `Vai & tập${state.roles.length ? ` · ${state.roles.length} vai` : ''}`,
                children: !famixaCharacters ? (
                  <p className="fx-desk__note">Chưa cấu hình bộ nhân vật cho phim này.</p>
                ) : (
                  <>
      <ContentFamixaCharacterUniverseCard
        rows={universe}
        busy={universeBusy}
        onRefresh={() => void loadUniverse()}
        onOpenWorkspace={(code) => setWorkspaceCode(code)}
        onCreate={async (input) => {
          if (!famixaCharacters) return;
          const row = await createFamixaCharacter({
            name: input.name,
            role: input.role,
            gender: input.gender,
            initialAge: input.initialAge,
            universe: input.universe,
            description: input.description,
            allowNew: true,
            forceCreate: input.forceCreate,
          });
          setUniverse((cur) => {
            const next = toUniverseRow(row);
            return cur.some((r) => r.characterCode === next.characterCode)
              ? cur.map((r) => (r.characterCode === next.characterCode ? next : r))
              : [...cur, next];
          });
          setWorkspaceCode(row.characterCode);
          message.success(`${row.characterCode} DRAFT. Mở workspace — chưa Canon.`);
        }}
        onApprove={(code) => {
          if (!famixaCharacters) return;
          setUniverseBusy(code);
          void approveFamixaCharacter(code)
            .then((row) => {
              setUniverse((cur) => cur.map((r) => (r.characterCode === code ? toUniverseRow(row) : r)));
              message.success(`Đã APPROVE ${code}.`);
            })
            .catch((e) => message.error(apiErrorMessage(e, 'Không APPROVE được.')))
            .finally(() => setUniverseBusy(undefined));
        }}
        onLock={(code) => {
          if (!famixaCharacters) return;
          setUniverseBusy(code);
          void lockFamixaCharacter(code)
            .then((row) => {
              setUniverse((cur) => cur.map((r) => (r.characterCode === code ? toUniverseRow(row) : r)));
              message.success(`Đã LOCK ${code}. Canon không tự overwrite.`);
            })
            .catch((e) => message.error(apiErrorMessage(e, 'Không LOCK được.')))
            .finally(() => setUniverseBusy(undefined));
        }}
        onUnlock={(code) => {
          if (!famixaCharacters) return;
          setUniverseBusy(code);
          void unlockFamixaCharacter(code)
            .then((row) => {
              setUniverse((cur) => cur.map((r) => (r.characterCode === code ? toUniverseRow(row) : r)));
              message.success(`Đã UNLOCK ${code} → version mới (DRAFT). V1 vẫn giữ. Phải REVIEW → APPROVE → LOCK.`);
            })
            .catch((e) => message.error(apiErrorMessage(e, 'Không UNLOCK được.')))
            .finally(() => setUniverseBusy(undefined));
        }}
      />
      <ContentFamixaCharacterWorkspace
        open={Boolean(workspaceCode)}
        row={universe.find((r) => r.characterCode === workspaceCode)}
        registry={universe}
        busy={Boolean(universeBusy)}
        onClose={() => setWorkspaceCode(undefined)}
        onChanged={(next) => {
          setUniverse((cur) =>
            cur.some((r) => r.characterCode === next.characterCode)
              ? cur.map((r) => (r.characterCode === next.characterCode ? toUniverseRow(next) : r))
              : [...cur, toUniverseRow(next)],
          );
        }}
      />
      <Card
        size="small"
        title="Vai · Cast & Canon"
        extra={
          <Button size="small" icon={<PlusOutlined />} onClick={addRole} disabled={castFrozen}>
            Thêm vai
          </Button>
        }
        style={{ marginBottom: 16 }}
      >
        {state.roles.length === 0 ? (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            Dán <code>ROLE: Story | An</code> trong pack, hoặc bấm Thêm vai. Gắn 1 ảnh Canon + Voice Canon / vai — shot sau kế thừa, không chọn lại Minh từng clip.
          </Typography.Paragraph>
        ) : null}
        <div className="content-video-lab-cards">
          {state.roles.map((r) => {
            const ch = characterOfRole(state, r);
            const canonOn = characterCanonReady(ch);
            const voiceOn = roleVoiceReady(state, r);
            const voiceId = (ch?.voiceId || r.voiceId || '').trim();
            const voicePick = voiceOptionsForRole(r, voiceId);
            const childLane = isChildVoiceLane(voicePick.lane);
            const voiceOnly = isVoiceOnlyRole(r, ch);
            const chosenVoice = voices.find((v) => v.voiceId === voiceId);
            const adultOnChild = Boolean(childLane && voiceId && chosenVoice && !isKidLibraryVoice(chosenVoice));
            const southOnRole = Boolean(
              (chosenVoice && !voiceSoundsNorthern(chosenVoice)) ||
                /southern|saigon|sai\s*gon|miền nam|mien nam|giọng nam bộ|giong nam/i.test(
                  `${ch?.voiceName || ''} ${r.voiceName || ''}`,
                ),
            );
            const portrait = ch ? canonDisplayOf(state, ch.id) : undefined;
            const master = famixaCanonSeedFor(ch ?? { id: r.characterId, name: r.name });
            const canonBlocked = castFrozen && canonOn;
            return (
            <div key={r.id} className="content-video-lab-card" style={{ cursor: 'default' }}>
              <Input
                placeholder="Tên vai"
                value={r.title}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { title: e.target.value })}
              />
              <Input
                style={{ marginTop: 8 }}
                placeholder="Tên người"
                value={r.name}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { name: e.target.value })}
              />
              <Space wrap style={{ marginTop: 8 }}>
                {r.characterId ? <Tag>{r.characterId}</Tag> : <Tag>Chưa gắn CHAR</Tag>}
                {voiceOnly ? (
                  <Tag>Chỉ giọng</Tag>
                ) : canonOn ? (
                  <Tag color="green">Canon ✓</Tag>
                ) : (
                  <Tag color="gold">Chưa Canon</Tag>
                )}
                {master ? <Tag>Master v1.0</Tag> : null}
                {voiceOn ? (
                  <Tag color="green">Voice {ch?.voiceName || '✓'}</Tag>
                ) : (
                  <Tag color="gold">Chưa Voice</Tag>
                )}
                {voices.some((v) => v.voiceId === voiceId && v.cloned) ? (
                  <Tag color="warning">Clone — gói chưa TTS</Tag>
                ) : null}
                {childLane ? <Tag>11 tuổi</Tag> : null}
                {adultOnChild ? <Tag color="gold">Giọng người lớn — chọn lại giọng trẻ</Tag> : null}
                {southOnRole ? <Tag color="gold">Giọng Nam/không gắn Bắc — đổi Hà Nội</Tag> : null}
              </Space>
              <div style={{ marginTop: 8 }}>
                {voiceOnly ? (
                  <Typography.Paragraph type="secondary" style={{ margin: 0, fontSize: 12 }}>
                    Lời bình không lên hình — không gắn ảnh Canon, không I2V. Chỉ chọn giọng nam miền Bắc (trầm ấm).
                  </Typography.Paragraph>
                ) : portrait ? (
                  <img
                    src={portrait}
                    alt={ch?.canonFileName || r.name || 'canon'}
                    style={{ width: 96, height: 96, objectFit: 'cover', borderRadius: 8, display: 'block' }}
                  />
                ) : (
                  <Tag>{ch?.canonFileName || 'Chưa có ảnh Canon'}</Tag>
                )}
                {voiceOnly ? null : (
                  <>
                    <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                      {ch?.canonFileName || ch?.canonLocalPath
                        ? [ch.canonFileName, ch.canonLocalPath].filter(Boolean).join(' · ')
                        : master
                          ? 'Mặt / tóc / tuổi — không dùng làm KF I2V'
                          : 'Không có Master sheet. Chọn ảnh mặt từ máy — không dùng làm KF I2V.'}
                    </Typography.Paragraph>
                    <input
                      type="file"
                      accept="image/*"
                      hidden
                      disabled={canonBlocked}
                      id={`canon-${r.id}`}
                      onChange={(e) => {
                        void onRoleCanon(r.id, e.target.files?.[0]);
                        e.target.value = '';
                      }}
                    />
                    <Space wrap style={{ marginTop: 4 }}>
                      <Button
                        size="small"
                        icon={<FolderOpenOutlined />}
                        disabled={canonBlocked}
                        onClick={() => document.getElementById(`canon-${r.id}`)?.click()}
                      >
                        {canonOn ? 'Thay Canon' : 'Gắn Canon'}
                      </Button>
                    </Space>
                  </>
                )}
              </div>
              <Input
                style={{ marginTop: 8 }}
                placeholder="Câu thoại duyệt (1 câu)"
                value={r.line ?? ''}
                disabled={state.scriptLocked}
                onChange={(e) => patchRole(r.id, { line: e.target.value })}
              />
              <Select
                style={{ marginTop: 8, width: '100%' }}
                placeholder={
                  keys.elevenLabs
                    ? voicePick.options.length
                      ? `Chọn ${voicePick.lane.label}`
                      : 'Không lọc được giọng — dán Voice ID bên dưới'
                    : 'Chưa có key ElevenLabs — vẫn gán Voice ID'
                }
                showSearch
                allowClear
                listHeight={280}
                disabled={castFrozen}
                value={voiceId || undefined}
                filterOption={voicePick.filterOption}
                options={voicePick.options}
                notFoundContent={voicesLoading ? 'Đang tải thư viện…' : 'Trống — xóa ô tìm, hoặc dán Voice ID nam Hà Nội'}
                onChange={(id, opt) => {
                  const label = !Array.isArray(opt) && opt && typeof opt === 'object' && 'label' in opt
                    ? String(opt.label)
                    : voices.find((v) => v.voiceId === id)?.name;
                  patchRole(r.id, { voiceId: id, voiceName: id ? label : undefined });
                }}
              />
              <Button
                size="small"
                icon={<ReloadOutlined />}
                loading={voicesLoading}
                style={{ marginTop: 6 }}
                onClick={() => void loadVoiceLibrary({ force: true })}
              >
                {voices.length ? `Tải lại thư viện (${voices.length})` : 'Tải thư viện giọng'}
              </Button>
              <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                {voicesLoading
                  ? 'Đang tải thư viện giọng ElevenLabs… Voice ID trên vai vẫn giữ.'
                  : childLane
                    ? southOnRole
                      ? 'An/Minh nói giọng Bắc. Voice đang chọn nghe Nam — đổi giọng Hà Nội (young) rồi Tạo Full Voice lại. Câu thoại giữ nguyên.'
                      : adultOnChild
                      ? 'Minh/An là bé trai 11 tuổi. Đổi sang giọng nam young/bé — TTS lại mới nghe khác.'
                      : voices.length
                        ? `${voicePick.options.length} giọng trẻ 11 tuổi miền Bắc. TTS lại sau khi đổi giọng.`
                        : 'Chưa lấy được thư viện giọng — Voice ID trên vai vẫn dùng được. Thử reload.'
                    : voicePick.options.length
                      ? `${voicePick.options.length} giọng ${voicePick.lane.label}. Thêm nhân vật sau cũng chỉ hiện đúng loại giọng.`
                      : keys.elevenLabs
                        ? voices.length
                          ? voiceOnly
                            ? 'Xóa chữ trong ô tìm — hiện giọng nam miền Bắc. Hoặc dán Voice ID Hà Nội.'
                            : 'Chưa có giọng miền Bắc khớp vai — reload hoặc dán Voice ID.'
                          : 'Chưa lấy được thư viện giọng — Voice ID trên vai vẫn dùng được. Thử reload.'
                        : 'Cần key ElevenLabs để tải thư viện giọng miền Bắc.'}
              </Typography.Paragraph>
              <Input
                style={{ marginTop: 8 }}
                placeholder="Hoặc dán Voice ID"
                value={voiceId}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { voiceId: e.target.value.trim() })}
              />
              <Input
                style={{ marginTop: 8 }}
                placeholder="Ghi chú giọng (tốc độ / cảm xúc) — không phải Voice ID"
                value={r.voiceNote ?? ''}
                disabled={state.scriptLocked}
                onChange={(e) => patchRole(r.id, { voiceNote: e.target.value })}
              />
              {ch ? (
                <Space wrap style={{ marginTop: 8 }}>
                  <Input
                    size="small"
                    style={{ width: 88 }}
                    placeholder="Tuổi (11)"
                    value={ch.voiceBible?.ageImpression ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { ageImpression: e.target.value }))}
                  />
                  <Input
                    size="small"
                    style={{ width: 120 }}
                    placeholder="Tone"
                    value={ch.voiceBible?.tone ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { tone: e.target.value }))}
                  />
                  <Input
                    size="small"
                    style={{ width: 140 }}
                    placeholder="Habit (ngập…)"
                    value={ch.voiceBible?.habit ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { habit: e.target.value }))}
                  />
                  <Input
                    size="small"
                    style={{ width: 120 }}
                    placeholder="Formality"
                    value={ch.voiceBible?.formality ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { formality: e.target.value }))}
                  />
                </Space>
              ) : null}
              <Button
                size="small"
                icon={<SoundOutlined />}
                loading={ttsBusy && listenCues.find((c) => c.id === playingLineId)?.characterId === (ch?.id || r.characterId)}
                disabled={!keys.elevenLabs || !voiceId || (!(r.line ?? '').trim() && !listenCues.some((c) => c.characterId === ch?.id))}
                style={{ marginTop: 4 }}
                onClick={() => {
                  const cue =
                    listenCues.find((c) => c.characterId === (ch?.id || r.characterId)) ||
                    ((r.line ?? '').trim()
                      ? {
                          id: `role-line-${r.id}`,
                          characterId: ch?.id || r.characterId || r.id,
                          name: r.name || ch?.name || r.title,
                          text: (r.line ?? '').trim(),
                          voiceId,
                        }
                      : undefined);
                  if (!cue) {
                    message.warning('Chưa có câu thoại của vai này trong kịch bản.');
                    return;
                  }
                  void playListenCue(cue);
                }}
              >
                {ttsBusy && listenCues.find((c) => c.id === playingLineId)?.characterId === (ch?.id || r.characterId)
                  ? 'Đang đọc…'
                  : 'Nghe thử câu của vai'}
              </Button>
              <Input
                style={{ marginTop: 8 }}
                placeholder="Performance Canon (tự nhiên, ít gesture…)"
                value={r.performance ?? ch?.performance ?? ''}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { performance: e.target.value })}
              />
              <Button
                type="link"
                size="small"
                danger
                icon={<DeleteOutlined />}
                onClick={() => removeRole(r.id)}
                style={{ paddingLeft: 0, marginTop: 4 }}
                disabled={castFrozen}
              >
                Xóa
              </Button>
            </div>
            );
          })}
        </div>
        <Space wrap style={{ marginTop: 8 }}>
          {state.castLocked ? (
            <>
              <Tag color="green">Cast & Canon đã khóa</Tag>
              <Button onClick={() => persistState({ ...state, castLocked: false })}>
                Mở khóa Cast
              </Button>
            </>
          ) : (
            <Button
              type="primary"
              icon={<LockOutlined />}
              disabled={!canLockCast(state)}
              onClick={() => {
                persistState(lockCast(state));
                message.success('Đã khóa Cast & Canon. Shot sau dùng cùng ảnh + giọng.');
              }}
            >
              Khóa Cast & Canon
            </Button>
          )}
          {ready && state.roles.every((r) => roleCanonReady(state, r) && roleVoiceReady(state, r)) ? (
            <Tag color="success">{state.roles.length} vai · Canon + Voice đủ</Tag>
          ) : ready && state.roles.some((r) => isVoiceOnlyRole(r, characterOfRole(state, r)) && !roleVoiceReady(state, r)) ? (
            <Tag color="gold">Gán Voice Canon cho Lời bình — không cần ảnh</Tag>
          ) : ready ? (
            <Tag color="gold">Thiếu ảnh Canon hoặc Voice Canon</Tag>
          ) : (
            <Tag>{state.roles.length === 0 ? 'Chưa có vai' : 'Điền đủ tên vai + người'}</Tag>
          )}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
          Canon v{state.canonVersion || 1} lưu máy — tập sau vẫn đúng: Minh / Nam / Linh trong khung · An ngoài khung ·
          không tự thêm người từ heading.
        </Typography.Paragraph>
      </Card>

      <Card size="small" title="Full Script → Voice Script → Full Voice" style={{ marginBottom: 16 }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          Kịch bản gốc không đổi. KIT tách Voice Script — chỉ lời CAST nói. Không đọc mô tả, SFX, tin nhắn, câu nhớ lại.
        </Typography.Paragraph>
        {voiceScript.droppedNonSpeechCount > 0 ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 8 }}
            message={`Đã bỏ ${voiceScript.droppedNonSpeechCount} dòng action/SMS/SFX khỏi Voice Script — không TTS.`}
          />
        ) : null}
        {ttsNote ? (
          <Typography.Paragraph style={{ marginBottom: 8 }}>{ttsNote}</Typography.Paragraph>
        ) : null}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 8 }}>
          <div>
            <Typography.Text strong>FULL SCRIPT</Typography.Text>
            <div style={{ maxHeight: 220, overflow: 'auto', marginTop: 6, fontSize: 12 }}>
              {(state.scenes ?? []).map((sc) => (
                <div key={sc.id} style={{ marginBottom: 8 }}>
                  <Typography.Text type="secondary">
                    {sc.id}
                    {sc.title ? ` — ${sc.title}` : ''}
                  </Typography.Text>
                  {(sc.actions ?? []).slice(0, 3).map((a) => (
                    <Typography.Paragraph key={a} type="secondary" style={{ margin: 0, fontSize: 11 }}>
                      {a}
                    </Typography.Paragraph>
                  ))}
                  {(sc.dialogue ?? []).map((d) => (
                    <Typography.Paragraph key={d.id} style={{ margin: 0 }}>
                      {(state.characters ?? []).find((c) => c.id === d.characterId)?.name || d.characterId}: {d.text}
                    </Typography.Paragraph>
                  ))}
                </div>
              ))}
            </div>
          </div>
          <div>
            <Typography.Text strong>VOICE SCRIPT (TTS)</Typography.Text>
            <Input.TextArea
              readOnly
              value={formatVoiceScriptPreview(voiceScript) || 'Chưa có thoại — Nhận pack.'}
              rows={10}
              style={{ marginTop: 6, fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
            />
          </div>
        </div>
        {listenCues.length === 0 ? null : (
          <div style={{ maxHeight: 280, overflow: 'auto', marginBottom: 8 }}>
            {listenCues.map((cue) => (
              <div
                key={cue.id}
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'flex-start',
                  padding: '4px 0',
                  background: playingLineId === cue.id ? 'rgba(22, 119, 255, 0.08)' : undefined,
                }}
              >
                <Button
                  size="small"
                  type="text"
                  icon={<SoundOutlined />}
                  loading={ttsBusy && playingLineId === cue.id}
                  disabled={!cue.voiceId || !keys.elevenLabs}
                  onClick={() => void playListenCue(cue)}
                />
                <div style={{ flex: 1 }}>
                  <Typography.Text strong>[{cue.name}]</Typography.Text>
                  {(() => {
                    const dir = resolveLinePerformance({
                      text: cue.text,
                      characterId: cue.characterId,
                      name: cue.name,
                      performance: cue.performance ?? linePerformanceOf(state, cue.id),
                    });
                    const locked = Boolean((cue.performance ?? linePerformanceOf(state, cue.id))?.locked);
                    return (
                      <Space size={4} wrap style={{ marginLeft: 6 }}>
                        <Tag color={locked ? 'green' : 'default'}>{locked ? 'LOCKED' : 'DRAFT'} {dir.label}</Tag>
                        <Select
                          size="small"
                          style={{ width: 110 }}
                          value={dir.emotion}
                          disabled={state.voiceLocked}
                          options={ACTING_EMOTIONS.map((e) => ({ value: e, label: e }))}
                          onChange={(emotion) => persistState(patchDialoguePerformance(state, cue.id, { emotion }))}
                        />
                        <Select
                          size="small"
                          style={{ width: 56 }}
                          value={dir.intensity}
                          disabled={state.voiceLocked}
                          options={[1, 2, 3, 4, 5].map((n) => ({ value: n, label: `${n}` }))}
                          onChange={(intensity) => persistState(patchDialoguePerformance(state, cue.id, { intensity }))}
                        />
                        <Select
                          size="small"
                          style={{ width: 88 }}
                          value={dir.pace}
                          disabled={state.voiceLocked}
                          options={['slow', 'natural', 'fast'].map((p) => ({ value: p, label: p }))}
                          onChange={(pace) => persistState(patchDialoguePerformance(state, cue.id, { pace }))}
                        />
                      </Space>
                    );
                  })()}
                  {!cue.voiceId ? <Tag style={{ marginLeft: 6 }}>Chưa Voice</Tag> : null}
                  <Typography.Paragraph style={{ margin: 0, fontSize: 12 }}>{cue.text}</Typography.Paragraph>
                </div>
              </div>
            ))}
          </div>
        )}
        <Space wrap style={{ marginBottom: 8 }}>
          <Tag>Dialogue lines: {voiceScript.sourceLineCount}</Tag>
          <Tag>Characters: {voiceScript.sourceCharCount.toLocaleString('vi-VN')}</Tag>
          <Tag>
            Voice generated: {(state.voicePreview?.generatedCharCount ?? 0).toLocaleString('vi-VN')}
          </Tag>
          <Tag color={state.voicePreview?.status === 'complete' ? 'green' : state.voicePreview?.status === 'incomplete' ? 'gold' : 'default'}>
            Status: {(state.voicePreview?.status ?? 'idle').toUpperCase()}
          </Tag>
          <Tag>~{voiceScript.estimatedSec}s</Tag>
        </Space>
        {(state.voicePreview?.issues ?? []).slice(0, 4).map((w) => (
          <Alert key={w} type="warning" showIcon style={{ marginBottom: 6 }} message={w} />
        ))}
        <Space wrap>
          <Button
            type="primary"
            icon={<SoundOutlined />}
            loading={ttsBusy}
            disabled={listenCues.length === 0 || !keys.elevenLabs}
            onClick={playWholeScript}
          >
            Tạo Full Voice
          </Button>
          {ttsBusy ? <Button onClick={stopScriptListen}>Dừng</Button> : null}
          <Button
            icon={<DownloadOutlined />}
            loading={ttsBusy}
            disabled={listenCues.length === 0 || !keys.elevenLabs}
            onClick={downloadFullScriptMp3}
          >
            Tải MP3 Voice Script
          </Button>
          {ttsFull ? (
            <Typography.Link href={ttsFull.url} download={ttsFull.fileName}>
              {ttsFull.fileName}
            </Typography.Link>
          ) : null}
        </Space>
      </Card>

      {ep && (ep.title || ep.episode || ep.premise) ? (
        <Card size="small" title={[ep.episode, ep.title].filter(Boolean).join(' · ') || 'Tập từ pack'} style={{ marginBottom: 16 }}>
          {ep.premise ? <Typography.Paragraph style={{ marginBottom: 8 }}>{ep.premise}</Typography.Paragraph> : null}
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {[ep.moral, ep.ctaRule].filter(Boolean).join(' · ')}
          </Typography.Paragraph>
        </Card>
      ) : null}

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="Story → Full Script → Voice Script → Full Voice LOCK → mới Image/Video. Ảnh Canon = mặt/tóc, không phải KF bàn ăn."
      />
      <Space wrap>
        {state.scriptLocked ? (
          <>
            <Tag color="green">Kịch bản đã khóa</Tag>
            <Button onClick={() => persistState({ ...state, scriptLocked: false })}>Mở khóa kịch bản</Button>
          </>
        ) : (
          <Button type="primary" disabled={!canLockScript(state)} onClick={lockScript}>
            Khóa kịch bản → duyệt thoại
          </Button>
        )}
        {state.voiceLocked ? (
          <>
            <Tag color="green">Full Voice đã khóa</Tag>
            <Button onClick={() => persistState({ ...state, voiceLocked: false })}>Mở khóa Voice</Button>
          </>
        ) : (
          <>
            <Tooltip title={canLockVoice(state) ? undefined : voiceLockHint}>
              <span>
                <Button type="primary" disabled={!canLockVoice(state)} onClick={() => lockVoice()}>
                  Khóa Full Voice
                </Button>
              </span>
            </Tooltip>
            {!canLockVoice(state) ? (
              <>
                <Button
                  type="primary"
                  onClick={() => {
                    modal.confirm({
                      title: 'Khóa Full Voice — không gọi ElevenLabs',
                      content:
                        'Dùng khi đã Tạo Full Voice rồi. F5 mất cache hoặc parser bỏ dòng action nên Status không còn COMPLETE. Không trừ phí TTS.',
                      okText: 'Khóa, không TTS lại',
                      cancelText: 'Hủy',
                      onOk: () => lockVoice({ skipRegen: true }),
                    });
                  }}
                >
                  Đã TTS — khóa không gọi lại
                </Button>
                <Button
                  icon={<SoundOutlined />}
                  loading={ttsBusy}
                  disabled={listenCues.length === 0 || !keys.elevenLabs}
                  onClick={playWholeScript}
                >
                  Tạo lại (tính phí)
                </Button>
              </>
            ) : null}
          </>
        )}
      </Space>
      {!state.voiceLocked && !canLockVoice(state) ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 12 }}
          message={voiceLockHint}
          description="Đã tạo Full Voice rồi thì đừng bấm Tạo lại — ElevenLabs tính phí ký tự. F5 / hydrate server có thể làm mất Status COMPLETE. Bấm «Đã TTS — khóa không gọi lại»."
        />
      ) : null}
                  </>
                ),
              },
                ]
              : []),
              ...(studioPane === 'shorts'
                ? [
              {
                key: 'shorts',
                label: shorts.length ? `Short 9:16 · ${shorts.length}` : 'Short 9:16',
                children: (
                  <>
      {shorts.length > 0 ? (
        <Card
          size="small"
          title="Danh sách short"
          extra={
            <Space wrap>
              <Button
                size="small"
                onClick={() => {
                  const cur = activeShort?.id;
                  const next = insertShortClip(stateRef.current, cur ? { beforeId: cur } : undefined);
                  persistState(next.state);
                  setActiveShortId(next.short.id);
                  message.success(
                    `Đã chèn ${next.short.id} trước ${cur || 'cuối danh sách'}. KF clip cũ giữ nguyên.`,
                  );
                }}
              >
                Chèn short trước clip này
              </Button>
              <Button
                size="small"
                onClick={() => {
                  const next = insertShortClip(stateRef.current, activeShort ? { afterId: activeShort.id } : undefined);
                  persistState(next.state);
                  setActiveShortId(next.short.id);
                  message.success(`Đã thêm ${next.short.id}. KF clip cũ giữ nguyên.`);
                }}
              >
                Thêm short sau
              </Button>
            <Button
              size="small"
              loading={Boolean(stillBusy)}
              disabled={!keys.gemini || shorts.length === 0}
              onClick={() => {
                const pending = shorts.filter((s) => !shortRunOf(stateRef.current, s.id).keyframeDataUrl);
                const targets = pending.length ? pending : shorts;
                modal.confirm({
                  title: `Tạo KF 9:16 cho ${targets.length} short từ Canon vai`,
                  content:
                    'Gemini vẽ cả khung cảnh (không phải crop mặt). Quota Gemini — không trừ Runway. Bạn vẫn duyệt / Gửi Turbo từng short.',
                  okText: 'Tạo ảnh',
                  cancelText: 'Hủy',
                  onOk: async () => {
                    for (const s of targets) {
                      const sc = (stateRef.current.scenes ?? []).find((x) => x.id === s.sceneId);
                      const kind = await generateKfFromCanon({
                        clipId: s.id,
                        aspect: '9:16',
                        visual: s.visual || s.hook,
                        action: s.motionPromptVi || s.hook,
                        location: s.scene || sc?.environment,
                        characterIds: s.characterIds?.length ? s.characterIds : s.characters,
                      });
                      if (kind === 'DUPLICATE_PIXEL') continue;
                      if (kind !== 'NEW_PENDING_PICTURE') break;
                    }
                  },
                });
              }}
            >
              Tạo ảnh KF từng short
            </Button>
            </Space>
          }
          style={{ marginBottom: 16 }}
        >
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={shorts}
            onRow={(row) => ({
              onClick: () => setActiveShortId(row.id),
              style: {
                cursor: 'pointer',
                background: row.id === activeShort?.id ? '#f5f3ff' : undefined,
              },
            })}
            columns={[
              { title: 'Short', dataIndex: 'id', width: 120 },
              { title: 's', dataIndex: 'seconds', width: 48 },
              { title: 'Hook', dataIndex: 'hook' },
              {
                title: 'TT',
                width: 120,
                render: (_, row) => {
                  const lab = SERIES_STATUS_LABEL[shortRunOf(state, row.id).status];
                  return <Tag color={lab.color}>{lab.text}</Tag>;
                },
              },
            ]}
          />
        </Card>
      ) : (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Tập này không có short 9:16"
          description={
            voiceProductionReady(state)
              ? `Bỏ qua Short. Có ${shots.length} shot 16:9 — bấm Dựng cảnh.`
              : 'Bỏ qua Short. Khóa Full Voice ở Kịch bản rồi vào Dựng cảnh (ảnh/video vẫn chờ Voice COMPLETE).'
          }
        />
      )}
      {(shorts.length === 0 || !voiceProductionReady(state)) ? (
        <Space wrap>
          <Button onClick={() => setStudioPane('script')}>Kịch bản · Full Voice</Button>
          {canOpenStudio(state) ? (
            <Button type="primary" onClick={() => setStudioPane('studio')}>
              Dựng cảnh 16:9
            </Button>
          ) : null}
        </Space>
      ) : null}
                  </>
                ),
              },
              {
                key: 'stills',
                label: 'Ảnh nhân vật',
                children: (
                  <>
      {shorts.length > 0 || shots.length > 0 ? (
        <Card
          size="small"
          title="Ảnh nhân vật — kế thừa Character Canon"
          extra={
            <Space>
              <Checkbox checked={stillOnlyActive} onChange={(e) => setStillOnlyActive(e.target.checked)}>
                Chỉ clip đang làm
              </Checkbox>
              <Button size="small" icon={<PlusOutlined />} onClick={addStill} disabled={state.scriptLocked}>
                Thêm hàng
              </Button>
            </Space>
          }
          style={{ marginBottom: 16 }}
        >
          <Typography.Paragraph type="secondary">
            Short/shot dùng cùng Canon trên vai. Không chọn lại mặt Minh từng clip. Ảnh này không gửi I2V — KF cảnh vẫn là bàn ăn.
          </Typography.Paragraph>
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={visibleStills}
            locale={{ emptyText: 'Chưa có hàng — nhận pack có CHAR/SCENE hoặc Thêm hàng.' }}
            columns={[
              {
                title: 'Short',
                width: 140,
                render: (_, row) => (
                  <Select
                    size="small"
                    style={{ width: '100%' }}
                    value={row.shortId || undefined}
                    placeholder="Short"
                    options={[
                      ...shorts.map((s) => ({ value: s.id, label: `Short ${s.id}` })),
                      ...shots.map((s) => ({ value: s.id, label: `Shot ${s.id}` })),
                    ]}
                    onChange={(shortId) => patchStill(row.id, { shortId })}
                  />
                ),
              },
              {
                title: 'Khung cảnh',
                width: 140,
                render: (_, row) => (
                  <Input
                    size="small"
                    value={row.scene}
                    placeholder="vd. Bàn cơm"
                    onChange={(e) => patchStill(row.id, { scene: e.target.value })}
                  />
                ),
              },
              {
                title: 'Nhân vật',
                width: 160,
                render: (_, row) => {
                  const opts = (state.characters ?? []).map((c) => ({
                    value: c.id,
                    label: `${c.id}${c.name ? ` ${c.name}` : ''}`,
                  }));
                  if (row.charCode && !opts.some((o) => o.value === row.charCode)) {
                    opts.unshift({ value: row.charCode, label: row.charCode });
                  }
                  return (
                    <Select
                      size="small"
                      style={{ width: '100%' }}
                      value={row.charCode || undefined}
                      placeholder="CHAR-001"
                      options={opts}
                      showSearch
                      allowClear
                      onChange={(charCode) => patchStill(row.id, { charCode: charCode ?? '' })}
                    />
                  );
                },
              },
              {
                title: 'Ảnh',
                width: 240,
                render: (_, row) => {
                  const canon = (state.characters ?? []).find((c) => c.id === row.charCode);
                  const src =
                    (row.imageDataUrl?.startsWith('data:image') ? row.imageDataUrl : undefined) ||
                    (row.charCode ? canonDisplayOf(state, row.charCode) : undefined);
                  const fromCanon = characterCanonReady(canon);
                  return (
                    <Space align="start" wrap>
                      {src ? (
                        <img
                          src={src}
                          alt={row.charCode || 'ref'}
                          style={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 8 }}
                        />
                      ) : (
                        <Tag>Chưa có</Tag>
                      )}
                      {fromCanon ? <Tag color="green">Canon</Tag> : null}
                      <Button
                        size="small"
                        disabled={!fromCanon}
                        onClick={() => persistState(applyStillFromCanon(state, row.id))}
                      >
                        Dùng Canon
                      </Button>
                    </Space>
                  );
                },
              },
              {
                title: 'Đường dẫn máy',
                width: 260,
                render: (_, row) => (
                  <Input
                    size="small"
                    value={row.localPath ?? ''}
                    placeholder="E:\Famixa\ref\CHAR-001.png"
                    disabled={state.scriptLocked}
                    onChange={(e) =>
                      patchStill(row.id, { localPath: e.target.value, fileName: row.fileName || e.target.value })
                    }
                  />
                ),
              },
              {
                title: 'Ghi chú',
                render: (_, row) => (
                  <Input
                    size="small"
                    value={row.note ?? ''}
                    placeholder="mặt / outfit / cảm xúc"
                    onChange={(e) => patchStill(row.id, { note: e.target.value })}
                  />
                ),
              },
              {
                title: '',
                width: 56,
                render: (_, row) => (
                  <Button
                    type="link"
                    danger
                    size="small"
                    icon={<DeleteOutlined />}
                    onClick={() =>
                      persistState({ ...state, stills: stills.filter((s) => s.id !== row.id) })
                    }
                  />
                ),
              },
            ]}
          />
        </Card>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Nhận pack rồi mới gắn ảnh theo cảnh.
        </Typography.Paragraph>
      )}
                  </>
                ),
              },
              {
                key: 'shortWork',
                label: activeShort ? `Short đang làm · ${activeShort.id}` : 'Short đang làm',
                children: (
                  <>
      {shorts.length > 0 && activeShort && shortRun ? (
        <Card size="small" title={`Short · ${activeShort.id}`} style={{ marginBottom: 16 }}>
          {stillsForShort(stills, activeShort.id).some((s) => s.imageDataUrl) ? (
            <Space wrap style={{ marginBottom: 12 }}>
              {stillsForShort(stills, activeShort.id).map((s) =>
                s.imageDataUrl ? (
                  <div key={s.id} style={{ textAlign: 'center' }}>
                    <img
                      src={s.imageDataUrl}
                      alt={s.charCode}
                      style={{ width: 128, height: 128, objectFit: 'cover', borderRadius: 8 }}
                    />
                    <div style={{ fontSize: 11 }}>{s.charCode || 'CHAR'}</div>
                    {s.localPath ? (
                      <div style={{ fontSize: 10, maxWidth: 128, wordBreak: 'break-all' }}>{s.localPath}</div>
                    ) : null}
                  </div>
                ) : null,
              )}
            </Space>
          ) : (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message="Chưa gắn ảnh nhân vật cho short này — gắn ở bảng trên trước khi Turbo."
            />
          )}
          <Typography.Paragraph>
                <strong>Hook.</strong> {activeShort.hook}
              </Typography.Paragraph>
              <Typography.Paragraph type="secondary">{activeShort.visual}</Typography.Paragraph>
              <Typography.Paragraph code style={{ display: 'block', whiteSpace: 'pre-wrap' }}>
                {activeShort.motionPrompt}
              </Typography.Paragraph>
              <Alert
                type={shortRun.keyframeDataUrl ? 'success' : 'warning'}
                showIcon
                style={{ marginBottom: 12 }}
                message="Keyframe short = khung 9:16 cả cảnh, không phải crop mặt CHAR."
                description={
                  <Space align="start" wrap>
                    {shortRun.keyframeDataUrl ? (
                      <img
                        src={shortRun.keyframeDataUrl}
                        alt="KF short"
                        style={{ width: 90, height: 160, objectFit: 'cover', borderRadius: 8 }}
                      />
                    ) : null}
                    <input
                      type="file"
                      accept="image/*"
                      onChange={(e) => void onKeyframeFile(activeShort.id, e.target.files?.[0])}
                    />
                    <Button
                      loading={stillBusy === activeShort.id}
                      disabled={!keys.gemini}
                      onClick={() => {
                        const sc = (state.scenes ?? []).find((x) => x.id === activeShort.sceneId);
                        void generateKfFromCanon({
                          clipId: activeShort.id,
                          aspect: '9:16',
                          visual: activeShort.visual || activeShort.hook,
                          action: activeShort.motionPromptVi || activeShort.hook,
                          location: activeShort.scene || sc?.environment,
                          characterIds: activeShort.characterIds?.length
                            ? activeShort.characterIds
                            : activeShort.characters,
                        });
                      }}
                    >
                      Tạo KF từ Canon
                    </Button>
                  </Space>
                }
              />
              <Space wrap style={{ marginBottom: 12 }}>
                <Button
                  icon={<CopyOutlined />}
                  onClick={() => {
                    void navigator.clipboard.writeText(
                      compileRunwayPromptV1({ action: activeShort.motionPrompt }).text,
                    );
                    message.success('Đã copy I2V V1');
                  }}
                >
                  Copy prompt
                </Button>
                <Button
                  onClick={() =>
                    checkTurbo(
                      compileRunwayPromptV1({ action: activeShort.motionPrompt }).text,
                      shortRun.keyframeDataUrl,
                    )
                  }
                >
                  Kiểm tra trước (0 cr)
                </Button>
                <Button
                  icon={<ThunderboltOutlined />}
                  disabled={!ready || !shortRun.keyframeDataUrl}
                  loading={turboBusy === activeShort.id}
                  onClick={startShortTurbo}
                >
                  Gửi Turbo dọc · ~{runwayCredits(activeShort.seconds).credits} cr
                </Button>
                <InputNumber
                  min={0}
                  max={999}
                  value={shortRun.credits}
                  onChange={(n) => patchRun(activeShort.id, { credits: n ?? undefined })}
                  placeholder="credit"
                />
              </Space>
              <ClipWatch
                clipId={activeShort.id}
                url={shortRun.previewUrl}
                path={shortRun.localVideoPath}
                sessionSrc={sessionClips[activeShort.id]}
                history={shortRun.takeHistory}
                onUrl={(previewUrl) => patchRun(activeShort.id, { previewUrl })}
                onPath={(localVideoPath) => patchRun(activeShort.id, { localVideoPath })}
                onFile={(file) => onClipFile(activeShort.id, file)}
              />
              {REVIEW_AXES.map((a) => (
                <div key={a.id}>
                  <Checkbox
                    checked={Boolean(shortRun.review?.[a.id])}
                    onChange={(e) =>
                      patchRun(activeShort.id, {
                        status: 'reviewed',
                        review: { ...shortRun.review, [a.id]: e.target.checked },
                      })
                    }
                  >
                    {a.label}
                  </Checkbox>
                </div>
              ))}
              <div style={{ marginTop: 12 }}>
                <Button type="primary" icon={<CheckOutlined />} onClick={lockShort}>
                  Khóa short
                </Button>
              </div>
        </Card>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {shorts.length ? 'Bấm một short ở nhóm Short 9:16.' : 'Pack chưa có short.'}
        </Typography.Paragraph>
      )}
                  </>
                ),
              },
                ]
              : []),
              ...(studioPane === 'advanced'
                ? [
              {
                key: 'long',
                label: active ? `I2V V1 · ${active.id}` : 'I2V V1',
                children: (
                  <>
      <Typography.Paragraph type="secondary">
        Confirm luôn gửi <strong>RUNWAY_PROMPT_V1</strong> (motion + camera). Không dán V02 / negative.
      </Typography.Paragraph>
      {active && run ? (
        <>
                  <Input.TextArea
                    rows={4}
                    value={compileI2vPrompt(state, active, run.shotAction ?? '', videoContext)}
                    readOnly
                    spellCheck={false}
                    style={{ marginTop: 4, marginBottom: 12, fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
                  />
                  <Space wrap>
                    <Button
                      icon={<CopyOutlined />}
                      onClick={() => {
                        void navigator.clipboard.writeText(
                          compileI2vPrompt(state, active, run.shotAction ?? '', videoContext),
                        );
                        message.success('Đã copy I2V V1');
                      }}
                    >
                      Copy prompt
                    </Button>
                    <Button
                      onClick={() =>
                        checkTurbo(
                          compileI2vPrompt(state, active, run.shotAction ?? '', videoContext),
                          run.keyframeDataUrl,
                        )
                      }
                    >
                      Kiểm tra I2V (0 cr)
                    </Button>
                    <Button onClick={() => setStudioPane('studio')}>Về Video Studio</Button>
                  </Space>
        </>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Chọn shot trên Video Studio để xem prompt sẽ gửi.
        </Typography.Paragraph>
      )}
                  </>
                ),
              },
                ]
              : []),
            ]}
          />
        ) : null}
      </ContentFamixaStudioView>
          </>
        }
      />
      </>
      )}

      <Modal
        title={watchClip?.title || 'Xem video'}
        open={Boolean(watchClip)}
        footer={null}
        width={880}
        destroyOnClose
        onCancel={() => {
          setWatchClip((prev) => {
            if (prev?.url) URL.revokeObjectURL(prev.url);
            return undefined;
          });
        }}
      >
        {watchClip ? (
          <video
            src={watchClip.url}
            controls
            autoPlay
            playsInline
            style={{ width: '100%', maxHeight: '70vh', background: '#111' }}
          />
        ) : null}
      </Modal>
      <Drawer
        title="Continuity Memory"
        open={memOpen}
        onClose={() => setMemOpen(false)}
        width={480}
      >
        <Typography.Paragraph>
          Continuity = quy tắc cả cảnh. Shot lock = video cụ thể. Hai thứ khác nhau.
        </Typography.Paragraph>
        <ContinuityMemoryCard
          lock={lockMem}
          prevShot={prevLocked}
          onChange={(continuity) => persistState({ ...state, continuity })}
        />
      </Drawer>
    </div>
  );
}
