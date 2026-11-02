import { useEffect, useMemo, useRef, useState, type ComponentProps } from 'react';
import { Button, Select, message } from 'antd';
import { FamixaMotionEnginePicker } from '../FamixaMotionEnginePicker';
import { FamixaQualityTierPicker } from '../FamixaQualityTierPicker';
import { qualityTierOf, readQualityTier } from '../famixa-quality-tier';
import { FamixaDirectorTimingPicker } from '../FamixaDirectorTimingPicker';
import type { FamixaMotionEngine } from '../famixa-motion-engine';
import type { DirectorVideoModelDisplay } from '../famixa-director-video-model-display';
import type { DirectorProductionDurationSec, DirectorProductionTiming } from '../famixa-director-timing';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from '../content-famixa-series';
import { bindShotChecklist, explicitArtifactDecision, reviewTargetForDecision, type OsPlaybackIdentity } from '../famixa-short-acceptance';
import { linesForShot, lipsyncNeedsSpeakerPick, lipsyncSpeakerLabel } from '../content-famixa-dialogue-map';
import { canMutateLiveSpeakerTarget, lipsyncSpeakerPickOf, lipsyncSpeakerPinRejectedByFal } from '../famixa-lipsync-speaker-target';
import { stillOf } from '../content-famixa-shot-catalog';
import { kfPixelsOf } from '../content-famixa-kf-store';
import { existingScenePipelineStatus } from '../kit-video-visual-mode';
import type { ProductionUserMode } from '../kit-video-production-ux';
import { confirmLeaveActingBeat, shotActingSpeakerOf, type ActingBeatEditorHandle } from './ShotProductionActingBeat';
import { ShotProductionActingBeatCard } from './ShotProductionActingBeatCard';
import { ShotProductionSmoothnessCard } from './ShotProductionSmoothnessCard';
import {
  applyEditorialPreset,
  smoothnessOf,
  type EditorialPreset,
  type EpisodeSmoothness,
  type ShotCoverage,
} from '../famixa-shot-smoothness-contract';
import { ShotProductionCard } from './ShotProductionCard';
import { dataUriHash } from '../content-famixa-runway-pipe';
import { currentApprovedPictureHash } from '../content-famixa-picture-pixel-invariant';
import { playableMotionTakeOf } from './ShotProductionArtifacts';
import { picturePixelHashOf } from './ShotProductionExecution';
import {
  resolveCurrentLipsyncPreviewBinding,
  resolveCurrentProductionVideoSource,
} from '../famixa-lipsync-motion-authority';
import { FAL_LIPSYNC_TIERS, lipsyncFalHasNoOutput, lipsyncResumeTaskId, normalizeLipsyncModel, shouldResumeLipsync, type FalLipsyncModel } from '../content-famixa-prod-v2';
import {
  DIRECTOR_LIPSYNC_NEW_LABEL,
  DIRECTOR_LIPSYNC_RESUME_LABEL,
  deskExposeLipsyncRemakeCta,
  deskLipsyncOutputCurrent,
  directorPictureVideoSurface,
  shotPipelineBusy,
  shotQaMissing,
  type StaffShotAction,
} from './ShotProductionCta';
import { snapshotOfShot } from './ShotProductionState';
import { shotProductionOrchestrationEnabled } from './ShotProductionIntent';
import { ShotProductionNavigator, resolveLaneAShotId, sceneShotsOf } from './ShotProductionNavigator';
import {
  applyProductionOsActionAuthority,
  isOsActionAvailable,
  looksLikeProductionShotCode,
  resolveShotWorkspaceIdentity,
  formatShotIdentityError,
  OS_ACTION_FOR_DESK,
  productionActionForDeskCta,
  visibleProductionOsCatalogActions,
  formatOfficialVideoProviderLine,
  type OsDeskCtaAction,
} from '../production-os-action-authority';
import type { AuthoritativeVideoTakeMeta, ProductionShotActions, ProductionVoiceArtifactMeta } from '@/shared/api/content.api';
import { directorNoteWithProductionStill } from '../famixa-production-still';
import { nextShotProductionCommand, produceShot } from './ShotProductionOrchestrator';
import {
  directorProductionCaption,
  directorPreviewStillUrl,
  directorShowsStillPreview,
  resolveShotPreviewKind,
  ShotProductionPreview,
} from './ShotProductionPreview';
import { ShotProductionTechnical } from './ShotProductionTechnical';
import { DIRECTOR_SHOT_PHASES, directorShotPhase } from '../kit-video-director-nav';
import {
  artifactPresentOfProof,
  asPlayableVideoBlob,
  finalArtifactIdentityOf,
  loadFinalArtifact,
  proveFinalArtifact,
} from './ShotProductionFinalStore';
import { fetchContentSeriesFalVideo, fetchContentSeriesVeoVideo } from '@/shared/api/content.api';
import { isVeoMotionTaskId } from '../famixa-veo-take-playback';
import { dialogueFloorOf } from '../famixa-shot-production-timing';
import { deskAuthVideoSrc, deskFalPlayableForShot, isFalLipsyncTaskId, lipsyncRunFileMissing } from '../famixa-fal-lipsync-playback';

export type PictureGate = { allowed: boolean; reason?: string; hint?: string; kind?: 'picture' | 'qa' };

