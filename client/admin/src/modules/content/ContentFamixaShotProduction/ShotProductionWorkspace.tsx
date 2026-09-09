import { useEffect, useMemo, useRef, useState, type ComponentProps } from 'react';
import { Button, message } from 'antd';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from '../content-famixa-series';
import { linesForShot } from '../content-famixa-dialogue-map';
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
import { directorPictureVideoSurface, shotPipelineBusy, shotQaMissing, type StaffShotAction } from './ShotProductionCta';
import { snapshotOfShot, type ShotProductionSnapshot } from './ShotProductionState';
import { shotProductionOrchestrationEnabled } from './ShotProductionIntent';
import { ShotProductionNavigator, resolveLaneAShotId, sceneShotsOf } from './ShotProductionNavigator';
import { nextShotProductionCommand, produceShot } from './ShotProductionOrchestrator';
import {
  directorMotionCaption,
  directorPreviewStillUrl,
  directorShowsStillPreview,
  resolveShotPreviewKind,
  ShotProductionPreview,
} from './ShotProductionPreview';
import { ShotProductionTechnical } from './ShotProductionTechnical';
import { DIRECTOR_SHOT_PHASES, directorShotPhase } from '../kit-video-director-nav';
import { loadFinalBlob } from './ShotProductionFinalStore';

