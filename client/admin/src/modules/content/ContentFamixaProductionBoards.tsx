import { useEffect, useState } from 'react';
import { Button, Collapse } from 'antd';
import {
  fetchImageDirectorReview,
  fetchImageGenerationExecutionArtifactBlob,
  fetchKitVideoProductionShots,
  fetchProductionVideoContract,
  fetchVideoGenerationExecution,
  fetchVideoGenerationExecutionArtifactBlob,
  type ImageGenerationDirectorReviewRow,
  type KitVideoProductionShotRow,
  type ProductionVideoContractRow,
  type VideoGenerationExecutionRow,
} from '@/shared/api/content.api';
import { deriveDirectorWorkspace, displayShotTitle, shortShotLabel, shotSceneLabel } from './kit-video-director-workspace';
import {
  imageBoardStatus,
  sceneListStatus,
  shotUserLabel,
  staffName,
  videoBoardStatus,
} from './kit-video-production-ux';
import { ContentFamixaCharacterDesignLanguageCard } from './ContentFamixaCharacterDesignLanguageCard';
import { ContentFamixaCharacterDesignLanguageV2Card } from './ContentFamixaCharacterDesignLanguageV2Card';
import { ContentFamixaCharacterStudio } from './ContentFamixaCharacterStudio';
import { ContentFamixaProjectVisualStyleCard } from './ContentFamixaProjectVisualStyleCard';
import { ContentFamixaVisualUniverseAuthorityCard } from './ContentFamixaVisualUniverseAuthorityCard';
import { ContentFamixaVisualCalibrationCard } from './ContentFamixaVisualCalibrationCard';
import { ContentFamixaIdentityConditionedCalibrationDirectorReviewCard } from './ContentFamixaIdentityConditionedCalibrationDirectorReviewCard';
import { ContentFamixaCharacterCalibrationShortcut } from './ContentFamixaCharacterCalibrationShortcut';
import { ContentKitVideoImageGenerationExecutionCard } from './ContentKitVideoImageGenerationExecutionCard';

type Live = {
  shot: KitVideoProductionShotRow;
  review?: ImageGenerationDirectorReviewRow;
  video?: ProductionVideoContractRow;
  generation?: VideoGenerationExecutionRow;
  still?: string;
  clip?: string;
};

function useLiveProduction() {
  const [rows, setRows] = useState<Live[]>([]);
  const [locked, setLocked] = useState(false);
  const [ready, setReady] = useState(false);
  useEffect(() => {
    const urls: string[] = [];
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
            let still: string | undefined;
            let clip: string | undefined;
            const execId = review?.executionId || video?.stillExecutionId;
            if (execId && (review?.artifactReadable || video?.artifactReadable)) {
              still = await fetchImageGenerationExecutionArtifactBlob(shot.id, execId).catch(() => undefined);
              if (still) urls.push(still);
            }
            if (generation?.id && generation.artifactReadable) {
              clip = await fetchVideoGenerationExecutionArtifactBlob(shot.id, generation.id).catch(() => undefined);
              if (clip) urls.push(clip);
            }
            return { shot, review, video, generation, still, clip };
          }),
        );
        setRows(live);
        setReady(true);
        setLocked(
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
      })
      .catch(() => setReady(true));
    return () => urls.forEach((u) => URL.revokeObjectURL(u));
  }, []);
  return { rows, locked, ready };
}

function heading(shot: KitVideoProductionShotRow) {
  const scene = shotSceneLabel(shot.shotCode);
  const name = staffName(shot.characterName);
  const title = displayShotTitle(shot.spec, shot.note);
  return title ? `${scene} — ${title}` : `${scene} · ${name}`;
}

