import { useEffect, useMemo, useState } from 'react';
import { Button, Collapse, Drawer, Input, Select, message } from 'antd';
import { recordAssembledCut, takeBlobFromUrl, triggerDownload } from './content-famixa-assemble-render';
import { ContentFamixaApprovalPanel } from './ContentFamixaApprovalPanel';
import { ContentFamixaSceneImagePreflight } from './ContentFamixaSceneImagePreflight';
import { ContentFamixaSceneWorkspace } from './ContentFamixaSceneWorkspace';
import type { FamixaCharacter, FamixaSceneNode, FamixaSeriesShot, SeriesShotRun } from './content-famixa-series';
import type { SceneImageBlocker, SceneImagePreflight } from './kit-video-scene-image-preflight';
import { existingScenePipelineStatus, visualModeLabel } from './kit-video-visual-mode';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import type { ProductionUserMode } from './kit-video-production-ux';
import {
  MIX_STAFF_EPISODE_ROLE,
  staffStageCopy,
  type PreflightResult,
} from './famixa-video-audio-lipsync-pipeline';
import {
  SHOT_PAGE_DEFAULT,
  SHOT_PAGE_SIZES,
  castNames,
  clipOf,
  filterShots,
  imageStatus,
  shortStory,
  shotStatus,
  stillOf,
  staffVideoSendError,
  videoStatus,
  type ShotFilter,
} from './content-famixa-shot-catalog';
import { playVoiceBlob, type VoiceSampleOption } from './content-famixa-voice-sample';
import { fetchContentSeriesVoicePreview } from '@/shared/api/content.api';

const FILTERS: { id: ShotFilter; label: string }[] = [
  { id: 'all', label: 'Tất cả' },
  { id: 'wait', label: 'Chưa làm' },
  { id: 'review', label: 'Cần duyệt' },
  { id: 'done', label: 'Đã xong' },
];

