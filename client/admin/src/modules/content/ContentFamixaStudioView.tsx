import { useEffect, useRef, useState, Fragment, type ReactNode } from 'react';
import { Alert, Button, Checkbox, Collapse, Select, Space, Tag, Typography } from 'antd';
import { CheckOutlined, DownloadOutlined, FolderOpenOutlined, ThunderboltOutlined } from '@ant-design/icons';
import {
  SERIES_STATUS_LABEL,
  groupShotsByBeat,
  memoryLine,
  shotActionBeats,
  shotOneLiner,
  outputAspectOf,
  studioI2vPrecheck,
  studioSceneCode,
  studioSceneTitle,
  studioShotCode,
  studioShotUi,
  type FamixaSeriesEpisode,
  type FamixaSeriesShot,
  type FamixaVoiceCue,
  type SceneContinuityLock,
  type SeriesPilotState,
  type SeriesReviewAxis,
  type SeriesShotRun,
  type SeriesShotStatus,
} from './content-famixa-series';
import { type SceneKfPlanRow } from './content-famixa-batch-plan';
import { type PreviewCutPlan } from './content-famixa-preview-cut';
import { looksLikeVideoUrl } from './content-famixa-assemble';
import { ContentFamixaProdV2 } from './ContentFamixaProdV2';
import { canEnterProdStep, lipsyncVideoUrl, PROD_V2_STEPS, productionShorts, type ProdV2Step } from './content-famixa-prod-v2';
import { voiceProductionReady } from './content-famixa-voice-script';
import './content-famixa-studio.css';

const QC: { id: SeriesReviewAxis; label: string }[] = [
  { id: 'character', label: 'Nhân vật' },
  { id: 'motion', label: 'Chuyển động' },
  { id: 'emotion', label: 'Cảm xúc' },
  { id: 'canon', label: 'Continuity & Canon' },
];

function clipSrc(url?: string, sessionSrc?: string) {
  if (sessionSrc) return sessionSrc;
  return looksLikeVideoUrl(url) ? (url ?? '').trim() : '';
}

type SceneProdStep = Extract<ProdV2Step, 'shorts' | 'image' | 'video' | 'preview' | 'final'>;