function displayableStillUrl(url?: string) {
  const v = (url || '').trim();
  if (v.startsWith('data:image') || v.startsWith('blob:')) return v;
  if (/^https?:\/\//i.test(v)) return v;
  return undefined;
}

export function ContentFamixaShotProductionWorkspace({
  shots,
  state,
  ttsFiles,
  busyShotId,
  motionBusyId,
  lipsyncBusyId,
  mixBusyId,
  mode = 'staff',
  selectedShotId,
  focusTick,
  onSelectShot,
  pictureGate,
  onOpenVoice,
  hasVoiceFile,
  readLive,
  onEnsureVoice,
  onEnsurePicture,
  onApprovePicture,
  onApproveVideo,
  onEnsureLipsync,
  onAttachLipsync,
  onEnsureMix,
  onWatch,
  onQa,
  onActingBeat,
  onCoverage,
  onEditorial,
  onSmoothness,
  onAcceptExisting,
  onRetryCamera,
  onPickLipsyncSpeaker,
  episodeLabel,
  engine,
  onEngine,
  engineKeys,
  osVideoModel,
  osDirectorTiming,
  onDirectorTiming,
  directorTimingBusy,
  directorSelectionBusy,
  lipsyncModel,
  onLipsyncModel,
  osPreviewUrl,
  osKeyframeUrl,
  osPictureBlock,
  osPaidStageLedger,
  osPreviewIdentity,
  osAuthoritativeVideo,
  osAuthoritativeVoice,
  osLipsyncArtifactId,
  osVoiceUrl,
  osActions,
  onExecuteOsAction,
  visualStill,
  onVisualApprove,
  onVisualVariant,
  onVisualLibrary,
  onVisualPhoto,
  visualBrandChoices,
  visualBrandChoice,
  onVisualBrandChoice,
  onPrepareOsVoice,
  onEnsureMotion: _onEnsureMotion,
}: {
  shots: FamixaSeriesShot[];
  state: SeriesPilotState;
  ttsFiles: Record<string, { url: string; fileName: string }>;
  busyShotId?: string;
  motionBusyId?: string;
  lipsyncBusyId?: string;
  mixBusyId?: string;
  mode?: ProductionUserMode;
  selectedShotId?: string;
  focusTick?: number;
  onSelectShot?: (shotId: string) => void;
  pictureGate?: (shotId: string) => PictureGate;
  onOpenVoice?: () => void;
  hasVoiceFile?: (lineId: string) => boolean;
  readLive?: () => { state: SeriesPilotState; ttsFiles: Record<string, { url: string; fileName: string }> };
  onEnsureVoice: (shotId: string) => Promise<void> | void;
  onEnsurePicture: (shotId: string) => Promise<void> | void;
  onApprovePicture: (shotId: string, liveStill?: string) => void;
  onApproveVideo?: (shotId: string) => void;
  onEnsureMotion: (shotId: string, liveStill?: string) => Promise<void> | void;
  onEnsureLipsync: (shotId: string, opts?: { resume?: boolean; forceNew?: boolean }) => Promise<void> | void;
  onAttachLipsync?: (shotId: string) => void;
  onEnsureMix: (shotId: string) => Promise<void> | void;
  onWatch?: (shotId: string) => void;
  onQa?: (shotId: string, next: NonNullable<SeriesShotRun['shotQa']>) => void;
  onActingBeat?: (shotId: string, next: NonNullable<FamixaSeriesShot['actingBeat']>) => void;
  onCoverage?: (shotId: string, next: ShotCoverage) => void;
  onEditorial?: (shotId: string, next: ReturnType<typeof applyEditorialPreset>) => void;
  onSmoothness?: (next: EpisodeSmoothness) => void;
  onAcceptExisting?: (shotId: string) => void;
  onRetryCamera?: (shotId: string) => void;
  onPickLipsyncSpeaker?: (
    shotId: string,
    pt: { x: number; y: number; width: number; height: number; characterId?: string; frameNumber?: number },
  ) => void;
  episodeLabel?: string;
  engine?: FamixaMotionEngine;
  onEngine?: (next: FamixaMotionEngine) => void;
  engineKeys?: { runway?: boolean; fal?: boolean; veo?: boolean };
  osVideoModel?: DirectorVideoModelDisplay;
  osDirectorTiming?: DirectorProductionTiming;
  onDirectorTiming?: (durationSec: DirectorProductionDurationSec) => void;
  directorTimingBusy?: boolean;
  directorSelectionBusy?: boolean;
  lipsyncModel?: FalLipsyncModel;
  /** Production OS VISIBLE blob. When set, desk must not use takeUrl / covering / latest playable. */
  osPreviewUrl?: string;
  /** Current production still. Shown instead of the older mix. */
  osKeyframeUrl?: string;
  /** Why the current still cannot be the picture in use. */
  osPictureBlock?: string;
  /** Paid attempts already used for each stage of this shot. */
  osPaidStageLedger?: string;
  /** Artifact id and hash of the bytes behind osPreviewUrl. Review binds this pair. */
  osPreviewIdentity?: OsPlaybackIdentity | null;
  /** Production OS ACCEPTED:VIDEO_TAKE. Video complete authority. */
  osAuthoritativeVideo?: AuthoritativeVideoTakeMeta;
  /** Production OS CURRENT:VOICE. Voice complete authority. */
  osAuthoritativeVoice?: ProductionVoiceArtifactMeta;
  /** Production OS CURRENT/ACCEPTED:LIPSYNC. LipSync complete authority. */
  osLipsyncArtifactId?: string | null;
  /** Production OS CURRENT:VOICE media blob URL. Never IDB. */
  osVoiceUrl?: string;
  /** GET /production/.../actions. CREATE_VIDEO CTA authority. */
  osActions?: ProductionShotActions;
  onExecuteOsAction?: (shotId: string, action: string) => void;
  visualStill?: { phase: string; detail: string; approved?: boolean; undrawable?: string; url?: string; variantUrl?: string; decision?: string; searchDecision?: string };
  onVisualApprove?: (shotId: string) => void;
  onVisualVariant?: (shotId: string) => void;
  onVisualLibrary?: (shotId: string) => void;
  onVisualPhoto?: (shotId: string) => void;
  visualBrandChoices?: { code: string; name: string }[];
  visualBrandChoice?: string;
  onVisualBrandChoice?: (code: string) => void;
  onPrepareOsVoice?: (shotId: string) => void;
  onLipsyncModel?: (next: FalLipsyncModel) => void;
}) {
  void _onEnsureMotion;
  const directorDesk = mode === 'director';
  const [artifact, setArtifact] = useState<Record<string, boolean>>({});
  const [finalUrl, setFinalUrl] = useState<Record<string, string>>({});
  const [advancing, setAdvancing] = useState<string>();
  const [hold, setHold] = useState<PictureGate>();
  const [localShotId, setLocalShotId] = useState<string>();
  const [veoPlaySrc, setVeoPlaySrc] = useState<string>();
  const [veoPlayTaskId, setVeoPlayTaskId] = useState<string>();
  const [falPlaySrc, setFalPlaySrc] = useState<string>();
  const [falPlayShotId, setFalPlayShotId] = useState<string>();
  const [falPlayTaskId, setFalPlayTaskId] = useState<string>();
  const [falFileMissing, setFalFileMissing] = useState(false);
  const falBlobRef = useRef<string | undefined>(undefined);
  const videoRef = useRef<HTMLVideoElement>(null);
  const studioRef = useRef<HTMLElement>(null);
  const actingEditorRef = useRef<ActingBeatEditorHandle | undefined>(undefined);
  useEffect(() => {
    setLocalShotId(undefined);
  }, [state.buildId]);

  const requestedShotId = (selectedShotId || localShotId || '').trim();
  const currentId = resolveLaneAShotId(shots, requestedShotId || undefined);
  const sceneShots = useMemo(() => sceneShotsOf(shots, currentId), [shots, currentId]);
  const current =
    sceneShots.find((s) => s.id === currentId) ||
    shots.find((s) => s.id === currentId) ||
    (requestedShotId && looksLikeProductionShotCode(requestedShotId) ? undefined : shots[0]);
  const shotIdentity = resolveShotWorkspaceIdentity({
    requestedBuildId: state.buildId,
    requestedShotId: requestedShotId || current?.id,
    loadedBuildId: state.buildId,
    loadedShotId: current?.id,
  });

  useEffect(() => {
    if (!focusTick) return;
    studioRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [focusTick, currentId]);

  const currentRun = current ? shotRunOf(state, current) : undefined;
  const speechFloorSec = current ? dialogueFloorOf(state, current) : 0;
  const motionPlayback = current
    ? resolveCurrentProductionVideoSource({
        run: currentRun!,
        shotId: current.id,
        fallbackMotionUrl: (currentRun?.takeUrl || currentRun?.previewUrl || '').trim() || undefined,
        fallbackMotionTaskId: (currentRun?.turboTaskId || '').trim() || undefined,
        speechFloorSec,
      })
    : undefined;
  const motionTaskId = (motionPlayback?.motionTaskId || '').trim();
  const motionFetchUrl = (motionPlayback?.motionUrl || '').trim();
  const lipsyncTaskId = (currentRun?.lipsyncTaskId || '').trim();

  useEffect(() => {
    if (!current?.id) {
      setVeoPlaySrc(undefined);
      setVeoPlayTaskId(undefined);
      return;
    }
    const taskId = motionTaskId;
    if (!isVeoMotionTaskId(taskId)) {
      setVeoPlaySrc(undefined);
      setVeoPlayTaskId(undefined);
      return;
    }
    let dead = false;
    let obj = '';
    setVeoPlaySrc(undefined);
    setVeoPlayTaskId(undefined);
    void fetchContentSeriesVeoVideo(taskId)
      .then((blob) => {
        if (dead || blob.size < 80) return;
        obj = URL.createObjectURL(blob);
        setVeoPlaySrc(obj);
        setVeoPlayTaskId(taskId);
      })
      .catch(() => {
        if (!dead) {
          setVeoPlaySrc(undefined);
          setVeoPlayTaskId(undefined);
        }
      });
    return () => {
      dead = true;
      if (obj) URL.revokeObjectURL(obj);
    };
  }, [current?.id, motionTaskId, motionFetchUrl]);

  useEffect(() => {
    if (falBlobRef.current) {
      URL.revokeObjectURL(falBlobRef.current);
      falBlobRef.current = undefined;
    }
    setFalPlaySrc(undefined);
    setFalPlayShotId(undefined);
    setFalPlayTaskId(undefined);
    setFalFileMissing(false);
    if (!current?.id || !isFalLipsyncTaskId(lipsyncTaskId)) return;
    const shotId = current.id;
    const taskId = lipsyncTaskId;
    let dead = false;
    void fetchContentSeriesFalVideo(taskId)
      .then((blob) => {
        if (dead) return;
        if (blob.size < 80) {
          setFalFileMissing(true);
          return;
        }
        const obj = URL.createObjectURL(blob);
        falBlobRef.current = obj;
        setFalPlaySrc(obj);
        setFalPlayShotId(shotId);
        setFalPlayTaskId(taskId);
        setFalFileMissing(false);
      })
      .catch(() => {
        if (!dead) {
          setFalFileMissing(true);
          setFalPlaySrc(undefined);
          setFalPlayShotId(undefined);
          setFalPlayTaskId(undefined);
        }
      });
    return () => {
      dead = true;
      if (falBlobRef.current) {
        URL.revokeObjectURL(falBlobRef.current);
        falBlobRef.current = undefined;
      }
    };
  }, [current?.id, lipsyncTaskId]);

  useEffect(() => {
    let alive = true;
    const created: string[] = [];
    void Promise.all(
      shots.map(async (shot) => {
        const stored = await loadFinalArtifact(shot.id);
        const proof = proveFinalArtifact(stored, finalArtifactIdentityOf(state, shot));
        const blob = proof.ok && stored.kind === 'record' ? asPlayableVideoBlob(stored.record.blob) : undefined;
        const href = blob ? URL.createObjectURL(blob) : '';
        return [shot.id, artifactPresentOfProof(proof), href] as const;
      }),
    ).then((rows) => {
      for (const row of rows) {
        if (row[2]) created.push(row[2]);
      }
      if (!alive) {
        created.forEach((u) => URL.revokeObjectURL(u));
        return;
      }
      setArtifact(Object.fromEntries(rows.map((r) => [r[0], r[1]])));
      setFinalUrl(Object.fromEntries(rows.filter((r) => r[2]).map((r) => [r[0], r[2]])));
    });
    return () => {
      alive = false;
      created.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [shots.map((s) => `${s.id}:${shotRunOf(state, s).shotProduction?.assembleFp || ''}`).join('|')]);

  const liveOf = () => readLive?.() ?? { state, ttsFiles };

  const snapOf = (shot: FamixaSeriesShot) => {
    const live = liveOf();
    return snapshotOfShot(live.state, shot, live.ttsFiles, {
      motionBusy: motionBusyId === shot.id,
      lipsyncBusy: lipsyncBusyId === shot.id,
      mixBusy: mixBusyId === shot.id,
      artifactPresent: artifact[shot.id],
      hasVoiceFile: hasVoiceFile || ((id) => Boolean(live.ttsFiles[id]?.url)),
      pictureUnusable: existingScenePipelineStatus(shotRunOf(live.state, shot)) === 'INVALID_REFERENCE_PIPELINE',
    });
  };

  const selectShot = (shotId: string) => {
    if (shotId === currentId) return;
    if (actingEditorRef.current?.dirty()) {
      if (!confirmLeaveActingBeat(true, (message) => window.confirm(message))) return;
      actingEditorRef.current.save();
    }
    setHold(undefined);
    setLocalShotId(shotId);
    onSelectShot?.(shotId);
  };

  const tryPicture = (shotId: string) => {
    const gate = pictureGate?.(shotId);
    if (gate && !gate.allowed) {
      setHold({ ...gate, kind: 'picture' });
      setAdvancing(undefined);
      message.warning([gate.reason, gate.hint].filter(Boolean).join(' '));
      return false;
    }
    setHold(undefined);
    return true;
  };

  const produce = async (shotId: string) => {
    const shot = shots.find((s) => s.id === shotId);
    if (!shot) return;
    const ahead = nextShotProductionCommand(snapOf(shot));
    if (ahead.type === 'ENSURE_VOICE') {
      if (onPrepareOsVoice) onPrepareOsVoice(shotId);
      else void Promise.resolve(onEnsureVoice(shotId)).catch(() => undefined);
      return;
    }
    if (ahead.type === 'ENSURE_PICTURE') {
      actingEditorRef.current?.save?.();
      if (!tryPicture(shotId)) return;
      void onEnsurePicture(shotId);
      return;
    }
    setAdvancing(shotId);
    try {
      await produceShot(
        snapOf(shot),
        {
          ensureVoice: () => Promise.resolve(onEnsureVoice(shotId)),
          ensurePicture: () => Promise.resolve(tryPicture(shotId) ? onEnsurePicture(shotId) : undefined),
          ensureMotion: () => Promise.resolve(),
          ensureLipsync: () => Promise.resolve(onEnsureLipsync(shotId, lipsyncActionOpts(shotId))),
          ensureMix: () => Promise.resolve(onEnsureMix(shotId)),
        },
        () => snapOf(shot),
      );
    } catch {
      setAdvancing(undefined);
      return;
    }
    const after = nextShotProductionCommand(snapOf(shot));
    if (after.type === 'ENSURE_VOICE' || after.kind !== 'ENSURE') setAdvancing(undefined);
  };

  useEffect(() => {
    if (!advancing) return;
    const shot = shots.find((s) => s.id === advancing);
    if (!shot) return;
    const cmd = nextShotProductionCommand(snapOf(shot));
    if (cmd.type === 'ENSURE_PICTURE') {
      void produce(shot.id);
      return;
    }
    if (cmd.kind !== 'ENSURE' && cmd.kind !== 'WAIT') setAdvancing(undefined);
  }, [state, ttsFiles, advancing, motionBusyId, lipsyncBusyId]);

  const lipsyncActionOpts = (shotId: string) => {
    const shot = shots.find((s) => s.id === shotId);
    const liveRun = shotRunOf(liveOf().state, shot || ({ id: shotId, status: 'story_locked' } as FamixaSeriesShot));
    const missing = shotId === current?.id ? falFileMissing || lipsyncRunFileMissing(liveRun) : lipsyncRunFileMissing(liveRun);
    if (lipsyncFalHasNoOutput(liveRun.lipsyncError)) return { forceNew: true as const };
    if (shot && deskLipsyncOutputCurrent(snapOf(shot)) && !snapOf(shot).lipSyncStale) {
      return { forceNew: true as const };
    }
    if (
      shouldResumeLipsync({ ...liveRun, lipsyncFileMissing: missing }) ||
      lipsyncResumeTaskId(liveRun, lipsyncModel)
    ) {
      return { resume: true as const };
    }
    return undefined;
  };

  const handleAction = (shotId: string, action: StaffShotAction) => {
    const pipe = shotPipelineBusy({
      shotId,
      busyShotId,
      motionBusyId,
      lipsyncBusyId,
    });
    if (action === 'picture' && (pipe.pictureBusy || pipe.motionBusy)) {
      message.warning(
        pipe.motionBusy ? 'Đang tạo video — không vẽ hình mới.' : 'Đang vẽ/chấm hình — chờ xong, không bấm lần 2.',
      );
      return;
    }
    if (action === 'motion' && pipe.pictureBusy) {
      message.warning('Đang vẽ/chấm hình. Đợi xong rồi mới Tạo video.');
      return;
    }
    if (action === 'accept-existing') {
      onAcceptExisting?.(shotId);
      return;
    }
    if (action === 'edit-input') {
      actingEditorRef.current?.save?.();
      studioRef.current?.querySelector<HTMLElement>('[data-acting-beat]')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      return;
    }
    if ((action === 'picture' || action === 'os-create-image') && (osPictureBlock || '').trim()) {
      const permittedRecreate = action === 'os-create-image'
        && isOsActionAvailable(osActions, 'CREATE_IMAGE')
        && (osPictureBlock || '').includes('Cần giấy phép');
      if (!permittedRecreate) {
        message.warning((osPictureBlock || '').trim());
        return;
      }
    }
    if ((action === 'picture' || action === 'os-create-image') && osKeyframeUrl && isOsActionAvailable(osActions, 'CREATE_IMAGE')) {
      message.info('Hình mới đã có trên shot này.');
      return;
    }
    if (action === 'picture') {
      actingEditorRef.current?.save?.();
      if (isOsActionAvailable(osActions, 'CREATE_IMAGE')) {
        onExecuteOsAction?.(shotId, 'CREATE_IMAGE');
        return;
      }
      if (onVisualLibrary) {
        onVisualLibrary(shotId);
        return;
      }
      void onEnsurePicture(shotId);
      return;
    }
    if (action in OS_ACTION_FOR_DESK) {
      if (action === 'os-create-voice') {
        if (onPrepareOsVoice) onPrepareOsVoice(shotId);
        else void Promise.resolve(onEnsureVoice(shotId)).catch(() => undefined);
        return;
      }
      if (action === 'os-create-image') actingEditorRef.current?.save?.();
      onExecuteOsAction?.(shotId, OS_ACTION_FOR_DESK[action as OsDeskCtaAction]);
      return;
    }
    if (action === 'retry-camera') {
      if (onRetryCamera) {
        onRetryCamera(shotId);
        return;
      }
      return;
    }
    if (action === 'approve-picture') {
      const liveShot = shots.find((s) => s.id === shotId);
      const liveRun = liveShot ? shotRunOf(liveOf().state, liveShot) : undefined;
      const livePixels =
        (liveRun?.keyframeDataUrl?.startsWith('data:image') ? liveRun.keyframeDataUrl : undefined) ||
        kfPixelsOf(shotId);
      if (!livePixels?.startsWith('data:image')) {
        if (!tryPicture(shotId)) return;
        void onEnsurePicture(shotId);
        return;
      }
      onApprovePicture(shotId, livePixels);
    }
    else if (action === 'approve-video' || action === 'use-current-take') onApproveVideo?.(shotId);
    else if (action === 'motion') return;
    else if (action === 'lipsync') void onEnsureLipsync(shotId, lipsyncActionOpts(shotId));
    else if (action === 'mix') void onEnsureMix(shotId);
    else if (action === 'watch') {
      onWatch?.(shotId);
    }
    else if (action === 'qa') {
      const shot = shots.find((s) => s.id === shotId);
      if (!shot) return;
      const live = liveOf();
      const snap = snapshotOfShot(live.state, shot, live.ttsFiles, {
        motionBusy: motionBusyId === shotId,
        lipsyncBusy: lipsyncBusyId === shotId,
        mixBusy: mixBusyId === shotId,
        artifactPresent: artifact[shotId],
        hasVoiceFile: hasVoiceFile || ((id) => Boolean(live.ttsFiles[id]?.url)),
      });
      const missing = shotQaMissing(shotRunOf(live.state, shot).shotQa, !snap.isSilent, {
        afterLipsync: snap.lipSyncReady,
        afterMix: snap.mixReady,
      });
      if (missing.length) {
        setHold({
          allowed: false,
          kind: 'qa',
          reason: 'Chưa đủ kiểm tra Shot.',
          hint: `Đánh dấu: ${missing.join(', ')}.`,
        });
        return;
      }
      setHold(undefined);
      if (!snap.lipSyncReady) {
        void onEnsureLipsync(shotId, lipsyncActionOpts(shotId));
        return;
      }
      if (!snap.mixReady) void onEnsureMix(shotId);
    }
    else if (action !== 'blocked') void produce(shotId);
  };

  const retryOf = (shotId: string) => (stage: 'voice' | 'picture' | 'motion' | 'lipsync' | 'mix') => {
    if (stage === 'voice') {
      if (onPrepareOsVoice) onPrepareOsVoice(shotId);
      else if (isOsActionAvailable(osActions, 'CREATE_VOICE')) onExecuteOsAction?.(shotId, 'CREATE_VOICE');
      else void Promise.resolve(onEnsureVoice(shotId)).catch(() => undefined);
    }
    if (stage === 'picture') {
      if (isOsActionAvailable(osActions, 'CREATE_IMAGE')) onExecuteOsAction?.(shotId, 'CREATE_IMAGE');
      else {
        if (!tryPicture(shotId)) return;
        void onEnsurePicture(shotId);
      }
    }
    if (stage === 'motion') {
      if (isOsActionAvailable(osActions, 'REGENERATE')) onExecuteOsAction?.(shotId, 'REGENERATE');
      return;
    }
    if (stage === 'lipsync') {
      if (isOsActionAvailable(osActions, 'CREATE_LIPSYNC')) onExecuteOsAction?.(shotId, 'CREATE_LIPSYNC');
      else void onEnsureLipsync(shotId, lipsyncActionOpts(shotId));
    }
    if (stage === 'mix') {
      if (isOsActionAvailable(osActions, 'MIX_VIDEO')) onExecuteOsAction?.(shotId, 'MIX_VIDEO');
      else void onEnsureMix(shotId);
    }
  };

  const renderCard = (shot: FamixaSeriesShot, index: number, opts?: { hideIdentity?: boolean; tone?: 'director' | 'staff' }) => {
    const snap = snapOf(shot);
    const forceNewLipsync = lipsyncFalHasNoOutput(shotRunOf(liveOf().state, shot).lipsyncError);
    return (
      <ShotProductionCard
        key={shot.id}
        label={`SHOT ${String(index + 1).padStart(2, '0')}`}
        title={snap.intent.action}
        snap={snap}
        forceNewLipsync={forceNewLipsync}
        shotQa={shotRunOf(liveOf().state, shot).shotQa}
        busy={busyShotId === shot.id || advancing === shot.id}
        tone={opts?.tone || (directorDesk ? 'director' : 'staff')}
        showIdentity={!opts?.hideIdentity}
        holdReason={hold?.reason}
        holdHint={hold?.hint}
        osActions={osActions}
        onAction={(action) => handleAction(shot.id, action)}
        reviewTarget={reviewTargetForDecision(
          shotRunOf(liveOf().state, shot),
          osPreviewIdentity,
          looksLikeProductionShotCode(shot.id),
        )}
        onReviewDecision={
          onQa
            ? (decision) => {
                const run = shotRunOf(liveOf().state, shot);
                const target = reviewTargetForDecision(run, osPreviewIdentity, looksLikeProductionShotCode(shot.id));
                const hash = (osPreviewIdentity?.contentHash || '').trim();
                const result = explicitArtifactDecision({
                  decision,
                  target,
                  hash,
                  checklist: shotRunOf(liveOf().state, shot).shotQa ?? {},
                });
                if (!result.ok) {
                  setHold({ allowed: false, kind: 'qa', reason: result.detail });
                  return;
                }
                setHold(undefined);
                onQa(shot.id, result.qa);
              }
            : undefined
        }
        onQa={
          onQa
            ? (next) => {
                const missing = shotQaMissing(next, !snapOf(shot).isSilent, {
                  afterLipsync: snapOf(shot).lipSyncReady,
                  afterMix: snapOf(shot).mixReady,
                });
                const run = shotRunOf(liveOf().state, shot);
                const target = reviewTargetForDecision(run, osPreviewIdentity, looksLikeProductionShotCode(shot.id));
                const hash = (osPreviewIdentity?.contentHash || '').trim();
                const stamped = bindShotChecklist(next, target, hash, missing.length === 0);
                if (!missing.length) setHold(undefined);
                onQa(shot.id, stamped);
              }
            : undefined
        }
        onRetry={retryOf(shot.id)}
      />
    );
  };

  if (!current || !shotIdentity.ok) {
    const missing = requestedShotId && looksLikeProductionShotCode(requestedShotId) && !current;
    return (
      <section className="fx-desk" data-shot-identity-error="1">
        <h2>Video Studio</h2>
        <pre className="fx-desk__note" data-shot-identity="1">
          {formatShotIdentityError({
            reason: missing ? 'SHOT_NOT_FOUND' : shotIdentity.reason || 'IDENTITY_MISMATCH',
            requestedBuildId: state.buildId,
            requestedShotId: requestedShotId || current?.id,
            loadedBuildId: state.buildId,
            loadedShotId: current?.id,
          })}
        </pre>
      </section>
    );
  }

  const snap = snapOf(current);
  const run = shotRunOf(state, current);
  const live = liveOf();
  const lines = linesForShot(live.state, current);
  const dialogue = lines.map((l) => l.text).filter(Boolean).join(' ');
  const osVoiceReady = Boolean(
    osAuthoritativeVoice &&
      !osAuthoritativeVoice.empty &&
      (osAuthoritativeVoice.artifactId || '').trim(),
  );
  const osLipsyncReady = Boolean((osLipsyncArtifactId || '').trim());
  const osAcceptedVideo = Boolean(
    osAuthoritativeVideo &&
      !osAuthoritativeVideo.empty &&
      (osAuthoritativeVideo.role || '').trim() === 'VIDEO_TAKE' &&
      (osAuthoritativeVideo.acceptanceStatus || '').trim() === 'ACCEPTED' &&
      (osAuthoritativeVideo.artifactId || '').trim(),
  );
  const osCurrentVideo = Boolean(
    !osAcceptedVideo &&
      osAuthoritativeVideo &&
      !osAuthoritativeVideo.empty &&
      (osAuthoritativeVideo.role || '').trim() === 'VIDEO_TAKE' &&
      (osAuthoritativeVideo.acceptanceStatus || '').trim() === 'CURRENT' &&
      (osAuthoritativeVideo.artifactId || '').trim(),
  );
  const osPreviewAuthority = looksLikeProductionShotCode(current.id) || osAcceptedVideo;
  const voiceUrl = osPreviewAuthority
    ? (osVoiceUrl || '').trim() || undefined
    : (osVoiceUrl || '').trim() || (osVoiceReady ? undefined : lines.map((l) => live.ttsFiles[l.id]?.url).find(Boolean));
  const osVisible = (osPreviewUrl || '').trim() || undefined;
  const currentMotionArt = snap.execution?.currentArtifacts.motion;
  const visibleMotionArt = snap.execution?.visibleArtifacts.motion;
  const currentTakeUrl = osVisible
    ? osVisible
    : osPreviewAuthority
      ? undefined
      : currentMotionArt?.validity === 'CURRENT' ? (currentMotionArt.url || '').trim() || undefined : undefined;
  const matchingTake = !osVisible && currentTakeUrl
    ? playableMotionTakeOf(run, picturePixelHashOf(run, current.id), run.pictureRevisionId)
    : undefined;
  const fallbackMotionUrl = osVisible
    ? osVisible
    : osPreviewAuthority
      ? undefined
      : (
          currentTakeUrl ||
          visibleMotionArt?.url ||
          run.takeUrl ||
          run.previewUrl ||
          ''
        ).trim() || undefined;
  const lipsyncPlayable = osVisible || osPreviewAuthority
    ? undefined
    : deskFalPlayableForShot({
        shotId: current.id,
        taskId: lipsyncTaskId,
        fetchedShotId: falPlayShotId,
        fetchedTaskId: falPlayTaskId,
        blobUrl: falPlaySrc,
      });
  const productionSource = osVisible
    ? { kind: 'motion' as const, motionUrl: osVisible, usesBoundMotion: true, usesSpeechCoveringTake: false, motionTaskId: undefined }
    : osPreviewAuthority
      ? { kind: 'motion' as const, motionUrl: undefined, usesBoundMotion: false, usesSpeechCoveringTake: false, motionTaskId: undefined }
      : resolveCurrentProductionVideoSource({
          run,
          shotId: current.id,
          lipsyncPlayableUrl: lipsyncPlayable,
          fallbackMotionUrl,
          fallbackMotionN: matchingTake?.n,
          fallbackMotionTaskId: (run.turboTaskId || '').trim() || undefined,
          speechFloorSec,
        });
  const coveringTake = !osVisible && Boolean(productionSource.usesSpeechCoveringTake) && productionSource.kind !== 'lipsync';
  const coveringBlob = veoPlayTaskId && veoPlayTaskId === (productionSource.motionTaskId || '').trim() ? veoPlaySrc : undefined;
  const previewBind = osVisible
    ? { takeUrl: osVisible, lipsyncUrl: undefined, finalUrl: undefined, holdLipsync: false }
    : osPreviewAuthority
      ? { takeUrl: undefined, lipsyncUrl: undefined, finalUrl: undefined, holdLipsync: false }
      : resolveCurrentLipsyncPreviewBinding({
          run,
          shotId: current.id,
          lipsyncPlayableUrl: lipsyncPlayable,
          falFileMissing,
          takeUrl: deskAuthVideoSrc(coveringBlob || (coveringTake ? undefined : veoPlaySrc)),
          finalUrl: coveringTake ? undefined : snap.finalReady || snap.mixReady ? deskAuthVideoSrc(finalUrl[current.id]) : undefined,
          speechFloorSec,
        });
  const takeUrl = previewBind.takeUrl;
  const lipsyncUrl = previewBind.lipsyncUrl;
  const falNoOutput = lipsyncFalHasNoOutput(run.lipsyncError);
  const lipsyncResume = !falNoOutput && shouldResumeLipsync({ ...run, lipsyncFileMissing: falFileMissing });
  const action = snap.intent.action || current.story || '';
  const cmd = nextShotProductionCommand(snap);
  const stamp = (run.kfSourceHash || '').trim();
  const approvedStill = Boolean(run.kfApproved || run.status === 'approved');
  const runKf = displayableStillUrl(run.keyframeDataUrl);
  const memKf = displayableStillUrl(kfPixelsOf(current.id));
  const stampOk = (url?: string) => {
    if (!url) return false;
    if (url.startsWith('data:image')) {
      return !approvedStill || !stamp || dataUriHash(url) === stamp;
    }
    return !approvedStill;
  };
  const rawKf =
    (stampOk(runKf) ? runKf : undefined) ||
    (stampOk(memKf) ? memKf : undefined) ||
    displayableStillUrl(stillOf(run, action)) ||
    undefined;
  const liveHash = rawKf?.startsWith('data:image') ? dataUriHash(rawKf) : '';
  const approvedHash = currentApprovedPictureHash(run);
  const duplicatePixel = Boolean(
    liveHash && approvedHash && liveHash === approvedHash && !(run.kfApproved || run.status === 'approved'),
  );
  const desk = applyProductionOsActionAuthority(
    directorPictureVideoSurface({
      snap,
      cmd,
      hasLiveStill: Boolean(rawKf),
      kfApproved: approvedStill,
      duplicatePixel,
      forceNewLipsync: falNoOutput,
    }),
    osActions,
  );
  const phase = desk.phase || directorShotPhase(cmd);
  const kfUrl = directorPreviewStillUrl({
    pictureUnusable: snap.pictureUnusable,
    picturePendingApproval: snap.picturePendingApproval || desk.pendingApproval,
    keyframeApproved: snap.keyframeApproved,
    rawKf,
    allowedStill: stillOf({ ...run, keyframeDataUrl: rawKf }, action) || undefined,
  });
  const playableFinal = coveringTake ? undefined : previewBind.finalUrl;
  /** Display player only. Current-valid comes from snap.execution, not previewUrl. */
  const hasUsableVideo = Boolean(playableFinal || lipsyncUrl || takeUrl);
  const livePicturePixel = (snap.execution?.currentInputs.picture.pixelHash || '').trim();
  const takePicturePixel = (visibleMotionArt?.provenance.pixelHash || '').trim();
  const picturePixelMismatch = Boolean(livePicturePixel && takePicturePixel && livePicturePixel !== takePicturePixel);
  const hideStaleAv = Boolean(
    !currentTakeUrl &&
      !veoPlaySrc &&
      !isVeoMotionTaskId(run.turboTaskId) &&
      (picturePixelMismatch ||
        snap.picturePendingApproval ||
        desk.pendingApproval ||
        (run.kfApproved === false && hasUsableVideo)),
  );
  const showStill = Boolean(
    directorDesk &&
      (hideStaleAv
        ? Boolean(kfUrl)
        : directorShowsStillPreview({
            phase,
            picturePendingApproval: snap.picturePendingApproval || desk.pendingApproval,
            pictureUnusable: snap.pictureUnusable,
            hasUsableVideo,
            takeFromOtherPicture: snap.takeFromOtherPicture && picturePixelMismatch,
          })),
  );
  const pictureBlock = (osPictureBlock || '').trim();
  const directorFocus = directorDesk && Boolean(pictureBlock);
  const productionStill = pictureBlock ? undefined : (osKeyframeUrl || '').trim() || undefined;
  const libraryStill = (visualStill?.url || '').trim() || undefined;
  const shownStill = productionStill || libraryStill;
  const previewRole = (osPreviewIdentity?.role || '').toUpperCase();
  const osLipsync = previewRole.includes('LIPSYNC') || previewRole.includes(':MIX');
  const preview = pictureBlock && !libraryStill
    ? resolveShotPreviewKind({})
    : osVisible
    ? resolveShotPreviewKind(osLipsync ? { lipsyncUrl: osVisible } : { takeUrl: osVisible })
    : shownStill
    ? resolveShotPreviewKind({ keyframeUrl: shownStill, preferKeyframe: true })
    : resolveShotPreviewKind({
        finalUrl: showStill || hideStaleAv ? undefined : playableFinal,
        lipsyncUrl: showStill || hideStaleAv ? undefined : lipsyncUrl,
        takeUrl: showStill || hideStaleAv ? undefined : takeUrl,
        keyframeUrl: kfUrl || undefined,
        preferKeyframe: showStill,
      });
  const imageChoice = directorDesk && isOsActionAvailable(osActions, 'CREATE_IMAGE');
  const [imageTier, setImageTier] = useState(() => readQualityTier(current.id, 'IMAGE'));
  useEffect(() => {
    setImageTier(readQualityTier(current.id, 'IMAGE'));
  }, [current.id]);
  const permittedRecreate = directorFocus
    && imageChoice
    && pictureBlock.includes('Cần giấy phép');
  const imagePackage = qualityTierOf('IMAGE', imageTier);
  const deskNote = permittedRecreate
    ? `Giấy phép đã có. Công đoạn này tối đa 3 lượt có phí, tính cả lượt đầu. Đang chọn ${imagePackage.label}, ${imagePackage.detail}, chưa phải hóa đơn.`
    : pictureBlock || directorNoteWithProductionStill(desk.note, Boolean(productionStill));
  const technicalDetail = [
    desk.technical,
    pictureBlock && osAuthoritativeVideo?.artifactId ? `VIDEO ${osAuthoritativeVideo.artifactId} ${osAuthoritativeVideo.providerId || ''} ${osAuthoritativeVideo.modelId || ''}` : '',
    pictureBlock && osAuthoritativeVoice?.artifactId ? `VOICE ${osAuthoritativeVoice.artifactId}` : '',
    pictureBlock && osLipsyncArtifactId ? `LIPSYNC ${osLipsyncArtifactId}` : '',
    osPaidStageLedger ? `Lượt có phí ${osPaidStageLedger}` : '',
  ].filter(Boolean).join('\n');
  const pipe = shotPipelineBusy({
    shotId: current.id,
    busyShotId,
    motionBusyId,
    lipsyncBusyId,
    mixBusyId,
  });
  const sceneKey = current.sceneId || current.scene || 'SCENE';
  const sceneTitle = (current.scene || '').replace(/^SC\d+\s*[—–-]\s*/i, '').trim();
  const shotNo = sceneShots.findIndex((s) => s.id === current.id);
  const navItems = sceneShots.map((s, i) => {
    const row = snapOf(s);
    return {
      id: s.id,
      index: i,
      mark: (s.id === current.id ? '●' : row.finalReady ? '✓' : '○') as '✓' | '●' | '○',
    };
  });

  return (
    <section ref={studioRef} id="fx-shot-studio" className="fx-desk" data-director-bench={directorDesk ? '1' : undefined} style={{ maxWidth: 1100 }}>
      <header>
        <p className="fx-prod__kicker">{directorDesk ? 'BÀN SHOT' : sceneKey}</p>
        {episodeLabel ? <p className="fx-sw__now" data-episode-title="1">Đang mở tập: {episodeLabel}</p> : null}
        {sceneTitle ? <p className="fx-desk__note">{sceneTitle}</p> : null}
        <h2>
          Shot {String((shotNo >= 0 ? shotNo : 0) + 1).padStart(2, '0')}
          {current.shot ? `-${String(current.shot).padStart(2, '0')}` : ''}
        </h2>
        <p className="fx-desk__note" data-shot-identity="1">
          SHOT ID: {current.id}
          <br />
          BUILD: {shotIdentity.loadedBuildId}
        </p>
        {!pictureBlock && osAcceptedVideo ? (
          <p
            className="fx-desk__note"
            data-os-video-complete="1"
            data-os-video-artifact={osAuthoritativeVideo?.artifactId || undefined}
            data-os-video-role={osAuthoritativeVideo?.role || undefined}
            data-os-video-accepted="1"
          >
            VIDEO: ✓ Đã hoàn thành
            <br />
            Provider:{' '}
            {formatOfficialVideoProviderLine(osAuthoritativeVideo?.providerId, osAuthoritativeVideo?.modelId) ||
              'Google / Veo 3.1'}
          </p>
        ) : !pictureBlock && osCurrentVideo ? (
          <p
            className="fx-desk__note"
            data-os-video-current="1"
            data-os-video-artifact={osAuthoritativeVideo?.artifactId || undefined}
            data-os-video-role={osAuthoritativeVideo?.role || undefined}
          >
            VIDEO: CURRENT
            <br />
            Provider:{' '}
            {formatOfficialVideoProviderLine(osAuthoritativeVideo?.providerId, osAuthoritativeVideo?.modelId) ||
              'import / existing-video'}
          </p>
        ) : null}
        {!pictureBlock && osVoiceReady ? (
          <p
            className="fx-desk__note"
            data-os-voice-complete="1"
            data-os-voice-artifact={osAuthoritativeVoice?.artifactId || undefined}
            data-os-voice-role={osAuthoritativeVoice?.role || undefined}
          >
            VOICE: ✓ Đã có thoại
            <br />
            Artifact: {osAuthoritativeVoice?.artifactId}
          </p>
        ) : null}
        {!pictureBlock && osLipsyncReady ? (
          <p
            className="fx-desk__note"
            data-os-lipsync-complete="1"
            data-os-lipsync-artifact={osLipsyncArtifactId || undefined}
            data-os-lipsync-role="LIPSYNC"
          >
            LIPSYNC: ✓ Đã khớp môi
            <br />
            Artifact: {osLipsyncArtifactId}
          </p>
        ) : null}
        <p>{snap.intent.action || current.story || 'Shot đang sản xuất'}</p>
      </header>
      {directorDesk ? (
        <ol className="fx-finish__check" data-director-phases="1">
          {DIRECTOR_SHOT_PHASES.map((row) => (
            <li key={row.id}>
              {row.id === phase ? '●' : DIRECTOR_SHOT_PHASES.findIndex((p) => p.id === phase) > DIRECTOR_SHOT_PHASES.findIndex((p) => p.id === row.id) ? '✓' : '○'} {row.label}
            </li>
          ))}
        </ol>
      ) : null}
      {onEngine && engine && !directorFocus ? (
        <div className="fx-desk__note" style={{ marginBottom: 12 }}>
          <FamixaMotionEnginePicker
            engine={engine}
            onEngine={onEngine}
            keys={engineKeys}
            selectionProviderId={osVideoModel?.selection?.providerId}
            selectionModelId={osVideoModel?.selection?.modelId}
            takeProvenance={osVideoModel?.take}
            busy={directorSelectionBusy}
          />
          {onDirectorTiming ? (
            <div style={{ marginTop: 10 }}>
              <FamixaDirectorTimingPicker
                timing={osDirectorTiming}
                busy={directorTimingBusy}
                onConfirm={onDirectorTiming}
              />
            </div>
          ) : null}
          {onLipsyncModel ? (
            <p style={{ marginTop: 8 }}>
              Khớp môi · Fal{' '}
              <Select
                size="small"
                style={{ minWidth: 280 }}
                value={normalizeLipsyncModel(lipsyncModel)}
                onChange={onLipsyncModel}
                options={FAL_LIPSYNC_TIERS.map((t) => ({
                  value: t.value,
                  label: `${t.title} — ${t.hint}`,
                }))}
              />
            </p>
          ) : null}
        </div>
      ) : null}
      <ShotProductionPreview
        key={`${current.id}:${preview.src || ''}`}
        kind={preview.kind}
        src={preview.src}
        label={
          directorDesk && preview.kind !== 'keyframe'
            ? directorProductionCaption({
                kind: preview.kind,
                takeN: productionSource.motionN,
              })
            : preview.label
        }
        title={snap.intent.action || current.story}
        pendingApproval={snap.picturePendingApproval || desk.pendingApproval}
        emptyLabel={pictureBlock ? 'Chưa có kết quả hiện tại.' : undefined}
        videoRef={videoRef}
        tone={directorDesk ? 'director' : 'staff'}
        pickSpeaker={Boolean(
          directorDesk &&
            onPickLipsyncSpeaker &&
            !run.lipsyncFrozenInput?.speakerCharacterId &&
            lipsyncNeedsSpeakerPick(current, lines, live.state.characters),
        )}
        speakerMark={
          current.id === 'EP01-SC01-SH01'
            ? { x: 0.36, y: 0.22 }
            : run.lipsyncSpeaker
        }
        onPickSpeaker={onPickLipsyncSpeaker ? (pt) => onPickLipsyncSpeaker(current.id, pt) : undefined}
        voiceSrc={
          preview.kind === 'motion' || preview.kind === 'video'
            ? falNoOutput || falFileMissing || !lipsyncUrl
              ? voiceUrl
              : undefined
            : undefined
        }
      />
      {!directorFocus && !lipsyncUrl && !osLipsync && dialogue ? (
        <div className="fx-desk__note" data-mute-take-voice="1">
          {voiceUrl ? (
            <>
              <p>Take Veo câm. Bấm Play ở video — hoặc nghe thoại ở đây. Miệng chưa khớp.</p>
              <audio controls src={voiceUrl} preload="metadata" aria-label="Nghe thoại" />
            </>
          ) : snap.voiceDurationSec > 0.2 ? (
            <p>
              Bản nghe thoại chưa vào phiên này.{' '}
              <Button
                type="primary"
                size="small"
                data-os-prepare-voice="1"
                onClick={() =>
                  onPrepareOsVoice
                    ? onPrepareOsVoice(current.id)
                    : void onEnsureVoice(current.id)
                }
              >
                Nạp thoại
              </Button>
              {' '}— không tạo TTS mới, không trừ Fal.
            </p>
          ) : (
            <p>Chưa có bản nghe thoại trên máy. Mở tab Người / Voice — không bấm Khớp môi.</p>
          )}
        </div>
      ) : null}
      {pipe.pictureBusy ? (
        <p className="fx-desk__note" data-picture-busy="1">
          Đang vẽ hoặc chấm hình — đợi xong. Không tạo video, không vẽ thêm.
        </p>
      ) : pipe.motionBusy ? (
        <p className="fx-desk__note" data-motion-busy="1">
          Đang tạo video — đợi file. Không tắt trang, không bấm Tạo hình mới.
        </p>
      ) : pipe.mixBusy ? (
        <p className="fx-desk__note" data-mix-busy="1">
          Đang ghép cảnh — đợi file. Không tắt trang.
        </p>
      ) : pipe.lipsyncBusy ? (
        <p className="fx-desk__note" data-lipsync-busy="1">
          Đang khớp môi — đợi file. Không tắt trang.
        </p>
      ) : directorDesk && deskNote ? (
        <p
          className="fx-desk__note"
          data-motion-empty={desk.pendingApproval ? undefined : '1'}
          data-picture-pending={desk.pendingApproval || productionStill ? '1' : undefined}
          data-production-still={preview.kind === 'keyframe' && productionStill ? '1' : undefined}
          data-os-create-video-unavailable={
            osActions && ![desk.primary, desk.secondary, desk.tertiary].some((cta) => cta?.action === 'os-create-video')
              ? '1'
              : undefined
          }
        >
          {deskNote}
        </p>
      ) : null}
      {directorDesk && pictureBlock.includes('khóa master') && onOpenVoice ? (
        <Button size="small" onClick={onOpenVoice}>Mở nhân vật</Button>
      ) : null}
      {imageChoice ? (
        <FamixaQualityTierPicker
          shotId={current.id}
          capability="IMAGE"
          value={imageTier}
          onChange={setImageTier}
        />
      ) : null}
      {permittedRecreate ? (
        <Button type="primary" onClick={() => handleAction(current.id, 'os-create-image')}>Tạo hình</Button>
      ) : null}
      {onVisualLibrary ? (
        <div className="fx-desk__note" data-visual-phase={visualStill?.phase || 'idle'} data-visual-decision={visualStill?.decision || ''} data-visual-search-decision={visualStill?.searchDecision || ''}>
          {visualBrandChoices && onVisualBrandChoice ? (
            <label>
              Brand của phim
              <select
                data-visual-brand="1"
                value={visualBrandChoice || ''}
                onChange={(event) => onVisualBrandChoice(event.target.value)}
              >
                <option value="">Chưa có brand</option>
                {visualBrandChoices.map((brand) => (
                  <option key={brand.code} value={brand.code}>{brand.name}</option>
                ))}
              </select>
            </label>
          ) : null}
          {visualStill && visualStill.phase !== 'idle' ? <p>{visualStill.detail}</p> : <p>Thư viện hình dùng chung. Ảnh production hiện có không bị thay.</p>}
          {visualStill?.undrawable ? <p>Chưa vẽ được chữ có dấu: {visualStill.undrawable}</p> : null}
          {visualStill?.url ? <img src={visualStill.url} alt="Ảnh thư viện của shot" data-visual-still="1" style={{ display: 'block', maxWidth: '100%', marginTop: 8 }} /> : null}
          {visualStill?.variantUrl ? <img src={visualStill.variantUrl} alt="Biến thể mới, không thay ảnh đã duyệt" data-visual-variant="1" style={{ display: 'block', maxWidth: '100%', marginTop: 8 }} /> : null}
          <Button size="small" data-visual-search="1" onClick={() => onVisualLibrary(current.id)}>Tìm hình dùng chung</Button>
          {onVisualPhoto ? <Button size="small" data-visual-photo="1" onClick={() => onVisualPhoto(current.id)}>Ảnh chân thực</Button> : null}
          {visualStill?.phase === 'ready' && onVisualApprove ? (
            <Button size="small" onClick={() => onVisualApprove(current.id)}>Duyệt ảnh này</Button>
          ) : null}
          {(visualStill?.phase === 'ready' || visualStill?.phase === 'approved' || visualStill?.phase === 'variant') && onVisualVariant ? (
            <Button size="small" onClick={() => onVisualVariant(current.id)}>Tạo biến thể</Button>
          ) : null}
        </div>
      ) : null}
      {directorDesk && (technicalDetail || imageChoice) ? (
        <details className="fx-desk__note">
          <summary>Chi tiết kỹ thuật</summary>
          {technicalDetail ? <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{technicalDetail}</pre> : null}
          <FamixaQualityTierPicker shotId={current.id} capability="VIDEO" />
          <FamixaQualityTierPicker shotId={current.id} capability="VOICE" />
          <FamixaQualityTierPicker shotId={current.id} capability="LIPSYNC" />
          <p>Gói nháp là mặc định. Gói cao chỉ chạy khi chọn trước lúc bấm. Mỗi công đoạn tối đa 3 lượt có phí. Lượt sau chỉ chạy khi đã rõ nguyên nhân và đầu vào đã đổi.</p>
        </details>
      ) : null}
      {directorDesk &&
      !directorFocus &&
      onPickLipsyncSpeaker &&
      lipsyncNeedsSpeakerPick(current, lines, live.state.characters) ? (
        <div className="fx-desk__note" data-lipsync-speaker-pick="1">
          <p>
            {lipsyncSpeakerPinRejectedByFal(run.lipsyncError)
              ? `Thoại Linh — Minh không được máy môi. Confirm gửi khung mặt Linh, đúng số khung file take. Không để câm.`
              : run.lipsyncFrozenInput?.speakerCharacterId &&
                  !canMutateLiveSpeakerTarget(run.lipsyncFrozenInput, run)
                ? `Speaker đã freeze ${run.lipsyncFrozenInput.speakerCharacterId} — không đổi target Fal.`
                : run.lipsyncSpeaker
                  ? `Đã chấm mặt ${lipsyncSpeakerLabel(lines, live.state.characters)}. Có thể chấm lại trên hình/video.`
                  : `Chưa chấm mặt ${lipsyncSpeakerLabel(lines, live.state.characters)}. Bấm mặt trên hình/video, hoặc chọn bên dưới — rồi mới Khớp môi.`}
          </p>
          <p>
            <Button
              size="small"
              disabled={
                Boolean(run.lipsyncFrozenInput?.speakerCharacterId) &&
                !canMutateLiveSpeakerTarget(run.lipsyncFrozenInput, run)
              }
              onClick={() => onPickLipsyncSpeaker(current.id, lipsyncSpeakerPickOf('right', live.state.characters))}
            >
              Chấm mặt phải (Linh)
            </Button>{' '}
            <Button
              size="small"
              disabled={
                Boolean(run.lipsyncFrozenInput?.speakerCharacterId) &&
                !canMutateLiveSpeakerTarget(run.lipsyncFrozenInput, run)
              }
              onClick={() => onPickLipsyncSpeaker(current.id, lipsyncSpeakerPickOf('left', live.state.characters))}
            >
              Chấm mặt trái (Minh)
            </Button>
          </p>
        </div>
      ) : null}
      {directorDesk && !directorFocus && deskExposeLipsyncRemakeCta(snap) && preview.kind === 'lipsync' ? (
        <p className="fx-desk__note" data-lipsync-remake="1">
          File môi hiện tại đã phát. Nếu cả hai miệng nhấp: chấm mặt Linh (phải) rồi Khớp môi mới — Confirm mới trừ Fal. Hủy = 0$.
        </p>
      ) : null}
      {directorFocus || pipe.pictureBusy || pipe.motionBusy || pipe.mixBusy || pipe.lipsyncBusy ? null : directorDesk && (desk.primary || desk.secondary || desk.tertiary) ? (
        <div className="fx-desk__btns" data-director-picture-cta={desk.pendingApproval || desk.primary?.action === 'picture' ? '1' : undefined} data-director-remake-picture={!desk.primary && (desk.secondary?.action === 'picture' || desk.tertiary?.action === 'picture') ? '1' : undefined} data-director-remake-motion={desk.secondary?.action === 'motion' ? '1' : undefined}>
          {desk.primary ? (
            <Button
              type="primary"
              data-approve-picture={desk.primary.action === 'approve-picture' ? '1' : undefined}
              data-os-create-video={desk.primary.action === 'os-create-video' ? '1' : undefined}
              data-os-create-image={desk.primary.action === 'os-create-image' ? '1' : undefined}
              data-os-action={desk.primary.action in OS_ACTION_FOR_DESK ? OS_ACTION_FOR_DESK[desk.primary.action as OsDeskCtaAction] : undefined}
              loading={desk.primary.action === 'os-create-video' && motionBusyId === current.id}
              onClick={() => handleAction(current.id, desk.primary!.action)}
            >
              {desk.primary.label}
            </Button>
          ) : null}
          {desk.secondary && desk.secondary.action !== desk.primary?.action ? (
            <Button
              data-os-create-video={desk.secondary.action === 'os-create-video' ? '1' : undefined}
              data-os-create-image={desk.secondary.action === 'os-create-image' ? '1' : undefined}
              data-os-action={desk.secondary.action in OS_ACTION_FOR_DESK ? OS_ACTION_FOR_DESK[desk.secondary.action as OsDeskCtaAction] : undefined}
              loading={desk.secondary.action === 'os-create-video' && motionBusyId === current.id}
              onClick={() => handleAction(current.id, desk.secondary!.action)}
            >
              {desk.secondary.label}
            </Button>
          ) : null}
          {desk.tertiary && desk.tertiary.action !== desk.primary?.action && desk.tertiary.action !== desk.secondary?.action ? (
            <Button
              data-os-create-video={desk.tertiary.action === 'os-create-video' ? '1' : undefined}
              data-os-create-image={desk.tertiary.action === 'os-create-image' ? '1' : undefined}
              data-os-action={desk.tertiary.action in OS_ACTION_FOR_DESK ? OS_ACTION_FOR_DESK[desk.tertiary.action as OsDeskCtaAction] : undefined}
              onClick={() => handleAction(current.id, desk.tertiary!.action)}
            >
              {desk.tertiary.label}
            </Button>
          ) : null}
          {hold?.kind === 'picture' && hold.reason ? (
            <p className="fx-desk__note">{[hold.reason, hold.hint].filter(Boolean).join(' ')}</p>
          ) : pictureGate && !pictureGate(current.id).allowed ? (
            <p className="fx-desk__note">
              {[pictureGate(current.id).reason, pictureGate(current.id).hint].filter(Boolean).join(' ')}
            </p>
          ) : null}
          {visibleProductionOsCatalogActions(osActions)
            .filter((row) => ![desk.primary, desk.secondary, desk.tertiary].some((cta) => productionActionForDeskCta(cta) === row.action))
            .map((row) => (
              <Button
                key={row.action}
                data-os-action={row.action === 'CREATE_VOICE' ? undefined : row.action}
                data-os-prepare-voice={row.action === 'CREATE_VOICE' ? '1' : undefined}
                onClick={() =>
                  row.action === 'CREATE_VOICE' && onPrepareOsVoice
                    ? onPrepareOsVoice(current.id)
                    : onExecuteOsAction?.(current.id, row.action)
                }
              >
                {row.label}
              </Button>
            ))}
        </div>
      ) : null}
      {!directorFocus && directorDesk && osActions && !(desk.primary || desk.secondary || desk.tertiary) ? (
        <div className="fx-desk__btns" data-os-catalog-actions="1">
          {visibleProductionOsCatalogActions(osActions).map((row) => (
            <Button
              key={row.action}
              data-os-action={row.action === 'CREATE_VOICE' ? undefined : row.action}
              data-os-prepare-voice={row.action === 'CREATE_VOICE' ? '1' : undefined}
              onClick={() =>
                row.action === 'CREATE_VOICE' && onPrepareOsVoice
                  ? onPrepareOsVoice(current.id)
                  : onExecuteOsAction?.(current.id, row.action)
              }
            >
              {row.label}
            </Button>
          ))}
        </div>
      ) : null}
      {!directorFocus && ((run.lipsyncError || '').trim() || falFileMissing) ? (
        <div className="fx-desk__note" data-lipsync-error="1" style={{ display: 'block', width: '100%', marginTop: 8 }}>
          <label style={{ display: 'block', width: '100%' }}>
            Lỗi khớp môi (giữ trên bàn — copy được)
            <textarea
              readOnly
              rows={7}
              value={[
                (run.lipsyncError || '').trim()
                  || (falFileMissing
                    ? 'Chưa có file môi trên máy. Đang xem take Veo (câm). Gắn Fal · 0$ từ Usage — đừng Khớp môi.'
                    : ''),
                run.lipsyncTaskId?.trim() && !(run.lipsyncError || '').includes(run.lipsyncTaskId)
                  ? `Task ${run.lipsyncTaskId}`
                  : '',
                falNoOutput
                  ? 'Job Fal cũ không có file. Take Veo + thoại vẫn xem được — không bắt buộc gửi tiếp. Confirm lần sau gửi khung mặt Linh, đúng số khung của file take. Hủy = 0$.'
                  : lipsyncResume
                    ? 'Bấm Hỏi lại môi · 0$ — KIT hỏi Fal lấy file cũ, không gửi job mới.'
                    : '',
              ]
                .filter(Boolean)
                .join('\n')}
              style={{ display: 'block', width: '100%', marginTop: 6, fontFamily: 'inherit' }}
            />
          </label>
          <p style={{ marginTop: 8 }}>
            {falNoOutput ? (
              <Button type="primary" onClick={() => void onEnsureLipsync(current.id, { forceNew: true })}>
                {DIRECTOR_LIPSYNC_NEW_LABEL}
              </Button>
            ) : lipsyncResume ? (
              <Button type="primary" onClick={() => void onEnsureLipsync(current.id, { resume: true })}>
                {DIRECTOR_LIPSYNC_RESUME_LABEL} · 0$
              </Button>
            ) : null}{' '}
            {onAttachLipsync ? (
              <Button onClick={() => onAttachLipsync(current.id)}>Gắn Fal · 0$</Button>
            ) : null}
          </p>
        </div>
      ) : null}
      <div className="fx-desk__note">
        <p>Thoại</p>
        {dialogue ? <p>«{dialogue}»</p> : <p>Shot chưa có thoại.</p>}
        {directorFocus ? null : preview.kind === 'final' || preview.kind === 'lipsync' || (lipsyncUrl && !falFileMissing && !falNoOutput) ? (
          <p>Nghe trên video đã lồng tiếng. Không tạo thoại mới.</p>
        ) : voiceUrl ? (
          <audio controls src={voiceUrl} preload="metadata" aria-label="Nghe thoại" />
        ) : lipsyncUrl ? (
          <p>Nghe trên video đã lồng tiếng. Không tạo thoại mới.</p>
        ) : snap.voiceDurationSec > 0.2 ? (
          <p>
            Bản nghe mất khỏi phiên. Bấm Nạp thoại — không tạo TTS mới.{' '}
            <Button size="small" data-os-prepare-voice="1" onClick={() => (onPrepareOsVoice ? onPrepareOsVoice(current.id) : void onEnsureVoice(current.id))}>
              Nạp thoại
            </Button>
          </p>
        ) : dialogue ? (
          <p>Chưa có bản nghe thoại.</p>
        ) : null}
        {hold?.reason && /thoại/i.test(hold.reason) && onOpenVoice ? (
          <Button onClick={onOpenVoice}>{directorDesk ? 'Mở Người' : 'Xem thoại'}</Button>
        ) : null}
      </div>
      {!directorFocus && onActingBeat ? (
        <ShotProductionActingBeatCard
          shot={current}
          spoken={lines.length > 0}
          who={shotActingSpeakerOf(live.state, current, lines).name || 'nhân vật'}
          speechText={lines[0]?.text}
          onChange={(next) => onActingBeat(current.id, next)}
          onReady={(handle) => {
            actingEditorRef.current = handle;
          }}
        />
      ) : null}
      {!directorDesk && onCoverage && onEditorial ? (
        <ShotProductionSmoothnessCard
          state={live.state}
          shot={current}
          smoothness={smoothnessOf(live.state)}
          onCoverage={(next) => onCoverage(current.id, next)}
          onEditorial={(preset: EditorialPreset) => onEditorial(current.id, applyEditorialPreset(live.state, current, preset))}
          onSmoothness={onSmoothness}
        />
      ) : null}
      {directorFocus ? null : renderCard(current, shotNo >= 0 ? shotNo : 0, { hideIdentity: true, tone: directorDesk ? 'director' : 'staff' })}
      <ShotProductionNavigator
        sceneLabel={sceneKey}
        sceneTitle={sceneTitle}
        items={navItems}
        currentId={current.id}
        onSelect={selectShot}
      />
      {!directorDesk ? <ShotProductionTechnical snap={snap} run={run} state={state} shot={current} ttsFiles={ttsFiles} /> : null}
    </section>
  );
}

export function ContentFamixaShotProductionPanel(
  props: ComponentProps<typeof ContentFamixaShotProductionWorkspace>,
) {
  if (!shotProductionOrchestrationEnabled()) return null;
  return <ContentFamixaShotProductionWorkspace {...props} />;
}