function ShotDetail({
  shot,
  index,
  total,
  run,
  mode,
  creating,
  preflight,
  onClose,
  onCreate,
  onRetry,
  onApprove,
  onReject,
  onPreflightAction,
  onPrev,
  onNext,
  onProduceShot,
}: {
  shot: FamixaSeriesShot;
  index: number;
  total: number;
  run: SeriesShotRun;
  mode: 'scenes' | 'images' | 'video';
  creating?: boolean;
  preflight?: SceneImagePreflight;
  onClose: () => void;
  onCreate?: (shotId: string) => void;
  onRetry?: (shotId: string) => void;
  onApprove?: (shotId: string) => void;
  onReject?: (shotId: string, reason: string) => void;
  onPreflightAction?: (action: SceneImageBlocker['action']) => void;
  onPrev?: () => void;
  onNext?: () => void;
  onProduceShot?: (shotId: string) => void;
}) {
  const still = stillOf(run, shot.story);
  const clip = clipOf(run, shot.story);
  const st = mode === 'images' ? imageStatus(run) : mode === 'video' ? videoStatus(run) : shotStatus(run);
  const [askCreate, setAskCreate] = useState(false);
  const sceneLabel = `Cảnh ${String(index + 1).padStart(2, '0')}`;
  const blocked = Boolean(preflight && !preflight.allowed);
  const ready = Boolean(preflight?.allowed);
  const pipeline = existingScenePipelineStatus(run);
  const invalidPipeline = pipeline === 'INVALID_REFERENCE_PIPELINE';
  const imageApproved = Boolean(run.kfApproved || run.status === 'approved');
  return (
    <div className="fx-shot-detail">
      <p className="fx-prod__kicker">
        {sceneLabel} / {total}
      </p>
      <h2>{sceneLabel}</h2>
      <ContentFamixaVisualModeBadge compact />
      <p className="fx-prod__st">
        {invalidPipeline
          ? '❌ INVALID_REFERENCE_PIPELINE'
          : blocked
            ? '⚠ Chưa đủ điều kiện tạo ảnh'
            : ready && !still
              ? '✓ Scene Ready for Generation'
              : `${st.mark} ${st.label}`}
      </p>
      {invalidPipeline ? (
        <p className="fx-desk__note">
          Cảnh này đã dùng photoreal Canon cũ. Không duyệt vào canonical production. Không xóa. Không tự generate lại.
        </p>
      ) : null}
      <p>Nội dung: {shot.story || shot.scene || '—'}</p>
      <p>Nhân vật: {castNames(shot.characters)}</p>
      {mode !== 'video' && still ? <img src={still} alt="" className="fx-approve__media" /> : null}
      {mode === 'video' && clip ? (
        <video src={clip} controls playsInline preload="metadata" className="fx-approve__media" />
      ) : null}
      {mode === 'video' && clip ? (
        <p className="fx-desk__note">
          Take Runway không có tiếng. Mix TTS khi ghép tập ở Hoàn thiện — miệng chưa khớp lời.
        </p>
      ) : null}
      {mode === 'video' && !clip && still ? (
        <img src={still} alt="" className="fx-approve__media" />
      ) : null}
      {mode === 'video' && !clip ? (
        <div className="fx-desk__btns">
          <p className="fx-desk__note">
            {staffVideoSendError(run.turboError)
              || (still
                ? imageApproved
                  ? 'Ảnh đã duyệt. Tạo video từ ảnh này — trừ credit Runway / Wan.'
                  : 'Ảnh đã có nhưng chưa duyệt. Về tab Hình ảnh để duyệt trước.'
                : 'Cảnh này chưa đủ điều kiện để tạo video.')}
          </p>
          {still && imageApproved && onRetry && /INTERNAL\.BAD_OUTPUT|RENDER_FAILURE/i.test(run.turboError || '') ? (
            <Button type="primary" size="large" loading={creating} onClick={() => onRetry(shot.id)}>
              Gửi lại với camera khác
            </Button>
          ) : still && imageApproved && onCreate ? (
            <Button type="primary" size="large" loading={creating} onClick={() => onCreate(shot.id)}>
              Tạo video {sceneLabel}
            </Button>
          ) : still && !imageApproved ? (
            <Button type="primary" size="large" disabled>
              Chưa duyệt ảnh
            </Button>
          ) : null}
        </div>
      ) : null}
      {mode === 'images' && !still && preflight ? (
        <aside className="fx-vmode-refs">
          <p className="fx-pvs__kicker">CHARACTER REFERENCES</p>
          {(preflight.visualRefs ?? []).length ? (
            <ul>
              {preflight.visualRefs!.map((row) => (
                <li key={row.characterId}>
                  {row.eligible ? '✓' : '❌'} {row.name}
                  {' · '}
                  {row.locked ? 'Character Studio Locked' : 'Chưa khóa Studio'}
                  {' · '}
                  {visualModeLabel(row.visual) || row.visual}
                </li>
              ))}
            </ul>
          ) : (
            <p className="fx-desk__note">Chưa có identity reference đủ điều kiện.</p>
          )}
          <p>
            Visual compatibility: {preflight.visualCompatibility === 'PASS' ? '✓ PASS' : '❌ BLOCKED'}
          </p>
          <p>Legacy photoreal references: {preflight.legacyEligibleCount ?? 0} eligible</p>
        </aside>
      ) : null}
      {mode === 'images' && !still && preflight && onPreflightAction ? (
        <ContentFamixaSceneImagePreflight
          sceneLabel={sceneLabel}
          preflight={preflight}
          onAction={onPreflightAction}
        />
      ) : null}
      {mode === 'images' && !still ? (
        <div className="fx-desk__btns">
          {!preflight ? <p className="fx-desk__note">Chưa có hình ảnh cho cảnh này.</p> : null}
          {blocked ? (
            <Button type="primary" size="large" disabled>
              Chưa thể tạo ảnh
            </Button>
          ) : onCreate && askCreate ? (
            <>
              <p className="fx-desk__note">Gemini vẽ 1 khung theo kịch bản + Canon. Không đổi kịch bản. Không trừ Runway.</p>
              <Button onClick={() => setAskCreate(false)}>Hủy</Button>
              <Button type="primary" size="large" loading={creating} onClick={() => onCreate(shot.id)}>
                Xác nhận tạo ảnh
              </Button>
            </>
          ) : onCreate ? (
            <Button type="primary" size="large" onClick={() => setAskCreate(true)}>
              {ready ? `Tạo ảnh ${sceneLabel}` : 'Tạo ảnh'}
            </Button>
          ) : null}
        </div>
      ) : null}
      {mode === 'images' && still && invalidPipeline ? (
        <p className="fx-desk__note">Không approve. Không đưa vào canonical production.</p>
      ) : null}
      {mode === 'images' && still && !invalidPipeline && imageApproved ? (
        <p className="fx-prod__st">✓ Đã duyệt ảnh. Có thể sang bước Video.</p>
      ) : null}
      {mode === 'images' && still && !invalidPipeline && !imageApproved && onApprove ? (
        <ContentFamixaApprovalPanel
          kind="image"
          onPass={() => onApprove(shot.id)}
          onFail={(reason) => (onReject ? onReject(shot.id, reason) : onClose())}
        />
      ) : null}
      {mode === 'video' && clip && (run.videoApproved || run.status === 'approved') ? (
        <p className="fx-prod__st">✓ Đã duyệt video. Có thể sang bước Hoàn thiện khi đủ cảnh.</p>
      ) : null}
      {mode === 'video' && clip && !(run.videoApproved || run.status === 'approved') && onApprove ? (
        <ContentFamixaApprovalPanel
          kind="video"
          onPass={() => onApprove(shot.id)}
          onFail={(reason) => (onReject ? onReject(shot.id, reason) : onClose())}
        />
      ) : null}
      {mode === 'video' && onProduceShot ? (
        <div className="fx-desk__btns">
          <Button
            type="primary"
            size="large"
            onClick={() => {
              onClose();
              onProduceShot(shot.id);
            }}
          >
            Sản xuất Shot
          </Button>
        </div>
      ) : null}
      <div className="fx-desk__btns">
        <Button disabled={!onPrev} onClick={onPrev}>
          ← Cảnh trước
        </Button>
        <Button disabled={!onNext} onClick={onNext}>
          Cảnh tiếp theo →
        </Button>
      </div>
      <Collapse
        className="fx-sw__tech"
        items={[
          {
            key: 'tech',
            label: 'Thông tin kỹ thuật',
            children: (
              <p className="fx-desk__note">
                Chi tiết hệ thống của cảnh nằm ở chế độ Kỹ thuật. Nhân viên không cần dùng phần này để duyệt.
              </p>
            ),
          },
        ]}
      />
    </div>
  );
}