export function ContentFamixaStudioView({
  shots,
  episode,
  active,
  run,
  lock,
  pilot,
  videoContext,
  prevLocked,
  prevRun,
  unlocked,
  sceneLocked,
  scriptLocked,
  shortsReady,
  sessionSrc,
  turboBusy,
  kitCredits,
  expectedCost,
  costLabel,
  engine,
  onEngine,
  keys,
  voiceProvider,
  onVoiceProvider,
  onSelectShot,
  onLockShotGraph,
  onSeconds,
  onAction,
  onPickKeyframe,
  onGenerateKeyframe,
  generateKfBusy,
  onInheritKeyframe,
  onApproveKeyframe,
  onCreateVideo,
  onCreateSceneVideo,
  onAbDiagnostic,
  onLipsync,
  onAttachLipsync,
  lipsyncBusy,
  sceneBatchLabel,
  sceneBatchDisabled,
  batchSceneShots,
  onGenerateSceneKf,
  onPickSceneKeyframe,
  onApproveSceneKf,
  onRegenerateSelectedKf,
  generateSceneKfBusy,
  sceneBatchCredits,
  runwayQuietMin,
  onClearRunwayQuiet,
  sessionSrcOf,
  cutFrom,
  cutTo,
  cutPick,
  onCutRange,
  onCutPick,
  onLockSceneMaster,
  onOutputAspect,
  onPatchSceneMaster,
  cutPlan,
  ttsUrlOf,
  onFillPreviewCut,
  onDownloadTakes,
  onAssembleCut,
  onEnsureTts,
  assembleBusy,
  assembleAspect,
  onAssembleAspect,
  placeHint,
  onAcceptPlaceChange,
  onKeepPlaceBaseline,
  onReview,
  onLockShot,
  onFailShot,
  onPickVideo,
  onOpenScript,
  onOpenVoice,
  onOpenStudio,
  onOpenTimeline,
  onOpenAdvanced,
  onOpenMemory,
  onLockScene,
  onApprovePreview,
  onPatchRun,
  onApproveScene,
  statusOf,
  runOf,
  actionOf,
  pane,
  children,
}: {
  shots: FamixaSeriesShot[];
  episode?: FamixaSeriesEpisode;
  active?: FamixaSeriesShot;
  run?: SeriesShotRun;
  lock: SceneContinuityLock;
  pilot?: SeriesPilotState;
  videoContext?: string;
  prevLocked?: FamixaSeriesShot;
  prevRun?: SeriesShotRun;
  unlocked: boolean;
  sceneLocked?: boolean;
  scriptLocked?: boolean;
  shortsReady?: boolean;
  shortsCount?: number;
  shortsLockedCount?: number;
  sessionSrc?: string;
  turboBusy?: boolean | string;
  kitCredits: number;
  runwaySpent: number;
  expectedCost: number;
  costLabel: string;
  engine: 'turbo' | 'wan';
  onEngine: (engine: 'turbo' | 'wan') => void;
  keys: { runway: boolean; fal: boolean; elevenLabs: boolean };
  voiceProvider: 'elevenlabs' | 'f5';
  onVoiceProvider: (value: 'elevenlabs' | 'f5') => void;
  onSelectShot: (shot: FamixaSeriesShot) => void;
  onLockShotGraph?: () => void;
  onSeconds?: (shotId: string, seconds: 5 | 10) => void;
  onRemoveShots?: (ids: string[]) => void;
  onAction: (value: string) => void;
  onPickKeyframe: (file: File) => void;
  onGenerateKeyframe?: () => void;
  generateKfBusy?: boolean;
  onInheritKeyframe: () => void;
  onApproveKeyframe: () => void;
  onCreateVideo: () => void;
  onCreateSceneVideo: (ids?: string[]) => void;
  onAbDiagnostic?: (successId: string, failId: string) => void;
  onLipsync?: (ids?: string[]) => void;
  onAttachLipsync?: (shotId: string) => void;
  lipsyncBusy?: boolean | string;
  sceneBatchLabel: string;
  sceneBatchDisabled: boolean;
  batchPlan?: SceneKfPlanRow[];
  batchSceneShots?: FamixaSeriesShot[];
  batchKfNew?: number;
  batchKfReuse?: number;
  onGenerateSceneKf?: (ids?: string[]) => void;
  onPickSceneKeyframe?: (file: File, shotId?: string) => void;
  onApproveSceneKf?: (ids?: string[]) => void;
  onRegenerateSelectedKf?: (ids: string[]) => void;
  generateSceneKfBusy?: boolean;
  sceneVideoReady?: number;
  sceneVideoBlocked?: number;
  sceneBatchCredits?: number;
  runwayQuietMin?: number;
  onClearRunwayQuiet?: () => void;
  sessionSrcOf?: (id: string) => string | undefined;
  onPassTake?: (shot: FamixaSeriesShot) => void;
  onFailTake?: (shot: FamixaSeriesShot) => void;
  cutFrom?: string;
  cutTo?: string;
  cutPick?: string[];
  onCutRange?: (fromId: string, toId: string) => void;
  onCutPick?: (ids: string[]) => void;
  onLockSceneMaster?: (sceneId: string, locked: boolean) => void;
  onOutputAspect?: (aspect: '16:9' | '9:16') => void;
  onPatchSceneMaster?: (
    sceneId: string,
    patch: {
      location?: string;
      time?: string;
      lighting?: string;
      wardrobe?: string;
      props?: string;
      camera?: string;
      mood?: string;
      screenDirection?: string;
      coverage?: string;
      lens?: string;
      cameraHeight?: string;
      blocking?: string;
      pacing?: string;
    },
  ) => void;
  cutPlan?: PreviewCutPlan;
  ttsUrlOf?: (lineId: string) => string | undefined;
  onFillPreviewCut?: (kind: 'story' | 'motion') => void;
  onDownloadTakes?: (ids?: string[], kind?: 'take' | 'lipsync') => void;
  onAssembleCut?: () => void;
  onEnsureTts?: () => Promise<number>;
  assembleBusy?: boolean;
  assembleAspect?: '16:9' | '9:16';
  onAssembleAspect?: (aspect: '16:9' | '9:16') => void;
  placeHint?: { from: string; to: string };
  onAcceptPlaceChange?: () => void;
  onKeepPlaceBaseline?: () => void;
  onForceKfNew?: (shot: FamixaSeriesShot) => void;
  onReview: (axis: SeriesReviewAxis, on: boolean) => void;
  onLockShot: () => void;
  onFailShot: () => void;
  onPickVideo: (file: File) => void;
  onOpenScript: () => void;
  onOpenVoice?: () => void;
  onOpenShorts: () => void;
  onOpenStudio: () => void;
  onOpenTimeline: () => void;
  onOpenAdvanced: () => void;
  onOpenMemory: () => void;
  onLockScene?: () => void;
  onApprovePreview?: () => void;
  onPatchRun?: (shotId: string, patch: Partial<SeriesShotRun>) => void;
  onApproveScene?: (sceneId: string) => void;
  statusOf: (shot: FamixaSeriesShot) => SeriesShotStatus;
  runOf?: (shot: FamixaSeriesShot) => SeriesShotRun;
  actionOf: (shot: FamixaSeriesShot) => string | undefined;
  pane: 'script' | 'voice' | 'shorts' | 'studio' | 'timeline' | 'advanced';
  children?: ReactNode;
}) {
  const kfPick = useRef<HTMLInputElement>(null);
  const vidPick = useRef<HTMLInputElement>(null);
  const [memOpen, setMemOpen] = useState(false);
  const [editAction, setEditAction] = useState(false);
  const [floor, setFloor] = useState<'scene' | 'shot'>('scene');
  const [sceneStep, setSceneStep] = useState<SceneProdStep>('shorts');
  const scenePack = batchSceneShots?.length ? batchSceneShots : shots;
  const sh = (s?: FamixaSeriesShot) => studioShotCode(s, shots);
  const code = sh(active);
  const scene = studioSceneCode(active, episode);
  const next = active ? shots[shots.findIndex((s) => s.id === active.id) + 1] : undefined;
  const play = clipSrc(run?.previewUrl, sessionSrc);
  const kfOk = Boolean(run?.keyframeDataUrl);
  const actionText = (run?.shotAction ?? '').trim() || (active ? actionOf(active) : '') || active?.story || '';
  const actionOk = Boolean(actionText.trim());
  const beats = shotActionBeats(active?.story, actionText);
  const kfLabel = code.replace(/^SH/i, 'KF') || 'KF';
  const inheritFrom = prevLocked ? sh(prevLocked) : '';
  const allLocked = shots.length > 0 && shots.every((s) => statusOf(s) === 'approved');
  const shotN = active ? shots.findIndex((s) => s.id === active.id) + 1 : 0;
  const hasTake = Boolean(play);
  const kfApproved = Boolean(
    run?.keyframeDataUrl &&
      (run.status === 'turbo_testing' ||
        run.status === 'reviewed' ||
        run.status === 'approved' ||
        run.status === 'rejected' ||
        Boolean(run.continuity && Object.values(run.continuity).some(Boolean))),
  );
  const showVideo = Boolean(kfApproved || hasTake);
  const showQc = Boolean(hasTake || run?.status === 'reviewed' || run?.status === 'approved');
  const voiceOk = Boolean(pilot && voiceProductionReady(pilot));
  const epTitle = episode?.title || studioSceneTitle(lock, episode);
  const sceneTitle = studioSceneTitle(lock, episode);
  const oneLine = shotOneLiner(active?.story, actionText);
  const precheck = studioI2vPrecheck({
    lock,
    action: run?.shotAction,
    keyframeDataUrl: run?.keyframeDataUrl,
    status: run?.status,
    unlocked,
    sceneLocked,
    scriptLocked,
    shortsReady: Boolean(shortsReady),
    engine,
    hasEngineKey: engine === 'wan' ? keys.fal : keys.runway,
    state: pilot,
    shot: active,
    videoContext,
  });
  const i2vPrompt = precheck.prompt;
  const prevKf = prevRun?.keyframeDataUrl;
  const inherited = Boolean(run?.keyframeInheritedFrom && prevLocked && run.keyframeInheritedFrom === prevLocked.id);
  const graphLocked = pilot?.shotGraphLocked === true;
  const epCode = scene.split('·')[0]?.trim() || 'EP';
  const scLabel = (scene.split('·').pop() ?? scene).trim();
  const openDetail = (s: FamixaSeriesShot) => {
    onSelectShot(s);
    setFloor('shot');
  };

  useEffect(() => {
    if (pane === 'studio') setFloor('scene');
  }, [pane]);

  return (
    <div className="fx-studio">
      <div className="fx-main">
        <div className="fx-top">
          <div>
            <p className="fx-head-ep">{epCode}</p>
            <p className="fx-head-title">
              {scLabel}
              {sceneTitle ? ` — ${sceneTitle}` : epTitle ? ` — ${epTitle}` : ''}
            </p>
            {pane === 'studio' && floor === 'scene' ? (
              <p className="fx-head-sub">
                {pilot ? productionShorts(pilot).length : 0} Shorts từ kịch bản
                {graphLocked ? ' · đã duyệt chia Short' : ' · chưa duyệt chia Short'}
              </p>
            ) : (
              <p className="fx-head-sub">
                {active
                  ? `${code}${run?.lipsynced ? ' · KHỚP MÔI' : ''} · ${shotN}/${shots.length || 0} · ${active.seconds ?? 5} giây · Shot Detail`
                  : 'Chọn một shot để sửa'}
              </p>
            )}
            {pane === 'studio' && floor === 'shot' && active ? (
              <p className="fx-head-line">{oneLine}</p>
            ) : null}
            {pane === 'studio' && floor === 'scene' ? (
              <p className="fx-gates">
                <span className={voiceOk ? 'fx-gate fx-gate--ok' : 'fx-gate'}>Voice{voiceOk ? ' ✓' : ''}</span>
                <span className={lock.locked ? 'fx-gate fx-gate--ok' : 'fx-gate'}>Continuity{lock.locked ? ' ✓' : ''}</span>
              </p>
            ) : pane === 'studio' ? (
              <p className="fx-gates">
                <span className={scriptLocked ? 'fx-gate fx-gate--ok' : 'fx-gate'}>Script{scriptLocked ? ' ✓' : ''}</span>
                <span className={voiceOk ? 'fx-gate fx-gate--ok' : 'fx-gate'}>Voice{voiceOk ? ' ✓' : ''}</span>
                <span className={lock.locked ? 'fx-gate fx-gate--ok' : 'fx-gate'}>Continuity{lock.locked ? ' ✓' : ''}</span>
              </p>
            ) : null}
            <div className="fx-steps">
              {PROD_V2_STEPS.map((s) => {
                const on =
                  (s.id === 'script' && pane === 'script') ||
                  (s.id === 'voice' && pane === 'voice') ||
                  (pane === 'studio' && floor === 'scene' && sceneStep === s.id);
                const ok =
                  (s.id === 'script' && scriptLocked) ||
                  (s.id === 'voice' && voiceOk) ||
                  (s.id === 'shorts' && graphLocked) ||
                  (s.id === 'final' && allLocked);
                return (
                  <button
                    key={s.id}
                    type="button"
                    className={`fx-step${ok ? ' fx-step--ok' : ''}${on ? ' fx-step--on' : ''}`}
                    onClick={() => {
                      if (s.id === 'script') {
                        onOpenScript();
                        return;
                      }
                      if (s.id === 'voice') {
                        (onOpenVoice ?? onOpenScript)();
                        return;
                      }
                      const why = pilot ? canEnterProdStep(pilot, s.id) : 'Chưa có kịch bản.';
                      if (why && s.id !== 'shorts') {
                        if (!scriptLocked) {
                          onOpenScript();
                          return;
                        }
                        if (!voiceOk) {
                          (onOpenVoice ?? onOpenScript)();
                          return;
                        }
                        setFloor('scene');
                        setSceneStep('shorts');
                        onOpenStudio();
                        return;
                      }
                      setFloor('scene');
                      setSceneStep(s.id);
                      onOpenStudio();
                    }}
                  >
                    {s.n} {s.label}
                    {ok ? ' ✓' : ''}
                  </button>
                );
              })}
              <button type="button" className={`fx-step${pane === 'advanced' ? ' fx-step--on' : ''}`} onClick={onOpenAdvanced}>
                Nâng cao
              </button>
            </div>
          </div>
        </div>

        {pane !== 'studio' ? <div className="fx-pane">{children}</div> : null}

        {pane === 'studio' && !voiceOk ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 12 }}
            message="Khóa Full Voice ở Kịch bản trước khi tạo KF / video (trừ credit)."
            action={
              <Button size="small" onClick={onOpenScript}>
                Mở Kịch bản
              </Button>
            }
          />
        ) : null}


        {pane === 'studio' && floor === 'scene' && !(pilot ? productionShorts(pilot).length : scenePack.length) ? (
          <Alert
            type="info"
            showIcon
            message="Chưa có Short từ kịch bản."
            description="Nhận pack rồi duyệt thoại. KIT chỉ chia Short khi có Script Beat — không pad số lượng."
            style={{ marginBottom: 12 }}
          />
        ) : null}

        {pane === 'studio' && floor === 'scene' && pilot && onCutRange && onGenerateSceneKf ? (
          <ContentFamixaProdV2
            step={sceneStep}
            onStep={(s) => {
              if (s === 'script') onOpenScript();
              else if (s === 'voice') (onOpenVoice ?? onOpenScript)();
              else setSceneStep(s);
            }}
            state={pilot}
            queue={scenePack}
            fromId={cutFrom}
            toId={cutTo}
            pickIds={cutPick}
            onRange={onCutRange}
            onPickIds={onCutPick}
            onLockSceneMaster={onLockSceneMaster}
            onOutputAspect={onOutputAspect}
            onPatchSceneMaster={onPatchSceneMaster}
            onSeconds={(id, sec) => onSeconds?.(id, sec)}
            onLockShotGraph={onLockShotGraph}
            onOpenShot={openDetail}
            runOf={(s) => runOf?.(s) ?? { status: statusOf(s) }}
            actionOf={actionOf}
            lock={lock}
            episode={episode}
            generateKfBusy={generateSceneKfBusy}
            turboBusy={turboBusy}
            onGenerateKf={(ids) => onGenerateSceneKf?.(ids)}
            onPickKf={onPickSceneKeyframe}
            onApproveKf={onApproveSceneKf}
            onRegenerateKf={onRegenerateSelectedKf}
            onCreateVideo={(ids) => onCreateSceneVideo(ids)}
            onAbDiagnostic={onAbDiagnostic}
            onLipsync={onLipsync}
            onAttachLipsync={onAttachLipsync}
            lipsyncBusy={lipsyncBusy}
            cutPlan={cutPlan}
            ttsUrlOf={ttsUrlOf}
            sessionSrcOf={sessionSrcOf}
            onFillPreview={onFillPreviewCut}
            onDownloadTakes={onDownloadTakes}
            onAssembleCut={onAssembleCut}
            onEnsureTts={onEnsureTts}
            assembleBusy={assembleBusy}
            assembleAspect={assembleAspect}
            onAssembleAspect={onAssembleAspect}
            sceneBatchLabel={sceneBatchLabel}
            sceneBatchDisabled={sceneBatchDisabled}
            sceneBatchCredits={sceneBatchCredits}
            runwayQuietMin={runwayQuietMin}
            onClearRunwayQuiet={onClearRunwayQuiet}
            kitCredits={kitCredits}
            onLockScene={() => onLockScene?.()}
            onApprovePreview={onApprovePreview}
            onPatchRun={onPatchRun}
            onApproveScene={onApproveScene}
            sceneLocked={sceneLocked}
            sceneReady={allLocked}
          />
        ) : null}

        {pane === 'studio' && floor === 'shot' ? (
        <>
        <div style={{ marginBottom: 10 }}>
          <Button type="link" style={{ paddingLeft: 0 }} onClick={() => setFloor('scene')}>
            ← Shorts
          </Button>
          <span className="fx-shot__hint">Chi tiết Short — sửa ngoại lệ, không phải màn dựng hàng loạt.</span>
        </div>
        <div className="fx-grid">
          <section className="fx-card fx-card--shots">
            <h3>SHOT</h3>
            {groupShotsByBeat(shots).map((g, gi, arr) => {
              const head = g.shots[0];
              if (!head) return null;
              const sc = studioSceneCode(head, episode);
              const prevSc = gi > 0 && arr[gi - 1]?.shots[0] ? studioSceneCode(arr[gi - 1]!.shots[0]!, episode) : '';
              const beatNo = g.beatId?.match(/BEAT\s*(\d+)/i)?.[1] || String(gi + 1).padStart(2, '0');
              return (
                <Fragment key={g.key}>
                  {sc !== prevSc ? <div className="fx-shot__scene">{sc}</div> : null}
                  <div className={`fx-beat${g.beatId ? '' : ' fx-beat--hold'}`}>
                    <div className="fx-beat__head">BEAT {beatNo}</div>
                    <div className="fx-beat__text">{g.label}</div>
                    {g.shots.map((s) => {
                      const on = s.id === active?.id;
                      const rowRun = runOf?.(s);
                      const ui = studioShotUi(rowRun ?? { status: statusOf(s) });
                      return (
                        <button
                          key={s.id}
                          type="button"
                          className={`fx-shot${on ? ' fx-shot--on' : ''}${ui.tone === 'locked' ? ' fx-shot--lock' : ''}`}
                          onClick={() => onSelectShot(s)}
                        >
                          <div className="fx-shot__row">
                            <span className="fx-shot__id">
                              {sh(s)} · {s.seconds}s
                            </span>
                            <span className={`fx-badge fx-badge--${ui.tone === 'locked' ? 'lock' : ui.tone === 'error' ? 'err' : ui.tone === 'warn' ? 'warn' : ui.tone === 'on' ? 'on' : 'wait'}`}>
                              {ui.label}
                            </span>
                            {rowRun?.lipsynced ? <Tag color="green">KHỚP MÔI</Tag> : null}
                          </div>
                          <div className="fx-shot__story">{shotOneLiner(s.story, actionOf(s))}</div>
                          {ui.hint ? <div className="fx-shot__hint">{ui.hint}</div> : null}
                        </button>
                      );
                    })}
                  </div>
                </Fragment>
              );
            })}
            {!graphLocked && onLockShotGraph ? (
              <Button block type="primary" onClick={onLockShotGraph} style={{ marginTop: 8 }}>
                Duyệt cách chia shot
              </Button>
            ) : null}
          </section>

          <div className="fx-work">

          <section className="fx-card fx-card--action">
            <h3>
              {code} — CHUYỆN GÌ XẢY RA
            </h3>
            {!active ? (
              <Typography.Text type="secondary">Chọn shot bên trái.</Typography.Text>
            ) : (
              <>
                {active.beatText ? (
                  <p className="fx-beat-source">
                    Script Beat: {active.beatText}
                  </p>
                ) : (
                  <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 10 }}
                    message="Không có Script Beat"
                    description="KIT không production shot này. Sửa kịch bản rồi Nhận pack, hoặc gỡ SH rỗng khi duyệt cách chia shot."
                  />
                )}
                {!unlocked ? (
                  <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 10 }}
                    message="Xem được — chưa I2V"
                    description="Khóa shot liền trước rồi mới trừ credit / khóa shot này. Action và KF vẫn sửa được."
                  />
                ) : null}
                <p className="fx-story">{actionText || 'KIT chưa tách được hành động từ kịch bản cho shot này.'}</p>
                {beats.length > 1 ? (
                  <ul className="fx-beats">
                    {beats.map((b) => (
                      <li key={b}>{b}</li>
                    ))}
                  </ul>
                ) : null}
                {placeHint ? (
                  <Alert
                    type="warning"
                    showIcon
                    style={{ margin: '8px 0' }}
                    message="Continuity có thể đổi"
                    description={
                      <span>
                        Bối cảnh baseline: {placeHint.from}. Action gợi ý: {placeHint.to}. KIT không tự sửa Memory.
                      </span>
                    }
                    action={
                      <Space direction="vertical">
                        <Button size="small" onClick={onAcceptPlaceChange}>
                          Chấp nhận — KF mới
                        </Button>
                        <Button size="small" onClick={onKeepPlaceBaseline}>
                          Giữ baseline
                        </Button>
                      </Space>
                    }
                  />
                ) : null}
                <Button type="link" style={{ paddingLeft: 0 }} onClick={() => setEditAction((v) => !v)}>
                  {editAction ? 'Ẩn sửa action' : 'Sửa action (I2V)'}
                </Button>
                {editAction ? (
                  <textarea
                    key={`${active.id}-action`}
                    value={run?.shotAction ?? ''}
                    onChange={(e) => onAction(e.target.value)}
                    placeholder="Hành động shot — KIT lấy từ kịch bản, chỉ sửa nếu lệch."
                    rows={4}
                    style={{
                      width: '100%',
                      border: '1px solid #d5deea',
                      borderRadius: 10,
                      padding: 10,
                      fontSize: 14,
                      resize: 'vertical',
                    }}
                  />
                ) : null}
              </>
            )}
          </section>

          <section className="fx-card fx-card--kf">
            <h3>
              {kfLabel} — FRAME MỞ ĐẦU
            </h3>
            <input
              ref={kfPick}
              type="file"
              accept="image/*"
              hidden
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) onPickKeyframe(file);
                e.target.value = '';
              }}
            />
            {run?.keyframeDataUrl ? (
              <div className="fx-kf-hero">
                {prevKf ? (
                  <div className="fx-kf-row">
                    <div className="fx-kf-match">
                      <span>Khớp {inheritFrom}</span>
                      <img src={prevKf} alt={`KF ${inheritFrom}`} />
                    </div>
                    <img src={run.keyframeDataUrl} alt={`${kfLabel} CLEAN`} />
                  </div>
                ) : (
                  <img src={run.keyframeDataUrl} alt={`${kfLabel} CLEAN`} />
                )}
              </div>
            ) : (
              <div className="fx-kf-hero">
                <div className="fx-kf__empty">
                  {run?.keyframeFileName
                    ? `${run.keyframeFileName} — chọn lại ảnh (F5 không giữ file)`
                    : 'Chưa có ảnh cảnh'}
                </div>
              </div>
            )}
            {inherited ? (
              <Tag color="green" style={{ marginTop: 8 }}>
                KF kế thừa từ {inheritFrom} — duyệt nếu khung cảnh không đổi
              </Tag>
            ) : null}
            <ul className="fx-checks">
              <li>{kfOk ? '✓' : '○'} Có ảnh cảnh</li>
              <li>{actionOk ? '✓' : '○'} Có hành động</li>
              <li>{kfApproved ? '✓' : '○'} Đã duyệt KF</li>
            </ul>
            <Space wrap style={{ marginTop: 10 }}>
              {onGenerateKeyframe ? (
                <Button type="primary" loading={generateKfBusy} onClick={onGenerateKeyframe}>
                  Tạo KF CLEAN
                </Button>
              ) : null}
              <Button onClick={() => kfPick.current?.click()}>Chọn ảnh KF</Button>
              {prevLocked ? (
                <Button disabled={!prevKf} onClick={onInheritKeyframe}>
                  Dùng KF {inheritFrom}
                </Button>
              ) : null}
              <Button disabled={!kfOk || kfApproved} onClick={onApproveKeyframe}>
                Duyệt KF
              </Button>
            </Space>
          </section>

          <section className={`fx-card fx-card--create${showVideo ? '' : ' fx-card--muted'}`}>
            <h3>TẠO VIDEO</h3>
            {!showVideo ? (
              <p className="fx-card__wait">Duyệt KF CLEAN rồi mới tạo video. Không trừ credit khi chưa gửi.</p>
            ) : null}
            <div className="fx-create-meta">
              <Space wrap>
                <Tag>{active?.seconds ?? 5} giây · {outputAspectOf(pilot ?? {})}</Tag>
                <Tag>{kfLabel}</Tag>
              </Space>
              <Collapse
                size="small"
                style={{ marginTop: 10 }}
                items={[
                  {
                    key: 'i2v',
                    label: 'Chi tiết gửi video',
                    children: (
                      <>
                        {i2vPrompt ? (
                          <div className="fx-prompt-preview" title={i2vPrompt}>
                            {i2vPrompt}
                          </div>
                        ) : (
                          <Typography.Paragraph type="secondary" style={{ margin: 0, fontSize: 12 }}>
                            KIT tự ghép Memory + Action. Không viết prompt Runway ở đây.
                          </Typography.Paragraph>
                        )}
                        <div style={{ marginTop: 10 }}>
                          <Space style={{ marginBottom: 4 }}>
                            <Typography.Text type="secondary">Máy dựng</Typography.Text>
                            <Tag color={(engine === 'turbo' ? keys.runway : keys.fal) ? 'success' : 'warning'}>
                              {(engine === 'turbo' ? keys.runway : keys.fal) ? 'Đã có key' : 'Chưa có key'}
                            </Tag>
                          </Space>
                          <Select
                            value={engine}
                            onChange={onEngine}
                            style={{ width: '100%' }}
                            options={[
                              { value: 'turbo', label: 'Runway Turbo' },
                              { value: 'wan', label: 'Wan 2.1 (Fal)' },
                            ]}
                          />
                          <Typography.Paragraph type="secondary" style={{ margin: '6px 0 0' }}>
                            {costLabel}
                            {engine === 'turbo' ? ` · −${expectedCost} cr` : null}
                          </Typography.Paragraph>
                          <Space style={{ margin: '10px 0 4px' }}>
                            <Typography.Text type="secondary">Giọng nói</Typography.Text>
                            <Tag
                              color={
                                (voiceProvider === 'elevenlabs' ? keys.elevenLabs : keys.fal)
                                  ? 'success'
                                  : 'warning'
                              }
                            >
                              {(voiceProvider === 'elevenlabs' ? keys.elevenLabs : keys.fal)
                                ? 'Đã có key'
                                : 'Chưa có key'}
                            </Tag>
                          </Space>
                          <Select
                            value={voiceProvider}
                            onChange={onVoiceProvider}
                            style={{ width: '100%' }}
                            options={[
                              { value: 'elevenlabs', label: 'ElevenLabs' },
                              { value: 'f5', label: 'F5-TTS (Fal)' },
                            ]}
                          />
                        </div>
                      </>
                    ),
                  },
                ]}
              />
            </div>
            <div className={`fx-ready${precheck.ok ? '' : ' fx-ready--wait'}`}>
              <strong>{precheck.ok ? 'Pre-check đạt (0 cr)' : 'Pre-check — chưa gửi, 0 cr'}</strong>
              <ul style={{ margin: '6px 0 0', padding: 0 }}>
                {precheck.items.map((item) => (
                  <li key={item.id} className={item.ok ? 'fx-ready__ok' : 'fx-ready__off'}>
                    {item.ok ? '✓' : '○'} {item.label}
                  </li>
                ))}
              </ul>
              {precheck.warnings.length ? (
                <p className="fx-ready__warn">{precheck.warnings.join(' ')}</p>
              ) : null}
            </div>
            {run?.turboError ? (
              <Alert type="error" showIcon style={{ marginBottom: 8 }} message={run.turboError} />
            ) : null}
            {run?.lipsyncError && !run.lipsynced ? (
              <Alert type="warning" showIcon style={{ marginBottom: 8 }} message={run.lipsyncError} />
            ) : null}
            <Space wrap>
              <Button
                type="primary"
                icon={<ThunderboltOutlined />}
                disabled={!precheck.ok || !active || !showVideo}
                loading={Boolean(turboBusy)}
                onClick={onCreateVideo}
              >
                {precheck.ok
                  ? engine === 'wan'
                    ? 'Tạo video · xác nhận Wan'
                    : `Tạo video · xác nhận −${expectedCost} cr`
                  : 'Pre-check chưa đạt (0 cr)'}
              </Button>
              {onLipsync && active && run?.previewUrl && !run.lipsynced ? (
                <>
                  <Button
                    loading={Boolean(lipsyncBusy)}
                    disabled={Boolean(turboBusy) || Boolean(lipsyncBusy)}
                    onClick={() => onLipsync([active.id])}
                  >
                    Khớp môi · Fal
                  </Button>
                  {onAttachLipsync ? (
                    <Button onClick={() => onAttachLipsync(active.id)}>Gắn Fal · 0$</Button>
                  ) : null}
                </>
              ) : active && run?.lipsynced && lipsyncVideoUrl(run) ? (
                <>
                  <Tag color="green">KHỚP MÔI</Tag>
                  <Button
                    icon={<DownloadOutlined />}
                    loading={assembleBusy}
                    onClick={() => onDownloadTakes?.([active.id], 'lipsync')}
                  >
                    Tải khớp môi
                  </Button>
                  <Typography.Link href={lipsyncVideoUrl(run)} target="_blank" rel="noopener noreferrer">
                    Link
                  </Typography.Link>
                </>
              ) : null}
            </Space>
          </section>

          <section className={`fx-card fx-card--qc${showQc ? '' : ' fx-card--muted'}`}>
            <h3>KIỂM TRA &amp; KHÓA</h3>
            {!showQc ? (
              <p className="fx-card__wait">Có take rồi mới QC và khóa shot.</p>
            ) : null}
            <input
              ref={vidPick}
              type="file"
              accept="video/*,.mp4,.webm,.mov,.m4v"
              hidden
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) onPickVideo(file);
                e.target.value = '';
              }}
            />
            {play ? (
              <video src={play} controls playsInline preload="metadata" />
            ) : (
              <div className="fx-qc__empty">Chưa có take — chọn file trên máy hoặc tạo video</div>
            )}
            <Space wrap style={{ margin: '8px 0' }}>
              <Button size="small" icon={<FolderOpenOutlined />} onClick={() => vidPick.current?.click()}>
                Chọn video trên máy
              </Button>
            </Space>
            {QC.map((a) => (
              <div key={a.id}>
                <Checkbox
                  checked={Boolean(run?.review?.[a.id])}
                  disabled={!unlocked || !showQc}
                  onChange={(e) => onReview(a.id, e.target.checked)}
                >
                  {a.label}
                </Checkbox>
              </div>
            ))}
            <Space wrap style={{ marginTop: 10 }}>
              {run?.status === 'approved' ? (
                <Tag color="green">{code} đã khóa</Tag>
              ) : (
                <>
                  <Button
                    type="primary"
                    className="fx-lock-btn"
                    icon={<CheckOutlined />}
                    disabled={!unlocked || !showQc}
                    onClick={onLockShot}
                  >
                    Khóa {code} (LOCK)
                  </Button>
                  <Button disabled={!unlocked || !showQc} onClick={onFailShot}>
                    Không đạt
                  </Button>
                </>
              )}
            </Space>
            <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
              Sau khi khóa, shot sau sẽ được mở để làm tiếp.
            </Typography.Paragraph>
          </section>
        </div>
        </div>

        <div className="fx-foot">
          <span>
            <strong>TIẾP THEO</strong>
            {' — '}
            {next
              ? `Sau khi khóa ${code}, hệ thống sẽ mở ${sh(next)}.`
              : allLocked
                ? 'Hết shot đã khóa. Timeline · Final để ghi chú ghép và khóa cảnh.'
                : `Sau khi khóa ${code || 'shot này'}, thêm shot hoặc nhận pack ở Cài đặt nâng cao.`}
          </span>
          {next ? (
            <Button type="primary" onClick={() => onSelectShot(next)}>
              Sang {sh(next)} →
            </Button>
          ) : (
            <Button type="primary" onClick={onOpenTimeline}>
              {allLocked ? 'Timeline · Final →' : 'Xem Timeline'}
            </Button>
          )}
        </div>
        </>
        ) : null}
      </div>

      <aside className="fx-mem fx-mem--slim">
        <h3>CONTINUITY</h3>
        <Tag color={lock.locked ? 'green' : 'gold'}>{lock.locked ? 'LOCKED' : 'Chưa khóa'}</Tag>
        <p className="fx-mem__line">
          <strong>{memoryLine(lock.characters, 32) || 'CHAR'}</strong>
          {lock.wardrobe ? <><br />{memoryLine(lock.wardrobe, 40)}</> : null}
        </p>
        {lock.environment ? (
          <p className="fx-mem__line">
            <span className="fx-mem__k">Bối cảnh</span>
            {memoryLine(lock.environment, 48)}
          </p>
        ) : null}
        {lock.camera ? (
          <p className="fx-mem__line">
            <span className="fx-mem__k">Camera</span>
            {memoryLine(lock.camera, 40)}
          </p>
        ) : null}
        {placeHint ? (
          <Alert
            type="warning"
            showIcon
            style={{ margin: '8px 0' }}
            message="Có thay đổi bối cảnh"
            description={placeHint.to}
            action={
              <Space direction="vertical" size={4}>
                <Button size="small" onClick={onAcceptPlaceChange}>
                  Xem thay đổi
                </Button>
                <Button size="small" onClick={onKeepPlaceBaseline}>
                  Giữ baseline
                </Button>
              </Space>
            }
          />
        ) : null}
        <Button type="link" style={{ paddingLeft: 0 }} onClick={() => setMemOpen((v) => !v)}>
          {memOpen ? 'Thu gọn' : 'Xem chi tiết'}
        </Button>
        {memOpen ? (
          <>
            <dl>
              <dt>Nhân vật</dt>
              <dd>{lock.characters || '—'}</dd>
              <dt>Trang phục</dt>
              <dd>{lock.wardrobe || '—'}</dd>
              <dt>Bối cảnh</dt>
              <dd>{lock.environment || '—'}</dd>
              <dt>Vị trí</dt>
              <dd>{lock.position || '—'}</dd>
              <dt>Camera</dt>
              <dd>{lock.camera || '—'}</dd>
              <dt>Performance</dt>
              <dd>{lock.performance || '—'}</dd>
            </dl>
            <Button type="link" style={{ paddingLeft: 0 }} onClick={onOpenMemory}>
              Mở Memory
            </Button>
          </>
        ) : null}
      </aside>
    </div>
  );
}

