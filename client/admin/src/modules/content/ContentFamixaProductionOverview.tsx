import { useEffect, useState } from 'react';
import { Alert, Button } from 'antd';
import {
  fetchImageDirectorReview,
  fetchKitVideoProductionShots,
  fetchProductionVideoContract,
  fetchVideoGenerationExecution,
  type ImageGenerationDirectorReviewRow,
  type KitVideoProductionShotRow,
  type ProductionVideoContractRow,
  type VideoGenerationExecutionRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { deriveDirectorWorkspace, displayShotTitle, shotSceneLabel } from './kit-video-director-workspace';
import {
  countImageReviewed,
  countPrepDone,
  countVideoMade,
  countVideoReviewed,
  deriveSeriesTrack,
  episodeWorkStatus,
  nextWorkCopy,
  sceneListStatus,
  shotUserLabel,
} from './kit-video-production-ux';
import { episodeStoryLine, type ProductionProgressNode } from './kit-video-production-workflow';

type ShotLive = {
  shot: KitVideoProductionShotRow;
  review?: ImageGenerationDirectorReviewRow;
  video?: ProductionVideoContractRow;
  generation?: VideoGenerationExecutionRow;
};

export function ContentFamixaProductionOverview({
  episodeCode,
  episodeTitle,
  scriptLocked,
  sceneCount,
  shotCount,
  characterCount,
  storyLine,
  shotGraphLocked,
  onOpenShot,
  onFocusShot,
  onFinish,
  onOpenTab,
  embedded,
  buildScope,
}: {
  episodeCode: string;
  episodeTitle?: string;
  scriptLocked: boolean;
  sceneCount: number;
  shotCount?: number;
  characterCount?: number;
  storyLine?: string;
  shotGraphLocked: boolean;
  onOpenShot: (shotId: string) => void;
  onFocusShot?: (shotId: string) => void;
  onFinish?: () => void;
  onOpenTab?: (tab: 'script' | 'scenes' | 'characters' | 'images' | 'video' | 'finish' | 'publish') => void;
  embedded?: boolean;
  buildScope?: {
    characterLocked: boolean;
    cards: { id: string; firstShotId?: string; scene: string; title: string; mark: string; label: string }[];
    imagesMade: number;
    videosMade: number;
    progress?: ProductionProgressNode[];
    stageLine?: string;
    nextAction?: string;
  };
}) {
  const [rows, setRows] = useState<ShotLive[]>([]);
  const [characterLocked, setCharacterLocked] = useState(false);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (buildScope) {
      setBusy(false);
      setCharacterLocked(buildScope.characterLocked);
      setRows([]);
      setError(undefined);
      return;
    }
    setBusy(true);
    void fetchKitVideoProductionShots()
      .then(async (bundle) => {
        const shots = bundle.shots || [];
        const live = await Promise.all(
          shots.map(async (shot) => {
            const [review, video, generation] = await Promise.all([
              fetchImageDirectorReview(shot.id).catch(() => undefined),
              fetchProductionVideoContract(shot.id).catch(() => undefined),
              fetchVideoGenerationExecution(shot.id).catch(() => undefined),
            ]);
            return { shot, review, video, generation };
          }),
        );
        setRows(live);
        setCharacterLocked(
          (Boolean(bundle.master?.lockedAt) &&
            (bundle.dna?.status || '').toUpperCase() === 'LOCKED' &&
            (bundle.pack?.status || '').toUpperCase() === 'LOCKED') ||
            live.some(
              (r) =>
                (r.review?.master || '').toUpperCase() === 'LOCKED' &&
                (r.review?.dna || '').toUpperCase() === 'LOCKED' &&
                (r.review?.prp || '').toUpperCase() === 'LOCKED',
            ),
        );
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không tải được tiến độ sản xuất.')))
      .finally(() => setBusy(false));
  }, [buildScope?.characterLocked, buildScope?.cards.length]);

  const shotTotal = shotCount ?? (buildScope ? buildScope.cards.length : rows.length);
  const liveSceneCount = sceneCount;
  const prepDone = rows.filter((r) => countPrepDone(r.review?.shotContract)).length;
  const imagesReviewed = rows.filter((r) => countImageReviewed(r.review?.directorApproval)).length;
  const videosMade = rows.filter((r) => countVideoMade(r.generation?.status)).length;
  const videosReviewed = rows.filter((r) => countVideoReviewed(r.generation?.status)).length;
  const track = deriveSeriesTrack({
    scriptLocked,
    sceneCount: liveSceneCount,
    shotCount: shotTotal,
    shotGraphLocked,
    characterLocked: buildScope?.characterLocked ?? characterLocked,
    prepDone: buildScope ? buildScope.cards.length : prepDone,
    shotTotal,
    imagesMade: buildScope ? buildScope.imagesMade : imagesReviewed,
    imagesReviewed: buildScope ? buildScope.imagesMade : imagesReviewed,
    videosMade: buildScope ? buildScope.videosMade : videosMade,
    videosReviewed: buildScope ? buildScope.videosMade : videosReviewed,
    videosDone: buildScope ? buildScope.videosMade : videosReviewed,
  });
  const work = episodeWorkStatus({
    shotTotal,
    videosDone: buildScope ? buildScope.videosMade : videosReviewed,
  });
  const progressNodes = buildScope?.progress ?? track.nodes.map((node) => ({ ...node, detail: node.label }));
  const doneCount = progressNodes.filter((n) => n.done).length;
  const stageLine = buildScope?.stageLine || `${doneCount}/${Math.max(progressNodes.length, 1)} bước`;

  const liveRows = rows.map((row) => {
    const input = {
      master: row.review?.master,
      dna: row.review?.dna,
      prp: row.review?.prp,
      shotContract: row.review?.shotContract,
      executionStatus: row.review?.executionStatus,
      directorApproval: row.review?.directorApproval,
      stillApproved: row.video?.stillApproved,
      videoContractStatus: row.video?.status,
      videoContractId: row.video?.id,
      videoGenerationStatus: row.generation?.status,
      videoPreflightPass: row.generation?.preflightPass,
    };
    return {
      row,
      user: shotUserLabel(input),
      derived: deriveDirectorWorkspace(input),
    };
  });
  const urgent = liveRows.find((x) => x.user.urgent);
  const next = nextWorkCopy({
    scriptLocked,
    sceneCount: liveSceneCount,
    characterLocked: buildScope?.characterLocked ?? characterLocked,
    action: buildScope ? undefined : urgent?.derived.action,
    sceneLabel: urgent ? shotSceneLabel(urgent.row.shot.shotCode) : 'Cảnh',
    done: work.tone === 'done',
    imagesPending: buildScope ? buildScope.imagesMade < buildScope.cards.length : false,
    videosPending: buildScope ? buildScope.videosMade < buildScope.cards.length : false,
  });

  return (
    <section className="fx-prod">
      {embedded ? null : (
        <header className="fx-prod__head">
          <h2 className="fx-prod__title">Tổng quan tập phim</h2>
          <p className="fx-prod__lead">{episodeTitle || episodeCode}</p>
        </header>
      )}

      {embedded ? (
        <header className="fx-prod__head">
          <p className="fx-prod__kicker">TỔNG QUAN TẬP PHIM</p>
          <h2 className="fx-prod__title">{episodeTitle || episodeCode}</h2>
          <p className="fx-prod__st">{storyLine || episodeStoryLine({ sceneCount: liveSceneCount, shotCount: shotTotal, characterCount: characterCount ?? 0 })}</p>
          <p className="fx-prod__st">Trạng thái: {work.label}</p>
        </header>
      ) : null}

      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 12 }} /> : null}

      {embedded ? null : (
      <aside className="fx-prod__now">
        <p className="fx-prod__kicker">VIỆC CẦN LÀM NGAY</p>
        <h3>{next.headline}</h3>
        <p>{next.hint}</p>
        <Button
          type="primary"
          size="large"
          onClick={() => {
            if (next.tab === 'finish' || next.tab === 'publish') onFinish?.();
            else if (!buildScope && urgent && (next.tab === 'images' || next.tab === 'video' || next.tab === 'scenes')) onOpenShot(urgent.row.shot.id);
            else onOpenTab?.(next.tab);
          }}
        >
          {next.tab === 'finish' ? 'Sang hoàn thiện tập →' : `${next.action} →`}
        </Button>
      </aside>
      )}

      <section className="fx-prod__progress">
        <p className="fx-prod__kicker">TIẾN ĐỘ SẢN XUẤT</p>
        <p className="fx-prod__pct">
          Tiến độ: {stageLine}
        </p>
        <div className="fx-prod__bar" aria-hidden>
          <span style={{ width: `${Math.round((doneCount / Math.max(progressNodes.length, 1)) * 100)}%` }} />
        </div>
        <ol className="fx-prod__pipe">
          {progressNodes.map((node, i, list) => {
            const now = !node.done && list.slice(0, i).every((x) => x.done);
            return (
              <li key={node.id} className={`fx-prod__node${now ? ' is-now' : ''}${node.done ? ' is-done' : ''}`}>
                <span>{node.done ? '✓' : now ? '●' : '○'}</span>
                <strong>{node.label}</strong>
                <em>{'detail' in node ? node.detail : ''}</em>
              </li>
            );
          })}
        </ol>
      </section>

      <section className="fx-prod__shots">
        <p className="fx-prod__kicker">CẢNH GẦN ĐÂY</p>
        {buildScope ? (
          <>
            {!buildScope.cards.length ? <p className="fx-prod__empty">Bản dựng này chưa có cảnh.</p> : null}
            {buildScope.cards
              .filter((card) => card.mark !== '✓')
              .concat(buildScope.cards.filter((card) => card.mark === '✓'))
              .slice(0, 3)
              .map((card) => (
              <article key={card.id} className="fx-prod__card">
                <div>
                  <p className="fx-prod__code">{card.scene}</p>
                  <h3>{card.title}</h3>
                  <p className="fx-prod__st">
                    {card.mark} {card.label}
                  </p>
                </div>
                <Button
                  type="primary"
                  onClick={() => {
                    const target = card.firstShotId || card.id;
                    if (onFocusShot) onFocusShot(target);
                    else onOpenTab?.('scenes');
                  }}
                >
                  {onFocusShot ? 'Mở Shot' : 'Xem cảnh'}
                </Button>
              </article>
            ))}
            {buildScope.cards.length > 3 ? (
              <Button type="link" onClick={() => onOpenTab?.('scenes')}>
                Xem tất cả cảnh
              </Button>
            ) : null}
          </>
        ) : (
          <>
        {busy && !rows.length ? <p className="fx-prod__empty">Đang tải cảnh…</p> : null}
        {!busy && !rows.length ? (
          <p className="fx-prod__empty">Chưa có cảnh nào vào sản xuất.</p>
        ) : null}
        {liveRows.map(({ row, derived }) => {
          const { shot } = row;
          const title = displayShotTitle(shot.spec, shot.note);
          const scene = shotSceneLabel(shot.shotCode);
          const st = sceneListStatus(derived.action);
          return (
            <article key={shot.id} className={`fx-prod__card${urgent?.row.shot.id === shot.id ? ' fx-prod__card--review' : ''}`}>
              <div>
                <p className="fx-prod__code">{scene}</p>
                <h3>{title || `${shot.characterName?.trim() || 'Nhân vật'} · ${scene}`}</h3>
                <p className="fx-prod__st">
                  {st.mark} {st.label}
                </p>
              </div>
              <Button type="primary" onClick={() => onOpenShot(shot.id)}>
                Xem cảnh
              </Button>
            </article>
          );
        })}
          </>
        )}
      </section>
    </section>
  );
}
