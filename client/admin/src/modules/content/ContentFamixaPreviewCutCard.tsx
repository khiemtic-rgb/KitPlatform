import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Radio, Select, Space } from 'antd';
import { PauseOutlined, PlayCircleOutlined } from '@ant-design/icons';
import {
  existingMotionReady,
  storyPreviewReady,
  type PreviewCutPlan,
} from './content-famixa-preview-cut';
import { studioShotCode, type FamixaSeriesShot, type SeriesShotRun } from './content-famixa-series';
import { looksLikeVideoUrl } from './content-famixa-assemble';
import { takeVideoUrl } from './content-famixa-prod-v2';

function sleep(ms: number, stop: { current: boolean }) {
  return new Promise<void>((resolve) => {
    const t = window.setTimeout(resolve, ms);
    const iv = window.setInterval(() => {
      if (stop.current) {
        window.clearTimeout(t);
        window.clearInterval(iv);
        resolve();
      }
    }, 80);
    window.setTimeout(() => window.clearInterval(iv), ms + 20);
  });
}

function unlockPreviewAudio() {
  const AC = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AC) return;
  const ctx = new AC();
  void ctx.resume();
  const buf = ctx.createBuffer(1, 1, 22050);
  const src = ctx.createBufferSource();
  src.buffer = buf;
  src.connect(ctx.destination);
  try {
    src.start(0);
  } catch {
    /* already unlocked */
  }
  window.setTimeout(() => void ctx.close(), 800);
}

function playUrl(url: string, stop: { current: boolean }) {
  return new Promise<void>((resolve) => {
    const el = new Audio(url);
    el.preload = 'auto';
    el.volume = 1;
    let settled = false;
    const done = () => {
      if (settled) return;
      settled = true;
      window.clearInterval(iv);
      el.pause();
      resolve();
    };
    const iv = window.setInterval(() => {
      if (stop.current) done();
    }, 80);
    el.onended = done;
    el.onerror = done;
    void el.play().catch(done);
  });
}

