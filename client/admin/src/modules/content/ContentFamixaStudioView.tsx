import { useRef, useState, type ReactNode } from 'react';
import { Alert, Button, Checkbox, Collapse, Select, Space, Tag, Typography } from 'antd';
import { CheckOutlined, FolderOpenOutlined, PlusOutlined, ThunderboltOutlined } from '@ant-design/icons';
import {
  SERIES_STATUS_LABEL,
  memoryLine,
  shotActionBeats,
  shotOneLiner,
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
import { modeLabel, type SceneKfPlanRow } from './content-famixa-batch-plan';
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
  const href = (url ?? '').trim();
  if (/^https?:\/\//i.test(href) && /\.(mp4|webm|ogg)(\?|#|$)/i.test(href)) return href;
  return '';
}

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
  shortsCount,
  shortsLockedCount,
  sessionSrc,
  turboBusy,
  kitCredits,
  runwaySpent,
  expectedCost,
  costLabel,
  engine,
  onEngine,
  keys,
  voiceProvider,
  onVoiceProvider,
  onSelectShot,
  onAddShot,
  onAction,
  onPickKeyframe,
  onGenerateKeyframe,
  generateKfBusy,
  onInheritKeyframe,
  onApproveKeyframe,
  onCreateVideo,
  onCreateSceneVideo,
  sceneBatchLabel,
  sceneBatchDisabled,
  batchPlan,
  batchSceneShots,
  batchKfNew,
  batchKfReuse,
  onGenerateSceneKf,
  onApproveSceneKf,
  generateSceneKfBusy,
  placeHint,
  onAcceptPlaceChange,
  onKeepPlaceBaseline,
  onForceKfNew,
  onReview,
  onLockShot,
  onFailShot,
  onPickVideo,
  onOpenScript,
  onOpenShorts,
  onOpenStudio,
  onOpenTimeline,
  onOpenAdvanced,
  onOpenMemory,
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
  turboBusy?: boolean;
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
  onAddShot: () => void;
  onAction: (value: string) => void;
  onPickKeyframe: (file: File) => void;
  onGenerateKeyframe?: () => void;
  generateKfBusy?: boolean;
  onInheritKeyframe: () => void;
  onApproveKeyframe: () => void;
  onCreateVideo: () => void;
  onCreateSceneVideo: () => void;
  sceneBatchLabel: string;
  sceneBatchDisabled: boolean;
  batchPlan?: SceneKfPlanRow[];
  batchSceneShots?: FamixaSeriesShot[];
  batchKfNew?: number;
  batchKfReuse?: number;
  onGenerateSceneKf?: () => void;
  onApproveSceneKf?: () => void;
  generateSceneKfBusy?: boolean;
  placeHint?: { from: string; to: string };
  onAcceptPlaceChange?: () => void;
  onKeepPlaceBaseline?: () => void;
  onForceKfNew?: (shot: FamixaSeriesShot) => void;
  onReview: (axis: SeriesReviewAxis, on: boolean) => void;
  onLockShot: () => void;
  onFailShot: () => void;
  onPickVideo: (file: File) => void;
  onOpenScript: () => void;
  onOpenShorts: () => void;
  onOpenStudio: () => void;
  onOpenTimeline: () => void;
  onOpenAdvanced: () => void;
  onOpenMemory: () => void;
  statusOf: (shot: FamixaSeriesShot) => SeriesShotStatus;
  runOf?: (shot: FamixaSeriesShot) => SeriesShotRun;
  actionOf: (shot: FamixaSeriesShot) => string | undefined;
  pane: 'script' | 'shorts' | 'studio' | 'timeline' | 'advanced';
  children?: ReactNode;
}) {
  const kfPick = useRef<HTMLInputElement>(null);
  const vidPick = useRef<HTMLInputElement>(null);
  const [memOpen, setMemOpen] = useState(false);
  const [editAction, setEditAction] = useState(false);
  const code = studioShotCode(active);
  const scene = studioSceneCode(active, episode);
  const next = active ? shots[shots.findIndex((s) => s.id === active.id) + 1] : undefined;
  const play = clipSrc(run?.previewUrl, sessionSrc);
  const kfOk = Boolean(run?.keyframeDataUrl);
  const actionText = (run?.shotAction ?? '').trim() || (active ? actionOf(active) : '') || active?.story || '';
  const actionOk = Boolean(actionText.trim());
  const beats = shotActionBeats(active?.story, actionText);
  const kfLabel = code.replace(/^SH/i, 'KF') || 'KF';
  const inheritFrom = prevLocked ? studioShotCode(prevLocked) : '';
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
  const shortsOk = !shortsCount || (shortsLockedCount ?? 0) >= shortsCount;
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

  return (
    <div className="fx-studio">
      <div className="fx-main">
        <div className="fx-top">
          <div>
            <p className="fx-head-title">
              {scene.replace(' · ', ' · ')}
              {sceneTitle ? ` — ${sceneTitle}` : epTitle ? ` — ${epTitle}` : ''}
            </p>
            <p className="fx-head-sub">
              {active
                ? `${code} · ${shotN}/${shots.length || 0} · ${active.seconds ?? 5} giây`
                : 'Chọn một shot để dựng'}
            </p>
            {active ? <p className="fx-head-line">{oneLine}</p> : null}
            <div className="fx-steps">
              <button type="button" className={`fx-step${scriptLocked ? ' fx-step--ok' : ''}${pane === 'script' ? ' fx-step--on' : ''}`} onClick={onOpenScript}>
                ① Kịch bản{scriptLocked ? ' ✓' : ''}
              </button>
              <button
                type="button"
                className={`fx-step${voiceOk ? ' fx-step--ok' : ''}${pane === 'script' && !voiceOk ? ' fx-step--on' : ''}`}
                onClick={onOpenScript}
              >
                ② Full Voice{voiceOk ? ' ✓' : ''}
              </button>
              {typeof shortsCount === 'number' && shortsCount > 0 ? (
                <button type="button" className={`fx-step${shortsOk ? ' fx-step--ok' : ''}${pane === 'shorts' ? ' fx-step--on' : ''}`} onClick={onOpenShorts}>
                  ③ Short{shortsOk ? ' ✓' : ` ${shortsLockedCount ?? 0}/${shortsCount}`}
                </button>
              ) : null}
              <button type="button" className={`fx-step${pane === 'studio' ? ' fx-step--on' : ''}`} onClick={onOpenStudio}>
                {typeof shortsCount === 'number' && shortsCount > 0 ? '④' : '③'} Dựng cảnh{pane === 'studio' ? ' ←' : ''}
              </button>
              <button type="button" className={`fx-step${allLocked ? ' fx-step--ok' : ''}${pane === 'timeline' ? ' fx-step--on' : ''}`} onClick={onOpenTimeline}>
                {typeof shortsCount === 'number' && shortsCount > 0 ? '⑤' : '④'} Timeline{allLocked ? ' ✓' : ''}
              </button>
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

        {pane === 'studio' && shots.length === 0 ? (
          <Alert
            type="info"
            showIcon
            message="Chưa có shot trên bảng."
            description="Khóa kịch bản và Full Voice trước. Nếu tập có short thì khóa short. Rồi thêm shot 16:9 ở đây."
            style={{ marginBottom: 12 }}
          />
        ) : null}

        {pane === 'studio' ? (
        <>
        <div className="fx-grid">
          <section className="fx-card fx-card--shots">
            <h3>SHOT</h3>
            {shots.map((s) => {
              const on = s.id === active?.id;
              const ui = studioShotUi(runOf?.(s) ?? { status: statusOf(s) });
              return (
                <button
                  key={s.id}
                  type="button"
                  className={`fx-shot${on ? ' fx-shot--on' : ''}${ui.tone === 'locked' ? ' fx-shot--lock' : ''}`}
                  onClick={() => onSelectShot(s)}
                >
                  <div className="fx-shot__row">
                    <span className="fx-shot__id">
                      {studioShotCode(s)} · {s.seconds}s
                    </span>
                    <span className={`fx-badge fx-badge--${ui.tone === 'locked' ? 'lock' : ui.tone === 'error' ? 'err' : ui.tone === 'warn' ? 'warn' : ui.tone === 'on' ? 'on' : 'wait'}`}>
                      {ui.label}
                    </span>
                  </div>
                  <div className="fx-shot__story">{shotOneLiner(s.story, actionOf(s))}</div>
                  {ui.hint ? <div className="fx-shot__hint">{ui.hint}</div> : null}
                </button>
              );
            })}
            <Button block icon={<PlusOutlined />} onClick={onAddShot}>
              Thêm shot
            </Button>
          </section>

          <div className="fx-work">

          {batchPlan && batchPlan.length > 0 ? (
            <section className="fx-card fx-card--batch">
              <h3>CẢNH · PRODUCTION</h3>
              <p className="fx-plan">
                {batchPlan.map((p) => `${p.code} ${p.mode === 'new' ? 'NEW' : `REUSE ${p.sourceCode || ''}`}`.trim()).join(' · ')}
              </p>
              <p className="fx-shot__hint">
                {(batchKfNew ?? 0) > 0 ? `${batchKfNew} KF mới` : 'Không cần KF mới'}
                {batchKfReuse ? ` · ${batchKfReuse} reuse` : ''}
                {' · không gửi storyboard 12 ô vào I2V'}
              </p>
              <Space wrap style={{ marginTop: 8 }}>
                <Button type="primary" loading={generateSceneKfBusy} onClick={onGenerateSceneKf}>
                  Tạo KF toàn cảnh
                </Button>
                <Button onClick={onApproveSceneKf} disabled={!batchSceneShots?.some((s) => runOf?.(s)?.keyframeDataUrl)}>
                  Duyệt tất cả KF
                </Button>
              </Space>
              <div className="fx-sheet">
                {(batchSceneShots ?? []).map((s) => {
                  const row = batchPlan.find((p) => p.shotId === s.id);
                  const take = runOf?.(s);
                  return (
                    <button
                      key={s.id}
                      type="button"
                      className={`fx-sheet__cell${s.id === active?.id ? ' fx-sheet__cell--on' : ''}`}
                      onClick={() => onSelectShot(s)}
                    >
                      {take?.keyframeDataUrl ? (
                        <img src={take.keyframeDataUrl} alt={studioShotCode(s)} />
                      ) : (
                        <div className="fx-sheet__empty">Chưa KF</div>
                      )}
                      <span>
                        {studioShotCode(s)} · {s.seconds}s
                      </span>
                      <span className="fx-sheet__mode">{row ? modeLabel(row) : ''}</span>
                      {row?.kfApproved ? <span className="fx-sheet__ok">Đã duyệt</span> : null}
                    </button>
                  );
                })}
              </div>
              {active && onForceKfNew ? (
                <Button size="small" style={{ marginTop: 8 }} onClick={() => onForceKfNew(active)}>
                  Đổi ảnh — KF mới cho {code}
                </Button>
              ) : null}
            </section>
          ) : null}

          <section className="fx-card fx-card--action">
            <h3>
              {code} — CHUYỆN GÌ XẢY RA
            </h3>
            {!active ? (
              <Typography.Text type="secondary">Chọn shot bên trái.</Typography.Text>
            ) : !unlocked ? (
              <Alert type="warning" showIcon message="Khóa shot liền trước đã." />
            ) : (
              <>
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
                <Button type="primary" loading={generateKfBusy} onClick={onGenerateKeyframe} disabled={!unlocked}>
                  Tạo KF CLEAN
                </Button>
              ) : null}
              <Button onClick={() => kfPick.current?.click()}>Chọn ảnh KF</Button>
              {prevLocked ? (
                <Button disabled={!prevKf || !unlocked} onClick={onInheritKeyframe}>
                  Dùng KF {inheritFrom}
                </Button>
              ) : null}
              <Button disabled={!kfOk || !unlocked || kfApproved} onClick={onApproveKeyframe}>
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
                <Tag>{active?.seconds ?? 5} giây · 16:9</Tag>
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
            <Space wrap>
              <Button
                type="primary"
                icon={<ThunderboltOutlined />}
                disabled={!precheck.ok || !active || !showVideo}
                loading={turboBusy}
                onClick={onCreateVideo}
              >
                {precheck.ok
                  ? engine === 'wan'
                    ? 'Tạo video · xác nhận Wan'
                    : `Tạo video · xác nhận −${expectedCost} cr`
                  : 'Pre-check chưa đạt (0 cr)'}
              </Button>
              <Button
                icon={<ThunderboltOutlined />}
                disabled={sceneBatchDisabled}
                loading={turboBusy}
                onClick={onCreateSceneVideo}
              >
                Tạo hết shot cảnh này · {sceneBatchLabel}
              </Button>
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
              ? `Sau khi khóa ${code}, hệ thống sẽ mở ${studioShotCode(next)}.`
              : allLocked
                ? 'Hết shot đã khóa. Timeline · Final để ghi chú ghép và khóa cảnh.'
                : `Sau khi khóa ${code || 'shot này'}, thêm shot hoặc nhận pack ở Cài đặt nâng cao.`}
          </span>
          {next ? (
            <Button type="primary" onClick={() => onSelectShot(next)}>
              Sang {studioShotCode(next)} →
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
        <h3>KIT ĐANG NHỚ</h3>
        <Tag color={lock.locked ? 'green' : 'gold'}>
          {lock.locked ? 'Continuity đã khóa' : 'Continuity chưa khóa'}
        </Tag>
        <p className="fx-mem__src">
          {lock.sourceShotId || inheritFrom
            ? `Kế thừa từ ${inheritFrom || studioShotCode({ id: lock.sourceShotId } as FamixaSeriesShot)}`
            : 'Khóa shot đầu cảnh để nhớ trang phục / bối cảnh.'}
        </p>
        <p className="fx-mem__line">
          {memoryLine(lock.characters, 28) || '—'}
          {lock.wardrobe ? ` · ${memoryLine(lock.wardrobe, 28)}` : ''}
        </p>
        {lock.environment ? <p className="fx-mem__line">{memoryLine(lock.environment, 48)}</p> : null}
        {lock.camera ? <p className="fx-mem__line">{memoryLine(lock.camera, 40)}</p> : null}
        <Button type="link" style={{ paddingLeft: 0 }} onClick={() => setMemOpen((v) => !v)}>
          {memOpen ? 'Thu gọn ▴' : 'Xem chi tiết ▾'}
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
        <p className="fx-mem__credits">
          Runway đã trừ <strong>{runwaySpent} cr</strong>
          <br />
          Sổ KIT khi khóa: {kitCredits} cr
        </p>
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
  onAddShot,
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
  onAddShot: () => void;
}) {
  const total = shots.reduce((n, s) => n + (s.seconds || 5), 0);
  const locked = shots.filter((s) => statusOf(s) === 'approved').length;
  const pending = shots.filter((s) => statusOf(s) !== 'approved').map((s) => studioShotCode(s));
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
        <Alert type="info" showIcon message="Chưa có shot. Mở Cài đặt nâng cao để dán pack, hoặc thêm shot." />
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
                <span className="fx-timeline__code">{studioShotCode(s)}</span>
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
                  {cues.length ? <Tag>Thoại</Tag> : null}
                  <Tag color={lab.color}>{st === 'approved' ? 'LOCK' : lab.text}</Tag>
                </Space>
              </button>
            );
          })}
        </div>
      )}
      <Button icon={<PlusOutlined />} onClick={onAddShot} style={{ marginTop: 10 }} disabled={sceneLocked}>
        + Thêm shot vào plan
      </Button>

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
