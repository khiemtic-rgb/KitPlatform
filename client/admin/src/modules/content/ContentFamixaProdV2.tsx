/** Famixa Production Workflow V2 screens: Shorts → Image → Video → Preview → Final. */

import { Alert, Button, Checkbox, Drawer, Input, Radio, Space, Tag, Typography } from 'antd';
import { DownloadOutlined, FolderOpenOutlined } from '@ant-design/icons';
import { useEffect, useRef, useState } from 'react';
import {
  clampShortSeconds,
  kfFollowIds,
  kfNeedIds,
  selectPreset,
  selectionSummary,
  shortSimpleStatus,
  shortsByScene,
  videoNeedIds,
  type ProdV2Step,
} from './content-famixa-prod-v2';
import {
  groupShotsByBeat,
  shotOneLiner,
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
  prodGateState,
  sceneIdOfShot,
  sceneMasterOf,
  shotProdStatus,
} from './content-famixa-scene-first';
import { buildAssembleTimeline } from './content-famixa-assemble';
import { actingOfShot } from './content-famixa-acting-law';
import { compileKfRewrite, emptyKfRewrite, sanitizeKfRewrite, type KfRewrite } from './content-famixa-kf-rewrite';
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
  kitCredits,
  onLockScene,
  sceneLocked,
  onApprovePreview,
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
  onPatchSceneMaster?: (
    sceneId: string,
    patch: { location?: string; time?: string; lighting?: string; wardrobe?: string; props?: string; camera?: string; mood?: string },
  ) => void;
  onSeconds: (shotId: string, seconds: 5 | 10) => void;
  onLockShotGraph?: () => void;
  onOpenShot: (shot: FamixaSeriesShot) => void;
  runOf: (shot: FamixaSeriesShot) => SeriesShotRun;
  actionOf: (shot: FamixaSeriesShot) => string | undefined;
  lock: SceneContinuityLock;
  episode?: FamixaSeriesEpisode;
  generateKfBusy?: boolean;
  turboBusy?: boolean;
  onGenerateKf: (ids?: string[]) => void;
  onPickKf?: (file: File, shotId?: string) => void;
  onApproveKf?: (ids?: string[]) => void;
  onRegenerateKf?: (ids: string[], continuityNote?: string) => void;
  onCreateVideo: (ids?: string[]) => void;
  cutPlan?: PreviewCutPlan;
  ttsUrlOf?: (lineId: string) => string | undefined;
  sessionSrcOf?: (id: string) => string | undefined;
  onFillPreview?: (kind: 'story' | 'motion') => void;
  onDownloadTakes?: (ids?: string[]) => void;
  onAssembleCut?: () => void;
  onEnsureTts?: () => Promise<number>;
  assembleBusy?: boolean;
  assembleAspect?: '16:9' | '9:16';
  onAssembleAspect?: (aspect: '16:9' | '9:16') => void;
  sceneBatchLabel: string;
  sceneBatchDisabled: boolean;
  sceneBatchCredits?: number;
  kitCredits: number;
  onLockScene: () => void;
  sceneLocked?: boolean;
  sceneReady?: boolean;
  onApprovePreview?: () => void;
}) {
  const pick = useRef<HTMLInputElement>(null);
  const sum = selectionSummary(queue, fromId, toId, pickIds);
  const sel = sum.shots;
  const graphLocked = state.shotGraphLocked === true;
  const kfNeed = kfFollowIds(state, sel);
  const kfEmpty = kfNeedIds(state, sel);
  const vidNeed = videoNeedIds(state, sel);
  const takeN = sel.filter((s) => runOf(s).previewUrl?.trim()).length;
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
    setKfPicked(sel.map((s) => s.id));
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
                    · {block.shots.length} Shorts ·{' '}
                    {block.shots.reduce((n, s) => n + clampShortSeconds(s.seconds), 0)}s
                  </span>
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
                            <span>{shotOneLiner(s.story, actionOf(s))}</span>
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
                          {item?.silent ? <Tag>Voice: NONE</Tag> : item?.line ? <Tag>{item.lines.length} thoại</Tag> : null}
                          {item?.line ? <Tag>{actingOfShot(item.lines, actionOf(s)).label}</Tag> : null}
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
            const need = kfPicked.filter((id) => kfNeed.includes(id));
            const genN = kfMethod === 'ai' ? need.length : 0;
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
                  <p className="fx-plan">Shot chỉ đổi Action. Không đổi phòng, áo, mặt, đèn.</p>
                  <div className="fx-master__grid">
                    {(
                      [
                        ['location', 'Location', master.location],
                        ['time', 'Time', master.time],
                        ['lighting', 'Lighting', master.lighting],
                        ['wardrobe', 'Wardrobe', master.wardrobe],
                        ['props', 'Props', master.props],
                        ['camera', 'Camera', master.camera],
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
                  <Space wrap>
                    {master.locked ? (
                      <Button size="small" onClick={() => onLockSceneMaster?.(sceneId, false)}>
                        Mở khóa Master
                      </Button>
                    ) : (
                      <Button type="primary" size="small" onClick={() => onLockSceneMaster?.(sceneId, true)}>
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
                      ? `Tuần tự ${genN} hình. SH02 dựa SH01, không song song.`
                      : 'SK: mở Shot → Đổi ảnh. KIT không generate.'}
                  </p>
                  {kfMethod === 'ai' ? (
                    <Button
                      type="primary"
                      loading={generateKfBusy}
                      disabled={!genN || !master.locked}
                      onClick={() => onGenerateKf(need)}
                    >
                      Tạo KF tuần tự · {genN}
                    </Button>
                  ) : (
                    <Alert type="info" showIcon message="SK: bấm hàng Shot → Đổi ảnh." />
                  )}
                </div>
                <div className="fx-shot-seq">
                  {sel.map((s) => {
                    const run = runOf(s);
                    const item = cutPlan?.items.find((i) => i.shotId === s.id);
                    const action = shotOneLiner(s.story, actionOf(s) || s.visual);
                    const on = kfPicked.includes(s.id);
                    const st = shotProdStatus(state, s);
                    return (
                      <label key={s.id} id={`fx-kf-${s.id}`} className={`fx-shot-seq__row${on ? ' fx-shot-seq__row--on' : ''}`}>
                        <Checkbox
                          checked={on}
                          onChange={() =>
                            setKfPicked((cur) => (cur.includes(s.id) ? cur.filter((id) => id !== s.id) : [...cur, s.id]))
                          }
                        />
                        <button type="button" className="fx-shot-seq__thumb" onClick={() => setKfDetailId(s.id)}>
                          {run.keyframeDataUrl ? <img src={run.keyframeDataUrl} alt="" /> : <span />}
                        </button>
                        <button type="button" className="fx-shot-seq__main" onClick={() => setKfDetailId(s.id)}>
                          <strong>
                            {studioShotCode(s, queue)}
                            {s.voiceChainFrom ? ' · REUSE' : ''}
                          </strong>
                          <span>{action || '—'}</span>
                        </button>
                        <Tag>{st}</Tag>
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
                      <p>
                        <span>Action</span>
                        {actionOf(kfDetail) || kfDetail.story || '—'}
                      </p>
                      <p>
                        <span>Voice</span>
                        {(() => {
                          const item = cutPlan?.items.find((i) => i.shotId === kfDetail.id);
                          if (!item || item.silent || !item.line) return 'NONE';
                          return `${item.line.name}: “${item.line.text}”`;
                        })()}
                      </p>
                      <p>
                        <span>Character</span>
                        {(kfDetail.characterIds || kfDetail.characters || [])
                          .map((id) => (state.characters ?? []).find((c) => c.id === id)?.name || id)
                          .join(', ') || '—'}
                      </p>
                      <p>
                        <span>Continuity</span>
                        {lock.environment || kfDetail.location || '—'}
                        {continuityPlaceHint(lock, kfDetail, actionOf(kfDetail))
                          ? ` → ${continuityPlaceHint(lock, kfDetail, actionOf(kfDetail))!.to}`
                          : ''}
                      </p>
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
                          disabled={!runOf(kfDetail).keyframeDataUrl}
                          onClick={() => onApproveKf?.([kfDetail.id])}
                        >
                          Duyệt KF
                        </Button>
                      </Space>
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
            Mỗi Short = 1 clip 5s hoặc 10s. Confirm mới trừ credit. Không gửi từ bước Preview.
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
              message={`${sel.filter((s) => runOf(s).turboError && !runOf(s).previewUrl).map((s) => studioShotCode(s, queue)).join(', ')} lỗi Runway — 6 take đã có giữ nguyên.`}
              description="Thường là lỗi tạm phía Runway (unexpected), không phải TTS. Gửi 4 Short (KIT tự thử lại 1 lần / clip) hoặc Gửi lại từng hàng."
            />
          ) : null}
          {sel.map((s) => {
            const run = runOf(s);
            const item = cutPlan?.items.find((i) => i.shotId === s.id);
            return (
              <div key={s.id} className="fx-prod-row">
                <strong>{studioShotCode(s, queue)}</strong>
                <span>{shotOneLiner(s.story, actionOf(s))}</span>
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
                {run.previewUrl ? (
                  <Button
                    size="small"
                    icon={<DownloadOutlined />}
                    onClick={() => onDownloadTakes?.([s.id])}
                    loading={assembleBusy}
                  >
                    Tải
                  </Button>
                ) : run.turboError ? (
                  <>
                    <Tag color="red">Lỗi Runway</Tag>
                    <span className="fx-prod-row__warn" title={run.turboError}>
                      {run.turboError.length > 72 ? `${run.turboError.slice(0, 72)}…` : run.turboError}
                    </span>
                    <Button size="small" disabled={turboBusy} onClick={() => onCreateVideo([s.id])}>
                      Gửi lại
                    </Button>
                  </>
                ) : (
                  <Tag>Chưa take</Tag>
                )}
              </div>
            );
          })}
          {sceneBatchDisabled ? (
            <Alert type="warning" showIcon style={{ margin: '12px 0' }} message={sceneBatchLabel} />
          ) : null}
          <Space wrap style={{ marginTop: 12 }}>
            <Button
              type="primary"
              loading={turboBusy}
              disabled={sceneBatchDisabled || !vidNeed.length}
              onClick={() => onCreateVideo(vidNeed.length ? vidNeed : sel.map((s) => s.id))}
            >
              {sceneBatchLabel}
            </Button>
            <Button onClick={() => onStep('preview')}>Sang Preview →</Button>
            <Button
              icon={<DownloadOutlined />}
              disabled={!takeN}
              loading={assembleBusy}
              onClick={() => onDownloadTakes?.(sel.map((s) => s.id))}
            >
              Tải {takeN || sel.length} take
            </Button>
            <Button disabled={!haveTakes} loading={assembleBusy} onClick={() => onAssembleCut?.()}>
              {takesOk ? 'Ghép tập MP4 + thoại + SRT' : `Ghép ${takeN} take đã có + thoại`}
            </Button>
          </Space>
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
          </p>
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
              message={`Ghép ${takeN} take đã có để nghe thoại. Short chưa có take bị bỏ qua — không tạo ảnh / Runway mới.`}
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
            {onAssembleAspect ? (
              <Radio.Group
                value={assembleAspect ?? '9:16'}
                onChange={(e) => onAssembleAspect(e.target.value)}
              >
                <Radio.Button value="9:16">Xuất 9:16</Radio.Button>
                <Radio.Button value="16:9">Xuất 16:9</Radio.Button>
              </Radio.Group>
            ) : null}
            <Button type="primary" disabled={!haveTakes} loading={assembleBusy} onClick={() => onAssembleCut?.()}>
              {takesOk ? 'FINAL EPISODE · MP4' : `Ghép ${takeN} take đã có · MP4 + thoại`}
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