export function ContentFamixaPreviewCutCard({
  shots,
  allShots,
  fromId,
  toId,
  onRange,
  plan,
  runOf,
  ttsUrlOf,
  sessionSrcOf,
  fillBusy,
  onFillMissing,
  onGoVideo,
  onBump10s,
  onEnsureTts,
  onAssembleCut,
  assembleBusy,
  assembleAspect,
  onAssembleAspect,
}: {
  shots: FamixaSeriesShot[];
  allShots?: FamixaSeriesShot[];
  fromId?: string;
  toId?: string;
  onRange: (fromId: string, toId: string) => void;
  plan: PreviewCutPlan;
  runOf?: (shot: FamixaSeriesShot) => SeriesShotRun;
  ttsUrlOf: (lineId: string) => string | undefined;
  sessionSrcOf?: (id: string) => string | undefined;
  fillBusy?: boolean;
  turboBusy?: boolean | string;
  onFillMissing: (kind: 'story' | 'motion') => void;
  onOpenShot: (shot: FamixaSeriesShot) => void;
  onGoVideo?: () => void;
  onBump10s?: (ids: string[]) => void;
  onEnsureTts?: () => Promise<number>;
  onAssembleCut?: () => void;
  assembleBusy?: boolean;
  assembleAspect?: '16:9' | '9:16';
  onAssembleAspect?: (aspect: '16:9' | '9:16') => void;
}) {
  const stop = useRef(false);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [playing, setPlaying] = useState<'story' | 'motion' | undefined>(undefined);
  const [cursor, setCursor] = useState<string | undefined>(plan.items[0]?.shotId);
  const kfNow = cursor ? plan.items.find((i) => i.shotId === cursor) : plan.items[0];
  const shotNow = shots.find((s) => s.id === (cursor || plan.items[0]?.shotId));
  const kfSrc = shotNow ? runOf?.(shotNow)?.keyframeDataUrl : undefined;
  const vidSrc = shotNow
    ? takeVideoUrl(runOf?.(shotNow)) || sessionSrcOf?.(shotNow.id)
    : undefined;
  const showTake = Boolean(vidSrc && looksLikeVideoUrl(vidSrc) && playing !== 'story');

  useEffect(() => () => {
    stop.current = true;
  }, []);
  useEffect(() => {
    if (!cursor || !plan.items.some((i) => i.shotId === cursor)) {
      setCursor(plan.items.find((i) => i.hasKf)?.shotId || plan.items[0]?.shotId);
    }
  }, [cursor, plan.fromCode, plan.toCode, plan.items.length]);

  const opts = shots.map((s) => ({
    value: s.id,
    label: `${studioShotCode(s, allShots ?? shots)}${runOf(s)?.lipsynced ? ' · KHỚP MÔI' : ''}`,
  }));
  const storyOk = storyPreviewReady(plan);
  const haveTakes = existingMotionReady(plan);
  const spoken = plan.items.filter((i) => !i.silent);
  const voiceFiles = spoken.filter((i) => i.hasVoiceFile).length;
  const missingTts = spoken.filter((i) => !i.lipsynced && !i.hasVoiceFile).length;
  const lipN = plan.items.filter((i) => i.lipsynced).length;

  const runPreview = async (kind: 'story' | 'motion') => {
    unlockPreviewAudio();
    stop.current = false;
    setPlaying(kind);
    try {
      if (onEnsureTts) await onEnsureTts();
      const queue = kind === 'motion' ? plan.items.filter((i) => i.hasVideo) : plan.items;
      if (kind === 'motion' && !queue.length) return;
      for (const item of queue) {
        if (stop.current) break;
        setCursor(item.shotId);
        await sleep(80, stop);
        const clip = videoRef.current;
        const keepLip = kind === 'motion' && item.lipsynced;
        if (clip) {
          clip.muted = !keepLip;
          clip.volume = keepLip ? 1 : 0;
        }
        const voices = keepLip ? [] : item.lines.length ? item.lines : item.line ? [item.line] : [];
        const ttsJob = (async () => {
          let played = 0;
          for (const line of voices) {
            const url = ttsUrlOf(line.id);
            if (!url) continue;
            played += 1;
            await playUrl(url, stop);
            if (stop.current) break;
          }
          return played;
        })();
        if (kind === 'motion' && clip && item.hasVideo) {
          try {
            clip.currentTime = 0;
            await clip.play();
          } catch {
            /* autoplay / missing take */
          }
        }
        const played = await ttsJob;
        if (!played) {
          const hold =
            kind === 'motion' && clip && Number.isFinite(clip.duration) && clip.duration > 0
              ? clip.duration
              : item.silent
                ? item.seconds
                : item.voiceSec || item.seconds;
          await sleep(hold * 1000, stop);
        }
        clip?.pause();
      }
    } finally {
      videoRef.current?.pause();
      setPlaying(undefined);
    }
  };

  const kfN = plan.items.filter((i) => i.hasKf).length;
  const vidN = plan.items.filter((i) => i.hasVideo).length;
  const overflow = plan.items.filter((i) => i.durationIssue);
  const extraPreview = plan.extraLines[0];

  return (
    <section className="fx-card">
      <p className="fx-kf__kicker">06 PREVIEW — xem hình + thoại theo đúng thứ tự Short</p>
      <h3>PREVIEW</h3>
      <p className="fx-shot__hint">
        Dải đã chọn. GHÉP PREVIEW = KF tĩnh + TTS session (0 Runway). Không đảo thứ tự.
      </p>
      <Space wrap style={{ marginBottom: 10 }}>
        <Select
          value={fromId}
          options={opts}
          style={{ width: 128 }}
          onChange={(id) => onRange(id, toId || shots.at(-1)?.id || id)}
        />
        <span>→</span>
        <Select
          value={toId}
          options={opts}
          style={{ width: 128 }}
          onChange={(id) => onRange(fromId || shots[0]?.id || id, id)}
        />
      </Space>
      <p className="fx-plan">
        {plan.items.length} Shorts · {plan.estimatedSec}s · KF {kfN}/{plan.items.length} · Voice file {voiceFiles}/
        {spoken.length || 0} · Video {vidN}/{plan.items.length}
        {lipN ? ` · Khớp môi ${lipN}` : ''}
      </p>
      <ul className="fx-cut-list">
        {plan.items.map((row) => (
          <li key={row.shotId}>
            <button
              type="button"
              className={row.shotId === kfNow?.shotId ? 'fx-cut-list__on' : undefined}
              onClick={() => setCursor(row.shotId)}
            >
              {row.code}
              {row.lipsynced ? <span className="fx-cut-list__lip">KHỚP MÔI</span> : null}
              <span>{row.hasKf ? '✓' : '⚠'}</span>
            </button>
          </li>
        ))}
      </ul>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 8 }}
        message={
          lipN
            ? `${lipN} take đã khớp môi — ghép giữ tiếng trong file Fal, không overlay TTS hàng đó. Take còn lại vẫn mix thoại session.`
            : 'Take Runway câm. Nghe tiếng = TTS session overlay, hoặc Ghép take đã có (MP4 + thoại). Không cần tạo ảnh / Runway mới.'
        }
      />
      {missingTts > 0 ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          message={`${missingTts} Short có thoại nhưng mất file TTS session (F5). Hình vẫn chạy, không có tiếng. Phát lại ở bước Thoại — không gọi ElevenLabs nếu đã khóa.`}
        />
      ) : null}
      {plan.extraLines.length ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          message={`${plan.extraLines.length} câu chưa gắn Short — không ghép. ${extraPreview?.name ?? ''} “${(extraPreview?.text ?? '').slice(0, 80)}”`}
        />
      ) : null}
      {plan.durationBlocked ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          message={`${overflow.map((i) => i.code).join(', ')}: một câu dài hơn 10s. Tách câu trong kịch bản — KIT không cắt giữa chữ.`}
          action={
            onBump10s ? (
              <Button size="small" type="primary" onClick={() => onBump10s(overflow.map((i) => i.shotId))}>
                Đổi 10s
              </Button>
            ) : null
          }
        />
      ) : null}
      <Space wrap style={{ margin: '10px 0' }}>
        <Button loading={fillBusy} disabled={!plan.storyMissingKf.length} onClick={() => onFillMissing('story')}>
          {plan.storyMissingKf.length ? `Tạo ${plan.storyMissingKf.length} hình còn thiếu` : 'Đã đủ hình'}
        </Button>
        <Button
          type="primary"
          icon={playing ? <PauseOutlined /> : <PlayCircleOutlined />}
          disabled={!storyOk && !playing}
          onClick={() => {
            if (playing) {
              stop.current = true;
              return;
            }
            void runPreview('story');
          }}
        >
          {playing === 'story' ? 'Dừng' : '▶ GHÉP PREVIEW'}
        </Button>
        {vidN > 0 ? (
          <Button
            disabled={!haveTakes && playing !== 'motion'}
            onClick={() => {
              if (playing === 'motion') {
                stop.current = true;
                return;
              }
              void runPreview('motion');
            }}
          >
            {playing === 'motion'
              ? 'Dừng motion'
              : lipN
                ? `Phát ${vidN} take · ${lipN} khớp môi`
                : `Phát ${vidN} take đã có + thoại`}
          </Button>
        ) : (
          <Button type="primary" ghost onClick={onGoVideo}>
            Sang 5 Video — gửi Runway
          </Button>
        )}
        {vidN > 0 && onAssembleCut ? (
          <Button type="primary" ghost loading={assembleBusy} disabled={assembleBusy} onClick={() => onAssembleCut()}>
            {lipN
              ? `Ghép tập hoàn chỉnh · ${vidN} take · ${lipN} khớp môi`
              : `Ghép tập hoàn chỉnh · đủ shot + thoại`}
          </Button>
        ) : null}
        {onAssembleAspect ? (
          <Radio.Group
            size="small"
            value={assembleAspect ?? '16:9'}
            onChange={(e) => onAssembleAspect(e.target.value)}
          >
            <Radio.Button value="16:9">Xuất 16:9</Radio.Button>
            <Radio.Button value="9:16">Xuất 9:16</Radio.Button>
          </Radio.Group>
        ) : null}
      </Space>
      <div className="fx-cut-stage fx-cut-stage--hero">
        {showTake && vidSrc ? (
          <video
            key={shotNow?.id}
            ref={videoRef}
            src={vidSrc}
            controls
            muted={!kfNow?.lipsynced}
            playsInline
            preload="auto"
          />
        ) : kfSrc ? (
          <img src={kfSrc} alt={kfNow?.code} />
        ) : (
          <div className="fx-cut-empty">Chưa có hình trên Short này</div>
        )}
        {kfNow?.line ? (
          <p className="fx-cut-line">
            {kfNow.code} · {kfNow.line.name}: “{kfNow.line.text}”
          </p>
        ) : kfNow ? (
          <p className="fx-cut-line">{kfNow.code} · Voice: NONE</p>
        ) : (
          <p className="fx-cut-line">Chọn Short trên dải để xem KF</p>
        )}
      </div>
    </section>
  );
}