export function FamixaTimelinePane({
  shots,
  episode,
  lock,
  sceneNotes,
  sceneReady,
  sceneLocked,
  statusOf,
  actionOf,
  runOf,
  cuesOf,
  kitCredits,
  runwaySpent,
  onNotes,
  onLockScene,
  onUnlockScene,
  onOpenShot,
}: {
  shots: FamixaSeriesShot[];
  episode?: FamixaSeriesEpisode;
  lock: SceneContinuityLock;
  sceneNotes?: string;
  sceneReady: boolean;
  sceneLocked?: boolean;
  statusOf: (shot: FamixaSeriesShot) => SeriesShotStatus;
  actionOf: (shot: FamixaSeriesShot) => string | undefined;
  runOf: (shot: FamixaSeriesShot) => SeriesShotRun;
  cuesOf?: (shot: FamixaSeriesShot) => FamixaVoiceCue[];
  kitCredits: number;
  runwaySpent: number;
  onNotes: (value: string) => void;
  onLockScene: () => void;
  onUnlockScene: () => void;
  onOpenShot: (shot: FamixaSeriesShot) => void;
}) {
  const total = shots.reduce((n, s) => n + (s.seconds || 5), 0);
  const locked = shots.filter((s) => statusOf(s) === 'approved').length;
  const pending = shots.filter((s) => statusOf(s) !== 'approved').map((s) => studioShotCode(s, shots));
  let clock = 0;
  return (
    <div>
      <Typography.Title level={5} style={{ marginTop: 0 }}>
        Timeline · {studioSceneCode(shots[0], episode).replace(' · ', ' > ')} {studioSceneTitle(lock, episode)}
        {sceneLocked ? (
          <Tag color="green" style={{ marginLeft: 8 }}>
            FINAL
          </Tag>
        ) : null}
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Plan cảnh · {locked}/{shots.length} LOCK · {total}s · Continuity {lock.locked ? 'đã khóa' : 'chưa khóa'}
        {lock.sourceShotId ? ` · Baseline ${lock.sourceShotId}` : ''}
      </Typography.Paragraph>
      {shots.length === 0 ? (
        <Alert type="info" showIcon message="Chưa có shot từ kịch bản. Nhận pack Story rồi duyệt cách chia shot." />
      ) : (
        <div className="fx-timeline">
          {shots.map((s) => {
            const st = statusOf(s);
            const lab = SERIES_STATUS_LABEL[st];
            const start = clock;
            clock += s.seconds || 5;
            const take = runOf(s);
            const hasTake = Boolean(take.previewUrl || take.localVideoPath);
            const cues = cuesOf?.(s) ?? [];
            return (
              <button
                key={s.id}
                type="button"
                className={`fx-timeline__row${st === 'approved' ? ' fx-timeline__row--lock' : ''}${st === 'story_locked' ? ' fx-timeline__row--wait' : ''}`}
                onClick={() => onOpenShot(s)}
              >
                <span className="fx-timeline__t">{start}s</span>
                <span className="fx-timeline__code">
                  {studioShotCode(s, shots)}
                  {take.lipsynced ? ' · KHỚP MÔI' : ''}
                </span>
                <span className="fx-timeline__sec">{s.seconds}s</span>
                <span className="fx-timeline__story">
                  {shotOneLiner(s.story, actionOf(s))}
                  {cues.map((c) => (
                    <span key={c.characterId} className="fx-timeline__cue">
                      {c.inSec}–{c.outSec}s · {c.name}: {c.text}
                    </span>
                  ))}
                </span>
                <Space size={4}>
                  {hasTake ? <Tag>Take</Tag> : null}
                  {take.lipsynced ? <Tag color="green">KHỚP MÔI</Tag> : null}
                  {cues.length ? <Tag>Thoại</Tag> : null}
                  <Tag color={lab.color}>{st === 'approved' ? 'LOCK' : lab.text}</Tag>
                </Space>
              </button>
            );
          })}
        </div>
      )}
      <Typography.Paragraph type="secondary" style={{ marginTop: 10 }}>
        Shot graph theo Script Beat. KIT không thêm SH rỗng trên Timeline.
      </Typography.Paragraph>

      <div className="fx-card" style={{ marginTop: 16 }}>
        <h3>Final · khóa cảnh</h3>
        {!sceneReady ? (
          <Alert
            type="warning"
            showIcon
            message={`Khóa hết shot dài rồi mới Final. Còn: ${pending.join(', ') || '—'}`}
          />
        ) : (
          <>
            <Typography.Paragraph type="secondary">
              KIT không ghép file tự động. Kiểm tra thứ tự LOCK ({total}s). Runway đã trừ {runwaySpent} cr · sổ
              KIT khi khóa {kitCredits} cr. Dán link timeline / ghi chú xuất, rồi khóa cảnh.
            </Typography.Paragraph>
            <textarea
              rows={3}
              disabled={sceneLocked}
              value={sceneNotes ?? ''}
              onChange={(e) => onNotes(e.target.value)}
              placeholder="Link timeline / ghi chú ghép…"
              style={{ width: '100%', border: '1px solid #d5deea', borderRadius: 8, padding: 8 }}
            />
            <Space wrap style={{ marginTop: 10 }}>
              {sceneLocked ? (
                <>
                  <Tag color="green">Cảnh đã Final</Tag>
                  <Button onClick={onUnlockScene}>Mở khóa cảnh</Button>
                </>
              ) : (
                <Button type="primary" onClick={onLockScene}>
                  Khóa scene (Final)
                </Button>
              )}
            </Space>
          </>
        )}
      </div>
    </div>
  );
}