export type PictureGate = { allowed: boolean; reason?: string; hint?: string; kind?: 'picture' | 'qa' };

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
  onEnsureMotion,
  onEnsureLipsync,
  onEnsureMix,
  onWatch,
  onQa,
  onActingBeat,
  onCoverage,
  onEditorial,
  onSmoothness,
  onAcceptExisting,
  onRetryCamera,
  episodeLabel,
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
  onEnsureLipsync: (shotId: string) => Promise<void> | void;
  onEnsureMix: (shotId: string) => Promise<void> | void;
  onWatch?: (shotId: string) => void;
  onQa?: (shotId: string, next: NonNullable<SeriesShotRun['shotQa']>) => void;
  onActingBeat?: (shotId: string, next: NonNullable<FamixaSeriesShot['actingBeat']>) => void;
  onCoverage?: (shotId: string, next: ShotCoverage) => void;
  onEditorial?: (shotId: string, next: ReturnType<typeof applyEditorialPreset>) => void;
  onSmoothness?: (next: EpisodeSmoothness) => void;
  onAcceptExisting?: (shotId: string) => void;
  onRetryCamera?: (shotId: string) => void;
  episodeLabel?: string;
}) {
  const directorDesk = mode === 'director';
  const [artifact, setArtifact] = useState<Record<string, boolean>>({});
  const [finalUrl, setFinalUrl] = useState<Record<string, string>>({});
  const [advancing, setAdvancing] = useState<string>();
  const [hold, setHold] = useState<PictureGate>();
  const [localShotId, setLocalShotId] = useState<string>();
  const mixOnce = useRef<string>();
  const videoRef = useRef<HTMLVideoElement>(null);
  const studioRef = useRef<HTMLElement>(null);
  const actingEditorRef = useRef<ActingBeatEditorHandle>();
  const currentId = resolveLaneAShotId(shots, selectedShotId || localShotId);
  const sceneShots = useMemo(() => sceneShotsOf(shots, currentId), [shots, currentId]);
  const current = sceneShots.find((s) => s.id === currentId) || shots.find((s) => s.id === currentId) || shots[0];

  useEffect(() => {
    if (!focusTick) return;
    studioRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [focusTick, currentId]);

  useEffect(() => {
    let alive = true;
    const created: string[] = [];
    void Promise.all(
      shots.map(async (shot) => {
        const blob = await loadFinalBlob(shot.id);
        const href = blob ? URL.createObjectURL(blob) : '';
        return [shot.id, Boolean(blob), href] as const;
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

  const liveStillOf = (shotId: string) => {
    const liveShot = shots.find((s) => s.id === shotId);
    const liveRun = liveShot ? shotRunOf(liveOf().state, liveShot) : undefined;
    return (
      (liveRun?.keyframeDataUrl?.startsWith('data:image') ? liveRun.keyframeDataUrl : undefined) ||
      kfPixelsOf(shotId)
    );
  };

  const produce = async (shotId: string) => {
    const shot = shots.find((s) => s.id === shotId);
    if (!shot) return;
    const ahead = nextShotProductionCommand(snapOf(shot));
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
          ensureMotion: () => Promise.resolve(onEnsureMotion(shotId, liveStillOf(shotId))),
          ensureLipsync: () => Promise.resolve(onEnsureLipsync(shotId)),
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

  useEffect(() => {
    if (!directorDesk || !current) return;
    const snap = snapOf(current);
    const cmd = nextShotProductionCommand(snap);
    if (cmd.type !== 'ENSURE_MIX' || snap.mixFailed || mixBusyId === current.id) {
      if (cmd.type !== 'ENSURE_MIX' && mixOnce.current === current.id) mixOnce.current = undefined;
      return;
    }
    if (mixOnce.current === current.id) return;
    mixOnce.current = current.id;
    void onEnsureMix(current.id);
  }, [directorDesk, current?.id, state, ttsFiles, mixBusyId]);

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
    if (action === 'picture') {
      actingEditorRef.current?.save?.();
      if (!tryPicture(shotId)) return;
      void onEnsurePicture(shotId);
      return;
    }
    if (action === 'retry-camera') {
      if (onRetryCamera) {
        onRetryCamera(shotId);
        return;
      }
      void onEnsureMotion(shotId, liveStillOf(shotId));
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
    else if (action === 'motion') void onEnsureMotion(shotId, liveStillOf(shotId));
    else if (action === 'lipsync') void onEnsureLipsync(shotId);
    else if (action === 'mix') void onEnsureMix(shotId);
    else if (action === 'watch') {
      if (finalUrl[shotId] && videoRef.current) {
        void videoRef.current.play();
        return;
      }
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
        void onEnsureLipsync(shotId);
        return;
      }
      if (!snap.mixReady) void onEnsureMix(shotId);
    }
    else if (action !== 'blocked') void produce(shotId);
  };

  const retryOf = (shotId: string) => (stage: 'voice' | 'picture' | 'motion' | 'lipsync' | 'mix') => {
    if (stage === 'voice') void Promise.resolve(onEnsureVoice(shotId)).catch(() => undefined);
    if (stage === 'picture') {
      if (!tryPicture(shotId)) return;
      void onEnsurePicture(shotId);
    }
    if (stage === 'motion') void onEnsureMotion(shotId, liveStillOf(shotId));
    if (stage === 'lipsync') void onEnsureLipsync(shotId);
    if (stage === 'mix') void onEnsureMix(shotId);
  };

  const renderCard = (shot: FamixaSeriesShot, index: number, opts?: { hideIdentity?: boolean; tone?: 'director' | 'staff' }) => {
    const snap = snapOf(shot);
    return (
      <ShotProductionCard
        key={shot.id}
        label={`SHOT ${String(index + 1).padStart(2, '0')}`}
        title={snap.intent.action}
        snap={snap}
        shotQa={shotRunOf(liveOf().state, shot).shotQa}
        busy={busyShotId === shot.id || advancing === shot.id}
        tone={opts?.tone || (directorDesk ? 'director' : 'staff')}
        showIdentity={!opts?.hideIdentity}
        holdReason={hold?.reason}
        holdHint={hold?.hint}
        onAction={(action) => handleAction(shot.id, action)}
        onQa={
          onQa
            ? (next) => {
                const missing = shotQaMissing(next, !snapOf(shot).isSilent, {
                  afterLipsync: snapOf(shot).lipSyncReady,
                  afterMix: snapOf(shot).mixReady,
                });
                if (!missing.length) setHold(undefined);
                onQa(shot.id, next);
              }
            : undefined
        }
        onRetry={retryOf(shot.id)}
      />
    );
  };

  if (!current) {
    return (
      <section className="fx-desk">
        <h2>Video Studio</h2>
        <p className="fx-desk__note">Chưa có Shot để sản xuất.</p>
      </section>
    );
  }

  const snap = snapOf(current);
  const run = shotRunOf(state, current);
  const live = liveOf();
  const lines = linesForShot(live.state, current);
  const dialogue = lines.map((l) => l.text).filter(Boolean).join(' ');
  const voiceUrl = lines.map((l) => live.ttsFiles[l.id]?.url).find(Boolean);
  const currentMotionArt = snap.execution.currentArtifacts.motion;
  const currentTakeUrl =
    currentMotionArt?.validity === 'CURRENT' ? (currentMotionArt.url || '').trim() || undefined : undefined;
  const matchingTake = currentTakeUrl
    ? playableMotionTakeOf(run, picturePixelHashOf(run, current.id), run.pictureRevisionId)
    : undefined;
  const takeUrl = currentTakeUrl;
  const lipsyncUrl = (run.lipsyncUrl || '').trim() || undefined;
  const action = snap.intent.action || current.story || '';
  const cmd = nextShotProductionCommand(snap);
  const stamp = (run.kfSourceHash || '').trim();
  const approvedStill = Boolean(run.kfApproved || run.status === 'approved');
  const runKf = run.keyframeDataUrl?.startsWith('data:image') ? run.keyframeDataUrl : undefined;
  const memKf = kfPixelsOf(current.id);
  const stampOk = (url?: string) =>
    Boolean(url?.startsWith('data:image') && (!approvedStill || !stamp || dataUriHash(url) === stamp));
  const rawKf =
    (stampOk(runKf) ? runKf : undefined) ||
    (stampOk(memKf) ? memKf : undefined) ||
    stillOf(run, action) ||
    undefined;
  const liveHash = rawKf?.startsWith('data:image') ? dataUriHash(rawKf) : '';
  const approvedHash = currentApprovedPictureHash(run);
  const duplicatePixel = Boolean(
    liveHash && approvedHash && liveHash === approvedHash && !(run.kfApproved || run.status === 'approved'),
  );
  const desk = directorPictureVideoSurface({
    snap,
    cmd,
    hasLiveStill: Boolean(rawKf?.startsWith('data:image')),
    kfApproved: Boolean(run.kfApproved || run.status === 'approved'),
    duplicatePixel,
  });
  const phase = desk.phase || directorShotPhase(cmd);
  const kfUrl = directorPreviewStillUrl({
    pictureUnusable: snap.pictureUnusable,
    picturePendingApproval: snap.picturePendingApproval || desk.pendingApproval,
    keyframeApproved: snap.keyframeApproved,
    rawKf,
    allowedStill: stillOf({ ...run, keyframeDataUrl: rawKf }, action) || undefined,
  });
  const playableFinal = snap.finalReady || snap.mixReady ? finalUrl[current.id] : undefined;
  /** Display player only. Current-valid comes from snap.execution, not previewUrl. */
  const hasUsableVideo = Boolean(playableFinal || lipsyncUrl || takeUrl);
  const hideStaleAv = Boolean(
    !currentTakeUrl &&
      (snap.takeFromOtherPicture ||
        snap.picturePendingApproval ||
        desk.pendingApproval ||
        run.motionNeedsRemake ||
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
            takeFromOtherPicture: snap.takeFromOtherPicture,
          })),
  );
  const preview = resolveShotPreviewKind({
    finalUrl: showStill || hideStaleAv ? undefined : playableFinal,
    lipsyncUrl: showStill || hideStaleAv ? undefined : lipsyncUrl,
    takeUrl: showStill || hideStaleAv ? undefined : takeUrl,
    keyframeUrl: kfUrl || undefined,
    preferKeyframe: showStill,
  });
  const pipe = shotPipelineBusy({
    shotId: current.id,
    busyShotId,
    motionBusyId,
    lipsyncBusyId,
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
      <ShotProductionPreview
        key={preview.src || current.id}
        kind={preview.kind}
        src={preview.src}
        label={
          directorDesk && preview.kind !== 'keyframe' && currentTakeUrl
            ? directorMotionCaption({
                current: true,
                takeN: matchingTake?.n,
              })
            : preview.label
        }
        title={snap.intent.action || current.story}
        pendingApproval={snap.picturePendingApproval || desk.pendingApproval}
        videoRef={videoRef}
        tone={directorDesk ? 'director' : 'staff'}
      />
      {pipe.pictureBusy ? (
        <p className="fx-desk__note" data-picture-busy="1">
          Đang vẽ hoặc chấm hình — đợi xong. Không tạo video, không vẽ thêm.
        </p>
      ) : motionBusyId === current.id ? (
        <p className="fx-desk__note" data-motion-busy="1">
          Đang tạo video — đợi file. Không tắt trang, không bấm Tạo hình mới.
        </p>
      ) : directorDesk && desk.note ? (
        <p className="fx-desk__note" data-motion-empty={desk.pendingApproval ? undefined : '1'} data-picture-pending={desk.pendingApproval ? '1' : undefined}>
          {desk.note}
        </p>
      ) : null}
      {pipe.pictureBusy || pipe.motionBusy ? null : directorDesk && (desk.primary || desk.secondary) ? (
        <div className="fx-desk__btns" data-director-picture-cta={desk.pendingApproval || desk.primary?.action === 'picture' ? '1' : undefined} data-director-remake-picture={!desk.primary && desk.secondary?.action === 'picture' ? '1' : undefined}>
          {desk.primary ? (
            <Button
              type="primary"
              loading={desk.primary.action === 'motion' && motionBusyId === current.id}
              onClick={() => handleAction(current.id, desk.primary!.action)}
            >
              {desk.primary.label}
            </Button>
          ) : null}
          {desk.secondary && desk.secondary.action !== desk.primary?.action ? (
            <Button onClick={() => handleAction(current.id, desk.secondary!.action)}>{desk.secondary.label}</Button>
          ) : null}
          {hold?.kind === 'picture' && hold.reason ? (
            <p className="fx-desk__note">{[hold.reason, hold.hint].filter(Boolean).join(' ')}</p>
          ) : null}
        </div>
      ) : null}
      <div className="fx-desk__note">
        <p>Thoại</p>
        {dialogue ? <p>«{dialogue}»</p> : <p>Shot chưa có thoại.</p>}
        {voiceUrl ? (
          <audio controls src={voiceUrl} preload="metadata" aria-label="Nghe thoại" />
        ) : snap.lipSyncReady || snap.input.hasLipSync ? (
          <p>Nghe trên video đã lồng tiếng. Không tạo thoại mới.</p>
        ) : snap.voiceDurationSec > 0.2 ? (
          <p>Bản nghe mất khỏi phiên. Bấm Nạp thoại — không tạo TTS mới.</p>
        ) : dialogue ? (
          <p>Chưa có bản nghe thoại.</p>
        ) : null}
        {hold?.reason && /thoại/i.test(hold.reason) && onOpenVoice ? (
          <Button onClick={onOpenVoice}>{directorDesk ? 'Mở Người' : 'Xem thoại'}</Button>
        ) : null}
      </div>
      {onActingBeat ? (
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
      {renderCard(current, shotNo >= 0 ? shotNo : 0, { hideIdentity: true, tone: directorDesk ? 'director' : 'staff' })}
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
