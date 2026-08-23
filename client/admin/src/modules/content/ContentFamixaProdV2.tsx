/** Famixa Production Workflow V2 screens: Shorts → Image → Video → Preview → Final. */

import { Alert, Button, Checkbox, Drawer, Input, Radio, Space, Tag, Typography } from 'antd';
import { DownloadOutlined, FolderOpenOutlined } from '@ant-design/icons';
import { useEffect, useRef, useState } from 'react';
import {
  clampShortSeconds,
  kfFollowIds,
  kfNeedIds,
  visualLockShot,
  selectPreset,
  selectionSummary,
  shortSimpleStatus,
  shortsByScene,
  videoNeedIds,
  lipsyncNeedIds,
  lipsyncInFlight,
  lipsyncVideoUrl,
  takeVideoUrl,
  shouldResumeLipsync,
  inferRunwayBilled,
  productionCostLedger,
  shouldResumeTurboPoll,
  isTurboRateLimit,
  turboInFlight,
  type ProdV2Step,
} from './content-famixa-prod-v2';
import {
  canManualRetry,
  classifyVideoPipe,
  compareRunwayJobs,
  formatRunwayDiagnostic,
  formatProductionLog,
  latestAttempt,
  parseFailureCode,
  sameKfAsInternalFail,
  videoPipeLabel,
} from './content-famixa-runway-pipe';
import {
  groupShotsByBeat,
  outputAspectOf,
  outputFrameOf,
  sceneHasKeyframe,
  studioSceneTitle,
  studioShotCode,
  type FamixaSeriesEpisode,
  type FamixaSeriesShot,
  type SceneContinuityLock,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';
import { existingTakesReady, rangeTakesReady } from './content-famixa-assemble';
import { type PreviewCutPlan } from './content-famixa-preview-cut';
import { ContentFamixaPreviewCutCard } from './ContentFamixaPreviewCutCard';
import { continuityPlaceHint, kfIsApproved } from './content-famixa-batch-plan';
import { buildContinuityChain } from './content-famixa-continuity-chain';
import {
  PROD_GATES,
  buildTimelineLanes,
  compileShotSceneCard,
  previousSceneKf,
  prodGateState,
  sceneIdOfShot,
  sceneMasterOf,
  shotProdStatus,
  fullEpisodeBlockReason,
} from './content-famixa-scene-first';
import { buildAssembleTimeline } from './content-famixa-assemble';
import { resolveFinalSource, resolveTakeUrl } from './content-famixa-final-source';
import { multiSpeakerBlock } from './content-famixa-dialogue-map';
import { actingOfLines } from './content-famixa-acting-law';
import { consecutiveDialogueWarning, emotionArcJumpWarning, sceneHasReactionShot } from './content-famixa-performance';
import { compileKfRewrite, emptyKfRewrite, sanitizeKfRewrite, type KfRewrite } from './content-famixa-kf-rewrite';
import {
  applyOperatorCheck,
  approveBlockReason,
  storyboardLabel,
  visualQaAllowsApprove,
} from './content-famixa-visual-spec';
import { rewriteContentSeriesKfNote } from '../../shared/api/content.api';
import './content-famixa-studio.css';

function kfOrigin(run: SeriesShotRun) {
  if (!run.keyframeDataUrl) return 'NONE';
  if (run.keyframeInheritedFrom) return 'REUSE';
  const name = (run.keyframeFileName || '').toLowerCase();
  if (/-canon\.(png|jpe?g|webp)$/.test(name) || /kf-.+-canon/.test(name)) return 'AI';
  if (run.keyframeFileName || run.keyframePath) return 'SK';
  return 'AI';
}

function kfLabel(run: SeriesShotRun) {
  const origin = kfOrigin(run);
  if (origin === 'NONE') return 'NONE';
  if (kfIsApproved(run)) return `${origin} · READY`;
  return `${origin} · DRAFT`;
}

export function ContentFamixaProdV2({
  step,
  onStep,
  state,
  queue,
  fromId,
  toId,
  pickIds,
  onRange,
  onPickIds,
  onLockSceneMaster,
  onOutputAspect,
  onPatchSceneMaster,
  onSeconds,
  onLockShotGraph,
  onOpenShot,
  runOf,
  actionOf,
  lock,
  episode,
  generateKfBusy,
  turboBusy,
  onGenerateKf,
  onPickKf,
  onApproveKf,
  onRegenerateKf,
  onCreateVideo,
  onAbDiagnostic,
  onLipsync,
  onAttachLipsync,
  lipsyncBusy,
  cutPlan,
  ttsUrlOf,
  sessionSrcOf,
  onFillPreview,
  onDownloadTakes,
  onAssembleCut,
  onEnsureTts,
  assembleBusy,
  assembleAspect,
  onAssembleAspect,
  sceneBatchLabel,
  sceneBatchDisabled,
  sceneBatchCredits,
  runwayQuietMin,
  onClearRunwayQuiet,
  kitCredits,
  onLockScene,
  sceneLocked,
  onApprovePreview,
  onPatchRun,
  onApproveScene,
}: {
  step: ProdV2Step;
  onStep: (step: ProdV2Step) => void;
  state: SeriesPilotState;
  queue: FamixaSeriesShot[];
  fromId?: string;
  toId?: string;
  pickIds?: string[];
  onRange: (fromId: string, toId: string) => void;
  onPickIds?: (ids: string[]) => void;
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
  onSeconds: (shotId: string, seconds: 5 | 10) => void;
  onLockShotGraph?: () => void;
  onOpenShot: (shot: FamixaSeriesShot) => void;
  runOf: (shot: FamixaSeriesShot) => SeriesShotRun;
  actionOf: (shot: FamixaSeriesShot) => string | undefined;
  lock: SceneContinuityLock;
  episode?: FamixaSeriesEpisode;
  generateKfBusy?: boolean;
  turboBusy?: boolean | string;
  onGenerateKf: (ids?: string[]) => void;
  onPickKf?: (file: File, shotId?: string) => void;
  onApproveKf?: (ids?: string[]) => void;
  onRegenerateKf?: (ids: string[], continuityNote?: string) => void;
  onCreateVideo: (ids?: string[]) => void;
  onAbDiagnostic?: (successId: string, failId: string) => void;
  onLipsync?: (ids?: string[]) => void;
  onAttachLipsync?: (shotId: string) => void;
  lipsyncBusy?: boolean | string;
  cutPlan?: PreviewCutPlan;
  ttsUrlOf?: (lineId: string) => string | undefined;
  sessionSrcOf?: (id: string) => string | undefined;
  onFillPreview?: (kind: 'story' | 'motion') => void;
  onDownloadTakes?: (ids?: string[], kind?: 'take' | 'lipsync') => void;
  onAssembleCut?: () => void;
  onEnsureTts?: () => Promise<number>;
  assembleBusy?: boolean;
  assembleAspect?: '16:9' | '9:16';
  onAssembleAspect?: (aspect: '16:9' | '9:16') => void;
  sceneBatchLabel: string;
  sceneBatchDisabled: boolean;
  sceneBatchCredits?: number;
  runwayQuietMin?: number;
  onClearRunwayQuiet?: () => void;
  kitCredits: number;
  onLockScene: () => void;
  sceneLocked?: boolean;
  sceneReady?: boolean;
  onApprovePreview?: () => void;
  onPatchRun?: (shotId: string, patch: Partial<SeriesShotRun>) => void;
  onApproveScene?: (sceneId: string) => void;
}) {
  const pick = useRef<HTMLInputElement>(null);
  const [logShotId, setLogShotId] = useState<string | undefined>();
  const [logCompareId, setLogCompareId] = useState<string | undefined>();
  const sum = selectionSummary(queue, fromId, toId, pickIds);
  const sel = sum.shots;
  const graphLocked = state.shotGraphLocked === true;
  const kfNeed = kfFollowIds(state, sel);
  const kfEmpty = kfNeedIds(state, sel);
  const vidNeed = videoNeedIds(state, sel);
  const vidSend = sel
    .filter((s) => {
      if (!vidNeed.includes(s.id)) return false;
      const retry = canManualRetry(runOf(s));
      return retry.kind === 'resume' || classifyVideoPipe(runOf(s)) === 'VIDEO_NOT_SENT';
    })
    .map((s) => s.id);
  const lipNeed = lipsyncNeedIds(state, sel);
  const lipReady = sel.filter((s) => lipsyncVideoUrl(runOf(s)));
  const takeN = sel.filter((s) => takeVideoUrl(runOf(s))).length;
  const takesOk = cutPlan ? rangeTakesReady(cutPlan) : takeN === sel.length && sel.length > 0;
  const haveTakes = cutPlan ? existingTakesReady(cutPlan) : takeN > 0;
  const epTitle = episode?.title || studioSceneTitle(lock, episode) || 'EP';

  const applyPreset = (preset: 5 | 10 | 20 | 'all') => {
    const next = selectPreset(queue, preset);
    if (next.fromId && next.toId) onRange(next.fromId, next.toId);
    const range = selectionSummary(queue, next.fromId, next.toId).ids;
    onPickIds?.(range);
  };

  const togglePick = (shot: FamixaSeriesShot) => {
    const cur = new Set(pickIds?.length ? pickIds : sel.map((s) => s.id));
    if (cur.has(shot.id)) cur.delete(shot.id);
    else cur.add(shot.id);
    const ids = queue.filter((s) => cur.has(s.id)).map((s) => s.id);
    onPickIds?.(ids);
    if (ids.length) onRange(ids[0]!, ids.at(-1)!);
  };

  const dropFromCut = (ids: string[]) => {
    const drop = new Set(ids);
    const next = sel.filter((s) => !drop.has(s.id)).map((s) => s.id);
    if (!next.length) return false;
    onPickIds?.(next);
    onRange(next[0]!, next.at(-1)!);
    return true;
  };

  const selectedSet = new Set(sel.map((s) => s.id));
  const [kfPicked, setKfPicked] = useState<string[]>([]);
  const [kfMethod, setKfMethod] = useState<'ai' | 'sk'>('ai');
  const [kfDetailId, setKfDetailId] = useState<string | undefined>(undefined);
  const [kfUserNote, setKfUserNote] = useState('');
  const [kfRewrite, setKfRewrite] = useState<KfRewrite>(emptyKfRewrite());
  const [kfRewriteBusy, setKfRewriteBusy] = useState(false);
  const pickFor = useRef<string | undefined>(undefined);
  const selKey = sel.map((s) => s.id).join('|');
  useEffect(() => {
    const lock = visualLockShot(state, sel);
    setKfPicked(sel.filter((s) => s.id !== lock?.id).map((s) => s.id));
  }, [selKey]);
  const kfDetail = sel.find((s) => s.id === kfDetailId) || queue.find((s) => s.id === kfDetailId);
  const compileKfNote = async () => {
    if (!kfDetail) return;
    const ctx = { action: actionOf(kfDetail) || kfDetail.story, location: lock.environment || kfDetail.location };
    const local = compileKfRewrite(kfUserNote, ctx);
    setKfRewrite(local);
    if (kfUserNote.trim().length < 4) return;
    setKfRewriteBusy(true);
    try {
      const ai = await rewriteContentSeriesKfNote({
        note: kfUserNote.trim(),
        action: ctx.action,
        location: ctx.location,
      });
      setKfRewrite(
        sanitizeKfRewrite(
          {
            instruction: ai.instruction,
            place: ai.place,
            lighting: ai.lighting,
            wardrobe: ai.wardrobe,
            camera: ai.camera,
            inherit: ai.inherit,
            source: 'ai',
          },
          local,
        ),
      );
    } catch (e) {
      /* local command already shown */
      void e;
    } finally {
      setKfRewriteBusy(false);
    }
  };

  return (
    <div className="fx-prod">
      {step === 'shorts' ? (
        <section className="fx-card">
          <h3>
            {episode?.episode || 'EP01'} — {epTitle}
          </h3>
          <p className="fx-plan">
            {queue.length} Shorts · {queue.reduce((n, s) => n + clampShortSeconds(s.seconds), 0)}s
            {graphLocked ? ' · SHOT GRAPH LOCKED' : ' · chưa duyệt cách chia'}
          </p>
          <Typography.Paragraph type="secondary">
            Shot sinh từ kịch bản đã khóa. Tick từng Shot hoặc chọn 5/10. Không pad số lượng. Preview không cần cả tập.
          </Typography.Paragraph>
          {!graphLocked ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message="Chưa duyệt cách chia Short"
              description="KIT không tạo SH rỗng. Duyệt cấu trúc rồi mới sang Hình."
            />
          ) : null}
          <Space wrap style={{ marginBottom: 12 }}>
            <Button onClick={() => applyPreset(5)}>Chọn 5 Short đầu</Button>
            <Button onClick={() => applyPreset(10)}>Chọn 10 Short</Button>
            <Button onClick={() => applyPreset(20)}>Chọn 20 Short</Button>
            <Button onClick={() => applyPreset('all')}>Chọn tất cả</Button>
          </Space>
          <p className="fx-plan">
            Đã chọn: {sum.count} / {sum.total} Shorts · {sum.from} → {sum.to} · {sum.sec}s
            {cutPlan
              ? ` · Thoại ${cutPlan.items.filter((i) => !i.silent).length} · Câm ${cutPlan.items.filter((i) => i.silent).length} · Voice ${cutPlan.items.filter((i) => !i.silent && i.hasVoiceFile).length}/${cutPlan.items.filter((i) => !i.silent).length}`
              : ''}
          </p>
          {cutPlan?.extraLines.length ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message={`${cutPlan.extraLines.length} câu thoại chưa gắn Short — không ghép theo thứ tự file.`}
              description={`${cutPlan.extraLines[0]?.name}: “${(cutPlan.extraLines[0]?.text ?? '').slice(0, 80)}”`}
            />
          ) : null}
          {shortsByScene(queue).map((block) => {
            const scene = (state.scenes ?? []).find((s) => s.id === block.scene);
            return (
              <div key={block.scene} className="fx-prod-scene">
                <div className="fx-prod-scene__h">
                  {block.scene}
                  {scene?.title ? ` — ${scene.title}` : ''}
                  <span>
                    {' '}
                    · {block.shots.length} beat ·{' '}
                    {block.shots.reduce((n, s) => n + clampShortSeconds(s.seconds), 0)}s
                    {state.sceneApproved?.[block.scene] ? ' · PREVIEW OK' : ''}
                  </span>
                  <Button
                    size="small"
                    onClick={() => {
                      const a = block.shots[0];
                      const b = block.shots.at(-1);
                      if (a && b) {
                        onRange(a.id, b.id);
                        onPickIds?.(block.shots.map((s) => s.id));
                        onStep('preview');
                      }
                    }}
                  >
                    Preview {block.scene}
                  </Button>
                  <Button
                    size="small"
                    disabled={Boolean(state.sceneApproved?.[block.scene])}
                    onClick={() => onApproveScene?.(block.scene)}
                  >
                    {state.sceneApproved?.[block.scene] ? 'Đã duyệt Scene' : `Duyệt ${block.scene}`}
                  </Button>
                </div>
                {groupShotsByBeat(block.shots).map((g, gi) => (
                  <div key={g.key} className="fx-beat">
                    <div className="fx-beat__head">BEAT {String(gi + 1).padStart(2, '0')}</div>
                    <div className="fx-beat__text">{g.label}</div>
                    {g.shots.map((s) => {
                      const on = selectedSet.has(s.id);
                      const st = shortSimpleStatus(state, s);
                      const run = runOf(s);
                      const item = cutPlan?.items.find((i) => i.shotId === s.id);
                      const place = lock.locked ? continuityPlaceHint(lock, s, run.shotAction) : undefined;
                      return (
                        <label key={s.id} className={`fx-prod-row${on ? ' fx-prod-row--on' : ''}`}>
                          <Checkbox checked={on} onChange={() => togglePick(s)} />
                          <button type="button" className="fx-prod-row__main" onClick={() => onOpenShot(s)}>
                            <strong>
                              {studioShotCode(s, queue)}
                              {s.voiceChainFrom ? ' · nối KF' : ''}
                            </strong>
                            <span>{compileShotSceneCard(state, s, previousSceneKf(state, s, queue)?.shot).oneLiner}</span>
                          </button>
                          <Space size={4}>
                            <Button
                              size="small"
                              type={clampShortSeconds(s.seconds) === 5 ? 'primary' : 'default'}
                              onClick={() => onSeconds(s.id, 5)}
                            >
                              5s
                            </Button>
                            <Button
                              size="small"
                              type={clampShortSeconds(s.seconds) === 10 ? 'primary' : 'default'}
                              onClick={() => onSeconds(s.id, 10)}
                            >
                              10s
                            </Button>
                            <Tag>{st}</Tag>
                            {run.lipsynced ? <Tag color="green">KHỚP MÔI</Tag> : null}
                          {item?.silent ? <Tag>Voice: NONE</Tag> : item?.line ? <Tag>{item.lines.length} thoại</Tag> : null}
                          {item?.line ? <Tag>{actingOfLines(item.lines, actionOf(s)).label}</Tag> : null}
                          </Space>
                          {place ? (
                            <span className="fx-prod-row__warn">
                              Continuity: {place.from} → {place.to}
                            </span>
                          ) : null}
                        </label>
                      );
                    })}
                  </div>
                ))}
              </div>
            );
          })}
          <Space wrap style={{ marginTop: 12 }}>
            {!graphLocked ? (
              <Button type="primary" onClick={onLockShotGraph} disabled={!onLockShotGraph}>
                Duyệt cách chia Short
              </Button>
            ) : (
              <Button type="primary" onClick={() => onStep('image')} disabled={!sum.count}>
                Sang Hình → {sum.count} Short
              </Button>
            )}
          </Space>
        </section>
      ) : null}

      {step === 'image' ? (
        <section className="fx-card fx-kf">
          {(() => {
            const sceneId = sel[0] ? sceneIdOfShot(sel[0]) : 'SC01';
            const master = sceneMasterOf(state, sceneId);
            const sceneHead = `${master.sceneId} — ${master.title || epTitle}`;
            const template = visualLockShot(state, sel);
            const need = kfPicked.filter((id) => kfNeed.includes(id) && id !== template?.id);
            const redo = kfPicked.filter((id) => {
              if (template && id === template.id) return false;
              const hit = sel.find((s) => s.id === id) || queue.find((s) => s.id === id);
              return Boolean(hit && runOf(hit).keyframeDataUrl);
            });
            const genN = kfMethod === 'ai' ? need.length : 0;
            const fromCode = need[0] ? studioShotCode(sel.find((s) => s.id === need[0]) || queue.find((s) => s.id === need[0])!, queue) : '';
            return (
              <>
                <p className="fx-kf__kicker">04 IMAGE — Scene Master rồi mới Shot</p>
                <h3>{sceneHead}</h3>
                <p className="fx-plan">
                  {sum.count} Shot · edit {sum.sec}s · I2V {sel.reduce((n, s) => n + clampShortSeconds(s.seconds), 0)}s
                  {kfEmpty.length ? ` · ${kfEmpty.length} chưa có hình` : ''}
                </p>
                <div className="fx-master">
                  <div className="fx-master__h">
                    <strong>SCENE MASTER</strong>
                    <Tag color={master.locked ? 'green' : 'gold'}>{master.locked ? 'LOCKED' : 'CHƯA KHÓA'}</Tag>
                  </div>
                  <p className="fx-plan">Shot đổi Action và sắc thái kịch bản. Giữ phòng, áo, mặt. Đèn có thể tối hơn — không copy nụ cười khung khóa.</p>
                  <div className="fx-master__field" style={{ margin: '8px 0' }}>
                    <span>Khung xuất — khóa pixel trước khi vẽ KF</span>
                    <Radio.Group
                      value={state.outputAspect}
                      disabled={master.locked && Boolean(state.outputAspect)}
                      onChange={(e) => onOutputAspect?.(e.target.value)}
                    >
                      <Radio.Button value="16:9">16:9 phim · 1280×720</Radio.Button>
                      <Radio.Button value="9:16">9:16 Reels · 720×1280</Radio.Button>
                    </Radio.Group>
                    <p className="fx-plan">
                      {state.outputAspect
                        ? `Gemini + Runway + file cuối cùng cùng ${state.outputAspect} · ${outputFrameOf(state.outputAspect).width}×${outputFrameOf(state.outputAspect).height} JPEG. Gemini có thể trả PNG lệch — KIT crop về đúng số này, không tự chọn 1344×768.`
                        : 'Chưa chọn — không khóa Master / không vẽ KF. Pixel khóa theo khung, không gõ tay width/height.'}
                      {sceneHasKeyframe(state) ? ' Đã có KF — đổi khung phải vẽ lại.' : ''}
                    </p>
                  </div>
                  <div className="fx-master__grid">
                    {(
                      [
                        ['location', 'Location', master.location],
                        ['time', 'Time', master.time],
                        ['lighting', 'Lighting', master.lighting],
                        ['wardrobe', 'Wardrobe', master.wardrobe],
                        ['props', 'Props', master.props],
                        ['camera', 'Camera', master.camera],
                        ['screenDirection', 'Screen direction', master.screenDirection || ''],
                        ['coverage', 'Coverage', master.coverage || ''],
                        ['lens', 'Lens', master.lens || ''],
                        ['cameraHeight', 'Height', master.cameraHeight || ''],
                        ['blocking', 'Blocking', master.blocking || ''],
                        ['pacing', 'Pacing', master.pacing || ''],
                        ['emotionPrev', 'Emotion trước', master.emotionPrev || ''],
                        ['emotionNow', 'Emotion hiện', master.emotionNow || ''],
                        ['emotionNext', 'Emotion sau', master.emotionNext || ''],
                      ] as const
                    ).map(([key, label, value]) => (
                      <label key={key} className="fx-master__field">
                        <span>{label}</span>
                        <Input
                          size="small"
                          disabled={master.locked}
                          value={value}
                          onChange={(e) => onPatchSceneMaster?.(sceneId, { [key]: e.target.value })}
                        />
                      </label>
                    ))}
                  </div>
                  <p className="fx-master__chars">{master.characters || '—'}</p>
                  {(() => {
                    const talk = consecutiveDialogueWarning(state, sel);
                    const arc = emotionArcJumpWarning(master, sceneHasReactionShot(state, sel, sceneId));
                    if (!talk && !arc) return null;
                    return <Alert type="warning" showIcon style={{ marginTop: 8 }} message={talk || arc} />;
                  })()}
                  <Space wrap>
                    {master.locked ? (
                      <Button size="small" onClick={() => onLockSceneMaster?.(sceneId, false)}>
                        Mở khóa Master
                      </Button>
                    ) : (
                      <Button
                        type="primary"
                        size="small"
                        disabled={!state.outputAspect}
                        onClick={() => onLockSceneMaster?.(sceneId, true)}
                      >
                        Khóa Scene Master
                      </Button>
                    )}
                  </Space>
                </div>
                <p className="fx-gates">
                  <span className={state.scriptLocked ? 'fx-gate fx-gate--ok' : 'fx-gate'}>SCRIPT{state.scriptLocked ? ' ✓' : ''}</span>
                  <span className={state.voiceLocked ? 'fx-gate fx-gate--ok' : 'fx-gate'}>VOICE{state.voiceLocked ? ' ✓' : ''}</span>
                  <span className={master.locked ? 'fx-gate fx-gate--ok' : 'fx-gate'}>
                    MASTER{master.locked ? ' ✓' : ''}
                  </span>
                </p>
                <div className="fx-kf__batch">
                  <Radio.Group value={kfMethod} onChange={(e) => setKfMethod(e.target.value)} style={{ margin: '8px 0' }}>
                    <Radio value="sk">SK — ảnh có sẵn</Radio>
                    <Radio value="ai">AI — tuần tự từ Master + KF trước</Radio>
                  </Radio.Group>
                  <p className="fx-plan">
                    {kfMethod === 'ai'
                      ? template
                        ? `Khóa ${studioShotCode(template, queue)}. Tạo từ ${fromCode || 'shot sau'} · ${genN} hình, sắc thái kịch bản.`
                        : `Tuần tự ${genN} hình. Shot sau dựa shot trước. Sắc thái theo kịch bản.`
                      : 'SK: mở Shot → Đổi ảnh. KIT không generate.'}
                  </p>
                  {kfMethod === 'ai' ? (
                    <Space wrap>
                      <Button
                        type="primary"
                        loading={generateKfBusy}
                        disabled={!genN || !master.locked || !state.outputAspect}
                        onClick={() => onGenerateKf(need)}
                      >
                        {fromCode ? `Tạo KF từ ${fromCode} · ${genN}` : `Tạo KF tuần tự · ${genN}`}
                      </Button>
                      <Button
                        loading={generateKfBusy}
                        disabled={!redo.length || !master.locked || !state.outputAspect}
                        onClick={() => onRegenerateKf?.(redo)}
                      >
                        Tạo lại {redo.length} hình đã chọn
                      </Button>
                    </Space>
                  ) : (
                    <Alert type="info" showIcon message="SK: bấm hàng Shot → Đổi ảnh." />
                  )}
                </div>
                {(() => {
                  const labels = sel.map((s) =>
                    storyboardLabel(
                      runOf(s).visualSpec ?? compileShotSceneCard(state, s, previousSceneKf(state, s, queue)?.shot).visualSpec,
                    ),
                  );
                  const allWide = labels.length >= 3 && labels.every((l) => l === 'WIDE' || l === 'ESTABLISHING');
                  return (
                    <div className="fx-storyboard">
                      <p className="fx-storyboard__kicker">STORYBOARD STRIP</p>
                      <div className="fx-storyboard__strip">
                        {sel.map((s, i) => (
                          <button
                            key={s.id}
                            type="button"
                            className="fx-storyboard__cell"
                            onClick={() => setKfDetailId(s.id)}
                          >
                            <span>{labels[i]}</span>
                            <strong>{studioShotCode(s, queue)}</strong>
                          </button>
                        ))}
                      </div>
                      {allWide ? (
                        <Alert type="warning" showIcon message="Cả dải đều WIDE — coverage sẽ chán. Đổi MCU / CU / INSERT theo Action." />
                      ) : null}
                    </div>
                  );
                })()}
                <div className="fx-shot-seq">
                  {sel.map((s) => {
                    const run = runOf(s);
                    const item = cutPlan?.items.find((i) => i.shotId === s.id);
                    const prev = previousSceneKf(state, s, queue)?.shot;
                    const card = compileShotSceneCard(state, s, prev);
                    const spec = run.visualSpec ?? card.visualSpec;
                    const action = card.oneLiner;
                    const on = kfPicked.includes(s.id);
                    const st = shotProdStatus(state, s);
                    const qa = run.visualQa;
                    return (
                      <label key={s.id} id={`fx-kf-${s.id}`} className={`fx-shot-seq__row${on ? ' fx-shot-seq__row--on' : ''}`}>
                        <Checkbox
                          checked={on}
                          disabled={Boolean(template && s.id === template.id)}
                          onChange={() =>
                            setKfPicked((cur) => (cur.includes(s.id) ? cur.filter((id) => id !== s.id) : [...cur, s.id]))
                          }
                        />
                        <button type="button" className="fx-shot-seq__thumb" onClick={() => setKfDetailId(s.id)}>
                          {run.keyframeDataUrl ? <img src={run.keyframeDataUrl} alt="" /> : <span />}
                        </button>
                        <button type="button" className="fx-shot-seq__main" onClick={() => setKfDetailId(s.id)}>
                          <strong>
                            {studioShotCode(s, queue)} {spec.framing}
                            {s.voiceChainFrom ? ' · REUSE' : ''}
                          </strong>
                          <span>{spec.intent || action || '—'}</span>
                        </button>
                        <Tag>{template && s.id === template.id ? 'KHÓA' : st}</Tag>
                        {run.lipsynced ? <Tag color="green">KHỚP MÔI</Tag> : null}
                        <Tag color={spec.primary ? 'blue' : 'gold'}>
                          {spec.primary
                            ? `${spec.framing} · ${spec.primary.name}${spec.secondary[0] ? ` + ${spec.secondary[0].name} ${spec.secondary[0].face}` : ''}`
                            : 'Chưa khóa subject'}
                        </Tag>
                        <Tag
                          color={
                            qa?.status === 'PASS' ? 'green' : qa?.status === 'REJECT' ? 'red' : qa?.status === 'PENDING' ? 'gold' : 'default'
                          }
                        >
                          QA {qa?.total != null ? `${qa.total}` : qa?.status || '—'}
                        </Tag>
                        <span className="fx-shot-seq__sec">
                          {s.editSeconds ? `${s.editSeconds}s edit` : ''} · I2V {clampShortSeconds(s.seconds)}s
                        </span>
                        {item?.silent ? <Tag>Voice NONE</Tag> : item?.line ? <Tag>{item.lines.length} thoại</Tag> : null}
                      </label>
                    );
                  })}
                </div>
                <p className="fx-kf__next">
                  <Button type="link" onClick={() => onStep('video')} disabled={!sel.length} style={{ paddingLeft: 0 }}>
                    Duyệt KF xong → Video
                  </Button>
                </p>
                <Drawer
                  title={kfDetail ? `${studioShotCode(kfDetail, queue)} · ${clampShortSeconds(kfDetail.seconds)}s` : 'Short'}
                  open={Boolean(kfDetail)}
                  onClose={() => {
                    setKfDetailId(undefined);
                    setKfUserNote('');
                    setKfRewrite(emptyKfRewrite());
                  }}
                  width={440}
                  destroyOnClose
                >
                  {kfDetail ? (
                    <div className="fx-kf-detail">
                      {runOf(kfDetail).keyframeDataUrl ? (
                        <img src={runOf(kfDetail).keyframeDataUrl} alt="" />
                      ) : (
                        <div className="fx-kf-detail__empty">Chưa có KF</div>
                      )}
                      {(() => {
                        const prev = previousSceneKf(state, kfDetail, queue)?.shot;
                        const card = compileShotSceneCard(state, kfDetail, prev);
                        const spec = runOf(kfDetail).visualSpec ?? card.visualSpec;
                        const qa = runOf(kfDetail).visualQa;
                        const item = cutPlan?.items.find((i) => i.shotId === kfDetail.id);
                        return (
                          <>
                            <p>
                              <span>Intent</span>
                              {spec.intent}
                            </p>
                            <p>
                              <span>Framing</span>
                              {spec.shotType} · {spec.camera} · {spec.lens}
                            </p>
                            <p>
                              <span>Primary</span>
                              {spec.primary ? `${spec.primary.name} — ${spec.primary.face.toUpperCase()} FACE · ${spec.primary.body}` : '—'}
                            </p>
                            <p>
                              <span>Secondary</span>
                              {spec.secondary.length
                                ? spec.secondary.map((p) => `${p.name} — ${p.body} / ${p.face}`).join(' · ')
                                : 'NONE'}
                            </p>
                            <p>
                              <span>Required</span>
                              {spec.required.map((r) => `${qa?.checks[r.id] ? '✓' : '○'} ${r.label}`).join(' · ') || '—'}
                            </p>
                            <p>
                              <span>Continuity</span>
                              HARD {spec.hardContinuity.join(', ')}
                              {spec.inheritFromPrev ? ` · ${spec.inheritFromPrev}` : ''}
                            </p>
                            <p>
                              <span>Voice</span>
                              {!item || item.silent || !item.line ? 'NONE' : `${item.line.name}: “${item.line.text}”`}
                            </p>
                            <p>
                              <span>AI QA</span>
                              {qa?.status === 'PASS' && qa.total != null
                                ? `${qa.total}/100 PASS`
                                : qa?.status === 'REJECT'
                                  ? `REJECT ${qa.hardFails.join(', ') || qa.notes || ''}`.trim()
                                  : qa?.status === 'PENDING'
                                    ? 'PENDING — tick REQUIRED'
                                    : 'Chưa QA'}
                            </p>
                            {qa?.axes ? (
                              <p className="fx-vqa">
                                {(['character', 'face', 'action', 'prop', 'composition', 'continuity', 'emotion'] as const).map((k) =>
                                  qa.axes?.[k] != null ? (
                                    <span key={k}>
                                      {k} {qa.axes[k]}
                                    </span>
                                  ) : null,
                                )}
                              </p>
                            ) : null}
                            {spec.required.some((r) => r.hard) ? (
                              <Space wrap style={{ marginBottom: 8 }}>
                                {spec.required
                                  .filter((r) => r.hard)
                                  .map((r) => (
                                    <Checkbox
                                      key={r.id}
                                      checked={Boolean(qa?.checks[r.id])}
                                      onChange={(e) =>
                                        onPatchRun?.(kfDetail.id, {
                                          visualSpec: spec,
                                          visualQa: applyOperatorCheck(qa ?? { status: 'PENDING', hardFails: [], checks: {} }, r.id, e.target.checked),
                                        })
                                      }
                                    >
                                      {r.label}
                                    </Checkbox>
                                  ))}
                              </Space>
                            ) : null}
                          </>
                        );
                      })()}
                      <p>
                        <span>KF</span>
                        {kfLabel(runOf(kfDetail))}
                      </p>
                      <p>
                        <span>Method</span>
                        {kfOrigin(runOf(kfDetail))}
                      </p>
                      <p>
                        <span>Vì sao tạo lại</span>
                      </p>
                      <Input.TextArea
                        rows={3}
                        maxLength={400}
                        value={kfUserNote}
                        onChange={(e) => setKfUserNote(e.target.value)}
                        placeholder="Lời thường, ví dụ: phòng này sáng ban ngày, shot trước đang ăn tối."
                      />
                      <Space wrap style={{ margin: '8px 0' }}>
                        <Button size="small" loading={kfRewriteBusy} onClick={() => void compileKfNote()}>
                          KIT viết lệnh
                        </Button>
                        {kfRewrite.source === 'ai' ? <Tag color="green">AI</Tag> : kfRewrite.instruction ? <Tag>Máy</Tag> : null}
                      </Space>
                      <Space wrap>
                        <Checkbox checked={kfRewrite.place} onChange={(e) => setKfRewrite((n) => ({ ...n, place: e.target.checked }))}>
                          Sai chỗ
                        </Checkbox>
                        <Checkbox
                          checked={kfRewrite.lighting}
                          onChange={(e) => setKfRewrite((n) => ({ ...n, lighting: e.target.checked }))}
                        >
                          Sai đèn
                        </Checkbox>
                        <Checkbox
                          checked={kfRewrite.wardrobe}
                          onChange={(e) => setKfRewrite((n) => ({ ...n, wardrobe: e.target.checked }))}
                        >
                          Sai áo
                        </Checkbox>
                        <Checkbox checked={kfRewrite.inherit} onChange={(e) => setKfRewrite((n) => ({ ...n, inherit: e.target.checked }))}>
                          Giữ shot trước
                        </Checkbox>
                      </Space>
                      {kfRewrite.instruction ? (
                        <Input.TextArea
                          rows={3}
                          value={kfRewrite.instruction}
                          onChange={(e) => setKfRewrite((n) => ({ ...n, instruction: e.target.value }))}
                          style={{ marginTop: 8 }}
                        />
                      ) : (
                        <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                          Để trống mô tả vẫn Tạo lại được — KIT chỉ giữ shot trước. Không gửi lời thường vào vẽ.
                        </Typography.Paragraph>
                      )}
                      <Space wrap style={{ marginTop: 12 }}>
                        <Button
                          icon={<FolderOpenOutlined />}
                          onClick={() => {
                            pickFor.current = kfDetail.id;
                            pick.current?.click();
                          }}
                        >
                          Đổi ảnh
                        </Button>
                        <Button
                          loading={generateKfBusy}
                          onClick={() => {
                            const note =
                              kfRewrite.instruction.trim() ||
                              compileKfRewrite(kfUserNote, {
                                action: actionOf(kfDetail) || kfDetail.story,
                                location: lock.environment || kfDetail.location,
                              }).instruction;
                            onRegenerateKf?.([kfDetail.id], note) ?? onGenerateKf([kfDetail.id]);
                          }}
                        >
                          Tạo lại
                        </Button>
                        <Button
                          type="primary"
                          disabled={!runOf(kfDetail).keyframeDataUrl || !visualQaAllowsApprove(runOf(kfDetail).visualQa)}
                          onClick={() => onApproveKf?.([kfDetail.id])}
                        >
                          Duyệt KF
                        </Button>
                      </Space>
                      {approveBlockReason(runOf(kfDetail).visualQa) ? (
                        <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                          {approveBlockReason(runOf(kfDetail).visualQa)}
                        </Typography.Paragraph>
                      ) : null}
                    </div>
                  ) : null}
                </Drawer>
              </>
            );
          })()}
          <input
            ref={pick}
            type="file"
            accept="image/*"
            hidden
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) onPickKf?.(file, pickFor.current);
              pickFor.current = undefined;
              e.target.value = '';
            }}
          />
        </section>
      ) : null}

      {step === 'video' ? (
        <section className="fx-card">
          <p className="fx-kf__kicker">05 VIDEO — gửi Runway / Wan từ KF đã chọn</p>
          {consecutiveDialogueWarning(state, sel) ? (
            <Alert type="warning" showIcon message={consecutiveDialogueWarning(state, sel)} />
          ) : null}
          <p className="fx-plan">
            {(() => {
              const cost = productionCostLedger(state, sel);
              return `Cost · Runway ${cost.billedRunway} cr đã trừ / ước ${cost.estimatedRunway} cr · Fal ~$${cost.confirmedFalUsd.toFixed(2)} đã khớp / ~$${cost.estimatedFalUsd.toFixed(2)} nếu gửi hết`;
            })()}
          </p>
          <h3>
            {(() => {
              const sceneId =
                (sel[0]?.sceneId || sel[0]?.scene || '').match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ||
                'SC';
              const scene = (state.scenes ?? []).find((s) => s.id === sceneId);
              return `${sceneId}${scene?.title ? ` — ${scene.title}` : ''}`;
            })()}
          </h3>
          <p className="fx-plan">
            {sum.from} → {sum.to} · {vidNeed.length} Short chưa có take · ước tính {sceneBatchCredits ?? 0} cr
            {kitCredits ? ` · sổ KIT ${kitCredits} cr` : ''}
          </p>
          <Typography.Paragraph type="secondary">
            Mỗi Short = 1 clip 5s hoặc 10s. Confirm mới gửi job. Runway trừ cr khi nhận job — fail unexpected vẫn mất cr. KIT không tự gửi lần 2. Không gửi từ bước Preview.
          </Typography.Paragraph>
          {(() => {
            const chain = buildContinuityChain(state, sel);
            const risky = chain.filter((l) => l.risks.some((r) => !r.ok));
            if (!risky.length) return null;
            return (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                message="TRANSITION RISK — kiểm END → START trước khi gửi Runway"
                description={risky
                  .map((l) => `${l.previousShotId ? l.code : l.code}: ${l.risks.filter((r) => !r.ok).map((r) => r.id).join(', ')}`)
                  .join(' · ')}
              />
            );
          })()}
          {sel.some((s) => Boolean(runOf(s).turboError) && !runOf(s).previewUrl) ? (
            <Alert
              type="error"
              showIcon
              closable
              style={{ marginBottom: 12 }}
              message={(() => {
                const failed = sel.filter((s) => runOf(s).turboError && !runOf(s).previewUrl);
                const kept = sel.filter((s) => runOf(s).previewUrl?.trim()).length;
                const billed = failed.reduce((n, s) => n + inferRunwayBilled(runOf(s), s.seconds), 0);
                return `${failed.map((s) => studioShotCode(s, queue)).join(', ')} lỗi Runway — ${kept} take đã có giữ nguyên${billed ? ` · đã trừ ~${billed} cr` : ''}.`;
              })()}
              description={
                <Space direction="vertical" size={8}>
                  <span>
                    {runwayQuietMin
                      ? `KIT còn khóa gửi job mới (nhớ 429 cũ, ~${runwayQuietMin} phút).`
                      : sel.some((s) => isTurboRateLimit(runOf(s).turboError || '') && !runOf(s).previewUrl)
                        ? '429 = hết số job/ngày. Hỏi lại · 0 cr trước.'
                        : 'Bỏ shot lỗi để test ghép take đã có — 0 cr Runway. Đừng Gửi lại hàng loạt.'}
                  </span>
                  <Button
                    type="primary"
                    onClick={() => {
                      const failed = sel.filter((s) => runOf(s).turboError && !runOf(s).previewUrl);
                      if (!dropFromCut(failed.map((s) => s.id))) return;
                      onStep('preview');
                    }}
                  >
                    Bỏ shot lỗi · Preview {takeN} take
                  </Button>
                </Space>
              }
            />
          ) : null}
          {lipNeed.length ? (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message={`${lipNeed.length} take chưa khớp môi`}
              description="Runway không nhận TTS nên miệng không theo lời. Fal 1.9 ~$0.70/phút (~$0.12/clip 10s) — nhận job là trừ. 0 cr Runway. KIT chỉ hiện KHỚP MÔI khi có file. Lỗi 405 ≠ hoàn tiền."
            />
          ) : null}
          {runwayQuietMin && onClearRunwayQuiet ? (
            <Button type="primary" onClick={onClearRunwayQuiet} style={{ marginBottom: 12 }}>
              Đã lên tier — mở gửi
            </Button>
          ) : null}
          {sel.map((s) => {
            const run = runOf(s);
            const item = cutPlan?.items.find((i) => i.shotId === s.id);
            return (
              <div key={s.id} className="fx-prod-row">
                <strong>{studioShotCode(s, queue)}</strong>
                <span>{compileShotSceneCard(state, s, previousSceneKf(state, s, queue)?.shot).oneLiner}</span>
                <Tag>{item?.silent ? 'Voice: NONE' : item?.line ? `${item.line.name}: ${item.line.text}` : 'Voice'}</Tag>
                <Space size={4}>
                  <Button size="small" type={clampShortSeconds(s.seconds) === 5 ? 'primary' : 'default'} onClick={() => onSeconds(s.id, 5)}>
                    5s
                  </Button>
                  <Button
                    size="small"
                    type={clampShortSeconds(s.seconds) === 10 || item?.durationIssue ? 'primary' : 'default'}
                    onClick={() => onSeconds(s.id, 10)}
                  >
                    10s
                  </Button>
                </Space>
                {item?.durationIssue ? <Tag color="gold">{item.durationIssue}</Tag> : null}
                <Tag>{shortSimpleStatus(state, s)}</Tag>
                {(() => {
                  const src = resolveFinalSource(run, item?.silent);
                  const take = resolveTakeUrl(run);
                  const finalOk = src === 'FAL' || (item?.silent && Boolean(take));
                  const speakers = multiSpeakerBlock(item?.lines ?? []);
                  return (
                    <>
                      <Tag color={run.keyframeDataUrl ? 'green' : 'default'}>KF {run.keyframeDataUrl ? '✓' : '—'}</Tag>
                      <Tag color={take ? 'green' : 'default'}>TAKE {take ? '✓' : '—'}</Tag>
                      <Tag color={finalOk ? 'green' : src === 'RUNWAY_TTS' ? 'gold' : 'default'}>
                        FINAL {finalOk ? '✓' : '—'}
                      </Tag>
                      <Tag color={src === 'FAL' ? 'green' : src === 'RUNWAY_TTS' ? 'gold' : 'default'}>{src}</Tag>
                      {speakers ? <Tag color="gold">{speakers}</Tag> : null}
                    </>
                  );
                })()}
                <Space size={0} wrap>
                  {(['action', 'continuity', 'motion', 'voiceFace'] as const).map((key) => (
                    <Checkbox
                      key={key}
                      checked={Boolean(run.shotQa?.[key])}
                      onChange={(e) =>
                        onPatchRun?.(s.id, { shotQa: { ...run.shotQa, [key]: e.target.checked } })
                      }
                    >
                      {key === 'voiceFace' ? 'VOICE/FACE' : key.toUpperCase()}
                    </Checkbox>
                  ))}
                </Space>
                {(() => {
                  const pipe = classifyVideoPipe(run);
                  if (pipe === 'VIDEO_READY' || pipe === 'VIDEO_NOT_SENT') return null;
                  return (
                    <Tag color={pipe.startsWith('VIDEO_') ? 'blue' : 'red'}>{videoPipeLabel(pipe)}</Tag>
                  );
                })()}
                <Button size="small" type="link" onClick={() => { setLogShotId(s.id); setLogCompareId(undefined); }}>
                  Nhật ký
                </Button>
                {run.previewUrl ? (
                  <>
                    <Button
                      size="small"
                      icon={<DownloadOutlined />}
                      onClick={() => onDownloadTakes?.([s.id])}
                      loading={assembleBusy}
                    >
                      Tải
                    </Button>
                    {run.lipsynced && lipsyncVideoUrl(run) ? (
                      <>
                        <Tag color="green">KHỚP MÔI</Tag>
                        <Button
                          size="small"
                          type="primary"
                          ghost
                          icon={<DownloadOutlined />}
                          loading={assembleBusy}
                          onClick={() => onDownloadTakes?.([s.id], 'lipsync')}
                        >
                          Tải khớp môi
                        </Button>
                        <Typography.Link href={lipsyncVideoUrl(run)} target="_blank" rel="noopener noreferrer">
                          Link
                        </Typography.Link>
                      </>
                    ) : lipsyncInFlight(run) || lipsyncBusy === s.id ? (
                      <Tag color="blue">{run.lipsyncStatus || 'PENDING'}</Tag>
                    ) : onLipsync ? (
                      <>
                        <Button
                          size="small"
                          loading={lipsyncBusy === s.id}
                          disabled={Boolean(lipsyncBusy) || Boolean(turboBusy)}
                          onClick={() => onLipsync([s.id])}
                        >
                          {shouldResumeLipsync(run) ? 'Hỏi lại môi' : 'Khớp môi'}
                        </Button>
                        {onAttachLipsync ? (
                          <Button size="small" onClick={() => onAttachLipsync(s.id)}>
                            Gắn Fal · 0$
                          </Button>
                        ) : null}
                      </>
                    ) : onAttachLipsync ? (
                      <Button size="small" onClick={() => onAttachLipsync(s.id)}>
                        Gắn Fal · 0$
                      </Button>
                    ) : null}
                    {run.lipsyncError && !run.lipsynced ? (
                      <span className="fx-prod-row__warn" title={run.lipsyncError}>
                        {run.lipsyncError.length > 48 ? `${run.lipsyncError.slice(0, 48)}…` : run.lipsyncError}
                      </span>
                    ) : null}
                  </>
                ) : turboInFlight(run) || turboBusy === s.id ? (
                  <>
                    <Tag color="blue">{run.turboStatus || 'PENDING'}</Tag>
                    <span className="fx-prod-row__warn">Đang tạo trên Runway — History 200 chưa phải có file. Đợi Tải.</span>
                  </>
                ) : run.turboError ? (
                  <>
                    <Tag color="red">Lỗi Runway</Tag>
                    <span className="fx-prod-row__warn" title={run.turboError}>
                      {shouldResumeTurboPoll(run)
                        ? `Task cũ còn · ${run.turboError.length > 56 ? `${run.turboError.slice(0, 56)}…` : run.turboError}`
                        : inferRunwayBilled(run, s.seconds)
                          ? `Đã trừ ~${inferRunwayBilled(run, s.seconds)} cr · ${run.turboError.length > 48 ? `${run.turboError.slice(0, 48)}…` : run.turboError}`
                          : run.turboError.length > 72
                            ? `${run.turboError.slice(0, 72)}…`
                            : run.turboError}
                    </span>
                    <Button
                      size="small"
                      onClick={() => {
                        if (!dropFromCut([s.id])) return;
                        onStep('preview');
                      }}
                    >
                      Bỏ khỏi dải test
                    </Button>
                    {(() => {
                      const retry = canManualRetry(run);
                      const internal = /INTERNAL/i.test(run.turboError || '') || /INTERNAL/i.test(parseFailureCode(run.turboError) || '');
                      if (retry.kind === 'resume') {
                        return (
                          <Button size="small" loading={turboBusy === s.id} onClick={() => onCreateVideo([s.id])}>
                            Hỏi lại · 0 cr
                          </Button>
                        );
                      }
                      if (retry.kind === 'recover') {
                        return (
                          <Button size="small" loading={turboBusy === s.id} onClick={() => onCreateVideo([s.id])}>
                            Thử đọc file · 0 cr
                          </Button>
                        );
                      }
                      if (internal) {
                        if (sameKfAsInternalFail(run)) {
                          return <Tag>Cùng KF — không gửi lại INTERNAL</Tag>;
                        }
                        return (
                          <Button size="small" danger loading={turboBusy === s.id} onClick={() => onCreateVideo([s.id])}>
                            Đã sửa KF · 1 job (trừ cr)
                          </Button>
                        );
                      }
                      return (
                        <Button
                          size="small"
                          disabled={Boolean(runwayQuietMin) && retry.kind === 'new'}
                          loading={turboBusy === s.id}
                          onClick={() => onCreateVideo([s.id])}
                        >
                          {runwayQuietMin ? 'Cần mở gửi' : `Gửi lại · attempt ${(latestAttempt(run)?.n ?? 0) + 1}`}
                        </Button>
                      );
                    })()}
                  </>
                ) : (
                  <>
                    <Tag>Chưa take</Tag>
                    {kfIsApproved(run) ? (
                      <Button
                        size="small"
                        loading={turboBusy === s.id}
                        onClick={() => onCreateVideo([s.id])}
                      >
                        Gửi
                      </Button>
                    ) : null}
                  </>
                )}
              </div>
            );
          })}
          {sceneBatchDisabled ? (
            <Alert type="warning" showIcon style={{ margin: '12px 0' }} message={sceneBatchLabel} />
          ) : null}
          {(() => {
            const drafts = sel.filter((s) => {
              const run = runOf(s);
              return Boolean(run.keyframeDataUrl) && !kfIsApproved(run) && run.status !== 'approved';
            });
            if (!drafts.length) return null;
            return (
              <Space wrap style={{ marginTop: 8 }}>
                <Button type="primary" onClick={() => onApproveKf?.(drafts.map((s) => s.id))}>
                  Duyệt {drafts.length} KF DRAFT
                </Button>
                <Typography.Text type="secondary">Xem lại hình ở bước Hình nếu áo / bố / số người sai.</Typography.Text>
              </Space>
            );
          })()}
          <Space wrap style={{ marginTop: 12 }}>
            {(() => {
              const leftoverShots = sel.filter((s) => {
                const run = runOf(s);
                if (!kfIsApproved(run) || run.previewUrl?.trim() || run.prodSkip) return false;
                const retry = canManualRetry(run);
                return retry.kind === 'resume' || retry.kind === 'recover' || classifyVideoPipe(run) === 'VIDEO_NOT_SENT';
              });
              const leftover = leftoverShots.map((s) => s.id);
              if (!leftover.length) return null;
              const resumeN = leftoverShots.filter((s) => shouldResumeTurboPoll(runOf(s))).length;
              return (
                <Button
                  disabled={Boolean(turboBusy) || Boolean(runwayQuietMin)}
                  loading={Boolean(turboBusy)}
                  onClick={() => onCreateVideo(leftover)}
                >
                  {resumeN === leftover.length
                    ? `Hỏi lại ${leftover.length} task cũ · 0 cr`
                    : `Gửi nốt ${leftover.length} Short lỗi / chưa take`}
                </Button>
              );
            })()}
            <Button
              type="primary"
              loading={Boolean(turboBusy)}
              disabled={sceneBatchDisabled || !vidSend.length}
              onClick={() => onCreateVideo(vidSend)}
            >
              {sceneBatchLabel}
            </Button>
            {(() => {
              const readyShot = sel.find((s) => classifyVideoPipe(runOf(s)) === 'VIDEO_READY' && runOf(s).keyframeDataUrl);
              const failShot = sel.find((s) => /INTERNAL/i.test(runOf(s).turboError || '') && runOf(s).keyframeDataUrl);
              if (!onAbDiagnostic || !readyShot || !failShot) return null;
              return (
                <Button
                  disabled={Boolean(turboBusy)}
                  onClick={() => onAbDiagnostic(readyShot.id, failShot.id)}
                >
                  A/B 3+3 JPEG 1280 · Confirm cr
                </Button>
              );
            })()}
            <Button onClick={() => onStep('preview')}>Sang Preview →</Button>
            <Button
              icon={<DownloadOutlined />}
              disabled={!takeN}
              loading={assembleBusy}
              onClick={() => onDownloadTakes?.(sel.map((s) => s.id))}
            >
              Tải {takeN || sel.length} take
            </Button>
            {lipReady.length ? (
              <Button
                icon={<DownloadOutlined />}
                loading={assembleBusy}
                onClick={() => onDownloadTakes?.(lipReady.map((s) => s.id), 'lipsync')}
              >
                Tải {lipReady.length} khớp môi
              </Button>
            ) : null}
            {onLipsync && lipNeed.length ? (
              <Button
                disabled={Boolean(turboBusy) || Boolean(lipsyncBusy)}
                loading={Boolean(lipsyncBusy)}
                onClick={() => onLipsync(lipNeed)}
              >
                Khớp môi {lipNeed.length} take · Fal
              </Button>
            ) : null}
            <Button disabled={!haveTakes} loading={assembleBusy} onClick={() => onAssembleCut?.()}>
              {takesOk
                ? lipReady.length
                  ? `Ghép tập · ${lipReady.length} khớp môi`
                  : 'Ghép tập MP4 + thoại + SRT'
                : lipReady.length
                  ? `Ghép ${takeN} take · ${lipReady.length} khớp môi`
                  : `Ghép ${takeN} take đã có + thoại`}
            </Button>
          </Space>
          {logShotId ? (() => {
            const shot = sel.find((s) => s.id === logShotId) || queue.find((s) => s.id === logShotId);
            if (!shot) return null;
            const run = runOf(shot);
            const readyPeers = sel.filter((s) => s.id !== shot.id && runOf(s).previewUrl?.trim());
            const compareShot = logCompareId
              ? sel.find((s) => s.id === logCompareId) || queue.find((s) => s.id === logCompareId)
              : readyPeers[0];
            const diff = compareShot
              ? compareRunwayJobs(
                  { code: studioShotCode(compareShot, queue), run: runOf(compareShot) },
                  { code: studioShotCode(shot, queue), run },
                )
              : [];
            return (
              <Drawer
                title={`Nhật ký · ${studioShotCode(shot, queue)}`}
                open
                width={560}
                onClose={() => setLogShotId(undefined)}
              >
                <Typography.Paragraph type="secondary">
                  Diagnostic: JPEG đã gửi vs PNG Gemini. Không gửi job mới từ nhật ký.
                </Typography.Paragraph>
                <pre className="fx-prod-log__pre">
                  {formatRunwayDiagnostic({
                    code: studioShotCode(shot, queue),
                    kfApproved: kfIsApproved(run),
                    run,
                    ratio: outputAspectOf(state),
                  })}
                </pre>
                {readyPeers.length ? (
                  <Space wrap style={{ marginBottom: 12 }}>
                    <span>So với</span>
                    {readyPeers.map((s) => (
                      <Button
                        key={s.id}
                        size="small"
                        type={(compareShot?.id || readyPeers[0]?.id) === s.id ? 'primary' : 'default'}
                        onClick={() => setLogCompareId(s.id)}
                      >
                        {studioShotCode(s, queue)}
                      </Button>
                    ))}
                  </Space>
                ) : (
                  <Alert type="info" showIcon message="Chưa có shot READY trên dải để so." style={{ marginBottom: 12 }} />
                )}
                {diff.length ? (
                  <table className="fx-prod-log">
                    <thead>
                      <tr>
                        <th></th>
                        <th>{compareShot ? studioShotCode(compareShot, queue) : 'READY'}</th>
                        <th>{studioShotCode(shot, queue)}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {diff.map((row) => (
                        <tr key={row.key} className={row.same ? undefined : 'fx-prod-log__diff'}>
                          <td>{row.key}</td>
                          <td>{row.a}</td>
                          <td>{row.b}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : null}
                <pre className="fx-prod-log__pre">
                  {formatProductionLog({
                    code: studioShotCode(shot, queue),
                    kfLabel: kfLabel(run),
                    kfApproved: kfIsApproved(run),
                    run,
                  })}
                </pre>
              </Drawer>
            );
          })() : null}
        </section>
      ) : null}

      {step === 'preview' ? (
        cutPlan && ttsUrlOf && onFillPreview ? (
          <ContentFamixaPreviewCutCard
            shots={sel}
            allShots={queue}
            fromId={fromId}
            toId={toId}
            onRange={onRange}
            plan={cutPlan}
            runOf={runOf}
            ttsUrlOf={ttsUrlOf}
            sessionSrcOf={sessionSrcOf}
            fillBusy={generateKfBusy}
            turboBusy={turboBusy}
            onFillMissing={onFillPreview}
            onOpenShot={onOpenShot}
            onGoVideo={() => onStep('video')}
            onBump10s={(ids) => {
              for (const id of ids) onSeconds(id, 10);
            }}
            onEnsureTts={onEnsureTts}
            onAssembleCut={onAssembleCut}
            assembleBusy={assembleBusy}
            assembleAspect={assembleAspect}
            onAssembleAspect={onAssembleAspect}
          />
        ) : (
          <Alert type="info" showIcon message="Chọn dải Short ở bước Shorts rồi ghép Preview theo đúng thứ tự kể chuyện." />
        )
      ) : null}

      {step === 'final' ? (
        <section className="fx-card">
          <p className="fx-kf__kicker">07 FINAL — tải take và ghép 1 file theo dải đã chọn</p>
          <h3>FINAL</h3>
          <p className="fx-plan">
            {sum.from} → {sum.to} · {sel.length} Short · {sum.sec}s · Take {takeN}/{sel.length}
            {(() => {
              const cost = productionCostLedger(state, sel);
              return ` · Runway ~${cost.billedRunway}/${cost.estimatedRunway} cr · Fal ~$${cost.confirmedFalUsd.toFixed(2)}/$${cost.estimatedFalUsd.toFixed(2)}`;
            })()}
          </p>
          {fullEpisodeBlockReason(state, sel) ? (
            <Alert type="warning" showIcon message={fullEpisodeBlockReason(state, sel)} />
          ) : (
            <Alert type="success" showIcon message="Gate Final đủ — Timeline chỉ dùng FINAL_SOURCE." />
          )}
          <Typography.Paragraph type="secondary">
            Một tập: 30fps H.264 + TTS đúng chỗ. 16:9 pad hoặc 9:16 crop (Reels). Không fade che continuity. Confirm không trừ credit.
          </Typography.Paragraph>
          <ul className="fx-final-gates">
            {PROD_GATES.map((g) => {
              const ok = prodGateState(state, queue)[g.id];
              return (
                <li key={g.id}>
                  {ok ? '✓' : '—'} {g.label}
                </li>
              );
            })}
            <li>
              {cutPlan && !cutPlan.extraLines.length && cutPlan.items.filter((i) => !i.silent).every((i) => i.hasVoiceFile)
                ? '✓'
                : '—'}{' '}
              Map thoại
              {cutPlan?.extraLines.length ? ` · ${cutPlan.extraLines.length} câu chưa gắn` : ''}
            </li>
          </ul>
          {cutPlan && ttsUrlOf ? (
            <div className="fx-lanes">
              {buildTimelineLanes(buildAssembleTimeline(cutPlan, { hasVoiceFile: (id) => Boolean(ttsUrlOf(id)) })).map((lane) => (
                <p key={lane.id} className="fx-lanes__row">
                  <strong>{lane.label}</strong>
                  <span>{lane.spans.length ? lane.spans.map((s) => s.label).join(' · ') : '—'}</span>
                </p>
              ))}
            </div>
          ) : null}
          {!takesOk && haveTakes ? (
            <Alert
              type="info"
              showIcon
              message={
                lipReady.length
                  ? `Ghép ${takeN} take đã có. ${lipReady.map((s) => studioShotCode(s, queue)).join(', ')} giữ tiếng khớp môi — không overlay TTS hàng đó. Short chưa take bị bỏ qua.`
                  : `Ghép ${takeN} take đã có để nghe thoại. Short chưa có take bị bỏ qua — không tạo ảnh / Runway mới.`
              }
            />
          ) : null}
          {!haveTakes ? (
            <Alert
              type="warning"
              showIcon
              message="Chưa có take trên dải. Gửi Runway ở bước 5 Video rồi ghép."
            />
          ) : null}
          <Space wrap style={{ marginTop: 12 }}>
            <Button
              icon={<DownloadOutlined />}
              disabled={!takeN}
              loading={assembleBusy}
              onClick={() => onDownloadTakes?.(sel.map((s) => s.id))}
            >
              Tải {takeN || sel.length} take
            </Button>
            {lipReady.length ? (
              <Button
                icon={<DownloadOutlined />}
                loading={assembleBusy}
                onClick={() => onDownloadTakes?.(lipReady.map((s) => s.id), 'lipsync')}
              >
                Tải {lipReady.length} khớp môi
              </Button>
            ) : null}
            {onAssembleAspect ? (
              <Radio.Group value={outputAspectOf(state)} onChange={(e) => onAssembleAspect(e.target.value)}>
                <Radio.Button value="16:9">Xuất 16:9</Radio.Button>
                <Radio.Button value="9:16">Xuất 9:16</Radio.Button>
              </Radio.Group>
            ) : (
              <Tag>{outputAspectOf(state)}</Tag>
            )}
            {onLipsync && lipNeed.length ? (
              <Button disabled={Boolean(lipsyncBusy)} loading={Boolean(lipsyncBusy)} onClick={() => onLipsync(lipNeed)}>
                Khớp môi {lipNeed.length} take · Fal
              </Button>
            ) : null}
            <Button
              type="primary"
              disabled={!haveTakes || (takesOk && Boolean(fullEpisodeBlockReason(state, sel)))}
              loading={assembleBusy}
              onClick={() => onAssembleCut?.()}
            >
              {takesOk && !fullEpisodeBlockReason(state, sel)
                ? 'FINAL EPISODE · MP4'
                : lipReady.length
                  ? `Preview ${takeN} take · ${lipReady.length} FAL`
                  : `Preview ${takeN} take đã có`}
            </Button>
            <Button onClick={() => onApprovePreview?.()} disabled={state.previewApproved || !haveTakes}>
              {state.previewApproved ? 'Đã duyệt Preview' : 'Duyệt Preview dải này'}
            </Button>
            <Button onClick={onLockScene} disabled={sceneLocked || !takesOk || !state.previewApproved}>
              {sceneLocked ? 'Đã khóa cảnh' : 'Khóa cảnh (Final)'}
            </Button>
          </Space>
        </section>
      ) : null}
    </div>
  );
}