function inputOf(row: Live) {
  return {
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
}

function userOf(row: Live) {
  return shotUserLabel(inputOf(row));
}

function actionOf(row: Live) {
  return deriveDirectorWorkspace(inputOf(row)).action;
}

export function ContentFamixaSceneList({ onOpen }: { onOpen: (id: string) => void }) {
  const { rows, ready } = useLiveProduction();
  return (
    <section className="fx-desk">
      <h2>Cảnh</h2>
      <p className="fx-desk__lead">Video này có {ready ? rows.length : '…'} cảnh production. Chọn cảnh để xem việc cần làm.</p>
      {!ready ? <p className="fx-desk__note">Đang tải cảnh…</p> : null}
      {ready && !rows.length ? <p className="fx-desk__note">Chưa có cảnh vào sản xuất.</p> : null}
      {rows.map((row) => {
        const user = userOf(row);
        const st = sceneListStatus(actionOf(row));
        const sec = row.generation?.durationSeconds || 5;
        return (
          <article key={row.shot.id} className="fx-scene-card">
            <h3>{heading(row.shot)}</h3>
            <p>Nhân vật: {staffName(row.shot.characterName)}</p>
            <p>Thời lượng: {sec} giây</p>
            <p className="fx-prod__st">
              {st.mark} {st.label}
            </p>
            <Button type="primary" onClick={() => onOpen(row.shot.id)}>
              {st.label === 'Hoàn thành' ? 'Xem cảnh' : user.action}
            </Button>
          </article>
        );
      })}
    </section>
  );
}

export function ContentFamixaImageBoard({ onOpen }: { onOpen: (id: string) => void }) {
  const { rows, ready } = useLiveProduction();
  return (
    <section className="fx-desk">
      <h2>Hình ảnh</h2>
      <p className="fx-desk__lead">Xem ảnh từng cảnh. Duyệt trước khi tạo video. Máy không tự duyệt.</p>
      {!ready ? <p className="fx-desk__note">Đang tải cảnh…</p> : null}
      {ready && !rows.length ? <p className="fx-desk__note">Chưa có cảnh production.</p> : null}
      {rows.map((row) => {
        const st = imageBoardStatus(actionOf(row), Boolean(row.still));
        return (
          <article key={row.shot.id} className="fx-media-card">
            <h3>{heading(row.shot)}</h3>
            <p>Nhân vật: {staffName(row.shot.characterName)}</p>
            {row.still ? <img src={row.still} alt="" className="fx-media-card__still" /> : <p>Chưa tạo ảnh</p>}
            <p className="fx-prod__st">
              {st.mark} {st.label}
            </p>
            <Button type="primary" onClick={() => onOpen(row.shot.id)}>
              {st.action}
            </Button>
          </article>
        );
      })}
    </section>
  );
}

export function ContentFamixaVideoBoard({ onOpen }: { onOpen: (id: string) => void }) {
  const { rows, ready } = useLiveProduction();
  return (
    <section className="fx-desk">
      <h2>Video</h2>
      <p className="fx-desk__lead">Xem và duyệt video từng cảnh. Máy không tự duyệt.</p>
      {!ready ? <p className="fx-desk__note">Đang tải cảnh…</p> : null}
      {ready && !rows.length ? <p className="fx-desk__note">Chưa có cảnh production.</p> : null}
      {rows.map((row) => {
        const st = videoBoardStatus(actionOf(row), Boolean(row.clip));
        const sec = row.generation?.durationSeconds || 5;
        return (
          <article key={row.shot.id} className="fx-media-card">
            <h3>{heading(row.shot)}</h3>
            <p>Nhân vật: {staffName(row.shot.characterName)}</p>
            {row.clip ? (
              <video src={row.clip} controls playsInline preload="metadata" className="fx-media-card__clip" />
            ) : (
              <p>Chưa có video.</p>
            )}
            <p>
              {sec} giây · {st.mark} {st.label}
            </p>
            {row.clip ? (
              <p className="fx-dir-ws__dl">
                <a href={row.clip} download={`${shortShotLabel(row.shot.shotCode)}.mp4`}>
                  Tải video · {shortShotLabel(row.shot.shotCode)}.mp4
                </a>
              </p>
            ) : null}
            <Button type="primary" onClick={() => onOpen(row.shot.id)}>
              {st.action}
            </Button>
          </article>
        );
      })}
    </section>
  );
}

export function ContentFamixaCharacterBoard({
  mode = 'staff',
  shots: _shots,
  onBuildEpisode,
  focusCharacterId,
  focusPanel,
  returnToScene,
  onReturnToScene,
  voices,
  voiceById,
  onAssignVoice,
}: {
  mode?: 'staff' | 'director';
  shots?: { characterIds?: string[]; characters?: string[] }[];
  onBuildEpisode?: () => void;
  focusCharacterId?: string;
  focusPanel?: 'studio' | 'universe';
  returnToScene?: { shotId: string; sceneLabel: string };
  onReturnToScene?: () => void;
  voices?: { value: string; label: string; previewUrl?: string | null }[];
  voiceById?: Record<string, string>;
  onAssignVoice?: (characterId: string, voiceId: string, voiceName?: string) => void;
}) {
  const [bibleReady, setBibleReady] = useState(false);
  const director = mode === 'director';
  const keepTechOpen = !director && (focusPanel === 'universe' || Boolean(returnToScene));
  const technical = (
    <>
      <ContentFamixaCharacterCalibrationShortcut />
      <ContentFamixaProjectVisualStyleCard />
      <ContentFamixaVisualUniverseAuthorityCard />
      <ContentFamixaVisualCalibrationCard />
      <ContentFamixaIdentityConditionedCalibrationDirectorReviewCard />
      <ContentFamixaCharacterDesignLanguageCard />
      <ContentFamixaCharacterDesignLanguageV2Card />
    </>
  );
  return (
    <section className="fx-desk" data-director-desk={director ? 'people' : undefined}>
      {director ? (
        <>
          <h2>Người</h2>
          <p className="fx-desk__lead">Mặt, ảnh chuẩn, giọng và khóa hồ sơ. Không sang tab Thoại để gán lại cùng giọng.</p>
        </>
      ) : null}
      {director || (bibleReady && !keepTechOpen) ? (
        <Collapse
          ghost
          className="fx-director-tech"
          items={[{
            key: 'cast-tech',
            label: 'Advanced / Calibration / Technical Details',
            children: technical,
          }]}
        />
      ) : (
        technical
      )}
      {returnToScene && onReturnToScene ? (
        <p className="fx-desk__note">
          Đang xử lý nhân vật cho {returnToScene.sceneLabel}.{' '}
          <Button type="link" onClick={onReturnToScene}>
            Quay lại {returnToScene.sceneLabel}
          </Button>
        </p>
      ) : null}
      <ContentFamixaCharacterStudio
        onBuildEpisode={onBuildEpisode}
        onBibleReady={setBibleReady}
        focusCharacterId={focusCharacterId}
        returnToScene={returnToScene}
        onReturnToScene={onReturnToScene}
        voices={voices}
        voiceById={voiceById}
        onAssignVoice={onAssignVoice}
      />
      <p className="fx-desk__note">
        Bộ ảnh chuẩn đã khóa không bị ghi đè. Ảnh production Shot 01 vẫn chờ duyệt ở card bên dưới — Studio không tạo shot.
      </p>
      {director ? null : <ContentKitVideoImageGenerationExecutionCard />}
    </section>
  );
}

export function ContentFamixaFinishBoard({
  episodeTitle,
  episodeCode,
  done,
  onOpenShot,
  firstShotId,
}: {
  episodeTitle?: string;
  episodeCode: string;
  done: boolean;
  onOpenShot: (id: string) => void;
  firstShotId?: string;
}) {
  const { rows } = useLiveProduction();
  const first = rows[0];
  return (
    <section className="fx-desk">
      <h2>Hoàn thiện tập phim</h2>
      <p className="fx-desk__lead">
        {episodeTitle || 'Tập đang sản xuất'} · {episodeCode}
      </p>
      {done ? (
        <>
          <p>✓ {rows.length}/{rows.length} cảnh hoàn thành</p>
          {first?.clip ? <video src={first.clip} controls playsInline className="fx-media-card__clip" /> : null}
          <ul className="fx-finish__check">
            <li>✓ Đủ cảnh</li>
            <li>✓ Đúng thứ tự</li>
            <li>✓ Hình ảnh nhất quán</li>
            <li>✓ Nhân vật nhất quán</li>
            <li>✓ Video hoàn chỉnh</li>
          </ul>
          {firstShotId ? (
            <Button type="primary" onClick={() => onOpenShot(firstShotId)}>
              Xem video
            </Button>
          ) : null}
          {first?.clip ? (
            <p className="fx-dir-ws__dl">
              <a href={first.clip} download={`${episodeCode}.mp4`}>
                Xuất video
              </a>
            </p>
          ) : null}
        </>
      ) : (
        <>
          <p>Hoàn thiện khi mọi cảnh đã duyệt video.</p>
          <ul className="fx-finish__check">
            <li>○ Âm thanh — chuẩn bị triển khai</li>
            <li>○ Nhạc — chuẩn bị triển khai</li>
            <li>○ Lời thoại / phụ đề — chuẩn bị triển khai</li>
            <li>○ Ghép cảnh — chuẩn bị triển khai</li>
          </ul>
        </>
      )}
    </section>
  );
}