function ShotCatalog({
  shots,
  runOf,
  mode,
  creating,
  onCreate,
  onRetry,
  onApprove,
  onReject,
  preflightOf,
  onPreflightAction,
  restoreShotId,
  restoreNonce,
  onProduceShot,
}: {
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  mode: 'scenes' | 'images' | 'video';
  creating?: boolean;
  onCreate?: (shotId: string) => void;
  onRetry?: (shotId: string) => void;
  onApprove?: (shotId: string) => void;
  onReject?: (shotId: string, reason: string) => void;
  preflightOf?: (shotId: string) => SceneImagePreflight;
  onPreflightAction?: (shotId: string, action: SceneImageBlocker['action']) => void;
  restoreShotId?: string;
  restoreNonce?: number;
  onProduceShot?: (shotId: string) => void;
}) {
  const [filter, setFilter] = useState<ShotFilter>('all');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(SHOT_PAGE_DEFAULT);
  const [openId, setOpenId] = useState<string>();
  const [picked, setPicked] = useState<string[]>([]);

  useEffect(() => {
    if (restoreShotId) setOpenId(restoreShotId);
  }, [restoreShotId, restoreNonce]);

  const rows = useMemo(
    () => filterShots(shots, runOf, filter, query, mode),
    [shots, runOf, filter, query, mode],
  );
  const pages = Math.max(1, Math.ceil(rows.length / pageSize));
  const visible = rows.slice(page * pageSize, page * pageSize + pageSize);
  const openRow = rows.find((r) => r.shot.id === openId);
  const openAt = openRow ? rows.indexOf(openRow) : -1;

  const title = mode === 'scenes' ? 'Chia cảnh' : mode === 'images' ? 'Hình ảnh các shot' : 'Video các shot';

  return (
    <section className="fx-desk">
      <h2>{title}</h2>
      <p className="fx-desk__lead">
        {shots.length} shot. Đang xem {visible.length}/{rows.length}. Ảnh/video lớn chỉ mở khi xem chi tiết.
      </p>
      {!shots.length ? (
        <article className="fx-media-card">
          <h3>Chưa có cảnh</h3>
          <p>Video này chưa được chia thành các cảnh.</p>
        </article>
      ) : null}

      <div className="fx-shot-tools">
        <div className="fx-series-desk__filters">
          {FILTERS.map((item) => (
            <button
              key={item.id}
              type="button"
              className={filter === item.id ? 'is-on' : ''}
              onClick={() => {
                setFilter(item.id);
                setPage(0);
              }}
            >
              {item.label}
            </button>
          ))}
        </div>
        <Input
          allowClear
          placeholder="Tìm cảnh..."
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setPage(0);
          }}
        />
        <Select
          value={pageSize}
          options={SHOT_PAGE_SIZES.map((n) => ({ value: n, label: `${n} / trang` }))}
          onChange={(n) => {
            setPageSize(n);
            setPage(0);
          }}
        />
      </div>

      {picked.length ? (
        <p className="fx-desk__note">
          Đã chọn {picked.length} cảnh.{' '}
          <Button
            type="link"
            onClick={() => {
              const first = visible.find((r) => picked.includes(r.shot.id));
              if (first) setOpenId(first.shot.id);
            }}
          >
            Xem đã chọn
          </Button>
        </p>
      ) : null}

      {mode === 'scenes' ? (
        <div className="fx-shot-table-wrap">
          <table className="fx-shot-table">
            <thead>
              <tr>
                <th />
                <th>Cảnh</th>
                <th>Nội dung ngắn</th>
                <th>Nhân vật</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {visible.map(({ shot, index, run }) => {
                const st = shotStatus(run);
                const thumb = stillOf(run, shot.story);
                return (
                  <tr key={shot.id} onClick={() => setOpenId(shot.id)}>
                    <td>
                      <input
                        type="checkbox"
                        checked={picked.includes(shot.id)}
                        onClick={(e) => e.stopPropagation()}
                        onChange={(e) =>
                          setPicked((cur) => (e.target.checked ? [...cur, shot.id] : cur.filter((id) => id !== shot.id)))
                        }
                      />
                    </td>
                    <td>
                      {thumb ? <img src={thumb} alt="" className="fx-shot-thumb" /> : <span className="fx-shot-ph" />}
                      {String(index + 1).padStart(2, '0')}
                    </td>
                    <td>{shortStory(shot)}</td>
                    <td>{castNames(shot.characters)}</td>
                    <td>
                      {st.mark} {st.label}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="fx-shot-grid">
          {visible.map(({ shot, index, run }) => {
            const st = mode === 'images' ? imageStatus(run) : videoStatus(run);
            const thumb = stillOf(run, shot.story);
            return (
              <button key={shot.id} type="button" className="fx-shot-tile" onClick={() => setOpenId(shot.id)}>
                {thumb ? <img src={thumb} alt="" /> : <span className="fx-shot-ph fx-shot-ph--tile" />}
                <strong>Cảnh {String(index + 1).padStart(2, '0')}</strong>
                <span>
                  {st.mark} {st.label}
                </span>
              </button>
            );
          })}
        </div>
      )}

      {pages > 1 ? (
        <div className="fx-series-desk__page">
          <Button disabled={page <= 0} onClick={() => setPage((p) => p - 1)}>
            Trước
          </Button>
          <span>
            {page + 1}/{pages}
          </span>
          <Button disabled={page + 1 >= pages} onClick={() => setPage((p) => p + 1)}>
            Sau
          </Button>
        </div>
      ) : null}

      <Drawer
        title={openRow ? `Cảnh ${String(openRow.index + 1).padStart(2, '0')}` : 'Cảnh'}
        open={Boolean(openRow)}
        width={520}
        onClose={() => setOpenId(undefined)}
        destroyOnHidden
      >
        {openRow ? (
          <ShotDetail
            shot={openRow.shot}
            index={openRow.index}
            total={shots.length}
            run={openRow.run}
            mode={mode}
            creating={creating}
            onClose={() => setOpenId(undefined)}
            onCreate={onCreate}
            onRetry={onRetry}
            onApprove={onApprove}
            onReject={onReject}
            preflight={preflightOf?.(openRow.shot.id)}
            onPreflightAction={
              onPreflightAction
                ? (action) => onPreflightAction(openRow.shot.id, action)
                : undefined
            }
            onPrev={openAt > 0 ? () => setOpenId(rows[openAt - 1].shot.id) : undefined}
            onNext={openAt >= 0 && openAt < rows.length - 1 ? () => setOpenId(rows[openAt + 1].shot.id) : undefined}
            onProduceShot={
              onProduceShot
                ? (shotId) => {
                    setOpenId(undefined);
                    onProduceShot(shotId);
                  }
                : undefined
            }
          />
        ) : null}
      </Drawer>
    </section>
  );
}

export function ContentFamixaBuildSceneList({
  shots,
  runOf,
  scenes,
  characters,
  scriptLocked,
  mode,
  onEditScript,
  onSaveCharacters,
  onCreateImage,
  creating,
}: {
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  scenes?: FamixaSceneNode[];
  characters?: FamixaCharacter[];
  scriptLocked?: boolean;
  mode?: ProductionUserMode;
  onEditScript?: () => void;
  onSaveCharacters?: (sceneId: string, characterIds: string[]) => void;
  onCreateImage?: (shotId: string) => void;
  creating?: boolean;
}) {
  return (
    <ContentFamixaSceneWorkspace
      shots={shots}
      runOf={runOf}
      scenes={scenes}
      characters={characters}
      scriptLocked={scriptLocked}
      mode={mode}
      onEditScript={onEditScript}
      onSaveCharacters={onSaveCharacters}
      onCreateImage={onCreateImage}
      creating={creating}
    />
  );
}

export function ContentFamixaBuildImageBoard({
  shots,
  runOf,
  creating,
  onCreate,
  onApprove,
  onReject,
  preflightOf,
  onPreflightAction,
  restoreShotId,
}: {
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  creating?: boolean;
  onCreate?: (shotId: string) => void;
  onApprove?: (shotId: string) => void;
  onReject?: (shotId: string, reason: string) => void;
  preflightOf?: (shotId: string) => SceneImagePreflight;
  onPreflightAction?: (shotId: string, action: SceneImageBlocker['action']) => void;
  restoreShotId?: string;
}) {
  return (
    <ShotCatalog
      shots={shots}
      runOf={runOf}
      mode="images"
      creating={creating}
      onCreate={onCreate}
      onApprove={onApprove}
      onReject={onReject}
      preflightOf={preflightOf}
      onPreflightAction={onPreflightAction}
      restoreShotId={restoreShotId}
    />
  );
}

export function ContentFamixaBuildVideoBoard({
  shots,
  runOf,
  creating,
  onCreate,
  onRetry,
  onApprove,
  onReject,
  restoreShotId,
  restoreNonce,
  onProduceShot,
}: {
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  creating?: boolean;
  onCreate?: (shotId: string) => void;
  onRetry?: (shotId: string) => void;
  onApprove?: (shotId: string) => void;
  onReject?: (shotId: string, reason: string) => void;
  restoreShotId?: string;
  restoreNonce?: number;
  onProduceShot?: (shotId: string) => void;
}) {
  return (
    <ShotCatalog
      shots={shots}
      runOf={runOf}
      mode="video"
      creating={creating}
      onCreate={onCreate}
      onRetry={onRetry}
      onApprove={onApprove}
      onReject={onReject}
      restoreShotId={restoreShotId}
      restoreNonce={restoreNonce}
      onProduceShot={onProduceShot}
    />
  );
}

function EpisodeReel({
  episodeCode,
  items,
  voiceLines,
  onMixVoice,
  mixing,
  canFinalize,
}: {
  episodeCode: string;
  items: { id: string; label: string; src: string; seconds: number }[];
  voiceLines?: { shotId: string; label: string; speaker: string; text: string }[];
  onMixVoice?: () => void;
  mixing?: boolean;
  canFinalize?: boolean;
}) {
  const [i, setI] = useState(0);
  const [busy, setBusy] = useState(false);
  const cur = items[i];
  const stitch = async () => {
    if (items.length < 2) return;
    setBusy(true);
    try {
      let t = 0;
      const clips = items.map((it) => {
        const seconds = it.seconds > 0 ? it.seconds : 5;
        const clip = {
          shotId: it.id,
          code: it.label,
          startSec: t,
          seconds,
          videoUrl: it.src,
          cues: [] as { lineId: string; shotId: string; code: string; name: string; text: string; startSec: number; endSec: number }[],
        };
        t += seconds;
        return clip;
      });
      const blob = await recordAssembledCut({
        clips,
        videoOf: async (id) => {
          const src = items.find((it) => it.id === id)?.src;
          if (!src) throw new Error('Thiếu clip.');
          return takeBlobFromUrl(src);
        },
        audioOf: async () => undefined,
      });
      triggerDownload(blob, `${episodeCode}-tap-preview.webm`);
      message.success(`Đã ghép preview ${items.length} cảnh (${MIX_STAFF_EPISODE_ROLE}). Không phải Final.`);
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Không ghép được 1 file. Tải từng cảnh.');
    } finally {
      setBusy(false);
    }
  };
  if (!cur) return null;
  const total = items.reduce((n, it) => n + (it.seconds || 0), 0);
  return (
    <div className="fx-reel">
      <p className="fx-desk__note">
        Đang phát {cur.label} ({i + 1}/{items.length}). Take Runway là MUTE_TAKE. Thời lượng cảnh ~{cur.seconds}s — chưa phải file ghép {total || items.length} giây.
      </p>
      {voiceLines?.length ? (
        <p className="fx-desk__note">
          Có {voiceLines.length} câu thoại. Ghép TTS trên trình duyệt = {MIX_STAFF_EPISODE_ROLE} — miệng chưa khớp, không phải Final.
        </p>
      ) : (
        <p className="fx-desk__note">
          Cảnh câm: I2V có thể Final sau mix server. Máy không tự viết thoại.
        </p>
      )}
      {canFinalize === false && voiceLines?.length ? (
        <p className="fx-desk__note">Chờ Lip-sync. Video hình ảnh đã tạo — chưa hoàn tất.</p>
      ) : null}
      {voiceLines?.length ? (
        <ul className="fx-wizard__sum">
          {voiceLines.map((line) => (
            <li key={`${line.shotId}-${line.text}`}>
              {line.label} · {line.speaker}: {line.text}
            </li>
          ))}
        </ul>
      ) : null}
      <video
        key={cur.src}
        src={cur.src}
        controls
        playsInline
        autoPlay={i > 0}
        className="fx-approve__media"
        onEnded={() => setI((n) => (n + 1 < items.length ? n + 1 : n))}
      />
      <div className="fx-desk__btns">
        {items.map((it, idx) => (
          <Button key={it.id} type={idx === i ? 'primary' : 'default'} onClick={() => setI(idx)}>
            {it.label}
          </Button>
        ))}
      </div>
      <div className="fx-desk__btns">
        {items.map((it) => (
          <a key={`dl-${it.id}`} className="ant-btn" href={it.src} download={`${episodeCode}-${it.label}.mp4`}>
            Tải {it.label}
          </a>
        ))}
        {items.length > 1 ? (
          <Button loading={busy} onClick={() => void stitch()}>
            Preview {items.length} cảnh câm
          </Button>
        ) : null}
        {onMixVoice ? (
          <Button loading={mixing || busy} onClick={onMixVoice}>
            Preview TTS (không phải Final)
          </Button>
        ) : null}
      </div>
    </div>
  );
}

export function ContentFamixaShotPipelineList({
  rows,
  onCreateLipsync,
}: {
  rows: { id: string; label: string; preflight: PreflightResult }[];
  onCreateLipsync?: (shotId: string) => void;
}) {
  return (
    <ul className="fx-finish__check">
      {rows.map((row) => {
        const copy = staffStageCopy(row.preflight);
        return (
          <li key={row.id}>
            <strong>{row.label}</strong> · {row.preflight.kind}
            <ul>
              <li>{copy.visual.mark} VISUAL · {copy.visual.label}</li>
              <li>{copy.voice.mark} VOICE · {copy.voice.label}</li>
              <li>{copy.video.mark} VIDEO · {copy.video.label}</li>
              <li>{copy.lip.mark} LIP-SYNC · {copy.lip.label}</li>
              <li>{copy.mix.mark} MIX · {copy.mix.label}</li>
              <li>{copy.fin.mark} FINAL · {copy.fin.label}</li>
            </ul>
            {row.preflight.blockers.includes('LIPSYNC_REQUIRED') && onCreateLipsync ? (
              <Button type="primary" onClick={() => onCreateLipsync(row.id)}>
                Create Lip-Sync
              </Button>
            ) : null}
            {row.preflight.kind === 'MULTI_SPEAKER_SHOT' ? (
              <p className="fx-desk__note">LIP-SYNC ⚠ NOT SUPPORTED FOR MULTI-SPEAKER. FINAL BLOCKED.</p>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

export function ContentFamixaBuildVoiceBoard({
  profiles,
  cues,
  voiceOptions,
  locked,
  canLock,
  lockHint,
  ttsBusy,
  onAssignVoice,
  onLockVoice,
  onLockWithoutTts,
  onCreateVoice,
}: {
  profiles: { characterId: string; name: string; status: string; voiceId?: string }[];
  cues: { shotLabel: string; speaker: string; text: string; ready: boolean; durationSec?: number }[];
  voiceOptions?: VoiceSampleOption[];
  locked?: boolean;
  canLock?: boolean;
  lockHint?: string;
  ttsBusy?: boolean;
  onAssignVoice?: (characterId: string, voiceId: string, voiceName?: string) => void;
  onLockVoice?: () => void;
  onLockWithoutTts?: () => void;
  onCreateVoice?: () => void;
}) {
  const missingAudio = cues.filter((c) => !c.ready).length;
  return (
    <section className="fx-desk" id="fx-voice-board">
      <h2>Thoại</h2>
      <p className="fx-desk__lead">
        Giọng đã gán ở tab Nhân vật. Tab này chỉ tạo file đọc từng câu và khóa tập — không chọn giọng theo short.
      </p>
      <h3>1. Giọng nhân vật</h3>
      <ul className="fx-wizard__sum">
        {profiles.map((p) => {
          const open = p.status !== 'ASSIGNED' && !p.voiceId;
          return (
            <li
              key={p.characterId}
              data-voice-gap={open ? '1' : undefined}
              tabIndex={open ? -1 : undefined}
            >
              {p.name}: {open ? 'Chưa gán giọng' : `Đã gán giọng nhân vật · ${p.voiceId}`}
              {!open && p.voiceId ? (
                <Button
                  size="small"
                  style={{ marginLeft: 8 }}
                  onClick={() => {
                    void fetchContentSeriesVoicePreview(p.voiceId!)
                      .then((blob) => playVoiceBlob(blob))
                      .then((ok) => {
                        if (!ok) message.warning('Không phát được mẫu. Không gọi TTS.');
                      })
                      .catch(() => message.warning('Giọng này chưa có file mẫu thư viện.'));
                  }}
                >
                  Nghe mẫu
                </Button>
              ) : null}
              {open && onAssignVoice ? (
                <div style={{ marginTop: 8, maxWidth: 420 }}>
                  <Select
                    size="small"
                    style={{ width: '100%' }}
                    placeholder={`Gán Voice cho ${p.name}`}
                    showSearch
                    optionFilterProp="label"
                    options={voiceOptions}
                    onChange={(id, opt) => {
                      const label = !Array.isArray(opt) && opt && typeof opt === 'object' && 'label' in opt
                        ? String(opt.label)
                        : undefined;
                      onAssignVoice(p.characterId, id, label);
                    }}
                  />
                </div>
              ) : null}
            </li>
          );
        })}
      </ul>
      <h3>2. File đọc từng câu</h3>
      {cues.length ? (
        <ul className="fx-wizard__sum">
          {cues.map((c) => (
            <li
              key={`${c.shotLabel}-${c.text}`}
              data-voice-gap={c.ready ? undefined : '1'}
              tabIndex={c.ready ? undefined : -1}
            >
              {c.shotLabel} · {c.speaker}: {c.text} · {c.ready ? `TTS ${c.durationSec?.toFixed(1)}s` : 'thiếu audio'}
            </li>
          ))}
        </ul>
      ) : (
        <p className="fx-desk__note">Chưa gắn thoại vào cảnh. Thêm lời ở Kịch bản — máy không tự viết.</p>
      )}
      <h3>3. Khóa thoại cả tập</h3>
      {locked ? (
        <p className="fx-desk__note">Thoại đã khóa. Bấm Quay lại tạo ảnh ở banner trên.</p>
      ) : (
        <>
          {lockHint && !canLock ? <p className="fx-desk__note">{lockHint}</p> : null}
          {missingAudio > 0 ? (
            <p className="fx-desk__note">
              Còn {missingAudio} câu thiếu audio. Tạo Full Voice (tính phí ElevenLabs, Confirm) rồi mới khóa — hoặc khóa
              không TTS lại nếu đã tạo trước đó.
            </p>
          ) : (
            <p className="fx-desk__note">Đủ file đọc. Khóa thoại rồi quay lại Hình ảnh.</p>
          )}
          <div className="fx-desk__btns">
            {onCreateVoice ? (
              <Button loading={ttsBusy} onClick={onCreateVoice}>
                Tạo Full Voice
              </Button>
            ) : null}
            {onLockVoice ? (
              <Button type="primary" disabled={!canLock} onClick={onLockVoice}>
                Khóa thoại
              </Button>
            ) : null}
            {onLockWithoutTts && !canLock ? (
              <Button onClick={onLockWithoutTts}>Đã có audio — khóa không TTS lại</Button>
            ) : null}
          </div>
        </>
      )}
    </section>
  );
}

export function ContentFamixaBuildFinishBoard({
  episodeTitle,
  episodeCode,
  shots,
  runOf,
  onBack,
  onPublish,
  voiceLines,
  onMixVoice,
  mixing,
  pipeline,
  canFinalize,
  onCreateLipsync,
}: {
  episodeTitle?: string;
  episodeCode: string;
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  onBack: () => void;
  onPublish?: () => void;
  voiceLines?: { shotId: string; label: string; speaker: string; text: string }[];
  onMixVoice?: () => void;
  mixing?: boolean;
  pipeline?: { id: string; label: string; preflight: PreflightResult }[];
  canFinalize?: boolean;
  onCreateLipsync?: (shotId: string) => void;
}) {
  const stills = shots.map((s) => stillOf(runOf(s.id), s.story)).filter(Boolean);
  const reel = shots
    .map((s, index) => ({
      id: s.id,
      label: `Cảnh ${String(index + 1).padStart(2, '0')}`,
      src: clipOf(runOf(s.id), s.story),
      seconds: s.seconds || 5,
    }))
    .filter((row) => row.src);
  const videos = reel.length;
  const picturesReady = shots.length > 0 && stills.length >= shots.length && videos >= shots.length;
  const done = Boolean(canFinalize) && picturesReady;
  const missing = shots.length - stills.length;
  return (
    <section className="fx-desk">
      <h2>Hoàn thiện video</h2>
      <p className="fx-desk__lead">
        {episodeTitle || 'Video đang sản xuất'} · {episodeCode}
      </p>
      {pipeline?.length ? <ContentFamixaShotPipelineList rows={pipeline} onCreateLipsync={onCreateLipsync} /> : null}
      <ul className="fx-finish__check">
        <li>✓ Kịch bản</li>
        <li>✓ Chia cảnh</li>
        <li>✓ Nhân vật</li>
        <li>{stills.length >= shots.length && shots.length ? '✓' : '○'} Hình ảnh</li>
        <li>{videos >= shots.length && shots.length ? '✓' : '○'} Video hình ảnh đã tạo</li>
        <li>{done ? '✓' : '○'} Final / Director Review</li>
      </ul>
      {picturesReady ? (
        <>
          <EpisodeReel
            episodeCode={episodeCode}
            items={reel}
            voiceLines={voiceLines}
            onMixVoice={onMixVoice}
            mixing={mixing}
            canFinalize={done}
          />
          <div className="fx-desk__btns">
            <Button type="primary" disabled={!done} onClick={onPublish}>
              {done ? 'Gửi Director Review' : 'Chưa Final — chờ Lip-sync / Voice'}
            </Button>
            <Button onClick={onBack}>Về Video Studio</Button>
          </div>
        </>
      ) : (
        <article className="fx-media-card">
          <h3>Video chưa thể hoàn thành</h3>
          <p>{missing > 0 ? `Còn ${missing} shot chưa có hình.` : 'Cần keyframe + I2V trước. Shot có thoại còn cần Voice + Lip-sync + mix server.'}</p>
        </article>
      )}
    </section>
  );
}

export function ContentFamixaBuildPublishBoard({
  episodeTitle,
  episodeCode,
  shots,
  runOf,
  voiceLines,
  onMixVoice,
  mixing,
  canFinalize,
}: {
  episodeTitle?: string;
  episodeCode: string;
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  voiceLines?: { shotId: string; label: string; speaker: string; text: string }[];
  onMixVoice?: () => void;
  mixing?: boolean;
  canFinalize?: boolean;
}) {
  const reel = shots
    .map((s, index) => ({
      id: s.id,
      label: `Cảnh ${String(index + 1).padStart(2, '0')}`,
      src: clipOf(runOf(s.id), s.story),
      seconds: s.seconds || 5,
    }))
    .filter((row) => row.src);
  const stills = shots.map((s) => stillOf(runOf(s.id), s.story)).filter(Boolean);
  const ready = shots.length > 0 && stills.length >= shots.length && reel.length >= shots.length;
  const seconds = reel.reduce((n, s) => n + (s.seconds || 0), 0);
  return (
    <section className="fx-desk">
      <h2>Xuất bản</h2>
      <p className="fx-desk__lead">
        {canFinalize ? 'Sẵn sàng Director Review.' : 'Chưa Final — preview only. Shot có thoại cần Lip-sync.'}
      </p>
      <ul className="fx-wizard__sum">
        <li>Tên video: {episodeTitle || '—'}</li>
        <li>Mã: {episodeCode}</li>
        <li>Số shot: {shots.length}</li>
        <li>Thời lượng ước tính: {seconds} giây (theo shot, không cứng 5 giây)</li>
      </ul>
      {ready ? (
        <EpisodeReel
          episodeCode={episodeCode}
          items={reel}
          voiceLines={voiceLines}
          onMixVoice={onMixVoice}
          mixing={mixing}
          canFinalize={canFinalize}
        />
      ) : null}
      {!ready ? (
        <article className="fx-media-card">
          <h3>Chưa sẵn sàng xuất bản</h3>
          <p>Hoàn thiện mọi shot trước khi xuất bản.</p>
        </article>
      ) : null}
    </section>
  );
}

export function ContentFamixaSharedProductionNote({
  onOpen,
}: {
  onOpen: () => void;
}) {
  return (
    <article className="fx-media-card">
      <h3>Cảnh production dùng chung</h3>
      <p>Đây là cảnh Minh trên Video Engine, không thuộc bản dựng đang mở.</p>
      <Button onClick={onOpen}>Xem cảnh kỹ thuật</Button>
    </article>
  );
}
